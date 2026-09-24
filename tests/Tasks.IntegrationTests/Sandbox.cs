using System.Diagnostics;

namespace Tasks.IntegrationTests;

internal sealed record Run(int ExitCode, string Output, string Error);

/// <summary>
/// A throwaway world for one test: a home folder the tool keeps its config in, a bare
/// "remote" repository, and a clone of it the tool manages. The tool itself runs as a separate
/// process - it is exercised exactly as a user runs it.
/// </summary>
internal sealed class Sandbox : IDisposable
{
    private static readonly string _toolPath = typeof(Tasks.Program).Assembly.Location;

    internal Sandbox()
    {
        Root = Directory.CreateTempSubdirectory("tasks-cli-").FullName;
        Home = Directory.CreateDirectory(Path.Combine(Root, "home")).FullName;
        Remote = Path.Combine(Root, "remote.git");
        Work = Path.Combine(Root, "work");

        Git(Root, "init", "--bare", "-b", "main", Remote);
        Clone(Work);
        File.WriteAllText(Path.Combine(Work, "README.md"), "notes\n");
        Git(Work, "add", "README.md");
        Git(Work, "commit", "-m", "initial");
        Git(Work, "push", "-u", "origin", "main");
    }

    internal string Root { get; }

    internal string Home { get; }

    internal string Remote { get; }

    internal string Work { get; }

    internal string TasksFile => Path.Combine(Work, "tasks", "tasks.md");

    internal void Clone(string path)
    {
        Git(Root, "clone", Remote, path);
        Git(path, "config", "user.name", "Test");
        Git(path, "config", "user.email", "test@example.com");
        Git(path, "config", "commit.gpgsign", "false");
    }

    internal Run Tasks(params string[] arguments)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Root,
        };

        info.ArgumentList.Add(_toolPath);
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        info.Environment["HOME"] = Home;
        info.Environment["USERPROFILE"] = Home;
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";

        return Execute(info);
    }

    internal string Git(string directory, params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = directory,
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        var run = Execute(info);
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {run.Error}");
        }

        return run.Output;
    }

    /// <summary>The subjects on the remote's main branch, newest first.</summary>
    internal string[] RemoteLog() =>
        Git(Root, "--git-dir", Remote, "log", "--format=%s", "main")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

    internal string RemoteTasksFile() =>
        Git(Root, "--git-dir", Remote, "show", "main:tasks/tasks.md");

    public void Dispose()
    {
        try
        {
            // Git marks its objects read-only, which Windows refuses to delete.
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temp folder is not a test failure.
        }
    }

    private static Run Execute(ProcessStartInfo info)
    {
        using var process = Process.Start(info)!;
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();

        if (!process.WaitForExit(TimeSpan.FromMinutes(1)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{info.FileName} did not finish");
        }

        return new Run(process.ExitCode, output, error.Result);
    }
}
