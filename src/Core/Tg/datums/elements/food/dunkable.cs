// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/food/dunkable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2b, D316: the Donk pinata's tator tots). The var block is generated (D111).

// If an item has the dunkable element, it's able to be dunked into reagent containers like beakers and glasses.
// Dunking the item into a container will transfer reagents from the container to the item.
// tg: code/datums/elements/food/dunkable.dm:3
public partial class datum_element_dunkable : datum_element
{
    // tg: code/datums/elements/food/dunkable.dm:8
    [DmParams("target", "amount_per_dunk")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object amount_per_dunk = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;
        dunk_amount = amount_per_dunk;
        RegisterSignal(target, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(get_dunked));
        return dot;
    }

    // tg: code/datums/elements/food/dunkable.dm:15
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, COMSIG_ITEM_INTERACTING_WITH_ATOM);
        return dot;
    }

    // tg: code/datums/elements/food/dunkable.dm:19
    [SignalHandler]
    public virtual object get_dunked(obj_item source = null, mob user = null, atom target = null, object @params = null)
    {
        if (!Dm.truthy(target.is_dunkable())) // container should be a valid target for dunking
            return NONE;
        if (!Dm.truthy(target.is_drainable()))
        {
            to_chat(user, span_warning(I18n.T("[] is unable to be dunked in!", target)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(Waiting.NoSleep(((datum_reagents)target.reagents).trans_to(source, Dm.num(dunk_amount), transferred_by: user)))) //if reagents were transferred, show the message
        {
            to_chat(user, span_notice(I18n.T("You dunk \\the [] into \\the [].", target, target)));
            return ITEM_INTERACT_SUCCESS;
        }
        if (!Dm.truthy(((datum_reagents)target.reagents).total_volume))
            to_chat(user, span_warning(I18n.T("[] is empty!", target)));
        else
            to_chat(user, span_warning(I18n.T("[] is full!", source)));
        return ITEM_INTERACT_BLOCKING;
    }
}
