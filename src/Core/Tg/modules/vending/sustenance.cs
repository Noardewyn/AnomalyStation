// Translated from tgstation (snapshot 1d99bd7f): code/modules/vending/sustenance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The sustenance vendor (prisoners and the access holders only) and the labour camp's, which sells for the prisoner's labour points (its
// returned items free). The var blocks are generated.

// tg: code/modules/vending/sustenance.dm:2
public partial class obj_machinery_vending_sustenance : obj_machinery_vending
{
    // tg: code/modules/vending/sustenance.dm:29
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        mob_living living_user = user as mob_living;
        if (!isliving(living_user))
            return null;
        if (!istype(living_user.get_idcard(TRUE), typeof(obj_item_card_id_advanced_prisoner)))
        {
            if (!Dm.truthy(req_access))
            {
                await speak("No valid prisoner account found. Vending is not permitted.");
                return null;
            }
            if (!Dm.truthy(await allowed__proc(living_user)))
            {
                await speak("No valid permissions. Vending is not permitted.");
                return null;
            }
        }
        return await base.interact(user, __extra);
    }
}

//Labor camp subtype that uses labor points obtained from mining and processing ore
// tg: code/modules/vending/sustenance.dm:46
public partial class obj_machinery_vending_sustenance_labor_camp : obj_machinery_vending_sustenance
{
    // tg: code/modules/vending/sustenance.dm:56
    public override async DmTask<object> proceed_payment(obj_item_card_id paying_id_card = null, mob_living mob_paying = null, datum_data_vending_product product_to_vend = null, object price_to_use = null, object premium = null)
    {
        var paying_scum_id = paying_id_card as obj_item_card_id_advanced_prisoner;
        if (paying_scum_id == null)
        {
            await speak(I18n.T("I don't take bribes! Pay with labor points!"));
            return FALSE;
        }
        if (Dm.truthy(LAZYLEN(product_to_vend.returned_products)))
            price_to_use = 0; //returned items are free
        if (Dm.truthy(price_to_use) && !(paying_scum_id.points >= Dm.num(price_to_use))) //not enough good prisoner points
        {
            await speak(I18n.T("You do not possess enough points to purchase [].", product_to_vend.name));
            flick(icon_deny, this);
            return FALSE;
        }

        paying_scum_id.points -= Dm.num(price_to_use);
        return TRUE;
    }

    // tg: code/modules/vending/sustenance.dm:70
    public override object fetch_balance_to_use(obj_item_card_id passed_id = null)
    {
        if (!istype(passed_id, typeof(obj_item_card_id_advanced_prisoner)))
            return null; //no points balance - no balance at all
        var paying_scum_id = (obj_item_card_id_advanced_prisoner)passed_id;
        return paying_scum_id.points;
    }
}
