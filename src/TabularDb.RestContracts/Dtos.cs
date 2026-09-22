namespace TabularDb.RestContracts;

public sealed record DatabaseDto(string Name, bool IsLoaded, bool IsModified);

public sealed record DatabaseListDto(IReadOnlyList<DatabaseDto> Databases, IReadOnlyList<string> Types);

public sealed record CreateDatabaseRequest(string? Name);

public sealed record TableListDto(IReadOnlyList<string> Tables, bool IsModified);

public sealed record FieldDto(string? Name, string? Type);

public sealed record TableSchemaDto(string Name, IReadOnlyList<FieldDto> Fields);

public sealed record CreateTableRequest(string? Name, IReadOnlyList<FieldDto>? Fields);

public sealed record RowDto(long Id, IReadOnlyList<string?> Values);

public sealed record RowPageDto(IReadOnlyList<RowDto> Rows, int Total, int Skip, int Take);

public sealed record RowValuesRequest(IReadOnlyList<string?>? Values);

public sealed record DedupResultDto(int RemovedCount, IReadOnlyList<RowDto> RemovedRows, bool DryRun);

public static class ApiRoutes
{
    public const int DefaultPageSize = 100;

    public static string Esc(string value) => Uri.EscapeDataString(value);

    public static string Databases => "api/databases";
    public static string Import => "api/databases/import";
    public static string Database(string db) => $"api/databases/{Esc(db)}";
    public static string Save(string db) => $"{Database(db)}/save";
    public static string Load(string db) => $"{Database(db)}/load";
    public static string Export(string db) => $"{Database(db)}/export";
    public static string Tables(string db) => $"{Database(db)}/tables";
    public static string Table(string db, string table) => $"{Tables(db)}/{Esc(table)}";
    public static string Rows(string db, string table) => $"{Table(db, table)}/rows";
    public static string Row(string db, string table, long id) => $"{Rows(db, table)}/{id}";
    public static string RemoveDuplicates(string db, string table, bool dryRun) =>
        $"{Table(db, table)}/remove-duplicates?dryRun={(dryRun ? "true" : "false")}";
}
