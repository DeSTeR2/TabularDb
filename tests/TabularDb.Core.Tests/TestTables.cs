using TabularDb.Core.Model;
using TabularDb.Core.Types;
using TabularDb.Core.Validation;

namespace TabularDb.Core.Tests;

internal static class TestTables
{
    public static Table Create(params (string Name, DataType Type)[] fields)
    {
        var db = new Database("Test");
        return db.CreateTable("T", fields.Select(f => new Field(f.Name, f.Type)).ToList());
    }

    public static Row Add(this Table table, params string?[] raw) =>
        table.AddRow(RowValidator.Parse(table.Schema, raw));
}
