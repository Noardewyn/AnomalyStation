// Translated from tgstation (snapshot 1d99bd7f): code/modules/vending/security.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the file's one proc (the vendor's grenades thrown armed, its flashlights thrown lit). The var blocks are generated.

// tg: code/modules/vending/security.dm:2
public partial class obj_machinery_vending_security : obj_machinery_vending
{
    // tg: code/modules/vending/security.dm:41
    public override async DmTask pre_throw(obj_item thrown_item = null)
    {
        if (istype(thrown_item, typeof(obj_item_grenade))) // isgrenade(), __DEFINES/is_helpers.dm
        {
            obj_item_grenade thrown_grenade = (obj_item_grenade)thrown_item;
            await thrown_grenade.arm_grenade();
        }
        else if (istype(thrown_item, typeof(obj_item_flashlight)))
        {
            obj_item_flashlight thrown_flashlight = (obj_item_flashlight)thrown_item;
            await thrown_flashlight.set_light_on(TRUE);
            thrown_flashlight.update_brightness();
        }
    }
}
