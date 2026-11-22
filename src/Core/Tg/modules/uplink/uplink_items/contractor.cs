// Translated from tgstation (snapshot 1d99bd7f): code/modules/uplink/uplink_items/contractor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the entries' procs (the entries and their categories are the generated var blocks); X1a: the contract
// reroll's spawn_item (the contracts).

// tg: code/modules/uplink/uplink_items/contractor.dm:5
public partial class datum_uplink_item_bundles_tc_contract_kit : datum_uplink_item_bundles_tc
{
    // tg: code/modules/uplink/uplink_items/contractor.dm:18
    public override async DmTask<object> purchase(mob user = null, datum_uplink_handler uplink_handler = null, atom_movable source = null)
    {
        object dot = await base.purchase(user, uplink_handler, source);
        foreach (Type uplink_items in Dm.each<Type>(subtypesof(typeof(datum_uplink_item_contractor))))
        {
            datum_uplink_item uplink_item = await Dm.new_async(uplink_items) as datum_uplink_item;
            Dm.append(uplink_handler.extra_purchasable, uplink_item);
        }
        return dot;
    }
}

// tg: code/modules/uplink/uplink_items/contractor.dm:24
public partial class datum_uplink_item_contractor : datum_uplink_item
{
    //prevents buying contractor stuff before you make an account.
    // tg: code/modules/uplink/uplink_items/contractor.dm:30
    [DmParams("uplink_handler")]
    public override object can_be_bought(datum_uplink_handler source = null)
    {
        datum_uplink_handler uplink_handler = source;
        if (uplink_handler.contractor_hub == null)
            return FALSE;
        return base.can_be_bought(uplink_handler);
    }
}

// tg: code/modules/uplink/uplink_items/contractor.dm:35
public partial class datum_uplink_item_contractor_reroll : datum_uplink_item_contractor
{
    // tg: code/modules/uplink/uplink_items/contractor.dm:43
    public override async DmTask<object> spawn_item(object spawn_path = null, mob user = null, datum_uplink_handler uplink_handler = null, atom_movable source = null)
    {
        //We're not regenerating already completed/aborted/extracting contracts, but we don't want to repeat their targets.
        DmList new_target_list = Dm.list();
        foreach (datum_syndicate_contract contract_check in Dm.each_cast<datum_syndicate_contract>(uplink_handler.contractor_hub.assigned_contracts))
        {
            if (!Dm.eq(contract_check.status, CONTRACT_STATUS_ACTIVE) && !Dm.eq(contract_check.status, CONTRACT_STATUS_INACTIVE))
            {
                if (contract_check.contract.target != null)
                    Dm.append(ref new_target_list, contract_check.contract.target);
                continue;
            }
        }

        //Reroll contracts without duplicates
        foreach (datum_syndicate_contract rerolling_contract in Dm.each_cast<datum_syndicate_contract>(uplink_handler.contractor_hub.assigned_contracts))
        {
            if (!Dm.eq(rerolling_contract.status, CONTRACT_STATUS_ACTIVE) && !Dm.eq(rerolling_contract.status, CONTRACT_STATUS_INACTIVE))
                continue;

            await rerolling_contract.generate(new_target_list);
            Dm.append(ref new_target_list, rerolling_contract.contract.target);
        }

        //Set our target list with the new set we've generated.
        uplink_handler.contractor_hub.assigned_targets = new_target_list;
        return source; //for log icon
    }
}
