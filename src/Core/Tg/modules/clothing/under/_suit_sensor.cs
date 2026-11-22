// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/_suit_sensor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///The item representing suit sensors when removed from a suit or dress (obj/item/clothing/under)
// tg: code/modules/clothing/under/_suit_sensor.dm:3
public partial class obj_item_suit_sensor : obj_item
{
    // tg: code/modules/clothing/under/_suit_sensor.dm:22
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(broken))
            dot.Add(span_warning(I18n.T("It's currently broken. You can use a piece of [] to fix it.", EXAMINE_HINT("cable"))));
        else
            dot.Add(span_notice(I18n.T("It's currently set on '[]'.", GLOB.suit_sensor_mode_to_defines.Find(Dm.num(sensor_mode) + 1))));
        return dot;
    }

    // tg: code/modules/clothing/under/_suit_sensor.dm:29
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(broken))
            return dot;
        switch ((int)Dm.num(sensor_mode))
        {
            case SENSOR_LIVING:
                dot.Add("suit_sensor_binary");
                break;
            case SENSOR_VITALS:
                dot.Add("suit_sensor_vitals");
                break;
            case SENSOR_COORDS:
                dot.Add("suit_sensor_tracking");
                break;
        }
        return dot;
    }

    // tg: code/modules/clothing/under/_suit_sensor.dm:41
    public virtual object set_mode(object new_mode = null)
    {
        if (Dm.eq(sensor_mode, new_mode))
            return FALSE;
        sensor_mode = Dm.num(new_mode);
        update_appearance(UPDATE_OVERLAYS);
        return TRUE;
    }

    // tg: code/modules/clothing/under/_suit_sensor.dm:48
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        mob_living living = user as mob_living;
        if (!Dm.truthy(Dm.bitand(living.mobility_flags, MOBILITY_USE)) || !Dm.truthy(await IsReachableBy(user)))
            return FALSE;
        if (Dm.truthy(broken))
        {
            balloon_alert(user, I18n.T("fix it first!"));
            return dot;
        }
        object current_mode_text = GLOB.suit_sensor_mode_to_defines[Dm.num(sensor_mode) + 1];
        object new_mode = await tgui_input_list(user, I18n.T("Select a sensor mode"), I18n.T("Suit Sensors"), GLOB.suit_sensor_mode_to_defines, current_mode_text);
        if (new_mode == null || Dm.truthy(broken) || !Dm.truthy(Dm.bitand(living.mobility_flags, MOBILITY_USE)) || !Dm.truthy(await IsReachableBy(user)))
        {
            user.balloon_alert(user, I18n.T("can't do that now!"));
            return dot;
        }
        set_mode(GLOB.suit_sensor_mode_to_defines[new_mode]);
        balloon_alert(user, I18n.T("sensor set to '[]'", LOWER_TEXT(new_mode)));
        return dot;
    }

    // tg: code/modules/clothing/under/_suit_sensor.dm:62
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)) || Dm.truthy(broken))
            return dot;

        if (Dm.num(severity) <= EMP_HEAVY)
            broken = TRUE;
        else
            set_mode(pick(SENSOR_OFF, SENSOR_OFF, SENSOR_OFF, SENSOR_LIVING, SENSOR_LIVING, SENSOR_VITALS, SENSOR_VITALS, SENSOR_COORDS));
        playsound(this, Dm.resource("sound/effects/sparks/sparks3.ogg"), 75, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, ignore_walls: FALSE);
        return dot;
    }

    // tg: code/modules/clothing/under/_suit_sensor.dm:72
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_stack_cable_coil)
            return await base.item_interaction(user, tool, modifiers, __extra);
        if (!Dm.truthy(broken))
        {
            balloon_alert(user, I18n.T("not broken!"));
            return ITEM_INTERACT_BLOCKING;
        }
        obj_item_stack_cable_coil cabling = tool as obj_item_stack_cable_coil;
        await cabling.use(1);
        balloon_alert(user, I18n.T("suit sensor repaired"));
        broken = FALSE;
        update_appearance(UPDATE_OVERLAYS);
        return ITEM_INTERACT_SUCCESS;
    }
}
