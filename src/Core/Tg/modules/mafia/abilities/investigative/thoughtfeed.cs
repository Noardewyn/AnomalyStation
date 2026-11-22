// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/abilities/investigative/thoughtfeed.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): The thoughtfeeder's feast, which tells a player's role. The var block is generated (D111).

/**
 * Thoughtfeeding
 *
 * During the night, thoughtfeeding will reveal the person's exact role.
 */
// tg: code/modules/mafia/abilities/investigative/thoughtfeed.dm:6
public partial class datum_mafia_ability_thoughtfeeder : datum_mafia_ability
{
    // tg: code/modules/mafia/abilities/investigative/thoughtfeed.dm:10
    public override async DmTask<object> perform_action_target(datum_mafia_controller game = null, datum_mafia_role day_target = null)
    {
        object dot = await base.perform_action_target(game, day_target);
        if (!Dm.truthy(dot))
            return FALSE;

        if (Dm.truthy(Dm.bitand(target_role.role_flags, ROLE_UNDETECTABLE)))
            host_role.send_message_to_player(span_warning(I18n.T("[]'s memories reveal that they are the [].", target_role.body.real_name, pick(Dm.sub(game.all_roles, target_role) as DmList))));
        else
            host_role.send_message_to_player(span_warning(I18n.T("[]'s memories reveal that they are the [].", target_role.body.real_name, target_role.name)));
        return TRUE;
    }
}
