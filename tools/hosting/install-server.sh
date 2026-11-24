#!/bin/sh
# Installs or updates the server on a Linux VPS (docs/hosting.md), as root; tools/deploy-server.sh uploads and runs it:
#
#   sudo sh install-server.sh AnomalyStation-server-linux-x64.zip
#
# Into /opt/anomaly, run by the system user anomaly as the systemd service anomaly: started at boot, stopped with SIGTERM (the server ends cleanly), started
# again 5 s after it stops for any reason (a crash, an admin's quit). An update replaces every file but config/ (the host's settings and admins; it
# only adds the config files the host does not have yet) and data/ (the database, the character saves, the bug reports). When ufw is on, the server's
# UDP port is opened in it.
set -eu

zip=${1:?usage: install-server.sh AnomalyStation-server-linux-x64.zip}
dir=/opt/anomaly

if ! command -v unzip >/dev/null; then
    # a new machine's first minutes may hold apt's locks (apt-daily, unattended-upgrades, cloud-init): tried again for about 3 minutes
    tries=0
    until apt-get -o DPkg::Lock::Timeout=10 update -q && apt-get -o DPkg::Lock::Timeout=10 install -y -q unzip; do
        tries=$((tries + 1)); [ "$tries" -lt 12 ] || exit 1; sleep 5
    done
fi
# D347: a server installed before the game was named Anomaly Station ran from /opt/ss3d as the service and user ss3d; it moves here once,
# with its config/ and data/ (an old config's DATABASE line still names data/ss3d.sqlite, which keeps working)
if [ -d /opt/ss3d ] && [ ! -e "$dir" ]; then
    systemctl stop ss3d 2>/dev/null || true
    systemctl disable ss3d 2>/dev/null || true
    rm -f /etc/systemd/system/ss3d.service
    mv /opt/ss3d "$dir"
    id ss3d >/dev/null 2>&1 && userdel ss3d 2>/dev/null || true
fi
id anomaly >/dev/null 2>&1 || useradd --system --home-dir "$dir" --shell /usr/sbin/nologin anomaly

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
unzip -q "$zip" -d "$tmp"
src=$tmp/server-linux-x64
test -x "$src/Anomaly.Server" || { echo "install-server: $zip holds no server-linux-x64/Anomaly.Server" >&2; exit 1; }

systemctl stop anomaly 2>/dev/null || true
mkdir -p "$dir/data"
find "$dir" -mindepth 1 -maxdepth 1 ! -name config ! -name data -exec rm -rf {} +
for item in "$src"/*; do
    if [ "$(basename "$item")" = config ] && [ -d "$dir/config" ]; then
        cp -Rn "$item"/. "$dir/config/" # the config files a release adds (tg's dynamic.toml); the host's own stay as they are
        continue
    fi
    cp -R "$item" "$dir/"
done
chown -R anomaly:anomaly "$dir"

cat > /etc/systemd/system/anomaly.service <<'UNIT'
[Unit]
Description=Anomaly Station server
Wants=network-online.target
After=network-online.target

[Service]
User=anomaly
WorkingDirectory=/opt/anomaly
ExecStart=/opt/anomaly/run-server.sh --no-console
Restart=always
RestartSec=5
TimeoutStopSec=60

[Install]
WantedBy=multi-user.target
UNIT
systemctl daemon-reload
systemctl enable anomaly
systemctl restart anomaly

port=$(awk 'toupper($1) == "PORT" { print $2 }' "$dir/config/server.txt")
port=${port:-7777}
if command -v ufw >/dev/null && ufw status | grep -q 'Status: active'; then ufw allow "$port/udp"; fi

echo "install-server: OK, the server runs from $dir on UDP port $port (its log: journalctl -u anomaly -f; settings: $dir/config/server.txt and admins.txt)"
