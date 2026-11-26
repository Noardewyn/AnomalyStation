#!/bin/sh
# Publishes the game clients as files of the GitHub release TAG, beside the server tools/publish-server.sh puts there, with the
# client manifest dist/client-build.json pointing at them (docs/hosting.md). Needs GitHub's gh, logged in (gh auth login):
#
#   tools/publish-clients.sh v1.14.0-pre-alpha              # export the Windows, Linux and macOS clients from the tag's commit, upload them
#   tools/publish-clients.sh v1.14.0-pre-alpha --no-build   # upload the client zips of dist/ built before
#
# The tag must be on GitHub already (git push origin main v1.14.0-pre-alpha). With ANOMALY_SIGNING_KEY set to the project's private
# key the clients are signed as official builds (tools/make-client-manifest.sh). Run again, it replaces the release's client files.
set -eu

repo=$(cd "$(dirname "$0")/.." && pwd)
cd "$repo"
tag=${1:?usage: tools/publish-clients.sh TAG [--no-build]}
version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Version.props | head -1)
# The manifest takes its version from Version.props, which must be the tag's.
[ "$tag" = "v$version" ] || { echo "publish-clients: $tag is not this checkout's version v$version" >&2; exit 1; }
if [ "${2:-}" != --no-build ]; then
    [ "$(git rev-parse HEAD)" = "$(git rev-parse "$tag^{commit}")" ] || { echo "publish-clients: HEAD is not $tag; check the tag out first" >&2; exit 1; }
    rm -f dist/AnomalyStation-client-*.zip
    godot/tools/export-windows.sh
    godot/tools/export-linux.sh
    godot/tools/export-macos.sh
fi
[ -n "${ANOMALY_SIGNING_KEY:-}" ] || echo "publish-clients: ANOMALY_SIGNING_KEY is not set: the clients go out unsigned" >&2

slug=$(gh repo view --json nameWithOwner -q .nameWithOwner)
tools/make-client-manifest.sh "https://github.com/$slug/releases/download/$tag"
set -- dist/AnomalyStation-client-*.zip dist/client-build.json

if gh release view "$tag" >/dev/null 2>&1; then
    gh release upload "$tag" "$@" --clobber
else
    gh release create "$tag" "$@" --verify-tag --title "$tag" --notes "Anomaly Station $tag"
fi
echo "publish-clients: OK, $tag; copy dist/client-build.json to each server's config/ (CLIENT_BUILD)"
