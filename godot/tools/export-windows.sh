#!/bin/sh
# Exports the client for Windows x64 with --bench built in (docs/ai/specs/apps.md 2.11, D118, D123) and zips it with its run sheet:
#
#   godot/tools/export-windows.sh            # writes build/win-x64/ and dist/AnomalyStation-client-win-x64.zip (+ dist/README-windows.txt), both git-ignored
#
# Steps: convert tg's icons, copy tg's sounds (tools/Anomaly.Tools.Sounds, M4b: sound/ and sound-nc/ come along with assets/tg) and convert the maps when they are missing; run Godot's release export of the preset godot/tools/export_presets.windows.cfg (copied to the
# git-ignored godot/export_presets.cfg) into build/win-x64/AnomalyStation.exe (embedded PCK); make sure the .NET assemblies are next to it;
# the converted icons (the client reads them as plain files, Render/IconPaths.cs, ANOMALY_ICON_ROOT overrides the folder) and the converted maps; check the result; zip it.
#
# The .NET assemblies (M3 I1d: self-contained, so the PC needs no .NET install): Godot's own export publishes the project SELF-CONTAINED for win-x64, which needs
# the runtime pack Microsoft.NETCore.App.Runtime.win-x64 (about 65 MB, from nuget.org: the only download, D123's Windows build) in the NuGet cache. When Godot's
# publish left no assemblies (the pack was not in the cache yet, or Godot's publish failed after the PCK was written), this script publishes the same project
# itself, self-contained (dotnet publish -r win-x64 --self-contained true restores the pack from nuget.org), into the data folder Godot would have made. The
# result is checked for the runtime (hostfxr.dll, coreclr.dll, System.Private.CoreLib.dll), which is what makes Godot start the bundled runtime instead of looking
# for an installed one. ANOMALY_FRAMEWORK_DEPENDENT=1 keeps the old framework-dependent publish (the PC then needs the .NET 10 x64 runtime). Needs the Godot 4.7.2
# mono export templates (Editor > Manage Export Templates) and the .NET 10 SDK. Runs on macOS and on Windows (Git Bash); GODOT overrides the binary
# (export-common.sh finds it), GODOT_TEMPLATES the templates' folder.
set -eu

project=$(cd "$(dirname "$0")/.." && pwd)
repo=$(cd "$project/.." && pwd)
. "$project/tools/export-common.sh"
GODOT=$(find_godot)
need_template windows_release_x86_64.exe
ensure_tg_data

cp "$project/tools/export_presets.windows.cfg" "$project/export_presets.cfg"
out=$repo/build/win-x64
dist=$repo/dist
rm -rf "$out"
mkdir -p "$out" "$dist"
# Godot's export of the preset to the executable $1, its .NET assemblies in the data folder beside it (published here when Godot's publish left
# none), checked. Sets mode. AnomalyEditor=true in the environment builds the map editor in (Godot's publish and this script's read it).
export_client() {
    mkdir -p "$(dirname "$1")"
    echo "== export to $1"
    "$GODOT" --headless --path "$project" --export-release "Windows x64 (bench)" "$1" || echo "export-windows: Godot's export reported a failure (see above); checking what it wrote"
    test -s "$1" || { echo "export-windows: no executable written" >&2; exit 1; }

    # The data folder the exported executable reads its assemblies from: data_<assembly name>_windows_x86_64 (Godot's GetCsharpProjectName; the assembly is Anomaly.Client).
    data=$(dirname "$1")/data_Anomaly.Client_windows_x86_64
    mode="self-contained (Godot's own publish)"
    if [ ! -f "$data/Anomaly.Client.dll" ]; then
        rm -rf "$data"
        if [ "${ANOMALY_FRAMEWORK_DEPENDENT:-0}" = 1 ]; then
            mode="framework-dependent (needs the .NET 10 x64 runtime on the PC)"
            echo "== Godot's publish left no assemblies: publish framework-dependent into $data"
            # NuGetAudit off: every package is in the local cache, a vulnerability lookup must not need the network.
            dotnet publish "$project/Anomaly.Client.csproj" -c ExportRelease -r win-x64 --self-contained false \
                -p:UseAppHost=false -p:GodotTargetPlatform=windows -p:NuGetAudit=false -nologo -v quiet -o "$data"
        else
            mode="self-contained (the .NET runtime is in the data folder; nothing to install)"
            echo "== Godot's publish left no assemblies: publish self-contained into $data (the win-x64 runtime pack comes from nuget.org once)"
            dotnet publish "$project/Anomaly.Client.csproj" -c ExportRelease -r win-x64 --self-contained true \
                -p:UseAppHost=false -p:GodotTargetPlatform=windows -p:NuGetAudit=false -nologo -v quiet -o "$data"
        fi
        rm -f "$data"/*.pdb
    fi
    case "$mode" in
        self-contained*)
            for f in hostfxr.dll hostpolicy.dll coreclr.dll System.Private.CoreLib.dll; do
                test -s "$data/$f" || { echo "export-windows: $f is missing from $data (not a self-contained publish)" >&2; exit 1; }
            done
            grep -q '"includedFrameworks"' "$data/Anomaly.Client.runtimeconfig.json" || { echo "export-windows: the runtimeconfig is not self-contained" >&2; exit 1; } ;;
    esac
    for f in Anomaly.Client.dll GodotSharp.dll Anomaly.Core.Tg.dll Anomaly.Core.Kernel.dll Anomaly.Core.DmShim.dll Anomaly.Core.Motion.dll Anomaly.Protocol.dll Anomaly.Net.dll LiteNetLib.dll Anomaly.Client.runtimeconfig.json; do
        test -s "$data/$f" || { echo "export-windows: $f is missing from $data" >&2; exit 1; }
    done
}

export_client "$out/AnomalyStation.exe"
test ! -e "$out/data_Anomaly.Client_windows_x86_64/Anomaly.Server.dll" || { echo "export-windows: the game's build carries the server (AnomalyEditor)" >&2; exit 1; }
# Anomaly Editor (docs/ai/specs/map-editor.md 5.13): the same project with the editor and the game's server built in, in a folder of its own (its data
# folder has the game's name); the game's data beside it is the editor's too (DataPaths, TgStrings.Locate)
(export AnomalyEditor=true; export_client "$out/editor/AnomalyEditor.exe")
test -s "$out/editor/data_Anomaly.Client_windows_x86_64/Anomaly.Server.dll" || { echo "export-windows: the editor's build has no server" >&2; exit 1; }

# tg's icons and sounds, the 3D models, the maps and the map patches beside the executable (Render/DataPaths.cs).
copy_client_data "$out"

# A Windows x64 PE file: "MZ", and the machine field 0x8664 at the PE header.
"$(find_python)" - "$out/AnomalyStation.exe" <<'PY'
import struct, sys
d = open(sys.argv[1], 'rb').read(4096)
pe = struct.unpack_from('<I', d, 0x3c)[0]
assert d[:2] == b'MZ' and d[pe:pe + 4] == b'PE\0\0', 'not a PE file'
machine = struct.unpack_from('<H', d, pe + 4)[0]
assert machine == 0x8664, 'machine %#x is not x86-64' % machine
print('export-windows: AnomalyStation.exe is a Windows x64 PE file')
PY

copy_readmes "$out" windows
zip_folder "$repo/build" win-x64 "$dist/AnomalyStation-client-win-x64.zip"
echo "export-windows: OK, $out ($mode)"
echo "export-windows: zip $(du -h "$dist/AnomalyStation-client-win-x64.zip" | cut -f1): $dist/AnomalyStation-client-win-x64.zip; run sheet: $dist/README-windows.txt"
