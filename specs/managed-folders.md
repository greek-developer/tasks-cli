# Managed folders

A monitored folder is read-only by default: the tool scans it and never changes it. A
**managed** folder is one the user has opted in to letting the tool write to. It has one tasks
file where tasks are added, edited, completed and removed from the command line, and the
folder's git repository is kept in step with its remote on every change.

## Marking a folder managed

- `tasks folders add <path> --managed` adds a folder already managed; `tasks folders manage
  <name>` manages a folder already monitored; `tasks folders unmanage <name>` makes it read-only
  again without removing it.
- A folder can be managed only if it is inside a git work tree. Otherwise the command refuses.
- `--tasks-file <relative path>` sets where the tasks file lives. The default is
  `tasks/tasks.md`, the location the grdev standard gives for task tracking.
- `--no-push` commits each change without pushing it.
- `--default` makes the folder the one new tasks go to when no folder is named.
- `tasks folders list` marks managed folders with their tasks file, whether they push, and
  which one is the default.

Folder names are unique; adding a second folder with a name already in use is refused.

## The tasks file

```markdown
# Tasks

## Open

- [ ] renew passport #admin @life {due: 2026-10-15} {pri: A} {id: 23ph-s8z5}

## Done

- [x] call bob #work {done-date: 2026-09-24} {id: wyvp-m9pw}
```

- The file and its folder are created on the first write, with a `# Tasks` heading.
- New tasks go at the end of the open section (`## Open`); completed tasks go at the end of
  the done section (`## Done`). A missing section is created, the open section ahead of the
  done section. Both headings are configurable per folder.
- Text between tasks, subsections and anything else in the file is kept as it is.
- Line endings follow the file's existing ones.
- Tasks are written in the default syntax: `#tag`, `@project`, `{due: yyyy-MM-dd}`,
  `{pri: X}`, `{done-date: yyyy-MM-dd}` and `{id: xxxx-xxxx}`. The id always comes last.

## Task ids

Every task the tool adds to a tasks file gets an id: eight characters hashed from the task's
text, written as `abcd-efgh`. The characters come from Crockford's base32 alphabet, which has
no `i`, `l`, `o` or `u` that could be misread. If another task in the same file already has the
same id, the id is made again with a salt. The id is fixed when the task is added and does not
change when the text is edited.

A task is referred to by its full id or by any unique prefix of at least four characters, with
or without the dash, in any case (`23ph`, `23PH-S8`, `23phs8z5`). A reference that matches more
than one task, across all managed folders, is refused.

A task can also be referred to as `<tasks file>:<line>`. This is how a line written by hand
without an id is reached. It is refused if pulling changed the file, since the line number may
then point at a different task.

## Adding

```
tasks todo add <description> [--folder <name>] [--due <date>] [--tag <tag>]… [--project <p>] [--priority <X>]
```

- The folder is the one named by `--folder`, else the configured default, else the only managed
  folder. If none of these applies, or there are several managed folders and no default, the
  command is refused.
- `--due` accepts `yyyy-MM-dd`, `today`, `tomorrow`, `+Nd`, `+Nw`, or a weekday name (`mon` …
  `sunday`), meaning the next such day after today. The absolute date is written.
- `--tag` can be repeated or given a comma-separated list. Tags and projects may be written with
  or without `#` / `@`. A tag already in the description is not added again.
- `--priority` is a single letter; it is upper-cased.
- The line starts with the folder's first configured todo prefix (`- [ ]` by default). A prefix
  typed into the description is replaced by it.
- On success the new line is printed with its location, as `todo list` shows it.

Given a file path instead of a folder, `todo add` appends to that file as it always has. The
field options still apply, but no id is added and nothing is committed.

## Completing, editing, removing, opening

```
tasks todo done <task>
tasks todo edit <task> [--text <t>] [--due <date> | --no-due] [--add-tag <t>]… [--remove-tag <t>]…
                       [--project <p> | --no-project] [--priority <X> | --no-priority]
tasks todo rm <task>
tasks todo open <task>
```

- `done` ticks the checkbox (`- [ ]` → `- [x]`; a `TODO`-style prefix becomes `- [x]`), adds
  `{done-date: <today>}`, and moves the line to the end of the done section. A task that is not
  open is refused.
- `edit` changes only what is asked. `--text` replaces the free text but keeps the tags,
  project and markers already on the line. An edit that would change nothing is refused.
- `rm` deletes the line, whether the task is open or done.
- `open` opens the tasks file in `$VISUAL` / `$EDITOR` at the task's line. It changes nothing
  and syncs nothing.
- These commands act only on the tasks file of a managed folder. Any other file is refused.

## Keeping in step with the remote

Every write follows the same sequence:

1. Refuse if a merge or rebase is in progress in the repository.
2. Pull with `--rebase --autostash`, unless `pullBeforeWrite` is off. If the branch has no
   upstream, or the pull fails, the command is refused and nothing is written. A rebase stopped
   by a conflict is aborted.
3. Make the change to the file as it is after the pull.
4. Stage and commit **only the tasks file**, with a message of the form `add task: …`,
   `complete task: …`, `edit task: …` or `remove task: …`. Anything else the user has staged
   stays staged and stays out of the commit.
5. Push, unless the folder is set not to.

Only one `tasks` write runs at a time in a repository. Another one waits up to ten seconds for
it to finish, then gives up.

`tasks sync [--folder <name>]` catches a managed folder up with its remote: it commits any
uncommitted change to the tasks file (`update tasks`), which includes edits made by hand, then
pulls, then pushes. Without `--folder` it does this for every managed folder and carries on
past a folder that fails.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Done, including the commit and push |
| `1` | Refused or failed; the tasks file was not changed |
| `3` | The tasks file was changed but the change was not committed or not pushed; `tasks sync` finishes it once the cause is fixed |

## Configuration

```json
{
  "defaultFolder": "notes",
  "folders": [
    {
      "path": "C:\\notes",
      "friendlyName": "notes",
      "managed": {
        "tasksFile": "tasks/tasks.md",
        "openSection": "## Open",
        "doneSection": "## Done",
        "git": { "pullBeforeWrite": true, "push": true }
      }
    }
  ]
}
```

A folder without `managed` is read-only.
