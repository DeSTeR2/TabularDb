using TabularDb.Core.Model;
using TabularDb.Core.Types;

namespace TabularDb.Core.Validation;

public sealed record FieldDefinition(string? Name, string? Type);

public static class SchemaValidator
{
    public const int MaxNameLength = 64;

    public static ValidationResult Validate(Database? database, string? tableName, IReadOnlyList<Field>? fields) =>
        Validate(database, tableName, fields?.Select(f => new FieldDefinition(f.Name, f.Type?.Name)).ToList());

    public static ValidationResult Validate(Database? database, string? tableName, IReadOnlyList<FieldDefinition>? fields)
    {
        var result = new ValidationResult();
        var name = tableName?.Trim() ?? "";

        if (name.Length == 0)
            result.AddError("Назва таблиці не може бути порожньою");
        else if (name.Length > MaxNameLength)
            result.AddError($"Назва таблиці довша за {MaxNameLength} символи");
        else if (database is not null && database.HasTable(name))
            result.AddError($"Таблиця '{name}' вже існує");

        if (fields is null || fields.Count == 0)
        {
            result.AddError("Таблиця повинна мати хоча б одне поле");
            return result;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < fields.Count; i++)
        {
            var fieldName = fields[i].Name?.Trim() ?? "";
            if (fieldName.Length == 0)
                result.AddError($"Поле №{i + 1}: назва не може бути порожньою");
            else if (fieldName.Length > MaxNameLength)
                result.AddError($"Поле №{i + 1}: назва довша за {MaxNameLength} символи");
            else if (!seen.Add(fieldName))
                result.AddError($"Поле '{fieldName}' повторюється");

            var typeName = fields[i].Type;
            if (string.IsNullOrWhiteSpace(typeName))
                result.AddError($"Поле №{i + 1}: не вказано тип");
            else if (!TypeRegistry.TryGet(typeName, out _))
                result.AddError($"Поле №{i + 1}: невідомий тип '{typeName}'");
        }
        return result;
    }

    public static IReadOnlyList<Field> ToFields(IReadOnlyList<FieldDefinition> fields) =>
        fields.Select(f => new Field(f.Name?.Trim() ?? "", TypeRegistry.Create(f.Type ?? ""))).ToList();
}
