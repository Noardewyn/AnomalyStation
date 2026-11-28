#!/bin/sh
# Smoke check of the Godot client (docs/ai/specs/apps.md section 2.5): headless import, C# build, a headless run against the
# client's own scripted world (--fake-server) that must show the core smoke line and a summary line with other entities
# moving, optionally a headless connect run against a live server, and optionally a short windowed run that saves a
# screenshot of the first-person view (Godot has no offscreen renderer on macOS: --headless uses a dummy renderer).
#
#   godot/tools/smoke.sh [screenshot.png]
#   SMOKE_CONNECT=127.0.0.1:7777 godot/tools/smoke.sh      # also joins a running server for a few seconds
#
# GODOT overrides the Godot 4.7.2 mono binary (default: the usual place on this OS, export-common.sh's find_godot).
# tg's icons, sounds and maps must be converted first: tools/convert-tg.sh does that, this script converts nothing.
# When maps/ holds MetaStation, a headless run of the fake server on it checks the chunked world renderer at its real size.
# SMOKE_META_SHOTS=DIR also saves windowed screenshots of MetaStation (a lit hall, a wall corner, a corridor with windows, a room with a table and props, a wall with lamps and signs, the hull against the starfield, an airlock opening) as DIR/<name>.png; the debug HUD is hidden (F3 by --keys) so that the whole view, ceiling included, can be judged (M3 R1, D119).
# SMOKE_SHOWCASE=DIR also renders the renderers' showcase scenarios (room, dirs, lying, damage) as DIR/<scenario>.png (windows open).
set -eu

project=$(cd "$(dirname "$0")/.." && pwd)
. "$project/tools/export-common.sh"
GODOT=$(find_godot)

repo=$(cd "$project/.." && pwd)
if [ ! -d "$project/assets/tg/icons" ]; then
    echo "smoke: no converted icons in godot/assets/tg (run tools/convert-tg.sh first)" >&2
    exit 1
fi
[ -f "$project/assets/tg/sound/index.json" ] || echo "smoke: no copied sounds (tools/convert-tg.sh); the audio check is skipped"
meta="$repo/maps/map_files/MetaStation/MetaStation.as3dmap"

echo "== import (headless)"
"$GODOT" --headless --path "$project" --import

echo "== build Anomaly.Client (Debug)"
dotnet build "$project/Anomaly.Client.csproj" -c Debug -nologo

# Runs Godot headless with the arguments after "--", checks its log. $1 is the label, the rest the user arguments.
# Under `set -e` a failing command substitution would end the script before the log is shown, so the status is kept.
run_headless() {
    label=$1
    shift
    echo "== $label (headless)"
    run_status=0
    run_log=$("$GODOT" --headless --path "$project" -- "$@" 2>&1) || run_status=$?
    echo "$run_log"
    [ "$run_status" -eq 0 ] || { echo "smoke: Godot exited with status $run_status" >&2; exit 1; }
    echo "$run_log" | grep -q '^\[anomaly\] core smoke: ' || { echo "smoke: the core smoke line is missing" >&2; exit 1; }
    echo "$run_log" | grep -q '^\[anomaly\] client summary: ' || { echo "smoke: the summary line is missing" >&2; exit 1; }
    if echo "$run_log" | grep -q -E '^(ERROR|SCRIPT ERROR)'; then
        echo "smoke: Godot reported errors" >&2
        exit 1
    fi
}

run_headless "run the scripted world, 6 s" --fake-server --autowalk --quit-after=6
echo "$run_log" | grep -q 'phase=Playing' || { echo "smoke: the fake world did not start" >&2; exit 1; }
echo "$run_log" | grep -q 'own_seen=True' || { echo "smoke: the own entity never appeared" >&2; exit 1; }
echo "$run_log" | grep -q -E 'others_moved=[1-9]' || { echo "smoke: no other entity moved" >&2; exit 1; }
# M4b audio slice 3: the fake server plays a door every 2 s; the headless Dummy driver mixes nothing, so this checks the bookkeeping.
if [ -f "$project/assets/tg/sound/index.json" ]; then
    echo "$run_log" | grep -q -E '^\[anomaly\] audio: events [1-9][0-9]* voices [1-9][0-9]* missing 0' || { echo "smoke: the fake server's door sounds were not played" >&2; exit 1; }
fi

echo "== showcase: tg's pictures on the turf kit and billboards, and the pick ray (headless)"
showcase_status=0
showcase_log=$(ANOMALY_SHOWCASE=dirs ANOMALY_SHOWCASE_PICK=1 "$GODOT" --headless --path "$project" res://scripts/Render/Showcase.tscn -- --quit-after=3 2>&1) || showcase_status=$?
echo "$showcase_log"
[ "$showcase_status" -eq 0 ] || { echo "smoke: the showcase exited with status $showcase_status" >&2; exit 1; }
echo "$showcase_log" | grep -q -E 'pictures=4 .*textured_groups=4 .*icon_problems=0' || { echo "smoke: the showcase did not draw tg's pictures" >&2; exit 1; }
echo "$showcase_log" | grep -q 'showcase pick: hits=4/4 over_head_misses=4/4 wall_ray_hit=False' || { echo "smoke: the pick ray found the wrong entities" >&2; exit 1; }
if echo "$showcase_log" | grep -q -E '^(ERROR|SCRIPT ERROR)'; then
    echo "smoke: Godot reported errors" >&2
    exit 1
fi

if [ -f "$meta" ]; then
    run_headless "the fake server on converted MetaStation, 10 s" --fake-server --fake-map="$meta" --fake-bots=8 --quit-after=10
    echo "$run_log" | grep -q -E 'world: [1-9][0-9]* chunks, [1-9][0-9]* walls' || { echo "smoke: the chunked world was not built" >&2; exit 1; }
    echo "$run_log" | grep -q -E 'props=[1-9][0-9]* pieces=[1-9][0-9]*' || { echo "smoke: MetaStation drew no props or pieces" >&2; exit 1; }
else
    echo "smoke: no converted MetaStation at $meta, skipping the Meta run"
fi

if [ -n "${SMOKE_CONNECT:-}" ]; then
    run_headless "join the server at $SMOKE_CONNECT, 8 s" --connect="$SMOKE_CONNECT" --name=smoke --autowalk --quit-after=8
    echo "$run_log" | grep -q 'phase=Playing' || { echo "smoke: did not get into the game" >&2; exit 1; }
    echo "$run_log" | grep -q -E 'deltas=[1-9]' || { echo "smoke: no delta arrived" >&2; exit 1; }
fi

if [ $# -ge 1 ]; then
    case "$1" in
        /*) png=$1 ;;
        *) png=$(pwd)/$1 ;;
    esac
    echo "== render the first-person view to $png (opens a window for about five seconds)"
    # A file left by an earlier run would pass the check below even if this render saved nothing.
    rm -f "$png"
    "$GODOT" --path "$project" --resolution 1280x720 -- --fake-server --autowalk --seed=3 --screenshot-after=4,"$png"
    test -s "$png" || { echo "smoke: no screenshot written" >&2; exit 1; }
fi

if [ -n "${SMOKE_META_SHOTS:-}" ] && [ -f "$meta" ]; then
    mkdir -p "$SMOKE_META_SHOTS"
    shot() {
        name=$1
        shift
        echo "== MetaStation $name to $SMOKE_META_SHOTS/$name.png (opens a window)"
        rm -f "$SMOKE_META_SHOTS/$name.png"
        "$GODOT" --path "$project" --resolution 1280x720 -- --fake-server --fake-map="$meta" --fake-bots=0 --ceiling --keys=1:F3+,1.1:F3- "$@" --screenshot-after=4.5,"$SMOKE_META_SHOTS/$name.png"
        test -s "$SMOKE_META_SHOTS/$name.png" || { echo "smoke: no Meta screenshot written" >&2; exit 1; }
    }
    shot hall --view=100,115,1.6,-90,0
    shot walls --view=98,114,1.6,-38,0
    shot corner --view=105,114.4,1.6,140,0
    shot corridor --view=45,121.6,1.6,90,0
    shot room --view=107,134,1.6,0,0
    shot lampsign --view=133,151,1.6,0,4
    shot hull --view=22,128,10,-60,-22
    echo "== MetaStation airlock opening to $SMOKE_META_SHOTS/airlock*.png (opens a window)"
    "$GODOT" --path "$project" --resolution 1280x720 -- --fake-server --fake-map="$meta" --fake-bots=0 --ceiling --view=39,122,1.6,180,-5 --keys=1:F3+,1.1:F3- \
        --dev-after=3,"door 39 120 open" --screenshot-after=2.5,"$SMOKE_META_SHOTS/airlock0.png" --screenshot-after=3.25,"$SMOKE_META_SHOTS/airlock1.png" \
        --screenshot-after=5,"$SMOKE_META_SHOTS/airlock2.png"
fi

if [ -n "${SMOKE_SHOWCASE:-}" ]; then
    mkdir -p "$SMOKE_SHOWCASE"
    for scenario in room dirs lying damage; do
        echo "== showcase $scenario to $SMOKE_SHOWCASE/$scenario.png"
        rm -f "$SMOKE_SHOWCASE/$scenario.png"
        ANOMALY_SHOWCASE=$scenario "$GODOT" --path "$project" --resolution 1280x720 res://scripts/Render/Showcase.tscn -- --screenshot-after=2,"$SMOKE_SHOWCASE/$scenario.png"
        test -s "$SMOKE_SHOWCASE/$scenario.png" || { echo "smoke: no showcase screenshot written" >&2; exit 1; }
    done
fi

echo "smoke: OK"
