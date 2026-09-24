using System.Text.RegularExpressions;

using Tasks.Todo;

namespace Tasks.UnitTests;

public class TaskIdTests
{
    [Fact]
    public void Create_HasEightCharactersInTwoGroups()
    {
        var id = TaskId.Create("renew passport", new HashSet<string>());

        Assert.Matches(new Regex("^[0-9a-hjkmnp-tv-z]{4}-[0-9a-hjkmnp-tv-z]{4}$"), id);
    }

    [Fact]
    public void Create_SameText_SameId()
    {
        Assert.Equal(TaskId.Create("renew passport", new HashSet<string>()), TaskId.Create("  renew passport ", new HashSet<string>()));
    }

    [Fact]
    public void Create_Taken_ReturnsAnotherId()
    {
        var first = TaskId.Create("renew passport", new HashSet<string>());
        var second = TaskId.Create("renew passport", new HashSet<string> { first });

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("ABCD-EFGH", "abcdefgh")]
    [InlineData("abcd", "abcd")]
    [InlineData("abc", null)]
    [InlineData("abcd-efghj", null)]
    [InlineData("file.md:3", null)]
    public void NormalizeReference_AcceptsIdsAndPrefixes(string raw, string? expected)
    {
        Assert.Equal(expected, TaskId.NormalizeReference(raw));
    }

    [Fact]
    public void Matches_PrefixAcrossTheDash()
    {
        Assert.True(TaskId.Matches("abcd-efgh", "abcde"));
        Assert.False(TaskId.Matches("abcd-efgh", "abce"));
    }
}
