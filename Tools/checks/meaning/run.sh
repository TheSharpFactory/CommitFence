#!/bin/bash
# Meaning and structure rules, and CommitFence on itself. Builds every
# test repository from nothing and prints PASS or FAIL per check. Expected results are in expected.md.
# Nothing is ever committed in this repository: case 10 works on a clone.
# Usage: Tools/checks/meaning/run.sh <path of the commitfence program> [parent folder for the scratch folder] [case ids...]
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

if ! command -v cygpath > /dev/null 2>&1; then # outside Git Bash on Windows the paths are already plain
    cygpath() { if [ "${1:-}" = -m ] || [ "${1:-}" = -w ]; then echo "$2"; else echo "$1"; fi; }
fi

HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPOSITORY=$(cd "$HERE/../../.." && pwd)
EXE_WIN=$(native "$(cd "$(dirname "$1")" && pwd)/$(basename "$1")")
EXE=$EXE_WIN
PARENT=${2:-${TMPDIR:-/tmp}}
shift
if [ $# -gt 0 ]; then shift; fi
SELECTED="$*"
mkdir -p "$PARENT" || stop_now "cannot create the parent folder $PARENT"
ROOT=$(mktemp -d "$PARENT/commitfence-meaning.XXXXXX") || stop_now "cannot create a scratch folder under $PARENT"
ROOT=$(cd "$ROOT" && pwd) || stop_now "cannot enter the scratch folder"
if [ -z "$ROOT" ] || [ "$ROOT" = "/" ] || [ ! -d "$ROOT" ]; then
    stop_now "the scratch folder path is not usable: '$ROOT'"
fi

STARTED=$SECONDS
WORK=$ROOT/work
mkdir -p "$WORK/repos" "$WORK/remotes" "$WORK/data" "$WORK/out"
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
	name = Check Runner
	email = check@example.invalid
[init]
	defaultBranch = main
[core]
	autocrlf = false
	longpaths = true
[commitfence]
	path = $EXE_WIN
[advice]
	detachedHead = false
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

report() { # status case description
    if [ "$1" = "PASS" ]; then PASSES=$((PASSES + 1)); else FAILS=$((FAILS + 1)); fi
    echo "$1  $2: $3"
}

check() { # case description command...
    local case_id=$1 text=$2
    shift 2
    if "$@"; then report PASS "$case_id" "$text"; else report FAIL "$case_id" "$text"; fi
}

has() { grep -Fq -- "$2" "$1"; }
hasnt() { ! grep -Fq -- "$2" "$1"; }
equals() { [ "$1" = "$2" ]; }

# ---------- the test repository ----------
write_csproj() { # file target-framework [project reference]
    local reference=${3:-}
    {
        echo '<Project Sdk="Microsoft.NET.Sdk">'
        echo '  <PropertyGroup>'
        echo "    <TargetFramework>$2</TargetFramework>"
        echo '    <Nullable>enable</Nullable>'
        echo '    <ImplicitUsings>enable</ImplicitUsings>'
        echo '  </PropertyGroup>'
        if [ -n "$reference" ]; then
            echo '  <ItemGroup>'
            echo "    <ProjectReference Include=\"..\\$reference\\$reference.csproj\" />"
            echo '  </ItemGroup>'
        fi
        echo '</Project>'
    } > "$1"
}

write_settings() {
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
        },
        {
            "id": "TEAM011",
            "language": "csharp",
            "for": "method",
            "severity": "block",
            "title": "Only public and private methods inside an internal class",
            "where": { "containingType": "class", "containingTypeDeclaredAccess": "internal", "isOverride": false },
            "require": { "declaredAccess": ["public", "private"] }
        },
        {
            "id": "TEAM012",
            "language": "csharp",
            "for": "usage",
            "severity": "block",
            "title": "Use IClock, not DateTime.Now",
            "where": { "target": "System.DateTime.Now" },
            "forbid": true
        }
    ],
    "structure": {
        "modules": {
            "domain": [ "folder:src/Shop/Domain/**" ],
            "infrastructure": [ "folder:src/Shop/Infrastructure/**" ],
            "reports": [ "project:Shop.Reports" ]
        },
        "directions": {
            "allow": [ "infrastructure -> domain", "reports -> domain" ]
        }
    }
}
EOF
}

write_shop() { # uses TFM (default net11.0) and MOVED (price calculator already in Infrastructure)
    local tfm=${TFM:-net11.0} calculator=Domain
    [ -n "${MOVED:-}" ] && calculator=Infrastructure
    mkdir -p src/Shop/Domain src/Shop/Infrastructure src/Shop.Reports
    write_csproj src/Shop/Shop.csproj "$tfm"
    write_csproj src/Shop.Reports/Shop.Reports.csproj "$tfm" Shop
    cat > src/Shop/$calculator/PriceCalculator.cs <<'EOF'
namespace Shop.Domain;

public class PriceCalculator
{
    public decimal Total(decimal amount)
    {
        return amount * 2;
    }
}
EOF
    cat > src/Shop/Domain/PriceFactory.cs <<'EOF'
namespace Shop.Domain;

public static class PriceFactory
{
    public static PriceCalculator Create()
    {
        return new PriceCalculator();
    }
}
EOF
    cat > src/Shop/Domain/OrderService.cs <<'EOF'
namespace Shop.Domain;

public class OrderService
{
    private readonly PriceCalculator _calculator = new PriceCalculator();

    public decimal Place(decimal amount)
    {
        return _calculator.Total(amount);
    }

    public decimal PlaceInferred(decimal amount)
    {
        var calculator = PriceFactory.Create();
        return calculator.Total(amount);
    }
}
EOF
    cat > src/Shop/Infrastructure/SqlHelper.cs <<'EOF'
namespace Shop.Infrastructure;

public static class SqlHelper
{
    public static string Quote(string value)
    {
        return "'" + value + "'";
    }
}
EOF
    cat > src/Shop.Reports/MonthlyReport.cs <<'EOF'
using Shop.Domain;

namespace Shop.Reports;

public class MonthlyReport
{
    public decimal Build(decimal amount)
    {
        var calculator = new PriceCalculator();
        return calculator.Total(amount);
    }
}
EOF
    cat > src/Shop.Reports/LegacyReport.cs <<'EOF'
namespace Shop.Reports;

public class LegacyReport
{
    public string Run(string name)
    {
        return Shop.Infrastructure.SqlHelper.Quote(name);
    }
}
EOF
    write_settings
    echo "readme" > README.md
}

write_clock() { # Clock.cs with the three forms of DateTime.Now
    cat > src/Shop/Domain/Clock.cs <<'EOF'
using static System.DateTime;

namespace Shop.Domain;

public class Clock
{
    public System.DateTime Direct()
    {
        return System.DateTime.Now;
    }

    public System.DateTime Plain()
    {
        return DateTime.Now;
    }

    public System.DateTime Static()
    {
        return Now;
    }
}
EOF
}

line_of() { grep -n -- "$2" "$1" | head -1 | cut -d: -f1; }

finding_lines() { # output-file source-file rule: the distinct lines of the findings of the rule in the file, in order
    grep -F -- "$2(" "$1" | grep -F -- " $3:" | sed -E 's/^[^(]*\(([0-9]+),.*/\1/' | sort -un | tr '\n' ' ' | sed 's/ $//'
}

count_rule() { grep -c -F -- " $2:" "$1"; }

# ---------- repositories ----------
new_repo() { # name [setup function that runs before the first commit]: a repository with a bare remote and the team's hook
    NAME=$1
    REPO=$WORK/repos/$NAME
    REMOTE=$WORK/remotes/$NAME.git
    git init -q --bare "$REMOTE"
    git init -q "$REPO"
    cd "$REPO" || exit 1
    git remote add origin "$(cygpath -m "$REMOTE")"
    printf '#!/bin/sh\n"$(git config --get commitfence.path)" review --hook pre-push "$@" || exit $?\n' > .git/hooks/pre-push.team
    printf '#!/bin/sh\n"$(dirname "$0")/pre-push.team" "$@"\nrc=$?\necho $rc > "$(dirname "$0")/hook-exit.txt"\nexit $rc\n' > .git/hooks/pre-push
    chmod +x .git/hooks/pre-push .git/hooks/pre-push.team
    write_shop
    echo "repository $NAME" > README.md
    [ -n "${2:-}" ] && "$2"
    git add -A && git commit -q -m "initial"
    [ -n "${NO_FIRST_PUSH:-}" ] || git push -q --no-verify origin main 2> /dev/null
}

commit_all() { git add -A && git commit -q -m "$1"; }

dev_prep() { # an unstaged edit, an untracked file and a stash entry
    echo "stashed" >> README.md
    git stash -q
    echo "unstaged edit" >> README.md
    echo "untracked" > notes.txt
}

state_of() {
    sha256sum .git/index .git/config
    (cd .git/hooks && find . -type f ! -name hook-exit.txt -print0 | LC_ALL=C sort -z | xargs -0 --no-run-if-empty sha256sum)
    git rev-parse HEAD
    git symbolic-ref -q HEAD
    git for-each-ref --format='%(refname) %(objectname)' | grep -v '^refs/remotes/'
    git stash list
    git rev-parse -q --verify refs/stash
    find . -path ./.git -prune -o -type f -print0 | LC_ALL=C sort -z | xargs -0 sha256sum
    git status --porcelain
}

snap_before() { export GIT_OPTIONAL_LOCKS=0; state_of > "$WORK/out/$NAME.before" 2>&1; }
snap_after() { export GIT_OPTIONAL_LOCKS=0; state_of > "$WORK/out/$NAME.after" 2>&1; }

remote_state() { git --git-dir="$REMOTE" for-each-ref --format='%(refname) %(objectname)' 2>&1; }

now_ms() { date +%s%N | cut -b1-13; }

do_push() {
    OUT=$WORK/out/$NAME.push
    rm -f .git/hooks/hook-exit.txt
    local before after started ended
    before=$(remote_state)
    started=$(now_ms)
    git push "$@" > "$OUT" 2>&1
    ended=$(now_ms)
    ELAPSED_MS=$((ended - started))
    HOOK_EXIT=$(cat .git/hooks/hook-exit.txt 2> /dev/null || echo none)
    after=$(remote_state)
    if [ "$before" = "$after" ]; then MOVED_REMOTE=no; else MOVED_REMOTE=yes; fi
}

private_copy_clean() { # nothing but the commit's files and build outputs in the private copy's tree
    local tree
    tree=$(ls -d "$WORK"/data/"$NAME"-*/tree 2> /dev/null | head -1)
    [ -z "$tree" ] && return 0
    local extras
    extras=$(cd "$tree" && find . -type f -not -path '*/bin/*' -not -path '*/obj/*' | sed 's|^\./||' | LC_ALL=C sort > "$WORK/out/$NAME.tree-files" ; \
        git -C "$REPO" ls-tree -r --name-only HEAD | LC_ALL=C sort > "$WORK/out/$NAME.head-files" ; \
        comm -23 "$WORK/out/$NAME.tree-files" "$WORK/out/$NAME.head-files")
    [ -z "$extras" ] || { echo "extra files in the private copy: $extras"; return 1; }
}

finish() { # case-id: check 9
    snap_after
    check "9.$1" "working folder, index, config, hooks, every ref, stash and status unchanged" cmp -s "$WORK/out/$NAME.before" "$WORK/out/$NAME.after"
    check "9.$1" "the private copy holds no file of ours inside the tree besides build outputs" private_copy_clean
}

first_line_ok() { head -1 "$1" | grep -Eq '^CommitFence [^ ]+ \(.+\)$'; }

expect_push() { # case-id exit-code moved
    check "$1" "the hook exited with $2" equals "$HOOK_EXIT" "$2"
    check "$1" "the remote moved: $3" equals "$MOVED_REMOTE" "$3"
    check "$1" "first printed line is the version and folder" first_line_ok "$OUT"
}

wanted() { [ -z "$SELECTED" ] || [[ " $SELECTED " == *" $1 "* ]]; }

move_calculator() { git mv src/Shop/Domain/PriceCalculator.cs src/Shop/Infrastructure/PriceCalculator.cs; }

expect_acceptance() { # case-id: the findings of the acceptance test
    local id=$1 os pf mr
    os="$(line_of src/Shop/Domain/OrderService.cs 'private readonly PriceCalculator') $(line_of src/Shop/Domain/OrderService.cs '_calculator.Total') $(line_of src/Shop/Domain/OrderService.cs 'var calculator = PriceFactory')"
    pf=$(line_of src/Shop/Domain/PriceFactory.cs 'public static PriceCalculator')
    mr=$(line_of src/Shop.Reports/MonthlyReport.cs 'var calculator')
    check "$id" "OrderService.cs: the callers nobody touched, at their own lines ($os), the var included" equals "$(finding_lines "$OUT" src/Shop/Domain/OrderService.cs ARCH001)" "$os"
    check "$id" "PriceFactory.cs: reported at its own line ($pf)" equals "$(finding_lines "$OUT" src/Shop/Domain/PriceFactory.cs ARCH001)" "$pf"
    check "$id" "MonthlyReport.cs in another project: reported at its own line ($mr)" equals "$(finding_lines "$OUT" src/Shop.Reports/MonthlyReport.cs ARCH001)" "$mr"
    check "$id" "the violation that was already there (LegacyReport.cs) is not reported" hasnt "$OUT" "LegacyReport.cs("
    check "$id" "the finding says who must not use whom" has "$OUT" "Layer direction: domain must not use infrastructure."
    check "$id" "no rule was left not run" hasnt "$OUT" "not run:"
    check "$id" "Shop reviewed" has "$OUT" "Shop (net"
    check "$id" "Shop.Reports reviewed" has "$OUT" "Shop.Reports (net"
}

# ---------- the cases ----------
case_1() {
    new_repo c1
    move_calculator
    commit_all "PriceCalculator moves to Infrastructure, namespace kept"
    dev_prep; snap_before
    do_push origin main
    echo "case 1: first review, both trees built: ${ELAPSED_MS} ms" | tee "$WORK/out/time-1.txt"
    expect_push 1 1 no
    expect_acceptance 1
    check 1 "exactly 5 distinct places (3 + 1 + 1) of ARCH001" equals "$(grep -F ' ARCH001:' "$OUT" | sed -E 's/^([^(]*\([0-9]+),.*/\1/' | sort -u | wc -l | tr -d ' ')" 5
    check 1 "Shop (net11.0) reviewed" has "$OUT" "Shop (net11.0): reviewed"
    check 1 "Shop.Reports (net11.0) reviewed" has "$OUT" "Shop.Reports (net11.0): reviewed"
    finish 1
}

case_1b() {
    new_repo c1b
    move_calculator
    cat > Directory.Build.targets <<'EOF'
<Project>
  <Target Name="DropCommitFenceAnalyzer" BeforeTargets="CoreCompile" Condition="'$(MSBuildProjectName)' == 'Shop.Reports'">
    <ItemGroup>
      <Analyzer Remove="@(Analyzer)" Condition="'%(Filename)' == 'CF.Meaning'" />
    </ItemGroup>
  </Target>
</Project>
EOF
    commit_all "the move, and a repository that removes our analyzer from Shop.Reports"
    dev_prep; snap_before
    do_push origin main
    expect_push 1b 2 no
    check 1b "ARCH001 is named as not run" has "$OUT" "not run: ARCH001 was not run"
    check 1b "Shop.Reports is not reviewed" has "$OUT" "Shop.Reports (net11.0): not reviewed"
    check 1b "the reason names the missing closing entry" has "$OUT" "has no closing entry of the meaning analyzer"
    check 1b "no ARCH001 finding is printed" equals "$(count_rule "$OUT" ARCH001)" 0
    finish 1b
}

case_1c() {
    new_repo c1c
    move_calculator
    printf '\npublic class Broken\n{\n    public int Value()\n    {\n        return Missing.Nothing();\n    }\n}\n' >> src/Shop/Domain/PriceFactory.cs
    commit_all "the move, and a compile error in Shop"
    dev_prep; snap_before
    do_push origin main
    expect_push 1c 1 no
    check 1c "the compile error is reported" has "$OUT" "error CS"
    check 1c "Shop.Reports is not reviewed because its prerequisite failed" has "$OUT" "Shop.Reports (net11.0): not reviewed: its prerequisite"
    check 1c "ARCH001 is named as not run" has "$OUT" "not run: ARCH001 was not run"
    check 1c "no ARCH001 finding is printed" equals "$(count_rule "$OUT" ARCH001)" 0
    finish 1c
}

case_1t() {
    new_repo c1t
    printf '\npublic class Extra1\n{\n    public int One()\n    {\n        return 1;\n    }\n}\n' >> src/Shop/Domain/PriceFactory.cs
    commit_all "a new class in Shop"
    dev_prep; snap_before
    do_push origin main
    local first=$ELAPSED_MS
    expect_push 1t.1 0 yes
    finish 1t.1
    printf '\npublic class Extra2\n{\n    public int Two()\n    {\n        return 2;\n    }\n}\n' >> src/Shop/Domain/PriceFactory.cs
    commit_all "another new class in Shop"
    dev_prep; snap_before
    do_push origin main
    local second=$ELAPSED_MS
    expect_push 1t.2 0 yes
    echo "case 1t: first review (both trees built): ${first} ms; second review of a later push (tree before kept): ${second} ms" | tee "$WORK/out/time-1t.txt"
    check 1t "answers are kept in the data folder" test -n "$(ls "$WORK"/data/csharp/answers/*.json 2> /dev/null | head -1)"
    check 1t "the second review is faster than the first" [ "$second" -lt "$first" ]
    finish 1t
}

case_2() {
    MOVED=1 new_repo c2
    sed -i 's|^}$||' src/Shop/Domain/OrderService.cs
    printf '    public decimal Extra(decimal a)\n    {\n        return new PriceCalculator().Total(a);\n    }\n}\n' >> src/Shop/Domain/OrderService.cs
    local line
    line=$(line_of src/Shop/Domain/OrderService.cs 'return new PriceCalculator')
    commit_all "OrderService gets a second method that uses PriceCalculator"
    dev_prep; snap_before
    do_push origin main
    expect_push 2 1 no
    check 2 "exactly one finding of ARCH001" equals "$(count_rule "$OUT" ARCH001)" 1
    check 2 "it is in OrderService.cs at the new line ($line)" equals "$(finding_lines "$OUT" src/Shop/Domain/OrderService.cs ARCH001)" "$line"
    check 2 "no old violation of PriceFactory.cs is printed" hasnt "$OUT" "PriceFactory.cs("
    check 2 "no old violation of MonthlyReport.cs is printed" hasnt "$OUT" "MonthlyReport.cs("
    finish 2
}

case_3() {
    MOVED=1 new_repo c3
    { printf '// a note\n// another note\n// a third note\n'; cat src/Shop/Domain/OrderService.cs; } > "$WORK/out/os.tmp"
    cp "$WORK/out/os.tmp" src/Shop/Domain/OrderService.cs
    commit_all "three comment lines in OrderService, moving its lines"
    dev_prep; snap_before
    do_push origin main
    expect_push 3 0 yes
    check 3 "nothing is reported by ARCH001" equals "$(count_rule "$OUT" ARCH001)" 0
    check 3 "Shop reviewed" has "$OUT" "Shop (net11.0): reviewed"
    check 3 "Shop.Reports reviewed" has "$OUT" "Shop.Reports (net11.0): reviewed"
    finish 3
}

case_4() {
    new_repo c4
    write_clock
    cat > src/Shop/Domain/Own.cs <<'EOF'
namespace Shop.Domain.Own;

public class DateTime
{
    public static int Now
    {
        get { return 1; }
    }
}

public class UsesOwn
{
    public int Get()
    {
        return DateTime.Now;
    }
}
EOF
    local lines
    lines="$(line_of src/Shop/Domain/Clock.cs 'System.DateTime.Now') $(line_of src/Shop/Domain/Clock.cs 'return DateTime.Now') $(line_of src/Shop/Domain/Clock.cs 'return Now')"
    commit_all "the clock is read in three ways, and a type of the team's own is called DateTime"
    dev_prep; snap_before
    do_push origin main
    expect_push 4 1 no
    check 4 "TEAM012 in Clock.cs at ($lines): System.DateTime.Now, DateTime.Now and the using static form" equals "$(finding_lines "$OUT" src/Shop/Domain/Clock.cs TEAM012)" "$lines"
    check 4 "the team's own DateTime in Own.cs is not reported by TEAM012" equals "$(finding_lines "$OUT" src/Shop/Domain/Own.cs TEAM012)" ""
    check 4 "the message is the rule's title" has "$OUT" "Use IClock, not DateTime.Now"
    finish 4
}

case_4b() {
    new_repo c4b
    cat > src/Shop/Domain/Year.cs <<'EOS'
namespace Shop.Domain;

public class YearReader
{
    public int Current()
    {
        return System.DateTime.Now.Year;
    }
}
EOS
    local line
    line=$(line_of src/Shop/Domain/Year.cs 'System.DateTime.Now.Year')
    commit_all "one use of System.DateTime.Now, followed by .Year"
    dev_prep; snap_before
    do_push origin main
    expect_push 4b 1 no
    check 4b "exactly one TEAM012 finding for the one use" equals "$(count_rule "$OUT" TEAM012)" 1
    check 4b "it is on the line of the use ($line)" equals "$(finding_lines "$OUT" src/Shop/Domain/Year.cs TEAM012)" "$line"
    check 4b "it points at the member Now (the column after System.DateTime.), not at the type" has "$OUT" "src/Shop/Domain/Year.cs($line,32)"
    finish 4b
}

case_5() {
    new_repo c5
    printf 'namespace Shop.Domain;\n\npublic partial class Wide\n{\n}\n' > src/Shop/Domain/PartA.cs
    printf 'namespace Shop.Domain;\n\npartial class Wide\n{\n    protected void Extend()\n    {\n    }\n}\n' > src/Shop/Domain/PartB.cs
    printf 'namespace Shop.Domain;\n\ninternal partial class Hidden\n{\n}\n' > src/Shop/Domain/HiddenA.cs
    printf 'namespace Shop.Domain;\n\npartial class Hidden\n{\n    internal void Quiet()\n    {\n    }\n}\n' > src/Shop/Domain/HiddenB.cs
    commit_all "partial classes: one whose other part says public, one that is internal"
    dev_prep; snap_before
    do_push origin main
    expect_push 5 1 no
    check 5 "exactly one TEAM011 finding" equals "$(count_rule "$OUT" TEAM011)" 1
    check 5 "it is the internal class's method, HiddenB.cs line 5" equals "$(finding_lines "$OUT" src/Shop/Domain/HiddenB.cs TEAM011)" 5
    check 5 "no TEAM011 finding for the partial class whose other part says public (PartB.cs)" equals "$(finding_lines "$OUT" src/Shop/Domain/PartB.cs TEAM011)" ""
    finish 5
}

case_5o() {
    new_repo c5o
    printf 'namespace Shop.Domain;

public class KeysBase
{
    protected virtual void OnKeyDown(int key)
    {
    }

    internal virtual void Measure()
    {
    }
}
' > src/Shop/Domain/KeysBase.cs
    printf 'namespace Shop.Domain;

internal class Keys : KeysBase
{
    protected override void OnKeyDown(int key)
    {
    }

    internal override void Measure()
    {
    }

    protected void Helper()
    {
    }

    public void Open()
    {
    }

    private void Close()
    {
    }
}
' > src/Shop/Domain/Keys.cs
    commit_all "an internal class with overrides, a protected helper, a public and a private method"
    dev_prep; snap_before
    do_push origin main
    expect_push 5o 1 no
    check 5o "exactly one TEAM011 finding" equals "$(count_rule "$OUT" TEAM011)" 1
    check 5o "it is the protected helper, Keys.cs line 13, and neither override nor the public and private methods" equals "$(finding_lines "$OUT" src/Shop/Domain/Keys.cs TEAM011)" 13
    finish 5o
}

setup_switch_off() {
    cat > Directory.Build.props <<'EOF'
<Project>
  <PropertyGroup>
    <RunAnalyzers>false</RunAnalyzers>
    <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
    <NoWarn>$(NoWarn);CFM001;CFM999</NoWarn>
    <WarningsAsErrors>$(WarningsAsErrors);CFM001;CFM999</WarningsAsErrors>
  </PropertyGroup>
</Project>
EOF
    printf 'root = true\n\n[*.cs]\ndotnet_diagnostic.CFM001.severity = none\ndotnet_diagnostic.CFM999.severity = error\ndotnet_analyzer_diagnostic.severity = none\n' > .editorconfig
}

setup_warnings_as_errors() {
    cat > Directory.Build.props <<'EOF'
<Project>
  <PropertyGroup>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors>$(WarningsAsErrors);CFM001;CFM999</WarningsAsErrors>
  </PropertyGroup>
</Project>
EOF
    printf 'root = true\n\n[*.cs]\ndotnet_diagnostic.CFM001.severity = error\ndotnet_diagnostic.CFM999.severity = error\n' > .editorconfig
}

clock_case() { # id repository-setup description
    new_repo "c$1" "$2"
    printf 'namespace Shop.Domain;\n\npublic class Clock\n{\n    public System.DateTime Direct()\n    {\n        return System.DateTime.Now;\n    }\n}\n' > src/Shop/Domain/Clock.cs
    local line
    line=$(line_of src/Shop/Domain/Clock.cs 'System.DateTime.Now')
    commit_all "the clock is read"
    dev_prep; snap_before
    do_push origin main
    expect_push "$1" 1 no
    check "$1" "$3: TEAM012 still reported at the line of Now ($line)" equals "$(finding_lines "$OUT" src/Shop/Domain/Clock.cs TEAM012)" "$line"
    check "$1" "the build did not fail" hasnt "$OUT" "the build fails"
    check "$1" "Shop reviewed" has "$OUT" "Shop (net11.0): reviewed"
    check "$1" "Shop.Reports reviewed" has "$OUT" "Shop.Reports (net11.0): reviewed"
    check "$1" "no rule was left not run" hasnt "$OUT" "not run:"
    finish "$1"
}

case_6a() { clock_case 6a setup_switch_off "the repository switches analyzers and our entries off"; }

case_6b() { clock_case 6b setup_warnings_as_errors "every warning is an error and our entries are raised"; }

setup_everything_an_error() {
    printf 'root = true

[*.cs]
dotnet_analyzer_diagnostic.severity = error
' > .editorconfig
}

case_6c() {
    new_repo c6c setup_everything_an_error
    printf 'namespace Shop.Domain;

public class Clock
{
    public System.DateTime Direct()
    {
        return System.DateTime.Now;
    }
}
' > src/Shop/Domain/Clock.cs
    commit_all "the clock is read, in a repository whose analyzers are all errors"
    dev_prep; snap_before
    do_push origin main
    expect_push 6c 1 no
    check 6c "the repository's own rules set to error are reported at the changed lines, as findings: CA1822 at the new method" has "$OUT" "src/Shop/Domain/Clock.cs(5,28): error CA1822"
    check 6c "the build does not fail on them (changed 2026-11-22: a breach of a rule is a finding, never a failed build)" hasnt "$OUT" "the build fails"
    check 6c "none of our entries is among the errors" hasnt "$OUT" "CRF"
    finish 6c
}

case_7() {
    TFM=net9.0 new_repo c7 setup_sdk9
    move_calculator
    commit_all "PriceCalculator moves to Infrastructure, namespace kept, under SDK 9"
    dev_prep; snap_before
    do_push origin main
    expect_push 7 1 no
    expect_acceptance 7
    check 7 "the compiler of the .NET 9 SDK (4.14) wrote the logs" compiler_is_4_14
    check 7 "Shop (net9.0) reviewed" has "$OUT" "Shop (net9.0): reviewed"
    finish 7
}

setup_sdk9() { printf '{\n  "sdk": {\n    "version": "9.0.320",\n    "rollForward": "disable"\n  }\n}\n' > global.json; }

compiler_is_4_14() {
    local logs
    logs=$(ls -d "$WORK"/data/c7-*/logs 2> /dev/null | head -1)
    [ -n "$logs" ] && grep -rEl '"(semanticVersion|version)": "4\.14\.' "$logs" > /dev/null 2>&1
}

case_8() {
    new_repo c8
    write_clock
    local out=$WORK/out/c8.out
    (cd "$REPO" && "$EXE_WIN" review --files src/Shop/Domain/Clock.cs --settings commitfence.json > "$out" 2>&1; echo $? > "$WORK/out/c8.exit")
    check 8 "exit code 2 (incomplete), no blocking finding" equals "$(cat "$WORK/out/c8.exit")" 2
    for rule in TEAM011 TEAM012 ARCH001; do
        check 8 "$rule is named as not run" has "$out" "not run: $rule was not run"
    done
    check 8 "no TEAM012 finding is printed (it did not run)" equals "$(count_rule "$out" TEAM012)" 0
    printf 'namespace Shop.Domain;\n\npublic class Braced\n{\n    public int Pick(int a)\n    {\n        if(a > 1) { return 1; }\n\n        return 0;\n    }\n}\n' > "$REPO/src/Shop/Domain/Braced.cs"
    local line
    line=$(line_of "$REPO/src/Shop/Domain/Braced.cs" 'if(a > 1)')
    (cd "$REPO" && "$EXE_WIN" review --files src/Shop/Domain/Braced.cs --settings commitfence.json > "$out" 2>&1; echo $? > "$WORK/out/c8.exit")
    check 8 "with a TEAM010 breach: exit code 1 wins" equals "$(cat "$WORK/out/c8.exit")" 1
    check 8 "the syntax rule ran: TEAM010 at line $line" equals "$(finding_lines "$out" src/Shop/Domain/Braced.cs TEAM010)" "$line"
    for rule in TEAM011 TEAM012 ARCH001; do
        check 8 "$rule is named as not run (with the breach)" has "$out" "not run: $rule was not run"
    done
}

case_10() {
    local self=$ROOT/self
    rm -rf "$self"
    git clone -q "$REPOSITORY" "$self"
    cd "$self" || exit 1
    git config user.name "Check Runner"
    git config user.email check@example.invalid
    # the clone holds the committed names, so the old tree is dropped before the working folder is copied in, which keeps renamed folders right
    git rm -rq .
    # the working folder's new and changed files go into the clone's commit: a plain copy, nothing is written to the original
    (cd "$REPOSITORY" && git ls-files -z -c -o --exclude-standard | tar --null -T - -cf -) | tar -xf -
    # allowed to be empty: when the working folder is the clone's own last commit, nothing differs
    git add -A && git commit -q --allow-empty -m "the working folder"
    local out=$WORK/out/c10.out started ended
    started=$(now_ms)
    # the whole tree as of HEAD: it exists whatever the history is, a single commit included
    "$EXE_WIN" review --all > "$out" 2>&1
    echo $? > "$WORK/out/c10.exit"
    ended=$(now_ms)
    echo "case 10: CommitFence on itself, $((ended - started)) ms, exit $(cat "$WORK/out/c10.exit")" | tee "$WORK/out/time-10.txt"
    sed -n '1,40p' "$out"
    check 10 "the review of CommitFence itself ends with exit code 0 (no blocking finding, complete; warnings do not change it)" equals "$(cat "$WORK/out/c10.exit")" 0
    check 10 "its summary says 0 blocking" has "$out" ": 0 blocking,"
    check 10 "no error line is printed, so the unchanged repository is not blocked by TEAM013 or any other rule" hasnt "$out" ": error "
    # a line in the C# plug-in that uses the adapters: it compiles, and the layering refuses it
    sed -i 's|</Project>|  <ItemGroup>\n    <ProjectReference Include="..\\CF.Adapters\\CF.Adapters.csproj" />\n  </ItemGroup>\n</Project>|' Dependencies/CF.Languages.CSharp/CF.Languages.CSharp.csproj
    printf 'namespace CF.Languages.CSharp;\n\n/// <summary>\n/// A forbidden dependency, added only in this clone.\n/// </summary>\npublic static class ForbiddenUse\n{\n    /// <summary>\n    /// Uses a type of the adapters.\n    /// </summary>\n    public static object Make()\n    {\n        return new CF.Adapters.Machine.SystemClock();\n    }\n}\n' > Dependencies/CF.Languages.CSharp/ForbiddenUse.cs
    local before
    before=$(git rev-parse HEAD)
    git add -A && git commit -q -m "the C# plug-in uses the adapters"
    started=$(now_ms)
    "$EXE_WIN" review --base "$before" --head HEAD > "$out" 2>&1
    echo $? > "$WORK/out/c10.exit"
    ended=$(now_ms)
    echo "case 10b: the forbidden use, $((ended - started)) ms, exit $(cat "$WORK/out/c10.exit")" | tee -a "$WORK/out/time-10.txt"
    sed -n '1,30p' "$out"
    check 10 "the forbidden use is refused (exit 1)" equals "$(cat "$WORK/out/c10.exit")" 1
    check 10 "ARCH001 names the plug-in file" has "$out" "Dependencies/CF.Languages.CSharp/ForbiddenUse.cs("
    check 10 "ARCH001 says csharp must not use adapters" has "$out" "Layer direction: csharp must not use adapters."
    # the same clone, taken back to before the plug-in change: a Core file that reads a file through the base library is refused by the rule that keeps Core free of file, process and network APIs
    git reset -q --hard "$before"
    printf 'namespace CF.Core;\n\n/// <summary>\n/// A forbidden file use, added only in this clone.\n/// </summary>\npublic static class ForbiddenFileUse\n{\n    /// <summary>\n    /// Reads a file with the base library.\n    /// </summary>\n    public static string Read(string path)\n    {\n        return System.IO.File.ReadAllText(path);\n    }\n}\n' > Dependencies/CF.Core/ForbiddenFileUse.cs
    git add -A && git commit -q -m "Core reads a file itself"
    started=$(now_ms)
    "$EXE_WIN" review --base "$before" --head HEAD > "$out" 2>&1
    echo $? > "$WORK/out/c10.exit"
    ended=$(now_ms)
    echo "case 10c: the forbidden file use in Core, $((ended - started)) ms, exit $(cat "$WORK/out/c10.exit")" | tee -a "$WORK/out/time-10.txt"
    sed -n '1,30p' "$out"
    check 10 "a Core file that calls System.IO.File.ReadAllText is refused (exit 1)" equals "$(cat "$WORK/out/c10.exit")" 1
    check 10 "TEAM013 names the Core file and the type" has "$out" "Dependencies/CF.Core/ForbiddenFileUse.cs(13,"
    check 10 "TEAM013 is the rule that blocks" has "$out" "error TEAM013"
    cd "$ROOT" || exit 1
}

# ---------- part 2: the three faults found in the review of the uncommitted code ----------
write_forms_settings() {
    cat > commitfence.json <<'EOF'
{
    "rules": [
        {
            "id": "TEAM012",
            "language": "csharp",
            "for": "usage",
            "severity": "block",
            "title": "Use IClock, not DateTime.Now",
            "where": { "target": "System.DateTime.Now" },
            "forbid": true
        }
    ],
    "structure": {
        "modules": {
            "domain": [ "namespace:Shop.Domain" ],
            "infrastructure": [ "namespace:Shop.Infrastructure" ],
            "factory": [ "namespace:Shop.Factory" ],
            "reports": [ "project:Shop.Reports" ]
        },
        "directions": {
            "allow": [ "infrastructure -> domain", "factory -> infrastructure", "factory -> domain", "reports -> domain", "reports -> factory" ]
        }
    }
}
EOF
}

write_rich_world() { # forbidden: the Rich family is in Infrastructure; safe: it is in Domain
    local ns=Infrastructure
    [ "$1" = safe ] && ns=Domain
    mkdir -p "src/Shop/$ns" src/Shop/Factory
    cat > "src/Shop/$ns/Rich.cs" <<'EOF'
using System.Collections;
using System.Runtime.CompilerServices;

namespace Shop.@NS@;

public class Rich : IEnumerable, IDisposable
{
    public string Name { get; set; } = "";

    public int Count
    {
        get { return 0; }
    }

    public int this[int index]
    {
        get { return index; }
        set { }
    }

    public static Rich operator +(Rich left, Rich right)
    {
        return left;
    }

    public static Rich operator -(Rich value)
    {
        return value;
    }

    public static implicit operator int(Rich value)
    {
        return 1;
    }

    public static implicit operator Rich(int value)
    {
        return new Rich();
    }

    public static explicit operator long(Rich value)
    {
        return 2;
    }

    public void Add(int item)
    {
    }

    public RichEnumerator GetEnumerator()
    {
        return new RichEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return Array.Empty<int>().GetEnumerator();
    }

    public void Dispose()
    {
    }

    public RichAwaiter GetAwaiter()
    {
        return new RichAwaiter();
    }

    public void Deconstruct(out int first, out int second)
    {
        first = 1;
        second = 2;
    }

    public int Fn()
    {
        return 3;
    }
}

public class RichEnumerator
{
    public bool MoveNext()
    {
        return false;
    }

    public int Current
    {
        get { return 0; }
    }
}

public class RichAwaiter : INotifyCompletion
{
    public bool IsCompleted
    {
        get { return true; }
    }

    public int GetResult()
    {
        return 1;
    }

    public void OnCompleted(Action continuation)
    {
    }
}

public interface IRichContract
{
}

public class RichAttribute : Attribute
{
}

public static class Extensions
{
    public static int Twice(this int value)
    {
        return value * 2;
    }
}
EOF
    sed -i "s/@NS@/$ns/g" "src/Shop/$ns/Rich.cs"
    cat > src/Shop/Factory/Source.cs <<'EOF'
using Shop.@NS@;

namespace Shop.Factory;

public static class Source
{
    public static Func<Rich> Maker { get; } = Make;

    public static Rich Make()
    {
        return new Rich();
    }

    public static void Accept(Rich value)
    {
    }

    public static void Run(Func<Rich, int> body)
    {
    }

    public static List<T> Wrap<T>(Func<T> maker)
    {
        return new List<T> { maker() };
    }
}
EOF
    sed -i "s/@NS@/$ns/g" src/Shop/Factory/Source.cs
}

forms_baseline() { # the setup of a repository of part 2: WORLD is forbidden or safe
    rm -f src/Shop.Reports/LegacyReport.cs
    write_rich_world "$WORLD"
    write_forms_settings
}

FORMS="IndexerRead IndexerWrite IndexFromEnd Binary Unary ImplicitTo ImplicitFrom Explicit TargetNew ObjectInit CollectionInit Foreach Using Await Deconstruct Extension Lambda DelegateVar Attribute Is As Cast PatternProp TypeOf NameOf GenericInferred BaseType InterfaceType ReturnType ParamType VarLocal"

form_body() { # form name: the class text; a line that carries the dependency ends with // L
    case $1 in
        IndexerRead) printf 'public class IndexerReadForm
{
    public int Run()
    {
        return Source.Make()[0]; // L
    }
}
' ;;
        IndexerWrite) printf 'public class IndexerWriteForm\n{\n    public void Run()\n    {\n        Source.Make()[0] = 1; // L\n    }\n}\n' ;;
        IndexFromEnd) printf 'public class IndexFromEndForm\n{\n    public int Run()\n    {\n        return Source.Make()[^1]; // L\n    }\n}\n' ;;
        Binary) printf 'public class BinaryForm\n{\n    public void Run()\n    {\n        _ = Source.Make() + Source.Make(); // L\n    }\n}\n' ;;
        Unary) printf 'public class UnaryForm\n{\n    public void Run()\n    {\n        _ = -Source.Make(); // L\n    }\n}\n' ;;
        ImplicitTo) printf 'public class ImplicitToForm\n{\n    public int Run()\n    {\n        int number = Source.Make(); // L\n        return number;\n    }\n}\n' ;;
        ImplicitFrom) printf 'public class ImplicitFromForm\n{\n    public void Run()\n    {\n        Source.Accept(5); // L\n    }\n}\n' ;;
        Explicit) printf 'public class ExplicitForm\n{\n    public long Run()\n    {\n        return (long)Source.Make(); // L\n    }\n}\n' ;;
        TargetNew) printf 'public class TargetNewForm\n{\n    public void Run()\n    {\n        Source.Accept(new()); // L\n    }\n}\n' ;;
        ObjectInit) printf 'public class ObjectInitForm\n{\n    public void Run()\n    {\n        Source.Accept(new() { Name = "a" }); // L\n    }\n}\n' ;;
        CollectionInit) printf 'public class CollectionInitForm\n{\n    public void Run()\n    {\n        Source.Accept(new() { 1, 2 }); // L\n    }\n}\n' ;;
        Foreach) printf 'public class ForeachForm\n{\n    public int Run()\n    {\n        foreach(var item in Source.Make()) // L\n        {\n            return item;\n        }\n\n        return 0;\n    }\n}\n' ;;
        Using) printf 'public class UsingForm\n{\n    public void Run()\n    {\n        using(Source.Make()) // L\n        {\n        }\n    }\n}\n' ;;
        Await) printf 'public class AwaitForm\n{\n    public async Task<int> Run()\n    {\n        return await Source.Make(); // L\n    }\n}\n' ;;
        Deconstruct) printf 'public class DeconstructForm\n{\n    public int Run()\n    {\n        var (first, second) = Source.Make(); // L\n        return first + second;\n    }\n}\n' ;;
        Extension) printf 'public class ExtensionForm\n{\n    public int Run()\n    {\n        return 3.Twice(); // L\n    }\n}\n' ;;
        Lambda) printf 'public class LambdaForm\n{\n    public void Run()\n    {\n        Source.Run(item => 1); // L\n    }\n}\n' ;;
        DelegateVar) printf 'public class DelegateVarForm\n{\n    public void Run()\n    {\n        var maker = Source.Maker; // L\n    }\n}\n' ;;
        Attribute) printf 'public class AttributeForm\n{\n    [Rich] // L\n    public void Run()\n    {\n    }\n}\n' ;;
        Is) printf 'public class IsForm\n{\n    public bool Run(object value)\n    {\n        return value is Rich; // L\n    }\n}\n' ;;
        As) printf 'public class AsForm\n{\n    public object? Run(object value)\n    {\n        return value as Rich; // L\n    }\n}\n' ;;
        Cast) printf 'public class CastForm\n{\n    public object Run(object value)\n    {\n        return (Rich)value; // L\n    }\n}\n' ;;
        PatternProp) printf 'public class PatternPropForm\n{\n    public bool Run()\n    {\n        return Source.Make() is { Count: 1 }; // L\n    }\n}\n' ;;
        TypeOf) printf 'public class TypeOfForm\n{\n    public Type Run()\n    {\n        return typeof(Rich); // L\n    }\n}\n' ;;
        NameOf) printf 'public class NameOfForm\n{\n    public string Run()\n    {\n        return nameof(Rich); // L\n    }\n}\n' ;;
        GenericInferred) printf 'public class GenericInferredForm\n{\n    public void Run()\n    {\n        Source.Wrap(Source.Make); // L\n    }\n}\n' ;;
        BaseType) printf 'public class BaseTypeForm : Rich // L\n{\n}\n' ;;
        InterfaceType) printf 'public class InterfaceTypeForm : IRichContract // L\n{\n}\n' ;;
        ReturnType) printf 'public class ReturnTypeForm\n{\n    public Rich Get() => Source.Make(); // L\n}\n' ;;
        ParamType) printf 'public class ParamTypeForm\n{\n    public void Take(Rich value) // L\n    {\n    }\n}\n' ;;
        VarLocal) printf 'public class VarLocalForm\n{\n    public void Run()\n    {\n        var value = Source.Make(); // L\n    }\n}\n' ;;
    esac
}

write_form() { # form name, namespace of the Rich family: src/Shop.Reports/Forms/<name>.cs
    mkdir -p src/Shop.Reports/Forms
    {
        printf 'using Shop.Factory;\nusing Shop.%s;\n\nnamespace Shop.Reports;\n\n' "$2"
        form_body "$1"
    } > "src/Shop.Reports/Forms/$1.cs"
}

marked_lines() { grep -n -- '// L$' "$1" | cut -d: -f1 | tr '\n' ' ' | sed 's/ $//'; }

case_11() {
    WORLD=forbidden new_repo c11 forms_baseline
    local name file lines
    for name in $FORMS; do
        file=src/Shop.Reports/Forms/$name.cs
        write_form "$name" Infrastructure
        lines=$(marked_lines "$file")
        commit_all "form $name"
        do_push origin main
        cp "$OUT" "$WORK/out/c11-$name.push"
        check 11 "$name: the hook exited with 1" equals "$HOOK_EXIT" 1
        check 11 "$name: the remote did not move" equals "$MOVED_REMOTE" no
        check 11 "$name: ARCH001 at the marked lines ($lines)" equals "$(finding_lines "$OUT" "$file" ARCH001)" "$lines"
        check 11 "$name: no ARCH001 finding in another file" equals "$(grep -F ' ARCH001:' "$OUT" | grep -vcF "Forms/$name.cs(")" 0
        check 11 "$name: the finding says reports must not use infrastructure" has "$OUT" "Layer direction: reports must not use infrastructure."
        check 11 "$name: no compile error" hasnt "$OUT" "error CS"
        git reset -q --hard origin/main
    done
}

case_11c() {
    WORLD=safe new_repo c11c forms_baseline
    local name
    for name in $FORMS; do
        write_form "$name" Domain
    done
    commit_all "all 31 forms, pointing at an allowed layer"
    dev_prep; snap_before
    do_push origin main
    expect_push 11c 0 yes
    check 11c "no ARCH001 finding" equals "$(count_rule "$OUT" ARCH001)" 0
    check 11c "no compile error" hasnt "$OUT" "error CS"
    check 11c "Shop.Reports reviewed" has "$OUT" "Shop.Reports (net11.0): reviewed"
    check 11c "no rule was left not run" hasnt "$OUT" "not run:"
    finish 11c
}

fields_baseline() {
    forms_baseline
    cat > src/Shop.Reports/Fields.cs <<'EOF'
using Shop.Factory;

namespace Shop.Reports;

public class Fields
{
    private static readonly object _a = new Shop.Infrastructure.Rich(),
        _b = 1;

    public static event Func<int>? E1 = Source.Make().Fn,
        E2 = null;

    public static object P1 { get; } = new Shop.Infrastructure.Rich();
}
EOF
}

fields_case() { # id edit-function: a repository with the old uses, the edit committed and pushed
    local id=$1
    WORLD=forbidden new_repo "c$id" fields_baseline
    "$2"
    commit_all "fields case $id"
    dev_prep; snap_before
    do_push origin main
}

edit_12a() { sed -i 's/_b = 1;/_b = new Shop.Infrastructure.Rich();/' src/Shop.Reports/Fields.cs; LINES=$(line_of src/Shop.Reports/Fields.cs '_b = new Shop'); OLD=$(line_of src/Shop.Reports/Fields.cs '_a = new Shop'); }
edit_12b() { sed -i 's/E2 = null;/E2 = Source.Make().Fn;/' src/Shop.Reports/Fields.cs; LINES=$(line_of src/Shop.Reports/Fields.cs 'E2 = Source.Make()'); OLD=$(line_of src/Shop.Reports/Fields.cs 'E1 = Source.Make()'); }
edit_12c() { sed -i '$d' src/Shop.Reports/Fields.cs; printf '\n    public static object P2 { get; } = new Shop.Infrastructure.Rich();\n}\n' >> src/Shop.Reports/Fields.cs; LINES=$(line_of src/Shop.Reports/Fields.cs 'P2 { get; }'); OLD=$(line_of src/Shop.Reports/Fields.cs 'P1 { get; }'); }
edit_12d() { sed -i 's/_b = 1;/_b = 2;/' src/Shop.Reports/Fields.cs; sed -i '$d' src/Shop.Reports/Fields.cs; printf '\n    public static object P3 { get; } = 3;\n}\n' >> src/Shop.Reports/Fields.cs; }

case_12() {
    local id
    for id in 12a 12b 12c; do
        fields_case "$id" "edit_$id"
        cp "$OUT" "$WORK/out/c$id.push"
        check "$id" "the hook exited with 1" equals "$HOOK_EXIT" 1
        check "$id" "the remote did not move" equals "$MOVED_REMOTE" no
        check "$id" "exactly one finding line: the new declarator's ($LINES)" equals "$(finding_lines "$OUT" src/Shop.Reports/Fields.cs ARCH001)" "$LINES"
        check "$id" "the old use (line $OLD) is not reported" hasnt "$OUT" "Fields.cs($OLD,"
        check "$id" "the finding says reports must not use infrastructure" has "$OUT" "Layer direction: reports must not use infrastructure."
        finish "$id"
    done
    fields_case 12d edit_12d
    expect_push 12d 0 yes
    check 12d "no ARCH001 finding" equals "$(count_rule "$OUT" ARCH001)" 0
    check 12d "Shop.Reports reviewed" has "$OUT" "Shop.Reports (net11.0): reviewed"
    finish 12d
}

case_15() {
    WORLD=forbidden new_repo c15 fields_baseline
    echo "// a comment at the end of the file" >> src/Shop.Reports/Fields.cs
    commit_all "a comment, nothing else"
    dev_prep; snap_before
    do_push origin main
    expect_push 15 0 yes
    check 15 "no ARCH001 finding: the three old violations are not reported" equals "$(count_rule "$OUT" ARCH001)" 0
    check 15 "Shop.Reports reviewed" has "$OUT" "Shop.Reports (net11.0): reviewed"
    check 15 "no rule was left not run" hasnt "$OUT" "not run:"
    finish 15
}

linked_baseline() {
    rm -rf src
    mkdir -p src/Shared src/Shop.A src/Shop.B src/Shop.CA src/Shop.CB
    printf 'namespace Shared;\n\npublic class Linked\n{\n    public int Value()\n    {\n        return 1;\n    }\n}\n' > src/Shared/Linked.cs
    local project
    for project in A B; do
        printf '<Project Sdk="Microsoft.NET.Sdk">\n  <PropertyGroup>\n    <TargetFramework>net11.0</TargetFramework>\n    <Nullable>enable</Nullable>\n    <ImplicitUsings>enable</ImplicitUsings>\n  </PropertyGroup>\n  <ItemGroup>\n    <Compile Include="..\\Shared\\Linked.cs" Link="Linked.cs" />\n  </ItemGroup>\n</Project>\n' > "src/Shop.$project/Shop.$project.csproj"
        write_csproj "src/Shop.C$project/Shop.C$project.csproj" net11.0 "Shop.$project"
        printf 'namespace Shop.C%s;\n\npublic class Consumer\n{\n    public int Go()\n    {\n        return 1;\n    }\n}\n' "$project" > "src/Shop.C$project/Consumer.cs"
    done
    cat > commitfence.json <<'EOF'
{
    "rules": [
        {
            "id": "TEAM012",
            "language": "csharp",
            "for": "usage",
            "severity": "block",
            "title": "Use IClock, not DateTime.Now",
            "where": { "target": "System.DateTime.Now" },
            "forbid": true
        }
    ],
    "structure": {
        "modules": {
            "shared": [ "folder:src/Shared/**" ],
            "consumer": [ "project:Shop.CA", "project:Shop.CB" ]
        },
        "directions": {
            "allow": [ "shared -> consumer" ]
        }
    }
}
EOF
}

linked_change() { # uses (yes or no): both projects get a harmless file, both consumers change
    local project body=1
    [ "$1" = yes ] && body="new Shared.Linked().Value()"
    for project in A B; do
        printf 'namespace Shop.%s;\n\npublic class Extra\n{\n}\n' "$project" > "src/Shop.$project/Extra.cs"
        printf 'namespace Shop.C%s;\n\npublic class Consumer\n{\n    public int Go()\n    {\n        return %s;\n    }\n}\n' "$project" "$body" > "src/Shop.C$project/Consumer.cs"
    done
}

case_13() {
    new_repo c13 linked_baseline
    linked_change yes
    commit_all "both consumers use the linked type"
    dev_prep; snap_before
    do_push origin main
    expect_push 13 1 no
    local project line
    for project in CA CB; do
        line=$(line_of "src/Shop.$project/Consumer.cs" 'new Shared.Linked()')
        check 13 "Shop.$project: ARCH001 at the line of the use ($line)" equals "$(finding_lines "$OUT" "src/Shop.$project/Consumer.cs" ARCH001)" "$line"
    done
    check 13 "the finding says consumer must not use shared" has "$OUT" "Layer direction: consumer must not use shared."
    check 13 "no compile error" hasnt "$OUT" "error CS"
    finish 13
    new_repo c13c linked_baseline
    linked_change no
    commit_all "both consumers change, none uses the linked type"
    dev_prep; snap_before
    do_push origin main
    expect_push 13c 0 yes
    check 13c "no ARCH001 finding" equals "$(count_rule "$OUT" ARCH001)" 0
    check 13c "Shop.CA reviewed" has "$OUT" "Shop.CA (net11.0): reviewed"
    check 13c "Shop.CB reviewed" has "$OUT" "Shop.CB (net11.0): reviewed"
    finish 13c
}

split_baseline() { # MODULE_STYLE: folder or namespace
    rm -rf src
    mkdir -p src/Shop.Shared/Forbidden src/Shop.Shared/Allowed src/Shop.Consumer
    write_csproj src/Shop.Shared/Shop.Shared.csproj net11.0
    write_csproj src/Shop.Consumer/Shop.Consumer.csproj net11.0 Shop.Shared
    printf 'namespace Shop.Shared.Forbidden;\n\npublic class Secret\n{\n    public int Value()\n    {\n        return 1;\n    }\n}\n' > src/Shop.Shared/Forbidden/Secret.cs
    printf 'namespace Shop.Shared.Allowed;\n\npublic class Open\n{\n    public int Value()\n    {\n        return 1;\n    }\n}\n' > src/Shop.Shared/Allowed/Open.cs
    printf 'namespace Shop.Consumer;\n\npublic class Consumer\n{\n    public int Go()\n    {\n        return 1;\n    }\n}\n' > src/Shop.Consumer/Consumer.cs
    local forbidden="folder:src/Shop.Shared/Forbidden/**" allowed="folder:src/Shop.Shared/Allowed/**"
    if [ "$MODULE_STYLE" = namespace ]; then forbidden="namespace:Shop.Shared.Forbidden"; allowed="namespace:Shop.Shared.Allowed"; fi
    cat > commitfence.json <<EOT
{
    "rules": [],
    "structure": {
        "modules": {
            "consumer": [ "project:Shop.Consumer" ],
            "forbidden": [ "$forbidden" ],
            "allowed": [ "$allowed" ]
        },
        "directions": {
            "allow": [ "consumer -> allowed" ]
        }
    }
}
EOT
}

split_case() { # id module-style type touch-shared: a repository, the consumer uses the type, pushed through the hook
    local id=$1 type=$3
    MODULE_STYLE=$2 new_repo "c$id" split_baseline
    printf 'namespace Shop.Consumer;\n\npublic class Consumer\n{\n    public int Go()\n    {\n        return new Shop.Shared.%s().Value();\n    }\n}\n' "$type" > src/Shop.Consumer/Consumer.cs
    [ "$4" = yes ] && printf 'namespace Shop.Shared;\n\npublic class Extra\n{\n}\n' > src/Shop.Shared/Extra.cs
    commit_all "split case $id"
    dev_prep; snap_before
    do_push origin main
}

check_not_run_reason() { # id type: the not-run reason names the using file and member, the used type and its assembly
    check "$1" "ARCH001 is named as not run" has "$OUT" "ARCH001 was not run:"
    check "$1" "the reason names the using file" has "$OUT" "src/Shop.Consumer/Consumer.cs"
    check "$1" "the reason names the using member" has "$OUT" "Shop.Consumer.Consumer.Go"
    check "$1" "the reason names the used type" has "$OUT" "$2"
    check "$1" "the reason names the used assembly" has "$OUT" "of Shop.Shared,"
    check "$1" "no ARCH001 finding" equals "$(count_rule "$OUT" ARCH001)" 0
    check "$1" "the summary says the review is incomplete" has "$OUT" "the review is incomplete"
    check "$1" "the consumer was reviewed" has "$OUT" "Shop.Consumer (net11.0): reviewed"
    check "$1" "no compile error" hasnt "$OUT" "error CS"
}

case_14() {
    split_case 14a folder Forbidden.Secret no
    expect_push 14a 2 no
    check_not_run_reason 14a "Shop.Shared.Forbidden.Secret"
    finish 14a
    split_case 14b namespace Forbidden.Secret no
    expect_push 14b 1 no
    local line
    line=$(line_of src/Shop.Consumer/Consumer.cs 'new Shop.Shared.Forbidden.Secret()')
    check 14b "ARCH001 at the line of the use ($line)" equals "$(finding_lines "$OUT" src/Shop.Consumer/Consumer.cs ARCH001)" "$line"
    check 14b "the finding says consumer must not use forbidden" has "$OUT" "Layer direction: consumer must not use forbidden."
    check 14b "no rule was left not run" hasnt "$OUT" "not run:"
    finish 14b
    split_case 14c folder Forbidden.Secret yes
    expect_push 14c 1 no
    line=$(line_of src/Shop.Consumer/Consumer.cs 'new Shop.Shared.Forbidden.Secret()')
    check 14c "ARCH001 at the line of the use ($line)" equals "$(finding_lines "$OUT" src/Shop.Consumer/Consumer.cs ARCH001)" "$line"
    check 14c "the finding says consumer must not use forbidden" has "$OUT" "Layer direction: consumer must not use forbidden."
    check 14c "no rule was left not run" hasnt "$OUT" "not run:"
    check 14c "Shop.Shared reviewed" has "$OUT" "Shop.Shared (net11.0): reviewed"
    check 14c "Shop.Consumer reviewed" has "$OUT" "Shop.Consumer (net11.0): reviewed"
    finish 14c
    split_case 14d folder Allowed.Open no
    expect_push 14d 2 no
    check_not_run_reason 14d "Shop.Shared.Allowed.Open"
    finish 14d
}

clean_legacy_report() { # the first commit without the forbidden use
    printf 'namespace Shop.Reports;

public class LegacyReport
{
    public string Run(string name)
    {
        return name;
    }
}
' > src/Shop.Reports/LegacyReport.cs
}

case_16() {
    NO_FIRST_PUSH=1 new_repo c16a
    local line
    line=$(line_of src/Shop.Reports/LegacyReport.cs 'Shop.Infrastructure.SqlHelper.Quote')
    dev_prep; snap_before
    do_push origin main
    expect_push 16a 1 no
    check 16a "ARCH001 at the line of the use ($line)" equals "$(finding_lines "$OUT" src/Shop.Reports/LegacyReport.cs ARCH001)" "$line"
    check 16a "the finding says reports must not use infrastructure" has "$OUT" "Layer direction: reports must not use infrastructure."
    check 16a "no rule was left not run" hasnt "$OUT" "not run:"
    check 16a "it does not speak of a tree before: there is none" hasnt "$OUT" "the tree before"
    finish 16a

    NO_FIRST_PUSH=1 new_repo c16b clean_legacy_report
    dev_prep; snap_before
    do_push origin main
    expect_push 16b 0 yes
    check 16b "no ARCH001 finding" equals "$(count_rule "$OUT" ARCH001)" 0
    check 16b "no rule was left not run" hasnt "$OUT" "not run:"
    check 16b "it does not speak of a tree before: there is none" hasnt "$OUT" "the tree before"
    check 16b "Shop reviewed" has "$OUT" "Shop (net11.0): reviewed"
    check 16b "Shop.Reports reviewed" has "$OUT" "Shop.Reports (net11.0): reviewed"
    finish 16b
}

for id in 1 1b 1c 1t 2 3 4 4b 5 5o 6a 6b 6c 7 8 10 11 11c 12 13 14 15 16; do
    if wanted "$id"; then
        "case_$id"
        cd "$ROOT" || exit 1
    fi
done

finish_script
