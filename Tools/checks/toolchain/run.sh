#!/bin/bash
# The toolchain: the build in a private copy, evidence, packages, no network, a machine without dotnet, a build that
# hangs. Builds every test repository from nothing and prints
# PASS or FAIL per check. Expected results are in expected.md.
# Usage: Tools/checks/toolchain/run.sh <path of the commitfence program> [parent folder for the scratch folder] [case ids...]
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
ROOT=$(mktemp -d "$PARENT/commitfence-toolchain.XXXXXX") || stop_now "cannot create a scratch folder under $PARENT"
ROOT=$(cd "$ROOT" && pwd) || stop_now "cannot enter the scratch folder"
if [ -z "$ROOT" ] || [ "$ROOT" = "/" ] || [ ! -d "$ROOT" ]; then
    stop_now "the scratch folder path is not usable: '$ROOT'"
fi

STARTED=$SECONDS
# the connection counter of cases 9 and 16 is a small Python script; the shared interpreter of this machine is used when it is there
if command -v ai-py > /dev/null 2>&1; then LISTENER_PYTHON=(ai-py -I); else LISTENER_PYTHON=(python3 -I); fi
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

# ---------- the test solution ----------
write_editorconfig() { # severity of IDE0044
    cat > .editorconfig <<EOF
root = true

[*.cs]
dotnet_style_readonly_field = true
dotnet_diagnostic.IDE0044.severity = $1
dotnet_diagnostic.IDE0051.severity = warning
dotnet_diagnostic.IDE0060.severity = suggestion
EOF
}

write_csproj() { # file frameworks-property(TargetFramework|TargetFrameworks) frameworks [reference]
    local reference=${4:-}
    {
        echo '<Project Sdk="Microsoft.NET.Sdk">'
        echo '  <PropertyGroup>'
        echo "    <$2>$3</$2>"
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

write_greeter() { # uses G_NO_HELLO, G_NO_OLD, G_EXTRA
    {
        echo "namespace Library;"
        echo
        echo "public static class Greeter"
        echo "{"
        if [ -z "${G_NO_HELLO:-}" ]; then
            printf '    public static string Hello(string name)\n    {\n        return "Hello " + name;\n    }\n\n'
        fi
        if [ -z "${G_NO_OLD:-}" ]; then
            printf '    public static int Old()\n    {\n        return 1;\n    }\n\n'
        fi
        printf '    public static int One()\n    {\n        return 1;\n    }\n'
        if [ -n "${G_EXTRA:-}" ]; then
            printf '\n%s\n' "$G_EXTRA"
        fi
        echo "}"
    } > Library/Greeter.cs
}

write_use() {
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
}

write_runner() { # uses T_BREACH (a field that is never reassigned), T_EXTRA
    {
        echo "namespace Tool;"
        echo
        echo "public static class Runner"
        echo "{"
        if [ -n "${T_BREACH:-}" ]; then
            printf '    private static int _calls = 1;\n\n    public static int Calls()\n    {\n        return _calls;\n    }\n\n'
        fi
        printf '    public static string Run()\n    {\n        return Consumer.Use.Run();\n    }\n'
        if [ -n "${T_EXTRA:-}" ]; then
            printf '\n%s\n' "$T_EXTRA"
        fi
        echo "}"
    } > Tool/Runner.cs
}

write_solution() { # uses EDITOR_SEVERITY (default error)
    mkdir -p Library Consumer Tool
    write_editorconfig "${EDITOR_SEVERITY:-error}"
    write_csproj Library/Library.csproj TargetFramework net9.0
    write_csproj Consumer/Consumer.csproj TargetFrameworks "net9.0;net11.0" Library
    write_csproj Tool/Tool.csproj TargetFramework net11.0 Consumer
    write_greeter
    write_use
    write_runner
    echo "readme" > README.md
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
}

line_of() { grep -n -- "$2" "$1" | head -1 | cut -d: -f1; }

# ---------- repositories ----------
new_repo() { # name: a repository with a bare remote and the team's hook; the solution is pushed without the hook
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
    write_solution
    git add -A && git commit -q -m "initial"
    git push -q --no-verify origin main 2> /dev/null
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

do_push() {
    OUT=$WORK/out/$NAME.push
    rm -f .git/hooks/hook-exit.txt
    local before after started ended
    before=$(remote_state)
    started=$(date +%s)
    git push "$@" > "$OUT" 2>&1
    ended=$(date +%s)
    ELAPSED=$((ended - started))
    HOOK_EXIT=$(cat .git/hooks/hook-exit.txt 2> /dev/null || echo none)
    after=$(remote_state)
    if [ "$before" = "$after" ]; then MOVED=no; else MOVED=yes; fi
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

finish() { # case-id: check 11
    snap_after
    check "11.$1" "working folder, index, config, hooks, every ref, stash and status unchanged" cmp -s "$WORK/out/$NAME.before" "$WORK/out/$NAME.after"
    check "11.$1" "the private copy holds no file of ours inside the tree besides build outputs" private_copy_clean
}

first_line_ok() { head -1 "$1" | grep -Eq '^CommitFence [^ ]+ \(.+\)$'; }

expect_push() { # case-id exit-code moved
    check "$1" "the hook exited with $2" equals "$HOOK_EXIT" "$2"
    check "$1" "the remote moved: $3" equals "$MOVED" "$3"
    check "$1" "first printed line is the version and folder" first_line_ok "$OUT"
}

wanted() { [ -z "$SELECTED" ] || [[ " $SELECTED " == *" $1 "* ]]; }

# ---------- the cases ----------
case_1() {
    new_repo c1
    G_EXTRA=$'    public static int Twice(int a)\n    {\n        return a * 2;\n    }' write_greeter
    commit_all "a new static method in Library"
    dev_prep; snap_before
    do_push origin main
    expect_push 1 0 yes
    check 1 "no finding printed" hasnt "$OUT" " error "
    for unit in "Library (net9.0)" "Consumer (net9.0)" "Consumer (net11.0)" "Tool (net11.0)"; do
        check 1 "coverage: $unit reviewed" has "$OUT" "$unit: reviewed"
    done
    check 1 "summary: 4 of 4 builds checked" has "$OUT" "4 of 4 builds checked"
    finish 1
}

case_2() {
    new_repo c2
    G_EXTRA=$'    public static int Bad()\n    {\n        int x = "text";\n        return x;\n    }' write_greeter
    local line
    line=$(line_of Library/Greeter.cs 'int x = "text"')
    commit_all "a type error in Library"
    dev_prep; snap_before
    do_push origin main
    expect_push 2 1 no
    check 2 "the error names file and line ($line)" has "$OUT" "Library/Greeter.cs($line,"
    check 2 "the error is CS0029 and names Library, net9.0" has "$OUT" "error CS0029"
    check 2 "the error names its project and framework" has "$OUT" "[Library, net9.0]"
    check 2 "Library reviewed with the failing build said" has "$OUT" "Library (net9.0): reviewed: the build fails: 1 compile errors"
    for unit in "Consumer (net9.0)" "Consumer (net11.0)" "Tool (net11.0)"; do
        check 2 "$unit not reviewed, prerequisite failed" has "$OUT" "$unit: not reviewed: its prerequisite"
        check 2 "$unit is never shown reviewed" hasnt "$OUT" "$unit: reviewed"
    done
    finish 2
}

case_3() {
    new_repo c3
    G_NO_HELLO=1 write_greeter
    local line
    line=$(line_of Consumer/Use.cs 'Greeter.Hello')
    commit_all "Library removes a member Consumer uses"
    dev_prep; snap_before
    do_push origin main
    expect_push 3 1 no
    check 3 "the error is in Consumer/Use.cs at the call ($line)" has "$OUT" "Consumer/Use.cs($line,"
    check 3 "CS0117 reported once, naming both frameworks" has "$OUT" "[Consumer, net11.0, net9.0]"
    check 3 "reported once" equals "$(grep -c 'error CS0117' "$OUT")" 1
    check 3 "Tool not reviewed" has "$OUT" "Tool (net11.0): not reviewed"
    finish 3
}

case_4() {
    new_repo c4
    G_NO_OLD=1 write_greeter
    local line
    line=$(line_of Consumer/Use.cs 'Greeter.Old')
    commit_all "Library removes a member Consumer uses under net9.0 only"
    dev_prep; snap_before
    do_push origin main
    expect_push 4 1 no
    check 4 "the error is in Consumer/Use.cs ($line)" has "$OUT" "Consumer/Use.cs($line,"
    check 4 "the finding names net9.0 only" has "$OUT" "[Consumer, net9.0]"
    check 4 "net11.0 is not named in a finding" hasnt "$OUT" "[Consumer, net11.0, net9.0]"
    check 4 "Consumer (net11.0) reviewed" has "$OUT" "Consumer (net11.0): reviewed"
    check 4 "Tool (net11.0) reviewed" has "$OUT" "Tool (net11.0): reviewed"
    check 4 "Consumer (net9.0) reviewed, its build failing" has "$OUT" "Consumer (net9.0): reviewed: the build fails"
    finish 4
}

breach_field() { printf '    private static int _calls = 1;\n\n    public static int Calls()\n    {\n        return _calls;\n    }'; }

case_5a() {
    new_repo c5a
    G_EXTRA=$(breach_field) write_greeter
    local line
    line=$(line_of Library/Greeter.cs '_calls = 1')
    commit_all "a breach at error level"
    dev_prep; snap_before
    do_push origin main
    expect_push 5a 1 no
    check 5a "IDE0044 error at the changed line ($line)" has "$OUT" "Library/Greeter.cs($line,"
    check 5a "it is IDE0044, an error, naming Library and net9.0" has "$OUT" "error IDE0044"
    check 5a "names the project" has "$OUT" "[Library, net9.0]"
    finish 5a
}

case_5b() {
    EDITOR_SEVERITY=warning new_repo c5b
    G_EXTRA=$(breach_field)$'\n\n    public static int Twice(int a, int unused)\n    {\n        return a * 2;\n    }' write_greeter
    local line
    line=$(line_of Library/Greeter.cs '_calls = 1')
    commit_all "a breach at warning level and an unused parameter"
    dev_prep; snap_before
    do_push origin main
    expect_push 5b 0 yes
    check 5b "IDE0044 shown as a warning at the changed line ($line)" has "$OUT" "Library/Greeter.cs($line,"
    check 5b "it is a warning" has "$OUT" "warning IDE0044"
    check 5b "the suggestion (IDE0060) is shown as a warning" has "$OUT" "warning IDE0060"
    check 5b "no error printed" hasnt "$OUT" " error "
    for unit in "Library (net9.0)" "Consumer (net9.0)" "Consumer (net11.0)" "Tool (net11.0)"; do
        check 5b "coverage: $unit reviewed" has "$OUT" "$unit: reviewed"
    done
    finish 5b
}

case_5c() {
    new_repo c5c
    T_BREACH=1 write_runner
    git add -A && git commit -q -m "an old breach in Tool"
    git push -q --no-verify origin main 2> /dev/null
    T_BREACH=1 T_EXTRA=$'    public static int Twice(int a)\n    {\n        return a * 2;\n    }' write_runner
    commit_all "an unrelated method in Tool"
    dev_prep; snap_before
    do_push origin main
    expect_push 5c 0 yes
    check 5c "the old breach is not reported" hasnt "$OUT" "IDE0044"
    check 5c "Tool (net11.0) reviewed" has "$OUT" "Tool (net11.0): reviewed"
    finish 5c
}

case_6() {
    new_repo c6
    G_EXTRA=$'#pragma warning disable IDE0044\n    private static int _calls = 1;\n#pragma warning restore IDE0044\n\n    public static int Calls()\n    {\n        return _calls;\n    }' write_greeter
    commit_all "a suppressed breach"
    dev_prep; snap_before
    do_push origin main
    expect_push 6 0 yes
    check 6 "the suppressed breach is not reported" hasnt "$OUT" "IDE0044"
    check 6 "Library (net9.0) reviewed" has "$OUT" "Library (net9.0): reviewed"
    finish 6
}

case_7() {
    new_repo c7
    mkdir -p scripts
    printf 'namespace Stray;\n\npublic static class Stray\n{\n    public static int One()\n    {\n        return 1;\n    }\n}\n' > scripts/Stray.cs
    commit_all "a file no project compiles"
    dev_prep; snap_before
    do_push origin main
    expect_push 7 2 no
    check 7 "says the file is not covered" has "$OUT" "scripts/Stray.cs: not reviewed: not covered"
    check 7 "the summary says the review is incomplete" has "$OUT" "the review is incomplete"
    finish 7
}

case_8() {
    new_repo c8
    printf '{\n  "sdk": {\n    "version": "99.0.100",\n    "rollForward": "disable"\n  }\n}\n' > global.json
    G_EXTRA=$'    public static int Twice(int a)\n    {\n        return a * 2;\n    }' write_greeter
    commit_all "global.json asks for an SDK that is not installed"
    dev_prep; snap_before
    do_push origin main
    expect_push 8 2 no
    check 8 "plain message: the machine is not ready" has "$OUT" "The machine is not ready"
    check 8 "it names global.json and what to do" has "$OUT" "Install the SDK version global.json asks for"
    finish 8
}

case_9() {
    new_repo c9
    local port=18999 log="$WORK/out/proxy.log" stop="$WORK/out/proxy.stop" proxy
    proxy="http://127.0.0.1:$port"
    rm -f "$stop"
    "${LISTENER_PYTHON[@]}" "$(cygpath -w "$HERE/listener.py")" "$port" "$(cygpath -w "$log")" "$(cygpath -w "$stop")" &
    sleep 3
    write_csproj Library/Library.csproj TargetFramework net9.0
    sed -i 's|</Project>|  <ItemGroup>\n    <PackageReference Include="Missing.Package.ForCommitFence" Version="1.2.3" />\n  </ItemGroup>\n</Project>|' Library/Library.csproj
    commit_all "Library needs a package that is not on the machine"
    dev_prep; snap_before
    # control: a plain restore of this tree, with the same proxy settings, must reach the proxy
    local control=$WORK/out/c9-control
    rm -rf "$control"; mkdir -p "$control"
    git archive HEAD | tar -x -C "$control"
    : > "$log"
    (cd "$control" && HTTP_PROXY=$proxy HTTPS_PROXY=$proxy http_proxy=$proxy https_proxy=$proxy timeout 120 dotnet restore Library/Library.csproj > "$WORK/out/c9-control.txt" 2>&1)
    local control_hits
    control_hits=$(wc -l < "$log")
    : > "$log"
    HTTP_PROXY=$proxy HTTPS_PROXY=$proxy http_proxy=$proxy https_proxy=$proxy do_push origin main
    local review_hits
    review_hits=$(wc -l < "$log")
    touch "$stop"
    wait 2> /dev/null
    expect_push 9 2 no
    check 9 "plain message: packages are not on this machine, no network used" has "$OUT" "packages it needs are not on this machine, and a review never uses the network"
    check 9 "it says what to run" has "$OUT" "Run dotnet restore on the solution while online"
    check 9 "control: a plain restore made $control_hits connection(s) to the proxy (at least 1)" [ "$control_hits" -ge 1 ]
    check 9 "the review made $review_hits connections to the proxy (0)" equals "$review_hits" 0
    for unit in "Library (net9.0)" "Consumer (net9.0)" "Consumer (net11.0)" "Tool (net11.0)"; do
        check 9 "$unit not reviewed" has "$OUT" "$unit: not reviewed"
    done
    finish 9
}

case_10() {
    new_repo c10
    echo "more words" >> README.md
    commit_all "only the README changes"
    dev_prep; snap_before
    do_push origin main
    expect_push 10 0 yes
    check 10 "the note says the file of no known language was not looked at" has "$OUT" "1 changed file of no known language was not looked at."
    check 10 "no build coverage line" hasnt "$OUT" "(net"
    check 10 "no logs folder was created" test ! -d "$(ls -d "$WORK"/data/c10-* | head -1)/logs"
    finish 10
}

case_12() {
    new_repo c12
    local outside="$WORK/out/c12-outside" before_bytes
    mkdir -p "$outside"
    printf 'decoy' > "$outside/Library.dll"
    before_bytes=$(sha256sum "$outside/Library.dll")
    sed -i "s|</PropertyGroup>|  <IntermediateOutputPath>$outside/</IntermediateOutputPath>\n  </PropertyGroup>|" Library/Library.csproj
    G_EXTRA=$'    public static int Twice(int a)\n    {\n        return a * 2;\n    }' write_greeter
    commit_all "Library puts its compiler output outside the tree"
    dev_prep; snap_before
    do_push origin main
    expect_push 12 2 no
    check 12 "the file outside the tree is still there" test -f "$outside/Library.dll"
    check 12 "and its bytes are unchanged" equals "$(sha256sum "$outside/Library.dll")" "$before_bytes"
    check 12 "Library reported not reviewed" has "$OUT" "Library (net9.0): not reviewed"
    check 12 "the reason says outside the private copy" has "$OUT" "outside the private copy"
    finish 12
}

case_13() {
    new_repo c13
    git rm -q Library/Library.csproj
    git commit -q -m "the library project is deleted, Consumer still references it"
    dev_prep; snap_before
    do_push origin main
    check 13 "the hook refused the push with exit 1, a blocking compile error (it exited with $HOOK_EXIT)" equals "$HOOK_EXIT" 1
    check 13 "the remote did not move" equals "$MOVED" no
    check 13 "Consumer was selected and compiled" has "$OUT" "Consumer (net9.0):"
    finish 13
}

case_14() {
    new_repo c14
    mkdir -p Shared
    printf 'namespace Shared;\n\npublic static class Types\n{\n    public static int Value()\n    {\n        return 1;\n    }\n}\n' > Shared/Types.cs
    sed -i 's|</Project>|  <ItemGroup>\n    <Compile Include="..\Shared\Types.cs" Link="Types.cs" />\n  </ItemGroup>\n</Project>|' Library/Library.csproj
    G_EXTRA=$'    public static int FromShared()\n    {\n        return Shared.Types.Value();\n    }' write_greeter
    commit_all "Library compiles a linked shared file"
    git push -q --no-verify origin main 2> /dev/null
    git rm -q Shared/Types.cs
    git commit -q -m "the shared file is deleted"
    dev_prep; snap_before
    do_push origin main
    expect_push 14 1 no
    check 14 "Library (net9.0) reviewed with its failing build" has "$OUT" "Library (net9.0): reviewed: the build fails"
    check 14 "the missing input is named" has "$OUT" "Types.cs"
    check 14 "Consumer not reviewed, prerequisite failed" has "$OUT" "Consumer (net9.0): not reviewed: its prerequisite"
    finish 14
}

case_15() {
    new_repo c15
    git rm -rq Library Consumer Tool
    git commit -q -m "no project left"
    git push -q --no-verify origin main 2> /dev/null
    mkdir -p src
    printf 'namespace Stray;\n\npublic static class Stray\n{\n    public static int One()\n    {\n        return 1;\n    }\n}\n' > src/Stray.cs
    commit_all "a source file in a tree with no project"
    dev_prep; snap_before
    do_push origin main
    expect_push 15 2 no
    check 15 "says the file is not covered" has "$OUT" "src/Stray.cs: not reviewed: not covered"
    check 15 "the summary says the review is incomplete" has "$OUT" "the review is incomplete"
    finish 15
}

case_16() {
    new_repo c16
    local port=18998 log="$WORK/out/proxy16.log" stop="$WORK/out/proxy16.stop"
    rm -f "$stop"
    "${LISTENER_PYTHON[@]}" "$(cygpath -w "$HERE/listener.py")" "$port" "$(cygpath -w "$log")" "$(cygpath -w "$stop")" &
    sleep 3
    write_csproj Library/Library.csproj TargetFramework net9.0
    sed -i "s|</Project>|  <PropertyGroup>\n    <RestoreAdditionalProjectSources>https://127.0.0.1:$port/index.json</RestoreAdditionalProjectSources>\n  </PropertyGroup>\n  <ItemGroup>\n    <PackageReference Include=\"Missing.Package.ForCommitFence\" Version=\"1.2.3\" />\n  </ItemGroup>\n</Project>|" Library/Library.csproj
    commit_all "Library needs a package and names an extra feed"
    dev_prep; snap_before
    local control=$WORK/out/c16-control
    rm -rf "$control"; mkdir -p "$control"
    git archive HEAD | tar -x -C "$control"
    : > "$log"
    (cd "$control" && timeout 120 dotnet restore Library/Library.csproj > "$WORK/out/c16-control.txt" 2>&1)
    local control_hits
    control_hits=$(wc -l < "$log")
    : > "$log"
    do_push origin main
    local review_hits
    review_hits=$(wc -l < "$log")
    touch "$stop"
    wait 2> /dev/null
    expect_push 16 2 no
    check 16 "packages are not on this machine, no network used" has "$OUT" "packages it needs are not on this machine, and a review never uses the network"
    check 16 "control: a plain restore made $control_hits connection(s) to the listener (at least 1)" [ "$control_hits" -ge 1 ]
    check 16 "the review made $review_hits connections to the listener (0)" equals "$review_hits" 0
    check 16 "Library not reviewed" has "$OUT" "Library (net9.0): not reviewed"
    finish 16
}

case_17() {
    new_repo c17
    mkdir -p AppB
    write_csproj AppB/AppB.csproj TargetFramework net11.0 Library
    printf 'namespace AppB;\n\npublic static class Helper\n{\n    public static int One()\n    {\n        return Library.Greeter.One();\n    }\n}\n' > AppB/Helper.cs
    sed -i 's|</Project>|  <ItemGroup>\n    <PackageReference Include="Missing.Package.ForCommitFence" Version="1.2.3" />\n  </ItemGroup>\n</Project>|' Consumer/Consumer.csproj
    commit_all "a second application and a package Consumer needs"
    git push -q --no-verify origin main 2> /dev/null
    G_EXTRA=$'    public static int Twice(int a)\n    {\n        return a * 2;\n    }' write_greeter
    printf 'namespace AppB;\n\npublic static class Helper\n{\n    private static int _calls = 1;\n\n    public static int Calls()\n    {\n        return _calls;\n    }\n\n    public static int One()\n    {\n        return Library.Greeter.One();\n    }\n}\n' > AppB/Helper.cs
    local line
    line=$(line_of AppB/Helper.cs '_calls = 1')
    commit_all "a method in Library and a breach in AppB"
    dev_prep; snap_before
    do_push origin main
    expect_push 17 1 no
    check 17 "the breach in the healthy application is reported ($line)" has "$OUT" "AppB/Helper.cs($line,"
    check 17 "it is IDE0044 naming AppB" has "$OUT" "[AppB, net11.0]"
    check 17 "Library (net9.0) reviewed" has "$OUT" "Library (net9.0): reviewed"
    check 17 "AppB (net11.0) reviewed" has "$OUT" "AppB (net11.0): reviewed"
    for unit in "Consumer (net9.0)" "Consumer (net11.0)" "Tool (net11.0)"; do
        check 17 "$unit not reviewed" has "$OUT" "$unit: not reviewed"
    done
    check 17 "the packages message appears" has "$OUT" "packages it needs are not on this machine"
    check 17 "Library is not named as failing" hasnt "$OUT" "Library (net9.0): not reviewed"
    finish 17
}

# ---------- more cases (verdicts in expected.md) ----------
hangers() { { ps -W 2> /dev/null || ps -e 2> /dev/null; } | grep -ciE '(^|[ \/])(ping|sleep)(\.exe)?( |$)'; }

case_18() {
    new_repo c18
    local port=18997 log="$WORK/out/proxy18.log" stop="$WORK/out/proxy18.stop" proxy
    proxy="http://127.0.0.1:$port"
    rm -f "$stop"
    "${LISTENER_PYTHON[@]}" "$(cygpath -w "$HERE/listener.py")" "$port" "$(cygpath -w "$log")" "$(cygpath -w "$stop")" &
    sleep 3
    G_EXTRA=$'    public static int Twice(int a)\n    {\n        return a * 2;\n    }' write_greeter
    commit_all "a clean change in Library"
    dev_prep; snap_before
    # control: a plain restore of a project that needs a package, with the same proxy settings, must reach the proxy
    local control=$WORK/out/c18-control
    rm -rf "$control"; mkdir -p "$control"
    write_csproj "$control/Control.csproj" TargetFramework net9.0
    sed -i 's|</Project>|  <ItemGroup>\n    <PackageReference Include="Missing.Package.ForCommitFence" Version="1.2.3" />\n  </ItemGroup>\n</Project>|' "$control/Control.csproj"
    : > "$log"
    (cd "$control" && HTTP_PROXY=$proxy HTTPS_PROXY=$proxy http_proxy=$proxy https_proxy=$proxy timeout 120 dotnet restore Control.csproj > "$WORK/out/c18-control.txt" 2>&1)
    local control_hits
    control_hits=$(wc -l < "$log")
    : > "$log"
    HTTP_PROXY=$proxy HTTPS_PROXY=$proxy http_proxy=$proxy https_proxy=$proxy ALL_PROXY=$proxy do_push origin main
    local review_hits
    review_hits=$(wc -l < "$log")
    touch "$stop"
    wait 2> /dev/null
    expect_push 18 0 yes
    check 18 "control: a plain restore made $control_hits connection(s) to the proxy (at least 1)" [ "$control_hits" -ge 1 ]
    check 18 "the review made $review_hits connections to the proxy (0)" equals "$review_hits" 0
    for unit in "Library (net9.0)" "Consumer (net9.0)" "Consumer (net11.0)" "Tool (net11.0)"; do
        check 18 "$unit reviewed" has "$OUT" "$unit: reviewed"
    done
    finish 18
}

case_19() {
    new_repo c19
    G_EXTRA=$'    public static int Twice(int a)\n    {\n        return a * 2;\n    }' write_greeter
    commit_all "a clean change in Library"
    # the hook alone starts the program with a PATH that holds the folder of git and nothing else, so dotnet cannot be found
    local only_git
    only_git=$(dirname "$(command -v git)")
    printf '#!/bin/sh\nprogram=$(git config --get commitfence.path)\nPATH="%s" "$program" review --hook pre-push "$@" || exit $?\n' "$only_git" > .git/hooks/pre-push.team
    dev_prep; snap_before
    do_push origin main
    expect_push 19 2 no
    check 19 "the machine is not ready" has "$OUT" "The machine is not ready"
    check 19 "the dotnet command cannot be started" has "$OUT" "the dotnet command cannot be started"
    check 19 "no finding is printed" hasnt "$OUT" ": error "
    finish 19
}

case_20() {
    new_repo c20
    sed -i 's|</Project>|  <Target Name="Hang" BeforeTargets="CoreCompile">\n    <Exec Command="ping -n 300 127.0.0.1 \&gt; nul" Condition="'"'"'$(OS)'"'"' == '"'"'Windows_NT'"'"'" />\n    <Exec Command="sleep 300" Condition="'"'"'$(OS)'"'"' != '"'"'Windows_NT'"'"'" />\n  </Target>\n</Project>|' Library/Library.csproj
    G_EXTRA=$'    public static int Twice(int a)\n    {\n        return a * 2;\n    }' write_greeter
    commit_all "the build of Library hangs"
    dev_prep; snap_before
    local before_hangers
    before_hangers=$(hangers)
    COMMITFENCE_PROCESS_LIMIT_SECONDS=20 do_push origin main
    expect_push 20 2 no
    check 20 "Library (net9.0) is not reviewed because the build did not finish" has "$OUT" "Library (net9.0): not reviewed: the build did not finish"
    check 20 "the output says dotnet did not finish within 20 seconds" has "$OUT" "did not finish within 20 seconds and was stopped"
    check 20 "the push ended in under 120 seconds ($ELAPSED)" [ "$ELAPSED" -lt 120 ]
    check 20 "nothing of the hanging target is left running" equals "$(hangers)" "$before_hangers"
    check 20 "the review says it is incomplete" has "$OUT" "the review is incomplete"
    finish 20
}

gate_file() { # the symbol: the file the #if cases add to Consumer
    cat <<EOF
namespace Consumer;

public static class Gate
{
    public static int Pick(int a)
    {
#if $1
        if(a > 1) { return 1; }
#endif
        return 0;
    }
}
EOF
}

case_21() {
    new_repo c21
    gate_file NET11_0_OR_GREATER > Consumer/Gate.cs
    local line
    line=$(line_of Consumer/Gate.cs 'if(a > 1)')
    commit_all "a breach under #if NET11_0_OR_GREATER"
    dev_prep; snap_before
    do_push origin main
    expect_push 21 1 no
    check 21 "one finding at the line of the if ($line), ending [Consumer, net11.0]" equals "$(grep -c "^Consumer/Gate.cs($line,[0-9]*): error TEAM010: No braces around a single-line if \[Consumer, net11.0\]$" "$OUT")" 1
    check 21 "no finding names net9.0" equals "$(grep -c 'TEAM010.*net9.0' "$OUT")" 0
    finish 21
}

case_21b() {
    new_repo c21b
    gate_file NET9_0_OR_GREATER > Consumer/Gate.cs
    local line
    line=$(line_of Consumer/Gate.cs 'if(a > 1)')
    commit_all "a breach under #if NET9_0_OR_GREATER"
    dev_prep; snap_before
    do_push origin main
    expect_push 21b 1 no
    check 21b "one finding at the line of the if ($line), without a project and framework suffix" equals "$(grep -c "^Consumer/Gate.cs($line,[0-9]*): error TEAM010: No braces around a single-line if$" "$OUT")" 1
    finish 21b
}

case_21c() {
    new_repo c21c
    gate_file NEVER_DEFINED > Consumer/Gate.cs
    commit_all "a breach under a symbol no framework defines"
    dev_prep; snap_before
    do_push origin main
    expect_push 21c 0 yes
    check 21c "no finding" hasnt "$OUT" "TEAM010"
    finish 21c
}

add_team011() { # the team's rule that needs meaning: what a class really is, over all its parts
    cat > commitfence.json <<'EOF'
{
    "rules": [
        {
            "id": "TEAM011",
            "language": "csharp",
            "for": "method",
            "severity": "block",
            "title": "Only public and private methods inside an internal class",
            "where": { "containingType": "class", "containingTypeDeclaredAccess": "internal" },
            "require": { "declaredAccess": ["public", "private"] }
        }
    ]
}
EOF
}

case_21d() {
    new_repo c21d
    add_team011
    cat > Consumer/Hidden.cs <<'EOF'
namespace Consumer;

internal class Hidden
{
#if NET11_0_OR_GREATER
    protected int Late()
    {
        return 1;
    }
#endif

    public int Open()
    {
        return 2;
    }
}
EOF
    local line
    line=$(line_of Consumer/Hidden.cs 'protected int Late')
    commit_all "a protected method of an internal class, under #if NET11_0_OR_GREATER"
    dev_prep; snap_before
    do_push origin main
    expect_push 21d 1 no
    check 21d "exactly one TEAM011 finding" equals "$(grep -c 'error TEAM011:' "$OUT")" 1
    check 21d "it is at the line of the protected method ($line)" equals "$(grep -c "^Consumer/Hidden.cs($line,[0-9]*): error TEAM011: " "$OUT")" 1
    finish 21d
}

case_21e() {
    new_repo c21e
    cat > Consumer/Guard.cs <<'EOF'
namespace Consumer;

public static class Guard
{
#if NET9_0_OR_GREATER
    public static int Value()
    {
        return 1;
    }
#else
#error This code needs .NET 9 or later
#endif
}
EOF
    commit_all "an #error in the branch no framework of the project compiles"
    dev_prep; snap_before
    do_push origin main
    expect_push 21e 0 yes
    check 21e "Consumer (net9.0) is reviewed" has "$OUT" "Consumer (net9.0): reviewed"
    check 21e "Consumer (net11.0) is reviewed" has "$OUT" "Consumer (net11.0): reviewed"
    check 21e "nothing is said not to parse" hasnt "$OUT" "does not parse"
    finish 21e
}

case_21f() {
    new_repo c21f
    sed -i 's|    <Nullable>enable</Nullable>|    <Nullable>enable</Nullable>\n    <DefineConstants>$(DefineConstants);FEATURE_A,FEATURE_B</DefineConstants>|' Consumer/Consumer.csproj
    gate_file FEATURE_B > Consumer/Gate.cs
    local line
    line=$(line_of Consumer/Gate.cs 'if(a > 1)')
    commit_all "a breach under a symbol that follows a comma in DefineConstants"
    dev_prep; snap_before
    do_push origin main
    expect_push 21f 1 no
    check 21f "one finding at the line of the if ($line), without a project and framework suffix" equals "$(grep -c "^Consumer/Gate.cs($line,[0-9]*): error TEAM010: No braces around a single-line if$" "$OUT")" 1
    finish 21f
}

case_22() {
    new_repo c22
    local envfile=$WORK/out/c22-environment.txt
    rm -f "$envfile"
    sed -i 's|</Project>|  <Target Name="WriteEnvironment" BeforeTargets="CoreCompile">\n    <WriteLinesToFile File="$(CHECK_ENV_FILE)" Lines="ADD=$(DOTNET_ADD_GLOBAL_TOOLS_TO_PATH);NOLOGO=$(DOTNET_NOLOGO);OPTOUT=$(DOTNET_CLI_TELEMETRY_OPTOUT);HOME=$(DOTNET_CLI_HOME)" Overwrite="true" />\n  </Target>\n</Project>|' Library/Library.csproj
    G_EXTRA=$'    public static int Twice(int a)\n    {\n        return a * 2;\n    }' write_greeter
    commit_all "Library writes the environment its build sees"
    dev_prep; snap_before
    unset DOTNET_CLI_HOME
    CHECK_ENV_FILE="$(cygpath -m "$envfile")" DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=1 DOTNET_NOLOGO=0 do_push origin main
    expect_push 22 0 yes
    check 22 "the build saw DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0 (the check's own value was 1)" bash -c "tr -d '\r' < '$envfile' | grep -qx 'ADD=0'"
    check 22 "the build saw DOTNET_NOLOGO=1 (the check's own value was 0)" bash -c "tr -d '\r' < '$envfile' | grep -qx 'NOLOGO=1'"
    check 22 "the build saw DOTNET_CLI_TELEMETRY_OPTOUT=1" bash -c "tr -d '\r' < '$envfile' | grep -qx 'OPTOUT=1'"
    check 22 "the build saw no DOTNET_CLI_HOME: the program never sets it" bash -c "tr -d '\r' < '$envfile' | grep -qx 'HOME='"
    finish 22
}


case_23() {
    new_repo c23
    printf 'this is not a resource file' > Library/Strings.resx
    commit_all "an invalid resource file in Library"
    dev_prep; snap_before
    do_push origin main
    expect_push 23 2 no
    check 23 "Library (net9.0) is named as not reviewed, the build failing on the resource file" has "$OUT" "Library (net9.0): not reviewed: the build failed before the compiler ran"
    check 23 "the invalid resource file is named" has "$OUT" "Strings.resx : error MSB3103"
    check 23 "first printed line is the version and folder" first_line_ok "$OUT"
    finish 23
}

case_24() {
    new_repo c24
    echo "a note" > Library/notes.txt
    commit_all "a harmless file inside the Library folder"
    dev_prep; snap_before
    do_push origin main
    expect_push 24 0 yes
    for unit in "Library (net9.0)" "Consumer (net9.0)" "Consumer (net11.0)" "Tool (net11.0)"; do
        check 24 "coverage: $unit reviewed" has "$OUT" "$unit: reviewed"
    done
    check 24 "summary: 4 of 4 builds checked" has "$OUT" "4 of 4 builds checked"
    check 24 "the file is not counted as one that was not looked at" hasnt "$OUT" "not looked at"
    finish 24
}

builds_quietly() { # project: builds it outside the repository's folders, so nothing is left in the working folder
    dotnet build "$1" -nologo -v:q -p:UseSharedCompilation=false -nodeReuse:false -o "$WORK/out/$NAME.control-bin" -p:BaseIntermediateOutputPath="$(cygpath -m "$WORK/out/$NAME.control-obj")/" > "$WORK/out/$NAME.control-build" 2>&1
}

case_25() {
    new_repo c25
    mkdir -p build
    printf '<Project>
  <PropertyGroup>
    <DefineConstants>$(DefineConstants);NEEDED</DefineConstants>
  </PropertyGroup>
</Project>
' > build/common.props
    sed -i 's|</Project>|  <Import Project="../build/common.props" />\n</Project>|' Library/Library.csproj
    G_EXTRA=$'#if NEEDED
    public static int Needed()
    {
        return 1;
    }
#endif

    public static int UseNeeded()
    {
        return Needed();
    }' write_greeter
    check 25 "the project file imports the props file" has Library/Library.csproj 'Import Project="../build/common.props"'
    check 25 "before the push, Library builds with the symbol the props file defines" builds_quietly Library/Library.csproj
    commit_all "Library imports a props file that defines a symbol its code needs"
    git push -q --no-verify origin main 2> /dev/null
    printf '<Project>
  <PropertyGroup>
    <DefineConstants>$(DefineConstants);OTHER</DefineConstants>
  </PropertyGroup>
</Project>
' > build/common.props
    commit_all "the props file no longer defines the symbol"
    dev_prep; snap_before
    do_push origin main
    expect_push 25 1 no
    check 25 "the compile error is printed in the unchanged Greeter.cs, naming Library" has "$OUT" "error CS0103: The name 'Needed' does not exist in the current context [Library, net9.0]"
    check 25 "Library (net9.0) is reviewed, its build failing" has "$OUT" "Library (net9.0): reviewed: the build fails"
    finish 25
}

case_26() {
    new_repo c26
    echo "more words" >> README.md
    commit_all "only the README at the root changes"
    dev_prep; snap_before
    do_push origin main
    expect_push 26 0 yes
    check 26 "the note says one file was not looked at" has "$OUT" "1 changed file of no known language was not looked at."
    check 26 "no build coverage line" hasnt "$OUT" "(net"
    finish 26
}

case_27() {
    new_repo c27
    G_EXTRA=$(breach_field) write_greeter
    commit_all "an old breach in Library"
    git push -q --no-verify origin main 2> /dev/null
    sed -i 's|Hello("x")|Hello("y")|' Consumer/Use.cs
    commit_all "one unrelated line in Consumer"
    dev_prep; snap_before
    do_push origin main
    expect_push 27 0 yes
    check 27 "Library (net9.0) is not reported as not reviewed" hasnt "$OUT" "Library (net9.0): not reviewed"
    check 27 "Consumer (net9.0) reviewed" has "$OUT" "Consumer (net9.0): reviewed"
    check 27 "Consumer (net11.0) reviewed" has "$OUT" "Consumer (net11.0): reviewed"
    check 27 "Tool (net11.0) reviewed" has "$OUT" "Tool (net11.0): reviewed"
    check 27 "the summary: 3 of 3 builds checked" has "$OUT" "3 of 3 builds checked"
    check 27 "nothing is reported: no IDE0044" hasnt "$OUT" "IDE0044"
    check 27 "no coverage line says not reviewed" hasnt "$OUT" "not reviewed"
    finish 27
}

breach_field_other() { printf '    private static int _other = 1;\n\n    public static int Others()\n    {\n        return _other;\n    }'; }

case_30() {
    new_repo c30
    G_EXTRA=$(breach_field) write_greeter
    commit_all "an old breach in Library"
    git push -q --no-verify origin main 2> /dev/null
    G_EXTRA="$(breach_field)"$'\n\n'"$(breach_field_other)" write_greeter
    local line old
    line=$(line_of Library/Greeter.cs '_other = 1')
    old=$(line_of Library/Greeter.cs '_calls = 1')
    commit_all "a second breach of the same rule on new lines"
    dev_prep; snap_before
    do_push origin main
    expect_push 30 1 no
    check 30 "exactly one IDE0044 finding" equals "$(grep -c ' IDE0044:' "$OUT")" 1
    check 30 "it is at the new line ($line)" has "$OUT" "Library/Greeter.cs($line,"
    check 30 "the old line ($old) is not reported" hasnt "$OUT" "Library/Greeter.cs($old,"
    check 30 "Library (net9.0) reviewed" has "$OUT" "Library (net9.0): reviewed"
    finish 30
}

case_31() {
    new_repo c31
    G_EXTRA=$(breach_field) write_greeter
    commit_all "an old breach in Library"
    git push -q --no-verify origin main 2> /dev/null
    sed -i 's|"Hello " + name|"Hi " + name|' Library/Greeter.cs
    check 31 "the setup edit changed the file" has Library/Greeter.cs '"Hi " + name'
    commit_all "an unrelated line in the file with the old breach"
    dev_prep; snap_before
    do_push origin main
    expect_push 31 0 yes
    check 31 "no IDE0044 is reported" hasnt "$OUT" "IDE0044"
    check 31 "Library (net9.0) reviewed" has "$OUT" "Library (net9.0): reviewed"
    finish 31
}

case_32() {
    new_repo c32
    G_EXTRA=$(breach_field) write_greeter
    commit_all "an old breach in Library"
    git push -q --no-verify origin main 2> /dev/null
    G_EXTRA="$(breach_field)"$'\n\n'$'    public static int Bad()\n    {\n        int x = "text";\n        return x;\n    }' write_greeter
    local line
    line=$(line_of Library/Greeter.cs 'int x = "text"')
    commit_all "a real compile error beside the old breach"
    dev_prep; snap_before
    do_push origin main
    expect_push 32 1 no
    check 32 "CS0029 at the changed line ($line)" has "$OUT" "Library/Greeter.cs($line,"
    check 32 "it is the compile error CS0029" has "$OUT" "error CS0029"
    check 32 "Library (net9.0) reviewed: the build fails" has "$OUT" "Library (net9.0): reviewed: the build fails"
    check 32 "the old breach is not reported" hasnt "$OUT" "IDE0044"
    finish 32
}

case_33() {
    new_repo c33
    sed -i 's|  </PropertyGroup>|    <GenerateDocumentationFile>true</GenerateDocumentationFile>\n    <NoWarn>CS1591</NoWarn>\n    <WarningsAsErrors>CS1591</WarningsAsErrors>\n  </PropertyGroup>|' Library/Library.csproj
    check 33 "the project sets its own NoWarn" has Library/Library.csproj "<NoWarn>CS1591</NoWarn>"
    G_EXTRA=$(breach_field) write_greeter
    commit_all "an old breach in Library, in a project with its own NoWarn"
    git push -q --no-verify origin main 2> /dev/null
    sed -i 's|"Hello " + name|"Hi " + name|' Library/Greeter.cs
    commit_all "an unrelated line in Library"
    dev_prep; snap_before
    do_push origin main
    expect_push 33 0 yes
    check 33 "no IDE0044 is reported" hasnt "$OUT" "IDE0044"
    check 33 "the project's own NoWarn held: no CS1591" hasnt "$OUT" "CS1591"
    check 33 "Library (net9.0) reviewed" has "$OUT" "Library (net9.0): reviewed"
    finish 33
}

own_slot_case() { # case-id, the file the repository keeps (relative), what it names in the slot (relative; may hold a wildcard)
    local id=$1 file=$2 named=$3
    new_repo "c$id"
    mkdir -p "$(dirname "$file")"
    printf '<Project>\n  <PropertyGroup>\n    <DefineConstants>$(DefineConstants);NEEDED</DefineConstants>\n  </PropertyGroup>\n</Project>\n' > "$file"
    printf '<Project>\n  <PropertyGroup>\n    <CustomAfterDirectoryBuildTargets>$(MSBuildThisFileDirectory)%s</CustomAfterDirectoryBuildTargets>\n  </PropertyGroup>\n</Project>\n' "$named" > Directory.Build.props
    G_EXTRA=$'#if NEEDED
    public static int Needed()
    {
        return 1;
    }
#endif

    public static int UseNeeded()
    {
        return Needed();
    }' write_greeter
    check "$id" "before the push, Library builds with the symbol the repository's own targets file defines" builds_quietly Library/Library.csproj
    commit_all "the repository names its own file in the build slot, and Library needs what it defines"
    git push -q --no-verify origin main 2> /dev/null
    sed -i 's|"Hello " + name|"Hi " + name|' Library/Greeter.cs
    commit_all "an unrelated line in Library"
    dev_prep; snap_before
    do_push origin main
    expect_push "$id" 0 yes
    check "$id" "no finding and no compile error" hasnt "$OUT" " error "
    for unit in "Library (net9.0)" "Consumer (net9.0)" "Consumer (net11.0)" "Tool (net11.0)"; do
        check "$id" "coverage: $unit reviewed" has "$OUT" "$unit: reviewed"
    done
    check "$id" "summary: 4 of 4 builds checked" has "$OUT" "4 of 4 builds checked"
    finish "$id"
}

case_34() { own_slot_case 34 build/repo.targets build/repo.targets; }

case_35() { own_slot_case 35 "build/repo,team.targets" "build/repo,team.targets"; }

case_36() { own_slot_case 36 build/checks/needed.targets "build/checks/*.targets"; }

not_accepted() { # case-id exit-code: the push ended with exactly that code, and the remote did not move
    check "$1" "the hook exited with $2 (it exited with $HOOK_EXIT)" equals "$HOOK_EXIT" "$2"
    check "$1" "the remote moved: no" equals "$MOVED" no
    check "$1" "first printed line is the version and folder" first_line_ok "$OUT"
}

case_28() {
    new_repo c28
    mkdir -p sib
    printf '<Project>\n</Project>\n' > sib/Directory.Build.targets
    sed -i 's|</Project>|  <Import Project="../sib/Directory.Build.targets" />\n</Project>|' Library/Library.csproj
    commit_all "Library imports a targets file with a standard name from another folder"
    git push -q --no-verify origin main 2> /dev/null
    printf '<Project>\n  <Target Name="Stop" BeforeTargets="CoreCompile">\n    <Error Text="stopped by the imported targets file" />\n  </Target>\n</Project>\n' > sib/Directory.Build.targets
    commit_all "the imported targets file now stops the build"
    dev_prep; snap_before
    do_push origin main
    not_accepted 28 2
    check 28 "Library (net9.0) has a coverage line" has "$OUT" "Library (net9.0):"
    finish 28
}

case_29() {
    new_repo c29
    echo "some data" > Library/Data.txt
    sed -i 's|</Project>|  <ItemGroup>\n    <EmbeddedResource Include="Data.txt" />\n  </ItemGroup>\n</Project>|' Library/Library.csproj
    commit_all "Library embeds a data file by name"
    git push -q --no-verify origin main 2> /dev/null
    mkdir -p docs
    git mv Library/Data.txt docs/Data.txt
    commit_all "the data file moves out of the project"
    dev_prep; snap_before
    do_push origin main
    not_accepted 29 1
    check 29 "Library (net9.0) has a coverage line" has "$OUT" "Library (net9.0):"
    finish 29
}


case_37() {
    new_repo c37
    local standin=$WORK/standin real_dotnet
    mkdir -p "$standin/src"
    printf '<Project Sdk="Microsoft.NET.Sdk">\n  <PropertyGroup>\n    <OutputType>Exe</OutputType>\n    <TargetFramework>net11.0</TargetFramework>\n    <AssemblyName>dotnet</AssemblyName>\n    <ImplicitUsings>enable</ImplicitUsings>\n  </PropertyGroup>\n</Project>\n' > "$standin/src/standin.csproj"
    cat > "$standin/src/Program.cs" <<'STANDIN'
using System.Diagnostics;

// a stand-in for dotnet: the project evaluation is answered with valid JSON that holds no key, everything else goes to the real dotnet
if(args.Length > 0 && args[0] == "msbuild" && args.Contains("-getProperty:TargetFramework"))
{
    Console.Out.Write("{}");
    return 0;
}

var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("CHECK_REAL_DOTNET")!) { UseShellExecute = false };
foreach(var argument in args)
    start.ArgumentList.Add(argument);

using var process = Process.Start(start)!;
process.WaitForExit();
return process.ExitCode;
STANDIN
    check 37 "precondition: the stand-in dotnet is built" bash -c "dotnet build '$(cygpath -m "$standin/src")' -c Release -o '$(cygpath -m "$standin/bin")' -nologo -v q > '$WORK/out/c37-standin.log' 2>&1"
    real_dotnet=$(cygpath -m "$(command -v dotnet)")
    G_EXTRA=$'    public static int Twice(int a)\n    {\n        return a * 2;\n    }' write_greeter
    commit_all "a clean change in Library"
    printf '#!/bin/sh\nprogram=$(git config --get commitfence.path)\nPATH="%s:$PATH" CHECK_REAL_DOTNET="%s" "$program" review --hook pre-push "$@" || exit $?\n' "$standin/bin" "$real_dotnet" > .git/hooks/pre-push.team
    dev_prep; snap_before
    do_push origin main
    expect_push 37 2 no
    check 37 "a coverage line names the failure and its kind" has "$OUT" "C# build: not reviewed: the build could not be run: KeyNotFoundException"
    check 37 "the review says it is incomplete" has "$OUT" "the review is incomplete"
    check 37 "no line says the program could not finish" hasnt "$OUT" "could not finish"
    check 37 "no finding is printed" hasnt "$OUT" ": error "
    finish 37
}

for id in 1 2 3 4 5a 5b 5c 6 7 8 9 10 12 13 14 15 16 17 18 19 20 21 21b 21c 21d 21e 21f 22 23 24 25 26 27 28 29 30 31 32 33 34 35 36 37; do
    if wanted "$id"; then
        "case_$id"
        cd "$ROOT" || exit 1
    fi
done

finish_script
