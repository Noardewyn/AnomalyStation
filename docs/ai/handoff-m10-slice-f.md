# M10 slice F handoff (2026-10-24; finished 2026-10-25)

**Finished.** The items below were done on 2026-10-25; the slice's record is `docs/ai/specs/multiplayer.md` section 9. Left open: the real
Godot client's lobby screen (never run), the two `BridgeTests` failing since b4db6855. The text below is the handoff as it was.

## Done (builds clean: Core, Server, Godot client; 0 warnings)

- **Core, translated from tg**: SSticker up to PLAYING (`controllers/subsystem/ticker.cs`); `/mob/dead/new_player`,
  its login/logout, `preferences_setup.cs`, `latejoin_menu.cs` (`modules/mob/dead/new_player/`); `modules/lobby_menu/` (lobby menu
  datum + processing SS); SSjob assignment (`controllers/subsystem/job.cs`), `_job.cs` spawn procs, `job_exp.cs`, legacy
  `jobs.txt` loader (`configuration/entries/jobs.cs`, `validate_job_config`); the preference framework (`modules/client/preferences/*`,
  `preferences.cs` incl. its tgui ui_* procs, `preferences_savefile.cs`, `datums/json_savefile.cs`, middleware `random.cs`);
  helpers (`sanitize_values.cs`, `reject_bad_name`, `repeat_string`, `get_policy`, `is_file_type_in_list`, `living_player_count`,
  `announce_arrival`, `log_manifest`, `toggle_ooc/dooc`, `check_rights_for`, `is_banned_from` stub, `spawn_text_overlay.cs`,
  `loadout_helpers.cs`, `new_player_state`); species `get_selectable_species`, `on_species_loss`, `get_features`,
  `clear_tail_moodlets`, `remove_body_markings`; manifest `build`; sites that skipped "prefs null" now read prefs
  (hud.cs, ghost.cs, action_button.cs, sound.cs, alert.cs, tooltip.cs, login.cs, client_procs keybinds).
- **Generated types**: hand-written [DmPath] classes had their generated `[DmPath...]` lines stripped by hand
  (`tools/strip-generated-dmpath.py`, run from the repo root with the DM paths) and `datum_preferences.path` removed from datum.g.cs; the chair JoinPlayerHere stub removed from
  `Generated/LivePartial/obj.g.cs` and `docs/ai/specs/live-partial.txt`. **Still to do: rerun tools/Anomaly.Tools.Types** (types-converter.md
  section 1) so the generated files are regenerated properly, and commit them separately.
- **Server**: `ServerConfig.Lobby` (`--lobby`, `LOBBY`; on in config/server.txt). Without it the round starts at once and joins open
  once it is in progress; each join late-joins as an assistant at the host's spawn point (`ServerHost.World.cs` `DirectJoins`,
  jobspawn_overrides). With it: `OVERFLOW_JOB Station Engineer`, `LOAD_JOBS_FROM_TXT`, host-written `config/jobs.txt`
  (`ServerHost.TgJobsText`). ApplyJoin no longer makes a human (BYOND's world.mob = new_player); ClientMobChanged registers living
  mobs with locomotion. Lobby window: `Limits.LobbyUiWindow` (=200) / `LobbyUiInterface` "LobbyMenu"; replicator maps tg's
  "lobby_menu" window's init/state messages (`Replicator.Tgui.cs`), `ApplyLobbyAct` maps acts to tg's "action" message.
  No protocol change (stays 43).
- **Client**: `Hud/LobbyScreen.cs` (tgui-lobby translated: sprites, hover/disabled, APNG flicks via `Hud/ApngFrames.cs`, shutter/
  collapse, info TV, sounds), wired in `TguiHost` (lobby under other windows) and `GameScene.Interaction.cs`; `TguiWindows` keeps the
  lobby at the bottom and Esc does not close it. Interfaces `JobSelection.cs`, `PreferencesMenu.cs` (name, gender, age; D267).
  Section renderer got StyleableSection background/title colour/title node. Icons tool copies `icons/hud/lobby/png/` (run done).
- **Docs**: D270 (DECISIONS.md), DV180-DV182 (DEVIATIONS.md), not-translated.md section 79, CHANGELOG [Unreleased] lines.
  `docs/ai/specs/multiplayer.md` section 9 is NOT written yet.
- **Tests**: `tests/Anomaly.Server.Tests/Integration/LobbyGameplayTests.cs` (Meta, lobby on). Passes up to the end: Alice in the lobby,
  PreferencesMenu edits (name "Ada Lovelace", female, 40), ready, countdown, spawns as engineer at an engineer start with tg's
  outfit; Bob late-joins via JobSelection as engineer. The rig (`IntegrationRig`) is unchanged; ReconnectTests and CrewOutfitTests pass.

## Where it stopped

1. LobbyGameplayTests fails only on the runtime count (3 runtimes after boot):
   - `fade: M4a P2 (skeleton)` (screen_objects.dm:1078): `atom_movable_screen_splash.fade` is called by `transfer_characters` and
     new_player Initialize: translate `/atom/movable/screen/splash` fade (tg `_onclick/hud/screen_objects.dm`).
   - `Unable to find last resort spawn point` at the late join: the start landmarks are deleted after the round start
     (landmarks.dm after_round_start), so DV180's fallback finds none and Bob lands at the error room (4,4). Fix DV180: remember the
     station's start landmark turfs at the round start (or use the job's roundstart turfs), or bring slice G's arrivals shuttle.
   - (`monitored_area` at tick 20 is a boot runtime, already counted before.)
2. A full Server suite run (started before the last fixes) showed ~156 failures: not analysed yet. Its output:
   the failing test names are in `docs/ai/handoff-m10-slice-f-failures.txt` (the messages are not kept; rerun `dotnet test tests/Anomaly.Server.Tests`). Likely causes to
   check first: tests that assumed joins at tick 20 / the old CrewStandIn names or outfit, the ticker now firing (runlevels: the world
   is RUNLEVEL_LOBBY until the round starts; without the lobby the round starts at the ticker's first fire), the prefs now non-null
   (other `prefs != null` NotTranslated sites), the HostV4/DevCommand tests, the Meta tests' join spawn points.
3. Core tests not run yet (Core boot tests may hit the ticker's Initialize: `strings/round_start_sounds.txt` must be mounted, else
   `pick()` of an empty list).
4. Then: multiplayer.md section 9 (results, the assistant stays open at late join by tg's newbie rule without a database:
   `player_age` -1), memory update (next ids D271, DV183, n-t 80), types tool rerun, commit, real Godot client check of the lobby
   screen (never run yet), release 0.9.3 is not needed (no protocol change) unless asked.
