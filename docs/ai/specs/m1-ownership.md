# M1 file ownership

Status: v1 (the M1 contracts step, M1.0), 2026-10-07. Who edits which file while the M1 packages work in parallel (lead plan,
`scope.md` section 3): P1 the client layer (tg translation and engine parts), P2 protocol and transport, P3 the server host,
P4 replication, P5 bots and soak, P6 the Godot client, and T1, an independent tester of P1. The contracts commits declared
everything two packages share, so that every package compiles against the others' declarations and edits only its own
files. Paths are repository paths.

## Rules

- **One owner per file.** A package edits only the files of its rows. It may create new files inside the folders its rows
  give it (and, for P1, new mirror files of tg files that no row names) and lists them in its report. A file that no row
  names is not edited: report the needed member to the lead (a `// GAP(<owner>): ...` comment at the call).
- **Contracts are frozen.** The public types, members and signatures the contracts step wrote (the protocol messages,
  `NetField`, the codec entry points, `ReplicaWorld`, the transport interfaces, `IReplicator`, `ServerConfig`, `Session`,
  `QueuedCommand`, `CommandQueue`, `ConsoleCommand`, the `Bot` API, `ClientSeam`, `IClientHost`, `ClientConnection`,
  `TestRoom`, `MapView`, the tg skeletons with their `slots.tsv` signatures) do not change without the lead. The owner fills
  the bodies (`throw new NotImplementedException("M1 Pn")` in new logic, `Dm.NotTranslated` in tg translation, rules 1.4),
  may add private members and new types, and may add public members a contract does not have (it reports them).
- **The meaning in the doc comments is the contract.** Tick numbering, the order of the join sequence, `lastInputSeq`, the
  type table, the change masks (`Anomaly.Protocol.Messages`, `NetField`), the transport threading rule (D31: events only in
  `Poll` on the calling thread), the replicator's read rules (`IReplicator`), the client seam's order and `usr`
  (`ClientSeam`): a package that finds one wrong for the real code reports it before working around it.
- **Overrides are not declared** (as in wave 4), with one exception the contracts asked for: `client/Destroy` (a
  skeleton next to `client/New` and `client/Del`; nothing qdels a client before P1). The skeletons declare the roots of the
  slots their files define; an override of a slot rooted elsewhere (`persistent_client/New` and `Destroy`, `/atom/movable/keyLoop`,
  `SSinput`'s `Initialize`, `fire`, `can_queue_verb`, `run_verb_queue`, `stat_entry`, `SSverb_manager`'s `fire`) is added
  by the owner of its file, so that the base keeps running until then.
- **Bodies throw until their owner lands.** A test that reaches another package's skeleton fails, as intended. Test your
  own code with your own fixtures and, for P3, P4 and P5, against the loopback transport once P2 has merged. **Merge
  order: P2, P1 (with T1), P4, P3, P5, P6**, then the lead's integration (the loopback end-to-end and real-socket tests,
  the soak, apps.md section 3, the WORKLOG entry).
- **Specs.** P2 writes `docs/ai/specs/protocol.md` (the byte layout, the encoding of every message, the loopback's
  semantics); P4 writes `docs/ai/specs/replication.md` (the mechanism, R1 or R3, and its measured table, scope 2d); P3
  rewrites `docs/ai/specs/apps.md` section 1 (the server), P6 section 2 (the client), the lead section 3.
- **Shared documents** (`docs/ai/DECISIONS.md`, `ROADMAP.md`, `WORKLOG.md`, `DEVIATIONS.md`, `docs/ai/translation-rules.md`
  Appendix A and C, `docs/ai/specs/object-model.md`, `presentation-seam.md`, `waiting-model.md`, `shim-ownership.md`,
  `dm-conformance.md`): the lead at merge; packages report additions (a new deviation with its `// DEVIATION(DVn)` text, a
  decided slot, a new public name of the Kernel, the shim or the Tg infrastructure). `docs/ai/specs/not-translated.md`:
  P1 appends the rows of its own deferred leaves and stubs; the lead checks them at merge.
- **The shim, the Kernel and the atom engine parts** (`src/Core/DmShim/`, `src/Core/Kernel/`, `Builtin/atom.builtin.cs`,
  `atom_movable.builtin.cs`, `MapGrid.cs`) are not edited by the packages; a missing member is reported
  (`// SHIM-GAP: ...` at the site).
- **The solutions and project files** (`AnomalyStation.sln`, `godot/Anomaly.Client.sln`, every `.csproj`,
  `Directory.Build.props`) are the lead's, except `godot/Anomaly.Client.csproj` (P6, which keeps the Protocol and Net
  references and the ExportRelease mapping) and `tools/Anomaly.Tools.Bots/Anomaly.Tools.Bots.csproj` (P5). A package that needs a
  new project or package reference asks the lead; P4's measurement harness lives in its test folder (tests marked
  `[Trait("Category", "Bench")]`, run on demand) or in the scratchpad, not in a new project.
- **Tests.** P2 to P5 test in `tests/Anomaly.Server.Tests` (folders below; the project already references Server, Protocol,
  Net, Bots and Tg); P1 and T1 in `tests/Anomaly.Core.Tests/Tg/M1Client/`. The M1 client path runs with
  `DmWorldState.NotTranslatedThrows = true` in tests (scope 2j), so no leaf is reached silently.
- **D31 everywhere new.** No wall clock, thread or static mutable state in the core (`CoreHasNoWallClockTests`,
  `NoStaticStateTests`); the server host may use a wall clock for pacing and metrics, and LiteNetLib's threads only move
  bytes. Host randomness (tokens) never enters the world.

## Things every package should know (contracts findings)

- **The join sequence travels on one channel.** `Welcome`, `MapGrid`, `Snapshot` and the `Delta`s all go on the State
  channel, so a client sees them in that order; `ServerText` and `InputFrame` use Control. A session that received the
  Snapshot of tick T gets exactly the Deltas of T + 1, T + 2, ...
- **One Delta body per tick.** `Codec.EncodeDeltaBody` once per tick, `Codec.EncodeDelta` (a small per-connection header:
  tick, `lastInputSeq`, own entity) per session. The entity type table is the server's, append-only for the run (an index
  per DM path at its first replication; `Delta.NewTypes` carries the additions), so every joined connection holds a prefix
  of one table. The own-mob flag (`NetField.OwnMob`) is never in an entity record: it is the own entity id of the headers.
- **The loopback counts latency in network steps**, advanced by the test (`LoopbackNetwork.Advance`, once per server
  tick): deterministic and clock-free. Reliable channels never lose (a "loss" is a retransmission delay with head-of-line
  blocking); loss and reorder apply to the unreliable channel only.
- **Replication reads outside the world** (`IReplicator`): after `World.Step`, with no ambient world, only reading, no
  strong reference kept across ticks; the map through `Anomaly.Core.Tg.MapView`. Hints: `PresentationSinks.Replication`
  (moved, appearance, visibility, deleted) and the creation watermark `ObjectRegistry.HighestId`; object-model.md 6.1. A
  world without a sink pays one null check per hint site (measured: a raw `loc` write and a tg `step()` cost the same as
  before within noise).
- **The client seam** (`src/Core/Tg/Infrastructure/ClientSeam.cs`): P3 creates the avatar (`new /mob` at a
  `TestRoom.SpawnPoints` cell), keys it (`ClientSeam.KeyMob`), then `ClientSeam.Connect`; it sends the Welcome once the
  returned `ClientConnection` is `Done` and `Accepted`; `SetHeldDirections` for an InputFrame; `Disconnect` when the
  transport connection ends. The engine part tells the host through `IClientHost` (`ClientDeleted`, `ClientMobChanged`),
  installed per world with `ClientSeam.SetHost`. `to_chat` reaches the host through `IMessageSink.Chat`.
- **`client.mob` is a property** (BYOND's engine behaviour on assignment, P1's `dm_set_mob`); `mob.key`, `mob.ckey` and
  `mob.client` stay plain fields until P1 decides (changing a field of `/mob` changes every mob's state hash: record the new
  toy-world hash). Inside `/client`'s methods `x is mob` and `mob.focus` both compile (C#'s Color Color rule; checked).
- **`slots.tsv` writes `public new virtual` for `/client`'s `New` and `Del`**; C# hides by signature and `/datum` has
  `New(params object[])` and no `Del`, so `new` would be warning CS0109 and is left out (a RULE-GAP comment at each). The
  binder still calls `client/New` for `Dm.@new` of a client (the most derived declaration of a name).
- **Making a client in a test.** tg's `client/New` is a skeleton until P1 lands, and afterwards it attaches and logs in; a
  test that only needs a client object for a mob's `client` var uses the fixture `client_test_m1_client`
  (`tests/Anomaly.Core.Tests/Tg/TgTestClient.cs`, `/client/test_m1_client`, whose `New` does nothing). A real connection is
  `ClientSeam.Connect`.
- **The toy world's hash moved** from `0x990262cc003e5804` to `0xb9d4100a6423c28e` (seed 1) with the stubs of
  `SSverb_manager` and `SSinput` (Master/New creates one more datum per subsystem type) and the two GLOB lists of
  `persistent_client.dm`; without those two the contracts change nothing (`M1Seams/ReplicationHintHashTests`). P1 changes
  it again when it makes SSinput live and keys mobs; that is expected, recorded, not pinned.
- **LiteNetLib 2.1.4** (scope 2e): unreliable messages over 1023 bytes throw; the NuGet build's packet loss and latency
  simulation does nothing (use the loopback); normal mode starts 2 to 4 threads per manager, so bots use manual mode;
  events fire only in `PollEvents`.

## Files by owner

| File or folder | Owner | State after the contracts step | Others use |
|---|---|---|---|
| `src/Protocol/**` (`ProtocolVersion`, `MessageId`, `Limits`, `ProtocolException`, `Messages.cs`, `NetField`, `WireWriter`, `WireReader`, `Codec`, `ReplicaWorld`) | P2 | contracts: types, fields and doc complete; codec, reader, writer and replica bodies throw | P3, P4, P5, P6 |
| `src/Net/**` (`Transport.cs`: `Channel`, `IServerTransport`, `IConnection`, `IConnectionRequest`, `IClientTransport`; `LiteNetTransport.cs`; `LoopbackTransport.cs`) | P2 | interfaces complete; LiteNetLib and loopback skeletons | P3, P5, P6 |
| `tests/Anomaly.Server.Tests/Protocol/**`, `Net/**`; `docs/ai/specs/protocol.md` (new) | P2 | not created | - |
| `src/Core/Tg/modules/client/client_defines.cs` | P1 | complete: the var block (P1 fixes a var it finds wrong and reports it) | P3 (reads client vars) |
| `src/Core/Tg/Builtin/client.builtin.cs` | P1 | BYOND's client vars with their defaults; `client.mob`'s engine method throws | P3 |
| `src/Core/Tg/Builtin/mob.builtin.cs` | P1 | as in M0 (`key`, `ckey`, `client` plain fields) | P3, P4 |
| `src/Core/Tg/modules/client/client_procs.cs` | P1 | skeleton: `New`, `Del`, `Destroy` | P3 (through `ClientSeam`) |
| `src/Core/Tg/modules/client/persistent_client.cs` | P1 | the GLOB lists and the var block translated; the roots skeletons | - |
| `src/Core/Tg/modules/client/**` (new files), `modules/keybindings/**` (`bindings_client.cs`, `bindings_atom.cs`, `setup.cs` skeletons; `focus.cs` from wave 4) | P1 | skeletons | - |
| `src/Core/Tg/controllers/subsystem/input.cs`, `verb_manager.cs` | P1 | inert stubs (`STUB(M1 P1)`) with their roots declared | - |
| `src/Core/Tg/modules/mob/login.cs`, `logout.cs`, `mob_movement.cs` (client/Move declared, the GAP at its `holder` read), `mob.cs`, `mob_lists.cs`, `__HELPERS/logging/_logging.cs`, `game/world.cs` (`world/Del`), `datums/progressbar.cs`, `datums/cogbar.cs` | P1 | skeletons and the M1 leaves (`Dm.NotTranslated` naming M1 or the client layer) | P3 (`world/Del` on `quit`) |
| the type-only skeletons the contracts made for the var blocks (`modules/client/preferences.cs`, `modules/tooltip/tooltip.cs`, `datums/view.cs`, `_onclick/hud/parallax/parallax.cs`, `modules/lootpanel/_lootpanel.cs`, `modules/admin/admin_verb_panel.cs`, `modules/tgui/tgui_window.cs`, `datums/verb_callbacks.cs`, `datums/achievements/_achievement_data.cs`; the added types in `_onclick/click.cs` and `modules/admin/admin.cs`) | P1 | type only (rules 2.3) | - |
| `src/Core/Tg/Infrastructure/ClientSeam.cs` | P1 (the host entry points `KeyMob`, `Connect`, `SetHeldDirections`, `Disconnect` and the filling of `ClientConnection`) | the engine-to-host half complete (contracts; P1 does not change it); the entry points throw | P3 |
| `tests/Anomaly.Core.Tests/Tg/M1Client/P1/**` | P1 | not created | - |
| `tests/Anomaly.Core.Tests/Tg/M1Client/T1/**` (DM-behaviour tests of P1, written from the DM code: keyLoop's direction rules, client/Move's move_delay, Login and Logout list membership, attach by key, a key assignment moves the client) | T1 | not created | - |
| `src/Server/**` except `src/Server/Replication/**` (`ServerConfig`, `Session`, `Commands.cs` contracts; `Program`, `ServerLoop`, `ServerOptions`, `TickPacer`, `SmokeLoad` from M0, replaced or kept by P3) | P3 | contracts with throwing bodies; the M0 command line still works | P4, P5 |
| `src/Core/Tg/Infrastructure/TestRoom.cs` | P3 | `Build` throws | - |
| `tests/Anomaly.Server.Tests/Server/**`; `tests/Anomaly.Core.Tests/Apps/**` (the M0 server tests: P3 updates them as it replaces the command line); `config/server.txt` (new, an example); `docs/ai/specs/apps.md` section 1 | P3 | - | - |
| `src/Server/Replication/IReplicator.cs` | contracts (frozen) | complete | P3 calls, P4 implements |
| `src/Server/Replication/**` (the implementation), `tests/Anomaly.Server.Tests/Replication/**`, `docs/ai/specs/replication.md` (new) | P4 | not created | P3 |
| `tools/Anomaly.Tools.Bots/**` (`Bot`, `BotOptions`, `BotBehaviour`, `Program` contracts), `tools/soak/**` (new), `tests/Anomaly.Server.Tests/Bots/**` | P5 | contracts; `Program` prints "not implemented" | lead (integration) |
| `godot/scripts/Shared/**` (the client's Godot-free logic, such as the view-relative input mapping; also compiled into `tests/Anomaly.Server.Tests` as linked files), `tests/Anomaly.Server.Tests/Client/**` | P6 | not created | - |
| `godot/**` (scenes, scripts, `tools/smoke.sh`, `Anomaly.Client.csproj`), `docs/ai/specs/apps.md` section 2 | P6 | csproj and `godot/Anomaly.Client.sln` reference Protocol and Net | - |
| `tests/Anomaly.Server.Tests/ProjectsLoadTests.cs`, `tests/Anomaly.Server.Tests/Integration/**` (the end-to-end tests, apps.md 3.3), `tests/Anomaly.Core.Tests/M1Seams/**`, `tests/Anomaly.Core.Tests/Tg/TgTestClient.cs` | lead | complete (Integration: the M1 integration) | all |
| `src/Core/Tg/Infrastructure/MapView.cs`, `ToyScenario.cs`, `ToyRoom.cs`, `TgWorld.cs`, `TgState.cs`; `src/Core/Kernel/**`, `src/Core/DmShim/**`, the atom engine parts | lead | complete | all |
| `AnomalyStation.sln`, `godot/Anomaly.Client.sln`, the `.csproj` files but P5's and P6's, `Directory.Build.props`; `docs/ai/specs/m1-ownership.md`; `docs/ai/specs/apps.md` section 3 | lead | - | all |

## The interfaces each package implements or calls

| Contract | File | Implemented by | Called by |
|---|---|---|---|
| `Codec` (one `Encode`/`TryDecode` per message; `EncodeDeltaBody`, `EncodeDelta`), `WireWriter`, `WireReader` | `src/Protocol/Codec.cs`, `WireWriter.cs`, `WireReader.cs` | P2 | P3, P4, P5, P6 |
| `ReplicaWorld`, `ReplicaEntity` | `src/Protocol/ReplicaWorld.cs` | P2 | P5, P6, tests |
| `IServerTransport`, `IConnection`, `IConnectionRequest`; `LiteNetServerTransport`, `LoopbackServerTransport` | `src/Net/Transport.cs`, `LiteNetTransport.cs`, `LoopbackTransport.cs` | P2 | P3 |
| `IClientTransport`; `LiteNetClientTransport`, `LoopbackClientTransport`; `LoopbackNetwork` | `src/Net/*.cs` | P2 | P5, P6, tests |
| `IReplicator` | `src/Server/Replication/IReplicator.cs` | P4 | P3 |
| `IReplicationHintSink` | `src/Core/DmShim/Presentation.cs` | P4 | the atom engine parts, the registry (complete) |
| `ServerConfig.Parse`, `CommandQueue`, `ConsoleCommand.TryParse`, `Session`'s state machine | `src/Server/ServerConfig.cs`, `Commands.cs`, `Session.cs` | P3 | P3, P5 (soak script options) |
| `TestRoom.Build` | `src/Core/Tg/Infrastructure/TestRoom.cs` | P3 | P3 |
| `ClientSeam.KeyMob`, `Connect`, `SetHeldDirections`, `Disconnect`; `ClientConnection` | `src/Core/Tg/Infrastructure/ClientSeam.cs` | P1 | P3 |
| `IClientHost` | `src/Core/Tg/Infrastructure/ClientSeam.cs` | P3 | P1's `/client` engine part, through `ClientSeam.NotifyClientDeleted` and `NotifyClientMobChanged` |
| `Bot`, `BotOptions` | `tools/Anomaly.Tools.Bots/Bot.cs` | P5 | the lead's integration tests, the soak script |
