using TabularDb.Core.Model;

namespace TabularDb.Core.Validation;

public static class RowValidator
{
    public static object?[] ParseAndValidate(Schema schema, IReadOnlyList<string?> raw, out ValidationResult result)
    {
        result = new ValidationResult();
        var values = new object?[schema.Count];
        if (raw.Count != schema.Count)
        {
            result.AddError($"Очікується значень: {schema.Count}, отримано: {raw.Count}");
            return values;
        }

        for (var i = 0; i < schema.Count; i++)
        {
            var field = schema[i];
            if (field.Type.TryParse(raw[i], out var value, out var error))
                values[i] = value;
            else
                result.AddError($"Поле '{field.Name}': {error}");
        }
        return values;
    }

    public static object?[] Parse(Schema schema, IReadOnlyList<string?> raw)
    {
        var values = ParseAndValidate(schema, raw, out var result);
        result.ThrowIfInvalid();
        return values;
    }

    public static ValidationResult ValidateValues(Schema schema, IReadOnlyList<object?> values)
    {
        var result = new ValidationResult();
        if (values.Count != schema.Count)
        {
            result.AddError($"Очікується значень: {schema.Count}, отримано: {values.Count}");
            return result;
        }
        for (var i = 0; i < schema.Count; i++)
            if (!schema[i].Type.Validate(values[i]))
                result.AddError($"Поле '{schema[i].Name}': значення не відповідає типу {schema[i].Type.Name}");
        return result;
    }

    public static string?[] Format(Schema schema, IReadOnlyList<object?> values)
    {
        var text = new string?[schema.Count];
        for (var i = 0; i < schema.Count; i++)
            text[i] = schema[i].Type.Format(values[i]);
        return text;
    }
}
