# Changelog

What changed in each published release of CommitFence, newest first. Each section is also the text of that release on
GitHub, with its downloads.

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
