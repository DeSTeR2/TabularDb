namespace TabularDb.Core.Model;

public sealed class Schema
{
    public IReadOnlyList<Field> Fields { get; }

    public Schema(IEnumerable<Field> fields)
    {
        Fields = fields.ToList().AsReadOnly();
    }

    public int Count => Fields.Count;

    public Field this[int index] => Fields[index];

    public int IndexOf(string fieldName)
    {
        for (var i = 0; i < Fields.Count; i++)
            if (string.Equals(Fields[i].Name, fieldName, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }
}
