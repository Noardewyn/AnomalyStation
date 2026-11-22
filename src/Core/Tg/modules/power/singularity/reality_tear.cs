// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/singularity/reality_tear.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Tear in the Fabric of Reality ///
// Typically spawned by placing two bags of holding into one another, collapsing into a wandering singularity after a brief period as a stationary singularity.

// tg: code/modules/power/singularity/reality_tear.dm:5
public partial class obj_reality_tear : obj
{
    // tg: code/modules/power/singularity/reality_tear.dm:28
    public virtual async DmTask start_disaster()
    {
        apply_wibbly_filters(this);
        playsound(loc as atom, "sound/effects/clockcult_gateway_disrupted.ogg", vary: 200, extrarange: 3, falloff_exponent: 1, frequency: 0.33f, pressure_affected: FALSE, ignore_walls: TRUE, falloff_distance: 7);
        await AddComponent(
            typeof(datum_component_singularity),
            Dm.named("consume_range", singularity_consume_range),
            Dm.named("grav_pull", singularity_grav_pull),
            Dm.named("roaming", FALSE),
            Dm.named("singularity_size", STAGE_SIX));
        _addtimer(Dm.@new<datum_callback>(this, nameof(reality_collapse)), collapse_spawn_time, TIMER_DELETE_ME, file: "code/modules/power/singularity/reality_tear.dm", line: 38);
        Dm.animate(this, Dm.named("time", 7.5f * SECONDS), Dm.named("transform", ((matrix)transform).Scale(2)), Dm.named("flags", ANIMATION_PARALLEL));
        Dm.animate(Dm.named("time", 2 * SECONDS), Dm.named("transform", ((matrix)transform).Scale(0.25f)), Dm.named("easing", ELASTIC_EASING));
        Dm.animate(Dm.named("time", 0.5f * SECONDS), Dm.named("alpha", 0));
    }

    // tg: code/modules/power/singularity/reality_tear.dm:43
    public virtual async DmTask reality_collapse()
    {
        playsound(loc as atom, "sound/effects/supermatter.ogg", 200, vary: TRUE, extrarange: 3, falloff_exponent: 1, frequency: 0.5f, pressure_affected: FALSE, ignore_walls: TRUE, falloff_distance: 7);
        obj_singularity bagulo = Dm.@new<obj_singularity>(loc);
        await bagulo.expand(STAGE_TWO);
        bagulo.energy = 400;
        qdel(this);
    }

    // tg: code/modules/power/singularity/reality_tear.dm:50
    public override async DmTask<object> attack_tk(mob user = null)
    {
        if (!isliving(user))
            return null;
        mob_living jedi = user as mob_living;
        to_chat(jedi, span_userdanger(I18n.T("You don't feel like you are real anymore.")));
        await jedi.dust(just_ash: TRUE);
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }
}

//The temporary tears in reality. Collapses into nothing, and has a significantly lower gravity pull range, but consumes more widely.

// tg: code/modules/power/singularity/reality_tear.dm:61
public partial class obj_reality_tear_temporary : obj_reality_tear
{
    // tg: code/modules/power/singularity/reality_tear.dm:67
    public override async DmTask reality_collapse()
    {
        qdel(this);
    }
}
