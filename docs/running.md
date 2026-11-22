# Running Anomaly Station

How to start a server and a client, the controls, and the console and dev commands. How to build is in
[building.md](building.md).

## Server and client from source

The real server on MetaStation, with one player on this machine:

```bash
dotnet run -c Release --project src/Server -- --realtime --map metastation
```

```bash
godot-mono --path godot -- --connect=127.0.0.1:7777 --name=Owner
```

`dotnet run` rebuilds the server when the code has changed. Starting `src/Server/bin/Release/net10.0/Anomaly.Server.dll`
directly skips the rebuild and can run an old build, where newer commands are "unknown". The server listens on
`127.0.0.1:7777`. It prints "joins open" when players can join, and its terminal is the server console (see below). Run
the second command again with another `--name` to open a second player.

Other ways to run it:

| Command | What it is |
|---|---|
| `dotnet run -c Release --project src/Server -- --realtime` | the small host-built test room instead of Meta |
| `dotnet run -c Release --project src/Server -- --realtime --map metastation --no-lobby` | no lobby: the round starts at once and every join comes in as an assistant (`config/server.txt` turns the lobby on; `--no-lobby` overrides it for this run) |
| `dotnet run -c Release --project src/Server -- --realtime --map showroom --no-interest --no-lobby` | the showroom: every MetaStation object type in rows, for checking the art (see [showroom.md](showroom.md)) |
| `dotnet run -c Release --project src/Server -- --realtime --map armory --no-interest --no-lobby --client-console` | the armory: every working weapon by kind with its ammo, armor, mechs and a shooting range (a stripe every 10 m) |
| `godot-mono --path godot -- --fake-server --fake-map --fake-bots=10` | the client alone: its own scripted world on the converted MetaStation (no server) |
| `dotnet run -c Release --project src/Server -- --realtime --map metastation --client-console` | players may also type the server console's commands (spawn of any type, teleport, list, kick, status, quit) in their dev console |
| `dotnet run -c Release --project src/Server -- --realtime --map metastation --bind 0.0.0.0` | LAN play. Players' dev commands are then **off** unless `--dev-commands` is given (the server console always has them) |
| `godot-mono --path godot -- --connect=127.0.0.1:7777 --sim-latency 100 --sim-loss 2` | a bad network, for the prediction |
| `dotnet tools/Anomaly.Tools.Bots/bin/Release/net10.0/Anomaly.Tools.Bots.dll --bots 10 --minutes 5 --behaviour mixed` | bots that join and walk around |

Every server option (port, seed, record and replay, metrics) is in `config/server.txt`.

## Running a release build

Unzip it anywhere and keep its folders together.

- The server: `run-server.cmd` (Windows) or `./run-server.sh` (macOS, Linux) in its folder. It listens on the LAN (UDP
  port 7777, which the firewall must let in); arguments go to it: `run-server.cmd --map metastation`,
  `./run-server.sh --map metastation --dev-commands`. A server on a VPS: [hosting.md](hosting.md).
- The Windows client: `AnomalyStation.exe`, then the server's address
  on the connect screen, or `AnomalyStation.exe -- --connect=SERVER_IP:7777 --name=YourName`.
- The macOS client: `Anomaly Station.app`. A copy that came over the internet or a chat is quarantined (macOS calls it
  damaged); clear that once with `xattr -dr com.apple.quarantine "Anomaly Station.app"`. From Terminal:
  `"Anomaly Station.app/Contents/MacOS/Anomaly Station" -- --connect=SERVER_IP:7777`.

## Controls

The defaults; F10 rebinds them.

| Input | Action |
|---|---|
| WASD, mouse | move, look |
| C / U / F | walk or run / lie down and get up / combat mode |
| Left click / right tap / right hold | tg's left click / tg's right click / the radial menu |
| Middle click, X | swap hands |
| Q / E / Z | drop / quick equip / use the held item (tg's activate in hand) |
| R, or hold Space | throw mode |
| Tab | cursor mode: the mouse is free for the HUD, the inventory slots and open storage |
| Shift+left click | examine |
| Ctrl+left click / H or Delete | pull what is under the crosshair (or the pointer in cursor mode) / stop pulling (also the HUD's pull button) |
| ` (backtick) | the dev console (the commands below) |
| F1 / F2 / F3 / F4 | camera (first person, free fly, orbit) / ceiling / names / debug HUD |
| F9 / F10 | the health analyzer panel / options |

## Commands

There are two kinds of commands.
- **Console commands** are typed into the server's terminal. They are always allowed.
- **Dev commands** are typed into the server console, or into a player's dev console (backtick). They also work from a
  client when the server binds a loopback address, which is the default, or when `--dev-commands` is on. The reply goes
  only to the player who typed it.
- **Console commands from a client:** start the server with `--client-console` (or `CLIENT_CONSOLE` in
  `config/server.txt`), and players can type the console commands in their dev console too. The answer comes back to
  them; `spawn TYPE` without coordinates puts the thing at their feet; `teleport me X Y` moves them. It is off by
  default, whatever the bind, because every player then gets admin power (kick, quit).

In the client's dev console, every line you type is echoed with a label saying who runs it: `[client]` (the client
itself, never sent), `[dev]` (a dev command, run by the server) or `[server]` (a server console command, which needs
`--client-console`). The client has two commands of its own: `help` lists every command with its syntax and a short
description (`help VERB` shows one in detail, with the values its words take), and `clear` empties the log. While you
type, the matching commands show under the log and the rest of the word appears greyed after the cursor: Tab (or Right at
the end of the line) takes it, so `he` then Tab gives `help`, and `give me /obj/item/cr` then Tab gives the crowbar.
`dev-help` asks the server for its own text on the dev commands. On the server console, `help` and `dev-help` print the
syntax. Every command calls tg's own procs, the way tg's admin verbs do, and logs a `log_admin` line.

### Server console

| Command | What it does |
|---|---|
| `spawn TYPE [X Y [Z]]` | tg's admin spawn of any `/obj` or `/mob` type, at X Y Z or at the first spawn point |
| `teleport NAME\|ID X Y [Z]` (`tp`) | moves a player (by key) or any movable (by world id) to a turf |
| `list` (`who`) | the sessions, their mobs and world ids |
| `kick NAME\|ID [REASON]` | disconnects a player |
| `status`, `hash` | tick, sessions, runtimes and metrics; the state hash |
| `quit` | a clean shutdown |

### Dev commands

`WHO` is a player's key, a world id (`#123`) or `me`, the typing player. Commands that take `WHO | X Y [Z]` act on the
turf WHO faces, or on the turf at those map coordinates.

| Command | What it does |
|---|---|
| `give WHO TYPE` | a new `/obj/item/...` of tg's type, in WHO's hands (on the floor when the hands are full) |
| `damage WHO brute\|burn\|tox\|oxy N [ZONE]` | damage (zones: `head`, `chest`, `l_arm`, `r_arm`, `l_leg`, `r_leg`) |
| `heal WHO [type]`, `fullheal WHO` | heal one damage type or all four; tg's full heal |
| `kill WHO`, `revive WHO` | death; revival |
| `knockdown WHO S`, `stun WHO S` | knockdown or stun for S seconds |
| `bleed WHO ZONE STACKS`, `blood WHO ML` | bleeding on a body part; the blood volume |
| `analyze WHO` | tg's health scan, to you |
| `spawn /mob/living/carbon/human [X Y [Z]]` | a dressed human without a player (a dummy to test on) |
| `godmode WHO`, `fly WHO`, `noclip WHO`, `god WHO` | no damage; moving without gravity; passing walls; all three at once |
| `pilot WHO` | climb into the mech on or next to the target's tile at once, or out of the one it is in |
| `speedscale F` | everyone's movement speed times F (up to 4) |
| `gas air\|vacuum\|MIX [X Y [Z]]` | replaces the air of one turf, or of the whole map, with a gas string like `o2=22;n2=82;TEMP=293.15` |
| `breach WHO \| X Y [Z]` | that turf becomes space; the windows and doors on it are removed |
| `patch WHO \| X Y [Z]` | a space turf becomes plating |
| `valve WHO \| X Y [Z]` | opens or closes the valve of the canister there |
| `areapower X Y [Z] equip\|light\|environ\|all on\|off` | a power channel of the room at those coordinates, as its power controller sets it: with `light off` the fixtures of the room fall back on their emergency cells (a dim red light, dark after about ten minutes) and `light on` brings them back |
| `emp X Y [Z] HEAVY LIGHT` | an electromagnetic pulse at those coordinates, as the admin EM Pulse does: machines, cells, suits, jetpacks and lockers within HEAVY tiles are hit hard and out to LIGHT tiles lightly (power cells drain, doors and alarms trip, a worn suit or jetpack is disabled) |
| `irradiate X Y [Z] INTENSITY [THRESHOLD]` | a radiation pulse at those coordinates, as tg's `radiation_pulse` sends it: it reaches INTENSITY tiles and stops where the walls, doors and windows on its way bring its insulation down to THRESHOLD (0.7 by default: one wall); people and items it reaches may be irradiated (a green glow, toxin damage and burns over time), except a person in a full radiation suit and hood. With four numbers a last one under 1 is the THRESHOLD |
| `recharge X Y [Z]` / `recharge all` | fills the charge of the power storage unit (SMES) at those coordinates, or of every one: the station starts on the Engineering unit's charge, and this keeps a long playtest from running it dark |
| `zap X Y [Z] POWER` | a lightning bolt of POWER joules from those coordinates, as tg's tesla zap: it strikes a tesla coil first (which turns its share into power for the network), then a grounding rod (which grounds it), a mob (shocked and stunned), a machine or a structure, and jumps on from there; a bolt below 400000 J does nothing |
| `smcrystal X Y [Z] status\|power\|damage [N]` | the supermatter crystal at those coordinates: `power` sets its internal energy (MeV, up to 100000), `damage` its damage (0 to 100; at 100 its countdown starts), `status` reports its energy, integrity, status, strategy and absorbed gas |
| `smdelam X Y [Z] [cascade\|singularity\|tesla\|explosive]` | the supermatter crystal at those coordinates delaminates now, as tg's admins make it: with that strategy forced first, or its own |
| `airmode WHO MODE` | the mode of the air alarm in WHO's area: `filtering`, `contaminated`, `draught`, `refill`, `cycle`, `siphon`, `panic`, `off` |

### Recipes

Players can pick these up from the dev console. On the server console, replace `me` with a player's key.

**Access and tools**
- `give me /obj/item/card/id/advanced/debug`: an all-access card. Held in a hand, it opens every airlock, security and
  command included. Every player already wears tg's assistant ID, which opens maintenance.
- Doors: a left click on an airlock or windoor you have access to opens it, and another click closes it, as in tg.
  Walking into one also opens it. Without access the airlock stays shut.
- `give me /obj/item/storage/toolbox/mechanical`: a toolbox full of tools. Open it with Tab and a click on it in a hand.
- `give me /obj/item/storage/toolbox/emergency`: tg's emergency toolbox.
- Any `/obj/item` path from tg works with `give`. Items whose own behaviour is not ported yet still exist: you can hold
  them, throw them and hit with them.

**Getting around**
- `god me`: fly through walls without taking damage, to look around the station. Run it again to turn it off.
- `fly me`: move in zero gravity without the noclip.
- `teleport Owner X Y`: a teleport, on the server console (`list` shows the keys, world ids and positions). With
  `--client-console`, `teleport me X Y` from the client.

**Health**
- `spawn /mob/living/carbon/human`: a dummy to punch, shove, throw things at or `analyze #ID`.
- `damage me brute 40 chest`, `damage me burn 30 l_arm`, `bleed me head 5`, then `analyze me` or F9: test the health HUD
  and the scanner.
- `fullheal me`, or `revive me` after `kill me`: back to normal.

**Atmosphere**
- `breach me` in front of a window: the room vents into space. The space wind pulls loose objects and people out. The
  firelocks around it close, because the air next to them gets cold, and the area's air alarm goes red. `patch me` on the
  same spot closes the hole.
- `gas vacuum X Y`, or `gas o2=100;TEMP=500 X Y`: put a vacuum or hot oxygen on one turf, then watch it spread.
- `spawn /obj/machinery/portable_atmospherics/canister/nitrous_oxide` (on the server console with X Y, or from a client
  with `--client-console`, at your feet; step off it), then face the canister and type `valve me`: the canister releases
  its gas into the room.
- `airmode me siphon` pulls the room's air out (the vents turn off and the scrubbers siphon). `airmode me refill` and
  `airmode me filtering` bring it back.
- Pull a fire alarm with a left click: the area's firelocks close and the alarm sounds. A right click (a quick tap)
  resets it: the alarm clears and the firelocks crack open. As in tg, a firelock that still has hot or cold air next to
  it closes again after about 3 seconds.
