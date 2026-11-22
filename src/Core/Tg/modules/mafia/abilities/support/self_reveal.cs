// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/abilities/support/self_reveal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): The head of personnel's reveal: triple votes, no protection. The var block is generated (D111).

/**
 * Self reveal
 *
 * During the day, reveals your role to everyone and gives you a voting power boost,
 * however it will additionally make you unable to be protected.
 */
// tg: code/modules/mafia/abilities/support/self_reveal.dm:7
public partial class datum_mafia_ability_self_reveal : datum_mafia_ability
{
    // tg: code/modules/mafia/abilities/support/self_reveal.dm:14
    public override async DmTask<object> perform_action_target(datum_mafia_controller game = null, datum_mafia_role day_target = null)
    {
        object dot = await base.perform_action_target(game, day_target);
        if (!Dm.truthy(dot))
            return FALSE;
        await host_role.reveal_role(game, TRUE);
        host_role.role_flags = Dm.num(Dm.bitor(host_role.role_flags, ROLE_VULNERABLE));
        host_role.vote_power *= 3;
        Dm.remove(ref host_role.role_unique_actions, this);
        qdel(this);
        return dot;
    }
}
