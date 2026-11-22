// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/exports/stamped_paperwork.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the whole file: stamped paperwork pays, unstamped costs; stamped photocopies pay until one is caught as counterfeit. The var blocks are generated.

// tg: code/modules/cargo/exports/stamped_paperwork.dm:1
public partial class datum_export_paperwork : datum_export
{
    // tg: code/modules/cargo/exports/stamped_paperwork.dm:8
    public override object get_base_cost(obj exported_item = null)
    {
        var sold_paperwork = (obj_item_paperwork)exported_item;
        return Dm.truthy(sold_paperwork.stamped) ? base.get_base_cost(exported_item) : -cost;
    }
}

// tg: code/modules/cargo/exports/stamped_paperwork.dm:11
public partial class datum_export_photocopy : datum_export
{
    // tg: code/modules/cargo/exports/stamped_paperwork.dm:21
    public override object get_base_cost(obj exported_item = null)
    {
        var sold_paperwork = (obj_item_paperwork_photocopy)exported_item;
        if (Dm.truthy(sold_paperwork.stamped) && !Dm.truthy(backfired)) //Upon backfiring, no more photocopies are processed or sold until the next cargo shipment
        {
            if (Dm.truthy(sold_paperwork.voided))
                return 0; //Voided photocopies do nothing
            backfire_chance += rand(15, 25);
            if (prob(backfire_chance))
            {
                backfire_chance = 0;
                backfired = TRUE;
                return -cost * 4; //too high of an amount to allow for infinite money
            }
            return base.get_base_cost(exported_item);
        }
        return -cost;
    }

    // tg: code/modules/cargo/exports/stamped_paperwork.dm:37
    public override object total_printout(datum_export_report ex = null, object notes = null)
    {
        object dot = base.total_printout(ex, notes);
        if (Dm.truthy(backfired))
        {
            backfired = FALSE;
            dot = Dm.add(dot, I18n.T(" Counterfeit paperwork was detected in this shipment. A fine has been taken from your budget as a result."));
        }
        return dot;
    }
}
