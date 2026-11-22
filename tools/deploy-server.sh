#!/bin/sh
# Builds the Linux server and installs it on a VPS over SSH, or updates it there (docs/hosting.md):
#
#   tools/deploy-server.sh root@203.0.113.5               # build dist/AnomalyStation-server-linux-x64.zip, upload it, install or update, restart
#   tools/deploy-server.sh root@203.0.113.5 --no-build    # upload the zip built before
#
# The user on the VPS is root or may sudo. The server's config/ and data/ there stay as they are (tools/hosting/install-server.sh).
set -eu

repo=$(cd "$(dirname "$0")/.." && pwd)
host=${1:?usage: tools/deploy-server.sh USER@HOST [--no-build]}
[ "${2:-}" = --no-build ] || "$repo/tools/build-server.sh" linux-x64
zip=$repo/dist/AnomalyStation-server-linux-x64.zip
test -s "$zip" || { echo "deploy-server: $zip is missing (run without --no-build)" >&2; exit 1; }

scp "$zip" "$repo/tools/hosting/install-server.sh" "$host:/tmp/"
ssh -t "$host" 'sudo sh /tmp/install-server.sh /tmp/AnomalyStation-server-linux-x64.zip && rm -f /tmp/install-server.sh /tmp/AnomalyStation-server-linux-x64.zip'
