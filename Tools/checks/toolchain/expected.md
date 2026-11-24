# Toolchain checks: expected results

Test solution (built from nothing for each case, with a local bare remote): `Library` (net9.0), `Consumer`
(net9.0 and net11.0, references Library), `Tool` (net11.0, references Consumer), a README, a root
`.editorconfig` with IDE0044 at error, IDE0051 at warning and IDE0060 at suggestion, and a `commitfence.json`.
Coverage line formats printed by the command line: `Name (framework): reviewed[: note]` and
`Name (framework): not reviewed: <reason>`; a finding reads
`file(line,col): error|warning RULE: message [Project, frameworks]`.

Each case also checks: the hook's exit code, whether the remote moved, and (case 11, after every case) that the
developer's working folder, index, HEAD, branches, stash and status are unchanged and that the private copy holds
no file inside the tree other than the commit's files and build outputs (bin, obj).

| Case | Change | Hook exit | Remote moved | Findings | Coverage lines |
| --- | --- | --- | --- | --- | --- |
| 1 | a new static method in Library | 0 | yes | none | Library (net9.0), Consumer (net9.0), Consumer (net11.0), Tool (net11.0): all `reviewed`; summary says `4 of 4 builds checked` |
| 2 | a type error (`int x = "text";`, CS0029) in Library/Greeter.cs | 1 | no | CS0029 at Library/Greeter.cs and its line, `[Library, net9.0]` | Library (net9.0) `reviewed: the build fails: 1 compile errors ...`; Consumer (net9.0), Consumer (net11.0), Tool (net11.0) `not reviewed` with "prerequisite ... did not succeed"; none of them is shown `reviewed` |
| 3 | Library removes `Hello`, which Consumer/Use.cs calls under both frameworks; Consumer untouched | 1 | no | CS0117 in Consumer/Use.cs on the call line, `[Consumer, ...]` naming both frameworks (reported once; the order of the two names is alphabetical: net11.0, net9.0) | Library reviewed; Consumer (net9.0) and Consumer (net11.0) `reviewed: ...`, Tool (net11.0) `not reviewed` (prerequisite) |
| 4 | Library removes `Old`, which Consumer uses only under `#if NET9_0` | 1 | no | CS0117 in Consumer/Use.cs, `[Consumer, net9.0]` and not net11.0 | Consumer (net11.0) reviewed and Tool (net11.0) reviewed (their prerequisite succeeded) |
| 5a | a static field never reassigned added to Library, IDE0044 at error | 1 | no | IDE0044 error at the new line, `[Library, net9.0]` | Library reviewed |
| 5b | the same, with IDE0044 at warning in the committed `.editorconfig` | 0 | yes | IDE0044 shown as `warning` at the new line; the same push also adds an unused parameter, so IDE0060 (suggestion) is shown as `warning` too | all four reviewed |
| 5c | the breach (IDE0044 at error) is already in Tool.cs on a line the push does not touch; the push adds another method to Tool.cs | 0 | yes | no IDE0044 | Tool (net11.0) `reviewed` (its build fails on old code; nothing it depends on is affected) |
| 6 | a new field with the breach, wrapped in `#pragma warning disable IDE0044` | 0 | yes | none | all four reviewed |
| 7 | a new `scripts/Stray.cs` no project compiles | 2 | no | none | `scripts/Stray.cs: not reviewed: not covered ...`; summary says the review is incomplete |
| 8 | `global.json` asking for SDK 99.0.100 (rollForward disable) plus a Library edit | 2 | no | none | `C# build: not reviewed: The machine is not ready: no installed .NET SDK fits the global.json ...` |
| 9 | Library gets a PackageReference to a package that is not on the machine | 2 | no | none | each unit of Library and its dependents `not reviewed` with "packages it needs are not on this machine, and a review never uses the network"; a proxy listener sees 0 connections during the review (a plain `dotnet restore` of the same tree, as a control, makes at least 1) |
| 10 | only README.md changes | 0 | yes | none | no build coverage line; no logs folder created for the repository |
| 11 | after every case above | | | | working folder, index, HEAD, branches, tags, stash, status unchanged; no foreign file in the private copy |


## Cases added for the seven faults of the review of the uncommitted code

| Case | Change | Hook exit | Remote moved | Findings | Coverage lines |
| --- | --- | --- | --- | --- | --- |
| 12 | Library.csproj sets its intermediate folder to an absolute folder outside the tree; that folder holds a file named `Library.dll` (decoy); the push adds a method to Library | 2 | no | none | `Library (net9.0): not reviewed: ...outside the private copy...`; after the review the decoy file is still there with its bytes unchanged |
| 13 | the push only deletes `Library/Library.csproj`; Consumer still references it | 1 (the compiler runs on Consumer and reports a real error, `CS0103: The name 'Library' does not exist`, and a blocking finding outranks an incomplete review) | no | any | Consumer is selected and compiled: a coverage line `Consumer (net9.0):` exists |
| 14 | Library compiles the linked `..\Shared\Types.cs` and uses it (pushed without the hook); the push only deletes `Shared/Types.cs` | 1 | no | the build of Library fails | `Library (net9.0): reviewed: the build fails`; the output names `Types.cs`; Consumer and Tool not reviewed (prerequisite) |
| 15 | a tree with no project at all (the three projects removed, pushed without the hook); the push adds `src/Stray.cs` | 2 | no | none | `src/Stray.cs: not reviewed: not covered`; the review is incomplete |
| 16 | Library has a PackageReference to a package that is not on the machine and `RestoreAdditionalProjectSources` pointing at the local listener (a plain restore as control must connect at least once) | 2 | no | none | Library and its dependents `not reviewed` with "a hook run never uses the network"; the listener sees 0 connections during the review |
| 17 | `AppB` (net11.0) and `Consumer`/`Tool` both build on Library; Consumer needs an uncached package; the push adds a method to Library and a breach (IDE0044 at error) to `AppB/Helper.cs` | 1 | no | IDE0044 error in AppB/Helper.cs, `[AppB, net11.0]` | `Library (net9.0): reviewed`, `AppB (net11.0): reviewed`; `Consumer (net9.0)`, `Consumer (net11.0)` and `Tool (net11.0)` `not reviewed`; the packages message appears; the healthy library is not named as failing |

Case 11 (state unchanged) runs after each of these too.

## More cases

| Case | What is done | Hook exit | Remote moved | What is printed |
| --- | --- | --- | --- | --- |
| 18 the network is blocked | a clean push of Library, with every proxy variable pointing at the local listener of case 9 and the review started as usual | 0 | yes | `Library (net9.0): reviewed`; the listener sees 0 connections during the review; a plain restore as control connects at least once |
| 19 no dotnet | the program is started with a PATH that holds the folder of git and nothing else, so `dotnet` cannot be started | 2 | no | `The machine is not ready`, `the dotnet command cannot be started`; nothing is built; no finding |
| 20 a build that hangs | Library.csproj has a target before `CoreCompile` that runs a command sleeping for 300 seconds; `COMMITFENCE_PROCESS_LIMIT_SECONDS=20` | 2 | no | `Library (net9.0): not reviewed: the build did not finish` and `did not finish within 20 seconds`; no `ping` process of the target is left running; the push takes less than 120 seconds; never a pass |
| 21 `#if` under two frameworks | Consumer targets net9.0 and net11.0; the push adds `Consumer/Gate.cs` with `#if NET11_0_OR_GREATER` around a braced single-line if (TEAM010) | 1 | no | one finding line ending `[Consumer, net11.0]` and never `net9.0` |
| 21b `#if` held by both | the same code under `#if NET9_0_OR_GREATER` (true for net9.0 and net11.0) | 1 | no | one finding line, without a `[Consumer, ...]` suffix (it holds for every way the file is built) |
| 21c `#if` held by none | the same code under `#if NEVER_DEFINED` | 0 | yes | no finding |
| 21d `#if` and a rule that needs meaning | `commitfence.json` gains TEAM011 (only public and private methods inside an internal class); the push adds `Consumer/Hidden.cs`, an internal class with a `protected` method inside `#if NET11_0_OR_GREATER` | 1 | no | exactly one TEAM011 finding, at the line of the protected method: the method exists only as net11.0 compiles the file, and the rule still sees it |
| 21e `#error` in a branch no build compiles | the push adds `Consumer/Guard.cs`: code under `#if NET9_0_OR_GREATER` and `#error` under its `#else` (both frameworks define the symbol, so the build is clean) | 0 | yes | `Consumer (net9.0): reviewed` and `Consumer (net11.0): reviewed`; no `does not parse`; the file is never read with no symbol when a build says how it is compiled |
| 21f symbols separated by a comma | Consumer.csproj sets `DefineConstants` to `$(DefineConstants);FEATURE_A,FEATURE_B`; the push adds the braced single-line if under `#if FEATURE_B` | 1 | no | one TEAM010 finding at the line of the if, without a `[Consumer, ...]` suffix: the comma separates two symbols, as it does for the compiler |
| 22 what the build sees | Library.csproj has a target that writes the values of `DOTNET_ADD_GLOBAL_TOOLS_TO_PATH`, `DOTNET_NOLOGO`, `DOTNET_CLI_TELEMETRY_OPTOUT` and `DOTNET_CLI_HOME` into a file chosen by the check; the check's own shell has no `DOTNET_CLI_HOME` | 0 | yes | the file says `0`, `1`, `1` and nothing for the fourth: the program sets the first three for every dotnet it starts and never sets `DOTNET_CLI_HOME` |

Case 11 (state unchanged) runs after each of these too.

## Cases added for the files the build reads that are not C#

A changed file with the extension `.props` or `.targets` selects every project for a build, because only the build says which project imports it. Any other changed file that is not a `.cs` and lies in the folder of a project, or below it, selects that project (and its dependents) exactly as a change to one of its `.cs` files does. No syntax rule runs on such a file; the build's verdict is the evidence. A changed file outside every project folder that is not `.props` or `.targets` stays a note, as before.

| Case | Change | Hook exit | Remote moved | What is printed |
| --- | --- | --- | --- | --- |
| 23 an invalid resource file | the push only adds `Library/Strings.resx`, which is not valid XML | 1 or 2, never 0 | no | `Library (net9.0): not reviewed: the build failed before the compiler ran`, naming `Strings.resx : error MSB3103` (an invalid resource file); the other three units `not reviewed` (prerequisite) |
| 24 a harmless file inside a project | the push only adds `Library/notes.txt` | 0 | yes | `Library (net9.0): reviewed`, and every project that depends on it (`Consumer (net9.0)`, `Consumer (net11.0)`, `Tool (net11.0)`) `reviewed`; summary says `4 of 4 builds checked`; the sentence "of no known language were not looked at" is not printed |
| 25 a custom props file | Library imports `build/common.props`, which defines the symbol `NEEDED` that `Library/Greeter.cs` needs (pushed without the hook); the push only changes `build/common.props` so that it no longer defines `NEEDED` | 1 or 2, never 0 | no | `Library/Greeter.cs(29,16): error CS0103: The name 'Needed' does not exist in the current context [Library, net9.0]`: the error is in a file the push did not change and is still reported; `Library (net9.0): reviewed: the build fails`; Consumer and Tool `not reviewed` (prerequisite) |
| 26 a file outside every project | only `README.md` at the root changes, all projects being in sub-folders (case 10, with its note asserted) | 0 | yes | `1 changed file of no known language was not looked at.`; no build coverage line |

## Case 27: an old breach of a rule is let go

Only what a change touches is judged: an old breach of a rule is let go.

`Library` holds an old breach of IDE0044 (set to `error` in the `.editorconfig`), pushed without the hook. The push then changes one unrelated line in `Consumer/Use.cs`; `Consumer` depends on `Library`. A breach of a rule (a diagnostic whose id does not start with `CS`) never fails a project's build in a review: when the first build fails only because of such errors, the unit is built once more with exactly those rules let through, and that second build is the evidence.

Changed verdict (old, written for the earlier behaviour: exit 2, the remote does not move, `Library (net9.0): not reviewed: ... it does not build: ...IDE0044`, the three other units `not reviewed` because of their prerequisite, summary `0 of 4 builds checked ... 4 could not be reviewed`). New:

- Exit 0, the remote moves.
- `Library (net9.0)` is not reported as `not reviewed` (it is needed first and it builds); `Consumer (net9.0)`, `Consumer (net11.0)` and `Tool (net11.0)` are `reviewed`; the summary says `3 of 3 builds checked`.
- Nothing is reported: no IDE0044 anywhere.

## Cases 30 to 34: the same decision at its edges, and a repository's own build slot

| Case | Setup (pushed without the hook) and change | Hook exit | Remote moved | What is printed |
| --- | --- | --- | --- | --- |
| 30 a second breach on new lines | old breach of IDE0044 in `Library/Greeter.cs`; the push adds a second field with the same breach on new lines of the same file | 1 | no | exactly one finding, `error IDE0044` at the line of the new field; the line of the old breach is not reported; `Library (net9.0)` is `reviewed` |
| 31 an unrelated line in the file with the old breach | the same old breach; the push changes the text of a string in another method of the same file | 0 | yes | no IDE0044; `Library (net9.0): reviewed` |
| 32 an old breach and a real compile error | the same old breach; the push adds a method with `int x = "text";` (CS0029) to `Library/Greeter.cs` | 1 | no | `error CS0029` at its line, `Library (net9.0): reviewed: the build fails`; no IDE0044 (the old breach is not reported, and a compile error is not let through) |
| 33 the project's own NoWarn holds | `Library.csproj` has `GenerateDocumentationFile` true, `NoWarn` CS1591 and `WarningsAsErrors` CS1591 (replacing its NoWarn would turn every undocumented public member into an error); old breach of IDE0044; the push changes an unrelated line | 0 | yes | no IDE0044, no CS1591; `Library (net9.0): reviewed` |
| 34 the repository's own CustomAfterDirectoryBuildTargets | `Directory.Build.props` sets `CustomAfterDirectoryBuildTargets` to `build/repo.targets`, which defines the symbol `NEEDED` that `Library/Greeter.cs` needs (without it the compile fails with CS0103); a clean push of one unrelated line in Library | 0 | yes | all four units `reviewed`, no finding; `4 of 4 builds checked` |

Cases 5a, 5b and 5c (a new breach at `error`, `warning`, `suggestion`) keep their verdicts. Case 11 (state unchanged) runs after each of these too.

## Cases 28 and 29: a build file with a standard name, and a file that leaves its project

| Case | Change | Hook exit | Remote moved | What is printed |
| --- | --- | --- | --- | --- |
| 28 a `Directory.Build.targets` imported from another folder | Library imports `sib/Directory.Build.targets` by name (pushed without the hook; no project lives under `sib`); the push only changes that file so that it stops the build with an error | 2 (the build stops with an MSBuild error before the compiler runs, so there is no finding, no unit is reviewed and the review is incomplete) | no | a coverage line for `Library (net9.0)`; the push is not accepted |
| 29 a needed file moved out of its project | Library embeds `Library/Data.txt` by name (pushed without the hook); the push only moves that file to `docs/Data.txt`, where no project is | 1 (the compiler runs and reports `CS1566: Error reading resource 'Library.Data.txt'`, a blocking finding, which outranks the units that could not be reviewed) | no | a coverage line for `Library (net9.0)`; the push is not accepted |

Case 11 (state unchanged) runs after each of these too.

## Cases 35 and 36: the repository's own build slot, named awkwardly

Both are case 34 with another name for the repository's own file. The verdict is the same: hook exit 0, the remote moves, all four builds reviewed, no error.

| Case | The file the repository keeps | What it names in `CustomAfterDirectoryBuildTargets` | Why |
| --- | --- | --- | --- |
| 35 a comma in the file's name | `build/repo,team.targets` | the same path | The build reads a comma in a property given on its command line as the start of another property; the path must arrive whole |
| 36 a wildcard | `build/checks/needed.targets` | `build/checks/*.targets` | The build expands a wildcard in an import; the file must still be imported |


## Case 37: the build tool answers something the program did not expect

A stand-in `dotnet` (a small program the case builds, put first on the PATH of the hook only) passes every question to the real `dotnet` except the project evaluation `msbuild <project> -getProperty:TargetFramework ...`, which it answers with `{}`: valid JSON, with none of the keys the program reads. No kind of failure that the program lists for this answer fits, so before the failure boundary of the toolchain stage the review ended as `CommitFence could not finish`.

| Case | What is done | Hook exit | Findings | Output | Remote |
| --- | --- | --- | --- | --- | --- |
| 37 an unexpected answer of the build tool | a clean change in Library; the hook runs with the stand-in `dotnet` first on its PATH | 2 | none | a coverage line `C# build: not reviewed: the build could not be run: KeyNotFoundException`, and the summary says the review is incomplete; no line says `could not finish` | not moved |

Case 11 (state unchanged) runs after this case too.
