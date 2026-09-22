using System.Text;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TabularDb.Contracts.Grpc;

namespace TabularDb.Integration.Tests;

public sealed class GrpcServerTests : IDisposable
{
    private const string ApiKey = "test-key";
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "tdb-grpc-" + Guid.NewGuid().ToString("N"));
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var d in _disposables)
            d.Dispose();
        if (Directory.Exists(_dataDir))
            Directory.Delete(_dataDir, recursive: true);
    }

    private TabularDbService.TabularDbServiceClient CreateClient(out Metadata headers, string? key = ApiKey)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:DataDir", _dataDir);
            builder.UseSetting("Security:ApiKey", ApiKey);
        });
        var channel = GrpcChannel.ForAddress(factory.Server.BaseAddress, new GrpcChannelOptions
        {
            HttpHandler = factory.Server.CreateHandler(),
        });
        _disposables.Add(channel);
        _disposables.Add(factory);
        headers = [];
        if (key is not null)
            headers.Add("x-api-key", key);
        return new TabularDbService.TabularDbServiceClient(channel);
    }

    private static AddRowRequest Row(string db, string table, params string?[] values)
    {
        var request = new AddRowRequest { DbName = db, TableName = table };
        request.Cells.AddRange(values.Select(v => v is null ? new Cell() : new Cell { Value = v }));
        return request;
    }

    private static async Task CreateShiftsAsync(TabularDbService.TabularDbServiceClient client, Metadata h)
    {
        await client.CreateDatabaseAsync(new DbRef { DbName = "Shop" }, h);
        var create = new CreateTableRequest { DbName = "Shop", TableName = "Shifts" };
        create.Fields.Add(new FieldMsg { Name = "Worker", Type = "string" });
        create.Fields.Add(new FieldMsg { Name = "Shift", Type = "timeInvl" });
        await client.CreateTableAsync(create, h);
    }

    [Fact]
    public async Task Scenario_CreateAddRemoveDuplicates()
    {
        var client = CreateClient(out var h);
        await CreateShiftsAsync(client, h);

        await client.AddRowAsync(Row("Shop", "Shifts", "Ivan", "9:00-17:30"), h);
        await client.AddRowAsync(Row("Shop", "Shifts", "Ivan", "09:00:00-17:30:00"), h);
        await client.AddRowAsync(Row("Shop", "Shifts", "Olha", null), h);

        var preview = await client.RemoveDuplicatesAsync(new DedupRequest { DbName = "Shop", TableName = "Shifts", DryRun = true }, h);
        Assert.Equal(1, preview.RemovedCount);
        Assert.Equal(2, preview.RemovedRows[0].Id);

        var unchanged = await client.GetRowsAsync(new GetRowsRequest { DbName = "Shop", TableName = "Shifts", Take = 10 }, h);
        Assert.Equal(3, unchanged.Total);

        var result = await client.RemoveDuplicatesAsync(new DedupRequest { DbName = "Shop", TableName = "Shifts" }, h);
        Assert.Equal(1, result.RemovedCount);

        var page = await client.GetRowsAsync(new GetRowsRequest { DbName = "Shop", TableName = "Shifts", Take = 10 }, h);
        Assert.Equal(2, page.Total);
        Assert.Equal([1L, 3L], page.Rows.Select(r => r.Id));
        Assert.Equal("09:00:00-17:30:00", page.Rows[0].Cells[1].Value);
        Assert.False(page.Rows[1].Cells[1].HasValue);

        var tables = await client.ListTablesAsync(new DbRef { DbName = "Shop" }, h);
        Assert.True(tables.IsModified);
    }

    [Fact]
    public async Task InvalidRow_ReturnsInvalidArgumentWithDetails()
    {
        var client = CreateClient(out var h);
        await CreateShiftsAsync(client, h);

        var ex = await Assert.ThrowsAsync<RpcException>(async () =>
            await client.AddRowAsync(Row("Shop", "Shifts", "Ivan", "17:00-09:00"), h));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
        var errors = ex.Trailers.Where(t => t.Key == "validation-errors-bin").Select(t => Encoding.UTF8.GetString(t.ValueBytes)).ToList();
        Assert.Contains(errors, e => e.StartsWith("Поле 'Shift'"));
    }

    [Fact]
    public async Task InvalidSchema_ReturnsAllErrors()
    {
        var client = CreateClient(out var h);
        await client.CreateDatabaseAsync(new DbRef { DbName = "Shop" }, h);
        var create = new CreateTableRequest { DbName = "Shop", TableName = "" };
        create.Fields.Add(new FieldMsg { Name = "A", Type = "money" });
        create.Fields.Add(new FieldMsg { Name = "a", Type = "time" });

        var ex = await Assert.ThrowsAsync<RpcException>(async () => await client.CreateTableAsync(create, h));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
        Assert.Equal(3, ex.Trailers.Count(t => t.Key == "validation-errors-bin"));
    }

    [Fact]
    public async Task MissingApiKey_IsRejected()
    {
        var client = CreateClient(out var h, key: null);

        var ex = await Assert.ThrowsAsync<RpcException>(async () => await client.ListDatabasesAsync(new Empty(), h));

        Assert.Equal(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [Fact]
    public async Task Errors_MapToStatusCodes()
    {
        var client = CreateClient(out var h);
        await CreateShiftsAsync(client, h);

        async Task<StatusCode> CodeOf(Func<Task> call) => (await Assert.ThrowsAsync<RpcException>(call)).StatusCode;

        Assert.Equal(StatusCode.AlreadyExists, await CodeOf(async () => await client.CreateDatabaseAsync(new DbRef { DbName = "shop" }, h)));
        Assert.Equal(StatusCode.NotFound, await CodeOf(async () => await client.ListTablesAsync(new DbRef { DbName = "Nope" }, h)));
        Assert.Equal(StatusCode.NotFound, await CodeOf(async () => await client.DeleteRowAsync(new RowRef { DbName = "Shop", TableName = "Shifts", Id = 99 }, h)));
        Assert.Equal(StatusCode.InvalidArgument, await CodeOf(async () => await client.CreateDatabaseAsync(new DbRef { DbName = "../evil" }, h)));
        Assert.Equal(StatusCode.NotFound, await CodeOf(async () => await client.LoadDatabaseAsync(new DbRef { DbName = "Shop" }, h)));
    }

    [Fact]
    public async Task SaveAndLoad_SurviveServerRestart()
    {
        var client = CreateClient(out var h);
        await CreateShiftsAsync(client, h);
        await client.AddRowAsync(Row("Shop", "Shifts", "Ivan", "08:00-12:00"), h);
        await client.SaveDatabaseAsync(new DbRef { DbName = "Shop" }, h);

        var restarted = CreateClient(out h);
        var list = await restarted.ListDatabasesAsync(new Empty(), h);
        var info = Assert.Single(list.Databases);
        Assert.Equal("Shop", info.Name);
        Assert.Contains("timeInvl", list.Types_);

        var page = await restarted.GetRowsAsync(new GetRowsRequest { DbName = "Shop", TableName = "Shifts" }, h);
        Assert.Equal("08:00:00-12:00:00", Assert.Single(page.Rows).Cells[1].Value);

        await restarted.AddRowAsync(Row("Shop", "Shifts", "Olha", null), h);
        await restarted.LoadDatabaseAsync(new DbRef { DbName = "Shop" }, h);
        page = await restarted.GetRowsAsync(new GetRowsRequest { DbName = "Shop", TableName = "Shifts" }, h);
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task ExportImport_CreatesCopy()
    {
        var client = CreateClient(out var h);
        await CreateShiftsAsync(client, h);
        await client.AddRowAsync(Row("Shop", "Shifts", "Ivan", "08:00-12:00"), h);

        var file = await client.ExportDatabaseAsync(new DbRef { DbName = "Shop" }, h);
        Assert.Equal("Shop.tdb.json", file.FileName);

        await client.ImportDatabaseAsync(new ImportRequest { DbName = "Copy", Content = file.Content }, h);
        var ex = await Assert.ThrowsAsync<RpcException>(async () =>
            await client.ImportDatabaseAsync(new ImportRequest { DbName = "Copy", Content = file.Content }, h));
        Assert.Equal(StatusCode.AlreadyExists, ex.StatusCode);

        var bad = await Assert.ThrowsAsync<RpcException>(async () =>
            await client.ImportDatabaseAsync(new ImportRequest { DbName = "Bad", Content = "{" }, h));
        Assert.Equal(StatusCode.FailedPrecondition, bad.StatusCode);

        var page = await client.GetRowsAsync(new GetRowsRequest { DbName = "Copy", TableName = "Shifts" }, h);
        Assert.Equal(1, page.Total);
        Assert.True(File.Exists(Path.Combine(_dataDir, "Copy.tdb.json")));

        await client.DeleteDatabaseAsync(new DbRef { DbName = "Copy" }, h);
        Assert.False(File.Exists(Path.Combine(_dataDir, "Copy.tdb.json")));
    }

    [Fact]
    public async Task UpdateAndDeleteRow_AndDropTable()
    {
        var client = CreateClient(out var h);
        await CreateShiftsAsync(client, h);
        var row = await client.AddRowAsync(Row("Shop", "Shifts", "Ivan", "08:00-12:00"), h);

        var update = new UpdateRowRequest { DbName = "Shop", TableName = "Shifts", Id = row.Id };
        update.Cells.Add(new Cell { Value = "Petro" });
        update.Cells.Add(new Cell { Value = "10:00-11:00" });
        var updated = await client.UpdateRowAsync(update, h);
        Assert.Equal("Petro", updated.Cells[0].Value);

        await client.DeleteRowAsync(new RowRef { DbName = "Shop", TableName = "Shifts", Id = row.Id }, h);
        var page = await client.GetRowsAsync(new GetRowsRequest { DbName = "Shop", TableName = "Shifts" }, h);
        Assert.Equal(0, page.Total);

        await client.DropTableAsync(new TableRef { DbName = "Shop", TableName = "shifts" }, h);
        var tables = await client.ListTablesAsync(new DbRef { DbName = "Shop" }, h);
        Assert.Empty(tables.Tables);
    }
}
