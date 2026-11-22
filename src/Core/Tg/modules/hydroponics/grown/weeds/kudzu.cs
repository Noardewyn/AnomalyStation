// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/weeds/kudzu.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses); planting the seeds starts the space vines
// (gaps E4b). The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/weeds/kudzu.dm:4
public partial class obj_item_seeds_kudzu : obj_item_seeds
{
    // tg: code/modules/hydroponics/grown/weeds/kudzu.dm:22
    public override object Copy()
    {
        obj_item_seeds_kudzu S = base.Copy() as obj_item_seeds_kudzu;
        S.mutations = mutations.Copy();
        return S;
    }

    // tg: code/modules/hydroponics/grown/weeds/kudzu.dm:27
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] swallows the pack of kudzu seeds! It looks like [] trying to commit suicide!", user, user.p_theyre())));
        await plant(user);
        return BRUTELOSS;
    }

    // tg: code/modules/hydroponics/grown/weeds/kudzu.dm:32
    public virtual async DmTask<object> plant(mob user = null)
    {
        if (isspaceturf(user.loc))
            return null;
        if (!isturf(user.loc))
        {
            to_chat(user, span_warning(I18n.T("You need more space to plant [].", this)));
            return FALSE;
        }
        if (Dm.locate<obj_structure_spacevine>(user.loc) != null)
        {
            to_chat(user, span_warning(I18n.T("There is too much kudzu here to plant [].", this)));
            return FALSE;
        }
        to_chat(user, span_notice(I18n.T("You plant [].", this)));
        message_admins(Dm.text("Kudzu planted by [] at []", ADMIN_LOOKUPFLW(user), ADMIN_VERBOSEJMP(user)));
        investigate_log(Dm.text("was planted by [] at []", key_name(user), AREACOORD(user)), INVESTIGATE_BOTANY);
        await Dm.new_async<datum_spacevine_controller>(get_turf(user), mutations, potency, production);
        qdel(this);
        return null;
    }

    // tg: code/modules/hydroponics/grown/weeds/kudzu.dm:47
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        user.visible_message(span_danger(I18n.T("[] begins throwing seeds on the ground...", user)));
        if (Dm.truthy(await do_after(user, 5 * SECONDS, target: user.drop_location())))
        {
            await plant(user);
            to_chat(user, span_notice(I18n.T("You plant the kudzu. You monster.")));
        }
        return null;
    }

    // tg: code/modules/hydroponics/grown/weeds/kudzu.dm:53
    public override object get_unique_analyzer_data()
    {
        DmList all_mutations = Dm.list();
        foreach (datum_spacevine_mutation vine_trait in Dm.each<datum_spacevine_mutation>(mutations))
            all_mutations[vine_trait.name] = vine_trait.description;

        return Dm.list(Dm.kv("Kudzu Traits", all_mutations));
    }

    // tg: code/modules/hydroponics/grown/weeds/kudzu.dm:60
    public override void on_chem_reaction(datum_reagents reagents = null)
    {
        DmList temp_mut_list = Dm.list();

        if (Dm.truthy(reagents.has_reagent(typeof(datum_reagent_space_cleaner_sterilizine), 5)))
        {
            foreach (datum_spacevine_mutation SM in Dm.each<datum_spacevine_mutation>(mutations))
            {
                if (Dm.eq(SM.quality, NEGATIVE))
                    Dm.append(ref temp_mut_list, SM);
            }
            if (prob(20) && Dm.truthy(temp_mut_list.len))
                mutations.Remove(pick(temp_mut_list));
            temp_mut_list.Cut();
        }

        if (Dm.truthy(reagents.has_reagent(typeof(datum_reagent_fuel), 5)))
        {
            foreach (datum_spacevine_mutation SM in Dm.each<datum_spacevine_mutation>(mutations))
            {
                if (Dm.eq(SM.quality, POSITIVE))
                    Dm.append(ref temp_mut_list, SM);
            }
            if (prob(20) && Dm.truthy(temp_mut_list.len))
                mutations.Remove(pick(temp_mut_list));
            temp_mut_list.Cut();
        }

        if (Dm.truthy(reagents.has_reagent(typeof(datum_reagent_phenol), 5)))
        {
            foreach (datum_spacevine_mutation SM in Dm.each<datum_spacevine_mutation>(mutations))
            {
                if (Dm.eq(SM.quality, MINOR_NEGATIVE))
                    Dm.append(ref temp_mut_list, SM);
            }
            if (prob(20) && Dm.truthy(temp_mut_list.len))
                mutations.Remove(pick(temp_mut_list));
            temp_mut_list.Cut();
        }

        if (Dm.truthy(reagents.has_reagent(typeof(datum_reagent_blood), 15)))
            adjust_production(rand(15, -5));

        if (Dm.truthy(reagents.has_reagent(typeof(datum_reagent_toxin_amatoxin), 5)))
            adjust_production(rand(5, -15));

        if (Dm.truthy(reagents.has_reagent(typeof(datum_reagent_toxin_plasma), 5)))
            adjust_potency(rand(5, -15));

        if (Dm.truthy(reagents.has_reagent(typeof(datum_reagent_water_holywater), 10)))
            adjust_potency(rand(15, -5));
    }
}
