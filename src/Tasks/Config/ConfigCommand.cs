using System.CommandLine;

namespace Tasks.Config;

/// <summary><c>tasks config path</c> and <c>tasks config edit</c>.</summary>
internal static class ConfigCommand
{
    internal static Command Create()
    {
        var path = new Command("path", "Print the resolved config file path.");
        path.SetAction(RunPath);

        var edit = new Command("edit", "Open the config file in $VISUAL / $EDITOR.");
        edit.SetAction(RunEdit);

        return new Command("config", "Inspect and edit configuration.") { path, edit };
    }

    private static int RunPath(ParseResult _)
    {
        Console.WriteLine(ConfigurationManager.GetConfigPath());
        return 0;
    }

    private static int RunEdit(ParseResult _)
    {
        var path = ConfigurationManager.GetConfigPath();

        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // Same default shape ConfigurationManager.LoadConfig() would create - a
            // fill-in-the-blanks starting point, not a missing file the editor would refuse
            // to open.
            File.WriteAllText(path, "{\n  \"folders\": []\n}\n");
        }

        return Editor.Open(path);
    }
}
