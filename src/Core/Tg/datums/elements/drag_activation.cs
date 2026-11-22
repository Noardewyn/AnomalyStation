// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/drag_activation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice E: the PDA opens when dragged onto oneself). The var block and the element flags are generated (D111).

/**
 * drag_to_activate element
 *
 * Allowing things to be activated by mouse dragging.
 * Useful for objects which have a TGUI window on interaction.
 */
// tg: code/datums/elements/drag_activation.dm:7
public partial class datum_element_drag_to_activate : datum_element
{
    // tg: code/datums/elements/drag_activation.dm:9
    [DmParams("target")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_MOUSEDROP_ONTO, nameof(activate));
        return await base.Attach(target, __extra);
    }

    // tg: code/datums/elements/drag_activation.dm:15
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_MOUSEDROP_ONTO);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/drag_activation.dm:19
    [SignalHandler]
    public virtual object activate(atom_movable source = null, atom over = null, mob user = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(user.can_perform_action(source, FORBID_TELEKINESIS_REACH))))
            return NONE;

        obj_item item_source = source as obj_item;
        if (!(over is atom_movable_screen))
        {
            Waiting.Detach(item_source.attack_self(user));
            source.add_fingerprint(user);
            return COMPONENT_CANCEL_MOUSEDROP_ONTO;
        }
        return null;
    }
}
