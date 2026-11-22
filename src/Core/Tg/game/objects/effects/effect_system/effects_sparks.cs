// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/effect_system/effects_sparks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M5 slice 1: sparks of damage, the door's spark system and the light tube's). The var blocks are generated
// (D111). The light middleman (modules/lighting/light_middleman.dm: the spark's light intercepted through a render source
// and faded with animate) is a deferred leaf: the client's light grid draws the spark's own light (D24).

/////////////////////////////////////////////
//SPARK SYSTEM (like steam system)
// The attach(atom/atom) proc is optional, and can be called to attach the effect
// to something, like the RCD, so then you can just call start() and the sparks
// will always spawn at the items location.
/////////////////////////////////////////////

public static partial class GlobalProcs
{
    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:8
    public static void do_sparks(object number = null, object cardinal_only = null, atom source = null, atom holder = null, Type spark_type = null)
    {
        spark_type ??= typeof(datum_effect_system_basic_spark_spread);
        datum_effect_system_basic_spark_spread sparks = Dm.@new(spark_type, get_turf(source), number, cardinal_only) as datum_effect_system_basic_spark_spread;
        if (holder != null)
            sparks.attach(holder);
        sparks.autocleanup = TRUE;
        Waiting.NoSleep(sparks.start());
    }
}

// tg: code/game/objects/effects/effect_system/effects_sparks.dm:15  (a hand-written var block: delete_timer_id holds tg's text
// timer ids, num2text in timer.dm:392, so it is object by a "-" cell of rules 3.5, where its default -1 would make it a number)
[DmPath("/obj/effect/particle_effect/sparks")]
public partial class obj_effect_particle_effect_sparks : obj_effect_particle_effect
{
    /// Should this spark's effect be animated
    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:24
    public float? animated = TRUE;
    /// Timer id for the timer that will wipe us out
    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:26
    public object delete_timer_id = TIMER_ID_NULL;
    /// Middleman object we're using to animate our light
    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:28
    public datum_light_middleman middleman;

    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:15
    public obj_effect_particle_effect_sparks()
    {
        name = "sparks";
        icon_state = "sparks";
        anchored = TRUE;
        light_system = OVERLAY_LIGHT;
        light_range = 1.5f;
        light_power = 2;
        light_color = LIGHT_COLOR_FIRE;
    }

    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        if (Dm.truthy(animated))
        {
            // middleman = new(src, "sparks"); middleman.being_overriding_light(): the light middleman (render-source light
            // interception, light_middleman.dm) is not translated; the client draws the spark's light from its light vars (D24).
            Dm.Deferred("code/game/objects/effects/effect_system/effects_sparks.dm:33", "Initialize: the spark's light middleman (its light's fade; client lighting, D24) (M10+)");
        }
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:37
    public override void LateInitialize(params object[] __extra)
    {
        RegisterSignals(this, Dm.list(COMSIG_MOVABLE_CROSS, COMSIG_MOVABLE_CROSS_OVER), nameof(sparks_touched));
        flick(icon_state, this);
        playsound(this, SFX_SPARKS, 100, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        turf location = loc as turf;
        if (isturf(location))
            affect_location(location, just_initialized: TRUE);
        decay_in(2 * SECONDS);
    }

    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:46  (defined twice; the later one at :68 calls it)
    public object Destroy__d0(float? force = null)
    {
        if (middleman != null)
            QDEL_NULL(ref middleman);
        return base.Destroy(force);
    }

    /// Sets up our death effects given the passed in duration
    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:52
    public virtual void decay_in(object decay_time = null)
    {
        if (!Dm.eq(delete_timer_id, TIMER_ID_NULL))
            deltimer(delete_timer_id);
        // QDEL_IN_STOPPABLE(src, decay_time + world.tick_lag) (__DEFINES/qdel.dm:61), expanded inline (rules 11.1 (f))
        float time = Dm.num(decay_time) + world.tick_lag;
        delete_timer_id = (_addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), (time > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this)), time, TIMER_STOPPABLE, file: "code/game/objects/effects/effect_system/effects_sparks.dm", line: 55));
        if (!Dm.truthy(animated))
            return;
        // var/obj/effect/abstract/main_light = middleman.primary_intercept and its animate() fade (:58-66): the light
        // middleman is not translated (Initialize's deferred leaf); the spark's light ends with the spark.
        Dm.Deferred("code/game/objects/effects/effect_system/effects_sparks.dm:58", "decay_in: the fade of the spark's light through its middleman (client lighting, D24) (M10+)");
    }

    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:68
    public override object Destroy(float? force = null)
    {
        turf location = loc as turf;
        if (isturf(location))
            affect_location(location);
        return Destroy__d0(force);
    }

    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:74
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        object dot = await base.Move(newloc, direction, glide_size_override, update_dir);
        turf location = loc as turf;
        if (isturf(location))
            affect_location(location);
        return dot;
    }

    /*
    * Apply the effects of this spark to its location.
    *
    * When the spark is first created, Cross() and Crossed() don't get called,
    * so for the first initialization, we make sure to specifically invoke the
    * behavior of the spark on all the mobs and objects in the location.
    * turf/location - The place the spark is affectiong
    * just_initialized - If the spark is just being created, and we need to manually affect everything in the location
    */
    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:93
    public virtual void affect_location(turf location = null, float? just_initialized = null)
    {
        just_initialized ??= FALSE;
        location.hotspot_expose(1000, 100);
        if (Dm.truthy(location._listen_lookup?[COMSIG_ATOM_TOUCHED_SPARKS]))
            location._SendSignal(COMSIG_ATOM_TOUCHED_SPARKS, Dm.list(location, this)); // for plasma floors; other floor types only have to worry about the mysterious HAZARDOUS sparks
        if (Dm.truthy(just_initialized))
        {
            foreach (atom_movable singed in Dm.each_cast<atom_movable>(location))
                sparks_touched(this, singed);
        }
    }

    /*
    * This is called when anything passes through the same tiles as a spark, or when a spark passes through something's tile.
    *
    * This is invoked by the signals sent by every atom when they're crossed or crossing something. It
    * signifies that something has been touched by sparks, and should be affected by possible pyrotechnic affects..
    * datum/source - Can either be the spark itself or an object that just walked into it
    * mob/living/singed - What was touched by the spark
    */
    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:108
    [SignalHandler]
    public virtual void sparks_touched(datum source = null, atom singed = null)
    {
        if (Dm.truthy(singed._listen_lookup?[COMSIG_ATOM_TOUCHED_SPARKS]))
            singed._SendSignal(COMSIG_ATOM_TOUCHED_SPARKS, Dm.list(singed, this));
        if (isobj(singed))
        {
            datum_reagents reagents = singed.reagents as datum_reagents; // heat up things that contain reagents before we check to see if they burn
            if (reagents != null && !Dm.truthy(Dm.bitand(reagents.flags, SEALED_CONTAINER)))
                Waiting.NoSleep(reagents.expose_temperature(1000)); // we set this at 1000 because that's the max reagent temp for a chem heater, higher temps require more than sparks
            return;
        }

        if (ishuman(singed))
        {
            mob_living_carbon_human singed_human = singed as mob_living_carbon_human;
            foreach (obj_item anything in Dm.each_cast<obj_item>(singed_human.get_visible_items()))
                sparks_touched(this, anything);
        }
    }
}

// tg: code/game/objects/effects/effect_system/effects_sparks.dm:123
public partial class datum_effect_system_basic_spark_spread : datum_effect_system_basic
{
    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:127
    public override object generate_effect()
    {
        obj_effect_particle_effect_sparks spark = base.generate_effect() as obj_effect_particle_effect_sparks;
        spark.decay_in(last_loop_length);
        return null;
    }

    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:131
    public override object get_step_count()
    {
        return rand(2, 3); // never 1 cause 1 looks dumb
    }

    // tg: code/game/objects/effects/effect_system/effects_sparks.dm:134
    public override void move_failed(datum_move_loop loop = null, obj_effect failed = null)
    {
        if (QDELETED(failed))
            return;
        obj_effect_particle_effect_sparks spark = failed as obj_effect_particle_effect_sparks;
        spark.decay_in(0.1f * SECONDS);
    }
}
