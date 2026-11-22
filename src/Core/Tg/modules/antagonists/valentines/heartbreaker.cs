// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/valentines/heartbreaker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file: the heartbreaker, the odd one out of Valentine's Day. The var block is generated (D111).

// tg: code/modules/antagonists/valentines/heartbreaker.dm:1
public partial class datum_antagonist_heartbreaker : datum_antagonist
{
    // tg: code/modules/antagonists/valentines/heartbreaker.dm:8
    public override async DmTask forge_objectives(params object[] __extra)
    {
        var normiesgetout = Dm.@new<datum_objective_martyr>();
        normiesgetout.owner = owner;
        Dm.append(ref objectives, normiesgetout);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/antagonists/valentines/heartbreaker.dm:13
    public override async DmTask on_gain()
    {
        await forge_objectives();
        await base.on_gain();
    }

    // tg: code/modules/antagonists/valentines/heartbreaker.dm:17
    public override async DmTask greet()
    {
        await base.greet();
        to_chat(owner, span_boldwarning(I18n.T("You didn't get a date! They're all having fun without you! You'll show them though...")));
        await owner.announce_objectives();
    }
}
