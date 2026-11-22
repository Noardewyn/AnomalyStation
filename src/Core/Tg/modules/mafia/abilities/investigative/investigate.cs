// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/abilities/investigative/investigate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): The detective's investigation, which tells a player's team. The var block is generated (D111).

/**
 * Investigate
 *
 * During the night, Investigating will reveal the person's faction.
 */
// tg: code/modules/mafia/abilities/investigative/investigate.dm:6
public partial class datum_mafia_ability_investigate : datum_mafia_ability
{
    // tg: code/modules/mafia/abilities/investigative/investigate.dm:10
    public override async DmTask<object> perform_action_target(datum_mafia_controller game = null, datum_mafia_role day_target = null)
    {
        object dot = await base.perform_action_target(game, day_target);
        if (!Dm.truthy(dot))
            return FALSE;

        string fluff = I18n.T("a member of the station, or great at deception.");
        if (!Dm.truthy(Dm.bitand(target_role.role_flags, ROLE_UNDETECTABLE)))
        {
            switch (Dm.num(target_role.team))
            {
                case MAFIA_TEAM_MAFIA:
                    fluff = I18n.T("an unfeeling, hideous changeling!");
                    break;
                case MAFIA_TEAM_SOLO:
                    fluff = I18n.T("rogue, with their own objectives...");
                    break;
            }
        }

        host_role.send_message_to_player(span_warning(I18n.T("Your investigations reveal that [] is []", target_role.body.real_name, fluff)));
        return TRUE;
    }
}
