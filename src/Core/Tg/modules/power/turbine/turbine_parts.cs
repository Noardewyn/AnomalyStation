// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/turbine/turbine_parts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering package E11: the turbine's three parts (compressor, rotor, stator), their tiers and the sheets that raise them.
// The examine is translated (X1). The var blocks are generated.

// tg: code/modules/power/turbine/turbine_parts.dm:7
public partial class obj_item_turbine_parts : obj_item
{
    // tg: code/modules/power/turbine/turbine_parts.dm:2  #define TURBINE_UPGRADE_PART "part" (file-local)
    internal const string TURBINE_UPGRADE_PART = "part";
    // tg: code/modules/power/turbine/turbine_parts.dm:4  #define TURBINE_UPGRADE_AMOUNT "amount" (file-local)
    internal const string TURBINE_UPGRADE_AMOUNT = "amount";

    // tg: code/modules/power/turbine/turbine_parts.dm:16
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("This is a tier [] turbine part, rated for [] rpm and [] K.", current_tier, get_tier_value(TURBINE_MAX_RPM), get_tier_value(TURBINE_MAX_TEMP))));

        DmList required_parts = get_tier_upgrades() as DmList;
        if (Dm.truthy(length(required_parts)))
        {
            Type material = required_parts[TURBINE_UPGRADE_PART] as Type;
            Dm.append(ref dot, span_notice(I18n.T("Can be upgraded with [] [] sheets.", required_parts[TURBINE_UPGRADE_AMOUNT], Dm.initial(material, (obj_item_stack p) => p.name))));
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("Is already at max tier.")));
        return dot;
    }

    /**
     * Returns the max values of various attributes of this turbine based on its tier
     * param - see code/__DEFINES/turbine_defines/.dm
     */
    // tg: code/modules/power/turbine/turbine_parts.dm:31
    public virtual object get_tier_value(object param = null)
    {
        // SHOULD_BE_PURE(TRUE)
        float max_value = 0;
        for (float _ = 1; _ <= current_tier; _++)
        {
            if (Dm.eq(param, TURBINE_MAX_RPM))
            {
                if (!Dm.truthy(max_value))
                    max_value = TURBINE_MAX_BASE_RPM;
                else
                    max_value *= 2.5f;
            }
            else if (Dm.eq(param, TURBINE_MAX_TEMP))
            {
                if (!Dm.truthy(max_value))
                    max_value = 50000;
                else
                    max_value = Dm.pow(max_value, 1.2f);
            }
            else if (Dm.eq(param, TURBINE_MAX_EFFICIENCY))
            {
                if (!Dm.truthy(max_value))
                    max_value = 0.25f;
                else
                    max_value += 0.2f;
            }
        }
        return max_value;
    }

    ///Returns a list containing the typepath & amount of it required to upgrade to the next tier
    // tg: code/modules/power/turbine/turbine_parts.dm:57
    public virtual object get_tier_upgrades()
    {
        // PROTECTED_PROC(TRUE), SHOULD_BE_PURE(TRUE), RETURN_TYPE(/list)
        float sw = current_tier;
        if (sw == TURBINE_PART_TIER_ONE)
            return Dm.list(Dm.kv(TURBINE_UPGRADE_PART, typeof(obj_item_stack_sheet_plasteel)), Dm.kv(TURBINE_UPGRADE_AMOUNT, 10));
        if (sw == TURBINE_PART_TIER_TWO)
            return Dm.list(Dm.kv(TURBINE_UPGRADE_PART, typeof(obj_item_stack_sheet_mineral_titanium)), Dm.kv(TURBINE_UPGRADE_AMOUNT, 10));
        if (sw == TURBINE_PART_TIER_THREE)
            return Dm.list(Dm.kv(TURBINE_UPGRADE_PART, typeof(obj_item_stack_sheet_mineral_metal_hydrogen)), Dm.kv(TURBINE_UPGRADE_AMOUNT, 5));
        return null;
    }

    // tg: code/modules/power/turbine/turbine_parts.dm:70
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = NONE;

        DmList required_parts = get_tier_upgrades() as DmList;
        if (!Dm.truthy(length(required_parts)))
        {
            balloon_alert(user, I18n.T("already at max tier!"));
            return ITEM_INTERACT_FAILURE;
        }

        obj_item_stack_sheet material = tool as obj_item_stack_sheet;
        if (!Dm.istype(material, required_parts[TURBINE_UPGRADE_PART] as Type))
        {
            balloon_alert(user, I18n.T("incorrect part!"));
            return ITEM_INTERACT_FAILURE;
        }

        object amount = required_parts[TURBINE_UPGRADE_AMOUNT];
        if (Dm.num(material.amount) < Dm.num(amount))
        {
            balloon_alert(user, I18n.T("requires [] sheets!", amount));
            return ITEM_INTERACT_FAILURE;
        }

        if (Dm.truthy(await do_after(user, current_tier * SECONDS, this)) && Dm.truthy(await material.use(amount)))
        {
            current_tier += 1;
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }
}

// tg: code/modules/power/turbine/turbine_parts.dm:103
public partial class obj_item_turbine_parts_stator : obj_item_turbine_parts
{
    // tg: code/modules/power/turbine/turbine_parts.dm:107
    public override object get_tier_value(object param = null)
    {
        if (Dm.eq(param, TURBINE_MAX_EFFICIENCY))
        {
            float max_value = 0;
            for (float _ = 1; _ <= current_tier; _++)
            {
                if (!Dm.truthy(max_value))
                    max_value = 0.85f;
                else
                    max_value += 0.015f;
            }
            return max_value;
        }

        return base.get_tier_value(param);
    }

    // tg: code/modules/power/turbine/turbine_parts.dm:119
    public override object get_tier_upgrades()
    {
        float sw = current_tier;
        if (sw == TURBINE_PART_TIER_ONE)
            return Dm.list(Dm.kv(TURBINE_UPGRADE_PART, typeof(obj_item_stack_sheet_mineral_titanium)), Dm.kv(TURBINE_UPGRADE_AMOUNT, 15));
        if (sw == TURBINE_PART_TIER_TWO)
            return Dm.list(Dm.kv(TURBINE_UPGRADE_PART, typeof(obj_item_stack_sheet_mineral_metal_hydrogen)), Dm.kv(TURBINE_UPGRADE_AMOUNT, 15));
        if (sw == TURBINE_PART_TIER_THREE)
            return Dm.list(Dm.kv(TURBINE_UPGRADE_PART, typeof(obj_item_stack_sheet_mineral_zaukerite)), Dm.kv(TURBINE_UPGRADE_AMOUNT, 10));
        return null;
    }
}
