// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/alien.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the adult alien's HUD (screen_alien.dmi: the hands, the plasma display, the queen sense, the
// hunter's leap toggle). The floor changer and the navigate button stay out as on the human's HUD (their screen types are inert).
// The var block is generated (D111).

// tg: code/_onclick/hud/alien.dm:1
public partial class datum_hud_alien : datum_hud
{
    // tg: code/_onclick/hud/alien.dm:4
    public override async DmTask initialize_screen_objects()
    {
        await base.initialize_screen_objects();
        await build_hand_slots();

        atom_movable_screen @using = null;
        @using = await add_screen_object(typeof(atom_movable_screen_swap_hand), HUD_MOB_SWAPHAND_1, HUD_GROUP_STATIC, ui_style, ui_swaphand_position(mymob, 1)) as atom_movable_screen;
        @using.icon_state = "swap_1";
        @using = await add_screen_object(typeof(atom_movable_screen_swap_hand), HUD_MOB_SWAPHAND_2, HUD_GROUP_STATIC, ui_style, ui_swaphand_position(mymob, 2)) as atom_movable_screen;
        @using.icon_state = "swap_2";

        await add_screen_object(typeof(atom_movable_screen_combattoggle_flashy), HUD_MOB_INTENTS, HUD_GROUP_INFO, ui_style);
        Dm.Deferred("code/_onclick/hud/alien.dm:15", "initialize_screen_objects: the floor changer button (multi-z, its screen type inert, as the human's HUD)");
        await add_screen_object(typeof(atom_movable_screen_language_menu), HUD_MOB_LANGUAGE_MENU, HUD_GROUP_STATIC, ui_style, ui_alien_language_menu);
        Dm.Deferred("code/_onclick/hud/alien.dm:17", "initialize_screen_objects: the navigate button (its screen type inert, as the human's HUD, M10+)");
        await add_screen_object(typeof(atom_movable_screen_zone_sel_alien), HUD_MOB_ZONE_SELECTOR);
        await add_screen_object(typeof(atom_movable_screen_drop), HUD_MOB_DROP, HUD_GROUP_STATIC, ui_style);
        await add_screen_object(typeof(atom_movable_screen_pull), HUD_MOB_PULL, HUD_GROUP_STATIC, ui_style, ui_above_movement);
        await add_screen_object(typeof(atom_movable_screen_resist), HUD_MOB_RESIST, HUD_GROUP_HOTKEYS, ui_style);
        await add_screen_object(typeof(atom_movable_screen_throw_catch), HUD_MOB_THROW, HUD_GROUP_HOTKEYS, ui_style);
        await add_screen_object(typeof(atom_movable_screen_rest), HUD_MOB_REST, HUD_GROUP_HOTKEYS, ui_style);
        await add_screen_object(typeof(atom_movable_screen_sleep), HUD_MOB_SLEEP, HUD_GROUP_HOTKEYS, ui_style);
        await add_screen_object(typeof(atom_movable_screen_healths_alien), HUD_MOB_HEALTH, HUD_GROUP_INFO);
        await add_screen_object(typeof(atom_movable_screen_alien_plasma_display), HUD_ALIEN_PLASMA_DISPLAY, HUD_GROUP_INFO);
        await add_screen_object(typeof(atom_movable_screen_memories), HUD_MOB_MEMORIES, HUD_GROUP_STATIC, ui_style, ui_alien_memories_menu);

        if (isalienhunter(mymob))
            await add_screen_object(typeof(atom_movable_screen_alien_leap), HUD_ALIEN_HUNTER_LEAP, HUD_GROUP_STATIC, ui_style);

        if (!isalienqueen(mymob))
            await add_screen_object(typeof(atom_movable_screen_alien_alien_queen_finder), HUD_ALIEN_QUEEN_FINDER, HUD_GROUP_INFO);
    }
}
