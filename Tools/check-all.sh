#!/bin/bash
# Runs every check of CommitFence against one built or published program: the smoke script and the check script of
# each subject. Each check script has its own folder under Tools/checks with its expected.md, which holds the verdict
# of every case.
#
#   Tools/check-all.sh <path of the commitfence program> [parent folder for the scratch folders]
#
# It prints the versions of the SDK and of git and the SHA-256 of the program under test first, then for each script every
# case line in order (PASS, FAIL and SKIP, a FAIL with its detail lines) and the total of the script, and a grand total.
# A skipped case is a case the machine cannot build; it is printed as SKIP and counted in every total. The last line is
# always `exit code: <n>`, the exit code this script itself ends with, so that no wrapper is needed to know how it ended.
# It exits non-zero when any case of any script fails, when a script does not end with its total, or
# when a script cannot be started. The whole output of each script is kept in a log file next to the scratch
# folders; the log is removed when the script passed.
#
# Safe: every script makes its own fresh scratch folder with mktemp -d under the parent folder (default: the system's
# temp folder), points GIT_CONFIG_GLOBAL, COMMITFENCE_DATA_FOLDER and COMMITFENCE_USER_PATH_FILE into it, uses local
# bare repositories as remotes, and removes its folder when it passed. The user's git settings, PATH and CommitFence
# data are never touched. Needs: bash, git, the .NET SDK and no network. Takes about thirty-five minutes.
set -u

if [ $# -lt 1 ] || [ ! -f "$1" ]; then
    echo "usage: check-all.sh <path of the commitfence program> [parent folder for the scratch folders]" >&2
    exit 64
fi

HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
PROGRAM=$1
PARENT=${2:-${TMPDIR:-/tmp}}
mkdir -p "$PARENT" || { echo "check-all.sh: cannot create the parent folder $PARENT" >&2; exit 70; }
PARENT=$(cd "$PARENT" && pwd)
STARTED=$SECONDS

echo "CommitFence checks"
echo "program: $PROGRAM ($("$PROGRAM" --version 2> /dev/null | head -1))"
echo "program SHA-256: $(sha256sum "$PROGRAM" | cut -d" " -f1)"
echo "dotnet SDK: $(dotnet --version 2> /dev/null || echo 'not found')"
echo "git: $(git --version 2> /dev/null || echo 'not found')"
echo "bash: ${BASH_VERSION}"
echo

SCRIPTS=(
    "smoke|$HERE/smoke.sh"
    "pushed|$HERE/checks/pushed/run.sh"
    "toolchain|$HERE/checks/toolchain/run.sh"
    "meaning|$HERE/checks/meaning/run.sh"
    "rules|$HERE/checks/rules/run.sh"
    "acceptance|$HERE/checks/acceptance/run.sh"
    "whole|$HERE/checks/whole/run.sh"
    "app|$HERE/checks/app/run.sh"
    "filerules|$HERE/checks/filerules/run.sh"
    "guard|$HERE/checks/guard/run.sh"
)

FAILED_SCRIPTS=0
GRAND_PASSED=0
GRAND_FAILED=0
GRAND_SKIPPED=0

print_cases() { # log: every case line in order, a FAIL with the detail lines under it
    awk '
        /^(PASS|FAIL|SKIP)/ { print; after_fail = ($0 ~ /^FAIL/); next }
        /^        / { if (after_fail) print; next }
        { after_fail = 0 }
    ' "$1"
}

total_field() { # total-line name: the number before the word (passed, failed, skipped)
    echo "$1" | sed -nE "s/.*[ :]([0-9]+) $2.*/\1/p" | head -1
}

run_script() { # name path
    local name=$1 path=$2 log="$PARENT/check-all-$1.log" rc total passed failed skipped
    echo "== $name"
    if [ ! -f "$path" ]; then
        echo "FAIL  the script $path does not exist"
        FAILED_SCRIPTS=$((FAILED_SCRIPTS + 1))
        return
    fi

    bash "$path" "$PROGRAM" "$PARENT" > "$log" 2>&1 < /dev/null
    rc=$?
    print_cases "$log"
    total=$(grep -E '^TOTAL: ' "$log" | tail -1)
    if [ -z "$total" ]; then
        echo "FAIL  $name ended without a total (exit code $rc); see $log"
        FAILED_SCRIPTS=$((FAILED_SCRIPTS + 1))
        return
    fi

    echo "$name $total"
    passed=$(total_field "$total" passed)
    failed=$(total_field "$total" failed)
    skipped=$(total_field "$total" skipped)
    if [ -z "$passed" ] || [ -z "$failed" ] || [ -z "$skipped" ]; then
        echo "FAIL  $name ended with a total line that lacks the passed, failed or skipped count: $total; see $log"
        FAILED_SCRIPTS=$((FAILED_SCRIPTS + 1))
        return
    fi

    GRAND_PASSED=$((GRAND_PASSED + passed))
    GRAND_FAILED=$((GRAND_FAILED + failed))
    GRAND_SKIPPED=$((GRAND_SKIPPED + skipped))
    if [ "$rc" -ne 0 ] || [ "$failed" -ne 0 ]; then
        FAILED_SCRIPTS=$((FAILED_SCRIPTS + 1))
        echo "     log kept: $log"
    else
        rm -f "$log"
    fi

    echo
}

for entry in "${SCRIPTS[@]}"; do
    run_script "${entry%%|*}" "${entry#*|}"
done

echo "TOTAL: $GRAND_PASSED passed, $GRAND_FAILED failed, $GRAND_SKIPPED skipped, $FAILED_SCRIPTS of ${#SCRIPTS[@]} scripts failed, $((SECONDS - STARTED)) seconds"
STATUS=0
if [ "$GRAND_FAILED" -ne 0 ] || [ "$FAILED_SCRIPTS" -ne 0 ]; then STATUS=1; fi
echo "exit code: $STATUS"
exit $STATUS