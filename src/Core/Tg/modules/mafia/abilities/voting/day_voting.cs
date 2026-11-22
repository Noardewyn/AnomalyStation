// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/abilities/voting/day_voting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): The day vote for the trial. The var block is generated (D111).

/**
 * Voting
 *
 * During the vote period, voting for someone is showing your intent to get them lynched.
 */
// tg: code/modules/mafia/abilities/voting/day_voting.dm:6
public partial class datum_mafia_ability_voting : datum_mafia_ability
{
    // tg: code/modules/mafia/abilities/voting/day_voting.dm:12
    public override async DmTask<object> perform_action_target(datum_mafia_controller game = null, datum_mafia_role day_target = null)
    {
        object dot = await base.perform_action_target(game, day_target);
        if (!Dm.truthy(dot))
            return FALSE;
        game.vote_for(host_role, day_target, vote_type: "Day");
        return TRUE;
    }
}
