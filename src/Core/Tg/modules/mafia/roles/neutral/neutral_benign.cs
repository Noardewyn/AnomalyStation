// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/roles/neutral/neutral_benign.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): the fugitive, who wins by being alive when the game ends. The var block is generated (D111).

// tg: code/modules/mafia/roles/neutral/neutral_benign.dm:1
public partial class datum_mafia_role_fugitive : datum_mafia_role
{
    // tg: code/modules/mafia/roles/neutral/neutral_benign.dm:15
    [DmParams("game")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        datum_mafia_controller game = Dm.arg(__extra, 0) as datum_mafia_controller;
        RegisterSignal(game, COMSIG_MAFIA_GAME_END, nameof(survived));
    }

    // tg: code/modules/mafia/roles/neutral/neutral_benign.dm:19
    [SignalHandler]
    public virtual void survived(datum_mafia_controller game = null)
    {
        if (game_status == MAFIA_ALIVE)
        {
            game.award_role(winner_award, this);
            game.send_message(I18n.T("<span class='big comradio'>!! FUGITIVE VICTORY !!</span>"));
        }
    }
}
