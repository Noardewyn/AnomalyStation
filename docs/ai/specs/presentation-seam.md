# Presentation seam: messages, sounds, appearance and light

Status: v1 (wave 0), 2026-10-05. Contract between translated tg code, the DmShim and the hosts (the headless test world now, the server in M1, the Godot client behind it). Translation forms are in `docs/ai/translation-rules.md` section 12; object identity and builtin vars in `docs/ai/specs/object-model.md`.

## 1. Principle

tg's presentation **logic** is translated literally, like any other tg code (D83, D86): who sees and who hears (`visible_message`, `get_hearers_in_view`, blindness and deafness checks), sound falloff, pressure and wall checks (`playsound`, `playsound_local`), what an atom looks like (`update_appearance`, `update_icon_state`, `update_overlays`, `set_light*`). Only **BYOND's output primitives** are replaced: the engine services that hand data to a client (the tgchat transport, the `<<` output operator, sound delivery, `animate`, `flick`, client images, appearance rendering, tg's lighting engine). They become three world-owned **sinks**.

Rules for every sink (they keep the core deterministic and replication-friendly):

1. **Write-only.** Sink methods return nothing, and nothing a sink does feeds back into the simulation. The simulation never asks a sink a question (no "can this client see that", no measured text width).
2. **Synchronous and ordered.** Sinks are called during the tick, at the point where tg's code would have produced the output, in the deterministic order of the simulation. A recording sink therefore yields an exact order-of-events trace.
3. **DM values as payload.** Arguments are the DM values tg computed (atoms, mobs, clients, text, `DmList`s, the builtin `sound` object). The server maps objects to `dm_id`s immediately, because the objects keep changing later in the tick.
4. **Registered objects only.** Nothing is reported for an object with `dm_id == 0` (a prototype or an object still in its constructor; object-model.md section 2).
5. **State is state, events are events.** Appearance vars (`icon_state`, `dir`, `overlays`, `color`, light vars ...) are ordinary simulation state that replication (M1) diffs from shadow copies; the sink's change notification is only a dirty hint, so a missed hint cannot desynchronize a client. Messages, sounds, `animate`, `flick` and balloon alerts are events.
6. **No-op by default.** Every world starts with null sinks; headless tests run with them or with recording sinks.

## 2. Where the sinks live

The interfaces are in `Anomaly.Core.DmShim` (they stand for BYOND engine services; targets are typed `object` because atoms, mobs and clients are tg types). Each world has one set, in the shim's per-world state (object-model.md section 10):

```csharp
public sealed class PresentationSinks
{
    public static PresentationSinks Of(World world);      // the world's set; created with null sinks
    public IMessageSink Messages { get; set; } = NullSinks.Messages;
    public ISoundSink Sounds { get; set; } = NullSinks.Sounds;
    public IAppearanceSink Appearance { get; set; } = NullSinks.Appearance;
}
```

Since M1 the set has a fourth member, `IReplicationHintSink Replication { get; set; }` (null by default, no no-op stand-in): the replication hints of rule 5 (moved, appearance changed, visibility changed, deleted), which the server's replicator installs; docs/ai/specs/object-model.md section 6.1 lists their sources. Hosts install their implementations (`PresentationSinks.Of(world).Messages = ...`). Translated code never calls a sink directly; it reaches them only through the shim builtins (`Dm.output_to`, `animate`, `flick`, the builtin var setters) and through the few tg procs replaced at proc level (section 6). Tests use `RecordingSinks`, which append one line per call (`"tick 12 chat mob#41 notice: You start prying the crowbar..."`).

## 3. Messages: `IMessageSink`

```csharp
public interface IMessageSink
{
    /// tg chat transport (code/modules/tgchat): what SSchat.queue / SSchat.send_immediate would send.
    /// target: a client, a mob, world, or a DmList of those, exactly as tg passes it to SSchat.
    /// message: tg's associative list with the keys "type", "text", "html", "avoidHighlighting".
    void Chat(object target, DmList message);

    /// BYOND `target << text` (DIRECT_OUTPUT with text): plain output to a client, mob or world.
    void Output(object target, string text);

    /// tg balloon alert: the floating text one viewer sees above an atom (balloon_alert_perform).
    void Balloon(object viewer, object atom, string text);
}
```

- `to_chat` and `to_chat_immediate` (`code/modules/tgchat/to_chat.dm`) are translated literally down to their last line, `SSchat.queue(target, message)` / `SSchat.send_immediate(target, message)`. SSchat itself (its per-client queues and tgui payloads) is not translated in M0: a small `SSchat` shim forwards both calls to `Chat` at once. Message order is preserved; SSchat's batching only changes when a client receives a message within the tick.
- `visible_message`, `audible_message`, `show_message`, `Hear` and the `span_*` macros are translated tg code that ends in `to_chat`.
- Text arrives as the English strings rendered by `I18n.T` (translation-rules.md 5.3) inside tg's HTML spans. The template and arguments behind each string (D75: the server sends keys and parameters) are added to the payload when client localization is designed (M9); the method signatures above do not change for that.
- `world.log << x` is not a message: `Dm.output_to(world.log, x)` goes to the host log through the hook `DmWorldState.WorldLog` (an `Action<string>` in the shim's per-world state, object-model.md section 10, which the host sets; null by default: discarded), not to `IMessageSink`. The Kernel's `World.RuntimeErrorSink` receives runtime errors only.

## 4. Sounds: `ISoundSink`

```csharp
public interface ISoundSink
{
    /// BYOND `target << sound` (tg SEND_SOUND, __DEFINES/logging.dm:191): the sound as computed for one listener.
    /// A sound whose file is null stops its channel (tg's stop_sound_channel and similar).
    void Play(object target, sound s);
}
```

- `playsound` (`code/game/sound/sound.dm:19`) and `playsound_local` (`sound.dm:101`) are translated literally: listener selection, range, falloff, pressure, `ignore_walls`, frequency, environment and the relative position written into `sound.x/y/z`. Their final `SEND_SOUND(src, sound_to_use)` is `Dm.output_to(this, sound_to_use)`, which calls `Play`.
- `sound` is the BYOND builtin data class (DmShim) with the DM vars `file`, `repeat`, `wait`, `channel`, `volume`, `frequency`, `offset`, `len`, `pan`, `pitch`, `priority`, `status`, `x`, `y`, `z`, `falloff`, `environment`, `echo`, `atom`, `params`, `transform`. `sound(file, repeat, wait, channel, volume)` (bare builtin) and `Dm.@new<sound>(...)` create it.
- tg shares one `sound` across the listeners of a `playsound` and rewrites its volume and position before each `SEND_SOUND` (a token reuses its master sound), so a sink copies the fields it needs at the call, never keeps the datum.
- The server's sink (`SoundFeed`, M4b audio slice 2; protocol.md section 22) sends the payload as given (kind, channel, file, volume, frequency, repeat, mute, offset, environment, the source tile from the relative position) and adds two presentation numbers from core data (D43), read from the world and never written to it: the muffle (1 minus tg's pressure factor without its touching floor, the vacuum low-pass of D22, DV37) and the occlusion (the station graph of LINDA's `atmos_adjacent_turfs` between the two tiles, DV39). The 3D client turns them into volume and a low-pass on top of tg's values (protocol.md 22.3: `SoundMixer` and `SoundPlayer`; tg's volume is the whole attenuation, the panning follows the first-person view, DV36).

## 5. Appearance and light: `IAppearanceSink`

```csharp
public interface IAppearanceSink
{
    /// A builtin appearance var of a registered atom was assigned, or one of its engine lists
    /// (overlays, underlays, vis_contents) was mutated. A dirty hint for replication.
    void Changed(object atom, AppearanceAspect aspect);

    /// BYOND animate(): target (atom, image or client) and the call's arguments as given
    /// (named arguments as text keys). A call without a target continues the previous animation.
    void Animate(object target, DmList arguments);

    /// M4v (protocol version 9): what Dm.animate calls. The target and arguments as Animate gets them, the
    /// subject the step applies to (the target, or the object of the last call that named one) and the
    /// values the step's named vars of the subject had before the shim applied it (a matrix copied).
    /// The default forwards to Animate, so a sink written for Animate keeps working.
    void AnimateStep(object target, object subject, DmList arguments, DmList before) => Animate(target, arguments);

    /// BYOND flick(icon_state or icon, target).
    void Flick(object target, object state);

    /// tg update_light() (lighting module not translated, D24): the light source vars of the atom changed.
    void LightChanged(object atom);

    /// opacity or invisibility changed: inputs of the client's view, LOS and light computation.
    void VisibilityChanged(object atom);
}

public enum AppearanceAspect { Icon, IconState, Dir, Layer, Plane, Alpha, Color, Pixel, Transform, Overlays, Underlays, VisContents, Maptext, AppearanceFlags, BlendMode, Filters, Render, Glide }
```

- The builtin appearance vars of `/atom` and `/atom/movable` are properties in the engine parts (object-model.md section 2); their setters store the value and, for a registered atom, call `Changed` (or `VisibilityChanged` for `opacity` and `invisibility`). tg code that computes appearance (`update_appearance`, `update_icon_state`, `update_overlays`, `update_name`, `update_desc`) is translated.
- tg's overlay helpers (`controllers/subsystem/overlays.dm`: `add_overlay`, `cut_overlay`, `cut_overlays`, `copy_overlays`, `build_appearance_list`) are translated as tg code; at the snapshot they mutate the builtin `overlays` list directly (no deferred compile), and each mutation reaches `Changed(Overlays)`.
- Light: the atom's light vars (`light_range`, `light_power`, `light_color`, `light_on`, `light_flags`, `light_system`, `light_angle`, `light_dir`, `light_height`) and the `set_light*` procs (`code/modules/lighting/lighting_atom.dm`) are translated: they are state, and other tg code reads them. `update_light()` and the rest of `modules/lighting` (light sources, lighting objects and corners, `overlay_lighting`) are not: `update_light()` becomes the call `LightChanged(this)`, and the client computes light from the replicated source state (D24).
- `image()`, `mutable_appearance()`, `icon()` and `matrix()` create builtin data objects; they produce no sink call until they are attached to something visible.

## 6. tg calls and where they meet the seam

| tg | Translated | Meets the seam at |
|---|---|---|
| `to_chat`, `to_chat_immediate` | yes | `SSchat.queue` / `send_immediate` -> `Chat` |
| `visible_message`, `audible_message`, `show_message`, `Hear`, `span_*` | yes | through `to_chat` |
| `balloon_alert`, `balloon_alert_to_viewers` | yes | `/atom/proc/balloon_alert_perform` (`modules/balloon_alert/balloon_alert.dm:55`) is replaced at proc level by `Balloon(viewer, this, text)` (it builds a client image, measures text on the client and animates it) |
| `say`, `emote`, runechat `create_chat_message` | M9 | the chat bubble is a later `IMessageSink` member |
| `playsound`, `playsound_local`, `stop_sound_channel`, `SEND_SOUND` | yes | `Dm.output_to` -> `Play` |
| `update_appearance`, `update_icon`, `update_icon_state`, `update_overlays` | yes | builtin var setters -> `Changed` |
| `add_overlay`, `cut_overlay`, `cut_overlays`, `copy_overlays` | yes | `overlays` mutation -> `Changed(Overlays)` |
| `set_light`, `set_light_range`, `set_light_power`, `set_light_color`, `set_light_on`, ... | yes | `update_light()` -> `LightChanged` |
| `animate(...)`, `flick(...)`, `Shake()` | builtins / yes | `Animate`, `Flick` |
| `do_sparks`, effect systems | yes | they create `obj/effect` atoms, which replicate as objects |
| `image()` shown through `client.images`, `client.screen`, HUDs, plane masters, `maptext` HUD text | later milestones | further `IAppearanceSink` members |
| `browse()`, `<< browse(...)`, `alert()`, `input()`, tgui | later milestones | a UI seam; `Dm.NotTranslated` or stubs in M0 (waiting-model.md section 6) |

## 7. Player-visible vars

For translation-rules.md 5.3, runtime assignments to these vars **of an atom** take `I18n.T` text: `name`, `desc`, `real_name`, `desc_controls`, `maptext`. Their type-level defaults stay plain literals (data keyed by type and var). The `name` of a datum that is not an atom never reaches a player as a name (tg uses it for log and statistics labels: `/datum/qdel_item/New(mytype)` sets `name = "[mytype]"`, `controllers/subsystem/garbage.dm:341`), so it is plain text, `name = Dm.text("[]", mytype);`. Any other var holds plain text unless its value is passed directly to one of the procs of section 6.

## 8. Not in M0

UI windows (browse, tgui, alert, input), HUD and screen objects, `client.images`, plane masters, particles, filter animations, runechat bubbles, emote visuals, sound occlusion (D43), the lighting computation (D24, client-side).

## 9. Open items

- **Joined lists.** tg's helpers that join pieces into one text (`english_list`, `type_english_list`, `__HELPERS/_lists.dm:243`, `:265`) take their separator and filler words as parameters with English defaults (`"nothing"`, `" and "`, `", "`). The translation keeps those defaults as plain literals (data, translation-rules.md 5.3), and the caller's message that embeds the joined text is wrapped in `I18n.T` as one argument. How a joined list is localized (the conjunction, the comma rule, the word for an empty list, word order) is decided with the client localization in M9, together with how templates and arguments travel to the client (section 3).
