// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/venus_human_trap/venus_human_trap.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4b (D327): the whole file, the venus human trap's antagonist datum and its objective (spread the infestation, kept while
// the trap lives). The var blocks are generated (D111).

// tg: code/modules/antagonists/venus_human_trap/venus_human_trap.dm:1
public partial class datum_antagonist_venus_human_trap : datum_antagonist
{
    // tg: code/modules/antagonists/venus_human_trap/venus_human_trap.dm:9
    public override async DmTask on_gain()
    {
        await forge_objectives();
        await base.on_gain();
    }

    // tg: code/modules/antagonists/venus_human_trap/venus_human_trap.dm:13
    public override async DmTask greet()
    {
        await base.greet();
        await owner.announce_objectives();
    }

    // tg: code/modules/antagonists/venus_human_trap/venus_human_trap.dm:23
    public override async DmTask forge_objectives(params object[] __extra)
    {
        datum_objective_venus_human_trap objective = Dm.@new<datum_objective_venus_human_trap>();
        objective.owner = owner;
        Dm.append(ref objectives, objective);
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/antagonists/venus_human_trap/venus_human_trap.dm:17
public partial class datum_objective_venus_human_trap : datum_objective
{
    // tg: code/modules/antagonists/venus_human_trap/venus_human_trap.dm:20
    public override object check_completion()
    {
        return owner.current != null && owner.current.stat != DEAD ? TRUE : FALSE;
    }
}
