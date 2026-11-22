#!/bin/sh
# Publishes the Linux server as a GitHub release, which tools/hosting/cloud-init.sh installs on a new server (docs/hosting.md). Needs GitHub's
# gh, logged in (gh auth login):
#
#   tools/publish-server.sh v1.1.13-pre-alpha               # build dist/AnomalyStation-server-linux-x64.zip from the tag's commit, make the release or replace its files
#   tools/publish-server.sh v1.1.13-pre-alpha --no-build    # upload the zip built before
#
# The tag must be on GitHub already (git push origin main v1.1.13-pre-alpha): the server has to match the clients built from it.
set -eu

repo=$(cd "$(dirname "$0")/.." && pwd)
cd "$repo"
tag=${1:?usage: tools/publish-server.sh TAG [--no-build]}
if [ "${2:-}" != --no-build ]; then
    [ "$(git rev-parse HEAD)" = "$(git rev-parse "$tag^{commit}")" ] || { echo "publish-server: HEAD is not $tag; check the tag out first" >&2; exit 1; }
    tools/build-server.sh linux-x64
fi
zip=dist/AnomalyStation-server-linux-x64.zip
test -s "$zip" || { echo "publish-server: $zip is missing (run without --no-build)" >&2; exit 1; }

if gh release view "$tag" >/dev/null 2>&1; then
    gh release upload "$tag" "$zip" tools/hosting/install-server.sh --clobber
else
    gh release create "$tag" "$zip" tools/hosting/install-server.sh --verify-tag --title "Anomaly Station server $tag" \
        --notes "Anomaly Station server release"
fi
echo "publish-server: OK, $tag"
