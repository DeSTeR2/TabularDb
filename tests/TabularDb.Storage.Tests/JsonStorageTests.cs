using TabularDb.Core.Model;
using TabularDb.Core.Types;
using TabularDb.Core.Validation;

namespace TabularDb.Storage.Tests;

public sealed class JsonStorageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tdb-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private static Database CreateSample()
    {
        var db = new Database("Shop");
        var table = db.CreateTable("AllTypes",
        [
            new Field("I", IntegerType.Instance),
            new Field("R", RealType.Instance),
            new Field("C", CharType.Instance),
            new Field("S", StringType.Instance),
            new Field("T", TimeType.Instance),
            new Field("Shift", TimeInvlType.Instance),
        ]);
        table.AddRow(RowValidator.Parse(table.Schema, ["-7", "3.25", "ї", "Привіт, \"світ\"", "9:05", "09:00-17:30"]));
        table.AddRow(RowValidator.Parse(table.Schema, [null, null, null, null, null, null]));
        var removed = table.AddRow(RowValidator.Parse(table.Schema, ["1", "1", "x", "y", "1:00", "1:00-2:00"]));
        table.DeleteRow(removed.Id);
        db.CreateTable("Empty", [new Field("Only", StringType.Instance)]);
        return db;
    }

    [Fact]
    public void RoundTrip_AllSixTypes()
    {
        var storage = new JsonStorage();
        var path = Path.Combine(_dir, "Shop" + JsonStorage.Extension);
        var original = CreateSample();

        storage.Save(original, path);
        var loaded = storage.Load(path);

        Assert.False(original.IsModified);
        Assert.False(loaded.IsModified);
        Assert.Equal("Shop", loaded.Name);
        Assert.Equal(["AllTypes", "Empty"], loaded.TableNames);

        var table = loaded.GetTable("AllTypes");
        Assert.Equal(["integer", "real", "char", "string", "time", "timeInvl"], table.Schema.Fields.Select(f => f.Type.Name));
        Assert.Equal(4, table.NextRowId);
        Assert.Equal([1L, 2L], table.Rows.Select(r => r.Id));
        Assert.Equal(
            [-7L, 3.25, 'ї', "Привіт, \"світ\"", new TimeOnly(9, 5), new TimeInterval(new TimeOnly(9, 0), new TimeOnly(17, 30))],
            table.Rows[0].Values);
        Assert.All(table.Rows[1].Values, Assert.Null);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        var storage = new JsonStorage();
        var path = Path.Combine(_dir, "Shop" + JsonStorage.Extension);
        var db = CreateSample();
        storage.Save(db, path);

        db.DropTable("Empty");
        storage.Save(db, path);

        Assert.Equal(["AllTypes"], storage.Load(path).TableNames);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{"formatVersion":2,"name":"X","tables":[]}""")]
    [InlineData("""{"formatVersion":1,"name":"X","tables":[{"name":"T","nextRowId":1,"fields":[{"name":"A","type":"money"}],"rows":[]}]}""")]
    [InlineData("""{"formatVersion":1,"name":"X","tables":[{"name":"T","nextRowId":1,"fields":[{"name":"A","type":"time"}],"rows":[{"id":1,"values":["25:00"]}]}]}""")]
    [InlineData("""{"formatVersion":1,"name":"X","tables":[{"name":"T","nextRowId":1,"fields":[{"name":"A","type":"time"}],"rows":[{"id":1,"values":["10:00"]},{"id":1,"values":["11:00"]}]}]}""")]
    [InlineData("""{"formatVersion":1,"name":"X","tables":[{"name":"T","nextRowId":1,"fields":[],"rows":[]}]}""")]
    [InlineData("""{"formatVersion":1,"name":"X","tables":[{"name":"T","fields":[{"name":"A","type":"time"}]},{"name":"t","fields":[{"name":"A","type":"time"}]}]}""")]
    [InlineData("null")]
    public void Load_CorruptedFile_ThrowsStorageException(string json)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "bad" + JsonStorage.Extension);
        File.WriteAllText(path, json);

        Assert.Throws<StorageException>(() => new JsonStorage().Load(path));
    }

    [Fact]
    public void Load_MissingFile_ThrowsStorageException()
    {
        Assert.Throws<StorageException>(() => new JsonStorage().Load(Path.Combine(_dir, "none.tdb.json")));
    }

    [Fact]
    public void FromJson_NameOverride_IsApplied()
    {
        var json = DatabaseSerializer.ToJson(CreateSample());

        Assert.Equal("Copy", DatabaseSerializer.FromJson(json, "Copy").Name);
    }
}
