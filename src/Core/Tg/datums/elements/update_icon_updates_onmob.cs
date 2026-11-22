// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/update_icon_updates_onmob.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4c slice 2: helmets and magboots redraw on the wearer when their icon changes). The var block and the
// element flags are generated.

//update_icon() may change the onmob icons
//Very good name, I know
// tg: code/datums/elements/update_icon_updates_onmob.dm:3
public partial class datum_element_update_icon_updates_onmob : datum_element
{
    // tg: code/datums/elements/update_icon_updates_onmob.dm:11
    [DmParams("target", "flags", "body")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object flags = Dm.arg(__extra, 0);
        object body = Dm.arg(__extra, 1) ?? FALSE;
        object dot = null;
        dot = await base.Attach(target, Dm.forward(__extra, flags, body));
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_ATOM_UPDATED_ICON, nameof(update_onmob));
        update_flags = Dm.num(flags);
        update_body = Dm.num(body);
        return dot;
    }

    // tg: code/datums/elements/update_icon_updates_onmob.dm:19
    [SignalHandler]
    public virtual void update_onmob(obj_item target = null)
    {
        if (ismob(target.loc))
        {
            mob M = target.loc as mob;
            if (Dm.truthy(M.is_holding(target)))
                Waiting.NoSleep(M.update_held_items());
            else
                Waiting.NoSleep(M.update_clothing(Dm.bitor(target.slot_flags, update_flags)));
        }
    }
}
