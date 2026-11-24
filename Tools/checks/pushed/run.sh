#!/bin/bash
# Pushed code: what a push, the staged content or a range changes, read from the commits git names
# Builds every test repository from nothing and prints PASS or FAIL per check.
# Expected results are in expected.md.
# Usage: Tools/checks/pushed/run.sh <path of the commitfence program> [parent folder for the scratch folder] [case ids...]
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
ROOT=$(mktemp -d "$PARENT/commitfence-pushed.XXXXXX") || stop_now "cannot create a scratch folder under $PARENT"
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
RESULTS=""

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

# ---------- the rules and the sources ----------
write_rules() {
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

m_add() { cat <<'EOF'
    public int Add(int a, int b)
    {
        return a + b;
    }
EOF
}
m_twice() { cat <<'EOF'
    public int Twice(int a)
    {
        return a * 2;
    }
EOF
}
m_sign_bad() { cat <<'EOF'
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
m_sign_bad_edited() { cat <<'EOF'
    public int Sign(int x)
    {
        if(x > 0)
        {
            return 2;
        }

        return 0;
    }
EOF
}
m_sign_ok() { cat <<'EOF'
    public int Sign(int x)
    {
        if(x > 0)
            return 1;

        return 0;
    }
EOF
}
m_syntax_error() { cat <<'EOF'
    public int Broken( {
EOF
}

write_class() { # file class methods...
    local file=$1 name=$2
    shift 2
    mkdir -p "$(dirname "$file")"
    {
        echo "namespace Demo;"
        echo
        echo "public class $name"
        echo "{"
        local first=1
        for method in "$@"; do
            [ $first = 0 ] && echo
            first=0
            $method
        done
        echo "}"
    } > "$file"
}

line_of_if() { grep -n 'if(x > 0)' "$1" | head -1 | cut -d: -f1; }

# ---------- repositories ----------
commit_src() { git add src && git commit -q -m "$1"; }

write_project() { # a minimal library project and the ignore file, so that the .cs files are compiled
    cat > Demo.csproj <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <EnableNETAnalyzers>false</EnableNETAnalyzers>
  </PropertyGroup>
</Project>
XML
    printf 'bin/\nobj/\n' > .gitignore
}

new_repo() { # name  (empty repository with an empty bare remote and the team's hook)
    NAME=$1
    REPO=$WORK/repos/$NAME
    REMOTE=$WORK/remotes/$NAME.git
    git init -q --bare "$REMOTE"
    git init -q "$REPO"
    cd "$REPO" || exit 1
    git remote add origin "$(cygpath -m "$REMOTE")"
    write_rules
    write_project
    echo "readme" > README.txt
    printf '#!/bin/sh\n"$(git config --get commitfence.path)" review --hook pre-push "$@" || exit $?\n' > .git/hooks/pre-push.team
    printf '#!/bin/sh\n"$(dirname "$0")/pre-push.team" "$@"\nrc=$?\necho $rc > "$(dirname "$0")/hook-exit.txt"\nexit $rc\n' > .git/hooks/pre-push
    chmod +x .git/hooks/pre-push .git/hooks/pre-push.team
}

base_repo() { # name classfile-methods...  (main has src/Core.cs, pushed without the hook)
    new_repo "$1"
    shift
    write_class src/Core.cs Core m_add
    git add -A && git commit -q -m "initial"
    git push -q --no-verify origin main 2> /dev/null
}

dev_prep() { # an unstaged edit, an untracked file and a stash entry
    echo "stashed" >> README.txt
    git stash -q
    echo "unstaged edit" >> README.txt
    echo "untracked" > notes.txt
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

snap_before() { export GIT_OPTIONAL_LOCKS=0; state_of > "$WORK/out/$NAME.before" 2>&1; }
snap_after() { export GIT_OPTIONAL_LOCKS=0; state_of > "$WORK/out/$NAME.after" 2>&1; }

remote_state() { git --git-dir="$REMOTE" for-each-ref --format='%(refname) %(objectname)' 2>&1; }

do_push() { # git push arguments; sets OUT, PUSH_RC, HOOK_EXIT, MOVED
    OUT=$WORK/out/$NAME.push
    rm -f .git/hooks/hook-exit.txt
    local before after
    before=$(remote_state)
    git push "$@" > "$OUT" 2>&1
    PUSH_RC=$?
    HOOK_EXIT=$(cat .git/hooks/hook-exit.txt 2> /dev/null || echo none)
    after=$(remote_state)
    if [ "$before" = "$after" ]; then MOVED=no; else MOVED=yes; fi
}

first_line_ok() { head -1 "$1" | grep -Eq '^CommitFence [^ ]+ \(.+\)$'; }

finish() { # case-id: the developer's state must be unchanged (check 12)
    snap_after
    check "12.$1" "working folder, index, config, hooks, every ref, stash and status unchanged" cmp -s "$WORK/out/$NAME.before" "$WORK/out/$NAME.after"
}

expect_push() { # case-id exit-code moved(yes|no)
    check "$1" "the hook exited with $2" equals "$HOOK_EXIT" "$2"
    check "$1" "the remote moved: $3" equals "$MOVED" "$3"
    check "$1" "first printed line is the version and folder" first_line_ok "$OUT"
}

# ---------- the cases ----------
case_1() {
    base_repo c1
    write_class src/Core.cs Core m_add m_twice
    commit_src "clean change"
    dev_prep; snap_before
    do_push origin main
    expect_push 1 0 yes
    check 1 "no finding printed" hasnt "$OUT" "TEAM010"
    check 1 "coverage: 1 of 1 files reviewed, 0 blocking" has "$OUT" "1 of 1 files reviewed, 1 of 1 builds checked: 0 blocking"
    finish 1
}

case_2() {
    base_repo c2
    write_class src/Core.cs Core m_add m_sign_bad
    local line
    line=$(line_of_if src/Core.cs)
    commit_src "breaking change"
    write_class src/Core.cs Core m_add m_sign_ok
    dev_prep; snap_before
    do_push origin main
    expect_push 2 1 no
    check 2 "finding names the file and the line ($line)" has "$OUT" "src/Core.cs($line,"
    check 2 "finding is TEAM010" has "$OUT" "error TEAM010"
    check 2 "coverage: 1 of 1 files reviewed, 1 blocking" has "$OUT" "1 of 1 files reviewed, 1 of 1 builds checked: 1 blocking"
    check 2 "the push was judged from the commit, not from the fixed working folder" equals "$PUSH_RC" 1
    finish 2
}

case_3() {
    new_repo c3
    write_class src/Old.cs Old m_add m_sign_bad
    git add -A && git commit -q -m "initial with an old breach"
    git push -q --no-verify origin main 2> /dev/null
    write_class src/Old.cs Old m_add m_sign_bad m_twice
    commit_src "clean change elsewhere in the same file"
    dev_prep; snap_before
    do_push origin main
    expect_push 3 0 yes
    check 3 "the old breach is not reported" hasnt "$OUT" "TEAM010"
    check 3 "coverage: 1 of 1 files reviewed, 0 blocking" has "$OUT" "1 of 1 files reviewed, 1 of 1 builds checked: 0 blocking"
    finish 3
}

case_3b() {
    new_repo c3b
    write_class src/Old.cs Old m_add m_sign_bad
    git add -A && git commit -q -m "initial with an old breach"
    git push -q --no-verify origin main 2> /dev/null
    write_class src/Old.cs Old m_add m_sign_bad_edited
    local line
    line=$(line_of_if src/Old.cs)
    commit_src "edit inside the old breach"
    dev_prep; snap_before
    do_push origin main
    expect_push 3b 1 no
    check 3b "the touched breach is reported at its line ($line)" has "$OUT" "src/Old.cs($line,"
    finish 3b
}

case_4() {
    base_repo c4
    git checkout -q -b clean
    write_class src/Good.cs Good m_add
    commit_src "good"
    git checkout -q main
    git checkout -q -b bad
    write_class src/Bad.cs Bad m_sign_bad
    commit_src "bad"
    git checkout -q main
    dev_prep; snap_before
    do_push origin clean bad
    expect_push 4 1 no
    check 4 "finding is from the breaking branch" has "$OUT" "src/Bad.cs("
    check 4 "no finding from the clean branch" hasnt "$OUT" "src/Good.cs("
    check 4 "coverage: 2 of 2 files reviewed, 1 blocking" has "$OUT" "2 of 2 files reviewed, 1 of 1 builds checked: 1 blocking"
    finish 4
}

new_branch_base() { # name: main on the remote holds an old breach in src/Old.cs
    new_repo "$1"
    write_class src/Old.cs Old m_add m_sign_bad
    git add -A && git commit -q -m "initial with an old breach"
    git push -q --no-verify origin main 2> /dev/null
    git checkout -q -b feature
}

case_5() {
    new_branch_base c5
    write_class src/New.cs New m_add
    commit_src "new clean file"
    git checkout -q main
    dev_prep; snap_before
    do_push origin feature
    expect_push 5 0 yes
    check 5 "only its own commits: 1 of 1 files reviewed" has "$OUT" "1 of 1 files reviewed, 1 of 1 builds checked: 0 blocking"
    check 5 "the old breach of main is not reported" hasnt "$OUT" "src/Old.cs"
    finish 5
}

case_5b() {
    new_branch_base c5b
    write_class src/New.cs New m_sign_bad
    commit_src "new breaking file"
    git checkout -q main
    dev_prep; snap_before
    do_push origin feature
    expect_push 5b 1 no
    check 5b "finding on the new file" has "$OUT" "src/New.cs("
    check 5b "no finding on the old file" hasnt "$OUT" "src/Old.cs("
    check 5b "coverage: 1 of 1 files reviewed, 1 blocking" has "$OUT" "1 of 1 files reviewed, 1 of 1 builds checked: 1 blocking"
    finish 5b
}

case_5c() {
    base_repo c5c
    git checkout -q -b dev
    write_class src/Dev.cs Dev m_sign_bad
    commit_src "dev has a breach"
    git push -q --no-verify origin dev 2> /dev/null
    git checkout -q -b topic
    write_class src/Top.cs Top m_add
    commit_src "topic clean"
    git checkout -q main
    dev_prep; snap_before
    do_push origin topic
    expect_push 5c 1 no
    check 5c "the oldest common point is used: the breach of dev is seen" has "$OUT" "src/Dev.cs("
    check 5c "coverage: 2 of 2 files reviewed, 1 blocking" has "$OUT" "2 of 2 files reviewed, 1 of 1 builds checked: 1 blocking"
    finish 5c
}

case_6() {
    new_repo c6
    write_class src/Core.cs Core m_add m_sign_bad
    git add -A && git commit -q -m "first commit with the breach"
    write_class src/Extra.cs Extra m_add
    commit_src "second commit"
    dev_prep; snap_before
    do_push origin main
    expect_push 6 1 no
    check 6 "the breach in the first commit is found" has "$OUT" "src/Core.cs("
    check 6 "coverage: 2 of 2 files reviewed, 1 blocking" has "$OUT" "2 of 2 files reviewed, 1 of 1 builds checked: 1 blocking"
    finish 6
}

case_6b() {
    new_repo c6b
    write_class src/Core.cs Core m_add
    git add -A && git commit -q -m "first commit"
    write_class src/Extra.cs Extra m_add
    commit_src "second commit"
    dev_prep; snap_before
    do_push origin main
    expect_push 6b 0 yes
    check 6b "coverage: 2 of 2 files reviewed, 0 blocking" has "$OUT" "2 of 2 files reviewed, 1 of 1 builds checked: 0 blocking"
    finish 6b
}

case_7a() {
    base_repo c7a
    write_class src/Core.cs Core m_add m_twice
    git add src && git commit -q --amend --no-edit
    dev_prep; snap_before
    do_push --force origin main
    expect_push 7a 0 yes
    check 7a "remote holds the amended commit" equals "$(git --git-dir="$REMOTE" rev-parse main)" "$(git rev-parse main)"
    check 7a "coverage: 1 of 1 files reviewed" has "$OUT" "1 of 1 files reviewed, 1 of 1 builds checked: 0 blocking"
    check 7a "no note about a remote commit that is missing: it is in the clone" hasnt "$OUT" "is not in this clone"
    finish 7a
}

case_7b() {
    base_repo c7b
    write_class src/Core.cs Core m_add m_sign_bad
    git add src && git commit -q --amend --no-edit
    dev_prep; snap_before
    do_push --force origin main
    expect_push 7b 1 no
    check 7b "finding printed" has "$OUT" "error TEAM010"
    finish 7b
}

case_7f() {
    base_repo c7f
    local other=$WORK/repos/c7f-other remote_tip
    git clone -q "$(cygpath -m "$REMOTE")" "$other" 2> /dev/null
    (cd "$other" && echo "work of somebody else" > other.txt && git add other.txt && git commit -q -m "somebody else pushes" && git push -q --no-verify origin main 2> /dev/null)
    remote_tip=$(git --git-dir="$REMOTE" rev-parse main)
    check 7f "precondition: the remote's tip is not in this clone" bash -c "! git cat-file -e '$remote_tip^{commit}' 2> /dev/null"
    write_class src/Core.cs Core m_add m_twice
    commit_src "a different clean commit"
    dev_prep; snap_before
    do_push --force origin main
    expect_push 7f 0 yes
    check 7f "remote holds this clone's commit" equals "$(git --git-dir="$REMOTE" rev-parse main)" "$(git rev-parse main)"
    check 7f "no finding printed" hasnt "$OUT" "TEAM010"
    check 7f "coverage: 1 of 1 files reviewed" has "$OUT" "1 of 1 files reviewed, 1 of 1 builds checked: 0 blocking"
    check 7f "the note names the remote's commit and says the ref was reviewed as a new branch" has "$OUT" "The remote's commit ${remote_tip:0:10} is not in this clone, so refs/heads/main was reviewed as a new branch: more is reviewed, never less."
    finish 7f
}

case_7c() {
    base_repo c7c
    git branch feature
    git push -q --no-verify origin feature 2> /dev/null
    dev_prep; snap_before
    do_push origin --delete feature
    expect_push 7c 0 yes
    check 7c "the branch is gone from the remote" equals "$(git --git-dir="$REMOTE" for-each-ref refs/heads/feature)" ""
    check 7c "summary says nothing to review" has "$OUT" "deleted on the remote, so there is nothing to review"
    finish 7c
}

case_7d() {
    base_repo c7d
    git tag v1
    dev_prep; snap_before
    do_push origin v1
    expect_push 7d 0 yes
    check 7d "the tag is on the remote" equals "$(git --git-dir="$REMOTE" for-each-ref --format='%(refname)' refs/tags/v1)" "refs/tags/v1"
    check 7d "summary says nothing to review" has "$OUT" "holds no commit that the remote is not known to have"
    finish 7d
}

case_7e() {
    base_repo c7e
    write_class src/Core.cs Core m_add m_sign_bad
    commit_src "breaking, unpushed"
    git tag -a v2 -m "release two"
    dev_prep; snap_before
    do_push origin v2
    expect_push 7e 1 no
    check 7e "finding printed" has "$OUT" "src/Core.cs("
    finish 7e
}

hook_passed_or_absent() { [ "$HOOK_EXIT" = 0 ] || [ "$HOOK_EXIT" = none ]; }
nothing_said_when_run() { [ "$HOOK_EXIT" = none ] || grep -Fq "nothing to review" "$OUT"; }

case_8a() {
    base_repo c8a
    dev_prep; snap_before
    do_push origin main
    check 8a "push succeeded" equals "$PUSH_RC" 0
    check 8a "the hook exited with 0 (or was not run)" hook_passed_or_absent
    check 8a "the remote did not move" equals "$MOVED" no
    check 8a "nothing to review is said when the hook ran" nothing_said_when_run
    finish 8a
}

case_8b() {
    base_repo c8b
    write_class src/Core.cs Core m_add m_twice
    commit_src "unpushed commit"
    dev_prep; snap_before
    OUT=$WORK/out/$NAME.hook
    rm -f .git/hooks/hook-exit.txt
    local before after
    before=$(remote_state)
    .git/hooks/pre-push origin "$(cygpath -m "$REMOTE")" < /dev/null > "$OUT" 2>&1
    local rc=$?
    after=$(remote_state)
    check 8b "the hook exited with 2" equals "$rc" 2
    check 8b "the reason is printed" has "$OUT" "gave the hook no ref lines"
    check 8b "coverage says the scope is incomplete" has "$OUT" "review scope: incomplete"
    check 8b "first printed line is the version and folder" first_line_ok "$OUT"
    check 8b "the remote did not move" equals "$before" "$after"
    finish 8b
}

review_staged() { # sets OUT and RC
    OUT=$WORK/out/$NAME.staged
    "$EXE" review --staged > "$OUT" 2>&1
    RC=$?
}

case_9a() {
    base_repo c9a
    dev_prep
    write_class src/Core.cs Core m_add m_sign_bad
    local line
    line=$(line_of_if src/Core.cs)
    git add src/Core.cs
    write_class src/Core.cs Core m_add m_sign_ok
    snap_before
    review_staged
    check 9a "exit code 1: what is staged is judged" equals "$RC" 1
    check 9a "finding at the staged line ($line)" has "$OUT" "src/Core.cs($line,"
    check 9a "coverage: 1 of 1 files reviewed, 1 blocking" has "$OUT" "1 of 1 files reviewed, 1 of 1 builds checked: 1 blocking"
    check 9a "first printed line is the version and folder" first_line_ok "$OUT"
    finish 9a
}

case_9b() {
    base_repo c9b
    dev_prep
    write_class src/Core.cs Core m_add m_twice
    git add src/Core.cs
    write_class src/Core.cs Core m_add m_sign_bad
    snap_before
    review_staged
    check 9b "exit code 0: the breach in the working folder is not judged" equals "$RC" 0
    check 9b "no finding" hasnt "$OUT" "TEAM010"
    check 9b "coverage: 1 of 1 files reviewed, 0 blocking" has "$OUT" "1 of 1 files reviewed, 1 of 1 builds checked: 0 blocking"
    finish 9b
}

case_10() {
    base_repo c10
    write_class src/Broken.cs Broken m_syntax_error
    commit_src "a file that does not parse"
    dev_prep; snap_before
    do_push origin main
    expect_push 10 1 no
    check 10 "the file is named with the reason" has "$OUT" "src/Broken.cs: incomplete: does not parse"
    finish 10
}

case_11() {
    base_repo c11
    local c0 c1 c2
    c0=$(git rev-parse HEAD)
    write_class src/Core.cs Core m_add m_twice
    commit_src "clean"
    c1=$(git rev-parse HEAD)
    write_class src/Core.cs Core m_add m_twice m_sign_bad
    commit_src "breaking"
    c2=$(git rev-parse HEAD)
    dev_prep; snap_before
    rm -rf "$WORK/data"/*
    "$EXE" review --base "$c0" --head "$c1" > "$WORK/out/c11.soloA" 2>&1
    "$EXE" review --base "$c1" --head "$c2" > "$WORK/out/c11.soloB" 2>&1
    local round good=0
    rm -rf "$WORK/data"/*
    for round in 1 2 3 4 5; do
        ("$EXE" review --base "$c0" --head "$c1" > "$WORK/out/c11.a$round" 2>&1; echo $? > "$WORK/out/c11.a$round.rc") &
        ("$EXE" review --base "$c1" --head "$c2" > "$WORK/out/c11.b$round" 2>&1; echo $? > "$WORK/out/c11.b$round.rc") &
        wait
        if [ "$(cat "$WORK/out/c11.a$round.rc")" = 0 ] && [ "$(cat "$WORK/out/c11.b$round.rc")" = 1 ] \
            && cmp -s "$WORK/out/c11.a$round" "$WORK/out/c11.soloA" && cmp -s "$WORK/out/c11.b$round" "$WORK/out/c11.soloB"; then
            good=$((good + 1))
        fi
    done
    check 11 "solo clean run exits 0" has "$WORK/out/c11.soloA" "0 blocking"
    check 11 "solo breaking run reports the breach" has "$WORK/out/c11.soloB" "error TEAM010"
    check 11 "five repeats of two simultaneous reviews all finished correctly ($good of 5)" equals "$good" 5
    finish 11
}

m_counter_inc() { cat <<'INNER'
    public int Count()
    {
        var count = 0;
++ count;
        return count;
    }
INNER
}
m_counter_dec() { cat <<'INNER'
    public int Count()
    {
        var count = 0;
-- count;
        return count;
    }
INNER
}
m_counter_plain() { cat <<'INNER'
    public int Count()
    {
        var count = 0;
        return count;
    }
INNER
}

review_range() { # base head; sets OUT and RC
    OUT=$WORK/out/$NAME.range
    "$EXE" review --base "$1" --head "$2" > "$OUT" 2>&1
    RC=$?
}

case_13a() {
    base_repo c13a
    write_class src/Core.cs Core m_add m_counter_inc m_sign_bad
    local line
    line=$(line_of_if src/Core.cs)
    commit_src "adds a line that begins with plus plus"
    dev_prep; snap_before
    review_range HEAD~1 HEAD
    check 13a "exit code 1" equals "$RC" 1
    check 13a "finding at the right file and line ($line)" has "$OUT" "src/Core.cs($line,"
    check 13a "coverage: 1 of 1 files reviewed, 1 blocking" has "$OUT" "1 of 1 files reviewed, 1 of 1 builds checked: 1 blocking"
    check 13a "the diff line was not taken for a file" hasnt "$OUT" "count;"
    finish 13a
}

case_13b() {
    base_repo c13b
    write_class src/Core.cs Core m_add m_counter_dec
    git add -A && git commit -q -m "a line that begins with minus minus"
    write_class src/Core.cs Core m_add m_counter_plain m_sign_bad
    local line
    line=$(line_of_if src/Core.cs)
    commit_src "removes it and adds a breach"
    dev_prep; snap_before
    review_range HEAD~1 HEAD
    check 13b "exit code 1" equals "$RC" 1
    check 13b "finding at the right file and line ($line)" has "$OUT" "src/Core.cs($line,"
    check 13b "coverage: 1 of 1 files reviewed, 1 blocking" has "$OUT" "1 of 1 files reviewed, 1 of 1 builds checked: 1 blocking"
    finish 13b
}

copy_file() { find "$WORK/data" -path '*/tree/src/Core.cs' | head -1; }

case_14() {
    base_repo c14
    local c0 c1 c2 line copy committed
    c0=$(git rev-parse HEAD)
    write_class src/Core.cs Core m_add m_sign_bad
    line=$(line_of_if src/Core.cs)
    commit_src "breaking"
    c1=$(git rev-parse HEAD)
    write_class src/Core.cs Core m_add m_sign_bad_edited
    commit_src "touches the breach"
    c2=$(git rev-parse HEAD)
    rm -rf "$WORK/data"/*
    dev_prep; snap_before
    review_range "$c0" "$c1"
    check 14a "first review: exit code 1" equals "$RC" 1
    copy=$(copy_file)
    committed=$(sha256sum "$copy" | cut -d' ' -f1)
    write_class "$copy" Core m_add m_sign_ok
    echo "build output" > "$(dirname "$(dirname "$copy")")/untracked-build.txt"
    review_range "$c0" "$c1"
    check 14a "edited copy: exit code 1, the committed content is judged" equals "$RC" 1
    check 14a "finding at the same line ($line)" has "$OUT" "src/Core.cs($line,"
    check 14a "the copy holds the committed bytes again" equals "$(sha256sum "$copy" | cut -d' ' -f1)" "$committed"
    check 14a "an untracked file in the copy is left alone" test -f "$(dirname "$(dirname "$copy")")/untracked-build.txt"
    rm -f "$copy"
    review_range "$c0" "$c1"
    check 14b "deleted file: exit code 1" equals "$RC" 1
    check 14b "finding at the same line ($line)" has "$OUT" "src/Core.cs($line,"
    check 14b "the file exists again with the committed bytes" equals "$(sha256sum "$copy" 2> /dev/null | cut -d' ' -f1)" "$committed"
    sed -i 's/return 1;/return 9;/' "$copy"
    review_range "$c1" "$c2"
    check 14c "other tree after a same-size edit: exit code 1" equals "$RC" 1
    check 14c "finding at the line of the breach ($line)" has "$OUT" "src/Core.cs($line,"
    check 14c "the copy holds the committed content of the new tree" has "$copy" "return 2;"
    check 14c "and not the hand edit" hasnt "$copy" "return 9;"
    finish 14
}

missing_count() { GIT_NO_LAZY_FETCH=1 git rev-list --objects --missing=print --all 2> /dev/null | grep -c '^?'; }

case_15() {
    base_repo c15
    local c0 c1 clone blob before after
    c0=$(git rev-parse HEAD)
    write_class src/Core.cs Core m_add m_sign_bad
    commit_src "breaking"
    c1=$(git rev-parse HEAD)
    git push -q --no-verify origin main 2> /dev/null
    git --git-dir="$REMOTE" config uploadpack.allowFilter true
    clone=$WORK/repos/c15clone
    git clone -q --filter=blob:none --no-checkout "file:///$(cygpath -m "$REMOTE")" "$clone" || { report FAIL 15 "the partial clone could not be built"; return; }
    cd "$clone" || return
    blob=$(git rev-parse "$c1:src/Core.cs")
    before=$(missing_count)
    check 15 "precondition: the blob is missing in the clone" bash -c "! GIT_NO_LAZY_FETCH=1 git cat-file -e $blob 2> /dev/null"
    review_range "$c0" "$c1"
    after=$(missing_count)
    check 15 "exit code 2" equals "$RC" 2
    check 15 "the reason names the failed git command" has "$OUT" "git diff failed"
    check 15 "coverage says the scope is incomplete" has "$OUT" "review scope: incomplete"
    check 15 "no fetch: the blob is still missing" bash -c "! GIT_NO_LAZY_FETCH=1 git cat-file -e $blob 2> /dev/null"
    check 15 "no fetch: the missing objects are the same ($before before, $after after)" equals "$before" "$after"
}

# ---------- more cases (verdicts in expected.md) ----------
rule_json() { # id: one rule of the added cases
    case "$1" in
        TEAM010) cat <<'EOF'
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
        ;;
        TEAM011) cat <<'EOF'
        {
            "id": "TEAM011",
            "language": "csharp",
            "for": "method",
            "severity": "block",
            "title": "Only public and private methods inside an internal class",
            "where": { "containingType": "class", "containingTypeDeclaredAccess": "internal" },
            "require": { "declaredAccess": ["public", "private"] }
        }
EOF
        ;;
        TEAM011S) cat <<'EOF'
        {
            "id": "TEAM011S",
            "language": "csharp",
            "for": "method",
            "severity": "block",
            "title": "Only public and private methods inside a class written internal",
            "where": { "containingType": "class", "containingTypeWrittenAccess": "internal" },
            "require": { "writtenAccess": ["public", "private"] }
        }
EOF
        ;;
        DOC1) cat <<'EOF'
        {
            "id": "DOC1",
            "language": "csharp",
            "for": "method",
            "severity": "block",
            "title": "A method has documentation",
            "require": { "documentationPresent": true }
        }
EOF
        ;;
        NAME1) cat <<'EOF'
        {
            "id": "NAME1",
            "language": "csharp",
            "for": "method",
            "severity": "block",
            "title": "No method named Run",
            "where": { "name": "Run" },
            "forbid": true
        }
EOF
        ;;
    esac
}

write_rules_of() { # ids...: commitfence.json holding these rules
    local first=1 id
    {
        echo '{'
        echo '    "rules": ['
        for id in "$@"; do
            [ $first = 0 ] && echo '        ,'
            first=0
            rule_json "$id"
        done
        echo '    ]'
        echo '}'
    } > commitfence.json
}

sleepers() { ps -W 2> /dev/null | grep -c '[s]leep'; }

line_matching() { grep -n -- "$2" "$1" | head -1 | cut -d: -f1; }

finding_lines_of() { # output-file source-file rule: the distinct lines of the findings of the rule in the file
    grep -E "^$2\\([0-9]+,[0-9]+\\): (error|warning) $3:" "$1" | sed -E 's/^[^(]*\(([0-9]+),.*/\1/' | sort -un | tr '\n' ' ' | sed 's/ $//'
}

case_16() {
    local folder=$WORK/notrepo
    mkdir -p "$folder"
    cd "$folder" || return
    NAME=c16
    export GIT_CEILING_DIRECTORIES=$WORK_WIN
    "$EXE" review --staged > "$WORK/out/c16a.txt" 2>&1
    local rc_a=$?
    "$EXE" review --base HEAD --head HEAD > "$WORK/out/c16b.txt" 2>&1
    local rc_b=$?
    unset GIT_CEILING_DIRECTORIES
    check 16 "review --staged: exit code 2" equals "$rc_a" 2
    check 16 "review --staged: says the folder is not in a git repository" has "$WORK/out/c16a.txt" "the current folder is not inside a git repository"
    check 16 "review --staged: review scope incomplete" has "$WORK/out/c16a.txt" "review scope: incomplete"
    check 16 "review --base: exit code 2" equals "$rc_b" 2
    check 16 "review --base: says the folder is not in a git repository" has "$WORK/out/c16b.txt" "the current folder is not inside a git repository"
    check 16 "the folder holds no new file" equals "$(ls -A "$folder" | wc -l | tr -d ' ')" 0
}

case_17a() {
    base_repo c17a
    printf '*.slow filter=slow\n' > .gitattributes
    echo "x" > data.slow
    git config filter.slow.smudge "sleep 300"
    git add -A && git commit -q -m "a file whose checkout hangs"
    dev_prep; snap_before
    local before_sleepers started
    before_sleepers=$(sleepers)
    started=$SECONDS
    COMMITFENCE_PROCESS_LIMIT_SECONDS=8 do_push origin main
    local took=$((SECONDS - started))
    expect_push 17a 2 no
    check 17a "the output says git did not finish within 8 seconds" has "$OUT" "git did not finish within 8 seconds and was stopped"
    check 17a "coverage says the review is incomplete and why" has "$OUT" "incomplete: git read-tree failed: git did not finish within 8 seconds"
    check 17a "the push ended in under 60 seconds ($took)" [ "$took" -lt 60 ]
    check 17a "no sleep process of the filter is left running" equals "$(sleepers)" "$before_sleepers"
    finish 17a
}

case_17b() {
    base_repo c17b
    local c0
    c0=$(git rev-parse HEAD)
    write_class src/Core.cs Core m_add m_twice
    commit_src "a change"
    echo "not a folder" > "$WORK/afile"
    COMMITFENCE_DATA_FOLDER="$WORK_WIN/afile" "$EXE" review --base "$c0" --head HEAD > "$WORK/out/c17b.out" 2> "$WORK/out/c17b.err"
    local rc=$?
    COMMITFENCE_TRACE=1 COMMITFENCE_DATA_FOLDER="$WORK_WIN/afile" "$EXE" review --base "$c0" --head HEAD > /dev/null 2> "$WORK/out/c17b.trace"
    check 17b "exit code 2" equals "$rc" 2
    check 17b "standard error is exactly one line" equals "$(wc -l < "$WORK/out/c17b.err" | tr -d ' ')" 1
    check 17b "the line says CommitFence could not finish, with the kind" has "$WORK/out/c17b.err" "CommitFence could not finish: IOException: "
    check 17b "no stack frame is printed" equals "$(grep -c '^   at ' "$WORK/out/c17b.err")" 0
    check 17b "with COMMITFENCE_TRACE=1 the stack trace follows" bash -c "[ \"\$(grep -c '^   at ' '$WORK/out/c17b.trace')\" -gt 0 ]"
}

case_17c() {
    base_repo c17c
    "$EXE" rules list --settings "a:b:c.json" > "$WORK/out/c17c.out" 2> "$WORK/out/c17c.err"
    local rc=$?
    check 17c "exit code 3" equals "$rc" 3
    check 17c "standard error says something" [ -s "$WORK/out/c17c.err" ]
    check 17c "no stack frame is printed" equals "$(grep -c '^   at ' "$WORK/out/c17c.err")" 0
    check 17c "standard output is empty" [ ! -s "$WORK/out/c17c.out" ]
}

reach_repo() { # name rule-ids...: a repository whose settings hold these rules only
    local name=$1
    shift
    new_repo "$name"
    write_rules_of "$@"
}

case_18a() {
    reach_repo c18a TEAM011S TEAM011
    mkdir -p src
    cat > src/Box.cs <<'EOF'
namespace Demo;

public class Box
{
    /// <summary>
    /// The size.
    /// </summary>
    protected int Size()
    {
        return 1;
    }
}
EOF
    git add -A && git commit -q -m "initial"
    git push -q --no-verify origin main 2> /dev/null
    sed -i 's/^public class Box/internal class Box/' src/Box.cs
    local line
    line=$(line_matching src/Box.cs 'protected int Size')
    commit_src "only the class changes"
    dev_prep; snap_before
    do_push origin main
    expect_push 18a 1 no
    check 18a "TEAM011S at the line of Size ($line)" equals "$(finding_lines_of "$OUT" src/Box.cs TEAM011S)" "$line"
    check 18a "TEAM011 (meaning) at the line of Size ($line)" equals "$(finding_lines_of "$OUT" src/Box.cs TEAM011)" "$line"
    check 18a "one finding line for each rule" equals "$(grep -cE ' TEAM011S?:' "$OUT")" 2
    finish 18a
}

case_18b() {
    reach_repo c18b DOC1
    mkdir -p src
    cat > src/Doc.cs <<'EOF'
namespace Demo;

public class Doc
{
    /// <summary>
    /// Says it.
    /// </summary>
    public int Documented()
    {
        return 1;
    }
}
EOF
    git add -A && git commit -q -m "initial"
    git push -q --no-verify origin main 2> /dev/null
    sed -i '/\/\/\//d' src/Doc.cs
    local line
    line=$(line_matching src/Doc.cs 'public int Documented')
    commit_src "the documentation goes"
    dev_prep; snap_before
    do_push origin main
    expect_push 18b 1 no
    check 18b "DOC1 at the line of Documented ($line)" equals "$(finding_lines_of "$OUT" src/Doc.cs DOC1)" "$line"
    finish 18b
}

wide_file() { # the two undocumented methods; $1 is the body of First
    cat <<EOF
namespace Demo;

public class Wide
{
    public int First()
    {
        return $1;
    }

    public int Second()
    {
        return 2;
    }
}
EOF
}

case_18c() {
    reach_repo c18c DOC1
    mkdir -p src
    wide_file 1 > src/Wide.cs
    git add -A && git commit -q -m "initial with two old breaches"
    git push -q --no-verify origin main 2> /dev/null
    wide_file 11 > src/Wide.cs
    local first second
    first=$(line_matching src/Wide.cs 'public int First')
    second=$(line_matching src/Wide.cs 'public int Second')
    commit_src "the body of First changes"
    dev_prep; snap_before
    do_push origin main
    expect_push 18c 1 no
    check 18c "DOC1 at the line of First ($first) and only there" equals "$(finding_lines_of "$OUT" src/Wide.cs DOC1)" "$first"
    check 18c "the old breach of Second (line $second) is not reported" hasnt "$OUT" "src/Wide.cs($second,"
    finish 18c
}

case_18d() {
    reach_repo c18d DOC1
    mkdir -p src
    wide_file 1 > src/Wide.cs
    git add -A && git commit -q -m "initial with two old breaches"
    git push -q --no-verify origin main 2> /dev/null
    cat > src/Wide.cs <<'EOF'
namespace Demo;

public class Wide
{
    /// <summary>
    /// A new method above the old ones.
    /// </summary>
    public int Zero()
    {
        return 0;
    }

    public int First()
    {
        return 1;
    }

    public int Second()
    {
        return 2;
    }
}
EOF
    commit_src "a documented method is inserted above"
    dev_prep; snap_before
    do_push origin main
    expect_push 18d 0 yes
    check 18d "no DOC1 finding: the old breaches are not touched" hasnt "$OUT" "DOC1"
    finish 18d
}

case_19() {
    reach_repo c19 NAME1
    mkdir -p src
    cat > src/Names.cs <<'EOF'
namespace Demo;

public class Names
{
    public int Run(int a)
    {
        return a;
    }

    public int @Run(string a)
    {
        return a.Length;
    }

    public int Run(long a)
    {
        return (int)a;
    }
}
EOF
    local plain at escaped backslash
    # the third spelling is written with a unicode escape for its second letter
    backslash=$(printf '\')
    sed -i "s/public int Run(long a)/public int R${backslash}${backslash}u0075n(long a)/" src/Names.cs
    plain=$(line_matching src/Names.cs 'public int Run(int a)')
    at=$(line_matching src/Names.cs 'public int @Run')
    escaped=$(line_matching src/Names.cs 'R\\u0075n')
    git add -A && git commit -q -m "one name, three spellings"
    dev_prep; snap_before
    do_push origin main
    expect_push 19 1 no
    check 19 "NAME1 at the three lines ($plain $at $escaped)" equals "$(finding_lines_of "$OUT" src/Names.cs NAME1)" "$plain $at $escaped"
    finish 19
}

case_20() {
    local probe=$WORK/casecheck
    mkdir -p "$probe"
    : > "$probe/a"
    : > "$probe/A"
    if [ "$(ls "$probe" | wc -l | tr -d ' ')" != 2 ]; then
        skip_case 20 "this machine cannot hold two files whose names differ only by case in one folder, so the case cannot be built here"
        return
    fi

    reach_repo c20 TEAM010
    mkdir -p src
    write_class src/Dup.cs Dup m_add m_sign_bad
    write_class src/dup.cs DupLower m_add m_sign_ok
    git add -A && git commit -q -m "two files, one case apart; the breach is in Dup.cs"
    git push -q --no-verify origin main 2> /dev/null
    write_class src/dup.cs DupLower m_add m_sign_bad
    local line
    line=$(line_of_if src/dup.cs)
    commit_src "the breach is added to dup.cs"
    dev_prep; snap_before
    do_push origin main
    expect_push 20 1 no
    check 20 "TEAM010 at src/dup.cs ($line)" equals "$(finding_lines_of "$OUT" src/dup.cs TEAM010)" "$line"
    check 20 "none at src/Dup.cs, whose old breach the push did not touch" equals "$(finding_lines_of "$OUT" src/Dup.cs TEAM010)" ""
    finish 20
}


case_21() {
    base_repo c21
    local before blob
    before=$(git rev-parse HEAD)
    blob=$(git rev-parse HEAD:commitfence.json)
    # only the file's mode changes: the push counts commitfence.json as changed, while no diff has to read its content from git
    git update-index --chmod=+x commitfence.json
    git commit -q -m "commitfence.json changes its mode"
    "$EXE" review --base "$before" --head HEAD > "$WORK/out/c21.warm" 2>&1
    check 21 "before the object is removed the review completes and sees the settings file as changed" has "$WORK/out/c21.warm" "Rule files changed: commitfence.json"
    check 21 "before the object is removed nothing is incomplete" hasnt "$WORK/out/c21.warm" "incomplete"
    rm -f ".git/objects/${blob:0:2}/${blob:2}"
    "$EXE" review --base "$before" --head HEAD > "$WORK/out/c21.out" 2>&1
    local rc=$?
    cat "$WORK/out/c21.out"
    check 21 "the blob of commitfence.json is gone from the object store" bash -c "! git cat-file -e '$blob' 2> /dev/null"
    check 21 "exit code 2, never 0" equals "$rc" 2
    check 21 "the reason is printed: git could not read the missing object" has "$WORK/out/c21.out" "unable to read $blob"
    check 21 "review scope: incomplete" has "$WORK/out/c21.out" "review scope: incomplete"
}

case_22() {
    base_repo c22
    dev_prep
    write_class src/Core.cs Core m_add m_twice
    git add src/Core.cs
    snap_before
    review_staged
    check 22 "exit code 0" equals "$RC" 0
    check 22 "the repository's data folder exists" equals "$(find "$WORK/data" -mindepth 1 -maxdepth 1 -type d -name 'c22-*' | wc -l | tr -d ' ')" 1
    check 22 "no commitfence-index file is left in the data folder" equals "$(find "$WORK/data" -name 'commitfence-index-*' | wc -l | tr -d ' ')" 0
    finish 22
}

case_23a() {
    base_repo c23a
    printf '{
    "rules": [
        { "id": "TEAM010" "language": "csharp" }
    ]
}
' > commitfence.json
    git add commitfence.json && git commit -q -m "only the settings file changes, and it is broken"
    dev_prep; snap_before
    do_push origin main
    expect_push 23a 3 no
    check 23a "the path is named as the file in the tree under review, with a line and a column" bash -c "grep -Eq 'commitfence.json \(in the tree under review\)\([0-9]+,[0-9]+\)' '$OUT'"
    check 23a "the message says the file is not valid JSON" has "$OUT" "not valid JSON"
    finish 23a
}

case_23b() {
    base_repo c23b
    sed -i 's/"severity": "block"/"severity": "warn"/' commitfence.json
    git add commitfence.json && git commit -q -m "only the settings file changes, and it is valid"
    dev_prep; snap_before
    do_push origin main
    expect_push 23b 0 yes
    check 23b "the new settings were read: no error about them" hasnt "$OUT" "in the tree under review"
    finish 23b
}

hook_with_missing_index() { # the team's wrapper exports GIT_INDEX_FILE, the path of a file that does not exist
    printf '#!/bin/sh
export GIT_INDEX_FILE="%s"
"$(dirname "$0")/pre-push.team" "$@"
rc=$?
echo $rc > "$(dirname "$0")/hook-exit.txt"
exit $rc
' "$WORK_WIN/no-such-index" > .git/hooks/pre-push
    chmod +x .git/hooks/pre-push
}

case_24a() {
    base_repo c24a
    write_class src/Core.cs Core m_add m_sign_bad
    local line
    line=$(line_of_if src/Core.cs)
    commit_src "breaking change"
    hook_with_missing_index
    check 24a "precondition: the index file the hook names does not exist" bash -c "[ ! -e '$WORK/no-such-index' ]"
    dev_prep; snap_before
    do_push origin main
    expect_push 24a 1 no
    check 24a "finding names the file and the line ($line)" has "$OUT" "src/Core.cs($line,"
    check 24a "finding is TEAM010" has "$OUT" "error TEAM010"
    finish 24a
}

case_24b() {
    base_repo c24b
    write_class src/Core.cs Core m_add m_twice
    commit_src "clean change"
    hook_with_missing_index
    dev_prep; snap_before
    do_push origin main
    expect_push 24b 0 yes
    check 24b "no finding printed" hasnt "$OUT" "TEAM010"
    check 24b "coverage: 1 of 1 files reviewed, 0 blocking" has "$OUT" "1 of 1 files reviewed, 1 of 1 builds checked: 0 blocking"
    finish 24b
}

case_25a() {
    base_repo c25a
    printf '*.cs -diff\n' > .gitattributes
    git add .gitattributes && git commit -q -m "git treats .cs files as binary"
    git push -q --no-verify origin main 2> /dev/null
    write_class src/Core.cs Core m_add m_sign_bad
    local line
    line=$(line_of_if src/Core.cs)
    commit_src "breaking change in a file git calls binary"
    dev_prep; snap_before
    do_push origin main
    expect_push 25a 1 no
    check 25a "finding names the file and the line ($line)" has "$OUT" "src/Core.cs($line,"
    check 25a "finding is TEAM010" has "$OUT" "error TEAM010"
    finish 25a
}

case_25b() {
    base_repo c25b
    printf '*.cs -diff\n' > .gitattributes
    git add .gitattributes && git commit -q -m "git treats .cs files as binary"
    git push -q --no-verify origin main 2> /dev/null
    write_class src/Core.cs Core m_add m_twice
    commit_src "clean change in a file git calls binary"
    dev_prep; snap_before
    do_push origin main
    expect_push 25b 0 yes
    check 25b "no finding printed" hasnt "$OUT" "TEAM010"
    finish 25b
}

case_25c() {
    base_repo c25c
    printf '*.cs -diff\n' > .gitattributes
    write_class src/Core.cs Core m_sign_bad
    git add -A && git commit -q -m "git treats .cs files as binary, and the file holds an old breach"
    git push -q --no-verify origin main 2> /dev/null
    write_class src/Core.cs Core m_sign_bad m_twice
    commit_src "a clean method far from the old breach, in a file git calls binary"
    dev_prep; snap_before
    do_push origin main
    expect_push 25c 0 yes
    check 25c "the old breach is not reported" hasnt "$OUT" "TEAM010"
    finish 25c
}

write_old_file() { # file [extra]: a breach near the top, twenty clean lines, and with an argument one more method at the end
    mkdir -p "$(dirname "$1")"
    {
        echo "namespace Demo;"
        echo
        echo "public class Old"
        echo "{"
        m_sign_bad
        local index
        for index in 1 2 3 4 5 6; do
            printf '\n    public int Value%s()\n    {\n        return %s;\n    }\n' "$index" "$index"
        done

        if [ -n "${2:-}" ]; then
            printf '\n    public int Last()\n    {\n        return 99;\n    }\n'
        fi

        echo "}"
    } > "$1"
}

case_25d() {
    base_repo c25d
    printf '*.cs -diff\n' > .gitattributes
    write_old_file src/Old.cs
    git add -A && git commit -q -m "git treats .cs files as binary, and Old.cs holds an old breach"
    git push -q --no-verify origin main 2> /dev/null
    git mv src/Old.cs src/New.cs
    write_old_file src/New.cs extra
    commit_src "Old.cs is renamed to New.cs and gets a clean method at its end"
    check 25d "precondition: git sees the commit as a rename" bash -c "git diff -M --name-status HEAD~1 HEAD | grep -q '^R'"
    dev_prep; snap_before
    do_push origin main
    expect_push 25d 0 yes
    check 25d "the old breach is not reported" hasnt "$OUT" "TEAM010"
    finish 25d
}

case_25e() {
    base_repo c25e
    printf '*.cs -diff\n' > .gitattributes
    write_class Foo.cs Top m_sign_bad
    write_class "src b/Foo.cs" Inner m_add
    git add -A && git commit -q -m "two files named Foo.cs, the top one with an old breach"
    git push -q --no-verify origin main 2> /dev/null
    write_class "src b/Foo.cs" Inner m_add m_sign_bad
    local line
    line=$(line_of_if "src b/Foo.cs")
    git add -A && git commit -q -m "a breaking change in a file whose folder is named src b"
    dev_prep; snap_before
    do_push origin main
    expect_push 25e 1 no
    check 25e "finding names src b/Foo.cs and the line ($line)" has "$OUT" "src b/Foo.cs($line,"
    check 25e "finding is TEAM010" has "$OUT" "error TEAM010"
    check 25e "no finding on the top Foo.cs" bash -c "! grep -q '^Foo.cs(' '$OUT'"
    finish 25e
}

case_25f() {
    base_repo c25f
    printf '*.cs -diff\n' > .gitattributes
    local index
    for index in $(seq -w 1 60); do
        write_class "src/File$index.cs" "File$index" m_add
    done

    git add -A && git commit -q -m "sixty files, and git treats .cs files as binary"
    git push -q --no-verify origin main 2> /dev/null
    for index in $(seq -w 1 60); do
        if [ "$index" = 01 ] || [ "$index" = 60 ]; then
            write_class "src/File$index.cs" "File$index" m_add m_sign_bad
        else
            write_class "src/File$index.cs" "File$index" m_add m_twice
        fi
    done

    local first_line last_line
    first_line=$(line_of_if src/File01.cs)
    last_line=$(line_of_if src/File60.cs)
    commit_src "a change in each of sixty files, a breach in the first and in the last"
    dev_prep; snap_before
    do_push origin main
    expect_push 25f 1 no
    check 25f "the first file is named at its line ($first_line)" has "$OUT" "src/File01.cs($first_line,"
    check 25f "the last file is named at its line ($last_line)" has "$OUT" "src/File60.cs($last_line,"
    check 25f "exactly two findings" equals "$(grep -c 'error TEAM010' "$OUT")" 2
    finish 25f
}

write_settings_26() { # severity enabled structure-extra: TEAM010, the rule that needs the build, and optionally modules and directions
    cat > commitfence.json <<JSON
{
    "rules": [
        {
            "id": "TEAM010",
            "language": "csharp",
            "for": "if-statement",
            "severity": "$1",
            "title": "No braces around a single-line if",
            "where": { "statementsInBody": 1, "bodyFitsOnOneLine": true },
            "require": { "braces": false }
        },
        {
            "id": "TEAM012",
            "language": "csharp",
            "for": "usage",
            "severity": "block",
            "enabled": $2,
            "title": "Use IClock, not DateTime.Now",
            "where": { "target": "System.DateTime.Now" },
            "forbid": true
        }
    ]$3
}
JSON
}

settings_only_checks() { # case-id
    dev_prep; snap_before
    do_push origin main
    expect_push "$1" 0 yes
    check "$1" "the settings file is named as changed" has "$OUT" "Rule files changed: commitfence.json"
    check "$1" "no rule is reported as not run" hasnt "$OUT" "was not run"
    check "$1" "the review is not incomplete" hasnt "$OUT" "incomplete"
    check "$1" "no build is made for a push with no code in it" hasnt "$OUT" "builds checked"
    finish "$1"
}

case_26a() {
    base_repo c26a
    write_settings_26 block true ""
    git add -A && git commit -q -m "rules"
    git push -q --no-verify origin main 2> /dev/null
    write_settings_26 warn true ""
    git add -A && git commit -q -m "TEAM010 only warns now"
    settings_only_checks 26a
}

case_26b() {
    base_repo c26b
    write_settings_26 block true ',
    "structure": { "modules": { "demo": [ "namespace:Demo" ], "other": [ "namespace:Other" ] }, "directions": { "allow": [] } }'
    git add -A && git commit -q -m "rules and modules"
    git push -q --no-verify origin main 2> /dev/null
    write_settings_26 block true ',
    "structure": { "modules": { "demo": [ "namespace:Demo" ], "other": [ "namespace:Other" ] }, "directions": { "allow": [ "demo -> other" ] } }'
    git add -A && git commit -q -m "a direction"
    settings_only_checks 26b
}

case_26c() {
    base_repo c26c
    write_settings_26 block false ""
    git add -A && git commit -q -m "rules, TEAM012 off"
    git push -q --no-verify origin main 2> /dev/null
    write_settings_26 block true ""
    git add -A && git commit -q -m "TEAM012 switched on"
    settings_only_checks 26c
}

# ---------- run ----------
for c in 1 2 3 3b 4 5 5b 5c 6 6b 7a 7b 7f 7c 7d 7e 8a 8b 9a 9b 10 11 13a 13b 14 15 16 17a 17b 17c 18a 18b 18c 18d 19 20 21 22 23a 23b 24a 24b 25a 25b 25c 25d 25e 25f 26a 26b 26c; do
    if [ -z "$SELECTED" ] || [[ " $SELECTED " == *" $c "* ]]; then
        ( case_$c )
    fi
done > "$WORK/out/results.txt" 2>&1
cat "$WORK/out/results.txt"

# ---------- times (not a check: set CHECK_TIMES=1 to print them) ----------
big_repo() {
    new_repo big
    local index
    mkdir -p src
    for index in $(seq 1 1000); do
        printf 'namespace Demo;\n\npublic class Part%s\n{\n    public int Value()\n    {\n        return %s;\n    }\n\n    public int Twice()\n    {\n        return Value() * 2;\n    }\n}\n' "$index" "$index" > "src/Part$index.cs"
    done
    git add -A && git commit -q -m "big initial"
    B0=$(git rev-parse HEAD)
    sed -i 's/return 1;/return 11;/' src/Part1.cs
    git add src && git commit -q -m "nearby one"
    B1=$(git rev-parse HEAD)
    sed -i 's/return 2;/return 22;/' src/Part2.cs
    git add src && git commit -q -m "nearby two"
    B2=$(git rev-parse HEAD)
}

now_ms() { echo $(($(date +%s%N) / 1000000)); }

median() { printf '%s\n' "$@" | sort -n | sed -n '3p'; }

hook_run() { # old new
    printf 'refs/heads/main %s refs/heads/main %s\n' "$2" "$1" | "$EXE" review --hook pre-push origin "$(cygpath -m "$REMOTE")" > "$WORK/out/time.last" 2>&1
}

if [ "${CHECK_TIMES:-0}" = 1 ]; then
echo
echo "times (big repository, hook run, 5 repetitions each):"
(
    big_repo
    echo "files in the repository: $(git ls-files | wc -l) (.cs files: $(git ls-files '*.cs' | wc -l))"
    firsts=()
    seconds=()
    for rep in 1 2 3 4 5; do
        rm -rf "$WORK/data"/*
        start=$(now_ms); hook_run "$B0" "$B1"; firsts+=($(($(now_ms) - start)))
        start=$(now_ms); hook_run "$B1" "$B2"; seconds+=($(($(now_ms) - start)))
    done
    echo "first review (private copy built): ${firsts[*]} ms, median $(median "${firsts[@]}") ms"
    echo "second review (nearby tree):       ${seconds[*]} ms, median $(median "${seconds[@]}") ms"
    tail -2 "$WORK/out/time.last"
)
fi

PASSES=$(grep -c '^PASS' "$WORK/out/results.txt")
FAILS=$(grep -c '^FAIL' "$WORK/out/results.txt")
SKIPS=$(grep -c '^SKIP' "$WORK/out/results.txt")
finish_script
