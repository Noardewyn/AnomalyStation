// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implantcase.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M8 slice F, D186).

/**
 * Item used to store implants. Can be renamed with a pen. Implants are moved between those and implanters when a mob uses an implanter on a case.
 */
// tg: code/game/objects/items/implants/implantcase.dm:4
public partial class obj_item_implantcase : obj_item
{
    // tg: code/game/objects/items/implants/implantcase.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (imp_type != null)
            imp = Dm.@new(imp_type, this) as obj_item_implant;
        update_appearance();
        if (imp != null)
            reagents = imp.reagents;
        return dot;
    }

    // tg: code/game/objects/items/implants/implantcase.dm:30
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref imp);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/implants/implantcase.dm:34
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("implantcase-[]", imp != null ? imp.implant_color : 0);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/implants/implantcase.dm:38
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_implanter)))
        {
            obj_item_implanter used_implanter = (obj_item_implanter)tool;
            if (used_implanter.imp != null && imp == null)
            {
                //implanter to case implant transfer
                await used_implanter.imp.forceMove(this);
                imp = used_implanter.imp;
                used_implanter.imp = null;
                update_appearance();
                reagents = imp.reagents;
                used_implanter.update_appearance();
            }
            else if (used_implanter.imp == null && imp != null)
            {
                //implant case to implanter implant transfer
                await imp.forceMove(used_implanter);
                used_implanter.imp = imp;
                imp = null;
                reagents = null;
                update_appearance();
                used_implanter.update_appearance();
            }
            return ITEM_INTERACT_SUCCESS;
        }
        if (imp != null)
            return await imp.base_item_interaction(user, tool, modifiers);
        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/game/objects/items/implants/implantcase.dm:61
    public override async DmTask<object> nameformat(object input = null, mob_living user = null)
    {
        return Dm.text("implant case[]", Dm.truthy(input) ? Dm.text(" - '[]'", input) : null);
    }
}
