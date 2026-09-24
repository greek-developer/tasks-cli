using System.ComponentModel;
using System.Diagnostics;

namespace Tasks;

/// <summary>Opens a file in the user's editor - $VISUAL, then $EDITOR, then the platform default.</summary>
internal static class Editor
{
    /// <summary>Editors that take <c>+N file</c> to open at a line.</summary>
    private static readonly HashSet<string> _plusLine = new(StringComparer.OrdinalIgnoreCase)
    {
        "vi", "vim", "nvim", "gvim", "nano", "emacs", "emacsclient", "micro", "kak", "helix", "hx", "joe", "mcedit",
    };

    /// <summary>Editors that take <c>-g file:N</c>.</summary>
    private static readonly HashSet<string> _gotoLine = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "code-insiders", "codium", "cursor", "windsurf",
    };

    internal static int Open(string path, int? line = null)
    {
        var editor = Environment.GetEnvironmentVariable("VISUAL")
            ?? Environment.GetEnvironmentVariable("EDITOR")
            ?? (OperatingSystem.IsWindows() ? "notepad" : "vi");

        try
        {
            // ArgumentList quotes each argument itself, so a path containing a quote or a space
            // survives intact instead of being re-split by a hand-built command line.
            var info = new ProcessStartInfo(editor) { UseShellExecute = false };
            var name = Path.GetFileNameWithoutExtension(editor);

            if (line is { } number && _plusLine.Contains(name))
            {
                info.ArgumentList.Add($"+{number}");
                info.ArgumentList.Add(path);
            }
            else if (line is { } gotoNumber && _gotoLine.Contains(name))
            {
                info.ArgumentList.Add("-g");
                info.ArgumentList.Add($"{path}:{gotoNumber}");
            }
            else
            {
                info.ArgumentList.Add(path);
            }

            using var process = Process.Start(info);
            if (process is null)
            {
                Console.Error.WriteLine($"Could not launch editor '{editor}'.");
                return 1;
            }

            process.WaitForExit();

            // The editor's own exit code is not our contract - a non-zero from it would otherwise
            // misleadingly read as a tasks failure.
            if (process.ExitCode == 0)
            {
                return 0;
            }

            Console.Error.WriteLine($"editor exited with code {process.ExitCode}");
            return 1;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            Console.Error.WriteLine($"Could not launch editor '{editor}': {exception.Message}");
            return 1;
        }
    }
}
