// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/living.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M2 P3): the HUD of non-carbon living mobs (basic mobs); their buttons of later milestones are deferred.

// tg: code/_onclick/hud/living.dm:1
[DmPath("/datum/hud/living")]
public partial class datum_hud_living : datum_hud
{
    // tg: code/_onclick/hud/living.dm:1
    public override async DmTask initialize_screen_objects()
    {
        await base.initialize_screen_objects();
        await add_screen_object(typeof(atom_movable_screen_pull), HUD_MOB_PULL, HUD_GROUP_STATIC, ui_style, ui_living_pull);
        // combattoggle/flashy (:4): the combat toggle button of basic mobs (M10, basic mobs)
        Dm.Deferred("code/_onclick/hud/living.dm:4", "initialize_screen_objects: the combat toggle button (basic mobs, M10+)");
        await add_screen_object(typeof(atom_movable_screen_healthdoll_living), HUD_MOB_HEALTHDOLL, HUD_GROUP_INFO);
        // stamina, floor_changer (:6-7): not declared (M3-M9)
        Dm.Deferred("code/_onclick/hud/living.dm:6", "initialize_screen_objects: the stamina and floor changer buttons (M10+)");
        await add_screen_object(typeof(atom_movable_screen_language_menu), HUD_MOB_LANGUAGE_MENU, HUD_GROUP_STATIC, ui_style, ui_basic_language_menu); // gaps L2
        await add_screen_object(typeof(atom_movable_screen_memories), HUD_MOB_MEMORIES, HUD_GROUP_STATIC, ui_style, ui_basic_memories_menu);
    }
}
