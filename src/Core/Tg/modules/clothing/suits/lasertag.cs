// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/lasertag.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The laser tag armours whole: equipped and dropped (M4a clothing); gaps CL1 (D333): the vest gives its team's lasertag component
// (suits/_lasertag_components.cs), which the laser tag pins and beams read.

// tg: code/modules/clothing/suits/lasertag.dm:1
public partial class obj_item_clothing_suit_bluetag : obj_item_clothing_suit
{
    // tg: code/modules/clothing/suits/lasertag.dm:14
    [DmParams("equipper", "slot")]
    public override async DmTask equipped__proc(mob equipper = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(equipper, slot, initial);
        if (!Dm.eq(slot, ITEM_SLOT_OCLOTHING))
            return;
        await equipper.AddComponentFrom(REF(this), typeof(datum_component_lasertag), LASERTAG_TEAM_BLUE);
    }

    // tg: code/modules/clothing/suits/lasertag.dm:21
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.dropped(user, silent, __extra);
        user.RemoveComponentSource(REF(this), typeof(datum_component_lasertag));
        return dot;
    }
}

// tg: code/modules/clothing/suits/lasertag.dm:25
public partial class obj_item_clothing_suit_redtag : obj_item_clothing_suit
{
    // tg: code/modules/clothing/suits/lasertag.dm:38
    [DmParams("equipper", "slot")]
    public override async DmTask equipped__proc(mob equipper = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(equipper, slot, initial);
        if (!Dm.eq(slot, ITEM_SLOT_OCLOTHING))
            return;
        await equipper.AddComponentFrom(REF(this), typeof(datum_component_lasertag), LASERTAG_TEAM_RED);
    }

    // tg: code/modules/clothing/suits/lasertag.dm:45
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.dropped(user, silent, __extra);
        user.RemoveComponentSource(REF(this), typeof(datum_component_lasertag));
        return dot;
    }
}
