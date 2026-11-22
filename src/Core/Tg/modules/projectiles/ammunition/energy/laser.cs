// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/ammunition/energy/laser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps GU1 (D333): the file whole. Every energy casing of the file is a var block (generated); the laser gatling's caseless laser
// casing (/obj/item/ammo_casing/laser) keeps its icon state live. The var blocks are generated (D111).

///not exactly an energy ammo casing, but it's used by the laser gatling.
// tg: code/modules/projectiles/ammunition/energy/laser.dm:183
public partial class obj_item_ammo_casing_laser : obj_item_ammo_casing
{
    // tg: code/modules/projectiles/ammunition/energy/laser.dm:194
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_caseless)));
        return dot;
    }

    // tg: code/modules/projectiles/ammunition/energy/laser.dm:198
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[]", base_icon_state);
        return dot;
    }
}
