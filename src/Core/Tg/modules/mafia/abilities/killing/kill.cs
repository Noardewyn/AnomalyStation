// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/abilities/killing/kill.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): The attack and the head of security's execution. The var block is generated (D111).

/**
 * Attack
 *
 * During the night, attacks a player in attempts to kill them.
 */
// tg: code/modules/mafia/abilities/killing/kill.dm:6
public partial class datum_mafia_ability_attack_player : datum_mafia_ability
{
    // tg: code/modules/mafia/abilities/killing/kill.dm:15
    public override async DmTask<object> perform_action_target(datum_mafia_controller game = null, datum_mafia_role day_target = null)
    {
        object dot = await base.perform_action_target(game, day_target);
        if (!Dm.truthy(dot))
            return FALSE;

        if (!Dm.truthy(await target_role.kill(game, host_role, FALSE)))
            host_role.send_message_to_player(span_danger(I18n.T("Your attempt at killing [] was prevented!", target_role.body.real_name)));
        else
        {
            target_role.send_message_to_player(span_userdanger(I18n.T("You have been [] \\a []!", attack_action, host_role.name)));
            if (Dm.truthy(honorable) && Dm.truthy(Dm.bitand(target_role.team, MAFIA_TEAM_TOWN)))
            {
                host_role.send_message_to_player(span_userdanger(I18n.T("You have killed an innocent crewmember. You will die tomorrow night.")));
                RegisterSignal(game, COMSIG_MAFIA_SUNDOWN, nameof(internal_affairs));
            }
        }
        return TRUE;
    }

    // tg: code/modules/mafia/abilities/killing/kill.dm:29
    [SignalHandler]
    public virtual void internal_affairs(datum_mafia_controller game = null)
    {
        host_role.send_message_to_player(span_userdanger(I18n.T("You have been killed by Nanotrasen Internal Affairs!")));
        Waiting.NoSleep(host_role.reveal_role(game, verbose: TRUE));
        Waiting.NoSleep(host_role.kill(game, host_role, FALSE)); //you technically kill yourself but that shouldn't matter
    }
}
