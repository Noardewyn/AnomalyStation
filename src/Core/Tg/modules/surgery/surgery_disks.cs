// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/surgery_disks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The disks carry their operations' typepaths (the generated var blocks); the operating
// computer and the cyborg's surgical processor load them.

// tg: code/modules/surgery/surgery_disks.dm:10
public partial class obj_item_disk_surgery_debug : obj_item_disk_surgery
{
    // tg: code/modules/surgery/surgery_disks.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        surgeries = Dm.list();
        foreach (datum_surgery_operation operation in Dm.each_cast<datum_surgery_operation>(GLOB.operations.get_instances_from(subtypesof(typeof(datum_surgery_operation)) as DmList)))
            Dm.append(ref surgeries, operation.type);
        return dot;
    }
}

// tg: code/modules/surgery/surgery_disks.dm:22
public partial class obj_item_disk_surgery_advanced_plastic_surgery : obj_item_disk_surgery
{
    // tg: code/modules/surgery/surgery_disks.dm:29
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("Unlocks the <b>[]</b> surgical operation.", Dm.initial(typeof(datum_surgery_operation_limb_add_plastic), (datum_surgery_operation o) => o.name))));
        Dm.append(ref dot, span_info(I18n.T("Performing this before a <i>[]</i> upgrades the operation, allowing you to copy the appearance of any individual - provided you have a photo of them in your offhand during the surgery.", Dm.initial(typeof(datum_surgery_operation_limb_plastic_surgery), (datum_surgery_operation o) => o.name))));
        return dot;
    }

    // tg: code/modules/surgery/surgery_disks.dm:36
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore", I18n.T("Most forms of plastic surgery became obsolete due in no small part to advances in genetics technology. Very basic methods still remain in use, but scarcely, and primarily to reverse a patient's disfigurements. As a consequence, this item became an antique to many collectors - though some back alley surgeons still seek one out for its now uncommon knowledge."))));
        return dot;
    }
}
