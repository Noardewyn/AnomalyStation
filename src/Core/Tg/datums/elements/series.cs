// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/series.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the collectable hats' "Super duper collectable hats" series on examine). The var block is
// generated (D111).

/**
 * ## series element!
 *
 * bespoke element that assigns a series number to toys on examine, and shows their series name!
 * used for mechas and rare collectable hats, should totally be used for way more ;)
 */
// tg: code/datums/elements/series.dm:7
public partial class datum_element_series : datum_element
{
    // tg: code/datums/elements/series.dm:13
    [DmParams("target", "subtype", "series_name")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object subtype = Dm.arg(__extra, 0);
        object series_name = Dm.arg(__extra, 1);
        object dot = await base.Attach(target, __extra);
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;
        if (!Dm.truthy(subtype))
        {
            stack_trace("series element without subtype given!", "code/datums/elements/series.dm", 18);
            return ELEMENT_INCOMPATIBLE;
        }
        subtype_list = subtypesof(subtype);
        this.series_name = series_name;
        atom attached = (atom)target;
        RegisterSignal(attached, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        return dot;
    }

    // tg: code/datums/elements/series.dm:25
    [DmParams("target")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_ATOM_EXAMINE);
        return dot;
    }

    ///signal called examining
    // tg: code/datums/elements/series.dm:30
    [SignalHandler]
    public virtual void on_examine(datum target = null, mob user = null, DmList examine_list = null)
    {
        object series_number = subtype_list.Find(target.type);
        Dm.append(ref examine_list, span_boldnotice(I18n.T("[] is part of the \"[]\" series!", target, series_name)));
        Dm.append(ref examine_list, span_notice(I18n.T("Collect them all: []/[].", series_number, length(subtype_list))));
    }
}
