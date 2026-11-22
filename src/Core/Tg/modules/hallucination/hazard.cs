// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/hazard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The var blocks are generated (D111).

/// Hallucinations that create a hazard somewhere nearby that actually has a danger associated.
// tg: code/modules/hallucination/hazard.dm:2
public partial class datum_hallucination_hazard : datum_hallucination
{
    // tg: code/modules/hallucination/hazard.dm:11
    public override async DmTask<object> start()
    {
        if (IS_UNCONSCIOUS(hallucinator))
            return FALSE;

        DmList possible_points = Dm.list();
        foreach (turf_open_floor floor_in_view in Dm.each<turf_open_floor>(view(hallucinator)))
            Dm.append(possible_points, floor_in_view);

        if (length(possible_points) == 0)
            return FALSE;

        Dm.@new(hazard_type, pick(possible_points), hallucinator, this);
        QDEL_IN_HALLUCINATION(this, rand(20 * SECONDS, 45 * SECONDS), "code/modules/hallucination/hazard.dm", 22);
        return TRUE;
    }
}

/// These hallucination effects cause side effects when the hallucinator walks into them.
// tg: code/modules/hallucination/hazard.dm:36
public partial class obj_effect_client_image_holder_hallucination_danger : obj_effect_client_image_holder_hallucination
{
    // tg: code/modules/hallucination/hazard.dm:40
    [DmParams("mapload", "mobs_which_see_us", "parent")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        DmList loc_connections = Dm.list(
            Dm.kv(COMSIG_ATOM_ENTERED, nameof(atom_touched_holder)),
            Dm.kv(COMSIG_ATOM_EXITED, nameof(atom_touched_holder)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        return dot;
    }

    // tg: code/modules/hallucination/hazard.dm:48
    [SignalHandler]
    public virtual void atom_touched_holder(datum source = null, atom_movable entered = null)
    {
        if (!Dm.@in(entered, who_sees_us))
            return;

        Waiting.Detach(on_hallucinator_entered(entered as mob_living));
    }

    /// Applies effects whenever the hallucinator enters the turf with our hallucination present.
    // tg: code/modules/hallucination/hazard.dm:55
    public virtual async DmTask on_hallucinator_entered(mob_living afflicted = null)
    {
        return;
    }
}

// tg: code/modules/hallucination/hazard.dm:58
public partial class obj_effect_client_image_holder_hallucination_danger_lava : obj_effect_client_image_holder_hallucination_danger
{
    // tg: code/modules/hallucination/hazard.dm:62
    public override object generate_image()
    {
        turf danger_turf = get_turf(this);
        image_state = Dm.text("lava-[]", Dm.truthy(danger_turf.smoothing_junction) ? danger_turf.smoothing_junction : 0);
        return base.generate_image();
    }

    // tg: code/modules/hallucination/hazard.dm:67
    public override async DmTask on_hallucinator_entered(mob_living afflicted = null)
    {
        await afflicted.adjust_stamina_loss(20);
        await afflicted.cause_hallucination(typeof(datum_hallucination_fire), "fake lava hallucination");
    }
}

// tg: code/modules/hallucination/hazard.dm:71
public partial class obj_effect_client_image_holder_hallucination_danger_chasm : obj_effect_client_image_holder_hallucination_danger
{
    // tg: code/modules/hallucination/hazard.dm:75
    public override object generate_image()
    {
        turf danger_turf = get_turf(this);
        image_state = Dm.text("chasms-[]", Dm.truthy(danger_turf.smoothing_junction) ? danger_turf.smoothing_junction : 0);
        return base.generate_image();
    }

    // tg: code/modules/hallucination/hazard.dm:80
    public override async DmTask on_hallucinator_entered(mob_living afflicted = null)
    {
        to_chat(afflicted, span_userdanger("You fall into the chasm!"));
        afflicted.visible_message(span_warning(Dm.text("[] falls to the ground suddenly!", afflicted)), ignored_mobs: afflicted);
        await afflicted.Paralyze(4 * SECONDS);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.to_chat), afflicted, span_notice("...It's surprisingly shallow.")), 1.5f * SECONDS, file: "code/modules/hallucination/hazard.dm", line: 84);
        datum_hallucination.QDEL_IN_HALLUCINATION(this, 3 * SECONDS, "code/modules/hallucination/hazard.dm", 85);
    }
}

// tg: code/modules/hallucination/hazard.dm:87
public partial class obj_effect_client_image_holder_hallucination_danger_anomaly : obj_effect_client_image_holder_hallucination_danger
{
    // tg: code/modules/hallucination/hazard.dm:93
    public override object generate_image()
    {
        image dot = base.generate_image() as image;
        apply_wibbly_filters(dot);
        return dot;
    }

    // tg: code/modules/hallucination/hazard.dm:97
    [DmParams("mapload", "mobs_which_see_us", "parent")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/hallucination/hazard.dm:101
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/hazard.dm:105
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (SPT_PROB(ANOMALY_MOVECHANCE, seconds_per_tick))
            Waiting.NoSleep(step(this, Dm.num(pick(GLOB.alldirs))));
        return null;
    }

    // tg: code/modules/hallucination/hazard.dm:109
    public override async DmTask on_hallucinator_entered(mob_living afflicted = null)
    {
        await afflicted.cause_hallucination(typeof(datum_hallucination_shock), "fake anomaly hallucination");
    }
}
