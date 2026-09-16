using System.Globalization;

namespace TabularDb.Core.Types;

public sealed class TimeType : DataType
{
    public static readonly TimeType Instance = new();

    private static readonly string[] Formats = ["H:mm", "H:mm:ss"];

    public override string Name => "time";
    public override string FormatHint => "ГГ:ХХ або ГГ:ХХ:СС, напр. 09:30";
    public override Type ClrType => typeof(TimeOnly);

    internal static bool TryParseTime(string text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text.Trim(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    internal static string FormatTime(TimeOnly time) =>
        time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    protected override bool TryParseValue(string text, out object? value, out string? error)
    {
        if (TryParseTime(text, out var time))
        {
            value = time;
            error = null;
            return true;
        }
        value = null;
        error = "очікується час у форматі ГГ:ХХ або ГГ:ХХ:СС (00:00–23:59:59)";
        return false;
    }

    protected override bool IsValidValue(object value) => ((TimeOnly)value).Ticks % TimeSpan.TicksPerSecond == 0;

    protected override string FormatValue(object value) => FormatTime((TimeOnly)value);
}

public sealed class TimeInvlType : DataType
{
    public static readonly TimeInvlType Instance = new();

    public override string Name => "timeInvl";
    public override string FormatHint => "початок-кінець, напр. 09:00-17:30";
    public override Type ClrType => typeof(TimeInterval);

    protected override bool TryParseValue(string text, out object? value, out string? error)
    {
        value = null;
        var parts = text.Split('-');
        if (parts.Length != 2)
        {
            error = "очікується інтервал у форматі ГГ:ХХ-ГГ:ХХ";
            return false;
        }
        if (!TimeType.TryParseTime(parts[0], out var start) || !TimeType.TryParseTime(parts[1], out var end))
        {
            error = "некоректний час у межах інтервалу (формат ГГ:ХХ або ГГ:ХХ:СС)";
            return false;
        }
        if (start > end)
        {
            error = "початок інтервалу пізніше кінця";
            return false;
        }
        value = new TimeInterval(start, end);
        error = null;
        return true;
    }

    protected override bool IsValidValue(object value)
    {
        var interval = (TimeInterval)value;
        return interval.Start <= interval.End
            && TimeType.Instance.Validate(interval.Start)
            && TimeType.Instance.Validate(interval.End);
    }

    protected override string FormatValue(object value) => ((TimeInterval)value).ToString();
}
