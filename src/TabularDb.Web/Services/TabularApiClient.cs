using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TabularDb.RestContracts;

namespace TabularDb.Web.Services;

public sealed class ApiException(HttpStatusCode? status, string message, IReadOnlyList<string>? errors = null)
    : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
    public IReadOnlyList<string> Errors { get; } = errors ?? [];
}

public sealed class TabularApiClient(HttpClient http)
{
    public const long MaxImportSize = 60 * 1024 * 1024;

    public string ExportUrl(string db) => new Uri(http.BaseAddress!, ApiRoutes.Export(db)).ToString();

    public Task<DatabaseListDto> ListDatabasesAsync() => GetAsync<DatabaseListDto>(ApiRoutes.Databases);

    public Task CreateDatabaseAsync(string name) =>
        SendAsync(HttpMethod.Post, ApiRoutes.Databases, new CreateDatabaseRequest(name));

    public Task DeleteDatabaseAsync(string db) => SendAsync(HttpMethod.Delete, ApiRoutes.Database(db));

    public Task SaveDatabaseAsync(string db) => SendAsync(HttpMethod.Post, ApiRoutes.Save(db));

    public Task LoadDatabaseAsync(string db) => SendAsync(HttpMethod.Post, ApiRoutes.Load(db));

    public async Task ImportDatabaseAsync(string name, Stream content, string fileName, bool overwrite)
    {
        using var form = new MultipartFormDataContent();
        var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        form.Add(file, "file", fileName);
        form.Add(new StringContent(name), "name");
        form.Add(new StringContent(overwrite ? "true" : "false"), "overwrite");
        await ReadAsync<object>(() => http.PostAsync(ApiRoutes.Import, form), expectBody: false);
    }

    public Task<TableListDto> ListTablesAsync(string db) => GetAsync<TableListDto>(ApiRoutes.Tables(db));

    public Task<TableSchemaDto> GetSchemaAsync(string db, string table) =>
        GetAsync<TableSchemaDto>(ApiRoutes.Table(db, table));

    public Task<TableSchemaDto> CreateTableAsync(string db, CreateTableRequest request) =>
        SendAsync<TableSchemaDto>(HttpMethod.Post, ApiRoutes.Tables(db), request);

    public Task DropTableAsync(string db, string table) => SendAsync(HttpMethod.Delete, ApiRoutes.Table(db, table));

    public Task<RowPageDto> GetRowsAsync(string db, string table, int skip, int take) =>
        GetAsync<RowPageDto>($"{ApiRoutes.Rows(db, table)}?skip={skip}&take={take}");

    public Task<RowDto> AddRowAsync(string db, string table, IReadOnlyList<string?> values) =>
        SendAsync<RowDto>(HttpMethod.Post, ApiRoutes.Rows(db, table), new RowValuesRequest(values));

    public Task<RowDto> UpdateRowAsync(string db, string table, long id, IReadOnlyList<string?> values) =>
        SendAsync<RowDto>(HttpMethod.Put, ApiRoutes.Row(db, table, id), new RowValuesRequest(values));

    public Task DeleteRowAsync(string db, string table, long id) =>
        SendAsync(HttpMethod.Delete, ApiRoutes.Row(db, table, id));

    public Task<DedupResultDto> RemoveDuplicatesAsync(string db, string table, bool dryRun) =>
        SendAsync<DedupResultDto>(HttpMethod.Post, ApiRoutes.RemoveDuplicates(db, table, dryRun));

    private Task<T> GetAsync<T>(string url) => ReadAsync<T>(() => http.GetAsync(url), expectBody: true)!;

    private Task SendAsync(HttpMethod method, string url, object? body = null) =>
        ReadAsync<object>(() => http.SendAsync(Build(method, url, body)), expectBody: false);

    private Task<T> SendAsync<T>(HttpMethod method, string url, object? body = null) =>
        ReadAsync<T>(() => http.SendAsync(Build(method, url, body)), expectBody: true)!;

    private static HttpRequestMessage Build(HttpMethod method, string url, object? body) =>
        new(method, url) { Content = body is null ? null : JsonContent.Create(body) };

    private static async Task<T?> ReadAsync<T>(Func<Task<HttpResponseMessage>> send, bool expectBody)
    {
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(null, $"Сервер недоступний: {ex.Message}");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw await ToExceptionAsync(response);
            if (!expectBody)
                return default;
            return await response.Content.ReadFromJsonAsync<T>()
                ?? throw new ApiException(response.StatusCode, "Сервер повернув порожню відповідь");
        }
    }

    private static async Task<ApiException> ToExceptionAsync(HttpResponseMessage response)
    {
        var message = $"Помилка {(int)response.StatusCode}";
        var errors = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                message = title.GetString()!;
            if (doc.RootElement.TryGetProperty("errors", out var dict) && dict.ValueKind == JsonValueKind.Object)
                foreach (var entry in dict.EnumerateObject())
                    if (entry.Value.ValueKind == JsonValueKind.Array)
                        errors.AddRange(entry.Value.EnumerateArray().Select(e => e.GetString() ?? ""));
        }
        catch (JsonException)
        {
        }
        return new ApiException(response.StatusCode, message, errors);
    }
}
