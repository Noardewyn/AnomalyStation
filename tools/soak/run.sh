#!/bin/bash
# The soak test (D100, scope 2i; M2: the capsule locomotion, rest and shove): a Release build, the icons tool, the server in
# real time with metrics, N bots over LiteNetLib, two windowed Godot clients that walk by themselves, and a results file.
#
#   tools/soak/run.sh                     the 15-minute run (D102): 50 mixed bots (wander, rest, shove), 2 Godot windows
#   tools/soak/run.sh --stress            the 150-bot run: 150 bots, 10 minutes, no Godot windows
#   tools/soak/run.sh --bots 20 --minutes 2 --godot 0      a short try
#
# Output goes to tools/soak/results/<UTC stamp>/ (results.txt is the file to read). Everything the script starts is
# stopped on exit, Ctrl+C or SIGTERM; a stopped run still writes its results (marked aborted). See tools/soak/README.md.
#
# bash 3.2 (macOS's /bin/bash) compatible.

set -u
set -m # every background job is its own process group, so one kill reaches whatever it started

SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
ROOT=$(cd "$SCRIPT_DIR/../.." && pwd)

# ---------------------------------------------------------------------------------------------------------------------
# The Godot client's automation arguments (P6, docs/ai/specs/apps.md section 2), given after `--`. Change them here only.
GODOT_ARG_CONNECT="--connect"        # --connect=host:port: connect at once, no connect screen
GODOT_ARG_NAME="--name"              # --name=X: the player name
GODOT_ARG_AUTOWALK="--autowalk"      # hold random directions, re-pick when blocked
GODOT_ARG_QUIT_AFTER="--quit-after"  # --quit-after=S: leave and quit after S seconds
GODOT_WINDOW_SIZE="640x400"          # the engine's own --resolution (before the `--`)
GODOT_NAME_PREFIX="SoakG"
# The server: a line in its log that says it accepts joins (a regex, case-insensitive): the M1 server prints
# "joins open at tick T" once the Master initialized (docs/ai/specs/apps.md 1.3). Without it the script waits
# SERVER_READY_TIMEOUT seconds and carries on: the bots keep trying while the server boots.
SERVER_READY_PATTERN="${SERVER_READY_PATTERN:-^joins open at tick}"
SERVER_READY_TIMEOUT="${SERVER_READY_TIMEOUT:-120}"
# Testing hooks: replace the server or the bots by another command line (the script's own options still apply).
SOAK_SERVER_CMD="${SOAK_SERVER_CMD:-}"
SOAK_BOTS_CMD="${SOAK_BOTS_CMD:-}"
# ---------------------------------------------------------------------------------------------------------------------

BOTS=50
MINUTES=15  # D102: long runs at most 15 minutes
GODOT_CLIENTS=2
BEHAVIOUR=mixed
PREDICT=0
PORT=7777
SEED=1
OUT=""
NO_BUILD=0
STRICT=0
SERVER_EXTRA=""
BOTS_EXTRA=""
DEV_ARGS=()

usage() {
    sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//'
    cat <<'USAGE'

Options:
  --bots N          bots (default 50)
  --minutes M       run length in minutes, decimals are fine (default 15; D102: at most 15)
  --godot K         windowed Godot clients (default 2; 0 with --stress)
  --stress          preset for the 150-bot run: --bots 150 --minutes 10 --godot 0 (explicit options still win)
  --behaviour B     bot behaviour: mixed | wander | rest | shove | idle | churn (default mixed: wander, rest and shove by turns)
  --predict         every bot predicts its own capsule with the motion core (counts corrections in the report)
  --port P          the server's UDP port (default 7777)
  --seed S          world and bot seed (default 1)
  --out DIR         output folder (default tools/soak/results/<UTC stamp>)
  --no-build        use the binaries that are there
  --strict          exit 3 when the results carry flags
  --server-args S   extra arguments for the server (one quoted string)
  --bots-args S     extra arguments for the bots (one quoted string, split at spaces)
  --dev T:COMMAND   a dev command the first joined bot sends T seconds into the run (repeatable; spaces kept):
                    --dev 20:startnow --dev "900:shuttle call"
  -h, --help        this text
USAGE
}

say() { printf '%s\n' "$*"; }
step() { printf '\n== %s\n' "$*"; }
fatal() { printf 'soak: %s\n' "$*" >&2; exit 1; }

# ---- options (the preset first, so that explicit options override it) ----
for arg in "$@"; do
    if [ "$arg" = "--stress" ]; then
        BOTS=150
        MINUTES=10
        GODOT_CLIENTS=0
    fi
done
while [ $# -gt 0 ]; do
    case "$1" in
        --stress) shift ;;
        --bots) BOTS=${2:?--bots needs a value}; shift 2 ;;
        --bots=*) BOTS=${1#*=}; shift ;;
        --minutes) MINUTES=${2:?--minutes needs a value}; shift 2 ;;
        --minutes=*) MINUTES=${1#*=}; shift ;;
        --godot) GODOT_CLIENTS=${2:?--godot needs a value}; shift 2 ;;
        --godot=*) GODOT_CLIENTS=${1#*=}; shift ;;
        --behaviour) BEHAVIOUR=${2:?--behaviour needs a value}; shift 2 ;;
        --behaviour=*) BEHAVIOUR=${1#*=}; shift ;;
        --predict) PREDICT=1; shift ;;
        --port) PORT=${2:?--port needs a value}; shift 2 ;;
        --port=*) PORT=${1#*=}; shift ;;
        --seed) SEED=${2:?--seed needs a value}; shift 2 ;;
        --seed=*) SEED=${1#*=}; shift ;;
        --out) OUT=${2:?--out needs a value}; shift 2 ;;
        --out=*) OUT=${1#*=}; shift ;;
        --no-build) NO_BUILD=1; shift ;;
        --strict) STRICT=1; shift ;;
        --server-args) SERVER_EXTRA=${2:?--server-args needs a value}; shift 2 ;;
        --bots-args) BOTS_EXTRA=${2:?--bots-args needs a value}; shift 2 ;;
        --dev) DEV_ARGS+=(--dev "${2:?--dev needs a value}"); shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) usage >&2; fatal "unknown option '$1'" ;;
    esac
done

case "$BOTS" in ''|*[!0-9]*) fatal "--bots must be a whole number" ;; esac
case "$GODOT_CLIENTS" in ''|*[!0-9]*) fatal "--godot must be a whole number" ;; esac
case "$PORT" in ''|*[!0-9]*) fatal "--port must be a whole number" ;; esac
case "$BEHAVIOUR" in mixed|wander|rest|shove|idle|churn) ;; *) fatal "--behaviour must be mixed, wander, rest, shove, idle or churn for a soak" ;; esac
[ "$BOTS" -ge 1 ] || fatal "--bots must be at least 1"
MINUTES_SECONDS=$(awk -v m="$MINUTES" 'BEGIN { if (m + 0 <= 0) exit 1; printf "%d", m * 60 }') || fatal "--minutes must be a positive number"

STAMP=$(date -u +%Y%m%dT%H%M%SZ)
[ -n "$OUT" ] || OUT="$SCRIPT_DIR/results/$STAMP-b$BOTS-m$MINUTES"
mkdir -p "$OUT" || fatal "cannot create $OUT"
OUT=$(cd "$OUT" && pwd)

# Windows (Git Bash): a native process gets no SIGTERM (kill ends it at once, without its shutdown summary), so the server reads
# its console from a pipe there and the script stops it with the console's quit (M10 slice J).
case "$(uname -s)" in MINGW*|MSYS*|CYGWIN*) WINDOWS=1 ;; *) WINDOWS=0 ;; esac
STOP_FILE="$OUT/.stop-server"
rm -f "$STOP_FILE"

SERVER_PID=""
CAFFEINATE_PID=""
BOTS_PID=""
GODOT_PIDS=()
ABORTED=0
SIGNALS=0
SERVER_DIED=0
CLEANED=0

# ---- process control ----

alive() { [ -n "${1:-}" ] && kill -0 "$1" 2>/dev/null; }

# Stops a job's whole process group (TERM, a grace period, then KILL).
stop_group() {
    local pid=$1 grace=${2:-10} i
    alive "$pid" || return 0
    kill -TERM -- "-$pid" 2>/dev/null || kill -TERM "$pid" 2>/dev/null
    for ((i = 0; i < grace * 5; i++)); do
        alive "$pid" || return 0
        sleep 0.2
    done
    kill -KILL -- "-$pid" 2>/dev/null || kill -KILL "$pid" 2>/dev/null
}

cleanup() {
    [ "$CLEANED" = 1 ] && return
    CLEANED=1
    trap '' INT TERM
    local pid
    if [ ${#GODOT_PIDS[@]} -gt 0 ]; then
        for pid in "${GODOT_PIDS[@]}"; do stop_group "$pid" 5; done
    fi
    stop_group "$BOTS_PID" 10
    touch "$STOP_FILE" 2>/dev/null
    stop_group "$SERVER_PID" 30
    stop_group "$CAFFEINATE_PID" 2
}

on_signal() {
    SIGNALS=$((SIGNALS + 1))
    ABORTED=1
    if [ "$SIGNALS" -ge 2 ]; then
        say "soak: second signal, stopping everything now"
        exit 130
    fi
    say "soak: stop requested; the bots and clients are leaving, the results will cover the partial run (again to stop at once)"
    alive "$BOTS_PID" && kill -TERM "$BOTS_PID" 2>/dev/null
    local pid
    if [ ${#GODOT_PIDS[@]} -gt 0 ]; then
        for pid in "${GODOT_PIDS[@]}"; do alive "$pid" && kill -TERM "$pid" 2>/dev/null; done
    fi
    return 0
}

trap cleanup EXIT
trap on_signal INT TERM

# ---- preflight ----

step "Preflight"
command -v dotnet >/dev/null 2>&1 || fatal "dotnet is not on PATH"
command -v python3 >/dev/null 2>&1 || fatal "python3 is not on PATH (it writes the results file)"
if command -v caffeinate >/dev/null 2>&1; then HAVE_CAFFEINATE=1; else HAVE_CAFFEINATE=0; say "warning: no caffeinate: the Mac may sleep during the run"; fi

GODOT_BIN=""
if [ "$GODOT_CLIENTS" -gt 0 ]; then
    GODOT_BIN=${GODOT:-$(command -v godot-mono || echo /Applications/Godot_mono.app/Contents/MacOS/Godot)}
    [ -x "$GODOT_BIN" ] || fatal "Godot not found (set GODOT to the Godot 4.7.2 mono binary, or use --godot 0)"
fi
if command -v lsof >/dev/null 2>&1 && lsof -nP -iUDP:"$PORT" >/dev/null 2>&1; then
    fatal "UDP port $PORT is already in use (another server? use --port)"
fi

SERVER_DLL="$ROOT/src/Server/bin/Release/net10.0/Anomaly.Server.dll"
BOTS_DLL="$ROOT/tools/Anomaly.Tools.Bots/bin/Release/net10.0/Anomaly.Tools.Bots.dll"

# ---- build ----

if [ "$NO_BUILD" = 0 ]; then
    step "Release build (server and bots)"
    dotnet build "$ROOT/src/Server/Anomaly.Server.csproj" -c Release -nologo -v q || fatal "the server does not build"
    dotnet build "$ROOT/tools/Anomaly.Tools.Bots/Anomaly.Tools.Bots.csproj" -c Release -nologo -v q || fatal "the bots do not build"
    if [ "$GODOT_CLIENTS" -gt 0 ]; then
        # M2 (P7's tool): the client draws tg's own icons, converted from ../tgstation/icons into the git-ignored godot/assets/tg.
        # The tool is run before the Godot import (which reads what it writes). A tree without the tool skips this.
        ICONS_PROJECT="$ROOT/tools/Anomaly.Tools.Icons/Anomaly.Tools.Icons.csproj"
        if [ -f "$ICONS_PROJECT" ]; then
            step "Icons (tools/Anomaly.Tools.Icons: ../tgstation/icons to godot/assets/tg)"
            # The tgstation clone: ANOMALY_TGSTATION, else a "tgstation" folder next to the repository or one of its parents
            # (as tools/convert-tg.sh finds it; a git worktree under .claude/worktrees has it further up).
            TG_DIR=${ANOMALY_TGSTATION:-}
            if [ -z "$TG_DIR" ]; then
                d=$ROOT
                while [ "$d" != "/" ]; do
                    if [ -f "$d/tgstation/tgstation.dme" ]; then TG_DIR=$d/tgstation; break; fi
                    d=$(dirname "$d")
                done
            fi
            [ -n "$TG_DIR" ] || TG_DIR="$ROOT/../tgstation"
            dotnet run --project "$ICONS_PROJECT" -c Release -- --tg "$TG_DIR" --out "$ROOT/godot/assets/tg" >"$OUT/icons.log" 2>&1 \
                || fatal "the icons tool failed (see $OUT/icons.log)"
            say "icons ready: $(tail -n 1 "$OUT/icons.log")"
        else
            say "icons: tools/Anomaly.Tools.Icons is not in this tree; the clients use what is in godot/assets/tg"
        fi
        step "Godot client (import and build)"
        "$GODOT_BIN" --headless --path "$ROOT/godot" --import >"$OUT/godot-import.log" 2>&1 || fatal "the Godot import failed (see $OUT/godot-import.log)"
        dotnet build "$ROOT/godot/Anomaly.Client.csproj" -c Debug -nologo -v q || fatal "the Godot client does not build"
    fi
fi
if [ -z "$SOAK_SERVER_CMD" ]; then [ -f "$SERVER_DLL" ] || fatal "missing $SERVER_DLL (build first)"; fi
if [ -z "$SOAK_BOTS_CMD" ]; then [ -f "$BOTS_DLL" ] || fatal "missing $BOTS_DLL (build first)"; fi

# ---- run info ----

{
    echo "started_utc=$(date -u +%FT%TZ)"
    echo "command=$0 $*"
    echo "git=$(git -C "$ROOT" rev-parse --short HEAD 2>/dev/null) $(git -C "$ROOT" status --porcelain 2>/dev/null | wc -l | tr -d ' ') changed files"
    echo "dotnet=$(dotnet --version)"
    echo "machine=$(sysctl -n machdep.cpu.brand_string 2>/dev/null) / $(sw_vers -productVersion 2>/dev/null)"
    echo "bots=$BOTS minutes=$MINUTES godot=$GODOT_CLIENTS behaviour=$BEHAVIOUR predict=$PREDICT port=$PORT seed=$SEED"
} >"$OUT/run-info.txt"
STARTED_UTC=$(date -u +%FT%TZ)

step "Output folder: $OUT"

# ---- the server ----

step "Server (real time, metrics to soak.csv)"
if [ -n "$SOAK_SERVER_CMD" ]; then
    # shellcheck disable=SC2086
    $SOAK_SERVER_CMD </dev/null >"$OUT/server.log" 2>&1 &
elif [ "$WINDOWS" = 1 ]; then
    # shellcheck disable=SC2086
    (while [ ! -f "$STOP_FILE" ]; do sleep 1; done; echo quit) \
        | dotnet "$SERVER_DLL" --realtime "--port=$PORT" "--seed=$SEED" "--metrics=$OUT/soak.csv" "--database=$OUT/soak.sqlite" $SERVER_EXTRA >"$OUT/server.log" 2>&1 &
else
    # shellcheck disable=SC2086
    dotnet "$SERVER_DLL" --realtime --no-console "--port=$PORT" "--seed=$SEED" "--metrics=$OUT/soak.csv" "--database=$OUT/soak.sqlite" $SERVER_EXTRA </dev/null >"$OUT/server.log" 2>&1 &
fi
SERVER_PID=$!
say "server pid $SERVER_PID, log $OUT/server.log"

# caffeinate -dims holds the display, idle, disk and system sleep assertions; -w ties them to the server's life, so
# there is nothing to forget when the run ends (the same effect as wrapping the server in `caffeinate -dims`).
if [ "$HAVE_CAFFEINATE" = 1 ]; then
    caffeinate -dims -w "$SERVER_PID" &
    CAFFEINATE_PID=$!
fi

waited=0
ready=0
while [ "$waited" -lt "$SERVER_READY_TIMEOUT" ]; do
    if ! alive "$SERVER_PID"; then
        tail -n 30 "$OUT/server.log" >&2
        fatal "the server exited while starting (log above)"
    fi
    if grep -Eiq "$SERVER_READY_PATTERN" "$OUT/server.log" 2>/dev/null; then ready=1; break; fi
    [ "$ABORTED" = 1 ] && break
    sleep 1
    waited=$((waited + 1))
done
if [ "$ready" = 1 ]; then
    say "the server is ready after ${waited}s"
else
    say "warning: no line matching '$SERVER_READY_PATTERN' in ${SERVER_READY_TIMEOUT}s; going on (the bots retry until $((SERVER_READY_TIMEOUT * 3))s)"
fi

# ---- the bots ----

step "Bots ($BOTS, $BEHAVIOUR$([ "$PREDICT" = 1 ] && echo ", predicting"), $MINUTES min)"
PREDICT_ARG=""
[ "$PREDICT" = 1 ] && PREDICT_ARG="--predict"
RUN_START=$(date +%s)
RUN_END=$((RUN_START + MINUTES_SECONDS))
if [ -n "$SOAK_BOTS_CMD" ]; then
    # shellcheck disable=SC2086
    $SOAK_BOTS_CMD >"$OUT/bots.log" 2>&1 &
else
    # shellcheck disable=SC2086
    dotnet "$BOTS_DLL" --host 127.0.0.1 --port "$PORT" --bots "$BOTS" --minutes "$MINUTES" --behaviour "$BEHAVIOUR" \
        --seed "$SEED" --name-prefix Soak --reconnect --join-timeout "$((SERVER_READY_TIMEOUT * 3))" \
        --report "$OUT/bots.json" $PREDICT_ARG $BOTS_EXTRA ${DEV_ARGS[@]+"${DEV_ARGS[@]}"} >"$OUT/bots.log" 2>&1 &
fi
BOTS_PID=$!
say "bots pid $BOTS_PID, log $OUT/bots.log"

# ---- the Godot clients ----

if [ "$GODOT_CLIENTS" -gt 0 ]; then
    step "Godot clients ($GODOT_CLIENTS windows)"
    sleep 3 # let the first bots through the join first
    i=1
    while [ "$i" -le "$GODOT_CLIENTS" ] && [ "$ABORTED" = 0 ]; do
        remaining=$((RUN_END - $(date +%s)))
        if [ "$remaining" -lt 20 ]; then say "warning: too little time left for client $i"; break; fi
        "$GODOT_BIN" --path "$ROOT/godot" --resolution "$GODOT_WINDOW_SIZE" --position "$((30 + (i - 1) * 660)),80" -- \
            "$GODOT_ARG_CONNECT=127.0.0.1:$PORT" "$GODOT_ARG_NAME=$GODOT_NAME_PREFIX$i" "$GODOT_ARG_AUTOWALK" \
            "$GODOT_ARG_QUIT_AFTER=$remaining" >"$OUT/godot-$i.log" 2>&1 &
        GODOT_PIDS+=($!)
        say "client $i: pid $!, $GODOT_NAME_PREFIX$i, quits after ${remaining}s"
        i=$((i + 1))
        sleep 2
    done
fi

# ---- wait for the bots, watching the server ----

step "Running until $(date -u -r "$RUN_END" +%T 2>/dev/null || echo "the bots finish") UTC"
next_progress=$((RUN_START + 60))
abort_started=0
while alive "$BOTS_PID"; do
    if [ "$SERVER_DIED" = 0 ] && ! alive "$SERVER_PID"; then
        say "soak: the server died during the run"
        SERVER_DIED=1
        ABORTED=1
        kill -TERM "$BOTS_PID" 2>/dev/null
    fi
    now=$(date +%s)
    if [ "$ABORTED" = 1 ]; then
        # The bots were told to leave (TERM); they write their report and exit within seconds, or they are killed.
        [ "$abort_started" = 0 ] && abort_started=$now
        if [ $((now - abort_started)) -ge 30 ]; then
            say "soak: the bots did not exit in 30 s; killing them"
            stop_group "$BOTS_PID" 1
            break
        fi
    fi
    if [ "$now" -ge "$next_progress" ]; then
        say "[$(( (now - RUN_START) / 60 )) min] bots: $(tail -n 1 "$OUT/bots.log" 2>/dev/null)"
        say "          server: $(tail -n 1 "$OUT/server.log" 2>/dev/null)"
        next_progress=$((now + 60))
    fi
    sleep 1
done
wait "$BOTS_PID" 2>/dev/null
BOTS_EXIT=$?

# The Godot clients quit on their own (--quit-after); give them time, then they are stopped.
if [ ${#GODOT_PIDS[@]} -gt 0 ]; then
    step "Waiting for the Godot clients"
    waited=0
    while [ "$waited" -lt 60 ]; do
        any=0
        for pid in "${GODOT_PIDS[@]}"; do alive "$pid" && any=1; done
        [ "$any" = 0 ] && break
        sleep 1
        waited=$((waited + 1))
    done
    for pid in "${GODOT_PIDS[@]}"; do stop_group "$pid" 5; done
fi

# ---- stop the server (it writes its summary on SIGTERM) ----

step "Stopping the server"
SERVER_EXIT=""
if alive "$SERVER_PID"; then
    if [ "$WINDOWS" = 1 ]; then touch "$STOP_FILE"; else kill -TERM "$SERVER_PID" 2>/dev/null; fi
    waited=0
    while alive "$SERVER_PID" && [ "$waited" -lt 60 ]; do sleep 1; waited=$((waited + 1)); done
    if alive "$SERVER_PID"; then
        say "warning: the server did not stop in 60 s; killing it"
        kill -KILL "$SERVER_PID" 2>/dev/null
        SERVER_EXIT=137
    fi
fi
wait "$SERVER_PID" 2>/dev/null
status=$?
[ -n "$SERVER_EXIT" ] || SERVER_EXIT=$status
[ "$SERVER_DIED" = 1 ] && [ "$SERVER_EXIT" = 0 ] && SERVER_EXIT=1
FINISHED_UTC=$(date -u +%FT%TZ)

# ---- results ----

step "Results"
STRICT_ARG=""
[ "$STRICT" = 1 ] && STRICT_ARG="--strict"
# shellcheck disable=SC2086
python3 "$SCRIPT_DIR/summarize.py" --out "$OUT" $STRICT_ARG \
    --meta "started_utc=$STARTED_UTC" --meta "finished_utc=$FINISHED_UTC" \
    --meta "bots=$BOTS" --meta "behaviour=$BEHAVIOUR" --meta "predict=$PREDICT" --meta "minutes=$MINUTES" --meta "godot_clients=$GODOT_CLIENTS" \
    --meta "seed=$SEED" --meta "port=$PORT" --meta "aborted=$ABORTED" --meta "server_exit=$SERVER_EXIT" \
    --meta "server_args=--realtime --no-console --port=$PORT --seed=$SEED --metrics=soak.csv --database=soak.sqlite $SERVER_EXTRA"
RESULT_STATUS=$?

say ""
say "results: $OUT/results.txt (and results.json, soak.csv, server.log, bots.json, bots.log, godot-N.log)"
if [ "$SERVER_DIED" = 1 ]; then exit 1; fi
if [ "$ABORTED" = 1 ] && [ "$SIGNALS" -gt 0 ]; then exit 130; fi
if [ "$BOTS_EXIT" -ne 0 ] && [ "$BOTS_EXIT" -ne 127 ]; then
    say "soak: the bots exited with status $BOTS_EXIT (3: none ever joined)"
    exit 1
fi
exit "$RESULT_STATUS"
