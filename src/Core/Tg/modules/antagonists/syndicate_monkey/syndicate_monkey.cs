// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/syndicate_monkey/syndicate_monkey.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4c, D316). The var blocks are generated (D111).

// tg: code/modules/antagonists/syndicate_monkey/syndicate_monkey.dm:1
public partial class datum_antagonist_syndicate_monkey : datum_antagonist
{
    // tg: code/modules/antagonists/syndicate_monkey/syndicate_monkey.dm:12
    public override async DmTask on_gain()
    {
        monky_master = owner.enslaved_to?.resolve() as mob_living;
        if (monky_master != null)
            await forge_objectives(monky_master);
        await base.on_gain();
    }

    // tg: code/modules/antagonists/syndicate_monkey/syndicate_monkey.dm:18
    public override object Destroy(float? force = null)
    {
        monky_master = null;
        return base.Destroy(force);
    }

    // tg: code/modules/antagonists/syndicate_monkey/syndicate_monkey.dm:22
    public override async DmTask greet()
    {
        await base.greet();
        await owner.announce_objectives();
    }

    // tg: code/modules/antagonists/syndicate_monkey/syndicate_monkey.dm:32
    [DmParams("monky_master")]
    public override async DmTask forge_objectives(params object[] __extra)
    {
        mob monky_master = Dm.arg(__extra, 0) as mob;
        datum_objective_syndicate_monkey objective = Dm.@new<datum_objective_syndicate_monkey>();
        objective.monky_master = monky_master as mob_living;
        objective.explanation_text = I18n.T("You are a badass monkey syndicate agent. Protect and obey all of your master []'s orders!", monky_master);
        objective.owner = owner;
        objectives.Add(objective);
    }
}

// tg: code/modules/antagonists/syndicate_monkey/syndicate_monkey.dm:26
public partial class datum_objective_syndicate_monkey : datum_objective
{
    // tg: code/modules/antagonists/syndicate_monkey/syndicate_monkey.dm:29
    public override object check_completion()
    {
        return monky_master != null && monky_master.stat != DEAD ? TRUE : FALSE;
    }
}
