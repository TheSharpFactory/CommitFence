#!/bin/bash
# Acceptance: the acceptance test, through the real hook and the published program.
# With the private copy already warm on a solution that targets two frameworks, a push renames a source file that
# a project picks up by wildcard and removes a public member that an untouched project uses on only one of the
# frameworks. The real hook must refuse the push and name the broken project and framework, and its findings must
# equal those of a clean rebuild of the same commits. The time of the run is recorded.
#
# Verdicts:
#   claim 1: the hook exits 1 and the remote does not move
#   claim 2: the output names the project Consumer and the framework net9.0 in a CS0117 error at Consumer/Use.cs
#            (the line of Library.Greeter.Old()), and does not name net11.0 for it
#   claim 3: the findings of the hook run equal the findings of a review of the same two commits made with an
#            empty data folder (nothing kept, everything rebuilt)
#   claim 4: the warm run is the second review of the repository, so the private copy and the kept answers exist

# Usage: Tools/checks/acceptance/run.sh <path of the commitfence program> [parent folder for the scratch folder] [case ids...]
# Safe: everything happens in a fresh folder that this script makes with mktemp -d under the parent folder (default: the
# system's temp folder), removed when every check passed. GIT_CONFIG_GLOBAL, COMMITFENCE_DATA_FOLDER and
# COMMITFENCE_USER_PATH_FILE point into it, remotes are local bare repositories, and the user's git settings, PATH and
# CommitFence data are never touched. Needs bash, git and the .NET SDK, and no network. Nothing outside this repository
# and the scratch folder is read.
set -u

if [ $# -lt 1 ] || [ ! -f "$1" ]; then
    echo "usage: run.sh <path of the commitfence program> [parent folder for the scratch folder] [case ids...]" >&2
    exit 64
fi

native() { if command -v cygpath > /dev/null 2>&1; then cygpath -m "$1"; else echo "$1"; fi; }

stop_now() { echo "run.sh: $1" >&2; exit 70; }

HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPOSITORY=$(cd "$HERE/../../.." && pwd)
EXE_WIN=$(native "$(cd "$(dirname "$1")" && pwd)/$(basename "$1")")
EXE=$EXE_WIN
PARENT=${2:-${TMPDIR:-/tmp}}
shift
if [ $# -gt 0 ]; then shift; fi
SELECTED="$*"
mkdir -p "$PARENT" || stop_now "cannot create the parent folder $PARENT"
ROOT=$(mktemp -d "$PARENT/commitfence-acceptance.XXXXXX") || stop_now "cannot create a scratch folder under $PARENT"
ROOT=$(cd "$ROOT" && pwd) || stop_now "cannot enter the scratch folder"
if [ -z "$ROOT" ] || [ "$ROOT" = "/" ] || [ ! -d "$ROOT" ]; then
    stop_now "the scratch folder path is not usable: '$ROOT'"
fi

STARTED=$SECONDS
WORK=$ROOT/work
mkdir -p "$WORK/data" "$WORK/data-clean" "$WORK/out"
WORK_WIN=$(native "$WORK")

export GIT_CONFIG_GLOBAL="$WORK_WIN/gitconfig"
export GIT_CONFIG_NOSYSTEM=1
export COMMITFENCE_DATA_FOLDER="$WORK_WIN/data"
export COMMITFENCE_USER_PATH_FILE="$WORK_WIN/userpath.txt"
export GIT_TERMINAL_PROMPT=0
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
cat > "$WORK/gitconfig" <<EOF
[user]
	name = Check
	email = c@example.invalid
[init]
	defaultBranch = main
[core]
	autocrlf = false
[commitfence]
	path = $EXE
EOF


finish_script() { # prints the total, removes the scratch folder when every check passed, and exits
    echo
    echo "TOTAL: $PASSES passed, $FAILS failed, $SKIPS skipped, $((SECONDS - STARTED)) seconds"
    if [ "$FAILS" -eq 0 ] && [ $((PASSES + FAILS)) -gt 0 ]; then
        rm -rf "$ROOT"
        exit 0
    fi

    echo "the scratch folder is kept: $ROOT"
    exit 1
}

PASSES=0
FAILS=0
SKIPS=0

skip_case() { # case reason: a case that cannot be built on this machine; it is printed and counted
    SKIPS=$((SKIPS + 1))
    echo "SKIP  $1: $2"
}
claim() { # text command...
    local text=$1
    shift
    if "$@"; then PASSES=$((PASSES + 1)); echo "PASS  $text"; else FAILS=$((FAILS + 1)); echo "FAIL  $text"; fi
}
has() { grep -Fq -- "$2" "$1"; }
hasnt() { ! grep -Fq -- "$2" "$1"; }
now_ms() { date +%s%N | cut -b1-13; }

csproj() { # file frameworks [reference]
    {
        echo '<Project Sdk="Microsoft.NET.Sdk">'
        echo '  <PropertyGroup>'
        echo "    <TargetFrameworks>$2</TargetFrameworks>"
        echo '    <Nullable>enable</Nullable>'
        echo '    <ImplicitUsings>enable</ImplicitUsings>'
        echo '  </PropertyGroup>'
        if [ -n "${3:-}" ]; then
            printf '  <ItemGroup>\n    <ProjectReference Include="..\\%s\\%s.csproj" />\n  </ItemGroup>\n' "$3" "$3"
        fi
        echo '</Project>'
    } > "$1"
}

REMOTE=$WORK/remote.git
REPO=$WORK/repo
git init -q --bare "$REMOTE"
git init -q "$REPO"
cd "$REPO" || exit 1
git remote add origin "$(cygpath -m "$REMOTE")"
printf '#!/bin/sh\n"$(git config --get commitfence.path)" review --hook pre-push "$@" || exit $?\n' > .git/hooks/pre-push.team
printf '#!/bin/sh\n"$(dirname "$0")/pre-push.team" "$@"\nrc=$?\necho $rc > "$(dirname "$0")/hook-exit.txt"\nexit $rc\n' > .git/hooks/pre-push
chmod +x .git/hooks/pre-push .git/hooks/pre-push.team

mkdir -p Library Consumer
csproj Library/Library.csproj "net9.0;net11.0"
csproj Consumer/Consumer.csproj "net9.0;net11.0" Library
cat > Library/Greeter.cs <<'EOF'
namespace Library;

public static class Greeter
{
    public static string Hello(string name)
    {
        return "Hello " + name;
    }

    public static int Old()
    {
        return 1;
    }
}
EOF
cat > Consumer/Use.cs <<'EOF'
namespace Consumer;

public static class Use
{
    public static string Run()
    {
        return Library.Greeter.Hello("x");
    }

    public static int Legacy()
    {
#if NET9_0
        return Library.Greeter.Old();
#else
        return 0;
#endif
    }
}
EOF
cat > commitfence.json <<'EOF'
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
EOF
printf 'bin/\nobj/\n' > .gitignore
git add -A && git commit -q -m "initial" && git push -q --no-verify origin main 2> /dev/null
C0=$(git rev-parse HEAD)

# the warm-up: a clean push through the real hook, so that the private copy and the kept answers exist
cat > Library/Extra.cs <<'EOF'
namespace Library;

public static class Extra
{
    public static int Two()
    {
        return 2;
    }
}
EOF
git add -A && git commit -q -m "a clean change that warms the private copy"
rm -f .git/hooks/hook-exit.txt
W0=$(now_ms)
git push origin main > "$WORK/out/warm.txt" 2>&1
W1=$(now_ms)
WARM_EXIT=$(cat .git/hooks/hook-exit.txt 2> /dev/null || echo none)
echo "warm-up push: hook exit $WARM_EXIT, $((W1 - W0)) ms"
C1=$(git rev-parse HEAD)

# the push under test
git mv Library/Greeter.cs Library/Salutation.cs
sed -i '/public static int Old()/,/^    }/d' Library/Salutation.cs
git add -A && git commit -q -m "Greeter.cs is renamed and Old() is removed"
C2=$(git rev-parse HEAD)
LINE=$(grep -n 'Greeter.Old()' Consumer/Use.cs | head -1 | cut -d: -f1)
cat Library/Salutation.cs

rm -f .git/hooks/hook-exit.txt
T0=$(now_ms)
git push origin main > "$WORK/out/hook.txt" 2>&1
T1=$(now_ms)
HOOK_EXIT=$(cat .git/hooks/hook-exit.txt 2> /dev/null || echo none)
echo "the push under test: hook exit $HOOK_EXIT, $((T1 - T0)) ms"
cat "$WORK/out/hook.txt"
echo "$((T1 - T0))" > "$WORK/out/time-warm-ms.txt"

# a clean rebuild of the same commits: a data folder that holds nothing
C0M=$C1
COMMITFENCE_DATA_FOLDER="$WORK_WIN/data-clean" "$EXE" review --base "$C0M" --head "$C2" > "$WORK/out/clean.txt" 2>&1
CLEAN_EXIT=$?
echo "clean rebuild of the same commits: exit $CLEAN_EXIT"
cat "$WORK/out/clean.txt"

findings() { grep -E ': (error|warning) ' "$1" | LC_ALL=C sort; }
findings "$WORK/out/hook.txt" > "$WORK/out/hook.findings"
findings "$WORK/out/clean.txt" > "$WORK/out/clean.findings"

echo
claim "1 the hook exits 1" test "$HOOK_EXIT" = 1
claim "1 the remote did not move (still at the warm-up commit)" test "$(git --git-dir="$REMOTE" rev-parse main)" = "$C1"
claim "2 a CS0117 error at Consumer/Use.cs($LINE," has "$WORK/out/hook.txt" "Consumer/Use.cs($LINE,"
claim "2 it names the project and framework: [Consumer, net9.0]" has "$WORK/out/hook.txt" "[Consumer, net9.0]"
claim "2 net11.0 is not named for Consumer" hasnt "$WORK/out/hook.txt" "[Consumer, net11.0"
claim "2 it is error CS0117" has "$WORK/out/hook.txt" "error CS0117"
claim "3 the findings equal those of the clean rebuild" cmp -s "$WORK/out/hook.findings" "$WORK/out/clean.findings"
claim "3 the findings are not empty" test -s "$WORK/out/hook.findings"
claim "4 the warm-up push passed: the hook exits 0" test "$WARM_EXIT" = 0
claim "4 the warm-up review was done through the hook" test "$(grep -c 'Library (net' "$WORK/out/warm.txt")" -ge 1
finish_script
