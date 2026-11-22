// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/pirate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the pirate hat's pirate speak (the language granted while worn); its fishing difficulty element waits for
// fishing. The var blocks are generated (D111).

// tg: code/modules/clothing/head/pirate.dm:1
public partial class obj_item_clothing_head_costume_pirate : obj_item_clothing_head_costume
{
    // tg: code/modules/clothing/head/pirate.dm:8
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/head/pirate.dm:10", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, -5): fishing (M10+)");
        return dot;
    }

    // tg: code/modules/clothing/head/pirate.dm:12
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot_flags, slot)) || isdrone(user))
            return;
        user.grant_language(typeof(datum_language_piratespeak), source: LANGUAGE_HAT);
        to_chat(user, span_boldnotice(I18n.T("You suddenly know how to speak like a pirate!")));
    }

    // tg: code/modules/clothing/head/pirate.dm:19
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (QDELETED(this)) //This can be called as a part of destroy
            return dot;
        user.remove_language(typeof(datum_language_piratespeak), source: LANGUAGE_HAT);
        to_chat(user, span_boldnotice(I18n.T("You can no longer speak like a pirate.")));
        return dot;
    }
}
