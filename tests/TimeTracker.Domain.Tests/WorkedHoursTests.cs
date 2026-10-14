using TimeTracker.Domain;

namespace TimeTracker.Domain.Tests;

public class WorkedHoursTests
{
    [Fact]
    public void FromMinutes_WithPositiveValue_ExposesMinutes()
    {
        var hours = WorkedHours.FromMinutes(90);

        Assert.Equal(90, hours.Minutes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void FromMinutes_WithNegativeValue_Throws(int minutes)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => WorkedHours.FromMinutes(minutes));

        Assert.Equal("minutes", exception.ParamName);
    }

    [Fact]
    public void FromMinutes_WithZero_IsValid()
    {
        var hours = WorkedHours.FromMinutes(0);

        Assert.Equal(0, hours.Minutes);
    }

    [Fact]
    public void Equals_SameMinutes_AreEqualWithSameHash()
    {
        var first = WorkedHours.FromMinutes(60);
        var second = WorkedHours.FromMinutes(60);

        Assert.True(first == second);
        Assert.True(first.Equals(second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentMinutes_AreNotEqual()
    {
        var first = WorkedHours.FromMinutes(60);
        var second = WorkedHours.FromMinutes(61);

        Assert.True(first != second);
        Assert.False(first.Equals(second));
    }

    [Fact]
    public void Add_TwoValues_ReturnsExactSumAndKeepsOperands()
    {
        var left = WorkedHours.FromMinutes(90);
        var right = WorkedHours.FromMinutes(45);

        var sum = left + right;

        Assert.Equal(135, sum.Minutes);
        Assert.Equal(90, left.Minutes);
        Assert.Equal(45, right.Minutes);
    }

    [Fact]
    public void Add_BeyondIntMaxValue_ThrowsOverflow()
    {
        var max = WorkedHours.FromMinutes(int.MaxValue);
        var one = WorkedHours.FromMinutes(1);

        Assert.Throws<OverflowException>(() => max + one);
    }

    [Fact]
    public void Add_WithZero_IsNeutral()
    {
        var value = WorkedHours.FromMinutes(75);
        var zero = WorkedHours.FromMinutes(0);

        Assert.Equal(value, value + zero);
        Assert.Equal(value, zero + value);
    }

    [Fact]
    public void Add_UpToIntMaxValue_ReturnsIntMaxValue()
    {
        var almostMax = WorkedHours.FromMinutes(int.MaxValue - 1);
        var one = WorkedHours.FromMinutes(1);

        Assert.Equal(int.MaxValue, (almostMax + one).Minutes);
    }
}
