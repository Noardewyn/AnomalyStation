// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/roles/changelings/changeling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): the changelings, told at sundown to vote for their kill, and the thoughtfeeder. The var blocks are
// generated (D111).

// tg: code/modules/mafia/roles/changelings/changeling.dm:1
public partial class datum_mafia_role_mafia : datum_mafia_role
{
    // tg: code/modules/mafia/roles/changelings/changeling.dm:17
    [DmParams("game")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        datum_mafia_controller game = Dm.arg(__extra, 0) as datum_mafia_controller;
        RegisterSignal(game, COMSIG_MAFIA_SUNDOWN, nameof(mafia_text));
    }

    // tg: code/modules/mafia/roles/changelings/changeling.dm:21
    [SignalHandler]
    public virtual void mafia_text(datum_mafia_controller source = null)
    {
        to_chat(body, I18n.T("<b>Vote for who to kill tonight. The killer will be chosen randomly from voters.</b>"));
    }
}
