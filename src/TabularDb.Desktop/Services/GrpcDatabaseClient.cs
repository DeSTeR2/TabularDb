using System.Net;
using System.Text;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using TabularDb.Contracts.Grpc;

namespace TabularDb.Desktop.Services;

public sealed class GrpcDatabaseClient : IDatabaseClient
{
    private const string ErrorsTrailer = "validation-errors-bin";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly GrpcChannel _channel;
    private readonly TabularDbService.TabularDbServiceClient _client;
    private readonly Metadata _headers = [];

    public string ServerUrl { get; }

    public GrpcDatabaseClient(ClientSettings settings)
    {
        ServerUrl = settings.ServerUrl.Trim();
        var options = new GrpcChannelOptions { MaxReceiveMessageSize = null, MaxSendMessageSize = null };
        if (settings.UseGrpcWeb)
        {
            options.HttpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, new SocketsHttpHandler());
            options.HttpVersion = HttpVersion.Version11;
            options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        }
        else
        {
            options.HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true };
        }
        _channel = GrpcChannel.ForAddress(ServerUrl, options);
        _client = new TabularDbService.TabularDbServiceClient(_channel);
        if (!string.IsNullOrEmpty(settings.ApiKey))
            _headers.Add("x-api-key", settings.ApiKey);
    }

    private CallOptions Options => new(_headers, DateTime.UtcNow.Add(Timeout));

    public Task<DatabaseListing> ListDatabasesAsync() => Call(async () =>
    {
        var reply = await _client.ListDatabasesAsync(new Empty(), Options);
        return new DatabaseListing(
            reply.Databases.Select(d => new DatabaseItem(d.Name, d.IsLoaded, d.IsModified)).ToList(),
            reply.Types_.ToList());
    });

    public Task CreateDatabaseAsync(string db) =>
        Call(async () => await _client.CreateDatabaseAsync(new DbRef { DbName = db }, Options));

    public Task DeleteDatabaseAsync(string db) =>
        Call(async () => await _client.DeleteDatabaseAsync(new DbRef { DbName = db }, Options));

    public Task SaveDatabaseAsync(string db) =>
        Call(async () => await _client.SaveDatabaseAsync(new DbRef { DbName = db }, Options));

    public Task LoadDatabaseAsync(string db) =>
        Call(async () => await _client.LoadDatabaseAsync(new DbRef { DbName = db }, Options));

    public Task<ExportedFile> ExportDatabaseAsync(string db) => Call(async () =>
    {
        var reply = await _client.ExportDatabaseAsync(new DbRef { DbName = db }, Options);
        return new ExportedFile(reply.FileName, reply.Content);
    });

    public Task ImportDatabaseAsync(string db, string content, bool overwrite) =>
        Call(async () => await _client.ImportDatabaseAsync(
            new ImportRequest { DbName = db, Content = content, Overwrite = overwrite }, Options));

    public Task<TableListing> ListTablesAsync(string db) => Call(async () =>
    {
        var reply = await _client.ListTablesAsync(new DbRef { DbName = db }, Options);
        return new TableListing(reply.Tables.ToList(), reply.IsModified);
    });

    public Task<TableSchema> GetSchemaAsync(string db, string table) => Call(async () =>
    {
        var reply = await _client.GetSchemaAsync(new TableRef { DbName = db, TableName = table }, Options);
        return new TableSchema(reply.TableName, reply.Fields.Select(f => new FieldItem(f.Name, f.Type)).ToList());
    });

    public Task CreateTableAsync(string db, string table, IReadOnlyList<FieldItem> fields) => Call(async () =>
    {
        var request = new CreateTableRequest { DbName = db, TableName = table };
        request.Fields.AddRange(fields.Select(f => new FieldMsg { Name = f.Name, Type = f.Type }));
        return await _client.CreateTableAsync(request, Options);
    });

    public Task DropTableAsync(string db, string table) =>
        Call(async () => await _client.DropTableAsync(new TableRef { DbName = db, TableName = table }, Options));

    public Task<RowsPage> GetRowsAsync(string db, string table, int skip, int take) => Call(async () =>
    {
        var reply = await _client.GetRowsAsync(
            new GetRowsRequest { DbName = db, TableName = table, Skip = skip, Take = take }, Options);
        return new RowsPage(reply.Rows.Select(FromMessage).ToList(), reply.Total);
    });

    public Task<RowItem> AddRowAsync(string db, string table, IReadOnlyList<string?> values) => Call(async () =>
    {
        var request = new AddRowRequest { DbName = db, TableName = table };
        request.Cells.AddRange(ToCells(values));
        return FromMessage(await _client.AddRowAsync(request, Options));
    });

    public Task<RowItem> UpdateRowAsync(string db, string table, long id, IReadOnlyList<string?> values) => Call(async () =>
    {
        var request = new UpdateRowRequest { DbName = db, TableName = table, Id = id };
        request.Cells.AddRange(ToCells(values));
        return FromMessage(await _client.UpdateRowAsync(request, Options));
    });

    public Task DeleteRowAsync(string db, string table, long id) =>
        Call(async () => await _client.DeleteRowAsync(new RowRef { DbName = db, TableName = table, Id = id }, Options));

    public Task<DedupOutcome> RemoveDuplicatesAsync(string db, string table, bool dryRun) => Call(async () =>
    {
        var reply = await _client.RemoveDuplicatesAsync(
            new DedupRequest { DbName = db, TableName = table, DryRun = dryRun }, Options);
        return new DedupOutcome(reply.RemovedCount, reply.RemovedRows.Select(FromMessage).ToList());
    });

    public void Dispose() => _channel.Dispose();

    private static IEnumerable<Cell> ToCells(IEnumerable<string?> values) =>
        values.Select(v => v is null ? new Cell() : new Cell { Value = v });

    private static RowItem FromMessage(RowMsg row) =>
        new(row.Id, row.Cells.Select(c => c.HasValue ? c.Value : null).ToList());

    private static async Task<T> Call<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (RpcException ex)
        {
            throw Translate(ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ClientException($"Сервер недоступний: {ex.Message}", isUnavailable: true);
        }
    }

    private static ClientException Translate(RpcException ex)
    {
        var errors = ex.Trailers
            .Where(e => e.Key == ErrorsTrailer && e.IsBinary)
            .Select(e => Encoding.UTF8.GetString(e.ValueBytes))
            .ToList();
        return ex.StatusCode switch
        {
            StatusCode.Unavailable => new ClientException("Сервер недоступний", isUnavailable: true),
            StatusCode.Internal when ex.Status.DebugException is HttpRequestException or IOException =>
                new ClientException($"Не вдалося з'єднатися з сервером: {ex.Status.DebugException!.Message}", isUnavailable: true),
            StatusCode.DeadlineExceeded => new ClientException(
                "Сервер не відповів вчасно. Оновіть дані, щоб перевірити, чи виконалась операція."),
            StatusCode.ResourceExhausted => new ClientException("Дані завеликі для передачі"),
            StatusCode.Unauthenticated => new ClientException("Сервер відхилив API-ключ"),
            StatusCode.Internal => new ClientException("Внутрішня помилка сервера"),
            _ when errors.Count > 0 => new ClientException(ex.Status.Detail, errors),
            _ => new ClientException(ex.Status.Detail),
        };
    }
}
