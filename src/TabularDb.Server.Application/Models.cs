namespace TabularDb.Server.Application;

public sealed record DatabaseSummary(string Name, bool IsLoaded, bool IsModified);

public sealed record FieldInfo(string Name, string Type);

public sealed record TableSchemaInfo(string Name, IReadOnlyList<FieldInfo> Fields);

public sealed record RowData(long Id, IReadOnlyList<string?> Values);

public sealed record RowPageData(IReadOnlyList<RowData> Rows, int Total);

public sealed record DedupData(int RemovedCount, IReadOnlyList<RowData> RemovedRows);

public sealed record TableListData(IReadOnlyList<string> Tables, bool IsModified);
