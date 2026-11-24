# Whole-folder review checks: `commitfence review --all [<folder or project file>]`

Every case builds its own repository under `work\repos\<case>` from nothing, with its own
`.gitignore` (`bin/`, `obj/`) and a global git configuration inside the scratch folder. Unless a
case says otherwise the repository holds:

- `commitfence.json` with TEAM010 (no braces around a single-line `if`, blocking) and, in the
  layer-direction cases, a `structure` section;
- `.editorconfig` with `dotnet_diagnostic.IDE0044.severity = error` (make a field readonly);
- `src/Api/Api.csproj` and `src/Legacy/Legacy.csproj`, both net11.0, one library each.

"Old breaches" of `src/Legacy/Legacy.cs`: a private static field that is never reassigned (IDE0044,
one finding at the line of the field) and a method with a braced single-line `if` (TEAM010, one
finding at the line of the `if`).

Every whole review in every case also checks, on all of its output: the first line is
`CommitFence <version> (<program folder>)`; the words `push` and `changed` are not in it (the
wrong-use messages of case 9 are exempt, because the usage text names `pre-push`); and when a
`By rule:` line is there it is the line directly before the last (summary) line.

| Case | What is done | Exit | Printed |
| --- | --- | --- | --- |
| 1a | Api is clean; Legacy has the two old breaches. Commit 1 is pushed with `--no-verify`. Commit 2 appends a clean method to Legacy.cs, on lines far from the breaches, and is pushed through the real hook | hook 0, remote moved | none of the breaches printed |
| 1b | `review --all src/Legacy` | 1 | `src/Legacy/Legacy.cs(<line of the field>,` with `error IDE0044` and `src/Legacy/Legacy.cs(<line of the if>,` with `error TEAM010`; `By rule: IDE0044 1, TEAM010 1` directly before the summary; the first note is `the whole of src/Legacy at commit <10 characters of HEAD>: 2 files.` |
| 1c | `review --all src/Api` | 0 | no `IDE0044`, no `TEAM010`, no `Legacy.cs`; no `By rule:` line; first note `the whole of src/Api at commit <10>: 2 files.` (csproj and Service.cs); no `of no known language` sentence (a project folder holds only files the build looks at) |
| 2a | Api holds one TEAM010 breach, Legacy the two old ones; `review --all` | 1 | `By rule: TEAM010 2, IDE0044 1` (most frequent first); (the projects switch the .NET analyzers off, so that only the two rules count) the findings at their files and lines; summary `3 of 3 files reviewed, 2 of 2 builds checked: 3 blocking`; the note `4 files of no known language were not looked at.` (the four files at the top: .editorconfig, .gitignore, README.txt, commitfence.json); first note `the whole of the repository at commit <10>: <number of tracked files> files.` |
| 2b | the same repository, a second commit adds a clean file to Api; `review --base HEAD~1 --head HEAD` | 0 | no `By rule:` line (only a whole review prints it), and none of the old breaches |
| 3 | `review --all src/Legacy/Legacy.csproj` | 1 | the same finding lines as the folder, `src/Legacy` named in the first note, not the project file |
| 4a | current folder `src`, `review --all Legacy` | 1 | the same finding lines as `review --all src/Legacy` from the top |
| 4b | current folder `src/Api`, `review --all ../Legacy` | 1 | the same finding lines |
| 4c | current folder `src/Api`, `review --all` (no target) | 1 | first note says `the whole of the repository`, with all three findings |
| 5a | `loose/Loose.cs` (a TEAM010 breach) in a folder with no project, in a repository whose Api project exists; `review --all loose` | 1 | TEAM010 at `loose/Loose.cs(<line>,`; the coverage line `loose/Loose.cs: not reviewed: not covered: no project compiles this file`, as a range review of the commit that adds the file prints it today (same text, checked in the same case) |
| 5b | a repository with no project at all, one loose breaching `.cs` file; `review --all` | 1 | TEAM010 reported; the note `No C# project was found in this tree, so nothing was compiled.`; the file is `not reviewed: not covered`; blocked wins over incomplete |
| 6a | Legacy has used an Api type since the first commit (module `legacy` must not use module `api`; modules are the projects). Commit 2 adds a clean file to Api; push through the real hook | hook 0, remote moved | no `ARCH001`: the old forbidden use is not reported by a push |
| 6b | `review --all src/Legacy` | 1 | `src/Legacy/UsesApi.cs(<line>,` `error ARCH001` `Layer direction: legacy must not use api.`; `By rule: ARCH001 1` |
| 6c | `review --all src/Api` | 0 | no `ARCH001`; no `not run` |
| 7a | Legacy has a compile error (`int x = "text";`); `review --all src/Legacy` | 1 | `error CS0029` at `src/Legacy/Broken.cs(<line>,` |
| 7b | `src/Core` does not compile, `src/App` references it; `review --all src/App` | 2 | `Core (net11.0): not reviewed:` naming the project Core; no finding line on a file of Core (it is outside the target; the reason of the coverage line carries the compiler text); `App (net11.0): not reviewed: its prerequisite Core (net11.0)`; summary says the review is incomplete |
| 7c | the same repository, `review --all src/Core` | 1 | `error CS0029` in `src/Core/Broken.cs` |
| 8a | no uncommitted work: `review --all src/Legacy` | 1 | the note `The working folder holds changes that are not committed; they are not part of this review.` is not printed |
| 8b | an unstaged edit under `src/Api`: `review --all src/Legacy` | 1 | no note; `review --all src/Api` prints the note |
| 8c | under `src/Legacy` the working folder has the TEAM010 breach fixed (unstaged) and a new untracked file with a new TEAM010 breach | 1 | the note is printed exactly once; the finding is the committed breach at its committed line; the untracked file is not named |
| 8d | a staged-only change under `src/Api`: `review --all src/Api` | 1 | the note is printed |
| 9a | `review --all --hook pre-push o u` | 3 | a plain message, nothing reviewed |
| 9b | `review --all --staged` | 3 | the same |
| 9c | `review --all --base HEAD` | 3 | the same |
| 9d | `review --all --head HEAD` | 3 | the same |
| 9e | `review --all --files src/Api/Service.cs` | 3 | the same |
| 9f | `review --all src/Api src/Legacy` (two targets) | 3 | the same |
| 9g | target outside the repository (the folder that holds the repositories) | 3 | message says `outside the repository` |
| 9h | target that is not in the commit (`src/Nope`) | 3 | message says `has no files in the last commit` |
| 9i | target that exists only as an untracked folder | 3 | the same message |
| 9j | target is a source file (`src/Api/Service.cs`) | 3 | message says it is not a project file |
| 9k | a repository with no commit, `review --all` | 2 | the reason says `no commit` |
| 10 | an unstaged edit, an untracked file and a stash entry are present; whole reviews of `src/Legacy` and of the whole repository are run | 1 | before and after, the working folder (content of every file outside `.git`), `.git/index` (bytes), `.git/config`, HEAD, branches, tags, stash list and `git status` are identical |
| 11a | two projects `A` and `B` in the one folder `src/Shared` (each with its own intermediate folder at the top of the repository, because two projects that share an `obj` folder compile each other's generated files), both compiling the one `Code.cs` that has a TEAM010 breach; `review --all src/Shared` | 1 | both projects are judged: `2 of 2 builds checked`; the finding is printed once; `A (net11.0): reviewed` and `B (net11.0): reviewed` (first written as "naming both projects": wrong, a syntax rule's finding belongs to no project, so it names none) |
| 11b | `src/Api` compiles `..\Shared\Linked.cs` (a breach) from outside its own folder; `review --all src/Api` | 0 | the linked file is outside the target: no `Linked.cs` finding; summary `1 of 1 builds checked` |
| 11c | the same repository, `review --all src/Shared` | 1 | `src/Shared/Linked.cs(<line>,` `error TEAM010`: the file is in the target and the Api project that compiles it is built, so it is judged |

## Cases 12 to 14

| Case | What is done | Exit | Printed |
| --- | --- | --- | --- |
| 12a | `.gitattributes` holds `*.cs -diff`, so git writes every `.cs` file as `Binary files ... differ`. `src/Api/Service.cs` (committed) holds a TEAM010 breach; `review --all src/Api` | 1 | `src/Api/Service.cs(<line of the if>,` with `error TEAM010`; `By rule:` line holds TEAM010 |
| 12b | the same repository with the rule TEAM012 (the IClock rule: `System.DateTime.Now` is forbidden) and a committed `src/Api/Clock.cs` that reads `DateTime.Now` | 1 | `src/Api/Clock.cs(<line of DateTime.Now>,` with `error TEAM012`; the TEAM010 finding of 12a is printed too |
| 13 | `src/Api` is clean and holds a `Directory.Build.props`; `src/Legacy` breaks IDE0044 (set to error) and TEAM010; `review --all src/Api` | 0 | no finding, `Legacy` not named anywhere (Legacy is not built, so no coverage line), summary `1 of 1 builds checked` |
| 14a | `src/Api` is healthy; `src/Broken/Broken.csproj` is not well-formed XML; `review --all src/Api` | 0 | `Broken` not named anywhere; `1 of 1 builds checked` |
| 14b | the same repository, `review --all src/Broken` | 2 | a line that starts with `Broken: not reviewed:` and gives the reason (the project file could not be read); the summary says the review is incomplete |
| 14c | `src/App` references `src/Broken`; `review --all src/App` | 2 | a line that starts with `App (net11.0): not reviewed:` whose reason names `Broken` (first written as `App: not reviewed:`, wrong in the label: the unit is named with its framework; the reason turned out to be the failed restore of the packages, which names Broken.csproj and the MSB4025 error) (the project that App needs and that cannot be read); the summary says the review is incomplete |

## The change set and the arguments

The change set of a whole review is made from the list of the files of the tree (git `ls-tree`), each an added file with
every line touched, and the filters need no branch. Cases 12a to 14c keep their verdicts. The target is accepted wherever it
stands, the messages for `--all` with another option are plain, and a target in other letters than the tracked folder is
accepted.

### Case 12b, one more check

| Case | What is done | Printed |
| --- | --- | --- |
| 12b | the repository of case 12 | the TEAM010 finding of 12a is printed too (this verdict was always written above and not checked) |

### Cases 15 to 18

| Case | What is done | Exit | Printed |
| --- | --- | --- | --- |
| 15a | standard repository (Api and Legacy breach TEAM010). A settings file outside the repository holds one rule, `EXT010`, which is the braced single-line `if` rule under another identifier; `review --all --settings <that file> src/Api` (the option between `--all` and the target) | 1 | `error EXT010` at `src/Api/Service.cs(<line of the if>,`; no `TEAM010` (the file given was used, not the tree's own) |
| 15b | `review --all src/Api --settings <that file>` | 1 | the same |
| 15c | `review --settings <that file> --all src/Api` | 1 | the same |
| 15d | `review --all --settings <a file outside the repository with a comma missing> src/Api` | 3 | the path of that file, `(<line>,<column>)` and `not valid JSON`; nothing reviewed |
| 16a | `review --all --hook pre-push o u` | 3 | a message that says `--all is never run by a hook` |
| 16b | `review --all --staged` | 3 | `--all stands alone` and `--staged` |
| 16c | `review --all --base HEAD` | 3 | `--all stands alone` and `--base` |
| 16d | `review --all --head HEAD` | 3 | `--all stands alone` and `--head`; never `--head needs --base` |
| 16e | `review --all --files src/Api/Service.cs` | 3 | `--all stands alone` and `--files` |
| 17a | the tracked folder is `src/Legacy`; `review --all src/legacy` | 1 | the same findings as `review --all src/Legacy`; the first note says `the whole of src/Legacy at commit` (the tracked spelling) |
| 17b | `review --all src/legacy/legacy.csproj` | 1 | the same findings; the first note says `the whole of src/Legacy at commit` |
| 17c | two tracked folders `src/Twin` and `src/twin` (SKIP on a machine that cannot hold both: the reason is printed); `review --all src/TWIN` | 3 | `has no files in the last commit` (two folders match, so none is taken) |
| 18 | `src/Api` compiles `..\Shared\Linked.cs`, a file outside the target that does not compile (`int x = "text";`); `review --all src/Api` | 1 | `error CS0029` at `src/Shared/Linked.cs(<line>,`: a compile error is never filtered by lines or by file, as in a push, because it says the project the target holds does not build; this is intended |

### Cases 16f and 16g: an option where a value should be

| Case | What is done | Exit | Printed |
| --- | --- | --- | --- |
| 16f | `review --base --all` | 3 | `--base needs one value` (the option `--all` is not taken as the base revision) |
| 16g | `review --all --settings` (the value forgotten at the end) | 3 | `--settings needs one value` |

## Notes on the verdicts above

- **2a, "3 of 3 files reviewed":** the repository of 2a holds three `.cs` files at the time of the review
  (`src/Api/Service.cs`, `src/Api/Extra.cs` added by the second commit of the case, and `src/Legacy/Legacy.cs`), so the
  summary is `3 of 3 files reviewed, 2 of 2 builds checked: 3 blocking`.
- **8d, exit `1`:** the repository of case 8 is the standard one, whose Api project holds one TEAM010 breach, so a review
  of `src/Api` exits 1 and prints `By rule: TEAM010 1`.
- **17c, `has no files in the last commit`:** replaced by case 21 (the message would be untrue for a target that several
  tracked paths match apart from case).
- **Also checked by `run.sh`:** 3 (the first note does not name the project file),
  8b (exit 1 for the Legacy review), 11a (`A (net11.0): reviewed` as well as B), 12a (`By rule:` holds TEAM010), 15d (the
  message names the settings file by its path).

### The `By rule:` check, now two checks

| Case | Printed |
| --- | --- |
| every whole review that prints findings (a line that starts with a path and `(line,column): error` or `warning`) | the `By rule:` line is there, once, and it is the line directly before the summary |
| every whole review that prints no finding | there is no `By rule:` line |

### The reader of the arguments (cases 19)

| Case | What is done | Exit | Printed |
| --- | --- | --- | --- |
| 19a | standard repository; `review src/Legacy --all` (the target before `--all`) | 1 | the same finding lines as `review --all src/Legacy`; first note `the whole of src/Legacy at commit` |
| 19b | an external settings file with the rule `EXT010`; `review --settings <file> src/Legacy --all` (the target between options) | 1 | `error EXT010` at the line of the if of `src/Legacy/Legacy.cs`; no `TEAM010` |
| 19c | `review --all --all` | 3 | `--all was given twice.` |
| 19d | `review --staged extra` | 3 | `'extra' is not understood here.` |
| 19e | `review --files a.cs --all` | 3 | `--all stands alone` and `--files` |

### The tree's own settings file (case 20)

| Case | What is done | Exit | Printed |
| --- | --- | --- | --- |
| 20 | the committed `commitfence.json` has a comma missing between two keys of a rule; `review --all src/Api` | 3 | `commitfence.json (in the tree under review)(<line>,<column>)` and `not valid JSON`; nothing reviewed |

### Two tracked paths that differ only by case (case 21, built with `git hash-object -w` and `git update-index --add --cacheinfo`, no second folder on disk)

| Case | What is done | Exit | Printed |
| --- | --- | --- | --- |
| 21 | `src/Legacy/A.cs` and `src/legacy/B.cs` are both tracked; `review --all SRC/LEGACY` | 3 | a message that names `'src/Legacy'` and `'src/legacy'` and says `name one of them exactly`; it does not say `has no files in the last commit` |

## A folder that is part of a project

The cases above name a whole project, a loose folder or the repository. Cases 22 to 24 name a folder that is only part of a
project.

### A change of the program that these cases pin, and what it does to a push

`ReachFilter` (Core, with no knowledge of "whole") now treats a finding that has no reach (no place in a file) like this:
it is kept when it names no file, when it is a compile error (compiler source, blocking), or when its file is one of the
files of the change; a rule finding with no place on a file the change does not hold is dropped. Every finding
without reach used to be kept, whatever its file; a push changes in this one point: such a finding on a file the push did not
touch is an old breach, so it is not printed. Compile errors stay unfiltered everywhere. The compiler
reports no finding of the build that names a file and no line (CA1014 and similar, and a `#line hidden` region) without a
line, so the push side of this point has no `pushed` or `toolchain` case. It is
pinned only by what the filter says (and by case 24, for the finding that names no file).

### Cases 22 to 24 (a target inside a project)

Repository of 22: project `src/Legacy` (`Legacy.csproj`, net11.0) holds `Legacy.cs` with the two old breaches (the
field and the `if` of the standard repository) and the subfolder `src/Legacy/Sub` with `Sub.cs`: its own private static
field never reassigned (IDE0044) and its own braced single-line `if` (TEAM010). Sub is part of the Legacy project.

| Case | What is done | Exit | Printed |
| --- | --- | --- | --- |
| 22a | `review --all src/Legacy/Sub` | 1 | the two findings of `src/Legacy/Sub/Sub.cs` at their lines (`error IDE0044`, `error TEAM010`); no `Legacy.cs`; the project is built (`Legacy (net11.0): reviewed`); `By rule: IDE0044 1, TEAM010 1`; the first note `the whole of src/Legacy/Sub at commit <10>: 1 files.` |
| 22b | `review --all src/Legacy` | 1 | all four findings (both files, each at its lines); `By rule: IDE0044 2, TEAM010 2` |
| 23a | repository of 23: `Api` (one class `Orders`), `Legacy` references Api, `structure` section as in case 6 (legacy must not use api); `Legacy.cs` and `Sub/Sub.cs` each use `new Api.Orders()`, no other breach; `review --all src/Legacy/Sub` | 1 | one `error ARCH001` at the use in `src/Legacy/Sub/Sub.cs`; no `Legacy.cs`; `By rule: ARCH001 1` |
| 23b | the same repository, `review --all src/Legacy` | 1 | two ARCH001, one in each file; `By rule: ARCH001 2` |
| 24a | a finding of the build that names a file outside the target and no line | not produced | not run: no such finding could be made (see above); the file-outside-the-target case is pinned for findings with a line by 11b |
| 24b | repository of 24: `Legacy.csproj` has `EnableNETAnalyzers` true and `GenerateAssemblyInfo` false, the `.editorconfig` sets CA1016 to error (mark assemblies with AssemblyVersion: the finding has no file); clean code in `Legacy.cs` and `Sub/Sub.cs`; `review --all src/Legacy/Sub` | 1 | the line `(no file): error CA1016` is printed: it belongs to the project, and part of the project is under review |

### Other cases

| Case | What is done | Exit | Printed |
| --- | --- | --- | --- |
| 9h (one more check) | `review --all src/Nope` | 3 | besides `has no files in the last commit`, the words `no tracked folder has this name in another letter case either` |
| 17b (two more checks) | `review --all src/legacy/legacy.csproj` | 1 | the `By rule:` line is `By rule: IDE0044 1, TEAM010 1` (as for the folder) and the first note names the tracked folder `src/Legacy` (the latter was checked already) |
| 19f | an external settings file; `review --all --settings <file> --settings <file> src/Api` | 3 | `--settings was given twice` |
| 19g | `review --base HEAD --base HEAD` | 3 | `--base was given twice` |

### A count of one

- **22a, first note:** the program says `1 file.` for one file (singular). The note of 22a is
  `the whole of src/Legacy/Sub at commit <10>: 1 file.`; `run.sh` checks that.
