// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_generic_basic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// Some operations that mirror basic carbon state-moving operations but for basic mobs
/// Incision of skin for basic mobs
// tg: code/modules/surgery/operations/operation_generic_basic.dm:3
public partial class datum_surgery_operation_basic_incise_skin : datum_surgery_operation_basic
{
    // tg: code/modules/surgery/operations/operation_generic_basic.dm:24
    public override object get_any_tool()
    {
        return I18n.T("Any sharp edged item");
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:27
    public override object all_blocked_strings()
    {
        return Dm.add(base.all_blocked_strings(), Dm.list(I18n.T("The patient must not have complex anatomy")));
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:30
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "make_incision");
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:33
    public override object state_check(atom_movable operating_on = null)
    {
        mob_living patient = (mob_living)operating_on;
        return Dm.num(!Dm.truthy(patient.has_limbs)); // Only for limbless mobs
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:36
    public override object tool_check(obj_item tool = null)
    {
        // Require edged sharpness OR a tool behavior match
        if (Dm.truthy(Dm.bitand(tool.get_sharpness(), SHARP_EDGED)) || Dm.truthy(implements[tool.tool_behaviour]))
            return TRUE;
        // these are here by popular demand, even though they don't fit the above criteria
        if (istype(tool, typeof(obj_item_pen)) || istype(tool, typeof(obj_item_screwdriver)))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:45
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        mob_living patient = (mob_living)operating_on;
        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You begin to make an incision in []...", patient)),
            span_notice(I18n.T("[] begins to make an incision in [].", surgeon, patient)),
            span_notice(I18n.T("[] begins to make an incision in [].", surgeon, patient)));
        await display_pain(patient, I18n.T("You feel a sharp stabbing sensation!"));
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:55
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        mob_living patient = (mob_living)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        await patient.apply_status_effect(typeof(datum_status_effect_basic_surgery_state), SURGERY_SKIN_OPEN);
        return dot;
    }
}

// tg: code/modules/surgery/operations/operation_generic_basic.dm:59
public partial class datum_surgery_operation_basic_saw_bone : datum_surgery_operation_basic
{
    // tg: code/modules/surgery/operations/operation_generic_basic.dm:87
    public override object get_any_tool()
    {
        return I18n.T("Any sharp edged item with decent force");
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:90
    public override object all_blocked_strings()
    {
        return Dm.add(base.all_blocked_strings(), Dm.list(I18n.T("The patient must not have complex anatomy")));
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:93
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "mend_incision");
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:96
    public override object state_check(atom_movable operating_on = null)
    {
        mob_living patient = (mob_living)operating_on;
        return Dm.num(!Dm.truthy(patient.has_limbs)); // Only for limbless mobs
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:99
    public override object tool_check(obj_item tool = null)
    {
        // Require edged sharpness and sufficient force OR a tool behavior match
        return (Dm.truthy(Dm.bitand(tool.get_sharpness(), SHARP_EDGED)) && Dm.num(tool.force) >= 10) ? TRUE : implements[tool.tool_behaviour];
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:103
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        mob_living patient = (mob_living)operating_on;
        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You begin to saw through []'s bones...", patient)),
            span_notice(I18n.T("[] begins to saw through []'s bones.", surgeon, patient)),
            span_notice(I18n.T("[] begins to saw through []'s bones.", surgeon, patient)));
        await display_pain(patient, I18n.T("You feel a horrid ache spread through your insides!"));
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:113
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        mob_living patient = (mob_living)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        await patient.apply_status_effect(typeof(datum_status_effect_basic_surgery_state), SURGERY_BONE_SAWED);
        await patient.apply_damage(patient.maxHealth * 0.5f, sharpness: Dm.num(((obj_item)tool).get_sharpness()), wound_bonus: CANT_WOUND, attacking_item: tool);
        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You saw [] open.", patient)),
            span_notice(I18n.T("[] saws [] open!", surgeon, patient)),
            span_notice(I18n.T("[] saws [] open!", surgeon, patient)));
        await display_pain(patient, I18n.T("It feels like something just broke!"));
        return dot;
    }
}

// Closing of skin for basic mobs
// tg: code/modules/surgery/operations/operation_generic_basic.dm:127
public partial class datum_surgery_operation_basic_close_skin : datum_surgery_operation_basic
{
    // tg: code/modules/surgery/operations/operation_generic_basic.dm:150
    public override object get_any_tool()
    {
        return I18n.T("Any heat source");
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:153
    public override object all_blocked_strings()
    {
        return Dm.add(base.all_blocked_strings(), Dm.list(I18n.T("The patient must not have complex anatomy")));
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:156
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_cautery));
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:159
    public override object state_check(atom_movable operating_on = null)
    {
        mob_living patient = (mob_living)operating_on;
        return Dm.num(!Dm.truthy(patient.has_limbs)); // Only for limbless mobs
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:162
    public override object tool_check(obj_item tool = null)
    {
        if (istype(tool, typeof(obj_item_stack_medical_suture)))
            return TRUE;

        if (istype(tool, typeof(obj_item_gun_energy_laser)))
        {
            obj_item_gun_energy_laser lasergun = (obj_item_gun_energy_laser)tool;
            return Dm.num(Dm.num(lasergun.cell?.charge) > 0);
        }

        return Dm.num(Dm.num(tool.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST);
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:172
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        mob_living patient = (mob_living)operating_on;
        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You begin to mend the incision in []...", patient)),
            span_notice(I18n.T("[] begins to mend the incision in [].", surgeon, patient)),
            span_notice(I18n.T("[] begins to mend the incision in [].", surgeon, patient)));
        await display_pain(patient, I18n.T("You are being []!", istype(tool, typeof(obj_item_stack_medical_suture)) ? I18n.T("pinched") : I18n.T("burned")));
    }

    // tg: code/modules/surgery/operations/operation_generic_basic.dm:182
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        mob_living patient = (mob_living)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        // Just nuke the status effect, wipe the slate clean
        patient.remove_status_effect(typeof(datum_status_effect_basic_surgery_state));
        return dot;
    }
}
