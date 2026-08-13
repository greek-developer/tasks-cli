# grdev.tasks-cli

A .NET command-line tool for managing tasks in .txt or .md files. The tool monitors multiple folders and gather the tasks based on common prefixes (configurable). Tasks can be filtered by tags, and can be displayed in a GTD-compatible list (`tasks gtd`)

## Features
- List todos, and append new ones to a file
- Organize tasks by project, tag, and folder
- Configuration management for custom workflows
- Extensible command structure

## Prerelease Disclaimer

This is a pre-release version so the functionality is not yet refined and there may be bugs. Apart from its own config file, the only file the tool writes is the one named by `tasks todo add` — and it only ever appends a line. There is no command that edits, completes or removes an existing todo.

## Getting Started


### Prerequisites
- [.NET 9.0 or 10.0 SDK](https://dotnet.microsoft.com/download)

### Install

`dotnet tool install --global grdev.tasks-cli`

### Setup 

Add one or more folders for the tool to monitor

`tasks folders add <path> [--name <name>]`

Folder options (file patterns to scan, tasks prefixes, etc) can be configured in the configuration file. Use `get-config-path` to get the path to the configuration file.

`tasks get-config-path`

## Available Commands

### Todo Commands
- `todo list` — List all todos, optionally filter by tags (`--tags tag1,tag2`).
- `todo add <description> <filePath>` — Append a todo line to a file. The folder must exist; the file is created if it does not. Write `#tags`, `@projects` and `{due: yyyy-MM-dd}` inside the description.

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
- `folders add <path> [--name <name>]` — Add a monitored folder.

### Tool Commands
- `get-config-path` — Print the full path to the config file.
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
