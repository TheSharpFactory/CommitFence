<p align="center">
  <img src="docs/images/logo.png" alt="The CommitFence logo: three pickets in purple, blue and green" width="96">
</p>

<h1 align="center">CommitFence</h1>

<p align="center">
  <a href="https://github.com/TheSharpFactory/CommitFence/actions/workflows/ci.yml"><img src="https://github.com/TheSharpFactory/CommitFence/actions/workflows/ci.yml/badge.svg?branch=main" alt="CI on Windows and Linux"></a>
  <a href="https://github.com/TheSharpFactory/CommitFence/actions/workflows/ci-macos.yml"><img src="https://github.com/TheSharpFactory/CommitFence/actions/workflows/ci-macos.yml/badge.svg?branch=main" alt="CI on macOS"></a>
  <a href="https://github.com/TheSharpFactory/CommitFence/releases/latest"><img src="https://img.shields.io/github/v/release/TheSharpFactory/CommitFence" alt="Latest release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/TheSharpFactory/CommitFence" alt="MIT license"></a>
</p>

<p align="center">
  A code reviewer for C# that runs before a push and refuses it when the code breaks a rule your team wrote.<br>
  Free and open source. For Windows, macOS and Linux.<br>
  One of <a href="https://www.thesharpfactory.com">The Sharp Factory</a> products.
</p>

<p align="center">
  <a href="https://github.com/TheSharpFactory/CommitFence/releases/latest"><strong>Download</strong></a> ·
  <a href="https://github.com/TheSharpFactory/CommitFence/issues">Questions and bugs</a>
</p>

CommitFence reads what a push would send, builds the changed projects with your own compiler, and judges the code
against the rules in `commitfence.json` and the rules your `.editorconfig` already sets. A rule that blocks stops the
push, with the file, the line and the reason. Only what the push touches is judged, so an old breach in code you did
not change never stops you. It comes as a command line, for the `pre-push` hook, and an app, to write and try rules
and to read a review.

## Try it

1. **Download** the [latest release](https://github.com/TheSharpFactory/CommitFence/releases/latest) for your
   system: `CommitFence-win-x64.zip` (or `win-arm64`), `CommitFence-osx-arm64.zip` (or `osx-x64`) or
   `CommitFence-linux-x64.tar.gz` holds the app with the command line beside it; the `commitfence-cli-*` downloads hold
   the command line alone. Unzip it into a folder you can write to. It is self-contained, so you do not need .NET to
   run it.
2. **Install the command line** for your user: run `commitfence install`. It records where the program is and puts its
   folder on your `PATH`.
3. **Set a repository up:** in the repository, run `commitfence init`. It writes a `commitfence.json` with no rules and
   prints the line to put first in the repository's `pre-push` hook (with husky, the file `.husky/pre-push`):

   ```
   "$(git config --get commitfence.path)" review --hook pre-push "$@" || exit $?
   ```
4. **Add a rule** (below), then push. A push that breaks a rule that blocks is refused and says why.
5. **The app:** start `commitfenceapp.exe` from a terminal in the repository's folder, or give it the folder as its
   argument. It opens on the Rules screen.

You can review without the hook: `commitfence review --push` says what the hook would say for the current branch.

## A quick look

**Rules.** The team's rules and the rules of `.editorconfig`, each as a sentence you can edit. Try it, under the rule,
runs the rule on the code you type or paste and marks each place that breaks it, follows it or is not covered.

![The Rules screen: four team rules in a list, and the page of the rule "No braces around a single-line if" with its sentence and the code it was tried on](docs/images/rules-screen.png)

**Review.** What the push would send: the verdict, every finding under its file with the code around it, and the files
of the push.

![The Review screen after a refused push: two findings block it and one only warns, with the code of the first finding](docs/images/review-screen.png)

**Dashboard.** The reviews of pushes on this machine in the last 30 days: how many were refused, which rules found the
most, which never fired, and how long a push waited.

![The Dashboard: a chart with one box per review by day, the rules that never fired, and the rules most broken](docs/images/dashboard-screen.png)

**Architecture.** The modules of the repository in layers, each above the ones it may use, and the use that breaks a
direction in red. Every direction is also a table, and each change is saved at once.

![The Architecture screen: eleven modules in layers, the chosen module's lines, and one use that breaks a direction](docs/images/architecture-screen.png)

**Exemptions, Settings and Doctor.** Every line that sets a rule aside, with its reason; the strictness of the
repository, the version and its updates; and seven checks of whether a push from this machine will be reviewed, each
with how to fix it.

## The command line

| Command | What it does |
|---|---|
| `commitfence install [--print] [--no-path]` | Sets the program up for your user |
| `commitfence init` | Starts `commitfence.json` in a repository and prints the hook line |
| `commitfence doctor` | Checks whether pushes from this repository will be reviewed and explains how to fix failed checks |
| `commitfence settings show`, `set strictness <quiet\|standard\|strict>` | Shows or changes what the severity words in `.editorconfig` do to a push |
| `commitfence stats [--all]` | Shows local push reviews from the last 30 days, the rules they found and how long they took. Use `--all` to list every rule that never fired |
| `commitfence structure show`, `structure set` | Shows modules, allowed directions and the last structure judgment, or edits a module, part or direction |
| `commitfence exemptions list [--rule <id>]` | Lists the lines of the last commit that set a rule aside, by file, with the reason each one gives |
| `commitfence catalog [<language> [<artifact type>]]` | Lists what a rule can be about and the properties it can look at |
| `commitfence rules list`, `show <id>` | Lists the rules, or shows one, including the rules `.editorconfig` sets |
| `commitfence rules add`, `set`, `copy`, `remove` | Writes, changes, copies and removes a team rule |
| `commitfence rules enable`, `disable`, `severity <id> block\|warn` | Switches a rule, or chooses whether it blocks or only warns |
| `commitfence rules try <id> [<file>]` | Runs a rule on a file, or on standard input |
| `commitfence review --push` | Reviews what a push of the current branch would send |
| `commitfence review --staged` | Reviews what is staged |
| `commitfence review --base <revision> [--head <revision>]` | Reviews a range of commits |
| `commitfence review --files <path>...` | Reviews the named files |
| `commitfence review --all [<folder or project file>]` | Reviews the whole of a folder, as of the last commit |
| `commitfence review ... --format prompt` | Prints what was found as a prompt to hand to an AI coding agent, in place of the usual report |
| `commitfence update [--check]` | Installs the newest release; `--check` only says whether there is one |
| `commitfence --version`, `--help` | The version and the usage |

`commitfence --help` has every option. Exit codes of `review`: 0 the push may go, 1 a rule that blocks is broken,
2 the review could not be completed, 3 wrong settings or a wrong command.

## What a rule is

A rule says what it is about, where it applies, and what the code must be. The rule "No braces around a single-line if"
reads: *for an if statement in C#, where it has one statement in its body and the body fits on one line, require that
braces is false.* It blocks the push. Written with the command line:

```
commitfence rules add TEAM010 --for if-statement --title "No braces around a single-line if" --where statementsInBody=1 --where bodyFitsOnOneLine=true --require braces=false
```

In `commitfence.json` it is:

```json
{
    "rules": [
        {
            "id": "TEAM010",
            "language": "csharp",
            "for": "if-statement",
            "severity": "block",
            "title": "No braces around a single-line if",
            "where": { "statementsInBody": 1, "bodyFitsOnOneLine": true },
            "require": { "braces": false }
        }
    ]
}
```

`commitfence catalog csharp` lists what a rule can be about (an if statement, a method, a use of an API, and more) and
what it can look at in each. A rule can also forbid something altogether, apply only to some paths, and carry a message
and a reason. This repository's own `commitfence.json` holds the rules it is reviewed by.

## Good to know

- **It updates itself.** The app says when a new release is out, shows its notes and installs it on "Update now".
  The command line says so in one line, at most once a day and only at a terminal; `commitfence update` installs it.
  A hook run and a build server never check and never use the network. An update is installed only when the
  release's signed list of checksums matches the download.
- **C# only.** Other languages are not reviewed.
- A review needs the .NET SDK on the machine that pushes, because it builds the changed projects. When the build cannot
  run, the review says so and the push is not allowed; it never passes a review it could not finish.
- **Setting a rule aside.** `// commitfence:allow TEAM010: the reason`, on the line or alone on the line above,
  sets a finding of that rule aside. The reason is required: a comment without one is a finding of its own.
- **Strictness.** `"strictness"` in `commitfence.json` (`quiet`, `standard`, `strict`) says what the words of
  `.editorconfig` do to a push. Without it, `error` blocks and `warning` and `suggestion` warn.
- The product never writes `.editorconfig`.

## Building

```
dotnet build CF.slnx
dotnet run --project App/CF.App
dotnet run --project App/CF.Cli -- --version
dotnet test CF.slnx
```

Requires the .NET 11 SDK. The tests are xUnit; a few of them run real git and a real build, so they need `git` on the
`PATH`. CI builds and runs them on Windows, Linux and macOS on every push.

## Repository

| Folder | What |
|---|---|
| `App/CF.App` | The desktop app (Avalonia) |
| `App/CF.Cli` | The command line (`commitfence`) |
| `Dependencies/CF.Core` | The review, the rules and the ports to the outside world |
| `Dependencies/CF.Adapters` | The ports' real work: git, files, processes |
| `Dependencies/CF.Languages.CSharp` | C#: the catalog, the syntax and the build with Roslyn |
| `Dependencies/CF.Meaning` | The analyzer the team's compiler loads to learn what the code means |
| `Dependencies/CF.Composition` | Puts the parts together for both programs |
| `Tests/CF.Tests` | The tests of the review, the rules, the build and the updater (xUnit) |
| `Tests/CF.App.Tests` | The tests of the app, run without a display (xUnit, Avalonia headless) |
| `.github/` | CI and the release workflow |

## Releases

Each release on GitHub has its notes and the downloads for Windows (x64, ARM64), Linux (x64) and macOS (x64, Apple
silicon): the app and the command line, self-contained, with `LICENSE`, `THIRD-PARTY-NOTICES.md` and a `licenses`
folder, and a signed `SHA256SUMS.txt`. The programs are not signed with Windows' or Apple's own programmes yet, so
Windows warns the first time, and macOS blocks the app the first time: open it once, then go to System Settings >
Privacy & Security and choose **Open Anyway**. On macOS 14 or earlier, right-click the app and choose **Open** instead.

To publish one: add the version's section to `CHANGELOG.md` (the heading `## <VERSION> — <date>`), commit, and push
the tag `v<VERSION>`. `.github/workflows/release.yml` checks that the tag matches `VERSION`, builds everything, signs
the checksums and publishes the release with that section as its text.

## License

CommitFence is one of [The Sharp Factory](https://www.thesharpfactory.com) products, written by Jonathan Alfaro (JAM)
and released under the [MIT License](LICENSE). It includes third-party
code under its own licenses: see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
