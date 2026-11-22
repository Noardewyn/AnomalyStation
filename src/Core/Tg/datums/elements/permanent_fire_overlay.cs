// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/permanent_fire_overlay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D304: lava's burning mobs).

/// When applied to a mob, they will always have a fire overlay regardless of if they are *actually* on fire.
// tg: code/datums/elements/permanent_fire_overlay.dm:2
public partial class datum_element_perma_fire_overlay : datum_element
{
    // tg: code/datums/elements/permanent_fire_overlay.dm:4
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(add_fire_overlay));
        ((atom)target).update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/datums/elements/permanent_fire_overlay.dm:12
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_ATOM_UPDATE_OVERLAYS);
        ((atom)source).update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/datums/elements/permanent_fire_overlay.dm:17
    [SignalHandler]
    public virtual void add_fire_overlay(mob_living source = null, DmList overlays = null)
    {
        var created_overlay = source.get_fire_overlay(stacks: MAX_FIRE_STACKS, on_fire: TRUE) as mutable_appearance;
        if (created_overlay == null)
            return;
        Dm.combine(ref overlays, created_overlay);
        Dm.combine(ref overlays, source.make_fire_emissive(created_overlay));
    }
}
