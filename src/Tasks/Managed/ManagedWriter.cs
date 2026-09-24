using Tasks.Config;

namespace Tasks.Managed;

/// <summary>What an edit did to a tasks file: a failure, or the commit message and what to print.</summary>
internal sealed record Edit(string? Error, string CommitMessage, string Output)
{
    internal static Edit Failed(string error) => new(error, string.Empty, string.Empty);

    internal static Edit Done(string commitMessage, string output) => new(null, commitMessage, output);
}

/// <summary>
/// Every change to a managed tasks file goes through here: pull, edit, write, commit, push.
/// Nothing is written unless the pull succeeded, and only the tasks file is ever staged or
/// committed - whatever else the user has in progress in that repository is left alone.
/// </summary>
internal static class ManagedWriter
{
    internal const int Success = 0;
    internal const int Failure = 1;

    /// <summary>The file was changed but the change did not reach the remote - `tasks sync` finishes the job.</summary>
    internal const int NotSynced = 3;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);

    /// <param name="edit">
    /// Receives the file as it is after the pull, and whether the pull changed it.
    /// </param>
    internal static int Write(MonitoredFolder folder, IGitClient git, Func<TaskFile, bool, Edit> edit)
    {
        var settings = folder.Managed!;
        var tasksPath = ManagedFolders.TasksFilePath(folder);

        if (!Directory.Exists(folder.Path))
        {
            return Fail($"The managed folder '{folder.Path}' does not exist on this machine.");
        }

        var repository = Repository.Open(git, folder.Path, out var error);
        if (repository is null)
        {
            return Fail(error);
        }

        using var gate = repository.Lock(_lockTimeout, out error);
        if (gate is null)
        {
            return Fail(error);
        }

        if (!repository.IsIdle(out error))
        {
            return Fail(error);
        }

        string before;
        try
        {
            before = File.Exists(tasksPath) ? File.ReadAllText(tasksPath) : string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Fail($"Could not read '{tasksPath}': {exception.Message}");
        }

        if (settings.Git.PullBeforeWrite && !repository.Pull(out error))
        {
            return Fail($"{error}{Environment.NewLine}Nothing was written.");
        }

        string after;
        try
        {
            after = File.Exists(tasksPath) ? File.ReadAllText(tasksPath) : string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Fail($"Could not read '{tasksPath}': {exception.Message}");
        }

        var file = TaskFile.Parse(after);
        var result = edit(file, !string.Equals(before, after, StringComparison.Ordinal));
        if (result.Error is not null)
        {
            return Fail(result.Error);
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tasksPath)!);
            File.WriteAllText(tasksPath, file.ToString());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Fail($"Could not write '{tasksPath}': {exception.Message}");
        }

        Console.WriteLine(result.Output);

        if (!repository.Commit(tasksPath, result.CommitMessage, out error))
        {
            return NotSyncedBecause($"The change is written but not committed: {error}");
        }

        if (settings.Git.Push && !repository.Push(out error))
        {
            return NotSyncedBecause($"The change is committed but not pushed: {error}");
        }

        return Success;
    }

    /// <summary>
    /// Brings a managed folder level with its remote: commits any change to the tasks file
    /// (a failed commit, or a hand edit), pulls, and pushes.
    /// </summary>
    internal static int Sync(MonitoredFolder folder, IGitClient git)
    {
        var settings = folder.Managed!;
        var tasksPath = ManagedFolders.TasksFilePath(folder);

        if (!Directory.Exists(folder.Path))
        {
            return Fail($"{folder.FriendlyName}: the folder '{folder.Path}' does not exist on this machine.");
        }

        var repository = Repository.Open(git, folder.Path, out var error);
        if (repository is null)
        {
            return Fail($"{folder.FriendlyName}: {error}");
        }

        using var gate = repository.Lock(_lockTimeout, out error);
        if (gate is null)
        {
            return Fail($"{folder.FriendlyName}: {error}");
        }

        if (!repository.IsIdle(out error))
        {
            return Fail($"{folder.FriendlyName}: {error}");
        }

        if (File.Exists(tasksPath) && repository.HasChanges(tasksPath)
            && !repository.Commit(tasksPath, "update tasks", out error))
        {
            return Fail($"{folder.FriendlyName}: could not commit {tasksPath}: {error}");
        }

        if (settings.Git.PullBeforeWrite && !repository.Pull(out error))
        {
            return Fail($"{folder.FriendlyName}: {error}");
        }

        if (settings.Git.Push && !repository.Push(out error))
        {
            return Fail($"{folder.FriendlyName}: {error}");
        }

        Console.WriteLine($"{folder.FriendlyName}: in sync");
        return Success;
    }

    private static int Fail(string error)
    {
        Console.Error.WriteLine(error);
        return Failure;
    }

    private static int NotSyncedBecause(string error)
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine("Run `tasks sync` once the cause is fixed.");
        return NotSynced;
    }
}

/// <summary>The git work tree a managed folder lives in, and the handful of operations run on it.</summary>
internal sealed class Repository
{
    private readonly IGitClient _git;

    private Repository(IGitClient git, string root, string gitDirectory)
    {
        _git = git;
        Root = root;
        GitDirectory = gitDirectory;
    }

    internal string Root { get; }

    internal string GitDirectory { get; }

    internal static Repository? Open(IGitClient git, string folder, out string error)
    {
        error = string.Empty;

        var root = git.Run(folder, "rev-parse", "--show-toplevel");
        var gitDirectory = git.Run(folder, "rev-parse", "--absolute-git-dir");

        if (!root.Succeeded || !gitDirectory.Succeeded)
        {
            error = $"'{folder}' is not inside a git repository, so it cannot be managed: {root.Message}";
            return null;
        }

        return new Repository(git, Path.GetFullPath(root.Output.Trim()), gitDirectory.Output.Trim());
    }

    /// <summary>
    /// Serializes `tasks` invocations on one repository, so two writes cannot interleave their
    /// pull, edit and commit. The lock file sits in the git directory, where it is never tracked.
    /// </summary>
    internal IDisposable? Lock(TimeSpan timeout, out string error)
    {
        error = string.Empty;
        var path = Path.Combine(GitDirectory, "grdev.tasks-cli.lock");
        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(200);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                error = $"Another `tasks` command is writing to {Root} (lock: {path}): {exception.Message}";
                return null;
            }
        }
    }

    /// <summary>False while a merge or rebase is in progress - that is the user's to finish, not ours to build on.</summary>
    internal bool IsIdle(out string error)
    {
        var busy = new[] { "rebase-merge", "rebase-apply", "MERGE_HEAD", "CHERRY_PICK_HEAD" }
            .Any(name => Path.Exists(Path.Combine(GitDirectory, name)));

        error = busy
            ? $"A merge or rebase is in progress in {Root}. Finish or abort it first; nothing was written."
            : string.Empty;
        return !busy;
    }

    internal bool Pull(out string error)
    {
        error = string.Empty;

        var upstream = _git.Run(Root, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}");
        if (!upstream.Succeeded)
        {
            error = $"The current branch in {Root} has no upstream to pull from. Set one (`git push -u origin <branch>`), or turn off `managed.git.pullBeforeWrite` for this folder.";
            return false;
        }

        var pull = _git.Run(Root, "pull", "--rebase", "--autostash");
        if (pull.Succeeded)
        {
            return true;
        }

        // A conflicting rebase stops half-way. The check before the pull established that no
        // rebase was ours to preserve, so put the repository back as it was.
        if (Path.Exists(Path.Combine(GitDirectory, "rebase-merge")) || Path.Exists(Path.Combine(GitDirectory, "rebase-apply")))
        {
            _git.Run(Root, "rebase", "--abort");
        }

        error = $"git pull failed in {Root}: {pull.Message}";
        return false;
    }

    internal bool HasChanges(string path)
    {
        var status = _git.Run(Path.GetDirectoryName(path)!, "status", "--porcelain", "--", Path.GetFileName(path));
        return status.Succeeded && status.Output.Trim().Length > 0;
    }

    /// <summary>Stages and commits the one file. The pathspec keeps anything else staged out of the commit.</summary>
    internal bool Commit(string path, string message, out string error)
    {
        error = string.Empty;

        // Run beside the file and name it bare: a pathspec relative to the working directory
        // survives symlinked paths that a path relative to the repository root would not.
        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileName(path);

        var add = _git.Run(directory, "add", "--", name);
        if (!add.Succeeded)
        {
            error = add.Message;
            return false;
        }

        var commit = _git.Run(directory, "commit", "-m", message, "--", name);
        if (!commit.Succeeded)
        {
            error = commit.Message;
            return false;
        }

        return true;
    }

    internal bool Push(out string error)
    {
        var push = _git.Run(Root, "push");
        error = push.Succeeded ? string.Empty : push.Message;
        return push.Succeeded;
    }
}
