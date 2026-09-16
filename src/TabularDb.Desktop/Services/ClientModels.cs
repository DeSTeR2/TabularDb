namespace TabularDb.Desktop.Services;

public sealed record DatabaseItem(string Name, bool IsLoaded, bool IsModified);

public sealed record DatabaseListing(IReadOnlyList<DatabaseItem> Databases, IReadOnlyList<string> Types);

public sealed record FieldItem(string Name, string Type);

public sealed record TableSchema(string Name, IReadOnlyList<FieldItem> Fields);

public sealed record TableListing(IReadOnlyList<string> Tables, bool IsModified);

public sealed record RowItem(long Id, IReadOnlyList<string?> Values);

public sealed record RowsPage(IReadOnlyList<RowItem> Rows, int Total);

public sealed record DedupOutcome(int RemovedCount, IReadOnlyList<RowItem> RemovedRows);

public sealed record ExportedFile(string FileName, string Content);

public sealed class ClientException(string message, IReadOnlyList<string>? errors = null, bool isUnavailable = false)
    : Exception(message)
{
    public IReadOnlyList<string> Errors { get; } = errors ?? [];
    public bool IsUnavailable { get; } = isUnavailable;
}
