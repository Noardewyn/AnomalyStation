// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implant_freedom.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3). The var blocks are generated (D111).

// tg: code/game/objects/items/implants/implant_freedom.dm:1
public partial class obj_item_implant_freedom : obj_item_implant
{
    // tg: code/game/objects/items/implants/implant_freedom.dm:19
    [DmParams("target", "user", "silent", "force")]
    public override async DmTask<object> implant(mob_living target = null, mob user = null, float? silent = null, float? force = null, params object[] __extra)
    {
        object dot = await base.implant(target, user, silent, force, __extra);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!iscarbon(target)) //This is pretty much useless for anyone else since they can't be cuffed
        {
            balloon_alert(user, I18n.T("that would be a waste!"));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/game/objects/items/implants/implant_freedom.dm:28
    public override async DmTask<object> activate(params object[] __extra)
    {
        object dot = await base.activate(__extra);
        mob_living_carbon carbon_imp_in = imp_in as mob_living_carbon;
        if (!Dm.truthy(can_trigger(carbon_imp_in)))
        {
            balloon_alert(carbon_imp_in, I18n.T("no restraints!"));
            return dot;
        }

        uses--;

        await carbon_imp_in.uncuff(resist_strength);
        obj_item_clothing_shoes shoes = carbon_imp_in.get_item_by_slot(ITEM_SLOT_FEET) as obj_item_clothing_shoes;
        if (shoes != null && Dm.eq(shoes.tied, SHOES_KNOTTED))
            shoes.adjust_laces(SHOES_TIED, carbon_imp_in);

        if (!Dm.truthy(uses))
        {
            _addtimer(Dm.@new<datum_callback>(carbon_imp_in, nameof(balloon_alert), carbon_imp_in, I18n.T("implant degraded!")), 1 * SECONDS, file: "code/game/objects/items/implants/implant_freedom.dm", line: 44);
            qdel(this);
        }
        return dot;
    }

    // tg: code/game/objects/items/implants/implant_freedom.dm:46
    public virtual object can_trigger(mob_living_carbon implanted_in = null)
    {
        foreach (obj_item restraint in Dm.each<obj_item>(implanted_in.get_all_attached_restraints()))
        {
            if (Dm.num(restraint.breakouttime) <= resist_strength)
                return TRUE;
        }

        obj_item_clothing_shoes shoes = implanted_in.get_item_by_slot(ITEM_SLOT_FEET) as obj_item_clothing_shoes;
        if (shoes != null && Dm.eq(shoes.tied, SHOES_KNOTTED))
            return TRUE;

        return FALSE;
    }
}
