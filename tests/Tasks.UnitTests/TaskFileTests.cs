using Tasks.Managed;

namespace Tasks.UnitTests;

public class TaskFileTests
{
    [Fact]
    public void AddToSection_EmptyFile_CreatesSkeleton()
    {
        var file = TaskFile.Parse(string.Empty);

        file.AddToSection("## Open", "- [ ] a", createBefore: "## Done");

        Assert.Equal(["# Tasks", "", "## Open", "", "- [ ] a"], file.Lines);
    }

    [Fact]
    public void AddToSection_AppendsAfterLastItem_BeforeNextHeading()
    {
        var file = TaskFile.Parse("# Tasks\n\n## Open\n\n- [ ] a\n\n## Done\n\n- [x] b\n");

        var index = file.AddToSection("## Open", "- [ ] c");

        Assert.Equal(5, index);
        Assert.Equal("# Tasks\n\n## Open\n\n- [ ] a\n- [ ] c\n\n## Done\n\n- [x] b\n", file.ToString());
    }

    [Fact]
    public void AddToSection_KeepsExplanatoryTextInSection()
    {
        var file = TaskFile.Parse("# Tasks\n\nRunning checklist.\n\n## Open\n\n- [ ] a\nnotes about a\n");

        file.AddToSection("## Open", "- [ ] b");

        Assert.Equal("# Tasks\n\nRunning checklist.\n\n## Open\n\n- [ ] a\nnotes about a\n- [ ] b\n", file.ToString());
    }

    [Fact]
    public void AddToSection_MissingSection_CreatedBeforeDone()
    {
        var file = TaskFile.Parse("# Tasks\n\n## Done\n\n- [x] b\n");

        file.AddToSection("## Open", "- [ ] a", createBefore: "## Done");

        Assert.Equal("# Tasks\n\n## Open\n\n- [ ] a\n\n## Done\n\n- [x] b\n", file.ToString());
    }

    [Fact]
    public void MoveToSection_MissingDone_AppendedAtEnd()
    {
        var file = TaskFile.Parse("# Tasks\n\n## Open\n\n- [ ] a\n- [ ] b\n");

        var index = file.MoveToSection(4, "## Done", "- [x] a");

        Assert.Equal("# Tasks\n\n## Open\n\n- [ ] b\n\n## Done\n\n- [x] a\n", file.ToString());
        Assert.Equal(8, index);
    }

    [Fact]
    public void Parse_CrLf_IsPreservedOnWrite()
    {
        var file = TaskFile.Parse("# Tasks\r\n\r\n## Open\r\n");

        file.AddToSection("## Open", "- [ ] a");

        Assert.Equal("# Tasks\r\n\r\n## Open\r\n- [ ] a\r\n", file.ToString());
    }

    [Fact]
    public void Subsection_DoesNotEndSection()
    {
        var file = TaskFile.Parse("## Open\n\n### Home\n\n- [ ] a\n\n## Done\n");

        file.AddToSection("## Open", "- [ ] b");

        Assert.Equal("## Open\n\n### Home\n\n- [ ] a\n- [ ] b\n\n## Done\n", file.ToString());
    }

    [Fact]
    public void FindByReference_MatchesPrefix()
    {
        var file = TaskFile.Parse("## Open\n- [ ] a {id: abcd-efgh}\n- [ ] b {id: abcz-0000}\n");

        Assert.Equal([1], file.FindByReference("abcde"));
        Assert.Equal([2], file.FindByReference("abcz"));
        Assert.Equal([1, 2], file.FindByReference("abc"));
        Assert.Empty(file.FindByReference("zzzz"));
    }
}
