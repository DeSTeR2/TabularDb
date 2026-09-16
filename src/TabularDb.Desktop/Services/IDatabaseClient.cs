namespace TabularDb.Desktop.Services;

public interface IDatabaseClient : IDisposable
{
    string ServerUrl { get; }

    Task<DatabaseListing> ListDatabasesAsync();
    Task CreateDatabaseAsync(string db);
    Task DeleteDatabaseAsync(string db);
    Task SaveDatabaseAsync(string db);
    Task LoadDatabaseAsync(string db);
    Task<ExportedFile> ExportDatabaseAsync(string db);
    Task ImportDatabaseAsync(string db, string content, bool overwrite);

    Task<TableListing> ListTablesAsync(string db);
    Task<TableSchema> GetSchemaAsync(string db, string table);
    Task CreateTableAsync(string db, string table, IReadOnlyList<FieldItem> fields);
    Task DropTableAsync(string db, string table);

    Task<RowsPage> GetRowsAsync(string db, string table, int skip, int take);
    Task<RowItem> AddRowAsync(string db, string table, IReadOnlyList<string?> values);
    Task<RowItem> UpdateRowAsync(string db, string table, long id, IReadOnlyList<string?> values);
    Task DeleteRowAsync(string db, string table, long id);

    Task<DedupOutcome> RemoveDuplicatesAsync(string db, string table, bool dryRun);
}
