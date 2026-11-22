// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/uplink_reimburse.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the whole file (the uplink's refunds). The var block is generated.

/**
 * Uplink Reimburse element.
 * When element is applied onto items, it allows them to be reimbursed if an user pokes an item with a uplink component with them.
 *
 * Element is only compatible with items.
 */
// tg: code/datums/elements/uplink_reimburse.dm:8
public partial class datum_element_uplink_reimburse : datum_element
{
    // tg: code/datums/elements/uplink_reimburse.dm:14
    [DmParams("target", "refundable_tc")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object refundable_tc = Dm.arg(__extra, 0) ?? 1;
        object dot = await base.Attach(target, __extra);

        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        this.refundable_tc = Dm.num(refundable_tc);

        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(target, COMSIG_ITEM_ATTEMPT_TC_REIMBURSE, nameof(reimburse));
        RegisterSignal(target, COMSIG_TRAITOR_ITEM_USED(target.type), nameof(used));
        return dot;
    }

    // tg: code/datums/elements/uplink_reimburse.dm:27
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_TRAITOR_ITEM_USED(source.type), COMSIG_ITEM_ATTEMPT_TC_REIMBURSE));

        return base.Detach(source, __extra);
    }

    ///signal called on parent being examined
    // tg: code/datums/elements/uplink_reimburse.dm:34
    [SignalHandler]
    public virtual void on_examine(datum target = null, mob user = null, DmList examine_list = null)
    {
        if (!Dm.truthy(IS_TRAITOR(user)) && !Dm.truthy(IS_NUKE_OP(user)))
        {
            Dm.append(ref examine_list, span_warning(I18n.T("There's a label on the side, but it's written in indecipherable gibberish. You have no idea what it means!")));
            return;
        }

        Dm.append(ref examine_list, span_notice(I18n.T("There's a label written in codespeak on the side, saying that this item can be refunded for [] by applying it onto an uplink.", refundable_tc)));
    }

    // tg: code/datums/elements/uplink_reimburse.dm:43
    [SignalHandler]
    public virtual void reimburse(obj_item refund_item = null, mob user = null, datum_component_uplink uplink_comp = null)
    {
        if (uplink_comp == null)
            throw Dm.CRASH("No uplink component in arguments detected", "code/datums/elements/uplink_reimburse.dm", 47);

        to_chat(user, span_notice(I18n.T("You tap [] with [], and a moment after [] disappears in a puff of red smoke!", uplink_comp.parent, refund_item, refund_item)));
        do_sparks(2, source: uplink_comp.parent as atom);
        uplink_comp.uplink_handler.add_telecrystals(refundable_tc);
        if (Dm.truthy(refund_item._listen_lookup?[COMSIG_ITEM_TC_REIMBURSED]))
            refund_item._SendSignal(COMSIG_ITEM_TC_REIMBURSED, Dm.list(refund_item));
        qdel(refund_item);
    }

    /// If the item is used, it needs to no longer be refundable
    // tg: code/datums/elements/uplink_reimburse.dm:55
    [SignalHandler]
    public virtual void used(datum target = null)
    {
        Detach(target);
    }
}
