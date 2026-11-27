// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/shade/shade_minion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the loyal shade, a shade bound to a master who is not a cultist, with its AntagInfoShade window. The var
// block is generated (D111).

/**
 * This datum is for use by shades who have a master but are not cultists.
 * Cult shades don't get it because they get the cult datum instead.
 * They are bound to follow the orders of their creator, probably a chaplain or miner.
 * Technically they're not 'antagonists' but they have antagonist-like properties.
 */
// tg: code/modules/antagonists/shade/shade_minion.dm:7
public partial class datum_antagonist_shade_minion : datum_antagonist
{
    // tg: code/modules/antagonists/shade/shade_minion.dm:17
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["master_name"] = master_name;
        return data;
    }

    /// Apply a new master to the shade, will display the popup again also.
    // tg: code/modules/antagonists/shade/shade_minion.dm:23
    public virtual async DmTask update_master(string master_name = null)
    {
        if (this.master_name == master_name)
            return;

        this.master_name = master_name;
        await update_static_data(owner.current);
        Waiting.Detach(display_panel());
    }

    /// Shows the info panel, moved out into its own proc for signal handling reasons.
    // tg: code/modules/antagonists/shade/shade_minion.dm:32
    public virtual async DmTask display_panel()
    {
        datum_action_antag_info info_button = info_button_ref?.resolve() as datum_action_antag_info;
        if (info_button != null)
            await info_button.Trigger();
    }
}
