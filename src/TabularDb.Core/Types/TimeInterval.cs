namespace TabularDb.Core.Types;

public readonly record struct TimeInterval
{
    public TimeOnly Start { get; }
    public TimeOnly End { get; }

    public TimeInterval(TimeOnly start, TimeOnly end)
    {
        if (start > end)
            throw new ArgumentException("Початок інтервалу пізніше кінця");
        Start = start;
        End = end;
    }

    public TimeSpan Duration => End - Start;

    public bool Contains(TimeOnly time) => time >= Start && time <= End;

    public override string ToString() =>
        $"{TimeType.FormatTime(Start)}-{TimeType.FormatTime(End)}";
}
