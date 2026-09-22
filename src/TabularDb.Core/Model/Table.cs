using TabularDb.Core.Exceptions;
using TabularDb.Core.Validation;

namespace TabularDb.Core.Model;

public sealed class Table
{
    private readonly List<Row> _rows = [];

    public string Name { get; }
    public Schema Schema { get; }
    public long NextRowId { get; private set; } = 1;

    public IReadOnlyList<Row> Rows => _rows;

    public Table(string name, Schema schema)
    {
        Name = name;
        Schema = schema;
    }

    public ValidationResult ValidateValues(IReadOnlyList<object?> values) =>
        RowValidator.ValidateValues(Schema, values);

    public Row AddRow(IReadOnlyList<object?> values)
    {
        ValidateValues(values).ThrowIfInvalid();
        var row = new Row(NextRowId++, values.ToArray());
        _rows.Add(row);
        return row;
    }

    public Row UpdateRow(long id, IReadOnlyList<object?> values)
    {
        var row = GetRow(id);
        ValidateValues(values).ThrowIfInvalid();
        row.SetValues(values.ToArray());
        return row;
    }

    public void DeleteRow(long id)
    {
        var row = GetRow(id);
        _rows.Remove(row);
    }

    public Row GetRow(long id) =>
        _rows.Find(r => r.Id == id) ?? throw new NotFoundException($"Рядок з id {id} не знайдено в таблиці '{Name}'");

    public IReadOnlyList<Row> GetRows(int skip, int take) =>
        _rows.Skip(Math.Max(0, skip)).Take(Math.Max(0, take)).ToList();

    public int RemoveRows(IEnumerable<Row> rows)
    {
        var ids = rows.Select(r => r.Id).ToHashSet();
        return _rows.RemoveAll(r => ids.Contains(r.Id));
    }

    // Тільки для завантаження з файлу: id там уже є.
    internal void RestoreRow(long id, object?[] values)
    {
        if (id <= 0 || _rows.Exists(r => r.Id == id))
            throw new ValidationException($"Некоректний або повторний id рядка: {id}");
        ValidateValues(values).ThrowIfInvalid();
        _rows.Add(new Row(id, values));
        if (id >= NextRowId)
            NextRowId = id + 1;
    }

    internal void RestoreNextRowId(long nextRowId)
    {
        if (nextRowId > NextRowId)
            NextRowId = nextRowId;
    }
}
