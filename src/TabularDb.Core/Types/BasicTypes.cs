using System.Globalization;

namespace TabularDb.Core.Types;

public sealed class IntegerType : DataType
{
    public static readonly IntegerType Instance = new();

    public override string Name => "integer";
    public override string FormatHint => "ціле число, напр. -42";
    public override Type ClrType => typeof(long);

    protected override bool TryParseValue(string text, out object? value, out string? error)
    {
        if (long.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n))
        {
            value = n;
            error = null;
            return true;
        }
        value = null;
        error = "очікується ціле число";
        return false;
    }

    protected override string FormatValue(object value) =>
        ((long)value).ToString(CultureInfo.InvariantCulture);
}

public sealed class RealType : DataType
{
    public static readonly RealType Instance = new();

    public override string Name => "real";
    public override string FormatHint => "дійсне число з крапкою, напр. 3.14";
    public override Type ClrType => typeof(double);

    protected override bool TryParseValue(string text, out object? value, out string? error)
    {
        if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d))
        {
            value = d == 0 ? 0.0 : d;
            error = null;
            return true;
        }
        value = null;
        error = "очікується дійсне число (роздільник — крапка)";
        return false;
    }

    protected override bool IsValidValue(object value) => double.IsFinite((double)value);

    protected override string FormatValue(object value) =>
        ((double)value).ToString("R", CultureInfo.InvariantCulture);
}

public sealed class CharType : DataType
{
    public static readonly CharType Instance = new();

    public override string Name => "char";
    public override string FormatHint => "один символ";
    public override Type ClrType => typeof(char);

    protected override bool TryParseValue(string text, out object? value, out string? error)
    {
        if (text.Length != 1)
            text = text.Trim();
        if (text.Length == 1 && !char.IsSurrogate(text[0]))
        {
            value = text[0];
            error = null;
            return true;
        }
        value = null;
        error = "очікується рівно один символ";
        return false;
    }

    protected override bool IsValidValue(object value) => !char.IsSurrogate((char)value);

    protected override string FormatValue(object value) => ((char)value).ToString();
}

public sealed class StringType : DataType
{
    public static readonly StringType Instance = new();

    public override string Name => "string";
    public override string FormatHint => "довільний текст";
    public override Type ClrType => typeof(string);

    protected override bool TryParseValue(string text, out object? value, out string? error)
    {
        value = text;
        error = null;
        return true;
    }

    protected override bool IsValidValue(object value) => ((string)value).Trim().Length > 0;

    protected override string FormatValue(object value) => (string)value;
}
