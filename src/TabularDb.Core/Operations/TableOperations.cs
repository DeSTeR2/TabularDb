using TabularDb.Core.Model;

namespace TabularDb.Core.Operations;

public sealed class RowValuesComparer : IEqualityComparer<IReadOnlyList<object?>>
{
    public static readonly RowValuesComparer Instance = new();

    public bool Equals(IReadOnlyList<object?>? x, IReadOnlyList<object?>? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null || x.Count != y.Count)
            return false;
        for (var i = 0; i < x.Count; i++)
            if (!object.Equals(x[i], y[i]))
                return false;
        return true;
    }

    public int GetHashCode(IReadOnlyList<object?> obj)
    {
        var hash = new HashCode();
        foreach (var value in obj)
            hash.Add(value);
        return hash.ToHashCode();
    }
}

public sealed record DedupResult(int RemovedCount, IReadOnlyList<Row> RemovedRows);

public static class TableOperations
{
    // Лишаємо перше входження, порядок рядків не міняється.
    public static DedupResult RemoveDuplicates(Table table, bool dryRun = false)
    {
        var seen = new HashSet<IReadOnlyList<object?>>(RowValuesComparer.Instance);
        var duplicates = new List<Row>();
        foreach (var row in table.Rows)
            if (!seen.Add(row.Values))
                duplicates.Add(row);

        if (!dryRun && duplicates.Count > 0)
            table.RemoveRows(duplicates);

        return new DedupResult(duplicates.Count, duplicates);
    }
}
