# Hosting a server on a VPS

How to run the game's server on a rented Linux machine for a play test: a Linux build with a public UDP port, restarted automatically when it
stops. The scripts are `tools/deploy-server.sh` (run here) and `tools/hosting/install-server.sh` (run on the VPS by the first); for a server
that installs itself when it is created, `tools/publish-server.sh` and `tools/hosting/cloud-init.sh` ([below](#a-server-that-installs-itself-cloud-init)).

## The machine

| | Least | Recommended |
|---|---|---|
| CPU | 2 vCPU | 4 vCPU of a fast core (the tick is mostly one thread) |
| Memory | 4 GB | 8 GB |
| Disk | 20 GB | 40 GB |
| System | Ubuntu 24.04 x64 | |

Measured: the server boots MetaStation in about 16 s and holds 1.3 GB with no players (2026-10-30, WSL); a 100-bot soak test held a heap of
1.1-1.4 GB at a p99 tick of 25-27 ms, with 13-21 KB/s sent to each player. Sixteen players use about 3 Mbit/s.
Any VPS of that size serves a play test of 8-16 people; pick a region near the players (the ping is what they feel). An ARM VPS needs a
`linux-arm64` build, which the scripts do not make yet.

## First install

1. Rent the VPS with Ubuntu 24.04 and log in with an SSH key (`ssh root@IP` works).
2. Open **UDP 7777** (the server's `PORT`) in the provider's firewall, if it has one. The install opens it in `ufw` when `ufw` is on.
3. From this repository (Git Bash on Windows, a terminal on macOS):

   ```bash
   tools/deploy-server.sh root@203.0.113.5
   ```

   It builds `dist/AnomalyStation-server-linux-x64.zip`, uploads it and installs it into `/opt/anomaly` as the systemd service `anomaly`, run by the system
   user `anomaly`: started at boot, stopped with SIGTERM (the round ends cleanly), started again 5 s after it stops (a crash, an admin's `quit`).
   A user other than root needs sudo.
4. Name the admins on the VPS, then restart:

   ```bash
   ssh root@203.0.113.5
   nano /opt/anomaly/config/admins.txt      # a line: YourName = Host (the ranks are config/admin_ranks.txt, tg's)
   systemctl restart anomaly
   ```

5. **Join first under each admin name.** A name belongs to the client that first connects with it (the claim is kept in `data/anomaly.sqlite`);
   until an admin has claimed theirs, anyone may.

The players connect with the client of the **same build** (an older or newer one is refused as `WrongVersion`) to the VPS's address and port
7777. A free DNS name (DuckDNS and the like) saves them typing the address.

## A server that installs itself (cloud-init)

For a server rented by the hour from a cloud provider: it is created for a play test, installs the server on its first boot with no SSH and no
upload from this machine, and is deleted afterwards. The build comes from a GitHub release.

Once:

1. Install GitHub's `gh` (`winget install GitHub.cli`) and log in: `gh auth login`.
2. Make a fine-grained token on GitHub (Settings, Developer settings, Personal access tokens, Fine-grained tokens): repository access
   *Only select repositories*, this one; permission *Contents: Read-only*; an expiry. Keep it outside the repository, with your own filled
   copy of `tools/hosting/cloud-init.sh`. The server's user data stays readable from inside it to every process (the provider's metadata address
   `169.254.169.254`), the game server's own among them: that is why the token can only read this repository.

Each version, after `tools/release.sh X.Y.Z`:

```bash
git push origin main vX.Y.Z
tools/publish-server.sh vX.Y.Z
tools/publish-clients.sh vX.Y.Z
```

The first builds the server from the tag's commit and makes the release `vX.Y.Z` with `AnomalyStation-server-linux-x64.zip` and `install-server.sh`.
The second exports the Windows, Linux and macOS clients (Godot 4.7.2 mono and its export templates) and adds their zips and the client manifest
`client-build.json` to the same release; set `ANOMALY_SIGNING_KEY` first to sign them as official builds. Copy `dist/client-build.json` to each
server's `config/` ([Settings](#settings), `CLIENT_BUILD`). Either script run again replaces its files in the release.

Each server, in the provider's server creation form:

1. Ubuntu 24.04, a location near the players, a size from [the table above](#the-machine), your SSH key (to read the log).
2. A firewall letting in **UDP 7777** (the game) and **TCP 22** (SSH): once a firewall is attached, everything else is shut.
3. *Cloud config*: paste `tools/hosting/cloud-init.sh` and fill in `GITHUB_TOKEN` and `ADMINS` (`TAG` empty takes the newest release).
4. Create it. A few minutes later the server runs: `ssh root@IP tail -f /var/log/cloud-init-output.log` ends with `cloud-init: OK`, then
   `journalctl -u anomaly -f` shows `joins open`.

The database is new on every such server: each admin joins first under their name (step 5 of [First install](#first-install)). The script runs
only on the first boot; a reboot starts the installed service. **Delete the server when the test is over**: a server is billed by the hour as
long as it exists, switched off or not. Deleting it deletes `data/` too; copy it off first if you want it ([Backups](#backups)).

## Settings

`/opt/anomaly/config/server.txt` (the sample `config/server.txt` explains every key); restart the service after a change. The server build sets
`REALTIME`, `BIND_ADDRESS 0.0.0.0` and `MAP metastation`. Worth setting for a play test:

- `BUG_REPORT_WEBHOOK_URL`: the Esc menu's bug reports, with their screenshots, posted to a Discord channel (they are also saved in
  `data/bug_reports/`).
- `ADMINHELP_WEBHOOK_URL`: admin helps posted to Discord when no admin is online.
- `RECORD_PATH data/commands.log`: the run's commands, to replay a bug (`--replay`). Keep it in `data/`: an update replaces every other
  folder.

For the launcher (the launcher's own repository, AnomalyLauncher, describes its side):

- `SERVER_NAME`, `SERVER_DESCRIPTION` and `SERVER_MODE` (free text, shown when set): what the launcher's server list shows, with
  the map the server has loaded.
- `STATUS`: the server answers `GET /status` over TCP on the game's port number, so open TCP 7777 beside UDP 7777. The launcher's
  hub asks it twice a minute, and so does a launcher that connects by address. The server is put on the list on the hub's host:
  `./Anomaly.Hub servers add play.example.org:7777`.
- `CLIENT_BUILD config/client-build.json`: the game client this server's players run. `tools/publish-clients.sh vX.Y.Z` puts the
  clients in the GitHub release and writes this file for them. To host the zips elsewhere, after a release build upload the client
  zips of `dist/` and run `tools/make-client-manifest.sh https://<where the zips are>`: it writes `dist/client-build.json` with each
  zip's address, size and SHA-256. With `ANOMALY_SIGNING_KEY` set to the project's private key it also signs them as official
  builds; the launcher asks a player before it runs an unsigned one. Copy the file beside the server's config.
- `HUB_KEY config/hub-key.pem` and `PUBLIC_ADDRESS play.example.org:7777`: players join through the launcher's login only, and a
  player's name is the account's. The key is the hub's public one (`curl -o config/hub-key.pem https://<hub>/auth/game-token-key`).
  `PUBLIC_ADDRESS` is the address the server is listed under and any other that players type, in lower case with the port, several
  with commas: a login is good for one address, and the server refuses one made for another. Without `HUB_KEY` a player is the
  name typed, as before (development servers, bots, tests). When it is first turned on, a name someone played under before
  belongs to whoever holds that account name on the hub. The game's own traffic is not encrypted: the login stops a stranger from
  playing as someone else, not someone who can read the network between a player and the server.

Leave `DEV_COMMANDS` and `CLIENT_CONSOLE` off: either gives every player the dev or console commands. An admin types them in the game's dev
console with the rights of tg's matching verbs (`spawn` SPAWN; `kick`, `teleport`, `shuttle` ADMIN; `ban` BAN; the dev commands, the
gases among them (`air`, `gas`, `ignite`, the dev menu's gas tool and its overlay), and `startnow` DEBUG; `quit` and `reboot` SERVER). The dev menu (the `\` key) opens only for admins, or for everyone with `DEV_COMMANDS` on, and an admin stays one in any job. The free camera (F1) and
F2's ceiling are not locked for players yet; ask the testers not to use them.

The AI upload's custom law board takes tg's default laws (Asimov's); a `config/silicon_laws.txt` of your own (tg's format: one law a line,
`#` comments) replaces them after a restart.

## Running it

```bash
journalctl -u anomaly -f               # the log: "joins open" when players can join, a status line every 10 s
systemctl restart anomaly              # stop and start (a new round)
systemctl stop anomaly                 # stop until the next start or boot
```

The service has no console. For the console (`startnow`, `shuttle call`, `status`), stop the service and run the server in `tmux`:
`systemctl stop anomaly`, then `tmux new -s anomaly 'sudo -u anomaly /opt/anomaly/run-server.sh'`; `systemctl start anomaly` again when done.

## Updating

Run `tools/deploy-server.sh root@IP` again (`--no-build` uploads the zip built before). Every file is replaced but `config/` (your settings and
admins) and `data/` (the database, the character saves, the bug reports); a config file the new version brings that `config/` does not have yet
(tg's `dynamic.toml`) is added. A key a new version adds to the sample `config/server.txt` takes its default until you add it. Give the testers the client built from the same commit.

## Backups

`/opt/anomaly/data/` is all the server keeps: copy it off now and then (`scp -r root@IP:/opt/anomaly/data backup/`).
