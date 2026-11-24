# Meaning and structure checks: expected results

Test repository "shop", built from nothing by run.sh, with a local bare remote and the team's
pre-push hook (which calls the real `commitfence.exe` built from `App\CF.Cli`).
The baseline commit is pushed with --no-verify; the commit under review is pushed through the hook.

Layout (net11.0 unless said):

    src/Shop/Shop.csproj
    src/Shop/Domain/PriceCalculator.cs   line 3 `public class PriceCalculator`
    src/Shop/Domain/PriceFactory.cs      line 5 return type PriceCalculator, line 7 `new PriceCalculator()`
    src/Shop/Domain/OrderService.cs      line 5 field of type PriceCalculator, line 9 `_calculator.Total`,
                                         line 14 `var calculator = PriceFactory.Create();` (type only inferred), line 15 `calculator.Total`
    src/Shop/Infrastructure/SqlHelper.cs
    src/Shop.Reports/Shop.Reports.csproj (references Shop)
    src/Shop.Reports/MonthlyReport.cs    line 9 `var calculator = new PriceCalculator();`
    src/Shop.Reports/LegacyReport.cs     uses Shop.Infrastructure.SqlHelper (the old violation)
    commitfence.json: TEAM010, TEAM011, TEAM012 and structure: modules domain = folder src/Shop/Domain/**,
        infrastructure = folder src/Shop/Infrastructure/**, reports = project Shop.Reports;
        allow: infrastructure -> domain, reports -> domain.

"Finding lines" below means the distinct line numbers of the findings of that rule in that file.

## Case 1: the acceptance test
Commit: `git mv` of PriceCalculator.cs from Domain to Infrastructure, namespace kept (Shop.Domain). Nothing else changes. Both trees compile.
- Hook exit 1; remote did not move.
- ARCH001 in src/Shop/Domain/OrderService.cs, finding lines {5, 9, 14}: callers nobody touched, line 14 being the `var` whose type is only inferred.
- ARCH001 in src/Shop/Domain/PriceFactory.cs, finding lines {5}.
- ARCH001 in src/Shop.Reports/MonthlyReport.cs, finding lines {9}: the caller in another project.
- No ARCH001 anywhere in src/Shop.Reports/LegacyReport.cs (the violation that was already there).
- No other ARCH001 file. No "not run". Coverage: Shop (net11.0) and Shop.Reports (net11.0) reviewed.
- First review (both trees built) and, in case 1t, the time of a second review are recorded.

## Case 1b: missing evidence, the closing entry (same commit as case 1, plus a Directory.Build.targets that removes our analyzer from Shop.Reports only)
- Hook exit 2; remote did not move.
- Output says ARCH001 was not run; Shop.Reports (net11.0) is "not reviewed" and the reason names the missing closing entry.
- No ARCH001 finding is printed (answers with a gap are never judged).

## Case 1c: a build that stops early (same move, plus a compile error in Shop)
- Hook exit 1 (the compile error blocks); remote did not move.
- A CS error in Shop is reported; Shop.Reports is "not reviewed: its prerequisite ... did not succeed".
- ARCH001 is named as not run. No ARCH001 finding.

## Case 1t: times
Baseline pushed with --no-verify. Push 1: a new method in Shop (clean). Push 2: another new method in Shop (clean).
- Both exit 0, the remote moves. Push 1 builds before and after (nothing kept), push 2 finds the before tree kept
  (the tree after of push 1). Push 2 must be faster than push 1 and the data folder holds the kept answers file(s).
  Times are printed, not asserted.

## Case 2: a second forbidden use in another method of a file that already has one
Baseline: the move of case 1 is already in the baseline (old violations in OrderService, PriceFactory, MonthlyReport).
Commit: OrderService gets a new method `Extra` that does `return new PriceCalculator().Total(a);`.
- Exit 1; exactly one ARCH001 finding, in src/Shop/Domain/OrderService.cs, at the line of `new PriceCalculator()` in `Extra`.
- None of the old violations is printed.

## Case 3: a push that only moves lines in a file with an old violation
Baseline as case 2. Commit: three comment lines inserted at the top of OrderService.cs.
- Exit 0; remote moves; no ARCH001; the build coverage says both projects reviewed.

## Case 4: TEAM012
Commit adds src/Shop/Domain/Clock.cs with `System.DateTime.Now` (line 9), `DateTime.Now` (line 14), and `Now` through
`using static System.DateTime;` (line 19), and src/Shop/Domain/Own.cs with a type of the team's own called DateTime that has a static Now, used by `DateTime.Now`.
- Exit 1; TEAM012 finding lines in Clock.cs {9, 14, 19}; no TEAM012 in Own.cs.

## Case 4b: one use of a member is one finding
Commit adds src/Shop/Domain/Year.cs with `return System.DateTime.Now.Year;` (line 7), the use of `System.DateTime.Now` followed by another member.
- Exit 1; remote did not move; exactly one TEAM012 finding, on the line of the use, at the column of the member `Now` (column 32: eight spaces, `return `, then `System.DateTime.` is 16 characters). Before the fix the same use was reported twice, once at the start of `System.DateTime.Now` and once at `Now`.

## Case 5: TEAM011 on partial classes
Commit adds PartA.cs `public partial class Wide`, PartB.cs `partial class Wide` with `protected void Extend()`
(read from one file alone, the type looked internal and the method protected), and HiddenA.cs `internal partial class Hidden`,
HiddenB.cs `partial class Hidden` with `internal void Quiet()` at line 5.
- Exit 1; exactly one TEAM011 finding, in src/Shop/Domain/HiddenB.cs line 5; none for PartB.cs.

## Case 5o: TEAM011 lets an override through
The settings file's TEAM011 is: where the type is a class, declared internal, and `isOverride` is false.
Commit adds KeysBase.cs, a public class with a protected and an internal virtual method, and Keys.cs, `internal class Keys : KeysBase` with `protected override void OnKeyDown(int key)` (line 5), `internal override void Measure()` (line 9),
`protected void Helper()` (line 13), `public void Open()` and `private void Close()`.
- Exit 1; exactly one TEAM011 finding, in src/Shop/Domain/Keys.cs line 13 (the protected helper); none for the overrides, the public and the private method.

## Case 6a: the repository tries to switch our entries off
Baseline already holds a Directory.Build.props with RunAnalyzers=false, RunAnalyzersDuringBuild=false, NoWarn and WarningsAsErrors naming CFM001;CFM999,
and an .editorconfig with dotnet_diagnostic.CFM001.severity = none, CFM999 = error, dotnet_analyzer_diagnostic.severity = none.
Commit adds Clock.cs with `System.DateTime.Now`.
- The build still works ("Shop (net11.0): reviewed", no "the build fails"); exit 1 with TEAM012 reported at the line of Now.

## Case 6b: warnings as errors
Baseline with TreatWarningsAsErrors=true, WarningsAsErrors CFM001;CFM999 and .editorconfig dotnet_analyzer_diagnostic.severity = error, CFM001 = error.
Commit adds Clock.cs with `System.DateTime.Now`.
- Build succeeds; exit 1 with TEAM012 reported; no "the build fails".

## Case 6c: every analyzer diagnostic is an error
Baseline with an .editorconfig `dotnet_analyzer_diagnostic.severity = error`. Commit adds Clock.cs with `System.DateTime.Now`.
- Exit 1 and the remote does not move, as before. Old: `Shop (net11.0): reviewed: the build fails: 0 compile errors, N errors of rules set to error`. New: the build does not fail (the rules are let through in a second build); the rule errors on the new lines are reported as findings (`Clock.cs(5,28): error CA1822`, `error IDE0160`), plus TEAM012 at the member `Now`; no "the build fails"; none of our entries is among the errors.

## Case 7: SDK 9.0
Shop with TargetFramework net9.0 and a global.json pinning 9.0.320. The commit of case 1.
- The same findings as case 1 (exit 1, same files and lines); the compiler log names the 4.14 compiler (the .NET 9 SDK's).

## Case 8: review --files, no build
`commitfence review --files src/Shop/Domain/Clock.cs --settings commitfence.json`, the file holding no TEAM010 breach.
- Exit 2. Output names TEAM011, TEAM012 and ARCH001 as "not run:"; no finding.
Then with a file that has a braced single-line `if` (TEAM010): exit 1, the TEAM010 finding printed and the same three "not run:" lines.

## Case 9 (after every case)
Working folder, index, HEAD, branches, tags, stash and status unchanged; the private copy's tree holds no file but the commit's files and build outputs.

## Case 10: CommitFence on itself (a clone of the repository under the scratch folder)
- `review --all` of the clone (the whole tree as of its last commit, so it does not depend on how many commits the history has; the clone's last commit holds the working folder's files, empty when they are the clone's own), whose files are the project's: exit code exactly 0 (nothing blocks and the review is complete; warnings do not change the code), the summary says `: 0 blocking,`, and no `: error ` line is printed, so TEAM013 and the other rules do not block the unchanged repository.
- Then a line in CF.Languages.CSharp (which may not use the Adapters) that uses a type of CF.Adapters, with the project reference added in the clone: it compiles; the review exits 1 and refuses it with ARCH001 (csharp must not use adapters), naming the file.
- Then, in the clone taken back to before that line, a file in CF.Core that calls `System.IO.File.ReadAllText`: the review exits 1 and TEAM013 (Core uses no file, process or network API) blocks it, naming the file.

# Part 2: the three faults found in the review of the uncommitted code

## Case 11: dependencies that have no name in the source (fault 1)
A second test repository "forms", built by run.sh, with a bare remote and the hook as above. Unlike "shop" its modules are:
domain = namespace Shop.Domain, infrastructure = namespace Shop.Infrastructure, factory = namespace Shop.Factory (namespaces, not folders: see the finding in the report about folder modules and untouched projects),
reports = project Shop.Reports. allow: infrastructure -> domain, factory -> infrastructure, factory -> domain, reports -> domain, reports -> factory.
So Shop.Reports may call the factory but must not use anything of infrastructure.
- src/Shop/Infrastructure/Rich.cs (forbidden world) holds `Rich` (indexer on int, Count, Name, `+`, unary `-`, implicit conversion to int and from int,
  explicit conversion to long, Add, GetEnumerator returning a RichEnumerator, Dispose, GetAwaiter returning a RichAwaiter, Deconstruct, Fn),
  `RichEnumerator`, `RichAwaiter`, `IRichContract`, `RichAttribute` and `Extensions` (`Twice(this int)`).
- src/Shop/Factory/Source.cs: `Make()` returns Rich, `Accept(Rich)`, `Run(Func<Rich, int>)`, `Wrap<T>(Func<T>)`, `Maker` (a `Func<Rich>`).
- Case 11 pushes, one by one on top of the baseline (the remote is reset after each refusal), ONE new file src/Shop.Reports/Forms/<Form>.cs.
  Every dependency of the form on the forbidden layer is on a line marked `// L` in the file. The type's name is not written in the file,
  except in the forms where the form is the name (attribute, is, as, cast, typeof, nameof, base type, interface, return type, parameter type).
  Expected for EACH form: hook exit 1, remote not moved, "Layer direction: reports must not use infrastructure." printed, no `error CS`,
  ARCH001 findings only in that file, and the distinct finding lines are exactly the lines marked `// L`.
  A finding is one per member and used type, at the first place of the use in that member, so a form puts its dependency in one member on one line.
  Forms: IndexerRead (`return Source.Make()[0];`), IndexerWrite, IndexFromEnd (`[^1]`),
  Binary (`+`), Unary (`-`), ImplicitTo (`int n = Source.Make();`), ImplicitFrom (`Source.Accept(5);`), Explicit (`(long)`), TargetNew (`Source.Accept(new());`),
  ObjectInit, CollectionInit (the Add that is called), Foreach, Using, Await, Deconstruct, Extension (`3.Twice()`), Lambda (`Source.Run(r => 1);`),
  DelegateVar (`var maker = Source.Maker;`, the Func<Rich>), Attribute (`[Rich]`), Is, As, Cast, PatternProp (`is { Count: 1 }`), TypeOf, NameOf,
  GenericInferred (`Source.Wrap(Source.Make);`, T inferred), BaseType (`: Rich`), InterfaceType (`: IRichContract`), ReturnType, ParamType, VarLocal.
  (31 forms.)
- Case 11c, the control: the same 31 files, in a repository whose `Rich` family is in Shop/Domain (allowed for reports) and whose Source returns it,
  in ONE push: exit 0, the remote moves, no ARCH001, "Shop.Reports (net11.0): reviewed", no `error CS`. Working folder untouched (check 9).

## Case 12: a use in a field initializer belongs to its own declarator (fault 2)
Repository "fields": the forms modules as in case 11; baseline src/Shop.Reports/Fields.cs, a class with
  `private static readonly object _a = new Shop.Infrastructure.Rich(),\n        _b = 1;`   (old forbidden use in _a; the name is written, so this case does not depend on fault 1),
  `public static event Func<int>? E1 = Source.Make().Fn,\n        E2 = null;`   (old forbidden use in E1),
  `public static object P1 { get; } = new Shop.Infrastructure.Rich();`   (old forbidden use in P1).
- 12a: `_b = 1` becomes `_b = new Shop.Infrastructure.Rich()`. Refused (exit 1), exactly one distinct finding line: the line of `_b`; the line of `_a` is not reported.
- 12b: `E2 = null` becomes `E2 = Source.Make().Fn`. Refused, exactly one distinct line: the line of E2; E1's line is not reported.
- 12c: a new property `P2 { get; } = new Shop.Infrastructure.Rich();` is added (a property has one declarator). Refused, one distinct line: the line of P2; P1's line is not reported.
- 12d: control: `_b = 1` becomes `_b = 2` and a property `P3` with initializer `3` is added: exit 0.

## Case 13: two projects compile one linked file (fault 3)
Repository "linked": src/Shared/Linked.cs (`namespace Shared; public class Linked`, method Value), projects Shop.A and Shop.B that both compile it as a
link (`..\Shared\Linked.cs`), Shop.CA (references Shop.A) and Shop.CB (references Shop.B), each with a Consumer class that does not use Linked.
Modules: shared = folder src/Shared/**, consumer = project Shop.C*; allow: shared -> consumer (nothing else, so consumer -> shared is forbidden).
- The push changes both Consumer classes to `return new Shared.Linked().Value();`, and adds a harmless file to Shop.A and to Shop.B so that both compile in the review.
- Expected: exit 1, remote not moved, ARCH001 finding in src/Shop.CA/Consumer.cs AND in src/Shop.CB/Consumer.cs (one of them is the "second assembly"),
  each at the line of `new Shared.Linked()`, message "Layer direction: consumer must not use shared.".
- Control 13c: the same push but with Consumers that do not use Linked: exit 0.

## Case 14 (existing cases)
All earlier cases (1 to 10) pass unchanged; the acceptance test of case 1 is unchanged. Case 10 on the plug-in/adapters is still refused.

## Measurements (printed, not asserted)
Log size (KB) of case 1 (data/c1-*/logs) and of case 10 (data/self-*/logs), and the times of case 1, 10 and 10b, before and after the change:
before: logs c1 1896 KB, self 23840 KB (both reviews of the clone); times case 1 8628 ms, case 10 35984 ms, case 10b 19723 ms.

# Part 3: the hole of untouched projects under modules named by folder

The hole: a use of a type declared in another project is traced to that type's file only when that project is compiled in the same review.
A module named by folder matches only a use whose file is known. So a push that touches only a consumer and adds a forbidden use of a type
of an untouched project passed when the modules were named by folder. The fix: such a use is reported as "ARCH001 was not run" (naming the using file
and member, the used type and its assembly) and the review is incomplete (exit 2). The use is "open" only when a module named by folder, listed before the
module the namespace and assembly alone give (or before all, when they give none), could hold the type and the using module may not use that module.

Test repository "split", built from nothing by run.sh, with a bare remote and the hook as above:

    src/Shop.Shared/Shop.Shared.csproj
    src/Shop.Shared/Forbidden/Secret.cs   namespace Shop.Shared.Forbidden, public class Secret { int Value() }
    src/Shop.Shared/Allowed/Open.cs       namespace Shop.Shared.Allowed,   public class Open   { int Value() }
    src/Shop.Consumer/Shop.Consumer.csproj (references Shop.Shared)
    src/Shop.Consumer/Consumer.cs         class Consumer, method Go() returns 1 (the baseline, pushed with --no-verify)
    modules: consumer = project Shop.Consumer; forbidden and allowed named by folder (case 14a, 14c, 14d) or by namespace (case 14b):
        forbidden = Shop.Shared.Forbidden (folder src/Shop.Shared/Forbidden/** or namespace Shop.Shared.Forbidden),
        allowed   = Shop.Shared.Allowed   (folder src/Shop.Shared/Allowed/**   or namespace Shop.Shared.Allowed)
    directions: allow consumer -> allowed (so consumer -> forbidden is a breach).

## Case 14a: modules by folder, the push touches only the consumer and uses a type of the forbidden folder
Commit: Consumer.Go returns `new Shop.Shared.Forbidden.Secret().Value()`. Shop.Shared is not touched, so it is built (prerequisite) but not reviewed.
- Hook exit 2; the remote did not move.
- Output says "ARCH001 was not run:" with a reason that names src/Shop.Consumer/Consumer.cs, the member (Shop.Consumer.Consumer.Go), the type Shop.Shared.Forbidden.Secret and the assembly Shop.Shared.
- No ARCH001 finding is printed (the use cannot be placed, so it is not judged). Shop.Consumer (net11.0): reviewed. No "error CS".
- The summary says the review is incomplete.

## Case 14b: the same push with the modules named by namespace
- Hook exit 1; the remote did not move. ARCH001 finding in src/Shop.Consumer/Consumer.cs at the line of `new Shop.Shared.Forbidden.Secret()`,
  "Layer direction: consumer must not use forbidden."; no "not run:" line (the namespace alone places the type).

## Case 14c: modules by folder, the push touches both projects
Commit: the same change in Consumer, plus a harmless file src/Shop.Shared/Extra.cs. Shop.Shared is compiled in the review, so the file of Secret is known.
- Hook exit 1; the remote did not move; ARCH001 finding in Consumer.cs at the line of the use, "Layer direction: consumer must not use forbidden."; no "not run:" line; Shop.Shared (net11.0) and Shop.Consumer (net11.0) reviewed.

## Case 14d: modules by folder, the push touches only the consumer and uses a type of an ALLOWED folder
Commit: Consumer.Go returns `new Shop.Shared.Allowed.Open().Value()`. Shop.Shared is not touched.
- Hook exit 2; the remote did not move; "ARCH001 was not run:" naming Consumer.cs, Shop.Consumer.Consumer.Go, Shop.Shared.Allowed.Open and Shop.Shared; no ARCH001 finding.
- Why this is right: the review does not know the file of Open, so it does not know that Open is in the allowed folder; the same text of use
  could as well be a type of the forbidden folder. It is neither a silent pass nor a made-up finding: the review says it cannot tell.
  (The cost: a consumer using any type of an untouched project is incomplete while the modules that could hold it are named by folder and one of them is forbidden to it.)

## Empty reason of "review scope: incomplete"
Not a check of the script (the cause is not reproducible by the script). Every path that reports "review scope" now carries a reason: an exception
without a message is named by its type; a blank reason is "no reason was given"; git exiting with -1 and no error text names the command and the exit code.

## Counts expected
All earlier cases pass unchanged.
Case 10 (CommitFence on itself) may change: if it uses folder modules for untouched projects it would turn incomplete; whatever it reports is listed.

## Case 15

Repository as case 12 (`fields_baseline`, the forms modules, the `reports` module must not use `infrastructure`): the tree already holds three forbidden uses (`_a`, `E1`, `P1`) and the earlier push of them was made without the hook. The push only adds a comment line at the end of `src/Shop.Reports/Fields.cs`, so the review and the layer check run on the project that holds the old violations, and no line of a use is touched.
- Hook exit 0, the remote moves, no ARCH001 finding is printed (a violation that was already there is not reported), and no rule is left not run. The state of the developer is unchanged.

## Cases 16a and 16b: the FIRST push of a repository that has a layer direction

Added after the review of the whole-folder work changed `StructureReview`: a change whose tree before is git's empty tree (the first push of a repository) now builds no tree before, because nothing was there. Nothing pinned that for a repository with structure rules. The repository is the shop of the other layer cases (modules domain, infrastructure and reports; reports and infrastructure may use domain; reports may not use infrastructure). Its only commit is made by the script and pushed through the real hook to an empty remote (no `--no-verify`).

| Case | What is done | Hook exit | Output | Remote |
| --- | --- | --- | --- | --- |
| 16a | the first commit already holds a forbidden use: `src/Shop.Reports/LegacyReport.cs` calls `Shop.Infrastructure.SqlHelper.Quote` (the use that is the "old violation" in the other cases; here nothing was there before, so it is new) | 1 | ARCH001 in `src/Shop.Reports/LegacyReport.cs` at the line of the use, `Layer direction: reports must not use infrastructure.`; no `not run:` line | stays empty |
| 16b | the same first commit without the forbidden use (`LegacyReport.Run` returns its argument) | 0 | no ARCH001, no `not run:` line; both projects `reviewed` | moved |

Case 9 (the developer's state is unchanged, the private copy holds nothing of ours) applies to both.

On a first push the summary does not say "The structure rules changed in this push, so the
tree before and the tree after were both judged by the new rules", because a first push has
no tree before. Cases 16a and 16b expect that the output does not speak of "the tree before".
