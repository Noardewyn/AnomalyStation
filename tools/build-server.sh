#!/bin/sh
# Builds the server for release (optimized, self-contained: the machine needs no .NET) with the data it reads, and zips it:
#
#   tools/build-server.sh                # this host's platform
#   tools/build-server.sh win-x64        # or osx-arm64, osx-x64, linux-x64: .NET builds any of them on any host
#
# writes build/server-<rid>/ and dist/AnomalyStation-server-<rid>.zip. The folder is laid out as the repository is, so the server finds what it looks for from its
# working folder (ServerHost: the maps upward, config/server.txt, the sound index, the models' catalog for the "dir" rules): config/
# (server.txt with REALTIME on, BIND_ADDRESS 0.0.0.0 and MAP metastation, a LAN or internet server: players' dev commands are then off unless DEV_COMMANDS 1
# or an admin types them; tg's admins.txt and admin_ranks.txt, where the host names the admins; tg's dynamic.toml), maps/,
# godot/assets/models/models.json, godot/assets/tg/sound/index.json, CHANGELOG.md (the changelog window), and tgstation/strings/ with tgstation/config/blanks.json and silicon_laws.txt
# (tg's name lists and the two config files, TgStrings; tgstation.dme marks the folder).
# run-server.cmd (Windows) and run-server.sh (macOS, Linux) start it from its folder; their arguments go to the server (--map metastation, --port 7778, ...).
set -eu

repo=$(cd "$(dirname "$0")/.." && pwd)
. "$repo/godot/tools/export-common.sh"

rid=${1:-}
if [ -z "$rid" ]; then
    case "$(host_os)" in
        windows) rid=win-x64 ;;
        macos) if [ "$(uname -m)" = arm64 ]; then rid=osx-arm64; else rid=osx-x64; fi ;;
        *) rid=linux-x64 ;;
    esac
fi
case "$rid" in win-x64|osx-arm64|osx-x64|linux-x64) ;; *) echo "error: the platform is win-x64, osx-arm64, osx-x64 or linux-x64, not $rid" >&2; exit 2 ;; esac

ensure_tg_data
tg=${ANOMALY_TGSTATION:-$repo/../tgstation}
test -d "$tg/strings" || { echo "error: tg's strings/ folder is missing from $tg (set ANOMALY_TGSTATION)" >&2; exit 1; }

out=$repo/build/server-$rid
dist=$repo/dist
rm -rf "$out"
mkdir -p "$out" "$dist"
echo "== publish the server for $rid into $out"
# NuGetAudit off: a vulnerability lookup must not need the network once the packages are cached.
dotnet publish "$repo/src/Server/Anomaly.Server.csproj" -c Release -r "$rid" --self-contained true -p:NuGetAudit=false -nologo -v quiet -o "$out"
rm -f "$out"/*.pdb

exe=Anomaly.Server
case "$rid" in win-*) exe=Anomaly.Server.exe ;; esac
test -s "$out/$exe" || { echo "build-server: $exe is missing from $out" >&2; exit 1; }

echo "== the server's data"
mkdir -p "$out/config" "$out/godot/assets/models" "$out/godot/assets/tg/sound" "$out/tgstation"
sed -e 's/^BIND_ADDRESS .*/BIND_ADDRESS 0.0.0.0/' -e 's/^#REALTIME$/REALTIME/' -e 's/^#MAP metastation$/MAP metastation/' "$repo/config/server.txt" > "$out/config/server.txt"
grep -q '^REALTIME$' "$out/config/server.txt" && grep -q '^BIND_ADDRESS 0.0.0.0$' "$out/config/server.txt" && grep -q '^MAP metastation$' "$out/config/server.txt" \
    || { echo "build-server: config/server.txt has no REALTIME, BIND_ADDRESS or #MAP metastation line to set" >&2; exit 1; }
cp "$repo/config/admins.txt" "$repo/config/admin_ranks.txt" "$repo/config/dynamic.toml" "$out/config/"
cp -R "$repo/maps" "$out/maps"
cp "$repo/godot/assets/models/models.json" "$out/godot/assets/models/models.json"
cp "$repo/godot/assets/tg/sound/index.json" "$out/godot/assets/tg/sound/index.json"
cp "$repo/CHANGELOG.md" "$out/CHANGELOG.md" # tg's changelog window reads it (datum_changelog, ServerHost.MountChangelog)
cp -R "$tg/strings" "$out/tgstation/strings"
rm -f "$out/tgstation/strings/cas_black.txt" "$out/tgstation/strings/cas_white.txt" # D343: CC BY-NC-SA 2.0, never mounted
# the two tg config files TgStrings mounts with the strings (the photocopier's forms, the custom AI law board's default laws)
mkdir -p "$out/tgstation/config"
cp "$tg/config/blanks.json" "$tg/config/silicon_laws.txt" "$out/tgstation/config/"
: > "$out/tgstation/tgstation.dme"

# cmd misreads a batch file with bare LF line ends (%~dp0 among others): CRLF.
cr=$(printf '\r')
{
    echo "@echo off$cr"
    echo "rem Starts the server from its folder; the arguments go to it (run-server.cmd --map metastation). Stop it with Ctrl+C or its console quit.$cr"
    echo "cd /d \"%~dp0\"$cr"
    echo "set \"ANOMALY_TGSTATION=%~dp0tgstation\"$cr"
    echo "\"%~dp0Anomaly.Server.exe\" %*$cr"
} > "$out/run-server.cmd"
cat > "$out/run-server.sh" <<'SH'
#!/bin/sh
# Starts the server from its folder; the arguments go to it (./run-server.sh --map metastation). Stop it with Ctrl+C or the console's quit.
cd "$(dirname "$0")"
export ANOMALY_TGSTATION="$PWD/tgstation"
if [ "$(uname -s)" = Darwin ]; then
    # a copy from a chat or the internet is quarantined; a server built off a Mac is unsigned, which Apple silicon refuses: signed ad hoc once
    xattr -dr com.apple.quarantine . 2>/dev/null || true
    codesign -v ./Anomaly.Server 2>/dev/null || codesign --force -s - ./Anomaly.Server
fi
exec ./Anomaly.Server "$@"
SH
chmod +x "$out/run-server.sh" "$out/$exe" 2>/dev/null || true

zip_folder "$repo/build" "server-$rid" "$dist/AnomalyStation-server-$rid.zip" "$exe createdump"
echo "build-server: OK, $out"
echo "build-server: zip $(du -h "$dist/AnomalyStation-server-$rid.zip" | cut -f1): $dist/AnomalyStation-server-$rid.zip"
