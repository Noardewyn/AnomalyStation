// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/vermin/butterfly.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/vermin/butterfly.dm:2
public partial class mob_living_basic_butterfly : mob_living_basic
{
    // tg: code/modules/mob/living/basic/vermin/butterfly.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        object newcolor = rgb(rand(0, 255), rand(0, 255), rand(0, 255));
        Waiting.NoSleep(add_atom_colour(newcolor, FIXED_COLOUR_PRIORITY));
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_BUTTERFLY, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        return dot;
    }

    // tg: code/modules/mob/living/basic/vermin/butterfly.dm:40
    public override object bee_friendly()
    {
        return TRUE; //treaty signed at the Beeneeva convention
    }
}

// tg: code/modules/mob/living/basic/vermin/butterfly.dm:48
public partial class mob_living_basic_butterfly_lavaland_temporary : mob_living_basic_butterfly_lavaland
{
    // tg: code/modules/mob/living/basic/vermin/butterfly.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSprocessing, this);
        return dot;
    }

    // tg: code/modules/mob/living/basic/vermin/butterfly.dm:62
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSprocessing, this);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/vermin/butterfly.dm:66
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(should_despawn()))
        {
            if (Dm.truthy(will_be_destroyed))
                return null;
            will_be_destroyed = TRUE;
            despawn_timer = (_addtimer(Dm.@new<datum_callback>(this, nameof(fadeout)), 5 * SECONDS, TIMER_STOPPABLE, file: "code/modules/mob/living/basic/vermin/butterfly.dm", line: 71));
            return null;
        }
        if (Dm.truthy(will_be_destroyed))
        {
            will_be_destroyed = FALSE;
            deltimer(despawn_timer);
        }
        return null;
    }

    // tg: code/modules/mob/living/basic/vermin/butterfly.dm:77
    public virtual object should_despawn()
    {
        if (QDELETED(source))
            return TRUE;
        if (Dm.num(get_dist(source, this)) > max_distance)
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/mob/living/basic/vermin/butterfly.dm:84
    public virtual void fadeout()
    {
        animate(this, Dm.named("alpha", 0), 1 * SECONDS);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), this), 1 * SECONDS, file: "code/modules/mob/living/basic/vermin/butterfly.dm", line: 86);
    }

    // tg: code/modules/mob/living/basic/vermin/butterfly.dm:88
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice("Something about it seems unreal..."));
        return dot;
    }
}
