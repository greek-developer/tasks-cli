using System.Reflection;

namespace Tasks.Skills;

/// <summary>
/// The agent-facing guide, carried inside the tool so a machine with only the package can
/// still produce it.
/// </summary>
internal static class EmbeddedSkill
{
    internal const string Name = "tasks";

    internal const string FileName = "SKILL.md";

    /// <summary>Where an agent harness expects to find it, relative to the user's home.</summary>
    internal const string SuggestedPath = ".claude/skills/tasks/SKILL.md";

    internal static string Read()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(FileName)
            ?? throw new InvalidOperationException(
                $"{FileName} is missing from this build. It is embedded from skills/{Name}/{FileName}.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
