using TabularDb.Core.Model;
using TabularDb.Core.Operations;
using TabularDb.Core.Types;
using TabularDb.Core.Validation;
using TabularDb.Storage;

namespace TabularDb.Server.Application;

public sealed class DatabaseAppService(DatabaseStore store)
{
    public const int MaxPageSize = 1000;

    public IReadOnlyList<string> SupportedTypes => TypeRegistry.Names;

    public IReadOnlyList<DatabaseSummary> ListDatabases() => store.List();

    public void CreateDatabase(string dbName) => store.Create(dbName);

    public void DeleteDatabase(string dbName) => store.Delete(dbName);

    public void SaveDatabase(string dbName) => store.Save(dbName);

    public void LoadDatabase(string dbName) => store.Reload(dbName);

    public (string FileName, string Json) ExportDatabase(string dbName) =>
        WithDatabase(dbName, db => (db.Name + JsonStorage.Extension, DatabaseSerializer.ToJson(db)));

    public void ImportDatabase(string dbName, string json, bool overwrite) =>
        store.Import(dbName, json, overwrite);

    public TableListData ListTables(string dbName) =>
        WithDatabase(dbName, db => new TableListData(db.TableNames, db.IsModified));

    public TableSchemaInfo GetSchema(string dbName, string tableName) =>
        WithDatabase(dbName, db => ToSchemaInfo(db.GetTable(tableName)));

    public TableSchemaInfo CreateTable(string dbName, string tableName, IReadOnlyList<FieldInfo> fields)
    {
        var definitions = fields.Select(f => new FieldDefinition(f.Name, f.Type)).ToList();
        return WithDatabase(dbName, db =>
        {
            SchemaValidator.Validate(db, tableName, definitions).ThrowIfInvalid();
            return ToSchemaInfo(db.CreateTable(tableName, SchemaValidator.ToFields(definitions)));
        });
    }

    public void DropTable(string dbName, string tableName) =>
        WithDatabase(dbName, db =>
        {
            db.DropTable(tableName);
            return true;
        });

    public RowPageData GetRows(string dbName, string tableName, int skip, int take)
    {
        if (take <= 0 || take > MaxPageSize)
            take = MaxPageSize;
        return WithDatabase(dbName, db =>
        {
            var table = db.GetTable(tableName);
            var rows = table.GetRows(skip, take).Select(r => ToRowData(table, r)).ToList();
            return new RowPageData(rows, table.Rows.Count);
        });
    }

    public RowData AddRow(string dbName, string tableName, IReadOnlyList<string?> values) =>
        WithDatabase(dbName, db =>
        {
            var table = db.GetTable(tableName);
            var row = table.AddRow(RowValidator.Parse(table.Schema, values));
            db.MarkModified();
            return ToRowData(table, row);
        });

    public RowData UpdateRow(string dbName, string tableName, long id, IReadOnlyList<string?> values) =>
        WithDatabase(dbName, db =>
        {
            var table = db.GetTable(tableName);
            table.GetRow(id);
            var row = table.UpdateRow(id, RowValidator.Parse(table.Schema, values));
            db.MarkModified();
            return ToRowData(table, row);
        });

    public void DeleteRow(string dbName, string tableName, long id) =>
        WithDatabase(dbName, db =>
        {
            db.GetTable(tableName).DeleteRow(id);
            db.MarkModified();
            return true;
        });

    public DedupData RemoveDuplicates(string dbName, string tableName, bool dryRun) =>
        WithDatabase(dbName, db =>
        {
            var table = db.GetTable(tableName);
            var result = TableOperations.RemoveDuplicates(table, dryRun);
            if (!dryRun && result.RemovedCount > 0)
                db.MarkModified();
            var shown = result.RemovedRows.Take(MaxPageSize).Select(r => ToRowData(table, r)).ToList();
            return new DedupData(result.RemovedCount, shown);
        });

    private T WithDatabase<T>(string dbName, Func<Database, T> action)
    {
        var entry = store.Get(dbName);
        lock (entry.Sync)
        {
            DatabaseStore.EnsureAlive(entry);
            return action(entry.Database);
        }
    }

    private static TableSchemaInfo ToSchemaInfo(Table table) =>
        new(table.Name, table.Schema.Fields.Select(f => new FieldInfo(f.Name, f.Type.Name)).ToList());

    private static RowData ToRowData(Table table, Row row) =>
        new(row.Id, RowValidator.Format(table.Schema, row.Values));
}
