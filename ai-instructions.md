## Specifications:
This is a text-based todo management cli tool.

It will be installed as a dotnet global tool

It must support a configuration file in user profile

It must support commands

Commands

--config displays the full path to the config file (in user's profile)
also creates the file if not exists

--add-current-folder [optinal parameter: friendly name]

adds the current folder as a folder in the list of monitored folders


--list-todos (default, used if no argument)

lists all todos from all monitored folders organized by
(A) monitored folder, (B) file name and order by position in file


--list-due
lists all todos from all monitored folders organized by due date distance

it should use the following seperators
due date passed
due date today
due date in the next 7 days
due date in the future after 7 days

each monitored folder should have a "friendly name" (same as the last directory if not defined)

--add [friendly name] {due-date} [description] will add the todo in the todo.md file on the foor of the folder indentified by friendly name


in the configuration file the following section must exists
a. global it will define global settings for all folders
b. an array of monitored folders
  each monitored folder will be represented as a json object that will contain:
  (a) folder path
  (b) friendly name
c both global and folders will contain the following settings (optional for folders)  
 - file pattern (default *.txt|*.md)
 - todo detection regex (defaults should match: - [ ], [ ],//TODO, TODO,)
 - due date pattern, regex, default should extract {due: YYYY/MM/DD}
 - tag pattern  : (default: #tag)
 - project pattern : default: @
 - priority patter : default {priority:}
 - priorities should be single letter from A to Z
 - default todo filename: todo.md

the tool should support listing from any file in any of the monitored folders and adding to the default filename on the root of every project.

due dates in YYYY/MM/DD or YYYY-MM-DD 

the tool must scan subfolders

## Implementation: 
Use: System.CommandLine for command line argument parsing

project folder should be .\src\todo-cli\todo-cli.csproj