#!/bin/bash
# The check of the app's shell and the list of the team's rules: builds Tools/CF.App.Check, which opens the
# app's real main window headless on a sample repository, checks what the window shows and does against the command-line
# program, reads the colours of the drawn window, and saves pictures. Expected results are in expected.md.
#
#   Tools/checks/app/run.sh <path of the commitfence program> <folder for the pictures>
#
# It prints one PASS or FAIL line per check, with detail lines under a FAIL, then a TOTAL line, and exits non-zero when any
# check fails. The folder for the pictures is made when it is not there. Under Tools/check-all.sh the second argument is the
# parent folder for the scratch folders, so the pictures are saved there as app-*.png.
# Safe: the check program works in its own scratch folder under the system's temporary folder, and points the data folder of
# both programs into it (COMMITFENCE_DATA_FOLDER), so the person's own data and settings are never touched. Nothing is
# written outside the scratch folder and the pictures folder, and the sample settings file (the root commitfence.json) is only
# copied. Needs: bash, git, the .NET SDK and no network. The program given is run, never changed.
set -u

if [ $# -lt 2 ] || [ ! -f "$1" ]; then
    echo "usage: run.sh <path of the commitfence program> <folder for the pictures>" >&2
    exit 64
fi

native() { if command -v cygpath > /dev/null 2>&1; then cygpath -m "$1"; else echo "$1"; fi; }

stop_now() { echo "run.sh: $1" >&2; exit 70; }

HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPOSITORY=$(cd "$HERE/../../.." && pwd)
PROJECT="$REPOSITORY/Tools/CF.App.Check/CF.App.Check.csproj"
SAMPLE="$REPOSITORY/commitfence.json"
[ -f "$PROJECT" ] || stop_now "the check program is not at $PROJECT"
[ -f "$SAMPLE" ] || stop_now "the sample settings file is not at $SAMPLE"
mkdir -p "$2" || stop_now "cannot create the pictures folder $2"
PICTURES=$(cd "$2" && pwd)
PROGRAM=$(cd "$(dirname "$1")" && pwd)/$(basename "$1")

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

BUILD_LOG=$(mktemp "${TMPDIR:-/tmp}/commitfence-app-check-build.XXXXXX") || stop_now "cannot make a log file"
if ! dotnet build "$(native "$PROJECT")" -c Release -nologo -v q > "$BUILD_LOG" 2>&1; then
    cat "$BUILD_LOG" >&2
    stop_now "the check program does not build (log: $BUILD_LOG)"
fi

rm -f "$BUILD_LOG"
CHECK_DLL=$(ls "$REPOSITORY"/Tools/CF.App.Check/bin/Release/net*/CF.App.Check.dll 2> /dev/null | head -1)
[ -f "$CHECK_DLL" ] || stop_now "the built check program is not under Tools/CF.App.Check/bin/Release"

dotnet "$(native "$CHECK_DLL")" --commitfence "$(native "$PROGRAM")" --pictures "$(native "$PICTURES")" --sample "$(native "$SAMPLE")"
exit $?
