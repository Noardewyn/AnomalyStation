// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/robot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326): the cyborg's HUD. As the human's HUD, the language and navigate buttons (their screen types
// are not translated) and the floor changer (multi-z) are recorded deferred lines. The var block is generated (D111).

// tg: code/_onclick/hud/robot.dm:1
public partial class datum_hud_robot : datum_hud
{
    // tg: code/_onclick/hud/robot.dm:4
    public override async DmTask initialize_screen_objects()
    {
        await base.initialize_screen_objects();
        // i, Robit
        mob_living_silicon_robot robit = mymob as mob_living_silicon_robot;
        await add_screen_object(typeof(atom_movable_screen_language_menu), HUD_MOB_LANGUAGE_MENU, HUD_GROUP_STATIC, ui_style, ui_borg_language_menu); // gaps L2
        // :9 the navigate button: its screen type is not translated, as the human's HUD (human.dm:9)
        Dm.Deferred("code/_onclick/hud/robot.dm:9", "initialize_screen_objects: the navigate button (as the human's HUD, M10+)");
        await add_screen_object(typeof(atom_movable_screen_memories), HUD_MOB_MEMORIES, HUD_GROUP_STATIC, ui_style, ui_borg_memories_menu);
        await add_screen_object(typeof(atom_movable_screen_robot_radio), HUD_CYBORG_RADIO);

        // Cyborg modules are their "hands", and use hand keys for ease of handling as they behave functionally the same
        // Generic code can just typecheck for /atom/movable/screen/inventory/hud if it needs explicitly hands
        for (int i = BORG_CHOOSE_MODULE_ONE; i <= BORG_CHOOSE_MODULE_THREE; i++)
        {
            atom_movable_screen_robot_module_slot module_slot = await add_screen_object(typeof(atom_movable_screen_robot_module_slot), HUD_KEY_HAND_SLOT(i)) as atom_movable_screen_robot_module_slot;
            module_slot.set_slot(i);
        }

        await add_screen_object(typeof(atom_movable_screen_robot_lamp), HUD_CYBORG_LAMP);
        await add_screen_object(typeof(atom_movable_screen_ai_image_take), HUD_SILICON_TAKE_IMAGE, ui_loc: ui_borg_camera);

        atom_movable_screen_robot_modpc tablet = await add_screen_object(typeof(atom_movable_screen_robot_modpc), HUD_SILICON_TABLET, HUD_GROUP_STATIC) as atom_movable_screen_robot_modpc;
        if (robit.modularInterface != null)
        {
            // Just trust me
            robit.modularInterface.vis_flags = Dm.bitor(robit.modularInterface.vis_flags, VIS_INHERIT_PLANE);
            Dm.append(tablet.vis_contents, robit.modularInterface);
        }

        await add_screen_object(typeof(atom_movable_screen_robot_alerts), HUD_SILICON_ALERTS);
        await add_screen_object(typeof(atom_movable_screen_combattoggle_robot), HUD_MOB_INTENTS, HUD_GROUP_INFO);
        // :27 the floor changer: multi-z (after the core version), as the human's HUD (human.dm:12)
        Dm.Deferred("code/_onclick/hud/robot.dm:27", "initialize_screen_objects: the floor changer button (multi-z, after the core version)");
        await add_screen_object(typeof(atom_movable_screen_zone_sel_robot), HUD_MOB_ZONE_SELECTOR);
        atom_movable_screen_robot_module module = await add_screen_object(typeof(atom_movable_screen_robot_module), HUD_CYBORG_HANDS, ui_loc: ui_borg_module) as atom_movable_screen_robot_module;
        module.icon_state = robit.model != null ? robit.model.model_select_icon : "nomod";

        await add_screen_object(typeof(atom_movable_screen_healths_robot), HUD_MOB_HEALTH, HUD_GROUP_INFO);

        await add_screen_object(typeof(atom_movable_screen_pull), HUD_MOB_PULL, HUD_GROUP_HOTKEYS, ui_style, ui_borg_pull);
    }
}
