// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/boombox/boombox.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole but its screentips (add_context: a live-partial row, D291). The Nanomusic boombox: a music tape put
// in, played, stopped and ejected from its radial (tg's tape recorder icons), the tape's song a looping sound on the jukebox channel
// (music.dm's `/datum/looping_sound/boombox` kinds), the "boombox_on" picture while it plays, worn in hand or on the shoulder
// (Alt-Right-click; the "boombox_swag" in-hand state) and dragged up like a crate. Left (not-translated.md 216): the music notes
// floating over it (`/particles/musical_notes` in a particle holder: DM particles, which the client does not draw). The var block is
// generated (D111).

// tg: code/game/objects/items/devices/boombox/boombox.dm:1
public partial class obj_item_boombox : obj_item
{
    // tg: code/game/objects/items/devices/boombox/boombox.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_drag_pickup)));
        update_available_icons();
        register_context();
        return dot;
    }

    // tg: code/game/objects/items/devices/boombox/boombox.dm:33
    public override async DmTask handle_deconstruct(float? disassembled = null)
    {
        if (Dm.truthy(tapedeck))
            await tapedeck.forceMove(drop_location() as atom);
    }

    // tg: code/game/objects/items/devices/boombox/boombox.dm:37
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref boombox_audio);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/boombox/boombox.dm:41
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("The boombox can be worn in hand or on your shoulder. Alt-Right-click to toggle.")));
        if (Dm.truthy(tapedeck))
            Dm.append(ref dot, I18n.T("It has [] inside.", span_bold(Dm.text("[]", tapedeck))));
        else
            Dm.append(ref dot, I18n.T("It has no tapedeck inside."));
        return dot;
    }

    // tg: code/game/objects/items/devices/boombox/boombox.dm:49
    public override async DmTask<object> click_alt_secondary(mob user = null)
    {
        swag_mode = Dm.truthy(swag_mode) ? FALSE : TRUE;
        balloon_alert(user, I18n.T("wearing []", Dm.truthy(swag_mode) ? I18n.T("on shoulder") : I18n.T("in hand")));
        if (loc == user)
            playsound(user, pickup_sound, 30);
        update_appearance();
        await user.update_held_items();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/devices/boombox/boombox.dm:58
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_music_tape)
        {
            if (Dm.truthy(tapedeck))
            {
                balloon_alert(user, I18n.T("eject first!"));
                return ITEM_INTERACT_BLOCKING;
            }
            obj_item_music_tape tunes = (obj_item_music_tape)tool;
            await user.transferItemToLoc(tunes, this);
            tapedeck = tunes;
            balloon_alert(user, I18n.T("tape inserted"));
            playsound(this, Dm.resource("sound/items/taperecorder/taperecorder_close.ogg"), 50, FALSE);
            return ITEM_INTERACT_SUCCESS;
        }
        if (tool is obj_item_tape)
        {
            balloon_alert(user, I18n.T("won't fit!"));
            return ITEM_INTERACT_BLOCKING;
        }
        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/game/objects/items/devices/boombox/boombox.dm:74
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        update_available_icons();
        if (!Dm.truthy(check_menu(user as mob_living)))
            return dot;
        await display_radial_menu(user as mob_living);
        return dot;
    }

    // tg: code/game/objects/items/devices/boombox/boombox.dm:81
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        update_available_icons();
        if (!Dm.truthy(check_menu(user as mob_living)))
            return dot;
        await display_radial_menu(user as mob_living);
        return dot;
    }

    // tg: code/game/objects/items/devices/boombox/boombox.dm:95
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        inhand_icon_state = Dm.truthy(swag_mode) ? "boombox_swag" : "boombox";
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/devices/boombox/boombox.dm:99
    public override async DmTask<object> mouse_drop_dragged(atom over = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        object dot = await base.mouse_drop_dragged(over, user, src_location, over_location, @params);
        update_appearance(); //Update appearance when dragged for shoulder icon state.
        return dot;
    }

    /**
     * Handles the radial menu of the boombox and it's active effects when selected.
     */
    // tg: code/game/objects/items/devices/boombox/boombox.dm:106
    public virtual async DmTask<object> display_radial_menu(mob_living user = null)
    {
        if (!Dm.truthy(user))
            return FALSE;
        object choice = await show_radial_menu(user, this, boombox_acts, radius: 36, require_near: TRUE);
        if (!Dm.truthy(choice))
            return FALSE;
        object sw__1 = choice;
        if (Dm.eq(sw__1, "Play"))
        {
            if (!Dm.truthy(tapedeck))
            {
                balloon_alert(user, I18n.T("no tape!"));
                return null;
            }
            boombox_audio = tapedeck.song_inside as datum_looping_sound;
            // music_particles = new (src, /particles/musical_notes, PARTICLE_ATTACH_MOB): the floating notes (DM particles, not-translated.md 216)
            Dm.Deferred("code/game/objects/items/devices/boombox/boombox.dm:118", "display_radial_menu: the musical notes particles (the client draws no DM particles)");
            boombox_audio.start();
            icon_state = "boombox_on";
            update_appearance();
            active = TRUE;
        }
        else if (Dm.eq(sw__1, "Stop"))
        {
            if (!Dm.truthy(boombox_audio))
            {
                balloon_alert(user, I18n.T("nothing playing!"));
                return null;
            }
            stop_music(user);
            active = FALSE;
        }
        else if (Dm.eq(sw__1, "Eject"))
        {
            stop_music(user);
            await user.transferItemToLoc(tapedeck, drop_location());
            tapedeck = null;
            update_available_icons();
            active = FALSE;
        }

        playsound(this, Dm.resource("sound/machines/click.ogg"), 40, TRUE);
        return null;
    }

    /// Updates the list of boombox_acts for the radial menu.
    // tg: code/game/objects/items/devices/boombox/boombox.dm:141
    public virtual void update_available_icons()
    {
        boombox_acts = Dm.list();
        if (!Dm.truthy(active))
            Dm.append(ref boombox_acts, Dm.list(Dm.kv("Play", image(icon: radial_icon_file, icon_state: "play"))));
        else
            Dm.append(ref boombox_acts, Dm.list(Dm.kv("Stop", image(icon: radial_icon_file, icon_state: "stop"))));
        if (Dm.truthy(tapedeck))
            Dm.append(ref boombox_acts, Dm.list(Dm.kv("Eject", image(icon: radial_icon_file, icon_state: "eject"))));
    }

    /// Do we meet the requirements to interact with the boombox?
    // tg: code/game/objects/items/devices/boombox/boombox.dm:151
    public virtual object check_menu(mob_living user = null)
    {
        if (user is not mob_living)
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        return TRUE;
    }

    /// Stops looping audio, handles particles
    // tg: code/game/objects/items/devices/boombox/boombox.dm:159
    public virtual void stop_music(mob user = null)
    {
        if (!Dm.truthy(boombox_audio))
            return;
        boombox_audio.stop();
        boombox_audio = null;

        if (Dm.truthy(music_particles))
            QDEL_NULL(ref music_particles);
        icon_state = "boombox";
        update_appearance();
    }
}
