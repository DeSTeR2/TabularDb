using TabularDb.Core.Exceptions;
using TabularDb.Core.Model;
using TabularDb.Core.Types;
using TabularDb.Core.Validation;

namespace TabularDb.Core.Tests;

public class ModelTests
{
    [Fact]
    public void SchemaValidator_CollectsAllErrors()
    {
        var db = new Database("Db");
        db.CreateTable("Existing", [new Field("A", IntegerType.Instance)]);
        FieldDefinition[] fields = [new("Name", "string"), new("name", "integer"), new(" ", "time"), new("X", null), new("Y", "money")];

        var result = SchemaValidator.Validate(db, "existing", fields);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("вже існує"));
        Assert.Contains(result.Errors, e => e.Contains("повторюється"));
        Assert.Contains(result.Errors, e => e.Contains("порожньою"));
        Assert.Contains(result.Errors, e => e.Contains("не вказано тип"));
        Assert.Contains(result.Errors, e => e.Contains("невідомий тип 'money'"));
        Assert.Equal(5, result.Errors.Count);
    }

    [Fact]
    public void SchemaValidator_RequiresNameAndFields()
    {
        var result = SchemaValidator.Validate(null, "  ", Array.Empty<FieldDefinition>());

        Assert.Equal(2, result.Errors.Count);
        Assert.Throws<ValidationException>(() => new Database("Db").CreateTable("T", []));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("what?")]
    [InlineData("50%")]
    [InlineData("#1")]
    public void SchemaValidator_RejectsUnsafeTableNames(string name)
    {
        var result = SchemaValidator.Validate(null, name, [new FieldDefinition("A", "integer")]);

        Assert.Single(result.Errors);
    }

    [Fact]
    public void Database_CreateAndDropTable_TracksModification()
    {
        var db = new Database("Db");
        Assert.False(db.IsModified);

        var table = db.CreateTable("  Shifts ", [new Field(" Worker ", StringType.Instance)]);
        Assert.Equal("Shifts", table.Name);
        Assert.Equal("Worker", table.Schema[0].Name);
        Assert.True(db.IsModified);

        db.MarkSaved();
        db.DropTable("SHIFTS");
        Assert.True(db.IsModified);
        Assert.Empty(db.TableNames);
        Assert.Throws<NotFoundException>(() => db.GetTable("Shifts"));
    }

    [Fact]
    public void RowValidator_ReportsFieldName()
    {
        var table = TestTables.Create(("Worker", StringType.Instance), ("Shift", TimeInvlType.Instance));

        RowValidator.ParseAndValidate(table.Schema, ["Ivan", "17:00-09:00"], out var result);

        var error = Assert.Single(result.Errors);
        Assert.StartsWith("Поле 'Shift'", error);
    }

    [Fact]
    public void RowValidator_WrongValueCount()
    {
        var table = TestTables.Create(("A", IntegerType.Instance));

        Assert.Throws<ValidationException>(() => RowValidator.Parse(table.Schema, ["1", "2"]));
        Assert.Throws<ValidationException>(() => table.AddRow([1L, 2L]));
        Assert.Throws<ValidationException>(() => table.AddRow(["not a long"]));
    }

    [Fact]
    public void Table_RowIdsAreStableAndUnique()
    {
        var table = TestTables.Create(("A", IntegerType.Instance));
        var first = table.Add("1");
        var second = table.Add("2");
        table.DeleteRow(first.Id);
        var third = table.Add("3");

        Assert.Equal(1, first.Id);
        Assert.Equal(2, second.Id);
        Assert.Equal(3, third.Id);
        Assert.Equal(second.Id, table.Rows[0].Id);
    }

    [Fact]
    public void Table_UpdateRow_ChangesValues()
    {
        var table = TestTables.Create(("At", TimeType.Instance));
        var row = table.Add("8:00");

        table.UpdateRow(row.Id, [new TimeOnly(9, 30)]);

        Assert.Equal(new TimeOnly(9, 30), table.GetRow(row.Id)[0]);
    }

    [Fact]
    public void Table_UpdateRow_UnknownId_Throws()
    {
        var table = TestTables.Create(("A", IntegerType.Instance));

        Assert.Throws<NotFoundException>(() => table.UpdateRow(42, [1L]));
        Assert.Throws<NotFoundException>(() => table.DeleteRow(42));
    }

    [Fact]
    public void Table_GetRows_Pages()
    {
        var table = TestTables.Create(("A", IntegerType.Instance));
        for (var i = 0; i < 5; i++)
            table.Add(i.ToString());

        Assert.Equal([3L, 4L], table.GetRows(3, 10).Select(r => (long)r[0]!));
        Assert.Empty(table.GetRows(10, 10));
    }
}
