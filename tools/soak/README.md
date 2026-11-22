# The soak test (`tools/soak`)

The acceptance run (M1: D100, ROADMAP M1 "done when", scope 2i; M2 adds the capsule locomotion, rest and shove): the server at 20 TPS in
real time for 15 minutes (D102: long runs at most 15 minutes) with 50 bots (by default `mixed`: wander, rest and shove by turns)
and two windowed Godot clients, then a results file with the numbers that decide whether the server holds. The AI runs it
unattended on the owner's Mac; the owner may join one client by hand.

```
tools/soak/run.sh                         # 50 mixed bots, 15 minutes, 2 Godot windows
tools/soak/run.sh --predict               # the same, every bot predicts its own capsule (the report counts corrections)
tools/soak/run.sh --stress                # the 150-bot run: 150 bots, 10 minutes, no Godot windows
tools/soak/run.sh --bots 20 --minutes 2 --godot 0     # a short try (no windows)
tools/soak/run.sh --stress --godot 2      # the 150-bot run with windows after all
```

`run.sh --help` lists every option (`--bots`, `--minutes`, `--godot`, `--stress`, `--behaviour mixed|wander|rest|shove|idle|churn`,
`--predict`, `--port`, `--seed`, `--out`, `--no-build`, `--strict`, `--server-args`, `--bots-args`, `--dev`). M10 slice J's round on
MetaStation, with tg's lobby, two round ends and their restarts in one hour:

```
tools/soak/run.sh --bots 100 --minutes 61 --godot 0 --server-args "--map metastation" \
    --dev 20:startnow --dev "120:shuttle call" --dev "1950:shuttle call"
```

The script is plain bash 3.2 (macOS's `/bin/bash`)
and needs `dotnet`, `python3`, and for the windows the Godot 4.7.2 mono binary (`godot-mono` on `PATH`, else
`/Applications/Godot_mono.app/Contents/MacOS/Godot`; `GODOT` overrides it).

## What it does

1. **Preflight.** Tools present, the UDP port free (default 7777).
2. **Build.** `dotnet build -c Release` of `src/Server` and `tools/Anomaly.Tools.Bots`; with Godot windows also the icons tool
   (M2, `tools/Anomaly.Tools.Icons`: `dotnet run ... -- --tg ../tgstation --out godot/assets/tg`, which writes the git-ignored
   atlases the client draws; skipped with a line when the project is not in the tree), the headless import and
   `dotnet build godot/Anomaly.Client.csproj` (as `godot/tools/smoke.sh` does after `tools/convert-tg.sh`). `--no-build` skips all of it.
3. **The server**: `dotnet src/Server/bin/Release/net10.0/Anomaly.Server.dll --realtime --no-console --port=P --seed=S --metrics=OUT/soak.csv`,
   standard input from `/dev/null` (so the console reader is off), output to `OUT/server.log`. `caffeinate -dims -w <server pid>` runs beside it: the display,
   idle, disk and system sleep assertions are held for exactly as long as the server lives (D100: the Mac must not sleep). The
   script waits for a line in the server log that says it accepts joins (`SERVER_READY_PATTERN`, at the top of the script: the server's `joins open at tick T`; if
   none shows in `SERVER_READY_TIMEOUT` seconds it carries on, because the bots keep retrying while the server boots).
4. **The bots**: `Anomaly.Tools.Bots --bots N --minutes M --behaviour mixed [--predict] --reconnect --report OUT/bots.json`, 25 bots started
   per second (the server queues at most 64 connection requests), output to `OUT/bots.log` (a progress line every 10 s).
5. **The Godot clients** (when `--godot K` is above 0): K windows of 640x400 side by side, each
   `godot --path godot --resolution ... -- --connect=127.0.0.1:P --name=SoakGi --autowalk --quit-after=SECONDS`, started a few
   seconds after the bots with `SECONDS` set so that they end when the bots do. The four argument names are variables at the
   top of `run.sh` (`GODOT_ARG_CONNECT`, `GODOT_ARG_NAME`, `GODOT_ARG_AUTOWALK`, `GODOT_ARG_QUIT_AFTER`): they are P6's
   automation arguments (`docs/ai/specs/apps.md` section 2); change them there if the client's change. Output goes to
   `OUT/godot-i.log`.
6. **Wait** for the bots, printing a progress line (bots, server) every minute, and watch the server: if it dies, the run is
   cut short and marked so.
7. **Stop** the Godot clients (they quit on their own; stragglers get a grace period, then are killed), then the server with
   SIGTERM (it writes its shutdown summary and exits 0; after 60 s it is killed).
8. **Results**: `summarize.py` writes `OUT/results.txt` (read this) and `OUT/results.json`.

The default output folder is `tools/soak/results/<UTC stamp>-b<bots>-m<minutes>/` (ignored by git). Next to the results it holds
`soak.csv`, `server.log`, `bots.json`, `bots.log`, `godot-N.log` and `run-info.txt` (the command line, git revision, .NET
version, machine).

### Stopping and cleaning up

Everything the script starts is stopped when it ends, however it ends. Every job runs in its own process group (`set -m`), and
an `EXIT` trap stops the Godot clients, the bots, the server and `caffeinate` (TERM, a grace period, then KILL). **Ctrl+C or
SIGTERM** once asks the bots and clients to leave; the run is then finished normally and its results are written, marked
*aborted* (a partial run). A second signal stops everything at once. If the server dies, the bots are told to leave and the
results say so. A bot process that ignores TERM is killed after 30 s.

## The results file

`results.txt` has these sections:

- **Header**: when, how many bots, which behaviour, minutes, Godot clients, seed, port, whether the run was aborted, the
  server's exit status.
- **Server tick metrics**: from the server's `--metrics` CSV (one row per tick; P3 owns its columns: `tick, poll_us, step_us,
  replication_us, send_us, total_us, lateness_us, overrun, sessions, bytes_out, bytes_in, alloc_bytes, gc0, gc1, gc2,
  heap_bytes, mobs_fired`; the last, M2's, is 1 in the ticks SSmobs fired in). `summarize.py` does not hard-code the names: every numeric column gets n, min, mean, p50, p95, p99, p99.9, max
  and sum (a column ending in `_us` is shown in milliseconds), and the columns whose names match the patterns in `HEADLINE` (tick
  total, world step, replication, network, lateness, overruns, GC and allocation, bytes and sessions, runtimes) are repeated as a
  headline. The sum is the number for the count columns (`overrun`, `gc0`...). The bytes per player per second (down and up) are
  derived from the per-tick bytes and session counts. With `mobs_fired` it gives the world step of the ticks SSmobs fired in
  (every 40 ticks it runs the Life of every living mob in one tick, DV3: the peak tick of the run) next to the other ticks', and
  flags one that takes more than the 50 ms slot. It also says whether the CSV has one row per tick or fewer (then the
  percentiles are over rows, not ticks). A column that no pattern matches is still in the full table.
- **Server log**: the runtime count of its last status line (`runtimes=N`, the world's runtime errors), counts of exceptions, and
  its last 40 lines (the shutdown summary).
- **Bots**: joined ever and at the end, connected seconds, input messages and frames sent, actions sent by kind (rest, walk/run,
  combat mode, click), shoves and rests, the behaviours the bots ran, the prediction counters of the predicting bots
  (reconciliations, corrections of 1 cm or more, snaps over a tile, bit mismatches, the largest error), own and other moves, deltas applied, protocol payload
  bytes per bot per second down and up (the client-side "bytes per player"; the server's own are in its CSV and summary), RTT
  mean and percentiles (the bots poll at 20 Hz and so does the server, so the RTT LiteNetLib reports has a floor of about one
  loop period, 50 ms: a figure near that is the polling, not the network), **desyncs** (a replica missed a delta), decode errors, exceptions, and every disconnect by kind and
  reason (`RemoteClose:Kicked`, `LocalClose:None`, ...).
- **Godot clients**: lines and error lines of each log.
- **Flags**: what to look at first. Raised for: an aborted run, a non-zero server exit, an overruns or runtimes column that ends
  above 0, a tick-total p99 over the 50 ms slot, runtime errors or exceptions in the server log, any bot desync, bot exception
  or unexpected disconnect, fewer bots joined at the end than started, a bot loop that was late in more than 1% of its
  iterations (the load itself would be off), no bot ever moving, Godot error lines. `--strict` makes the script exit 3 when any
  flag is raised; without it flags only show in the file.

The script's own exit status: 0 after a run (with or without flags), 1 when the run could not be done or finished (a build
failed, the server died, the bots exited with an error such as none ever joining, 3), 130 after Ctrl+C.

## Notes for the people running it

- **Windows (Git Bash).** A native process gets no SIGTERM there (a kill ends it at once, without its shutdown summary), so the
  script runs the server with its console reading a pipe and stops it with the console's `quit`. There is no `caffeinate` and no
  `lsof` (the port check is skipped); `python3` must be on `PATH`. The server keeps its keys, bans and round ids in the run's own
  `soak.sqlite` (`--database`), never in `data/anomaly.sqlite`, so the bots' names do not claim names in the developer's database.

- **Server flags.** The script passes `--realtime --no-console --port --seed --metrics` and `--server-args` (for example
  `--server-args "--max-players 300"`). The server's standard input is `/dev/null`. A server that does not accept one of the flags
  fails at startup and the log says so.
- **App Nap.** macOS may throttle background windows, so the Godot clients can drop frames while the terminal is in front.
  That lowers the clients' FPS only; the server and the bots are processes without windows. Note it in the report when FPS is
  part of the verdict.
- **Network prompts.** The server binds 127.0.0.1 by default; the Mac's firewall is off (scope risk 8). Nothing leaves the
  machine.
- **The bots' own load.** The bot process shares the CPU with the server. It reports how many of its 50 ms iterations ran late
  (`late_iterations`); a flagged run means the bots, not the server, were short of time.
- **Room and slots.** 150 bots in the 48x48 test room (about 2,000 free cells) jam now and then (scope risk 12); the bots re-pick a direction when the replicated position stops changing. `max_players` must stay above the bots plus the clients (the default is 200).
- **Testing the script itself.** `SOAK_SERVER_CMD` and `SOAK_BOTS_CMD` replace the server and the bots by any command line, for
  trying the script's lifecycle without the real programs (the options still apply).

## The bot tool

`dotnet tools/Anomaly.Tools.Bots/bin/Release/net10.0/Anomaly.Tools.Bots.dll --help`. N bots in one process over LiteNetLib's manual mode,
one loop at 20 Hz, no thread per bot; each is a protocol client with its own `ReplicaWorld`.

| Option | Meaning |
|---|---|
| `--host H`, `--port P` | server (default 127.0.0.1:7777) |
| `--bots N` | 1 to 2000 (default 10) |
| `--minutes M` | run length, decimals fine; 0 runs until Ctrl+C (default 1) |
| `--behaviour B` | `wander` (hold a compass heading in continuous space for a random time, look ahead along it in the replicated solidity grid and steer away from walls, re-pick when the position stops changing), `rest` (wander, lie down every 5 to 15 s for 3 to 6 s with Rest, press walk/run and combat mode now and then), `shove` (walk to the nearest standing body it can see within 10 tiles and right-click it within 1.5 tiles, one click per 20 ticks), `mixed` (bot i is wander, rest, shove or wander for i % 4 = 0, 1, 2, 3), `idle` (connect and stay), `churn` (wander, then leave and reconnect with the token at random intervals of 5 to 20 s), and the abusive ones: `abuse-flood` (MotionInput messages far above the rate), `abuse-action-flood` (actions above the rate), `abuse-forged-seq` (a frame seq a minute ahead), `abuse-malformed` (undecodable bytes; alias `abuse-garbage`), `abuse-bad-action` (garbage Actions and legal clicks on an entity that does not exist), `abuse-dev` (dev commands at and over the byte cap), `abuse-oversized` (messages over the 256 byte cap), `abuse-early-command` (MotionInput before the Welcome), `abuse-wrong-version` (a Hello with another protocol version) |
| `--predict` | every bot predicts its own capsule with the motion core (`MotionPredictor` and `InputClock`, the headless prediction client): the report counts reconciliations, corrections, snaps and bit mismatches |
| `--seed S` | the bots' random seed; bot i derives its own stream (default 1) |
| `--name-prefix X` | names `X001`, `X002`, ... (default `Bot`) |
| `--report FILE` | the JSON report: aggregate and per bot |
| `--reconnect` | connect again (with the token) after an unexpected disconnect |
| `--join-timeout S` | seconds a bot keeps trying while the server is full or booting (default 60) |
| `--ramp N` | bots started per second, 0 for all at once (default 25) |
| `--status-every S`, `--no-per-bot` | progress line period; leave the per-bot rows out of the report |
| `--dev T:COMMAND` | M10 slice J: the first joined bot sends the dev command T seconds into the run (repeatable): `--dev 20:startnow` starts the round, `--dev 600:"shuttle call"` calls the emergency shuttle (the server's dev commands are on when it binds 127.0.0.1) |

Exit status 0 after a run, 2 for a bad command line, 3 when a non-abusive run never got a single bot in.

**The lobby and the restart (M10 slice J).** With tg's lobby on (`LOBBY`, the shipped config) a bot joins into it: in the pregame it
readies up and waits for the ticker's round start (tg's 120 s countdown, or `--dev 20:startnow`); after the start it presses Join and
picks the station engineer in tg's job menu. It does not walk while its lobby is open. When the round ends, the server closes every
connection with `RoundRestart` and each bot (abusive ones aside) joins the next round with its token, retrying `NotReady` while the new
world boots; the summary counts `RoundRestart` and `NotReady` as expected disconnects.

The report counts, per bot and in all: connected time, joins, inputs sent, own and other moves seen in the deltas, protocol payload
bytes and messages in and out, desyncs (the replica refused a delta, a join message did not decode, or the tick did not advance
by one; the bot then leaves and, with `--reconnect`, joins again), decode errors, disconnects by `Kind:Reason`, RTT (sampled
once a second; mean, p50, p95, p99, max), exceptions caught by the loop, and the loop's own lateness.
