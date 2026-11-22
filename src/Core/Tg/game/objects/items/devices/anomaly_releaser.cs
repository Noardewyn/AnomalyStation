// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/anomaly_releaser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The anomaly releaser: three seconds of injecting an anomaly core release its anomaly, stabilized,
// and use the injector up (its "used" picture). The anomaly cores themselves are not ported yet (research/anomaly/anomaly_core.dm,
// not-translated.md 216), so only an admin-made core reaches it. The var block is generated (D111).

///Use on an anomaly core to "awake" the anomaly and stabilize it
// tg: code/game/objects/items/devices/anomaly_releaser.dm:2
public partial class obj_item_anomaly_releaser : obj_item
{
    // tg: code/game/objects/items/devices/anomaly_releaser.dm:22
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        atom target = interacting_with;
        if (target is not obj_item_assembly_signaler_anomaly)
            return NONE;

        if (Dm.truthy(used))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target)))
            return ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(used))
            return ITEM_INTERACT_BLOCKING;

        obj_item_assembly_signaler_anomaly core = (obj_item_assembly_signaler_anomaly)target;
        if (core.anomaly_type == null)
            return ITEM_INTERACT_BLOCKING;

        obj_effect_anomaly anomaly = Dm.@new(core.anomaly_type, get_turf(core)) as obj_effect_anomaly;
        anomaly.stabilize();
        log_combat(user, anomaly, "released", @object: this, addition: Dm.text("in [].", get_area(target)));

        if (!Dm.truthy(infinite))
        {
            icon_state = used_icon_state;
            used = TRUE;
            name = I18n.T("used []", name);
            qdel(core);
        }
        return ITEM_INTERACT_SUCCESS;
    }
}
