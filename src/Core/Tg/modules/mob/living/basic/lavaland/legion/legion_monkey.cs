// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/legion/legion_monkey.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Weak mob spawned if a legion infests a monkey or any lesser humanoid
// tg: code/modules/mob/living/basic/lavaland/legion/legion_monkey.dm:2
public partial class mob_living_basic_mining_legion_lesser : mob_living_basic_mining_legion
{
    // tg: code/modules/mob/living/basic/lavaland/legion/legion_monkey.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        Waiting.NoSleep(AddComponent(typeof(datum_component_regenerator), Dm.named("outline_colour", COLOR_SOFT_RED)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion_monkey.dm:26
    public override async DmTask assign_abilities()
    {
        return;
    }

    // tg: code/modules/mob/living/basic/lavaland/legion/legion_monkey.dm:29
    public override object get_loot_list()
    {
        return null;
    }
}

/// Icebox variant
// tg: code/modules/mob/living/basic/lavaland/legion/legion_monkey.dm:33
public partial class mob_living_basic_mining_legion_lesser_snow : mob_living_basic_mining_legion_lesser
{
    // tg: code/modules/mob/living/basic/lavaland/legion/legion_monkey.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_appearance_on_aggro), Dm.named("aggro_state", "snow_lesser_alive"))); // Surprise! I was real!
        return dot;
    }
}
