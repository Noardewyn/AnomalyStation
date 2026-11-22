// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/abilities/investigative/autopsy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): The coroner's autopsy, which tells a dead player's role. The var block is generated (D111).

/**
 * Autopsy
 *
 * During the night, choose someone to check their role.
 */
// tg: code/modules/mafia/abilities/investigative/autopsy.dm:6
public partial class datum_mafia_ability_autopsy : datum_mafia_ability
{
    // tg: code/modules/mafia/abilities/investigative/autopsy.dm:11
    public override async DmTask<object> perform_action_target(datum_mafia_controller game = null, datum_mafia_role day_target = null)
    {
        object dot = await base.perform_action_target(game, day_target);
        if (!Dm.truthy(dot))
            return FALSE;

        to_chat(host_role.body, span_warning(I18n.T("Your autopsy report on [] reveals their role was <b>[]<b>.", target_role.body.real_name, target_role.name)));
        return TRUE;
    }
}
