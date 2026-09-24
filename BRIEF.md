# BRIEF.md — tasks-cli

## Overview

A command-line tool that gathers todo items already written in plain `.txt` and `.md` files
scattered across a developer's machine, and reports them as one list. It watches a set of
configured folders, scans them for lines that begin with a todo prefix (`- [ ]`, `[ ]`,
`TODO`, `//TODO`), and extracts a due date, tags and projects from each line by regular
expression.

The point is that the files stay the source of truth. Notes, READMEs and scratch files keep
holding the tasks in whatever form their author already uses; this tool reads them and
presents the result — filtered by tag, grouped by project, or arranged as the GTD lists
(inbox / next / review / backlog). Nothing is imported into a database and no separate task
store is introduced.

It is **pre-release**. Monitored folders are read-only. A folder the user marks **managed** is
the exception: it holds one tasks file (`tasks/tasks.md` by default) that the tool adds to,
edits, completes and removes tasks in, committing and pushing every change to the folder's git
remote. `todo add <description> <file>` also still appends one line to a file named on the
command line. How managed folders behave is specified in
[`specs/managed-folders.md`](specs/managed-folders.md).

## Build & run

```powershell
dotnet build                                       # build everything
dotnet build -c Release                            # release build
dotnet test                                        # run all tests (see Tests)
dotnet run --project src/Tasks -- config path      # run the CLI
dotnet pack -c Release                             # produce the global tool into ./release
```

Install the packed tool locally to exercise it as users will:

```powershell
dotnet tool install grdev.tasks-cli --global --add-source ./release --prerelease
```

No environment variables and no local services. The only state is the configuration file.
Managed folders need `git` on the `PATH`; pushes use whatever credentials the user's git
already has.

### Configuration

Everything the tool stores lives in one hidden folder named after the package id:

| Path | Holds |
|---|---|
| `%USERPROFILE%\.grdev.tasks-cli\config.json` | The monitored folders and their scan rules |

The file is created with defaults on first use. `tasks config path` prints its location and
`tasks config edit` opens it in `$VISUAL`/`$EDITOR`; `tasks folders add`, `folders manage` and
`folders unmanage` are the other commands that write to it. Per-folder settings — file patterns,
todo prefixes, the due-date/tag/project/priority regexes, excluded folders, and a managed
folder's section headings and git settings — are hand-edited there.

The folder name is defined once, in [`src/Tasks/UserStorage.cs`](src/Tasks/UserStorage.cs),
and must stay in step with `PackageId` in `Tasks.csproj`.

## Layout

Standard grdev layout ([AGENTS.md](AGENTS.md)), partially populated. `scripts/` and `docs/` do
not exist yet — each is created when it first holds something. `skills/` is the one addition to
the standard layout.

| Path | Contains |
|---|---|
| `src/Tasks` | The whole tool — the only project in the solution |
| `src/Tasks/Commands` | One static class per command group (`folders`, `todo`, `tag`, `project`, `gtd`), each returning the `System.CommandLine` commands it owns |
| `src/Tasks/Config` | The config model (`TasksConfig`, `MonitoredFolder`) and `ConfigurationManager`, which loads and saves it |
| `src/Tasks/Todo` | The `Todo` record and `TodoManager`, which does the scanning and the regex extraction; `TodoLine`, `TaskId` and `DueDate`, the pure rules for composing and editing a line |
| `src/Tasks/Managed` | Managed folders: `TaskFile` (the file's sections), `ManagedFolders` (finding folders and tasks), `ManagedWriter` (the pull → edit → commit → push cycle) and `GitClient` |
| `specs` | `managed-folders.md` — the only spec so far |
| `tests` | `Tasks.UnitTests` and `Tasks.IntegrationTests` |
| `skills/tasks` | `SKILL.md`, the agent-facing guide. Not part of the standard layout — it is embedded into the assembly by `Tasks.csproj` and printed by `tasks skill`, so the file that ships is the file that is versioned. Edit it here, never in a copy |
| `.github/workflows` | `publish-nuget.yml` — packs and pushes on a push to `release/production` |

The project folder and assembly are `Tasks`; the packaged tool is `grdev.tasks-cli` and the
command is `tasks`.

`TODO.md` at the root is the author's own backlog for the tool, not a sample of the format.

## Stack

| Concern | Choice | Note |
|---|---|---|
| Platform | .NET | `net10.0` |
| CLI parsing | `System.CommandLine` 2.0.7 | Per the standard's preferred packages. Commands are built in `Program.Main` from the `Generate*Commands()` factories |
| JSON | `System.Text.Json` | Per the standard. The config model carries explicit `[JsonPropertyName]` attributes — the on-disk names are camelCase and are a compatibility surface for existing users' files |
| Versioning | `Nerdbank.GitVersioning` 3.10.91 | Referenced from `Directory.Build.props`, so every project gets it. Version comes from `version.json` plus git height |
| Scanning | `System.Text.RegularExpressions` | Every field a todo line can carry is extracted by a regex the user can override per folder |
| Git | The `git` executable | Run as a child process, not through a git library, so the user's SSH keys, credential manager, hooks and config all apply unchanged |

`TodoManager.Todos` is populated once by a static constructor, so the scan happens on first
access and the result is fixed for the life of the process. That is adequate for a CLI that
does one thing and exits.

## Tests

| Project | Covers |
|---|---|
| `tests/Tasks.UnitTests` | The pure line and file rules: `TodoLine`, `TaskId`, `DueDate`, `TaskFile` |
| `tests/Tasks.IntegrationTests` | The built tool run as a separate process against throwaway git repositories with a bare remote, with `HOME` pointed at a temp folder so the real config is never touched. Needs `git` on the `PATH` |

The scanning in `TodoManager` is still not covered: it reads the disk directly and takes its
configuration from the static `ConfigurationManager`.

## Never

- **Never run `nbgv set-version`.** It rewrites `version.json` from scratch and silently
  drops the `publicReleaseRefSpec` and `cloudBuild` settings this repo relies on. To change
  the major or minor, hand-edit the `version` field.
- **Never add a `<Version>` element** to `Directory.Build.props` or a `.csproj` — it
  overrides the version Nerdbank.GitVersioning computes.
- **Never change `UserStorage.FolderName` without changing `PackageId`**, or the reverse.
  They are the same name, and moving the folder strands every existing user's configuration.
- **Never write to a monitored folder that is not managed**, other than the one line
  `todo add <description> <file>` appends to the file it is given. Users point this tool at
  their real notes; changing existing lines is only for the tasks file of a folder they have
  opted in to managing.
- **Never stage or commit anything but the tasks file** in a managed folder's repository. The
  user may have other work staged there.
- **Never assume the config file exists or is complete.** It is created on demand, and a
  user hand-edits it.

## Decisions

### 2026-09-24

- **Managed folders.** A monitored folder can be marked managed (`managed` in its config); the
  tool then adds, edits, completes and removes tasks in its tasks file. Folders that are not
  managed stay read-only.
- The tasks file defaults to **`tasks/tasks.md`** and is configurable per folder
  (`managed.tasksFile`). New tasks go under `## Open`.
- **Every write is committed and pushed**: pull (rebase, autostash) → edit → commit only the
  tasks file → push. Push can be turned off per folder (`managed.git.push`). `tasks sync`
  retries what did not reach the remote. Exit code `3` means written but not synced.
- **Task ids** are an eight-character hash of the task's text, written `{id: abcd-efgh}`, fixed
  when the task is added. A task can be referred to by any unique prefix of four or more
  characters, or by `file:line`.
- **Completing a task** ticks it, adds `{done-date: yyyy-MM-dd}` and moves it to `## Done`.
- Git is run as the `git` executable, not through a library.
- `todo add`, `todo done`, `todo edit`, `todo rm`, `todo open`, `sync`, `folders manage` and
  `folders unmanage` are the command surface for managed folders; `todo add` takes `--due`,
  `--tag`, `--project` and `--priority`.
- Folder names must be unique.

### 2026-09-08

- `get-config-path` was replaced with a `config` command group — `config path` (same
  behaviour) and a new `config edit`, which opens the file in `$VISUAL`/`$EDITOR`, per the
  refreshed grdev standard requiring at least these two subcommands on every tool that reads
  a config file.

### 2026-08-13

- Adopted the grdev agentic standard; `AGENTS.md` is synced from `greek-developer/agentic`
  and is never edited locally. Project-specific context lives here.
- Line endings are pinned to **CRLF** through `.gitattributes` and `.editorconfig`, with
  `.github/workflows/**` kept at LF so `run:` blocks work on a Linux runner.
- Everything the tool stores moved to **`%USERPROFILE%\.grdev.tasks-cli\`**, named for the
  package id per the standard's "Where a tool stores things". The folder name lives in
  `UserStorage`, next to the comment tying it to `PackageId`.
- **Existing users' `~/.tasks` is not migrated by the tool.** The old folder stays where it
  is and the tool behaves as if it had never been configured; the folder must be moved to
  `~/.grdev.tasks-cli` by hand.
- **The scan covers every monitored folder that exists on disk**, aggregated into one list.
  A configured folder that is missing is skipped and the remaining folders are still scanned.
- **A file is read once per scan, keyed on its full path.** Monitored folders may be nested,
  and the first folder in config order to reach a file supplies the settings its todos are
  parsed with.

### 2026-09-01

- `PackageProjectUrl` points at this tool's page on the blog,
  `https://greekdeveloper.com/tools/tasks-cli/`, so the NuGet listing and the site agree on one
  canonical address.
- `RepositoryUrl` is published for every tool, private repository or not.
- `README.md` carries the project-page link and the install command line.
