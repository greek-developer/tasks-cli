using Tasks.Skills;

namespace Tasks.UnitTests;

public class EmbeddedSkillTests
{
    [Fact]
    public void Read_IsEmbedded_OpensWithFrontmatter()
    {
        var skill = EmbeddedSkill.Read();

        Assert.StartsWith("---", skill);
        Assert.Contains("name: tasks", skill);
    }
}
