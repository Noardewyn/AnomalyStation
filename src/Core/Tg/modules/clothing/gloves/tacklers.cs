// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/gloves/tacklers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// /obj/item/clothing/gloves/tackler whole (M4a; the tackle with gaps J4). The fishing element is section 105.

// tg: code/modules/clothing/gloves/tacklers.dm:1
public partial class obj_item_clothing_gloves_tackler : obj_item_clothing_gloves
{
    // tg: code/modules/clothing/gloves/tacklers.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/adjust_fishing_difficulty, fishing_modifier): fishing difficulty (section 105)
        return dot;
    }

    // tg: code/modules/clothing/gloves/tacklers.dm:33
    public override object Destroy(float? force = null)
    {
        tackler = null;
        return base.Destroy(force);
    }

    // tg: code/modules/clothing/gloves/tacklers.dm:37
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!ishuman(user))
            return;
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_GLOVES)))
        {
            mob_living_carbon_human H = user as mob_living_carbon_human;
            tackler = await H.AddComponent(typeof(datum_component_tackler), Dm.named("stamina_cost", tackle_stam_cost), Dm.named("base_knockdown", base_knockdown), Dm.named("range", tackle_range), Dm.named("speed", tackle_speed), Dm.named("skill_mod", skill_mod), Dm.named("min_distance", min_distance)) as datum_component;
        }
    }

    // tg: code/modules/clothing/gloves/tacklers.dm:45
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.dropped(user, silent, __extra);
        if (!ishuman(user))
            return dot;
        mob_living_carbon_human H = user as mob_living_carbon_human;
        if (Dm.eq(H.get_item_by_slot(ITEM_SLOT_GLOVES), this))
            QDEL_NULL(ref tackler);
        return dot;
    }
}
