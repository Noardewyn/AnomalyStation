#!/bin/bash
# The client's frame measurement on MetaStation (docs/ai/specs/performance.md): a Release server without the lobby, the walk from the
# station's centre (fly, noclip and godmode, then --autowalk; the owner, 2026-10-29: from the spawn at the map's edge a walk mostly
# crosses space) with the frame probe, then the bench at the centre for each preset. One summary file per run.
#
#   tools/perf/walk.sh                          the walk (120 s) and the bench (20 s a preset: medium, low, medium, high, max)
#   tools/perf/walk.sh --out DIR --walk 60 --bench 0 -- --option=shadowed_lights=16     a shorter walk, no bench, extra client args
#   tools/perf/walk.sh --optimize               the client's C# compiled optimized (as a player's export), not the editor's Debug
#   tools/perf/walk.sh --at 45,133              start the walk and fly the bench at another tile (default the centre, 131,127)
#
# Output: DIR (default tools/perf/results/<UTC stamp>/), summary.txt is the file to read: the join's build time, the walk's
# frames (the frame probe's summary line: p50/p95/p99/max frame time, frames over 33/50/100 ms, draw calls) and one bench line
# per preset (the first preset overlaps the join's streaming: read the second). The client is the editor's Debug build.
#
# bash 3.2 (macOS's /bin/bash) compatible.

set -u

ROOT=$(cd "$(dirname "$0")/../.." && pwd)
OUT="$ROOT/tools/perf/results/$(date -u +%Y%m%dT%H%M%SZ)"
PORT=7791
WALK=120
BENCH=20
PRESETS=medium,low,medium,high,max
OPTIMIZE=false
AT=131,127
while [ $# -gt 0 ]; do
    case "$1" in
        --out) OUT=$2; shift 2 ;;
        --port) PORT=$2; shift 2 ;;
        --walk) WALK=$2; shift 2 ;;
        --bench) BENCH=$2; shift 2 ;;
        --presets) PRESETS=$2; shift 2 ;;
        --optimize) OPTIMIZE=true; shift ;;
        --at) AT=$2; shift 2 ;;
        --) shift; break ;;
        *) echo "unknown argument $1" >&2; exit 2 ;;
    esac
done
mkdir -p "$OUT"
OUT=$(cd "$OUT" && pwd) # the client takes absolute paths only
cd "$ROOT" || exit 1

dotnet build src/Server -c Release -v q -nologo > "$OUT/build.log" 2>&1 || { echo "server build failed: $OUT/build.log" >&2; exit 1; }
dotnet build godot/Anomaly.Client.csproj -v q -nologo >> "$OUT/build.log" 2>&1 || { echo "client build failed: $OUT/build.log" >&2; exit 1; }
# the client project alone again, optimized (the core projects set their own Optimize; a global one would override them), and
# back to the editor's Debug at the end
client_only() { dotnet build godot/Anomaly.Client.csproj -v q -nologo --no-incremental -p:BuildProjectReferences=false "$@" >> "$OUT/build.log" 2>&1; }
if [ "$OPTIMIZE" = true ]; then
    client_only -p:Optimize=true || { echo "optimized client build failed: $OUT/build.log" >&2; exit 1; }
fi

dotnet src/Server/bin/Release/net10.0/Anomaly.Server.dll --realtime --map metastation --port "$PORT" --no-lobby > "$OUT/server.log" 2>&1 &
SERVER=$!
trap 'kill $SERVER 2>/dev/null; [ "$OPTIMIZE" = true ] && client_only' EXIT INT TERM
until grep -q "joins open" "$OUT/server.log"; do
    kill -0 $SERVER 2>/dev/null || { echo "the server stopped: $OUT/server.log" >&2; exit 1; }
    sleep 1
done

# the server keeps a name for its token: every run plays new names
TAG="$(date +%s)x$$"
if [ "$WALK" -gt 0 ]; then
    ANOMALY_FRAMEPROBE=1 ANOMALY_FRAMEPROBE_MS=50 godot-mono --path godot --resolution 1600x900 -- --connect=127.0.0.1:"$PORT" --name="Walk$TAG" \
        --token-store="$OUT/walk-token.txt" "$@" "--dev-after=3,god me" "--dev-after=4,tp me ${AT%,*} ${AT#*,}" --autowalk --seed=11 \
        --quit-after=$((WALK + 5)) > "$OUT/walk.log" 2>&1
fi
if [ "$BENCH" -gt 0 ]; then
    godot-mono --path godot --resolution 1600x900 -- --connect=127.0.0.1:"$PORT" --name="Bench$TAG" --token-store="$OUT/bench-token.txt" "$@" \
        "--dev-after=1,tp me ${AT%,*} ${AT#*,}" --bench="$BENCH" --bench-presets="$PRESETS" --bench-at="$AT" --bench-out="$OUT/bench.txt" > "$OUT/bench.log" 2>&1
fi

{
    grep -h -m1 "\[anomaly\] world:" "$OUT/walk.log" "$OUT/bench.log" 2>/dev/null | sed -E 's/.*built in ([0-9.,]+) s.*/join_build_s=\1/' | head -1
    grep -h "\[frameprobe\] summary" "$OUT/walk.log" 2>/dev/null | sed 's/\[frameprobe\] summary /walk /'
    grep -h "bench: preset=" "$OUT/bench.log" 2>/dev/null | sed -E 's/.*bench: (preset=[^ ]+ avg_fps=[^ ]+ p50_fps=[^ ]+ p99_fps=[^ ]+ min_fps=[^ ]+).*draw_calls=([0-9]+).*/bench \1 draw_calls=\2/'
} > "$OUT/summary.txt"
cat "$OUT/summary.txt"
