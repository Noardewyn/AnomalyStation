#!/bin/sh
# Writes the JSON that names this build's game client for the launcher (D335). A server serves it in its status answer
# (CLIENT_BUILD, docs/hosting.md); the launcher downloads the zip it names, checks its SHA-256 and runs the program inside.
#
#   tools/make-client-manifest.sh https://example.org/builds/1.4.0     # writes dist/client-build.json from the client zips in dist/
#   tools/make-client-manifest.sh https://example.org/builds/1.4.0 OUT # ... into the file OUT
#
# The address is where the zips of dist/ will be downloaded from, under their own names: upload them there. With
# ANOMALY_SIGNING_KEY set to the project's private key (a PEM file, kept out of every repository) each download is signed as an
# official build; without it the launcher asks the player before it runs the build. The macOS zip is one universal .app for every
# Mac, under the platform key osx.
set -eu

repo=$(cd "$(dirname "$0")/.." && pwd)
base=${1:?usage: tools/make-client-manifest.sh URL_BASE [OUT]}
out=${2:-$repo/dist/client-build.json}
version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$repo/Version.props" | head -1)
[ -n "$version" ] || { echo "make-client-manifest: no <Version> in Version.props" >&2; exit 1; }

sha256() {
    if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | cut -d' ' -f1; else shasum -a 256 "$1" | cut -d' ' -f1; fi
}

entries=
# entry PLATFORM ZIP EXECUTABLE: the program's path inside the zip
entry() {
    zip="$repo/dist/$2"
    [ -f "$zip" ] || return 0
    sha=$(sha256 "$zip")
    size=$(wc -c < "$zip" | tr -d ' ')
    signature=
    if [ -n "${ANOMALY_SIGNING_KEY:-}" ]; then
        # the bytes the launcher checks (its BuildSignature): the version, the platform and the zip's hash
        signature=$(printf 'anomaly-client-v1\n%s\n%s\n%s' "$version" "$1" "$sha" | openssl dgst -sha256 -sign "$ANOMALY_SIGNING_KEY" | base64 | tr -d '\r\n')
        [ -n "$signature" ] || { echo "make-client-manifest: signing failed" >&2; exit 1; }
    fi
    [ -z "$entries" ] || entries="$entries,"
    entries="$entries
    \"$1\": { \"url\": \"${base%/}/$2\", \"sha256\": \"$sha\", \"size\": $size, \"executable\": \"$3\"${signature:+, \"signature\": \"$signature\"} }"
}
entry win-x64 AnomalyStation-client-win-x64.zip win-x64/AnomalyStation.exe
entry linux-x64 AnomalyStation-client-linux-x64.zip linux-x64/AnomalyStation.x86_64
entry osx AnomalyStation-client-macos-universal.zip "macos/Anomaly Station.app/Contents/MacOS/Anomaly Station"
[ -n "$entries" ] || { echo "make-client-manifest: no client zip in $repo/dist (tools/build-release.sh makes them)" >&2; exit 1; }

printf '{\n  "version": "%s",\n  "downloads": {%s\n  }\n}\n' "$version" "$entries" > "$out"
echo "make-client-manifest: wrote $out (version $version${ANOMALY_SIGNING_KEY:+, signed})"
