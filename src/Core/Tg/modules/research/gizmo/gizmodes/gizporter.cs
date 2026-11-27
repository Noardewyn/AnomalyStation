// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmodes/gizporter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the teleporter gizmo mode: it blinks itself, the living around it, or both, five to fifteen tiles in one
// direction with a cartoon pop. The var blocks are generated (D111).

/// Teleport... stuff...
// tg: code/modules/research/gizmo/gizmodes/gizporter.dm:13
public partial class datum_gizpulse_teleport : datum_gizpulse
{
    /// Teleport... stuff...
    // tg: code/modules/research/gizmo/gizmodes/gizporter.dm:20
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        DmList targets = get_teleport_targets(holder) as DmList;
        float range = rand(offset_min, offset_max);
        object dir = pick(GLOB.alldirs);

        foreach (object target in Dm.each(targets))
        {
            turf new_turf = get_ranged_target_turf(target as atom, dir, range) as turf;
            await do_teleport(target as atom_movable, new_turf, asoundin: Dm.resource("sound/effects/cartoon_sfx/cartoon_pop.ogg"), channel: TELEPORT_CHANNEL_BLUESPACE);
        }
    }

    // tg: code/modules/research/gizmo/gizmodes/gizporter.dm:29
    public virtual object get_teleport_targets(atom_movable holder = null)
    {
        return Dm.list();
    }
}

/// Teleport yourself
// tg: code/modules/research/gizmo/gizmodes/gizporter.dm:33
public partial class datum_gizpulse_teleport_self : datum_gizpulse_teleport
{
    // tg: code/modules/research/gizmo/gizmodes/gizporter.dm:33
    public override object get_teleport_targets(atom_movable holder = null)
    {
        return Dm.list(holder);
    }
}

/// Teleport someone else
// tg: code/modules/research/gizmo/gizmodes/gizporter.dm:37
public partial class datum_gizpulse_teleport_other : datum_gizpulse_teleport
{
    // tg: code/modules/research/gizmo/gizmodes/gizporter.dm:37
    public override object get_teleport_targets(atom_movable holder = null)
    {
        object dot = Dm.list();
        foreach (mob_living living in Dm.each<mob_living>(view(2, holder)))
            Dm.append(ref dot, living);
        return dot;
    }
}

/// Teleport yourself and someone else
// tg: code/modules/research/gizmo/gizmodes/gizporter.dm:43
public partial class datum_gizpulse_teleport_other_and_self : datum_gizpulse_teleport_other
{
    // tg: code/modules/research/gizmo/gizmodes/gizporter.dm:43
    public override object get_teleport_targets(atom_movable holder = null)
    {
        return Dm.add(base.get_teleport_targets(holder), holder);
    }
}
