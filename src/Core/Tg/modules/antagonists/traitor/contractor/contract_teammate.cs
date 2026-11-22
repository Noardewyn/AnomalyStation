// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/traitor/contractor/contract_teammate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X1b (D316): the whole file, the contractor support unit.

/// Support unit gets it's own very basic antag datum for admin logging.
// tg: code/modules/antagonists/traitor/contractor/contract_teammate.dm:2
public partial class datum_antagonist_traitor_contractor_support : datum_antagonist_traitor
{
    // tg: code/modules/antagonists/traitor/contractor/contract_teammate.dm:10
    public override DmTask forge_traitor_objectives()
    {
        var generic_objective = Dm.@new<datum_objective>();
        generic_objective.name = "Follow Contractor's Orders";
        generic_objective.explanation_text = "Follow your orders. Assist agents in this mission area.";
        generic_objective.completed = TRUE;
        Dm.append(ref objectives, generic_objective);
        return DmTask.CompletedTask;
    }

    // tg: code/modules/antagonists/traitor/contractor/contract_teammate.dm:17
    public override void forge_ending_objective()
    {
        return;
    }
}

// tg: code/modules/antagonists/traitor/contractor/contract_teammate.dm:20
public partial class datum_outfit_contractor_partner : datum_outfit
{
    // tg: code/modules/antagonists/traitor/contractor/contract_teammate.dm:42
    public override async DmTask post_equip(mob_living_carbon_human H = null, float? visuals_only = null)
    {
        await base.post_equip(H, visuals_only);
        var cig = H.get_item_by_slot(ITEM_SLOT_MASK) as obj_item_cigarette_syndicate;
        await cig.light__proc();
    }
}
