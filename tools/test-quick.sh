#!/usr/bin/env bash
# The quick test run (docs/testing.md): the whole solution but the tests that boot the converted MetaStation (Category=Meta), a few
# minutes. The full run, Meta included, is for the end of a stage. Extra arguments go to dotnet test (another --filter is ANDed in
# by hand: tools/test-quick.sh --filter "Category!=Meta&FullyQualifiedName~Admin").
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build AnomalyStation.sln
# the game's own client, built as its export is, with no map editor and no server (AnomalyEditor, docs/ai/specs/map-editor.md 5.13): GameScene.NoEditor.cs
# must keep up with what the game's code reads of the editor. (ExportDebug: Godot's Debug output, which its editor runs, keeps the map editor.)
dotnet build godot/Anomaly.Client.csproj -c ExportDebug -nologo -v q
# Each test host's GC heap is capped at 16 GB (docs/testing.md): uncapped, the server GC on 32 cores let the Server tests grow to
# 22-27 GB and the Core tests to 12 GB, all of it heap headroom, not live worlds; at 8 GB the Core tests ran out of memory.
# Set DOTNET_GCHeapHardLimit yourself to override.
export DOTNET_GCHeapHardLimit="${DOTNET_GCHeapHardLimit:-0x400000000}"
if [[ " $* " == *" --filter "* ]]; then
    dotnet test AnomalyStation.sln --no-build "$@"
else
    dotnet test AnomalyStation.sln --no-build --filter "Category!=Meta" "$@"
fi
