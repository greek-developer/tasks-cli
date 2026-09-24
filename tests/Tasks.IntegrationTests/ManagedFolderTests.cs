using System.Text.RegularExpressions;

namespace Tasks.IntegrationTests;

public class ManagedFolderTests : IDisposable
{
    private readonly Sandbox _sandbox = new();

    public void Dispose() => _sandbox.Dispose();

    private void Manage(params string[] extra)
    {
        var run = _sandbox.Tasks(["folders", "add", _sandbox.Work, "--name", "notes", "--managed", .. extra]);
        Assert.True(run.ExitCode == 0, run.Error);
    }

    private string AddTask(params string[] arguments)
    {
        var run = _sandbox.Tasks(["todo", "add", .. arguments]);
        Assert.True(run.ExitCode == 0, run.Error);
        return Regex.Match(run.Output, @"\{id: ([0-9a-z]{4}-[0-9a-z]{4})\}").Groups[1].Value;
    }

    [Fact]
    public void Add_ManagedFolder_WritesCommitsAndPushes()
    {
        Manage();

        var run = _sandbox.Tasks("todo", "add", "renew passport", "--due", "2026-10-15", "--tag", "admin,home", "--project", "life", "--priority", "a");

        Assert.Equal(0, run.ExitCode);
        Assert.Matches(@"^- \[ \] renew passport #admin #home @life \{due: 2026-10-15\} \{pri: A\} \{id: [0-9a-z]{4}-[0-9a-z]{4}\} \(-> .*tasks\.md:5\)", run.Output.Trim());
        Assert.Equal("add task: renew passport #admin #home @life", _sandbox.RemoteLog()[0]);
        Assert.Contains("## Open", _sandbox.RemoteTasksFile());
        Assert.Contains("renew passport", _sandbox.RemoteTasksFile());
    }

    [Fact]
    public void Add_RemoteMovedAhead_PullsBeforeWriting()
    {
        Manage();
        AddTask("first");

        var other = Path.Combine(_sandbox.Root, "other");
        _sandbox.Clone(other);
        File.AppendAllText(Path.Combine(other, "tasks", "tasks.md"), "- [ ] from elsewhere\n");
        _sandbox.Git(other, "commit", "-am", "edit elsewhere");
        _sandbox.Git(other, "push");

        AddTask("second");

        var remote = _sandbox.RemoteTasksFile();
        Assert.Contains("from elsewhere", remote);
        Assert.Contains("second", remote);
        Assert.Equal("add task: second", _sandbox.RemoteLog()[0]);
    }

    [Fact]
    public void Add_OtherStagedFile_IsNotCommitted()
    {
        Manage();
        File.WriteAllText(Path.Combine(_sandbox.Work, "draft.md"), "draft\n");
        _sandbox.Git(_sandbox.Work, "add", "draft.md");

        AddTask("keep my draft staged");

        var status = _sandbox.Git(_sandbox.Work, "status", "--porcelain");
        Assert.Contains("A  draft.md", status);
        Assert.DoesNotContain("draft.md", _sandbox.Git(_sandbox.Work, "show", "--name-only", "--format=", "HEAD"));
    }

    [Fact]
    public void Done_ByIdPrefix_TicksMovesAndStampsDate()
    {
        Manage();
        var id = AddTask("renew passport");
        AddTask("other task");

        var run = _sandbox.Tasks("todo", "done", id[..4]);

        Assert.True(run.ExitCode == 0, run.Error);
        var lines = File.ReadAllLines(_sandbox.TasksFile);
        var done = Array.IndexOf(lines, "## Done");
        Assert.True(done > 0);
        Assert.Matches(@"^- \[x\] renew passport \{done-date: \d{4}-\d{2}-\d{2}\} \{id: [0-9a-z-]+\}$", lines[^1]);
        Assert.DoesNotContain(lines[..done], l => l.Contains("renew passport"));
        Assert.Equal("complete task: renew passport", _sandbox.RemoteLog()[0]);
    }

    [Fact]
    public void Done_TwiceOnSameTask_IsRefused()
    {
        Manage();
        var id = AddTask("once");
        Assert.Equal(0, _sandbox.Tasks("todo", "done", id).ExitCode);

        var run = _sandbox.Tasks("todo", "done", id);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("not an open task", run.Error);
    }

    [Fact]
    public void Edit_ChangesFieldsAndKeepsId()
    {
        Manage();
        var id = AddTask("call bob", "--tag", "work", "--due", "2026-10-01");

        var run = _sandbox.Tasks("todo", "edit", id, "--text", "email bob", "--add-tag", "urgent", "--remove-tag", "work", "--no-due", "--priority", "B");

        Assert.True(run.ExitCode == 0, run.Error);
        Assert.Contains($"- [ ] email bob #urgent {{pri: B}} {{id: {id}}}", File.ReadAllText(_sandbox.TasksFile));
        Assert.StartsWith("edit task: email bob", _sandbox.RemoteLog()[0]);
    }

    [Fact]
    public void Remove_DeletesLine()
    {
        Manage();
        var id = AddTask("throw away");

        var run = _sandbox.Tasks("todo", "rm", id);

        Assert.True(run.ExitCode == 0, run.Error);
        Assert.DoesNotContain("throw away", File.ReadAllText(_sandbox.TasksFile));
        Assert.Equal("remove task: throw away", _sandbox.RemoteLog()[0]);
    }

    [Fact]
    public void FileLine_ChangedByPull_IsRefused()
    {
        Manage();
        AddTask("first");

        var other = Path.Combine(_sandbox.Root, "other");
        _sandbox.Clone(other);
        var otherFile = Path.Combine(other, "tasks", "tasks.md");
        var otherLines = File.ReadAllLines(otherFile).ToList();
        otherLines.Insert(otherLines.IndexOf("## Open") + 2, "- [ ] inserted above");
        File.WriteAllLines(otherFile, otherLines);
        _sandbox.Git(other, "commit", "-am", "insert");
        _sandbox.Git(other, "push");

        var before = File.ReadAllText(_sandbox.TasksFile);
        var run = _sandbox.Tasks("todo", "done", $"{_sandbox.TasksFile}:5");

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("changed when it was pulled", run.Error);
        Assert.DoesNotContain("[x]", File.ReadAllText(_sandbox.TasksFile));
        Assert.NotEqual(before, File.ReadAllText(_sandbox.TasksFile));
    }

    [Fact]
    public void FileLine_Unchanged_Works()
    {
        Manage();
        AddTask("by line");

        var run = _sandbox.Tasks("todo", "done", $"{_sandbox.TasksFile}:5");

        Assert.True(run.ExitCode == 0, run.Error);
        Assert.Contains("- [x] by line", File.ReadAllText(_sandbox.TasksFile));
    }

    [Fact]
    public void Push_Fails_ExitsThreeAndSyncRecovers()
    {
        Manage();
        AddTask("first");

        // Make the remote refuse pushes while still allowing the pull.
        var hook = Path.Combine(_sandbox.Remote, "hooks", "pre-receive");
        File.WriteAllText(hook, "#!/bin/sh\nexit 1\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var run = _sandbox.Tasks("todo", "add", "second");

        Assert.Equal(3, run.ExitCode);
        Assert.Contains("not pushed", run.Error);
        Assert.Equal("add task: second", _sandbox.Git(_sandbox.Work, "log", "-1", "--format=%s").Trim());
        Assert.Equal("add task: first", _sandbox.RemoteLog()[0]);

        File.Delete(hook);
        var sync = _sandbox.Tasks("sync");

        Assert.True(sync.ExitCode == 0, sync.Error);
        Assert.Equal("add task: second", _sandbox.RemoteLog()[0]);
    }

    [Fact]
    public void Sync_CommitsHandEdits()
    {
        Manage();
        AddTask("first");
        File.AppendAllText(_sandbox.TasksFile, "- [ ] typed by hand\n");

        var run = _sandbox.Tasks("sync");

        Assert.True(run.ExitCode == 0, run.Error);
        Assert.Equal("update tasks", _sandbox.RemoteLog()[0]);
        Assert.Contains("typed by hand", _sandbox.RemoteTasksFile());
    }

    [Fact]
    public void NoPush_CommitsOnly()
    {
        Manage("--no-push");

        AddTask("local only");

        Assert.Equal("add task: local only", _sandbox.Git(_sandbox.Work, "log", "-1", "--format=%s").Trim());
        Assert.Equal("initial", _sandbox.RemoteLog()[0]);
    }

    [Fact]
    public void NoUpstream_RefusesAndWritesNothing()
    {
        Manage();
        _sandbox.Git(_sandbox.Work, "branch", "--unset-upstream");

        var run = _sandbox.Tasks("todo", "add", "nowhere");

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("no upstream", run.Error);
        Assert.False(File.Exists(_sandbox.TasksFile));
    }

    [Fact]
    public void Manage_OutsideGit_IsRefused()
    {
        var plain = Directory.CreateDirectory(Path.Combine(_sandbox.Root, "plain")).FullName;

        var run = _sandbox.Tasks("folders", "add", plain, "--managed");

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("not inside a git repository", run.Error);
    }

    [Fact]
    public void UnmanagedFolder_TaskCannotBeChanged()
    {
        var plain = Directory.CreateDirectory(Path.Combine(_sandbox.Root, "plain")).FullName;
        var notes = Path.Combine(plain, "todo.md");
        File.WriteAllText(notes, "- [ ] mine\n");
        Assert.Equal(0, _sandbox.Tasks("folders", "add", plain, "--name", "plain").ExitCode);

        var run = _sandbox.Tasks("todo", "done", $"{notes}:1");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal("- [ ] mine\n", File.ReadAllText(notes));
    }

    [Fact]
    public void Add_TwoManagedFolders_NeedsFolderOrDefault()
    {
        Manage();
        var second = Path.Combine(_sandbox.Root, "second");
        _sandbox.Clone(second);
        Assert.Equal(0, _sandbox.Tasks("folders", "add", second, "--name", "second", "--managed").ExitCode);

        var ambiguous = _sandbox.Tasks("todo", "add", "where?");
        Assert.Equal(1, ambiguous.ExitCode);

        Assert.Equal(0, _sandbox.Tasks("folders", "manage", "second", "--default").ExitCode);
        AddTask("here");

        Assert.Contains("here", File.ReadAllText(Path.Combine(second, "tasks", "tasks.md")));
    }

    [Fact]
    public void List_ShowsManagedTasksWithPriority()
    {
        Manage();
        AddTask("listed", "--tag", "next");

        var run = _sandbox.Tasks("gtd", "next");

        Assert.Contains("listed", run.Output);
    }

    [Fact]
    public void Add_ToNamedFile_AppendsWithoutGit()
    {
        var plain = Directory.CreateDirectory(Path.Combine(_sandbox.Root, "plain")).FullName;
        var notes = Path.Combine(plain, "todo.md");
        File.WriteAllText(notes, "# notes\n");

        var run = _sandbox.Tasks("todo", "add", "pay invoice", notes, "--due", "2026-10-01", "--tag", "money");

        Assert.True(run.ExitCode == 0, run.Error);
        Assert.Equal("# notes\n- [ ] pay invoice #money {due: 2026-10-01}\n", File.ReadAllText(notes));
    }
}
