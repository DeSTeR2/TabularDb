namespace TabularDb.Core.Model;

public sealed class Row
{
    private object?[] _values;

    public long Id { get; }

    public IReadOnlyList<object?> Values => _values;

    internal Row(long id, object?[] values)
    {
        Id = id;
        _values = values;
    }

    internal object?[] RawValues => _values;

    internal void SetValues(object?[] values) => _values = values;

    public object? this[int index] => _values[index];
}
