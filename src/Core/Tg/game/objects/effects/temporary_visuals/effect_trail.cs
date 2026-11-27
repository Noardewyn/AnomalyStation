// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/temporary_visuals/effect_trail.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// An invisible effect which chases a target, spawning spikes every so often.
// tg: code/game/objects/effects/temporary_visuals/effect_trail.dm:2
public partial class obj_effect_temp_visual_effect_trail : obj_effect_temp_visual
{
    // tg: code/game/objects/effects/temporary_visuals/effect_trail.dm:24
    [DmParams("mapload", "target")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom target = Dm.arg<atom>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (target == null)
            return INITIALIZE_HINT_QDEL;

        Waiting.NoSleep(AddElement(typeof(datum_element_floor_loving)));
        Waiting.NoSleep(add_spawner());
        this.target = target;
        movement = GLOB.move_manager.move_towards(this, chasing: target, delay: move_speed, home: homing, timeout: duration, flags: MOVEMENT_LOOP_START_FAST) as datum_move_loop;

        RegisterSignal(target, COMSIG_QDELETING, nameof(on_target_invalid));
        if (isliving(target))
            RegisterSignal(target, COMSIG_LIVING_DEATH, nameof(on_target_invalid));
        return dot;
    }

    // tg: code/game/objects/effects/temporary_visuals/effect_trail.dm:38
    public virtual async DmTask add_spawner()
    {
        await AddComponent(typeof(datum_component_spawner), Dm.named("spawn_types", Dm.list(spawned_effect)), Dm.named("max_spawned", max_spawned), Dm.named("spawn_time", spawn_interval), Dm.named("spawner_logic", SPAWN_CONTINUOUS_BEHAVIOR));
    }

    /// Destroy ourselves if the target is no longer valid
    // tg: code/game/objects/effects/temporary_visuals/effect_trail.dm:42
    [SignalHandler]
    public virtual void on_target_invalid()
    {
        target = null;
        qdel(this);
    }

    // tg: code/game/objects/effects/temporary_visuals/effect_trail.dm:47
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref movement);
        return base.Destroy(force);
    }
}
