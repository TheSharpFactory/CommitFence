#!/bin/bash
# The whole of a folder, on command: `commitfence review --all [<folder or project file>]`, read from the last commit.
# Builds every test repository from nothing and prints PASS, FAIL or SKIP per check.
# Expected results are in expected.md.
# Usage: Tools/checks/whole/run.sh <path of the commitfence program> [parent folder for the scratch folder] [case ids...]
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
EXE_WIN=$(native "$(cd "$(dirname "$1")" && pwd)/$(basename "$1")")
EXE=$EXE_WIN
PARENT=${2:-${TMPDIR:-/tmp}}
shift
if [ $# -gt 0 ]; then shift; fi
SELECTED="$*"
mkdir -p "$PARENT" || stop_now "cannot create the parent folder $PARENT"
ROOT=$(mktemp -d "$PARENT/commitfence-whole.XXXXXX") || stop_now "cannot create a scratch folder under $PARENT"
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

check() { # case description command...   (the command's success is the check)
    local case_id=$1 text=$2
    shift 2
    if "$@"; then report PASS "$case_id" "$text"; else report FAIL "$case_id" "$text"; fi
}

has() { grep -Fq -- "$2" "$1"; }
hasnt() { ! grep -Fq -- "$2" "$1"; }
equals() { [ "$1" = "$2" ]; }
count_of() { grep -Fc -- "$2" "$1" | grep -qx "$3"; } # file text number: the text is on exactly that many lines

# ---------- the rules, the projects and the sources ----------
write_settings() { # [structure section, as the text after the comma]
    {
        cat <<'EOF'
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
EOF
        if [ -n "${CLOCK_RULE:-}" ]; then
            cat <<'EOF'
        ,
        {
            "id": "TEAM012",
            "language": "csharp",
            "for": "usage",
            "severity": "block",
            "title": "Use IClock, not DateTime.Now",
            "where": { "target": "System.DateTime.Now" },
            "forbid": true
        }
EOF
        fi
        echo "    ]"
        if [ -n "${1:-}" ]; then
            echo "    ,"
            echo "$1"
        fi
        echo "}"
    } > commitfence.json
}

STRUCTURE='    "structure": {
        "modules": { "api": [ "project:Api" ], "legacy": [ "project:Legacy" ] },
        "directions": { "allow": [ "api -> legacy" ] }
    }'

write_editorconfig() {
    cat > .editorconfig <<'EOF'
root = true

[*.cs]
dotnet_style_readonly_field = true
dotnet_diagnostic.IDE0044.severity = error
EOF
}

write_csproj() { # file [project reference] [one extra line inside the first property group]
    local reference=${2:-}
    {
        echo '<Project Sdk="Microsoft.NET.Sdk">'
        echo '  <PropertyGroup>'
        echo '    <TargetFramework>net11.0</TargetFramework>'
        echo '    <Nullable>enable</Nullable>'
        echo '    <ImplicitUsings>enable</ImplicitUsings>'
        echo '    <EnableNETAnalyzers>false</EnableNETAnalyzers>'
        if [ -n "${3:-}" ]; then echo "    $3"; fi
        echo '  </PropertyGroup>'
        if [ -n "$reference" ]; then
            echo '  <ItemGroup>'
            echo "    <ProjectReference Include=\"..\\$reference\\$reference.csproj\" />"
            echo '  </ItemGroup>'
        fi
        echo '</Project>'
    } > "$1"
}

sign_breach() { cat <<'EOF'
    public int Sign(int x)
    {
        if(x > 0)
        {
            return 1;
        }

        return 0;
    }
EOF
}

breach_class() { # namespace class: a class that holds the braced single-line if
    printf 'namespace %s;\n\npublic class %s\n{\n' "$1" "$2"
    sign_breach
    echo "}"
}

write_api() { # breach|clean
    mkdir -p src/Api
    write_csproj src/Api/Api.csproj
    {
        echo "namespace Api;"
        echo
        echo "public class Service"
        echo "{"
        echo "    public int Add(int a, int b)"
        echo "    {"
        echo "        return a + b;"
        echo "    }"
        if [ "$1" = breach ]; then
            echo
            sign_breach
        fi
        echo "}"
    } > src/Api/Service.cs
}

write_legacy() { # [extra]: with an argument, a clean method is appended after the old breaches
    mkdir -p src/Legacy
    write_csproj src/Legacy/Legacy.csproj
    {
        echo "namespace Legacy;"
        echo
        echo "public class Old"
        echo "{"
        echo "    private static int _calls = 1;"
        echo
        echo "    public int Calls()"
        echo "    {"
        echo "        return _calls;"
        echo "    }"
        echo
        sign_breach
        if [ -n "${1:-}" ]; then
            echo
            echo "    public int Twice(int a)"
            echo "    {"
            echo "        return a * 2;"
            echo "    }"
        fi
        echo "}"
    } > src/Legacy/Legacy.cs
}

clean_class() { # namespace class
    printf 'namespace %s;\n\npublic class %s\n{\n    public int Two()\n    {\n        return 2;\n    }\n}\n' "$1" "$2"
}

line_of() { grep -n -- "$2" "$1" | head -1 | cut -d: -f1; } # file text

# ---------- repositories ----------
commit_all() { git add -A && git commit -q -m "$1"; }

new_repo() { # name  (empty repository with an empty bare remote and the team's hook)
    NAME=$1
    REPO=$WORK/repos/$NAME
    REMOTE=$WORK/remotes/$NAME.git
    git init -q --bare "$REMOTE"
    git init -q "$REPO"
    cd "$REPO" || exit 1
    git remote add origin "$(cygpath -m "$REMOTE")"
    printf 'bin/\nobj/\n' > .gitignore
    echo "readme" > README.txt
    printf '#!/bin/sh\n"$(git config --get commitfence.path)" review --hook pre-push "$@" || exit $?\n' > .git/hooks/pre-push.team
    printf '#!/bin/sh\n"$(dirname "$0")/pre-push.team" "$@"\nrc=$?\necho $rc > "$(dirname "$0")/hook-exit.txt"\nexit $rc\n' > .git/hooks/pre-push
    chmod +x .git/hooks/pre-push .git/hooks/pre-push.team
}

standard_repo() { # name: Api with one breach, Legacy with the two old ones; committed, not pushed
    new_repo "$1"
    write_settings
    write_editorconfig
    write_api breach
    write_legacy
    commit_all "initial"
}

head10() { git rev-parse HEAD | cut -c1-10; }

remote_state() { git --git-dir="$REMOTE" for-each-ref --format='%(refname) %(objectname)' 2>&1; }

do_push() { # git push arguments; sets PUSH_OUT, HOOK_EXIT, MOVED
    PUSH_OUT=$WORK/out/$NAME.push
    rm -f .git/hooks/hook-exit.txt
    local before after
    before=$(remote_state)
    git push "$@" > "$PUSH_OUT" 2>&1
    HOOK_EXIT=$(cat .git/hooks/hook-exit.txt 2> /dev/null || echo none)
    after=$(remote_state)
    if [ "$before" = "$after" ]; then MOVED=no; else MOVED=yes; fi
}

# ---------- running a whole review ----------
first_line_ok() { head -1 "$1" | grep -Eq '^CommitFence [^ ]+ \(.+\)$'; }
no_push_no_changed() { ! grep -Eiqw 'push|changed' "$1"; }
has_finding_lines() { grep -Eq '^[^ ]+\([0-9]+,[0-9]+\): (error|warning) ' "$1"; }
by_rule_line_in_place() { # one By rule line, and it is the line right before the last (the summary)
    [ "$(grep -c '^By rule:' "$1")" = 1 ] && [ "$(tail -n 2 "$1" | head -n 1 | cut -c1-8)" = "By rule:" ]
}
finding_lines() { grep -E ': (error|warning) ' "$1" | LC_ALL=C sort; }
same_findings() { cmp -s <(finding_lines "$1") <(finding_lines "$2") && [ -n "$(finding_lines "$1")" ]; }

run_review() { # tag review-arguments...: from the current folder; sets OUT and RC
    OUT=$WORK/out/$1.txt
    shift
    "$EXE" review "$@" > "$OUT" 2>&1
    RC=$?
    echo "$RC" > "$OUT.rc"
}

whole() { # case-id tag review-arguments...: a whole review and the checks that hold for every one
    local id=$1
    shift
    run_review "$@"
    check "$id" "first printed line is the version and folder" first_line_ok "$OUT"
    check "$id" "the words push and changed are not printed" no_push_no_changed "$OUT"
    if has_finding_lines "$OUT"; then
        check "$id" "findings are printed: the By rule line is there, once, right before the summary" by_rule_line_in_place "$OUT"
    else
        check "$id" "no finding is printed: there is no By rule line" hasnt "$OUT" "By rule:"
    fi
}

state_of() { # prints everything the developer owns
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

# ---------- the cases ----------
case_1() {
    new_repo c1
    write_settings
    write_editorconfig
    write_api clean
    write_legacy
    commit_all "initial with two old breaches in Legacy"
    git push -q --no-verify origin main 2> /dev/null
    local field_line if_line
    field_line=$(line_of src/Legacy/Legacy.cs '_calls = 1')
    if_line=$(line_of src/Legacy/Legacy.cs 'if(x > 0)')
    write_legacy extra
    commit_all "a clean method elsewhere in Legacy"
    do_push origin main
    check 1a "the real hook exits 0: the old breaches are let go" equals "$HOOK_EXIT" 0
    check 1a "the remote moved" equals "$MOVED" yes
    check 1a "no breach printed by the push" hasnt "$PUSH_OUT" "TEAM010"
    check 1a "no breach printed by the push (IDE0044)" hasnt "$PUSH_OUT" "IDE0044"

    whole 1b c1-legacy --all src/Legacy
    check 1b "exit 1" equals "$RC" 1
    check 1b "IDE0044 at the old field ($field_line)" has "$OUT" "src/Legacy/Legacy.cs($field_line,"
    check 1b "it is error IDE0044" has "$OUT" "error IDE0044"
    check 1b "TEAM010 at the old if ($if_line)" has "$OUT" "src/Legacy/Legacy.cs($if_line,"
    check 1b "it is error TEAM010" has "$OUT" "error TEAM010"
    check 1b "By rule line: IDE0044 1, TEAM010 1" has "$OUT" "By rule: IDE0044 1, TEAM010 1"
    check 1b "the first note names the target, the commit and the file count" has "$OUT" "the whole of src/Legacy at commit $(head10): $(git ls-files src/Legacy | wc -l | tr -d ' ') files."

    whole 1c c1-api --all src/Api
    check 1c "exit 0" equals "$RC" 0
    check 1c "no IDE0044" hasnt "$OUT" "IDE0044"
    check 1c "no TEAM010" hasnt "$OUT" "TEAM010"
    check 1c "Legacy.cs is not named" hasnt "$OUT" "Legacy.cs"
    check 1c "no By rule line" hasnt "$OUT" "By rule:"
    check 1c "the first note says the whole of src/Api" has "$OUT" "the whole of src/Api at commit $(head10): $(git ls-files src/Api | wc -l | tr -d ' ') files."
    check 1c "no sentence about files of no known language" hasnt "$OUT" "no known language"
}

case_2() {
    standard_repo c2
    local first api_if legacy_if legacy_field
    api_if=$(line_of src/Api/Service.cs 'if(x > 0)')
    legacy_if=$(line_of src/Legacy/Legacy.cs 'if(x > 0)')
    legacy_field=$(line_of src/Legacy/Legacy.cs '_calls = 1')
    first=$(git rev-parse HEAD)
    clean_class Api Extra > src/Api/Extra.cs
    commit_all "a clean file in Api"

    whole 2a c2-all --all
    check 2a "exit 1" equals "$RC" 1
    check 2a "By rule line, most frequent first" has "$OUT" "By rule: TEAM010 2, IDE0044 1"
    check 2a "TEAM010 in Api at its line" has "$OUT" "src/Api/Service.cs($api_if,"
    check 2a "TEAM010 in Legacy at its line" has "$OUT" "src/Legacy/Legacy.cs($legacy_if,"
    check 2a "IDE0044 in Legacy at its line" has "$OUT" "src/Legacy/Legacy.cs($legacy_field,"
    check 2a "three blocking findings, all files and both builds" has "$OUT" "3 of 3 files reviewed, 2 of 2 builds checked: 3 blocking"
    check 2a "the first note names the commit and the number of files" has "$OUT" "the whole of the repository at commit $(head10): $(git ls-files | wc -l | tr -d ' ') files."
    check 2a "files of no known language are 'files', not 'changed files'" has "$OUT" "4 files of no known language were not looked at."

    run_review c2-range --base "$first" --head HEAD
    check 2b "a range review exits 0 (clean change)" equals "$RC" 0
    check 2b "a range review prints no By rule line" hasnt "$OUT" "By rule:"
    check 2b "a range review lets the old breaches go" hasnt "$OUT" "TEAM010"
}

case_3() {
    standard_repo c3
    whole 3 c3-folder --all src/Legacy
    cp "$OUT" "$WORK/out/c3-folder.keep"
    whole 3 c3-project --all src/Legacy/Legacy.csproj
    check 3 "exit 1" equals "$RC" 1
    check 3 "the project gives the findings of its folder" same_findings "$WORK/out/c3-folder.keep" "$OUT"
    check 3 "the first note names the folder" has "$OUT" "the whole of src/Legacy at commit $(head10):"
    check 3 "the first note does not name the project file" hasnt "$OUT" "the whole of src/Legacy/Legacy.csproj"
}

case_4() {
    standard_repo c4
    whole 4 c4-top --all src/Legacy
    cp "$OUT" "$WORK/out/c4-top.keep"
    (cd src && whole 4a c4a --all Legacy)
    check 4a "exit 1" equals "$(cat "$WORK/out/c4a.txt.rc")" 1
    check 4a "same findings as from the top" same_findings "$WORK/out/c4-top.keep" "$WORK/out/c4a.txt"
    (cd src/Api && whole 4b c4b --all ../Legacy)
    check 4b "exit 1" equals "$(cat "$WORK/out/c4b.txt.rc")" 1
    check 4b "same findings as from the top" same_findings "$WORK/out/c4-top.keep" "$WORK/out/c4b.txt"
    (cd src/Api && whole 4c c4c --all)
    check 4c "exit 1" equals "$(cat "$WORK/out/c4c.txt.rc")" 1
    check 4c "no target means the whole repository, whatever the current folder" has "$WORK/out/c4c.txt" "the whole of the repository at commit $(head10):"
    check 4c "all three findings" has "$WORK/out/c4c.txt" "By rule: TEAM010 2, IDE0044 1"
}

case_5() {
    new_repo c5
    write_settings
    write_api clean
    commit_all "initial"
    mkdir -p loose
    breach_class Loose Free > loose/Loose.cs
    commit_all "a loose file, no project"
    local line
    line=$(line_of loose/Loose.cs 'if(x > 0)')
    whole 5a c5a --all loose
    check 5a "exit 1: blocked wins over incomplete" equals "$RC" 1
    check 5a "TEAM010 at the loose file" has "$OUT" "loose/Loose.cs($line,"
    check 5a "the file is said not to be built" has "$OUT" "loose/Loose.cs: not reviewed: not covered: no project compiles this file"
    run_review c5a-range --base HEAD~1 --head HEAD
    check 5a "a range review of the commit says the same of the file" has "$OUT" "loose/Loose.cs: not reviewed: not covered: no project compiles this file"
    check 5a "and reports the breach, as the whole review does" has "$OUT" "loose/Loose.cs($line,"

    new_repo c5b
    write_settings
    mkdir -p loose
    breach_class Loose Free > loose/Loose.cs
    commit_all "only a loose file"
    whole 5b c5b --all
    check 5b "exit 1" equals "$RC" 1
    check 5b "TEAM010 reported" has "$OUT" "error TEAM010"
    check 5b "no project is said plainly" has "$OUT" "No C# project was found in this tree, so nothing was compiled."
    check 5b "the file is said not to be built" has "$OUT" "loose/Loose.cs: not reviewed: not covered"
}

case_6() {
    new_repo c6
    write_settings "$STRUCTURE"
    mkdir -p src/Api src/Legacy
    write_csproj src/Api/Api.csproj
    write_csproj src/Legacy/Legacy.csproj Api
    printf 'namespace Api;\n\npublic class Orders\n{\n    public int Count()\n    {\n        return 3;\n    }\n}\n' > src/Api/Orders.cs
    printf 'namespace Legacy;\n\npublic class UsesApi\n{\n    public int Run()\n    {\n        var orders = new Api.Orders();\n        return orders.Count();\n    }\n}\n' > src/Legacy/UsesApi.cs
    commit_all "initial, with the old forbidden use"
    git push -q --no-verify origin main 2> /dev/null
    local line
    line=$(line_of src/Legacy/UsesApi.cs 'new Api.Orders')
    clean_class Api Extra > src/Api/Extra.cs
    commit_all "a clean file in Api"
    do_push origin main
    check 6a "the real hook exits 0" equals "$HOOK_EXIT" 0
    check 6a "the remote moved" equals "$MOVED" yes
    check 6a "the old forbidden use is not reported by the push" hasnt "$PUSH_OUT" "ARCH001"

    whole 6b c6b --all src/Legacy
    check 6b "exit 1" equals "$RC" 1
    check 6b "ARCH001 at the use ($line)" has "$OUT" "src/Legacy/UsesApi.cs($line,"
    check 6b "it is error ARCH001" has "$OUT" "error ARCH001"
    check 6b "it says who must not use whom" has "$OUT" "Layer direction: legacy must not use api."
    check 6b "By rule line" has "$OUT" "By rule: ARCH001 1"

    whole 6c c6c --all src/Api
    check 6c "exit 0" equals "$RC" 0
    check 6c "no ARCH001" hasnt "$OUT" "ARCH001"
    check 6c "no rule left unrun" hasnt "$OUT" "not run"
}

case_7() {
    new_repo c7
    write_settings
    mkdir -p src/Core src/App
    write_csproj src/Core/Core.csproj
    write_csproj src/App/App.csproj Core
    printf 'namespace Core;\n\npublic class Broken\n{\n    public int Value()\n    {\n        int x = "text";\n        return x;\n    }\n}\n' > src/Core/Broken.cs
    printf 'namespace App;\n\npublic class Use\n{\n    public int Run()\n    {\n        return new Core.Broken().Value();\n    }\n}\n' > src/App/Use.cs
    commit_all "Core does not compile, App uses it"
    local line
    line=$(line_of src/Core/Broken.cs 'int x')
    whole 7b c7b --all src/App
    check 7b "exit 2: incomplete" equals "$RC" 2
    check 7b "the reason names the project Core" has "$OUT" "Core (net11.0): not reviewed:"
    check 7b "App is not reviewed, because of its prerequisite" has "$OUT" "App (net11.0): not reviewed: its prerequisite Core (net11.0)"
    check 7b "no finding on a file of Core is reported: it is outside the target" hasnt "$OUT" "src/Core/Broken.cs("
    check 7b "the summary says the review is incomplete" has "$OUT" "so the review is incomplete."

    whole 7c c7c --all src/Core
    check 7c "exit 1" equals "$RC" 1
    check 7c "CS0029 at the line" has "$OUT" "src/Core/Broken.cs($line,"
    check 7c "it is error CS0029" has "$OUT" "error CS0029"

    new_repo c7a
    write_settings
    write_api clean
    write_legacy
    printf 'namespace Legacy;\n\npublic class Broken\n{\n    public int Value()\n    {\n        int x = "text";\n        return x;\n    }\n}\n' > src/Legacy/Broken.cs
    commit_all "Legacy has a compile error"
    line=$(line_of src/Legacy/Broken.cs 'int x')
    whole 7a c7a --all src/Legacy
    check 7a "exit 1" equals "$RC" 1
    check 7a "error CS0029 at its file and line" has "$OUT" "src/Legacy/Broken.cs($line,"
    check 7a "it is error CS0029" has "$OUT" "error CS0029"
}

NOTE_TEXT="The working folder holds changes that are not committed; they are not part of this review."

case_8() {
    standard_repo c8
    local line
    line=$(line_of src/Legacy/Legacy.cs 'if(x > 0)')
    whole 8a c8a --all src/Legacy
    check 8a "exit 1" equals "$RC" 1
    check 8a "no note when everything is committed" hasnt "$OUT" "not committed"

    echo "// edited" >> src/Api/Service.cs
    whole 8b c8b-legacy --all src/Legacy
    check 8b "exit 1" equals "$RC" 1
    check 8b "no note for a folder the edit is not under" hasnt "$OUT" "not committed"
    whole 8b c8b-api --all src/Api
    check 8b "the note for the folder it is under" has "$OUT" "$NOTE_TEXT"
    git checkout -q -- src/Api/Service.cs

    sed -i '/if(x > 0)/,/^        }/c\        if(x > 0)\n            return 1;' src/Legacy/Legacy.cs
    breach_class Legacy Fresh > src/Legacy/Fresh.cs
    whole 8c c8c --all src/Legacy
    check 8c "exit 1: the committed breach still counts" equals "$RC" 1
    check 8c "the note is printed once" count_of "$OUT" "$NOTE_TEXT" 1
    check 8c "the committed breach is at its committed line ($line)" has "$OUT" "src/Legacy/Legacy.cs($line,"
    check 8c "the new untracked file is not named" hasnt "$OUT" "Fresh.cs"
    git checkout -q -- src/Legacy/Legacy.cs
    rm -f src/Legacy/Fresh.cs

    echo "// staged" >> src/Api/Service.cs
    git add src/Api/Service.cs
    whole 8d c8d --all src/Api
    check 8d "exit 1: Api holds one TEAM010 breach" equals "$RC" 1
    check 8d "the note for a staged change" has "$OUT" "$NOTE_TEXT"
    check 8d "the findings are those of the commit (the one TEAM010 of Api)" has "$OUT" "By rule: TEAM010 1"
}

wrong_use() { # id description review-arguments...: exit 3 and some message
    local id=$1 text=$2
    shift 2
    run_review "c9-$id" "$@"
    check "$id" "$text: exit 3" equals "$RC" 3
    check "$id" "$text: a message is printed" test -s "$OUT"
    check "$id" "$text: nothing was reviewed" hasnt "$OUT" "files reviewed"
}

case_9() {
    standard_repo c9
    mkdir -p untracked-folder
    echo "x" > untracked-folder/a.txt
    wrong_use 9a "--all with --hook" --all --hook pre-push o u
    wrong_use 9b "--all with --staged" --all --staged
    wrong_use 9c "--all with --base" --all --base HEAD
    wrong_use 9d "--all with --head" --all --head HEAD
    wrong_use 9e "--all with --files" --all --files src/Api/Service.cs
    wrong_use 9f "two targets" --all src/Api src/Legacy
    wrong_use 9g "a target outside the repository" --all "$(native "$WORK/repos")"
    check 9g "the message says outside the repository" has "$WORK/out/c9-9g.txt" "outside the repository"
    wrong_use 9h "a target not in the commit" --all src/Nope
    check 9h "the message says it has no files in the last commit" has "$WORK/out/c9-9h.txt" "has no files in the last commit"
    check 9h "the message says no tracked folder has the name in another letter case" has "$WORK/out/c9-9h.txt" "no tracked folder has this name in another letter case either"
    wrong_use 9i "an untracked folder" --all untracked-folder
    check 9i "the message says it has no files in the last commit" has "$WORK/out/c9-9i.txt" "has no files in the last commit"
    wrong_use 9j "a source file" --all src/Api/Service.cs
    check 9j "the message says it is not a project file" has "$WORK/out/c9-9j.txt" "not a project file"

    new_repo c9k
    run_review c9k --all
    check 9k "no commit: exit 2" equals "$RC" 2
    check 9k "the reason says there is no commit" has "$OUT" "no commit"
    check 9k "first printed line is the version and folder" first_line_ok "$OUT"
}

case_10() {
    standard_repo c10
    echo "stashed" >> README.txt
    git stash -q
    echo "unstaged edit" >> README.txt
    echo "untracked" > notes.txt
    export GIT_OPTIONAL_LOCKS=0
    state_of > "$WORK/out/c10.before" 2>&1
    whole 10 c10-folder --all src/Legacy
    check 10 "the folder review exits 1" equals "$RC" 1
    whole 10 c10-all --all
    check 10 "the repository review exits 1" equals "$RC" 1
    state_of > "$WORK/out/c10.after" 2>&1
    check 10 "working folder, index, config, hooks, every ref, stash and status unchanged" cmp -s "$WORK/out/c10.before" "$WORK/out/c10.after"
}

case_11() {
    new_repo c11
    write_settings
    mkdir -p src/Shared
    write_csproj src/Shared/A.csproj "" "<BaseIntermediateOutputPath>../../obj/a/</BaseIntermediateOutputPath>"
    write_csproj src/Shared/B.csproj "" "<BaseIntermediateOutputPath>../../obj/b/</BaseIntermediateOutputPath>"
    breach_class Shared Code > src/Shared/Code.cs
    commit_all "two projects in one folder"
    whole 11a c11a --all src/Shared
    check 11a "exit 1" equals "$RC" 1
    check 11a "both projects are judged" has "$OUT" "2 of 2 builds checked"
    check 11a "the finding is printed once" count_of "$OUT" "src/Shared/Code.cs(" 1
    check 11a "build A is listed as reviewed" has "$OUT" "A (net11.0): reviewed"
    check 11a "build B is listed as reviewed" has "$OUT" "B (net11.0): reviewed"

    new_repo c11b
    write_settings
    mkdir -p src/Shared src/Api
    write_csproj src/Api/Api.csproj
    sed -i 's#</Project>#  <ItemGroup>\n    <Compile Include="..\\Shared\\Linked.cs" />\n  </ItemGroup>\n</Project>#' src/Api/Api.csproj
    clean_class Api Own > src/Api/Own.cs
    breach_class Api Linked > src/Shared/Linked.cs
    commit_all "Api compiles a file from outside its folder"
    local line
    line=$(line_of src/Shared/Linked.cs 'if(x > 0)')
    whole 11b c11b --all src/Api
    check 11b "exit 0" equals "$RC" 0
    check 11b "the linked file is outside the target: no finding on it" hasnt "$OUT" "Linked.cs"
    check 11b "one build" has "$OUT" "1 of 1 builds checked"
    whole 11c c11c --all src/Shared
    check 11c "exit 1" equals "$RC" 1
    check 11c "the linked file is judged at its line ($line)" has "$OUT" "src/Shared/Linked.cs($line,"
    check 11c "it is error TEAM010" has "$OUT" "error TEAM010"
}

case_12() {
    new_repo c12
    CLOCK_RULE=1 write_settings
    printf '*.cs -diff\n' > .gitattributes
    write_api breach
    printf 'namespace Api;\n\npublic class Clock\n{\n    public DateTime Read()\n    {\n        return DateTime.Now;\n    }\n}\n' > src/Api/Clock.cs
    commit_all "a repository whose .cs files git treats as binary"
    local if_line clock_line
    if_line=$(line_of src/Api/Service.cs 'if(x > 0)')
    clock_line=$(line_of src/Api/Clock.cs 'DateTime.Now')
    whole 12a c12 --all src/Api
    check 12a "exit 1" equals "$RC" 1
    check 12a "TEAM010 at its file and line ($if_line)" has "$OUT" "src/Api/Service.cs($if_line,"
    check 12a "it is error TEAM010" has "$OUT" "error TEAM010"
    check 12a "the By rule line holds TEAM010" bash -c "grep '^By rule:' '$OUT' | grep -q TEAM010"
    check 12b "TEAM012 (a rule that needs meaning) at its file and line ($clock_line)" has "$OUT" "src/Api/Clock.cs($clock_line,"
    check 12b "it is error TEAM012" has "$OUT" "error TEAM012"
    check 12b "the TEAM010 finding of 12a is printed too" has "$OUT" "error TEAM010"
}

case_13() {
    new_repo c13
    write_settings
    write_editorconfig
    write_api clean
    write_legacy
    printf '<Project>\n  <PropertyGroup>\n    <LangVersion>latest</LangVersion>\n  </PropertyGroup>\n</Project>\n' > src/Api/Directory.Build.props
    commit_all "a props file under the target, a breaching project beside it"
    whole 13 c13 --all src/Api
    check 13 "exit 0" equals "$RC" 0
    check 13 "Legacy is not named anywhere: it is not built" hasnt "$OUT" "Legacy"
    check 13 "no finding" hasnt "$OUT" "error "
    check 13 "one build" has "$OUT" "1 of 1 builds checked"
}

write_broken_project() { # a project file that is not well-formed XML
    mkdir -p src/Broken
    printf '<Project Sdk="Microsoft.NET.Sdk">\n  <PropertyGroup>\n    <TargetFramework>net11.0</TargetFramework>\n' > src/Broken/Broken.csproj
}

case_14() {
    new_repo c14
    write_settings
    write_api clean
    write_broken_project
    mkdir -p src/App
    write_csproj src/App/App.csproj Broken
    clean_class App Use > src/App/Use.cs
    commit_all "a healthy project, a project file that cannot be read, a project that references it"
    whole 14a c14a --all src/Api
    check 14a "exit 0" equals "$RC" 0
    check 14a "Broken is not named" hasnt "$OUT" "Broken"
    check 14a "one build" has "$OUT" "1 of 1 builds checked"
    whole 14b c14b --all src/Broken
    check 14b "exit 2" equals "$RC" 2
    check 14b "a line says Broken is not reviewed" bash -c "grep -q '^Broken: not reviewed:' '$OUT'"
    check 14b "the summary says the review is incomplete" has "$OUT" "so the review is incomplete."
    whole 14c c14c --all src/App
    check 14c "exit 2" equals "$RC" 2
    check 14c "a line says App is not reviewed" bash -c "grep -q '^App (net11.0): not reviewed:' '$OUT'"
    check 14c "the reason names Broken" bash -c "grep '^App (net11.0): not reviewed:' '$OUT' | grep -q Broken"
    check 14c "the summary says the review is incomplete" has "$OUT" "so the review is incomplete."
}

write_external_settings() { # file rule-id [broken]: a settings file outside the repository
    {
        echo '{'
        echo '    "rules": ['
        echo '        {'
        echo "            \"id\": \"$2\","
        if [ -n "${3:-}" ]; then
            echo '            "language": "csharp"'
            echo '            "for": "if-statement",'
        else
            echo '            "language": "csharp",'
            echo '            "for": "if-statement",'
        fi
        echo '            "severity": "block",'
        echo '            "title": "No braces around a single-line if",'
        echo '            "where": { "statementsInBody": 1, "bodyFitsOnOneLine": true },'
        echo '            "require": { "braces": false }'
        echo '        }'
        echo '    ]'
        echo '}'
    } > "$1"
}

case_15() {
    standard_repo c15
    local if_line ext broken
    if_line=$(line_of src/Api/Service.cs 'if(x > 0)')
    ext=$(native "$WORK/ext-settings.json")
    broken=$(native "$WORK/broken-settings.json")
    write_external_settings "$ext" EXT010
    write_external_settings "$broken" EXT010 broken
    whole 15a c15a --all --settings "$ext" src/Api
    check 15a "exit 1" equals "$RC" 1
    check 15a "the file given was used: EXT010 at the if ($if_line)" has "$OUT" "src/Api/Service.cs($if_line,"
    check 15a "it is error EXT010" has "$OUT" "error EXT010"
    check 15a "the tree's own file was not used" hasnt "$OUT" "TEAM010"
    whole 15b c15b --all src/Api --settings "$ext"
    check 15b "exit 1" equals "$RC" 1
    check 15b "error EXT010 at the if" has "$OUT" "error EXT010"
    check 15b "the tree's own file was not used" hasnt "$OUT" "TEAM010"
    whole 15c c15c --settings "$ext" --all src/Api
    check 15c "exit 1" equals "$RC" 1
    check 15c "error EXT010 at the if" has "$OUT" "error EXT010"
    check 15c "the tree's own file was not used" hasnt "$OUT" "TEAM010"
    run_review c15d --all --settings "$broken" src/Api
    check 15d "exit 3" equals "$RC" 3
    check 15d "the message names the line and the column" bash -c "grep -Eq '\\([0-9]+,[0-9]+\\)' '$OUT'"
    check 15d "the message names the settings file given" has "$OUT" "broken-settings.json"
    check 15d "the message says it is not valid JSON" has "$OUT" "not valid JSON"
    check 15d "nothing was reviewed" hasnt "$OUT" "files reviewed"
}

message_of_all() { # id option-text review-arguments...: exit 3 and a message that names the option
    local id=$1 option=$2
    shift 2
    run_review "c16-$id" "$@"
    check "$id" "exit 3" equals "$RC" 3
    check "$id" "the message names $option" has "$OUT" "$option"
}

case_16() {
    standard_repo c16
    message_of_all 16a "never run by a hook" --all --hook pre-push o u
    message_of_all 16b "--all stands alone" --all --staged
    check 16b "the message names --staged" has "$OUT" "--staged"
    message_of_all 16c "--all stands alone" --all --base HEAD
    check 16c "the message names --base" has "$OUT" "--base"
    message_of_all 16d "--all stands alone" --all --head HEAD
    check 16d "the message names --head" has "$OUT" "--head"
    check 16d "it does not say that --head needs --base" hasnt "$OUT" "needs --base"
    message_of_all 16e "--all stands alone" --all --files src/Api/Service.cs
    check 16e "the message names --files" has "$OUT" "--files"
    run_review c16f --base --all
    check 16f "exit 3" equals "$RC" 3
    check 16f "the message says --base needs one value" has "$OUT" "--base needs one value"
    run_review c16g --all --settings
    check 16g "exit 3" equals "$RC" 3
    check 16g "the message says --settings needs one value" has "$OUT" "--settings needs one value"
}

case_17() {
    standard_repo c17
    whole 17-top c17-top --all src/Legacy
    cp "$OUT" "$WORK/out/c17-top.keep"
    whole 17a c17a --all src/legacy
    check 17a "exit 1" equals "$RC" 1
    check 17a "the same findings as the tracked spelling" same_findings "$WORK/out/c17-top.keep" "$OUT"
    check 17a "the first note names the tracked spelling" has "$OUT" "the whole of src/Legacy at commit $(head10):"
    whole 17b c17b --all src/legacy/legacy.csproj
    check 17b "exit 1" equals "$RC" 1
    check 17b "the same findings as the tracked spelling" same_findings "$WORK/out/c17-top.keep" "$OUT"
    check 17b "the first note names the tracked folder" has "$OUT" "the whole of src/Legacy at commit $(head10):"
    check 17b "the By rule line is the one of the tracked spelling" has "$OUT" "By rule: IDE0044 1, TEAM010 1"
}

case_18() {
    new_repo c18
    write_settings
    mkdir -p src/Shared src/Api
    write_csproj src/Api/Api.csproj
    sed -i 's#</Project>#  <ItemGroup>\n    <Compile Include="..\\Shared\\Linked.cs" />\n  </ItemGroup>\n</Project>#' src/Api/Api.csproj
    clean_class Api Own > src/Api/Own.cs
    printf 'namespace Api;\n\npublic class Linked\n{\n    public int Value()\n    {\n        int x = "text";\n        return x;\n    }\n}\n' > src/Shared/Linked.cs
    commit_all "Api compiles a file from outside its folder, and that file does not compile"
    local line
    line=$(line_of src/Shared/Linked.cs 'int x')
    whole 18 c18 --all src/Api
    check 18 "exit 1" equals "$RC" 1
    check 18 "the compile error of the linked file is printed at its line ($line)" has "$OUT" "src/Shared/Linked.cs($line,"
    check 18 "it is error CS0029" has "$OUT" "error CS0029"
}

case_19() {
    standard_repo c19
    local if_line ext
    if_line=$(line_of src/Legacy/Legacy.cs 'if(x > 0)')
    ext=$(native "$WORK/ext-settings.json")
    write_external_settings "$ext" EXT010
    whole 19-top c19-top --all src/Legacy
    cp "$OUT" "$WORK/out/c19-top.keep"
    whole 19a c19a src/Legacy --all
    check 19a "exit 1" equals "$RC" 1
    check 19a "the same findings as the target after --all" same_findings "$WORK/out/c19-top.keep" "$OUT"
    check 19a "the first note names the folder" has "$OUT" "the whole of src/Legacy at commit $(head10):"
    whole 19b c19b --settings "$ext" src/Legacy --all
    check 19b "exit 1" equals "$RC" 1
    check 19b "the file given was used: EXT010 at the if ($if_line)" has "$OUT" "src/Legacy/Legacy.cs($if_line,"
    check 19b "it is error EXT010" has "$OUT" "error EXT010"
    check 19b "the tree's own file was not used" hasnt "$OUT" "error TEAM010"
    run_review c19c --all --all
    check 19c "exit 3" equals "$RC" 3
    check 19c "the message says --all was given twice" has "$OUT" "--all was given twice."
    run_review c19d --staged extra
    check 19d "exit 3" equals "$RC" 3
    check 19d "the message says extra is not understood" has "$OUT" "'extra' is not understood here."
    run_review c19e --files a.cs --all
    check 19e "exit 3" equals "$RC" 3
    check 19e "the message says --all stands alone" has "$OUT" "--all stands alone"
    check 19e "the message names --files" has "$OUT" "--files"
    run_review c19f --all --settings "$ext" --settings "$ext" src/Api
    check 19f "exit 3" equals "$RC" 3
    check 19f "the message says --settings was given twice" has "$OUT" "--settings was given twice"
    run_review c19g --base HEAD --base HEAD
    check 19g "exit 3" equals "$RC" 3
    check 19g "the message says --base was given twice" has "$OUT" "--base was given twice"
}

case_20() {
    standard_repo c20
    printf '{\n    "rules": [\n        { "id": "TEAM010" "language": "csharp" }\n    ]\n}\n' > commitfence.json
    commit_all "the settings file of the tree is broken"
    run_review c20 --all src/Api
    check 20 "exit 3" equals "$RC" 3
    check 20 "the message names the file of the tree, with a line and a column" bash -c "grep -Eq 'commitfence.json \\(in the tree under review\\)\\([0-9]+,[0-9]+\\)' '$OUT'"
    check 20 "the message says it is not valid JSON" has "$OUT" "not valid JSON"
    check 20 "nothing was reviewed" hasnt "$OUT" "files reviewed"
}

case_21() {
    new_repo c21
    write_settings
    local blob_a blob_b
    blob_a=$(printf 'namespace Twin;\n\npublic class A\n{\n}\n' | git hash-object -w --stdin)
    blob_b=$(printf 'namespace Twin;\n\npublic class B\n{\n}\n' | git hash-object -w --stdin)
    git -c core.ignorecase=false update-index --add --cacheinfo "100644,$blob_a,src/Legacy/A.cs"
    git -c core.ignorecase=false update-index --add --cacheinfo "100644,$blob_b,src/legacy/B.cs"
    git commit -q -m "two tracked folders that differ only by case"
    check 21 "precondition: the commit tracks both spellings" equals "$(git ls-tree -r --name-only HEAD | grep -c 'A.cs\|B.cs')" 2
    run_review c21 --all SRC/LEGACY
    check 21 "exit 3" equals "$RC" 3
    check 21 "the message names the first spelling" has "$OUT" "'src/Legacy'"
    check 21 "the message names the second spelling" has "$OUT" "'src/legacy'"
    check 21 "the message asks for one of them exactly" has "$OUT" "name one of them exactly"
    check 21 "the message does not say there are no files" hasnt "$OUT" "has no files in the last commit"
}

write_sub() { # namespace: Sub.cs with its own old breaches (a field never reassigned and a braced single-line if)
    mkdir -p src/Legacy/Sub
    {
        printf 'namespace %s;\n\npublic class Inner\n{\n' "$1"
        printf '    private static int _n = 1;\n\n    public int N()\n    {\n        return _n;\n    }\n\n'
        sign_breach
        echo "}"
    } > src/Legacy/Sub/Sub.cs
}

case_22() {
    standard_repo c22
    write_sub Legacy.Sub
    commit_all "Sub, a folder inside the Legacy project, has breaches of its own"
    local field_line if_line legacy_field legacy_if
    field_line=$(line_of src/Legacy/Sub/Sub.cs '_n = 1')
    if_line=$(line_of src/Legacy/Sub/Sub.cs 'if(x > 0)')
    legacy_field=$(line_of src/Legacy/Legacy.cs '_calls = 1')
    legacy_if=$(line_of src/Legacy/Legacy.cs 'if(x > 0)')
    whole 22a c22a --all src/Legacy/Sub
    check 22a "exit 1" equals "$RC" 1
    check 22a "IDE0044 at the field of Sub.cs ($field_line)" has "$OUT" "src/Legacy/Sub/Sub.cs($field_line,"
    check 22a "TEAM010 at the if of Sub.cs ($if_line)" has "$OUT" "src/Legacy/Sub/Sub.cs($if_line,"
    check 22a "it is error IDE0044" has "$OUT" "error IDE0044"
    check 22a "it is error TEAM010" has "$OUT" "error TEAM010"
    check 22a "Legacy.cs is not named" hasnt "$OUT" "Legacy.cs"
    check 22a "the project is built" has "$OUT" "Legacy (net11.0): reviewed"
    check 22a "By rule counts only the findings of Sub" has "$OUT" "By rule: IDE0044 1, TEAM010 1"
    check 22a "the first note names the folder and one file" has "$OUT" "the whole of src/Legacy/Sub at commit $(head10): 1 file."
    whole 22b c22b --all src/Legacy
    check 22b "exit 1" equals "$RC" 1
    check 22b "IDE0044 at the field of Legacy.cs ($legacy_field)" has "$OUT" "src/Legacy/Legacy.cs($legacy_field,"
    check 22b "TEAM010 at the if of Legacy.cs ($legacy_if)" has "$OUT" "src/Legacy/Legacy.cs($legacy_if,"
    check 22b "IDE0044 at the field of Sub.cs ($field_line)" has "$OUT" "src/Legacy/Sub/Sub.cs($field_line,"
    check 22b "TEAM010 at the if of Sub.cs ($if_line)" has "$OUT" "src/Legacy/Sub/Sub.cs($if_line,"
    check 22b "By rule counts all four" has "$OUT" "By rule: IDE0044 2, TEAM010 2"
}

case_23() {
    new_repo c23
    write_settings "$STRUCTURE"
    mkdir -p src/Api src/Legacy/Sub
    write_csproj src/Api/Api.csproj
    write_csproj src/Legacy/Legacy.csproj Api
    printf 'namespace Api;\n\npublic class Orders\n{\n    public int Count()\n    {\n        return 3;\n    }\n}\n' > src/Api/Orders.cs
    printf 'namespace Legacy;\n\npublic class UsesApi\n{\n    public int Run()\n    {\n        var orders = new Api.Orders();\n        return orders.Count();\n    }\n}\n' > src/Legacy/Legacy.cs
    printf 'namespace Legacy.Sub;\n\npublic class InnerUsesApi\n{\n    public int Run()\n    {\n        var orders = new Api.Orders();\n        return orders.Count();\n    }\n}\n' > src/Legacy/Sub/Sub.cs
    commit_all "the forbidden use once in Legacy.cs and once in Sub"
    local sub_line legacy_line
    sub_line=$(line_of src/Legacy/Sub/Sub.cs 'new Api.Orders')
    legacy_line=$(line_of src/Legacy/Legacy.cs 'new Api.Orders')
    whole 23a c23a --all src/Legacy/Sub
    check 23a "exit 1" equals "$RC" 1
    check 23a "ARCH001 at the use in Sub.cs ($sub_line)" has "$OUT" "src/Legacy/Sub/Sub.cs($sub_line,"
    check 23a "it is error ARCH001" has "$OUT" "error ARCH001"
    check 23a "Legacy.cs is not named" hasnt "$OUT" "Legacy.cs"
    check 23a "By rule counts one" has "$OUT" "By rule: ARCH001 1"
    whole 23b c23b --all src/Legacy
    check 23b "exit 1" equals "$RC" 1
    check 23b "ARCH001 at the use in Sub.cs ($sub_line)" has "$OUT" "src/Legacy/Sub/Sub.cs($sub_line,"
    check 23b "ARCH001 at the use in Legacy.cs ($legacy_line)" has "$OUT" "src/Legacy/Legacy.cs($legacy_line,"
    check 23b "By rule counts two" has "$OUT" "By rule: ARCH001 2"
}

case_24() {
    new_repo c24
    write_settings
    printf 'root = true\n\n[*.cs]\ndotnet_diagnostic.CA1016.severity = error\n' > .editorconfig
    mkdir -p src/Legacy/Sub
    write_csproj src/Legacy/Legacy.csproj "" "<EnableNETAnalyzers>true</EnableNETAnalyzers><GenerateAssemblyInfo>false</GenerateAssemblyInfo>"
    clean_class Legacy Old > src/Legacy/Legacy.cs
    clean_class Legacy.Sub Inner > src/Legacy/Sub/Sub.cs
    commit_all "a project whose build reports CA1016, a finding with no file"
    whole 24b c24b --all src/Legacy/Sub
    check 24b "exit 1" equals "$RC" 1
    check 24b "the finding that names no file is printed" has "$OUT" "(no file): error CA1016"
    check 24b "Legacy.cs is not named" hasnt "$OUT" "Legacy.cs"
}

# ---------- run ----------
for c in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24; do
    if [ -z "$SELECTED" ] || [[ " $SELECTED " == *" $c "* ]]; then
        ( case_$c )
    fi
done > "$WORK/out/results.txt" 2>&1
cat "$WORK/out/results.txt"

PASSES=$(grep -c '^PASS' "$WORK/out/results.txt")
FAILS=$(grep -c '^FAIL' "$WORK/out/results.txt")
SKIPS=$(grep -c '^SKIP' "$WORK/out/results.txt")
finish_script
