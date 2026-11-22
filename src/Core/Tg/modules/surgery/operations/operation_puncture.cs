// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_puncture.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// tg: code/modules/surgery/operations/operation_puncture.dm:1
public partial class datum_surgery_operation_limb_repair_puncture : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_puncture.dm:14
    public override object get_time_modifiers(atom_movable operating_on = null, mob_living surgeon = null, object tool = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        float dot = Dm.num(base.get_time_modifiers(operating_on, surgeon, tool));
        foreach (datum_wound_pierce_bleed pierce_wound in Dm.each<datum_wound_pierce_bleed>(limb.wounds))
        {
            if (HAS_TRAIT(pierce_wound, TRAIT_WOUND_SCANNED))
                dot *= 0.5f;
        }
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_puncture.dm:20
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_hemostat));
    }

    // tg: code/modules/surgery/operations/operation_puncture.dm:23
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("the limb must have an unoperated puncture wound")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_puncture.dm:26
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        datum_wound_pierce_bleed pierce_wound = Dm.locate<datum_wound_pierce_bleed>(limb.wounds);
        if (pierce_wound == null || Dm.num(pierce_wound.blood_flow) <= 0 || Dm.truthy(pierce_wound.mend_state))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_puncture.dm:32
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to realign the torn blood vessels in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to realign the torn blood vessels in [] with [].", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
            span_notice(I18n.T("[] begins to realign the torn blood vessels in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a horrible stabbing pain in your []!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_puncture.dm:42
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        datum_wound_pierce_bleed pierce_wound = Dm.locate<datum_wound_pierce_bleed>(limb.wounds);
        if (pierce_wound != null)
            await pierce_wound.adjust_blood_flow(-0.25f);
        await limb.receive_damage(3, wound_bonus: CANT_WOUND, sharpness: Dm.num(((obj_item)tool).get_sharpness()), damage_source: tool);

        if (QDELETED(pierce_wound))
        {
            display_results(
                surgeon,
                limb.owner,
                span_notice(I18n.T("You successfully realign the last of the torn blood vessels in [].", FORMAT_LIMB_OWNER(limb))),
                span_notice(I18n.T("[] successfully realigns the last of the torn blood vessels in [] with []!", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
                span_notice(I18n.T("[] successfully realigns the last of the torn blood vessels in  []!", surgeon, FORMAT_LIMB_OWNER(limb))));
            return null;
        }

        if (pierce_wound != null)
            pierce_wound.mend_state = TRUE;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You successfully realign some of the blood vessels in [].", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] successfully realigns some of the blood vessels in [] with []!", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
            span_notice(I18n.T("[] successfully realigns some of the blood vessels in  []!", surgeon, FORMAT_LIMB_OWNER(limb))));
        return null;
    }

    // tg: code/modules/surgery/operations/operation_puncture.dm:65
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You jerk apart some of the blood vessels in [].", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] jerks apart some of the blood vessels in [] with []!", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
            span_notice(I18n.T("[] jerks apart some of the blood vessels in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await limb.receive_damage(rand(4, 8), wound_bonus: 10, sharpness: SHARP_EDGED, damage_source: tool);
    }
}

// tg: code/modules/surgery/operations/operation_puncture.dm:75
public partial class datum_surgery_operation_limb_seal_veins : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_puncture.dm:90
    public override object get_time_modifiers(atom_movable operating_on = null, mob_living surgeon = null, object tool = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        float dot = Dm.num(base.get_time_modifiers(operating_on, surgeon, tool));
        foreach (datum_wound_pierce_bleed pierce_wound in Dm.each<datum_wound_pierce_bleed>(limb.wounds))
        {
            if (HAS_TRAIT(pierce_wound, TRAIT_WOUND_SCANNED))
                dot *= 0.5f;
        }
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_puncture.dm:96
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_cautery));
    }

    // tg: code/modules/surgery/operations/operation_puncture.dm:99
    public override object get_any_tool()
    {
        return I18n.T("Any heat source");
    }

    // tg: code/modules/surgery/operations/operation_puncture.dm:102
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("the limb must have an operated puncture wound")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_puncture.dm:105
    public override object tool_check(obj_item tool = null)
    {
        if (istype(tool, typeof(obj_item_gun_energy_laser)))
        {
            obj_item_gun_energy_laser lasergun = (obj_item_gun_energy_laser)tool;
            return Dm.num(Dm.num(lasergun.cell?.charge) > 0);
        }

        return Dm.num(Dm.num(tool.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST);
    }

    // tg: code/modules/surgery/operations/operation_puncture.dm:112
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        datum_wound_pierce_bleed pierce_wound = Dm.locate<datum_wound_pierce_bleed>(limb.wounds);
        if (pierce_wound == null || Dm.num(pierce_wound.blood_flow) <= 0 || !Dm.truthy(pierce_wound.mend_state))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_puncture.dm:118
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to seal the realigned blood vessels in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to seal the realigned blood vessels in [] with [].", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
            span_notice(I18n.T("[] begins to seal the realigned blood vessels in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a burning sensation in your []!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_puncture.dm:128
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        datum_wound_pierce_bleed pierce_wound = Dm.locate<datum_wound_pierce_bleed>(limb.wounds);
        if (pierce_wound != null)
            await pierce_wound.adjust_blood_flow(-0.5f);

        if (QDELETED(pierce_wound))
        {
            display_results(
                surgeon,
                limb.owner,
                span_notice(I18n.T("You successfully seal the last of the ruptured blood vessels in [].", FORMAT_LIMB_OWNER(limb))),
                span_notice(I18n.T("[] successfully seals the last of the ruptured blood vessels in [] with []!", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
                span_notice(I18n.T("[] successfully seals the last of the ruptured blood vessels in  []!", surgeon, FORMAT_LIMB_OWNER(limb))));
            return null;
        }

        if (pierce_wound != null)
            pierce_wound.mend_state = FALSE;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You successfully seal some of the blood vessels in [].", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] successfully seals some of the blood vessels in [] with []!", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
            span_notice(I18n.T("[] successfully seals some of the blood vessels in  []!", surgeon, FORMAT_LIMB_OWNER(limb))));
        return null;
    }
}
