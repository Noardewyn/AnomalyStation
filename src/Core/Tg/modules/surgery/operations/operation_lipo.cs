// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_lipo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// tg: code/modules/surgery/operations/operation_lipo.dm:1
public partial class datum_surgery_operation_limb_lipoplasty : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_lipo.dm:26
    public override object get_any_tool()
    {
        return I18n.T("Any sharp edged item");
    }

    // tg: code/modules/surgery/operations/operation_lipo.dm:29
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_food_meat_slab_human));
    }

    // tg: code/modules/surgery/operations/operation_lipo.dm:32
    public override object all_required_strings()
    {
        object dot = Dm.list();
        Dm.append(ref dot, I18n.T("operate on chest (target chest)"));
        Dm.append(ref dot, base.all_required_strings());
        Dm.append(ref dot, I18n.T("the patient must have excess fat to remove"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_lipo.dm:38
    public override object tool_check(obj_item tool = null)
    {
        // Require edged sharpness OR a tool behavior match
        return Dm.truthy(Dm.bitand(tool.get_sharpness(), SHARP_EDGED)) ? Dm.bitand(tool.get_sharpness(), SHARP_EDGED) : implements[tool.tool_behaviour];
    }

    // tg: code/modules/surgery/operations/operation_lipo.dm:42
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        if (!Dm.eq(limb.body_zone, BODY_ZONE_CHEST))
            return FALSE;
        if (HAS_TRAIT(limb.owner, TRAIT_NOHUNGER))
            return FALSE;
        if (!HAS_TRAIT_FROM(limb.owner, TRAIT_FAT, OBESITY) || limb.owner.nutrition < NUTRITION_LEVEL_WELL_FED)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_lipo.dm:51
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to cut away []'s excess fat...", limb.owner)),
            span_notice(I18n.T("[] begins to cut away []'s excess fat.", surgeon, limb.owner)),
            span_notice(I18n.T("[] begins to cut []'s [] with [].", surgeon, limb.owner, limb.plaintext_zone, tool)));
        await display_pain(limb.owner, I18n.T("You feel a stabbing in your []!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_lipo.dm:61
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You successfully remove excess fat from []'s body!", limb.owner)),
            span_notice(I18n.T("[] successfully removes excess fat from []'s body!", surgeon, limb.owner)),
            span_notice(I18n.T("[] finishes cutting away excess fat from []'s [].", surgeon, limb.owner, limb.plaintext_zone)));
        float removednutriment = limb.owner.nutrition;
        limb.owner.overeatduration = 0; //patient is unfatted
        await limb.owner.set_nutrition(NUTRITION_LEVEL_WELL_FED);
        removednutriment -= NUTRITION_LEVEL_WELL_FED; //whatever was removed goes into the meat

        if (Dm.truthy(Dm.bitand(limb.owner.flags_1, HOLOGRAM_1)))
            return null;

        Type typeofmeat = null;
        foreach (object meat_path in Dm.each(limb.get_butcher_drops()))
        {
            if (ispath(meat_path, typeof(obj_item_food_meat)))
            {
                typeofmeat = (Type)meat_path;
                break;
            }
        }

        if (typeofmeat == null)
            return null;

        obj_item_food_meat_slab newmeat = (obj_item_food_meat_slab)Dm.@new(typeofmeat, limb.owner.drop_location());
        newmeat.name = I18n.T("fatty meat");
        newmeat.desc = I18n.T("Extremely fatty tissue taken from a patient.");
        newmeat.subjectname = Dm.text("[]", limb.owner.real_name);
        newmeat.subjectjob = limb.owner.job;
        await ((datum_reagents)newmeat.reagents).add_reagent(typeof(datum_reagent_consumable_nutriment), (removednutriment / Dm.num(Dm.initial(typeof(datum_reagent_consumable_nutriment), (datum_reagent_consumable_nutriment o) => o.nutriment_factor))));
        return null;
    }
}
