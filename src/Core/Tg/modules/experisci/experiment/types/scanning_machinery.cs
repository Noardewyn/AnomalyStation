// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/types/scanning_machinery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the machinery scans, which count a machine built with stock parts of a tier or
// better, or with one exact part. The var blocks are generated (D111).

///This experiment type will turn up TRUE if at least one of the stock parts in the scanned machine is of the required_tier.
///Pretend to upgrade security's techfab but in reality apply only one better matter bin!
///Note that a stock part in a machine can either be an object, or a datum.
// tg: code/modules/experisci/experiment/types/scanning_machinery.dm:4
public partial class datum_experiment_scanning_points_machinery_tiered_scan : datum_experiment_scanning_points
{
    // tg: code/modules/experisci/experiment/types/scanning_machinery.dm:11
    public override async DmTask<object> check_progress()
    {
        object dot = await base.check_progress();
        ((DmList)dot)[1] = EXPERIMENT_PROG_INT(I18n.T("Scan samples of the following machines built with parts of tier [] or better.", required_tier), points, required_points)[1];
        return dot;
    }

    // tg: code/modules/experisci/experiment/types/scanning_machinery.dm:15
    public override async DmTask<object> final_contributing_index_checks(datum_component_experiment_handler experiment_handler = null, atom target = null, object typepath = null)
    {
        object dot = await base.final_contributing_index_checks(experiment_handler, target, typepath);
        if (!Dm.truthy(dot))
            return FALSE;

        obj_machinery machine = target as obj_machinery;
        //check for the required tier in the machine's stock parts as items
        foreach (obj_item_stock_parts stock_part in Dm.each<obj_item_stock_parts>(machine.component_parts))
        {
            if (Dm.num(stock_part.rating) >= required_tier) //>= for backwards research cases when you want the discount done after you did the node
                return TRUE;
        }
        //check for the required tier in the machine's stock parts as datums
        foreach (datum_stock_part datum_stock_part in Dm.each<datum_stock_part>(machine.component_parts))
        {
            if (Dm.num(datum_stock_part.tier) >= required_tier)
                return TRUE;
        }
        await experiment_handler.announce_message(I18n.T("Scanned machine is missing high enough quality parts. Expecting tier [] parts or better.", required_tier));
        return FALSE;
    }
}

//This experiment type will turn up TRUE if there is a specific part in the scanned machine
// tg: code/modules/experisci/experiment/types/scanning_machinery.dm:33
public partial class datum_experiment_scanning_points_machinery_pinpoint_scan : datum_experiment_scanning_points
{
    // tg: code/modules/experisci/experiment/types/scanning_machinery.dm:42
    public override async DmTask<object> check_progress()
    {
        object dot = await base.check_progress();
        ((DmList)dot)[1] = EXPERIMENT_PROG_INT(I18n.T("Scan samples of the following machines upgraded with \\a [] to accumulate enough points to complete this experiment.", Dm.initial(required_stock_part, (obj_item_stock_parts p) => p.name)), points, required_points)[1];
        return dot;
    }

    // tg: code/modules/experisci/experiment/types/scanning_machinery.dm:46
    public override async DmTask<object> final_contributing_index_checks(datum_component_experiment_handler experiment_handler = null, atom target = null, object typepath = null)
    {
        object dot = await base.final_contributing_index_checks(experiment_handler, target, typepath);
        if (!Dm.truthy(dot))
            return FALSE;

        obj_machinery machine = target as obj_machinery;
        //check for the required stock part as an item in the machine
        foreach (obj stock_part in Dm.each<obj>(machine.component_parts))
        {
            if (istype(stock_part, required_stock_part))
                return TRUE;
        }
        //check for the required stock part as a datum in the machine
        foreach (datum_stock_part datum_stock_part in Dm.each<datum_stock_part>(machine.component_parts))
        {
            if (istype(datum_stock_part.physical_object_reference, required_stock_part))
                return TRUE;
        }
        // DEVIATION(DV495): tg reads required_stock_part.name off the typepath, a runtime error in DM that ends the check without the
        // announcement; the port announces the part's initial name.
        await experiment_handler.announce_message(I18n.T("Scanned machine is missing an exact quality part. Expecting tier [] part.", Dm.initial(required_stock_part, (obj_item_stock_parts p) => p.name)));
        return FALSE;
    }
}
