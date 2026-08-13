---
name: tasks
description: >-
  Read and write plain-text todos with the `tasks` CLI (grdev.tasks-cli), which collects todo
  lines out of the `.md`, `.txt` and `.todo` files in the folders it monitors — listing what is
  open, filtering by tag, showing the GTD views (inbox, next, review, backlog), naming the tags
  and projects in use, and appending a new todo to a file. Use whenever the user asks about
  their todos or wants one written down, including phrasings like "add a todo", "what's on my
  list", "what's due", "what's in my inbox", "what should I work on next", "put that on the
  backlog", "remind me to …", or "which projects am I tracking". Also use when choosing which
  folders are scanned, or when a `tasks` command printed nothing and the reason needs
  explaining.
---

# Working with plain-text todos

The `tasks` CLI does not own a database. It reads todo lines out of files the user already
keeps — notes, READMEs, a `todo.md` — and writes one back when asked. Nothing prompts,
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

Only `.md`, `.txt` and `.todo` files are scanned, and `node_modules`, `.git` and dot-folders
are skipped. All of this is per-folder configuration — see the config file below.

## Which folders are read

```bash
tasks folders list                       # what is monitored today
tasks folders add <path> [--name <name>] # start monitoring a folder
tasks get-config-path                    # the config file, for the patterns above
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

```bash
tasks todo add "call the accountant #work @taxes {due: 2026-09-01}" ~/notes/todo.md
```

The description is **one shell argument** — quote it. The line is appended at the end of the
file, prefixed with the folder's first configured marker (`- [ ] ` by default) unless the text
already starts with a marker itself. The file's existing line endings are matched, and the file
is created if it is missing — but its folder must already exist. On success the new line and
its location are printed, exactly as `tasks todo list` would show it.

Write the tags, project and due date into the description itself, in the syntax above; there
are no options for them.

## Exit codes

| Code | Meaning | Do this |
|---|---|---|
| `0` | Done | For `todo add`, the printed `(-> file:line)` is where it landed |
| `1` | Rejected or failed, or the command line was wrong | Read stderr; nothing was written |

`todo add` reports and refuses rather than half-writing: an empty description, a multi-line
description, a missing folder or an unwritable file all exit `1` and leave the file untouched.

## Never

- **Never tell the user a todo is done, closed or removed.** There is no complete, edit or
  delete command — this tool only reads and appends. Ticking `- [ ]` to `- [x]` means editing
  the user's own file, which is their call, not a side effect of a `tasks` command.
- **Never invent the path for `tasks todo add`.** Take it from `tasks folders list`, or from
  the `(-> file:line)` of a todo that already exists. A plausible-looking wrong path inside an
  existing folder will happily create a new file nobody reads.
- **Never report an empty list as "nothing to do"** without checking `tasks folders list` — and
  remember a configured folder that is missing from this machine is skipped silently.
- **Never treat `--tags a,b` as "both a and b".** It matches either; filter further yourself.
- **Never add a folder full of source or build output** to widen the search. The scan walks
  every `.md`, `.txt` and `.todo` under it and picks up every `TODO` comment marker it finds.
