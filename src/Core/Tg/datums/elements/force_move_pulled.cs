// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/force_move_pulled.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Pulling (2026-10-15): the whole file. The type is generated (no vars).

/// Click dragging (thing) will force move (thing). A good use-case example for this would be clicking on a tile with a blood decal.
// tg: code/datums/elements/force_move_pulled.dm:2
public partial class datum_element_force_move_pulled : datum_element
{
    // tg: code/datums/elements/force_move_pulled.dm:4
    [DmParams("target")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = null;
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;
        dot = await base.Attach(target, __extra);
        RegisterSignal(target, COMSIG_ATOM_ATTACK_HAND, nameof(on_click), @override: TRUE);
        return dot;
    }

    // tg: code/datums/elements/force_move_pulled.dm:10
    [DmParams("target")]
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Detach(target, __extra);
        UnregisterSignal(target, COMSIG_ATOM_ATTACK_HAND);
        return dot;
    }

    // tg: code/datums/elements/force_move_pulled.dm:14
    [SignalHandler]
    public virtual object on_click(atom moving_atom = null, mob user = null, DmList modifiers = null)
    {
        if (user.pulling == null)
            return NONE;

        // Move_Pulled does not sleep (a Move and the space move check)
        Waiting.NoSleep(user.Move_Pulled(moving_atom));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }
}
