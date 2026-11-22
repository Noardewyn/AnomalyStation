// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nukeop/datums/operative_reinforcement.dm and operative_lone.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole files (syndicate N1b, D316): the reinforcement (its var block only) and the lone operative's team. The var blocks are
// generated (D111).

// tg: code/modules/antagonists/nukeop/datums/operative_reinforcement.dm:1
public partial class datum_antagonist_nukeop_reinforcement : datum_antagonist_nukeop
{
}

// tg: code/modules/antagonists/nukeop/datums/operative_lone.dm:1
public partial class datum_antagonist_nukeop_lone : datum_antagonist_nukeop
{
    // tg: code/modules/antagonists/nukeop/datums/operative_lone.dm:10
    public override async DmTask create_team(datum_team new_team = null)
    {
        if (new_team != null)
        {
            await base.create_team(new_team);
            return;
        }
        // Lone ops always get a solo team solely because a lot of nukie code is on the team
        nuke_team = await Dm.new_async(typeof(datum_team_nuclear_loneop)) as datum_team_nuclear;
    }
}
