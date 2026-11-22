// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implant_kaza_ruk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3b (D316): the Kaza Ruk implant, whole (its implanter and case are var blocks). The var blocks are
// generated (D111).

// tg: code/game/objects/items/implants/implant_kaza_ruk.dm:1
public partial class obj_item_implant_kaza_ruk : obj_item_implant
{
    // tg: code/game/objects/items/implants/implant_kaza_ruk.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        style = Dm.@new<datum_martial_art_kaza_ruk>(this);
        return dot;
    }

    // tg: code/game/objects/items/implants/implant_kaza_ruk.dm:19
    public override object Destroy(float? force = null)
    {
        qdel(style as datum); // QDEL_NULL(style)
        style = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/implants/implant_kaza_ruk.dm:23
    public override async DmTask<object> activate(params object[] __extra)
    {
        object dot = await base.activate(__extra);
        if (imp_in.mind == null)
            return dot;
        var kaza_ruk = (datum_martial_art_kaza_ruk)style;
        if (Dm.truthy(await kaza_ruk.unlearn(imp_in as mob_living)))
            return dot;

        await kaza_ruk.teach(imp_in as mob_living);
        return dot;
    }
}
