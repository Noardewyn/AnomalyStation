// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/garlands.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the garlands' mood while worn, which waits for moods; the rainbow bunch's random crown). The var
// blocks are generated (D111).

// tg: code/modules/clothing/head/garlands.dm:1
public partial class obj_item_clothing_head_costume_garland : obj_item_clothing_head_costume
{
    // tg: code/modules/clothing/head/garlands.dm:7
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot_flags, slot)))
            await ((mob_living)user).add_mood_event("garland", typeof(datum_mood_event_garland));
    }

    // tg: code/modules/clothing/head/garlands.dm:12
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        ((mob_living)user).clear_mood_event("garland");
        return dot;
    }
}

// tg: code/modules/clothing/head/garlands.dm:16
public partial class obj_item_clothing_head_costume_garland_rainbowbunch : obj_item_clothing_head_costume_garland
{
    // tg: code/modules/clothing/head/garlands.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        float crown_type = rand(1, 4);
        icon_state = Dm.text("[]_[]", base_icon_state, crown_type);
        switch (crown_type)
        {
            case 1:
                desc += I18n.T(" This one has red, yellow, and white flowers.");
                break;
            case 2:
                desc += I18n.T(" This one has blue, yellow, green, and white flowers.");
                break;
            case 3:
                desc += I18n.T(" This one has red, blue, purple, and pink flowers.");
                break;
            case 4:
                desc += I18n.T(" This one has yellow, green, and white flowers.");
                break;
        }
        return dot;
    }
}
