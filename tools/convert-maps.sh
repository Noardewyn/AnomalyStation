#!/usr/bin/env bash
# The maps are checked in (D109, D337: maps/ at the repository's root, the mirror of tg's _maps/, edited with the map editor since 2026-11-17).
# This script converts tg's maps from the read-only tg clone with tools/Anomaly.Tools.Maps only where that would write over nothing: when maps/
# holds no maps (a folder someone emptied). --force converts tg's default maps over the folder anyway: the owner's edits of them are then lost
# from the files, and git shows what changed. Other arguments go to the tool and convert what they name (--map NAME, --all-station-maps,
# --check, --allow-bad-paths, --types FILE, --od-json FILE). The clone is ANOMALY_TG, or ../tgstation beside the repository.
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

force=0
args=()
for a in "$@"; do
    if [ "$a" = "--force" ]; then force=1; else args+=("$a"); fi
done
if [ "$force" = 0 ] && [ ${#args[@]} -eq 0 ] && [ -n "$(find "$repo/maps/map_jsons" -name '*.json' -print -quit 2>/dev/null)" ]; then
    echo "convert-maps: maps/ is checked in and edited with the map editor: nothing converted (--force writes tg's maps over it)"
    exit 0
fi

tg="${ANOMALY_TG:-}"
if [ -z "$tg" ]; then
    dir="$repo"
    while [ "$dir" != "/" ]; do
        if [ -d "$dir/tgstation/_maps" ]; then tg="$dir/tgstation"; break; fi
        dir="$(dirname "$dir")"
    done
fi
if [ -z "$tg" ] || [ ! -d "$tg/_maps" ]; then
    echo "convert-maps: no tg clone (ANOMALY_TG or ../tgstation); nothing converted" >&2
    exit 3
fi

exec dotnet run --project "$repo/tools/Anomaly.Tools.Maps" -c Release -- --tg "$tg" --out "$repo/maps" ${args[@]+"${args[@]}"}
