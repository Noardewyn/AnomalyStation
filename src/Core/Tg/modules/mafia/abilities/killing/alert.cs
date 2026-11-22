// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/abilities/killing/alert.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): The warden's alert, which kills every visitor of the night, once a game. The var block is generated (D111).

/**
 * Alert
 *
 * During the night, goes on watch, killing all players who visit.
 */
// tg: code/modules/mafia/abilities/killing/alert.dm:6
public partial class datum_mafia_ability_attack_visitors : datum_mafia_ability
{
    // tg: code/modules/mafia/abilities/killing/alert.dm:11
    public override object set_target(datum_mafia_role new_target = null)
    {
        object dot = base.set_target(new_target);
        if (!Dm.truthy(dot))
            return FALSE;
        if (Dm.truthy(using_ability))
            RegisterSignal(host_role, COMSIG_MAFIA_ON_VISIT, nameof(self_defense));
        else
            UnregisterSignal(host_role, COMSIG_MAFIA_ON_VISIT);
        return TRUE;
    }

    // tg: code/modules/mafia/abilities/killing/alert.dm:21
    [SignalHandler]
    public virtual object self_defense(datum source = null, datum_mafia_controller game = null, datum_mafia_role attacker = null)
    {
        if (attacker == host_role)
            return null;
        host_role.send_message_to_player(span_userdanger(I18n.T("You have shot a visitor!")));
        attacker.send_message_to_player(span_userdanger(I18n.T("You have visited the warden!")));
        Waiting.NoSleep(attacker.kill(game, host_role, lynch: FALSE));
        return MAFIA_VISIT_INTERRUPTED;
    }

    // tg: code/modules/mafia/abilities/killing/alert.dm:30
    [SignalHandler]
    public override void clean_action_refs(datum_mafia_controller game = null)
    {
        if (Dm.truthy(using_ability))
        {
            Dm.remove(ref host_role.role_unique_actions, this);
            qdel(this);
        }
        base.clean_action_refs(game);
    }
}
