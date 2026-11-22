// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/ghost.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the ghosts, 2026-10-16, D151). The ghost's HUD has the Re-enter Corpse button and, since M10x slice C (D281),
// the Orbit and Teleport buttons, since silicons SI6 (D326) the DNR and settings buttons, since gaps S4d (D327) the minigames button, since SI7 the spawners button; tg's other buttons (the language menu, the floor changer
// and the scanner and HUD boxes) are recorded deferred lines (docs/ai/specs/not-translated.md section 16).

// tg: code/_onclick/hud/ghost.dm:2
[DmPath("/datum/hud/ghost")]
public partial class datum_hud_ghost : datum_hud
{
    // tg: code/_onclick/hud/ghost.dm:5
    public override async DmTask initialize_screen_objects()
    {
        await base.initialize_screen_objects();
        await add_screen_object(typeof(atom_movable_screen_ghost_spawners_menu), HUD_GHOST_SPAWNERS); // silicons SI7 (D326): the ghost roles
        await add_screen_object(typeof(atom_movable_screen_ghost_orbit), HUD_GHOST_ORBIT);
        await add_screen_object(typeof(atom_movable_screen_ghost_reenter_corpse), HUD_GHOST_REENTER_CORPSE);
        await add_screen_object(typeof(atom_movable_screen_ghost_dnr), HUD_GHOST_DNR); // silicons SI6 (D326)
        await add_screen_object(typeof(atom_movable_screen_ghost_teleport), HUD_GHOST_TELEPORT);
        await add_screen_object(typeof(atom_movable_screen_ghost_settings), HUD_GHOST_SETTINGS); // silicons SI6 (D326): the ghost menu
        await add_screen_object(typeof(atom_movable_screen_ghost_minigames_menu), HUD_GHOST_MINIGAMES); // gaps S4d (D327): Mafia
        await add_screen_object(typeof(atom_movable_screen_language_menu), HUD_MOB_LANGUAGE_MENU, HUD_GROUP_STATIC, ui_style, ui_ghost_language_menu); // gaps L2
        // tg: :15  add_screen_object(/atom/movable/screen/floor_changer/vertical, HUD_MOB_FLOOR_CHANGER, HUD_GROUP_STATIC, ui_style, ui_ghost_floor_changer)
        Dm.Deferred("code/_onclick/hud/ghost.dm:15", "initialize_screen_objects: the floor changer button (multi-z, after the core version; as the human's HUD)");

        // tg: :17-19  var/list/hudboxes = valid_subtypesof(/atom/movable/screen/ghost/hudbox); for(var/i in 1 to length(hudboxes))
        //             add_screen_object(hudboxes[i], HUD_KEY_GHOST_HUDBOX(i), ui_loc = position_hudbox(i - 1))
        Dm.Deferred("code/_onclick/hud/ghost.dm:17", "initialize_screen_objects: the scanner and HUD toggle boxes (the scans and data HUDs, out of the minimal ghost scope, D151; M10+)");
    }

    // tg: code/_onclick/hud/ghost.dm:21
    public virtual object position_hudbox(object i = null)
    {
        float row = floor(Dm.num(i) / 3);
        float column = Dm.num(i) % 3;
        return Dm.text("SOUTH:[], CENTER+5:[]", 6 + row * 16, 7 + column * 15);
    }

    // tg: code/_onclick/hud/ghost.dm:26
    public override async DmTask<object> show_hud(float? version = null, mob viewmob = null)
    {
        version ??= 0;
        object dot = null;
        // don't show this HUD if observing; show the HUD of the observee
        mob_dead_observer O = mymob as mob_dead_observer;
        if (istype(O, typeof(mob_dead_observer)) && Dm.truthy(O.observetarget))
        {
            await plane_masters_update();
            return FALSE;
        }

        dot = await base.show_hud(version, viewmob);
        if (!Dm.truthy(dot))
            return dot;
        mob screenmob = (viewmob is var or__1 && Dm.truthy(or__1) ? or__1 : mymob);
        // tg: :37
        if (Dm.truthy(screenmob.client.prefs.read_preference(typeof(datum_preference_toggle_ghost_hud))))
        {
            Dm.combine(screenmob.client.screen, screen_groups[HUD_GROUP_STATIC]);
            foreach (atom_movable_screen_ghost_hudbox hud in Dm.each<atom_movable_screen_ghost_hudbox>(screen_groups[HUD_GROUP_STATIC]))
                hud.update_appearance();
        }
        else
        {
            Dm.remove(screenmob.client.screen, screen_groups[HUD_GROUP_STATIC]);
        }
        return dot;
    }

    //We should only see observed mob alerts.
    // tg: code/_onclick/hud/ghost.dm:45
    public override object reorganize_alerts(mob viewmob = null)
    {
        mob_dead_observer O = mymob as mob_dead_observer;
        if (istype(O, typeof(mob_dead_observer)) && Dm.truthy(O.observetarget))
            return null;
        return base.reorganize_alerts(viewmob);
    }
}
