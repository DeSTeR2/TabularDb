using TabularDb.Core.Exceptions;
using TabularDb.Core.Validation;

namespace TabularDb.Core.Model;

public sealed class Database
{
    private readonly Dictionary<string, Table> _tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _order = [];

    public string Name { get; }
    public bool IsModified { get; private set; }

    public Database(string name)
    {
        Name = name;
    }

    public IReadOnlyList<string> TableNames => _order.Select(n => _tables[n].Name).ToList();

    public IEnumerable<Table> Tables => _order.Select(n => _tables[n]);

    public bool HasTable(string name) => _tables.ContainsKey(name.Trim());

    public Table CreateTable(string name, IReadOnlyList<Field> fields)
    {
        SchemaValidator.Validate(this, name, fields).ThrowIfInvalid();
        var trimmed = name.Trim();
        var table = new Table(trimmed, new Schema(fields.Select(f => f with { Name = f.Name.Trim() })));
        AddTable(table);
        MarkModified();
        return table;
    }

    public void DropTable(string name)
    {
        var table = GetTable(name);
        _tables.Remove(table.Name);
        _order.RemoveAll(n => string.Equals(n, table.Name, StringComparison.OrdinalIgnoreCase));
        MarkModified();
    }

    public Table GetTable(string name) =>
        _tables.TryGetValue(name.Trim(), out var table)
            ? table
            : throw new NotFoundException($"Таблицю '{name}' не знайдено");

    public void MarkModified() => IsModified = true;

    public void MarkSaved() => IsModified = false;

    internal void AddTable(Table table)
    {
        if (!_tables.TryAdd(table.Name, table))
            throw new AlreadyExistsException($"Таблиця '{table.Name}' вже існує");
        _order.Add(table.Name);
    }
}
