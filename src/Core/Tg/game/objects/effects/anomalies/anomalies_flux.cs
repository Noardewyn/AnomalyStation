// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/anomalies/anomalies_flux.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 supermatter slice, part 2 (D167): the whole file, the flux wave anomaly (the minor and the big one too). The var blocks
// are generated (D111).

// tg: code/game/objects/effects/anomalies/anomalies_flux.dm:1
public partial class obj_effect_anomaly_flux : obj_effect_anomaly
{
    // tg: code/game/objects/effects/anomalies/anomalies_flux.dm:13  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_effect_anomaly_flux__Initialize__loc_connections;

    // tg: code/game/objects/effects/anomalies/anomalies_flux.dm:10
    [DmParams("mapload", "new_lifespan", "emp_zap")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object emp_zap = Dm.arg(__extra, 1);
        emp_zap ??= FLUX_EMP;
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        this.emp_zap = Dm.num(emp_zap);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        apply_wibbly_filters(this);
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_flux.dm:19
    public override async DmTask<object> anomalyEffect(object seconds_per_tick = null)
    {
        await base.anomalyEffect(seconds_per_tick);
        canshock = TRUE;
        foreach (mob_living M in Dm.each<mob_living>(range(0, this)))
            await mobShock(M);
        return null;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_flux.dm:25
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        // . += emissive_appearance(icon, icon_state, src, alpha=src.alpha): DEVIATION(DV12), no emissive plane; its light glows
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_flux.dm:29
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable AM = null)
    {
        Waiting.NoSleep(mobShock(AM as mob_living));
    }

    // tg: code/game/objects/effects/anomalies/anomalies_flux.dm:33
    public override async DmTask<object> Bump(atom bumped_atom = null)
    {
        await mobShock(bumped_atom as mob_living);
        return null;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_flux.dm:36
    public override object Bumped(atom_movable bumped_atom = null)
    {
        Waiting.NoSleep(mobShock(bumped_atom as mob_living));
        return null;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_flux.dm:39
    public virtual async DmTask mobShock(mob_living M = null)
    {
        if (Dm.truthy(canshock) && M is mob_living)
        {
            canshock = FALSE;
            await M.electrocute_act(shockdamage, name, flags: SHOCK_NOGLOVES);
        }
    }

    // tg: code/game/objects/effects/anomalies/anomalies_flux.dm:44
    public override async DmTask detonate()
    {
        switch (emp_zap)
        {
            case FLUX_EMP:
                await empulse(this, 4, 16, emp_source: this);
                await explosion(this, heavy_impact_range: 1, light_impact_range: 4, flash_range: 6); //Trashes the room a bit, might blow a small hole in the hull.
                break;
            case FLUX_LIGHT_EMP:
                await empulse(this, 4, 6, emp_source: this);
                await explosion(this, light_impact_range: 3, flash_range: 6);
                break;
            case FLUX_NO_EMP:
                Dm.@new<obj_effect_particle_effect_sparks>(loc);
                break;
        }
    }
}

/// A flux anomaly which doesn't emp or produce a core
// tg: code/game/objects/effects/anomalies/anomalies_flux.dm:56
public partial class obj_effect_anomaly_flux_minor : obj_effect_anomaly_flux
{
    // We need to override the default arguments here to achieve the desired effect
    // tg: code/game/objects/effects/anomalies/anomalies_flux.dm:60
    [DmParams("mapload", "new_lifespan", "emp_zap")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_lifespan = Dm.arg(__extra, 0);
        object emp_zap = Dm.arg(__extra, 1);
        emp_zap ??= FLUX_NO_EMP;
        return base.Initialize(mapload, Dm.forward(__extra, new_lifespan, emp_zap));
    }
}

///Bigger, meaner, immortal flux anomaly
// tg: code/game/objects/effects/anomalies/anomalies_flux.dm:64
public partial class obj_effect_anomaly_flux_big : obj_effect_anomaly_flux
{
    // tg: code/game/objects/effects/anomalies/anomalies_flux.dm:76
    [DmParams("mapload", "new_lifespan")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        transform = Dm.mul(transform, 3);
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_flux.dm:81
    public override async DmTask<object> anomalyEffect(object seconds_per_tick = null)
    {
        object dot = null;
        dot = await base.anomalyEffect(seconds_per_tick);

        await tesla_zap(source: this, zap_range: zap_range, power: zap_power, cutoff: 1e3f, zap_flags: zap_flags);
        return dot;
    }

    // tg: code/game/objects/effects/anomalies/anomalies_flux.dm:86
    public override object Bumped(atom_movable bumped_atom = null)
    {
        object dot = null;
        dot = base.Bumped(bumped_atom);

        if (isliving(bumped_atom))
        {
            mob_living living = (mob_living)bumped_atom;
            Waiting.NoSleep(living.dust());
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/anomalies/anomalies_flux.dm:13  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_effect_anomaly_flux__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_effect_anomaly_flux.on_entered)));
}
