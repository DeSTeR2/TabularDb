using TabularDb.Desktop.Services;

namespace TabularDb.Desktop.Controllers;

public sealed class RemoveDuplicatesController(IDatabaseClient client, string db)
{
    public async Task<IReadOnlyList<string>> GetTableNamesAsync() =>
        (await client.ListTablesAsync(db)).Tables;

    public Task<TableSchema> GetSchemaAsync(string table) => client.GetSchemaAsync(db, table);

    public Task<DedupOutcome> PreviewAsync(string table) => client.RemoveDuplicatesAsync(db, table, dryRun: true);

    public Task<DedupOutcome> RemoveDuplicatesAsync(string table) => client.RemoveDuplicatesAsync(db, table, dryRun: false);
}
