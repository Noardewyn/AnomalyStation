// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_dissection.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated. The research notes are an item only until the ancient
// R&D servers (research) take them.

// tg: code/modules/surgery/operations/operation_dissection.dm:1
public partial class datum_surgery_operation_basic_dissection : datum_surgery_operation_basic
{
    // tg: code/modules/surgery/operations/operation_dissection.dm:18
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_paper));
    }

    // tg: code/modules/surgery/operations/operation_dissection.dm:21
    public override object all_required_strings()
    {
        object dot = null;
        Dm.append(ref dot, base.all_required_strings());
        Dm.append(ref dot, I18n.T("the patient must be deceased"));
        Dm.append(ref dot, I18n.T("the patient must not have been dissected prior"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_dissection.dm:26
    public override object state_check(atom_movable operating_on = null)
    {
        mob_living patient = (mob_living)operating_on;
        return Dm.num(!HAS_TRAIT_FROM(patient, TRAIT_DISSECTED, EXPERIMENTAL_SURGERY_TRAIT) && Dm.eq(patient.stat, DEAD));
    }

    // tg: code/modules/surgery/operations/operation_dissection.dm:29
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        mob_living patient = (mob_living)operating_on;
        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You begin to dissect []...", patient)),
            span_notice(I18n.T("[] begins to dissect [].", surgeon, patient)),
            span_notice(I18n.T("[] begins to dissect [].", surgeon, patient)));
    }

    // tg: code/modules/surgery/operations/operation_dissection.dm:37
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        mob_living patient = (mob_living)operating_on;
        float points_earned = Dm.num(round(Dm.num(check_value(patient)) * 0.01f));
        display_results(
            surgeon,
            patient,
            span_warning(I18n.T("You dissect [], but don't find anything particularly interesting.", patient)),
            span_warning(I18n.T("[] dissects [].", surgeon, patient)),
            span_warning(I18n.T("[] dissects [].", surgeon, patient)));
        await give_paper(surgeon, points_earned);
        await patient.apply_damage(80, BRUTE, BODY_ZONE_CHEST);
        ADD_TRAIT(patient, TRAIT_DISSECTED, EXPERIMENTAL_SURGERY_TRAIT);
    }

    // tg: code/modules/surgery/operations/operation_dissection.dm:50
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        mob_living patient = (mob_living)operating_on;
        object points_earned = check_value(patient);
        display_results(
            surgeon,
            patient,
            span_warning(I18n.T("You dissect [], discovering [] point[] of data!", patient, points_earned, Dm.num(points_earned) == 1 ? "" : "s")),
            span_warning(I18n.T("[] dissects [].", surgeon, patient)),
            span_warning(I18n.T("[] dissects [].", surgeon, patient)));
        await give_paper(surgeon, points_earned);
        await patient.apply_damage(80, BRUTE, BODY_ZONE_CHEST);
        ADD_TRAIT(patient, TRAIT_DISSECTED, EXPERIMENTAL_SURGERY_TRAIT);
        return null;
    }

    // tg: code/modules/surgery/operations/operation_dissection.dm:63
    public virtual async DmTask give_paper(mob_living surgeon = null, object points = null)
    {
        obj_item_research_notes the_dossier = Dm.@new<obj_item_research_notes>(surgeon.loc, points, "biology");
        if (!Dm.truthy(await surgeon.put_in_hands(the_dossier)) && istype(surgeon.get_inactive_held_item(), typeof(obj_item_research_notes)))
        {
            obj_item_research_notes hand_dossier = (obj_item_research_notes)surgeon.get_inactive_held_item();
            hand_dossier.merge(the_dossier);
        }
    }

    ///Calculates how many research points dissecting 'target' is worth.
    // tg: code/modules/surgery/operations/operation_dissection.dm:70
    public virtual object check_value(mob_living target = null)
    {
        float reward = 10;

        if (ishuman(target))
        {
            mob_living_carbon_human human_target = (mob_living_carbon_human)target;
            if (Dm.truthy(human_target.dna?.species))
            {
                if (HAS_TRAIT(human_target, TRAIT_LESSER_HUMANOID))
                    reward /= 5;
                else if (isabductor(human_target))
                    reward *= 4;
                else if (isgolem(human_target) || Dm.truthy(human_target.has_status_effect(typeof(datum_status_effect_zombie))))
                    reward *= 3;
                else if (isjellyperson(human_target) || ispodperson(human_target))
                    reward *= 2;
            }
        }
        else if (isalienroyal(target))
            reward *= 10;
        else if (isalienadult(target))
            reward *= 5;
        else
            reward /= 6;

        return reward;
    }
}

// tg: code/modules/surgery/operations/operation_dissection.dm:94
public partial class obj_item_research_notes : obj_item
{
    // tg: code/modules/surgery/operations/operation_dissection.dm:108
    [DmParams("mapload", "value", "origin_type")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object value = Dm.arg(__extra, 0);
        object origin_type = Dm.arg(__extra, 1);
        object dot = base.Initialize(mapload, Dm.forward(__extra, value, origin_type));
        if (Dm.truthy(value))
            this.value = Dm.num(value);
        if (Dm.truthy(origin_type))
            this.origin_type = Dm.text("[]", origin_type);
        change_vol();
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_dissection.dm:116
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It is worth [] research points.", value)));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_dissection.dm:120
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_research_notes)))
            return NONE;
        obj_item_research_notes notes = (obj_item_research_notes)tool;
        value = Dm.num(value) + Dm.num(notes.value);
        change_vol();
        qdel(notes);
        return ITEM_INTERACT_SUCCESS;
    }

    /// proc that changes name and icon depending on value
    // tg: code/modules/surgery/operations/operation_dissection.dm:130
    public virtual void change_vol()
    {
        if (Dm.num(value) >= TECHWEB_TIER_5_POINTS)
        {
            name = I18n.T("revolutionary discovery in the field of []", origin_type);
            icon_state = "docs_verified";
        }
        else if (Dm.num(value) >= TECHWEB_TIER_3_POINTS)
        {
            name = I18n.T("essay about []", origin_type);
            icon_state = "paper_words";
        }
        else if (Dm.num(value) >= TECHWEB_TIER_1_POINTS)
        {
            name = I18n.T("notes of []", origin_type);
            icon_state = "paperslip_words";
        }
        else
        {
            name = I18n.T("fragmentary data of []", origin_type);
            icon_state = "scrap";
        }
    }

    ///proc when you slap research notes into another one, it applies a bonus if they are of different origin (only applied once)
    // tg: code/modules/surgery/operations/operation_dissection.dm:145
    public virtual void merge(obj_item_research_notes new_paper = null)
    {
        float bonus = Dm.num(min(value, new_paper.value));
        value = Dm.num(value) + Dm.num(new_paper.value);
        if (!Dm.eq(origin_type, new_paper.origin_type) && !Dm.truthy(mixed))
        {
            value = Dm.num(value) + bonus * 0.3f;
            origin_type = Dm.text("[] and []", origin_type, new_paper.origin_type);
            mixed = TRUE;
        }
        change_vol();
        qdel(new_paper);
    }
}
