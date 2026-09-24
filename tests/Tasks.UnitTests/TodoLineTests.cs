using Tasks.Todo;

namespace Tasks.UnitTests;

public class TodoLineTests
{
    private static readonly string[] _prefixes = ["- [ ]", "[ ]", "//TODO", "TODO"];

    [Fact]
    public void Compose_AllFields_AppendsInReadableOrder()
    {
        var line = TodoLine.Compose("- [ ]", "renew passport", ["admin", "home"], "life", new DateOnly(2026, 10, 15), "A", "abcd-efgh");

        Assert.Equal("- [ ] renew passport #admin #home @life {due: 2026-10-15} {pri: A} {id: abcd-efgh}", line);
    }

    [Fact]
    public void Compose_TagAlreadyInText_IsNotRepeated()
    {
        var line = TodoLine.Compose("- [ ]", "call #work about it", ["work"], null, null, null, null);

        Assert.Equal("- [ ] call #work about it", line);
    }

    [Fact]
    public void Compose_EmptyPrefix_LeavesNoLeadingSpace()
    {
        var line = TodoLine.Compose(string.Empty, "TODO pay", ["x"], null, null, null, null);

        Assert.Equal("TODO pay #x", line);
    }

    [Fact]
    public void GetId_LineWithId_ReturnsIt()
    {
        Assert.Equal("abcd-efgh", TodoLine.GetId("- [ ] x {id: abcd-efgh}"));
        Assert.Null(TodoLine.GetId("- [ ] x"));
    }

    [Fact]
    public void AddTag_WithMarkers_InsertsBeforeMarkers()
    {
        var line = TodoLine.AddTag("- [ ] x #a {due: 2026-01-01} {id: abcd-efgh}", "b");

        Assert.Equal("- [ ] x #a #b {due: 2026-01-01} {id: abcd-efgh}", line);
    }

    [Fact]
    public void AddTag_AlreadyPresent_ReturnsLineUnchanged()
    {
        const string line = "- [ ] x #a";

        Assert.Equal(line, TodoLine.AddTag(line, "a"));
    }

    [Fact]
    public void RemoveTag_DoesNotTouchLongerTagWithSamePrefix()
    {
        var line = TodoLine.RemoveTag("- [ ] x #work #workshop {id: abcd-efgh}", "work");

        Assert.Equal("- [ ] x #workshop {id: abcd-efgh}", line);
    }

    [Fact]
    public void RemoveTag_KeepsIndentation()
    {
        Assert.Equal("  - [ ] x", TodoLine.RemoveTag("  - [ ] x #a", "a"));
    }

    [Fact]
    public void SetMarker_Existing_ReplacesInPlace()
    {
        var line = TodoLine.SetMarker("- [ ] x {due: 2026-01-01} {id: abcd-efgh}", "due", "2026-02-02");

        Assert.Equal("- [ ] x {due: 2026-02-02} {id: abcd-efgh}", line);
    }

    [Fact]
    public void SetMarker_Missing_Appends()
    {
        Assert.Equal("- [ ] x {pri: B}", TodoLine.SetMarker("- [ ] x", "pri", "B"));
    }

    [Fact]
    public void SetMarker_Missing_GoesBeforeId()
    {
        Assert.Equal("- [ ] x {pri: B} {id: abcd-efgh}", TodoLine.SetMarker("- [ ] x {id: abcd-efgh}", "pri", "B"));
    }

    [Fact]
    public void SetMarker_Null_Removes()
    {
        Assert.Equal("- [ ] x {id: abcd-efgh}", TodoLine.SetMarker("- [ ] x {due: 2026-01-01} {id: abcd-efgh}", "due", null));
    }

    [Fact]
    public void SetProject_ReplacesAllProjects()
    {
        Assert.Equal("- [ ] x #t @new {id: abcd-efgh}", TodoLine.SetProject("- [ ] x @old #t {id: abcd-efgh}", "new"));
        Assert.Equal("- [ ] x #t", TodoLine.SetProject("- [ ] x @old #t", null));
    }

    [Fact]
    public void ReplaceText_KeepsTagsProjectAndMarkers()
    {
        var line = TodoLine.ReplaceText("- [ ] call #work @acme bob {due: 2026-01-01} {id: abcd-efgh}", "- [ ]", "email alice");

        Assert.Equal("- [ ] email alice #work @acme {due: 2026-01-01} {id: abcd-efgh}", line);
    }

    [Fact]
    public void ReplaceText_LongerProjectInNewText_KeepsOriginalProject()
    {
        var line = TodoLine.ReplaceText("- [ ] call @acme", "- [ ]", "email @acmecorp");

        Assert.Equal("- [ ] email @acmecorp @acme", line);
    }

    [Fact]
    public void ReplaceText_TagRepeatedInNewText_IsNotDuplicated()
    {
        var line = TodoLine.ReplaceText("- [ ] call #work {id: abcd-efgh}", "- [ ]", "email #work");

        Assert.Equal("- [ ] email #work {id: abcd-efgh}", line);
    }

    [Fact]
    public void Complete_Checkbox_TicksAndStampsDate()
    {
        var line = TodoLine.Complete("- [ ] x #a {id: abcd-efgh}", _prefixes, new DateOnly(2026, 9, 24));

        Assert.Equal("- [x] x #a {done-date: 2026-09-24} {id: abcd-efgh}", line);
    }

    [Fact]
    public void Complete_TodoPrefix_BecomesTickedCheckbox()
    {
        var line = TodoLine.Complete("TODO x", _prefixes, new DateOnly(2026, 9, 24));

        Assert.Equal("- [x] x {done-date: 2026-09-24}", line);
    }

    [Fact]
    public void Summary_StripsPrefixAndMarkers()
    {
        Assert.Equal("renew passport #admin", TodoLine.Summary("- [ ] renew passport #admin {due: 2026-10-15} {id: abcd-efgh}", _prefixes));
        Assert.Equal("done thing", TodoLine.Summary("- [x] done thing {done-date: 2026-09-24}", _prefixes));
    }

    [Fact]
    public void Summary_Long_IsTruncated()
    {
        var summary = TodoLine.Summary("- [ ] " + new string('a', 100), _prefixes, maxLength: 20);

        Assert.Equal(20, summary.Length);
        Assert.EndsWith("…", summary);
    }

    [Theory]
    [InlineData("work", "work")]
    [InlineData("#work", "work")]
    [InlineData("two words", null)]
    [InlineData("", null)]
    public void NormalizeTag_AcceptsBareOrHashed(string raw, string? expected)
    {
        Assert.Equal(expected, TodoLine.NormalizeTag(raw));
    }

    [Theory]
    [InlineData("a", "A")]
    [InlineData("B", "B")]
    [InlineData("AB", null)]
    [InlineData("1", null)]
    public void NormalizePriority_SingleLetterOnly(string raw, string? expected)
    {
        Assert.Equal(expected, TodoLine.NormalizePriority(raw));
    }
}
