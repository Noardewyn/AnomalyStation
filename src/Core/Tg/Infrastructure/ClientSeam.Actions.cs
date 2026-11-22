using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Owners (docs/ai/specs/m2-ownership.md): the M2 contracts step's and complete (the three keybinding entry points); the
// keybinding datums' procs they call are M2 P1's; the host's ActionKind commands call them (M2 P6). The click entry point
// is ClientSeam.Click.cs (M2 P4). Host code, not tg code: it stands for BYOND's engine side of a key press.

public static partial class ClientSeam
{
    /// <summary>
    /// The player pressed "rest" (U in tg's defaults): tg's <c>/datum/keybinding/living/rest</c>, through the keybinding
    /// path of tg's <c>keyDown</c> (<c>modules/keybindings/bindings_client.dm:70</c>: <c>kb.can_use(src)</c>, then
    /// <c>kb.down(src, ...)</c>), with <c>usr</c> the client's mob. Its <c>down</c> calls <c>toggle_resting</c>.
    /// </summary>
    public static void Rest(client player) => PressKeybinding(player, typeof(datum_keybinding_living_rest));

    /// <summary>
    /// The player toggled walk and run: tg's <c>/datum/keybinding/living/toggle_move_intent_alternative</c> ("press to cycle
    /// move intent, does not cycle back"): a client action is one press, not a held key, so the hold-to-toggle binding
    /// <c>living/toggle_move_intent</c> (which toggles back on release) is not the one. Its <c>down</c> calls
    /// <c>toggle_move_intent</c>.
    /// </summary>
    public static void ToggleMoveIntent(client player) => PressKeybinding(player, typeof(datum_keybinding_living_toggle_move_intent_alternative));

    /// <summary>The player toggled combat mode (F): tg's <c>/datum/keybinding/living/toggle_combat_mode</c>, whose <c>down</c> calls <c>set_combat_mode</c>.</summary>
    public static void ToggleCombatMode(client player) => PressKeybinding(player, typeof(datum_keybinding_living_toggle_combat_mode));

    /// <summary>
    /// tg's click <c>params</c> text for a button and the held modifier keys, as BYOND builds it for <c>Click</c> and
    /// <c>ClickOn</c>: <c>"left=1;button=left"</c>, <c>"right=1;shift=1;button=right"</c> (tg reads it with
    /// <c>params2list</c>: <c>LEFT_CLICK</c>, <c>RIGHT_CLICK</c>, <c>MIDDLE_CLICK</c>, <c>SHIFT_CLICK</c>, <c>CTRL_CLICK</c>,
    /// <c>ALT_CLICK</c>, <c>BUTTON</c>, <c>code/__DEFINES/_click.dm</c>). <paramref name="button"/> is "left", "right" or
    /// "middle".
    /// </summary>
    public static string ClickParams(string button, bool shift = false, bool ctrl = false, bool alt = false)
    {
        if (button != LEFT_CLICK && button != RIGHT_CLICK && button != MIDDLE_CLICK)
            throw new ArgumentOutOfRangeException(nameof(button), button, "A click is left, right or middle.");
        string text = button + "=1";
        if (shift) text += ";" + SHIFT_CLICK + "=1";
        if (ctrl) text += ";" + CTRL_CLICK + "=1";
        if (alt) text += ";" + ALT_CLICK + "=1";
        return text + ";" + BUTTON + "=" + button;
    }

    // ---- M4a (contracts, docs/ai/specs/interaction.md section 4; M4a P6 implements the bodies) ----

    /// <summary>
    /// M4a: the tg keybinding type of a protocol <c>KeybindingCode</c> (<c>Anomaly.Protocol.KeybindingCode</c>, the same numbers;
    /// a contract test keeps the two equal), or null for an unknown code. Frozen table (the M4a contracts step):
    /// 1 <c>/datum/keybinding/mob/swap_hands</c>, 2 <c>mob/select_hand/right</c>, 3 <c>mob/select_hand/left</c>,
    /// 4 <c>mob/activate_inhand</c>, 5 <c>mob/drop_item</c>, 6 <c>human/quick_equip</c>, 7 <c>living/toggle_throw_mode</c>,
    /// 8 <c>living/hold_throw_mode</c>; protocol version 20 (pulling): 9 <c>mob/stop_pulling</c>; version 41: 15 <c>living/give</c>.
    /// </summary>
    public static Type KeybindingType(byte code) => code switch
    {
        1 => typeof(datum_keybinding_mob_swap_hands),
        2 => typeof(datum_keybinding_mob_select_hand_right),
        3 => typeof(datum_keybinding_mob_select_hand_left),
        4 => typeof(datum_keybinding_mob_activate_inhand),
        5 => typeof(datum_keybinding_mob_drop_item),
        6 => typeof(datum_keybinding_human_quick_equip),
        7 => typeof(datum_keybinding_living_toggle_throw_mode),
        8 => typeof(datum_keybinding_living_hold_throw_mode),
        9 => typeof(datum_keybinding_mob_stop_pulling),
        10 => typeof(datum_keybinding_living_resist), // version 37 (M8z)
        15 => typeof(datum_keybinding_living_give), // version 41 (M9 slice F)
        16 => typeof(datum_keybinding_living_view_pet_data), // version 59 (gaps C2)
        _ => null,
    };

    /// <summary>
    /// M4a: the player pressed (<paramref name="down"/>) or released a tg keybinding (<see cref="KeybindingType"/> of
    /// <paramref name="code"/>): tg's <c>keyDown</c> path for one binding as <see cref="Rest"/> takes it (<c>kb.can_use(src)</c>,
    /// then <c>kb.down(src, ...)</c>), or tg's <c>keyUp</c> (<c>kb.up(src, ...)</c>, <c>bindings_client.dm:121</c>), with
    /// <c>usr</c> the client's mob, detached as a verb. An unknown code does nothing (the host refused it already).
    /// </summary>
    public static void Keybinding(client player, byte code, bool down)
    {
        Type kind = KeybindingType(code);
        if (kind != null)
            PressKeybinding(player, kind, down);
        else if (down && code is 11 or 12)
            Verb(player, code == 11 ? "mob_sleep" : "succumb");
        else if (down && code == 13)
            EmotePanel(player);
        else if (down && code == 14)
            OpenMessenger(player);
        else if (code is 17 or 18)
            LevelKey(player, code == 17, down);
        else if (code == 19)
            datum_deathmatch_scoreboard.Current?.Hold(player, down); // DV585: version 93's scoreboard key
    }

    // DEVIATION(DV422): Space and Ctrl are tg's Move Upwards and Move Downwards (version 83) for a mob that can change level; Space of any
    // other mob is tg's hold throw mode, press and release, and Ctrl does nothing (tg's "You are not Superman" is no answer to a held Ctrl).
    private static void LevelKey(client player, bool up, bool down)
    {
        if (!player.dm_connected || player.gc_destroyed != null || player.mob == null)
            return;
        if (down && ChangesLevel(player.mob))
            PressKeybinding(player, up ? typeof(datum_keybinding_movement_zlevel_upwards) : typeof(datum_keybinding_movement_zlevel_downwards));
        else if (up && down)
        {
            player.dm_level_key_threw = true;
            PressKeybinding(player, typeof(datum_keybinding_living_hold_throw_mode));
        }
        else if (up && player.dm_level_key_threw)
        {
            player.dm_level_key_threw = false;
            PressKeybinding(player, typeof(datum_keybinding_living_hold_throw_mode), down: false);
        }
    }

    /// <summary>
    /// DV422: whether <paramref name="who"/> changes level by tg's up and down rather than by stairs, ladders and lifts alone: a ghost, an eye, the
    /// AI (its eye), a mob steering an eye, an incorporeal one, one on a ladder (tg's up climbs it), a flying or floating one, or one without gravity.
    /// </summary>
    public static bool ChangesLevel(mob who)
    {
        if (who is mob_dead_observer or mob_eye or mob_living_silicon_ai || who.remote_control != null)
            return true;
        if (who is mob_living living && Dm.truthy(living.incorporeal_move))
            return true;
        if (who.loc is not turf here)
            return false;
        return Dm.locate<obj_structure_ladder>(here) != null || Dm.truthy(Dm.bitand(who.movement_type, Dm.bitor(FLYING, FLOATING))) || !Dm.truthy(who.has_gravity());
    }

    // Version 40 (M9 slice E, D251): code 14 is the big chat window's Message button: the messenger of the PDA the player carries opens
    // as tg's chat link of a PDA message opens it (the messenger's Topic with choice=open: turned on, the app run, its window), usr the
    // client's mob, detached as a verb. Without a PDA the player reads that it has none (chat.md 3).
    private static void OpenMessenger(client player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null || player.mob == null)
            return;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = player.mob;
            datum_computer_file_program_messenger messenger = null;
            foreach (obj_item_modular_computer_pda pda in Dm.each<obj_item_modular_computer_pda>(player.mob.get_all_contents()))
            {
                messenger = Dm.each<datum_computer_file_program_messenger>(pda.stored_files).FirstOrDefault();
                if (messenger != null)
                    break;
            }
            if (messenger == null)
            {
                GlobalProcs.to_chat(player.mob, GlobalProcs.span_warning(I18n.T("You have no PDA.")));
                return;
            }
            Waiting.Detach(messenger.Topic(Dm.text("choice=open"), Dm.list(Dm.kv("choice", "open"))));
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    // Version 39 (M9 slice B, D256): code 13 is the hidden verb /client/verb/emote_panel (interface/interface.dm), the emote wheel's
    // "More...", run as BYOND runs a verb (usr the client's mob, detached).
    private static void EmotePanel(client player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null)
            return;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = player.mob;
            player.emote_panel();
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    /// <summary>
    /// Version 39 (M9 slice B, D256): the emote wheel's pick, tg's emote keybinding of <paramref name="key"/> pressed
    /// (<c>/datum/keybinding/emote/down</c>: <c>user.mob.emote(emote_key, intentional = TRUE)</c>) through the keyDown path
    /// (<see cref="Keybinding"/>). tg has one emote keybinding per emote key; a key no emote has has none, so nothing happens.
    /// </summary>
    public static void Emote(client player, string key)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null)
            return;
        datum_keybinding_emote kb = Keybindings.Of(World.Current).Emote(key);
        if (kb != null)
            PressKeybinding(player, kb);
    }

    // Version 37 (M8z, D244): codes 11 and 12 are the hidden verbs Sleep and Succumb, which a tg player runs from the verb panel or the
    // command bar (M9's): the verb proc called with usr the client's mob, detached as BYOND runs a verb; a mob that is not living has
    // neither verb, as in tg.
    private static void Verb(client player, string name)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null || player.mob is not mob_living living)
            return;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = living;
            if (name == "mob_sleep")
                living.mob_sleep();
            else
                living.succumb();
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    // tg's keyDown loop body for one keybinding (bindings_client.dm:70-80, deferred there with the preferences, M9):
    // `if(kb.can_use(src) && kb.down(src, ...))`. Until GLOB.keybindings_by_name exists (init_keybindings is a deferred
    // leaf, M9), the world keeps one instance per keybinding type, made at its first use (Dm.@new, a registered datum
    // like tg's own, so the world's hash covers it).
    private static void PressKeybinding(client player, Type kind, bool down = true)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null)
            return;
        PressKeybinding(player, Keybindings.Of(World.Current).Get(kind), down);
    }

    private static void PressKeybinding(client player, datum_keybinding kb, bool down = true)
    {
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = player.mob;
            if (Dm.truthy(kb.can_use(player)))
                Waiting.Detach(down ? kb.down(player, null, null, null) : kb.up(player, null)); // BYOND: a verb call per key event, its own call
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    // The per-world keybinding instances (World.GetExtension: no static state). A datum is created inside the world, in
    // the command phase that runs the action.
    private sealed class Keybindings
    {
        private readonly Dictionary<Type, datum_keybinding> _byType = new();
        private readonly Dictionary<string, datum_keybinding_emote> _emotes = new();

        internal static Keybindings Of(World world) => world.GetExtension(static _ => new Keybindings());

        internal datum_keybinding Get(Type kind)
        {
            if (!_byType.TryGetValue(kind, out datum_keybinding kb) || kb.gc_destroyed != null)
            {
                kb = (datum_keybinding)Dm.@new(kind);
                _byType[kind] = kb;
            }
            return kb;
        }

        // tg's init_emote_keybinds: a keybinding linked to the emote type of the key (the first in GLOB.emote_list), null for none
        internal datum_keybinding_emote Emote(string key)
        {
            if (_emotes.TryGetValue(key, out datum_keybinding_emote kb) && kb.gc_destroyed == null)
                return kb;
            if ((GLOB.emote_list?[key] as DmList)?[1] is not datum_emote emote)
                return null;
            kb = Dm.@new<datum_keybinding_emote>();
            kb.link_to_emote(emote.GetType());
            _emotes[key] = kb;
            return kb;
        }
    }
}
