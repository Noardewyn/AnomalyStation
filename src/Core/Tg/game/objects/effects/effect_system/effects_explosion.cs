// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/effect_system/effects_explosion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5x slice 1 (explosions, D149): the whole file. The var blocks are generated (D111).

// tg: code/game/objects/effects/effect_system/effects_explosion.dm:1
public partial class obj_effect_particle_effect_expl_particles : obj_effect_particle_effect
{
    // tg: code/game/objects/effects/effect_system/effects_explosion.dm:8
    [SignalHandler]
    public virtual void end_particle(datum source = null)
    {
        if (!QDELETED(this))
            qdel(this);
    }
}

// tg: code/game/objects/effects/effect_system/effects_explosion.dm:13
public partial class datum_effect_system_basic_expl_particles : datum_effect_system_basic
{
    // tg: code/game/objects/effects/effect_system/effects_explosion.dm:19
    public override object get_step_count()
    {
        return pick_weighted((25, 1), (50, 2), (100, 3), (200, 4));
    }

    // tg: code/game/objects/effects/effect_system/effects_explosion.dm:22
    [SignalHandler]
    public override void loop_end(datum_move_loop source = null)
    {
        base.loop_end(source);
        obj_effect explosion_particle = source.moving as obj_effect;
        if (QDELETED(explosion_particle))
            return;
        qdel(explosion_particle);
    }
}

// tg: code/game/objects/effects/effect_system/effects_explosion.dm:29
public partial class obj_effect_explosion : obj_effect
{
    // tg: code/game/objects/effects/effect_system/effects_explosion.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 10 > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 10, file: "code/game/objects/effects/effect_system/effects_explosion.dm", line: 42); // QDEL_IN(src, 10), inline (rules 11.1 (f))
        return dot;
    }
}

// tg: code/game/objects/effects/effect_system/effects_explosion.dm:44
public partial class datum_effect_system_explosion : datum_effect_system
{
    // tg: code/game/objects/effects/effect_system/effects_explosion.dm:46
    public override async DmTask<object> start(params object[] __extra)
    {
        Dm.@new<obj_effect_explosion>(location);
        datum_effect_system_basic_expl_particles boom_particles = Dm.@new<datum_effect_system_basic_expl_particles>(location);
        await boom_particles.start();
        return null;
    }
}

// tg: code/game/objects/effects/effect_system/effects_explosion.dm:51
public partial class datum_effect_system_explosion_smoke : datum_effect_system_explosion
{
    // tg: code/game/objects/effects/effect_system/effects_explosion.dm:53
    public virtual async DmTask create_smoke()
    {
        datum_effect_system_fluid_spread_smoke smoke_system = Dm.@new<datum_effect_system_fluid_spread_smoke>(location, 2);
        await smoke_system.attach(holder as atom).start();
    }

    // tg: code/game/objects/effects/effect_system/effects_explosion.dm:57
    public override async DmTask<object> start(params object[] __extra)
    {
        await base.start(__extra);
        _addtimer(Dm.@new<datum_callback>(this, nameof(create_smoke)), 0.5f * SECONDS, file: "code/game/objects/effects/effect_system/effects_explosion.dm", line: 59);
        return null;
    }
}
