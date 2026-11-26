#!/bin/bash
# The streaming hitch test (docs/testing.md, "Profiling"): a Release server on MetaStation without the lobby, one client that flies
# with noclip at the normal speed across the map along one row, from the west edge to the east edge, and back the same way. The
# first leg meets every part of the station for the first time; the way back meets them again, so the two legs' hitches tell the
# first-time loads apart from the rest. With Tracy's client built (tools/perf/tracy-client.sh) and its tools on hand, the run is
# recorded with tracy-capture and summed up by tools/perf/tracy-frames.py.
#
#   tools/perf/fly.sh                              row 127, 80 s a leg (the normal speed crosses the map in about 75 s); DIR defaults to tools/perf/results/fly-<UTC stamp>/
#   tools/perf/fly.sh --row 100 --leg 50 --out DIR -- --quality=high      another row, longer legs, extra client arguments
#   GODOT=/path/to/godot-mono TRACY_TOOLS=/path/to/tracy tools/perf/fly.sh
#   GODOT_ARGS="--disable-vsync --max-fps 60" tools/perf/fly.sh       the engine's own arguments (before the game's)
#   tools/perf/fly.sh --still                    no flight: as long standing at the station's centre, looking east (what hitches
#                                                with no streaming at all)
#
# Output in DIR: server.log, client.log, fly.tracy (open it in tracy-profiler) and summary.txt (each leg's frame times and the
# longest frames with their zones).

set -u

ROOT=$(cd "$(dirname "$0")/../.." && pwd)
OUT="$ROOT/tools/perf/results/fly-$(date -u +%Y%m%dT%H%M%SZ)"
PORT=7791
ROW=127
LEG=80
STILL=false
GODOT=${GODOT:-godot-mono}
TRACY_TOOLS=${TRACY_TOOLS:-}
TRACY_LIB=${ANOMALY_TRACY:-$ROOT/build/tracy/TracyClient.dll}
while [ $# -gt 0 ]; do
    case "$1" in
        --out) OUT=$2; shift 2 ;;
        --port) PORT=$2; shift 2 ;;
        --row) ROW=$2; shift 2 ;;
        --leg) LEG=$2; shift 2 ;;
        --still) STILL=true; shift ;;
        --) shift; break ;;
        *) echo "unknown argument $1" >&2; exit 2 ;;
    esac
done
mkdir -p "$OUT"
OUT=$(cd "$OUT" && pwd)
cd "$ROOT" || exit 1

# the server into its own folder: a server already running from src/Server/bin keeps its files locked
dotnet build src/Server -c Release --artifacts-path build/perf-server -v q -nologo > "$OUT/build.log" 2>&1 || { echo "server build failed: $OUT/build.log" >&2; exit 1; }
dotnet build godot/Anomaly.Client.csproj -v q -nologo >> "$OUT/build.log" 2>&1 || { echo "client build failed: $OUT/build.log" >&2; exit 1; }

dotnet build/perf-server/bin/Anomaly.Server/release/Anomaly.Server.dll --realtime --map metastation --port "$PORT" --no-lobby > "$OUT/server.log" 2>&1 &
SERVER=$!
CAPTURE=
trap 'kill $SERVER 2>/dev/null; [ -n "$CAPTURE" ] && kill $CAPTURE 2>/dev/null' EXIT INT TERM
until grep -q "joins open" "$OUT/server.log"; do
    kill -0 $SERVER 2>/dev/null || { echo "the server stopped: $OUT/server.log" >&2; exit 1; }
    grep -q "cannot listen" "$OUT/server.log" && { echo "port $PORT is taken: $OUT/server.log" >&2; exit 1; }
    sleep 1
done

TRACY_ENV=
if [ -n "$TRACY_TOOLS" ] && [ -f "$TRACY_LIB" ]; then
    "$TRACY_TOOLS/tracy-capture" -o "$OUT/fly.tracy" -a 127.0.0.1 -f > "$OUT/capture.log" 2>&1 &
    CAPTURE=$!
    TRACY_ENV=$TRACY_LIB
fi

# the script's times count from the world's reveal; the view faces east (Godot's yaw: 90 is west), then west for the way back
BACK=$((8 + LEG))
END=$((BACK + LEG))
START="tp me 2 $ROW"
KEYS="5:yaw=-90,5:pitch=0,6:W+,$BACK:W-,$BACK:yaw=90,$((BACK + 1)):W+,$END:W-"
if [ "$STILL" = true ]; then
    START="tp me 131 $ROW"
    KEYS="5:yaw=-90,5:pitch=0"
fi
# shellcheck disable=SC2086 # GODOT_ARGS is a list of words
ANOMALY_TRACY="$TRACY_ENV" "$GODOT" ${GODOT_ARGS:-} --path godot --resolution 1600x900 -- --connect=127.0.0.1:"$PORT" --name="Fly$(date +%s)x$$" \
    --token-store="$OUT/token.txt" "$@" "--dev-after=2,god me" "--dev-after=3,$START" "--keys=$KEYS" --quit-after=$((END + 25)) > "$OUT/client.log" 2>&1

if [ -n "$CAPTURE" ]; then
    wait $CAPTURE
    CAPTURE=
    "$TRACY_TOOLS/tracy-csvexport" -u -p "$OUT/fly.tracy" > "$OUT/zones.csv" 2> "$OUT/csvexport.log"
    "$TRACY_TOOLS/tracy-csvexport" -m "$OUT/fly.tracy" > "$OUT/messages.csv" 2>> "$OUT/csvexport.log"
    python "$ROOT/tools/perf/tracy-frames.py" "$OUT/zones.csv" "$OUT/messages.csv" > "$OUT/summary.txt"
    cat "$OUT/summary.txt"
fi
grep -h "client summary\|preload miss" "$OUT/client.log" | tail -5
