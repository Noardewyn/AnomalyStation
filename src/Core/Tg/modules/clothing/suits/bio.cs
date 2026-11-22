// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/bio.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the allowed lists of the security, janitor's and plague doctor's bio suits (the base suit and hood are in
// job_gear_j5.cs, gaps J5). The var blocks are generated (D111).

// tg: code/modules/clothing/suits/bio.dm:94
public partial class obj_item_clothing_suit_bio_suit_security : obj_item_clothing_suit_bio_suit
{
    // tg: code/modules/clothing/suits/bio.dm:108
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, GLOB.security_vest_allowed);
        return dot;
    }
}

// tg: code/modules/clothing/suits/bio.dm:117
public partial class obj_item_clothing_suit_bio_suit_janitor : obj_item_clothing_suit_bio_suit
{
    // tg: code/modules/clothing/suits/bio.dm:121
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, Dm.list(typeof(obj_item_storage_bag_trash), typeof(obj_item_reagent_containers_spray)));
        return dot;
    }
}

// tg: code/modules/clothing/suits/bio.dm:144
public partial class obj_item_clothing_suit_bio_suit_plaguedoctorsuit : obj_item_clothing_suit_bio_suit
{
    // tg: code/modules/clothing/suits/bio.dm:154
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref allowed, Dm.list(typeof(obj_item_book_bible), typeof(obj_item_nullrod), typeof(obj_item_cane)));
        return dot;
    }
}
