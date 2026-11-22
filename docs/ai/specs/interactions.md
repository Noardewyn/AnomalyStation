# M8z Basic interactions: design and plan

Written 2026-10-22 against main (0.8.2, protocol 36). tg = `../tgstation/code` (snapshot 1d99bd7f). `Tg/` = `src/Core/Tg/`. Run autonomously
under D178; the owner's scope is D234 (the strip menu, climbing onto tables and railings, pointing, the Sleep, Succumb and Pull verbs, the
rest and sleep buttons and the resist key). The choices are D235-D243, the deviations start at DV150 and the not-translated sections at 67.
The grabs past the passive pull, the fireman carry and piggyback, the table slam and the table flip are M9's (D234).

## 0. What already exists (verified)

- **Done in M8y:** buckling, the drag gestures (interaction.md 11.1: the health doll stands for the dragger), getting out of a closet
  (D229), `/atom/visible_message` to bystanders (`modules/mob/mob.cs:365`; only its emphasis and runechat leaves remain, n-t 65). The cryo
  cell's and the suit storage unit's `container_resist_act` are translated; Meta's other mob containers are generated types nothing ported
  puts a mob into (n-t 65).
- **Grabs:** only the passive pull. A second ctrl-click on a pulled mob reaches `grippedby`, which is `Dm.NotTranslated`
  (`modules/mob/living/living_defense.cs:433`): a live "not ported" path until M9.
- **Strip menu:** absent; the human's `strippable` is deferred (`human.cs:61`). In place: `item_start_equip` (`game/objects/items.cs:1771`),
  `strip_delay`/`equip_delay_other` (`items.cs:264-267`), the status composers (`modules/tgui/status_composers.cs:120`),
  `balloon_alert_to_viewers`, the tgui bridge and the native window kit (D148). tg: `strippable.dm` 529, `carbon_stripping.dm` 136,
  `human_stripping.dm` 328, `StripMenu.tsx` 482 (item pictures by `icon2base64`, `strippable.dm:378`).
- **Climbing:** the climbable element is absent; its `AddElement` calls are deferred at `tables_racks.cs:143`, `:1188`, `railings.cs:25`,
  `crates.cs:41`, `:149`, `:158`, `portable_atmospherics.cs`, `reagent_dispenser.cs`, `safe.cs`; `climb_walkable.cs` is a skeleton;
  `elevation` is translated (M8y). tg: `climbable.dm` 142, `climb_walkable.dm` 45. Skills are not ported (an untrained mind, as
  `_species.cs:951`). The hard part: capsule solidity is one per-tile predicate (`Infrastructure/Locomotion/LocomotionSeam.IsSolid`, `:187`)
  shared with the client's MapGrid bits; a climber standing on a dense table tile and tg's `TRAIT_ON_CLIMBABLE` passage between tables have
  no place in it yet.
- **Pointing:** shift-middle-click reaches the server (`godot/scripts/GameScene.Interaction.cs:552`; `click.cs:164` calls
  `ShiftMiddleClickOn`, which is `NotTranslated` at `click.cs:703`); `obj_effect_temp_visual_point` is generated; the ID card's `on_pointed`
  listener exists (`cards_ids.cs:260`); `screen_gen.png` is present. Missing: `modules/point/point.dm` (139 lines), `living.dm:545-555`.
- **Verbs:** the verb layer is M9-M10 (translation-rules 7.10). Pull: the context-target body (`living.cs:651`) and D230's radial entry.
  Succumb: the body `__gvb_succumb` is translated (`living.cs:~690`), its verb wrapper (`:725`) and the succumb alert's Click
  (`_onclick/hud/screen_objects/alert.cs:613`, needs `tgui_input_text`) are not. Sleep: `mob_sleep` is `NotTranslated` (`living.cs:789-795`).
  `tgui_input_text` is not translated (`modules/tgui_input/` has alert, list and number).
- **HUD and keys:** the rest and sleep buttons are deferred (`_onclick/hud/human.cs:41`); `update_rest_hud_icon` is `NotTranslated`
  (`living.cs:1011`); the resist button exists, the resist key does not; `KeybindingCode` ends at `StopPulling = 9`
  (`src/Protocol/MessagesV4.cs:76`), so a new code is a protocol bump; tg's resist key B is free in the client's defaults
  (`Shared/Play/PlayOptions.cs:141`).

## 1. Slices

Two agents at a time: B with C, then D, then the closing. Protocol 37 is shared: the first slice to merge creates `MessagesV37.cs`, the
others add to it; one release closes it (D243).

| Slice | Who | Content | Ids |
|---|---|---|---|
| B strip menu | Sonnet | Server: `strippable.dm` whole, `carbon_stripping.dm`, `human_stripping.dm` (the jumpsuit, sensor, accessory, shoe-knot and internals alternate actions, `GLOB.strippable_human_items`), the human's `should_strip`; the element attached in `human.cs:61`; the radial's "Strip" entry (D238). Client: `StripMenu.cs`, a literal translation of `StripMenu.tsx` (slot grid, obscured and hidden slots, alternate actions, item pictures as icon and state, D236). Protocol 37: `ContextHintAction.DropOntoSelf`. Gameplay `StripGameplayTests` on Meta: Bob downed; Alice drags him onto her doll, takes his shoes (tg's `strip_delay`), his ID and a pocket item (Bob reads "You feel someone fumble with your belongings."), toggles his suit sensors, puts the shoes back (`equip_delay_other`); Carol reads "Alice tries to remove Bob's shoes." | DV150-152, n-t 67 |
| C climbing | Opus, parallel with B | Server: `climbable.dm` and `climb_walkable.dm` whole; every deferred `AddElement` on Meta's types goes live (tables, racks, railings, crates with their open and closed climb times, canisters, tanks and dispensers, space heaters, the safe); the capsule rule for climbers (D239); the radial's "Climb" entry (`ContextHintAction.DropSelf`). Client: an upright mob on a climb-walkable tile stands at its structure's model top, camera included (D239); the prediction flag (`MotionParams.OnClimbable`, as DV29's `NoClip`); a MapGrid bit for climb-walkable tiles (protocol 37). Gameplay `ClimbGameplayTests` on Meta: onto a table (tg's 2 s and the 2 s stun), walk to the next table, step down; vault a railing; climb a closed crate; a click knocks a climber off | DV153-157, n-t 68 |
| D point, verbs, HUD buttons, resist key | Sonnet, after B | Server: `point.dm` whole (`point_at`, the point bubble, the point and holo visuals), `ShiftMiddleClickOn`, `/mob/pointed`, `_pointed`, the living overrides (`living.dm:545-555`; the carbon no-hands emote branch deferred to M9's emotes); `mob_sleep` and the succumb wrapper through the Verb action (D240); the succumb alert's Click with `tgui_input_text` (D241); the `rest` and `sleep` screen objects (`screen_objects.dm:587-641`) in `hud/human.cs:41` with `update_rest_hud_icon`; `/datum/keybinding/living/resist`. Client: `PlayAction.Resist` on B; the radial opened over nothing lists Sleep and Succumb; the `TextInputModal` window; the point arrow and bubble (D237). Protocol 37: `KeybindingCode.Resist`, `ActionKind.Verb` with a `VerbCode` (Sleep, Succumb). Gameplay `PointGameplayTests` (Carol's chat line and the arrow for 2.5 s; the bubble for an item in Alice's own hand) and `VerbHudGameplayTests` (B resists out of a pull, a cuffed resist, out of a suit storage unit and a cryo cell; the rest button; the sleep button answered Yes; the succumb alert in crit; the Sleep verb from the radial; "You are unable to succumb to death! This life continues." when healthy) | DV158-160, n-t 69 |
| Closing | agent | `M8zScenarioTests` (D242), the full suites, ROADMAP status, CHANGELOG, C4 lines, release 0.8.3 with protocol 37 (D243) | - |

## 2. Done when

ROADMAP M8z: on MetaStation a player strips a downed friend's items and dresses him, climbs onto a table and a railing, points at a thing
his friend sees pointed at, sleeps, succumbs in crit and pulls by the verb, rests and resists by the HUD buttons and the hotkey; each with
tg's delays, messages and sounds and a gameplay test; `M8zScenarioTests` runs twice from a fresh boot for the same state hash.
