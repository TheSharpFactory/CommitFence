# Pushed code checks: expected results

Every case builds its own repository under `work\repos\<case>` with a local bare repository
as remote under `work\remotes\<case>.git`. The hook is a plain team script whose first
command is the hook line, wrapped by a thin `pre-push` that only
runs it and records its exit code in `hook-exit.txt` (the wrapper reads no input, so the
team script still gets git's lines). The rule file holds TEAM010 only (braces around a
single-line `if`). The initial commits of each case are pushed with `--no-verify` unless
the case is about the first push.

Four things are checked in each hook case: the hook's own exit code, the findings printed,
the coverage (the summary line), and whether the remote moved. The first printed line of
every hook run is `CommitFence <version> (<program folder>)`.

Case 12 applies to every case: before and after, the developer's working folder (content
of every file outside `.git`), `.git/index` (bytes), HEAD, the list of local branches and
tags, the stash list and `git status` are identical. Every working folder holds an
unstaged edit, an untracked file and a stash entry so that "unchanged" means something.

| Case | What is done | Hook exit | Findings | Coverage (summary) | Remote |
| --- | --- | --- | --- | --- | --- |
| 1 clean | one commit adds a clean method, push main | 0 | none | `1 of 1 files reviewed: 0 blocking` | moved |
| 2 breaking | one commit adds a braced single-line if; the working folder is then edited to the fixed form but not committed; push main | 1 | `src/Core.cs(<line of the if>,...)`: error TEAM010 | `1 of 1 files reviewed: 1 blocking` | not moved |
| 3 reach | old breach already on the remote in `src/Old.cs`; commit adds a clean method elsewhere in the same file | 0 | none (the old breach is on a line the push does not touch) | `1 of 1 files reviewed: 0 blocking` | moved |
| 3b reach, touched | same, but the commit edits a line inside the old breach | 1 | TEAM010 on `src/Old.cs` | `1 blocking` | not moved |
| 4 two branches | branches clean (adds `src/Good.cs`) and bad (adds `src/Bad.cs` with a breach) pushed together | 1 | TEAM010 on `src/Bad.cs` only, none on `src/Good.cs` | `2 of 2 files reviewed: 1 blocking` | not moved (neither branch) |
| 5 new branch | remote already has main (with the old breach in `src/Old.cs`); new branch with one clean new file `src/New.cs` | 0 | none | `1 of 1 files reviewed` (only its own commits; `src/Old.cs` is not reviewed) | moved (new branch) |
| 5b new branch, breaking | same, the new file breaks the rule | 1 | TEAM010 on `src/New.cs` | `1 of 1 files reviewed: 1 blocking` | not moved |
| 5c new branch, two known branches | remote has main and dev; dev holds a commit with a breach in `src/Dev.cs`; new branch topic from dev adds a clean file. The before is the oldest common point (main), so the breach of dev is seen | 1 | TEAM010 on `src/Dev.cs` | `2 of 2 files reviewed: 1 blocking` | not moved |
| 6 first push, empty remote | two commits; the breach is in the first one (`src/Core.cs`), the second adds `src/Extra.cs` | 1 | TEAM010 on `src/Core.cs` | `2 of 2 files reviewed: 1 blocking` | not moved (still empty) |
| 6b first push, clean | the same without the breach | 0 | none | `2 of 2 files reviewed: 0 blocking` | moved |
| 7a forced push, amend clean | main pushed, commit amended with a clean change, `push --force` | 0 | none | `1 of 1 files reviewed` | moved to the amended commit |
| 7b forced push, amend breaking | the amend adds a breach | 1 | TEAM010 | `1 blocking` | not moved |
| 7f forced push over work never fetched | a second clone pushes a commit to `main` of the remote; this clone has never fetched it, makes a different clean commit on the same initial commit and pushes with `--force`, so the remote's commit named in git's ref line is not in this clone | 0 | none | `1 of 1 files reviewed`, and the note `The remote's commit <first 10 characters of the remote's tip> is not in this clone, so refs/heads/main was reviewed as a new branch: more is reviewed, never less.` (cases 7a and 7b, where the remote's commit is in the clone, print no such note) | moved to this clone's commit |
| 7c delete branch | `push origin --delete feature` | 0 | none | summary says `deleted on the remote, so there is nothing to review`; no files | branch gone |
| 7d tag, already pushed commit | lightweight tag on the commit main already has on the remote | 0 | none | `holds no commit that the remote is not known to have`; no files | tag present |
| 7e tag, unpushed breaking commit | annotated tag on a local commit that is not on the remote and breaks the rule; only the tag is pushed | 1 | TEAM010 | `1 blocking` | not moved |
| 8a up to date | `git push` with nothing new (git gives the hook no lines) | 0 (or the hook is not run at all) | none | summary says there is nothing to review | unchanged |
| 8b empty input, unpushed commits | the hook run by hand with empty input while a local branch holds an unpushed commit | 2 | none; the reason `gave the hook no ref lines` printed | `review scope: incomplete` | unchanged |
| 9a staged breaking | staged content breaks the rule, the working folder has the fixed form | 1 | TEAM010 on `src/Core.cs` | `1 of 1 files reviewed: 1 blocking` | n/a |
| 9b staged clean | staged content is clean, the working folder has the breach | 0 | none | `1 of 1 files reviewed: 0 blocking` | n/a |
| 10 does not parse | the commit adds a file with a syntax error | 2 | none | `src/Broken.cs: incomplete: does not parse` | not moved |
| 11 two at once | five repeats of two `review --base/--head` runs started at the same moment on one repository, one range clean and one breaking | clean 0, breaking 1, every round | each output equals its output when run alone | the same | n/a |

Also reported, only when the script is started with `CHECK_TIMES=1` (not a case, nothing is asserted): the time of the first review of a repository (the private copy is built) and of a second review of a nearby tree (median of 5 each), with the number of files.

## Further cases

All use `review --base <c> --head <c>` (no hook needed)
on a repository built like the others; the finding line is the line of the `if(x > 0)` in the
head file. Case 12 (developer state unchanged) applies to 13a, 13b and 14 as before.

| Case | What is done | Exit | Findings | Coverage / other |
| --- | --- | --- | --- | --- |
| 13a added `++ ` line | `src/Core.cs` gains a method holding a line that begins with `++ ` (column 0, `++ count;`) and, after it, a braced single-line if | 1 | TEAM010 at `src/Core.cs(<line of the if>,` | `1 of 1 files reviewed: 1 blocking`; no file named `count;` anywhere in the output |
| 13b removed `-- ` line | base `src/Core.cs` holds a method with a line `-- count;` at column 0; the commit removes that line and adds a braced single-line if | 1 | TEAM010 at `src/Core.cs(<line of the if>,` | `1 of 1 files reviewed: 1 blocking` |
| 14a tracked file edited by hand | review of a breaking tree (exit 1, finding); the tracked `src/Core.cs` in the private copy is overwritten with the fixed form; the same tree is reviewed again | 1 | the same finding at the same line (the committed content is judged) | copy file holds the committed bytes again afterwards; an untracked file put in the copy is still there |
| 14b tracked file deleted | the tracked `src/Core.cs` in the private copy is deleted; the same tree is reviewed again | 1 | the same finding at the same line | the file exists again afterwards |
| 14c another tree after an edit | `src/Core.cs` in the copy is edited by hand again; the next commit (touches the line inside the breach) is reviewed | 1 | TEAM010 at the line of the breach | copy file equals the committed content of the new tree |
| 15 partial clone | bare remote with `uploadpack.allowFilter`; clone with `--filter=blob:none --no-checkout` through a `file://` address; the review needs blobs that are not there | 2 | none | the reason names the failed git command; before and after the review the blob is still missing (`cat-file -e` with lazy fetch off fails both times); the number of missing objects (`rev-list --objects --missing=print --all`) is the same before and after |

If case 15 cannot be built on this machine after two tries, the reason is given and it is not tried again.

## More cases

The fixture is the one of the cases above (a bare remote, the team's hook, TEAM010 in `commitfence.json`) unless a case says otherwise. Case 12 (the developer's state is unchanged) applies to each of them; it also covers `.git/config`, every file of `.git/hooks` except the hook's own `hook-exit.txt`, and the list of all refs except `refs/remotes` (git's own bookkeeping of a push).

| Case | What is done | Exit | Findings | Other |
| --- | --- | --- | --- | --- |
| 16 not a repository | `review --staged` and `review --base HEAD --head HEAD` run in a folder that is not in a git repository (`GIT_CEILING_DIRECTORIES` stops git from looking above it) | 2 both | none | the output says `the current folder is not inside a git repository` and `review scope: incomplete`; the folder holds no new file afterwards |
| 17a a git command that hangs | a `.gitattributes` line `*.slow filter=slow` and the repository setting `filter.slow.smudge` set to `sleep 300`; the push adds `data.slow`, so the private copy's checkout runs the filter and hangs; `COMMITFENCE_PROCESS_LIMIT_SECONDS=8` | hook 2 | none | the output says `incomplete: git read-tree failed: git did not finish within 8 seconds and was stopped`; the push is refused and the remote does not move; the whole push takes less than 60 seconds; no `sleep` process of the filter is left running |
| 17b an unexpected error | `COMMITFENCE_DATA_FOLDER` names an existing file, so the data folder cannot be made | review 2 | none | stderr is exactly one line `CommitFence could not finish: IOException: ...` with no stack frame (no line starting with `at `); with `COMMITFENCE_TRACE=1` the stack trace follows the line |
| 17c an unexpected error, other commands | `rules list --settings` with a path that the machine cannot read as a path (`a:b:c.json`) | 3 | none | stderr is not empty and holds no stack frame; the exit code is 3 whether the machine reports the error as unexpected (Windows) or as a missing settings file (elsewhere) |
| 18a reach, header | `src/Box.cs` has `public class Box` with an unchanged `protected int Size()`; the rule TEAM011S (the syntax form of an example: a method whose containing type is written `internal` must be written `public` or `private`) is in the settings; the push changes only `public class Box` to `internal class Box` | 1 | TEAM011S at the line of `Size` | one finding; the remote does not move |
| 18b reach, documentation | rule DOC1 (a method must have documentation); `src/Doc.cs` has a documented method `Documented()`; the push deletes only its documentation comment | 1 | DOC1 at the line of `Documented` | the remote does not move |
| 18c reach, not wider | `src/Wide.cs` holds two undocumented methods `First()` and `Second()` (old breaches of DOC1, already on the remote); the push changes the body of `First()` only | 1 | DOC1 at the line of `First`, not at the line of `Second` | one distinct finding line |
| 18d reach, a blank line | the same file; the push only inserts a new documented method above `First()`, with a blank line between | 0 | none (the old breaches are not touched) | the remote moves |
| 19 names as the compiler reads them | rule NAME1 forbids a method named `Run`; `src/Names.cs` holds `Run`, `@Run` and one more spelling of the same name with a unicode escape for its second letter | 1 | NAME1 at three distinct lines, one for each of the three methods | the remote does not move |
| 20 two paths, one case apart | the tree holds `src/Dup.cs` and `src/dup.cs` as two different files (written into the index, with the working folder holding only one of them on a machine that cannot hold both); only `src/dup.cs` carries a breach and is changed in the push | 1 | TEAM010 at `src/dup.cs`, not at `src/Dup.cs` | if the machine cannot hold both names in a folder, the case is printed as `SKIP  20: ...` and counted as skipped |

## Cases 21 and 22: the object store and the data folder

| Case | What is done | Exit | Output | Other |
| --- | --- | --- | --- | --- |
| 21 an object file of commitfence.json is gone | a commit changes only the mode of `commitfence.json`; a review of the range completes (the private copy then holds the tree); the blob's object file is removed from `.git/objects`; the same `review --base --head` is run again | 2, never 0 | `review scope: incomplete` and the reason `unable to read <blob id>` | the first review completes and names `commitfence.json` as a changed rule file. In this arrangement the diff stage meets the missing object before the settings of the tree before are read, so the case proves "never 0, reason printed" for the whole review; the failure of `ReadFile` itself (every git failure is an error, not "no such file") cannot be reached from the command line |
| 22 the staged copy of the index | `review --staged` with a staged change, an unstaged edit, an untracked file and a stash entry | 0 | none | the repository's folder inside the data folder exists, no `commitfence-index-*` file is left anywhere in the data folder, and the developer's state is unchanged (12.22) |

## Cases 23a and 23b: a push that changes only `commitfence.json` is read

A push whose only change is the settings file used to pass without reading it, so a broken file went in and every later push ended with exit 3. The settings of the tree under review are now read strictly whenever the push changes them.

| Case | What is done | Exit | Output | Remote |
| --- | --- | --- | --- | --- |
| 23a only a broken `commitfence.json` | a comma is missing between two keys of a rule, nothing else changes | 3 | `commitfence.json (in the tree under review)(<line>,<column>)` and `not valid JSON`, exactly as when a `.cs` file is pushed with the same file | not moved |
| 23b only a valid change to it | the severity of the one rule goes from block to warn, nothing else changes | 0 | no `in the tree under review` message | moved |

Case 12 (the developer's state is unchanged) applies to both.

## Cases 24a and 24b: the hook's environment names an index file that does not exist

Git sets `GIT_INDEX_FILE` for some hooks. Here the team's `pre-push` wrapper exports it with the path of a file that does not exist, and runs the hook line as in every other case; the program must not read it as the index of the developer.

| Case | What is done | Exit | Findings | Remote |
| --- | --- | --- | --- | --- |
| 24a breaking push | one commit adds a braced single-line if, push main | 1 | TEAM010 at `src/Core.cs(<line of the if>,`, the same finding as case 2 | not moved |
| 24b clean push | one commit adds a clean method, push main | 0 | none | moved |

Case 12 (the developer's state is unchanged) applies to both.

## Cases 25a and 25b: `.gitattributes` says `*.cs -diff`

With that attribute git writes every changed `.cs` file in its diff as `Binary files ... differ`, with no hunks. The base commit holds the attribute file and `src/Core.cs`, and is pushed with `--no-verify`.

| Case | What is done | Exit | Findings | Remote |
| --- | --- | --- | --- | --- |
| 25a breaking push | one commit adds a braced single-line if to `src/Core.cs`, push main | 1 | TEAM010 at `src/Core.cs(<line of the if>,` | not moved |
| 25b clean push | one commit adds a clean method, push main | 0 | none | moved |

Case 12 (the developer's state is unchanged) applies to both.

## Case 25c: `.gitattributes` says `*.cs -diff`, and the file already holds an old breach

A file git writes without lines is not counted as touched on every line, so an old breach in it does not block a push. Such a file that a language owns is read once more with `--text`, so that it behaves as any other file.

| Case | What is done | Exit | Findings | Remote |
| --- | --- | --- | --- | --- |
| 25c old breach, clean change | the base commit (pushed with `--no-verify`) holds the attribute file and `src/Core.cs` with a braced single-line if; the push adds a clean method at the end of the same file, far from the breach | 0 | none (the old breach is let go) | moved |

Case 12 (the developer's state is unchanged) applies.

## Cases 25d and 25e: more of `*.cs -diff`

`.gitattributes` holds `*.cs -diff` in both. The base commit of each is pushed with `--no-verify`.

| Case | What is done | Exit | Findings | Remote |
| --- | --- | --- | --- | --- |
| 25d renamed | the base holds `src/Old.cs`: a braced single-line if near the top and some twenty more clean lines (the file is large enough, and the push changes little enough, for git to pair the two names: the script checks that `git diff -M --name-status` of the commit says R for it). The push renames it to `src/New.cs` and adds one clean method at its end | 0 | none (the old breach is let go: a renamed file that git writes without lines is still seen as a rename) | moved |
| 25e a name with ` b/` in it | the base holds `Foo.cs` at the top with a braced single-line if (old) and a folder literally named `src b` with a clean `Foo.cs`. The push adds a braced single-line if to `src b/Foo.cs` | 1 | TEAM010 on `src b/Foo.cs` at the line of the new if; no finding on the top `Foo.cs` | not moved |

Case 12 (the developer's state is unchanged) applies to both.

## Case 25f: `*.cs -diff` and many files

Git writes no lines for any `.cs` file, so every changed file is read again as text, and the files are read in batches of 50 so that the command line of the system is never too long.

| Case | What is done | Exit | Findings | Remote |
| --- | --- | --- | --- | --- |
| 25f sixty files | `.gitattributes` holds `*.cs -diff`; the base commit (pushed with `--no-verify`) holds `src/File01.cs` to `src/File60.cs`, all clean, in the one project; the push appends a method to each of the sixty files, and the one appended to `File01.cs` and the one appended to `File60.cs` is a braced single-line if | 1 | TEAM010 on `src/File01.cs` and on `src/File60.cs` at their lines, and on no other file | not moved |

Case 12 (the developer's state is unchanged) applies.

## Case 26: a push of the settings file alone

With only `commitfence.json` staged, the rules that need the build were reported "not run" ("the build did not run, so nothing is known about what the code refers to") and the review was incomplete, so a team that changed a rule and pushed only that file was stopped. The rule is that only what a change touches is judged: a change with no file of code gives the rules that judge code nothing to judge, so the review is complete and no build is started. The structure rules are judged as before. The repository is built like the others (`base_repo`: `src/Core.cs` pushed with `--no-verify`); the push changes `commitfence.json` and nothing else.

| Case | What is done | Exit | Findings | Coverage / other | Remote |
| --- | --- | --- | --- | --- | --- |
| 26a effect changed | the settings hold TEAM010 (block) and TEAM012, a rule that needs the build (it forbids `System.DateTime.Now`); the push changes TEAM010 from block to warn | 0 | none | the output says `Rule files changed: commitfence.json`, has no `was not run` and no `incomplete`; no build is made (`builds checked` is not in the output) | moved |
| 26b direction added | the settings hold two modules by namespace (`Demo`, `Other`) and no direction; the push adds the direction `Demo -> Other` | 0 | none | the same: no `was not run`, no `incomplete` | moved |
| 26c rule switched on | the settings hold TEAM012 with `"enabled": false`; the push switches it on | 0 | none | the same | moved |

Case 12 (the developer's state is unchanged) applies to each.
