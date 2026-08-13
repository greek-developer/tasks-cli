# BRIEF.md — tasks-cli

## Overview

A command-line tool that gathers todo items already written in plain `.txt` and `.md` files
scattered across a developer's machine, and reports them as one list. It watches a set of
configured folders, scans them for lines that begin with a todo prefix (`- [ ]`, `[ ]`,
`TODO`, `//TODO`), and extracts a due date, tags and projects from each line by regular
expression.

The point is that the files stay the source of truth. Notes, READMEs and scratch files keep
holding the tasks in whatever form their author already uses; this tool only reads them and
presents the result — filtered by tag, grouped by project, or arranged as the GTD lists
(inbox / next / review / backlog). Nothing is imported into a database and no separate task
store is introduced.

It is **pre-release** and, apart from its own configuration file, currently **only reads** —
no command writes to a monitored file.

## Build & run

```powershell
dotnet build                                       # build everything
dotnet build -c Release                            # release build
dotnet test                                        # run all tests (see Tests - none yet)
dotnet run --project src/Tasks -- get-config-path  # run the CLI
dotnet pack -c Release                             # produce the global tool into ./nupkg
```

Install the packed tool locally to exercise it as users will:

```powershell
dotnet tool install grdev.tasks-cli --global --add-source ./nupkg --prerelease
```

No environment variables, no local services, no credentials. The only state is the
configuration file.

### Configuration

Everything the tool stores lives in one hidden folder named after the package id:

| Path | Holds |
|---|---|
| `%USERPROFILE%\.grdev.tasks-cli\config.json` | The monitored folders and their scan rules |

The file is created with defaults on first use. `tasks get-config-path` prints its location;
`tasks folders add <path> [--name <name>]` is the only command that writes to it. Per-folder
settings — file patterns, todo prefixes, the due-date/tag/project/priority regexes, excluded
folders — are hand-edited there, since nothing exposes them as options.

The folder name is defined once, in [`src/Tasks/UserStorage.cs`](src/Tasks/UserStorage.cs),
and must stay in step with `PackageId` in `Tasks.csproj`.

## Layout

Standard grdev layout ([AGENTS.md](AGENTS.md)), partially populated. `tests/`, `scripts/`,
`docs/` and `specs/` do not exist yet — each is created when it first holds something.

| Path | Contains |
|---|---|
| `src/Tasks` | The whole tool — the only project in the solution |
| `src/Tasks/Commands` | One static class per command group (`folders`, `todo`, `tag`, `project`, `gtd`), each returning the `System.CommandLine` commands it owns |
| `src/Tasks/Config` | The config model (`TasksConfig`, `MonitoredFolder`) and `ConfigurationManager`, which loads and saves it |
| `src/Tasks/Todo` | The `Todo` record and `TodoManager`, which does the scanning and the regex extraction |
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

`TodoManager.Todos` is populated once by a static constructor, so the scan happens on first
access and the result is fixed for the life of the process. That is adequate for a CLI that
does one thing and exits.

## Tests

**There is no test project yet.** `dotnet test` succeeds with nothing to run. When one is
added it goes in `tests/Tasks.UnitTests` (xUnit) per the standard, and the natural first
targets are the pure parts of `TodoManager` — due-date, tag and project extraction — which
need the line and the pattern only.

The scanning code currently reads the disk directly and takes its configuration from the
static `ConfigurationManager`, so covering it means passing the folder configuration and a
file source in rather than reaching for them.

## Never

- **Never run `nbgv set-version`.** It rewrites `version.json` from scratch and silently
  drops the `publicReleaseRefSpec` and `cloudBuild` settings this repo relies on. To change
  the major or minor, hand-edit the `version` field.
- **Never add a `<Version>` element** to `Directory.Build.props` or a `.csproj` — it
  overrides the version Nerdbank.GitVersioning computes.
- **Never change `UserStorage.FolderName` without changing `PackageId`**, or the reverse.
  They are the same name, and moving the folder strands every existing user's configuration.
- **Never make a command write to a monitored file** without that being a deliberate,
  recorded decision. Users point this tool at their real notes on the strength of it being
  read-only; `todo add` is a stub for exactly that reason.
- **Never assume the config file exists or is complete.** It is created on demand, and a
  user hand-edits it.

## Decisions

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
