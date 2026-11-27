// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/anomalies/_anomalies.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 supermatter slice, part 2 (D167): the anomaly base, the anomalies an overpowered supermatter makes, with their cores
// (the signaler a cored anomaly carries on a random free frequency and code, the analyzer's readout, the core a neutralized
// anomaly drops or the inert core past SSresearch's cap). Left out: the admins' variable editor (M10). The var block is generated
// (D111).
//
// GAP(types): tg's `var/obj/item/assembly/signaler/anomaly/anomaly_core` (:13) is generated as a Type (rules 3.2 V3: its default
// and the subtypes' overrides are typepaths, which tg replaces by the core on Initialize); the core the anomaly carries is
// anomaly_core_value (the same split as /obj/machinery/var/circuit).

//Anomalies, used for anomaly events. Anomalies cause adverse effects on their surroundings and can be mitigated by signalling their respective frequency.
// tg: code/game/objects/effects/anomalies/_anomalies.dm:2
public partial class obj_effect_anomaly : obj_effect
{
    /// GAP(types): tg's `anomaly_core` (:13) once it holds the core: the signaler that neutralizes the anomaly.
    public obj_item_assembly_signaler_anomaly anomaly_core_value;

    // tg: code/game/objects/effects/anomalies/_anomalies.dm:27
    [DmParams("mapload", "new_lifespan", "drops_core")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_lifespan = Dm.arg(__extra, 0);
        object drops_core = Dm.arg(__extra, 1);
        drops_core ??= TRUE;
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        if (!Dm.truthy(mapload))
        {
            Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this)); // tg: :31 (M10x slice C)
        }

        START_PROCESSING(SSobj, this);
        impact_area = get_area(this) as area;

        if (!Dm.truthy(impact_area))
            return INITIALIZE_HINT_QDEL;

        if (!Dm.truthy(drops_core))
            anomaly_core = null;

        if (Dm.truthy(anomaly_core))
        {
            anomaly_core_value = Dm.@new(anomaly_core, this) as obj_item_assembly_signaler_anomaly;
            anomaly_core_value.code = rand(1, 100);
            anomaly_core_value.anomaly_type = type;

            anomaly_core_value.set_frequency(sanitize_frequency(rand(MIN_FREE_FREQ, MAX_FREE_FREQ), free: TRUE));
        }

        if (Dm.truthy(new_lifespan))
            lifespan = Dm.num(new_lifespan);
        death_time = world.time + Dm.num(lifespan);
        countdown = Dm.@new<obj_effect_countdown_anomaly>(this);
        if (Dm.truthy(countdown_colour))
            countdown.color = countdown_colour;
        if (Dm.truthy(immortal))
            return dot;
        countdown.start();
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/_anomalies.dm:67  (set waitfor = FALSE, inherited from /datum/process: the wrapper of
    // waiting-model.md 5.4)
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null);
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        await anomalyEffect(seconds_per_tick);
        if (Dm.num(death_time) < world.time && !Dm.truthy(immortal))
        {
            if (Dm.truthy(loc))
                await detonate();
            qdel(this);
        }
        return null;
    }

    // tg: code/game/objects/effects/anomalies/_anomalies.dm:74
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        QDEL_NULL(ref countdown);
        QDEL_NULL(ref anomaly_core_value);
        anomaly_core = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/effects/anomalies/_anomalies.dm:80
    public virtual async DmTask<object> anomalyEffect(object seconds_per_tick = null)
    {
        // tg: #ifndef UNIT_TESTS: the production branch (rules 1.7)
        if (SPT_PROB(move_chance, seconds_per_tick))
            await move_anomaly();
        return null;
    }

    /// Move in a direction
    // tg: code/game/objects/effects/anomalies/_anomalies.dm:87
    public virtual async DmTask move_anomaly()
    {
        await step(this, Dm.num(pick(GLOB.alldirs)));
    }

    // tg: code/game/objects/effects/anomalies/_anomalies.dm:90
    public virtual async DmTask detonate()
    {
        return;
    }

    // tg: code/game/objects/effects/anomalies/_anomalies.dm:93
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        if (Dm.num(severity) >= EXPLODE_DEVASTATE)
        {
            qdel(this);
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/game/objects/effects/anomalies/_anomalies.dm:100
    public virtual async DmTask anomalyNeutralize()
    {
        Dm.@new<obj_effect_particle_effect_fluid_smoke_bad>(loc);
        // SSblackbox.record_feedback("nested tally", "anomaly_defused", ...)
        Dm.Deferred("code/game/objects/effects/anomalies/_anomalies.dm:102", "anomalyNeutralize: the blackbox tally (statistics, M10+)");

        if (anomaly_core_value != null)
        {
            Type anomaly_type = anomaly_core_value.type;
            if (SSresearch.is_core_available(anomaly_type))
            {
                SSresearch.increment_existing_anomaly_cores(anomaly_type);
                await anomaly_core_value.forceMove(drop_location() as atom);
                anomaly_core_value = null;
                anomaly_core = null;
            }
            else // You exceeded the cap sorry
            {
                visible_message(span_warning(I18n.T("[] loses its lustre as it falls to the ground, there is too little ambient energy to support another core of this type.", anomaly_core_value)));
                Dm.@new<obj_item_inert_anomaly>(drop_location());
            }
        }

        // else, anomaly core gets deleted by qdel(src).

        qdel(this);
    }

    // tg: code/game/objects/effects/anomalies/_anomalies.dm:126
    public override object analyzer_act(mob_living user = null, obj_item tool = null)
    {
        if (anomaly_core_value != null)
        {
            to_chat(user, span_notice(I18n.T("Analyzing... []'s unstable field is fluctuating along frequency [], code [].", this, format_frequency(anomaly_core_value.frequency), anomaly_core_value.code)));
            return ITEM_INTERACT_SUCCESS;
        }
        to_chat(user, span_notice(I18n.T("Analyzing... []'s unstable field is not fluctuating along a stable frequency.", this)));
        return ITEM_INTERACT_BLOCKING;
    }

    ///Stabilize an anomaly, letting it stay around forever or untill destabilizes by a player. An anomaly without a core can't be signalled, but can be destabilized
    // tg: code/game/objects/effects/anomalies/_anomalies.dm:134
    public virtual void stabilize(float? anchor = null, float? has_core = null)
    {
        anchor ??= FALSE;
        has_core ??= TRUE;
        immortal = TRUE;
        name = (Dm.truthy(has_core) ? "stable " : "hollow ") + name;
        if (!Dm.truthy(has_core))
        {
            QDEL_NULL(ref anomaly_core_value);
            anomaly_core = null;
        }
        if (Dm.truthy(anchor))
            move_chance = 0;
    }
}
