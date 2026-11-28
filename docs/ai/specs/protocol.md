# Wire protocol and transports (M1, version 2 in M2, version 3 in M3)

Status: v3 (M3 package P5), 2026-10-08; v2 was M2 package P5, v1 M1 package P2. Protocol version **3** (section 13 is the delta from version 2; sections 1 to 12 describe version 2, whose layout version 3 extends at the ends, never in the middle). Code: `src/Protocol` (messages, limits, codec,
client replica) and `src/Net` (transports). Design basis: the M1 scope notes 2d, 2e, 2f and 2j and the M2 scope notes 2d, 2h and 2i;
decisions D31 (the world is stepped on the host thread only), D37 (LiteNetLib), D40 (the server trusts nothing from the network and
sends nothing hidden), D60 (a protocol version check on connect), D103 (own-capsule prediction), D107 (the HUD is tg's screen
objects). The replicator that fills the messages is `docs/ai/specs/replication.md` (appearances, positions, private blocks); the
session logic that answers them is `docs/ai/specs/apps.md` section 1; the controller whose state the private block carries is
`docs/ai/specs/locomotion.md`.

The protocol is hand-written and has no serializer library: a message is a byte string, a codec turns messages into bytes and
back, and a transport moves byte strings. `Anomaly.Protocol` references only `Anomaly.Core.Motion` (the wire carries `MotionInput`,
`MotionState` and `MotionParams` as they are: one definition for the server's controller, the client's predictor and the bots);
the server, the bots and the Godot client share it; `Anomaly.Net` references it and LiteNetLib 2.1.4.

**What version 2 adds to version 1** (details in the sections named):

- entity fields `Pos`, `Yaw`, `Posture` and `Appearance`, a **varuint** change mask, and `LastMoveTick` relative to the message's
  tick (sections 4.2, 5);
- the **appearance, resource and string tables**: public ones every connection shares (new rows travel in the Delta body, whole
  tables in the Snapshot) and private ones per session for what only its owner may see (sections 3.5, 4.1, 5.5);
- the per-connection **private block** after the body of every Delta and in every Snapshot: the own capsule's motion state and
  parameters, the server's input buffer depth, the `client.screen` entries (section 4.3);
- `MapGrid` with each cell's turf appearance and solidity bit, and the **cell changes** of a Delta (sections 3.4, 4.1);
- `MotionInput` frames on the Input channel, each message repeating the last four, and `Action` on the Control channel
  (sections 3.2, 3.9, 3.10).

## 1. Overview

| Direction | Message | Channel | Delivery |
|---|---|---|---|
| client to server | `Hello` | the transport's connection request data | once per connection attempt |
| server to client | `Disconnect` (`DisconnectInfo`) | the transport's reject data or disconnect data | once |
| server to client | `Welcome`, `MapGrid`, `Snapshot` | State (1) | reliable ordered, the join sequence in this order |
| server to client | `Delta` | State (1) | reliable ordered, one per tick after the Snapshot |
| server to client | `ServerText` | Control (0) | reliable ordered |
| client to server | `InputFrame` | Control (0) | reliable ordered (M1's held directions; the M1 host still answers it) |
| client to server | `MotionInput` | Input (2) | unreliable, one per tick, each repeating the last 4 frames |
| client to server | `Action` | Control (0) | reliable ordered, tagged with its frame's seq |
| server to client | `Airflow` (version 6) | State (1) | reliable ordered, after the tick's Delta (section 16) |
| server to client | `Sounds` (version 13) | State (1) | reliable ordered, after the tick's Delta and Airflow, only when the session heard something (section 22) |

One transport message carries exactly one protocol message: `[u8 MessageId][payload]`. An id outside 1 to 14 (version 13), or one that does
not belong where it arrived, is a protocol violation (the server counts a strike, a client leaves).

The join sequence shares the State channel with the Deltas on purpose: LiteNetLib orders messages within a channel only, so
a client sees `Welcome`, `MapGrid`, `Snapshot`, then the Deltas of tick T + 1, T + 2, ... in this order. A session that was
sent the Snapshot of tick T receives exactly the Deltas of T + 1 onwards.

## 2. Encoding rules

Shared by `WireWriter` (encoder) and `WireReader` (decoder). The writer never allocates and throws `ProtocolException`
(`BufferTooSmall`, or a value over its cap, which is a sender bug); the reader throws nothing but `ProtocolException`, whatever
the input.

| Type | Encoding |
|---|---|
| fixed integers (`u8`, `u16`, `u32`, `u64`) | little-endian |
| `varuint` | unsigned LEB128, seven bits per byte, low group first, at most 10 bytes. **Canonical**: a redundant final zero group (`80 00`) is `BadVarint`, so a value has exactly one encoding; a 10th byte above 1 overflows 64 bits and is `BadVarint` |
| `varint` | zigzag (`(v << 1) ^ (v >> 63)`) then `varuint`: 0 is 0, -1 is 1, 1 is 2, -2 is 3 |
| `f32` | IEEE 754 single, little-endian. NaN and the infinities are refused (`BadValue`) on both sides |
| `string` | `varuint` of the UTF-8 byte length plus one (**0 is null, 1 is the empty string**), then the bytes. The bytes must be valid UTF-8 (checked before anything is allocated) and at most the field's byte cap (`BadString`) |
| `count` | `varuint`, at most the list's cap, and at most as many items as the bytes left can hold at the item's smallest size (`CountTooLarge`), so a forged count never makes a decoder allocate |
| `token` | 16 bytes: `Low` as `u64`, then `High` as `u64` |
| `id` (entity) | `varint` difference from the previous id of the same list (0 before the first), see section 5.4 |
| `pos` | two `u16`: x then y in 1/256 tile; the tile `floor((q + 128) / 256)` must lie in 1 to 255 (`BadValue`) |
| `tableref` | `varuint` `(index << 1) \| private`: an index into a table (from 1; 0 is none) and whether it is the session's private table; `(0, private)` is `BadValue` |

Decoding refuses trailing bytes (`TrailingBytes`), a message over its cap (`TooLong`), an empty message (`Truncated`), an
unknown first byte (`UnknownMessage`) and a known id that is not the decoder's (`WrongMessage`). Every decoded message
satisfies every cap and range rule below, and **a message that decodes encodes back to the same bytes** (the fuzz tests check
this for every accepted input).

## 3. Messages

The Delta is the one message with a shared part; its layout is section 4. The numbers in `[ ]` are byte counts; `str(N)` is a
string with a cap of N bytes.

### 3.1 Hello (client to server, in the connection request)

```
[u8 id = 1] [4 bytes "ANOM"] [u16 protocol] [str(168) name] [u8 flags] ([token 16] if flags bit 0) ([str(768) login token] if flags bit 1)
```

- `protocol` is `ProtocolVersion.Current` (1). The decoder reads the magic and the version first; **a Hello of another version
  decodes to `Hello(version, null, none)` without reading the rest**, because a later version may lay the rest out
  differently. The host answers `WrongVersion` to it.
- `name`: at most 42 characters (tg's `MAX_NAME_LEN`), 168 bytes at four bytes a character. The codec checks UTF-8 and the
  byte cap; the name rules are `NameRules.IsValid` (not empty, at most 42 Unicode scalar values, no control, format,
  line/paragraph separator, private-use or unassigned characters, not only spaces). The host answers `InvalidName`.
- `flags`: bit 0 says a reconnect token follows; the other bits are reserved and must be 0. A token that is all zeros with
  the flag set is refused (the canonical "no token" is flag 0).
- Version 85 (D335): bit 1 says a login token follows the reconnect token: the hub's game token the launcher started the client
  with, an ES256 JWT of at most 768 bytes (`Limits.MaxLoginTokenBytes`) that names the account and the one server it is for. A
  server that holds the hub's key (`HUB_KEY`) takes the player's name from it in place of `name`, and refuses a Hello that has
  neither a valid one nor the reconnect token of a session logged in since the server started (`LoginRequired`). A server
  without the key does not read it. A null or empty string with the flag set is refused. A Hello has its own size cap,
  `Codec.MaxHelloBytes` (964), as the login token is longer than the 256 bytes of the other inbound messages; one UDP packet
  still holds it.
- The longest Hello is 194 bytes (`Codec.MaxHelloBytes`), within the 256 of an inbound message.

```
01 53 53 33 44 01 00 03 41 6C 00           Hello(1, "Al", no token)
01 53 53 33 44 01 00 03 41 6C 01 01 .. 10   the same with the token 01 02 .. 10
```

### 3.2 InputFrame (client to server, Control)

```
[u8 id = 7] [varuint seq] [u8 heldDirs]
```

`seq` is 1 for the first frame of a connection and grows by one per frame (at most 32 bits; the host refuses a frame whose seq
does not grow and echoes the last applied one in every Delta). `heldDirs` holds the movement keys now: bit 0 north, 1 south, 2
east, 3 west (BYOND's 1, 2, 4, 8); any other bit is `BadValue`. At most 7 bytes. `07 2C 05` is seq 44 with north and east. Version 2 keeps it
for the M1 host path; the M2 host takes `MotionInput` (3.9) instead.

### 3.3 Welcome (server to client, State, first of the join sequence)

```
[u8 id = 2] [varuint session] [token 16] [varuint tick] [u8 tickRate] [varuint ownEntity]
```

`session` (at most 31 bits) is the session's ordinal; `token` the reconnect token to keep; `tick` the join tick (the Snapshot
that follows is of this tick); `tickRate` is ticks per second (20; 0 is refused); `ownEntity` the id of the client's mob, 0 for
none. At most 43 bytes.

**The client keeps the token across launches (M3 R4, scope 2j).** The server takes a known name back only with its token
(`NameInUse` without one, `BadToken` with another; a token for a name the server does not know is `BadToken` too), which is tg's
reconnect to the SSD mob. The Godot client keeps the token of each server (`host:port`) and name (as a ckey) in
`user://reconnect-tokens.txt` (`ReconnectTokens`, `godot/scripts/Shared/Play/Reconnect.cs`; `--token-store=PATH|none`; at most 64
entries, a file over 64 KB or a bad line is ignored), and the connect screen's join (`JoinAttempt`) sends it: a client that
crashed (no goodbye) or quit and is launched again takes its character back, and a second launch while the first plays replaces
it (`Replaced`). It keeps the Welcome's token once the game plays. A `BadToken` refusal of a kept token (the server restarted, or
the name was taken back elsewhere) forgets the token and connects once more without it, as a new player or to a `NameInUse`
refusal, which the connect screen explains: the name is taken back only by the client that played it; choose another name or
connect from the computer and user account that played it. `GameClient.CloseCode` is the server's `DisconnectReason`.

```
02 01 | 01 02 .. 10 | AC 02 | 14 | 07     session 1, tick 300, 20 TPS, own entity 7
```

### 3.4 MapGrid (server to client, State, after Welcome)

```
[u8 id = 3] [varuint tick] [varuint width] [varuint height] [varuint levels]
[count types] { str(256) path }*  [count runs] { [varuint cells] [varuint typeIndex] }*
[count appearanceRuns] { [varuint cells] [varuint appearance] }*  [varuint solidBytes] [solidBytes raw bytes]
```

The static map as it is when the session joins (D23: at most 255 by 255). Width and height are 1 to 255, levels 1 to 80 (`Limits.MaxMapLevels`: a map's own, at most 64, and the levels a round adds to them). `types` has
at most 65536 entries (a table local to this message); a path is never null. The runs list the cells in BYOND's turf order, x
fastest, then y, then z, each run at least one cell, each type index inside the table, the counts adding up to exactly
`width * height * levels`. `MapGrid.ExpandCells()` turns the runs into one table index per cell.

**Version 2.** `appearanceRuns` give every cell's turf appearance, a **public** appearance table index (0 for none; the Snapshot that
follows carries the table, so a client resolves them once it arrived); they are either absent (count 0: a map with no appearances) or
cover exactly every cell. `solidBytes` is 0 (no solidity information) or exactly `ceil(cells / 8)`: one bit per cell in the same
order (bit `i % 8` of byte `i / 8`), set when the cell blocks capsules, computed by the server with the predicate its controller
uses (`LocomotionSeam.IsSolid`), so the client's prediction collides with what the server's does (D103). The bits past the last cell
must be 0 (one encoding per map). Later changes of either come as the Delta's cell changes (4.1); a joiner gets the map as it is.

```
03 AC 02 | 03 02 01 | 02 | 11 "/turf/open/floor" | 12 "/turf/closed/wall" | 02 | 04 00 | 02 01 | 00 | 00
(tick 300, 3 x 2 x 1, two types, 4 cells of type 0, then 2 of type 1; no appearance runs, no solidity bits)
```

### 3.5 Snapshot (server to client, State, after MapGrid)

```
[u8 id = 4] [varuint tick] [varuint lastInputSeq] [varuint ownEntity]
[count types] { str(256) path }*
[count resources] { str(512) }*  [count strings] { str(256) }*  [count appearances] { appearance }*
[count entities] { entity record }*
[private block]
```

The full state of one tick for one connection (at join, and again whenever the server must resynchronize it). `lastInputSeq` is the
last input frame seq the world applied up to this tick (0: none yet); `ownEntity` is the connection's own mob (the `NetField.OwnMob`
flag travels as this id, never inside a record). `types` is the connection's type table, complete so far: **the table is the server's
for the whole run and append-only**, an index given to a DM path the first time an entity of that type is replicated, so every joined
connection holds a prefix of one table (a Delta can be encoded once for all of them, and bots and clients never depend on the server
build's type order). The same holds for the three **public tables** of version 2: resources (icon files and GAGS references), strings
(icon states) and appearances, each complete so far, index 1 being the first entry. Entities come **in strictly increasing id order**;
the ids are differences (section 5.4), so the first is the id itself. Their last move ticks are relative to this message's `tick`.
The private block (4.3) lists the session's private tables whole (from index 1) and every screen entry as added, and nothing as
changed or removed.

The entity record is section 5.1, an appearance entry 5.5. A Snapshot has at most 262144 entities, 65536 types, and at most 2^20
resource, string and appearance entries together.

```
04 AC 02 00 07 | 02 05 "/mob" 05 "/obj" | 00 00 00 | 02 | 0E 00 03 04 01 02 03 "Al" AB AA 2A 41 0A 00 00 | 04 01 05 05 01 00 00 00 00 00 00 00 00 00 | 00 00
tick 300, seq 0, own 7, two types, empty public tables; entity 7 ("/mob" at 3,4,1 facing south, glide 10.67, moved 10 ticks ago,
standing, no appearance); entity 9 ("/obj" at 5,5,1, no direction, null name); an empty private block
```

### 3.6 Delta (server to client, State, one per tick)

Section 4.

### 3.7 ServerText (server to client, Control)

```
[u8 id = 6] [str(64) type] [str(16384) html]
```

One chat message tg sent to this player (`to_chat`: the login messages, tg's kick reasons). `type` is tg's `MESSAGE_TYPE_*` text
and may be null; `html` is tg's message text with its HTML spans. `06 05 "chat" 03 "hi"`.

### 3.8 Disconnect (server to client, in the reject or disconnect data)

```
[u8 id = 8] [u8 reason] [str(1024) text]
```

`reason` is a `DisconnectReason` (0 to 15: none, server full, wrong version, bad Hello, invalid name, name in use, bad token,
not ready, kicked, flooding, protocol violation, server shutdown, replaced, refused, client deleted, round restart (version 45,
section 53)); another value is
`BadValue`. `text` is human-readable detail, may be null. It travels in LiteNetLib's reject data (a refused connection request,
before a peer exists) or disconnect data (a kick). **Keep it short**: LiteNetLib sends the reason in one packet, so the
transports send a reason of more than `Channels.MaxDisconnectBytes` (1000) bytes as no reason (the connection still closes).
`08 02 "old"` is a wrong-version reject.

### 3.9 MotionInput (client to server, Input channel)

```
[u8 id = 9] [u8 count 1..4] [varuint seq of the first frame] { [i8 moveX] [i8 moveY] [u16 yaw] [u8 flags = 0] }*
```

Scope 2d: the client sends one message per tick on the unreliable Input channel, each repeating its newest frames (up to 4, **oldest
first**, the last one the newest), so a lost message costs nothing while the next arrives. The seqs are consecutive (frame `i` has
seq `first + i`, `first` at least 1, none past 32 bits); the server's jitter buffer takes each frame once, by seq (docs/ai/specs/apps.md
section 1). `moveX` and `moveY` are the world-space wish vector, -127 to 127 (-128 is `BadValue`; the controller clamps the length to
127); `yaw` is a compass bearing (65536 per turn, 0 north, clockwise); `flags` are reserved and must be 0. A message is 5 + 6 per
frame bytes at most 31 (cap 64, `Limits.MaxMotionInputMessageBytes`), well under LiteNetLib's 1023 unreliable bytes.

The host's input policy (a host rule, not core logic; the lead's M2 decision): the host takes one frame per tick from the jitter
buffer; a taken frame whose wish and yaw equal the last one of the session is acknowledged (its seq goes into the private block)
but is not recorded as a `Motion` command, since the locomotion seam keeps a mob's last frame; with no frame queued the last one
repeats, and after half a second of that (10 ticks, `ActionLimits.MaxRepeatedFrameTicks`) the host takes an idle frame (no wish)
in its place, so a client that went silent stops instead of walking on until its connection times out (docs/ai/specs/apps.md 1.8).

```
09 02 AC 02 | 81 7F 34 12 00 | 00 05 FF FF 00      two frames: seq 300 (move -127, 127, yaw 0x1234) and 301 (0, 5, yaw 0xFFFF)
```

### 3.10 Action (client to server, Control channel)

```
[u8 id = 10] [varuint frameSeq] [u8 kind]  then by kind:
    Click (4):        [u8 target kind] [target] [u8 button] [u8 modifiers] [u8 icon-x] [u8 icon-y]
    Hover (7):        [u8 target kind] [target] [u8 aim zone 1..9, an entity's only, absent for none]   (version 100, section 97)
    Drop (13):        [u8 target kind] [target] [u8 target kind] [over] [u8 button] [u8 modifiers] [u8 icon-x] [u8 icon-y] [u8 drag time]   (version 17, section 26)
    Keybinding (6):   [u8 code] [u8 key up]
    DevCommand (5):   [str(200) text]
    Rest (1), ToggleMoveIntent (2), ToggleCombatMode (3): nothing
```

One discrete action, reliable ordered, tagged with the seq of the motion frame it belongs to: the server applies it in the command
phase of the tick that applies that frame (so a click sees the capsule where the player saw it), after that frame, or at once when
the frame was already applied. `kind` 1 to 7 (anything else is `BadValue`). The target, the icon pixel, the keybinding and the hover
are version 4's (section 14.1; version 2 and 3 clicked `[varuint target] [u8 button] [u8 modifiers]`). A click needs a `button` of
1 left, 2 right or 3 middle and `modifiers` within `shift = 1, ctrl = 2, alt = 4` (bit 3 and bit 7 zero); bits 4-6 of the
modifiers byte are the body zone a click on an entity aims at (`AimZone`: 0 none, 1 head, 2 chest, 3 left arm, 4 right arm,
5 left leg, 6 right leg; DV30; version 100 adds 7 eyes, 8 mouth, 9 groin with bit 3 as the zone's high bit, section 96), which the server selects as tg's `zone_selected` before a click on a carbon; the server checks that the target exists, is
visible to the player and is in view (section 14.1), and reach is tg's own (`Adjacent`, `ClickOn`). A dev command carries a non-null
text of at most 200 bytes, for the server's `DEV_COMMANDS` option. Every other field must be absent or zero. At most
`Limits.MaxActionMessageBytes` (248 since version 15, section 24) bytes, within the 256 of an inbound message.

```
0A 05 04 01 07 02 04 00 00   frame 5: a right click on entity 7 with alt held, no icon pixel
```

## 4. The Delta

```
header (per connection):  [u8 id = 5] [varuint tick] [varuint lastInputSeq] [varuint ownEntity]
body (the same for every connection):
    [varuint firstNewType] [count newTypes] { str(256) path }*
    [u8 parts]                                              bit 0 resources, 1 strings, 2 appearances, 3 solid changes, 4 turf appearance changes
    if bit 0:  [count] [varuint first] { str(512) resource }*
    if bit 1:  [count] [varuint first] { str(256) string }*
    if bit 2:  [count] [varuint first] { appearance }*
    [count spawned] { entity record }*
    [count changed] { change record }*
    [count despawned] { id }*
    if bit 3:  [count] { [varuint x] [varuint y] [varuint z] [u8 solid] }*
    if bit 4:  [count] { [varuint x] [varuint y] [varuint z] [varuint appearance] }*
private block (per connection):  section 4.3
```

`tick` is the tick whose changes these are: the state of tick `tick - 1` becomes the state of `tick`, so a Delta always follows
the previous Snapshot or Delta of its connection by exactly one tick. The lists are applied in this order: new types, new table
entries, spawned, changed, despawned, cell changes, then the private block; **an id is in at most one of the three entity lists**. A
Delta has at most 262144 entity records in all three lists together.

### 4.1 The body

- **Encoded once.** The server calls `Codec.EncodeDeltaBody(delta)` (or the reusable-buffer overload
  `EncodeDeltaBody(delta, ref buffer)`, which allocates nothing in a steady state) once per tick, and per connection
  `Codec.EncodeDelta(tick, lastInputSeq, ownEntity, body, ownBlock, destination)`: the header, the body bytes copied, the
  connection's encoded private block (`Codec.EncodeOwnBlock`). The body depends on `Delta.Tick` (an entity's last move tick travels
  relative to it) and on nothing else of the header, so two connections' Deltas of one tick share one body. The version-1 call
  `EncodeDelta(tick, lastInputSeq, ownEntity, body, destination)` appends an empty private block (`Codec.EmptyOwnBlockBytes`, 2 bytes;
  `Codec.MaxDeltaHeaderBytes` counts it).
- **New types.** `firstNewType` is the type-table index of `newTypes[0]`, equal to the table's length before this Delta; with an
  empty `newTypes` it carries nothing and is not checked. `firstNewType + newTypes.Count` is at most 65536.
- **New table entries** (version 2). A table update is its count, then, when the count is not 0, `first`: the index of the first new
  entry, from 1, equal to the table's length before this Delta plus one; with no new entries there is no `first` (it decodes as 0) and
  the part's bit is clear. A list is present exactly when it is not empty (one encoding per Delta). An appearance entry names only
  earlier entries and entries of the resource and string tables as they are after this Delta (5.5); the entities of this Delta may
  name the new rows. The three tables together grow by at most 2^20 entries in one Delta.
- **Spawned** are entities that appeared (created on a turf, or moved onto one from nullspace or a container), with their full
  record. **Despawned** are the ids that disappeared (deleted, or moved off the turfs).
- **Ids** in the three lists are differences from the previous id of the same list; the lists may be in any id order (an
  ascending order makes the differences small), and two equal ids next to each other are refused.
- **Cell changes** (version 2). The cells whose solidity bit or turf appearance changed this tick, for the client's collision view and
  its turf kit; the coordinates are within the map; a turf appearance is a public table index (0 for none). At most 65536 changes in
  one Delta (the replicator spreads more over the next ticks).

### 4.2 Example

Tick 300, last input seq 1, own entity 5; one new type `/a` at index 2; spawned entity 6 (type 2, at 3,4,1, name "n", moved this
tick); entity 5 changed (x = 4, y = 4, moved this tick); entities 9 and 7 despawned:

```
05 AC 02 01 05                                  header: tick 300, seq 1, own 5
02 01 03 "/a"                                   first new type 2, one new type "/a"
00                                              parts: no new table entries, no cell changes
01 0C 02 03 04 01 02 02 "n" 00 00 00 00 00 00 00   one spawned: id 6 (zigzag 12), type 2, 3,4,1, dir 2, name "n", glide 0,
                                                moved 0 ticks ago, flags 0 (not a capsule, standing), appearance 0
01 0A 4C 04 04 00                               one changed: id 5, mask 0x4C (X, Y, LastMoveTick), x = 4, y = 4, moved 0 ticks ago
02 12 03                                        two despawned: id 9 (zigzag 18 = 0x12), then 7 (difference -2 = zigzag 3)
00 00                                           the private block: no motion, no tables, no screen entries; input buffer depth 0
```

### 4.3 The private block

```
[u8 flags]                      bit 0 motion, bit 1 private tables, bit 2 screen entries, bit 3 private images (version 4); the other bits must be 0
if motion:   [u16 x] [u16 y] [varuint z] [u16 yaw]                  the own capsule: 1/256 tile, level, compass bearing
             [u8 packed]        bit 0 lying, bits 1-2 driver (0 grounded, 1 prone, 2 thrown, 3 zero-g), bit 3 can move, bit 4 slide, bit 5 noclip (the dev noclip, DV29), bit 6 drift (version 28), bit 7 on a climbable (version 37, D239)
             [varint pressTimer]    ticks until the next attempt into a blocked face
             if slide: [u16 targetX] [u16 targetY] [varuint ticksLeft]   (ticks left 1 to 65535)
             [f32 tilesPerTick] [f32 radius] [varuint moveDelayTicks]    the motion parameters for the next tick
[u8 inputBufferDepth]           frames waiting in the server's jitter buffer for this connection
if tables:   [count] [varuint first] { str(256) }*     the private string table's new entries
             [count] [varuint first] { str(512) }*     the private resource table's
             [count] [varuint first] { appearance }*   the private appearance table's (private entries may name public ones)
if screen:   [count added] { [varint id diff] [tableref appearance] [str(128) screenLoc] [u8 group] }*
             [count changed] { the same }*
             [count removed] { [varint id diff] }*
if images:   (version 4, section 14.3) [count added] { [varint id diff] [tableref appearance] [varuint attachedTo] }*
             [count changed] { the same }*
             [count removed] { [varint id diff] }*
```

D40 (scope 2h): the block is **per connection**: another session's block never appears in a session's bytes, and the shared body
never depends on a session. What only its owner may see travels in the private tables, not in the public ones every session
receives: the health doll's per-zone state, the health indicator, the fullscreens and alerts are tg's screen objects
(`client.screen`, D107) whose icon states are private strings; a client reads them with `tableref`s whose private bit is set. A
public appearance never names a private entry (the codec refuses it at both ends). No health or damage number is on the wire at
all: only tg's own pictures (icon states, overlays), as the pictures other players see too.

- The **motion** part (about 16 bytes of state and 8 of parameters) is the authoritative state after the frame `lastInputSeq`
  (the header's), what the predictor reconciles against (D103). Positions are inside the map (the tile of `x` and `y` within 1 to 255);
  `tilesPerTick` is 0 to 8 and `radius` 0 to 4 (finite); `moveDelayTicks` 0 to 32767; `pressTimer` a 16-bit signed number. Since
  version 66 the press timer is followed by the velocity and the parameters end with the movement feel (section 66).
- The private tables are **per session and append-only for it**: the Snapshot lists them whole (from index 1), every Delta after
  it the entries its tick added; `first` is the table's length before the block plus one. A rejoin starts them afresh.
- A **screen** entry's id is the screen object's world id; the three lists have their ids in strictly increasing order, at most 512
  entries each; `screenLoc` is tg's `screen_loc` text (`"WEST,CENTER-2"`, `"EAST-1:28,CENTER-1:15"`, at most 128 bytes, may be null),
  which the client maps from tg's 15 x 15 view grid onto its screen. An entry's appearance is a private table entry (or a public one).
- A part is present exactly when it is not empty (the flag says so): an empty block is two bytes, `00 00`.

Example (a capsule standing at the centre of tile 10,10 on level 1 facing north, running parameters, one frame in the buffer):

```
01 | 00 0A 00 0A | 01 | 00 00 | 08 | 00 | AB AA AA 3E | 33 33 33 3E | 03 | 01
flags; x = 2560 and y = 2560; z; yaw; packed (standing, grounded, can move); press timer; 1/3 tile per tick; radius 0.175; move delay 3; depth 1
```

## 5. Records and fields

### 5.1 Entity record (Snapshot entities and Delta spawned)

```
[id: varint difference] [varuint type] [varuint x] [varuint y] [varuint z] [u8 dir] [str(512) name] [f32 glide] [varuint ticksSinceLastMove]
[u8 flags]    bit 0 capsule, bit 1 posture, bit 2 light (version 3), bit 3 entity flags (version 4); the other bits must be 0
if capsule:   [pos] [u8 yaw]
if posture:   [u8 posture]            (never 0: a standing entity has no posture byte)
[varuint appearance]                  a public appearance table index, 0 for none
if light:     [light]                 (section 13)
if flags:     [u8 EntityFlags]        (version 4, never 0, section 14.4)
```

Every field of `NetField` but `OwnMob` (which is per connection). The wire's field set is exactly the `NetField` enum (D40: the
server sends no hidden data); `real_name`, `key`, health and every other tg var stay on the server until a milestone adds a field
here under a version bump. The smallest record is 14 bytes; a run-of-the-mill prop is 15 to 17; the entity flags byte is present only while it is not 0.

| Field | Range (decoder and encoder) |
|---|---|
| id | at least 1 (ids are `World.AllocateId` values: positive, never reused in a world) |
| type | an index below 65536; in a Snapshot also below its type count; in a Delta checked by the replica against the table |
| x, y | 1 to 255 (the replica also checks them against the `MapGrid`'s size) |
| z | 1 to 80 (and the map's levels) |
| dir | 0 (no direction) or one of BYOND's eight: 1, 2, 4, 8, 5, 6, 9, 10 |
| name | at most 512 bytes of UTF-8, may be null; tg's visible name, never `real_name` |
| glide | finite, 0 to 256 (tg clamps `glide_size` to 1 through 32; 0 is BYOND's default) |
| last move | the server tick in which x, y or z last changed, sent as the **ticks since** it, relative to the message's tick (a Snapshot's, a Delta's); it never lies in the future, so the number is at most the tick (`BadValue`) |
| pos | `NetField.Pos`: a capsule mob's continuous position, x and y in 1/256 tile (`[pos]`, section 2); its tile is the record's x and y (the invariant of the locomotion bridge). A record without the capsule flag has `PosX = x * 256`, `PosY = y * 256`, yaw 0 (the decoder fills them in; `EntityState.HasPos` says which) |
| yaw | `NetField.Yaw`: where a capsule mob looks, the high byte of its `MotionState.Yaw` (256 per turn, 0 north, clockwise); the billboard's frame by viewer angle (D104) |
| posture | `NetField.Posture`: bit 0 lying (tg `LYING_DOWN`), bits 1-2 the quarter turns of `lying_angle` (0, 90, 180, 270), bits 3-7 zero (`PostureBits`) |
| appearance | `NetField.Appearance`: a public table index (a private one is `BadValue`, D40); the Snapshot checks it against its own table, the replica against the table it holds |

`Pos`, `Yaw` and `Posture` are the "what others see" fields of M2: where a body is, which way it looks, whether it lies. Injuries
are in the appearance (tg's damage overlays), never as numbers.

### 5.2 Change record (Delta changed)

```
[id: varint difference] [varuint mask] { the values of the set bits, in wire order }
```

The mask is a `varuint` over the changeable fields in this **wire order** (the common ones in the first byte, so a mover's usual
change, Pos and Yaw, is one mask byte): bit 0 Pos, 1 Yaw, 2 X, 3 Y, 4 Z, 5 Dir, 6 LastMoveTick, 7 Appearance, 8 Posture, 9 Name, 10
GlideSize, 11 Light (version 3), 12 Flags (version 4: the value is a `u8` `EntityFlags`, section 14.4). An empty mask, or a bit above 12, is `BadValue`. The values follow in that order: `Pos` as `[pos]` (two `u16`), `Yaw` as a
`u8`, `X`, `Y`, `Z` as `varuint`, `Dir` as a `u8`, `LastMoveTick` as the `varuint` ticks since, `Appearance` as a `varuint` public
index, `Posture` as a `u8`, `Name` as a string, `GlideSize` as an `f32`. `id` and `type` never change (a DM object keeps its type and
id for life). A capsule mover's usual change (Pos and Yaw) is about 8 bytes: id (1), mask (1), pos (4), yaw (1) and, in the tick it
crosses into the next tile, X or Y, the last move tick and, with them, Pos (the replicator always sends Pos with a tile change of a
capsule). A change to an entity that has not sent `Pos` before makes it a capsule for the replica (`ReplicaEntity.HasPos`).

### 5.3 What the replica checks that the codec cannot

`ReplicaWorld` (section 6) knows the tables and the map, so it also refuses (as a desync, `BadValue`): a Delta that does not follow
the previous tick, a spawned id that exists, a changed or despawned id that does not, a type index outside the table, new types
that do not start at the table's length, a position (tile or `Pos`) outside the `MapGrid`; a new table entry whose `first` is not the
table's length plus one; a reference (an entity's, a screen entry's, an appearance's icon, icon state or overlay) to an entry the
table does not have; an overlay that does not name an earlier entry of its own table; a public appearance that names a private
entry; a cell change outside the map; a screen entry that is added twice, changed or removed without existing.

### 5.4 Ids

The ids of a list are `varint` differences from the previous id of the list (zero before the first). In a Snapshot the entities
are in strictly increasing order, so every difference is positive and typically 1 to 3 (one byte); in a Delta the order is the
sender's (the replicator's lists are in increasing id order). The screen entries' ids are in strictly increasing order too. The
decoder rebuilds each id, refuses one below 1 or past the range of a `long`, and refuses a difference of zero.

### 5.5 Appearance entries

An **appearance** is BYOND's own model: immutable and interned, an atom points at one (scope 2h). The server's replicator builds an
entry from an atom's builtin appearance vars and its `overlays` and `underlays` (`Anomaly.Core.DmShim.IDmAppearance`), looks it up by
value and sends it once; **one id per distinct appearance, append-only for the run**, and an entity's `Appearance` is an id. The
client composes it into a billboard texture (D104: tg's own icons and GAGS).

```
[varuint presence] then the fields whose bit is set, in this order:
 bit 0 icon        tableref (resource)          bit 8  pixel x, y     varint, varint
 bit 1 icon state  tableref (string)            bit 9  pixel w, z     varint, varint
 bit 2 dir         u8                           bit 10 transform      6 x f32  (a b c d e f of the /matrix)
 bit 3 colour      u32  0xRRGGBBAA              bit 11 flags          varuint (BYOND appearance_flags, 16 bits)
 bit 4 matrix      20 x f32                     bit 12 blend mode     u8
 bit 5 alpha       u8                           bit 13 overlays       [count 1..256] { tableref }*
 bit 6 layer       f32                          bit 14 underlays      [count 1..256] { tableref }*
 bit 7 plane       varint                       bit 15 maptext        tableref (string), width, height varuint, x, y varint (version 18)
```

A field is present **exactly when it is not its default** (no icon, no state, dir 0, white `0xFFFFFFFF`, no matrix, alpha 255, layer
+0, plane 0, pixels 0, no transform, flags 0, blend 0, no overlays): one encoding per entry, and the decoder refuses a bit set on a
default value. An entry with nothing set is the single byte `00`.

- **Values.** `icon` is a resource: an icon file path (`icons/mob/human/bodyparts_greyscale.dmi`) or a GAGS reference
  (`gags:<config type path>|<colours>`, composed by the client), in the table's resource strings. `dir` is 0 or one of BYOND's eight
  (an entity's own `Dir` wins for a top-level appearance, so the replicator sends 0 there and one appearance serves every facing;
  a screen object keeps its own). `colour` is BYOND's `color` text as RGBA, `matrix` a BYOND colour matrix in its 4 x 5 row order
  (`rr rg rb ra gr gg gb ga br bg bb ba ar ag ab aa r0 g0 b0 a0`; a list of 9, 12 or 16 numbers is completed the way BYOND completes
  it, an identity is none); when a matrix is present the colour is white. `layer` is BYOND's (negative `FLOAT_LAYER` styles float above
  the parent and are ordered among themselves by the client), `plane` only orders the few special planes (the lighting and emissive
  planes are not ported, DV12; `FLOAT_PLANE`, -32767, is the parent's), pixels are BYOND pixels (32 to a tile) in 16 bits. The
  `transform` is tg's lying turn and the like (the identity is none). Non-finite floats are refused (NaN, the infinities).
- **Overlays and underlays** are table references to **earlier** entries of the same table (the codec refuses a reference to the entry
  itself or a later one: one pass builds a table, nothing cycles), in BYOND's order. A string in tg's `overlays` list (an icon state
  of the parent's icon) is an entry with only a state, a resource an entry with only an icon. A nesting deeper than six levels, or
  more than 256 overlays, is cut.
- **Public and private.** The public table holds what any viewer sees. A session's private appearance table holds its HUD (the
  screen objects, 4.3); its entries may name public resources and strings but the replicator interns them privately (a health level
  must not enter a table another session receives).
- **No appearance.** An entity that shows nothing (no icon, no overlays, no underlays) has appearance 0: no row.

### 5.6 Tables

Three tables per run (public) and per session (private): **resources** (at most 65536 entries, 512 bytes each: file paths and GAGS
references), **strings** (at most 2^20, 1024 bytes since version 18: icon states, maptext) and **appearances** (at most 2^22). Index 0 is "none" and index 1 the
first entry. An entry never changes and is never removed. The Snapshot carries the public tables whole and the private tables
whole (inside the private block); a Delta carries the rows its tick added (4.1, 4.3), so a table grows on the client exactly as on the
server and a late joiner holds the same table as an early one.

## 6. The client replica

`ReplicaWorld` is the client-side copy of the entity table (the Godot client, the bots and the tests use it). It is
single-threaded. `Apply(MapGrid)`, `Apply(Snapshot)`, `Apply(Delta)` and `TryApply(bytes, out id, out error)` (decode, then
apply, for a State-channel message) work in the order the State channel delivers.

- **Snapshot** replaces the table: entities it lacks despawn, new ones spawn, the ones both have keep their `ReplicaEntity`
  (their history restarts at one sample: a resync is no motion); an id whose type path changed is a despawn plus a spawn.
  Entities must come in increasing id order, within the table and the map. A Snapshot of an older tick than the last is refused,
  the same tick is a legal resync.
- **Delta** must be tick + 1 of the last Snapshot or Delta. Any inconsistency throws `ProtocolException(BadValue)`, sets
  `IsDesynced`, and every later Delta is refused until a Snapshot arrives. The client then leaves and joins again; this never
  happens on a reliable ordered channel in M1.
- **Events.** `Spawned` and `Despawned` are raised after the whole message was applied, so a handler sees the full tick. A
  despawn followed later by a spawn of the same id gives a new `ReplicaEntity` with a fresh history.
- **Samples.** Each entity keeps a ring of the last `History` (16) samples `(tick, x, y, z, dir, glide, lastMoveTick, posX, posY,
  yaw, posture, appearance)`, newest first: one at spawn and one for every tick in which any field but the name changed.
  `Sample(age)`, `SampleCount`. `PosX` and `PosY` are the tile centre for an entity that has never sent `Pos`.
- **`TrySample(renderTick, out from, out to, out t)`**: the two kept samples around a fractional tick and the fraction between
  them; before the oldest both are the oldest, after the newest both are the newest (no extrapolation).
- **`TryInterpolate(renderTick, out pose)`** (an addition: the math the client draws with, tested headless): the entity moves a
  whole tile in one tick on the server, so the client draws it **gliding** from the render time of the step's tick to the new
  tile over `32 / glide_size` ticks (BYOND's own glide; the length is clamped to 1 to 10 ticks), or, when `glide_size` is 0, over
  the ticks since the entity's previous move (3 if there was none). A step that arrives while a glide runs starts from where the
  entity is drawn then, so a walk of one tile per 3 ticks, or a run of one per tick, is continuous. A move of more than 2.5
  tiles or to another level is a teleport (no glide). The direction changes at the tick of its sample. After the newest sample the
  entity stays where it is. The client's render time is the latest server tick minus about 2 (scope 2g); the clock that smooths it
  belongs to the client. A capsule mob (`ReplicaEntity.HasPos`) is not drawn by this glide but from its `Pos` samples (M2 P7, P8).

**Version 2 state.** The replica also keeps (all of it applied before any event of the message is raised):

- the **public tables** (`Resources`, `Strings`, `Appearances`; `ResolveResource`, `ResolveString`, `TryGetAppearance` take a
  `TableRef`, public or private) and the session's **private tables** (`PrivateResources`, `PrivateStrings`, `PrivateAppearances`);
  a Snapshot replaces all of them, a Delta appends its rows (the table's length plus one, else a desync);
- the **map's cells**: `IsSolid(x, y, z)` (outside the map: true) and `TurfAppearance(x, y, z)` start from the `MapGrid` (the replica
  copies the bits, the `MapGrid` object stays as received) and follow the Delta's cell changes; `CellsChanged` is raised once per
  Delta that changed cells, with the cells in the Delta's order;
- the **own state** (`Own`: the tick, the acked input seq, the motion state and parameters, the input buffer depth), the newest
  private block; `OwnStateReceived` is raised after every applied Snapshot or Delta once its private block is applied, the last event
  of the message: the predictor's reconciliation point (D103);
- `client.screen` (`Screen` by id; `ScreenEntryChanged` for each added or changed entry, `ScreenEntryRemoved` for a removed one,
  after the message is applied; a Snapshot replaces the whole screen and reports the entries it lacks as removed);
- the **collision view** (`CollisionView`, an `ICollisionView` for `Anomaly.Core.Motion`'s predictor): `IsSolid` from the cells and
  `GatherCapsules` from the other standing capsule mobs (`HasPos`, not lying: D51) at their **newest** received `Pos` (not the
  render-delayed one), on the same level, within the range, in increasing id order, the caller's own id excluded; every capsule has
  the default radius.

A Snapshot is validated before anything of the replica changes (entities, tables, private block), so a refused Snapshot leaves the
replica as it was.

## 7. Transports (`Anomaly.Net`)

### 7.1 Interfaces and the threading rule

`IServerTransport`, `IConnection`, `IConnectionRequest` and `IClientTransport` (`Transport.cs`). **Events are raised only inside
`Poll()`, synchronously, on the thread that calls it** (D31): the server host calls `Poll()` on its host thread between ticks, so
nothing reaches the world from another thread; the Godot client polls in `_Process`. LiteNetLib's own threads only move bytes.
`Send` copies the message before it returns; a message handler's span is valid during the call only.

Channels (`Channel`): Control 0 and State 1 are reliable ordered; Input 2 is unreliable. A `Send` of more than
`Channels.MaxUnreliableBytes` (1023) bytes on Input throws `ArgumentException` in every transport (LiteNetLib throws
`TooBigPacketException` past its MTU, the loopback mimics it). A send on a closed connection is dropped silently.

Counters: every connection and client transport has the totals of the interfaces and, through `IChannelCounters`, the payload
bytes and messages per channel and the wire bytes (LiteNetLib's own statistics, headers and acks included; the loopback's equal
the payload). `RoundTripMs` is LiteNetLib's measured round-trip time (0 until measured; one ping a second).

A request is answered inside the `ConnectionRequested` event with `Accept()` or `Reject(reasonBytes)`; one left unanswered
is rejected without data, and so is one whose handler throws (the exception still propagates out of `Poll`).

### 7.2 LiteNetLib 2.1.4 (`LiteNetServerTransport`, `LiteNetClientTransport`)

Measured facts the implementation relies on:

- Three channels (`ChannelsCount = 3`). Reliable packets carry their channel number; **an unreliable message always reports
  channel 0**, so the transports map "delivered unreliable" to the Input channel and refuse other combinations (counted in
  `DroppedMessages`). The initial MTU is 1024 and `MtuDiscovery` is off.
- A reliable ordered message of any size is fragmented and delivered (300 KB in a few ms on loopback); unreliable messages
  over 1023 bytes throw. The NuGet build's packet loss and latency simulation does nothing; use the loopback.
- The request's `Data` is exactly the bytes the client passed. The server judges the Hello in `OnConnectionRequest`, so a
  refusal happens before any peer exists: it uses `RejectForce`, which sends the reject packet without creating a peer (a
  request flood costs one packet each and no state). `MaxPendingRequests` (default 64) is the most requests raised in one
  `Poll`; the rest of that poll's requests get `ServerFull` without being raised.
- A reject or disconnect reason travels in one packet (about 1010 bytes at most): longer reasons are sent as no reason.
- **Inbound size.** LiteNetLib has one knob, `MaxFragmentsCount`, for both directions. The server sets it to 8 (about 8 KB), so a
  client message of more fragments is dropped before it is reassembled (nothing held in memory; the channel carries on), and a
  message of 257 to 8000 bytes reaches the host, which counts a strike. A server `Send` of a message that needs more fragments (a
  join Snapshot) widens the knob to 65535 for that call only, on the host thread, and puts it back. Clients accept up to 65535
  fragments (the 16 MiB `MaxOutboundMessageBytes` need about 17000).
- LiteNetLib raises a local close (our own `Disconnect`) synchronously inside the call; the transports defer that event to the
  next `Poll`, so every event obeys the rule above.
- Timeouts: `DisconnectTimeout` (5000 ms default; a constructor argument) with a ping every second; ICMP "unreachable" is
  treated as a timeout (`DisconnectOnUnreachable`). A connect to a closed port ends as `ConnectFailed` after
  `MaxConnectAttempts` tries `ReconnectDelayMs` apart (10 and 500 by default; settable on the client transport before
  `Connect`).
- Server: normal mode (two to four LiteNetLib threads), `Flush()` is `TriggerUpdate()`. `Start(address, 0)` takes an ephemeral
  port (`Port`). `Stop()` disconnects everyone with no reason; the `Disconnected` events of that stop come at the next `Poll`.
- Client: **manual mode** (no threads; `Poll()` runs `ManualUpdate` with the time since the last call) for bots (150 in one
  process, scope risk 5) and normal mode for the Godot client. `Connect` returns at once; a host name is resolved on a pool
  thread (an IP literal and `localhost` need no lookup), so a failing lookup cannot freeze the main thread. A transport can
  connect again after `Disconnected` (the bots' reconnect with a token).
- How a connection ends (`TransportDisconnectKind`): `RemoteClose` (with the other side's data), `LocalClose`, `Timeout`,
  `Rejected` (with the reject data), `ConnectFailed`.

### 7.3 The loopback (`LoopbackNetwork`)

An in-memory network of one server and any number of clients for tests (scope 2f). It is single-threaded and **moves only when
its owner calls `Advance`**: latency counts network steps (one per server tick in the tests, 50 ms at 20 TPS), so a run is
deterministic, independent of the wall clock and replays exactly from `LoopbackOptions.Seed`.

- **Latency.** A message, connection request, accept, reject or close sent during step S is raised by the receiver's first `Poll`
  at or after step S + L, with L drawn uniformly from `[MinLatencySteps, MaxLatencySteps]` per message (0: the next `Poll`).
- **Reliable channels** (Control, State) never lose and never reorder. A "lost" message (probability `LossRate`, drawn again
  after each retransmission, at most 8 times) is retransmitted `RetransmitSteps` later, and every later message of that channel
  waits behind it (head-of-line blocking, as in LiteNetLib's reliable ordered channel). An accept, a reject or a close waits for
  everything sent to that side before it.
- **Unreliable channel** (Input). A message is dropped with probability `LossRate` (`DroppedUnreliable` counts them) and, with
  probability `ReorderRate`, is held back to be delivered right after the next message of the same direction (a held message that
  no later message follows is released at the next `Advance`). A message over `MaxUnreliableBytes` is refused at the send.
- **Determinism.** One generator (SplitMix64) seeded with `Seed`, three draws for every message (latency, loss, reorder) and one
  more per retransmission, in send order. One seed and one script of sends give one schedule on every machine.
- **Connections.** `CreateServer()` once per network; `Start` registers it by port (0 takes the next free one from 40000, and
  the address is ignored); a connect to a port with no server fails with `ConnectFailed` at the next `Poll`. `Stop`/`Disconnect`
  behave as LiteNetLib's: the other side sees `RemoteClose` with the reason, this side `LocalClose` at its next `Poll`.
  `MaxPendingRequests` works as in the LiteNetLib server.
- **Dropping a client** (`LoopbackClientTransport.Drop()`, an addition): the client vanishes without a goodbye, nothing is
  delivered in either direction, and both ends raise `Timeout` `TimeoutSteps` (100) steps later.
- `RoundTripMs` is twice the last delivered message's latency in steps times 50.

## 8. Limits

`Limits` (src/Protocol/Limits.cs) holds the caps; P2 and P3 may tune a default and record it here, never remove a cap.

| Limit | Value | Where it is enforced |
|---|---|---|
| inbound message (client to server) | 256 bytes | the host before decoding (`Codec.TryDecode` also refuses over-long Hello and InputFrame: `TooLong`) |
| player name | 42 characters, 168 bytes | codec (bytes), `NameRules` (characters, control characters): the host |
| strikes | 3 in 200 ticks | the host (an unknown id, a decode error, a command before Welcome, an oversized message) |
| `InputFrame` rate | 60 per second, burst 30 | the host (tg's own keysend flood rule runs on top) |
| queued commands per session per tick | 16 | the host |
| pending connection requests | 64 per poll | the transports |
| `max_players` | 200 by default | the host |
| outbound message (server to client) | 16 MiB | every decoder (`TooLong`) and encoder |
| `ServerText.Html` / `.Type` | 16384 / 64 bytes | codec |
| `DisconnectInfo.Text` | 1024 bytes in the codec (keep under 1000 for LiteNetLib's single packet) | codec, transports |
| entity name | 512 bytes | codec |
| DM type path | 256 bytes | codec |
| type table, `MapGrid` types | 65536 entries | codec |
| entity records per Snapshot or Delta | 262144 (spawned plus changed plus despawned) | codec |
| map size | 255 x 255 x 16 | codec, replica |
| glide size | 0 to 256 | codec |
| a count against the bytes left | items x smallest item size | `WireReader.ReadCount` |
| `MotionInput` message | at most 4 frames, 64 bytes (31 used); rate 30 per second, burst 15; after a server stall (a tick that starts n whole ticks late) every bucket gets a stall credit of n ticks of its rate, above its burst, for that tick's poll only (M3 I1b: a 3.6 s stall had kicked a soak client for flooding; at most the 200-tick strike window) | codec; the host (the rate) |
| `Action` message | 248 bytes (a dev command's 200 bytes of text is the largest part; version 15 adds a radial pick's 15; a version 16 UiAct's body, at most 194 bytes, fits in the dev command's room); a UiAct takes the click bucket, a UiClose only the overall one; rate 20 per second, burst 10 overall; per kind: click 10 per second burst 5, keybinding 10 per second burst 5, hover 10 per second burst 5 (a hover over its rate is dropped silently), toggles 4 and 4, dev 4 and 4 | codec; the host (the rate) |
| click and hover targets | an id of at least 1; a turf of x and y 1 to 255, z 1 to 80; an icon pixel 0 to 32 | codec; the host (a turf or id the world does not have, or out of tg's view range of the mob, is dropped and counted) |
| keybinding codes | 1 to 9 (`KeybindingCode`; 9, `StopPulling`, since version 20) | codec; the host |
| `ContextHints` message | 10 hints, name 256 bytes, label 64 bytes each, 959 bytes in all | codec |
| private images | 64 in all and per list | codec, replica |
| `ScreenEntry.Group` | 0 to 6 (`HudGroup`) | codec, replica |
| input seq lead | 1200 frames past the last applied | the host (a forged seq is a strike) |
| jitter buffer | 4 frames per session (`Limits.MaxJitterBufferFrames`; 3 until the M2 integration) | the host |
| resource table (public, or one session's private) | 65536 entries, 512 bytes each | codec, replicator |
| string table | 2^20 entries, 1024 bytes each (256 before version 18) | codec, replicator |
| appearance table | 262144 entries (the Snapshot carries a table whole) | codec, replicator |
| table entries added in one message | 2^20 (resources, strings and appearances together) | codec |
| overlays (or underlays) of one appearance | 256, nesting cut at 6 | codec, replicator |
| cell changes in one Delta | 65536 | codec, replicator |
| `client.screen` entries | 512 per list of a block | codec |
| `screen_loc` | 128 bytes | codec |
| vis children of a screen entry (version 12) | 1 to 16 (`Limits.MaxScreenChildren`), an id >= 1, an appearance never none | codec, replica (the table check); the replicator lists the first 16 |
| `Pos` | the map's tiles (a tile 1 to 255), 16 bits per axis | codec |
| motion parameters | speed 0 to 8 tiles per tick, radius 0 to 4 tiles, move delay 0 to 32767 | codec |
| `Sounds` message (version 13) | 64 events, 32 new paths of 1 to 256 bytes, a path table of 65536 entries, channel 0 to 1024 (an update 1 to 1024), volume 0 to 200 half units, environment byte 0 to 26, a source tile of x and y 1 to 255 and z 1 to 80, frequency finite, non-zero and at most 10^6 in magnitude, offset above 0 and at most 36000; 9,992 bytes in all | codec; replica (the path table, the tile inside the map); the server's per-session budget per tick (the quietest plays dropped first, never a stop, an update or a mute) |
| animation (version 9) | 4 tracks, 36 steps a track (16 before version 32), loop 0 (for ever) to 65535, time and delay 0 to 36000 ds, values finite and at most 10^6 in magnitude, easing a curve 0 to 8 with the in and out bits | codec; the replicator (it drops the oldest track and does not send a longer chain) |

**Allocation.** A decode allocates at most about 24 bytes per input byte plus a constant for the fuzz inputs (lists sized from
a count that was already checked against the bytes left, strings of at most twice their bytes); a forged count or length is
refused with under 4 KB allocated. The one exception is the appearance table, whose entries are objects of about 100 bytes: a table
of one-byte empty entries costs up to 130 bytes per input byte, bounded by the 262144-entry cap (about 30 MB for the biggest message
a table can make); a table of ordinary entries stays under the 24. The fuzz tests assert all of it with
`GC.GetAllocatedBytesForCurrentThread`.

## 9. Versioning

`ProtocolVersion.Current` is **13** (sections 13 to 22 for versions 3 to 13). Every change of the wire format bumps it (version 1 was M1's: one-byte change mask, absolute last
move tick, no tables, no private block, a MapGrid of types only). A server and a client talk only when their
versions are equal: the server answers any other version with `DisconnectReason.WrongVersion` (a `Disconnect` message in the
reject data) before a connection exists, and because the Hello's first 7 bytes (id, magic, version) never change, it can do so for
any future version. A client that gets a message it cannot decode, or whose replica reports a desync, leaves.

## 10. Reserved for later milestones

- **M3: doors and `ChangeTurf`** (done, version 3, section 13): the cell flags (faces, opacity, starlight), the base lights, the
  turf type changes and the entity light.
- **M4a** (done, version 4, section 14): the click and hover targets, the keybindings, `ContextHints`, the screen entries' group, the private images and the entity flags. Container contents are **not** entities in M4a (a storage's contents are screen objects of its viewer); they move to "when a visible container shows its contents" (a tray, a crate: M5).
- **M8-M10: per-viewer visibility** (`see_invisible`, `client.images` for the medical and security HUDs, interest management).
- **M10: unreliable deltas with acks.** Deltas move to the Input-like unreliable-sequenced path with our own splitting at or
  below the MTU and per-client baselines (a change stamp per field exists in the replicator for this); the header gains the ack
  fields; `Snapshot` stays the resync. The table rows and the private block would then need the baselines too. Prediction and the
  jitter buffer already work on the unreliable Input channel.
- Replay and rollback (D38) need nothing from the wire: the replicator reads outside the world.

## 11. Tests and measurements

`tests/Anomaly.Server.Tests/Protocol` (primitives, round trips at every cap, refusals, seeded fuzz with allocation bounds for every
decoder including version 2's, a scripted server table the replica must reproduce tick by tick, samples and interpolation;
`CodecV2Tests` and `ReplicaWorldV2Tests` for version 2: documented bytes, the appearance entries, the private block, the cells, the
tables, the screen, the collision view) and `Net` (the loopback's latency, ordering, loss, reorder, determinism, closing and
counters; real sockets on 127.0.0.1 with an ephemeral port: a server and two manual-mode clients and one normal-mode client, a Hello
rejected for a wrong version, a bad name or no Hello, a message in each direction on each channel, a disconnect with a reason, a
timeout, a failed connect, the request cap, an oversized message). The server side of version 2 is tested through the replicator
(`tests/Anomaly.Server.Tests/Replication`, docs/ai/specs/replication.md section 10).

Measured, version 1 (Release, one core of the owner's M1 Pro): a Delta of 17, 52 and 150 one-tile moves (x, y, dir, last move tick)
had a body of 141, 421 and 1206 bytes (about 8.2 bytes per move); encoding the body takes about 1 µs (no allocation with the
reusable buffer), the per-connection header plus the body copy about 0.01 µs, decoding it about 1.5 to 4 µs. A Snapshot is about 16
bytes per entity with short names (1000, 4000, 16000 and 65000 entities: 15.6 KB, 63 KB, 255 KB and 1.1 MB; encode 0.4 to 7 ms,
decode 0.2 to 3.4 ms). A 255 x 255 map of 10923 runs is 22 KB.

Measured, version 2 (Release; humans running in straight lines through the 48 x 48 room, payload bytes of the State channel messages
as the host sends them, LiteNetLib's 4 to 8 bytes of header per packet and its fragmentation above the 1024-byte MTU not
included): **7.3 KB per second per player with 50 moving humans (8.1 with their yaw changing every tick) and 20.7 KB per second with
150 (23.0)**. A moving human costs about 6.7 bytes a tick in the shared body (id 1, mask 1, pos 4, a yaw byte and a move tick when
they change), a player's own header and private block 26 bytes a tick; the join is a MapGrid of 724 bytes and a Snapshot of 1.7 KB
(50 humans) or 4.3 KB (150). Scope 2h expected about 30 KB/s at 150 movers. Details in docs/ai/specs/replication.md section 9.

## 13. Version 3 (M3 package P5)

Version 3 adds, at the end of version 2's layouts, what the map port needs (scope 2f, 2h, 2j; D24, D114, D115, D116). The types are
the contracts' (`src/Protocol/MessagesV3.cs`, `Limits.cs`, `NetField.Light`); the codec is `Codec.V3.cs` behind the same entry
points. A version-2 client and a version-3 server (or the other way round) never talk: the Hello check refuses the version.

**Cell flags** (`CellFlags`): bits 0 to 3 the blocked faces with `TileEdges`' values (north 1, south 2, east 4, west 8), bit 4
opaque (tg's `IS_OPAQUE_TURF`), bit 5 starlit space (`enable_starlight`). A decoder refuses any other bit. The server computes the
faces with `LocomotionSeam.FacesOf` (the predicate its controller reads, so a client predicts against the faces the server
collides with, D103), opacity and starlight with `CellState`.

**MapGrid**, after the solidity bits:

| Part | Layout |
|---|---|
| flag runs | `[count] { [varuint run length] [varuint flags byte] }*`, none or covering every cell, in `Runs`' order |
| base-light table | `[count <= 4096] { [u32 0xRRGGBBAA] }*` (index 1 is the first entry; 0 is none) |
| base-light runs | `[count] { [varuint run length] [varuint index <= table size] }*`, none or covering every cell |
| starlight | one light (below): tg's `GLOB.starlight_range`, `_power`, `_color` with `LIGHTING_HEIGHT_SPACE`; range 0 for none |

**Delta**, in the body after the version-2 lists: the parts byte gets bit 5 (`PartV3`), and when it is set one more byte follows
the version-2 lists, saying which lists come: 1 cell flag changes, 2 new base lights, 4 base-light changes, 8 new turf types,
16 turf type changes, 32 starlight (a list is written only when it is not empty). Cells are `[varuint x] [varuint y] [varuint z]`
as in version 2.

- cell flag changes: `[count] { [cell] [u8 flags] }*` (a door opens: the flags of its cell change)
- new base lights: `[varuint first index] [count] { [u32] }*` (the first index is the table's length before the Delta, plus one)
- base-light changes: `[count] { [cell] [varuint index] }*` (an area edited; none in M3 after the join)
- new turf types: `[varuint first index] [count <= 1024] { [string path] }*`, appended to the MapGrid's turf type table
- turf type changes: `[count] { [cell] [varuint index] }*` (tg's `ChangeTurf` after the join)
- starlight: one light (`set_starlight`)

All the cell lists together hold at most `Limits.MaxCellChangesPerDelta` (65536) records; the table caps are
`MaxBaseLightEntries` (4096) and `MaxTurfTypeEntries` (the type table's). The replica checks what the codec cannot (section 5.3's
way): a table entry added at the wrong index, an index past the table, a cell outside the map are desyncs.

**Light** (`LightState`): `[f32 range]`, and only when the range is not 0: `[f32 power] [u32 rgba] [u8 flags: bit 0 on]
[f32 angle] [u8 dir] [f32 height] [f32 offset x] [f32 offset y] [u8 system]`. The decoder refuses non-finite numbers, a range above
32, a power above 64 in size, an angle outside 0 to 360, a direction that is none of the eight, a height above 16, an offset above
16 in size, a system outside 1 to 4, and a range of negative zero (one encoding per light).

**Entities.** The spawn record's flags byte gets bit 2: a light follows the appearance. `NetField.Light` is the twelfth bit of the
change mask (after `GlideSize`); its value is a light, and a range of 0 means the light is gone. `NetField.ChangeableV2` and
`AllV2` are gone: `Changeable` and `All` are the version-3 sets.

**Measured** (Release machine of the owner's M1 Pro, generated 255 x 255 station with 15 x 15 rooms, a margin of space, 6 base
lights, panes on 2.5 % of the floor tiles and noisy per-tile smoothing states, so the appearance runs are the worst case): a
`MapGrid` of **161 KB**, of which version 3's flags, base lights and starlight are **33 KB** (12 363 flag runs, 4 303 base-light
runs; version 2's part is 128 KB with 17 597 type runs and 42 108 appearance runs); encoding takes 6 ms, decoding 5 ms
(`JoinSizeV3Tests`). A Meta join adds its Snapshot (about 16 bytes per entity, section 11) and the appearance tables; I1 measures
the real one. A flag change costs 5 bytes, a base-light or turf type change 5 to 8, a light about 31 bytes in a spawn record or a
change.

**Tests.** `CodecV3Tests` (round trips of every part, the refusals, the cell cap, an entity light), the fuzz corpus of
`CodecFuzzTests` now holds every version-3 part (random bytes, truncations, bit flips, insertions, forged counts, NaN at every
offset: nothing escapes, accepted messages re-encode to the same bytes), `ReplicaWorldV3Tests`, and the replicator's
(`ReplicatorV3Tests`, replication.md section 13).

## 14. Version 4 (M4a package P6)

Version 4 adds, at the ends of version 3's layouts, what the interaction base needs (scope 2a, 2b, 2d, 2e, 2g, 2i; D124, D125, D127,
D128, D131; `docs/ai/specs/interaction.md`). The types are the contracts' (`src/Protocol/MessagesV4.cs`, the version-4 members of
`MessagesV2.cs` and `Messages.cs`, `Limits.cs`, `NetField.Flags`); the codec is `Codec.V4.cs` (the new message, the targets, the
images) and the version-4 parts of `Codec.V2.cs` and `Codec.cs`, behind the same entry points. A version-3 client and a version-4
server (or the other way round) never talk: the Hello check refuses the version. A version-3 click (`TargetKind.None` with a
`Target` above 0) is an entity click to the encoder (`ActionMessage.EffectiveTargetKind`), so M3's clients and bots built from this
tree keep working.

### 14.1 The click, keybinding and hover actions (Action, client to server, Control)

```
Click (kind 4):      [u8 target kind] [target] [u8 button] [u8 modifiers] [u8 icon-x] [u8 icon-y]
Hover (kind 7):      [u8 target kind] [target] [version 100: u8 aim zone 1..9 on an entity, absent for none (section 97)]
Keybinding (kind 6): [u8 code] [u8 key up]
target:              Entity (1) and Screen (3): [varuint id >= 1]       Turf (2): [u8 x 1..255] [u8 y 1..255] [varuint z 1..80]
```

```
0A 07 04 01 AC 02 02 02 10 20     frame 7: a right click with ctrl on entity 300, icon pixel 16, 32
0A 07 04 02 FF 01 10 01 05 01 01  frame 7: a left click with shift and alt on the turf 255,1,16, icon pixel 1, 1
0A 07 07 02 0C C8 03              frame 7: a hover on the turf 12,200,3
0A 07 06 08 01                    frame 7: the release (key up) of throw mode held (code 8)
```

- **Target** (`ActionMessage.TargetKind`): `Entity` (an entity id in `Target`), `Turf` (`TurfX`, `TurfY`, `TurfZ`) or `Screen` (a
  `ScreenEntry.Id` in `Target`), for `Click` and `Hover`; `None` for every other kind. No field a kind does not use may be set
  (a click's turf has no id, a hover has no button, modifiers or icon pixel, a keybinding has no target): the encoder refuses it
  and the decoder reads it as a malformed message (`BadValue` or `TrailingBytes`).
- **tg's pixel** (`IconX`, `IconY`): 1 to `Limits.MaxIconPixel` (32), 0 for none; the server writes them into tg's params as
  `icon-x` and `icon-y` (interaction.md 3). Both bytes are always on the wire for a click.
- **Keybinding** (`ActionKind.Keybinding`): a `KeybindingCode` (1-8: swap hands, select right and left hand, activate in-hand,
  drop, quick equip, toggle and hold throw mode) and `KeyUp` (0 press, 1 release; any other byte is `BadValue`). The core's table
  is `ClientSeam.KeybindingType`; the codes never change meaning (a new binding is a new code). The host runs tg's own path
  (`kb.can_use(client)` then `kb.down(...)`, or `kb.up(...)` for a release), with `usr` the client's mob.
- **Hover** (`ActionKind.Hover`): the crosshair's target changed; it always has a target (the client clears its hints itself when
  nothing is under the crosshair). The host runs BYOND's `MouseEntered` on it.
- **Validation (D40).** Stateless, when the message arrives (a failure is a strike, three in 200 ticks close the session): the
  kind is known, the target kind is one of the three (a version-3 click counts as an entity), an id target is positive, a turf is
  inside the protocol's ranges, the button, modifiers, icon pixel and keybinding code are in range, no field of another kind is
  set. Stateful, in the command phase of the tick that applies the frame (`TryResolveTarget`; a failure is dropped and counted in
  `Session.ClicksRejected`, never a strike, because a click that races a despawn is normal): an entity must be one the replicator
  shows (`ReplicatedFields.IsEntity`: a registered movable on a turf, `invisibility` at most 25) and not deleted; a turf must be
  tg's `locate(x, y, z)`, so inside the map; entity and turf must be on the mob's level and within `get_dist <= 7` of it (tg's
  15 x 15 view); a screen id must name an object that is in the session's `client.screen` right now (a HUD entry, a hand or slot,
  an open storage's cell or item). A session without a mob drops them all.
- **Rates** (`ActionLimits`): every action takes a token from `Limits.ActionRate` (20 per second, burst 10) and one from its
  kind's bucket: `Click` 10 per second burst 5, `Keybinding` 10 per second burst 5, `Hover` 10 per second burst 5. A click or
  keybinding over its bucket is a drop that counts toward the flood close (30 drops in 200 ticks: `DisconnectReason.Flooding`); a
  hover over its bucket is dropped **silently** (sweeping the crosshair over a crowd is normal; the overall bucket still bounds
  a flood). A session keeps one pending hover: a newer hover replaces the older one before the frame it belongs to is applied, so
  at most one `MouseEntered` runs per session and tick (tg's `SSmouse_entered` keeps the last hovered atom per client anyway).
- **Replay.** The command log records the target kind, turf, icon pixel, keybinding and key-up of each action (`CommandLog`'s
  `a` extra has 12 values; M3's lines have 4 and read as before).
- **Cap:** `Limits.MaxActionMessageBytes` (248; version 4 adds at most `MaxActionV4Bytes`, 12, version 15 `MaxActionV15Bytes`, 15), under the inbound 256.

### 14.2 ContextHints (server to client, Control, `MessageId.ContextHints` = 11)

```
[u8 id = 11] [target: as above] [str(256) name] [count <= 10] { [u8 button] [u8 modifiers] [str(64) label] }*
```

tg's screentip of the hovered target (D125): the target as the hover named it (an entity or screen entry by id, a turf by its
cell), tg's name (`used_name`, at most `MaxContextNameBytes`; null for no screentip: screentips off or `NO_SCREENTIPS_1`) and up
to `MaxContextHints` (10) entries in tg's order of lines (LMB, RMB, Ctrl-LMB, Ctrl-RMB, Alt-LMB, Alt-RMB, Shift-LMB,
Ctrl-Shift-LMB; `ScreentipSeam.ContextKeys`), a button of 1 left, 2 right (3 middle is allowed by the codec, tg has none) with
the held modifiers, and tg's label as plain text (a stray tag is removed, at most `MaxContextLabelBytes`, never null). Private to
the hovering session: tg's `on_mouse_enter` (once per tick, `SSmouse_entered`) calls `ScreentipSeam.Publish`, the host's sink
(`ScreentipRouter`, installed when the first player joins) encodes the message into the viewer's session and `SendChat` sends it
after the tick (the newest 8 are kept for a slow link). Cap `MaxContextHintsMessageBytes` (959).

Out of reach (DV691, the designer 2026-11-26; still protocol 100, no new field): a message for a world target (an entity, a turf, a ceiling
cell) with a **zero-length name** (not null) and no hints means "out of reach": the player's mob cannot reach the target by tg's test of a
click (`IsReachableBy` with the held item's reach; the ceiling's own range for a ceiling cell), and the client shows no prompt at all, not even
its own name for the thing; nor does it show anything for a world target before the answer for that target has arrived. A null name keeps
its meaning (tg shows no screentip, as for every floor) and the client then names the thing itself. Every living mob acting from where it
stands (on a turf, or in a `loc` that allows the click) is gated, whatever its state of the moment (`ServerHost.ReachGated`); a ghost, the
AI, a cyborg with no module out, the alien hunter while it leaps on click, a mob inside a mech or a locker, and screen targets always get
tg's screentip for things and turfs. A ceiling cell's answer is the project's own, not tg's screentip: its name is zero-length out of the
ceiling's range (`CeilingSystem.InReach`) for every living mob whatever it holds, the AI and a mob inside a mech or a locker included. The client repeats the hover of a world thing kept under the crosshair or the pointer every
0.5 seconds (`PlaySession.RepeatHoverInterval`), inside the hover bucket, so the answer follows the player walking into or out of reach.
Every hover, a repeat included, runs tg's hover again; so the client sends no repeat while the pet-commands keybinding is held (tg's pets
listen to `MouseEntered` then and would open their command radial again). An answer counts only for the look it arrived in: a hover of
another thing, or none, drops it on the client. Known limit: an answer still in flight from an earlier look at the same thing counts when it
arrives during the current look, since the message carries nothing to tell the two apart (that would need a new field).

### 14.3 The private block

- **Screen entries' group** (`ScreenEntry.Group`): a trailing `u8` in every added and changed entry: tg's HUD group (`HudGroup`: 0
  none, 1 static, 2 toggleable inventory, 3 info, 4 screen overlays, 5 hotkeys, 6 storage; `code/__DEFINES/hud.dm`), 0 to 6 (a
  larger byte is `BadValue`). It is the first group of the client mob's `hud_used.screen_groups` that lists the screen object; an
  open storage's cells, closer and items are in group 6 because tg's `show_contents` lists them there. A change of group alone is
  a changed entry (the replicator's shadow holds it).
- **Private images** (`OwnBlock.ImagesAdded`, `ImagesChanged`, `ImagesRemoved`; `ImageEntry(id, appearance, attachedTo)`): flag
  bit 3 of the private block, after the screen lists: `[count added] { [varint id diff] [tableref appearance] [varuint attachedTo] }*`,
  the same for changed, `[count removed] { [varint id diff] }*`; ids strictly increasing in each list, at most `MaxPrivateImages`
  (64) per list and in all, an appearance that names an entry (a private or a public table index, never none), `attachedTo` the
  world id of an entity or 0 for "nothing the client knows" (then the client draws it under the crosshair). The flag is set exactly
  when a list is not empty. A Snapshot lists every image as added. Images are tg's `client.images` of the own client (D128: the
  `do_after` progress bar): `/image` has no world id, so the replicator numbers a session's images in the order it first sees them
  and never reuses a number in the session.
- The replica checks (a desync): an image added twice, changed or removed without existing, an appearance past its table, more
  than 64 in all, a Snapshot that lists images as changed or removed, a screen group past 6.

### 14.4 The entity flags (`NetField.Flags`, bit 15)

One byte (`EntityFlags`): bit 0 `Thrown` (tg's `throwing` datum is set); bit 1 `Lunge` with bits 4-7 its BYOND direction (tg's
`do_attack_animation` ran this tick: the client plays tg's lunge when the bit rises; set for one tick, the owner's play test
2026-10-09); bit 2 `Hit` (tg showed its attack visual on the entity this tick: the client draws a burst of pixel particles
when the bit rises; one tick); version 56: bit 3 `Pushable` (a dense obj tg's PushAM moves when a walking mob bumps it: not
anchored, `move_resist` within `MOVE_FORCE_DEFAULT`; held while it holds, the client predicts the push). In the entity record: bit 3
of the record's flags byte, and the byte after the light (never 0; before version 56 a decoder refused bit 3). In the change record: the 13th bit of the wire
mask (bit 12, after Light), the value one `u8` which may be 0 (the throw ended). `NetField.ChangeableV3` and `AllV3` are gone:
`Changeable` and `All` are the version-4 sets. A flags-only change is no movement (the replica pushes no interpolation sample for
it).

### 14.5 Tests and measurements (P6)

Tests (`tests/Anomaly.Server.Tests/M4aServer/P6/` and the moved M3 ones): `CodecV4Tests` (every layout above byte for byte, the
refusals of the encoder and of crafted bytes), the fuzz corpus of `CodecFuzzTests` (the new messages and fields: random bytes,
truncations, bit flips, insertions, forged counts; accepted messages re-encode to the same bytes), `ReplicaWorldV4Tests`,
`ReplicatorV4Tests` (the group byte, the images, the Thrown poll, the "no other session's screen or image bytes" secret test for a
storage's contents and a progress bar), `HostV4Tests` (forged entity ids, turfs outside the map, on another level or out of
range, screen ids the session does not own, keybinding codes, malformed combinations, the buckets, one hover per tick, the
screentip sink, `click_ms`), `ReplayV4Tests` (recorded v4 commands replay to the same hash and the same calls), `ClientSeamV4Tests`,
`BotsV4Tests` (the fiddle and the three abuse behaviours against the whole server) and `MeasurementsV4Tests`.

**Measured** (Debug, the generated 48 x 48 room, one human; `MeasurementsV4Tests` prints them):

| What | Bytes |
|---|---|
| a quiet Delta (one session) | 13 |
| the Delta that opens a 21-slot backpack (46 screen entries: 21 cells, the closer, 3 edge pieces, 21 items; 14 distinct looks) | 1,270 |
| the next Delta | 13 |
| a rejoin Snapshot with that backpack open | 1,997 |
| the Delta that adds a progress bar image | 142 |
| a Delta that changes its state to a state not seen before (the string and the appearance once) | 46 to 48 |
| a Delta that changes it to a state seen before | 20 (7 over idle) |
| a Delta that flags one entity as thrown | 18 (5 over idle) |
| 20 fiddling bots, one minute, to each bot / from each bot | about 3.4 KB/s / 0.5 KB/s |
| the server's part of `click_ms` there (arrival to the end of its tick's Delta sends) | mean 1.8 ms over 956 clicks |

I1 measures the real Meta numbers and the whole `click_ms` (the client's part is the Godot client's).

## 15. Version 5 (M4b slice 3: the gas visuals and the dev gas probe)

Version 5 adds, at the ends of version 4's layouts, the gas a cell shows (tg's gas overlays, D18) and a dev tool, the gas
probe of the client's dev menu. The types are `src/Protocol/MessagesV5.cs` (and `ActionKind.DevGasProbe`, `MessageId.DevGasReport`,
the version-5 caps of `Limits.cs`); the codec is `Codec.V5.cs` and the version-5 parts of `Codec.cs` and `Codec.V2.cs`.

### 15.1 The gas a cell shows (MapGrid, Delta)

```
MapGrid, after the version-3 fields:
  [count <= 32] { [str(32) gas kind] }*          tg's gas overlay icon states; GasLevel.Kind 1 is the first
  [count] { [cell] [gases, at least one] }*      every cell that shows gas at the join
Delta body, after the version-3 part, when the parts byte has bit 64:
  [count >= 1] { [cell] [gases, maybe none] }*   cells whose gas changed (none: the cell shows nothing now)
[gases] = [count <= 16] { [u8 kind 1..kinds] [u8 level 1..80] }*
```

A cell's gases are the overlays tg's `update_visuals` put into the open turf's `vis_contents` (`GAS_OVERLAYS`: a gas above its
`moles_visible` shows its `gas_overlay` icon state at the level `ceil(moles / MOLES_GAS_VISIBLE_STEP)`, at most
`TOTAL_VISIBLE_STATES` 80, whose alpha is `log4((level + 32) / 28) * 255`, `GasVisual.Alpha`), in tg's order. The kinds table is
fixed for the run (`CellState.GasKinds`: every `/datum/gas` with an overlay, in type order). The replicator reads a cell's
overlays with `CellState.GasOf` (the level back from the overlay's alpha) whenever the cell is marked (tg changing the turf's
`vis_contents` is a hint) or swept; a change counts against `MaxCellChangesPerDelta` like the other cell changes. The replica
keeps them (`ReplicaWorld.GasAt`, `GasCells`, `GasKinds`) and raises `GasChanged` after a Delta (not `CellsChanged`: no mesh
depends on gas).

### 15.2 The dev gas probe (Action kind 8, client to server, Control) and DevGasReport (`MessageId.DevGasReport` = 12)

```
Action:        [u8 id = 10] [varuint frameSeq] [u8 kind = 8] [target: a turf, kind 2]
DevGasReport:  [u8 id = 12] [u8 x0 1..255] [u8 y0 1..255] [varuint z 1..80] [u8 width 1..15] [u8 height 1..15]
               [count <= 32] { [str(48) id] [str(48) name] [u32 rgb 0..0xFFFFFF] }*
               width x height cells, row by row from the south-west, x fastest:
                 [u8 kind: 0 none, 1 closed, 2 open, 3 space] and, for open and space:
                 [f32 kPa] [f32 K] [count <= 32] { [u8 gas] [f32 moles] }*
```

The client's dev menu (apps.md 2.17) sends a probe twice a second while its gas overlay is on. The host takes it only when
`DEV_COMMANDS` is on (otherwise one `ServerText` says so and later probes are dropped silently), through its own bucket
(`ActionLimits.DevProbe`, 4 per second), queues it like any action and answers in the command phase: the (2r + 1) x (2r + 1)
cells around the probe (r = `DevGasProbeRadius` 7, clipped to the map), each with tg's `return_pressure()`, `temperature` and
the moles of every gas present, and the table of the gases named (tg's `id`, `name`, `primary_color`). It reads, changes nothing
and is not sent in a replay. Cap `MaxDevGasReportBytes` (41,781; about 2 KB for a room of air).

### 15.3 Tests

`tests/Anomaly.Server.Tests/M4b/ProtocolV5Tests.cs` (the layouts, a 48 x 48 map full of gas, the refusals, the replica's gas state),
`GasVisualsServerTests.cs` (a turf's overlays reach the clients and a late joiner, the probe answered and refused) and the moved
version checks of `CodecTests` and `CodecV2Tests`.

## 16. Version 6 (D141: the airflow streams)

Version 6 adds one message; nothing else changes. Types: `src/Protocol/MessagesV6.cs` (`Airflow`, `AirflowDir`, `AirflowRun`,
`AirflowUpdate`), codec `Codec.V6.cs`, caps in `Limits.cs` (`MaxAirflowRuns` 2048, `MaxAirflowRunCells` 255, `MaxAirflowCells`
4096, `MaxAirflowBytes` 14,339).

```
Airflow (server to client, State channel): [u8 id = 13] [count <= 2048 runs]
  { [varuint z 1..80] [u8 y 1..255] [u8 x0 1..255] [count 1..255] { [u8 cell] }* }*
[cell] = 0 (no flow) | 0xFF (unchanged) | dir << 5 | level     dir 1..6 north, south, east, west, up, down; level 1..31
```

A cell's flow is tg's `pressure_difference` (kPa) toward tg's `pressure_direction` in the last SSair cycle, as a level on a log
scale: level 1 is 0.5 kPa, 3.5 levels a doubling, level 31 about 190 kPa (`Airflow.LevelOfKpa`, `KpaOf`); below 0.5 kPa is no flow.
A run covers consecutive cells of one row, x0 + count - 1 <= 255; inside it an unchanged byte skips a cell (the sender lets up to
3 of them join two changes into one run, cheaper than a run's header). The message goes after the tick's Delta and after the
MapGrid of a session that joined in the tick, so its cells are always on the client's map; the client starts with no flow and a
MapGrid drops it all. A run outside the map is a protocol error (`ReplicaWorld.Apply` throws, the client disconnects). The replica
keeps the flow (`AirflowAt`, `AirflowCells`, `AirflowCount`) and raises `AirflowChanged` with the cells each update touched.

Sizes: a run costs 4 or 5 bytes and a byte a cell. A breach of a 14 x 14 room sends about 100 to 200 cells once (a few hundred
bytes), then only the cells whose level moved by two steps or whose direction changed, twice a second at most (an SSair cycle);
nothing while no air moves within the player's square.

### 16.0 Version 7: the floor devices

After the runs, the Airflow message lists the floor devices whose air movement changed: `[count <= 512] { [varuint z]
[u8 y] [u8 x] [u8 vent] }*`, the byte 0 (stopped) or `kind << 5 | level`, kind 1 out of the device into the room, 2 from the
room into it, the level on the same scale (the pressure the gas moved in one SSair cycle makes in the cell's air; several
devices on one cell add up, signed). tg's vent pumps (releasing or siphoning), scrubbers (scrubbing or siphoning), air
injectors and passive vents report what they move (`AirflowSeam.Vent`, before the gas is merged). The replica keeps them
(`AirflowVentAt`, `AirflowVentCells`) and `AirflowChanged` names their cells too. Cap `MaxAirflowBytes` 16,389 with the vents.

### 16.1 Tests

`tests/Anomaly.Server.Tests/M4b/AirflowTests.cs` (the levels, the round trip and the refusals, the replica, the feed's interest,
hysteresis, clears, runs with gaps and the cap, a breach on a live host, the radius 0), the core seam in
`tests/Anomaly.Core.Tests/Tg/M4b/AirflowSeamTests.cs`, and the moved version checks of `ProjectsLoadTests`, `WireTests`, `CodecTests`.

## 17. Version 8 (D142: a mechanic ports with its visuals)

### 17.1 The entity's flick (`NetField.Flick`, bit 16)

BYOND's `flick(state, atom)` plays an icon state's frames once over the atom and then shows its appearance again (tg: a vent's
`vent_out-shutdown` when it loses its pipe, `vent_pump.dm:206`). The field is the last flick: the appearance it plays in the public
table (the atom's own appearance at the flick with the flicked icon state, or icon, in its place; never 0) and the ticks since it
started (`varuint`, at most the message's tick, like the last move tick). In the change record: the 14th bit of the wire mask (bit
13, after Flags); in the entity record: bit 4 of the record's flags byte and the two values after the flags byte (a decoder refuses
the other bits, an appearance 0 and a start before tick 0). A new flick is a new tick; a flick-only change is no movement. The
client plays the flicked state once from the tick it started in (a flick older than its own length is not played) and then the
entity's own picture, or animation, again.

### 17.2 The client's world animations

Not a wire change, the same D142 step: a static prop's picture (a billboard, an item, a wall or floor decal; not the shaped pieces)
animates by BYOND's rules (`AnimationTimeline`: the icon states' delays, loop counts and rewind; several animated states share their
common loop up to 10 s; at most 32 pictures), each picture stored once in the prop atlas, and the instance steps through them
(`MapRenderer.StepAnimations`). The moving entities, the shaped pieces and the turfs followed in M4v slice 2 (section 17.4). An animation restarts when the appearance changes, as BYOND's does with a new icon state. A security
camera stands out of its wall at a right angle, still, and plays its side view's frames (DV35).

The icon converter (`tools/Anomaly.Tools.Icons`) reads a .dmi's frames in BYOND's order: each animation frame holds its directions
(direction d of frame f is the state's frame f * dirs + d, as OpenDream's `DMIParser` reads it). It read them direction-major before
2026-10-14, which drew every animated state with several directions from the wrong frames (a camera's pan from its other directions);
`godot/assets/tg` is converted again after the fix.

### 17.3 Tests

`tests/Anomaly.Server.Tests/Integration/FlickReplicationTests.cs` (a vent with no pipe: its client sees `vent_out-shutdown` over
`vent_off`), `tests/Anomaly.Server.Tests/M4b/MetaAirTests.cs` (`A_vent_whose_pipe_is_deleted_turns_off_and_shows_it`),
`tests/Anomaly.Server.Tests/Client/Render/AnimationTimelineTests.cs`, `tests/Anomaly.Server.Tests/Client/World/WallFinTests.cs` (the camera's
fin and tg's side pictures of it and their pan frames from the converted icons), `tests/Anomaly.Core.Tests/Tools/Icons/DmiReaderTests.cs` (the frame
order), and the moved version check of `ProjectsLoadTests`.

### 17.4 M4v slice 2 (D143): icon state animations and flicks on everything the client draws

Not a wire change. What the client animates by the icon states' frames (`AnimationTimeline`) and plays a flick on:

- **Moving entities** (`EntityLayer`: mobs, things that moved, a lying mob, a thrown or lunging picture): the entity's last flick plays
  over its picture from the tick it started in, at the layer's render tick (`FlickPlayback`: not before it, not past its length, the
  flicked states played once; the flicked appearance also picks the drawn direction), then its own picture again, whose icon states
  play from when its root icon or icon state last changed (not a turn or a new overlay; they ran on the client's clock since it started before, so a state with a loop count
  showed its last frame at once).
- **Shaped pieces** (`MapRenderer.BuildPiece`): a piece drawn with its own appearance plays that appearance's frames on its faces (the
  south square; every part of the piece shows the one picture, `ChunkBatch.SetPieceCustom`) and a flick, as a static prop does. The
  door panels' picture follows `DoorPictures`: closed or denying, the own appearance with its frames (an airlock's
  `lights_emergency` and `lights_reta` blink, 1.2 s each, for ever; `sparks_damaged`, `sparks_broken` and the emag's `sparks`; the deny's
  `lights_denied`, six frames of 0.1 s once: the icon has no `deny` state, the overlays draw the closed door); opening, every layer's
  first frame (the closed position with the green lights), closing, every layer's last (`PropAtlas.TryStill`): tg's opening and
  closing frames draw the slide itself (the airlock's frame, fill, panel and lights move with it; a blast door's slab loses four rows a
  frame), which the 3D panels and slab do by moving (`DoorAnimation`, kept, with its deny flash); a door that came to rest open keeps
  its panels' picture, and one first seen open shows its icon's `closed` state. A full firelock does not move in 3D, so its slab plays
  all of tg's frames: `door_opening` and `door_closing` (six of 0.2 s once), `door_deny` (three of 0.1 s) and the alarm lights
  (`firelock_alarm_type_*`, 0.9 s and three of 0.1 s, for ever). A border firelock plays its full counterpart state's frames
  (`PropAtlas.LibraryAnimation`; its alarm overlay is not on the counterpart's picture). Still, as tg draws them: tables, railings,
  flaps, grilles (a swatch), full and directional windows, a windoor's glass (its own states are a top-down band; its `spark` flick on
  emag is not drawn), and lamps (their reachable states are still; the `_flickering` overlay belongs to `flicker()`, not ported, M10+).
- **Turfs** (`TileKit`, `MapRenderer.StepTurfFrames`): an animated turf picture's frames are consecutive layers of its Texture2DArray
  page, and its tiles' instance data takes the layer of the frame the picture is at (`TurfFrames`; every tile of one picture in step, as
  BYOND plays an icon state by the client's clock); only the pictures whose frame changed are written, no chunk is rebuilt. On
  MetaStation: the recharge floors (`recharge_floor`, two frames of 0.6 s; (71,112), (72,139), (119,179), (119,182)), the water by the
  dorms (`riverwater_motion` and `deep_riverwater_motion`, eight frames played back and forth, 2.9 s; (146..147, 149..151)) and the
  asteroid's basalt (the 15% of tiles tg gives a glowing `basalt1`, `2`, `3`, `5` or `9`, four frames of 2 s; (222..223, 25..26)). Space
  (`space`) and the circuit floors (`bcircuit`, `gcircuit`, `rcircuit`; `rcircuitanim` only under a nuke threat) are still.

Gaps: the emissive layers (DV12) are not drawn (the airlock's `unres_*` floor lights are
not drawn at all, D174, DV93); the airlock's damage sparks need atom integrity (M5) and its emergency and RETA lights a way to set them (AI
control, M10+) before a player sees them; animate() chains on pieces, decals and
turfs stay out (section 18.3). Tests: `tests/Anomaly.Server.Tests/Client/Render/FlickPlaybackTests.cs` (the flick's age, the turf frames),
`tests/Anomaly.Server.Tests/Client/Render/PieceAnimationTests.cs` (the doors' and turfs' frames from the converted icons, the door
panels' rule) and `FlickReplicationTests.A_flick_on_a_walking_mob_reaches_another_client_while_it_moves`.

## 18. Version 9 (M4v slice 1, D143: BYOND's animate() reaches the clients)

### 18.1 The entity's animation (`NetField.Animation`, bit 17)

BYOND's `animate()` changes an atom's vars at once and the client shows the change over time (BYOND reference `/proc/animate`). The
field is the atom's running animations, `EntityAnimation`: 1 to 4 **tracks**, each the chain of one `animate(atom, ...)` call and the
calls without an object that followed it. In the change record it is the 15th bit of the wire mask (bit 14, after Flick); a value
of 0 tracks is "none" (the chain ended or was stopped). In the entity record it is bit 5 of the record's flags byte and the value
after the flick, never 0 tracks (a decoder refuses it). An animation-only change is no movement.

```
[animation] = [u8 tracks 0..4] { [track] }
[track]     = [u8 track flags: bit 0 ANIMATION_PARALLEL] [varuint ticks since its start] [varuint loop: 0 for ever, 1..65535]
              [vars: the start values] [u8 steps 1..36 (16 before version 32)] { [step] }
[step]      = [f32 time ds] [f32 delay ds] [u8 easing] [u8 step flags: bit 0 ANIMATION_RELATIVE, bit 1 ANIMATION_LINEAR_TRANSFORM] [vars]
[vars]      = [u8 mask: bit 0 pixel_x, 1 pixel_y, 2 pixel_z, 3 pixel_w, 4 alpha, 5 color, 6 transform]
              then the set ones in that order: five f32, the colour as a u32 0xRRGGBBAA, the transform as six f32 (a to f)
```

The start (ticks since, like a move tick) is at most the message's tick. The easing is BYOND's byte: a curve (`LINEAR_EASING` 0,
`SINE` 1, `CIRCULAR` 2, `CUBIC` 3, `BOUNCE` 4, `ELASTIC` 5, `BACK` 6, `QUAD` 7, `JUMP` 8) with `EASE_IN` (64) and `EASE_OUT` (128);
bits 4 and 5 must be 0. A step's numbers are as tg gave them: an end value, or with `ANIMATION_RELATIVE` what it adds (a colour or a
transform multiplies). The track's start values are the vars' values before the call, for every var a step names. Undefined flag
bits, a mask bit 7, a non-finite number, one above 10^6, a time or delay outside 0 to 36000 ds and the counts outside their ranges
are `BadValue`.

What is not sent: a colour matrix (a list `color`: the step does not name the var; an appearance with one counts as white for the
playback), the vars the 3D client does not show (`icon_state`, `dir`, `layer`, `maptext*`, `glide_size`, `luminosity`, ...), `tag`
and `command`, and every animation of a turf, an `image` or a client (a screen object's chains travel in its screen entry since version 11, section 20; a flick visual of `flick_overlay_view` is a
movable: its chain is sent, section 19).

**The lunge.** tg's `do_attack_animation` (a living mob's chain naming `pixel_x` and `pixel_y`, time at most 1) stays the one-tick
`EntityFlags.Lunge` of section 14.4, drawn by the client's own lunge; its chain is not sent again. An attack with no direction
(the same tile) and the attack chain of anything but a living mob are sent as an ordinary animation (the 15-degree turn and back).

**The lying turn.** tg's `update_transform` turns a lying mob's transform with `dir` in one call; the client draws lying by the
posture (section 5.1), so a living mob's step that names `transform` with `dir` does not name the transform. As BYOND does, the
call still stops the mob's other animations (it is not parallel).

### 18.2 The server (`AnimationRecorder`)

`Dm.animate` reports each step to `IAppearanceSink.AnimateStep` with its subject and the named vars' values before it applied the
step (presentation-seam.md section 5). The replicator's recorder keeps, per movable id, the tracks: a call with an object starts a
track (without `ANIMATION_PARALLEL` it replaces the running ones; `ANIMATION_CONTINUE` adds to the last); a call without one adds a
step to the track of the last call with an object. A track starts in the tick the replicator next reads the entity (the tick of the
call). The read sends the field when a step was recorded since, and when the animation sent has ended (a poll at its end tick; an
animation looping for ever stays until it is replaced). A chain recorded for an id that is no entity after the tick's reads (an item
in a hand) is forgotten, but a dropped item's chain is read with its spawn, and a live screen object's is kept for the private blocks (version 11, section 20). The limits: the oldest track goes past 4;
a chain past 16 steps is not sent (its end shows at once; counted); loop above 65535 is 65535, a negative loop is for ever.

BYOND starts an animation that interrupts another from where it is drawn (unless `ANIMATION_END_NOW`); the server sends the vars'
values, so every chain starts as with `ANIMATION_END_NOW`: an interrupted float, for instance, jumps back by up to 2 pixels.

### 18.3 The client (`AnimationPlayback`, `EntityLayer`, `MapRenderer`)

`AnimationPlayback` (godot/scripts/Shared/Icons, engine-free) plays a track at the render tick: each step holds its delay, then moves
over its time by its easing to its value; the track plays `loop` times, a later round from where the last ended (a relative chain
goes on; a var a step sets outright ends each round at the same value). Easing: a curve's ease-in shape (Penner's, as OpenDream's);
`EASE_IN` plays it, `EASE_OUT` its mirror, neither or both the two halves (BOUNCE and ELASTIC default to `EASE_OUT`), JUMP holds and
jumps at the end (at the start with `EASE_IN` alone). A transform is interpolated as rotation (the shorter way), scale, shear and
move, unless `ANIMATION_LINEAR_TRANSFORM`. The tracks compose as BYOND's parallel animations: the appearance (which holds every chain's
end) plus each track's difference from its own end (a transform's numbers added: an approximation when two tracks turn one atom).

The look is drawn over the composed picture: `pixel_x` and `pixel_y` move it on the ground (east, north, as the placement reads the
appearance's own, M3 R2), `pixel_z` up (up the screen is up in the world for a standing picture), `pixel_w` to the side in its plane;
`color` and `alpha` scale its colour and alpha (from the colour it was composed with: a channel composed at 0, or a picture composed
transparent, cannot be brought back); the transform applies after the picture's own, about the icon's centre (an item's, a lying
mob's: its picture's middle). An alpha below 1 is a screen-door dither. Where: the moving entities' billboards (`EntityLayer`, the
`picture_billboard_animate` material; the lunge and the throw keep theirs while they play) and the static billboards, items and
resting machines (`MapRenderer.StepLooks`: the quad's instance moves, and its basis, which a billboard does not use, carries the rest
to `prop_atlas.gdshader`). M4v slice 4: the wall decals (and a camera's fin) and the floor decals carry the look the same way (their placement's
yaw moved to the instance colour's alpha; the transform turns the picture in its own plane; on the floor every move is east and north, on a
wall along it and up): a hot heat-exchanging pipe's glow fades in over 2 s. Not animated: the shaped pieces (doors, windows, tables, lamps:
no ported code animates one; their parts are boxes that a 2D transform does not fit, only a colour or alpha would carry) and the turfs.

### 18.4 Tests

`tests/Anomaly.Server.Tests/Protocol/AnimationCodecTests.cs` (round trips, the size of a change, the encoder's and the decoder's
refusals), the animation rows of `CodecV2Tests` and `WhitelistTests`, `tests/Anomaly.Server.Tests/Integration/AnimateReplicationTests.cs`
(a dropped item's glide and a staggered mob's sway on the replica; the field goes back to none), `tests/Anomaly.Server.Tests/Client/Render/AnimationPlaybackTests.cs`,
`tests/Anomaly.Core.Tests/Tg/M2Integration/ShimGapTests.AnimateStepGetsTheSubjectAndTheValuesBeforeEachStep`, and the version of `ProjectsLoadTests`.

## 19. Version 10 (M4v slice 3, D143: tg's short-lived overlay images reach the clients)

### 19.1 The visual's host (`NetField.VisHost`, bit 18)

tg shows a brief image over an atom with `/atom/proc/flick_overlay_view` (`__HELPERS/game.dm:124`): a new
`/atom/movable/flick_visual` takes the image's appearance (its icon, state, layer, plane, pixel offsets, transform, flags), joins
the atom's `vis_contents` and is deleted after the duration (`QDEL_IN_CLIENT_TIME`); the callers often `animate()` it. BYOND draws a
`vis_contents` object at its holder, for every viewer of the holder. Such a visual is an **entity** of the wire: its record and changes
carry the ordinary fields (its Type is `/atom/movable/flick_visual`; X, Y, Z are its host's turf; Appearance, Flick and Animation its own)
and the new field VisHost: the id of the entity it is drawn on, or 0 for the turf at its X, Y, Z.

```
entity record: flags bit 6 (64) set: [varuint host]   (after the animation; a decoder refuses bit 7)
change record: the 16th bit of the wire mask (bit 15, after Animation): [varuint host]
```

The host is never the visual itself (`BadValue`), and an entity that is no visual carries no host (the encoder refuses one). A change
never turns a visual into an ordinary entity or back (the server despawns it and spawns it again); a visual-only field change is no
movement. The visual leaves (a Despawn) when tg deletes it, when its host stops holding it or leaves the entity set (an item picked up).
Who sees it: every client that receives the host (the visuals follow the entity set, as BYOND's `vis_contents` follow their holder).

`flick_overlay_global` and `/atom/flick_overlay` (`game.dm:101`, `:109`) add an `/image` to the given clients' `client.images` and
take it out after the duration (a client-time timer): that reuses the private images of section 14 (`OwnBlock.ImagesAdded`, `ImagesRemoved`),
which reach only the session whose `client.images` holds them; an image's `animate()` is still not sent (section 18.1). No ported code calls
them yet.

### 19.2 The server

`ReplicatedFields.IsVisual`: a registered flick visual in nullspace, invisibility at most 25, held by the `vis_contents` of a turf in the map
range or of an entity. The replicator learns the hosts from the `VisContents` hint of a movable or a turf (the holder is read after the tick),
polls every known visual each tick and despawns it when it no longer qualifies. Its appearance is interned from its own vars, and its chain is
recorded under its own id (section 18.2), so the pickup's two steps and the attack images' parallel tracks are sent as for any entity. The
host's appearance skips flick visuals when it flattens `vis_contents` (a screen object still draws its own).
M7x: a gun's firing effect (`temp_visual/dir_setting/firing_effect`, which joins the shooter's `vis_contents` in its `Initialize` with the
shooter as its `loc`) is such a visual too: the replicator accepts it on a host that is also its `loc`, and the host's appearance skips it, so
every client that sees the shooter gets the muzzle flash as an entity drawn on him (`EntityLayer` flashes it, the own view model kicks).
M8x slice C (D210, no new version): tg's visual helpers (`/atom/movable/visual`, in nullspace: the cryo cell's `cryo_occupant`, cryo.dm:14)
are visuals as well, for as long as their holder keeps them: the cell's occupant carries the patient in its own appearance (his body is
flattened from its `vis_contents`), its alpha mask filter and its bob chain (`pixel_y` 22 to 24 and back, 2 s each way, looping, while
the cell is on); the cell's appearance skips it.
M8y slice C (no new version): a unique vis overlay (`SSvis_overlays.add_vis_overlay(..., unique = TRUE)`, an `/obj/effect/overlay/vis` in
nullspace whose `cache_expiration` is -1; `ReplicatedFields.IsHeldVisual`) is such a visual too: the stasis bed's mattress (`stasis_on`), whose
alpha tg animates from 0 to 255 and back over 5 s with CUBIC easing (`stasis.dm:99`), reaches the clients with its chain, and the bed's
appearance skips it. A shared vis overlay (one object in many holders' `vis_contents`: a holosign's picture) stays flattened into each holder.
M10x (no new version): tg's eyelid effects (`/obj/effect/abstract/eyelid_effect`, `_eyes.dm:72`) are such visuals too, though they sit in the
eye organ inside the human whose `vis_contents` hold them (`ReplicatedFields.IsInnerVisual`): each carries tg's looping blink chain (alpha 0,
then 255 for `BLINK_DURATION` every few seconds); the human's appearance skips them, and the client draws each into the human's picture at
its chain's alpha (section 19.3), not through tg's `render_source` overlays (DV207).

Translated with it (core): `add_image_to_client(s)`, `remove_image_from_client(s)`, `flick_overlay_global`, `/atom/flick_overlay`,
`/atom/flick_overlay_view` (and the `/area` override that shows nothing); the engine's `vis_locs` (a lazy per-world index:
`Builtin/atom.vis_locs.builtin.cs`; tg's `/atom/movable/Destroy` takes a deleted visual out of its holder with `vis_locs = null`) and
`appearance` var; an image's `transform` reads as a copy (the identity when it has none), and `image(atom)` copies the atom's `pixel_w`,
`pixel_z` and transform too. The callers: `do_pickup_animation` (`items.dm:1508`), `do_item_attack_animation`'s effect image (`:1592`),
`animate_attack` (`:1604`) and the button's success and error flashes (`buttons.dm:303`, `:307`). The attack visuals still report
`HitEffectSeam` (the owner's burst of particles, 2026-10-09) next to tg's image.

### 19.3 The client (`EntityLayer`, `VisualAnchor`)

A visual is not a static prop and gets no placeholder: `EntityLayer` keeps it apart (it is not picked: tg's `mouse_opacity` is
transparent) and places it each frame by `VisualAnchor` (godot/scripts/Shared/World, engine-free) at its host as drawn:

- a host the layer draws (a mob, a moving thing): a billboard at the host node's feet, 5 cm toward the viewer so that it draws over the host,
  at the host's scale (an item's `ItemScale`, D121, else the tile scale);
- a static prop of the map renderer (`MapRenderer.TryVisualAnchor`): on a wall decal (a button) the picture lies flat on the decal's face
  (`WallMounts.DecalPlacement`, one `DecalLift` further out; the shader's `fixed_plane`), its pixel offsets along the wall and up; any other
  prop: a billboard at its resting point;
- a turf (or a host the client does not draw): a billboard on the tile's floor at the item scale, as the picked item lay there.

Its picture is its appearance with its own dir, flick and icon state frames, as an entity's; its root pixel offsets move it on the ground
(east, north) and its `animate()` chain plays as section 18.3 reads it, so the pickup image travels a tile toward the picker (`pixel_x` or
`pixel_y` to plus or minus 32) while it turns, shrinks and fades. A visual's chain ends at alpha 0 (the appearance holds it), so its picture
is composed with the root opaque (`AppearanceCompositor.TryCompose(rootOpaque)`) and the chain's alpha (or, once it ended, the appearance's)
is drawn over it. It is removed when the server despawns it.

A visual helper (`/atom/movable/visual`, M8x) is not drawn on its own: BYOND draws a `vis_contents` object as part of its holder, sorted by
layer and plane among the holder's overlays (the cryo cell's cover is drawn over the patient), so `EntityLayer` composes the holder's
picture with the helper's appearance as one more overlay at the pixel offsets its chain gives it now, in whole pixels (`VisInsets`), and a
holder the map renderer batches is handed to the layer (`MapRenderer.HandOver`) to be drawn so; a click on that picture is the holder's (DV121).
A unique vis overlay (`/obj/effect/overlay/vis`, M8y) is drawn so as well, at the alpha its chain gives it now (`VisInsets.With`'s alpha: the
stasis bed's mattress fading in and out).

Gaps: a chain on a wall-face visual moves it with the billboard's axes (no ported caller animates one); the visual of a host that is not an
entity (an item in a hand, a screen object) is not drawn; BYOND's `RESET_TRANSFORM` and `PIXEL_SCALE` of tg's `APPEARANCE_UI` flags do not
change the 3D picture.

### 19.4 Tests

`tests/Anomaly.Server.Tests/Protocol/VisHostCodecTests.cs` (round trips, the replica, the refusals), the VisHost rows of `CodecV2Tests`, `Msg` and
`WhitelistTests`, `tests/Anomaly.Server.Tests/Integration/FlickVisualReplicationTests.cs` (a pickup's image on the floor tile with its two
steps reaches both clients and expires after 0.4 s; a pressed button's error flash on the button, not in its appearance, gone after 1 s),
`tests/Anomaly.Core.Tests/Tg/M4v/FlickOverlayTests.cs` (flick_overlay_view's visual, layer and deletion with vis_locs, the area, flick_overlay_global,
the image transform and appearance var, the pickup, the attack images, the button flashes), `tests/Anomaly.Server.Tests/Client/World/VisualAnchorTests.cs`,
`AppearanceCompositorTests.ARootComposedOpaqueDropsItsOwnAlphaForAVisualWhoseChainEndsTransparent`, and the version of `ProjectsLoadTests`.
M8x: `Client/World/VisInsetsTests.cs` and `Integration/CryoGameplayTests.cs` (the cell's occupant on both clients with the patient and
its chain, the cell's picture as the client composes it with the patient at both ends of the bob).

## 20. Version 11 (M4v slice 4, D143: a screen object's flick and animate() chains)

### 20.1 The screen entry's look

tg animates and flicks its screen objects as it does atoms: an alert slides in from 32 pixels to the right with an elastic easing
(`alert.dm:66`), a cleared fullscreen fades to alpha 0 before it is deleted (`fullscreen.dm:31`), the resist button flicks its `_on`
state when clicked (`screen_objects.dm:582`). A screen entry (section 6, `ScreenEntry`) carries them since version 11:
`FlickAppearance` and `FlickTick` (the last flick: the object's appearance with the flicked icon state in its place, a private or public
reference, and the tick it started in; none for no flick) and `Animation` (the running chains, an entity's `EntityAnimation` of section
18.1; null for none). Every added and changed entry writes them after its group byte:

```
[u8 look: bit 0 flick, bit 1 animation]     (other bits: BadValue)
when look != 0: [varuint R]                 the entry's reference tick: the latest start of the flick and the tracks
flick:          [varuint (appearance index << 1) | private] [varuint R - flick tick]     (never none; not before tick 0)
animation:      [animation] as section 18.1, with R in the message tick's place (never 0 tracks)
```

The reference tick stands for the message's tick because the private block is encoded on its own (`Codec.EncodeOwnBlock`, appended
to the shared body). A change entry is the whole entry, as before: the flick stays in it (the last one, as an entity's Flick field),
and an entry whose flick or animation changed is a changed entry. The replica refuses a flick appearance outside its table, as the
entry's own appearance.

### 20.2 The server

`Replicator.SessionState.Update` takes, for each screen object of the session's `client.screen`, its flick (`_flicks`, which the hint
sink fills for any movable) interned into the session's private table (`AppearanceTables.InternFlick`) and its chain
(`AnimationRecorder.TryTake`, which also ends a chain that ran out). A chain recorded for a live screen object is not forgotten after
the entities' reads (section 18.2); a deleted screen object's is forgotten when it leaves the screen. The first session whose screen
lists the object takes them (a screen object belongs to one client's HUD).

### 20.3 The client (`HudLayer`, `HudLook`)

`HudLayer` plays the entry's flick at the render tick (`FlickPlayback`, as `EntityLayer` does: the flicked states once, not past their
length, then the entry's own picture) and its chain (`HudLook.TryPlay`: `AnimationPlayback` against the appearance as it is, the shown
alpha taken over 255 (`composedAlpha`) because the picture is composed with the root opaque while a chain plays, as a visual's in section
19.3, so that a fade to 0 shows; before its start a chain shows its start values, after its end the appearance). `HudLook` (godot/scripts/Shared/Play,
engine-free) turns the look into the control's: the pixel offsets and the transform's translation move it (screen pixels at the HUD's
pixel scale, BYOND's up is the screen's up), the transform's linear part turns and scales it about the icon square's centre (a shear is
not drawn), and the colour and alpha are its modulate (times the fullscreen strength of D70 for a fullscreen).

### 20.4 Tests

`tests/Anomaly.Server.Tests/Protocol/ScreenLookCodecTests.cs` (round trips in a Delta and a Snapshot, the bytes of a flick, the refusals),
the updated bytes of `CodecV4Tests.ScreenEntriesCarryTheirHudGroup`, `tests/Anomaly.Server.Tests/Integration/ScreenLookReplicationTests.cs`
(a cleared fullscreen stays on its owner's screen with its chain for the fade and the HUD's alpha goes down over it, then it leaves; a clicked resist button's flick), `tests/Anomaly.Server.Tests/Client/Play/HudLookTests.cs`,
`tests/Anomaly.Core.Tests/Tg/M4v/AnimationSiteTests.cs` (the resist button's flick in the core), and the version of `ProjectsLoadTests`.

## 21. Version 12 (M4a: a screen object's clickable vis_contents children)

### 21.1 The rule

tg draws the item in a hand or an inventory slot through the slot object's `vis_contents`, not by putting the item in `client.screen`
(`code/_onclick/hud/inventory_slot.dm:55`, `update_slot_icon_contents`: `slot.vis_contents += slot_item`). BYOND makes every object in an
atom's `vis_contents` a click target of its own: a click whose pixel lands on an opaque pixel of a child (the topmost first, by plane and
layer, `mouse_opacity` respected) is the child's own `Click`, unless the child's `vis_flags` has `VIS_INHERIT_ID` ("the object will act as
if it is part of the parent, for purposes of mouse interaction", BYOND ref `/atom/var/vis_flags`; OpenDream's `DreamViewOverlay` keeps a
direct vis child's own click id unless `VIS_INHERIT_ID`). tg relies on it: a click on the toolbox in the other hand with the active hand
empty is the toolbox's `Click`, `ClickOn`, `attack_hand`, and its `datum/storage` opens (`on_attack`: `parent.loc == user`); with an item in
the active hand it is `attackby` (a tool goes into the held toolbox); a worn item's slot works the same way. Before version 12 the wire
flattened the children into the holder's appearance only, so the click reached the hand slot, whose `Click` swaps hands: a held toolbox
could never be opened.

### 21.2 The screen entry's children

A screen entry carries, since version 12, `VisChildren` (`ScreenChildren`, MessagesV12.cs): the screen object's clickable `vis_contents`
children, each its world id, its own appearance (the child drawn alone: the same table row the holder's appearance is flattened from for it,
a private or public reference) and `MouseOpaque` (BYOND's `mouse_opacity` 2, `MOUSE_OPACITY_OPAQUE`: its whole box takes the click), bottom
to top in BYOND's draw order. A child with `VIS_INHERIT_ID`, `VIS_HIDE` or `mouse_opacity` 0 is not listed (its pixels are the holder's, or
take no click: tg's health doll limbs and hunger bar are `VIS_INHERIT_ID`), nor one that shows nothing; only direct children count. It is the
third part of the look byte of section 20.1, after the flick and the animation:

```
[u8 look: bit 0 flick, bit 1 animation, bit 2 vis children]     (other bits: BadValue)
when bit 0 or 1: [varuint R] and the flick and the animation as in section 20.1 (a look of children alone writes no R)
bit 2:           [count 1..16] { [varuint id >= 1] [varuint (appearance index << 2) | (mouse opaque << 1) | private] }*   bottom to top; never none
```

A change of the children (an item put in or taken out of a hand, the held item's own look) is a changed entry, as the entry's other parts.
The replica refuses a child appearance outside its table, as the entry's own appearance.

### 21.3 The server

`Replicator.SessionState.Update` reads, for each screen object, its `vis_contents` every tick (`CollectVisChildren`: the clickable children
by `ClientSeam.IsClickableVisChild`, sorted by BYOND's draw order: plane, then layer, `VIS_INHERIT_PLANE`, `VIS_INHERIT_LAYER` and a
`FLOAT_LAYER` taking the holder's, a `VIS_UNDERLAY` child below the others of its plane and layer, equal ones in list order) and interns
each child alone (`AppearanceTables.InternVisChild`) when the holder's look is read: a hint for the holder or a child, the sweep, or a
change of the children's ids, which also re-reads the holder's flattened picture.

The click resolution (`ServerHost.TryResolveTarget`, interaction.md section 2) takes a `Screen` target that is in `client.screen` or a
listed child of an object there (`ClientSeam.ScreenHolderOf`, through the child's `vis_locs`); a child of another session's screen object,
or one that is not clickable, is refused and counted as before (`Session.ClicksRejected`). BYOND's params for it carry the holder's
`screen_loc` as `screen-loc` (BYOND's is where the mouse was, over the holder; the item's own `screen_loc` is a leftover of a storage
window), and `ClientSeam.Click` and `Hover` give it the location of a HUD click (null). tg's screentip router names such a target a `Screen`
one too.

### 21.4 The client (`HudPicker`, `HudLayer`, `ContainerWindow`, the FakeServer)

`HudPicker.PickScreen` tries, within each drawn entry under the point (topmost entry first), the entry's children topmost first, each
against its own composed picture (`HudChildDrawn`, `HudPictures.ChildrenOf`: placed in the holder's icon square, moved by the holder's own
pixel offset as BYOND draws a child): a pixel with any alpha (BYOND's opacity test) or, for `MouseOpaque`, the picture's whole box. A hit is a
`Screen` click on the child's id, with tg's `icon-x`/`icon-y` within the child's icon; else the entry's own test as before (the
picture's alpha, or the whole cell of a native window). `HudLayer` and the native windows (`ContainerWindow`: the inventory panel's worn
items) give the children. The FakeServer lists the held and worn items as their slot entries' children and answers a screen click on
one as tg's item `Click` does (`FakeServerOptions.Inventory`). The holder's transform and colour are not applied to its children's pictures
for the pick (no tg slot turns or scales).

### 21.5 Tests

`tests/Anomaly.Server.Tests/Protocol/ScreenChildrenCodecTests.cs` (round trips in a Delta and a Snapshot beside a flick and an animation, the
bytes of children alone, the refusals, the replica's table check, a version-11 client refused), the fuzz corpus's children
(`CodecFuzzTests`), `tests/Anomaly.Server.Tests/Integration/HandVisChildTests.cs` (through the real server: the hand entry lists the held
toolbox with the row its picture is flattened from, a screen click on it with the other, empty hand opens its storage and the welder shows
in the storage window; the entry follows a welder lit and dropped; a child of another player's hand is refused for a click and a hover, the
owner's own hover is applied), `Client/Interaction/ModelTests` (the pick: an opaque pixel of the item is the item, a clear one the slot,
`mouse_opacity` 2, the holder's offset, a window cell), `Client/Interaction/FakeInventoryTests` (the fake's hand child and its click), and
the version of `ProjectsLoadTests`.

## 22. Version 13 (M4b audio: tg's sounds reach the clients)

Version 13 adds one message; nothing else changes. Types: `src/Protocol/MessagesV13.cs` (`Sounds`, `SoundEvent`, `SoundKind`), codec
`Codec.V13.cs`, caps in `Limits.cs` (`MaxSoundEvents` 64, `MaxSoundPaths` 32, `MaxSoundPathBytes` 256, `MaxSoundTableEntries` 65536,
`MaxSoundChannel` 1024, `MaxSoundVolume` 200, `MaxSoundEnvironment` 25, `MaxSoundFrequency` 10^6, `MaxSoundOffset` 36000, `MaxSoundsBytes`
9,992), the replica's part in `ReplicaWorld.V13.cs`, the server's in `src/Server/Replication/SoundFeed.cs` and `ServerHost.Sounds.cs`.

### 22.1 The message

```
Sounds (server to client, State channel): [u8 id = 14]
  [count <= 32 new paths] [varuint first: 0 with no new paths, else the table index of the first] { str(1..256) }*
  [count <= 64 events] { event }*
event = [u8 head]  bits 0-1 kind (0 play, 1 update, 2 stop; 3 BadValue), bit 2 repeat, bit 3 mute, bit 4 positional,
                   bit 5 frequency, bit 6 offset, bit 7 zero (a stop sets none of bits 2-7)
        [u16 channel 0..1024]                     0: a play on any free channel (BYOND's channel 0) or a stop of every channel
        play:        [varuint path index 1..65536]
        play/update: [u8 volume, half units 0..200] [u8 muffle 0..255] [u8 occlusion 0..255] [u8 environment + 1, 0 none .. 26]
                     if positional: [u8 x 1..255] [u8 y 1..255] [varuint z 1..80]
                     if frequency:  [f32, non-zero, |f| <= 10^6]       if offset: [f32, 0 < offset <= 36000, tg's value]
```

tg sends a `/sound` to one listener with `SEND_SOUND` (BYOND `mob << sound`, `Dm.output_to`, presentation-seam.md section 4); each one the
session's mob or client got in the tick is one event, in tg's order. The kind: tg's `status & SOUND_UPDATE` is an update (the channel's
sound takes the new volume, position, mute and environment without restarting; tg's `set_sound_channel_volume`, a token moving out of
range), else a null file is a stop (`stop_sound_channel`; channel 0 stops every channel), else a play. The volume is tg's final one
(after the falloff, the pressure factor and the preferences), in half units: 13.890 is 28. The environment is tg's (the listener's area's
`sound_environment`, sound.dm:157-165), none for a sound tg wrote none for or whose echo tg silenced (`use_reverb` FALSE: the echo's room
level -10000, sound.dm:166-169; D296). The frequency is tg's (BYOND reads a magnitude
up to 100 as a speed multiplier, more as a rate in Hz; `get_rand_frequency` gives 32000 to 55000); the offset is a token's place in its
loop. Positional: tg wrote a non-zero `x`, `y` or `z`; the source tile is the listener's turf moved by tg's `x` (east), `z` (north) and
`y / 5` (levels; tg writes the level difference times 5), rounded and kept inside the map. A sound on the listener's own tile has no tile
and plays centred, as BYOND plays x = y = z = 0.

The muffle and the occlusion are the port's (D43), presentation only:
- **muffle** (DEVIATION DV37): 1 minus tg's pressure factor of `playsound_local` (sound.dm:131-141) without its "touching the source"
  floor of 0.15: the lower pressure of the two tiles, 1 atmosphere or more is 0, 10 kPa or less (a vacuum) is 255. Only for a sound tg
  played from a turf (it wrote the area's environment or the echo list); a closed turf's tile does not count. The client's low-pass.
- **occlusion** (DEVIATION DV39): the station graph between the two tiles, LINDA's `atmos_adjacent_turfs` (walls, closed doors, windows
  and airtight things cut it, open doors join it). A search from the listener's tile (once a tick per session, grown only as far as the
  tick's sounds need) along the graph's cardinal and vertical links (1 a step) and diagonals (1.414) where both cardinal ways round the
  corner are open, to a path of 32 tiles: a path at most 2 tiles longer than the straight line is 0 (an open line; a diagonal across an
  open room is one); a longer one is (path - straight) / 8 up to 0.5, as a byte up to 128 (heard around corners, duller); none is 255
  (sealed off). A source on an airtight tile (a closed door, a wall) is reached through the tiles beside it, as the sound of the door's
  face. A sound on the listener's own tile or with no tile is 0. The client applies volume x (1 - 0.65 x occlusion) and a low-pass when
  max(muffle, occlusion) reaches 0.5 (22.3; the numbers are tunable until the owner's listening test).

The paths: a play names its file (tg's resource path, `sound/machines/airlock/airlock.ogg`, the key of the client's sound index) by its
index in the session's path table, which only grows; a message adds new paths at its end (`first` = the table's size + 1). The table
starts empty at every join (the server's restarts when it sends the join, the replica's when it applies a MapGrid). Private per session
(D40): the shared Delta never carries a sound, and another session's events never appear in a session's bytes. Not replica state: a join
gets nothing old (a running token re-adds itself for a new player, tg's `sound_token.player_login`, slice 5).

The message goes after the tick's Delta and Airflow, and after the MapGrid of a session that joined in the tick (its join tick's sounds
come in it), only when the session heard something. The replica refuses (a protocol error, the client disconnects) a Sounds before any
MapGrid, new paths that do not continue the table, a play whose index is past the table, and a source tile outside the map; it keeps no
sound state and raises `SoundsReceived` with the events, a play's `File` resolved.

### 22.2 The server (`SoundFeed`, `ServerHost.SendSounds`)

`SoundFeed : ISoundSink` is the world's sound sink when the host has a transport (not in a replay, not for `--empty`). At the call it
copies the datum's fields (tg shares one `/sound` across the listeners of a `playsound` and rewrites its volume and position for each; a
token reuses its master sound), maps the target to a session as `ChatRouter` does (a client, a mob with a client, a list of those, the
world), computes the muffle and the occlusion, and records the copy in the session's `SoundView`. A play on channel 0 stays on channel 0;
an update on channel 0 names no sound and is not sent. It only reads the world (turfs, their air and adjacency lists); a run with and
without it keeps the same state hash.

After the tick (`ServerHost.SendSounds`, after `SendAirflow`), each joined session's sounds become its message (`SoundFeed.Build`); a session
that is not joined loses them. The budget per session per tick: at most 64 events and 32 new paths; past it the quietest plays are dropped
first (counted: `ServerHost.SoundsDropped`, `ServerMetrics.SoundsDropped`, "sounds dropped N" at the end of a status line), never a stop,
an update or a mute (they keep the client's channels right). Stops, updates and mutes past 64 go out in more messages of the same tick.

The sound lengths (tg's `SSsounds.get_sound_length`, rust-g's `sound_len`) come from the sound tool's index (`--sound-index FILE`,
`SOUND_INDEX`; default `godot/assets/tg/sound/index.json`, found from the working folder or the server's folder upward; the field `ds`,
deciseconds), installed in every mode (a replay reads the same lengths). A missing or unreadable index gives one line on the output
("sounds: the index ... cannot be read") and every length 0. The lengths are world input (a loop's timing reads them): the command log ends with
`sounds X16`, a digest of the run's table (`SoundLengths.Digest`), and a replay whose table has another digest refuses to start
rather than end with another hash (a log without the line predates it).

### 22.3 The client (`SoundMixer`, `SoundPlayer`, `SoundLibrary`; M4b audio slice 3)

The game scene's `SoundPlayer` (`godot/scripts/Audio/SoundPlayer.cs`) takes `ReplicaWorld.SoundsReceived` and runs the engine-free
`SoundMixer` (`godot/scripts/Shared/Audio/SoundMixer.cs`) on a pool of 32 positional (`AudioStreamPlayer3D`) and 8 centred
(`AudioStreamPlayer`) voices; a positional event takes a positional voice, the rest a centred one. BYOND's channel rules as tg relies on them:
- a play on a channel replaces what it played (BYOND `wait = 0`); a play on channel 0 takes any free voice, which no later play replaces
  (tg's `SEND_SOUND` with no channel, about 150 calls) and only a stop of every channel stops; a stop frees its channel's voice, a stop on
  channel 0 stops every voice;
- an update changes the playing sound's volume, muffle, occlusion, mute, environment, a non-zero frequency and its tile without
  restarting it; a positional voice whose update has no tile plays on at the listener (centred); a centred voice whose update has a tile
  goes on in a positional voice from where it was; an update on a channel with nothing playing is dropped;
- a mute keeps the voice playing at silence (-80 dB), so a token coming back into range is heard where it is;
- a full pool steals its quietest voice (the volume after the occlusion, a mute the quietest; the oldest of equals), a voice of tg's
  reserved channels (above 1011) last (DV228);
- a one-shot frees its voice when the player has played it out, or at the latest its index length (from the offset, at its pitch) plus
  0.25 s.

The numbers: the volume is tg's (DV36: the players add no attenuation, filter or doppler; they only pan, from the current Camera3D, the
source at its tile's centre at ear height): `VolumeDb = LinearToDb(volume / 100 x (1 - k x occlusion))`, k 0.65 by default (DV39). A
voice whose max(muffle, occlusion) is 0.5 or more plays on the `SfxMuffled` bus (a low-pass, 900 Hz by default), the rest on `Sfx`
(DV37, DV39); both buses are made in code under Master. The frequency is a speed (Godot's pitch scale changes speed and pitch together,
as BYOND does): 0 is 1, a magnitude up to 100 a multiplier, more a rate in Hz over the file's sample rate from the sound index (44.1 kHz
when the index does not know the file), kept within 0.01 to 16; a negative one plays forwards. The offset is read as deciseconds (DV38),
modulo the length for a loop; a one-shot whose offset is past its end plays nothing. D296: a voice with an environment plays through
that environment's copy of its bus (`SfxEnvN`, `SfxMuffledEnvN`, all made with the others; a hard limiter on Master keeps many loud sounds from clipping) with Godot's reverb set from BYOND's I3DL2
preset (`ReverbPresets`, DV227); the Settings' room echo switch plays every voice dry. The Settings' ambience and ship hum volumes scale
the voices of channels 1018 and 1017 (DV226); a sound replaced or stopped on either fades out over 4 s, a short loop whose ends
fade plays as a seamless crossfaded copy, and a full pool steals a reserved channel's voice (above 1011) only when no other is left (DV228). k, the cutoff, the threshold and the reverb's wet scale are the dev menu's Audio tab's
(apps.md 2.20).

`SoundLibrary` loads a file where the sound index's `file` says (`sound/...` or `sound-nc/...` under the asset root: `ANOMALY_SOUND_ROOT`,
else the icons' root), with `AudioStreamOggVorbis.LoadFromFile` (`AudioStreamWav.LoadFromFile` for the index's `wav` file), caches about
64 MB (least recently used out first), and gives a looping play its own looping copy. A file that is not in the index or cannot be loaded
is logged once and plays nothing (counted as missing).

### 22.4 Tests

`tests/Anomaly.Server.Tests/M4b/ProtocolV13Tests.cs` (the layout's bytes, the round trip of every field, every refusal of the encoder and
the decoder, every truncation, the replica's table, map and order checks, a version-12 client refused), the fuzz corpus's Sounds
(`CodecFuzzTests`), `tests/Anomaly.Server.Tests/M4b/AudioServerTests.cs` (through the real server: an airlock opened 5 tiles from one player
reaches it at 28 half units, with its tile, no muffle or occlusion and tg's random frequency, and nothing of it reaches a player 20 tiles
away; a join's sound comes after its MapGrid in its own tick; the budget drops the quietest plays and keeps the stops, updates and mutes,
caps the new paths, splits a flood of stops; the host counts the drops; the occlusion of an open line, a diagonal, a corner, a wall, a
closed door, the door's own tile and the opened door; the muffle at 50 kPa and in a vacuum, where tg still lets 0.15 through at a tile;
the lengths from an index fixture, a missing index), and the moved version checks of `ProjectsLoadTests`, `WireTests`, `CodecTests`,
`ScreenChildrenCodecTests`. The client's: `tests/Anomaly.Server.Tests/Client/Audio/SoundMixerTests.cs` (a play replacing its channel's sound,
stop and stop all, channel 0 on any free voice, an update without a restart and the moves between the pools, a mute keeping its voice,
the steal order, the decibels with the occlusion, the muffled bus, the pitch from Hz and from a multiplier, the offset, the expiry, a
missing file, the log, the index, the cache, the dev settings, the fake server's door sound), and the headless smoke's
`[anomaly] audio: events N voices M missing 0` line (godot/tools/smoke.sh; the Dummy driver mixes nothing audible).

## 23. Version 14 (M4c slice 3: BYOND's MouseExited; the client hovers its HUD)

Version 14 adds one action kind; nothing else changes. Types: `ActionKind.Unhover` (9) in `src/Protocol/MessagesV2.cs`, `ActionKinds.Last`
in `MessagesV14.cs`, the codec's kind check in `Codec.V2.cs`.

### 23.1 The rule

BYOND calls `MouseExited(location, control, params)` on the object the mouse leaves, before `MouseEntered` on the one it enters, and alone
when it leaves to nothing (BYOND ref `/atom/proc/MouseExited`). tg's hover states rely on the pair: the space suit display shows its charge
while hovered (`screen_objects.dm:479`, :487), an inventory slot shows a ghost of the held item (:219, :224), the zone selector highlights the
part under the mouse (:748, :781), the health doll its numbers (:939, :946), the action buttons and the palette open and close their
tooltips (`action_button.dm:99`, :104). Before version 14 the host ran only `MouseEntered`, so every hover state stuck.

### 23.2 The wire and the server

`Unhover` is untargeted: `[u8 id = 10] [varuint frameSeq] [u8 kind = 9]` and nothing more (a target, a button, a keybinding or text on it
is refused as on the other untargeted kinds; kind 10 and above are unknown). The server keeps the thing each client's pointer is over
(`client.dm_mouse_over`, engine state): `ClientSeam.Hover(player, B)` calls `A.MouseExited` on the last one when it is another live atom,
then `B.MouseEntered` as before; `ClientSeam.Unhover(player)` calls `A.MouseExited` and forgets it (nothing when nothing is hovered or the
object is gone; BYOND exits nothing deleted). Both run with `usr` the client's mob, the location of `Hover` (null for a screen object, the
turf otherwise) and the map's control. An `Unhover` is a hover for the newest-pending rule (one hover or unhover a tick and session, the
newest), takes only the overall action bucket (dropping one would leave a hover state on; the client sends at most one hover or unhover per
`HoverInterval`), and is recorded in the command log by its kind number like the other actions (`IClientEntryPoints.Unhover`).

The replicator also follows BYOND's draw rule for screen objects: an object of `client.screen` whose `invisibility` is above the client's
mob's `see_invisible` is not sent (tg hides the empty action palette with `INVISIBILITY_ABSTRACT`, `action_button.dm:328`).

### 23.3 The client (`PlaySession.Hover`, `GameScene.Interaction`)

`PlaySession.Hover(target, now)` keeps what the server was told the pointer is over. Nothing under the crosshair or the pointer clears the
hints at once and, when the server was told of a thing, sends one `Unhover` on the same `HoverInterval` as the hovers (`UnhoversSent`
counts them); coming back to the thing the server still has before the interval sends nothing. In cursor mode the hover is the HUD entry
under the pointer (`HudPicker.PickScreen`: a `Screen` target, as the mouse over BYOND's screen), so the HUD's hover states work; in the
captured view it stays the crosshair's entity or turf.

### 23.4 Tests

`tests/Anomaly.Server.Tests/Protocol/ProtocolV14Tests.cs` (the bytes and round trip of an Unhover, the refusals, the host applying it and
keeping the newest of a hover and an unhover of one tick, a version-13 client refused, the play session's unhover on the interval), the fuzz
corpus's Unhover (`CodecFuzzTests`), the moved kind check of `CodecV2Tests` and the version of `ProjectsLoadTests`;
`tests/Anomaly.Core.Tests/M4c/ActionButtonTests.cs` (`MouseExitedClearsTheHoverStateMouseEnteredSet`: the suit display's hovering cleared by a
hover of another screen object and by an unhover) and `tests/Anomaly.Server.Tests/Integration/ActionButtonGameplayTests.cs` (through the real
server on Meta: the client's hover of an empty pocket slot with a tank in hand shows tg's ghost of the tank on the slot, and the unhover
takes it away; the empty palette is not sent).

## 24. Version 15 (M4c slice 4: tg's radial menu, DV40)

Version 15 adds one message and one action kind; nothing else changes. Types: `src/Protocol/MessagesV15.cs` (`RadialMenuMessage`,
`RadialSlice`, `RadialMenuKind`, `RadialMenuFlags`, `RadialElementFlags`, `ActionKinds.Last`), `ActionKind.RadialPick` (10) with
`ActionMessage.RadialMenu` and `RadialElement` in `MessagesV2.cs`, `MessageId.Radial` (15), codec `Codec.V15.cs` (the action in
`Codec.V2.cs`), caps in `Limits.cs` (`MaxRadialElements` 16, `MaxRadialNameBytes` 128, `MaxRadialMenuBytes`, `MaxActionV15Bytes` 15),
the replica's part in `ReplicaWorld.V15.cs`.

### 24.1 The rule

tg's `show_radial_menu` (`_onclick/hud/screen_objects/radial.dm:398`) shows a menu of choices to one client and waits (`stoplag(1)`,
`:377`) until a slice or the centre button is clicked, the player moves out of reach (`require_near`), the `custom_check` fails at its
`check_delay`, the same menu is asked for again (its unique id), or the client goes. The suit storage unit (`suit_storage_unit.dm:375`)
and the toolbox's quick tool use (`_toolbox.dm:78`) are its M4c callers. The server keeps tg's menu (the datum, translated whole but
for the slices, DV40) and the client draws it: a page of the menu is a RadialMenu message, the client's click a RadialPick action.

### 24.2 The wire

```
RadialMenu (server to client, State): [u8 id = 15] [u8 kind: 0 open, 1 close] [varuint menu id >= 1]
  open:  [u8 flags: bit 0 tooltips, bit 1 close button, bit 2 slide in, bit 3 fade in, bit 4 fade out; bits 5-7 zero]
         [u8 page 1..pages] [u8 pages 1..255]
         close button: [tableref centre] [tableref centre hovered]                (never none)
         [count 1..16 slices] { [str(1..128) name] [u8 slice flags: bit 0 next page, bit 1 click on hover; bits 2-7 zero]
                                [tableref picture (none: a choice without one)] [tableref slice] [tableref slice hovered] }*
  close: nothing more
RadialPick (Action kind 10, client to server, Control): [u8 id = 10] [varuint frameSeq] [u8 kind = 10] [varuint menu id >= 1] [varuint element 0..16]
```

A tableref is the appearance table reference of section 4.3 (`(index << 1) | private`). The menu id is the server's world id of the
`/datum/radial_menu`. A slice's picture is tg's choice image as `extract_image` makes it (`new /mutable_appearance(choice)`: an atom's
appearance, an image, or `icon(file, state)`: the file and that state), on `ABOVE_HUD_PLANE`, layer `RADIAL_CONTENT_LAYER`; the slice
pictures are `icons/hud/radial.dmi`'s `radial_slice` (or the menu's `radial_slice_icon`) and its `_focus` state, a next-page slice
`radial_slice` with the `radial_next` picture, the centre `radial_center` and `radial_center_focus`. The element of a pick is the slice's
place in the message (1 based); 0 is the centre. The codec refuses a kind other than 0 or 1, a menu id of 0, unknown flag bits, a page
outside its pages, an open with no slices or more than 16, an empty name, a centre without its flag (or the flag without a centre), a
slice or centre without a picture, a close with an open's fields, and a pick of element 17 or more or without a menu, or a pick's fields
on another kind.

### 24.3 The server (`Replicator`, `ServerHost`, `ClientSeam.RadialPick`)

The port's `show_to` lists the menu on the client (`client.dm_radial_menus`) and `hide` takes it off; `update_screen_objects` writes the
page's slices (`datum_radial_menu.dm_elements`, a serial counts the changes). The replicator's session update (`UpdateRadials`) reads the
client's list after the tick: a menu not sent yet or whose page changed is an open, its pictures interned into the session's private
tables in that same private block (D40), and a menu no longer listed is a close (closes first, then the opens in the list's order). The
host sends them right after the session's Delta (or its join's Snapshot) on the State channel (`ServerHost.SendRadials`), so the client
has the pictures' rows when the message arrives. A RadialPick is validated (a menu above 0, an element up to 16), takes the overall and
the click buckets, is queued with its frame like a click and logged (the command log's action extra gains the menu and the element after
the aim zone), and applied in the command phase: `ClientSeam.RadialPick` finds the menu among those shown to the player (another
player's menu, a closed one, an element past the page or a centre the menu has not is a click on nothing) and runs tg's slice `Click`
(`radial.dm:53`: `next_page()`, or `element_chosen`) or centre `Click` (`:72`: `finished`) with `usr` the client's mob. The waiting
`show_radial_menu` sees the answer at its next `stoplag` and returns it.

### 24.4 The client (`ReplicaWorld.V15`, `RadialModel`, `Hud/Radial.cs`, `GameScene.Interaction`)

The replica keeps the open menus in the order they opened or last changed (`Radials`, `TopRadial`; a MapGrid forgets them; a close of a
menu that is not open, or a picture outside the tables, is a desync) and raises `RadialChanged`. The game scene shows the newest open
menu as a sticky radial (`RadialModel.OpenServer`): tg's pictures composed from the tables on a ring around the crosshair, the first
slice at the top and clockwise, the hovered slice in its `_focus` picture with its name under the ring (tg's screentip of a hovered
slice), the number keys 1 to 9 on the slices. The captured mouse aims (cursor mode: the pointer), a left click picks the aimed slice
(`PlaySession.RadialPick`), the centre picks the close button; Esc and the right button are the close button too; a menu without one
stays until a slice is picked. A slice with `click_on_hover` is picked when it is highlighted. The bots decode the message and count a
bad one as a decode error; the FakeServer shows no radial.

### 24.5 Tests

`tests/Anomaly.Server.Tests/Protocol/ProtocolV15Tests.cs` (the bytes and round trips of a pick, an open and a close, the refusals, the
replica's order and checks, the host applying and logging a pick and the log's round trip, a version-14 client refused, the client
model's server menu: slices, centre, number keys, aim), the fuzz corpus's RadialMenu and RadialPick (`CodecFuzzTests`), the moved kind
checks of `CodecV2Tests` and `ProtocolV14Tests`, the version of `ProjectsLoadTests`; `tests/Anomaly.Core.Tests/M4c/RadialTests.cs` (the
wait and the pick, autopick, the centre, require_near, the custom check's delay, pages, a repeat, another player's pick, a client
leaving), `SuitStorageUnitTests.cs`; `tests/Anomaly.Server.Tests/Integration/SuitStorageGameplayTests.cs` (through the real server on
Meta: the unit's radial reaches the client with radial.dmi's pictures, the client's picks open it and dispense the suit, the helmet and
the mask, the centre closes it).

**Persistent menus (M7, D164, DV79).** tg's `show_radial_menu_persistent` (the rapid pipe cleaner layer's wiring menu) stays on the screen: a pick calls its callback and the centre button picks null. The wire is unchanged: the client closes its drawing at any pick, and the server sends a persistent menu again right after each pick (`ClientSeam.RadialPick` bumps the menu's `dm_serial`), so it reappears; `tests/Anomaly.Server.Tests/Integration/RclGameplayTests.cs` checks it on Meta.


## 25. Version 16 (M5 slice 3: tg's tgui windows drawn natively, D148, DV44)

Version 16 adds one message and two action kinds; nothing else changes. Types: `src/Protocol/MessagesV16.cs` (`UiWindowMessage`,
`UiWindowKind`, `UiValue`, `UiValueKind`, `ActionKinds.Last`), `ActionKind.UiAct` (11) and `ActionKind.UiClose` (12) with
`ActionMessage.UiWindow`, `UiAction` and `UiParams` in `MessagesV2.cs`, `MessageId.UiWindow` (16), codec `Codec.V16.cs` (the actions are
written and read by `Codec.V2.cs`'s action codec through it), caps in `Limits.cs` (`MaxUiWindows` 200, `MaxUiInterfaceBytes` 64,
`MaxUiTitleBytes` 256, `MaxUiStringBytes` 4096, `MaxUiKeyBytes` 128, `MaxUiDepth` 16, `MaxUiValues` 65536 since version 98 (32768 before), `MaxUiWindowBytes` 1 MiB since version 98 (256 KiB before),
`MaxUiActionBytes` 48, `MaxUiParamsBytes` 140), the replica's part in `ReplicaWorld.V16.cs`.

### 25.1 The rule

tg's windows are tgui: the server's `/datum/tgui` (one per open window of a player and an object) writes JSON messages into a pooled
browser control of the player's BYOND client (`tgui_window`, `"tgui-window-N"`): `update` with the payload (`config`: the title, the ui
status, the interface's name, refreshing; `data`: the object's `ui_data`; `static_data`: its `ui_static_data`), `suspend` (hide),
`ping`; `browse(null)` closes it. The page sends `act/<action>` with a JSON payload (tg's `ui_act(action, params)`) and `close` back
(`tgui_Topic`). SStgui calls each open window's `process` every 0.9 s, which sends the whole `ui_data` again. The port keeps all of
tg's DM side (SStgui, `/datum/tgui`, the ui states, the pool) and draws the window natively in the client (D148): the window's output is
a UiWindow message, the page's messages are UiAct and UiClose actions (DV44).

### 25.2 The wire

```
UiWindow (server to client, State): [u8 id = 16] [u8 kind: 0 update, 1 close] [varuint window 1..200]
  update: [str(1..64) interface] [str(0..256) title] [u8 status + 1: 0..3]
          [u8 flags: bit 0 refreshing, bit 1 data, bit 2 static data; bits 3-7 zero]
          (data: [value]) (static data: [value])
  close:  nothing more
value: [u8 tag] then
  0 null, 1 false, 2 true:  nothing
  3 integer:  [varint zigzag]              (a whole number of at most 53 bits)
  4 number:   [u64 IEEE 754 double, LE]   (any other finite number)
  5 string:   [str(4096)]
  6 array:    [count] { value }*
  7 object:   [count] { [str(128) key] value }*
UiAct (Action kind 11, client to server, Control):   [u8 id = 10] [varuint frameSeq] [u8 kind = 11] [varuint window 1..200]
                                                     [str(1..48) action] [value params: an object or null, at most 140 bytes]
UiClose (Action kind 12, client to server, Control): [u8 id = 10] [varuint frameSeq] [u8 kind = 12] [varuint window 1..200]
```

The window is tg's pool index (`TGUI_WINDOW_INDEX`). A value is JSON's model: tg `json_encode`s its DM lists (an associative list is an
object whose fields keep their order, a plain list an array, a number a number, text a string, null null), the client's act params are
JavaScript values. A value nests at most 16 deep and a payload holds at most `Limits.MaxUiValues` values (65536 since version 98, 32768 before); the integer tag is used exactly when a number is
whole and within 53 bits, so every value has one encoding and a message that decodes encodes back to the same bytes. The codec refuses a
kind other than 0 or 1, a window outside 1 to 200, an update without an interface, a status outside -1 to 2, unknown flag bits, an
unknown tag, a NaN or infinite number, a whole number sent as a double, a value nested too deep or too many values, a close with an
update's fields; for the actions: a window outside its range, an act without an action or with params that are no object or over 140
bytes, a close with an action or params, or the UI fields on another kind. A UiAct body (5 + 1 + 48 + 140 = 194 bytes) fits in the dev
command's 200, so `MaxActionMessageBytes` and `MaxInboundMessageBytes` are unchanged.

### 25.3 The server (`tgui_window.cs`, `Replicator.Tgui.cs`, `ServerHost`, `ClientSeam.TguiMessage`)

The window datum is translated with its BYOND output replaced (DV44): `initialize`'s `browse(html)`, `send_message`'s `output()` and
`close`'s `browse(null)` become records in `client.dm_tgui_output` (`TguiOutput`: the window id, initialize, message with tg's JSON
`{"type", "payload"}`, or close; presentation output, not hashed), and the page's `ready` after loading and its `ping/reply` are answered
by the window at once. The replicator's session update (`UpdateUiWindows`) takes the client's records after the tick: an initialize or a
close of a window the client has is a close, a `suspend` message is a close, an `update` message is an update (its JSON parsed into the
UiValue tree; over-long strings and keys cut, values nested past the cap null; an empty DM list's `[]` is an empty object); an update
whose config and data equal the last one sent for that window and that has no static data is not sent (SStgui's 0.9 s refresh of the
same `ui_data` changes nothing the client merges). A join sends every open window again, whole (the merged data and static data). The
host sends them after the session's Delta or join Snapshot on the State channel (`ServerHost.SendUiWindows`; a message over the caps is
dropped and counted, `Session.UiWindowsDropped`). A UiAct takes the overall and the click buckets, a UiClose the overall one; both are
queued with their frame like a click and logged (the command log's action extra gains the aim zone, the radial fields, the window and the
params' JSON in hex; the act's action is the command's text), and applied in the command phase as tg's `tgui_Topic` would:
`ClientSeam.TguiMessage(client, "tgui-window-N", "act/<action>" or "close", the params json_decode'd)` finds the window among the
client's (`tgui_windows`; an unknown one is force-closed, `SStgui.force_close_window`) and calls its `on_message` with `usr` the client's
mob; `/datum/tgui/on_message` runs `ui_act` through the verb queue and `SStgui.update_uis` on a TRUE.

### 25.4 The client (`ReplicaWorld.V16`, `Shared/Tgui`, `Hud/Tgui`, `GameScene`)

The replica keeps the open windows by index (`UiWindows`: the interface, title, status, refreshing, and the data and static data merged
update over update as tgui's store does, `{...prev, ...payload}`; an update with another interface starts the window afresh; a MapGrid
forgets them; a close of a window that is not open, or data that is no object, is a desync) and raises `UiWindowChanged`. The window
manager (`TguiWindows`) renders each window through its translated interface (`TguiInterfaces`: Canister, StackCrafting,
AirlockElectronics, NumberInputModal, AlertModal, PersonalCrafting, AirAlarm, Smes, PortableGenerator, PowerMonitor; another name shows a notice) with the merged data (static under data, tgui's
`useBackend().data`) and the window's local state (React's `useState`), and its acts go out as UiAct (`PlaySession.UiAct`); a window
whose status is below interactive sends none (the server would refuse them). The Godot host (`Hud/Tgui/TguiHost.cs`) draws the windows
in tg's theme over the HUD; the close button and Esc send UiClose (the input modals' Esc cancels, their Enter submits, as their TSX key
handlers do). The bots decode the message and count a bad one as a decode error; the FakeServer opens no window.

### 25.5 Tests

`tests/Anomaly.Server.Tests/Protocol/ProtocolV16Tests.cs` (the bytes and round trips of an update, a close and the value tags, the
refusals, the actions and their refusals, the replica's merge and checks, the host applying and logging an act and a close, a bad action
as a strike, a version-15 client refused), the fuzz corpus's UiWindow, UiAct and UiClose (`CodecFuzzTests`), the moved kind checks of
`CodecV2Tests`, `ProtocolV14Tests` and `ProtocolV15Tests`, the known ids of `WireTests` and `CodecTests`, the version of
`ProjectsLoadTests`; `tests/Anomaly.Server.Tests/Client/Tgui/TguiKitTests.cs` (the interfaces' data and acts, the window manager, the
window positions option); `tests/Anomaly.Core.Tests/M5/TguiTests.cs` (tg's tgui on the server); `tests/Anomaly.Server.Tests/Integration/
TguiGameplayTests.cs` (through the real server on Meta: a click opens the air canister's window with tg's data, its knob and valve
button release the gas into the room, walking away closes the window).

## 26. Version 17 (drag and drop: BYOND's MouseDrop reaches the server)

Version 17 adds one action kind; nothing else changes. Types: `ActionKind.Drop` (13, after version 16's `UiAct` 11 and `UiClose` 12) and
the `ActionMessage` members `OverKind`, `OverTarget`, `OverX`, `OverY`, `OverZ`, `DragTime` in `src/Protocol/MessagesV2.cs`,
`ActionKinds.Last` in `MessagesV17.cs`, `Limits.MaxDropActionBytes`, the codec's action in `Codec.V2.cs`.

### 26.1 The rule

BYOND calls the client's `MouseDrag(src_object, over_object, src_location, over_location, src_control, over_control, params)` while the mouse
moves with a button held over a thing, and `MouseDrop` with the same arguments when the button is released over another; the defaults call
the same procs on `src_object` (OpenDream's DMStandard `Client.dm`). tg overrides both client procs (`code/_onclick/drag_drop.dm:173`, `:194`:
the drag's start time and params, the middle-button drag of the aimbot check, the autoclick target) and `/atom/MouseDrop` (`:8`: a short drag
is a click, `check_drag_proximity`; then `base_mouse_drop_handler`: `COMSIG_MOUSEDROP_ONTO` on the dragged thing, `COMSIG_MOUSEDROPPED_ONTO` on
the target, reach and usability unless both ignore them, `mouse_drop_dragged`, `mouse_drop_receive`). A worn backpack comes off into a hand
this way (tg's storage `on_mousedrop_onto`), and many of tg's actions are drags (interaction.md section 8).

### 26.2 The wire

```
Drop (Action kind 13, client to server, Control):
  [u8 id = 10] [varuint frameSeq] [u8 kind = 13]
  [u8 target kind 1..3] [dragged]   [u8 target kind 1..3] [over]          (targets as section 14.1: entity, turf, screen)
  [u8 button 1..3] [u8 modifiers: shift 1, ctrl 2, alt 4, the rest zero]
  [u8 icon-x 0..32] [u8 icon-y 0..32]                                       (tg's pixel within the over thing's icon at the release)
  [u8 drag time]                                                            (deciseconds from the press to the release, 255 for longer)
```

```
0A 05 0D 03 AC 02 02 07 05 01 01 02 10 09 04   frame 5: screen entry 300 dragged with ctrl onto the turf 7,5,1, icon pixel 16, 9, 0.4 s
```

The codec refuses a drop without both targets, a button outside 1 to 3, modifier bits above alt (a click's aim zone bits are a click's
only), an icon pixel over 32, a missing or extra byte, and a drop's fields (`OverKind` to `DragTime`) on any other kind. The drop is at most
`Limits.MaxDropActionBytes` (34) bytes, within `Limits.MaxActionMessageBytes`.

### 26.3 The server (`ServerHost.ApplyDrop`, `ClientSeam.Drop`, `IClientEntryPoints.Drop`)

The stateless checks are the click's (`ValidAction`: both targets by their kind, the button, the modifiers, the pixel; a failure is a strike).
A drop takes the overall action bucket and the click bucket, is queued with its frame like a click, and is applied in the command phase:
both things are resolved as a click's target (`TryResolveTarget`: an entity the player sees in view, a turf in view, an object of the
session's `client.screen` or a clickable vis child of one, protocol 12's items in hands and slots); a forged or stale one is dropped and
counted (`Session.DropsRejected`), never a strike. BYOND's params are built by the server (`ClientSeam.DropParams`): the button and modifier
keys, `button`, `drag` (the dragging button) and, for a screen object, its own `screen_loc` (the holder's for a vis child); the drop's params
also carry the icon pixel. `ClientSeam.Drop` runs, detached with `usr` the client's mob, tg's `/client/MouseDrag` with the drag's params and
then tg's `/client/MouseDrop` with the drop's, the locations as a click's (a map thing's turf, a turf itself, null for a screen object) and
the map's control. DV45: BYOND would call MouseDrag on every move of the drag; the server runs it once and dates tg's `drag_start` back by
the drag time, so tg's quick-drag-as-a-click rule (`is_drag_clickable`: shorter than 0.1 s and within 16 screen pixels) sees the drag's real
length. The command log records the drop after the aim zone, the radial and the window fields: `overKind,overTarget,overX,overY,overZ,dragTime`
(`CommandLog`, a drop's line always carries the aim, radial and version 16's window fields: zeros and `-`). `Session.DropsApplied` counts the applied ones.

### 26.4 The client (`DragModel`, `PlaySession.Drop`, `GameScene.Interaction`)

Only cursor mode (Tab) drags; the captured view has none. A left press on a HUD entry, an item drawn in a slot, or a world thing under the
pointer (the pick along the camera ray through the pointer) is tracked by the engine-free `DragModel`; a release without the pointer moving
more than 4 pixels is the click it always was (on the HUD entry; cursor mode never clicked the world), sent at the release; past the
threshold the release is a Drop over the HUD entry under the pointer, else the world thing or cell under it (`DragModel.ChooseOver`), and
nothing when nothing is there (BYOND's MouseDrop with no over object, which tg ignores). While dragging, the dragged thing's icon (a screen
entry's, a slot item's or an entity's appearance, `DragModel.IconOf`) is drawn at the pointer at the HUD's pixel size, and the world thing
under the pointer is outlined. The `--keys` script's `drag=left@XxY>XxY` step presses, moves and releases (docs/ai/specs/apps.md). The bots'
`Fiddle` drags one of its HUD entries onto another; the FakeServer ignores drops.

### 26.5 Tests

`tests/Anomaly.Server.Tests/Protocol/ProtocolV17Tests.cs` (the bytes and round trips, the refusals, the host handing tg's params to the seam and
logging the drop and the log's round trip, forged drops dropped without a strike, a malformed one a strike, a version-16 client refused), the
fuzz corpus's Drop (`CodecFuzzTests`), the moved kind checks of `CodecV2Tests` and `ProtocolV15Tests`, the version of `ProjectsLoadTests`;
`tests/Anomaly.Server.Tests/Client/Interaction/DragModelTests.cs` (the threshold, the click without a drag, the drop target's choice, the drag
time, the dragged icon, the script step); `tests/Anomaly.Core.Tests/Tg/M4aClick/DragDropTests.cs` (tg's handlers through the seam);
`tests/Anomaly.Server.Tests/Integration/DragDropGameplayTests.cs` and the M4c scenario (through the real server on Meta: the backpack dragged
off the back into a hand, a jetpack worn with E, the backpack emptied onto a table, forged drops).

## 27. Version 18 (HUD maptext, D142)

Version 18 adds BYOND's `maptext` to the appearance entry; there is no new message or action kind. Types: `AppearanceEntry.Maptext`, `MaptextWidth`,
`MaptextHeight`, `MaptextX`, `MaptextY` (`MessagesV2.cs`), the codec's presence bit 15 (`Codec.V2.cs`), `MessagesV18.cs`.

### 27.1 The rule

tg draws text on its screen objects with maptext: the space suit display's charge percentage on hover (`screen_objects.dm:510`), an action button's
cooldown counter (`cooldown_action.dm:80`) and keybind (`action_button.dm:177`, a mutable appearance overlay), a storage's item counts
(`storage_interface.dm:113`) and a stack's amount in a hand or a storage (`stack.dm:119`). The text is a small HTML subset (the `MAPTEXT*` macros of
`__DEFINES/text.dm`) in a box of `maptext_width` x `maptext_height` pixels (32 x 32 by default) whose lower left corner is `maptext_x`, `maptext_y`
from the icon's.

### 27.2 The wire

Presence bit 15: `[tableref maptext (a string table entry, never none)] [varuint width] [varuint height] [varint x] [varint y]`, after the underlays,
present exactly when the entry has a text (a box without a text is nothing). The entry names its string in the table it lives in or in the public
string table, as an icon state does. **Only an entry of a session's private appearance table carries it**: the codec refuses the bit in the public
table (the world's atoms never send maptext; runechat and the floating text over atoms are M9), and the replicator fills it only for the private
tables, the session's `client.screen` entries, their flattened `vis_contents` (the item in a hand or a slot) and overlays, and the session's
private images. The width and height are 0 to 32767 pixels, x and y -32768 to 32767. A maptext string is at most `Limits.MaxTableStringBytes`
bytes, raised from 256 to 1024 for it (a cut string is cut at a character, so its markup may end open; the client closes it).

### 27.3 The replicator

`IDmAppearance` gains `maptext`, `maptext_width`, `maptext_height`, `maptext_x`, `maptext_y` (atoms, images, mutable appearances and the
overlay snapshots `DmAppearance` carry them). `AppearanceTables.Fill` writes them for a private table; the interning key includes them, so a
counter that ticks is a new entry per distinct text (bounded by the values it takes). A maptext-only screen object (tg's `screen/text`) is an
entry. A `Maptext` hint of a movable joins the hinted ids of the tick (`Replicator._maptextHinted`), which is what makes the screen entry and
the holders of a `vis_contents` child look again; the public tables never see it.

### 27.4 The client

`MaptextMarkup` (engine-free, `godot/scripts/Shared/Maptext`) parses the subset into styled paragraphs, `MaptextLayout` places them in the box (wrapping, align, valign, line height) with measurements from the loaded fonts, `MaptextCollector` collects the texts of an appearance tree with their accumulated pixel offsets, and `MaptextRenderer` and `MaptextNode` (`godot/scripts/Hud/Maptext`) draw them over a HUD entry (`HudLayer`) or a storage cell (`ContainerWindow`). docs/ai/specs/apps.md has the rest. Each text is clipped to its box as BYOND draws it (`MaptextClip`: OpenDream draws a maptext into a render target of exactly `maptext_width` x `maptext_height` pixels, a 0 being 32, so nothing past the box shows; `MaptextLayout` splits a word longer than the box where the line overflows, OpenDream's word wrap); the client draws each box in a clipping canvas item (`MaptextBoxes`). The tooltips of tg's `openToolTip` are protocol version 19 (section 28).

### 27.5 Tests

`tests/Anomaly.Server.Tests/Protocol/ProtocolV18Tests.cs` (the bytes, the round trip, the refusals, the replica's check, the string cap), the fuzz corpus's private maptext entries, the moved version checks; `Client/Maptext/MaptextTests.cs`; `tests/Anomaly.Core.Tests` `OverlaySnapshotTests` (the vars on images, mutable appearances and snapshots); `Integration/HudMaptextGameplayTests.cs` (the real server on Meta).

## 28. Version 19 (tg's tooltips, DV47)

Version 19 adds one message; nothing else changes. Types: `src/Protocol/MessagesV19.cs` (`TooltipMessage`, `TooltipKind`), `MessageId.Tooltip` (17),
codec `Codec.V19.cs`, caps in `Limits.cs` (`MaxTooltipTextBytes` 4096, `MaxTooltipThemeBytes` 32, `MaxTooltipBytes`), the replica's part in
`ReplicaWorld.V19.cs`.

### 28.1 The rule

tg shows a tooltip for its HUD objects: an alert's, an action button's, the action palette's and its scroll buttons' `MouseEntered` call
`openToolTip(usr, src, params, title = name, content = desc, theme)` (`alert.dm:132`, `action_button.dm:99`, `:336`, `:462`), and an item on
the mob or in a storage does so after the player's tooltip delay (`items.dm:1160`, `openTip`: the description and the force word); their
`MouseExited` calls `closeToolTip`. `openToolTip` (`code/modules/tooltip/tooltip.dm:110`) picks the theme (the caller's, else the player's
HUD style lowercased, else `"default"`) and calls the client's `/datum/tooltip/show`, which wraps the texts (`"<h1>[title]</h1><p>[content]</p>"`,
a title or a content alone as a `<p>`), strips `\proper` and `\improper` and writes them with the cursor params, the view size and the theme
into a browser window of the BYOND client (`tooltip.html`), whose script lays the box out and shows it under the hovered tile; `hide` hides
the window, 0.1 s later when a show is under way. The port keeps the DM side whole; the window's output is a record the replicator sends
(DV47).

### 28.2 The wire

```
Tooltip (server to client, State): [u8 id = 17] [u8 kind: 0 show, 1 hide]
  show: [str(1..4096) text] [str(1..32) theme: a-z 0-9 '-' '_']
  hide: nothing more
```

```
11 00 09 3C 70 3E 78 3C 2F 70 3E 09 6D 69 64 6E 69 67 68 74   show "<p>x</p>" in "midnight"
11 01                                                         hide
```

The text is tg's HTML as `show` built it; the theme a class of `tooltip.html`. The codec refuses a kind other than 0 or 1, a show without a
text, a theme that is empty, longer than 32 bytes or has another character (an upper case one included), a hide with a show's fields, and a
byte more or less. A session has one tooltip; the server sends a message only when what the client shows changes, so a hide while nothing is
shown is a desync for the replica.

### 28.3 The server (`tooltip.cs`, `Replicator.Tooltip.cs`, `ServerHost`)

`client/New` makes the client's `/datum/tooltip` (`client_procs.dm:587`). The datum is translated with its BYOND output replaced (DV47):
`show`'s `output(..., "[control]:tooltip.update")` and `do_hide`'s `winshow` become records in `client.dm_tooltip_output` (`TooltipOutput`: a
show with tg's text, theme, special case and param object, or a hide; presentation output, not hashed); `New`'s `browse` of the page and
`show`'s `tooltip.init` write nothing. The hover's params (`ClientSeam.HoverParams`) are BYOND's `screen-loc` of a HUD object (its own
`screen_loc`, its holder's for an item in a hand or slot), empty for a map thing, so tg's `show`, which needs params, shows only for the HUD.
The replicator (`UpdateTooltip`) takes the tick's records and keeps the last state: a show and a hide in one tick (the pointer crossed a
button) send nothing, a show of another text or theme replaces the shown one; a text over the cap is cut, a theme that is no class name is
sent as `"default"`. A join sends the shown tooltip again. The host sends it after the session's Delta or join Snapshot on the State channel
(`ServerHost.SendTooltip`, `Session.TooltipsSent`). The preferences are not translated (M9): `openToolTip` reads no HUD style (a caller
without a theme gets `"default"`), and an item's tooltip reads the defaults of `enable_tooltips` (on) and `tooltip_delay` (500 ms, DV47).

### 28.4 The client (`ReplicaWorld.V19`, `Shared/Maptext/TooltipLayout`, `Hud/TooltipView`, `GameScene`)

The replica keeps the shown tooltip (`Tooltip`; a MapGrid forgets it) and raises `TooltipChanged`. `TooltipLayout` (engine-free) lays the box
out as `tooltip.html` does: the text parsed by `MaptextMarkup` with the page's sheet (`MaptextSheet.Tooltip`: the body bold 12px sans in the
theme's colour, `h1` 1.2em with line-height 1.4 and margins -5px above, 2px below, `p` line-height 1.2, `br`, `b`), the box as wide as the
unwrapped text but at most 298 pixels inside the outer border (the text area 278), the script's two extra pixels, 8 pixels of padding inside a
2-pixel inner and a 2-pixel outer border, in the theme's colours (`TooltipThemes`: `default` and the 17 themes of the page; an unknown one is
the default look). `TooltipView` (a canvas layer over the tgui windows) draws it at the HUD's UI scale under the pointer, above it when the
bottom edge would leave the window, kept on screen (`TooltipLayout.Position`), while the replica has one and the pointer is free (cursor mode
or a tgui window); the system's Arial or Helvetica is the font (`MaptextFonts`, `MaptextFont.Sans`). The bots decode the message; the
FakeServer sends none.

### 28.5 Tests

`tests/Anomaly.Server.Tests/Protocol/ProtocolV19Tests.cs` (the bytes, the round trips, the refusals, structured noise, the replica), the fuzz
corpus's Tooltip (`CodecFuzzTests`), the known ids of `WireTests` and `CodecTests`, the version of `ProjectsLoadTests`;
`tests/Anomaly.Server.Tests/Client/Maptext/TooltipLayoutTests.cs` (the tooltip sheet, the themes, the box's width, cap and height, the place by
the pointer; the maptext clip and the split of a long word); `tests/Anomaly.Core.Tests/M4c/TooltipTests.cs` (the client's datum, an alert's
and an action button's tooltip on hover and its hide on unhover, an item's after the delay with its force, the datum's rules);
`tests/Anomaly.Server.Tests/Integration/TooltipGameplayTests.cs` (through the real server on Meta: the tank's Set Internals button and the
Choking alert hovered and unhovered).

## 29. Version 20 (pulling)

Version 20 adds one keybinding code; nothing else changes. `KeybindingCode.StopPulling` = **9** (`src/Protocol/MessagesV4.cs`, now
`KeybindingCode.Last`) is tg's `/datum/keybinding/mob/stop_pulling` (hotkeys H and Delete, `code/datums/keybinding/mob.dm:5`): its `down`
calls the mob's `stop_pulling()`, or says "You are not pulling anything." (`ClientSeam.KeybindingType(9)`). The Action's layout is
section 14.1's (kind 6): `0A 07 06 09 00` is frame 7's press of the stop key. A decoder of version 20 refuses codes 0 and 10 and up. The client binds it to
the action `StopPulling` (H and Delete by default, remappable: `PlayOptions`, the options panel). The pull itself needs nothing on the wire: a
pull is tg's ctrl-click (`ClickModifiers.Ctrl`, section 14.1), the pulled thing's moves are entity moves with its glide size (the puller's,
DV48), the HUD's pull button is a screen entry (`/atom/movable/screen/pull`, icon states `pull` and `pull0`) and the pull offsets of a
pulled mob are its appearance's `pixel_x`/`pixel_y` and `animate()` chains (sections 18, 20).

Tests: the version of `ProjectsLoadTests`, the code range of `CodecV4Tests` and `HostV4Tests`, the keybinding table of
`ProtocolV4ContractTests` and `InteractionGuardContractTests`; the pull through the real server: `Integration/PullGameplayTests.cs` (Meta)
and `Integration/PullPredictionTests.cs` (the test room).


## 30. Version 21 (M5x slice 2: the camera shake)

Version 21 adds one message; nothing else changes. Types: `src/Protocol/MessagesV21.cs` (`CameraShakeMessage`, `CameraShakeStep`),
`MessageId.CameraShake` (18), codec `Codec.V21.cs`, caps in `Limits.cs` (`MaxCameraShakeSteps` 256, `MaxCameraShakePixels` 4096,
`MaxCameraShakeStepTime` 600 ds, `MaxCameraShakeBytes`), the replica's part in `ReplicaWorld.V21.cs`.

### 30.1 The rule

tg shakes a player's view with `shake_camera(M, duration, strength)` (`code/modules/mob/mob_helpers.dm:164`; an explosion's listeners,
`explosions.dm:383` and `shake_the_room`, the gravity generator's change): a chain of BYOND `animate()` steps on the player's client, each
to a random `pixel_x` and `pixel_y` inside a box of `strength` tiles, in a time of the step's distance, and a last step back to the start
in 3 ds. The port runs it as written; the presentation sink reports each step with the client as its subject. The server keeps the tick's
last chain per client (a call with an object starts a chain and drops the one before, as BYOND's animate does) and sends it to that
player as one `CameraShake`, after the tick's Delta (`ServerHost.SendCameraShake`, `Replicator.CameraShake.cs`). A chain on a client no
session takes is dropped two ticks later.

### 30.2 Wire format

```
CameraShake (server to client, State): [u8 id = 18] [varuint count(1..256)]
  count x ( [varint pixel_x(-4096..4096)] [varint pixel_y(-4096..4096)] [varuint time(1..600)] )
```

A path longer than 256 steps keeps its first 256. A decoder refuses an empty path, a step out of range and trailing bytes.

### 30.3 The client

The replica raises `CameraShakeReceived`; the play session hands the steps to `CameraLayer.FollowShakePath`, which moves the first-person
eye from where it is to each step's offset in the step's time (linear, BYOND's default easing), 32 pixels to a tile: `pixel_x` to the
view's right, `pixel_y` up. A new path replaces the running one from the present offset. D70's camera shake switch turns it off.

## 31. Version 22 (the ghosts, D151: the observer layer)

Version 22 changes the private block (section 4.3); no message is added. Types: `src/Protocol/MessagesV22.cs` (`OwnBlock.LayerTick`,
`LayerSpawned`, `LayerChanged`, `LayerDespawned`), codec `Codec.V22.cs`, the replica's part in `ReplicaWorld.V22.cs`.

### 31.1 The rule

BYOND sends a client only the atoms whose `invisibility` is at most its mob's `see_invisible` (BYOND ref `/atom/var/invisibility`,
`/mob/var/see_invisible`). The shared Delta body carries what every viewer sees: invisibility up to `SEE_INVISIBLE_LIVING` (25, DV24).
The entities above it and up to `SEE_INVISIBLE_OBSERVER` (60), a ghost's `INVISIBILITY_OBSERVER` above all, are the **observer layer**:
they travel only in the private block of each connection whose mob's `see_invisible` reaches them, as spawns, changes and despawns of
that connection's own view. A living player's client never receives a ghost; a ghost's client receives every entity of the layer its
sight reaches, its own ghost included (the header's own entity is then a layer entity). The server-side rule is `replication.md`
section 17.

### 31.2 Wire format

The private block's flag bit 4 (16) is the layer part, after the images (bit 3):

```
if layer:  [count spawned] { entity record }*                       (section 5.1, ids strictly increasing)
           [count changed] { [varint id diff] change record }*        (section 5.2, ids strictly increasing)
           [count despawned] { [varint id diff] }*                    (ids strictly increasing)
```

The records are those of the Delta body, relative to the message's tick (a Delta's or a Snapshot's): an entity's last move, flick and
animation ticks travel as in the body. At most `Limits.MaxEntitiesPerMessage` records in the three lists together; a part flagged and
empty is refused. A Snapshot's block may carry layer spawns (the layer a joining viewer sees), never changes or despawns of entities it
does not list.

### 31.3 The replica

The replica applies the layer after the body's lists and before the own state: a spawn of an id the table holds, a change or a despawn of
an id it does not hold, is a desync (`ProtocolError.BadValue`). Layer entities join the same table as the body's, so the renderer, the
picker and the events see them as any entity. An id is never named by the body and a block in the same Delta: an entity whose
invisibility crosses 25 leaves its layer in one tick and enters the other in the next.

### 31.4 Tests

`ReplicatorV22Tests` (a session that sees the layer and one that does not, a move, the viewer's sight dropping and coming back, the
crossing both ways, two late joiners, a deletion; the codec round trip of a block with all three lists), `GhostScenarioTests` on
MetaStation (a living player never holds the ghost in any tick, a second ghost does, the ghost's own client follows it through a wall),
and the V2 oracle, which compares a viewer that sees the layer with the living truth plus the layer entities its mob sees.

## 32. Version 23 (DV54: the ghost's body)

Version 23 changes the private block (section 4.3); no message is added. Types: `src/Protocol/MessagesV23.cs` (`MapCell`,
`OwnBlock.BodyChanged`, `OwnBlock.Body`, `ReplicaWorld.OwnBody`), codec `Codec.V23.cs`, the server's part
`src/Server/Replication/Replicator.Body.cs`.

### 32.1 The rule

tg's map shows a ghost no sign of its corpse; the client's overview marks it (DV54). A connection whose mob is a ghost that may re-enter its
body, by the conditions of tg's `reenter_corpse` (observer.dm: `can_reenter_corpse`, and its mind's `current` not deleted), is told the
turf of that body: the body's own turf, or the turf of whatever holds it (`get_turf`: a body in a locker is marked at the locker). The
server sends it when it changes, and none once the mob is no such ghost (it re-entered, or its body was deleted).

### 32.2 Wire format

The private block's flag bit 5 (32) is the body part, after the layer (bit 4):

```
if body:  [u8 has]  then, when has is 1:  [varuint x] [varuint y] [varuint z]    (a cell, as the Delta's cell records)
```

`has` other than 0 or 1 is refused, as is a cell outside the map's coordinate ranges. A Delta's block carries the part only when the body
changed; a Snapshot's block carries it when there is a body (a Snapshot without it means none).

### 32.3 The replica and the client

`ReplicaWorld.OwnBody` keeps the last body named; a Delta without the part keeps it, a Snapshot without it clears it. The client's
`Hud/BodyMarker.cs` draws tg's Re-enter Corpse picture (`icons/hud/screen_ghost.dmi`, `reenter_corpse`) over the world at the body's turf
with the distance in tiles, on top of the walls, at the screen's edge when the body is off the screen or behind the camera.

### 32.4 Tests

`ProtocolV23Tests` (the part's bytes, a none, the part left out when unchanged, a bad flag refused, the replica through a Snapshot, Deltas
and a Snapshot without it), `GhostScenarioTests` (a ghost's client is told its body's turf, a living player's is not, and the marker is
gone once the ghost re-enters), `SingularityDeathTests` (the body's turf on MetaStation).

## 33. Version 24 (D154, DV59: tg's chat links)

Version 24 adds one action kind; no message is added. Types: `src/Protocol/MessagesV24.cs` (the comment), `ActionKind.Topic` (14,
`MessagesV2.cs`), `Limits.MaxTopicHrefBytes`; codec `Codec.V2.cs` (the action's own codec); the server's part `src/Server/ChatLinks.cs`,
`ServerHost.Network.cs` (the intake) and `ServerHost.World.cs` (the chat router, the command phase); the world's part
`src/Core/Tg/Infrastructure/ClientSeam.Topic.cs`.

### 33.1 The rule

tg writes links into its chat lines, `<a href='byond://?src=[ref];key=value;...'>`: a limb's embedded object in the self-check
(`check_for_injuries`, `check_self_for_injuries`), a tourniquet or a gauze to take off. In BYOND a click on one is the client's
`Topic(href, href_list, hsrc)`, and tg's `/client/Topic` ends in BYOND's default, the `src` datum's `Topic(href, href_list)`. The port's
client shows the BYOND links of a chat line as links and sends a click on one (in cursor mode) as a `Topic` action with the href. The
server records the hrefs of every link it sends to a session's chat (the `href` attributes of a `ServerText` that start with `byond://?`
and fit the cap; the newest 128 distinct ones, `Session.ChatLinks`) and takes a `Topic` only for one of them (DV59): another is dropped
and counted (`Session.TopicsRefused`), never a strike, since a player may click a line whose link has been forgotten. A taken one
runs in the command phase of its frame's tick: `ClientSeam.Topic` parses the query (`params2list`), locates `src` (or the player's mob
for `_src_=usr`) and calls its `Topic` with `usr` the player's mob, detached.

### 33.2 Wire format

```
Topic (Action kind 14, Control): [str(1..200) href]
```

The href is `ActionMessage.Text`, which since version 24 belongs to a dev command or a `Topic`; an empty one, one over
`Limits.MaxTopicHrefBytes` (200, the dev command's cap, so `Limits.MaxActionMessageBytes` holds it unchanged), a target, a button or
another kind's field is refused. The server checks that the href is not empty and fits the cap (a malformed action is a strike, as
every action's). The click takes the click bucket.

### 33.3 The client

`HtmlText.ToPlain(html, links)` gives the plain text with the BYOND links' spans (`ChatLink`: start, length, href); `ChatFeed` keeps a
line's links (a line with links keeps its text up to its last link, at most 1024 characters, where a plain line keeps 240);
`Hud/ChatLines.cs` lays a line out in words, draws its links in a link colour, underlined, and answers `LinkAt(point)`; in cursor mode a
left press on a link sends `PlaySession.Topic(href)`.

### 33.4 Tests

`ProtocolV24Tests` (the bytes, the round trip, the refusals, the hrefs the host keeps and forgets, the client's spans and the feed's
cap, the host taking only a sent link and logging it, a version-23 client refused), `M6/EmbeddingTests` (the self-check's link through
`ClientSeam.Topic` rips the star out, a link naming another limb does nothing), `Integration/EmbeddingGameplayTests` (MetaStation: the
alert's click, the self-check's link from the client, the star in the hand).

## 34. Version 25 (D155: BYOND's filters)

Version 25 adds two fields to an appearance entry; no message is added. Types: `src/Protocol/MessagesV25.cs` (`FilterKind`,
`FilterEntry`, `FilterFlags`), `AppearanceEntry.Filters` and `AppearanceEntry.RenderTarget` (`MessagesV2.cs`),
`Limits.MaxAppearanceFilters` (16); codec `Codec.V2.cs`; the server's part `src/Server/Replication/AppearanceTables.cs`; the world's
part `src/Core/DmShim/dm_filter.cs`, `src/Core/DmShim/icon.cs`, tg's filter procs (`datums/datum.cs`, `__HELPERS/filters.cs`).

### 34.1 The rule

An atom's or an image's `filters` (BYOND ref "filters") are sent with its appearance in their order, as far as the client draws their
type: `alpha` (the picture's alpha times a mask's: an icon state or a render source; `x`, `y`; `MASK_INVERSE`, `MASK_SWAP`),
`outline` (`size`, `color`, `OUTLINE_SQUARE`), `displace` (a map: an icon state or a render source; `x`, `y`, `size`) and `color` (a
colour matrix, or a colour text as a multiply). The other types (`angular_blur`, `bloom`, `blur`, `drop_shadow`, `layer`, `motion_blur`,
`radial_blur`, `rays`, `ripple`, `wave`, `greyscale`) are left out, and so are an alpha or displace filter with neither icon nor
source and an identity colour matrix: the client draws the picture as if they were not there. A filter's parameters are the values it
holds when the appearance is interned: `animate()` of a filter assigns them on the server (the chain's last values) and no animation of
a filter is sent (not-translated.md section 24). BYOND `render_target` is sent as a string: a filter's `render_source` takes the
picture of the appearance whose render target it names, and an appearance whose render target starts with `*` is not drawn itself.
Version 26 (section 35) replaces the rest of this rule: every type is sent, at its position, and a filter's animation is sent.

### 34.2 Wire format

```
appearance entry, presence bit 16: [count 1..16] { [u8 kind 1..4] [u8 fields] field values }*
  fields: bit 0 x y (2 x f32), 1 size (f32), 2 rgba (u32), 3 matrix (20 x f32), 4 flags (u8), 5 icon (tableref, a resource),
          6 icon state (tableref, a string), 7 render source (tableref, a string)
appearance entry, presence bit 17: [tableref renderTarget] (a string)
```

A field is present when it is not its default (x, y 0; size 1; rgba opaque black; no matrix; flags 0; none). An undefined kind, a
count over 16, a number that is not finite and a render target that names nothing are refused. The size bound of an entry grows by 114
bytes a filter.

### 34.3 The client

`AppearanceCompositor` draws an appearance that has filters or a render target as a group (its picture with its children's, as
`KEEP_TOGETHER` draws one), applies the filters in order (`AppearanceCompositor.Filters.cs`; a mask or a map is placed with its icon
square on the picture's, moved by `x` and `y`; an outline grows the picture by its size), keeps the picture of a render target by name
for a later filter of the same composition, and draws nothing for a `*` target. tg's one-handed glove (`human_update_icons.dm:199`)
puts its mask, `effects.dmi` `missingl` or `missingr`, as an overlay of the glove with the target `*MissGlove ...`. An `/icon` object
with `Blend` operations (tg's damaged clothing, `clothing.dm:519`) is the resource `iconops:FILE|STATE|FRAME|OP|...` (each part
URI-escaped; an operation `FUNCTION,X,Y,c,COLOUR` or `FUNCTION,X,Y,i,FILE,STATE`), which the icon library composes (`IconOps.cs`): the
one state of the file, each frame blended in order as BYOND's `/icon/proc/Blend` does (a colour without an alpha leaves the alpha). The M7 zaps slice (no version change: the recipe is resource text, and an older client draws nothing for a recipe it cannot read): `DrawBox` is the operation `100,X1,Y1,b,COLOUR,X2,Y2` (an empty colour clears the box to transparent; 1, 1 is the lower left pixel), an operand icon with operations of its own is `FUNCTION,X,Y,i,FILE,STATE,K` followed by the K parts of its operations (the beam's cut tip and its alpha mask, `beam.dm:299-313`), and the recipe's state is also its default state (`""`: BYOND's `icon(file, state)` holds the state as the icon's default, OpenDream's `InsertStates`; the cut tip is shown with `icon_state` `""`). The M7 husks slice (no version change, as before): `MapColors` is the operation `101,1,1,m,N1,...,Nk` (k 12: three rows and the constants, the alpha passing through; or 20: four rows and the constants; `SetIntensity` is sent as its diagonal `MapColors`), drawn as BYOND rounds (checked against BYOND's own results in OpenDream's `Tests/Icon/expected_results/MapColors.dmi`); a recipe whose STATE is empty is `new /icon(file)`, every state of the file, each made when shown by its own `icon_state` (tg's `ColorTone` of a husk's limb file, `_bodyparts.dm:1487`, `__HELPERS/icons.dm:213`), and an operand icon of every state of its file (`FUNCTION,X,Y,i,FILE,,K`) blends its state of the same name; `ICON_ADD` and `ICON_SUBTRACT` multiply the alphas (BYOND: transparent where either icon is), and a colour without an alpha is opaque. A part escapes only `%` and `|` (a file or state inside an operation also `,`), which the client's URI unescaping reads as before, so that a husk's masked leg (about 470 bytes) fits `Limits.MaxResourceBytes` (512): a longer recipe is not sent (the replicator counts an overflow). M10x slice D4 (no version change, as before): `Crop` is the operation `102,X1,Y1,k,X2,Y2` (the icon becomes that box, 1, 1 the lower left, outside the icon transparent) and `Scale` is `103,1,1,s,WIDTH,HEIGHT` (nearest neighbour), both changing the icon's size (tg's preference pictures, sent in the preferences window's constant data, DV44). Gaps SP1 (no version change, as before): a direction pick is the operation `106,1,1,d,DIR` in its place among the operations (BYOND's `icon(icon, state, dir)`: the icon keeps that one direction, the .dmi order S, N, E, W, SE, SW, NE, NW; a state of fewer directions gives its first), so that tg's lizard features' side shots face east.

### 34.4 Tests

`ProtocolV25Tests` (the round trip, an entry without filters, an undefined kind), `Client/Render/CompositorFilterTests` (the outline,
the mask from a hidden render target, the displacement map, the colour matrix, the damaged clothing's recipe), `M6/FilterTests`
(`filter()`, the filters list, tg's add, modify, transition and remove, the glove's mask, the fissure, the damaged jumpsuit),
`Integration/FilterGameplayTests` (MetaStation: the masked glove, the split skull and the doll's outline reach the client).


## 35. Version 26 (D156: every BYOND filter, and filter animation)

### 35.1 The filters

An appearance's filters (section 34) carry every filter type BYOND has, each at its position in the atom's filters list (an animation
names a filter by it; at most `Limits.MaxAppearanceFilters`, the rest are not sent). The kinds: 1 alpha, 2 outline, 3 displace,
4 color, 5 angular_blur, 6 bloom, 7 blur, 8 drop_shadow, 9 layer, 10 motion_blur, 11 radial_blur, 12 rays, 13 ripple, 14 wave,
15 greyscale (OpenDream's). The server resolves BYOND's default of every parameter not given (BYOND ref "Filter effects": a drop
shadow's x 1, y -1 and 50% black; a radial blur's size 0.01; rays' size 16, white, density 10, threshold 0.5, both flags; a ripple's
repeat 2 and falloff 1; bloom's offset 1 as OpenDream's), so the wire holds the values the client draws:

```
[filter] = [u8 kind 1..15] [varuint fields] then the set fields in bit order:
           bit 0 x, y (two f32)   1 size (f32)   2 color (u32 RGBA)   3 a colour matrix (20 f32)   4 flags (u8)
           5 icon (resource ref)   6 icon state (string ref)   7 render source (string ref)   8 offset   9 alpha
           10 threshold (f32)   11 threshold colour (u32 RGBA)   12 density   13 factor   14 repeat   15 radius   16 falloff
           (f32 each)   17 colour space (u8 0..3)   18 transform (6 f32, a to f)   19 blend mode (u8 0..5)
```

A field at its wire default (0, a size of 1, black, an alpha of 255, none) is left out; undefined field bits, a colour space above 3, a
blend mode above 5 and a non-finite number are `BadValue`. Flags on the wire: alpha `MASK_INVERSE` 1, `MASK_SWAP` 2; outline
`OUTLINE_SHARP` 1, `OUTLINE_SQUARE` 2; layer, rays and displace over 1, under 2 (rays with neither: alone; with both, an image's
default, under); wave and ripple `WAVE_BOUNDED` 1, wave `WAVE_SIDEWAYS` 2. The shim's `FILTER_OVERLAY` and `FILTER_UNDERLAY` are
OpenDream's 0 and 1, so the server maps a given flags value with the underlay bit to under and any other to over.

### 35.2 Filter animation

An animation track (section 18.1) may animate one filter of its atom: the track flags' bit 1. Then [u8 the filter's position 1..16]
follows the flags, and every [vars] of the track (its start and each step) is [filter vars]:

```
[filter vars] = [varuint mask: bit 0 x, 1 y, 2 size, 3 offset, 4 alpha, 5 radius, 6 falloff, 7 repeat, 8 density, 9 threshold,
                 10 factor, 11 color, 12 colour matrix] then the set numbers in that order (f32), the colour (u32 RGBA), the matrix (20 f32)
```

Track flag bit 2 (with bit 1 only, in a screen entry): the track animates a filter of one of the screen object's `vis_contents` children,
which the server flattens into its appearance (section 21); [varuint (appearance index << 1) | private] after the position names that
child's appearance row. tg's health doll pulses each wounded limb this way (`screen_objects.dm:1023`). Version 26 allows 8 tracks.

The server (`AnimationRecorder`): `animate(filter, ...)` of a filter in a registered movable's filters list (the filter's owner, the
atom or image whose filters list holds it last: BYOND's atom filters are copies of the ones assigned) is a track of that movable; it
replaces the running tracks of the same filter only, unless parallel; its start values are the parameters before the call, BYOND's
default for one never given; a step without an object continues it. The appearance holds the chain's end values (the shim assigns
them, as for an atom's vars). A screen entry's animation is its own tracks and its children's filter tracks (`Replicator`).

The client (`FilterPlayback`): a filter track plays as an atom's (delays, times, easing, loops; a relative step adds, a colour
multiplies); the filter is drawn at the appearance's value plus the track's difference from its end. The compositor recomposes the
picture with those filters (`AppearanceCompositor.TryCompose` with filter looks, keyed by appearance entry; the values rounded to a
sixteenth of a pixel and a whole alpha, a small cache of such pictures). The entity layer, the HUD and the map's static pictures
(handed to the entity layer once a filter animates) draw them.

### 35.3 Tests

`ProtocolV26Tests` (every kind with its parameters, a filter track in an entity and with a child in a screen entry, the refusals),
`Client/Render/CompositorFilterKindsTests` (each new kind on a hand-made picture; the outline's alpha and its pulse played),
`M6/FilterRowsTests` (the rows translated with D156), `Integration/FilterAnimationGameplayTests` (the real server: the doll's pulse
and the high gravity blur reach the client and play).

## 36. Version 27 (D159: labelled progress bars, an item's bar under its action button)

### 36.1 The rule

The owner (2026-10-18): a bare bar did not tell what it counted, and an item's bar (an air tank's volume while breathing from it) belongs
under that item's icon at the top left. The client knew neither: a private image's `AttachedTo` names only a map entity (a tank on the
back is none, so the bar hung on 0), and an action button's look is the item's picture as an overlay, not the item.

### 36.2 Wire format

A private image (section 13) ends with [varuint holder] [string holder name] (string as section 2, null for none, at most
`Limits.MaxImageHolderNameBytes`): the atom of the image's `loc` wherever it is and its `name` (the server cuts a longer name). A
negative holder is `BadValue`.

A screen entry's look byte (section 20) has bit 3: [varuint action target >= 1] after the children, the world id of tg's action button's
`linked_action.target` when that is an atom (an item action's item). Bit 3 with a target of 0 is `BadValue`.

### 36.3 The client

`ProgressBars.Label`: a bar's label is its holder's name with a capital, or "Action" when it hangs on the own mob or nothing named.
`ProgressBarLayout.Place`: a bar whose holder is the item of a shown action button (`HudLayer.ActionButtonOf`) goes under that button, as
wide as it, without text; every other bar stands in the column at the top edge's middle, 100 by 14 text-scale units, with its label at
the left and its percent at the right on a line above it, the label cut with an ellipsis to the room the percent leaves.

### 36.4 Tests

`ProgressBarCodecTests` (the round trips, the bytes of a target alone, the refusals), `CodecV4Tests.PrivateImagesRoundTripWithTheirLayout`
(the image bytes), `Client/Interaction/ProgressBarLayoutTests` (the labels, the column without overlaps, the bar under its button),
`Integration/ActionButtonGameplayTests` (the real server: the tank's button names the tank, its volume bar names the tank as "oxygen tank"
and is placed under the button, and is gone when the internals close).

## 37. Version 28 (D160: free flight, DV68 and DV69)

### 37.1 The own motion's drift

The private block's motion (section 4) packs a seventh flag in its flags byte: bit 6 (64), a weightless capsule's drift follows the move delay:
[varint drift east] [varint drift north], subunits a tick (1/256 tile), each at most `Limits.MaxTilesPerTick` tiles a tick either way
(`MotionParams.DriftX`, `DriftY`). Bit 6 with a drift of zero, or a component out of range is `BadValue` (a bit above 6 was too until version 37 defined bit 7, section 46). Without bit 6 the
drift is zero. The controller (`MotionStep`) moves a `ZeroG` capsule by its drift in a tick its wish moves it not (no key, or the gate refuses).

### 37.2 Ghosts

Nothing new on the wire: a ghost is a capsule of the locomotion bridge (DV68), so its private block carries its motion (with `NoClip`) and the
replicator sends its continuous position as any capsule's.

### 37.3 Tests

`Integration/FreeFlightGameplayTests` (the real server: a ghost flies at an angle through a wall a fraction of a tile a tick, predicted; a
weightless body drifts a tenth of a tile a tick), `Client/Play/ZeroGViewTests` (the own body predicted drifting and flying),
`Integration/ZeroGPredictionTests`, `Integration/ExtinguisherSprayTests` (a spray's push in space).

## 38. Version 29 (the eye blur: the game plane)

### 38.1 The rule

tg blurs a player's whole game view by a filter on its hud's game plane master controller (`hud.plane_master_controllers[PLANE_MASTERS_GAME]`,
`_onclick/hud/rendering/plane_master_controller.dm`), which forwards it to the game plane masters: the eye blur
(`status_effects/debuffs/screen_blur.dm`) adds a gaussian blur of size `clamp(seconds left * 0.05, 0.6, 3)` there each second and removes it at
its end (a drug's colour or wave, a heretic's rays use the same controller). The port translates the controllers (`hud.dm:105`) and their
`add_filter` and `remove_filter` overrides; the plane masters themselves are a seam (not-translated.md section 3), so the controller keeps
its own filters (tg's `. = ..()`). The server sends the game controller's appearance, with those filters, to its player.

### 38.2 Wire format

The private block's flags byte (section 4) has bit 6 (64): [tableref appearance], a varuint `(index << 1) | private` as every table
reference (section 2), 0 for none: the game plane master controller's appearance in the session's private (or the public) table, carrying
its filters as any appearance (sections 34, 35). The block carries it when it changed since the last block (an appearance hint of the
controller, which a change of its filters gives, the screen sweep, another controller); a Snapshot's block carries it whenever the
controller has filters. A controller without filters (or a mob without a hud) is none (`AppearanceTables.InternPlane`: unlike an entity's, the
plane's appearance is kept for its filters alone). A reference outside its table
is `BadValue`; a flags byte with a bit above 6 still is.

### 38.3 The client

The replica keeps `ReplicaWorld.GamePlane` (none until a block names one, and none after a Snapshot that does not) and resolves its filters
(`GamePlaneFilters`). `ViewBlur.Sigma` (engine-free) turns the blur filters into the standard deviation of one gaussian in screen pixels: a
blur of size s is a deviation of s / 2 BYOND pixels (D156), two blurs add as variances, and BYOND's view is 15 tiles of 32 pixels high, so
the deviation scales by the view's height over 480. `Render/ViewBlurLayer` (canvas layer 1: under the HUD, the windows and the crosshair,
over the 3D view) draws a screen-filling 7 x 7 gaussian of the screen texture at that deviation (`shaders/view_blur.gdshader`), nothing
while there is no blur. The plane's other filter kinds are not drawn (not-translated.md section 24).

### 38.4 Tests

`Protocol/ProtocolV29Tests` (the round trip of the game plane, a none, the refusal of a reference outside its table, the replica's
filters), `Client/Play/ViewBlurTests` (the deviation of tg's sizes on a 1080-pixel view, two blurs, no blur), Core `M6/HeadTraumaTests`
(the controller's filter as the eye blur runs out), `Integration/HeadTraumaGameplayTests` (the real server: a toolbox on the head, the
victim's client's game plane blurred).

## 39. Version 30 (DV75: the T-ray scanner's images on tiles)

### 39.1 The rule

tg's T-ray scanner (`t_scanner.dm:50-62`) makes an `/image` for every under-floor object within three tiles, `image(loc = get_turf(O))` with the
object's appearance at alpha 128, and puts them in `client.images` of its holder for 8 deciseconds (`flick_overlay_global`). A private image
(section 13) knew only the entity it hangs on; the hidden objects are no entities (their `invisibility` is above `SEE_INVISIBLE_LIVING`), so
the image named nothing and the client had no place to draw it. A pulse also shows more than the 64 images a session was allowed.

### 39.2 Wire format

A private image (sections 13 and 36) ends with [varuint turf x] [varuint turf y] [varuint turf z]: the tile of an image whose `loc` is a turf, all
three 0 for an image that hangs on an entity or nothing. Only some of the three being 0, or a coordinate above `Limits.MaxMapSize` (x, y) or
`Limits.MaxMapLevels` (z), is `BadValue`. A session holds at most `Limits.MaxPrivateImages` private images, now 512 (it was 64).

### 39.3 The client

`ReplicaWorld` makes a short-lived visual of every private image on a tile that is not a progress bar (an icon state of the form `prog_bar_NN`, which
the HUD draws): a `ReplicaEntity` with the negated image id as its id, the image's appearance and its tile, announced by the `Spawned` and
`Despawned` events only (it is in no table of entities). The entity layer draws it as it draws a `flick_overlay_view` visual on a turf (section 18). The
server sends an image to its own session only (D40), so another player's client never gets it.

D170 (DV86, no new version): the engineering scanner goggles' pipe connection arrows and thermal overlay are private images on tiles too; their colour, alpha, layer and pixel offsets are in the appearance. The client lays a tile image flat when its layer is a floor layer (`FloorLayers.Rank`: an image made with `image()` has BYOND's `FLOAT_PLANE` (-32767; plane 0 in the fake feed), which counts as the floor plane when the layer is in the topdown block, 10000 and up) and moves it by its pixel offsets.

### 39.4 Tests

`Protocol/ProtocolV30Tests` (the round trip of a tile, the refusal of a partial or out-of-range tile, the replica's visual of an image on a tile and not of
a bar), Core `M7/ToolKitTests` (the pulse of a T-ray scanner), `Integration/ToolKitGameplayTests` (the real server on MetaStation: the holder's client sees
the cables under the floor around it, a second player's does not), `Integration/EngineGogglesGameplayTests` (D170).

## 40. Version 31 (DV81: the mesons' sight and the lighting cutoffs)

(Version 31 is another branch's, the projectile base; section 40 is its too.)

### 41.1 The rule

tg's `/mob/living/update_sight` (`living.dm:2029`) leaves a mob's `sight` flags (`SEE_TURFS` from `TRAIT_MESON_VISION`, `SEE_MOBS`, `SEE_OBJS`, `BLIND`),
its `lighting_cutoff` and its `lighting_color_cutoffs` (percentages: the eyes', the glasses' and the traits' lift of the dark, in all and by colour channel).
tg's plane masters turn them into colour matrix filters of the lighting plane (`render_plate.dm:337`) and BYOND's client draws the turfs of `SEE_TURFS`
through the dark and the walls. The port has no plane masters (rules 13), so the private block names the three values when they change.

### 41.2 Wire format

The private block's flags byte gets bit 7 (128, `OwnFlagVision`; `OwnFlagsAll` is 255). When set, after the game plane (section 38): [varuint sight]
[f32 lighting cutoff] [f32 red] [f32 green] [f32 blue]. The sight is at most 24 bits; each cutoff is in 0 to 100 (NaN, below 0 and above 100 are
`BadValue`, on writing and on reading). A Snapshot's block carries it only when the mob does not see as a person does (sight 0, all cutoffs 0:
`VisionState.Normal`); a Delta's block carries it when it changed since the last block sent to the connection (the server compares the three values of the
connection's mob, raw, clamped to the wire's range).

### 41.3 The client

`ReplicaWorld.OwnVision` holds the last state. `VisionState.LightFloor` is tg's floor per channel, `(cutoff + channel cutoff) / 100` clamped to 0 to 1;
the game scene sets the global shader uniform `anomaly_light_floor` from it and the light grid's include takes `max(light, floor)` (DV81). `SeesTurfs` (sight
bit 16) makes `Render/MesonLayer` draw the turfs of `Shared/Play/MesonView` through the walls. The colour of the glasses is not on this wire: it is a colour
matrix filter of the game plane (section 38), which `ViewBlurLayer` now draws as well as the blur (`ViewBlur.ColorMatrix`).

### 41.4 Tests

`Protocol/ProtocolV31Tests` (bytes, round trip, refusals, the replica's state, the lighting floor, the colour matrix of the game plane's filters), Core
`M7/MesonTests` (traits, sight, cutoffs when worn and removed, the night vision toggle, the tint toggle, client colour blending, tg's `client_colours`
unit test), `Integration/MesonGameplayTests` (the real server on MetaStation: the wearer's client gets the sight, the floor, the walls and floors to draw
and the tint, and loses them with the goggles).

## 42. Version 32 (D169: the energy ball's orbiting mini-balls)

### 42.1 The rule

tg's orbiter component (`datums/components/orbiter.dm:73`) puts an orbiter at its radius with a translated `transform` and turns it
about the orbited thing with `SpinAnimation` (`__HELPERS/visual_effects.dm:83`): one `animate()` chain of as many steps as the orbit's
segments, looping for ever. The orbiter's default and an energy ball's mini-ball one time in five use 36 segments (`orbiter.dm:14`,
`energy_ball.dm:153`), a chain past version 9's 16 steps, which the replicator did not send (section 18.2: its end showed at once, the
mini-ball stood still beside the ball).

### 42.2 Wire format

Unchanged but for the limit: an animation track has 1 to 36 steps (`Limits.MaxAnimationSteps`); a track of 37 or more is `BadValue`, on
writing and on reading. The replicator sends chains of up to 36 steps and still drops a longer one.

### 42.3 The client

`AnimationPlayback` plays the longer chains as any other: each step turns the translated transform by 10 degrees, and the decomposed
interpolation of section 18.3 (rotation, scale, shear and move apart, as BYOND's) draws the orbit as a polygon of the chain's segments,
as tg's comment describes ("less = a more block circle"). The orbiter's turf follows the orbited thing (`move_react`, `abstract_move`),
so the replica moves it with the ball.

### 42.4 Tests

`Protocol/AnimationCodecTests` (the limit's refusal at 37 steps), Core `M7/EnergyBallTests` (the orbit's chain), `Integration/EnergyBallGameplayTests`
(the real server on MetaStation: the ball and its mini-balls in the replica, the mini-balls' animation tracks, their moves with the ball).

## 43. Version 34 (M7x slice A, D177: a shot hits where it lands)

### 43.1 The rule

In first person the crosshair is the aim (DV94): a shot leaves the shooter's eye along the camera's ray and hits the body part its 3D line
crosses (DV95). tg's click params carry a 2D angle source (the clicked icon pixel, the screen-loc); the client now sends its camera's ray
with each click, and BYOND's mouse press, drag and release reach tg's `/client/MouseDown`, `MouseDrag` and `MouseUp` (`_onclick/drag_drop.dm`,
which tg's full auto listens to). The server tells the clients each shot's 3D line, so projectiles and their effects are drawn at their
height (DV78 revised), and a camera shake says whether it is tg's recoil kick (DV96).

### 43.2 Wire format

- The aim (`ShotAim`): `[varint east 0..65536] [varint north 0..65536] [u16 up] [u16 yaw] [i16 pitch -16384..16384]`: the eye in 1/256
  tile (a tile's centre at 256 times its x), its height above the level's floor in millimetres, the direction's compass yaw and pitch
  (65536 a turn, the pitch positive up).
- A Click's modifiers byte: bit 7 says an aim follows its icon-y (bits 0-2 the modifiers, bits 4-6 DV30's aim zone, as before).
- `MouseDown` (15), `MouseDrag` (16), `MouseUp` (17): `[u8 target kind 0..3] [target unless 0] [u8 button 1..3] [u8 modifiers, bit 7 aim]
  [u8 icon-x 0..32] [u8 icon-y 0..32] [aim if bit 7]`. Kind 0 is nothing under the crosshair. `ActionKinds.Last` is `MouseUp`.
- The entity's `Shot` field (`NetField.Shot`, bit 19; the entity record's flags bit 7, the change mask's bit 16): `[varint x 0..8192]
  [varint y 0..8192] [i16 height, 1/16 pixel] [i16 slope, 1/4096] [u16 angle, 65536 a turn clockwise from north]`: the line's start in
  absolute tile pixels (32 to a tile, a tile's centre at 32 times its x), its height there, height pixels per horizontal pixel, the heading.
  The height at a map point p is `height + slope * |p - start|`. A shot never loses the field.
- `CameraShake`: a kind byte after the id, `0` tg's `shake_camera`, `1` tg's `recoil_camera`.
- Limits: `MaxAimCoordinate` (65536), `MaxShotCoordinate` (8192), `MouseDragRate` (10 a second, burst 10).

### 43.3 The server

`ApplyClick` and the mouse actions check the aim's eye against the mob's own position (the capsule's, else its tile's centre): within
0.75 tile and between the floor and the highest eye (1.9 m standing, 0.8 m lying), or the server's own (1.3 m, 0.4 m lying) replaces it
(`Session.AimsCorrected`). The aim joins tg's params as `anomaly-aim=EAST,NORTH,UP,YAW,PITCH` (tile pixels and degrees), which reaches
`aim_projectile` through tg's own chain. A MouseDrag takes no token of the overall action bucket (its own, `MouseDragRate`; over it, or
replaced by a newer pending drag, it is dropped silently); a press and a release take only the overall bucket. The mouse actions' target
is checked as a click's; a forged or stale one is nothing under the mouse (a release must reach tg: it stops the full auto). MouseUp ends
the press's drags (tg's `drag_start` and `drag_details` reset, no MouseDrop: the cursor mode's Drop is the drop, DV45). The replicator
sends `Shot` for a projectile and the effects a shot left (its tracer, muzzle and impact, `/obj/effect`), from the port's line vars.

### 43.4 The client

A click on the world carries the camera's aim (the third person's from the own eye toward what its ray hit). A left press sends MouseDown
just before its Click (tg's full auto sets `click_intercept_time` on the press, which drops that click), drags while the button is held
(at most every 0.1 s) and MouseUp on the release. A projectile's quad is moved onto its line and both ends of every segment take the
line's heights; an impact's billboard stands with its middle at the shot's height. A Recoil camera shake turns the view (3 degrees per
tile of tg's push: up for the push behind the facing, right for the push to its right), out in the first step's time and back over the
second's with BYOND's back easing.

### 43.5 Tests

`Protocol/ProtocolV34Tests` (bytes, round trips, the Limits' refusals, the replica's Shot), `Protocol/CodecFuzzTests` (the new messages
in the corpus), `Server/CommandLogTests` (the aim in the replay log), Core `M7x/ShotGeometryTests` and `M7x/AimTests`,
`Client/Play/ShotAimTests` (the ray to the aim, the recoil turn, the server's frame choice against the client's), and
`Integration/AimedShotGameplayTests` (two real clients: a head shot, a shot past the body to the wall, the client's Shot heights, the mouse
actions and a refused eye, and the server's body-part grid against the client's `BodyZoneAim` on the replica).

## 44. Version 35 (M8 slice G, D187: an image that replaces)

### 44.1 The rule

tg's hallucinations show a client what is not there: a delusion makes a man look like a monkey, an invisible effect is hidden from all but one
viewer. In BYOND that is an `image` with `override = TRUE` in the client's `client.images`: the client draws it in place of the picture of the atom
it hangs on. Version 34 sent the private images (section 4 of the images, D128) without the flag, so an image was always drawn over its atom.

### 44.2 Wire format

Each private image entry (`OwnBlock.ImagesAdded` and `ImagesChanged`) gains a last byte: `[u8 override 0 or 1]`. Any other value is refused. An
override image may draw nothing: its appearance index is 0 (public, none) and it hides the atom it hangs on; an image without the flag still names a
real appearance. The replicator also sends an image that hangs on an effect the viewer cannot see (a hallucination's invisible holder,
`INVISIBILITY_OBSERVER`) as an image on that effect's tile (`TurfX`, `TurfY`, `TurfZ`, section 39), since the client has no such entity (DV112).

### 44.3 The client

The replica makes a visual of an override image as of any image on an entity and marks it `ReplicaEntity.IsOverride`; the entity layer does not
draw the picture of the entity it names while the image lives and draws the image as a billboard where the entity stands, at its scale (DV110).
The own body is never drawn in first person, so an override on it shows nothing.

### 44.4 Tests

`Protocol/ProtocolV35Tests` (bytes, round trip, the refused flag, an empty override image).
`M4aServer/P6/ReplicaWorldV4Tests` also applies empty overrides in snapshots and live updates, including transitions back to a visible image;
`ReplicatorV4Tests` sends an empty override through the actual replicator and codec to its owner's replica while checking session privacy.

## 45. Version 36 (M8y slice D, D230: the radial's Buckle and Stop pulling)

### 45.1 The rule

A context hint (section on ContextHints, version 4) names a click: tg's screentip keys are all clicks. The radial's Buckle (tg's drag of
oneself onto a chair or a bed) and Stop pulling (tg's stop-pulling keybinding, `KeybindingCode.StopPulling`) are not clicks on the target, so
a hint carries what its radial entry sends (`ContextHintAction`, `MessagesV36.cs`): `Click` (0, every tg entry), `DropSelf` (1: a Drop
action of the own mob onto the target, drag time 255) or `StopPulling` (2: the keybinding).

### 45.2 Wire format

Each hint gains a byte after its modifiers: `[u8 button 1..3] [u8 modifiers 0..7] [u8 action 0..2] [str(64) label]`. Any other action is
refused; `Limits.MaxContextHintsMessageBytes` grows by one byte a hint.

### 45.3 The server and the client

The host's screentip sink adds `RadialExtras.Of(viewer, target)` after tg's entries (interaction.md 6.3, DV139): Buckle (`DropSelf`, left),
Unbuckle (a left click), Pull (a ctrl left click), Stop pulling (`StopPulling`). The client's `RadialModel.Build` makes them `RadialKind.DropSelf`
and `RadialKind.StopPulling` entries; the scene sends the Drop or the keybinding. Opening the radial calls `PlaySession.Rehover`, so the hover
of the same target goes out again and its fresh hints replace the open radial's entries when they differ.

### 45.4 Tests

`Protocol/ProtocolV36Tests` (bytes, round trip, the refused action), `M4aServer/P6/CodecV4Tests` (the layout), `Client/Interaction/ModelTests`
(the radial kinds), `Integration/DragGestureGameplayTests` (the entries on MetaStation).

## 46. Version 37, slice C (M8z, D239: standing on tables)

### 46.1 The rule

tg lets a mover with `TRAIT_ON_CLIMBABLE` (given by a `climb_walkable` structure's turf to a dense mob that entered it: a table, a closed
crate) pass every `climb_walkable` structure (`can_allow_through`). The capsule's solidity stays `LocomotionSeam.IsSolid` (a table is solid);
a capsule whose mob has the trait (`MotionParams.OnClimbable`) is not stopped by a tile solid only through such structures
(`LocomotionSeam.IsClimbWalkable`: the turf is not dense and every dense non-border movable on it has the element). The client predicts the
same from two new bits.

### 46.2 Wire format

- A MapGrid cell's flags byte (section on version 3) gains bit 6 (64), `CellFlags.ClimbWalkable` (`MessagesV37.cs`), in the flag runs and
  in `CellFlagsChange`; bit 7 stays undefined and refused.
- The private block's motion flags byte gains bit 7 (128), `OnClimbable`; every bit of the byte is now defined.

### 46.3 The server and the client

The replicator's `FlagsOf` sets the bit from `ILocomotionView.IsClimbWalkable` (the seam's predicate); the bridge reads the trait into both
the step's parameters and `NextParams`. `ReplicaWorld.IsClimbWalkable` reads the bit; the replica's and the predictor's collision views
answer `ICollisionView.IsClimbWalkable`, and `MotionStep` wraps the view of an `OnClimbable` capsule in its `ClimberView`. The renderer
draws a standing mob on a climb-walkable or solid tile, and places the own eye, at the top of the structure model there (DV154).

### 46.4 Tests

`Protocol/ProtocolV37ClimbTests` (the bit, the flag's round trip), `Motion/ClimbMotionTests` (the controller, walking past tables
unchanged), `Integration/ClimbGameplayTests` (MetaStation: no correction while a climber walks over two tables and steps down).

### 46.5 Slice B (D238: the radial's Strip)

#### 46.5.1 The rule (slice B)

A context hint (section 45) names what its radial entry sends. Version 37 adds one action: `ContextHintAction.DropOntoSelf` (3), a Drop of
the hinted mob onto the own mob, which is tg's drag of a mob onto oneself (`/datum/element/strippable`'s `mouse_drop_onto`: the strip menu).
The hint's wire layout is version 36's; the action byte is `0..3` and a decoder or encoder refuses any other. The other slices of M8z add
their own parts to this version (`MessagesV37.cs` and the sections below).

#### 46.5.2 The server and the client

The host's screentip sink adds `RadialExtras.Of(viewer, target)`'s Strip (a human other than himself, in reach, when `should_strip` and
`can_perform_action(FORBID_TELEKINESIS_REACH | ALLOW_RESTING)` agree; DV151) as a left-button hint with `DropOntoSelf`. The client's
`RadialModel.Build` makes it a `RadialKind.DropOntoSelf` entry; the scene sends a Drop (drag time 255, a long drag) whose source is the hinted
target and whose receiver is the own mob (the health doll in first person), and the server's strip element opens the target's `StripMenu`
tgui window (D236: `ui_data` carries each item's `icon` and `icon_state`, DV150).

#### 46.5.3 Tests

`Protocol/ProtocolV37Tests` (bytes, round trip, the refused action), `Client/Interaction/ModelTests` (the radial kind),
`Integration/StripGameplayTests` (the entry on MetaStation and the whole strip).

### 46.6 Slice D (D244: the resist key and the verbs)

`KeybindingCode` gains 10 `Resist` (tg's `/datum/keybinding/living/resist`, B: its press resists and lights the resist button, its
release unlights it), 11 `VerbSleep` and 12 `VerbSuccumb` (not tg keybindings: the hidden verbs `mob_sleep` and `succumb`, which a tg
player runs from the verb panel or the command bar; the client sends them from the radial opened over a turf). `KeybindingCode.Last` is
12; a decoder refuses 13 and above. The server runs code 11 and 12 on their press only, as `ClientSeam.Keybinding` calls the verb proc
with `usr` the client's mob. Tests: `M4aContracts/ProtocolV4ContractTests`, `M4aServer/P6/CodecV4Tests`, `HostV4Tests`,
`Integration/PointVerbHudGameplayTests`.

## 47. Version 38 (M9 slice A, D246-D257: speech)

### 47.1 The rule

tg's say window (tgui-say) talks to the client's `/datum/tgui_say` with `window.send_message(type, payload)` both ways. The port's window is
the Godot client's (D250, `Hud/SayWindow.cs` over `Shared/Play/SayModel.cs`, a port of TguiSay.tsx); its messages to the server are a new
action kind and the datum's messages to it a new server message. Runechat (tg's `/datum/chatmessage`) ends on the server at the finished
maptext, which the server sends to each hearer; the client measures, stacks and fades it (DV160).

### 47.2 Wire format (`MessagesV38.cs`, `Codec.V38.cs`)

- `ActionKind.Say` (18, `ActionKinds.Last`), client to server, Control: after the action's head, `[str(1..16) type]` (open, close,
  thinking, typing, entry, force, save), `[str(0..16) channel or none]` (Say, Radio, Me, OOC, Admin, Pray), `[str(0..3072) entry or none]`
  (at most 1024 UTF-16 code units, tg's `MAX_MESSAGE_LEN`), `[u8 visible 0..1]` (1 only on thinking). A Say action may be up to
  `Limits.MaxSayActionBytes` long; every other action keeps `MaxActionMessageBytes`, and the decoder refuses a longer one as TooLong.
- `SayModal` (id 19), server to client, Control: `[u8 command 1..4]` (props, force, save, close); props adds `[varuint max length 1..1024]`
  `[u8 light mode 0..1]`.
- `Runechat` (id 20), server to client, Control: `[varuint target >= 1]` (the entity the line hangs on, tg's `message_loc`),
  `[str(1..4096) maptext]`, `[varuint lifespan 1..600]` (deciseconds).

### 47.3 The server

The inbound cap is chosen after the message id: an Action may be `MaxSayActionBytes` long, every other message keeps the 256 bytes of
`MaxInboundMessageBytes` (LiteNetLib's 8 inbound fragments hold it). A Say takes its own bucket (`Limits.SayRate`, 10 a second, burst 20)
outside the overall action bucket; `ValidAction` checks its type, channel and entry. In the command phase `ClientSeam.Say` hands it to
`client.tgui_say.on_message(type, payload)` with tg's payload keys (channel, entry, visible), `usr` the player's mob; the command log records
it with an `s` extra (the type and channel in hex, the flag). The message sink's `SayModal` and `Runechat` (the presentation seam) go to a
per-session queue sent with the chat after the tick; a runechat line over a turf or an entity the replica does not show is dropped.

### 47.4 The client

`GameClient` raises `SayModalReceived` and `RunechatReceived` (`IWorldFeed`, default accessors). The keys T, Y, M, O and P (tg's
communication keybindings) open the window on Say, Radio, Me, OOC and Pray; while it is open its text field holds the keyboard and the
movement keys are not read. `RunechatView` draws the lines over the speakers (DV160).

### 47.5 Tests

`Protocol/ProtocolV38Tests` (bytes, round trips, the caps), `Client/Play/SayModelTests` (tg's ChannelIterator and ChatHistory tests, the
window's messages and timers), `Client/Play/RunechatModelTests` (finish_image_generation), `Integration/SpeechGameplayTests` (MetaStation:
the indicators, say, shout, whisper, OOC, force say, a door, a ghost).

## 48. Version 39 (M9 slice B, D256, D258: emotes)

### 48.1 The rule

tg runs an emote from a typed "*key", the `me` verb, its emote keybindings (`/datum/keybinding/emote`, one per emote, unbound by default) and
its emote panel. The typed emote and `me` already travel as say window entries (section 47); the emote wheel (D256) is tg's emote keybinding
of the picked key, and its "More..." runs tg's hidden verb `emote_panel`, which opens the emote panel's tgui window (section 23).

### 48.2 Wire format (`MessagesV39.cs`, `Codec.V2.cs`)

- `ActionKind.Emote` (19, `ActionKinds.Last`), client to server, Control: after the action's head, `[str(1..32) emote key]`, lowercase ASCII
  letters, digits and underscores (`EmoteKeys.IsValid`); untargeted.
- `KeybindingCode.VerbEmotePanel` (13, `KeybindingCode.Last`): a Keybinding action's code, the hidden verb `/client/verb/emote_panel`, as
  the hidden verbs Sleep and Succumb are (version 37).

### 48.3 The server

An Emote takes the say window's bucket (`Limits.SayRate`), outside the overall action bucket: a typed "*key" is the same emote.
`ValidAction` checks the key's form; whether an emote has it is tg's `/mob/proc/emote`. In the command phase `ClientSeam.Emote` presses the
key's `/datum/keybinding/emote` (made at the key's first use, linked to the first emote of `GLOB.emote_list[key]`; a key no emote has
does nothing), `usr` the player's mob; code 13 runs `client.emote_panel()`.

### 48.4 The client

J (`PlayAction.EmoteWheel`) opens the wheel (`Shared/Play/EmoteWheel.cs` over `RadialModel`, drawn by `Hud/Radial`): held, the release
over a segment sends `PlaySession.Emote(key)`; tapped, it waits for a left click or Esc. "More..." sends the keybinding code 13; the panel is
the translated `EmotePanel.tsx` (`Shared/Tgui/Interfaces/EmotePanel.cs`, DV163).

### 48.5 Tests

`Protocol/ProtocolV39Tests` (bytes, round trips, the key's rules, the code), `Client/Play/EmoteWheelTests` (hold, tap, click, the
segments), `Client/Tgui/EmotePanelTests` (the list, the filters, the acts), `Integration/EmoteGameplayTests` (MetaStation: the wheel's wave
seen in range and over the speaker, the cooldown, the scream's sound, a typed "*jump", a custom emote, the blush overlay, "*help", the
panel's nod, the deathgasp).

## 49. Version 40 (M9 slice E, D251, D262: the PDA and the chat window's radio)

### 49.1 The rule

The big chat window's Message button (D251) opens the player's PDA messenger: it is a keybinding code, as the hidden verbs are (version 37),
whose server side finds the messenger of a PDA the mob carries and opens it as tg's `choice=open` link does. The window's Radio list needs
the channels the worn headset has: a State message the server sends when they change, as tg's radio verbs read `radio.channels`.

### 49.2 Wire format (`MessagesV40.cs`, `Codec.V40.cs`)

- `KeybindingCode.OpenMessenger` (14, `KeybindingCode.Last`): a Keybinding action's code.
- `RadioChannels` (21, server to client, State channel): `[u8 count 0..32] { [str(3) prefix] }*`, each prefix tg's say prefix of a channel
  (`":e "`: a colon, a lowercase ASCII letter or digit, a space; `RadioPrefixes.IsValid`), none twice, in the headset's channels' order
  (tg's `GLOB.department_radio_keys`); empty with no headset or all its channels off (`Limits.MaxRadioChannels`, `MaxRadioChannelsBytes`).

### 49.3 The server

Code 14 runs `ClientSeam.OpenMessenger`: the first messenger among the PDAs in the mob's `get_all_contents`, opened with its `Topic`
(`choice=open`), `usr` the player's mob; without one the player reads "You have no PDA.". The replicator (`Replicator.RadioChannels.cs`)
compares a player's prefixes each tick and queues the message on a change and on join; the session sends it after the camera shake.

### 49.4 The client

`ReplicaWorld` keeps `RadioPrefixes` and raises `RadioChannelsChanged`; the chat window's "Radio ▾" lists Common and the prefixes and puts the
pick at the input's start (`ChatInputModel.SetRadioPrefix`); its Message button sends code 14. The messenger's lines carry the chat type
"pda" (DV171), the Messages tab's.

### 49.5 Tests

`Protocol/ProtocolV40Tests` (bytes, round trips, the prefix rules, the code), `Client/Tgui/NtosWindowsTests` (the NtOS windows),
`Client/Play/ChatLogTests` (the Messages page), `Integration/PdaGameplayTests` (MetaStation: the imprinted belt PDAs and the manifest, the
Message button, a message through telecomms and the message server, the ring, the Reply link's text input, the Radio list's ":e ", the
server silent with the telecomms room's power off).

## 50. Version 41 (M9 slice F, D263: the offering)

### 50.1 The rule

tg offers the held item with its give keybinding (`/datum/keybinding/living/give`, G); the port sends it as a Keybinding action's code, as
the other tg keybindings (section 14).

### 50.2 Wire format (`MessagesV4.cs`)

- `KeybindingCode.GiveItem` (15, `KeybindingCode.Last`): a Keybinding action's code, tg's `living/give`.

### 50.3 The server

`ClientSeam.KeybindingType(15)` is `/datum/keybinding/living/give`: its `can_use` and `down` (`living.give()`), `usr` the player's mob.
The taker takes the item by clicking tg's give alert, a screen object (section 23's clicks).

### 50.4 The client

Until the UI overhaul's package 2, slice E, the client's G key (`PlayAction.GiveItem`) sent code 15; the client no
longer has that key (G is the crafting menu, the offer is the radial's "Offer item", DV139), and the code stays on
the wire.

### 50.5 Tests

`Protocol/ProtocolV41Tests` (the bytes, the code, the code past it refused), `M4aContracts/ProtocolV4ContractTests` (code 15 is tg's give),
`Integration/OfferingGameplayTests` (MetaStation: a crowbar given and taken, the high-five, the slap, a blown kiss, the hand that lets the
giver pull, the circle game, a noogie), `Integration/GrabGameplayTests` (the grabs up to a strangle, the resist, the throw).


## 51. Version 42 (M10 slice B: the capsule mobs' positions on the unreliable channel)

### 51.1 The rule

The Delta travels on the reliable ordered State channel: a lost packet holds every later Delta back until its retransmission. At 100 ms
each way and 2% loss (D38's test) the replica waited for that 28 times a minute, up to 400 ms (`Integration/LossyNetworkTests`), and
the others' movement froze for longer than the client's 100 ms of render delay. The positions and yaws of the capsule mobs, the data
that changes every tick, therefore move to a Motion message on the unreliable Input channel, sent every tick and covering its own losses
by resending: every capsule mob whose position or yaw changed in the last `Limits.MotionResendTicks` (20) ticks is listed with its
values as of the tick. The doors, items and every other field stay in the Delta (multiplayer.md section 1 B: measured first, the rest of
the Delta stays reliable).

### 51.2 Wire format (`MessagesV42.cs`, `Codec.V42.cs`)

- `MessageId.Motion` (22): `[u8 22] [varuint tick] [varuint count] { [varuint id step] [u16 pos x] [u16 pos y] [u8 yaw] }*`, ids strictly
  increasing (the step is the id minus the previous one; the first is the id), positions in 1/256 tile on the map, at most
  `Limits.MaxMotionBytes` (1000, under the unreliable channel's 1023); a tick with more entries sends several messages.
- A Delta's change record no longer carries `Pos` and `Yaw` once the entity is a capsule: the record in which it becomes one carries them,
  as do spawn records and the Snapshot. A tile crossing carries `X`/`Y` and `LastMoveTick` without `Pos`.

### 51.3 The server

`Replicator.Examine` stamps a capsule mob's `Pos` and `Yaw` changes (`ChangedAt`) without putting them in the record and notes the tick
(`Replicator.Motion.cs`); a despawn forgets it. After each session's Delta the host sends its Motion messages (`BuildMotion`): the same
entries for everyone, except that an observer-layer entity (a ghost, section 31) is listed only to the sessions whose client holds it
(D40). The own mob is listed too (the replica's own entity keeps its server position; the predictor reconciles from the private block).

### 51.4 The client

`ReplicaWorld.Apply(MotionMessage)`: an entry for an entity the replica holds applies when its tick is newer than that entity's last
Motion (the unreliable channel may reorder) and not older than its spawn or Snapshot; an id it does not hold is skipped (its spawn may
still be on the reliable channel). The sample ring stays in tick order: a Motion ahead of the Delta pushes its tick, and a Delta change
of an older tick then lands on the newest sample. `MotionTick` and `NewestTick` (the newer of the Delta's and the Motion's) drive the
render clock (`PlaySession`), so the others keep moving while a Delta is held back. The bots apply it too.

### 51.5 Tests

`Protocol/ProtocolV42Tests` (the bytes, the round trip, the id order, the replica's newer, older and unknown entries, the sample order),
the codec and fuzz tests, `Replication/ReplicatorV2Tests` (a capsule's first record carries `Pos` and `Yaw`, later changes only the Motion,
the 20 resends and then none), `Replication/ReplicatorV22Tests.AGhostsMotionReachesOnlyTheSessionsThatSeeIt` (D40), the V2 oracles (the
replica equals the world every tick with the Motion applied), `Integration/LossyNetworkTests` (100 ms and 2% loss: own walking with no
correction, the walker's Motion at most 100 ms apart in the other player's replica while the Delta waits up to 400 ms).

## 52. Version 43 (M10 slice E: what the client needs to guess its own hands and doors)

### 52.1 The rule

D38 stage 1 (multiplayer.md section 8): the client shows the outcome of a few instant actions at once and lets the server's next word
replace its guess. It needs to know when the replica holds that word, which doors its player may open, and what an inventory box is.

### 52.2 Wire format (`MessagesV43.cs`, `Codec.V43.cs`)

- **The private block**, after the input buffer depth: `[varuint actions done] [u8 flags: bit 0 access]`, then with bit 0
  `[count ≤ 256] { [string ≤ 64 bytes, not empty] }*`. `ActionsDone` counts the connection's Action messages the server has decoded and no
  longer holds (applied in their frame's tick, or dropped: rate limits, dev commands off, invalid targets); Actions are reliable and
  ordered, so the client's n-th Action is settled, and its result in the same block's replica state, once it reaches n. The access list
  is the own mob's `get_access()` (the IDs held and worn, through `COMSIG_MOB_RETRIEVE_ACCESS`), sent when it changes and in every Snapshot.
  An empty block is now 4 bytes (`Codec.EmptyOwnBlockBytes`).
- **Door** (`NetField.Door`, bit 20; last in the change mask's wire order): `[u8 flags: 1 openable, 2 emergency] [u8 unres_sides]
  [count ≤ 32] { [string] }* (req_access) [count ≤ 32] { [string] }* (req_one_access)`, on `/obj/machinery/door/airlock` only. Openable
  is tg's conditions for a bump or a hand to open it now, read from vars (door.dm `Bumped`, `try_to_activate_door`; airlock.dm
  `bumpopen`, `open`, `try_to_force_door_open`): closed, not operating, welded, bolted, sealed or emagged, openable by hand, the ID scan
  and the open wire whole, powered (main or backup), no cycling pump, no locked elevator door. Electrified is not in it: a shock is the
  client's surprise as it is in tg. A spawn record flags it through its extension byte: the record's flags bit 128 now means
  `[u8 ext: 1 shot, 2 door]` follows the visual host, then the shot, then the door.
- **Screen entries**: look bit 4 `[varuint slot_id] [u8 held_index]` last (tg's `atom/movable/screen/inventory` and its `hand`);
  look bit 5 (with children) puts `[varuint slot_flags << 3 | storage << 2 | equip delay << 1 | item]` after each vis child's look
  (the item tg puts in a box's `vis_contents`: its `slot_flags`, `equip_delay_self > 0`, `atom_storage`).

### 52.3 The server

`Session.ActionsReceived` counts each decoded Action; `PrivateInputs.ActionsDone` is it minus the pending ones. The host reads every
joined player's `get_access()` inside the world after the step (`ServerHost.ReadAccess`; a getter, the replicator itself still calls no
proc) and passes a new list only when it changed; the replicator sends it when the reference changed, and always in a join.
`ReplicatedFields.WireDoor` reads the door's vars with the entity (an airlock's state changes update its icon, which hints it).

### 52.4 The client

`ReplicaWorld` keeps the server's state and lays the guesses over it (`ReplicaWorld.V43.cs`): its own string and appearance entries past
`LocalBase` (1 << 30, beyond any table's limit), guessed screen entries (`Screen` merges them; the server's messages on a guessed id raise
nothing until it is cleared), hidden entities (`TryGet` and `Entities` leave them out; their despawn is raised once), door pictures
(`GuessedDoorState`) and cells passable from a frame on (`IsSolid` answers by `GuessFrame`, which the predictor sets through
`ICollisionView.SteppingFrame` before each step and replay). A Snapshot drops every guess. The guesses themselves are `OwnGuesses`
(multiplayer.md section 8).

### 52.5 Tests

`Protocol/ProtocolV43Tests` (the bytes, the round trips and the refusals of the three parts; the replica's local entries, a guessed screen
entry under the server's messages, a hidden entity's single despawn, a cell passable from its frame; tg's `check_access_list`), the
codec tests' new empty block, `Integration/PredictionGameplayTests` (MetaStation at 100 ms each way and 2% loss).

## 53. Version 45 (M10 slice H: the round's restart)

`DisconnectReason.RoundRestart` (15): tg's `world/Reboot` ended the round. BYOND reboots the world and every client reconnects; the
port's host builds a fresh world in-process (D15) and closes each connection with this reason and the text "The round has ended; the
server is starting the next one." The host keeps the players' reconnect tokens, so a client connects again with its token
(`JoinAttempt`: the connect screen does it by itself after this reason, and retries a `NotReady` refusal every 2 s, up to 60 times,
while the new world boots) and joins the new round as a new player. Nothing else changes on the wire.

## 54. Version 46 (M10 slice I: bans)

`DisconnectReason.Banned` (16): tg's `world/IsBanned` refused the connection request (a server ban of the key or of the address the
transport reports), or an admin's ban closed the session. A refusal's text is tg's ban description (who is banned, the reason, the ban's
id, admin, time and round, and when it expires), cut to `Limits.MaxDisconnectTextBytes`; a closed session's text names the banning
admin, after tg's notice in chat. Nothing else changes on the wire: the admins' commands travel as `ActionKind.DevCommand` lines like the
console's (apps.md 1.6b).

## 55. Version 47 (the sky's sun)

The Delta ends its cell changes, after the ceiling cells, with the sun: `[varuint]` 0 without one, else 1 + tg's `SSsun.azimuth` in
hundredths of a degree (0 to 35999; a larger value is refused). Every Delta carries it, so a joining client has it from its first Delta;
the replica keeps it as `ReplicaWorld.SunAzimuth` (NaN until known) and the client's sky draws the sun there (DV196). Tests:
`Protocol/ProtocolV47Tests`, `Integration/SolarGameplayTests` (the sun the replica holds is where the panels turned).

## 56. Version 51 (M10x slice C: the observer's eye and the double click; the push speed)

**The eye.** tg's `do_observe` (`observer.dm:890`) sets a ghost's `client.eye` to the mob it observes and shows that mob's HUD on the ghost's
screen (`show_hud(version, viewmob)`; the observed mob's `client.screen` objects reach the ghost's private block as every screen object,
their HUD group read from the observed mob's `hud_used`). The private block's version 43 flags byte gains bit 1 (2): after the access list,
`[varuint eye]`, the world id of `client.eye` when it is a mob other than the own mob, else 0 (the own mob, a turf, a locker, a mech). It is
sent when it changed and in every Snapshot (`OwnBlock.Eye`, null when unchanged); a negative eye is refused when writing, an undefined
bit of the flags byte when reading. The replica keeps it as `ReplicaWorld.EyeEntity` (0 for the own view); while it names a drawn entity
the client's camera looks through it (DV201). Interest already follows `client.eye` (M10 slice D).

**The double click.** `ActionKind.DblClick` (20, `ActionKinds.Last`): BYOND's `DblClick` on a target, with a mouse action's fields (section
43: the target, the button, the modifiers, the icon pixel; a target is required, no aim, no aim zone). The client sends it after the
second click of a double click (`DoubleClick`, DV202); the server checks its target as a click's, takes only the overall action bucket (its
press's Click took the click bucket) and calls the target's `DblClick` (tg's `usr.DblClickOn`; a ghost follows a movable or jumps to a
turf, `_onclick/observer.dm:1`) through `ClientSeam.DblClick`.

**The push speed.** The own capsule's motion parameters end with `[varuint push speed]` (`MotionParams.ImpulseSpeed`, subunits a tick as
the drift's, at most `Limits.MaxTilesPerTick` tiles; more is refused both ways): without a drift, the speed of the drift a push from rest would start
(version 50's `InertiaDelayFromRest`, tg's 1-newton `get_loop_delay`), sent only where the push surely drifts whatever its direction
(`LocomotionSeam.PushDrifts`: weightless, no stabilizers, nothing to push off or a lit jetpack's `TRAIT_NOGRAV_ALWAYS_DRIFT`), else 0.
Version 50's speed from rest comes only with a gravity mask, for a step off a floor; this one is for a push in open space. The predictor
starts that drift along its own push (DV177), so the first push from rest no longer corrects by the drift of every frame in flight (about
a tile at 500 ms; found by the slice H quick run's lossy drift test).

**Chat links without quotes.** Not a wire change: tg's `FOLLOW_LINK`, `TURF_LINK` and `FOLLOW_OR_TURF_LINK` (`__DEFINES/say.dm`) write
`<a href=byond://?...>` without quotes. The host's `ChatLinks` and the client's `HtmlText` take an unquoted href up to a space or the tag's
end, so dead chat's (F) and (T) links are links (section 33).

Tests: `Protocol/ProtocolV51Tests` (the eye's bytes and round trip, the replica keeping it, the double click's round trip and refusals, the
unquoted links, the push speed's round trip and range), `MotionPredictorTests` and `SpaceDriftTests` (the push speed predicted, sent
only where it drifts), `Integration/ObserverOrbitGameplayTests` (MetaStation: the eye and the observed HUD reach the ghost, a double click jumps
and follows, the death notice's (F) link follows).

## 57. Version 52 (M10x slice G3 and G4: the escape menu's window)

Not a new message: tg's escape menu is a raw tgui window (`datum_tgui_window("escape_menu")`, the lobby menu's shape) at the reserved
index `Limits.EscapeMenuUiWindow` (`MaxUiWindows - 1`, 199) with the interface `Limits.EscapeMenuUiInterface` (`"EscapeMenu"`); the
pooled tgui windows stop below it. The client draws it as its Esc menu (`Hud/EscMenu.cs`, DV205) and never as a window, and its acts
are the page's `action` messages (`ServerHost.ApplyPageAct`). An older client would show it as an unknown window, hence the version.

## 58. Version 53 (M10x slice G5: bug reports)

**BugReport** (id 23, client to server, State channel): one part of the Esc menu's bug report, `[varuint total] [varuint offset]
[varuint length] [bytes]`, with a total of 1 to `Limits.MaxBugReportBodyBytes`, a part of 1 to 4096 bytes inside it, sent in order from
offset 0 (`Codec.EncodeBugReportParts`). The body is `[str title 1..300] [str what happened 1..8000] [str steps 0..8000 or none]
[str client 0..1024 or none] [varuint image length 0..2 MiB] [image]` (`BugReportMessage`; the client lines are its version, OS, GPU,
preset and window). Parts, because the server's LiteNetLib takes no client message over 8 fragments (`MaxInboundFragments`, about
8 KB), which keeps a hostile client from making it hold megabytes; a 720p screenshot is about 75 KB, some 20 parts. The client sends
nothing else on State, so a report holds back no command on Control.

The host (`ServerHost.BugReports.cs`, DV206) checks a report at its first part: a server with neither `BUG_REPORTS` nor
`BUG_REPORT_WEBHOOK_URL` says it does not collect them, and a key's next report within 2 minutes (`Limits.BugReportCooldownTicks`) is
refused with the wait; a refused report's other parts are skipped. Accepted, its body grows with the bytes that came (never by the
claimed total), a part out of order is a strike, and the whole body is decoded at its last part. An image that is not a WebP (the
RIFF container and a VP8, VP8L or VP8X frame header of 1 to 16384 pixels a side) is dropped and the report kept. The host adds the
key, the time, its build, the round, the map, the mob, its area and tile, saves `report.txt` and `screenshot.webp` in
`BUG_REPORTS/DATE/ROUND-N/` and posts both to the Discord webhook off the host thread (no mentions), then tells the player in chat.

Tests: `Protocol/ProtocolV53Tests` (the parts and body round trip, the caps, a part under the 8 fragments, the WebP check),
`Integration/BugReportGameplayTests` (saved and posted to a local webhook, the 2-minute refusal with its parts skipped, a junk image
dropped), `Server/ServerConfigTests` (the options).

## 59. Version 54 (D285: the held motion)

**HeldMotion** (id 24, server to client, State channel, after the tick's Delta): `[u8 kind]`, 0 a load (a magazine or rounds went in),
1 an unload, 2 a rack (the bolt, slide or pump). tg's ballistic gun reports its user's motion to `HeldMotionSeam` (DV213); the
replicator keeps each user's last motion of the tick and sends it to that user's session only. The first-person view model dips the
gun on a load or unload and jerks it back on a rack; before, the client guessed a reload from tg's sound files.

Tests: `Protocol/ProtocolV54Tests` (the round trip, an unknown kind refused).

## 60. Version 55 (M10x slice G6: tg's browse() windows, the admin help)

Not a new message: tg's other `browse()` pages (`usr << browse(html, "window=NAME;size=WxH")`: the ticket panels and lists of
`adminhelp.dm`, `/datum/browser`) are raw tgui windows (DV216). The core writes them into `client.dm_tgui_output` as
`TguiOutputKind.Browse` records (`ClientSeam.Browse`: the window's name, the page, its `<title>` and size); the replicator gives each
name of a player an index from `Limits.BrowseUiWindowFirst` (`EscapeMenuUiWindow - 16`, 183) up, the oldest page's index when all 16
are open, and sends an update with the interface `Limits.BrowseUiInterface` (`"Browser"`) and the data `{"html": [pieces of at most
MaxUiStringBytes], "width", "height"}`; `browse(null, "window=NAME")` closes it. tgui's pooled windows stop below the range. The page's
links are tg's chat links (version 24): the host records them as it records a chat line's (DV59), so a click is a Topic. The client draws
the page (`Interfaces/Browser.cs`: the head, styles and scripts left out) as tg's chat draws its lines (`UiKind.Html`, `HtmlText`,
`Hud/ChatText.cs`); its close button forgets the window on the server (`Replicator.ForgetBrowse`), as BYOND's window closes without
telling tg. tg's Adminhelp window is an ordinary tgui window (`Interfaces/Adminhelp.cs`). An older client would show both as unknown
windows, hence the version.

Tests: `Integration/AdminHelpGameplayTests` (the player's ticket, the admin's reply link and the player's answer, the player panel and the
ticket list and panel as browse() windows drawn by the client, the resolve, the webhook with no admin on).


## 61. Version 59 (gaps C2: the pets' commands)

`KeybindingCode.ViewPetCommands` (16, `KeybindingCode.Last`): tg's `/datum/keybinding/living/view_pet_data` (`keybinding/living.dm:240`, default Shift). The client
sends its press and its release (`up`); while it is held, hovering a friendly pet (the existing Hover action, tg's `MouseEntered`) opens the pet's commands in the
existing radial menu. Nothing else changes on the wire.

## 62. Version 60 (D305: Lavaland's sky)

MapGrid ends, after version 44's ceiling cells, with the levels that have tg's `ZTRAIT_NOPARALLAX` (`__DEFINES/maps.dm`; Lavaland's
`ZTRAITS_LAVALAND`): `[varuint: count][varuint: z]*`, the count at most the map's levels, each z from 1 to the levels and higher than
the one before. The server reads them from `SSmapping.z_list` when it builds the join map. On those levels the client draws its own
dark sky in place of the starfield and no sun or light of space (DV256). Nothing else changes on the wire.


## 63. Version 62 (D306: the host's lobby between rounds)

A round's end no longer closes the connections with `DisconnectReason.RoundRestart` (version 45). When tg's `world/Reboot` asks for a
new round, the host closes each live session in the old world and opens a fresh session of the same key on the same connection, which
waits between rounds while a worker thread builds the next world. On the Control channel it gets a `ServerText` of type
`anomaly_between_rounds` (`BetweenRounds.Type`; the html is the plain status line) and, when the ended round had a title track, one of type
`anomaly_lobby_music` (the html is tg's sound path, `SSticker.login_music`). The client leaves the world (`FeedPhase.BetweenRounds`),
ignores the old world's last State and Input messages and sends nothing; whatever a waiting session sends is dropped without a strike. Once the new world opens its
joins, every waiting session's Join is queued as a reconnect's is: the same connection gets a second join sequence (Welcome with a new
session ordinal, MapGrid, Snapshot), which the client applies to a fresh replica, into the new round's lobby.

## 64. Version 63 (D307: text to speech on the client)

`TtsMessage` (`MessageId.Tts` = 25, server to client, Control channel): one line this player's client voices itself (tg's
`SStts.play_tts` plays a TTS server's audio; the port's clients synthesize, DV262):
`[varuint: speaker][string: text][string: voice][varint: pitch][varuint: volume]`. The speaker is the entity the line comes from, 0 for
the player's own voice preview (tg's local `queue_tts_message`, played unplaced); the text is the line as this player understands it
(`translate_language`), at most `Limits.MaxTtsTextBytes`; the voice one of `SStts.available_speakers` (at most 64 bytes); the pitch tg's
preference, -12 to 12 semitones; the volume tg's (85 for a listener, 60 for the speaker, plus the line's offset), at most 200. The server
sends a line to each listening mob's client within tg's range, and to the observers of the level, as tg's `play_tts` picks them. The
client voices it only when its Settings > Speech option is on, its model is downloaded and the line's language is one the player picked.

## 65. Version 64 (D308: the radio's lines and tg's voice filters)

`TtsMessage` ends with `[string: filter][byte: flags]`. The filter is tg's ffmpeg filter chain of the line (the speaker's `voice_filter`,
a worn mask's, the line's `tts_filter` such as the megaphone's, joined by commas), at most `Limits.MaxTtsFilterBytes`, or null. The flags
(`TtsFlags`, unknown bits refused): `Radio` 1, a broadcast played from the radio that receives it (the entity of its holder that the
replica shows); `Common` 2, on the common frequency; `Own` 4, the listener's own line; `Silicon` 8, tg's "silicon" special filter;
`Clicks` 16, tg's "radio" special filter on a spoken line. The client applies the filters, voices radio lines by its radio options and
frames radio lines with tg's clicks.

## 66. Version 65 (D312: whispers and shouts)

Two more `TtsFlags`: `Whisper` 32, a whispered line (the client voices it without its pitch and fades it out at 4 tiles; the server
sends it 35 quieter, to those within tg's whisper and eavesdrop range); `Yell` 64, a shouted line ending in `!!` (the client says it higher,
faster and strained and fades it out at 30 tiles; the server sends it 15 louder, out to the shout's 14 tiles). A radio broadcast of a
whispered headset line is not marked `Whisper`.

## 67. Version 66 (DV263: the movement feel)

The own motion's state carries `[varint: velX][varint: velY]` after the press timer: the walking capsule's velocity in 1/65536 tile a
tick (`MotionState.VelX`, `VelY`, at most `Limits.MaxTilesPerTick` tiles a tick either way; more is refused both ways). Its parameters end
with `[varuint: accelTicks][varuint: decelTicks][float: backSlow][float: strafeSlow]` (`MotionParams.Feel`, `MoveFeel`: ramps 0 to 40
ticks, slow-downs 0 to 0.9, finite; refused otherwise). The predictor steps with both (docs/ai/specs/locomotion.md section 2), so a
correction of a ramped walk never comes from the client not knowing the velocity the server had.

## 68. Version 67 (D315: the minimap)

**Minimap** (id 26, server to client, State channel, after the tick's Delta): `[varuint z][varuint minX][varuint minY][varuint width]
[varuint height]`, the colour table `[count][u32 rgba]...` (`Limits.MaxMinimapColours`), the cells' colour runs (count, then
`[varuint cells][varuint colour index]` pairs, x fastest, then y; 0 is a cell tg did not draw), the areas `[count][string name][string
code]...` (`Limits.MaxMinimapAreas`; the code is the area's department, `CMD`, `SEC`, `MED`, `SCI`, `ENG`, `CRG`, `SRV`, `MNT` or empty)
and the cells' area runs (1 is the first area, 0 none). The picture is tg's `load_z` (`modules/minimap/minimap.dm`) of that level, cropped
to what it draws: no larger than the map, its runs covering its cells and their values within the tables (refused otherwise). The server
makes a level's minimap once a round (`MinimapSeam`, outside the world's step, DV266) when a session's mob first stands on that level and
sends it to each session once per level and join; a level that tg draws nothing of (space, the reserved levels) has none. The client
keeps the levels' minimaps of its join (`GameClient.Minimaps`) and draws the own level's (`Hud/MinimapPanel.cs`).

Tests: `Protocol/ProtocolV67Tests` (the round trip, a picture off the map, runs past its cells and an index past its table refused),
`Integration/MinimapGameplayTests` (a join gets its level's minimap once, every drawn cell named after its area; Meta's departments),
`Minimap/MinimapSeamTests` in Core (tg's colours, the areas, the world's state hash unchanged).

## 69. Version 68 (D315: the minimap's dev trackers)

**MinimapMobs** (id 27, server to client, State channel, after the tick's Delta): `[varuint z][count]` then per mob `[varuint x][varuint
y][u8 player 0|1][string name]` (`Limits.MaxMinimapMobs`, names up to `Limits.MaxMinimapMobNameBytes`; a mob off the map or a player flag
above 1 is refused). The living mobs of the session's level but its own mob (tg's `GLOB.alive_mob_list`, read raw after the tick),
`player` for a mob with a ckey, sent every 10 ticks while the session's dev command `minimapmobs on` holds (dev commands on, or an admin
with `R_DEBUG`; `minimapmobs off` stops it). The client keeps the last one (`GameClient.MinimapMobs`) and, with the dev menu's View tab
toggle, draws a dot per mob on the minimap and the nearest one's name under the cursor (tg's `get_hover_text` of a blip).

Tests: `Protocol/ProtocolV68Tests` (the round trip, a mob off the map, a bad flag and a trailing byte refused),
`Integration/MinimapGameplayTests` (the mobs of the level but the own while on, none after off, none from a server whose dev commands are
off), `Client/Play/MinimapLayoutTests` (the hover's nearest mob).

## 70. Version 69 (DV347: the belts around the own mob)

**OwnBlock motion parameters** end with the running conveyor belts around the own mob (`MotionParams.Belt`, a `BeltField`): `[varuint
speed]`, subunits a tick (0: none, nothing follows; at most `Limits.MaxTilesPerTick` tiles a tick), then `[varuint x][varuint y]` (the
field's centre tile, the mob's) and four `[varuint word]`s: the 9 x 9 tiles' 3-bit codes (0 none, 1 north, 2 south, 3 east, 4 west), tile
`(dy + 4) * 9 + dx + 4`, 21 tiles a word from bit 0. A code above 4, a word's 64th bit, a bit of the last word past tile 80, or a field
without a speed is refused. The server sends the belts a capsule mob rides (`LocomotionSeam.BeltsAround`: tg's convey component would carry
it and its capsule walks); the client's prediction steps its capsule with them as the server does (`MotionStep.Belt`), so a ride is
predicted without a correction.

Tests: `Protocol/ProtocolV69Tests` (the round trip, the refusals, the capsule step on and off a belt), `Integration/ConveyorGameplayTests`
(riding and walking on a belt at 100 ms each way: no correction).

## 71. Version 70 (D320: the mouse wheel and the build preview)

**Action MouseWheel (21)**: BYOND's `MouseWheel` over the aimed thing, a mouse action's fields (`[u8 target kind 0..3] [target unless 0]
[u8 button] [u8 modifiers] [u8 icon-x] [u8 icon-y]`, no aim from the client) whose button is the turn: 1 up (tg's `delta_y` 120), 2 down
(-120). It takes the click bucket. The server calls tg's `/atom/MouseWheel` of the target (over nothing: the mob's turf, as BYOND's map
always has one), which calls `usr.MouseWheelOn` and the RPD's `mouse_wheeled`: the wheel picks its layer, with Shift it turns the next
build (DV352). The client sends it in first person and in the third person with Shift (the plain wheel zooms the orbit view there).

**BuildPreview (28)**, server to client, State, after the tick's Delta when it changed: `[u8 set 0|1]`, then if set `[str(256) icon]
[str(256) state] [u8 dir 0..10] [u32 colour RGBA] [i8 pixel x] [i8 pixel y] [u8 reach 1..7]`. It is what the item in the mob's active hand
would build (`obj_item.build_preview`; the RPD in its Dispense mode: the chosen recipe's window preview, its paint colour, shifted to the
first active layer, reach 1). An empty icon, a dir over 10 or a reach outside 1..7 is refused. The client keeps the last one
(`GameClient.BuildPreview`) and draws it as a ghost on the aimed tile (`BuildGhostPlacement`: a turf or a thing's tile on the own level
within the reach; `Render/BuildGhost`: tg's picture flat on the floor, tinted and pulsing).

Tests: `Protocol/ProtocolV70Tests` (the round trips, the refusals, the ghost's tile), `M7/RpdTests` (the turn, the layer, the preview),
`Integration/RpdGameplayTests` (the pump turned east with Shift and the wheel on MetaStation, the preview following it).

## 72. Version 71 (D320: the build preview's tile)

**BuildPreview (28)** ends with the tile the build would go to, `[varuint x 1..255] [varuint y 1..255] [varuint z 1..80]`, in place of
version 70's `[u8 reach]`; a tile off the map is refused. The server decides it from what the player hovers (tg's `MouseEntered`,
`client.dm_mouse_over`) as a click there would: the RPD's preview is none out of reach, on a thing the click would take away, unwrench or
reprogram (a loose fitting with Destroy on), where its category cannot build, or when every chosen pipe layer conflicts (tg's fitting
`wrench_act` check run dry: `obj_item_pipe.placement_conflict`); otherwise it is on the first free chosen layer. The client draws the ghost
only while its crosshair aims at that tile (`BuildGhostPlacement.Shows`), so it never lags on a tile the player looked away from.

Tests: `Protocol/ProtocolV70Tests` (the tile's round trip and refusals, the ghost's tile), `M7/RpdTests` (the preview shows exactly where a
click builds: a taken layer, a second chosen layer, a loose fitting with and without Destroy, out of reach), `Integration/RpdGameplayTests`
(the preview on the hovered floor).

## 73. Version 72 (DV357: the wind and gravity pulls around the own mob)

**OwnBlock motion parameters** end with the space wind around the own mob (`MotionParams.Wind`, a `BeltField` laid out as version 69's
belts: `[varuint speed]`, then the centre tile and four words; a tile's code is the wind's direction there, the speed this mob's push) and
a gravity pull: `[varuint speed]` (subunits a tick, 0: none, nothing follows), then `[varuint x][varuint y]`, the tile it draws the mob
toward. The refusals are the belts'; a speed above `Limits.MaxTilesPerTick` tiles a tick is refused. The server sends them while the wind
or a pull carries a walking capsule mob (`LocomotionSeam.Carried`); the client's prediction steps its capsule with them as the server does
(`MotionStep.Blow`: along the wind of the tile under the centre, toward the pull's tile centre, added to the own wish), and smooths an error
of up to 3 tiles while either is set (a change of the wind's strength moves the server's state by the change times the frames ahead).

Tests: `Protocol/ProtocolV72Tests` (the round trip, the refusals, the capsule carried, leaning against the wind, a pull to its centre, a
runner the wind pins to a wall), `Integration/DisplacedPredictionTests` (SSair's wind and a pull on MetaStation at 100 ms each way: no
snap, no shake, no tick standing still).

## 74. Version 73 (DV360: plastic flaps)

**MapGrid and Delta cell flags**: bit 128 is `CellFlags.Flaps`, the cell has tg's plastic flaps that refuse a standing mob
(`LocomotionSeam.IsFlaps`); every bit of the flags byte is now defined. **OwnBlock motion parameters** end with `[u8 passes flaps]`
(0 or 1, anything else is refused): `MotionParams.PassesFlaps`, the mob passes such flaps standing (`LocomotionSeam.PassesFlaps`). A
standing capsule without it treats a flaps cell as solid on the server and in the prediction; a lying one does not.

Tests: `Protocol/ProtocolV73Tests` (the bit and the flag round-trip), `Motion/FlapsMotionTests` (standing stops, lying, passing and
leaving go through), `Integration/MispredictReproTests.WalkingIntoAirtightFlapsAndCrawlingUnderThemIsPredicted` (no correction walking
into them, crawling under them, walking through the cold room's flaps).

## 75. Version 74 (gaps W1: world maptext)

**Appearance entries in a public table** may carry `maptext` (its offsets and box were already on the wire): the world's atoms show
their maptext to every client (tg's status displays, the ticket machine's number). **AnimatedVar** bit 128 is `MaptextX` (`[f32]`,
after the transform; NaN or infinity is refused): a chain may animate an entry's `maptext_x`. **A child track** (`TrackChild`) may now
animate its child's own vars, not only a filter: the server replicates a vis_contents child's maptext chains (tg's status display's
marquee, `animate(maptext_x = ..., loop = -1)` on its shared text object) on each holder that shows the child, as tracks naming the
flattened child's row (`Replicator.WithTextChains`). The client draws each text box on a quad from a page rendered with the HUD's
maptext renderer (`Render/WorldMaptext.cs`, `MapRenderer.Maptext.cs`): on a model's face (its base sprite's opaque bounds mapped onto
the mesh's front), on a billboard in its picture; an alpha mask filter on the entry clips it to the mask's opaque bounds, and a
`MaptextX` track scrolls it.

Tests: `Protocol/AnimationCodecTests` (the v74 round trip, a NaN refused), `Protocol/ProtocolV18Tests`
(`APublicEntryWithMaptextRoundTripsSinceVersion74`), `Protocol/ProtocolV26Tests` (a child's own-vars track),
`Integration/StatusDisplayGameplayTests` (the logo, the red alert and its revert, a message's two lines, the scrolling line's track and
speed, where the client draws them).

## 77. Version 76 (D325: multi-z stacks)

**MapGrid** ends with the levels linked to the level below them (tg's `ZTRAIT_DOWN`): `[varuint: count][varuint: z]*`, ascending, each
from 2 to the map's levels (a level out of order, the lowest level or one past the map is refused). A level and the one under it are one
multi-z stack (`ReplicaWorld.IsLinkedBelow`, `SameStack`). The client draws every level of its own stack, each at its height, and lights
it from textures that hold every level of the stack, a block of rows each (`anomaly_light_grid_levels`); open space draws nothing, and a
cell under a floor or wall of the level above gets that turf's underside as its ceiling. The server's interest holds the chunks round
the eye on every level of the eye's stack.

Tests: `Integration/MultiZGameplayTests` (the links on the join map, an entity of the other level held, a walker falling through open
space).

## 78. Version 77 (DV371: level rides)

An entity's `Ride` field (`NetField.Ride`, bit 21; `RideState`, MessagesV77.cs): a level change that tg makes after a wait, announced when
the wait starts: an elevator's car and riders (`linear_controller.move_after_delay`), a ladder's climber over its do_after. On the wire
`[s8 dz]` (-1, 0 or +1) and, when dz is not 0, `[zigzag varint end tick - message tick] [varuint ticks]` (ticks 1 to 65535). In a change
record it follows Door; in an entity record it is bit 4 of the extension byte. The server sends none again when the ride ends, in the
tick the level changes (or the climb is given up, or the rider steps off). The client draws the entity crossing the level over the
ticks (`LevelRise`); any other level change of a thing that stays about where it is (a fall) eases from where it was drawn.

Tests: `Protocol/ProtocolV77Tests`, `Client/Play/LevelRiseTests`, `Integration/MultiZGameplayTests` (an elevator's ride and a ladder's
climb drawn without a jump and with no misprediction).

## 79. Version 81 (DV416: free motion)

An entity's `Flight` field (`NetField.Flight`, bit 23; `Anomaly.Core.Motion.FlightArc`, Codec.V81.cs): one segment of a movable's free motion, sent when it starts (a throw, a hit, a bounce, a fall to another level, a landing, a push of the air): from `StartTick` (ticks with a fraction) the point is (`X0`, `Y0`) tiles plus the velocity times the time, `Lift0` tile pixels (32 to a level) above level `Z`'s floor plus `VLift` times the time less half `Gravity` times its square; a horizontal speed under `Friction` slows at that rate until it stops. A segment with no speed and no gravity is a rest point, kept while the thing lies there. On the wire `[varuint z]` (0: none) and then `[f32 start tick - message tick] [f32 x0] [f32 y0] [f32 lift0] [u8 moving]` and, when moving, `[f32 vx] [f32 vy] [f32 vlift] [f32 gravity] [f32 friction]` (finite, on the map's scale, no negative gravity or friction). In a change record it follows Tram; in an entity record it is bit 16 of the extension byte. The server polls a movable while it moves and once after each change. The client draws the segment under way at its render time (`ReplicaEntity.FlightAt`: the replica keeps the last six it replaced), its lift counted in the drawn height so a fall through a level is no jump; when it ends the tile glide starts from the point last drawn.

The **Throw action** (`ActionKind.Throw`, 24, D328): throw what the hand holds at the target, with a click's fields (the target, the icon
pixel, the aim zone on a mob, the camera's aim) on a plain left click (any other button or a modifier is refused). The server runs it as tg's
click in throw mode whatever the throw mode is (`ClientSeam.Throw`: the mob's throw mode is tg's hold for that click and back after), so tg's
ClickOn makes every check of a throw; it takes the click's rate limit. The client sends it on its one-key throw (R) and the radial's Throw at.

Tests: `Protocol/ProtocolV81Tests`, `Tg/Flight/FlightTests`, `Integration/FlightGameplayTests`.

## 80. Version 82 (D329: putting things down)

The **PutDown action** (`ActionKind.PutDown`, 25, DV418): put what the active hand holds down at the crosshair's point on the target, with a
click's fields (the target, the icon pixel, the camera's aim) on a plain left click (any other button or a modifier is refused, as is a
screen target). The server (`ClientSeam.PutDown`) runs tg's placing on an unflipped table in reach with the point where the aim meets its top
as the click's pixel, puts the item on a free floor in reach at the aim's point (tg's `transfer_item_to_turf` with that pixel offset), and
otherwise runs tg's `drop_item` keybinding (the feet). It takes the click's rate limit. The client sends it on its drop key (Q), and tg's
drop key when the crosshair names nothing in the world.

Tests: `Protocol/ProtocolV82Tests`, `Integration/PutDownGameplayTests`.

## 81. Version 83 (DV422: the level keys)

The keybinding codes 17 (`ZLevelUp`, Space) and 18 (`ZLevelDown`, Ctrl), press and release: tg's `zlevel_upwards` and `zlevel_downwards`
(`mob.up()`, `mob.down()`) for a mob that can change level (`ClientSeam.ChangesLevel`); for any other mob 17 is tg's `hold_throw_mode`
(its release lets go) and 18 does nothing. They replace the dev command `up me` / `down me` the client sent for PageUp and PageDown.

Tests: `Integration/LevelKeyGameplayTests`.

## 82. Version 84 (gaps E2a: the weather)

The **Weather message** (`MessageId.Weather`, 30, server to client, State, after the tick's Delta; `MessagesV84.cs`, `Codec.V84.cs`): the
weather running on the level the player's mob stands on (tg's `SSweather.processing`), sent when what it says changes and once when the mob
comes to a level (an empty list clears it). Each layer (at most 16) is one weather: an id stable while it runs, tg's stage (1 the telegraph,
2 the main stage, 3 the wind down), the stage's overlay state of `weather_effects.dmi` (`ash_storm`, `rain_high`, `snow_storm`,
`dust_high`, `lava`, ...; empty when tg draws none), its colour (RGBA, 0 for the state's own) and alpha, whether tg also draws its glow, and
for the particle weathers their particle set, severity (0 to 100) and wind sign; then the covered cells of its impacted areas on the level
as runs of 0 and 1 over their bounding box, x fastest. The server gathers a weather's cells once per level (tg fixes the areas when it
makes the weather) and encodes the message again only when its stages, looks or severities change. The client draws each layer as a haze
marched along the view rays through the covered cells (`weather.gdshader`) and, near the camera, its rain, snow, ash or sand
(`weather_particles.gdshader`), shown only inside the covered cells.

Tests: `Protocol/ProtocolV84Tests`, `Integration/WeatherGameplayTests`, `Gaps/WeatherE2aTests` (core).

## 83. Version 86 (DV503: the radial's own actions)

A context hint's action may be 4, **Extra** (`ContextHintAction.Extra`): a port action of the radial that is no click, today the table's
Flip and Flip upright (`RadialExtras`), which tg does with the empty hand's right click and the port only from the radial (the owner,
2026-11-17). Choosing it sends the **ContextPick** action (`ActionKind.ContextPick`, 26): the target as a click's (an entity id or a turf)
and the hint's label (at most `Limits.MaxContextLabelBytes`), no button, modifiers or pixel. The server checks the target as a click's
and runs the entry only when `RadialExtras.Of` offers that label for that target to that mob now (`RadialExtras.Pick`); it takes the
click bucket.

Tests: `Protocol/ProtocolV86Tests`, `Gaps/LeanAndTableTests` (core).

## 84. Version 87 (D336: a map's own sky and sun)

**MapGrid** ends with the map's own look (`MapLook`, `MessagesV87.cs`, `Codec.V87.cs`), after the linked levels of version 76:

```
[u8 0: the game's own sky and sun | 1: the map's]
the map's: [u8 sky 0..3] [u24 overhead 0xRRGGBB] [u24 horizon] [f32 stars 0..4] [varuint picture length 0..4194304] [bytes]
           [u8 sun 0|1] [u24 colour] [f32 energy 0..8] [f32 size 0.2..30] [f32 elevation 1..90]
```

The sky is 0 space with the game's planet, moon and rocks, 1 the stars alone, 2 two colours (overhead and at the horizon), 3 a picture: a
png or a jpg file's bytes, a panorama laid around the map (`Limits.MaxSkyImageBytes`, 4 MiB; only the picture sky has one, and a picture
sky whose file the server has not got sends none and is drawn as space). `stars` is their brightness (1: the game's own). The sun byte
says whether the map has a sun at all (its light, its disc, its flare); its colour, the strength of its light, the radius of its disc in
degrees and its height over the map's plane in degrees follow. A value outside its range, a picture that is neither, and a picture with
another sky are refused at both ends. The server reads them from the map json's `anomaly` key (`MapSettings`, docs/ai/specs/map-editor.md
5.10) when the map is mounted; a map without the key sends the one byte. Where the sun stands is still the Delta's azimuth (version 47): a
map that fixes its sun fixes tg's `SSsun`.

The same version raises the levels of a world to 80 (`Limits.MaxMapLevels`; 16 before): every z of a message is 1 to 80. A map's own
levels are at most 64 (`MapDocument.MaxLevels`); the rest are the levels a round adds (CentCom, the mining levels, the reserved ones).

Tests: `Protocol/ProtocolV87Tests`, `Client/Editor/PlayWorldTests` (a round's client has the map's sky and sun).

## 85. Version 88 (DV527: lag compensation)

A **ShotAim** (version 34) ends with `[varuint seen]`: the render tick the client drew the world at when it aimed, in 1/16 tick, in the
replica's tick numbers (`ShotAim.Seen`; 0: not said). The client stamps it on every aim it sends (`PlaySession.MakeTargeted`: its
`RenderTick`). The server takes it as how far behind its world the client drew the others when the command is applied, cut to 12 ticks
(`ShotRewind.MaxTicks`), and tests the aimed shot against the walking mobs where they stood then (DV527). The command log keeps it as a
sixth aim field when it is not 0.

Tests: `Protocol/ProtocolV88Tests`, `Protocol/ProtocolV34Tests`, `Integration/AimedShotGameplayTests` (server), `M7x/RewindTests` (core).

## 86. Version 89 (DV534: handholds within reach)

The own motion parameters (the private block, after version 73's `PassesFlaps` byte) end with `[u8 handhold]`, bits 1 and 2 only:
bit 1 `MotionParams.HaltsAtHandhold`, with a drift: tg's drift handler halts it where the mob can grab something (`after_move`'s
`Process_Spacemove(continuous)`: a player's mob without a lit jetpack's `TRAIT_NOGRAV_ALWAYS_DRIFT` or DV412's blow); bit 2
`MotionParams.SteersByHandhold`: the weightless mob steers only by something to push off (no jetpack stabilizer, no pull). A handhold
counts within `MotionMath.HandReach` of the capsule's centre (half a tile, 1 m; DV534): a solid cell or a lattice. The client's predictor
then lets the wish move the capsule only with a handhold in reach, starts the drift of `ImpulseSpeed` on a step that leaves the last one
behind, and halts a drift once one comes in reach, with input blocked until about tg's `block_inputs_until`.

## 87. Version 89, also (DV179: blocked hands)

The private block's version 43 flags byte has bit 6 (64), with no bytes of its own (`OwnBlock.HandsBlocked`, `OwnState.HandsBlocked`): the
own mob's clicks are refused now by tg's `ClickOn` before they reach what was clicked (`INCAPACITATED_IGNORING(INCAPABLE_RESTRAINTS |
INCAPABLE_STASIS)`: stunned, paralyzed, asleep, in crit; or `TRAIT_HANDS_BLOCKED`: cuffed). The server reads it inside the world once a
tick with the access (`ServerHost.ReadAccess`). The client then guesses no click's outcome (`OwnGuesses.Click`: no pick-up, no item taken
to a hand, no airlock opened by a click), which the server would refuse a round trip later.

Tests: `Protocol/ProtocolV89HandsTests`, `Integration/PredictionGameplayTests` (server).

## 88. Version 91 (the owner, 2026-11-20: the server's wall height)

`MapGrid` ends with `[u16 wall height in millimetres, 2000..4000]` (`MapGrid.WallHeightMillimetres`): the height of every level's walls and
ceiling, the server's `wall_height` setting (`ServerConfig.WallHeight`, the world's `LevelHeight`). The client builds its world with it
(`WorldScale.SetWallHeight`) and has no wall height of its own any more (D119 revised), so the server's level height, where its shots,
throws and pointers meet a ceiling and pass to the next level, is the one the client draws.

Tests: `Protocol/ProtocolV91WallHeightTests` (server).

## 89. Version 92 (DV96, the owner, 2026-11-21: the shooter's crosshair)

`GunSight` (server to client, State channel, id 31, after the tick's Delta when it changed): `[u8 gun 0|1] [f32 cone 0..90, 0 without a gun]
[u8 hits]`. `gun`: the own mob's active hand holds a gun (`obj_item_gun`); `cone`: the half angle in degrees of the cone that gun's shots scatter
in at most (`GunSight.ConeOf`: tg's largest roll of the gun's spread, or of its chambered casing's variance, times the server's
`spread_scale`); `hits`: how many of the mob's projectiles have hit a living thing, wrapping at 256. A join starts from no gun and the
mob's own count. The client draws the shooter's crosshair while a gun is held: four lines whose gap is the cone on the screen at its field of
view, opened by each shot and closing again, and a hit marker when `hits` changes.

Tests: `Protocol/ProtocolV92GunSightTests` (server), `M7x/GunSightTests` (core).

## 90. Version 95 (DV586: the own melee cooldown)

The private block's version 43 flags byte has bit 7 (128) when the own mob's melee click cooldown is not tg's `CLICK_CD_MELEE` (800 ms):
`[varuint milliseconds, 0..60000]` follows the view areas (`OwnBlock.MeleeCooldownMs`, `OwnState.MeleeCooldownMs`; 800 without the bit).
It is the cooldown a melee blow with the active hand would set now, as mob_living's `changeNext_move` takes it: the held thing's
`attack_speed` (an empty hand's `CLICK_CD_MELEE`) plus `next_move_adjust`, times `next_move_modifier`, each with the status effects' own
(`ServerHost.MeleeCooldownMs`, read inside the world once a tick with the access). The client swings its held thing and whooshes on a
click only once that long and a tick have passed since its last swing (`ViewModel.Clicked`), as tg's `ClickOn` takes no blow before its
`next_move`.

Tests: `Protocol/ProtocolV95CooldownTests`, `Integration/MeleeCooldownReplicationTests` (server).

## 91. Version 96 (DV596: the turfs' own lights)

MapGrid ends, after the wall height, and the Delta body ends, after the sun, with the turf lights (`MessagesV96.cs`, `Codec.V96.cs`):
`[count] { [cell] [u8 ceiling 0/1] [light] }*`, a cell and kind once per message. A floor entry (ceiling 0) is the light of the turf
itself, tg's own light vars (`ReplicatedFields.OwnLight` of the turf), switched on and not tg's starlight (space turfs and lights at
`LIGHTING_HEIGHT_SPACE`, which the cells' starlight flag gives). A ceiling entry (1) is the light of the ceiling's floor block (D295) as
`CeilingSystem.LitBlockAt` reads it: a light floor as a working one leaves it (its `currentcolor`, range 3), none when broken. MapGrid lists
every lit one (no light of range 0); a Delta lists the changed ones, range 0 for a light gone out, at most `MaxCellChangesPerDelta`. The
replica keeps them (`ReplicaWorld.TurfLightAt`, `TurfLights`) and raises `TurfLightChanged` after the Delta; a changed cell is also a
changed cell for the map's meshes (a glowing tile is drawn self-lit, `turf_kit.gdshader` condition 5).

Tests: `Protocol/ProtocolV96TurfLightTests`, `Integration/TurfLightGameplayTests` (server).

## 92. Version 97 (DV597: the client loads before it plays)

The Hello's flags gain bit 2, `Hello.WaitsForLoad`: the client loads the world behind a loading screen before it plays (`Codec.cs`,
`HelloFlagWaitsForLoad`). Only the game's own client sets it (the connect screen's `JoinAttempt` and the map editor's Play); the bots, the
test rigs and anything else keep joining at once. A new untargeted action, `ActionKind.Loaded` (27, no fields, the toggle bucket), says the
client has built the map, decoded the round's sprite sheets and warmed its shaders. The server keeps the flag on the session and on every
Join command (`QueuedCommand.WaitsForLoad`, the replay log's `w` extra), and without the lobby `DirectJoins` passes over a session whose
Join said it waits (`Session.AwaitingLoad`) until its Loaded comes, when it is late-joined at once: until then the key is tg's new player
(`/mob/dead/new_player`), with no body. A new round's Join (D306) sets the wait again, since the client builds the next world too. With the
lobby on the server refuses nothing: the client's loading screen covers the lobby's Join and Ready until it is loaded, and a client that
skips it only plays on a world it has not loaded.

Tests: `Protocol/ProtocolV97Tests`, `Integration/JoinGateTests` (server).

## 93. Version 98 (tg's cooking list)

`Limits.MaxUiWindowBytes` is 1 MiB (it was 256 KiB) and `Limits.MaxUiValues` 65536 (it was 32768; section 25): tg's crafting window
(`PersonalCrafting`) sends every cooking recipe at once in its static data, and since the restaurant the cooking list holds tg's
recipes with reagent ingredients and the chemical reactions' recipe guides: about 419 kB and 25,000 values encoded, the crafting tab about
152 kB and 8,300. Nothing else of the `UiWindow` message changes; a version 97 peer would refuse such a window.

Tests: `M5/CraftingTests` (core: the cooking tab's encoded window within the caps), `Protocol/` (server).

## 94. Version 99 (DV685, the owner, 2026-11-26: photos)

`PhotoRequest` (server to client, State channel, id 32, after the tick's Delta): `[varuint photo id >= 1] [varint x] [varint y]
[varint z] [u8 tiles x 1..15] [u8 tiles y 1..15] [u16 width 1..1024] [u16 height 1..1024]`. tg's camera took a picture of tile
(x, y, z), framing tiles x by tiles y tiles; the photographer's client renders one frame from its own eye toward the tile's middle at half
the wall height (`PhotoPose`: a field of view that fits the frame's width at that distance, 30 to 90 degrees; a shot of its own tile looks
along its view, 30 degrees down), at most width by height pixels, with what its own camera draws (no HUD), and answers with the PNG.

`PhotoPart` (client to server, State channel, id 33): `[varuint photo id >= 1] [varuint total 1..MaxPhotoBytes (512 KiB)]
[varuint offset < total] [varuint length 1..4096, offset + length <= total] [bytes]`: the PNG in parts sent in order from offset 0, as the
bug report's (section 57; the server takes no client message over 8 fragments). A part out of order is a strike. The whole PNG goes to the
world (`PhotoSeam.Develop`), which takes it only as the answer to a request it sent that client, within 10 seconds, once, when its own PNG
decoder reads it and it is 16 pixels to the requested size on each side; otherwise the server logs the refusal and the picture stays blank.
A client whose PNG is over the cap shrinks it by a quarter until it fits. The server keeps the client's bytes as a runtime resource
(`rsc/MD5.png`), which every client is sent once as a `RuntimeResource` (section 79's message; `Limits.MaxRuntimeResourceBytes` is now
512 KiB) before the picture's look names it.

The photo window is tg's `browse()` page: `browse_rsc(picture_image, "tmp_photo.png")` makes the page's `<img src='tmp_photo.png'>`
the image's resource, and the client's Html node draws an `<img>` whose source is a runtime image or an icon recipe (`HtmlImages`).

Tests: `Protocol/ProtocolV99PhotoTests` (server), `Gaps/PhotographyTests` (core), `Client/Play/PhotoClientTests` (the pose and the
page's pictures).

## 95. Version 99, also (DV687, DV688: particles and the flame flicker)

An appearance entry (section 3) gains the presence bit 18, `particles`: a table string (the public or the session's private table),
the JSON of the atom's BYOND particle set (`Anomaly.Server.Replication.ParticleSets`): `icon` (the file), `states` (each icon state and
its weight), `color` (`#rrggbbaa`), the numbers `width`, `height`, `count`, `spawning`, `lifespan`, `fade`, `fadein`, `friction`,
`rotation`, `spin`, and the vectors `position`, `velocity`, `gravity`, `drift`, `scale`, `grow` (an array of up to three numbers, or
one number for all axes). A var that is unset or a generator is left out; a set whose JSON passes 1024 bytes is not sent. tg keeps a
particle set on a particle holder in its parent's vis_contents, so it arrives as an overlay entry of the parent's picture. The client
(`ParticleSet`, `Render/ParticleEmitter.cs`) draws it at the picture's middle, BYOND's ticks read as deciseconds and its pixels as tg
pixels of a 2 m tile.

A light state's flags byte (section 13) gains bit 1, the flicker: the light flickers as a flame (tg's `fire_flicker_middleman`: a
lighter, a welder, a flare, the life candle; DV688). The client multiplies the lamp's energy by tg's alpha loop (`FlameFlicker`). A flags
byte with a bit past 1 is refused.

## 96. Version 100 (package T, slice T4a: the eyes, the mouth and the groin by look)

A Click's, a Throw's and a PutDown's modifiers byte: `[shift 1, ctrl 2, alt 4] [bit 3: the zone's high bit] [bits 4-6: the zone's low bits]
[bit 7: an aim follows]`, the zone `((mods >> 4) & 7) | (mods & 8)`: `AimZone` gains 7 eyes, 8 mouth and 9 groin (tg's
`BODY_ZONE_PRECISE_EYES`, `_MOUTH`, `_GROIN`); 10 to 15 are refused (`BadValue`). Every byte of version 99 (bit 3 zero, a zone of 0 to 6) keeps
its meaning, so an older recording replays as it was. A mouse action carries no zone (its bit 3 stays refused); a hover's zone came with slice T4b in the same version (section 97).
The server checks the zone again (`ValidAction`, 0 to 9) and selects it through tg's zone selector before the click or throw on a carbon, as any
other zone (DV30). Examples: `0x71` shift and the eyes, `0x09` shift and the mouth, `0x98` an aim and the groin, `0x29` refused (zone 10).

Tests: `Protocol/ProtocolV100AimZoneTests`, `Protocol/CodecV2Tests` (server), `Integration/BodyZoneRegionTests`, `Integration/MeleeAimTests`,
`Integration/PreciseZoneReplayTests`.

## 97. Version 100, also (package T, slice T4b: the hover's zone)

A **Hover** on an entity may end with `[u8 aim zone 1..9]` after its target: the body zone the crosshair is on (`ActionMessage.AimZone`, the
values of section 96). A hover without a zone has no such byte, so its bytes are version 99's and an older recording replays as it was. A zone
byte of 0 or above 9, a zone after a turf, ceiling or screen target, and any byte after the zone are refused (`BadValue`, `TrailingBytes`).
Examples: `0A 05 07 01 2C` a hover on entity 44 (no zone), `0A 05 07 01 2C 01` the same with the head, `0A 05 07 01 2C 09` the groin,
`0A 05 07 02 0C C8 03 01` refused (a zone on a turf).

The client sends the zone its cached zone map finds under the crosshair (`EntityLayer.AimZoneAt`; none for a thing without body parts) and
sends the hover again when only the zone changed on the same thing, at most `PlaySession.ZoneHoverInterval` (0.125 s) apart, the newest zone
waiting for the interval. The server checks the zone again (`ValidAction`: 0 to 9, an entity target only) and, on a carbon, selects it as tg's
`zone_selected` with `should_log = FALSE` before tg's hover runs, so the ContextHints of the same mob are built again from it; not while any of
tg's do-afters runs for the looker (DV30), when the server keeps the skipped hover and applies it in the first command phase after the do-afters
end (`ServerHost.CatchUpLooks`). In cursor mode the client's hovers carry no zone (`PlaySession.HoverCarriesZone`). The hover keeps its rate limit (`ActionLimits.Hover`, one applied a tick). The command log keeps
the zone as an action's zone field, as a click's.

Tests: `Protocol/ProtocolV100HoverZoneTests`, `Protocol/ProtocolV100AimZoneTests`, `Protocol/CodecV2Tests` (server),
`Integration/HoverZoneGameplayTests`.

## 98. Version 100, also (package T, slice T4c: a lying body's side, the radial's Turn over)

The posture byte (`NetField.Posture`, `PostureBits`) uses its two spare bits: bit 6 (64, `SideFixed`) says someone turned the lying body over
(DV690), bit 7 (128, `FaceUp`) with it says it lies face up, without it back up. They are set only while the body lies; the server forgets the side
when the body gets up. A decoder refuses bit 6 without bit 0 (a side on a standing body) and bit 7 without bit 6; every byte of version 99 (bits
6 and 7 clear) keeps its meaning. Examples: `0x43` lying at 90 degrees and turned back up, `0xC3` the same turned face up, `0x40` and `0x83`
refused. `PostureBits.TurnedFaceUp` reads the side (null when not turned). The byte is sent again when the side changes (the turn is an appearance
hint of the mob, so a mob with no capsule is read too).

Which side of a lying body a client draws (`BillboardMath.ChooseLyingDir`, mirrored by the server's shots in `ShotGeometry.ChooseLyingDir`): the side
it was turned to, from every viewer; else, lying buckled to an object (bits 0 and 5: a bed, an operating table), its front; else the owner's rule
of 2026-10-13 (conscious: its back; limp: by the viewer's side).

The radial's **Turn over** is a context hint of the Extra action (section 83) with the label `Turn over` (`I18n.T`); its pick is a ContextPick of
that label. The server's entry is `RadialExtra.TurnOver` (10), which is not on the wire: the label is. It is offered on a lying carbon on a turf next
to the player, never on himself; the pick takes the click bucket as every ContextPick.

Tests: `Protocol/ProtocolV100TurnOverTests`, `Protocol/CodecV2Tests`, `Client/Render/TurnOverLyingDirTests` (server), `Integration/TurnOverGameplayTests`,
`Integration/BodyZoneRegionTests`.

## 99. Version 100, also (ui-overhaul.md section 10, slice 3A, DV693: the belt hotbar)

**Hotbar** (server to client, State channel, id 34, after the tick's Delta or a join's Snapshot, when it changed or a press was refused;
`MessagesV100Hotbar.cs`, `Codec.V100Hotbar.cs`): the own human's ten hotbar cells, 1 to 7 the worn belt's contents, 8 and 9 the left and right pocket,
10 the ID slot, and the names of the items in the own hands.

```
[u8 flags: bit 0 the bar (a human), bit 1 a belt worn; bits 2-7 zero]
ten cells, cell 1 first: { [u8 state: 0 empty, 1 holding, 2 out] ([tableref picture, may be none] [varuint item entity >= 1] unless empty) }
[str(1..128) the left hand's item name, or none] [str(1..128) the right hand's] [u8 refused cell 0..10]
```

A cell is holding when its item is in the belt or in its slot, out when that item was taken from the bar and is in either hand. The item entity
(slice 4B-2, ui-overhaul.md section 12) is that item, in its cell or out in a hand, so the Tab inventory drags, clicks, quick-transfers and blinks the
item a belt cell holds (two items of one picture are told apart); an empty cell carries none, and 0 in a held or out cell is refused at both ends.
At most `Limits.MaxHotbarBytes` (1 + 1 + 10 x (1 + 5 + 10) + 2 x 130 + 1 = 423 bytes); a cell holding the item 501 with the private picture 5 is
`01 0B F5 03`. The picture is the
item drawn alone, the private row a slot's `ScreenChild` uses (`AppearanceTables.InternVisChild`), interned before the Delta's tables are taken,
so the Delta or Snapshot before the message carries it. Only a human has a bar (tg's quick-equip keys' `ishuman`): every other mob's message is
`HotbarMessage.None`, 15 bytes: the id 34, the flags byte 0, ten state bytes 0, two none names (a 0 byte each) and the refused byte 0. Without the bar no cell holds anything and there are no
names or refusal; without a belt (a belt-slot item with a storage) cells 1 to 7 are empty; an empty cell has no picture; a name is never empty.
Anything else is refused at both ends (`BadValue`). The refused cell is an event: the press of that cell this tick did nothing (the client's
blink of slice 3B); the next message carries 0 again. The server reads the cells after the tick (`Replicator.Hotbar.cs`, `BeltHotbar.Peek`)
and sends them when they differ from the last sent, when the cells' refusal count grew, and in a join.

**HotbarPress** (client to server, an Action of kind 28, `ActionKind.HotbarPress`): `[u8 cell 1..10]` after the kind, no target and no other
field (`ActionMessage.HotbarCell`; a cell on another kind, or 0, 11 and up, is refused). The server checks it again (`ValidAction`), takes it from
the overall action bucket and the keybinding bucket (`ActionLimits.Keybinding`, as tg's quick-equip keys), and in the command phase runs
`BeltHotbar.Press` with usr the player's mob: nothing unless the client's mob is a human, connected, not deleted, and the cell is 1 to 10; the
press's rules (ui-overhaul.md section 10) then move the item with tg's own procs, or count a refusal (dead or incapacitated, no rule applies, or
tg refuses the move). The command log keeps the cell in the action's target field (`QueuedCommand.Target`). Examples: `0A 07 1C 03` a press of
cell 3 in frame 7, `0A 07 1C 00` and `0A 07 1C 0B` refused.

The cells are the port's state on the mob (`mob_living_carbon_human.dm_hotbar`), brought up to date in each command phase for every player's human
(`ServerHost.TrackHotbars`, a replay's too) and before each press, so items that come into the belt take the lowest free cell in the order they
came.

Tests: `Protocol/ProtocolV100HotbarTests`, `Protocol/WireTests` (server), `UiOverhaul/BeltHotbarTests` (core), `Integration/HotbarGameplayTests`,
`Client/Play/HotbarClientTests`.

## 100. Version 100, also (ui-overhaul.md section 12, slices 4B-1 and 4C-1, DV696: the Tab inventory's storages, the other container and the moves)

**Storages** (server to client, State channel, id 35, after the tick's Delta or a join's Snapshot, when it changed or a move was refused; to the
owning session only; `MessagesV100Storages.cs`, `Codec.V100Storages.cs`): the storages the own human carries, in the Tab inventory's order (the back's,
the belt's, one in the suit-storage slot, the left pocket's, the right pocket's, the ID slot's, then those held, by held index), each with its items
in tg's order; then, last (slice 4C-1), the other container: the storage the player has open by tg's own means that is none of the carried ones.

```
[varuint count 0..9] then each entry, in the order of its place (and hand):
  [varuint entity >= 1] [u8 place: 1 back, 2 belt, 3 suit storage, 4 left pocket, 5 right pocket, 6 ID, 7 hand, 8 the other container] ([u8 held index 1..8] for a hand)
  [str(1..128) name] [varuint slots 0..65535] [varuint used 0..65535] [varuint limit 0..65535]
  [u8 flags: bit 0 locked, bit 1 hidden, bit 2 cut; bits 3-7 zero]
  [varuint item count 0..64] then each item: [varuint entity >= 1] [str(1..128) name] [tableref picture, may be none] [varuint count 0..65535]
[varuint refused item, 0 for none]
```

The slots are tg's `max_slots`, the limit `max_total_storage`, used `get_total_weight()` (the summed weight classes of the contents); a number over
65535 (tg's INFINITY) is sent as 65535. Locked is tg's `locked`; hidden is tg's `display_contents` off (a cigarette packet): neither sends items, and
used is 0 (tg shows nothing of what is in them). A storage holds more than 64 items only past tg's own limits; it then sends the first 64 and the cut
flag. An item's count is a stack's amount, 0 for an item that is no stack. A name is the thing's plain name (no article), cut at 128 bytes, never
empty. The picture is the item drawn alone, the private row a slot's `ScreenChild` uses (`AppearanceTables.InternVisChild`), interned before the
Delta's tables are taken, so the Delta or Snapshot before the message carries it. Every entity (a storage's or an item's) appears once, except that
the other container's own entity may also be an item of a carried storage (a box opened inside the backpack); the places are strictly in order, a
place other than a hand once, hands by increasing index; at most 8 carried storages (places 1 to 7, `Limits.MaxStorages`) and at most one other
container (place 8, so always last; 9 entries in all, `Limits.MaxStorageEntries`), which is never locked or hidden and has no held index; anything
else is refused at both ends (`BadValue`, a long name `BadString`, too many entries `CountTooLarge`). Every mob that is not a human sends the empty message: `23 00 00`, 3 bytes. The refused item is an
event: the server's last move of that item, named by this client and its own, did not reach its target (dead or unable to act, tg refused it, the item's
earlier move or a hotbar press still moving it, or a worn item that came off into the hand and the target then refused); the next message carries 0 again.

The server reads the storages after the tick from that session's own mob (`Replicator.Storages.cs`, `CarriedStorages.List`) and does not rebuild the
message every tick: it takes a cheap print of tg's state (each storage's entity, place, flags and numbers, and its contents' ids, weight classes and
stack amounts, no strings, no interning) and builds the message only when the print changed or a hint of the tick (a move, a new look, a new name)
named one of the storages or items last sent; it sends it when it differs from the last sent, when the move refusal count grew, and in a join. A
backpack filled to tg's limit with pens, with a toolbelt and a box: 294 bytes (`StoragesGameplayTests`); the bound is `Limits.MaxStoragesBytes`.

The other container (slice 4C-1, `CarriedStorages.Other`) is the player's tg `active_storage` while tg's state says the player has it open: the
player in the storage's `is_using` and with a client (tg's `can_see_contents`), the storage's parent not deleted, neither locked (tg's `set_locked`
closes it) nor hidden (tg's `show_contents` never opens it), the hands not blocked (tg's `on_handsblocked_start` closes it: a stun, cuffs, crit,
death), and none of the carried storages (a carried one is listed once, as carried). The replicator reads it after the tick from outside the world:
fields and traits only, no proc that acts. The listing follows tg's state and tg's own closing events, exactly as tg's storage window would show
the container; nothing is polled: tg closes it when the player steps out of reach (`living/Move`, translated with this slice), when the container
moves out of her reach (the storage's `close_distance` on its parent's move), when her hands are blocked, when it is locked or destroyed, or when
she opens another, and the next message has no such entry; while tg keeps it open it stays listed, also out of her reach (a box someone carried
off, a teleport), and a move out of it is then refused, as tg's ClickOn on the item would be, while a move into it goes in, as tg's window click
does. Its entity is the storage's parent's, its fields those of a
carried storage. It is sent to the player who has it open only; several players with the same container open each get their own entry. The print
covers it as it covers the carried storages, so it is not rebuilt while nothing changes. Measured with 50 players idle, each with a box of seven
items open: the print's state read 0.02 ms a tick for all of them, no rebuild (`Replication/Bench/StoragesOtherIdleBench`, a bench run with
`ANOMALY_BENCH=1`); `Integration/StoragesOtherNoPollTests`: a prison gate under an open box and a proximity sensor beside a long-reach player's
open box do nothing while the player stands still.

**InventoryMove** (client to server, an Action of kind 29, `ActionKind.InventoryMove`): `[varuint item >= 1] [u8 target]` after the kind, then by the
target: `1` a storage `[varuint storage >= 1] [u8 cell 0..64, 0 for none]` (into the worn belt the cell is a bar cell, 1 to 7, which the item takes on the bar once it is in the belt, DV693; a belt cell over 7 does nothing, and tg's order in the belt is left to tg); `2` a worn slot `[varuint tg slot id]`, one of
`InventorySlots.Worn` (the suit, the uniform, the gloves, the eyes, the ears, the mask, the head, the feet, the ID, the belt, the back, the neck, suit
storage, the two pockets: one bit each); `3` a hand `[u8 held index 1..8]`; `4` the floor, nothing. No target and no other field
(`ActionMessage.MoveItem`, `MoveTo`, `MoveStorage`, `MoveIndex`; the fields on another kind are refused). The server checks the fields again
(`ValidAction`), takes the request from the overall action bucket and the click bucket (`ActionLimits.Click`, as a drop), and in the command phase
runs `CarriedStorages.Move` with usr the player's mob: nothing (and no refusal reported) unless the client's mob is a human, the item is one it
holds, wears in one of those slots or sees in one of its listed storages or in its other container, the storage is one of its listed storages or its
other container, the hand is one it has; then the move through the steps a player's hands take in tg (DV696), or a refusal counted. The other
container (slice 4C-1) is asked again inside the world for every move: a move OUT of it as tg's ClickOn on the item asks it (`CarriedStorages.OtherInReachForMove`: still the
sender's `active_storage`, kept open by tg as above, and tg's reach rule holding now; no `on_found`, which tg ran at the opening), a move INTO it as
tg's window click asks it (screen_objects.dm:653-667: only tg's state, `CarriedStorages.Other`, and the click cooldown, incapacitated and a mech,
then `attempt_insert`, none of which asks reach), before and after every wait of the move and at the moment of the move (but not after the heavy-gravity wait inside tg's own pick-up, `attempt_pickup`,
which a move into the active hand runs as tg's click does: tg asks nothing again there either); a request naming any
other container (one beside the sender not opened, one another player has open, a locked or hidden one, an item deeper inside, or one tg should
have closed in the same tick) does what a request naming a stranger's item does. The command log keeps the item in the action's target field, the target kind in `TurfX`, the
index in `TurfY` and the storage in `RadialMenu` (`QueuedCommand`). Examples: `0A 09 1D 05 04` item 5 to the floor in frame 9,
`0A 09 1D 05 01 64 03` item 5 into storage 100 at cell 3; `0A 09 1D 05 01 64 41` (cell 65), `0A 09 1D 05 02 03` (two slots), `0A 09 1D 05 03 09`
(hand 9) and `0A 09 1D 05 05` (an unknown target) refused.

**A Shift+click on a stored item** (slice 4B-2, DV696): a Click (version 2's Action) with the left button, Shift alone and a `Screen` target naming an
item the sender's own carried storages show now (`CarriedStorages.ShowsItem`: directly in one of its listed storages, or in the other container it has
open by tg's state (slice 4C-1, `CarriedStorages.Other`: what its Storages message lists; no reach is asked, as tg's Shift+click asks none), among the first 64, the storage neither locked nor hidden) is taken although that item is no screen entry, and runs tg's Click with it (`ClickOn` -> `ShiftClickOn` -> `examinate`),
as a Shift+click on tg's storage screen would; the screen-loc param is empty. The inventory's right-click wheel (DV694) widens this, with no
change to any encoding: a Click with the left or right button and any modifiers, and a Hover (version 4's), naming such an item as a `Screen` target
are taken and run tg's Click (`ClickOn`, as on tg's storage screen, which takes every click on the item) or tg's MouseEntered with it; the server's
ContextHints answer to that hover names it as a `Screen` target. A click with the middle button, an `Entity` target, an item of anyone else's storage,
of a locked or hidden one, or deeper is refused as before (`ClicksRejected`); the click and hover buckets apply as to every click and hover
(`ServerHost.TryStoredTarget`). Tests: `Integration/StorageExamineGameplayTests`, `Integration/InventoryWheelGameplayTests`.

**The wheel's item actions** (DV694, no change to any encoding): the ContextHints answer to a hover of an item the player holds or wears (a `Screen`
target) adds, after tg's entries, one hint of the Extra action (section 83) per action button tg shows the player for that item (his own actions whose
target it is, with a button on his HUD), labelled with the action's name ("Set Internals", "Toggle Light"); a ContextPick (version 86's) may now name a
`Screen` target (the encoding always allowed it); for such an item it runs tg's click on the button of the action of that name
(`RadialExtras.Pick`: `action_button/Click`, its `can_use`, cooldown and `Trigger`), and does nothing when no such button is the player's now (a
forged name, another's item, an item in a bag, where tg grants no action).

**InventoryOpen** (client to server, an Action of kind 30, `ActionKind.InventoryOpen`): no fields (`0A 09 1E`). It takes the overall and the
keybinding buckets (a key); the server runs `CarriedStorages.Opened`: tg's opening of the carried storages without its screen, at most once a second (the back storage's checks, its `on_found` pass, rustle and animation; the `on_found` pass of each other listed storage; DV696).

Tests: `Protocol/ProtocolV100StoragesTests`, `Protocol/CodecTests`, `Protocol/WireTests` (server), `UiOverhaul/CarriedStoragesTests`,
`UiOverhaul/CarriedStoragesOtherTests` (core), `Integration/StoragesGameplayTests`, `Integration/StoragesHostileTests`,
`Integration/StoragesOtherGameplayTests`, `Integration/StoragesOtherHostileTests`, `Integration/StoragesOtherReplayTests`, `Integration/StoragesOtherNoPollTests`, `Client/Play/StoragesClientTests`, `Client/Play/InventoryRulesTests`.
The client (slice 4C-2, DV694) draws the other container's entry as the last panel of the Tab inventory's column, pinned above the nearby strip
(`StorageColumn.Panels`), and on the human HUD never shows tg's own storage window for it (`ContainerWindow.Withheld`).

**Interact** (slice 4E, DV697; client to server, an Action of kind 31, `ActionKind.Interact`, the last kind): the world interact key (E,
Space Station 14's ActivateItemInWorld) on a target, written as a click's target and nothing else: an entity (`0A 09 1F 01 <id varint>`) or a turf
(`... 1F 02 <x> <y> <z varint>`); a screen, ceiling or missing target, a button, modifiers, an icon pixel, an aim or a text are refused by both
ends. It takes the overall and the click buckets; the server resolves the target as a click's (unknown or unseen: `ClicksRejected`) and runs
`WorldInteract.Use`: tg's plain left click with the held item left out (ClickOn's gates, then `UnarmedAttack`; nothing out of reach, nothing on a mob
while in combat mode, nothing on an item lying elsewhere while the active hand is full).

**Keybinding codes 20 to 24** (slice 4E): tg's quick-equip keys of a slot, `/datum/keybinding/human/quick_equip_belt` and its subtypes, whose
`down` calls `smart_equip_targeted(slot_type, slot_item_name)`: 20 `QuickEquipBag` (the back), 21 `QuickEquipBelt`, 22 `QuickEquipLeftPocket`,
23 `QuickEquipRightPocket`, 24 `QuickEquipSuitStorage` (`KeybindingCode.Last`). The keybinding bucket, as every keybinding.

Tests: `Protocol/ProtocolV100KeysTests`, `M4aContracts/ProtocolV4ContractTests`, `M4aServer/P6/CodecV4Tests`, `Integration/KeysSlice4EGameplayTests`.

**Actions** (ui-overhaul.md section 15, DV698; server to client, State channel, id 36, after the tick's Delta or a join's Snapshot, when it changed; to
the owning session only; `MessagesV100Actions.cs`, `Codec.V100Actions.cs`): the own mob's action buttons (tg's
`/atom/movable/screen/movable/action_button` with a linked action) that are on the client's screen this tick, with what the client cannot read
for certain from a button's picture, and the screen objects of tg's action palette, which the client does not draw since it lays the buttons out
itself (the actions menu and its nine slots).

```
[varuint count 0..64] then each button, in increasing id:
  [varuint button >= 1] [str(0..64) name, or none] [u8 flags: bit 0 active, bit 1 cooling down; bits 2-7 zero]
  ([varuint cooldown end tick] [varuint cooldown ticks 1..72000] when cooling down)
[varuint palette count 0..8] then each palette object [varuint id >= 1], in increasing id, none of them a button
```

The button is the screen object a click names (tg's `action_button/Click` runs the action, with tg's own checks); its picture, its colour (tg's red
while unavailable) and its maptext (tg's count: a charge, a growth, a cooldown's seconds) are the screen entry's as before. The name is the button's
(tg's `update_button_name`), plain, cut at 64 bytes. Active is tg's `is_action_active(button)`. A cooldown action (`/datum/action/cooldown`) still
cooling down (`next_use_time` after `world.time`) sends the server tick it ends at and its length in ticks (its `cooldown_time`, or the time left
when an overridden cooldown is longer; at most 72000, one hour), so the client draws the share still to run as `(end - tick) / length`; the end
moves only when a cooldown starts, so the message is not resent every tick. The palette objects are tg's `button_palette`, `palette_scroll` (up and
down) and `action_landing` on the screen. At most 64 buttons (`Limits.MaxActionButtons`; a mob with more sends the first 64) and 8 palette objects;
anything out of order, a button twice, a palette object that is a button, unknown flag bits, a cooldown of 0 ticks with an end or past the cap is
refused at both ends (`BadValue`, a long name `BadString`, too many `CountTooLarge`). A mob with no buttons sends `24 00 00`, 3 bytes; a button 12
named "Toggle Light", not active, starts `0C 0D 54 6F ...`. The server reads them from the screen objects of the tick's screen entries
(`Replicator.Actions.cs`), so it never names a button the client has no entry for unless the screen held more than `Limits.MaxScreenEntries`.

Tests: `Protocol/ProtocolV100ActionsTests`, `Protocol/CodecTests`, `Client/Play/ActionsMenuClientTests`, `Integration/ActionsMenuGameplayTests`.
