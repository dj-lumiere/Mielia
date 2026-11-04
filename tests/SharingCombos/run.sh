#!/usr/bin/env bash
# Builds and runs the generated sharing combination programs (see generate.py).
#
#   run.sh WORKDIR [generate.py options]     generate into WORKDIR, build every case, run it RUNS times
#
# Environment: SUFLAE_DLL (default: Suflae/bin/Debug/net10.0/Suflae.dll next to this repo), RUNS (default 3),
# JOBS (default 2, never more: each build is heavy), LIMIT (seconds per run, default 90).
# Prints one line per case: status, then the case name. Status is OK, BUILD (the build failed), BAD (a wrong count),
# TSAN (ThreadSanitizer reported a race), SHARE (the sharing check stopped the program), TIMEOUT (a deadlock or a
# hang) or CRASH. The build log and the run logs stay in the case folder.
set -u
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
work="${1:?usage: run.sh WORKDIR [generate.py options]}"
shift
dll="${SUFLAE_DLL:-$root/bin/Debug/net10.0/Suflae.dll}"
runs="${RUNS:-3}"
jobs="${JOBS:-2}"
limit="${LIMIT:-90}"
mkdir -p "$work"
python3 "$here/generate.py" "$work" "$@" > "$work/cases.txt"

one_case() {
    name="$1"
    cd "$work/$name" || exit 1
    if ! dotnet "$dll" build main.sf > build.log 2>&1; then
        echo "BUILD $name"
        return
    fi
    status=OK
    for i in $(seq 1 "$runs"); do
        timeout "$limit" ./main.exe > "run$i.log" 2>&1
        code=$?
        if [ "$code" = 124 ]; then
            status=TIMEOUT
        elif grep -q "WARNING: ThreadSanitizer" "run$i.log"; then
            status=TSAN
        elif grep -q "Sharing violation" "run$i.log"; then
            status=SHARE
        elif grep -q "RESULT bad" "run$i.log"; then
            status=BAD
        elif ! grep -q "RESULT ok" "run$i.log"; then
            status=CRASH
        fi
        [ "$status" != OK ] && break
    done
    echo "$status $name"
}
export -f one_case
export work dll runs limit
xargs -a "$work/cases.txt" -P "$jobs" -I{} bash -c 'one_case {}' | tee "$work/results.txt"
echo "--- summary"
cut -d' ' -f1 "$work/results.txt" | sort | uniq -c
