// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/antagonists/ghost_role.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the domain's ghost actor, a ghost playing a domain's defender (the island brawl's islanders, the pirates, the
// syndicate's crew), with its fluff objective. The var blocks are generated.

// tg: code/modules/bitrunning/antagonists/ghost_role.dm:1
public partial class datum_antagonist_domain_ghost_actor : datum_antagonist
{
    // tg: code/modules/bitrunning/antagonists/ghost_role.dm:9
    public override async DmTask on_gain()
    {
        await base.on_gain();
        await owner.current.AddComponent(typeof(datum_component_npc_friendly)); //Just in case
        await forge_objectives();
    }

    // tg: code/modules/bitrunning/antagonists/ghost_role.dm:14
    public override async DmTask forge_objectives(params object[] __extra)
    {
        datum_objective_bitrunner_ghost_fluff objective = Dm.@new<datum_objective_bitrunner_ghost_fluff>();
        objective.owner = owner;
        Dm.append(ref objectives, objective);
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/bitrunning/antagonists/ghost_role.dm:19
public partial class datum_objective_bitrunner_ghost_fluff : datum_objective
{
    // tg: code/modules/bitrunning/antagonists/ghost_role.dm:21
    public override async DmTask New(params object[] __extra)
    {
        explanation_text = I18n.T("Defend your domain from the intruders!");
        await DmTask.CompletedTask;
    }
}
