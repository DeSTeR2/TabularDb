using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TabularDb.RestContracts;

namespace TabularDb.WebApi.Tests;

public sealed class RestApiTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "tdb-rest-" + Guid.NewGuid().ToString("N"));
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var d in _disposables)
            d.Dispose();
        if (Directory.Exists(_dataDir))
            Directory.Delete(_dataDir, recursive: true);
    }

    private HttpClient CreateClient()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Storage:DataDir", _dataDir));
        _disposables.Add(factory);
        return factory.CreateClient();
    }

    private static async Task CreateShiftsAsync(HttpClient http)
    {
        (await http.PostAsJsonAsync(ApiRoutes.Databases, new CreateDatabaseRequest("Shop"))).EnsureSuccessStatusCode();
        var response = await http.PostAsJsonAsync(ApiRoutes.Tables("Shop"),
            new CreateTableRequest("Shifts", [new("Worker", "string"), new("Shift", "timeInvl")]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static Task<HttpResponseMessage> AddRowAsync(HttpClient http, params string?[] values) =>
        http.PostAsJsonAsync(ApiRoutes.Rows("Shop", "Shifts"), new RowValuesRequest(values));

    private static async Task<List<string>> ErrorsOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("errors").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()!)).ToList();
    }

    [Fact]
    public async Task Scenario_CrudAndRemoveDuplicates()
    {
        var http = CreateClient();
        await CreateShiftsAsync(http);

        var created = await AddRowAsync(http, "Ivan", "9:00-17:30");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("/api/databases/Shop/tables/Shifts/rows/1", created.Headers.Location!.OriginalString);
        (await AddRowAsync(http, "Ivan", "09:00:00-17:30:00")).EnsureSuccessStatusCode();
        (await AddRowAsync(http, "Olha", null)).EnsureSuccessStatusCode();

        var preview = await (await http.PostAsync(ApiRoutes.RemoveDuplicates("Shop", "Shifts", true), null))
            .Content.ReadFromJsonAsync<DedupResultDto>();
        Assert.Equal(1, preview!.RemovedCount);
        Assert.True(preview.DryRun);
        Assert.Equal(2, Assert.Single(preview.RemovedRows).Id);

        var page = await http.GetFromJsonAsync<RowPageDto>(ApiRoutes.Rows("Shop", "Shifts"));
        Assert.Equal(3, page!.Total);

        var result = await (await http.PostAsync(ApiRoutes.RemoveDuplicates("Shop", "Shifts", false), null))
            .Content.ReadFromJsonAsync<DedupResultDto>();
        Assert.Equal(1, result!.RemovedCount);

        page = await http.GetFromJsonAsync<RowPageDto>(ApiRoutes.Rows("Shop", "Shifts") + "?skip=0&take=10");
        Assert.Equal([1L, 3L], page!.Rows.Select(r => r.Id));
        Assert.Equal("09:00:00-17:30:00", page.Rows[0].Values[1]);
        Assert.Null(page.Rows[1].Values[1]);

        var updated = await http.PutAsJsonAsync(ApiRoutes.Row("Shop", "Shifts", 3), new RowValuesRequest(["Olha", "10:00-11:00"]));
        Assert.Equal("10:00:00-11:00:00", (await updated.Content.ReadFromJsonAsync<RowDto>())!.Values[1]);

        Assert.Equal(HttpStatusCode.NoContent, (await http.DeleteAsync(ApiRoutes.Row("Shop", "Shifts", 1))).StatusCode);
        var tables = await http.GetFromJsonAsync<TableListDto>(ApiRoutes.Tables("Shop"));
        Assert.True(tables!.IsModified);

        var schema = await http.GetFromJsonAsync<TableSchemaDto>(ApiRoutes.Table("Shop", "shifts"));
        Assert.Equal(["string", "timeInvl"], schema!.Fields.Select(f => f.Type));
    }

    [Fact]
    public async Task InvalidRow_Returns400WithFieldErrors()
    {
        var http = CreateClient();
        await CreateShiftsAsync(http);

        var response = await AddRowAsync(http, "Ivan", "17:00-09:00");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(await ErrorsOf(response), e => e.StartsWith("Поле 'Shift'"));
    }

    [Fact]
    public async Task InvalidSchema_Returns400WithAllErrors()
    {
        var http = CreateClient();
        await CreateShiftsAsync(http);

        var response = await http.PostAsJsonAsync(ApiRoutes.Tables("Shop"),
            new CreateTableRequest("shifts", [new("A", "money"), new("a", "time"), new("", null)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(5, (await ErrorsOf(response)).Count);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("a/b")]
    public async Task UnsafeTableName_Returns400(string name)
    {
        var http = CreateClient();
        await CreateShiftsAsync(http);

        var response = await http.PostAsJsonAsync(ApiRoutes.Tables("Shop"),
            new CreateTableRequest(name, [new("A", "time")]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull((await http.GetFromJsonAsync<DatabaseListDto>(ApiRoutes.Databases))!.Databases.Single());
    }

    [Fact]
    public async Task Import_WithoutNameOrFile_ReturnsLocalizedErrors()
    {
        var http = CreateClient();

        using var noName = new MultipartFormDataContent
        {
            { new ByteArrayContent("{}"u8.ToArray()), "file", "db.tdb.json" },
        };
        var response = await http.PostAsync(ApiRoutes.Import, noName);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(await ErrorsOf(response), e => e.StartsWith("Назва бази"));

        using var noFile = new MultipartFormDataContent { { new StringContent("Shop"), "name" } };
        response = await http.PostAsync(ApiRoutes.Import, noFile);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Не вибрано файл для імпорту", await ErrorsOf(response));
    }

    [Fact]
    public async Task StatusCodes_AreMapped()
    {
        var http = CreateClient();
        await CreateShiftsAsync(http);

        Assert.Equal(HttpStatusCode.Conflict,
            (await http.PostAsJsonAsync(ApiRoutes.Databases, new CreateDatabaseRequest("shop"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await http.PostAsJsonAsync(ApiRoutes.Databases, new CreateDatabaseRequest("../x"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(ApiRoutes.Tables("Nope"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(ApiRoutes.Table("Shop", "Nope"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.DeleteAsync(ApiRoutes.Row("Shop", "Shifts", 42))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.PostAsync(ApiRoutes.Load("Shop"), null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await http.DeleteAsync(ApiRoutes.Table("Shop", "Shifts"))).StatusCode);
    }

    [Fact]
    public async Task SaveExportImport_RoundTrip()
    {
        var http = CreateClient();
        await CreateShiftsAsync(http);
        (await AddRowAsync(http, "Ivan", "08:00-12:00")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await http.PostAsync(ApiRoutes.Save("Shop"), null)).StatusCode);

        var restarted = CreateClient();
        var list = await restarted.GetFromJsonAsync<DatabaseListDto>(ApiRoutes.Databases);
        Assert.Equal("Shop", Assert.Single(list!.Databases).Name);
        Assert.Contains("time", list.Types);

        var export = await restarted.GetAsync(ApiRoutes.Export("Shop"));
        Assert.Equal("application/json", export.Content.Headers.ContentType!.MediaType);
        Assert.Equal("Shop.tdb.json", export.Content.Headers.ContentDisposition!.FileName?.Trim('"'));
        var json = await export.Content.ReadAsStringAsync();

        async Task<HttpResponseMessage> Import(string name, string content, bool overwrite)
        {
            using var form = new MultipartFormDataContent
            {
                { new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "file", "db.tdb.json" },
                { new StringContent(name), "name" },
                { new StringContent(overwrite ? "true" : "false"), "overwrite" },
            };
            return await restarted.PostAsync(ApiRoutes.Import, form);
        }

        Assert.Equal(HttpStatusCode.NoContent, (await Import("Copy", json, false)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Import("Copy", json, false)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Import("Copy", json, true)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Import("Bad", "{", false)).StatusCode);

        var page = await restarted.GetFromJsonAsync<RowPageDto>(ApiRoutes.Rows("Copy", "Shifts"));
        Assert.Equal("08:00:00-12:00:00", Assert.Single(page!.Rows).Values[1]);

        Assert.Equal(HttpStatusCode.NoContent, (await restarted.DeleteAsync(ApiRoutes.Database("Copy"))).StatusCode);
        Assert.False(File.Exists(Path.Combine(_dataDir, "Copy.tdb.json")));
    }

    [Fact]
    public async Task Frontend_IsServed()
    {
        var http = CreateClient();

        var home = await http.GetAsync("/");
        var page = await http.GetAsync("/db/Shop/t/Shifts");

        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("blazor.web", await home.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var missing = await http.GetAsync("/foo/bar");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("blazor.web", await missing.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/nothing")).StatusCode);
    }
}
