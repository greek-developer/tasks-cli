using System.ComponentModel;
using System.Diagnostics;

namespace Tasks.Managed;

internal sealed record GitResult(int ExitCode, string Output, string Error)
{
    internal bool Succeeded => ExitCode == 0;

    /// <summary>The most useful single line to show a person - git's complaint, or its output.</summary>
    internal string Message => (Error.Trim().Length > 0 ? Error : Output).Trim();
}

internal interface IGitClient
{
    GitResult Run(string workingDirectory, params string[] arguments);
}

/// <summary>
/// Runs the <c>git</c> executable. Shelling out rather than linking a git library means the
/// user's own setup - SSH keys, credential manager, hooks, config - applies unchanged.
/// </summary>
internal sealed class GitClient : IGitClient
{
    public GitResult Run(string workingDirectory, params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(info)
                ?? throw new InvalidOperationException("git did not start");

            // Read both streams concurrently so neither can fill up and stall the other.
            var error = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return new GitResult(process.ExitCode, output, error.Result);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return new GitResult(-1, string.Empty, $"could not run git: {exception.Message}");
        }
    }
}
