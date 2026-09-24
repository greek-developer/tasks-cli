---
name: tasks
description: >-
  Read and write plain-text todos with the `tasks` CLI (grdev.tasks-cli), which collects todo
  lines out of the `.md`, `.txt` and `.todo` files in the folders it monitors — listing what is
  open, filtering by tag, showing the GTD views (inbox, next, review, backlog), naming the tags
  and projects in use, and adding, completing, editing and removing tasks in a managed folder's
  git-synced tasks file. Use whenever the user asks about their todos or wants one written
  down or changed, including phrasings like "add a todo", "what's on my list", "what's due",
  "what's in my inbox", "what should I work on next", "put that on the backlog", "remind me to
  …", "mark that done", "push that to Friday", or "which projects am I tracking". Also use when
  choosing which folders are scanned, or when a `tasks` command printed nothing and the reason
  needs explaining.
---

# Working with plain-text todos

The `tasks` CLI does not own a database. It reads todo lines out of files the user already
keeps — notes, READMEs, a `todo.md`. Monitored folders are read-only, except a **managed**
folder: there the tool owns one tasks file (`tasks/tasks.md` by default) and commits and pushes
every change it makes to it. Nothing prompts,
everything is an argument, results go to stdout and complaints go to stderr, so an agent can
drive it end to end.

There is no `--json`. Output is human text with one machine-readable part: every todo is
printed as `<the line as written> (-> <file>:<line>)`. Use that suffix to open, quote or edit
the todo; do not try to parse the rest.

## Check the tool is there

```bash
tasks version
```

Not found → `dotnet tool install --global grdev.tasks-cli`.

## What counts as a todo

A line is a todo when — after leading whitespace — it starts with one of the folder's
configured prefixes. The defaults are:

```text
- [ ] pay the invoice
[ ] pay the invoice
TODO pay the invoice
//TODO pay the invoice
```

The whole line is the description, markers included. Three pieces of inline syntax are read
out of it:

| Written in the line | Means | Notes |
|---|---|---|
| `#word` | a tag | `#next`, `#review` and `#backlog` are what drive the GTD views |
| `@word` | a project | Only reported, never filtered on |
| `{due: 2026-09-01}` | a due date | `yyyy-MM-dd` or `yyyy/MM/dd`. Anything else is treated as undated |
| `{pri: A}` | a priority | One capital letter |
| `{id: 23ph-s8z5}` | the task's id | Only on lines in a managed tasks file; how you refer to the task |

Only `.md`, `.txt` and `.todo` files are scanned, and `node_modules`, `.git` and dot-folders
are skipped. All of this is per-folder configuration — see the config file below.

## Which folders are read

```bash
tasks folders list                       # what is monitored today
tasks folders add <path> [--name <name>] # start monitoring a folder (read-only)
tasks folders manage <name> [--default]  # let the tool write tasks there; must be in a git repo
tasks folders unmanage <name>            # read-only again
tasks config path                        # the config file, for the patterns above
tasks config edit                        # open it in $VISUAL/$EDITOR
```

Every monitored folder is scanned and the results are pooled into one list. Two things worth
knowing before you conclude the user has nothing to do:

- **A configured folder that is not on this machine is skipped without a word.** Nothing is
  printed and no command fails, so a path that has moved or was never checked out simply
  contributes nothing. `tasks folders list` prints the configured paths, not whether they
  exist — check the suspect ones yourself before reporting a short list as fact.
- An empty list far more often means *no folder is monitored* or *the lines do not start with
  a recognised prefix* than it means the user is done. Check `tasks folders list` first.

Folders may be nested — one monitored folder living inside another is fine. Each file is read
once, so a todo in the overlap is reported once, under the settings of whichever folder is
listed first in the config.

The config file is plain JSON and is created with defaults on first run. Editing it by hand is
the supported way to change file patterns, prefixes, the due-date pattern or the excluded
folders — there is no command for that.

## Reading

```bash
tasks todo list                  # everything: dated first by due date, then undated
tasks todo list --tags work,home # todos carrying ANY of those tags, not all of them
tasks tag list                   # every tag in use, printed without the #
tasks project list               # every project in use, printed with the @
```

`--tags` (short `-t`) takes a comma-separated list and is an OR. **Pass the bare names** —
`--tags work`, never `--tags #work`, which matches nothing. There is no way to ask for the
intersection, no text search and no filter by project or due date — read the list and reason
over it yourself.

## The GTD views

```bash
tasks gtd          # a summary of all four, truncated
tasks gtd next     # tagged #next
tasks gtd review   # tagged #review
tasks gtd backlog  # tagged #backlog
tasks gtd inbox    # tagged with none of the three - the untriaged pile
```

Each view hides todos whose due date is further out than its horizon; **undated todos always
appear**. The summary is deliberately short, so use the single view when the user asks what is
actually in one:

| View | Summary shows | Single view shows |
|---|---|---|
| `next` | due within 3 days, max 10 | due within 30 days, all |
| `review` | due within 7 days, max 10 | due within 30 days, all |
| `inbox` | due within 30 days, max 5 | due within a year, all |
| `backlog` | due within a year, max 5 | due within a year, all |

Overdue todos sort first, then today's, then undated, then the rest.

## Adding one

To a managed folder — the normal case:

```bash
tasks todo add "call the accountant" --due fri --tag work --project taxes --priority A
tasks todo add "call the accountant" --folder notes      # when several folders are managed
```

The description is **one shell argument** — quote it. The task lands at the end of `## Open` in
the folder's tasks file with a fresh `{id: …}`, and is committed and pushed. The folder is the
one named by `--folder`, else the configured default, else the only managed folder; with
several managed folders and no default the command refuses, and `tasks folders list` shows which
is which.

`--due` takes `yyyy-MM-dd`, `today`, `tomorrow`, `+3d`, `+2w` or a weekday (`fri` = the next
Friday after today); the absolute date is written. `--tag` repeats or takes a comma list. Tags
and projects are passed without `#` / `@`.

To a file of the user's that is not managed, name the file instead of a folder:

```bash
tasks todo add "call the accountant" ~/notes/todo.md --tag work
```

That appends one line at the end of the file — no id, no commit. The file is created if it is
missing, but its folder must already exist.

On success both forms print the new line and its location, as `tasks todo list` shows it.

## Changing one

Only tasks in a managed tasks file can be changed. Refer to a task by its id — the whole
`23ph-s8z5`, or any unique first four or more characters (`23ph`) — taken from the
`{id: …}` in `tasks todo list` output. A line written by hand without an id can be addressed as
`<tasks file>:<line>`, but that is refused if the pull brought in changes; list again and use
what it prints.

```bash
tasks todo done 23ph                     # tick, add {done-date: today}, move to ## Done
tasks todo edit 23ph --due +1w --add-tag waiting --priority B
tasks todo edit 23ph --text "call the accountant back"   # keeps tags, project, markers
tasks todo edit 23ph --no-due --remove-tag waiting --no-project --no-priority
tasks todo rm 23ph                       # delete the line
tasks todo open 23ph                     # the user's editor, at that line
```

Every change pulls first, then commits just the tasks file (`complete task: …`, `edit task: …`
…) and pushes. Nothing else in the user's repository is staged or committed.

`tasks sync` commits any uncommitted change to the tasks files (hand edits included), pulls
and pushes, for every managed folder or just `--folder <name>`.

## Exit codes

| Code | Meaning | Do this |
|---|---|---|
| `0` | Done — for a managed folder, committed and pushed | The printed `(-> file:line)` is where it landed |
| `1` | Rejected or failed, or the command line was wrong | Read stderr; nothing was written |
| `3` | Written, but not committed or not pushed | Tell the user what stderr says; `tasks sync` finishes it once the cause (network, credentials, a rejected push) is fixed |

Commands refuse rather than half-write: a bad date, an ambiguous id, a failed pull, a merge or
rebase in progress, or a branch with no upstream all exit `1` and leave the file untouched.

## Never

- **Never edit a todo outside a managed tasks file**, with the CLI or by hand. Todos in other
  files belong to the user's notes; `done`/`edit`/`rm` refuse them, and so should you. Say
  where the todo is and let the user change it.
- **Never edit a managed tasks file directly** when a `tasks todo` command does the job — the
  command pulls, keeps the id and commits; a hand edit does none of that until `tasks sync`.
- **Never report a change as synced on exit code `3`.** It is on disk, not on the remote.
- **Never `git push --force`, reset or rebase the user's repository** to get past a failed
  pull or push. Report it and stop.
- **Never guess a task id.** Take it from `tasks todo list`; if a prefix is ambiguous, use more
  of it.
- **Never invent the path for `tasks todo add <file>`.** Prefer a managed folder; otherwise take
  the path from `tasks folders list`, or from the `(-> file:line)` of a todo that already
  exists. A plausible-looking wrong path inside an existing folder will happily create a new
  file nobody reads.
- **Never report an empty list as "nothing to do"** without checking `tasks folders list` — and
  remember a configured folder that is missing from this machine is skipped silently.
- **Never treat `--tags a,b` as "both a and b".** It matches either; filter further yourself.
- **Never add a folder full of source or build output** to widen the search. The scan walks
  every `.md`, `.txt` and `.todo` under it and picks up every `TODO` comment marker it finds.
