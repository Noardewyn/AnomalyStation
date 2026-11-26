#!/bin/sh
# Exports the client for Linux x64 (D9) and zips it with its run sheet:
#
#   godot/tools/export-linux.sh              # writes build/linux-x64/ and dist/AnomalyStation-client-linux-x64.zip (+ dist/README-linux.txt), both git-ignored
#
# The steps of export-windows.sh: tg's data (export-common.sh), Godot's release export of the preset godot/tools/export_presets.linux.cfg (copied to the
# git-ignored godot/export_presets.cfg) into build/linux-x64/AnomalyStation.x86_64 (embedded PCK), the .NET assemblies and runtime beside it (self-contained: the PC
# needs no .NET), tg's icons and sounds, the 3D models and the maps beside it (Render/DataPaths.cs); checked; zipped with the executable's mode kept. Needs the
# Godot 4.7.2 mono export templates and the .NET 10 SDK. Runs on Windows (Git Bash), macOS or Linux: GODOT overrides the binary, GODOT_TEMPLATES the templates' folder.
set -eu

project=$(cd "$(dirname "$0")/.." && pwd)
repo=$(cd "$project/.." && pwd)
. "$project/tools/export-common.sh"
GODOT=$(find_godot)
need_template linux_release.x86_64
ensure_tg_data

cp "$project/tools/export_presets.linux.cfg" "$project/export_presets.cfg"
out=$repo/build/linux-x64
dist=$repo/dist
exe=AnomalyStation.x86_64
rm -rf "$out"
mkdir -p "$out" "$dist"
# Godot's export of the preset to the executable $1, its .NET assemblies in the data folder beside it (published here when Godot's publish left
# none), checked. AnomalyEditor=true in the environment builds the map editor in (Godot's publish and this script's read it).
export_client() {
    mkdir -p "$(dirname "$1")"
    echo "== export to $1"
    "$GODOT" --headless --path "$project" --export-release "Linux x64" "$1" || echo "export-linux: Godot's export reported a failure (see above); checking what it wrote"
    test -s "$1" || { echo "export-linux: no executable written" >&2; exit 1; }

    # The data folder the executable reads its assemblies from: data_<assembly name>_linuxbsd_x86_64 (Godot's name for this platform).
    data=$(dirname "$1")/data_Anomaly.Client_linuxbsd_x86_64
    if [ ! -f "$data/Anomaly.Client.dll" ]; then
        rm -rf "$data"
        echo "== Godot's publish left no assemblies: publish self-contained into $data"
        dotnet publish "$project/Anomaly.Client.csproj" -c ExportRelease -r linux-x64 --self-contained true \
            -p:UseAppHost=false -p:GodotTargetPlatform=linuxbsd -p:NuGetAudit=false -nologo -v quiet -o "$data"
        rm -f "$data"/*.pdb
    fi
    # The runtime (what makes Godot start the bundled one), the game's assemblies, and the native libraries of the dev menu and of speech and translation.
    for f in libhostfxr.so libhostpolicy.so libcoreclr.so System.Private.CoreLib.dll Anomaly.Client.dll GodotSharp.dll Anomaly.Core.Tg.dll Anomaly.Core.Kernel.dll Anomaly.Core.DmShim.dll \
        Anomaly.Core.Motion.dll Anomaly.Protocol.dll Anomaly.Net.dll LiteNetLib.dll Anomaly.Client.runtimeconfig.json libcimgui.so libsherpa-onnx-c-api.so libonnxruntime.so; do
        test -s "$data/$f" || { echo "export-linux: $f is missing from $data" >&2; exit 1; }
    done
    grep -q '"includedFrameworks"' "$data/Anomaly.Client.runtimeconfig.json" || { echo "export-linux: the runtimeconfig is not self-contained" >&2; exit 1; }
}

export_client "$out/$exe"
test ! -e "$out/data_Anomaly.Client_linuxbsd_x86_64/Anomaly.Server.dll" || { echo "export-linux: the game's build carries the server (AnomalyEditor)" >&2; exit 1; }
# Anomaly Editor (docs/ai/specs/map-editor.md 5.13): the same project with the editor and the game's server built in, in a folder of its own (its data
# folder has the game's name); the game's data beside it is the editor's too (DataPaths, TgStrings.Locate)
(export AnomalyEditor=true; export_client "$out/editor/AnomalyEditor.x86_64")
test -s "$out/editor/data_Anomaly.Client_linuxbsd_x86_64/Anomaly.Server.dll" || { echo "export-linux: the editor's build has no server" >&2; exit 1; }

copy_client_data "$out"

# A Linux x86-64 ELF file: the magic, and the machine field 0x3E.
"$(find_python)" - "$out/$exe" <<'PY'
import struct, sys
d = open(sys.argv[1], 'rb').read(64)
assert d[:4] == b'\x7fELF', 'not an ELF file'
machine = struct.unpack_from('<H', d, 18)[0]
assert machine == 0x3e, 'machine %#x is not x86-64' % machine
print('export-linux: the executable is a Linux x86-64 ELF file')
PY

copy_readmes "$out" linux
zip_folder "$repo/build" linux-x64 "$dist/AnomalyStation-client-linux-x64.zip" "$exe editor/AnomalyEditor.x86_64"
echo "export-linux: OK, $out"
echo "export-linux: zip $(du -h "$dist/AnomalyStation-client-linux-x64.zip" | cut -f1): $dist/AnomalyStation-client-linux-x64.zip; run sheet: $dist/README-linux.txt"
