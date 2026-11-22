# Replication: from the tg world to the clients' entity tables

Status: v4 (M4a package P6), 2026-10-09 (section 14); v3 was M3 package P5 (section 13), v2 M2 package P5, v1 M1 package P4. The server's replicator, `Anomaly.Server.Replication.Replicator`,
implements the frozen contract `IReplicator` (`src/Server/Replication/IReplicator.cs`). Design basis: M1 scope 2d (R3, the M1 minimum,
the measurement plan and the decision rule) and M2 scope 2h and 2i (appearances, positions, private blocks), D99 (the owner chose the
hybrid; the measurements have the final say), D38 (rollback in M10), D40 (no hidden data), D103 (own-capsule prediction), D107 (the HUD
is tg's screen objects), `docs/ai/specs/presentation-seam.md` rule 5 (hints select, shadows decide) and `docs/ai/specs/object-model.md`
sections 3, 6 and 6.1 (ids, the registry, the hint sink). The byte layout of the messages is the protocol's
(`docs/ai/specs/protocol.md`, version 2).

## 1. Decision

**R3, the hybrid, as D99 chose it.** M1's measurements (section 11) are far from D99's threshold: at 16k atoms with 150 movers the
replicator cost about **6 µs per tick** (threshold: about 2 ms). M2 keeps R3 and adds to it, measured again in section 9: with 150
humans running (a capsule each, polled every tick, with appearances interned from their atoms) a tick costs about 170 to 210 µs
(the machine was loaded by other builds), plus 4 µs for each session's private block: still two orders of magnitude under the
threshold. `name` became a hinted engine property in the contracts step (section 5.2), so every field now has a hint or is polled,
and the sweeps are safety nets for what the engine cannot name: an in-place edit of an `overlays` list, a turf's `density`, a
screen object's overlay edit (section 5.3). The owner does not need to be asked again.

What version 2 adds, in one place:

- **Appearances** (section 6): every entity's picture is interned from the atom's builtin appearance vars and its overlay and
  underlay lists into the run's public appearance table, one id per distinct appearance; new rows go out once, in the Delta that
  first uses them.
- **Capsules and posture** (section 7): `Pos` and `Yaw` of a capsule mob every tick they change (the replicator polls them), `Posture`
  of every living mob.
- **The map's cells** (section 8): the turf appearance and the solidity bit of every tile in `MapGrid`, kept current and sent as cell
  changes.
- **Private blocks** (section 9): per session, after the shared body: the own motion state, the input buffer depth, the
  `client.screen` entries in private tables (D40).

All of it under the rules of section 3.6: read-only, outside the world, nothing kept from one tick to the next but values and ids, so
the state hash is the same with and without a replicator.

## 2. What leaves the server

### 2.1 Entities

An **entity** is a registered `/atom/movable` whose raw `loc` is a turf and whose `invisibility` is below tg's
`INVISIBILITY_ABSTRACT` (101, "never visible to anyone", `code/__DEFINES/sight.dm:25`): the mobs and objs standing on the
floor. Everything else stays on the server:

- **Turfs** go in the join's `MapGrid` (size, a run-length list of turf type indexes, x fastest, then y, then z, and since version 2
  the turf appearance of every cell and its solidity bit); a late joiner gets the map as it is (a replaced turf marks the cached map
  dirty). Changes of a turf's appearance and of a cell's solidity reach joined clients as the Delta's cell changes (section 8);
  turf *types* are not replicated after the join (M3).
- **Container contents** (a mob's or a box's) are not entities until M4: moving into a container is a despawn, moving out a
  spawn.
- **Areas** and non-atom datums are never replicated.
- **Abstract-invisible movables** (`invisibility >= 101`) are not entities (D40): becoming abstract-invisible is a despawn.
  Per-viewer visibility (`see_invisible`, line of sight) is M10's interest filtering.

### 2.2 The whitelist

Exactly the fields of `Anomaly.Protocol.NetField`, each read in one place (`ReplicatedFields.All`; a test checks that the table,
the record types and the wire all hold exactly `NetField.All`):

| Field | Source | Hint |
|---|---|---|
| `Id` | `datum.dm_id` (object-model.md section 3) | created: the registry watermark; deleted: `ObjectRegistry.Unregistered` |
| `Type` | `DmTypes.PathOf` of the runtime type (the nearest ancestor with a path for a C#-only subclass), interned in the run's type table | created (a DM object never changes type) |
| `X`, `Y`, `Z` | the coordinates of the turf that is the raw `loc` | moved (every engine change of `loc`) |
| `Dir` | `atom.dir` (an engine property), as a byte: one of the eight directions (1, 2, 4, 8, 5, 6, 9, 10), anything else (UP, DOWN, an impossible sum) 0, the codec's "no direction" | appearance changed (`Dir`) |
| `Name` | `atom.name` (an engine property since M2), tg's **visible** name: `/atom/proc/get_visible_name` returns it for `/mob` and `/obj` (`atom_examine.dm:234`), and tg's humans keep `name` equal to their `get_visible_name()` (`update_visible_name`, `human_helpers.dm:40`, "Unknown" behind a mask); never `real_name`. Cut to 512 UTF-8 bytes at a character boundary (`Limits.MaxEntityNameBytes`) | appearance changed (`Name`) |
| `GlideSize` | `atom_movable.glide_size` (an engine property), within 0 to 256 (the codec's `Limits.MaxGlideSize`; tg's `DELAY_TO_GLIDE_SIZE` gives 1 to 32); NaN, negatives and -0 become 0 | appearance changed (`Glide`) |
| `LastMoveTick` | the replicator's own observation: the tick in which `X`, `Y` or `Z` last changed, or the entity appeared | moved |
| `OwnMob` | per connection, never in an entity record: the own entity id of the Welcome, Snapshot and Delta headers, which the host gives | - |
| `Pos` | for a mob the locomotion bridge drives (a capsule, `ILocomotionView.TryGetCapsule`, i.e. `LocomotionSeam.TryGetState`): `MotionState.X`, `Y` in 1/256 tile; once an entity has been a capsule, the centre of its tile when the bridge no longer drives it | **polled every tick** for every mob that is or was a capsule; a tile change always carries it |
| `Yaw` | the high byte of the capsule's `MotionState.Yaw` (the last one while not driven) | polled with `Pos` |
| `Posture` | `mob_living.body_position` and `lying_angle` as `PostureBits` (lying, quarter turns); standing is 0 | appearance changed (tg's `update_transform` and `update_appearance`), polled for capsule mobs, else the sweep |
| `Appearance` | the atom's builtin appearance vars, `overlays` and `underlays` (`IDmAppearance`), interned (section 6) | appearance changed (every aspect but `Dir`, `Glide` and `Name`); in-place list edits: the sweep (section 5.3) |

`real_name`, `key`, `ckey`, `desc`, `maptext`, `filters`, `render_target`, health and damage, and every other tg var stay on the
server (tests put marker text and distinctive numbers in them and look for them in every byte sent, section 10). A new field is a
new `NetField` value, a protocol version bump and a row here.

### 2.3 The type table

One table per server run, append-only: a DM path gets the next index the first time an entity of that type is replicated
(at `Attach`, in id order, then in the Delta that spawns it, `Delta.NewTypes` from `Delta.FirstNewType`). Every joined
connection holds a prefix of the same table (the Snapshot carries the whole table so far), so a Delta body is the same for
every connection, and clients never depend on the server build's type order.

### 2.4 The public tables

Besides the type table, the run has three more append-only public tables, built by the replicator and shared by every connection:
**resources** (icon files and GAGS references), **strings** (icon states) and **appearances** (section 6). A row is added the first
time something needs it and never changes or goes; the Snapshot carries each table whole, a Delta the rows its tick added. Rows that
exist before the first Delta (the entities and cells present at `Attach`) are in the first Snapshot.

## 3. The mechanism

### 3.1 Call order (the host)

1. `Attach(world)` once, after the boot, outside the world: installs the hint sink (`PresentationSinks.Replication`), builds the
   shadows of every entity present (one full scan of the registry) and the map's cells (every turf's appearance and solidity). The
   shadows hold the attach tick.
2. Every tick, right after `World.Step()` returned: `BuildDelta(world.Tick)`. It must be called for every tick, in order (it throws
   otherwise): a Delta always follows the previous tick's Snapshot or Delta.
3. Per joined session, right after: `BuildPrivate(tick, inputs)` gives the session's encoded private block for that tick (the host
   fills `PrivateInputs` from the locomotion seam and its jitter buffer), and `Codec.EncodeDelta(tick, lastInputSeq, ownEntity,
   body, block, destination)` puts the session's Delta together from the shared body and the block.
4. A join at tick T, after T's `BuildDelta` (or right after `Attach`): `BuildJoin(T, inputs)` gives the encoded `MapGrid` (cached
   until a turf or a cell changes) and the `Snapshot` of T: the whole type table, the public tables, the entities, and the
   session's private block with its private tables whole and every screen entry as added; it **starts the session's private state
   afresh**. The session then gets the Deltas of T + 1 on. (`BuildJoin(T, ownEntity, lastInputSeq)`, version 1's, gives the same
   with an empty private block.) A join never adds a row to a public table: every row is created in `Attach` or `BuildDelta`, so a
   joiner's tables are exactly a prefix of the Deltas that follow.
5. `Forget(session)` when a session leaves: its private tables and screen shadow go.

Every call throws when an ambient world is set (`World.CurrentOrNull != null`): the replicator reads from outside; `BuildPrivate`
and a join throw `ArgumentOutOfRangeException` for a tick other than the one the shadows hold.

### 3.2 One tick (`BuildDelta`)

1. **Candidates.** The ids the hint sink recorded during the tick; the movables created since the last watermark
   (`ObjectRegistry.TryGetNextLive` from the previous `HighestId`); the next slice of the round-robin sweep; every mob that is or was
   a capsule (the poll, section 7); the turfs the sink named, and the turfs created since the watermark (their cells, section 8).
2. **Comparison**, each candidate once, in increasing id order: look it up in the registry and test the entity rule. Not an
   entity: a despawn if it had a shadow. A new entity: a spawn (full state, a new type index if needed, its appearance interned). Otherwise compare
   the whitelisted values with the shadow: the position and direction, the name, the glide; the appearance when an appearance hint
   named it, the sweep reached it or it just appeared; the posture of a living mob; the capsule's `Pos` and `Yaw`. The changed fields form
   the mask (plus `LastMoveTick` when a coordinate changed, and `Pos` with a tile change of a capsule), the shadow takes the new
   values and every changed field its stamp.
3. **Cells**: the marked cells and a slice of the cell sweep are re-read; the changes go into the Delta (section 8).
4. **Encoding**, once: the Delta body (new types, the new table rows, spawned, changed, despawned, cell changes, each entity list in
   increasing id order) goes through the encoder into the replicator's reused buffer (`Codec.EncodeDeltaBody(delta, ref buffer)`,
   the pooled broadcast form); `DeltaFrame.Body` is a view of that buffer, shared by every session and valid until the next
   `BuildDelta`. The per-session parts, the header and the private block (`BuildPrivate`), are the host's, so the shared bytes stay
   shared.

### 3.3 Hint sources

The hint sink only records ids (presentation-seam.md rules 1 and 5; object-model.md 6.1); the shadows decide.

| Hint | Engine source | What the replicator does |
|---|---|---|
| moved | `IReplicationHintSink.Moved`: every raw change of an atom's container (`dm_set_loc`, which `Move`, `forceMove`, `abstract_move`, `moveToNullspace` and a new atom's silent placement reach; an applied DM contents edit; a turf replacement's movables) | a movable: a candidate; a turf: the cached MapGrid is rebuilt for the next joiner and its cell is re-read |
| appearance changed | `AppearanceChanged(atom, aspect)`, from the same points as `IAppearanceSink.Changed` | a movable: `Dir`, `Glide`, `Name`: a candidate for the cheap fields; the aspects of the picture (`Icon`, `IconState`, `Layer`, `Plane`, `Alpha`, `Color`, `Pixel`, `Transform`, `Overlays`, `Underlays`, `AppearanceFlags`, `BlendMode`): a candidate whose appearance is re-interned; `Maptext`, `VisContents`, `Filters`, `Render` are not part of the wire appearance. A turf: its cell is re-read |
| visibility changed | `VisibilityChanged(atom)` (`opacity`, `invisibility`) | a movable: a candidate (`invisibility` decides entityhood) |
| deleted | `Deleted(id)`, from `ObjectRegistry.Unregistered` (qdel's paths, a replaced turf) | an id that has a shadow: a candidate (it despawns) |
| created | no call: the registry's `HighestId` watermark | every live movable above the last watermark: a candidate |

### 3.4 The sweep

Every tick the sweep adds the next `max(MinSweepPerTick, ceil(slots / SweepDivisor))` shadow slots (defaults 32 and 64) to
the candidates, round robin over the slot array (slots are stable while an entity lives; freed slots are reused). So every
entity is compared at least once every `SweepPeriodTicks` ticks (at most 64; about `slots / 32` for small worlds: 7 ticks
with M1's 200 entities, 64 at 16k), whatever the hints said. It is what catches a change no hint names (`name`, section 5.2)
and any hint a future engine path forgets. `SweepDivisor = 1` compares every entity every tick (tests).

The **cell sweep** is the same safety net for the map: every tick `CellSweepPerTick` (16) cells, round robin, are re-read for their
solidity and turf appearance whatever the hints said (a turf's `density` is a plain var; a door in M3 will have a hint of its own),
so a change is found within `CellSweepPeriodTicks` (the cell count over 16: 144 ticks, 7 s, for the 48 x 48 test room; the sweep costs about 25 µs a tick). The **screen sweep**
re-reads every screen object's appearance of a session at least every `ScreenSweepPeriodTicks` (20), spread over the ticks by id;
`screen_loc` (a plain string) is compared every tick.

### 3.5 Shadows and stamps

A shadow is a struct per entity (`Id`, type index, `X`, `Y`, `Z`, `Dir`, tg's `name` reference and its wire form,
`GlideSize`, `LastMoveTick`, and since version 2 the appearance id, `PosX`, `PosY`, `Yaw`, `Posture`, and flags: capsule-ness, mob,
dense non-mob) plus an inline array of eleven stamps, the tick each changeable field last changed (its spawn tick until then),
`Replicator.ChangedAt(id, field)`. An id index (`Dictionary<long, int>`) finds the slot. M10 builds a client's
delta from the stamps (section 12).

### 3.6 Read-only, outside the world

- **No writes, no calls.** The replicator reads engine getters (`loc`, the turf's `x`, `y`, `z`, `dir`, `glide_size`,
  `invisibility`) and plain fields (`name`), calls no tg proc, creates nothing in the world, touches neither the scheduler nor
  the RNG.
- **Raw reads.** With no ambient world the engine getters apply no pending DM contents edit (D95 item 10): an edit made
  through a `contents` list reaches the replica in the tick the engine applies it (its `Moved` hint comes then), at most one
  tick late, and never inconsistently.
- **State outside the world.** Shadows hold values and ids; the hint sink records ids; nothing holds a world object across
  ticks (the screen lists a private block reads are walked and dropped within the call; a session keeps `dm_id`s and table
  references). The registry's lookups only purge entries the GC already collected. So `World.ComputeStateHash()` is the same with
  and without a replicator (`HashNeutralityTests`: a booted world with random movers for 600 ticks, deltas every tick and
  joins every 100, three hashes equal; and the M0 toy scenario with a replicator attached before the boot, whose hints pile up
  unread: equal hash and log).  The v2 tests repeat it with humans, sessions, private blocks and cells (section 10).

## 4. Settings and the encoder

`ReplicatorOptions`: `Encoder` (`IReplicationEncoder`: `EncodeMapGrid`, `EncodeSnapshot`, `EncodeDeltaBody(delta, ref
buffer)`; the protocol codec, `CodecReplicationEncoder`, by default), `SweepDivisor` (64), `MinSweepPerTick` (32),
`CellSweepPerTick` (16), `ScreenSweepPeriodTicks` (20), and `Locomotion`, the replicator's view of the locomotion bridge
(`ILocomotionView`: `TryGetCapsule(world, mobId, out MotionState)` and `IsSolid(world, x, y, z)`). The default,
`LocomotionSeamView`, forwards to `LocomotionSeam` (M2 P4) when the seam is installed in the world, and otherwise (the M1 rooms, the
tests, and, until P4 lands, every world) reports no capsules and answers `IsSolid` with `DenseSolidity`, the same predicate P4
implements (outside the map, a dense turf, or a dense non-mob movable on the tile); the tests plug in a fake view. The encoder
is an interface so that the measurements can time the encoding and a test can look at the messages; the server uses the
codec. Every value the replicator puts into a record is within the codec's ranges, which the encoder enforces on the sending
side (`docs/ai/specs/protocol.md`): entities stand on turfs of the map (x and y 1 to 255, z 1 to 80; a movable in nullspace or
a container is no entity), `dir` is one of the eight directions or 0, `glide_size` within 0 to 256, names within 512 bytes,
ids positive and Snapshot entities in increasing id order. `ReadMapGrid()` and `BuildSnapshot(...)` give the join's messages
before encoding; `LastStats` (the contract's counters) and `LastDetail` (hinted, created, deleted, swept, sweep catches)
feed the metrics.

## 5. Hint coverage

### 5.1 Measured (scope 2d measurement 3)

A full-scan oracle (the tests' own reader: it walks the registry outside the world, reads each movable's own `x`, `y`, `z`
and observes `LastMoveTick` itself) against the replica built from the bytes of `MapGrid`, `Snapshot` and every `Delta`,
every tick for 10,000 ticks of a random scenario in a 32 x 32 room: 52 movers stepping through tg's `step()` every third tick,
plus spawns (974), qdels (609), `abstract_move` (211), `forceMove` (449), `setDir` (979), `set_glide_size` (474), moves into
containers (408) and nullspace (186) and back (565), abstract invisibility toggles (210) and renames (523).

| Sweep | Mismatches of hinted fields | Names later than the sweep period | Changes only the sweep found | Name lateness, ticks (mean / p50 / p99 / max) |
|---|---|---|---|---|
| 1/64 per tick (scope's) | 0 | 0 | 280, all `Name` | 20 / 16 / 61 / 63 |
| 1/64, at least 32 (default) | 0 | 0 | 350, all `Name` | 3.1 / 2 / 10 / 11 |

The replica equals the world on every hinted field in every tick (0 mismatches over 10k ticks, 1,454 spawns, 1,148
despawns, 140k moves). The sweep found nothing but renames; a rename of an entity that also moved or turned arrives with that
hint at once, which is why the sweep catches fewer renames than were made.

### 5.2 `name`: hinted since M2

`name` was a plain field of `/atom` in M1, so no hint named a rename and the sweep found it within its period (at most 64 ticks, 12
at M1's sizes). The M2 contracts step made `/atom/var/name` an engine property (backing field `_name`, hashed as `_tag` is) whose
setter raises `AppearanceChanged(Name)`, as this document asked, because tg's humans rename themselves on equipping a mask or an ID
(`update_visible_name`). Now every whitelisted field has a hint or is polled, and a rename reaches the replica in its own tick
(`OracleEquivalenceTests` with renames and the default sweep: exact every tick, no sweep catches).

### 5.3 Appearances: what the hints do not name

Every builtin appearance var of an atom is an engine property that raises its hint, so assigning `icon`, `icon_state`, `color`,
`alpha`, `layer`, `plane`, `pixel_*`, `transform`, `appearance_flags` or `blend_mode`, or **assigning** `overlays` or `underlays`
(tg's `cut_overlays`, `overlays = list`), reaches the replica in its own tick. **An in-place edit of an `overlays` list through the
list operators** (`overlays += x`, `overlays.Add`, `Cut`: rules 4.3 change the list in place) raises no hint yet (the SHIM-GAP of
`atom.builtin.cs`: a `DmList` tells its owner after every write, `IDmListOwner.ListChanged`, but the atom listens to its `contents`
only). Until the overlay engine part (M2 P3's snapshot hook) calls `dm_appearance_changed(Overlays)` from `ListChanged`, such an edit
is found only by the entity sweep, within `SweepPeriodTicks` (at most 64 ticks, 3.2 s; every tick for a small world). The tests
cover both ways (`ReplicatorV2Tests`, `ReplicatorV2OracleTests`: hinted changes exact every tick, in-place edits within the period).
**Request for the lead / P3**: with that hook a human's damage overlays reach the other players in the tick tg adds them.

## 6. Appearances

`AppearanceTables` (`src/Server/Replication/AppearanceTables.cs`) interns appearances, once for the public tables and once per session
for its private ones. It reads the atom through `Anomaly.Core.DmShim.IDmAppearance` (the `/atom` engine part, `image`,
`mutable_appearance` and P3's overlay snapshot implement it) and never touches the world: no write, no sink, no tg proc.

**The entry.** The icon is a resource: a `DmResource`'s path, or a text where tg passes text (a GAGS reference
`gags:<config type path>|<colours>` is such a text: the client composes it). The icon state is a string (empty is none). `dir` is the
atom's (a top-level entity gets 0, because its own `Dir` is replicated and one appearance should serve every facing; a screen
object keeps its dir; an overlay keeps its own). `color` is a colour text parsed with `Dm.rgb2num` (`#rgb`, `#rrggbb`, `#rrggbbaa`,
the HTML names; a text BYOND cannot read is no tint) into `0xRRGGBBAA`, or a colour matrix list of 9, 12, 16 or 20 numbers completed
to BYOND's 20 (an identity is none; a list of another length is no tint). `alpha` is 0 to 255 rounded; `layer` and the transform
are finite floats (non-finite becomes the default); `plane` and the pixels round into 16 bits; `appearance_flags` keep their low 16
bits; `blend_mode` a byte. `transform` is the six numbers of the `/matrix`, none for the identity. The overlays and underlays are
walked in list order: an `IDmAppearance` item (an `image`, a `mutable_appearance`, P3's snapshot) is interned as an entry of its
own (its overlays too, to a depth of six), a text is an entry with only that icon state, a `DmResource` one with only that icon;
anything else is skipped. At most 256 are kept.

**The lookup.** The entry is built in a scratch object (one per nesting depth, reused) and looked up by value in a dictionary
(floats by their bits); a hit costs the build and the lookup, a miss adds one copy to the table. So one id per distinct appearance;
a changed look is a new row only the first time (toggling between two looks adds two rows for the run, which the tests assert).

**No appearance.** An entity with no icon, no overlays and no underlays shows nothing and has appearance 0: no row (a toy world's
thousands of props cost nothing).

**When it runs.** A new entity (at `Attach`, in `Spawn`), an appearance hint, and the sweep; never for a candidate named only by a
move, a direction, a glide, a rename, a visibility change or the capsule poll.

**Lifetime and caps.** The tables are append-only for the run. A table that reached its cap (65536 resources, 2^20 strings, 262144
appearances) or a resource over 512 bytes gives "no row" (`TableOverflows` counts it); a string over 256 bytes is cut at a character
boundary. The Snapshot carries a table whole, so the run's appearance table must fit one message: 262144 rows of about 100 bytes
decoded. A run that outgrows it needs the pruning M10's baselines bring.

**Flicks (version 8, D142).** BYOND's `flick(state, atom)` is an event, not state: the sink's `Flick` notes the movable and what it
flicked (an icon state text or an icon file), and when the entity is next read (the same tick) `InternFlick` interns the atom's
appearance as it is then with that state (or icon) in its place. The entity's `Flick` field carries that appearance and the tick;
a later flick of the same look is a new tick. A flick on a turf, or of anything but a text or an icon, is not sent.

**Animations (version 9, M4v).** BYOND's `animate()` is state for as long as it runs: the sink's `AnimateStep` gives
`AnimationRecorder` each step with its subject and the vars' values before it; the entity's `Animation` field carries its running
tracks (protocol.md section 18) and is sent when a step was recorded and again as none when the last track ends (a poll at its end
tick). The attack lunge stays `EntityFlags.Lunge`; the lying turn's transform is the posture's. A chain of anything that is no entity
after the tick's reads is forgotten.

**Short-lived visuals (version 10, M4v slice 3).** tg's `flick_overlay_view` puts an `/atom/movable/flick_visual` (in nullspace)
into a turf's or an atom's `vis_contents` and deletes it after its duration. Such a visual is an entity too (`ReplicatedFields.IsVisual`):
it stands at its host's turf and carries `VisHost`, the host entity's id or 0 for a turf (protocol.md section 19). The engine's
`vis_locs` index is not read from outside the world, so the replicator keeps its own: the `VisContents` appearance hint of a
movable or a turf queues the holder, the read lists the flick visuals its `vis_contents` holds, and every known visual is polled each
tick (it follows a moving host and leaves when it is deleted, when its host leaves the map or stops holding it). Its appearance,
flick and `animate()` chain are the ordinary fields. A host's own appearance no longer flattens a flick visual into its overlays
(`AppearanceTables.AddVisContents`), except for a screen object's.
M8x slice C (D210): tg's visual helpers (`/atom/movable/visual`: the cryo cell's occupant, made in nullspace by the cell's `Initialize`
and kept in its `vis_contents` for the cell's life) are such visuals too, so that their own `animate()` chain (the occupant's bob) is
sent; the patient in the occupant's `vis_contents` is flattened into the occupant's appearance (he is no entity inside the cell, which
is his `loc`). The first full scan notes the helpers of the entities it spawns (they were made at the map load, before the replicator
was attached), and they spawn in the first read.
M8y slice C: so are the unique vis overlays (`ReplicatedFields.IsHeldVisual`: the stasis bed's mattress, whose 5 s alpha fade is sent).

**Private tables.** A session's screen objects (D107) are interned into that session's own `AppearanceTables`, which names
nothing public: its entries, strings and resources are private (`TableRef.Private`). This is what keeps a health level off every
other session's wire (section 9).

## 7. Capsules and posture

The M2 server drives a session's living mob by a capsule (`Anomaly.Core.Motion`; D105, `docs/ai/specs/locomotion.md`); the replicator reads it
through `ILocomotionView.TryGetCapsule` (`LocomotionSeam.TryGetState`: the state after the tick's locomotion phase).

- **The poll.** The capsule moves inside a tile with no `loc` change, so no hint says so. Every entity that is a `/mob` is on a list
  (kept in step with spawns and despawns); each tick the replicator asks the view about every one of them (a dictionary lookup) and
  examines the ones that are capsules, or were: `Pos` (1/256 tile, clamped to the map's tiles) and `Yaw` (the high byte of the 16-bit
  yaw) are compared with the shadow, a change is a bit of the mask. 150 humans cost about 150 lookups and examinations a tick.
- **Always with a tile change.** When a capsule's tile changes (the bridge moved the mob: `X`, `Y`, `LastMoveTick`) `Pos` is sent in the
  same record, whether or not the position changed, so the client never holds a position in the wrong tile (the invariant: the
  capsule's centre is in the mob's tile at the end of the tick).
- **Once a capsule, always carrying `Pos`.** The first tick an entity is a capsule its record gets `Pos` and `Yaw`
  (`ReplicaEntity.HasPos` becomes true). If the bridge stops driving it (the session left, the mob stays) it keeps sending `Pos`: the
  centre of its tile, following every move, and its last yaw. A non-mob never has `Pos` even if a view named it.
- **Posture.** For every `mob_living` the replicator computes the posture byte from `body_position` and `lying_angle` (lying and the
  quarter turns; a standing mob carries no angle) whenever it examines the mob: capsules every tick (the poll), others when an
  appearance hint (tg's `update_transform` and `update_appearance` raise them) or the sweep names them.
- **What the client does with it.** The client draws a capsule from its `Pos` samples, collides its own predicted capsule with the other
  standing capsules at their newest `Pos` (`ReplicaWorld.CollisionView`, lying ones excluded: D51), and uses `Posture` for the lying quad
  (D104). The own mob's `Pos` is public like everybody's; its authoritative state is in the private block.

## 8. The map's cells

`MapGrid` version 2 carries every cell's turf appearance and solidity bit; the Delta carries their changes. The replicator keeps both
for the whole map (`_cellAppearance`, one id per cell; `_cellSolid`, one bit per cell), built at `Attach` and updated every tick, so a
join is built from them and agrees with the Deltas that follow.

- **A map that comes after `Attach` (M3 R4).** The host attaches the replicator to the booted world, but SSmapping loads the station
  in the Master's init, later (joins open at tick 20 on Meta). Before R4 the tables were built for a world with no map and stayed
  so: a client that joined Meta got one `/turf` run, no solidity, no turf appearances (the owner's empty map, and a prediction
  through walls the server has). Now `UpdateCells` (in `BuildDelta`) rebuilds the tables from the world when the world's size
  (`MapView` width, height, levels) differs from theirs; the type, base-light and starlight entries the rebuild adds go out in
  that tick's Delta. And `IReplicator.RereadMap` (a default member beside the frozen ones), which the host calls once when joins
  open, marks every cell, so whatever the init changed after the rebuild goes out like any hinted cell. A client that joined
  before a size change keeps a grid of the old size (no world changes its size after joins open in M3).

- **Solidity** is the one predicate the controller uses (`ILocomotionView.IsSolid` = `LocomotionSeam.IsSolid`: outside the map, a dense
  turf, or a dense non-mob movable on the tile). **Turf appearance** is the turf interned like any atom (public table).
- **Which cells are re-read.** A dense non-mob movable that spawned, despawned, moved or changed its `density` marks the cell it left
  and the cell it entered (the shadow keeps the flag); a turf named by an appearance or moved hint, or created since the watermark
  (a replaced turf), marks its cell; the cell sweep adds 16 cells a tick (section 3.4). A marked cell's solidity and appearance are
  compared with the arrays; a difference is a `SolidChanged` or `TurfAppearanceChanged` record in the Delta, in cell order, and
  dirties the cached `MapGrid`. At most 65535 changes go in one Delta; the rest stay marked for the next.
- A dense **mob** is no wall (capsules collide with standing capsules, not with the tile): the predicate excludes it.

## 9. The private blocks and what version 2 costs

`BuildPrivate(tick, inputs)` makes one session's block (D40; protocol.md 4.3) in a per-session buffer, valid until the session's next
call. `PrivateInputs` is what the host knows: the session's ordinal, its tg client's world id, its mob, the last applied input seq,
the capsule state and parameters from the locomotion seam (when the mob is a capsule), and the jitter buffer's depth.

- **The motion part and the depth** are copied from the inputs.
- **The screen** is the session's `client.screen` list, read each call: the atoms with a world id, in increasing id order, at most 512.
  Each one's shadow is its private appearance reference and `screen_loc`. A new object is added (its appearance interned into the
  session's private tables); an object whose id was named by an appearance hint this tick, or that the screen sweep reaches
  (`(tick + id) mod ScreenSweepPeriodTicks == 0`), is re-interned and, if its reference changed, listed as changed; a changed
  `screen_loc` (compared every tick) is listed as changed; an object that left the list is listed as removed.
- **The private tables' new rows** since the session's last call are in the block, with their first indices.
- **A join starts afresh** (`BuildJoin(tick, inputs)`): the session's state is replaced, the Snapshot lists every table whole from
  index 1 and every screen entry as added.

**D40.** What only its owner may see is interned into the owner's private tables and nowhere else; the shared body is built before
any session's block and never reads one. The tests in section 10 check this on bytes: for two sessions with distinctive screen
strings, resources and locations, neither is found in the other's messages (and each is found in its owner's, so the scan is not
vacuous), nor in the public tables the replicas hold; the shared body is the same bytes for both sessions; and with humans taking
damage (hidden numbers written into tg's `bruteloss`, `fireloss`, `toxloss`, `oxyloss` and `health`) not one of the numbers is
in any session's bytes as text, as an `f32` or as an `f64`. The health doll and the other HUD pictures are icon states (`"head3"`),
never numbers.

**What it costs** (Release, .NET 10.0.12, the owner's M1 Pro with other packages building; humans running in straight lines through
the 48 x 48 room, a capsule each, three sessions each with nine screen objects; `BandwidthBench`, `ANOMALY_BENCH=1`). Payload bytes of
the State channel messages as the host sends them; LiteNetLib adds 4 to 8 bytes of header to a packet and fragments a message
over its MTU of 1024 bytes (reliable ordered; the fragments are not counted here):

| Humans running | Yaw | Bytes per player per second | Shared body bytes per tick | Per human per tick | Own header and block per tick | `BuildDelta` µs (mean / p99) | `BuildPrivate` µs |
|---|---|---|---|---|---|---|---|
| 50 | steady | 7,333 | 341 | 6.8 | 26 | 95 / 168 | 4.5 (p99 14) |
| 50 | changing every tick | 8,117 | 380 | 7.6 | 26 | 90 / 141 | 3.9 (p99 12) |
| 150 | steady | 20,690 | 1,009 | 6.7 | 26 | 170 / 243 | 3.8 (p99 14) |
| 150 | changing every tick | 23,047 | 1,127 | 7.5 | 26 | 207 / 319 | 4.3 (p99 18) |

Scope 2h expected about 30 KB/s per client at 150 movers: the measured 21 to 23 KB/s is below it (a moving human costs 6.7 to 7.5
bytes a tick: id 1, mask 1, `Pos` 4, `Yaw` 1 when it changes, plus the move tick and `X`/`Y` in the tick it crosses a tile). The timings
vary by about a third between runs with the machine's load (load average 13 on 10 cores; a fixed part of about 45 µs, mostly the
cell sweep and the poll, and about 0.8 µs per running human); the order of magnitude is what counts: well under 1 ms a tick against
D99's 2 ms. A join at 150 humans is a `MapGrid` of 724 bytes (the toy room) and a
Snapshot of 4.3 KB (1.7 KB at 50), plus the private tables. The bandwidth is linear in the movers a session sees: M10's interest
management cuts it to the players in view. A guard test (`ABudgetOf45KilobytesPerPlayerPerSecondHoldsAt150MovingHumans`) keeps the
number under 45 KB/s.

## 10. Tests

`tests/Anomaly.Server.Tests/Replication/`, all through the server's own path: the replicator with the protocol's codec, every session's
Delta written as its header over the shared body and its private block (`Codec.EncodeDelta`), and `ReplicaWorld` applying the bytes.

- Version 1's, still in force: `OracleEquivalenceTests` (the replica equals the full-scan oracle every tick for 2,500 to 3,000 ticks
  of the random scenario: exactly with a full sweep, and with the default sweep too now that `name` is hinted), `LateJoinTests`,
  `CodecRoundTripTests`, `WhitelistTests` (the field table and the record types are exactly `NetField`; marker text in `real_name`,
  `key`, `ckey`, `desc`, `maptext`, `filters` never reaches the bytes; abstract invisibility despawns), `HashNeutralityTests`,
  `ReplicatorTests`.
- `ReplicatorV2Tests`: appearances interned from atoms (one id per distinct appearance, new rows once, reuse after a change back,
  every appearance var and list form reaching the replica in its own tick, in-place edits within the sweep period, nothing for an
  entity that shows nothing, invisible entities stay out of every table); capsules (`Pos` and `Yaw` in the ticks they change, the
  forced `Pos` with a tile change, the sticky tile centre, a non-mob ignored, a late joiner's record); posture (polled and swept);
  the cells (the join's map equals the world cell by cell, a dense obj, a moved obj, a deleted obj, a dense mob that is no wall, a
  turf's density flip found by the cell sweep, a turf appearance change); private blocks (the own motion, the depth, the screen
  entries following their objects, a rejoin starting afresh, the screen sweep); the stamps; the call order and the outside-the-world
  rule of the version-2 entry points.
- `ReplicatorV2OracleTests`: **the M1 oracle extended**: twenty humans moving continuously (a fake capsule each, `forceMove` on tile
  changes), taking damage (hidden numbers in tg's vars, tg's damage overlays set by assignment and in place), lying down, being
  renamed, deleted and replaced, for 1,500 ticks: both sessions' replicas equal the oracle's scan (the version-1 fields plus
  `HasPos`, `Pos`, `Yaw`, `Posture` and the appearance resolved to text through the tables) at every tick, and the cells equal the
  world's at the end; with the default sweep the hinted changes are exact every tick and the in-place edits arrive within the
  sweep period; a late joiner holds the same tables and stays equal; the state hash is the same with and without the replicator,
  its private blocks and its cells (12 humans, two sessions, 600 ticks); **the D40 tests** of section 9.
  These use stand-ins for the medical code (plain vars and overlays set by the test); the integration step runs the same oracle with
  the real humans of M2 P1 to P3.
- `Bench/BandwidthBench` (section 9) and `ReplicationBench` (section 11).

## 11. Measurements of the M1 mechanism (version 1)

The numbers below are M1's, kept because the mechanism is unchanged: the candidate set and the diff cost grow with the version-2 fields
as section 9 shows.


Release build, .NET 10.0.12, the owner's M1 Pro (10 cores), the protocol's codec (P2, merged at `16cd522`). Other M1
packages were building on the machine (load 10 to 25), so the spreads are wide; three full runs agreed within about 20% (the
tables give the last run; the dense variant varied most, 43 to 70 µs). The harness:
`tests/Anomaly.Server.Tests/Replication/Bench/` (`[Trait("Category", "Bench")]`, skipped unless `ANOMALY_BENCH=1`):
`ANOMALY_BENCH=1 ANOMALY_BENCH_OUT=<folder> dotnet test tests/Anomaly.Server.Tests -c Release --filter Category=Bench`. Worlds: tg's
real boot with `ToyRoom` of 32, 64 and 128 cells a side (1.2k, 4.2k and 16.5k atoms), 150 plain mobs of which 52 or 150
step through tg's `step()` every third tick (D98's pace), and a dense variant: the 128 room with 16,000 more objs on the floor
(32.5k atoms, 16,150 entities).

### 11.1 Write cost per var write (measurement 1)

| Form | ns per write |
|---|---|
| plain field | 0.42 |
| property with a dirty bit | 0.65 |
| property with a change stamp and a per-world dirty list (R1's form) | 1.50 |

4,096 objects, 5 vars (4 floats, 1 reference), 4 of them changing every round; median of four repetitions.
**Macro check:** in a throwaway copy of the core (scratchpad, not committed), the 20 `/atom/movable` vars the Move chain
reads and writes (`last_move`, `active_movement`, `anchored`, `move_resist`, `move_force`, `pull_force`, `pulledby`,
`inertia_moving`, `last_pushoff`, `pass_flags`, `generic_canpass`, `moving_diagonally`, `moving_from_pull`,
`spatial_grid_key`, `movement_type`, `grab_state`, `currently_z_moving`, `set_dir_on_move`, `pulling`, `blocks_emissive`)
became generated properties with a change stamp and a dirty list; 150 mobs stepping in a 48 room, five alternating runs of
6,000 ticks each: tick mean 0.081 ms (base) against 0.083 ms (R1), p50 0.061 against 0.062 ms, with 144 changed writes per
tick. A dynamic diff of every field of every atom shows what a plain mob's step really changes: `loc` (and the turfs'
`contents`), `dir`, `last_move` and sometimes `currently_z_moving`. **R1's write barrier costs about 2 µs per tick here: not
a reason for or against it**; R1's price is the rule change for every var and every `ref` site, not the time.

### 11.2 Diff cost and candidates (measurement 2)

| Room (atoms) | Movers | Candidates per tick (hinted + swept) | Changed entities | R3 `BuildDelta` µs (mean / p99) | of which encoding µs | World.Step µs (mean) |
|---|---|---|---|---|---|---|
| 32 (1.2k) | 52 | 43 (14 + 32) | 14 | 2.8 / 4.9 | 0.2 | 21 |
| 32 (1.2k) | 150 | 65 (42 + 32) | 42 | 5.8 / 11 | 0.6 | 65 |
| 64 (4.2k) | 52 | 45 (16 + 32) | 16 | 2.7 / 5.9 | 0.2 | 23 |
| 64 (4.2k) | 150 | 69 (47 + 32) | 47 | 5.5 / 7.9 | 0.6 | 60 |
| 128 (16.5k) | 52 | 46 (17 + 32) | 17 | 3.0 / 3.7 | 0.2 | 23 |
| **128 (16.5k)** | **150** | **71 (49 + 32)** | **49** | **5.9 / 8.0** | 0.6 | 64 |
| 128 + 16k objs (32.5k, 16k entities) | 150 | 301 (49 + 253) | 49 | 70 / 87 (43 to 48 in two other runs) | 0.6 | 88 |

R3 allocates 80 to 90 bytes per tick (its Delta records; the body buffer is reused). With the pure 1/64 sweep
(`MinSweepPerTick = 0`) the swept part is `ceil(entities / 64)`: 3 per tick at 150 entities. For comparison, R2 (every object
every tick, a registry walk):

| Room (objects) | R2 over the whitelist of every atom: ms per tick; ns per object; ns per slot | R2 over every hashed field of every object: ms per tick; ns per object; ns per slot |
|---|---|---|
| 32 (1.9k objects, 1.2k atoms) | 0.38; 320; 63 (6k slots) | 0.41; 215; 1.6 (259k slots) |
| 64 (5.0k, 4.2k atoms) | 0.57; 135; 27 (21k slots) | 0.74; 150; 0.9 (864k slots) |
| 128 (17.3k, 16.5k atoms) | 3.3 (3.3 to 6.5 over three runs); 200; 39 (83k slots) | 2.5; 150; 0.8 (3.3M slots) |
| dense (33.3k, 32.5k atoms) | 8.4 | - |

Per object, both R2 forms cost about 130 to 400 ns, dominated by the registry walk (a dictionary lookup and a weak
reference per object) and the shadow lookup, not by the fields: the whitelist's few engine getters cost about as much as
200 raw field reads compiled into one method. Whatever the field set, a full scan is milliseconds per tick at station scale,
which is why R3 compares only candidates.

### 11.3 Delta and join (measurement 4)

| Room | Delta body bytes per tick (52 / 150 movers) | MapGrid bytes; ms (warm) | Snapshot bytes; ms (warm) |
|---|---|---|---|
| 32 | 92 / 254 | 183; 0.09 | 3.05 KB (150 entities, 20 B each); 0.05 |
| 64 | 105 / 291 | 311; 0.07 | 3.05 KB; 0.02 |
| 128 | 111 / 305 | 572; 0.18 | 3.06 KB; 0.02 |
| dense | - / 305 | 574; 0.19 | 424 KB (16k entities, 26 B each); 0.7 |

Encoding a body takes under 1 µs. A changed entity costs about 6 bytes (the codec writes an id difference and a one-byte
mask, then one coordinate, often `dir`, and `LastMoveTick`); 150 movers make about 6 KB/s per client at 20 TPS. Suggestion
for the codec (P2): in a Delta, `LastMoveTick` always equals the Delta's tick (the replicator sends it only when a coordinate
changed or the entity spawned), so encoding it relative to the header's tick would make it one byte instead of three in a long
run, about a third of a moving entity's record. The first `MapGrid` and `Snapshot` of a process include the JIT (1 to 4 ms).

### 11.4 Shadow memory (measurement 5)

165 bytes per entity at 16k entities (the slot struct of about 120 bytes with its seven stamps, the id index entry, array
growth slack); 730 to 780 bytes per entity at 150 entities, where the fixed part (initial arrays, the Delta and lists)
dominates. 16k entities: 2.7 MB.

### 11.5 The decision rule (D99)

R1 only if R3 costs more than about 2 ms per tick at 16k atoms with 150 movers, or the sweep catches whitelisted changes too
late for M1. Measured: 6 µs (0.3% of the threshold; 45 to 70 µs with 16k entities), and the sweep catches only renames, within
64 ticks at worst, which M1 does not make. **R3 stays.** R1's write barrier is cheap too (section 11.1), so it remains the
option for `[Predicted]` vars if M10's prediction needs an undo log (D38).

## 12. What M10 adds

- **Per-client baselines from the stamps.** A client acknowledges the ticks it applied; its delta is every entity in its
  interest set with a field stamped after its acknowledged tick, the fields so stamped, plus spawns and despawns since. The
  shared body of M1 becomes per-client (or per baseline group), built from the same shadows; `Forget(session)` drops the
  client's baseline.
- **Interest filtering** (D37, D23): candidates and baselines intersected with each client's interest set (rooms and chunks
  with line of sight, `see_invisible`); entering the set is a spawn for that client. *Done in M10 slice C (multiplayer.md section 6):
  with interest on, every entity goes through the sessions' private blocks (the observer layer's lists), the shared body keeps the
  tables and the cells.* The `see_invisible` part is done for the
  observer layer (version 22, section 17): the entities above `SEE_INVISIBLE_LIVING` go to each session by its mob's sight.
- **Unreliable deltas with acks** (PLAN 2.9): deltas move to the unreliable-sequenced channel with our own splitting at the
  MTU; a lost delta is covered by the next one built from the client's last acknowledged baseline, not resent. *M10 slice B
  measured first (multiplayer.md section 5) and moved only the capsule mobs' positions to the unreliable channel (protocol.md
  section 51, version 42): own walking was exact at 2% loss already, and the others' movement was what the reliable channel's waits
  froze; the rest of the Delta stays reliable.*
- **Rollback snapshots** (D38): the same field tables give full snapshots of the replicated state; prediction's own vars may
  get R1's generated properties with an undo log (`[Predicted]`), measured cheap in section 11.1.
- **Container contents** (M4) add their own records and a protocol version (turf type changes, M3, are in section 13).

## 13. Version 3 (M3 package P5)

What the replicator adds for protocol version 3 (`docs/ai/specs/protocol.md` section 13 has the bytes). The mechanism is the same
(hints select, shadows decide, the sweep catches what no hint named); only the read side grows.

**The entity rule** (D115, DV24): `invisibility <= ReplicatedFields.MaxEntityInvisibility` (tg's `SEE_INVISIBLE_LIVING`, 25) instead of
`< INVISIBILITY_ABSTRACT`. Undertile pipes, cables and disposals (`INVISIBILITY_MAXIMUM`, 100), landmarks and mapping helpers are
not entities; a movable whose invisibility crosses the threshold spawns or despawns (the visibility hint selects it).

**The light** (`NetField.Light`, `ReplicatedFields.WireLight`): the atom's `light_range`, `light_power`, `light_color`, `light_on`,
`light_angle`, `light_dir`, `light_height`, `light_system` and the visual offset, read raw; none while the range or the power is 0
or the system is not one of tg's four. The offset is `calculate_light_offset` without the matrix and irregular-icon terms (the
negated pixel shift in tiles, unless `LIGHT_IGNORE_OFFSET`; the client has no icon sizes of tg's). Hints: tg's `update_light()`
is the presentation call `IAppearanceSink.LightChanged`; the replicator wraps the world's appearance sink (forwarding every call to
the one that was there) and turns it into a candidate for a movable and a cell re-read for a turf (a starlit space cell's
`set_light`). Creation, the visibility hint and the sweep also read the light, so a missed hint costs only latency.

**The cells** (`Replicator.BuildCells`, `UpdateCells`): besides solidity and the turf appearance every tile now has its flags
byte (`LocomotionSeam.FacesOf`, `CellState.IsOpaque`, `CellState.IsStarlit`), its base-light index (`CellState.BaseLightOf`: the
area's `base_lighting_color` with `base_lighting_alpha`, 0 when the alpha is 0; distinct values form an append-only table) and its
turf type (the runtime type's DM path in an append-only table, so a joiner and an early client agree on every index). A cell is
re-read when it is marked: by its turf's hints, by the cell sweep (`CellSweepPerTick`), and by an entity that contributes to it.
What an entity contributes is `CellKey`: dense (solidity), dense and `ON_BORDER_1` with its `dir` (the faces), opaque (light). The
shadow compares the key every time the entity is examined and marks the cell it leaves and the one it enters when the key or the
position changed (density, flags and `dir` of a face object change a face). A turf `ChangeTurf`ed after the join (a new turf at an
occupied cell) is a turf type change and, through the hint of the replaced turf, a re-read.

**Doors have no density hint.** `density` is a plain field of the atom (`set_density` is a tg proc), so nothing names a door that
opens. The replicator therefore **polls** the entities whose type path starts with `/obj/machinery/door`, `/obj/structure/mineral_door`
or `/obj/structure/plasticflaps` (their slots stay in a list, like the capsule mobs): they are examined every tick, and a change of
their key marks their cell, so a door's opacity and faces reach the clients in the tick they change. Windows and railings are
examined by hint and sweep only (their density rarely changes; a built or destroyed one is a creation or a despawn). A real
`density` hint (`set_density` calling the replication sink) would remove the poll: requested in `open_issues`.

**Starlight values** (`CellState.Starlight`): tg's `GLOB.starlight_*` are not ported (M4), so the values are `initial()` of
`/turf/open/space` (range 2, power 1, `COLOR_STARLIGHT`); a change would be `Delta.StarlightChanged`, compared every tick.

**The join** (`ReadMapGrid`): the flag runs, the base-light table and runs, the turf type table as the clients have it (so a
joiner after a `ChangeTurf` gets a table with the entries that are no longer used), and the starlight. The MapGrid bytes are
rebuilt when a cell changed (`_mapChanged`).

**`ILocomotionView.FacesOf`** is a default member over `LocomotionSeam.FacesOf` (the tests' views only have solidity);
`DenseSolidity` leaves `ON_BORDER_1` movables out of the tile predicate, as `LocomotionSeam.IsSolid` does.

**Tests.** `ReplicatorV3Tests`: faces follow a pane's position, direction and density and union on a tile; mobs with the flag are no
face; opacity by hint; base lights from the area and a table that grows in a Delta; the light by hint, by the sweep, in a joiner's
Snapshot and on despawn (`ReplicaWorld.LightChanged`); the entity threshold; a turf replaced after the join and a starlit cell;
the MapGrid round trip. Every one compares the replica with the world's raw predicates after the cell sweep.

## 9. Costs on Meta (M3 I1b)

- **The door poll** (section 7's cell-polled entities: doors, mineral doors, plastic flaps; 1,384 on Meta): `BuildDelta` at rest with
  one idle session, p50 over 400 ticks, three alternating rounds: 0.55-0.86 ms with the poll, 0.30 ms without it, so the poll costs
  about 0.25-0.55 ms per tick (median about 0.3 ms). Below the 0.5 ms threshold the lead set, so the poll stays and no
  density/opacity hint was added (the engine change it would need is `set_density`/`set_opacity` calling the hint sink).
- **The step profiler** (`World.Profiler`, the host's `StepProfiler`): the server's status window prints tg's MC costs per
  subsystem (avg/max ms per tick), the GC counters and the stall credit; a step over 250 ms prints a `slow tick` line with the
  pieces of the tick (the scheduler, each phase, each subsystem's fire, each timer callback), the GC's collections and pause, the
  bytes allocated and the methods the JIT compiled. The boot prints each subsystem's init time and the heap at joins open.
- **Joins (M3 I1d).** The cost of a human's join was BYOND's own key lookups in the `/client` engine part: `mob.key`'s "any other
  mob with the same key loses it", `client/New`'s "the mob of the player's key" (twice: the seam's `usr` and `New` itself) and
  the attach each ran `for (var/mob/M in world)`, a scan of every atom of the world in four passes (about 1M objects on Meta):
  four scans, about 0.3 s a join (a dotnet-trace of 10 joins: 3.5 s in `DmEach` over the world of 3.75 s of `ApplyJoin`). The
  engine now keeps an index of keyed mobs (ids per ckey, checked on every read; dm-conformance.md, the /client rows). Each
  join prints `status join step #N name: X ms` and its pieces (`join: new human`, `name`, `key`, `client/New`, `outfit`) show
  in the slow-tick lines. Measured on Meta (Release, M1 Pro, load average 15-30 from other packages): a warm join 1-5 ms of
  step (50 bots at once: 340 ms for all 50 together, p50 3.2 ms, p90 6.5 ms, max 108 ms, against 15 s over three ticks
  before); a later single join 5-19 ms; the first join after boot 160-440 ms, of which the JIT (about 1,200 methods, 260 ms)
  and a gen1 GC (up to 107 ms) are most: a one-off per process.
- **Two walking sessions (M3 I1d).** I1c's growth (step p99 0.4 to 352 ms in 50 s, `repl p99` 59-86 ms) was before I1b's
  `locate(tag)` index; on main with it, re-measured with two real Godot clients (640x360, `--autowalk`, both walking: own p99
  speed 14.5 m/s) on Meta for 10 minutes (12,400 ticks, Release, load average 15-31 from other packages): no growth and no
  kick. Over the run: step p50 0.10 ms, p99 2.24 ms, p99.9 13.6 ms (the boot tick aside: 14 s); total tick p50 1.76 ms,
  p99 17.9 ms; replication p50 1.6 ms, p99 16.4 ms, max 443 ms (a join's snapshot). Per 10-s window with both sessions (59
  windows): step p99 0.19-17.9 ms (median 0.74), `repl p99` 2.0-50 ms (median 4.7); the first third of the run has a step
  p99 median of 2.7 ms and a `repl p99` median of 19.5 ms, the last third 0.38 and 3.7 ms. The Delta is built once per tick
  for every session (one body), so a second session adds its send and its private block only; the 59-86 ms of I1c were the
  machine (two uncapped clients and the other packages' suites on the same Mac), and the windows above 20 ms here coincide
  with load peaks, not with the session count. Ten wander bots on the same build: `repl p99` 2.6-3.9 ms per window.


## 14. Version 4 (M4a package P6)

Version 4 adds three things to what the replicator reads, and no new mechanism (protocol.md section 14; `docs/ai/specs/interaction.md`
sections 5 and 8). All of it follows the read rules of section 3.6: outside the world, only reads, nothing held across ticks but
values and ids.

- **No new replication mechanism for hands, slots and storage** (the M4a scope 1.2): tg's hand and slot screen objects draw the
  item they hold through `vis_contents`, which the replicator already flattens into the entry's appearance (since protocol version 12
  it also lists the item as the entry's clickable child, `ScreenEntry.VisChildren`, so a click on it is the item's: protocol.md
  section 21); tg's `show_contents`
  puts a storage's cells and its items into `client.screen`, which the private block already carries. A closed storage's or
  closet's contents are not entities (they are not on a turf), as D40 wants; an open closet's contents are entities on the turf,
  which every viewer sees, as in tg.
- **The group byte** of a screen entry is the first group of `client.mob.hud_used.screen_groups` (a list of the six lists tg's
  `add_screen_object` and `show_contents` fill) that lists the screen object, `HudGroup.None` when none does (a mob without a
  HUD gives none for every entry). Each tick `SessionState.FillGroups` walks the six lists once into a scratch id table (a few dozen
  objects; an open backpack adds 46), then every screen entry reads its group from it. The group is part of the entry's shadow: a
  change of group alone is a changed entry, and an entry that leaves its group list while it stays on the screen is a changed
  entry with group 0.
- **The flick and the animation** of a screen entry (version 11, protocol.md section 20) are part of its shadow too: the update takes
  the object's flick (interned into the session's private table) and its animate() chain (`AnimationRecorder.TryTake`) each tick;
  a new flick, a new chain or a chain that ended is a changed entry. A live screen object's chain is kept past the entities' reads.
- **Private images** are the session client's `images` list (`/image` values: tg's `do_after` progress bar, later the cogbar).
  An `/image` has no world id and is not registered, so the session numbers them: a `ConditionalWeakTable<image, number>` holds
  the number for as long as the image lives (nothing is kept alive; a number is never reused in the session), assigned in the
  order the list is first walked. Each tick the walk takes the first 64 images of the list, interns each one's appearance in
  the session's private tables (`AppearanceTables.InternTop` on the image itself), reads its attachment (`loc` when it is an
  entity: `ReplicatedFields.IsEntity`, else 0: a turf, nullspace, a container, a deleted atom) and compares both with the
  image's shadow: new is added, a different reference or attachment is changed (a progress bar's `icon_state` changes by
  tg's `update_icon` each step, which is the change), one no longer listed is removed. There are no hints for images, so every
  one is interned every tick (64 at most; the typical session has none or one); a Snapshot lists all of them as added. DV13
  narrows to the others' cogbars.
- **The entity flags:** `Thrown` is `atom_movable.throwing != null`, read raw. SSthrowing moves the item every tick (a `Moved`
  hint makes it a candidate), but a throw can end without a move (blocked at once, or `throwing` cleared in a tick the item
  stays), and nothing hints that. So the replicator keeps the **ids whose last sent flags are not None** (`_flagged`, a set of
  ids) and adds each as a poll candidate every tick until the flag is sent cleared; a flag that is set without a move is found
  by the sweep like any other field. The set is cleared of an entity when it despawns. No other bit in M4a.
- **The whitelist** (`ReplicatedFields.All`) gains the `Flags` row; `WhitelistTests` is on `NetField.All` again, and
  `ChangeableV3`/`AllV3` are gone.
- **The ContextHints** are not replication: the host's screentip sink (`ScreentipSeam`, installed by the host when the first
  player joins) turns tg's `on_mouse_enter` call into a `ContextHintsMessage` for the hovering session, sent on Control after the
  tick (protocol.md 14.2).
- **Tests:** `ReplicatorV4Tests` (the group byte through a real human's HUD and an open storage's group list, a change of group
  alone, the images with their attachment, changes and removals, the 64 cap, the Thrown flag with a sweep of one entity a tick
  so only the poll finds its end, a newcomer's Snapshot with a flag in flight, a deleted flagged entity) and the secret test.
- **Measurements (P6, I1):** an open 21-slot backpack costs 1,270 B in the Delta that opens it (46 screen entries and their 14
  distinct private appearances) and nothing after; a rejoin with it open is a 1,997 B Snapshot; a progress bar is 142 B to add and
  a 20 B Delta (7 over idle) while its state is one the session has seen, 46 to 48 B when the state is new (its string and
  its appearance once); a flagged entity costs 5 B in the Delta that flags it. Twenty fiddling bots for a minute: about 3.4 KB/s to each
  bot, 0.5 KB/s from each. I1 measures the same on Meta.

## 15. Version 5 (M4b slice 3: the gas a cell shows)

The cell tables gain one more column: the gas each tile shows (`_cellGas`, null for none), read by `CellState.GasOf` from the
open turf's `atmos_overlay_types` (the overlays tg's `update_visuals` put into its `vis_contents`), as (kind, level) pairs in
tg's order; the kinds table (`CellState.GasKinds`, every tg gas with an overlay) is fixed when the cells are built and goes whole
into each MapGrid. A cell is re-read when it is marked (tg's `vis_contents` change on a turf is an `AppearanceChanged` hint for
the turf, which marks its cell) and by the cell sweep; a difference is a `CellGasChange` in the tick's Delta (an empty one when
the cell no longer shows gas). The read is cheap: a turf without gas has no overlay list. tg calls `update_visuals` when a turf's
air changes (LINDA's `process_cell`, the excited groups, `copy_air`), and a cell's change goes out only when its visible state
moved (tg's states are 0.25 moles apart): each change is a cell and its levels, 6 to 8 bytes for one gas.

## 16. Version 6 (D141: the air flow around each player)

Not part of the shared Delta: the air flow is per session. `AirflowFeed` (`src/Server/Replication/AirflowFeed.cs`) is the sink of
the core's `AirflowSeam`: during SSair's pressure pass it records each turf's level and direction (key `z << 16 | y << 8 | x`, no
map size needed) and at the pass's end publishes the cycle (its version moves only when the cycle differs from the last). After
each tick, after the Deltas and the joins, `ServerHost.SendAirflow` builds every joined session's update (`AirflowFeed.Build`)
from the session's `AirflowView` (what that client has): the published cells within `AIRFLOW_RADIUS` tiles (default 20, 0 turns
the feed off; `--airflow-radius`) of the session's mob's turf and one level above and below; a cell goes out when it starts, ends,
turns, or its level moved by 2 steps or more (`LevelHysteresis`, about half again); a cell the session had that left the square or
ended is cleared. Nothing is built for a session while neither the version nor the session's tile changed. One update takes at
most `MaxChangesPerUpdate` (2048) changes, the nearest first, and the rest the next tick. The feed reads no world state but what
the seam hands it and the mob's `loc` chain; it costs nothing while no air moves. Not created in a replay, with no replicator, or
for an empty world.

Version 7 adds the floor devices: the seam's `Vent` reports (a vent, a scrubber, an injector, a passive vent: the gas it
moved this cycle as the pressure it makes in its cell's air, positive out of the device) are summed per cell and published
with the cycle; a session gets the changes within its square (the same hysteresis; a change of direction always), at most
`MaxAirflowVents` (512) an update, the nearest first.

## 17. Version 22 (the ghosts, D151: the observer layer)

The owner (D151, 2026-10-16): visibility is server-side, as in BYOND: a client whose mob's `see_invisible` is below an entity's
`invisibility` never receives it. Ghosts have `INVISIBILITY_OBSERVER` (60) and see `SEE_INVISIBLE_OBSERVER` (60); the living see 25.

- **The entities.** `ReplicatedFields.IsEntity` takes movables up to `MaxLayerInvisibility` (`SEE_INVISIBLE_OBSERVER`, 60);
  `LayerOf` puts one above `MaxEntityInvisibility` (25) in the observer layer. Both layers share the shadows, the sweep, the hints,
  the stamps and the change records (`Examine`); a shadow carries its `Layer` and the atom's `Invisibility` as last read.
- **The shared body** carries only layer 0: a layer-1 spawn, change or despawn never enters `_delta`. The type, appearance, string and
  resource table entries of a layer entity are public and travel in the body (an appearance entry says nothing of where an entity is).
- **The blocks** (`Replicator.Layer.cs`): the replicator keeps the layer's ids in id order (`_layerIds`) and the tick's change records
  by id (`_layerChanged`). Each session's `SessionState.UpdateLayer` walks the layer against what its client holds (`_layerHeld`): an
  entity its mob's `see_invisible` reaches and the client lacks is a spawn with its full record; one the client holds and that changed
  this tick, a change; one the client holds that its mob no longer sees, or that left the layer, a despawn. A session without a client
  or a mob sees nothing of the layer. The block's `LayerTick` is the tick.
- **Crossing 25.** An entity whose invisibility crosses 25 is despawned from its layer in the read that sees it and spawned in the other
  in the next read (as a visual that changes kind), so the body and a block never name one id in the same Delta.
- **Joins.** The Snapshot lists layer 0 only; the join's first block (`full`) spawns the layer the joining mob sees.
- **Click targets.** The server refuses a target above the clicking mob's `see_invisible` (`ServerHost.TryResolveTarget`), as BYOND
  never shows it.
- **Cost.** Per session and tick, a walk of the layer's ids (the ghosts on the map), nothing for an empty layer. A layer entity's
  record is encoded once per session that sees it.
- **Tests**: protocol.md section 31.4.

