#!/bin/sh
# Exports the client for macOS (a release build) and zips it with its run sheet:
#
#   godot/tools/export-macos.sh              # writes build/macos/ (Anomaly Station.app and its data beside it) and dist/AnomalyStation-client-macos-universal.zip
#
# The app is universal (Apple silicon and Intel: Godot's mono template for macOS is one universal binary, and the bundle carries the .NET runtime of both).
#
# Steps: tg's data (export-common.sh: converted from ../tgstation when missing); Godot's release export of the preset godot/tools/export_presets.macos.cfg
# (copied to the git-ignored godot/export_presets.cfg) into a .zip holding Anomaly Station.app, with its .NET assemblies inside the bundle; unpacked into
# build/macos/; signed ad hoc by rcodesign (RCODESIGN overrides the binary; on a Mac without it, Apple's codesign), no hardened runtime; tg's icons and sounds, the 3D models, the maps and the map patches copied BESIDE the .app (Render/DataPaths.cs finds them
# there; inside, they would break the bundle's signature); zipped. Needs the Godot 4.7.2 mono export templates (Editor > Manage Export Templates) and the
# .NET 10 SDK. Runs on macOS, Windows (Git Bash) or Linux: GODOT overrides the binary, GODOT_TEMPLATES the templates' folder.
set -eu

project=$(cd "$(dirname "$0")/.." && pwd)
repo=$(cd "$project/.." && pwd)
. "$project/tools/export-common.sh"
GODOT=$(find_godot)
need_template macos.zip
ensure_tg_data

arch=universal
cp "$project/tools/export_presets.macos.cfg" "$project/export_presets.cfg"

out=$repo/build/macos
dist=$repo/dist
rm -rf "$out"
mkdir -p "$out" "$dist"
# Godot's export of the preset into the folder $1: Anomaly Station.app, unpacked from Godot's zip, checked and signed. AnomalyEditor=true in the
# environment builds the map editor in (Godot's publish reads it).
export_app() {
    mkdir -p "$1"
    echo "== export to $1 ($arch)"
    "$GODOT" --headless --path "$project" --export-release "macOS" "$1/AnomalyStation.zip" || echo "export-macos: Godot's export reported a failure (see above); checking what it wrote"
    test -s "$1/AnomalyStation.zip" || { echo "export-macos: no AnomalyStation.zip written" >&2; exit 1; }

    # Unpack the bundle, keeping its executables' modes and its frameworks' symbolic links.
    "$(find_python)" - "$1/AnomalyStation.zip" "$1" <<'PY'
import os, stat, sys, zipfile
src, dest = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(src) as z:
    for info in z.infolist():
        path = os.path.join(dest, info.filename)
        mode = info.external_attr >> 16
        if info.is_dir():
            os.makedirs(path, exist_ok=True)
        elif stat.S_ISLNK(mode):
            os.makedirs(os.path.dirname(path), exist_ok=True)
            target = z.read(info).decode()
            try:
                os.symlink(target, path)
            except OSError:  # Windows without the symlink right: a copy of the link's text, which zip_folder writes back as a file
                open(path, 'w').write(target)
        else:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, 'wb') as f:
                f.write(z.read(info))
            if mode & 0o777:
                os.chmod(path, mode & 0o777)
PY
    rm -f "$1/AnomalyStation.zip"
    app="$1/Anomaly Station.app"
    test -d "$app/Contents/MacOS" || { echo "export-macos: no Anomaly Station.app in the export" >&2; exit 1; }
    find "$app" -name Anomaly.Client.dll | grep -q . || { echo "export-macos: the bundle has no .NET assemblies (Anomaly.Client.dll)" >&2; exit 1; }

    # The editor's app carries the editor's name (its executable's name starts it as the editor, ClientArgs.EditorByName; the pack is named after the executable)
    if [ "${AnomalyEditor:-}" = true ]; then
        mv "$app/Contents/MacOS/Anomaly Station" "$app/Contents/MacOS/Anomaly Editor"
        mv "$app/Contents/Resources/Anomaly Station.pck" "$app/Contents/Resources/Anomaly Editor.pck"
        sed -i.bak -e 's|>Anomaly Station<|>Anomaly Editor<|g' -e 's|io\.github\.noardewyn\.anomalystation<|io.github.noardewyn.anomalystation.editor<|' "$app/Contents/Info.plist"
        rm -f "$app/Contents/Info.plist.bak"
        mv "$app" "$1/Anomaly Editor.app"
        app="$1/Anomaly Editor.app"
    fi

    # Apple silicon runs only signed code; Godot's own signer is off (export_presets.macos.cfg says why). rcodesign carries the template's flags (none) forward.
    rcodesign=${RCODESIGN:-$(command -v rcodesign 2>/dev/null || ls "${LOCALAPPDATA:-/nonexistent}"/rcodesign/*/rcodesign.exe 2>/dev/null | head -1 || true)}
    if [ -n "$rcodesign" ]; then
        "$rcodesign" sign "$app"
    elif [ "$(host_os)" = macos ]; then
        codesign --force --deep --sign - "$app"
    else
        echo "export-macos: no rcodesign to sign the app (github.com/indygreg/apple-platform-rs releases; RCODESIGN=path)" >&2; exit 1
    fi
}

export_app "$out"
test -z "$(find "$out/Anomaly Station.app" -name Anomaly.Server.dll)" || { echo "export-macos: the game's build carries the server (AnomalyEditor)" >&2; exit 1; }
# Anomaly Editor (docs/ai/specs/map-editor.md 5.13): the same project with the editor and the game's server built in, an app of its own, editor/Anomaly Editor.app; the game's data beside the game's app is the editor's too (DataPaths, TgStrings.Locate)
(export AnomalyEditor=true; export_app "$out/editor")
test -n "$(find "$out/editor/Anomaly Editor.app" -name Anomaly.Server.dll)" || { echo "export-macos: the editor's build has no server" >&2; exit 1; }

copy_client_data "$out"
copy_readmes "$out" macos
zip_folder "$repo/build" macos "$dist/AnomalyStation-client-macos-$arch.zip"
echo "export-macos: OK, $out"
echo "export-macos: zip $(du -h "$dist/AnomalyStation-client-macos-$arch.zip" | cut -f1): $dist/AnomalyStation-client-macos-$arch.zip; run sheet: $dist/README-macos.txt"
