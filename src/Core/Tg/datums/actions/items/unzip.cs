// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/items/unzip.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/datums/actions/items/unzip.dm:1
public partial class datum_action_item_action_zipper : datum_action_item_action
{
    // tg: code/datums/actions/items/unzip.dm:5
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(target, COMSIG_DUFFEL_ZIP_CHANGE, nameof(on_zip_change));
        obj_item_storage_backpack_duffelbag duffle_target = (obj_item_storage_backpack_duffelbag)target;
        on_zip_change(target, duffle_target.zipped_up);
    }

    // tg: code/datums/actions/items/unzip.dm:11
    [SignalHandler]
    public virtual void on_zip_change(datum source = null, object new_zip = null)
    {
        if (Dm.truthy(new_zip))
        {
            name = "Unzip";
            desc = "Unzip your equipped duffelbag so you can access its contents.";
        }
        else
        {
            name = "Zip";
            desc = "Zip your equipped duffelbag so you can move around faster.";
        }
        Waiting.NoSleep(build_all_button_icons(UPDATE_BUTTON_NAME));
    }
}
