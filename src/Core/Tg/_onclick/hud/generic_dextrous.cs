// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/generic_dextrous.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file but the floor changer and area creator buttons (multi-z and the blueprints' button,
// deferred as on the other HUDs). Its HUD serves the dextrous holoparasite; the drone's subtype stays with the drones.

//Used for normal mobs that have hands.
// tg: code/_onclick/hud/generic_dextrous.dm:8
public partial class datum_hud_dextrous : datum_hud
{
    // tg: code/_onclick/hud/generic_dextrous.dm:8
    public override async DmTask initialize_screen_objects()
    {
        await base.initialize_screen_objects();
        atom_movable_screen @using;
        await add_screen_object(typeof(atom_movable_screen_drop), HUD_MOB_DROP, HUD_GROUP_STATIC, ui_style, ui_swaphand_position(mymob, 1));
        @using = await add_screen_object(typeof(atom_movable_screen_swap_hand), HUD_MOB_SWAPHAND_2, HUD_GROUP_STATIC, ui_style, ui_swaphand_position(mymob, 2)) as atom_movable_screen;
        @using.icon_state = "act_swap";

        mymob.canon_client?.clear_screen();

        await build_hand_slots();

        await add_screen_object(typeof(atom_movable_screen_pull), HUD_MOB_PULL, HUD_GROUP_STATIC, ui_style, ui_below_throw);
        if (Dm.truthy(give_generic_combat))
            await add_screen_object(typeof(atom_movable_screen_combattoggle_flashy), HUD_MOB_INTENTS, HUD_GROUP_INFO, ui_style, ui_movi);
        if (Dm.truthy(give_health_doll))
            await add_screen_object(typeof(atom_movable_screen_healthdoll_living), HUD_MOB_HEALTHDOLL, HUD_GROUP_INFO);
        // floor_changer (:24) and area_creator (:26): multi-z and the area creator button, deferred as on the other HUDs
        Dm.Deferred("code/_onclick/hud/generic_dextrous.dm:24", "initialize_screen_objects: the floor changer and area creator buttons (multi-z, M10+)");
        await add_screen_object(typeof(atom_movable_screen_zone_sel), HUD_MOB_ZONE_SELECTOR, HUD_GROUP_STATIC, ui_style);
        await add_screen_object(typeof(atom_movable_screen_memories), HUD_MOB_MEMORIES, HUD_GROUP_STATIC, ui_style);

        if (HAS_TRAIT(mymob, TRAIT_CAN_THROW_ITEMS))
            await add_screen_object(typeof(atom_movable_screen_throw_catch), HUD_MOB_THROW, HUD_GROUP_HOTKEYS, ui_style, ui_drop_throw);
    }
}
