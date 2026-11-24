#!/bin/sh
# M4a P7: the interaction shots (docs/ai/specs/apps.md 2.12): the hover highlight with its screentip, the radial open, the HUD hands and the
# 15 slots in cursor mode, an open container, a thrown item in flight. Each opens a window for a few seconds.
#
#   godot/tools/interaction-shots.sh OUTDIR
#
# ANOMALY_SHOT scripts the scene's own code paths after the start (aim at the toolbox, click, hold the right button, Tab, a screen click on the hand
# box); ANOMALY_FAKE_INVENTORY=1 turns on the fake server's version-4 script; ANOMALY_FAKE_THROW_TICKS slows the thrown item to a tile every N ticks.
# GODOT overrides the Godot 4.7.2 mono binary. The icons must be converted (tools/convert-tg.sh does that).
set -eu
project=$(cd "$(dirname "$0")/.." && pwd)
out=${1:?usage: interaction-shots.sh OUTDIR}
mkdir -p "$out"
if [ -z "${GODOT:-}" ]; then
    GODOT=$(command -v godot-mono || echo /Applications/Godot_mono.app/Contents/MacOS/Godot)
fi
shot() {
    name=$1
    at=$2
    echo "== $name to $out/$name.png (opens a window)"
    ANOMALY_FAKE_INVENTORY=1 ANOMALY_SHOT=$name ANOMALY_FAKE_THROW_TICKS=${THROW_TICKS:-1} "$GODOT" --path "$project" --resolution 1280x720 -- --fake-server --fake-bots=0 --seed=3 \
        --keys=1:F4+,1.1:F4- --screenshot-after="$at","$out/$name.png"
    test -s "$out/$name.png" || { echo "interaction-shots: no screenshot written" >&2; exit 1; }
}
shot hover 2.6
shot radial 3.2
shot hud 3.4
shot container 3.8
THROW_TICKS=6 shot throw 4.1
echo "interaction-shots: OK"
