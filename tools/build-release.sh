#!/bin/sh
# Builds the game for release, optimized: the server (tools/build-server.sh) and the client (godot/tools/export-windows.sh, export-macos.sh, export-linux.sh), each zipped
# into dist/:
#
#   tools/build-release.sh               # this host's platform
#   tools/build-release.sh windows       # dist/AnomalyStation-server-win-x64.zip, dist/AnomalyStation-client-win-x64.zip
#   tools/build-release.sh macos         # dist/AnomalyStation-server-osx-arm64.zip, dist/AnomalyStation-client-macos-universal.zip (ANOMALY_MAC_ARCH=x86_64: an Intel Mac's server)
#   tools/build-release.sh linux         # dist/AnomalyStation-server-linux-x64.zip (for a VPS, docs/hosting.md), dist/AnomalyStation-client-linux-x64.zip
#   tools/build-release.sh all           # all three
#
# Any host builds any target (.NET and Godot export across platforms); the client needs Godot 4.7.2 mono and its export templates, both need the .NET 10
# SDK and tg's converted data (made from ../tgstation when missing). docs/versioning.md: a release (tools/release.sh) numbers the build, this makes it.
set -eu

repo=$(cd "$(dirname "$0")/.." && pwd)
. "$repo/godot/tools/export-common.sh"

target=${1:-$(host_os)}
build_windows() {
    "$repo/tools/build-server.sh" win-x64
    "$repo/godot/tools/export-windows.sh"
}
build_linux() {
    "$repo/tools/build-server.sh" linux-x64
    "$repo/godot/tools/export-linux.sh"
}
build_macos() {
    case "${ANOMALY_MAC_ARCH:-arm64}" in x86_64) rid=osx-x64 ;; *) rid=osx-arm64 ;; esac
    "$repo/tools/build-server.sh" "$rid"
    "$repo/godot/tools/export-macos.sh"
}
case "$target" in
    windows) build_windows ;;
    macos) build_macos ;;
    linux) build_linux ;;
    all) build_windows; build_macos; build_linux ;;
    *) echo "usage: tools/build-release.sh [windows|macos|linux|all] (default: this host's)" >&2; exit 2 ;;
esac
echo "build-release: done; the zips are in $repo/dist"
ls -l "$repo/dist"/*.zip
