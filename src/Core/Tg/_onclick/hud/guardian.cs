// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/guardian.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file but the floor changer button (multi-z, after the core version, as the other HUDs').

// tg: code/_onclick/hud/guardian.dm:1
public partial class datum_hud_guardian : datum_hud
{
    // tg: code/_onclick/hud/guardian.dm:1
    public override async DmTask initialize_screen_objects()
    {
        await base.initialize_screen_objects();
        await add_screen_object(typeof(atom_movable_screen_pull), HUD_MOB_PULL, HUD_GROUP_STATIC, Dm.resource("icons/hud/guardian.dmi"));
        // add_screen_object(/atom/movable/screen/floor_changer, HUD_MOB_FLOOR_CHANGER, HUD_GROUP_STATIC, ui_style, ui_below_throw) (:4)
        Dm.Deferred("code/_onclick/hud/guardian.dm:4", "initialize_screen_objects: the floor changer button (multi-z, after the core version)");
        await add_screen_object(typeof(atom_movable_screen_healths_guardian), HUD_MOB_HEALTH, HUD_GROUP_INFO);
        await add_screen_object(typeof(atom_movable_screen_combattoggle_flashy), HUD_MOB_INTENTS, HUD_GROUP_STATIC, ui_style, ui_loc: ui_zonesel);
    }
}

///Dextrous subtype for only dextrous holoparasites. Can hold things hence the inventory slot.
// tg: code/_onclick/hud/guardian.dm:10
public partial class datum_hud_dextrous_guardian : datum_hud_dextrous
{
    // tg: code/_onclick/hud/guardian.dm:14
    public override async DmTask initialize_screen_objects()
    {
        await base.initialize_screen_objects();
        await add_screen_object(typeof(atom_movable_screen_healths_guardian), HUD_MOB_HEALTH, HUD_GROUP_INFO);
    }
}
