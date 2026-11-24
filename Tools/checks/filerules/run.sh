#!/bin/bash
# The rules of .editorconfig on both faces: rules list and rules show list them with titles from the installed SDK, the
# commands that change a rule refuse on them, and what the list says about a rule is what the review does with a breach of
# it, proved with real builds. Prints PASS, FAIL or SKIP per check. Expected results are in expected.md
# The repository's own .editorconfig is only ever copied.
# Usage: Tools/checks/filerules/run.sh <path of the commitfence program> [parent folder for the scratch folder] [case ids...]
# Safe: everything happens in a fresh folder that this script makes with mktemp -d under the parent folder (default: the
# system's temp folder), removed when every check passed. GIT_CONFIG_GLOBAL, COMMITFENCE_DATA_FOLDER and
# COMMITFENCE_USER_PATH_FILE point into it, and the user's git settings, PATH and CommitFence data are never touched (the
# one case without dotnet sets PATH for the one child process only). Needs bash, git, sed, awk and the .NET SDK, and no
# network. Nothing outside this repository and the scratch folder is read, except the SDK's own config files.

set -u

if [ $# -lt 1 ] || [ ! -f "$1" ]; then
    echo "usage: run.sh <path of the commitfence program> [parent folder for the scratch folder] [case ids...]" >&2
    exit 64
fi

native() { if command -v cygpath > /dev/null 2>&1; then cygpath -m "$1"; else echo "$1"; fi; }

stop_now() { echo "run.sh: $1" >&2; exit 70; }

HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPOSITORY=$(cd "$HERE/../../.." && pwd)
EXE=$(native "$(cd "$(dirname "$1")" && pwd)/$(basename "$1")")
PARENT=${2:-${TMPDIR:-/tmp}}
mkdir -p "$PARENT" || stop_now "cannot create the parent folder $PARENT"
ROOT=$(mktemp -d "$PARENT/commitfence-filerules.XXXXXX") || stop_now "cannot create a scratch folder under $PARENT"
ROOT=$(cd "$ROOT" && pwd) || stop_now "cannot enter the scratch folder"
if [ -z "$ROOT" ] || [ "$ROOT" = "/" ] || [ ! -d "$ROOT" ]; then
    stop_now "the scratch folder path is not usable: '$ROOT'"
fi

STARTED=$SECONDS
WORK=$ROOT/work
mkdir -p "$WORK/data"
WORK_WIN=$(native "$WORK")
REPO_EDITORCONFIG=$REPOSITORY/.editorconfig
OWNER_JSON=$REPOSITORY/commitfence.json
[ -f "$REPO_EDITORCONFIG" ] || stop_now "the repository's .editorconfig is not at $REPO_EDITORCONFIG"

export GIT_CONFIG_GLOBAL="$WORK_WIN/gitconfig"
export GIT_CONFIG_NOSYSTEM=1
export COMMITFENCE_DATA_FOLDER="$WORK_WIN/data"
export COMMITFENCE_USER_PATH_FILE="$WORK_WIN/userpath.txt"
export GIT_TERMINAL_PROMPT=0
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
printf '[user]\n\tname = Check Runner\n\temail = check@example.invalid\n[init]\n\tdefaultBranch = main\n[core]\n\tautocrlf = false\n' > "$WORK/gitconfig"

PASSES=0
FAILS=0
SKIPS=0

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

skip_case() { # case reason: a case that cannot be built on this machine; it is printed and counted
    SKIPS=$((SKIPS + 1))
    echo "SKIP  $1: $2"
}

report() { # status id description
    if [ "$1" = "PASS" ]; then PASSES=$((PASSES + 1)); else FAILS=$((FAILS + 1)); fi
    echo "$1  $2: $3"
}

t() { # id description condition (evaluated)
    local id=$1 text=$2
    shift 2
    if eval "$@"; then report PASS "$id" "$text"; else report FAIL "$id" "$text"; fi
}

SO=$WORK/stdout.txt
SE=$WORK/stderr.txt
CODE=0

has() { grep -Fq -- "$2" "$1"; }
hasnt() { ! grep -Fq -- "$2" "$1"; }
matches() { grep -Eq -- "$2" "$1"; }
equals() { [ "$1" = "$2" ]; }

clean_output() { sed -i 's/\r$//' "$SO" "$SE"; }

run() { # folder args...   (standard input is empty)
    local folder=$1
    shift
    ( cd "$folder" && "$EXE" "$@" > "$SO" 2> "$SE" < /dev/null )
    CODE=$?
    clean_output
}

run_without_dotnet() { # folder args...   a PATH that holds no dotnet, for this one child process only
    local folder=$1
    shift
    mkdir -p "$WORK/emptypath"
    ( cd "$folder" && PATH="$(native "$WORK/emptypath")" "$EXE" "$@" > "$SO" 2> "$SE" < /dev/null )
    CODE=$?
    clean_output
}

listed() { # file: "id state effect line" for each rule line of a list, from the program's own output
    sed -nE 's/^([A-Za-z0-9_]+) +(on|off|default|not understood) +((blocks|warns) )?.*line ([0-9]+)$/\1 \2 \4 \5/p' "$1" | tr -s ' '
}

oracle() { # editorconfig: "id state effect line" computed with grep and sed alone (no code of the product)
    grep -nE '^dotnet_diagnostic\.' "$1" | tr -d '\r' | sed -E 's/^([0-9]+):dotnet_diagnostic\.([A-Za-z0-9_]+)\.severity *= *([A-Za-z]+).*/\1 \2 \3/' |
        awk '{ w = tolower($3); if (w == "error") s = "on blocks"; else if (w == "warning" || w == "suggestion") s = "on warns"; else s = "off"; print $2, s, $1 }' | tr -s ' '
}

line_of() { grep -nF -- "$2" "$1" | head -1 | cut -d: -f1; }

new_folder() { # name editorconfig-text: a folder with an empty commitfence.json and the text as its .editorconfig
    FOLDER=$WORK/$1
    mkdir -p "$FOLDER"
    printf '{\n    "rules": []\n}\n' > "$FOLDER/commitfence.json"
    if [ $# -ge 2 ]; then printf '%s\n' "$2" > "$FOLDER/.editorconfig"; fi
}

wanted() { [ -z "${SELECTED:-}" ] || [[ " $SELECTED " == *" $1 "* ]]; }
shift
if [ $# -gt 0 ]; then shift; fi
SELECTED="$*"

HAS_DOTNET=0
if dotnet --version > /dev/null 2>&1; then HAS_DOTNET=1; fi

# ---------- case 1: the repository's file ----------
if wanted 1; then
    FOLDER1=$WORK/c1
    mkdir -p "$FOLDER1"
    cp "$REPO_EDITORCONFIG" "$FOLDER1/.editorconfig"
    cp "$OWNER_JSON" "$FOLDER1/commitfence.json"
    run "$FOLDER1" rules list
    cp "$SO" "$WORK/c1-with.txt"
    t 1.1 "the repository's file: exit 0 and the heading names the file and 62 rules" '[ $CODE = 0 ] && has $SO ".editorconfig: 62 rules"'
    t 1.2 "62 rule lines are printed" 'equals "$(listed $SO | wc -l | tr -d " ")" 62'
    oracle "$REPO_EDITORCONFIG" | LC_ALL=C sort -s -k1,1 > "$WORK/c1-expected.txt"
    listed "$SO" > "$WORK/c1-actual.txt"
    t 1.3 "the script's own count from the file is 62" 'equals "$(wc -l < $WORK/c1-expected.txt | tr -d " ")" 62'
    t 1.4 "for every rule the id, on or off, blocks or warns and the line equal what grep and sed find in the file, in the order of the ids (ordinal)" 'cmp -s $WORK/c1-expected.txt $WORK/c1-actual.txt'
    t 1.5 "the team's own lines before the heading are the same as without the file" '
        mkdir -p $WORK/c1b && cp "$OWNER_JSON" $WORK/c1b/commitfence.json && run $WORK/c1b rules list &&
        head -n $(( $(grep -n "^\.editorconfig:" $WORK/c1-with.txt | cut -d: -f1) - 1 )) $WORK/c1-with.txt > $WORK/c1-team-with.txt &&
        sed "\$d" $SO > $WORK/c1-team-without.txt && cmp -s $WORK/c1-team-with.txt $WORK/c1-team-without.txt && has $WORK/c1-team-with.txt TEAM010'
    t 1.6 "without the file the output ends with one plain line that says there is none" 'has $SO "There is no .editorconfig in this repository." && hasnt $SO ".editorconfig: "'
fi

# ---------- case 2: comments ----------
if wanted 2; then
    new_folder c2 'root = true

[*.cs]
dotnet_diagnostic.ZZ1001.severity = error # ZZ1001: Trailing words.
# Words above
dotnet_diagnostic.ZZ1002.severity = warning
# ZZ1003: Words above three
dotnet_diagnostic.ZZ1003.severity = none ; Trailing three
dotnet_diagnostic.ZZ1004.severity = silent
# custom words for an SDK id
dotnet_diagnostic.CA1000.severity = warning # custom trailing words'
    run "$FOLDER" rules list
    t 2.1 "a comment after the value is the title; the leading id and the closing full stop are not part of it" 'matches $SO "^ZZ1001 +on +blocks +Trailing words  line 4$"'
    t 2.2 "a comment on the line above is the title" 'matches $SO "^ZZ1002 +on +warns +Words above  line 6$"'
    t 2.3 "both: the one after the value wins" 'matches $SO "^ZZ1003 +off +Trailing three  line 8$"'
    t 2.4 "neither: the id alone, no title" 'matches $SO "^ZZ1004 +off +line 9$"'
    run "$FOLDER" rules show ZZ1001
    t 2.5 "the setting shown has no comment in it" 'has $SO "setting:  '"'"'error'"'"'"'
    if [ "$HAS_DOTNET" = 1 ]; then
        run "$FOLDER" rules list
        t 2.6 "an id the SDK knows keeps the SDK's title, not the file's comment" 'has $SO "CA1000  on  warns   Do not declare static members on generic types  line 11" || matches $SO "^CA1000 +on +warns +Do not declare static members on generic types  line 11$"'
    else
        skip_case 2.6 "no dotnet on this machine"
    fi
fi

# ---------- case 3: sections ----------
if wanted 3; then
    TEXT3='root = true
[*.vb]
dotnet_diagnostic.ZZ2001.severity = error
[*.{cs,vb}]
dotnet_diagnostic.ZZ2002.severity = warning
[*]
dotnet_diagnostic.ZZ2003.severity = error
[*.cs]
dotnet_diagnostic.ZZ2004.severity = error
[*]
dotnet_diagnostic.ZZ2004.severity = none
dotnet_diagnostic.ZZ2005.severity = error
dotnet_diagnostic.ZZ2005.severity = warning
[src/**.cs]
dotnet_diagnostic.ZZ2006.severity = error
[tests/**.cs]
dotnet_diagnostic.ZZ2007.severity = error'
    new_folder c3 "$TEXT3"
    F3=$FOLDER/.editorconfig
    run "$FOLDER" rules list
    printf 'ZZ2002 on warns %s\nZZ2003 on blocks %s\nZZ2004 off %s\nZZ2005 on warns %s\n' "$(line_of $F3 'ZZ2002.severity')" "$(line_of $F3 'ZZ2003.severity')" "$(line_of $F3 'ZZ2004.severity = none')" "$(line_of $F3 'ZZ2005.severity = warning')" | tr -s ' ' > "$WORK/c3-expected.txt"
    listed "$SO" > "$WORK/c3-actual.txt"
    t 3.1 "[*.vb] is not listed; [*.{cs,vb}] and [*] are; the later section wins an id; the later line wins in one section; each with its own line" 'cmp -s $WORK/c3-expected.txt $WORK/c3-actual.txt'
    t 3.2 "the heading says 4 rules and the src and tests sections are not listed when the folder has no such C# files" 'has $SO ".editorconfig: 4 rules" && hasnt $SO ZZ2006 && hasnt $SO ZZ2007'
    mkdir -p "$FOLDER/src" && printf 'namespace S;\npublic class X\n{\n}\n' > "$FOLDER/src/x.cs"
    run "$FOLDER" rules list
    t 3.3 "with src/x.cs in the folder the section [src/**.cs] is listed and [tests/**.cs] still is not" 'matches $SO "^ZZ2006 +on +blocks .*line $(line_of $F3 ZZ2006.severity)$" && hasnt $SO ZZ2007'
fi

# ---------- case 4: case and spacing ----------
if wanted 4; then
    new_folder c4 '[*.cs]
DOTNET_DIAGNOSTIC.ca1050.SEVERITY=Error
dotnet_diagnostic.CA1051.severity   =   Warning
dotnet_diagnostic.CA1052.severity: none'
    run "$FOLDER" rules list
    listed "$SO" > "$WORK/c4-actual.txt"
    printf '%s\n' 'CA1051 on warns 3' 'CA1052 off 4' 'ca1050 on blocks 2' | tr -s ' ' > "$WORK/c4-expected.txt"
    t 4.1 "keys and words in any case, spaces around the separator and a colon are read, the id as the file writes it, listed in the order of the ids (plain character codes)" 'cmp -s $WORK/c4-expected.txt $WORK/c4-actual.txt'
    if [ "$HAS_DOTNET" = 1 ]; then
        t 4.2 "the title of ca1050 comes from the SDK although the case differs" 'matches $SO "^ca1050 +on +blocks +Declare types in namespaces  line 2$"'
    else
        skip_case 4.2 "no dotnet on this machine"
    fi
fi

# ---------- case 5: words ----------
if wanted 5; then
    new_folder c5 '[*.cs]
dotnet_diagnostic.ZZ5001.severity = bogus
dotnet_diagnostic.ZZ5002.severity =
dotnet_diagnostic.ZZ5003.severity = default'
    run "$FOLDER" rules list
    t 5.1 "a word the compiler does not know is shown as written and marked not understood, with no effect" 'matches $SO "^ZZ5001 +not understood +setting '"'"'bogus'"'"', line 2$"'
    t 5.2 "an empty value is not understood and shown as empty" 'matches $SO "^ZZ5002 +not understood +setting '"''"', line 3$"'
    t 5.3 "default is shown as default, without blocks or warns" 'matches $SO "^ZZ5003 +default +line 4$" && [ $CODE = 0 ]'
fi

# ---------- case 6: no file, empty, unreadable ----------
if wanted 6; then
    new_folder c6a
    run "$FOLDER" rules list
    t 6.1 "no .editorconfig: exit 0, one plain line, no heading" '[ $CODE = 0 ] && has $SO "There is no .editorconfig in this repository." && hasnt $SO ".editorconfig: "'
    new_folder c6b ''
    : > "$FOLDER/.editorconfig"
    run "$FOLDER" rules list
    t 6.2 "an empty .editorconfig: exit 0 and the heading says no rules set" '[ $CODE = 0 ] && has $SO ".editorconfig: no rules set"'
    new_folder c6c
    mkdir "$FOLDER/.editorconfig"
    run "$FOLDER" rules list
    t 6.3 "a folder named .editorconfig: exit 3, a plain message, nothing on standard output" '[ $CODE = 3 ] && has $SE "cannot be read" && [ ! -s $SO ]'
    run "$FOLDER" rules show CA1000
    t 6.4 "rules show on it: exit 3 and the same plain message" '[ $CODE = 3 ] && has $SE "cannot be read"'
fi

# ---------- case 7: titles from the machine ----------
if wanted 7; then
    FOLDER7=$WORK/c7
    mkdir -p "$FOLDER7"
    cp "$REPO_EDITORCONFIG" "$FOLDER7/.editorconfig"
    cp "$OWNER_JSON" "$FOLDER7/commitfence.json"
    if [ "$HAS_DOTNET" = 1 ]; then
        SDKVER=$(cd "$FOLDER7" && dotnet --version | tr -d '\r')
        SDKBASE=$(dotnet --list-sdks | tr -d '\r' | sed -nE "s/^$SDKVER \[(.*)\]$/\1/p")
        SDKDIR=$(cygpath -u "$SDKBASE")/$SDKVER
        CFG=$SDKDIR/Sdks/Microsoft.NET.Sdk/analyzers/build/config
        NEWEST=$(ls "$CFG" | grep -E '^analysislevel_[0-9]+_all\.globalconfig$' | sed -E 's/^analysislevel_([0-9]+)_all.*/\1 &/' | sort -n | tail -1 | cut -d' ' -f2)
        STYLE=$SDKDIR/Sdks/Microsoft.NET.Sdk/codestyle/cs/build/config/analysislevelstyle_all.globalconfig
        sdk_title() { grep -m1 -E "^# $1: " "$2" | tr -d '\r' | sed -E "s/^# $1: *//; s/[ .]+$//"; }
        CA_TITLE=$(sdk_title CA1000 "$CFG/$NEWEST")
        IDE_TITLE=$(sdk_title IDE0005 "$STYLE")
        t 7.0 "precondition: the script read three titles' sources from the SDK's own files" '[ -n "$CA_TITLE" ] && [ -n "$IDE_TITLE" ] && [ -z "$(grep -h -E "^# CS0006: " "$CFG/$NEWEST" "$STYLE")" ]'
        run "$FOLDER7" rules list
        t 7.1 "CA1000: the title equals the SDK's own comment" 'matches $SO "^CA1000 +off +$CA_TITLE  line [0-9]+$"'
        t 7.2 "IDE0005: the title equals the SDK's own comment" 'matches $SO "^IDE0005 +on +warns +$IDE_TITLE  line [0-9]+$"'
        CS_COMMENT=$(sed -n "$(( $(line_of "$FOLDER7/.editorconfig" 'dotnet_diagnostic.CS0006.severity') - 1 ))p" "$FOLDER7/.editorconfig" | tr -d '\r' | sed -E 's/^# *//; s/[ .]+$//')
        t 7.3 "CS0006: the SDK has no title, so the title is the file's own comment" '[ -n "$CS_COMMENT" ] && matches $SO "^CS0006 +on +warns +$CS_COMMENT  line [0-9]+$"'
    else
        skip_case 7.0 "no dotnet on this machine"
    fi
    run_without_dotnet "$FOLDER7" rules list
    cp "$SO" "$WORK/c7-nodotnet.txt"
    t 7.4 "without dotnet: exit 0 and all 62 rules are listed" '[ $CODE = 0 ] && equals "$(listed $SO | wc -l | tr -d " ")" 62'
    t 7.5 "without dotnet: CA1000 shows the file's own trailing comment" 'matches $SO "^CA1000 +off +Do not declare static members on generic types  line [0-9]+$"'
    t 7.6 "without dotnet: IDE0005 shows the comment above its line" 'matches $SO "^IDE0005 +on +warns +This is some bug in net so remove this error  line [0-9]+$"'
    t 7.7 "without dotnet: an id with no comment shows the id alone" 'matches $SO "^IDE0130 +off +line [0-9]+$"'
    t 7.8 "without dotnet: nothing on either stream calls the machine unready" '! grep -Eiq "machine|ready|SDK|dotnet" $SO $SE && [ ! -s $SE ]'
fi

# ---------- case 8: show and the refusals ----------
if wanted 8; then
    FOLDER8=$WORK/c8
    mkdir -p "$FOLDER8"
    cp "$REPO_EDITORCONFIG" "$FOLDER8/.editorconfig"
    cp "$OWNER_JSON" "$FOLDER8/commitfence.json"
    LINE8=$(line_of "$FOLDER8/.editorconfig" 'dotnet_diagnostic.CA1000.severity')
    BEFORE8=$(cat "$FOLDER8/.editorconfig" "$FOLDER8/commitfence.json" | md5sum)
    run "$FOLDER8" rules show CA1000
    t 8.1 "rules show on a rule of the file: exit 0, the setting, the file and the line, and that it is set in .editorconfig" '[ $CODE = 0 ] && has $SO "setting:  '"'"'none'"'"'" && has $SO "file:     .editorconfig, line $LINE8" && has $SO "set in .editorconfig"'
    t 8.2 "rules show says it is off and what the title is (when the machine gives one, or the file's comment)" 'has $SO "status:   off" && has $SO "Do not declare static members on generic types"'
    run "$FOLDER8" rules show ca1000
    t 8.3 "the id is found whatever its case" '[ $CODE = 0 ] && has $SO "line $LINE8"'
    refusal() { # id description args...
        local id=$1 text=$2
        shift 2
        run "$FOLDER8" rules "$@"
        t "8.$id" "$text: exit 3, one line naming the id, the file and the line, nothing on standard output" '[ $CODE = 3 ] && [ "$(wc -l < $SE | tr -d " ")" = 1 ] && has $SE "CA1000 is set in .editorconfig, line $LINE8; the product does not change that file." && [ ! -s $SO ]'
    }
    refusal 4 "rules enable" enable CA1000
    refusal 5 "rules disable" disable CA1000
    refusal 6 "rules severity" severity CA1000 warn
    refusal 7 "rules set" set CA1000 --title Other
    refusal 8 "rules remove" remove CA1000
    refusal 9 "rules try" try CA1000
    refusal 13 "rules copy" copy CA1000 TEAM099
    run "$FOLDER8" rules enable NOSUCH1
    t 8.10 "an id that is neither a team rule nor a rule of the file: as today, exit 3 and the file has no such rule" '[ $CODE = 3 ] && has $SE "has no rule '"'"'NOSUCH1'"'"'"'
    t 8.11 "the .editorconfig and commitfence.json are byte for byte unchanged" 'equals "$(cat $FOLDER8/.editorconfig $FOLDER8/commitfence.json | md5sum)" "$BEFORE8"'
    t 8.12 "the repository's .editorconfig of the repository is unchanged by this script (it was only copied)" 'cmp -s "$REPO_EDITORCONFIG" "$FOLDER8/.editorconfig"'
fi

# ---------- case 9: the tie to the review, with real builds ----------
make_project() { # name editorconfig-line: a repository whose second commit declares a type outside a namespace
    PROJECT=$WORK/$1
    mkdir -p "$PROJECT"
    ( cd "$PROJECT" &&
        git init -q -b main &&
        printf '<Project Sdk="Microsoft.NET.Sdk">\n  <PropertyGroup>\n    <OutputType>Library</OutputType>\n    <TargetFramework>net11.0</TargetFramework>\n    <ImplicitUsings>enable</ImplicitUsings>\n  </PropertyGroup>\n</Project>\n' > Lib.csproj &&
        printf 'namespace Sample;\n\npublic class Ok\n{\n}\n' > Ok.cs &&
        printf '{\n    "rules": []\n}\n' > commitfence.json &&
        printf 'root = true\n\n[*.cs]\n%s\n' "$2" > .editorconfig &&
        git add -A && git commit -q -m "one" &&
        printf 'public class Bad\n{\n}\n' > Bad.cs &&
        git add -A && git commit -q -m "two" )
}

list_effect() { # folder: what rules list says for CA1050: blocks, warns or nothing (off)
    run "$1" rules list
    case "$(listed "$SO" | grep -i '^ca1050 ')" in
        *" on blocks "*) echo blocks ;;
        *" on warns "*) echo warns ;;
        *" off "*) echo nothing ;;
        *) echo "unknown" ;;
    esac
}

review_effect() { # folder: what the review does with the breach: blocks, warns or nothing
    run "$1" review --base HEAD~1 --head HEAD
    if [ $CODE = 1 ] && has "$SO" "error CA1050"; then echo blocks
    elif [ $CODE = 0 ] && has "$SO" "warning CA1050"; then echo warns
    elif [ $CODE = 0 ] && hasnt "$SO" "CA1050"; then echo nothing
    else echo "other (exit $CODE)"
    fi
}

tie() { # id line expected-effect
    local listed_effect reviewed_effect expected_effect=$3
    make_project "c9-$1" "$2"
    listed_effect=$(list_effect "$PROJECT")
    reviewed_effect=$(review_effect "$PROJECT")
    t "9.$1" "[$2]: the list says $listed_effect, the review does $reviewed_effect (both must be $3)" '[ "$listed_effect" = "$expected_effect" ] && [ "$reviewed_effect" = "$expected_effect" ]'
}

if wanted 9; then
    if [ "$HAS_DOTNET" = 1 ]; then
        tie 1 'dotnet_diagnostic.CA1050.severity = error' blocks
        tie 2 'dotnet_diagnostic.CA1050.severity = warning' warns
        tie 3 'dotnet_diagnostic.CA1050.severity = suggestion' warns
        tie 4 'dotnet_diagnostic.CA1050.severity = silent' nothing
        tie 5 'dotnet_diagnostic.CA1050.severity = none' nothing
        tie 6 'dotnet_diagnostic.CA1050.severity = error # a comment after the value' blocks
        tie 7 'dotnet_diagnostic.CA1050.severity = warning ; a comment after the value' warns
        tie 8 'DOTNET_DIAGNOSTIC.ca1050.SEVERITY=Error' blocks
    else
        skip_case 9 "no dotnet on this machine: the builds cannot be run"
    fi
fi

# ---------- case 10: sub-folders ----------
if wanted 10; then
    new_folder c10 '[*.cs]
dotnet_diagnostic.ZZ1001.severity = error'
    run "$FOLDER" rules list
    t 10.1 "no .editorconfig in a sub-folder: no line about sub-folders" 'hasnt $SO "sub-folders"'
    mkdir -p "$FOLDER/sub" && printf '[*.cs]\ndotnet_diagnostic.ZZ9999.severity = error\n' > "$FOLDER/sub/.editorconfig"
    run "$FOLDER" rules list
    t 10.2 "one in a sub-folder: the one line says so, and its rules are not listed" 'has $SO "1 .editorconfig file in sub-folders is not read." && hasnt $SO ZZ9999 && has $SO ZZ1001'
    mkdir -p "$FOLDER/other/deeper" && printf '[*.cs]\n' > "$FOLDER/other/deeper/.editorconfig"
    run "$FOLDER" rules list
    t 10.3 "two in sub-folders: the line counts them" 'has $SO "2 .editorconfig files in sub-folders are not read."'
fi

finish_script
