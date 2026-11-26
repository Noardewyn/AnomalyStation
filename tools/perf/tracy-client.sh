#!/bin/bash
# Builds Tracy's client library (TracyClient.dll / libTracyClient.so / .dylib) from a Tracy source checkout into build/tracy/, for
# profiling the game client with Tracy (docs/testing.md, "Profiling"). The game loads it only when ANOMALY_TRACY names it.
#
#   tools/perf/tracy-client.sh /path/to/tracy        a checkout of the same version as your tracy-profiler (the protocol is per version)
#   CMAKE=/path/to/cmake tools/perf/tracy-client.sh ...
#
# The library listens on localhost only, sends no broadcast, installs no crash handler (.NET handles its own faults) and collects
# only while a profiler or tracy-capture is connected.

set -eu

SRC=${1:?usage: tools/perf/tracy-client.sh TRACY_SOURCE_DIR}
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
OUT="$ROOT/build/tracy"
# built outside the repository: inside it MSBuild would take the repository's Directory.Build.props into CMake's projects
WORK="${TMPDIR:-/tmp}/anomaly-tracy-client"
CMAKE=${CMAKE:-cmake}

"$CMAKE" -S "$SRC" -B "$WORK" -DBUILD_SHARED_LIBS=ON -DCMAKE_BUILD_TYPE=Release \
    -DTRACY_ENABLE=ON -DTRACY_ON_DEMAND=ON -DTRACY_ONLY_LOCALHOST=ON -DTRACY_NO_BROADCAST=ON -DTRACY_NO_CRASH_HANDLER=ON \
    -DTRACY_NO_SAMPLING=ON -DTRACY_NO_SYSTEM_TRACING=ON -DTRACY_NO_CODE_TRANSFER=ON -DTRACY_NO_FRAME_IMAGE=ON \
    -DTRACY_DELAYED_INIT=ON -DTRACY_MANUAL_LIFETIME=ON
"$CMAKE" --build "$WORK" --config Release --target TracyClient
mkdir -p "$OUT"
find "$WORK" -maxdepth 2 \( -name "TracyClient.dll" -o -name "libTracyClient.so" -o -name "libTracyClient.dylib" \) -exec cp {} "$OUT/" \;
ls "$OUT"
