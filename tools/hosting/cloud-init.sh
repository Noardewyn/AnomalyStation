#!/bin/bash
# A server that installs itself on its first boot (docs/hosting.md): paste this whole file into the provider's cloud-init field ("Cloud config",
# "User data") when you create an Ubuntu 24.04 server, and fill in the settings below there, in the field: never commit a token to this file.
# The server downloads the newest GitHub release that tools/publish-server.sh made (or TAG's) and installs it with that release's
# install-server.sh: the systemd service anomaly on UDP 7777, which the provider's firewall must let in. Its log: /var/log/cloud-init-output.log.
set -euo pipefail

GITHUB_TOKEN=''                  # fine-grained: this repository only, Contents read-only (any process on the server can read the user data back)
REPO='Noardewyn/AnomalyStation'
TAG=''                           # a release such as v1.1.13-pre-alpha; empty: the newest one
ADMINS=''                        # lines for config/admins.txt, one admin a line: 'YourName = Host'

# an asset with "|" takes the first name a release has: releases before D347 named the server Ss3d-server-linux-x64.zip
assets='AnomalyStation-server-linux-x64.zip|Ss3d-server-linux-x64.zip install-server.sh'
auth=()
[ -z "$GITHUB_TOKEN" ] || auth=(-H "Authorization: Bearer $GITHUB_TOKEN")
# every error is retried: a first boot's network may settle late. curl drops the token on the redirect to the file's storage host.
get() { curl -fsSL --retry 5 --retry-all-errors "${auth[@]}" -H 'X-GitHub-Api-Version: 2022-11-28' "$@"; }

if [ -n "$TAG" ]; then
    releases=$(get "https://api.github.com/repos/$REPO/releases/tags/$TAG")
else
    releases=$(get "https://api.github.com/repos/$REPO/releases?per_page=20")
fi
# "tag url url": the newest published release with every asset (the list is newest first)
picked=$(python3 -c '
import json, sys
specs = [s.split("|") for s in sys.argv[1:]]
data = json.load(sys.stdin)
for r in data if isinstance(data, list) else [data]:
    urls = {a["name"]: a["url"] for a in r["assets"]}
    found = [next((urls[n] for n in names if n in urls), None) for names in specs]
    if not r["draft"] and None not in found:
        print(r["tag_name"], *found)
        break
else:
    sys.exit("cloud-init: no release of the repository has " + " and ".join(sys.argv[1:]))
' $assets <<<"$releases")
read -r tag zip_url install_url <<<"$picked"

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
get -H 'Accept: application/octet-stream' -o "$work/server.zip" "$zip_url"
get -H 'Accept: application/octet-stream' -o "$work/install-server.sh" "$install_url"
echo "cloud-init: installing $REPO $tag"
sh "$work/install-server.sh" "$work/server.zip"

if [ -n "$ADMINS" ]; then
    printf '\n%s\n' "$ADMINS" >> /opt/anomaly/config/admins.txt
    systemctl restart anomaly
fi
echo "cloud-init: OK, $tag runs (journalctl -u anomaly -f: \"joins open\" when players can join)"
