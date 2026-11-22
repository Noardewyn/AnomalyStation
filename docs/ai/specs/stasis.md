# M8y Stasis beds: design and plan

Written 2026-10-22 against main (0.8.0 + M8x, protocol 35). tg = `../tgstation/code` (snapshot 1d99bd7f). `Tg/` = `src/Core/Tg/`,
`Gen/` = `src/Core/Tg/Generated/Types/`. Run autonomously under D178: the choices are D218-D222.

## 0. What already exists (verified)

- `/obj/machinery/stasis` and `circuitboard/machine/stasis` (tg `machine_circuitboards.dm:958`) are generated stubs (`Gen/obj.machinery.g.cs`,
  `Gen/obj.item.circuitboard.g.cs`); `/datum/status_effect/grouped/stasis` and `/atom/movable/screen/alert/status_effect/stasis` are generated;
  `/datum/component/free_operation` and `/datum/element/elevation` are generated stubs. The grouped status effect base is translated
  (`Tg/datums/status_effects/neutral.cs`, `heldup`).
- The TRAIT_STASIS consumers on the station's paths are translated: `living/life.cs:75` (no Life tick body), `carbon/life.cs:44`,
  `human/life.cs:54`, `living.cs:743`, `_species.cs:1259`, the wounds (`bones`, `burns`, `pierce`, `slash`, `bruised`), `patch.cs:186`,
  `irradiated.cs:150`, `drowsiness`, `tox_vomit`, `init_signals.cs:59` (tumor suppression), `_bodyparts.icons.cs:559`.
- **Buckling is NOT translated** (`Tg/game/objects/buckling.cs`: `buckle_mob`, `unbuckle_mob`, `user_buckle_mob`, `mouse_buckle_handling`
  and the hand unbuckle are `Dm.NotTranslated`; the chair's `post_buckle_mob` rows are live-partial "buckling, M9"). tg: `game/objects/buckling.dm`
  (388 lines), `/mob/living/proc/set_buckled` (`living.dm:2577`), `resist_buckle` (`living.dm:1252`), the unit test `unit_tests/buckle.dm`.
- `SSvis_overlays.add_vis_overlay` is translated (`Tg/controllers/subsystem/vis_overlays.cs`); the ripple filter kind exists on both sides
  (`Tg/__HELPERS/filters.cs`, `godot/scripts/Shared/Icons/AppearanceCompositor.FilterKinds.cs`); `end_metabolization` is translated.
- Meta maps two beds in the Treatment Center (79,105 and 85,105, D217; one facing south, one `dir = 4`), each under a defibrillator mount.
  tg has no unit test for the stasis bed.

## 1. Slices

| Slice | Who | Content | Ids |
|---|---|---|---|
| A buckling | Opus | (widened by D223) every buckle-capable type Meta maps or medbay uses (chairs, stools, beds, roller beds, wheelchairs, the operating table) with its buckle procs; `buckling.dm` whole (atom/movable and obj), `set_buckled`, `resist_buckle`, the buckled mob's movement and lying angle (`buckle_lying`), the chair and bed `post_buckle_mob`/`post_unbuckle_mob` rows that are live-partial "buckling"; tg's `unit_tests/buckle.dm`; a gameplay test (buckle a friend to a chair and to a roller bed by drag, unbuckle by hand, resist out while cuffed) | DV126-129, n-t 61 |
| B stasis server | Sonnet, parallel with A (its buckle-driven tests wait for A's merge) | `stasis.dm` whole; `status_effect/grouped/stasis` (`debuffs.dm:340-385`: the traits, `update_time_of_death`, `end_metabolization`, the ripple filter and its animation, the alert); `free_operation` (its traits; surgery itself waits); `elevation` element as far as the bed needs it; the board and construction; unit tests (the status effect directly, the bed's toggle cooldown, power, `process`) | DV130-133, n-t 62 |
| C client and gameplay | Opus, after A and B | the bed's states (`stasis`, `_broken`, `_maintenance`) and its mattress vis overlay fading in and out over 5 s (CUBIC), the patient lying on it at the elevation, the ripple on the patient, the Stasis alert, the sounds (`synth_yes`/`synth_no` at their random frequency, `click`, `spray`); `StasisGameplayTests` on Meta | DV134+, n-t 63 |
| D drag gestures | after A (Sonnet or Opus, whichever is free) | D224 with D225's rule and D226's targets and refusals, and D230's Buckle, Unbuckle, Pull and Stop pulling entries in the radial menu: the server decides whether tg handles the pair (own handler procs or signal listeners) and otherwise runs the gesture as tg's own actions; the client needs no new message (protocol.md 26's drop carries source and receiver, a doll slot and a hand slot included); gameplay tests on Meta for each gesture | DV138+, n-t 64 |
| Closing | main / agent | `M8yScenarioTests` (D221), the full suites, ROADMAP, the C4 lines, release 0.8.2 (D222) | - |

## 2. Done when

ROADMAP M8y: a dying patient buckled to a powered bed in stasis does not get worse (no damage from bleeding, no oxygen loss, his reagents
not metabolized) until he is taken off, and a medic treats him on it as tg allows; the bed stops with the room's power; a scenario on
MetaStation runs twice with the same state hash (D221).
