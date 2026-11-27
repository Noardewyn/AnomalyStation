// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/stamps.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/paperwork/stamps.dm:1
public partial class obj_item_stamp : obj_item
{
    // tg: code/modules/paperwork/stamps.dm:17
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] stamps 'VOID' on [] forehead, then promptly falls over, dead.", user, user.p_their())));
        playsound(this, Dm.resource("sound/items/handling/standard_stamp.ogg"), 50, vary: TRUE);
        return OXYLOSS;
    }

    // tg: code/modules/paperwork/stamps.dm:22
    public override async DmTask<object> get_writing_implement_details()
    {
        // The sheet's icon class (get_asset_datum(/datum/asset/spritesheet/simple/stamps)) is the paper window's: S3, the paperwork (DV44: the client draws its sprites).
        return Dm.list(
            Dm.kv("interaction_mode", MODE_STAMPING),
            Dm.kv("stamp_icon_state", icon_state),
            Dm.kv("stamp_icon", icon),
            Dm.kv("stamp_class", icon_state));
    }

    // tg: code/modules/paperwork/stamps.dm:123
    public override DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return attack_hand(user, modifiers);
    }
}

// tg: code/modules/paperwork/stamps.dm:93
public partial class obj_item_stamp_clown : obj_item_stamp
{
    // tg: code/modules/paperwork/stamps.dm:98
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_CLOWN, CELL_VIRUS_TABLE_GENERIC, rand(2, 3), 0));
        return dot;
    }
}

// tg: code/modules/paperwork/stamps.dm:33
public partial class obj_item_stamp_head : obj_item_stamp
{
    // tg: code/modules/paperwork/stamps.dm:39  (gaps J4; REGISTER_REQUIRED_MAP_ITEM is a unit test's)
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra);
}
