// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/forcefields.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps J2, D298): the forcefield base and the mime's invisible wall; the wizard's forcefield with the forcewall spell; the
// cosmic field waits for the heretics.

// tg: code/game/objects/effects/forcefields.dm:1
public partial class obj_effect_forcefield : obj_effect
{
    // tg: code/game/objects/effects/forcefields.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (initial_duration > 0)
            // QDEL_IN(src, initial_duration) (__DEFINES/qdel.dm:59), inline (rules 11.1 (f))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), initial_duration > GC_FILTER_QUEUE ? WEAKREF(this) : this), initial_duration, file: "code/game/objects/effects/forcefields.dm", line: 15);
        return dot;
    }

    // tg: code/game/objects/effects/forcefields.dm:17
    public override DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return default;
    }
}

/// The wizard's forcefield, summoned by forcewall
// tg: code/game/objects/effects/forcefields.dm:22
public partial class obj_effect_forcefield_wizard : obj_effect_forcefield
{
    // tg: code/game/objects/effects/forcefields.dm:29
    [DmParams("mapload", "caster", "flags")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob caster = Dm.arg(__extra, 0) as mob;
        object flags = Dm.arg(__extra, 1) ?? MAGIC_RESISTANCE;
        object dot = base.Initialize(mapload, __extra);
        if (caster != null)
            caster_weakref = WEAKREF(caster) as datum_weakref;
        antimagic_flags = Dm.num(flags);
        return dot;
    }

    // tg: code/game/objects/effects/forcefields.dm:35
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        if (IS_WEAKREF_OF(mover, caster_weakref))
            return TRUE;
        if (isliving(mover))
        {
            mob_living living_mover = (mob_living)mover;
            if (Dm.truthy(living_mover.can_block_magic(antimagic_flags, charge_cost: 0)))
                return TRUE;
        }

        return await base.CanAllowThrough(mover, border_dir);
    }
}

/// Mime forcefields (invisible walls)
// tg: code/game/objects/effects/forcefields.dm:60
public partial class obj_effect_forcefield_mime : obj_effect_forcefield
{
    // tg: code/game/objects/effects/forcefields.dm:65
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList loc_connections = Dm.list(Dm.kv(COMSIG_MOUSEDROPPED_ONTO, nameof(mousedrop_receive)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        return dot;
    }

    // tg: code/game/objects/effects/forcefields.dm:72
    [SignalHandler]
    public virtual object mousedrop_receive(atom source = null, atom_movable dropped = null, mob user = null, object @params = null)
    {
        // Reroute the call to ourselves so leanable component can trigger
        if (dropped == user)
        {
            Waiting.Detach(mouse_drop_receive(dropped, user, @params));
            return COMPONENT_CANCEL_MOUSEDROPPED_ONTO;
        }
        return null;
    }

    // tg: code/game/objects/effects/forcefields.dm:79
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        object dot = await base.mouse_drop_receive(dropped, user, @params);
        await LoadComponent(typeof(datum_component_leanable), dropped); // gaps L1
        return dot;
    }
}
