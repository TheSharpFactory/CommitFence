#!/bin/bash
# The guard of CommitFence until the unit tests exist.
#
#   Tools/smoke.sh <path of the commitfence program> [parent folder for the scratch folder]
#
# For every case it builds a small repository with a local bare remote and a real pre-push hook (the hook
# line `commitfence review --hook pre-push` as its first command), makes one push, and checks four things against a
# verdict that is written in the case BEFORE the push is made:
#   1. the hook's own exit code (recorded by a thin wrapper, because git reports every refusal the same way),
#   2. the findings printed,
#   3. the coverage printed,
#   4. whether the remote moved.
# Every case also checks that the first printed line is the version and folder, and that the developer's
# own state (working folder, index, .git/config, the hooks folder, every ref but git's own refs/remotes, stash, status) is unchanged.
# It prints PASS or FAIL for each case and a total, and exits non-zero when any case fails.
#
# Safe: everything happens in a fresh folder that the script creates itself, with mktemp -d, under the parent
# folder it is given (default: the system's temp folder). The given parent is never written to except for that one
# new child, and only the child is removed at the end, when every case passed. GIT_CONFIG_GLOBAL,
# COMMITFENCE_DATA_FOLDER and COMMITFENCE_USER_PATH_FILE point into the child, so the user's git settings, PATH and
# CommitFence data are never touched. The program must be run from a published folder or a build; a copy of it
# without its analyzers folder is made for one case.
# Nothing is set by hand: the first case runs the program's own install (git setting and PATH, both redirected into
# the child) and every repository that follows finds the program through that setting. One repository is also
# set up by the program's own init, and its hook line is the line init printed.
# Needs: bash, git, the .NET SDK (the reviewed repositories are C# projects), and no network.
set -u

if [ $# -lt 1 ] || [ ! -f "$1" ]; then
    echo "usage: smoke.sh <path of the commitfence program> [parent folder for the scratch folder]" >&2
    exit 64
fi

native() { if command -v cygpath > /dev/null 2>&1; then cygpath -m "$1"; else echo "$1"; fi; }

stop_now() { echo "smoke.sh: $1" >&2; exit 70; }

EXE=$(native "$(cd "$(dirname "$1")" && pwd)/$(basename "$1")")
PARENT=${2:-${TMPDIR:-/tmp}}
mkdir -p "$PARENT" || stop_now "cannot create the parent folder $PARENT"
SCRATCH=$(mktemp -d "$PARENT/commitfence-smoke.XXXXXX") || stop_now "cannot create a scratch folder under $PARENT"
SCRATCH=$(cd "$SCRATCH" && pwd) || stop_now "cannot enter the scratch folder"
if [ -z "$SCRATCH" ] || [ "$SCRATCH" = "/" ] || [ ! -d "$SCRATCH" ]; then
    stop_now "the scratch folder path is not usable: '$SCRATCH'"
fi

SCRATCH_NATIVE=$(native "$SCRATCH")
mkdir -p "$SCRATCH/repos" "$SCRATCH/remotes" "$SCRATCH/data" "$SCRATCH/out" || stop_now "cannot prepare $SCRATCH"

export GIT_CONFIG_GLOBAL="$SCRATCH_NATIVE/gitconfig"
export GIT_CONFIG_NOSYSTEM=1
export COMMITFENCE_DATA_FOLDER="$SCRATCH_NATIVE/data"
export COMMITFENCE_USER_PATH_FILE="$SCRATCH_NATIVE/userpath.txt"
export GIT_TERMINAL_PROMPT=0
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

write_gitconfig() { # the developer's global git settings, without the program's path: install writes that
    cat > "$SCRATCH/gitconfig" <<EOF2
[user]
	name = Smoke Runner
	email = smoke@example.invalid
[init]
	defaultBranch = main
[core]
	autocrlf = false
	longpaths = true
[advice]
	detachedHead = false
EOF2
}
write_gitconfig


PASSES=0
FAILS=0
SKIPS=0

skip_case() { # case reason: a case that cannot be built on this machine; it is printed and counted
    SKIPS=$((SKIPS + 1))
    echo "SKIP  $1: $2"
}
STARTED=$SECONDS

# ---------- the fixture: one C# project, two layers, three rules, one editorconfig rule ----------
write_settings() { # [requires] [extra top-level key]
    {
        echo '{'
        [ -n "${1:-}" ] && echo "    \"requires\": \"$1\","
        [ -n "${2:-}" ] && echo "    \"$2\": 1,"
        cat <<'EOF'
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
            "where": { "containingType": "class", "containingTypeDeclaredAccess": "internal" },
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
            "domain": [ "folder:src/Domain/**" ],
            "infrastructure": [ "folder:src/Infrastructure/**" ]
        },
        "directions": {
            "allow": [ "infrastructure -> domain" ]
        }
    }
}
EOF
    } > commitfence.json
}

write_project() {
    cat > Demo.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
EOF
    printf 'root = true\n\n[*.cs]\ndotnet_style_readonly_field = true\ndotnet_diagnostic.IDE0044.severity = error\n' > .editorconfig
    printf 'bin/\nobj/\n' > .gitignore
}

write_baseline_sources() {
    mkdir -p src/Domain src/Infrastructure
    cat > src/Domain/PriceCalculator.cs <<'EOF'
namespace Demo.Domain;

public class PriceCalculator
{
    public decimal Total(decimal amount)
    {
        return amount * 2;
    }
}
EOF
    cat > src/Domain/OrderService.cs <<'EOF'
namespace Demo.Domain;

public class OrderService
{
    private readonly PriceCalculator _calculator = new PriceCalculator();

    public decimal Place(decimal amount)
    {
        return _calculator.Total(amount);
    }
}
EOF
    cat > src/Infrastructure/SqlHelper.cs <<'EOF'
namespace Demo.Infrastructure;

public static class SqlHelper
{
    public static string Quote(string value)
    {
        return "'" + value + "'";
    }
}
EOF
}

# Each of these writes one source file whose lines are fixed, so a case can state its line numbers beforehand.
src_discount() { # a clean file
    cat > src/Domain/Discount.cs <<'EOF'
namespace Demo.Domain;

public class Discount
{
    public static decimal Apply(decimal amount)
    {
        return amount - 1;
    }
}
EOF
}

src_pricing_braced() { # the braced single-line if is on line 7
    cat > src/Domain/Pricing.cs <<'EOF'
namespace Demo.Domain;

public class Pricing
{
    public static int Sign(int x)
    {
        if(x > 0)
        {
            return 1;
        }

        return 0;
    }
}
EOF
}

src_pricing_fixed() { # the same class, as the developer fixed it in the working folder
    cat > src/Domain/Pricing.cs <<'EOF'
namespace Demo.Domain;

public class Pricing
{
    public static int Sign(int x)
    {
        if(x > 0)
            return 1;

        return 0;
    }
}
EOF
}

src_hidden() { # a protected method in an internal class, on line 5
    cat > src/Domain/Hidden.cs <<'EOF'
namespace Demo.Domain;

internal class Hidden
{
    protected void Quiet()
    {
    }
}
EOF
}

src_missing_package() { # a package no machine has, which a hook run may not fetch
    cat > Demo.csproj <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Nonexistent.Package.Xyz" Version="1.2.3" />
  </ItemGroup>
</Project>
EOF
}

src_type_error() { # a compile error on line 7
    cat > src/Domain/Bad.cs <<'EOF'
namespace Demo.Domain;

public class Bad
{
    public int Value()
    {
        int x = "text";
        return x;
    }
}
EOF
}

src_never_reassigned() { # a breach of the .editorconfig rule IDE0044 on line 5
    cat > src/Domain/Counter.cs <<'EOF'
namespace Demo.Domain;

public static class Counter
{
    private static int _calls = 1;

    public static int Calls()
    {
        return _calls;
    }
}
EOF
}

src_clock() { # a use of DateTime.Now on line 7
    cat > src/Domain/Clock.cs <<'EOF'
namespace Demo.Domain;

public class Clock
{
    public System.DateTime Direct()
    {
        return System.DateTime.Now;
    }
}
EOF
}

# ---------- repositories and pushes ----------
HOOK_WRAPPER='#!/bin/sh\n"$(dirname "$0")/pre-push.team" "$@"\nrc=$?\necho $rc > "$(dirname "$0")/hook-exit.txt"\nexit $rc\n'
HOOK_TEAM='#!/bin/sh\n"$(git config --get commitfence.path)" review --hook pre-push "$@" || exit $?\n'

new_repo() { # name: a repository with an empty bare remote and the team's hook; main is pushed without it
    NAME=$1
    REPO=$SCRATCH/repos/$NAME
    REMOTE=$SCRATCH/remotes/$NAME.git
    git init -q --bare "$REMOTE"
    git init -q "$REPO"
    cd "$REPO" || exit 1
    git remote add origin "$(native "$REMOTE")"
    write_settings
    write_project
    write_baseline_sources
    echo "readme" > README.txt
    printf "$HOOK_TEAM" > .git/hooks/pre-push.team
    printf "$HOOK_WRAPPER" > .git/hooks/pre-push
    chmod +x .git/hooks/pre-push .git/hooks/pre-push.team
    git add -A && git commit -q -m "initial"
    git push -q --no-verify origin main 2> /dev/null
}

commit_all() { git add -A && git commit -q -m "$1"; }

dev_prep() { # an unstaged edit, an untracked file and a stash entry, so that "unchanged" means something
    echo "stashed" >> README.txt
    git stash -q
    echo "unstaged edit" >> README.txt
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

snap_before() { export GIT_OPTIONAL_LOCKS=0; state_of > "$SCRATCH/out/$NAME.before" 2>&1; }
snap_after() { export GIT_OPTIONAL_LOCKS=0; state_of > "$SCRATCH/out/$NAME.after" 2>&1; }

remote_state() { git --git-dir="$REMOTE" for-each-ref --format='%(refname) %(objectname)' 2>&1; }

do_push() { # git push arguments; sets OUT, HOOK_EXIT, MOVED
    OUT=$SCRATCH/out/$NAME.push
    rm -f .git/hooks/hook-exit.txt
    local before after
    before=$(remote_state)
    git push "$@" > "$OUT" 2>&1
    HOOK_EXIT=$(cat .git/hooks/hook-exit.txt 2> /dev/null || echo none)
    after=$(remote_state)
    if [ "$before" = "$after" ]; then MOVED=no; else MOVED=yes; fi
}

do_review() { # review arguments, run by hand (no hook): sets OUT, HOOK_EXIT; the remote is not involved
    OUT=$SCRATCH/out/$NAME.review
    "$EXE" review "$@" > "$OUT" 2>&1
    HOOK_EXIT=$?
    MOVED=no
}

# ---------- the verdict ----------
# A case sets these BEFORE it acts: the hook's exit code, whether the remote moves, text that must appear in the
# findings (V_FINDINGS), text that must not (V_NOT_FINDINGS), the coverage line (V_COVERAGE), and whether no
# finding at all is expected (V_NO_FINDINGS=yes).
verdict() { # exit moved coverage
    V_EXIT=$1
    V_MOVED=$2
    V_COVERAGE=$3
    V_FINDINGS=()
    V_NOT_FINDINGS=()
    V_NO_FINDINGS=no
}

problems_of() { # prints one line for each way the case differs from its verdict
    [ "$HOOK_EXIT" = "$V_EXIT" ] || echo "hook exit code was $HOOK_EXIT, the verdict says $V_EXIT"
    [ "$MOVED" = "$V_MOVED" ] || echo "remote moved: $MOVED, the verdict says $V_MOVED"
    [ -z "$V_COVERAGE" ] || grep -Fq -- "$V_COVERAGE" "$OUT" || echo "coverage not printed: $V_COVERAGE"
    local text
    for text in "${V_FINDINGS[@]}"; do
        grep -Fq -- "$text" "$OUT" || echo "finding or message not printed: $text"
    done
    for text in "${V_NOT_FINDINGS[@]}"; do
        ! grep -Fq -- "$text" "$OUT" || echo "printed, but must not be: $text"
    done
    if [ "$V_NO_FINDINGS" = yes ] && grep -Eq ': (error|warning) ' "$OUT"; then
        echo "a finding was printed, the verdict says none"
    fi
    head -1 "$OUT" | grep -Eq '^CommitFence [^ ]+ \(.+\)$' || echo "the first line is not the version and folder"
    snap_after
    cmp -s "$SCRATCH/out/$NAME.before" "$SCRATCH/out/$NAME.after" || echo "the developer's state changed"
}

judge() { # case title
    report "$1" "$(problems_of)"
}

report() { # case title, the problems found (empty for none)
    local problems=$2
    local seconds=$((SECONDS - CASE_STARTED))
    if [ -z "$problems" ]; then
        PASSES=$((PASSES + 1))
        printf 'PASS  %-34s %3ss\n' "$1" "$seconds"
    else
        FAILS=$((FAILS + 1))
        printf 'FAIL  %-34s %3ss\n' "$1" "$seconds"
        echo "$problems" | sed 's/^/        /'
        echo "        output kept in $SCRATCH/out/$NAME.*"
    fi
}

begin() { CASE_STARTED=$SECONDS; }

# ---------- the cases ----------
case_clean() {
    begin
    new_repo clean
    verdict 0 yes "1 of 1 files reviewed, 1 of 1 builds checked: 0 blocking"
    V_NO_FINDINGS=yes
    src_discount
    commit_all "a clean change"
    dev_prep; snap_before
    do_push origin main
    judge "a clean change"
}

case_blocking() {
    begin
    new_repo blocking
    verdict 1 no "1 of 1 files reviewed, 1 of 1 builds checked: 1 blocking"
    V_FINDINGS=("src/Domain/Pricing.cs(7," "error TEAM010")
    src_pricing_braced
    commit_all "a braced single-line if"
    dev_prep
    src_pricing_fixed
    # At push time the commit holds the breach and the working folder holds the fix: a program that read the
    # working folder would let this push through.
    if git diff --quiet HEAD -- src/Domain/Pricing.cs; then
        stop_now "the case is not set up: the working folder holds the same Pricing.cs as HEAD"
    fi

    snap_before
    do_push origin main
    judge "a blocking change (judged from the commit)"
}

case_unreviewable() {
    begin
    new_repo unreviewable
    verdict 2 no "could not be reviewed, so the review is incomplete"
    V_FINDINGS=("packages it needs are not on this machine" "Run dotnet restore")
    V_NO_FINDINGS=yes
    src_missing_package
    src_discount
    commit_all "a project that needs a package this machine does not have"
    dev_prep; snap_before
    do_push origin main
    judge "a change that cannot be reviewed"
}

case_wrong_settings() {
    begin
    new_repo wrong-settings
    verdict 3 no ""
    V_FINDINGS=("commitfence.json (in the tree under review)(" "is not known in the settings file")
    V_NOT_FINDINGS=("files reviewed")
    write_settings "" "unknownKey"
    src_discount
    commit_all "settings with a key nobody knows"
    dev_prep; snap_before
    do_push origin main
    judge "wrong settings (exit code 3)"
}

case_staged_differs() {
    begin
    new_repo staged-differs
    verdict 1 no "1 of 1 files reviewed, 1 of 1 builds checked: 1 blocking"
    V_FINDINGS=("src/Domain/Pricing.cs(7," "error TEAM010")
    dev_prep
    src_pricing_braced
    git add src/Domain/Pricing.cs
    src_pricing_fixed
    snap_before
    do_review --staged
    judge "staged differs from the folder (staged bad)"

    begin
    new_repo staged-differs-clean
    verdict 0 no "1 of 1 files reviewed, 1 of 1 builds checked: 0 blocking"
    V_NO_FINDINGS=yes
    dev_prep
    src_pricing_fixed
    git add src/Domain/Pricing.cs
    src_pricing_braced
    snap_before
    do_review --staged
    judge "staged differs from the folder (staged ok)"
}

case_several_refs() {
    begin
    new_repo several-refs
    verdict 1 no "2 of 2 files reviewed, 1 of 1 builds checked: 1 blocking"
    V_FINDINGS=("src/Domain/Pricing.cs(7," "error TEAM010")
    V_NOT_FINDINGS=("src/Domain/Discount.cs(")
    git checkout -q -b clean
    src_discount
    commit_all "good"
    git checkout -q main
    git checkout -q -b bad
    src_pricing_braced
    commit_all "bad"
    git checkout -q main
    dev_prep; snap_before
    do_push origin clean bad
    judge "several refs in one push"
}

case_missing_evidence() {
    begin
    new_repo missing-evidence
    verdict 2 no ""
    V_FINDINGS=("Demo (net11.0): not reviewed" "has no closing entry of the meaning analyzer" "not run: ARCH001 was not run")
    V_NOT_FINDINGS=(" ARCH001: ")
    cat > Directory.Build.targets <<'EOF'
<Project>
  <Target Name="DropCommitFenceAnalyzer" BeforeTargets="CoreCompile">
    <ItemGroup>
      <Analyzer Remove="@(Analyzer)" Condition="'%(Filename)' == 'CF.Meaning'" />
    </ItemGroup>
  </Target>
</Project>
EOF
    src_discount
    commit_all "a repository that removes our analyzer from the build"
    dev_prep; snap_before
    do_push origin main
    judge "missing evidence"
}

case_older_program() {
    begin
    new_repo older-program
    verdict 2 no ""
    V_FINDINGS=("commitfence.json: incomplete: this repository requires CommitFence 99.0.0.0 or newer" "update CommitFence")
    V_NOT_FINDINGS=("TEAM010")
    write_settings "99.0.0.0"
    src_pricing_braced
    commit_all "the repository requires a newer program, and the change would be blocked"
    dev_prep; snap_before
    do_push origin main
    judge "a program older than required"

    begin
    new_repo not-older-program
    verdict 0 yes "1 of 1 files reviewed, 1 of 1 builds checked: 0 blocking"
    V_NO_FINDINGS=yes
    V_NOT_FINDINGS=("requires")
    write_settings "0.0.1"
    src_discount
    commit_all "the repository requires an older program than this one"
    dev_prep; snap_before
    do_push origin main
    judge "a program newer than required"
}

case_requires() {
    begin
    new_repo requires-readme-only
    verdict 2 no ""
    V_FINDINGS=("commitfence.json: incomplete: this repository requires CommitFence 99.0.0.0 or newer" "update CommitFence")
    write_settings "99.0.0.0"
    commit_all "the repository now requires a newer program"
    git push -q --no-verify origin main 2> /dev/null
    echo "a line about the product" >> README.txt
    commit_all "only the readme changes"
    dev_prep; snap_before
    do_push origin main
    judge "newer program required, readme-only push"

    begin
    new_repo requires-unknown-key
    verdict 2 no ""
    V_FINDINGS=("commitfence.json: incomplete: this repository requires CommitFence 99.0.0.0 or newer" "update CommitFence")
    V_NOT_FINDINGS=("is not known in the settings file")
    write_settings "99.0.0.0" "keyOfANewerVersion"
    src_discount
    commit_all "settings written for a newer program, with a key this one does not know"
    dev_prep; snap_before
    do_push origin main
    judge "newer program required, unknown key"

    begin
    new_repo requires-nonsense
    verdict 3 no ""
    V_FINDINGS=("The value of 'requires' must be a version of one to four numbers" "not '1-2'")
    V_NOT_FINDINGS=("files reviewed" "requires CommitFence")
    write_settings "1-2"
    src_discount
    commit_all "a requires value that is not a version"
    dev_prep; snap_before
    do_push origin main
    judge "requires value that is not a version"
}

case_install_and_init() {
    begin
    NAME=install-init
    local problems="" before after hookline file="$SCRATCH/out/install-init"
    local program_folder
    program_folder=$(native "$(dirname "$EXE")")

    # install --print says what it would do and writes nothing
    before=$(cat "$SCRATCH/gitconfig")
    "$EXE" install --print > "$file.print" 2>&1 || problems="$problems"$'\n'"install --print did not exit 0"
    [ "$(cat "$SCRATCH/gitconfig")" = "$before" ] || problems="$problems"$'\n'"install --print changed the git settings"
    [ ! -e "$COMMITFENCE_USER_PATH_FILE" ] || problems="$problems"$'\n'"install --print wrote the PATH"
    grep -Fq "Would set the git setting commitfence.path to $EXE" "$file.print" || problems="$problems"$'\n'"install --print did not say what it would set"

    # install writes the setting and the PATH entry
    "$EXE" install > "$file.install" 2>&1 || problems="$problems"$'\n'"install did not exit 0"
    [ "$(git config --global --get commitfence.path)" = "$EXE" ] || problems="$problems"$'\n'"commitfence.path is not the program's path"
    tr '\' '/' < "$COMMITFENCE_USER_PATH_FILE" 2> /dev/null | grep -Fq "$program_folder" || problems="$problems"$'\n'"the program's folder is not in the PATH"

    if [ -n "$problems" ]; then
        report "install and init: install" "$problems"
        echo "the programs of the other cases cannot be found without install: stopping" >&2
        exit 1
    fi

    # init, in a repository of its own: the hook line is the one it prints
    local repo=$SCRATCH/repos/$NAME remote=$SCRATCH/remotes/$NAME.git
    git init -q --bare "$remote"
    git init -q "$repo"
    cd "$repo" || exit 1
    git remote add origin "$(native "$remote")"
    REPO=$repo
    REMOTE=$remote
    "$EXE" init > "$file.init" 2>&1 || problems="$problems"$'\n'"init did not exit 0"
    [ -f commitfence.json ] || problems="$problems"$'\n'"init did not write commitfence.json"
    hookline=$(grep -A1 -F "Add this line as the first command of the pre-push hook" "$file.init" | tail -1)
    case "$hookline" in
        *"git config --get commitfence.path"*) ;;
        *) problems="$problems"$'\n'"init did not print a hook line: '$hookline'" ;;
    esac

    # a second init leaves the file alone
    before=$(sha256sum commitfence.json)
    "$EXE" init > "$file.init2" 2>&1 || problems="$problems"$'\n'"the second init did not exit 0"
    [ "$(sha256sum commitfence.json)" = "$before" ] || problems="$problems"$'\n'"the second init changed commitfence.json"
    grep -Fq "already exists and was left as it is" "$file.init2" || problems="$problems"$'\n'"the second init did not say the file was left as it is"
    report "install and init: install, init" "$problems"

    # a push through the hook that init's line wrote
    begin
    write_project
    write_baseline_sources
    echo "readme" > README.txt
    printf '#!/bin/sh\n%s\n' "$hookline" > .git/hooks/pre-push.team
    printf "$HOOK_WRAPPER" > .git/hooks/pre-push
    chmod +x .git/hooks/pre-push .git/hooks/pre-push.team
    git add -A && git commit -q -m "initial"
    git push -q --no-verify origin main 2> /dev/null
    verdict 0 yes "1 of 1 files reviewed, 1 of 1 builds checked: 0 blocking"
    V_NO_FINDINGS=yes
    src_discount
    commit_all "a clean change, through the hook line that init printed"
    dev_prep; snap_before
    do_push origin main
    judge "install and init: push through init's line"
}

case_two_at_once() {
    begin
    new_repo two-at-once
    local c0 c1 c2 round good=0
    c0=$(git rev-parse HEAD)
    src_discount
    commit_all "clean"
    c1=$(git rev-parse HEAD)
    src_pricing_braced
    commit_all "breaking"
    c2=$(git rev-parse HEAD)
    dev_prep; snap_before
    verdict 0 no "1 of 1 files reviewed, 1 of 1 builds checked: 0 blocking"
    V_NO_FINDINGS=yes
    rm -rf "$SCRATCH/data"/*
    do_review --base "$c0" --head "$c1"
    cp "$OUT" "$SCRATCH/out/two-at-once.soloA"
    "$EXE" review --base "$c1" --head "$c2" > "$SCRATCH/out/two-at-once.soloB" 2>&1
    rm -rf "$SCRATCH/data"/*
    for round in 1 2 3; do
        ("$EXE" review --base "$c0" --head "$c1" > "$SCRATCH/out/two-at-once.a$round" 2>&1; echo $? > "$SCRATCH/out/two-at-once.a$round.rc") &
        ("$EXE" review --base "$c1" --head "$c2" > "$SCRATCH/out/two-at-once.b$round" 2>&1; echo $? > "$SCRATCH/out/two-at-once.b$round.rc") &
        wait
        if [ "$(cat "$SCRATCH/out/two-at-once.a$round.rc")" = 0 ] && [ "$(cat "$SCRATCH/out/two-at-once.b$round.rc")" = 1 ] \
            && cmp -s "$SCRATCH/out/two-at-once.a$round" "$SCRATCH/out/two-at-once.soloA" && cmp -s "$SCRATCH/out/two-at-once.b$round" "$SCRATCH/out/two-at-once.soloB"; then
            good=$((good + 1))
        fi
    done
    grep -Fq "error TEAM010" "$SCRATCH/out/two-at-once.soloB" || echo "the breaking review alone did not find the breach" >> "$SCRATCH/out/two-at-once.problem"
    [ "$good" = 3 ] || echo "not every round finished as it did alone: $good of 3" >> "$SCRATCH/out/two-at-once.problem"
    if [ -f "$SCRATCH/out/two-at-once.problem" ]; then
        OUT=$SCRATCH/out/two-at-once.problem
        HOOK_EXIT=none
    fi
    judge "two reviews at once (3 repeats)"
}

case_code_rule() {
    begin
    new_repo kind-code
    verdict 1 no "1 of 1 builds checked"
    V_FINDINGS=("src/Domain/Hidden.cs(5," "error TEAM011")
    src_hidden
    commit_all "a protected method in an internal class"
    dev_prep; snap_before
    do_push origin main
    judge "rule kind: a code rule (TEAM011)"
}

case_compile_error() {
    begin
    new_repo kind-compile
    verdict 1 no "1 of 1 builds checked"
    V_FINDINGS=("src/Domain/Bad.cs(7," "error CS0029" "[Demo, net11.0]")
    src_type_error
    commit_all "a type error"
    dev_prep; snap_before
    do_push origin main
    judge "rule kind: a compile error"
}

case_editorconfig() {
    begin
    new_repo kind-editorconfig
    verdict 1 no "1 of 1 builds checked"
    V_FINDINGS=("src/Domain/Counter.cs(5," "error IDE0044" "[Demo, net11.0]")
    src_never_reassigned
    commit_all "a field that is never reassigned, an error in the .editorconfig"
    dev_prep; snap_before
    do_push origin main
    judge "rule kind: an .editorconfig breach"
}

case_meaning_rule() {
    begin
    new_repo kind-meaning
    verdict 1 no "1 of 1 builds checked"
    V_FINDINGS=("src/Domain/Clock.cs(7," "error TEAM012")
    V_NOT_FINDINGS=("not run:")
    src_clock
    commit_all "the clock is read"
    dev_prep; snap_before
    do_push origin main
    judge "rule kind: a meaning rule (TEAM012)"
}

case_layer_direction() {
    begin
    new_repo kind-layer
    verdict 1 no "builds checked"
    V_FINDINGS=("src/Domain/OrderService.cs(5," "src/Domain/OrderService.cs(9," "error ARCH001" "Layer direction: domain must not use infrastructure.")
    V_NOT_FINDINGS=("not run:" "src/Infrastructure/SqlHelper.cs(")
    git mv src/Domain/PriceCalculator.cs src/Infrastructure/PriceCalculator.cs
    commit_all "PriceCalculator moves to Infrastructure, its namespace kept"
    dev_prep; snap_before
    do_push origin main
    judge "rule kind: a layer direction (ARCH001)"
}

echo "CommitFence smoke script: $EXE"
echo "scratch folder: $SCRATCH_NATIVE"

case_install_and_init
case_clean
case_blocking
case_unreviewable
case_wrong_settings
case_staged_differs
case_several_refs
case_missing_evidence
case_older_program
case_requires
case_two_at_once
case_code_rule
case_compile_error
case_editorconfig
case_meaning_rule
case_layer_direction

echo
echo "TOTAL: $PASSES passed, $FAILS failed, $SKIPS skipped, $((SECONDS - STARTED)) seconds"

if [ "$FAILS" -ne 0 ]; then
    exit 1
fi

cd / && rm -rf "$SCRATCH"

exit 0
