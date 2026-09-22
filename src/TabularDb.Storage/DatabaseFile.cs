namespace TabularDb.Storage;

internal sealed class DatabaseFile
{
    public int FormatVersion { get; set; }
    public string? Name { get; set; }
    public List<TableFile>? Tables { get; set; }
}

internal sealed class TableFile
{
    public string? Name { get; set; }
    public long NextRowId { get; set; }
    public List<FieldFile>? Fields { get; set; }
    public List<RowFile>? Rows { get; set; }
}

internal sealed class FieldFile
{
    public string? Name { get; set; }
    public string? Type { get; set; }
}

internal sealed class RowFile
{
    public long Id { get; set; }
    public List<string?>? Values { get; set; }
}
