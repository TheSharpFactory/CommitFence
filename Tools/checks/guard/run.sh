#!/bin/bash
# The guard of the guard: shows that Tools/smoke.sh fails when the program is wrong. It builds three stand-ins for the
# program, shell scripts that always exit 0, always 1 and always 2, runs smoke against each and checks that smoke itself
# ends non-zero and that named cases print FAIL. Expected results are in expected.md.
# Usage: Tools/checks/guard/run.sh <path of the commitfence program> [parent folder for the scratch folder]
# The program given is not run by the checks: it only has to exist, so that the script has the same arguments as the others.
# Safe: everything happens in a fresh folder that this script makes with mktemp -d under the parent folder (default: the
# system's temp folder), removed when every check passed. Smoke makes its own scratch folder inside it and sets its own git
# settings and CommitFence data folder there, so the user's git settings, PATH and CommitFence data are never touched.
set -u

if [ $# -lt 1 ] || [ ! -f "$1" ]; then
    echo "usage: run.sh <path of the commitfence program> [parent folder for the scratch folder]" >&2
    exit 64
fi

native() { if command -v cygpath > /dev/null 2>&1; then cygpath -m "$1"; else echo "$1"; fi; }

stop_now() { echo "run.sh: $1" >&2; exit 70; }

HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
SMOKE=$(cd "$HERE/../.." && pwd)/smoke.sh
RULES=$HERE/../rules/run.sh
PUSHED=$HERE/../pushed/run.sh
WHOLE=$HERE/../whole/run.sh
FILERULES=$HERE/../filerules/run.sh
PARENT=${2:-${TMPDIR:-/tmp}}
[ -f "$SMOKE" ] || stop_now "smoke.sh is not at $SMOKE"
[ -f "$RULES" ] || stop_now "the rules script is not at $RULES"
[ -f "$PUSHED" ] || stop_now "the pushed script is not at $PUSHED"
[ -f "$WHOLE" ] || stop_now "the whole script is not at $WHOLE"
[ -f "$FILERULES" ] || stop_now "the filerules script is not at $FILERULES"
mkdir -p "$PARENT" || stop_now "cannot create the parent folder $PARENT"
ROOT=$(mktemp -d "$PARENT/commitfence-guard.XXXXXX") || stop_now "cannot create a scratch folder under $PARENT"
ROOT=$(cd "$ROOT" && pwd) || stop_now "cannot enter the scratch folder"
if [ -z "$ROOT" ] || [ "$ROOT" = "/" ] || [ ! -d "$ROOT" ]; then
    stop_now "the scratch folder path is not usable: '$ROOT'"
fi

STARTED=$SECONDS
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

# ---------- the stand-ins ----------
write_standin() { # folder exit-code: a program that is only a shell script
    mkdir -p "$1"
    cat > "$1/commitfence" <<'STANDIN'
#!/bin/sh
# A stand-in for the commitfence program: it does what install and init do, and answers every review with one fixed exit code.
EXIT_CODE=__EXIT_CODE__
case "$1" in
    --version)
        echo "CommitFence 0.0.0 (stand-in)"
        exit 0
        ;;
    install)
        if [ "${2:-}" = "--print" ]; then
            echo "Would set the git setting commitfence.path to $0"
            exit 0
        fi

        git config --global commitfence.path "$0"
        dirname "$0" > "$COMMITFENCE_USER_PATH_FILE"
        echo "Set the git setting commitfence.path to $0"
        exit 0
        ;;
    init)
        if [ -f commitfence.json ]; then
            echo "commitfence.json already exists and was left as it is."
        else
            printf '{\n    "rules": []\n}\n' > commitfence.json
            echo "Wrote commitfence.json."
        fi

        echo "Add this line as the first command of the pre-push hook:"
        echo '"$(git config --get commitfence.path)" review --hook pre-push "$@" || exit $?'
        exit 0
        ;;
    review)
        echo "CommitFence 0.0.0 ($(pwd))"
        exit "$EXIT_CODE"
        ;;
esac

echo "stand-in: unknown command $1" >&2
exit 3
STANDIN
    sed -i "s/__EXIT_CODE__/$2/" "$1/commitfence"
    chmod +x "$1/commitfence"
}

standin_path() { native "$ROOT/standins/$1/commitfence"; }

standin_runs() { # name exit-code: started by its path, it prints the version line and exits with its own code
    local out code
    out=$(cd "$ROOT" && "$(standin_path "$1")" review 2> /dev/null)
    code=$?
    [ "$code" = "$2" ] && echo "$out" | head -1 | grep -Eq '^CommitFence [^ ]+ \(.+\)$'
}

# ---------- smoke against a stand-in ----------
run_smoke() { # name: sets SMOKE_CODE and SMOKE_LOG
    local name=$1
    SMOKE_LOG=$ROOT/smoke-$name.log
    mkdir -p "$ROOT/parents/$name"
    bash "$SMOKE" "$(standin_path "$name")" "$ROOT/parents/$name" > "$SMOKE_LOG" 2>&1 < /dev/null
    SMOKE_CODE=$?
}

smoke_fails() { # smoke ended non-zero and reached its total, so it ran its cases
    [ "$SMOKE_CODE" != 0 ] && [ "$SMOKE_CODE" != 64 ] && [ "$SMOKE_CODE" != 70 ] && grep -q '^TOTAL: ' "$SMOKE_LOG"
}

expect_failures() { # name description titles...
    local name=$1 what=$2 title
    shift 2
    check "$name" "smoke ends non-zero against a program that $what" smoke_fails
    for title in "$@"; do
        check "$name" "smoke fails against a program that $what: case '$title' FAIL seen" has "$SMOKE_LOG" "FAIL  $title"
    done
}

script_fails_a_lot() { # name script floor: the script, run against the stand-in, ends non-zero and has at least floor failed checks
    local name=$1 script=$2 floor=$3 log=$ROOT/$1-always-0.log code failed
    mkdir -p "$ROOT/parents/$name"
    bash "$script" "$(standin_path always-0)" "$ROOT/parents/$name" > "$log" 2>&1 < /dev/null
    code=$?
    failed=$(sed -nE 's/^TOTAL: [0-9]+ passed, ([0-9]+) failed.*/\1/p' "$log" | head -1)
    check "$name" "$name ends non-zero against a program that passes everything (it ended $code)" bash -c "[ '$code' != 0 ] && [ '$code' != 64 ] && [ '$code' != 70 ]"
    check "$name" "$name fails at least $floor of its checks against a program that passes everything (it failed ${failed:-none})" bash -c "[ '${failed:-0}' -ge '$floor' ]"
}

STANDINS=("always-0|0" "always-1|1" "always-2|2")

for entry in "${STANDINS[@]}"; do
    write_standin "$ROOT/standins/${entry%%|*}" "${entry#*|}"
done

for entry in "${STANDINS[@]}"; do
    check "${entry%%|*}" "the stand-in started by its path prints the version line and exits ${entry#*|}" standin_runs "${entry%%|*}" "${entry#*|}"
done

run_smoke always-0
expect_failures always-0 "passes everything" \
    "a blocking change (judged from the commit)" \
    "a change that cannot be reviewed" \
    "wrong settings (exit code 3)" \
    "staged differs from the folder (staged bad)" \
    "several refs in one push" \
    "missing evidence" \
    "a program older than required" \
    "rule kind: a compile error" \
    "rule kind: a code rule (TEAM011)"

run_smoke always-1
expect_failures always-1 "blocks everything" \
    "a clean change" \
    "staged differs from the folder (staged ok)" \
    "a program newer than required" \
    "install and init: push through init's line"

run_smoke always-2
expect_failures always-2 "calls everything incomplete" \
    "a blocking change (judged from the commit)" \
    "rule kind: a compile error" \
    "rule kind: a code rule (TEAM011)" \
    "a clean change" \
    "staged differs from the folder (staged ok)" \
    "a program newer than required"

script_fails_a_lot rules "$RULES" 40
script_fails_a_lot pushed "$PUSHED" 50
script_fails_a_lot whole "$WHOLE" 60
script_fails_a_lot filerules "$FILERULES" 20

check time "the whole script took under ten minutes ($((SECONDS - STARTED)) seconds so far)" [ $((SECONDS - STARTED)) -lt 600 ]

finish_script
