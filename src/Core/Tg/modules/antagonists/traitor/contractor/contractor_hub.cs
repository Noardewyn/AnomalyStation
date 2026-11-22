// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/traitor/contractor/contractor_hub.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X1a (D316): the contractor hub, whole. The var block is generated (D111).

// tg: code/modules/antagonists/traitor/contractor/contractor_hub.dm:1
public partial class datum_contractor_hub : datum
{
    // tg: code/modules/antagonists/traitor/contractor/contractor_hub.dm:20
    public virtual async DmTask create_contracts(datum_mind owner = null)
    {
        // 6 initial contracts
        DmList to_generate = Dm.list(
            CONTRACT_PAYOUT_LARGE,
            CONTRACT_PAYOUT_MEDIUM,
            CONTRACT_PAYOUT_SMALL,
            CONTRACT_PAYOUT_SMALL,
            CONTRACT_PAYOUT_SMALL,
            CONTRACT_PAYOUT_SMALL);

        //What the fuck
        if (length(to_generate) > length(GLOB.manifest.locked))
            to_generate.Cut(1, length(GLOB.manifest.locked));

        // We don't want the sum of all the payouts to be under this amount
        float lowest_TC_threshold = 30;

        float total = 0;
        float lowest_paying_sum = 0;
        datum_syndicate_contract lowest_paying_contract = null;

        // Randomise order, so we don't have contracts always in payout order.
        to_generate = shuffle(to_generate) as DmList;

        // Support contract generation happening multiple times
        float start_index = 1;
        if (assigned_contracts.len != 0)
            start_index = assigned_contracts.len + 1;

        // Generate contracts, and find the lowest paying.
        for (int i = 1; i <= to_generate.len; i++)
        {
            var contract_to_add = await Dm.new_async(typeof(datum_syndicate_contract), owner, assigned_targets, to_generate[i]) as datum_syndicate_contract;
            float contract_payout_total = contract_to_add.contract.payout + contract_to_add.contract.payout_bonus;

            Dm.append(ref assigned_targets, contract_to_add.contract.target);

            if (lowest_paying_contract == null || contract_payout_total < lowest_paying_sum)
            {
                lowest_paying_sum = contract_payout_total;
                lowest_paying_contract = contract_to_add;
            }

            total += contract_payout_total;
            contract_to_add.id = start_index;
            Dm.append(ref assigned_contracts, contract_to_add);

            start_index++;
        }

        // If the threshold for TC payouts isn't reached, boost the lowest paying contract
        if (total < lowest_TC_threshold)
            lowest_paying_contract.contract.payout_bonus += lowest_TC_threshold - total;
    }
}
