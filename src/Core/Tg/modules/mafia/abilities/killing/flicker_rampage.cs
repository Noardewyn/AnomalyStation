// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/abilities/killing/flicker_rampage.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): The nightmare's flicker and rampage. The var block is generated (D111).

/**
 * Flicker/Rampage
 *
 * During the night, turns the lights off in a player's house.
 * If they visit someone with the lights off again, they will kill all players they previously visited.
 */
// tg: code/modules/mafia/abilities/killing/flicker_rampage.dm:7
public partial class datum_mafia_ability_flicker_rampage : datum_mafia_ability
{
    // tg: code/modules/mafia/abilities/killing/flicker_rampage.dm:15
    [DmParams("host_role")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        datum_mafia_role host_role = Dm.arg(__extra, 0) as datum_mafia_role;
        RegisterSignal(host_role, COMSIG_MAFIA_ON_KILL, nameof(flickering_immunity));
    }

    // tg: code/modules/mafia/abilities/killing/flicker_rampage.dm:19
    public override async DmTask<object> perform_action_target(datum_mafia_controller game = null, datum_mafia_role day_target = null)
    {
        object dot = await base.perform_action_target(game, day_target);
        if (!Dm.truthy(dot))
            return FALSE;

        if (!Dm.@in(target_role, darkened_players))
        {
            target_role.send_message_to_player(span_userdanger(I18n.T("The lights begin to flicker and dim. You're in danger.")));
            Dm.append(ref darkened_players, target_role);
        }
        else
        {
            foreach (datum_mafia_role dead_players in Dm.each_cast<datum_mafia_role>(darkened_players))
            {
                dead_players.send_message_to_player(span_userdanger(I18n.T("A shadowy figure appears out of the darkness!")));
                await dead_players.kill(game, host_role, FALSE);
                Dm.remove(ref darkened_players, dead_players);
            }
        }
        return TRUE;
    }

    // tg: code/modules/mafia/abilities/killing/flicker_rampage.dm:34
    [SignalHandler]
    public virtual object flickering_immunity(datum source = null, datum_mafia_controller game = null, object attacker = null, object lynch = null)
    {
        if (!Dm.truthy(attacker))
            return null; //no chance man, that's a town lynch

        if (Dm.@in(attacker, darkened_players))
        {
            host_role.send_message_to_player(span_userdanger(I18n.T("You were attacked by someone in a flickering room. You have danced in the shadows, evading them.")));
            return MAFIA_PREVENT_KILL;
        }
        return null;
    }
}
