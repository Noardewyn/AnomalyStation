// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/plumbing/pill_press.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var block is generated (D111).

// tg: code/datums/components/plumbing/pill_press.dm:4
public partial class datum_component_plumbing_pill_press : datum_component_plumbing
{
    ///max amount of pills allowed on our tile before we start storing them instead
    // tg: code/datums/components/plumbing/pill_press.dm:2  #define MAX_FLOOR_PRODUCTS 10 (file-local, rules 11.4)
    private const float MAX_FLOOR_PRODUCTS = 10;

    // tg: code/datums/components/plumbing/pill_press.dm:8
    [DmParams("ducting_layer")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!istype(parent, typeof(obj_machinery_plumbing_pill_press)))
            return COMPONENT_INCOMPATIBLE;
        return await base.Initialize(__extra);
    }

    // tg: code/datums/components/plumbing/pill_press.dm:13
    public override async DmTask send_request(object dir = null)
    {
        obj_machinery_plumbing_pill_press target = (obj_machinery_plumbing_pill_press)parent;

        //required volume of reagents to package the product has been sent
        if (reagents.total_volume >= target.current_volume)
            return;

        //dont dump too much products on the ground
        float container_amount = 0;
        foreach (obj_item_reagent_containers thing in Dm.each<obj_item_reagent_containers>(target.loc))
        {
            container_amount++;
            if (container_amount >= MAX_FLOOR_PRODUCTS) //too much so just stop
                return;
        }

        //request reagents
        await base.send_request(dir);
    }
}
