using System.CommandLine;

namespace Tasks.Skills;

internal static class SkillCommand
{
    internal static Command Create()
    {
        var command = new Command(
            "skill",
            "Print this tool's agent guide, ready to save as a skill file");

        command.SetAction(Run);
        return command;
    }

    private static int Run(ParseResult _)
    {
        // Trailing newlines are trimmed because WriteLine adds one back; without this,
        // `tasks skill > SKILL.md` gains a blank line on every round trip.
        var skill = EmbeddedSkill.Read().TrimEnd('\r', '\n');

        // Output is the file itself and nothing else, so `tasks skill > SKILL.md` works.
        Console.Out.WriteLine(skill);
        return 0;
    }
}
