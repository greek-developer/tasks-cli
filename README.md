# grdev.tasks

A .NET command-line tool for managing tasks using the Getting Things Done (GTD) methodology. This project provides commands for handling todos, projects, tags, and folders, making it easy to organize and track your work from the terminal.

## Features
- Add, list, and complete todos
- Organize tasks by project, tag, and folder
- Configuration management for custom workflows
- Extensible command structure

## Getting Started


### Prerequisites
- [.NET 9.0 or 10.0 SDK](https://dotnet.microsoft.com/download)

### Install

`dotnet tool install --global grdev.tasks`

### Setup 

Add one or more folders for the tool to monitor
`tasks folders add <path> [--name <name>]`

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
