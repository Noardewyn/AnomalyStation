// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/antagonists/_parent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: gaps BR1 (D339, D341): the agent's ID fix the quantum server calls when it re-equips a glitch (server/util.dm:
// reset_equipment). The glitch's antag datum, its objective and its conversion come with gaps BR2 (not-translated section 230).

// tg: code/modules/bitrunning/antagonists/_parent.dm:5
public partial class datum_antagonist_bitrunning_glitch : datum_antagonist
{
    /// Resets the agent's ID and name. Needed so this doesn't show as "unknown"
    // tg: code/modules/bitrunning/antagonists/_parent.dm:79
    public virtual async DmTask fix_agent_id()
    {
        if (!ishuman(owner.current))
            return;

        mob_living_carbon_human player = owner.current as mob_living_carbon_human;

        obj_item_card_id outfit_id = player.wear_id as obj_item_card_id;
        if (outfit_id == null)
            return;

        outfit_id.set_account(Dm.@new<datum_bank_account>());
        outfit_id.registered_account.replaceable = FALSE;
        outfit_id.registered_account.account_id = null;
        outfit_id.registered_name = player.name;
        outfit_id.update_label();
        await DmTask.CompletedTask;
    }
}
