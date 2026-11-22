// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/ruin_defender/stickman.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6: the paper wizard's minions).

// tg: code/modules/mob/living/basic/ruin_defender/stickman.dm:1
public partial class mob_living_basic_stickman : mob_living_basic
{
    // tg: code/modules/mob/living/basic/ruin_defender/stickman.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.@new<obj_effect_temp_visual_paper_scatter>(get_turf(this));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/ruin_defender/stickman.dm:56
public partial class mob_living_basic_stickman_ranged : mob_living_basic_stickman
{
    // tg: code/modules/mob/living/basic/ruin_defender/stickman.dm:69
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_item_gun_ballistic_automatic_pistol_stickman)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_attacks), Dm.named("casing_type", typeof(obj_item_ammo_casing_c9mm)), Dm.named("projectile_sound", Dm.resource("sound/misc/bang.ogg")), Dm.named("cooldown_time", 5 * SECONDS)));
        return dot;
    }
}
