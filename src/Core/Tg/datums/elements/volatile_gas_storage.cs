// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/volatile_gas_storage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1b, D316: the pressure tanks of the syndicate base). The var block is generated (D111).

/// An element to make an /obj explode based on gas pressure when broken
// tg: code/datums/elements/volatile_gas_storage.dm:2
public partial class datum_element_volatile_gas_storage : datum_element
{
    // tg: code/datums/elements/volatile_gas_storage.dm:13
    [DmParams("target", "minimum_explosive_pressure", "max_explosive_pressure", "max_explosive_force")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object minimum_explosive_pressure = Dm.arg(__extra, 0) ?? 5000;
        object max_explosive_pressure = Dm.arg(__extra, 1) ?? 100000;
        object max_explosive_force = Dm.arg(__extra, 2) ?? 9;
        object dot = await base.Attach(target, Dm.forward(__extra, minimum_explosive_pressure, max_explosive_pressure, max_explosive_force));
        if (target is obj_machinery_atmospherics_components)
            RegisterSignal(target, COMSIG_ATOM_BREAK, nameof(AtmosComponentBreak));
        else if (isobj(target))
            RegisterSignal(target, COMSIG_ATOM_BREAK, nameof(ObjBreak));
        else
            return ELEMENT_INCOMPATIBLE;

        this.minimum_explosive_pressure = minimum_explosive_pressure;
        this.max_explosive_pressure = max_explosive_pressure;
        this.max_explosive_force = max_explosive_force;
        return dot;
    }

    // tg: code/datums/elements/volatile_gas_storage.dm:26
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_ATOM_BREAK);
        return dot;
    }

    // tg: code/datums/elements/volatile_gas_storage.dm:30
    public virtual void Break(atom origin = null, datum_gas_mixture released_gas = null)
    {
        float expelled_pressure = min(Dm.num(released_gas?.return_pressure()), Dm.num(max_explosive_pressure));

        if (expelled_pressure < Dm.num(minimum_explosive_pressure))
            return;

        float explosive_force = MathF.Ceiling((expelled_pressure / Dm.num(max_explosive_pressure)) * Dm.num(max_explosive_force));
        // This is supposed to represent only shrapnel and no fire
        // Maybe one day we'll get something a bit better
        Waiting.NoSleep(explosion(get_turf(origin) as atom, light_impact_range: explosive_force, smoke: FALSE, explosion_cause: origin));
    }

    // tg: code/datums/elements/volatile_gas_storage.dm:41
    [SignalHandler]
    public virtual void AtmosComponentBreak(obj_machinery_atmospherics_components owner = null)
    {
        foreach (datum_gas_mixture gas_contents in Dm.each<datum_gas_mixture>(owner.airs))
        {
            if (gas_contents == null)
                continue;
            Break(owner, gas_contents);
        }
    }

    // tg: code/datums/elements/volatile_gas_storage.dm:48
    [SignalHandler]
    public virtual void ObjBreak(obj owner = null)
    {
        Break(owner, Waiting.NoSleep(owner.return_air()) as datum_gas_mixture);
    }
}
