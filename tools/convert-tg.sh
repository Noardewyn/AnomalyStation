#!/usr/bin/env bash
# Converts tg's data from the read-only tgstation clone into the forms the client reads, then imports them into Godot:
#   1. tg's icons and map fonts into the git-ignored godot/assets/tg (tools/Anomaly.Tools.Icons);
#   2. tg's sounds and their index into godot/assets/tg/sound, when the index is missing (tools/Anomaly.Tools.Sounds);
#   3. tg's maps through tools/convert-maps.sh, which converts nothing over the checked-in, editor-edited maps/;
#   4. Godot's headless import of the project (creates godot/.godot).
# Nothing is built or run here: the builds are `dotnet build`, the client's checks godot/tools/smoke.sh (docs/building.md).
#
#   tools/convert-tg.sh
#
# ANOMALY_TGSTATION names the tgstation clone (default: a "tgstation" folder next to the repository or one of its parents).
# GODOT overrides the Godot 4.7.2 mono binary (default: the usual place on this OS, godot/tools/export-common.sh's find_godot).
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
assets="$repo/godot/assets/tg"
. "$repo/godot/tools/export-common.sh"
GODOT=$(find_godot)

tg=${ANOMALY_TGSTATION:-}
if [ -z "$tg" ]; then
    dir="$repo"
    while [ "$dir" != "/" ]; do
        if [ -f "$dir/tgstation/tgstation.dme" ]; then tg="$dir/tgstation"; break; fi
        dir="$(dirname "$dir")"
    done
fi
if [ -z "$tg" ] || [ ! -f "$tg/tgstation.dme" ]; then
    echo "convert-tg: no tgstation clone found (put it at ../tgstation or set ANOMALY_TGSTATION)" >&2
    exit 1
fi

echo "== convert tg's icons (tools/Anomaly.Tools.Icons)"
dotnet run --project "$repo/tools/Anomaly.Tools.Icons" -c Debug -- --tg "$tg" --out "$assets"

if [ ! -f "$assets/sound/index.json" ]; then
    echo "== copy tg's sounds (tools/Anomaly.Tools.Sounds)"
    dotnet run --project "$repo/tools/Anomaly.Tools.Sounds" -c Debug -- --tg "$tg" --out "$assets"
fi

echo "== the maps (tools/convert-maps.sh)"
ANOMALY_TG="$tg" "$repo/tools/convert-maps.sh"

echo "== import (headless)"
"$GODOT" --headless --path "$repo/godot" --import

echo "convert-tg: OK"
