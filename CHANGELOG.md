# Changelog

What changed in each published release of CommitFence, newest first. Each section is also the text of that release on
GitHub, with its downloads.

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
