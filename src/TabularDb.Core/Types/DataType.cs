using TabularDb.Core.Exceptions;

namespace TabularDb.Core.Types;

public abstract class DataType
{
    public abstract string Name { get; }

    public abstract string FormatHint { get; }

    public abstract Type ClrType { get; }

    // Порожній рядок зберігаємо як null (для всіх типів).
    public bool TryParse(string? text, out object? value, out string? error)
    {
        value = null;
        error = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;
        return TryParseValue(text, out value, out error);
    }

    public object? Parse(string? text)
    {
        if (TryParse(text, out var value, out var error))
            return value;
        throw new ValidationException(error ?? "Некоректне значення");
    }

    public bool Validate(object? value) =>
        value is null || (value.GetType() == ClrType && IsValidValue(value));

    public string? Format(object? value)
    {
        if (value is null)
            return null;
        if (!Validate(value))
            throw new ArgumentException($"Значення не відповідає типу {Name}", nameof(value));
        return FormatValue(value);
    }

    protected abstract bool TryParseValue(string text, out object? value, out string? error);

    protected virtual bool IsValidValue(object value) => true;

    protected abstract string FormatValue(object value);

    public override string ToString() => Name;
}
