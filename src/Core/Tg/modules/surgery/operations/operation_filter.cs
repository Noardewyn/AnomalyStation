// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_filter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// tg: code/modules/surgery/operations/operation_filter.dm:1
public partial class datum_surgery_operation_limb_filter_blood : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_filter.dm:13
    public override object all_required_strings()
    {
        object dot = Dm.list();
        Dm.append(ref dot, I18n.T("operate on chest (target chest)"));
        Dm.append(ref dot, base.all_required_strings());
        Dm.append(ref dot, I18n.T("the patient must not be husked"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_filter.dm:19
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_blood_filter));
    }

    // tg: code/modules/surgery/operations/operation_filter.dm:22
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        return Dm.num(Dm.eq(limb.body_zone, BODY_ZONE_CHEST) && !HAS_TRAIT(limb.owner, TRAIT_HUSK));
    }

    // tg: code/modules/surgery/operations/operation_filter.dm:25
    public override object can_loop(mob_living patient = null, atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        return Dm.num(Dm.truthy(base.can_loop(patient, operating_on, surgeon, tool, operation_args)) && Dm.truthy(has_filterable_chems(limb.owner, tool as obj_item_blood_filter)));
    }

    // tg: code/modules/surgery/operations/operation_filter.dm:28
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        await base.on_preop(operating_on, surgeon, tool, operation_args);
        await display_pain(limb.owner, I18n.T("You feel a throbbing pain in your chest!"));
    }

    // tg: code/modules/surgery/operations/operation_filter.dm:32
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        obj_item_blood_filter bloodfilter = (obj_item_blood_filter)tool;
        foreach (datum_reagent chem in Dm.each_cast<datum_reagent>(((datum_reagents)limb.owner.reagents)?.reagent_list))
        {
            if (!Dm.truthy(length(bloodfilter.whitelist)) || !Dm.@in(chem.type, bloodfilter.whitelist))
                await ((datum_reagents)limb.owner.reagents).remove_reagent(chem.type, clamp(Dm.num(round(Dm.num(chem.volume) * 0.22f, 0.2f)), 0.4f, 10));
        }

        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("[] completes a cycle filtering []'s blood.", tool, limb.owner)),
            span_notice(I18n.T("[] whirrs as it filters []'s blood.", tool, limb.owner)),
            span_notice(I18n.T("[] whirrs as it pumps.", tool)));

        if (Dm.truthy(surgeon.is_holding_item_of_type(typeof(obj_item_healthanalyzer))))
            chemscan(surgeon, limb.owner);
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_filter.dm:51
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_warning(I18n.T("You screw up, bruising []'s chest!", limb.owner)),
            span_warning(I18n.T("[] screws up, bruising []'s chest!", surgeon, limb.owner)),
            span_warning(I18n.T("[] screws up!", surgeon)));
        await limb.receive_damage(5, damage_source: tool);
    }

    // tg: code/modules/surgery/operations/operation_filter.dm:60
    public virtual object has_filterable_chems(mob_living_carbon target = null, obj_item_blood_filter bloodfilter = null)
    {
        if (!Dm.truthy(length(((datum_reagents)target.reagents)?.reagent_list)))
        {
            bloodfilter.audible_message(span_notice(I18n.T("[] pings as it reports no chemicals detected in []'s blood.", bloodfilter, target)));
            playsound(target, Dm.resource("sound/machines/ping.ogg"), 75, TRUE, falloff_exponent: 12, falloff_distance: 1);
            return FALSE;
        }

        if (!Dm.truthy(length(bloodfilter.whitelist)))
            return TRUE;

        foreach (datum_reagent chem in Dm.each_cast<datum_reagent>(((datum_reagents)target.reagents).reagent_list))
        {
            if (Dm.@in(chem.type, bloodfilter.whitelist))
                return TRUE;
        }

        return FALSE;
    }
}
