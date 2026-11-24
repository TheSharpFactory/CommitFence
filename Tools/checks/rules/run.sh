#!/bin/bash
# The rules commands, settings files read with their exact place, path patterns.
# Prints PASS or FAIL per check. Expected results are in expected.md. The root commitfence.json of this repository is only ever read.
# Usage: Tools/checks/rules/run.sh <path of the commitfence program> [parent folder for the scratch folder] [case ids...]
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
ROOT=$(mktemp -d "$PARENT/commitfence-rules.XXXXXX") || stop_now "cannot create a scratch folder under $PARENT"
ROOT=$(cd "$ROOT" && pwd) || stop_now "cannot enter the scratch folder"
if [ -z "$ROOT" ] || [ "$ROOT" = "/" ] || [ ! -d "$ROOT" ]; then
    stop_now "the scratch folder path is not usable: '$ROOT'"
fi

STARTED=$SECONDS
WORK=$ROOT/work
mkdir -p "$WORK/repos" "$WORK/data"
PROJECT=$REPOSITORY
cp "$PROJECT/commitfence.json" "$WORK/base.json"
BASE=$WORK/base.json
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
SO=$WORK/stdout.txt
SE=$WORK/stderr.txt
CODE=0

report() { # status id description
    if [ "$1" = "PASS" ]; then PASSES=$((PASSES + 1)); else FAILS=$((FAILS + 1)); fi
    echo "$1  $2: $3"
}

t() { # id description condition (evaluated)
    local id=$1 text=$2
    shift 2
    if eval "$@"; then report PASS "$id" "$text"; else report FAIL "$id" "$text"; fi
}

has() { grep -Fq -- "$2" "$1"; }
hasnt() { ! grep -Fq -- "$2" "$1"; }
matches() { grep -Eq -- "$2" "$1"; }

run() { # folder args...   (standard input is empty)
    local folder=$1
    shift
    ( cd "$folder" && "$EXE" "$@" > "$SO" 2> "$SE" < /dev/null )
    CODE=$?
}

run_in() { # folder inputfile args...
    local folder=$1 input=$2
    shift 2
    ( cd "$folder" && "$EXE" "$@" > "$SO" 2> "$SE" < "$input" )
    CODE=$?
}

difflines() { diff "$1" "$2" | grep '^[<>]' | sed 's/^\([<>]\) */\1/'; }
structure_part() { sed -n '/^    "structure"/,$p' "$1"; }
same_file() { cmp -s "$1" "$2"; }
same_structure() { [ "$(structure_part "$1")" = "$(structure_part "$2")" ] && [ -n "$(structure_part "$1")" ]; }

new_case() { # name  -> $WORK/<name> with commitfence.json and src
    CASE=$WORK/$1
    mkdir -p "$CASE/src"
    cp "$BASE" "$CASE/commitfence.json"
    cp "$WORK/src/"*.cs "$CASE/src/"
}

# ---------- sources ----------
mkdir -p "$WORK/src"
cat > "$WORK/src/Three.cs" <<'EOF'
namespace Sample;

public class Samples
{
    public int Pick(int value)
    {
        if(value > 0)
        {
            return 1;
        }

        if(value < 0)
            return 2;

        if(value == 0)
        {
            Console.WriteLine("zero");
            return 3;
        }

        return 4;
    }
}
EOF
cat > "$WORK/src/Else.cs" <<'EOF'
namespace Sample;

public class Choices
{
    public int Pick(int value)
    {
        if(value == 5) return 5; else return 6;
    }
}
EOF

write_csproj() {
    cat > "$1" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
EOF
}

make_repo() { # name folder file   (reads the source from stdin)
    local repo=$WORK/repos/$1
    mkdir -p "$repo/$2"
    ( cd "$repo" && git init -q && write_csproj "$2/$2.csproj" && git add -A && git commit -q -m "base" )
    cat > "$repo/$2/$3"
    ( cd "$repo" && git add -A && git commit -q -m "add the code" )
}

make_repo tidy Tidy Worker.cs <<'EOF'
namespace Tidy;

internal class Worker
{
    protected void Hidden()
    {
    }
}
EOF

make_repo clock Clock Reader.cs <<'EOF'
namespace Clock;

public class Reader
{
    public DateTime Now()
    {
        return DateTime.Now;
    }

    public DateTime Utc()
    {
        return DateTime.UtcNow;
    }
}
EOF

review_repo() { # repo settings-folder
    ( cd "$WORK/repos/$1" && "$EXE" review --base HEAD~1 --head HEAD --settings "$(cygpath -m "$2/commitfence.json")" > "$SO" 2> "$SE" < /dev/null )
    CODE=$?
}

review_files() { # case folder
    run "$1" review --files src/Three.cs src/Else.cs
}

# ---------- case 1 and 2: each change, the next review follows it, nothing else changes ----------
new_case c1
review_files "$CASE"
t 1.1 "baseline review: TEAM010 at Three.cs line 7, exit 1" 'has $SO "src/Three.cs(7,9): error TEAM010" && hasnt $SO "src/Three.cs(12," && hasnt $SO "src/Three.cs(15," && [ $CODE = 1 ]'

run "$CASE" rules disable TEAM010
t 1.2a "rules disable TEAM010: exit 0" '[ $CODE = 0 ] && has $SO "TEAM010 is now off."'
t 2.1 "disable: the only difference is the enabled line" '[ "$(difflines $BASE $CASE/commitfence.json)" = ">\"enabled\": false," ]'
t 2.2 "disable: structure part identical" 'same_structure $BASE $CASE/commitfence.json'
run "$CASE" rules list
t 1.2b "rules list shows TEAM010 off" 'matches $SO "TEAM010 .*\(off\)" && hasnt $SO "TEAM011 .*(off)"'
run "$CASE" rules show TEAM010
t 1.2c "rules show says off" 'matches $SO "status: +off"'
review_files "$CASE"
t 1.2d "review no longer reports TEAM010 (exit 2: build rules not run, and the summary says why and what runs them)" 'hasnt $SO "TEAM010" && has $SO "not run: TEAM011" && has $SO "4 rules need a build, and a review of named files does not run a build, so they were not run: commitfence review --staged or a push runs them." && [ $CODE = 2 ]'

run "$CASE" rules enable TEAM010
t 1.3a "rules enable TEAM010: exit 0, file identical to base" '[ $CODE = 0 ] && same_file $BASE $CASE/commitfence.json'
review_files "$CASE"
t 1.3b "review reports TEAM010 again, exit 1" 'has $SO "src/Three.cs(7,9): error TEAM010" && [ $CODE = 1 ]'

# TEAM011 on the tidy repository
new_case c2
review_repo tidy "$CASE"
t 1.4 "tidy: TEAM011 at Worker.cs line 5, exit 1" 'matches $SO "Worker.cs\(5,[0-9]+\): error TEAM011" && [ $CODE = 1 ]'
run "$CASE" rules severity TEAM011 warn
t 1.5a "rules severity TEAM011 warn: exit 0" '[ $CODE = 0 ] && has $SO "TEAM011 now only warns."'
t 2.3 "severity: the only differences are the severity line" '[ "$(difflines $BASE $CASE/commitfence.json)" = "$(printf "<\"severity\": \"block\",\n>\"severity\": \"warn\",")" ]'
t 2.4 "severity: structure part identical" 'same_structure $BASE $CASE/commitfence.json'
review_repo tidy "$CASE"
t 1.5b "review shows TEAM011 as a warning and exits 0" 'matches $SO "Worker.cs\(5,[0-9]+\): warning TEAM011" && [ $CODE = 0 ]'
run "$CASE" rules list
t 1.5c "rules list shows warn for TEAM011" 'matches $SO "TEAM011 +warn"'
run "$CASE" rules severity TEAM011 block
t 1.6a "back to block: file identical to base" '[ $CODE = 0 ] && same_file $BASE $CASE/commitfence.json'
review_repo tidy "$CASE"
t 1.6b "review blocks again" 'matches $SO "Worker.cs\(5,[0-9]+\): error TEAM011" && [ $CODE = 1 ]'

# TEAM012 on the clock repository
new_case c3
review_repo clock "$CASE"
t 1.7 "clock: TEAM012 at Reader.cs line 7 and not line 12, exit 1" 'matches $SO "Reader.cs\(7,[0-9]+\): error TEAM012" && ! matches $SO "Reader.cs\(12," && [ $CODE = 1 ]'
run "$CASE" rules set TEAM012 --where target=System.DateTime.UtcNow
t 1.8a "rules set TEAM012 target: exit 0" '[ $CODE = 0 ] && has $SO "Changed rule TEAM012."'
t 2.5 "set: the only differences are the where line" '[ "$(difflines $BASE $CASE/commitfence.json)" = "$(printf "<\"where\": { \"target\": \"System.DateTime.Now\" },\n>\"where\": { \"target\": \"System.DateTime.UtcNow\" },")" ]'
t 2.6 "set: structure part identical" 'same_structure $BASE $CASE/commitfence.json'
run "$CASE" rules show TEAM012
t 1.8b "rules show TEAM012 has the new target" 'has $SO "target is System.DateTime.UtcNow"'
review_repo clock "$CASE"
t 1.8c "review: finding at line 12, none at line 7, exit 1" 'matches $SO "Reader.cs\(12,[0-9]+\): error TEAM012" && ! matches $SO "Reader.cs\(7," && [ $CODE = 1 ]'
run "$CASE" rules set TEAM012 --where target=System.DateTime.Now
t 2.7 "set back: file identical to base" 'same_file $BASE $CASE/commitfence.json'
run "$CASE" rules disable TEAM012
review_repo clock "$CASE"
t 1.9a "TEAM012 off: review passes, nothing about TEAM012" 'hasnt $SO "TEAM012" && [ $CODE = 0 ]'
run "$CASE" rules enable TEAM012
review_repo clock "$CASE"
t 1.9b "TEAM012 on again: review blocks" 'matches $SO "error TEAM012" && [ $CODE = 1 ]'

# a new rule, found by a review, and removed again
new_case c4
run "$CASE" rules add TEAM020 --for if-statement --title "An if with an else needs braces" --where hasElse=true --require braces=true --why "An else without braces is easy to misread."
t 1.10a "rules add TEAM020: exit 0" '[ $CODE = 0 ] && has $SO "Added rule TEAM020."'
run "$CASE" rules show TEAM020
t 1.10b "rules show TEAM020 shows where and require" 'matches $SO "where: +hasElse is true" && matches $SO "require: +braces is true" && has $SO "An else without braces"'
review_files "$CASE"
t 1.10c "review finds TEAM020 at Else.cs line 7, not in Three.cs" 'has $SO "src/Else.cs(7,9): error TEAM020" && ! matches $SO "Three.cs.*TEAM020"'
t 2.8 "add: nothing is removed, the new rule is only added after the last rule" '[ "$(difflines $BASE $CASE/commitfence.json | grep -c "^<")" = 0 ] && [ "$(difflines $BASE $CASE/commitfence.json | grep -c TEAM020)" = 1 ]'
t 2.9 "add: structure part identical" 'same_structure $BASE $CASE/commitfence.json'
run "$CASE" rules remove TEAM020
t 1.11a "rules remove TEAM020: exit 0, file identical to base" '[ $CODE = 0 ] && same_file $BASE $CASE/commitfence.json'
run "$CASE" rules list
t 1.11b "rules list has no TEAM020" 'hasnt $SO "TEAM020"'
review_files "$CASE"
t 1.11c "review has no TEAM020" 'hasnt $SO "TEAM020"'
t 2.10 "no leftover .new file" '[ -z "$(ls $CASE/commitfence.json.* 2>/dev/null)" ]'

new_case c5
run "$CASE" rules remove TEAM011
run "$CASE" rules list
t 1.12 "rules remove TEAM011: list has TEAM010 and TEAM012 only" '[ $CODE = 0 ] && has $SO "TEAM010" && has $SO "TEAM012" && hasnt $SO "TEAM011"'
t 2.11 "remove: structure part identical" 'same_structure $BASE $CASE/commitfence.json'
t 2.12 "remove: the first and third rule blocks unchanged" '[ "$(difflines $BASE $CASE/commitfence.json | grep -c "^>")" = 0 ]'

# ---------- case 3: refusals ----------
refused() { # id description expected-text args...
    local id=$1 text=$2 expected=$3
    shift 3
    new_case "refuse-$id"
    run "$CASE" "$@"
    t "3.$id" "$text" '[ $CODE = 3 ] && [ ! -s $SO ] && has $SE "$expected" && same_file $BASE $CASE/commitfence.json && [ -z "$(ls $CASE/commitfence.json.* 2>/dev/null)" ]'
}
refused 1a "unknown rule id (disable)" "no rule 'NOPE'" rules disable NOPE
refused 1b "unknown rule id (remove)" "no rule 'NOPE'" rules remove NOPE
refused 1c "unknown rule id (severity)" "no rule 'NOPE'" rules severity NOPE warn
refused 1d "unknown rule id (set)" "no rule 'NOPE'" rules set NOPE --title x
refused 1e "unknown rule id (try)" "no rule 'NOPE'" rules try NOPE src/Three.cs
refused 2 "unknown artifact type" "'banana' is not an artifact type" rules add X1 --for banana --title t --require braces=false
refused 3 "unknown property" "'colour' is not a property" rules add X1 --for if-statement --title t --require colour=red
refused 4a "wrong value type: boolean" "takes true or false" rules add X1 --for if-statement --title t --require braces=banana
refused 4b "wrong value type: number" "takes a whole number" rules add X1 --for if-statement --title t --where statementsInBody=many --require braces=true
refused 5 "rule id exists" "already used" rules add TEAM010 --for if-statement --title t --require braces=true
refused 6 "require and forbid together" "not both" rules add X1 --for if-statement --title t --require braces=true --forbid
refused 7 "set the type to one without the property" "is not a property of method" rules set TEAM010 --for method
refused 8 "neither require nor forbid" "needs either" rules add X1 --for if-statement --title t
refused 9 "options a command does not take" "does not take options" rules list --title x
refused 10 "unknown severity" "not a severity" rules severity TEAM010 loud
refused 11 "value not allowed for a property" "not a value of 'declaredAccess'" rules add X1 --for method --title t --require declaredAccess=sideways

refused 12a "set with --severity" "rules severity" rules set TEAM010 --severity block
refused 12b "set with --language" "language of a rule cannot be changed" rules set TEAM010 --language x
refused 13a "set with an empty --paths" "--paths needs at least one pattern" rules set TEAM010 --paths ,
refused 13b "set with an empty where value" "empty property or an empty value" rules set TEAM010 --where braces=
refused 13c "set with an empty require value" "empty property or an empty value" rules set TEAM010 --require braces=,
refused 14a "set with --where and --no-where" "cannot be given together" rules set TEAM010 --where braces=true --no-where
refused 14b "add with --no-paths" "does not take" rules add X1 --for if-statement --title t --require braces=true --no-paths

new_case c8
run "$CASE" rules set TEAM010 --no-paths
t 3.15 "set --no-paths is still accepted" '[ $CODE = 0 ]'

# a file named like the old temporary file survives; saves never share a temporary name
new_case c9
printf 'KEEP' > "$CASE/commitfence.json.new"
run "$CASE" rules disable TEAM010
t 3.16 "commitfence.json.new beside the settings file survives untouched, and nothing else is left" '[ $CODE = 0 ] && [ "$(cat $CASE/commitfence.json.new)" = KEEP ] && [ "$(ls $CASE/commitfence.json.* | wc -l)" = 1 ]'

new_case c10
RACE_PROBLEMS=""
for round in 1 2 3 4 5 6 7 8 9 10; do
    cp "$BASE" "$CASE/commitfence.json"
    ( cd "$CASE" && "$EXE" rules disable TEAM010 > "$WORK/race1.out" 2> "$WORK/race1.err" < /dev/null; echo $? > "$WORK/race1.code" ) &
    ( cd "$CASE" && "$EXE" rules severity TEAM011 warn > "$WORK/race2.out" 2> "$WORK/race2.err" < /dev/null; echo $? > "$WORK/race2.code" ) &
    wait
    [ "$(cat "$WORK/race1.code")$(cat "$WORK/race2.code")" = 00 ] || RACE_PROBLEMS="$RACE_PROBLEMS round $round: exit codes $(cat "$WORK/race1.code") and $(cat "$WORK/race2.code");"
    run "$CASE" rules list
    [ $CODE = 0 ] || RACE_PROBLEMS="$RACE_PROBLEMS round $round: the file does not read back;"
    grep -q '^TEAM010 .*(off)$' "$SO" || RACE_PROBLEMS="$RACE_PROBLEMS round $round: the edit of the first command is not in the file;"
    grep -Eq '^TEAM011  warn ' "$SO" || RACE_PROBLEMS="$RACE_PROBLEMS round $round: the edit of the second command is not in the file;"
    [ -z "$(ls $CASE/commitfence.json.* 2>/dev/null)" ] || RACE_PROBLEMS="$RACE_PROBLEMS round $round: a file is left beside the settings file;"
done
[ -z "$RACE_PROBLEMS" ] || echo "3.17 problems:$RACE_PROBLEMS"
t 3.17 "two changes at the same moment, ten repeats: both exit 0 and both edits are in the file every round, the file reads back, nothing left beside it" '[ -z "$RACE_PROBLEMS" ]'
t 3.18 "the lock file is in the scratch data folder, none beside the settings file" '[ "$(ls $WORK/data/settings-locks/*.lock 2>/dev/null | wc -l | tr -d " ")" -ge 1 ] && [ -z "$(ls $CASE/*.lock 2>/dev/null)" ]'

# ---------- case 4: rules try ----------
new_case c6
run "$CASE" rules try TEAM010 src/Three.cs
t 4.1 "try by file: three outcomes with line numbers" 'has $SO "src/Three.cs(7,9): breaks it: braces is true, the rule requires false" && has $SO "src/Three.cs(12,9): follows it" && has $SO "src/Three.cs(15,9): not covered: statementsInBody is 2, the rule looks only at 1" && has $SO "1 breaks it, 1 follows it, 1 not covered." && [ $CODE = 0 ]'
run_in "$CASE" "$CASE/src/Three.cs" rules try TEAM010
t 4.2 "try by standard input" 'has $SO "stdin(7,9): breaks it" && has $SO "stdin(12,9): follows it" && has $SO "stdin(15,9): not covered" && [ $CODE = 0 ]'
run_in "$CASE" "$CASE/src/Three.cs" rules try TEAM010 -
t 4.3 "try with - reads standard input" 'has $SO "stdin(7,9): breaks it" && [ $CODE = 0 ]'
echo "if(" > "$WORK/broken.cs"
run_in "$CASE" "$WORK/broken.cs" rules try TEAM010
t 4.4 "try on code that does not parse" 'has $SO "does not parse" && [ $CODE = 2 ]'
printf 'namespace N;\npublic class C { }\n' > "$WORK/noif.cs"
run_in "$CASE" "$WORK/noif.cs" rules try TEAM010
t 4.5 "try on code with no if" 'has $SO "No artifact of the rule" && has $SO "0 breaks it, 0 follows it, 0 not covered." && [ $CODE = 0 ]'
run "$CASE" rules try TEAM012 src/Three.cs
t 4.6 "TEAM012 cannot be tried without a build" 'has $SO "cannot be tried without a build" && [ $CODE = 2 ]'
run "$CASE" rules try TEAM011 src/Three.cs
t 4.7 "TEAM011 cannot be tried without a build" 'has $SO "cannot be tried without a build" && [ $CODE = 2 ]'
run "$CASE" rules try TEAM010 src/Missing.cs
t 4.8 "try on a file that is not there is refused" '[ $CODE = 3 ] && [ -s $SE ]'
same_file "$BASE" "$CASE/commitfence.json"
t 4.9 "try never writes the settings file" 'same_file $BASE $CASE/commitfence.json'

# ---------- case 5: what a command writes ----------
cat > "$WORK/strange.json" <<'EOF'
{"rules":[{"for":"if-statement","id":"X1","language":"csharp","severity":"warn","title":"T1","require":{"braces":false},"where":{"statementsInBody":1}, "why":"w", "enabled": true}],"requires":"0.1","structure":{"directions":{"forbid":["a -> b"],"allow":["b -> a"]},"modules":{"a":["namespace:A"],"b":["folder:src/B/**","project:B"]}}}
EOF
cat > "$WORK/expected-strange.json" <<'EOF'
{
    "requires": "0.1",
    "rules": [
        {
            "id": "X1",
            "language": "csharp",
            "for": "if-statement",
            "severity": "warn",
            "title": "T1",
            "where": { "statementsInBody": 1 },
            "require": { "braces": false },
            "why": "w"
        }
    ],
    "structure": {
        "modules": {
            "a": [ "namespace:A" ],
            "b": [ "folder:src/B/**", "project:B" ]
        },
        "directions": {
            "allow": [
                "b -> a"
            ],
            "forbid": [
                "a -> b"
            ]
        }
    }
}
EOF
mkdir -p "$WORK/strange"
cp "$WORK/strange.json" "$WORK/strange/commitfence.json"
run "$WORK/strange" rules list
t 5.1a "the strange file reads before the change" '[ $CODE = 0 ] && has $SO "X1"'
run "$WORK/strange" rules enable X1
t 5.2 "after a change the strange file is the stable layout, nothing lost" '[ $CODE = 0 ] && same_file $WORK/expected-strange.json $WORK/strange/commitfence.json'
run "$WORK/strange" rules list
t 5.1b "the rewritten file reads back through rules list" '[ $CODE = 0 ] && has $SO "X1"'

mkdir -p "$WORK/crlf"
sed 's/$/\r/' "$BASE" > "$WORK/crlf/commitfence.json"
run "$WORK/crlf" rules disable TEAM010
t 5.3 "windows line ends stay" '[ $CODE = 0 ] && [ "$(wc -l < $WORK/crlf/commitfence.json)" = "$(grep -c $'"'"'\r$'"'"' $WORK/crlf/commitfence.json)" ] && [ "$(tr -d "\r" < $WORK/crlf/commitfence.json | md5sum)" = "$(cd $WORK && mkdir -p lf && cp base.json lf/commitfence.json && cd lf && "$EXE" rules disable TEAM010 > /dev/null && md5sum < commitfence.json)" ]'

new_case c7
run "$CASE" rules add TEAM030 --for if-statement --title "A > B and é" --forbid
t 5.4 "a > and an accent are written as they are" '[ $CODE = 0 ] && has $CASE/commitfence.json "\"title\": \"A > B and é\"" && hasnt $CASE/commitfence.json "u003E"'
run "$CASE" rules list
t 5.5 "the file with the new rule reads back" '[ $CODE = 0 ] && has $SO "TEAM030"'

# ---------- case 6: the catalog ----------
run "$WORK" catalog
t 6.1 "catalog prints types and properties with value types" '[ $CODE = 0 ] && has $SO "csharp  C#" && has $SO "if-statement:" && has $SO "method:" && has $SO "class:" && has $SO "usage (needs the build):" && matches $SO "statementsInBody +number +reads the file" && matches $SO "braces +true or false +reads the file" && matches $SO "declaredAccess +text +needs the build" && matches $SO "target +text +needs the build"'
run "$WORK" catalog csharp method
t 6.2 "catalog csharp method prints only the method" '[ $CODE = 0 ] && has $SO "method:" && hasnt $SO "if-statement:" && has $SO "containingType"'
t 6.2b "the method has isOverride, true or false, read from the file" 'matches $SO "isOverride +true or false +reads the file +Whether the method is written with the override keyword."'
run "$WORK" catalog csharp banana
t 6.3 "catalog with an unknown type is refused" '[ $CODE = 3 ] && has $SE "not an artifact type" && [ ! -s $SO ]'
run "$WORK" catalog cobol
t 6.4 "catalog with an unknown language is refused" '[ $CODE = 3 ] && has $SE "not a language" && [ ! -s $SO ]'

# ---------- cases 8 to 11: a wrong settings file, with the exact place; path patterns (verdicts in expected.md) ----------
place_of() { # file needle: "line,column" of the first place the needle starts, counted from 1
    awk -v needle="$2" '{ at = index($0, needle); if (at > 0) { printf "%d,%d", NR, at; exit } }' "$1"
}

settings_case() { # name: a folder with a clean source and the settings file on standard input
    mkdir -p "$WORK/$1/src"
    printf 'namespace Sample;\n\npublic class A\n{\n    public int One()\n    {\n        return 1;\n    }\n}\n' > "$WORK/$1/src/A.cs"
    cat > "$WORK/$1/commitfence.json"
}

rule_with() { # extra text after the require line of the one rule
    cat <<EOF
{
    "rules": [
        {
            "id": "TEAM010",
            "language": "csharp",
            "for": "if-statement",
            "severity": "block",
            "title": "No braces around a single-line if",
            "where": { "statementsInBody": 1, "bodyFitsOnOneLine": true },
            "require": { "braces": false }$1
        }
    ]
}
EOF
}

refused_at() { # id description file needle message-text: exit 3, nothing on standard output, the message at the place of the needle
    local id=$1 text=$2 dir=$3 needle=$4 message=$5
    run "$dir" review --files src/A.cs --settings commitfence.json
    local place
    place=$(place_of "$dir/commitfence.json" "$needle")
    t "$id" "$text: exit 3, no output, $message at ($place)" '[ $CODE = 3 ] && [ ! -s $SO ] && [ -n "$place" ] && has $SE "commitfence.json($place): $message"'
}

printf '{\n    "rules": [\n        {\n            "id": "X1"\n            "language": "csharp"\n        }\n    ]\n}\n' | settings_case s8a
refused_at 8a "broken JSON" "$WORK/s8a" '"language"' "The file is not valid JSON"

printf '{\n    "rules": [],\n    "colour": 1\n}\n' | settings_case s8b
refused_at 8b "an unknown key at the root" "$WORK/s8b" '"colour"' "The key 'colour' is not known in the settings file."

rule_with ',
            "colour": "red"' | settings_case s8c
refused_at 8c "an unknown key in a rule" "$WORK/s8c" '"colour"' "The key 'colour' is not known in a rule."

printf '{\n    "rules": null\n}\n' | settings_case s9a
refused_at 9a "rules is null" "$WORK/s9a" 'null' "The value at 'rules' is null."

rule_with ',
            "paths": [ null ]' | settings_case s9b
refused_at 9b "a path is null" "$WORK/s9b" 'null' "The value at 'rules[0].paths[0]' is null."

printf '{\n    "requires": null,\n    "rules": []\n}\n' | settings_case s9c
refused_at 9c "requires is null" "$WORK/s9c" 'null' "The value at 'requires' is null."

printf '{\n    "rules": [],\n    "structure": null\n}\n' | settings_case s9d
refused_at 9d "structure is null" "$WORK/s9d" 'null' "The value at 'structure' is null."

printf '{\n    "rules": [\n        {\n            "id": "TEAM010",\n            "language": "csharp",\n            "for": "if-statement",\n            "severity": "block",\n            "title": "No braces around a single-line if",\n            "where": { "braces": null },\n            "require": { "braces": false }\n        }\n    ]\n}\n' | settings_case s9e
refused_at 9e "a condition is null" "$WORK/s9e" 'null' "The value at 'rules[0].where.braces' is null."

printf '{\n    "rules": [\n        5\n    ],\n    "rules[0]": 1\n}\n' | settings_case s10
refused_at 10 "the first element of rules has the wrong type, and a later key is named rules[0]" "$WORK/s10" '5' "The value of 'rules[0]' has the wrong type."

printf '{\n    "structure": {\n        "directions": 5\n    },\n    "structure.directions": 1\n}\n' | settings_case s10b
refused_at 10b "structure.directions has the wrong type, and a later key is named structure.directions" "$WORK/s10b" '"directions"' "The value of 'structure.directions' has the wrong type."

# path patterns: the whole path is matched
for_paths() { # the pattern text: three files with the same breach, the rule limited to the pattern
    PC=$WORK/p11
    rm -rf "$PC"
    mkdir -p "$PC/src/deep" "$PC/other"
    local file
    for file in src/A.cs src/deep/B.cs other/C.cs; do
        printf 'namespace Sample;\n\npublic class C%s\n{\n    public int Pick(int a)\n    {\n        if(a > 1) { return 1; }\n\n        return 0;\n    }\n}\n' "$RANDOM" > "$PC/$file"
    done
    rule_with ",
            \"paths\": [ \"$1\" ]" > "$PC/commitfence.json"
    run "$PC" review --files src/A.cs src/deep/B.cs other/C.cs --settings commitfence.json
}

for_paths 'src/**/*.cs'
t 11a "the pattern src/**/*.cs: findings in src/A.cs and src/deep/B.cs, none in other/C.cs" '[ $CODE = 1 ] && has $SO "src/A.cs(" && has $SO "src/deep/B.cs(" && hasnt $SO "other/C.cs("'
for_paths 'src/*.cs'
t 11a "the pattern src/*.cs: a finding only in src/A.cs (the whole path is matched)" '[ $CODE = 1 ] && has $SO "src/A.cs(" && hasnt $SO "src/deep/B.cs(" && hasnt $SO "other/C.cs("'

LINEBREAK_FOLDER=$WORK/p11b/"$(printf 'a\nb')"
case "$(uname -s)" in # Windows has no such folder name: Git Bash only makes it look as if it had, with another character in its place
    MINGW* | MSYS* | CYGWIN*) CAN_BREAK_LINES=no ;;
    *) CAN_BREAK_LINES=yes ;;
esac
if [ "$CAN_BREAK_LINES" = yes ] && mkdir -p "$LINEBREAK_FOLDER" 2> /dev/null && [ -d "$LINEBREAK_FOLDER" ]; then
    printf 'namespace Sample;\n\npublic class D\n{\n    public int Pick(int a)\n    {\n        if(a > 1) { return 1; }\n\n        return 0;\n    }\n}\n' > "$LINEBREAK_FOLDER/C.cs"
    rule_with ',
            "paths": [ "**/C.cs" ]' > "$WORK/p11b/commitfence.json"
    run "$WORK/p11b" review --files "$(printf 'a\nb')/C.cs" --settings commitfence.json
    t 11b "the pattern **/C.cs matches a file in a folder whose name holds a line break" '[ $CODE = 1 ] && has $SO "b/C.cs(" && has $SO "error TEAM010"'
else
    skip_case 11b "this machine cannot make a folder whose name holds a line break, so the case cannot be built here"
fi


# ---------- case 12: review --files says when conditional sections were not all read ----------
SC12=$WORK/s12
mkdir -p "$SC12/src"
rule_with "" > "$SC12/commitfence.json"
printf 'namespace Sample;\n\npublic class Cond\n{\n    public int Pick(int a)\n    {\n#if FEATURE\n        if(a > 1) { return 1; }\n#endif\n        return 0;\n    }\n}\n' > "$SC12/src/Cond.cs"
printf 'namespace Sample;\n\npublic class CondOff\n{\n    public int Pick(int a)\n    {\n        if(a > 1) { return 1; }\n        return 0;\n    }\n}\n' > "$SC12/src/CondOff.cs"
printf 'namespace Sample;\n\npublic class Plain\n{\n    public int One()\n    {\n        return 1;\n    }\n}\n' > "$SC12/src/Plain.cs"
printf 'namespace Sample;\n\npublic class Taken\n{\n    public int One()\n    {\n#if !FEATURE\n        return 1;\n#endif\n        return 2;\n    }\n}\n' > "$SC12/src/Taken.cs"
printf 'namespace Sample;\n\npublic class Other\n{\n    public int One()\n    {\n#if A\n        return 1;\n#else\n        return 2;\n#endif\n    }\n}\n' > "$SC12/src/Other.cs"

READ_IN_PART="read in part: code switched by a symbol was not read; a review with a build reads it"
run "$SC12" review --files src/Cond.cs --settings commitfence.json
t 12a "a breach inside #if FEATURE is not seen, so the file is read in part and the review is incomplete (exit 2)" '[ $CODE = 2 ] && hasnt $SO ": error " && has $SO "src/Cond.cs: incomplete: $READ_IN_PART" && has $SO "0 of 1 files reviewed"'
run "$SC12" review --files src/Plain.cs --settings commitfence.json
t 12b "a file without #if: exit 0, nothing read in part" '[ $CODE = 0 ] && hasnt $SO "read in part"'
run "$SC12" review --files src/Taken.cs --settings commitfence.json
t 12c "only #if !FEATURE, whose one branch is the one read: nothing is left unread, exit 0" '[ $CODE = 0 ] && hasnt $SO "read in part"'
run "$SC12" review --files src/Cond.cs src/Other.cs --settings commitfence.json
t 12d "two files: both are read in part, both named, exit 2" '[ $CODE = 2 ] && has $SO "src/Cond.cs: incomplete: $READ_IN_PART" && has $SO "src/Other.cs: incomplete: $READ_IN_PART" && has $SO "0 of 2 files reviewed"'
run "$SC12" review --files src/CondOff.cs --settings commitfence.json
t 12e "control: the same code as Cond.cs without #if is read whole: its breach is found, exit 1" '[ $CODE = 1 ] && has $SO "src/CondOff.cs(7,9): error TEAM010" && hasnt $SO "read in part"'
run "$SC12" review --files src/Cond.cs src/CondOff.cs --settings commitfence.json
t 12f "a blocking finding makes it 1 even when another file is read in part" '[ $CODE = 1 ] && has $SO "src/Cond.cs: incomplete: $READ_IN_PART" && has $SO "src/CondOff.cs(7,9): error TEAM010"'

# ---------- case 13: add at a place, copy, and code that is a member or a few statements ----------
new_case c13
C13=$CASE
cp "$C13/commitfence.json" "$C13/original.json"
TEAM010_ADD=(rules add TEAM010 --for if-statement --title "No braces around a single-line if" --where statementsInBody=1 --where bodyFitsOnOneLine=true --require braces=false)
run "$C13" rules remove TEAM010
run "$C13" "${TEAM010_ADD[@]}" --before TEAM011
t 13a "rules add --before puts the rule back before TEAM011: exit 0, the settings file is byte for byte what it was" '[ $CODE = 0 ] && has $SO "Added rule TEAM010 before TEAM011." && same_file "$C13/commitfence.json" "$C13/original.json"'
run "$C13" rules remove TEAM010
cp "$C13/commitfence.json" "$C13/removed.json"
run "$C13" "${TEAM010_ADD[@]}" --before NOPE
t 13b "--before an id that is not there: exit 3, one plain line, the file is unchanged" '[ $CODE = 3 ] && has $SE "The settings file has no rule '"'"'NOPE'"'"'." && same_file "$C13/commitfence.json" "$C13/removed.json"'
run "$C13" rules remove TEAM011 --before TEAM012
t 13c "rules remove takes no --before: exit 3, the usage" '[ $CODE = 3 ] && has $SE "rules remove does not take --before" && has $SE "Usage:"'
cp "$C13/original.json" "$C13/commitfence.json"
run "$C13" rules copy TEAM010 TEAM014
t 13d "rules copy: exit 0, the copy stands right after the rule, titled Copy of its title" '[ $CODE = 0 ] && has $SO "Copied rule TEAM010 as TEAM014."'
run "$C13" rules list
t 13d2 "rules list shows TEAM010, TEAM014, TEAM011, TEAM012, TEAM013 in that order and the title of the copy" '[ "$(grep -o "^TEAM01[0-9]" $SO | tr "\n" " ")" = "TEAM010 TEAM014 TEAM011 TEAM012 TEAM013 " ] && has $SO "Copy of No braces around a single-line if"'
run "$C13" rules show TEAM010
grep -v "^TEAM010\|^  title" "$SO" > "$C13/show10.txt"
run "$C13" rules show TEAM014
grep -v "^TEAM014\|^  title" "$SO" > "$C13/show14.txt"
t 13d3 "rules show of the rule and of the copy differ only in the id and the title" 'same_file "$C13/show10.txt" "$C13/show14.txt"'
cp "$C13/commitfence.json" "$C13/copied.json"
run "$C13" rules copy TEAM010 TEAM011
t 13e "rules copy to an id that exists: exit 3, the line rules add gives, the file is unchanged" '[ $CODE = 3 ] && has $SE "The rule id '"'"'TEAM011'"'"' is already used by an earlier rule." && same_file "$C13/commitfence.json" "$C13/copied.json"'
run "$C13" rules copy NOPE TEAM020
t 13f "rules copy of a rule that is not there: exit 3" '[ $CODE = 3 ] && has $SE "The settings file has no rule '"'"'NOPE'"'"'." && same_file "$C13/commitfence.json" "$C13/copied.json"'
run "$C13" rules copy TEAM010 "bad id"
t 13g "rules copy to an id with a space: exit 3, says it is not a rule id" '[ $CODE = 3 ] && has $SE "is not a rule id" && same_file "$C13/commitfence.json" "$C13/copied.json"'
run "$C13" rules copy TEAM010 ""
t 13g2 "rules copy to an empty id: exit 3, says it is not a rule id" '[ $CODE = 3 ] && has $SE "is not a rule id"'
run "$C13" rules copy TEAM010
t 13h "rules copy without the new id: exit 3, says what it takes, and the usage" '[ $CODE = 3 ] && has $SE "rules copy takes the id of the rule to copy and the new rule id." && has $SE "Usage:"'
run "$C13" --help
t 13i "the usage names rules copy and --before" 'has $SO "commitfence rules copy <id> <new id>" || has $SE "commitfence rules copy <id> <new id>"'
run "$C13" --help
t 13i2 "the usage names --before" 'has $SO "[--before <id>]" || has $SE "[--before <id>]"'
cat > "$C13/sample.cs" <<'EOF'
public void Send(Invoice invoice)
{
    if(invoice == null)
    {
        return;
    }

    if(invoice.Lines.Count == 0)
        return;

    if(invoice.Total < 0)
    {
        log.Warn("Negative total");
        invoice.Total = 0;
    }
}
EOF
run "$C13" rules try TEAM010 sample.cs
t 13j "the design's sample, with public, verbatim: three verdicts on lines 3, 8 and 11" '[ $CODE = 0 ] && has $SO "sample.cs(3,5): breaks it" && has $SO "sample.cs(8,5): follows it" && has $SO "sample.cs(11,5): not covered" && has $SO "1 breaks it, 1 follows it, 1 not covered." && hasnt $SO "does not parse"'
printf 'if(x) { return; }\n' > "$C13/lone.cs"
run "$C13" rules try TEAM010 lone.cs
t 13k "a lone if statement: one verdict at (1,1), breaks it" '[ $CODE = 0 ] && has $SO "lone.cs(1,1): breaks it" && has $SO "1 breaks it, 0 follows it, 0 not covered."'
printf '\n  if(x)\n  {\n      return;\n  }\n  return;\n' > "$C13/two.cs"
run "$C13" rules try TEAM010 two.cs
t 13l "two statements: the if at its own line and column (2,3)" '[ $CODE = 0 ] && has $SO "two.cs(2,3): breaks it" && has $SO "1 breaks it, 0 follows it, 0 not covered."'
printf 'public int Size { get; set; }\n\npublic void Go(int a)\n{\n    if(a > 1) { return; }\n}\n' > "$C13/members.cs"
run "$C13" rules try TEAM010 members.cs
t 13m "members of a type with no type around them: the if at (5,5)" '[ $CODE = 0 ] && has $SO "members.cs(5,5): breaks it"'
printf 'namespace Sample;\n\npublic class A\n{\n    public void Go(int a)\n    {\n        if(a > 1) { return; }\n    }\n}\n' > "$C13/whole.cs"
run "$C13" rules try TEAM010 whole.cs
t 13n "a whole file with a namespace: the if at (7,9)" '[ $CODE = 0 ] && has $SO "whole.cs(7,9): breaks it"'
printf 'this is not code at all {{\n' > "$C13/nocode.cs"
run "$C13" rules try TEAM010 nocode.cs
t 13o "text that is no C#: exit 2, does not parse, with a line" '[ $CODE = 2 ] && has $SO "nocode.cs(1," && has $SO "does not parse"'
printf 'void A() { if(x) { } ' > "$C13/open.cs"
run "$C13" rules try TEAM010 open.cs
t 13o2 "a method that is not closed: does not parse at (1,22), } expected" '[ $CODE = 2 ] && has $SO "open.cs(1,22): does not parse: } expected"'
run_in "$C13" "$C13/sample.cs" rules try TEAM010
t 13p "the sample on standard input: the same three verdicts" '[ $CODE = 0 ] && has $SO "stdin(3,5): breaks it" && has $SO "stdin(8,5): follows it" && has $SO "stdin(11,5): not covered"'
cp "$C13/original.json" "$C13/commitfence.json"
run "$C13" rules disable TEAM012
run "$C13" rules copy TEAM012 TEAM015
t 13q "rules copy of a rule that is off: exit 0, the copy is off too" '[ $CODE = 0 ] && has $SO "Copied rule TEAM012 as TEAM015."'
run "$C13" rules show TEAM015
t 13q2 "rules show of the copy says off" 'matches $SO "status: +off"'
printf 'class A\n{\n    protected override void OnKeyDown(int key)\n    {\n    }\n\n    protected void Helper()\n    {\n    }\n}\n' > "$C13/overrides.cs"
run "$C13" rules add TEAM016 --for method --title "Public methods" --where isOverride=false --require writtenAccess=public
run "$C13" rules try TEAM016 overrides.cs
t 13t "a rule with isOverride=false: the override is not covered, the helper breaks it" '[ $CODE = 0 ] && has $SO "overrides.cs(3,5): not covered: isOverride is true, the rule looks only at false" && has $SO "overrides.cs(7,5): breaks it" && has $SO "1 breaks it, 0 follows it, 1 not covered."'
cp "$C13/commitfence.json" "$C13/copied2.json"
run "$C13" rules add "bad id" --for if-statement --title T --require braces=false
t 13r "rules add with an id that has a space: exit 3, the line copy gives, the file is unchanged" '[ $CODE = 3 ] && has $SE "is not a rule id. An id is letters, digits" && same_file "$C13/commitfence.json" "$C13/copied2.json"'
ai-py -I -c "import sys; p=sys.argv[1]; s=open(p,encoding='utf-8').read(); open(p,'w',encoding='utf-8').write(s.replace('\"id\": \"TEAM013\"','\"id\": \"odd id\"',1))" "$C13/commitfence.json"
run "$C13" rules list
t 13s "a settings file that already holds the id odd id still lists" '[ $CODE = 0 ] && has $SO "odd id"'

# ---------- the project reviews itself ----------
SELF_FILES=$(cd "$PROJECT" && find Dependencies App -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' | sort)
( cd "$PROJECT" && "$EXE" review --files $SELF_FILES > "$SO" 2> "$SE" < /dev/null )
CODE=$?
t 7.1 "review --files of CommitFence's own files: no finding, only the build rules not run" 'hasnt $SO ": error " && hasnt $SO ": warning " && [ $CODE != 1 ]'
t 7.2 "the root commitfence.json is unchanged by this script" '[ "$(md5sum < $PROJECT/commitfence.json)" = "$(md5sum < $BASE)" ]'

finish_script
