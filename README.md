# grdev.tasks

A .NET command-line tool for managing tasks in .txt or .md files. The tool monitors multiple folders and gather the tasks based on common prefixes (configurable). Tasks can be filtered by tags, and can be displayed in a GTD-compatible list (`tasks gtd`)

## Features
- Add, list, and complete todos
- Organize tasks by project, tag, and folder
- Configuration management for custom workflows
- Extensible command structure

## Prerelease Disclaimer

This is a pre-release version so the functionality is not yet refined and there may be bugs. Currently the application only reads tasks and does not modify files in any way (except it's own config file)

## Getting Started


### Prerequisites
- [.NET 9.0 or 10.0 SDK](https://dotnet.microsoft.com/download)

### Install

`dotnet tool install --global grdev.tasks`

### Setup 

Add one or more folders for the tool to monitor

`tasks folders add <path> [--name <name>]`

Folder options (file patterns to scan, tasks prefixes, etc) can be configured in the configuration file. Use `get-config-path` to get the path to the configuration file.

`tasks get-config-path`

## Available Commands

### Todo Commands
- `list` — List all todos, optionally filter by tags (`--tags tag1,tag2`).

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



## License
MIT License
