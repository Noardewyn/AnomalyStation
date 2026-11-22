// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/panacea.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: Anatomic Panacea (the parasite organs expelled with a vomit, the purging and curing reagents,
// the traumas and the diseases cured, the latter since gaps VI1). The var block is generated (D111).

// tg: code/modules/antagonists/changeling/powers/panacea.dm:1
public partial class datum_action_changeling_panacea : datum_action_changeling
{
    //Heals the things that the other regenerative abilities don't.
    // tg: code/modules/antagonists/changeling/powers/panacea.dm:12
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        to_chat(user, span_notice(I18n.T("We cleanse impurities from our form.")));
        await base.sting_action(user, target);
        DmList bad_organs = Dm.list(
            user.get_organ_by_type(typeof(obj_item_organ_body_egg)),
            user.get_organ_by_type(typeof(obj_item_organ_legion_tumour)),
            user.get_organ_by_type(typeof(obj_item_organ_zombie_infection)));

        foreach (object o in Dm.each(bad_organs))
        {
            obj_item_organ O = o as obj_item_organ;
            if (!(O is obj_item_organ))
                continue;

            await O.Remove(user as mob_living_carbon);
            if (iscarbon(user))
            {
                mob_living_carbon C = user as mob_living_carbon;
                await C.vomit(VOMIT_CATEGORY_DEFAULT, lost_nutrition: 0);
            }
            await O.forceMove(get_turf(user));
        }

        await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_medicine_mutadone), 10);
        await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_medicine_pen_acid), 20);
        await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_medicine_antihol), 10);
        await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_medicine_mannitol), 25);

        if (iscarbon(user))
        {
            mob_living_carbon C = user as mob_living_carbon;
            C.cure_all_traumas(TRAUMA_RESILIENCE_LOBOTOMY);
        }

        if (isliving(user))
        {
            mob_living L = user;
            foreach (object thing in Dm.each(L.diseases))
            {
                datum_disease D = thing as datum_disease;
                if (D.severity == DISEASE_SEVERITY_POSITIVE)
                    continue;
                await D.cure(); // gaps VI1
            }
        }
        return TRUE;
    }
}
