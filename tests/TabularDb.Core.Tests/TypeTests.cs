using TabularDb.Core.Exceptions;
using TabularDb.Core.Types;

namespace TabularDb.Core.Tests;

public class TypeTests
{
    [Theory]
    [InlineData("9:05", "09:05:00")]
    [InlineData("09:05", "09:05:00")]
    [InlineData("23:59:59", "23:59:59")]
    [InlineData("0:00", "00:00:00")]
    [InlineData(" 7:30:15 ", "07:30:15")]
    public void TimeType_Parse_Valid(string input, string expected)
    {
        var value = TimeType.Instance.Parse(input);

        Assert.IsType<TimeOnly>(value);
        Assert.Equal(expected, TimeType.Instance.Format(value));
    }

    [Theory]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("abc")]
    [InlineData("12")]
    [InlineData("12:00:00:00")]
    [InlineData("12:5")]
    public void TimeType_Parse_Invalid(string input)
    {
        Assert.False(TimeType.Instance.TryParse(input, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TimeInvlType_StartAfterEnd_IsRejected()
    {
        Assert.False(TimeInvlType.Instance.TryParse("17:00-09:00", out _, out var error));
        Assert.Contains("пізніше", error);
    }

    [Fact]
    public void TimeInvlType_EqualBounds_IsAccepted()
    {
        var value = Assert.IsType<TimeInterval>(TimeInvlType.Instance.Parse("09:00-09:00"));

        Assert.Equal(TimeSpan.Zero, value.Duration);
    }

    [Theory]
    [InlineData("09:00")]
    [InlineData("09:00-")]
    [InlineData("09:00-10:00-11:00")]
    [InlineData("09:00-25:00")]
    public void TimeInvlType_BadFormat_IsRejected(string input)
    {
        Assert.False(TimeInvlType.Instance.TryParse(input, out _, out _));
    }

    [Fact]
    public void TimeInterval_ContainsAndFormat()
    {
        var interval = (TimeInterval)TimeInvlType.Instance.Parse("9:00 - 17:30")!;

        Assert.True(interval.Contains(new TimeOnly(12, 0)));
        Assert.False(interval.Contains(new TimeOnly(18, 0)));
        Assert.Equal("09:00:00-17:30:00", TimeInvlType.Instance.Format(interval));
        Assert.Throws<ArgumentException>(() => new TimeInterval(new TimeOnly(10, 0), new TimeOnly(9, 0)));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1,5")]
    [InlineData("1e400")]
    public void RealType_Invalid(string input)
    {
        Assert.False(RealType.Instance.TryParse(input, out _, out _));
    }

    [Fact]
    public void RealType_NegativeZero_IsNormalized()
    {
        var value = (double)RealType.Instance.Parse("-0.0")!;

        Assert.False(double.IsNegative(value));
        Assert.Equal("1.5", RealType.Instance.Format(RealType.Instance.Parse("1.50")));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("😀")]
    public void CharType_Invalid(string input)
    {
        Assert.False(CharType.Instance.TryParse(input, out _, out _));
    }

    [Fact]
    public void IntegerType_ParsesAndRejects()
    {
        Assert.Equal(7L, IntegerType.Instance.Parse("007"));
        Assert.Equal(-5L, IntegerType.Instance.Parse("-5"));
        Assert.Throws<ValidationException>(() => IntegerType.Instance.Parse("1.5"));
        Assert.Throws<ValidationException>(() => IntegerType.Instance.Parse("99999999999999999999"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyInput_IsNullForEveryType(string? input)
    {
        foreach (var name in TypeRegistry.Names)
        {
            Assert.True(TypeRegistry.Create(name).TryParse(input, out var value, out _));
            Assert.Null(value);
        }
    }

    [Fact]
    public void TypeRegistry_IsCaseInsensitive()
    {
        Assert.Same(TimeInvlType.Instance, TypeRegistry.Create("TIMEINVL"));
        Assert.False(TypeRegistry.TryGet("date", out _));
        Assert.Throws<ArgumentException>(() => TypeRegistry.Create("money"));
    }

    [Fact]
    public void Validate_ChecksClrType()
    {
        Assert.True(TimeType.Instance.Validate(new TimeOnly(1, 2, 3)));
        Assert.False(TimeType.Instance.Validate("01:02:03"));
        Assert.False(TimeType.Instance.Validate(new TimeOnly(1, 2, 3, 500)));
        Assert.False(RealType.Instance.Validate(double.NaN));
        Assert.False(IntegerType.Instance.Validate(5));
    }
}
