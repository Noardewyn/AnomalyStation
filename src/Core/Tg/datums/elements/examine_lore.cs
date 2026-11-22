// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/examine_lore.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D291: the whole file (the "look closer" hint of an examine and the lore a second examine prints). The var block is generated.

/// A simple element for adding additional lore to things. Examine more? More like examine *lore*.
/// Yes, the name's a pun, I'm sorry, I thought it was funny at the moment.
// tg: code/datums/elements/examine_lore.dm:4
public partial class datum_element_examine_lore : datum_element
{
    // tg: code/datums/elements/examine_lore.dm:11
    [DmParams("target", "lore_hint", "lore")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object lore_hint = Dm.arg(__extra, 0);
        object lore = Dm.arg(__extra, 1);
        object dot = await base.Attach(target, __extra);

        this.lore_hint = Dm.truthy(lore_hint) ? lore_hint : span_notice(I18n.T("You can [] to learn a little more about [].", EXAMINE_HINT(I18n.T("look closer")), target));
        this.lore = lore;

        if (!Dm.truthy(lore))
        {
            stack_trace(Dm.text("[] initialized without lore! Double-check element addition?", type), "code/datums/elements/examine_lore.dm", 18);
            return ELEMENT_INCOMPATIBLE;
        }

        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(target, COMSIG_ATOM_EXAMINE_MORE, nameof(on_examine_more));
        return dot;
    }

    // tg: code/datums/elements/examine_lore.dm:26
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);

        UnregisterSignal(source, Dm.list(
            COMSIG_ATOM_EXAMINE,
            COMSIG_ATOM_EXAMINE_MORE
        ));
        return dot;
    }

    // tg: code/datums/elements/examine_lore.dm:35
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, lore_hint);
    }

    // tg: code/datums/elements/examine_lore.dm:39
    [SignalHandler]
    public virtual void on_examine_more(datum source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, I18n.T("<i>[]</i>", lore));
    }
}
