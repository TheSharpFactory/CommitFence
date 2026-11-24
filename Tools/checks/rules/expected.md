# Rules commands checks: expected results

`run.sh` builds everything under `work/` (copies of the settings file, small sources, two small git repositories
with a scratch global git configuration and a scratch data folder). It never touches
`commitfence.json` at the root of the project: the script copies it to `work/base.json` and every case works on its own copy.
The program is the one whose path is the first argument of the script.

Sources (line numbers count from 1, an `if` sits at column 9):

- `src/Three.cs`: line 7 `if(value > 0)` with braces and one statement; line 12 `if(value < 0)` with no braces;
  line 15 `if(value == 0)` with braces and two statements.
- `src/Else.cs`: line 7 `if(value == 5) return 5; else return 6;`.
- Repository `tidy` (only `Tidy/Worker.cs`): line 5 `protected void Hidden()` in an internal class.
- Repository `clock` (only `Clock/Reader.cs`): line 7 `return DateTime.Now;`, line 12 `return DateTime.UtcNow;`.

The settings file is the root `commitfence.json` of the project (TEAM010, TEAM011, TEAM012, TEAM013 and the structure rules).
Exit codes: 0 passed, 1 blocked, 2 incomplete, 3 wrong use or refused change.

## Case 1: each change from the command line, and the next review follows it
1. `review --files Three.cs Else.cs`: finding TEAM010 at Three.cs line 7, none at line 12 or 15; exit 1.
2. `rules disable TEAM010`: exit 0; `rules list` shows TEAM010 with `(off)`; `rules show TEAM010` has a `status` line saying off; the next
   `review --files` has no TEAM010 finding at all (exit 2: the four rules that need the build, TEAM011, TEAM012, TEAM013 and the layer directions ARCH001, are not run, and nothing blocks), and its
   summary ends with the sentence `4 rules need a build, and a review of named files does not run a build, so they were not run:
   commitfence review --staged or a push runs them.` (the number is the number of rules that were not run; with one it reads
   `1 rule needs a build, ... so it was not run: ... runs it.`). The same sentence ends the summary of every `review --files` that leaves rules unrun, whatever its exit code (1.1 and 1.3b too); the verdicts of those cases do not change, and no case pins the absence of the sentence.
3. `rules enable TEAM010`: exit 0; the file is byte for byte the base file again; the review finds line 7 again, exit 1.
4. Repository `tidy`, `review --base --head --settings`: finding TEAM011 at Worker.cs line 5, exit 1.
5. `rules severity TEAM011 warn`: exit 0; the same review shows TEAM011 as `warning` and exits 0; `rules list` shows `warn` for it.
6. `rules severity TEAM011 block`: file is byte for byte the base again; the review exits 1 again.
7. Repository `clock`: TEAM012 finding at Reader.cs line 7 and none at line 12; exit 1.
8. `rules set TEAM012 --where target=System.DateTime.UtcNow`: exit 0; `rules show TEAM012` has the new target; the review now has a finding at
   line 12 and none at line 7; exit 1.
9. `rules disable TEAM012`: the review of `clock` exits 0, with no TEAM012 finding and no "not run: TEAM012" line. `rules enable TEAM012` and the review blocks again.
10. `rules add TEAM020 --for if-statement --title ... --where hasElse=true --require braces=true --why ...`: exit 0; `rules show TEAM020` shows
    the where and the require; `review --files Three.cs Else.cs` has a TEAM020 finding at Else.cs line 7 and none in Three.cs.
11. `rules remove TEAM020`: exit 0; `rules list` has no TEAM020; the file is byte for byte the base file again; no TEAM020 finding in the review.
12. `rules remove TEAM011` (on another copy): exit 0; `rules list` has TEAM010 and TEAM012 and not TEAM011.

## Case 2: nothing untouched is lost
After each change of case 1 on the base file (the base file already is in the stable layout):
- The `structure` part, from its first line to the end, is byte for byte the same as in the base file.
- Disable TEAM010: the only difference from the base is one added line, `"enabled": false,`.
- Severity of TEAM011 to warn: the only differences are the one `severity` line, from block to warn.
- Set TEAM012's target: the only differences are the one `where` line.
- Add TEAM020: no line is removed (diff shows the `},` that follows TEAM013 as added); the added lines hold TEAM020.
- Back-and-forth changes (disable then enable, warn then block, add then remove) leave the file byte for byte the base file.
- Remove TEAM011: the first and the third rule blocks are unchanged and the structure part is identical.
- No file `commitfence.json.new` and no temporary file `commitfence.json.<unique>.new` is left beside the settings file.

## Case 3: refused with exit code 3, a plain message, the file unchanged
Each of these ends with exit 3, an empty standard output, a message on standard error that names the problem, and a settings file
byte for byte unchanged:
1. `rules disable NOPE`: "no rule 'NOPE'". The same for `rules remove NOPE`, `rules severity NOPE warn`, `rules set NOPE --title x`, `rules try NOPE`.
2. `rules add X1 --for banana --title t --require braces=false`: names `banana` and "not an artifact type".
3. `rules add X1 --for if-statement --title t --require colour=red`: "'colour' is not a property".
4. `rules add X1 --for if-statement --title t --require braces=banana`: "true or false"; `--where statementsInBody=many`: "whole number".
5. `rules add TEAM010 --for if-statement --title t --require braces=true`: "already used".
6. `rules add X1 --for if-statement --title t --require braces=true --forbid`: "not both".
7. `rules set TEAM010 --for method`: refused (the where asks for statementsInBody, which a method does not have).
8. `rules add X1 --for if-statement --title t`: refused, needs `--require` or `--forbid`.
9. `rules list --title x`: wrong use, exit 3.
10. `rules severity TEAM010 loud`: exit 3.

## Case 3b: review fixes
Added to the refusals, each exit 3, empty standard output, message on standard error, settings file unchanged, nothing left beside it:
- `rules set TEAM010 --severity block`: message points to `rules severity`. `rules set TEAM010 --language x`: message says the language of a rule cannot be changed.
- `rules set TEAM010 --paths ,`: "--paths needs at least one pattern" (and names `--no-paths`). `rules set TEAM010 --where braces=`: "empty property or an empty value". `--require braces=,`: same.
- `rules set TEAM010 --where braces=true --no-where`: "cannot be given together". `rules add X1 ... --no-paths`: "does not take".
- `rules set TEAM010 --no-paths` still works (exit 0).

Saving never uses a fixed `<file>.new`:
- A file `commitfence.json.new` with the text KEEP beside the settings file survives `rules disable TEAM010` byte for byte (exit 0), and no other file is left beside the settings file.
- Two rule changes started at the same moment (`rules disable TEAM010` and `rules severity TEAM011 warn`) take turns: the commands that change a settings file hold a lock held in CommitFence's own data folder (`settings-locks\<hash of the file's full path>.lock`, under `COMMITFENCE_DATA_FOLDER` in the checks), so the verdict is deterministic. In every one of ten repeats both commands exit 0, `TEAM010` is `(off)` and `TEAM011` is `warn` in the file afterwards, the file reads back (`rules list` exit 0), and nothing is left beside the settings file. Any other result, a lost edit above all, fails. The script is run five times in a row (fifty repeats) for the report.
- Case 3.18: after those repeats a `.lock` file exists in `settings-locks` under the scratch data folder, and no lock file is beside the team's settings file.
- Not checked from outside: the comparison that runs just before the write (the file still holds the text the command read; if a person or another program saved it in between, nothing is written and the command exits 3 with `<path> changed while this command was working. Nothing was written; run the command again.`). The moment between the read and the write cannot be staged from a script without faking it, so no case claims it. The same goes for the lock's 30-second timeout (`Another command is changing <path>. Run this one again.`, exit 3).

## Case 4: `rules try`
On `src/Three.cs` for TEAM010, by file path and by standard input (with and without `-`):
- line 7 col 9: `breaks it`, reason mentions `braces is true`;
- line 12 col 9: `follows it`;
- line 15 col 9: `not covered`, reason mentions `statementsInBody is 2`;
- last line `1 breaks it, 1 follows it, 1 not covered.`; exit 0. A file path is shown relative to the folder of the settings file; standard input as `stdin`.
- `echo "if(" | rules try TEAM010`: says it does not parse, exit 2 (could not be completed). A completed trial exits 0 whatever its outcomes.
- Code with no `if`: "No artifact of the rule's type in this code." and `0 breaks it, 0 follows it, 0 not covered.`
- TEAM012 and TEAM011 (rules that need meaning): a line containing `cannot be tried without a build`, exit 2.

## Case 5: the file written by a command
1. After the changes above, `rules list` reads the file (exit 0) and shows the expected rules.
2. A hand-written file in a strange but valid layout (all on one line, keys in another order, `requires`, structure with a forbid and an allow list)
   after `rules enable X1` is byte for byte the stable layout written out in the script (`expected-strange.json`), nothing lost.
3. A file with Windows line ends stays with Windows line ends after a change (no lone line feed).
4. A text with a `>` and an accent in a title comes back as it went in (not escaped as `\u003E`).

## Case 6: the catalog
- `catalog` prints csharp, the types if-statement, method, class and `usage (needs the build)`, and under them the properties with their value types:
  `statementsInBody` number, `braces` true or false, `name` text, `declaredAccess` text that needs the build, `target` needs the build.
- `catalog csharp method` prints only the method type (no `if-statement` line); `catalog csharp banana` and `catalog cobol` exit 3 with a message.

## Also
- `review --files` of every `.cs` under `Dependencies` and `App` with the root `commitfence.json`: no TEAM010 finding and no blocking finding from the rules that run on the file alone (case 7.1: the exit code is not 1; it is 2 because the build rules are not run).
- The root `commitfence.json` is unchanged by the script (case 7.2).

## More cases

Every case runs `review --files src/A.cs --settings <file>` with the settings file written by the case, so that the line and the column are known from the text. The exit code is 3 and standard output is empty; standard error holds `<file>(<line>,<column>): <message>`.

| Case | The settings file | Exit | The message names |
| --- | --- | --- | --- |
| 8a broken JSON | a comma is missing between two keys of a rule | 3 | `not valid JSON` and a line and column |
| 8b unknown key at the root | `"colour": 1` on line 3 | 3 | `(3,5)` and `The key 'colour' is not known in the settings file.` |
| 8c unknown key in a rule | `"colour": "red"` inside the first rule, on its own line | 3 | the line and column of that key and `is not known in a rule` |
| 9a rules is null | `{ "rules": null }` written on three lines | 3 | `(2,14)`, the place of the `null`, and `is null` |
| 9b a path is null | `"paths": [ null ]` in a rule | 3 | the line and column of the `null` and `is null` |
| 9c requires is null | `"requires": null` | 3 | the place of the `null` and `is null` |
| 9d structure is null | `"structure": null` | 3 | the place of the `null` and `is null` |
| 9e a condition is null | `"where": { "braces": null }` | 3 | the place of the `null` and `is null` |
| 10 a key named rules[0] | `{ "rules": [ 5 ], "rules[0]": 1 }` over several lines: the first element of `rules` has the wrong type | 3 | the line and column of the `5` (the element), not those of the later key `"rules[0]"` |
| 10b a key with a dot | a rule whose `where` holds the key `"braces"` with a wrong value, and a later key of the rule named `"where.braces"` | 3 | the error of the rule is reported (or the unknown key, at its own place), never at a wrong line: the line named is the line of the key it speaks about |
| 11 path patterns | a rule with `"paths": [ "src/**/*.cs" ]` and files `src/A.cs`, `src/deep/B.cs`, `other/C.cs`, each holding a braced single-line if | 1 | findings in `src/A.cs` and `src/deep/B.cs`, none in `other/C.cs`; with `"paths": [ "src/*.cs" ]` a finding only in `src/A.cs` (the whole path is matched) |
| 11b a folder name with a line break | a folder named with a line break inside holding `C.cs` with the breach, and the pattern `**/C.cs` | 1 | a finding in the file; on a machine that cannot make such a folder (Windows) the case is printed as `SKIP  11b: ...` and counted as skipped |

## Case 12: `review --files` reads a file that holds conditional sections in part, so the review is incomplete

`review --files` builds nothing, so it reads each file as it is written, with no symbol defined. A breach inside `#if FEATURE` is therefore not seen. Such a file is **read in part**: its coverage is `incomplete: read in part: code switched by a symbol was not read; a review with a build reads it`, the review is incomplete (exit 2), unless a blocking finding makes it 1. A file with no conditional section is unaffected. (Before this change the run added a sentence and still exited 0; that was a pass it had not earned.)

| Case | The file | Exit | The output |
| --- | --- | --- | --- |
| 12a | `src/Cond.cs` holds a braced single-line if inside `#if FEATURE ... #endif` | 2 | the line `src/Cond.cs: incomplete: read in part: ...`; no finding; `0 of 1 files reviewed` |
| 12b | `src/Plain.cs` has no `#if` | 0 | does not say `read in part` |
| 12c | `src/Taken.cs` holds only `#if !FEATURE ... #endif` (the one branch is the one that is read, nothing is left unread) | 0 | does not say `read in part` |
| 12d | `src/Cond.cs` and `src/Other.cs` (with `#if A ... #else ... #endif`) together | 2 | both files have their `incomplete: read in part` line; `0 of 2 files reviewed` |
| 12e control | `src/CondOff.cs`, the code of `Cond.cs` without the `#if` lines | 1 | the finding `src/CondOff.cs(7,9): error TEAM010`; does not say `read in part` |
| 12f | `src/Cond.cs` and `src/CondOff.cs` together | 1 | the read-in-part line of `Cond.cs` and the finding of `CondOff.cs` |

Case 1.2d pins exit 2 (the four rules that need the build are not run, and nothing blocks): it never accepted another exit code.

## Case 13: a rule added at a place, a rule copied, and code that is a member or a few statements

Each case works on its own copy of the settings file (TEAM010, TEAM011, TEAM012, TEAM013 and the structure rules). The program refuses in one plain line on standard error with exit 3, and writes nothing, in every refusal.

| Case | What is run | Exit | What must be seen |
| --- | --- | --- | --- |
| 13a | `rules remove TEAM010`, then `rules add TEAM010 --for if-statement --title "No braces around a single-line if" --where statementsInBody=1 --where bodyFitsOnOneLine=true --require braces=false --before TEAM011` | 0 | the line `Added rule TEAM010 before TEAM011.`; the settings file is byte for byte the one before the remove |
| 13b | the same with `--before NOPE` | 3 | `The settings file has no rule 'NOPE'.`; the file is unchanged |
| 13c | `rules remove TEAM011 --before TEAM012` | 3 | a line saying `rules remove does not take --before` and the usage; nothing removed |
| 13d | `rules copy TEAM010 TEAM014` | 0 | `Copied rule TEAM010 as TEAM014.`; `rules list` shows TEAM010, TEAM014, TEAM011, TEAM012, TEAM013 in that order; TEAM014 is titled `Copy of No braces around a single-line if` (the design says so for the page's Duplicate rule and is silent for the command line, so it is the same); `rules show` of the two differs only in the id and the title |
| 13e | `rules copy TEAM010 TEAM011` | 3 | `The rule id 'TEAM011' is already used by an earlier rule.` (the line `add` gives); the file is unchanged |
| 13f | `rules copy NOPE TEAM020` | 3 | `The settings file has no rule 'NOPE'.` |
| 13g | `rules copy TEAM010 "bad id"` and `rules copy TEAM010 ""` | 3 | a line saying `is not a rule id` and what an id is |
| 13h | `rules copy TEAM010` (no new id) | 3 | a line saying `rules copy takes the id of the rule to copy and the new rule id.` and the usage |
| 13i | the usage text | | names `rules copy <id> <new id>` and `[--before <id>]` |
| 13j | `rules try TEAM010` on the design's sample verbatim, 16 lines with `public void Send(Invoice invoice)` | 0 | verdicts at (3,5) breaks it, (8,5) follows it and (11,5) not covered, and the line `1 breaks it, 1 follows it, 1 not covered.`; no `does not parse` |
| 13k | `rules try TEAM010` on a lone `if(x) { return; }` | 0 | one verdict, at (1,1), breaks it |
| 13l | on two statements, an `if` with braces and a `return;` after it | 0 | one verdict at (1,1), breaks it, the lines and columns those of the text |
| 13m | on members of a type: a property and a method with an `if` in it, no class around them | 0 | a verdict at the `if`, with its own line and column |
| 13n | on a whole file with a namespace and a class | 0 | the verdict at the line and column in the file, as before |
| 13o | on text that is no C# at all (`this is not code at all {{`) and on `void A() { if(x) { } ` | 2 (incomplete, as `rules try` ends today for a text that does not parse) | `does not parse` and the line and column the parser gives for the second text: (1,22) `} expected` |
| 13p | the same sample on standard input | 0 | the same three verdicts, named `stdin` |
| 13q | `rules disable TEAM012`, then `rules copy TEAM012 TEAM015` | 0 | `Copied rule TEAM012 as TEAM015.`; `rules show TEAM015` says `status: off`, as the original does (a copy keeps the switch of the rule it copies) |
| 13r | `rules add "bad id" --for if-statement --title T --require braces=false` | 3 | a line saying `is not a rule id` and what an id is, the same line `rules copy` gives; the file is unchanged (one rule for what a new id is: `add`, `copy` and the page refuse alike) |
| 13s | `rules list` on a settings file that already holds a rule with the id `odd id` | 0 | the rule is listed as before; only making a new id is refused |
| 13t | `rules add TEAM016 --for method --title "Public methods" --where isOverride=false --require writtenAccess=public`, then `rules try TEAM016` on a class with `protected override void OnKeyDown(int key)` (line 3) and `protected void Helper()` (line 7) | 0 | line 3 `not covered: isOverride is true, the rule looks only at false`; line 7 `breaks it`; `1 breaks it, 0 follows it, 1 not covered.` |
| 6.2b | `catalog csharp method` | 0 | lists `isOverride`, true or false, reads the file, "Whether the method is written with the override keyword." |
