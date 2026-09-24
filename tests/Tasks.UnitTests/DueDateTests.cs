using Tasks.Todo;

namespace Tasks.UnitTests;

public class DueDateTests
{
    // A Thursday.
    private static readonly DateOnly _today = new(2026, 9, 24);

    [Theory]
    [InlineData("2026-10-15", "2026-10-15")]
    [InlineData("today", "2026-09-24")]
    [InlineData("Tomorrow", "2026-09-25")]
    [InlineData("+3d", "2026-09-27")]
    [InlineData("+2w", "2026-10-08")]
    [InlineData("fri", "2026-09-25")]
    [InlineData("monday", "2026-09-28")]
    public void Parse_KnownForms_ReturnAbsoluteDate(string raw, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected), DueDate.Parse(raw, _today));
    }

    [Fact]
    public void Parse_SameWeekday_MeansNextWeek()
    {
        Assert.Equal(new DateOnly(2026, 10, 1), DueDate.Parse("thu", _today));
    }

    [Theory]
    [InlineData("next week")]
    [InlineData("2026-13-01")]
    [InlineData("15/10/2026")]
    [InlineData("")]
    public void Parse_Unknown_ReturnsNull(string raw)
    {
        Assert.Null(DueDate.Parse(raw, _today));
    }
}
