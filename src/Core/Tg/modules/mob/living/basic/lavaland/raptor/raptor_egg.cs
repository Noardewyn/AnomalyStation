// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/raptor/raptor_egg.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// tg: code/modules/mob/living/basic/lavaland/raptor/raptor_egg.dm:1
public partial class obj_item_food_egg_raptor_egg : obj_item_food_egg
{
    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_egg.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_floor_placeable)));
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_egg.dm:23
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        QDEL_NULL(ref inherited_stats);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_egg.dm:28
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (growth_progress >= RAPTOR_EGG_GROWTH_PROGRESS)
            Dm.append(ref dot, span_boldnotice(I18n.T("Its noticeably shaking, ready to hatch!")));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/raptor/raptor_egg.dm:33
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!isturf(loc) || Dm.length(GLOB.raptor_population) >= MAX_RAPTOR_POP)
            return null;

        float growth_value = rand(min_growth_rate, max_growth_rate) * Dm.num(seconds_per_tick) * (1 + Dm.num(inherited_stats?.growth_modifier));
        // Slower growth off hot lavaland
        if (!Dm.truthy(SSmapping.level_trait(z, ZTRAIT_ASHSTORM)))
            growth_value *= 0.75f;
        // Faster growth in hot hot lava
        if (islava(loc))
            growth_value *= 1.5f;

        growth_progress += growth_value;
        // Don't hatch on lava though, or the chick will die instantly
        if (growth_progress < RAPTOR_EGG_GROWTH_PROGRESS || islava(loc))
            return null;

        visible_message(span_notice(I18n.T("[] hatches with a quiet cracking sound.", this)));
        Dm.@new<mob_living_basic_raptor_baby>(loc, child_color, inherited_stats);
        inherited_stats = null;
        qdel(this);
        return null;
    }
}
