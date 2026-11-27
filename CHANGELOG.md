# Changelog

What changed in each published release of CommitFence, newest first. Each section is also the text of that release on
GitHub, with its downloads.

## 1.0.0.7 — 2026-11-27

### Build servers and editors

- **A SARIF report:** `commitfence review ... --format sarif` prints the findings as a SARIF 2.1.0 log for build servers
  and GitHub code scanning; `--output <file>` writes the report to a file and keeps the plain text on the console. The
  log names the reviewed commit and the remote, names files by their repository paths, shows no path of the machine,
  keeps findings set aside as accepted suppressions, and says when the review could not cover everything. A report
  that cannot be written fails a review that would have passed (exit code 3). The README shows a GitHub Actions job.
- **A JSON Schema for `commitfence.json`**, at `https://commitfence.com/schema/commitfence.schema.json`: an editor
  completes and checks the keys, the things a rule can be about and each one's properties. The file may name it with
  a `"$schema"` key, kept first when the file is saved; such a file asks for CommitFence 1.0.0.7.

### The app

- **Settings sets your own strictness:** "Your own" chooses what `error`, `warning` and `suggestion` do to a push, each
  in its column, saved at once; going back to a preset can be undone, and a refused save can be tried again.
- **Rules:**
  - A new rule's ID is chosen before saving. A refused ID is said once, beside the field, with a way out
    ("Use TEAM006"). A rule of the same ID in the list is marked.
  - A new rule is first in the list, marked "Not saved yet", until it is saved.
  - A rule of `.editorconfig` set in several places lists every place on its own line, with its effect and a link to
    its line. When the places differ, its effect reads "Varies" and the row looks the same under every filter. Under
    a filter, the places that do not match are dimmed.
  - A rule of `.editorconfig` is listed under every effect it has.
  - "Save rule", while a rule cannot be saved, says why and takes you to what is in the way.
  - Every tooltip in the app is dark.
- **Review lists every file a review covered,** one row each, long paths cut at folder boundaries. The files without
  findings open in a list you can filter. A review with no findings shows its files in the Files panel.
- `commitfence stats` says where reviews came from: the hook, the command line or the app's Review.

### Fixes

- A review no longer stops when the address of the remote cannot be read.
- Applying a fix is no longer recorded as a review.
- A finding with no file is no longer counted or listed as a file; a rule chosen in a folder review keeps every file
  listed.
- Review no longer shows an empty card under the files when every clean file is already listed.
- Changing the structure of a file that has a `"$schema"` line or newer rule features raises the version it asks for.
- Strictness saves and Undo are protected from changes made to the file meanwhile.
- Rule tooltips stay inside the window and clear of the condition they describe; shortened values keep whole
  characters.
- Setup paths use one kind of separator.

### Known limits

- The copied setup commands use `&&`: they run in PowerShell 7 and in git's own shells, not in Windows PowerShell 5.1.
- lefthook does not escape the hook's arguments: a remote name or URL holding `"`, `$` or a backtick is read by the shell.
- A copied Windows command whose hook path holds an apostrophe does not run as copied.
- The GitHub code scanning upload has not been tried on a hosted runner yet.

### Downloads

| Download | What |
|---|---|
| `CommitFence-win-x64.zip`, `CommitFence-win-arm64.zip` | The app for Windows, with the command line beside it |
| `CommitFence-osx-arm64.zip`, `CommitFence-osx-x64.zip` | The app for macOS (Apple silicon, Intel) |
| `CommitFence-linux-x64.tar.gz` | The app for Linux |
| `commitfence-cli-*` | The command line, `commitfence`, for the same systems |

---

## 1.0.0.6 — 2026-11-26

### Rules

- **Comparisons:** a condition can be a limit (`atLeast`, `atMost`), a range (both), a value it must not be (`not`), or
  a wildcard pattern (`matches`, with `*` and `?`). A method rule can say
  `"require": { "parameterCount": { "atLeast": 1, "atMost": 5 } }`.
- **New things to look at:** a method's `nestingDepth`; a class's `isNested` and `isNamedLikeFile`; and file rules, `for:
  "file"`, with the file's `name` and `typesInFile`. A file rule applies when the file is added or renamed, or when any
  line in it changes.
- A rule that uses any of these writes `"requires": "1.0.0.6"` into `commitfence.json`. An older CommitFence then says
  the review is incomplete and asks to be updated, instead of misreading the rule.
- **The rule page** sets where a rule applies, what a finding says and why, several requirements, and a comparison that
  fits each property. A rule that cannot be saved says why, in its head and under the part that is wrong. A range reads
  "is between 1 and 5" and cannot be reversed.
- Trying a rule that reads the file's name asks for a file, and shows the command to run with a Copy button.

### Reviews

- **Review the staged changes** in the app, or with `commitfence review --staged`. `--quick` (also with `--files`)
  reviews without a build and leaves out the rules that need one.
- **Open at line:** each finding's code opens its file at the line. When the code on disk moved, is not found or is found
  more than once, the code head says so and the file opens at the top.
- A change loads only the C# projects it can reach, so a change in one product costs what that product alone costs.
- A push is reviewed against the remote git would push to, following `push.default`, and says when git would refuse it.

### Setting a repository up

- **`commitfence init` adds the review line to the `pre-push` hook** of husky, lefthook (its `commands` and its `jobs`
  forms) or a plain git hook, keeping shared and Git LFS hooks intact. `--hook pre-commit` reviews the staged files
  without a build; `--print` shows both changes first.
- **In the app,** Rules can start `commitfence.json` and Doctor can preview the hook line, add it, and give the commands
  that commit only the setup file.

### Fixes

- A review run from a subfolder uses the repository's own settings.
- The branch in the window follows a checkout made outside the app.
- A review that finds no push clears the findings of the review before it.
- A staged or folder review that could not finish says so, even when it also found something.
- The rule of a finding is read from the tree the finding came from.
- A rule that sets two conditions on one property is refused instead of losing one.
- New and Duplicate never propose a rule ID that already exists, also beside a very long one.
- A finding of a rule ID without a description keeps its effect and says so.
- A suppression whose ID is a constant, or cannot be resolved, is kept; Exemptions reads an ID written as joined strings.
- Projects that reference other project types or move their build inputs are reviewed.

### Known limits

- The copied setup commands use `&&`: they run in PowerShell 7 and in git's own shells, not in Windows PowerShell 5.1.
- lefthook does not escape the hook's arguments: a remote name or URL holding `"`, `$` or a backtick is read by the shell.
- A copied Windows command whose hook path holds an apostrophe does not run as copied.

### Downloads

| Download | What |
|---|---|
| `CommitFence-win-x64.zip`, `CommitFence-win-arm64.zip` | The app for Windows, with the command line beside it |
| `CommitFence-osx-arm64.zip`, `CommitFence-osx-x64.zip` | The app for macOS (Apple silicon, Intel) |
| `CommitFence-linux-x64.tar.gz` | The app for Linux |
| `commitfence-cli-*` | The command line, `commitfence`, for the same systems |

---

## 1.0.0.5 — 2026-11-26

### Apply fix

- **A finding of a rule of `.editorconfig` can be fixed from Review.** When the finding is opened, CommitFence prepares
  the fix with the SDK's own formatter and shows the whole change: how many places, the lines added and removed, which
  place is this finding and which another, and that the formatter fixes every occurrence of the rule in the file, also
  on lines the push did not change. "Apply fix" writes exactly what is shown, only while the file is still the one that
  was reviewed; otherwise it writes nothing and says why.
- After a fix, every finding it covers is marked Fixed, the file counts what is still open, and a line under the verdict
  says that the fix is not committed yet: review again to update the verdict.
- **`commitfence fix <rule> <file>`** shows the same change on the command line, and `--apply` writes it. It takes the
  same choice of changes as `review` (`--staged`, `--base` / `--head`, `--all`).
- A fix keeps the file's line endings and byte order mark; when the formatter changes them on purpose, the change says
  so in words.
- Team rules, structure rules and compiler errors have no automatic fix: they keep "Copy as a prompt".

### Review

- A warning's marked line is drawn in the warning's colour, not the colour of a blocking finding.

### Downloads

| Download | What |
|---|---|
| `CommitFence-win-x64.zip`, `CommitFence-win-arm64.zip` | The app for Windows, with the command line beside it |
| `CommitFence-osx-arm64.zip`, `CommitFence-osx-x64.zip` | The app for macOS (Apple silicon, Intel) |
| `CommitFence-linux-x64.tar.gz` | The app for Linux |
| `commitfence-cli-*` | The command line, `commitfence`, for the same systems |

---

## 1.0.0.4 — 2026-11-25

### Reviews

- **A push of several branches is reviewed in full even when git is slow.** When git ran out of time preparing one
  branch, it left a lock behind and the next branch was not reviewed at all, so its findings were missing. Now the
  branch that ran out of time is reported as incomplete, with the reason, and the next one is reviewed.

### The app

- **One theme, dark**, whatever the system's theme.
- **Dashboard:** the headline says how many reviews were blocked or incomplete (an incomplete review also stops a push
  through the hook); the period counts reviews from the hook, the command line and Review alike; a busy day is drawn
  up to the top line with how many more were not drawn, tied to its own day; the chosen day is marked; time per review
  stands under the rules most broken.
- **Exemptions:** repeated exemptions are grouped by rule and by where the rule comes from; a finding opens on its own
  line; each file says how many findings it has; rules the repository does not have get their own heading; "Show all"
  keeps the keyboard in place; one line is counted once.
- **Doctor:** a failed check shows the hook or `global.json` as it is now and after the fix, with every line the fix
  names marked and its first button as the main one; while it checks, each row says whether it is waiting, checking
  or finished; F5 keeps the keyboard where it was.
- **Settings and the update notice:** clearer headings; the download's progress stays beside its words, and "Try
  again" is easy to find.

### Downloads

| Download | What |
|---|---|
| `CommitFence-win-x64.zip`, `CommitFence-win-arm64.zip` | The app for Windows, with the command line beside it |
| `CommitFence-osx-arm64.zip`, `CommitFence-osx-x64.zip` | The app for macOS (Apple silicon, Intel) |
| `CommitFence-linux-x64.tar.gz` | The app for Linux |
| `commitfence-cli-*` | The command line, `commitfence`, for the same systems |

---

## 1.0.0.3 — 2026-11-25

### Architecture

- **The diagram is drawn again.** The lines of the chosen module run at right angles, each in its own place, and never
  cross; the rows are numbered as the layers they are; a module not linked to the chosen one is a plain button with a
  clear edge, and the chosen one has a blue edge.
- **"Every direction" is a list by layer** in place of the table: one row per module with the modules it may use. A use
  that breaks a direction is a red mark with the word "Breaks", first in its row. Directions are allowed and stopped in
  the module's panel; the list does not edit.
- **Modules that may use each other** are drawn with their lines, and the list says that they share a layer.
- The panel's remove buttons and "Used by" rows are larger targets, and under "Allow this direction" a line says what
  it writes to `commitfence.json`.

### Dashboard

- **The chart's axis stands on round numbers** (0, 5, 10, and round steps above that).
- **A rule that found things and is off now** keeps its bar in "Most broken", in a neutral colour with the word "Off".
- When the history of reviews could not be read, the notice says that the numbers may leave reviews out and offers
  "Read again". On a day with more reviews than fit as boxes, the chart says why it draws blocks.

### Settings and Doctor

- **Settings shows an update once:** while Settings is open the update notice at the foot of the page steps back,
  since the status row there has the same words and buttons.
- **Doctor**, while it checks again, says "Last checked" with the time of the results it still shows.
- The heading of an empty screen is the size of the screen's name.

### Downloads

| Download | What |
|---|---|
| `CommitFence-win-x64.zip`, `CommitFence-win-arm64.zip` | The app for Windows, with the command line beside it |
| `CommitFence-osx-arm64.zip`, `CommitFence-osx-x64.zip` | The app for macOS (Apple silicon, Intel) |
| `CommitFence-linux-x64.tar.gz` | The app for Linux |
| `commitfence-cli-*` | The command line, `commitfence`, for the same systems |

---

## 1.0.0.2 — 2026-11-24

### Fixed

- **A review is complete again when a package adds a source file of its own to a project.** With 1.0.0.1 a push that
  touched a test project on xUnit v3 ended as "review incomplete" and was refused, because xUnit v3 compiles one file
  from the package folder. What the build says about a file outside the repository is now left out.

### The five screens, and their commands

- **Dashboard** and **`commitfence stats`:** the reviews of pushes on this machine in the last 30 days, one box per
  review by day (refused, incomplete, allowed), the rules that found the most, the rules that never fired, and how long
  a push waited. The history is kept on your machine, one line per review; a review never waits on it or fails for it.
- **Exemptions** and **`commitfence exemptions list`:** every line of the last commit that sets a rule aside, by file
  and by rule, with the reason it gives. "Narrow the rule" opens the rule on Rules with a new condition.
- **Architecture** and **`commitfence structure show`**, **`structure set`:** the modules in layers, each above the ones
  it may use, with the chosen module's lines drawn and every direction as a table; add, rename and remove modules and
  their parts, allow a direction or stop allowing one, each saved at once and undoable. It says whether the last review
  found a use that breaks a direction, and which.
- **Settings** and **`commitfence settings show`**, **`settings set strictness`:** the strictness of the repository, the
  languages this version reviews, the version with its update status, and the notes of the release.
- **Doctor** and **`commitfence doctor`:** seven checks of whether a push from this machine will be reviewed (the hook
  line first and able to stop a push, the program git finds, the PATH, the .NET SDK, the meaning component, the private
  copy, `.editorconfig`), each with what was found and, when it fails, how to fix it. It changes nothing on your machine.
- **Ctrl+1 to Ctrl+7** open the seven screens; F5 does each screen's "again".

### Rules and reviews

- **Exemptions in the code.** `// commitfence:allow TEAM010: the reason` on the line, or alone on the line above, sets a
  finding of that rule aside; the review says how many were set aside. A comment without a reason sets nothing aside
  and is a finding of its own (`ALLOW001`, it blocks). A compile error cannot be set aside.
- **Strictness.** `"strictness"` in `commitfence.json` is `quiet`, `standard` or `strict`, or a mapping of your own; it
  says what the words `error`, `warning` and `suggestion` of `.editorconfig` do to a push. Standard is what CommitFence
  did before, and is what holds when the key is not there. Team rules keep the effect they were given.
- **A use that breaks a direction** is followed through a renamed file, and is a new one when the file moved to
  another module.

### The app

- **The update notice** is the last row of the page on every screen, with a progress bar and the state of an update
  from "available" to "installed"; the release notes moved to Settings, where "What's new" leads.
- A file rule's page and the new screens wrap long lines of a file in place of cutting them.

### Not in this version

- Architecture does not show or edit reference rules and layout rules: CommitFence reads them and does not judge them
  yet.
- Doctor does not move the hook line for you: it shows the line, says where it belongs and copies it.
- With a strictness mapping of your own in `commitfence.json`, Settings shows it and cannot switch to a preset: remove
  the mapping in the file first.

### Downloads

| Download | What |
|---|---|
| `CommitFence-win-x64.zip`, `CommitFence-win-arm64.zip` | The app for Windows, with the command line beside it |
| `CommitFence-osx-arm64.zip`, `CommitFence-osx-x64.zip` | The app for macOS (Apple silicon, Intel) |
| `CommitFence-linux-x64.tar.gz` | The app for Linux |
| `commitfence-cli-*` | The command line, `commitfence`, for the same systems |

---

## 1.0.0.1 — 2026-11-24

### Changes

- **A review no longer compiles again a project that did not change.** It uses what that project's last compile found,
  and only while the compiler would be given exactly the same things: the same sources, references, analyzers, settings
  and SDK. A project that owns a changed file is always compiled, and anything that cannot be proven the same is
  compiled again.
- **Copy as a prompt.** A finding, or a whole review, is copied as a prompt to hand to an AI coding agent: two buttons
  on the Review screen, and **`commitfence review --format prompt`** on the command line. The prompt names each rule in
  the words the app uses, says whether it blocks a push or only warns, and does not claim all is well when the review
  could not check everything.
- **A review with thousands of findings opens and scrolls at once.** The Review screen makes only the rows in view.
- **A rule of `.editorconfig` that later sections set differently for some files is shown truly:** what holds for all
  C# files, and each exception with its files and its line. In the app that is in the Rules list, on the rule's page and
  on a finding, which shows the effect that holds for its own file and opens `.editorconfig` at the line that set it; on
  the command line, in `rules list` and `rules show`.
- **A finding from the build whose file cannot be placed makes the review incomplete**, where it could be lost before.
  A repository that sits behind a symbolic link is reviewed by its real path.
- **Updates in the app:** after an install succeeds the app shows the files as replaced, and a late progress report no
  longer changes the status once the install has ended.
- **For those who build it:** the tests are xUnit under `Tests`, run by `dotnet test`; the check scripts are gone.

### Downloads

| Download | What |
|---|---|
| `CommitFence-win-x64.zip`, `CommitFence-win-arm64.zip` | The app for Windows, with the command line beside it |
| `CommitFence-osx-arm64.zip`, `CommitFence-osx-x64.zip` | The app for macOS (Apple silicon, Intel) |
| `CommitFence-linux-x64.tar.gz` | The app for Linux |
| `commitfence-cli-*` | The command line, `commitfence`, for the same systems |

---

## 1.0.0.0 — 2026-11-24

The first release. CommitFence reviews the C# code of a push before it leaves your machine and refuses the push when it
breaks a rule your team has written.

### The command line

- **`commitfence install`** sets the program up for your user, and **`commitfence init`** starts a repository's
  `commitfence.json` and prints the one line to add to its `pre-push` hook.
- **`commitfence review`** judges what a push would send (`--push`), what is staged (`--staged`), a range of commits
  (`--base`, `--head`), named files (`--files`) or the whole of a folder (`--all`).
- A review builds the changed projects in a private copy with the team's own compiler, so rules that need meaning, such
  as what a type really is or what a call refers to, are judged on facts and not on guesses. Only what the push touches is
  judged: an old breach in code the push leaves alone is let go.
- It ends with 0 when the push may go, 1 when a rule that blocks is broken, 2 when the review could not be completed and
  3 for wrong settings or a wrong command. It never says 0 for a review it could not finish.

### The app

- **Rules** lists the team's rules and the rules `.editorconfig` sets, shows each as a sentence, and lets you write,
  copy, switch, remove and undo rules without touching the JSON.
- **Try it** runs a rule on code you type or paste, as you type, and says for each place whether it breaks the rule,
  follows it or is not covered.
- **Review** shows what the push would send: the verdict, the findings by file with the code around each, the count by
  rule, and a review of a whole folder. Light and dark themes follow the system.

### Rules

- A rule says what it is for (an artifact, such as an if statement, a method or a use of an API), where it applies, and
  what the code must or must not be. It blocks the push or only warns.
- Rules live in `commitfence.json` at the top of the repository, with the paths each applies to, a message and a reason.
- The rules of `.editorconfig` (the `dotnet_diagnostic` severities) are read too, and shown as blocking or warning.
- Structure rules say which parts of the code may use which, and a push that makes that worse is refused.
- A repository can require a newest-enough CommitFence with the `requires` key.

### Updates

- Both programs update themselves from the releases on GitHub. The app says when a new release is out, shows its notes
  and installs it on "Update now"; **`commitfence update`** does the same at a terminal, and `--check` only looks.
- An update is installed only when the release's signed list of checksums matches the download, and a failed update
  leaves the installation as it was. A hook run and a build server never check.

### Downloads

| Download | What |
|---|---|
| `CommitFence-win-x64.zip`, `CommitFence-win-arm64.zip` | The app for Windows, with the command line beside it |
| `CommitFence-osx-arm64.zip`, `CommitFence-osx-x64.zip` | The app for macOS (Apple silicon, Intel) |
| `CommitFence-linux-x64.tar.gz` | The app for Linux |
| `commitfence-cli-*` | The command line, `commitfence`, for the same systems |
