# grdev.tasks-cli

A .NET command-line tool for managing tasks in .txt or .md files. The tool monitors multiple folders and gather the tasks based on common prefixes (configurable). Tasks can be filtered by tags, and can be displayed in a GTD-compatible list (`tasks gtd`)

**Project page:** [greekdeveloper.com/tools/tasks-cli](https://greekdeveloper.com/tools/tasks-cli/)

## Features
- List todos, and append new ones to a file
- Keep a git-synced task list: add, edit, complete and remove tasks from the command line, with every change committed and pushed
- Organize tasks by project, tag, and folder
- Configuration management for custom workflows
- Extensible command structure

## Prerelease Disclaimer

This is a pre-release version so the functionality is not yet refined and there may be bugs. Monitored folders are read-only: apart from its own config file, the tool only appends the line `tasks todo add` is given to the file it names. The exception is a folder you mark as **managed** — there the tool owns one tasks file (`tasks/tasks.md`) and edits, completes and removes tasks in it, committing and pushing each change.

## Getting Started


### Prerequisites
- [.NET 9.0 or 10.0 SDK](https://dotnet.microsoft.com/download)

### Install

`dotnet tool install --global grdev.tasks-cli`

### Setup 

Add one or more folders for the tool to monitor

`tasks folders add <path> [--name <name>]`

Folder options (file patterns to scan, tasks prefixes, etc) can be configured in the configuration file. Use `config path` to get the path to the configuration file, or `config edit` to open it directly.

`tasks config path`
`tasks config edit`

### A git-synced task list

Mark a folder inside a git repository as managed, and add tasks to it from anywhere:

```
tasks folders manage notes --default
tasks todo add "renew passport" --due fri --tag admin --priority A
tasks todo done 23ph
```

Each task gets a short id (`{id: 23ph-s8z5}`) you can refer to by its first four characters. Every change pulls, commits the tasks file alone, and pushes.

## Available Commands

### Todo Commands
- `todo list` — List all todos, optionally filter by tags (`--tags tag1,tag2`).
- `todo add <description> [--folder <name>] [--due <date>] [--tag <tag>] [--project <p>] [--priority <X>]` — Add a task to a managed folder's tasks file, then commit and push it. `--due` takes `yyyy-MM-dd`, `today`, `tomorrow`, `+3d`, `+2w` or a weekday.
- `todo add <description> <filePath> [options]` — Append a todo line to any file instead. The folder must exist; the file is created if it does not. Nothing is committed.
- `todo done <id>` — Tick a task, add `{done-date: …}` and move it to `## Done`.
- `todo edit <id> [--text] [--due | --no-due] [--add-tag] [--remove-tag] [--project | --no-project] [--priority | --no-priority]` — Change a task.
- `todo rm <id>` — Delete a task.
- `todo open <id>` — Open the tasks file in your editor at the task's line.
- `sync [--folder <name>]` — Commit hand edits to the tasks files, pull, and push.

### Tag Commands
- `tag list` — List all tags used in todos.

### Project Commands
- `project list` — List all projects associated with todos.

### GTD Commands
- `gtd inbox` — Show GTD Inbox tasks (no GTD tag).
- `gtd next` — Show tasks tagged as `next`.
- `gtd review` — Show tasks tagged as `review`.
- `gtd backlog` — Show tasks tagged as `backlog`.
- `gtd` — Show a summary of all GTD lists.

### Folder Commands
- `folders list` — List all monitored folders.
- `folders add <path> [--name <name>] [--managed] [--tasks-file <path>] [--no-push] [--default]` — Add a monitored folder, optionally managed.
- `folders manage <name> [--tasks-file <path>] [--no-push] [--default]` — Let the tool write tasks to a monitored folder in a git repository.
- `folders unmanage <name>` — Make a managed folder read-only again.

### Tool Commands
- `config path` — Print the full path to the config file.
- `config edit` — Open the config file in `$VISUAL`/`$EDITOR`.
- `version` — Print the version, commit and build time of this build, read from `ProductionVersion.json`.
- `skill` — Print the agent guide embedded in the tool. `tasks skill > SKILL.md` reproduces [`skills/tasks/SKILL.md`](skills/tasks/SKILL.md) byte for byte.

## Versioning

Versions are computed by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning)
from [`version.json`](version.json) plus the git height — there is no hardcoded version anywhere.
`version.json` holds the `major.minor`; the patch is the number of commits since that value last
changed, so **every commit bumps the patch automatically**.

Install the CLI once:

```powershell
dotnet tool install --global nbgv
```

### Viewing the version

```powershell
nbgv get-version                    # full summary for HEAD
nbgv get-version -v SimpleVersion   # just x.y.z, for scripts
nbgv get-version -f json            # everything, as JSON
```

### Setting the version

The patch bumps on its own with every commit. To change the major or minor, hand-edit the
`version` field in `version.json` and commit it — the patch count restarts from there:

```json
"version": "1.3"
```

Do **not** run `nbgv set-version`. It rewrites `version.json` from scratch and silently drops the
`publicReleaseRefSpec` and `cloudBuild` settings this repo relies on. Never add a `<Version>`
element to `Directory.Build.props` or a `.csproj` either — it would override the computed version.

### Releases

A build from `release/production` is a public release and gets a clean version (`1.3.4`). Every
other branch is a prerelease and gets a commit-id suffix (`1.3.4-g1a2b3c4`). Pushing to
`release/production` triggers the [publish workflow](.github/workflows/publish-nuget.yml).

## License
MIT License
