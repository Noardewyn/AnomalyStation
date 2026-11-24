#!/bin/sh
# Shared steps of the client exports (export-windows.sh, export-macos.sh, export-linux.sh) and the server build (tools/build-server.sh), sourced by them: where Godot, its
# export templates and Python are on this host (macOS, Windows' Git Bash or Linux), tg's converted data the builds carry (made from ../tgstation when
# missing), the client's plain-file data copied beside a build, and a zip that keeps the executables' modes. Expects $repo (the repository's root).

GODOT_VERSION=4.7.2.stable.mono
# No lingering MSBuild nodes or compiler server: they inherit Godot's console pipe and hold its export open for minutes after Godot exits.
export MSBUILDDISABLENODEREUSE=1 UseSharedCompilation=false

# The host: macos, windows or linux.
host_os() {
    case "$(uname -s)" in
        Darwin) echo macos ;;
        MINGW*|MSYS*|CYGWIN*) echo windows ;;
        *) echo linux ;;
    esac
}

# Godot 4.7.2 mono: $GODOT, else the usual places. On Windows the winget link (godot.exe) starts Godot away from its GodotSharp folder, which then finds
# no .NET assemblies, so the installed executable itself is taken.
find_godot() {
    if [ -n "${GODOT:-}" ]; then echo "$GODOT"; return; fi
    case "$(host_os)" in
        macos)
            command -v godot-mono 2>/dev/null || echo /Applications/Godot_mono.app/Contents/MacOS/Godot ;;
        windows)
            for g in "${LOCALAPPDATA:-}"/Microsoft/WinGet/Packages/GodotEngine.GodotEngine.Mono_*/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe; do
                if [ -f "$g" ]; then echo "$g"; return; fi
            done
            command -v Godot_v4.7.2-stable_mono_win64_console.exe 2>/dev/null || command -v godot ;;
        *)
            command -v godot-mono 2>/dev/null || command -v godot ;;
    esac
}

# The export templates' folder of this Godot ($GODOT_TEMPLATES overrides it).
templates_dir() {
    if [ -n "${GODOT_TEMPLATES:-}" ]; then echo "$GODOT_TEMPLATES"; return; fi
    case "$(host_os)" in
        macos) echo "$HOME/Library/Application Support/Godot/export_templates/$GODOT_VERSION" ;;
        windows) echo "${APPDATA:-$HOME/AppData/Roaming}/Godot/export_templates/$GODOT_VERSION" ;;
        *) echo "${XDG_DATA_HOME:-$HOME/.local/share}/godot/export_templates/$GODOT_VERSION" ;;
    esac
}

# Stops with a hint when the export template $1 (a file in the templates' folder) is missing.
need_template() {
    t=$(templates_dir)
    if [ ! -f "$t/$1" ]; then
        echo "error: Godot's export template $1 is missing from $t" >&2
        echo "       install the $GODOT_VERSION export templates: Godot editor > Editor > Manage Export Templates > Download and Install" >&2
        exit 1
    fi
}

# Python 3 (Windows' python3 may be the Store's stub that only opens the Store).
find_python() {
    for p in python3 python py; do
        if command -v "$p" >/dev/null 2>&1 && "$p" -c "import sys; sys.exit(0 if sys.version_info[0] == 3 else 1)" >/dev/null 2>&1; then
            echo "$p"; return
        fi
    done
    echo "error: no Python 3 found (needed to zip the build)" >&2
    exit 1
}

# tg's data the builds carry, converted from the tgstation clone: the icons and map fonts, the sounds and their index when missing, the maps when stale.
ensure_tg_data() {
    tg=${ANOMALY_TGSTATION:-$repo/../tgstation}
    if [ ! -d "$repo/godot/assets/tg/icons" ] || [ ! -d "$repo/godot/assets/tg/fonts" ]; then
        echo "== convert tg's icons and copy its map fonts from $tg"
        dotnet run --project "$repo/tools/Anomaly.Tools.Icons" -c Release -- --tg "$tg" --out "$repo/godot/assets/tg"
    fi
    if [ ! -f "$repo/godot/assets/tg/sound/index.json" ]; then
        echo "== copy tg's sounds from $tg"
        dotnet run --project "$repo/tools/Anomaly.Tools.Sounds" -c Release -- --tg "$tg" --out "$repo/godot/assets/tg"
    fi
    # the maps are checked in; the script converts tg's only where the folder has none
    echo "== the maps: maps/ as checked in"
    ANOMALY_TG="$tg" "$repo/tools/convert-maps.sh"
}

# The client's plain-file data into the folder $1 (Render/DataPaths.cs finds it there): tg's icons and sounds, the 3D models without their Blender
# sources and READMEs (docs/ai/specs/models.md), and the maps.
copy_client_data() {
    mkdir -p "$1/assets"
    rm -rf "$1/assets/tg" "$1/assets/models" "$1/maps" "$1/map_patches" # (map_patches: a folder of an export from before 2026-11-17)
    cp -R "$repo/godot/assets/tg" "$1/assets/tg"
    # D345: a free build keeps the non-commercial sounds; ANOMALY_SKIP_NC=1 leaves them out (before any commercial release or paid perks)
    if [ "${ANOMALY_SKIP_NC:-0}" = 1 ]; then rm -rf "$1/assets/tg/sound-nc"; fi
    if [ -d "$repo/godot/assets/models" ]; then
        cp -R "$repo/godot/assets/models" "$1/assets/models"
        find "$1/assets/models" \( -name "*.blend" -o -name "*.blend1" -o -name "*.md" \) -delete
    fi
    cp -R "$repo/maps" "$1/maps"
    # tg's name lists and config files: the editor's Play runs the server in the program, and its boot reads them (TgStrings.Locate finds this folder)
    tg=${ANOMALY_TGSTATION:-$repo/../tgstation}
    rm -rf "$1/tgstation"
    mkdir -p "$1/tgstation/config"
    cp -R "$tg/strings" "$1/tgstation/strings"
    rm -f "$1/tgstation/strings/cas_black.txt" "$1/tgstation/strings/cas_white.txt" # D343: CC BY-NC-SA 2.0, never mounted
    cp "$tg/config/blanks.json" "$tg/config/silicon_laws.txt" "$1/tgstation/config/"
    : > "$1/tgstation/tgstation.dme"
}

# The players' READMEs into the export folder $1 for the platform $2 (windows, linux or macos): README.txt (the game, the editor, launch problems of
# this platform), editor/README.txt (the editor's own), and a copy of the first beside the zip in dist/.
copy_readmes() {
    cp "$repo/godot/tools/README-$2.txt" "$1/README.txt"
    cp "$repo/godot/tools/README-editor.txt" "$1/editor/README.txt"
    cp "$1/README.txt" "$repo/dist/README-$2.txt"
}

# Zips the folder $1/$2 into $3 with $2 as its top folder, keeping each file's mode (an executable stays one when unzipped on macOS or Linux) and symbolic
# links as links (a macOS bundle's frameworks). Windows' file system keeps no execute bit, so the files named in $4 (space-separated, relative to $1/$2),
# the launchers (.sh, .command) and a bundle's Contents/MacOS/ are marked executable whatever the host says.
zip_folder() {
    py=$(find_python)
    rm -f "$3"
    "$py" - "$1" "$2" "$3" "${4:-}" <<'PY'
import os, stat, sys, zipfile
parent, name, out = sys.argv[1], sys.argv[2], sys.argv[3]
executables = {name + '/' + e for e in sys.argv[4].split()}
with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for root, dirs, files in os.walk(os.path.join(parent, name)):
        for entry in sorted(dirs) + sorted(files):
            full = os.path.join(root, entry)
            arc = os.path.relpath(full, parent).replace(os.sep, '/')
            st = os.lstat(full)
            if stat.S_ISLNK(st.st_mode):
                info = zipfile.ZipInfo(arc)
                info.create_system = 3
                info.external_attr = (stat.S_IFLNK | 0o777) << 16
                z.writestr(info, os.readlink(full))
            elif stat.S_ISDIR(st.st_mode):
                if os.path.islink(full):
                    continue
                info = zipfile.ZipInfo(arc + '/')
                info.create_system = 3
                info.external_attr = (stat.S_IFDIR | 0o755) << 16
                z.writestr(info, b'')
            else:
                info = zipfile.ZipInfo.from_file(full, arc)
                info.create_system = 3
                mode = st.st_mode & 0o777
                if arc in executables or arc.endswith(('.sh', '.command')) or '/Contents/MacOS/' in arc:
                    mode |= 0o755
                info.external_attr = (stat.S_IFREG | mode) << 16
                info.compress_type = zipfile.ZIP_DEFLATED
                with open(full, 'rb') as f:
                    z.writestr(info, f.read())
PY
}
