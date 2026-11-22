// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/gas_leaker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1b, D316: the pressure tanks of the syndicate base). SSair processes the component as one of its machines
// (start_processing_machine takes a datum, as tg passes one).
//
// GAP(types): tg's `var/process_type` (:8) holds one of the PROCESS_* texts below; the generator types it `Type`, so it is declared
// here as the DM value it is.

/// A component to leak gas over time from damaged objects with gas storage
// tg: code/datums/components/gas_leaker.dm:6
public partial class datum_component_gas_leaker : datum_component
{
    // tg: code/datums/components/gas_leaker.dm:1  #define PROCESS_COMPONENT "component"
    private const string PROCESS_COMPONENT = "component";
    // tg: code/datums/components/gas_leaker.dm:2  #define PROCESS_MACHINE "machine"
    private const string PROCESS_MACHINE = "machine";
    // tg: code/datums/components/gas_leaker.dm:3  #define PROCESS_OBJ "obj"
    private const string PROCESS_OBJ = "obj";

    /// Keeps track of what type we were attached to so we don't need to istype every process
    // tg: code/datums/components/gas_leaker.dm:8  (hand-written: see the GAP(types) above)
    public object process_type;

    // tg: code/datums/components/gas_leaker.dm:19
    [DmParams("integrity_leak_percent", "leak_rate")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object integrity_leak_percent = Dm.arg(__extra, 0) ?? 0.9f;
        object leak_rate = Dm.arg(__extra, 1) ?? 1;
        object dot = await base.Initialize(__extra);
        if (parent is obj_machinery_atmospherics_components)
            process_type = PROCESS_COMPONENT;
        else if (ismachinery(parent))
            process_type = PROCESS_MACHINE;
        else if (isobj(parent))
            process_type = PROCESS_OBJ;
        else
            return COMPONENT_INCOMPATIBLE;

        this.integrity_leak_percent = integrity_leak_percent;
        this.leak_rate = leak_rate;
        return dot;
    }

    // tg: code/datums/components/gas_leaker.dm:33
    public override object Destroy(float? force = null)
    {
        SSair.stop_processing_machine(this);
        return base.Destroy(force);
    }

    // tg: code/datums/components/gas_leaker.dm:37
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ATOM_TAKE_DAMAGE, nameof(start_processing));
    }

    // tg: code/datums/components/gas_leaker.dm:41
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, COMSIG_ATOM_TAKE_DAMAGE);
    }

    // tg: code/datums/components/gas_leaker.dm:45
    public virtual object process_atmos(object seconds_per_tick = null)
    {
        object dot = PROCESS_KILL;
        switch (process_type as string)
        {
            case PROCESS_OBJ:
                dot = process_obj(parent as obj);
                break;
            case PROCESS_MACHINE:
                dot = process_machine(parent as obj_machinery);
                break;
            case PROCESS_COMPONENT:
                dot = process_component(parent as obj_machinery_atmospherics_components);
                break;
        }
        return dot;
    }

    // tg: code/datums/components/gas_leaker.dm:55
    [SignalHandler]
    public virtual void start_processing()
    {
        // Hello fellow atmospherics machines, I too am definitely an atmos machine like you!
        // This component needs to tick at the same rate as the atmos system
        SSair.start_processing_machine(this);
    }

    // tg: code/datums/components/gas_leaker.dm:61
    public virtual object process_obj(obj master = null, DmList airs = null)
    {
        airs ??= Dm.list();
        Dm.append(ref airs, Waiting.NoSleep(master.return_air()));
        return process_leak(master, airs);
    }

    // tg: code/datums/components/gas_leaker.dm:65
    public virtual object process_machine(obj_machinery master = null, DmList airs = null)
    {
        airs ??= Dm.list();
        if (Dm.truthy(Dm.bitand(master.machine_stat, BROKEN)))
            return PROCESS_KILL;
        return process_obj(master, airs);
    }

    // tg: code/datums/components/gas_leaker.dm:70
    public virtual object process_component(obj_machinery_atmospherics_components master = null, DmList airs = null)
    {
        airs ??= Dm.list();
        Dm.append(ref airs, master.airs);
        return process_machine(master, airs);
    }

    // tg: code/datums/components/gas_leaker.dm:74
    public virtual object process_leak(obj master = null, DmList airs = null)
    {
        float current_integrity = Dm.num(master.get_integrity());
        if (current_integrity > Dm.num(master.max_integrity) * Dm.num(integrity_leak_percent))
            return PROCESS_KILL;
        var location = get_turf(master) as turf;
        float true_rate = (1 - (current_integrity / Dm.num(master.max_integrity))) * Dm.num(leak_rate);
        foreach (datum_gas_mixture mix in Dm.each<datum_gas_mixture>(airs))
        {
            object pressure = mix.return_pressure();
            if (Dm.truthy(mix.release_gas_to(Waiting.NoSleep(location.return_air()) as datum_gas_mixture, pressure, true_rate)))
                location.air_update_turf(FALSE, FALSE);
        }
        return null;
    }
}
