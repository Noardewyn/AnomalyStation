// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/hostile_machine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4d, D316: the portable turrets are its first live users). Left out: the unit-test check that the type is
// in GLOB.target_interested_atoms (#ifdef UNIT_TESTS: a stack_trace for a coder's mistake). The var block is generated (D111).

/// AIs will attack this as a potential target if they see it
// tg: code/datums/elements/hostile_machine.dm:2
public partial class datum_element_hostile_machine : datum_element
{
    // tg: code/datums/elements/hostile_machine.dm:5
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);

        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        if (ismovable(target))
            RegisterSignal(target, COMSIG_MOVABLE_Z_CHANGED, nameof(on_z_change));

        add_to_z(target as atom);
        return dot;
    }

    // tg: code/datums/elements/hostile_machine.dm:20
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_MOVABLE_Z_CHANGED);
        remove_from_z(source as atom);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/hostile_machine.dm:25
    [SignalHandler]
    public virtual void on_z_change(atom_movable source = null, turf old_turf = null, turf new_turf = null, object same_z_layer = null)
    {
        if (Dm.truthy(same_z_layer))
            return;
        remove_from_z(source, old_turf.z);
        add_to_z(source, new_turf.z);
    }

    // tg: code/datums/elements/hostile_machine.dm:33
    public virtual void add_to_z(atom target = null, object z = null)
    {
        if (z == null)
        {
            turf target_turf = get_turf(target) as turf;
            z = target_turf?.z;
        }
        if (!Dm.truthy(z))
            return;
        if (!Dm.truthy(GLOB.hostile_machines_by_z[z]))
            GLOB.hostile_machines_by_z[z] = Dm.list();
        Dm.combine(GLOB.hostile_machines_by_z[z] as DmList, target);
    }

    // tg: code/datums/elements/hostile_machine.dm:43
    public virtual void remove_from_z(atom target = null, object z = null)
    {
        if (z == null)
        {
            turf target_turf = get_turf(target) as turf;
            z = target_turf?.z;
        }
        if (!Dm.truthy(z))
            return;
        Dm.remove(GLOB.hostile_machines_by_z[z] as DmList, target);
        if (!Dm.truthy(Dm.length(GLOB.hostile_machines_by_z[z])))
            Dm.remove(GLOB.hostile_machines_by_z, z);
    }
}
