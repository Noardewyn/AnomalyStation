// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/human_stripping.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8z slice B (D234-D238): the whole file. The var blocks are generated.

public partial class datum_controller_global_vars
{
    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:4  GLOBAL_LIST_INIT(strippable_human_items, create_strippable_list(list(...)))
    public DmList strippable_human_items;

    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:4  (GLOBAL_MANAGED(strippable_human_items, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstrippable_human_items()
    {
        strippable_human_items = create_strippable_list(Dm.list(
            typeof(datum_strippable_item_mob_item_slot_head),
            typeof(datum_strippable_item_mob_item_slot_back),
            typeof(datum_strippable_item_mob_item_slot_mask),
            typeof(datum_strippable_item_mob_item_slot_neck),
            typeof(datum_strippable_item_mob_item_slot_eyes),
            typeof(datum_strippable_item_mob_item_slot_ears),
            typeof(datum_strippable_item_mob_item_slot_jumpsuit),
            typeof(datum_strippable_item_mob_item_slot_suit),
            typeof(datum_strippable_item_mob_item_slot_gloves),
            typeof(datum_strippable_item_mob_item_slot_feet),
            typeof(datum_strippable_item_mob_item_slot_suit_storage),
            typeof(datum_strippable_item_mob_item_slot_id),
            typeof(datum_strippable_item_mob_item_slot_belt),
            typeof(datum_strippable_item_mob_item_slot_pocket_left),
            typeof(datum_strippable_item_mob_item_slot_pocket_right),
            typeof(datum_strippable_item_hand_left),
            typeof(datum_strippable_item_hand_right),
            typeof(datum_strippable_item_mob_item_slot_handcuffs),
            typeof(datum_strippable_item_mob_item_slot_legcuffs))) as DmList;
        Dm.append(ref gvars_datum_init_order, "strippable_human_items");
    }
}

// tg: code/modules/mob/living/carbon/human/human_stripping.dm:26
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:26
    public virtual object should_strip(mob user = null)
    {
        if (user.pulling != this || !Dm.eq(user.grab_state, GRAB_AGGRESSIVE))
            return TRUE;

        if (ishuman(user))
        {
            mob_living_carbon_human human_user = user as mob_living_carbon_human;
            return Dm.truthy(human_user.can_be_firemanned(this)) ? FALSE : TRUE;
        }

        return TRUE;
    }
}

// tg: code/modules/mob/living/carbon/human/human_stripping.dm:44
public partial class datum_strippable_item_mob_item_slot_jumpsuit : datum_strippable_item_mob_item_slot
{
    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:48
    public override object get_alternate_actions(atom source = null, mob user = null, obj_item item = null)
    {
        DmList dot = base.get_alternate_actions(source, user, item) as DmList;
        obj_item_clothing_under jumpsuit = item as obj_item_clothing_under;
        if (jumpsuit == null)
            return dot;

        if (jumpsuit.has_sensor == HAS_SENSORS)
            Dm.append(ref dot, "adjust_sensor");
        if (Dm.truthy(jumpsuit.can_adjust))
            Dm.append(ref dot, "adjust_jumpsuit");
        if (Dm.truthy(length(jumpsuit.attached_accessories)))
            Dm.append(ref dot, "strip_accessory");
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:61
    public override async DmTask<object> perform_alternate_action(atom source = null, mob user = null, object action_key = null, obj_item item = null)
    {
        if (!Dm.truthy(await base.perform_alternate_action(source, user, action_key, item)))
            return null;
        obj_item_clothing_under jumpsuit = item as obj_item_clothing_under;
        if (jumpsuit == null)
            return null;

        if (Dm.eq(action_key, "adjust_jumpsuit"))
            await do_adjust_jumpsuit(source, user, jumpsuit);
        else if (Dm.eq(action_key, "adjust_sensor"))
            await do_adjust_sensor(source, user, jumpsuit);
        else if (Dm.eq(action_key, "strip_accessory"))
            await do_strip_accessory(source, user, jumpsuit);
        else
            stack_trace(Dm.text("Unknown action key: [] for []", action_key, type), "code/modules/mob/living/carbon/human/human_stripping.dm", 74);
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:78
    public virtual async DmTask do_adjust_jumpsuit(atom source = null, mob user = null, obj_item_clothing_under jumpsuit = null)
    {
        to_chat(source, span_notice(I18n.T("[] is trying to adjust your [].", user, jumpsuit)));
        if (!Dm.truthy(await do_after(user, (jumpsuit.strip_delay * 0.5f), source)))
            return;
        to_chat(source, span_notice(I18n.T("[] successfully adjusted your [].", user, jumpsuit)));
        jumpsuit.toggle_jumpsuit_adjust();

        if (!ismob(source))
            return;

        mob mob_source = source as mob;
        await mob_source.update_worn_undersuit();
        await mob_source.update_body();
    }

    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:92
    public virtual async DmTask do_adjust_sensor(atom source = null, mob user = null, obj_item_clothing_under jumpsuit = null)
    {
        if (jumpsuit.has_sensor != HAS_SENSORS)
            return;

        DmList sensor_mode_text_to_num = TgState.Current.datum_strippable_item_mob_item_slot_jumpsuit__do_adjust_sensor__sensor_mode_text_to_num ??= Dm.list(
            Dm.kv("Off", SENSOR_OFF),
            Dm.kv("Living", SENSOR_LIVING),
            Dm.kv("Vitals", SENSOR_VITALS),
            Dm.kv("Tracking", SENSOR_COORDS));
        DmList senor_mode_num_to_text = TgState.Current.datum_strippable_item_mob_item_slot_jumpsuit__do_adjust_sensor__senor_mode_num_to_text ??= Dm.list( // keep this as the inverse of the above list
            Dm.kv(Dm.text("[]", SENSOR_OFF), "Off"),
            Dm.kv(Dm.text("[]", SENSOR_LIVING), "Living"),
            Dm.kv(Dm.text("[]", SENSOR_VITALS), "Vitals"),
            Dm.kv(Dm.text("[]", SENSOR_COORDS), "Tracking"));

        object new_mode_str = await tgui_input_list(user, I18n.T("Adjust suit sensors"), I18n.T("Adjust Sensors"), sensor_mode_text_to_num, senor_mode_num_to_text[Dm.text("[]", jumpsuit.sensor_mode)]);
        object new_mode = sensor_mode_text_to_num[new_mode_str];
        if (new_mode == null) // also catches returning null
            return;

        if (!Dm.truthy(await user.Adjacent(source)))
        {
            source.balloon_alert(user, I18n.T("can't reach!"));
            return;
        }

        to_chat(source, span_notice(I18n.T("[] is trying to adjust your []'s sensor.", user, jumpsuit.name)));
        if (!Dm.truthy(await do_after(user, jumpsuit.strip_delay * 0.5f, source)) || !Dm.truthy(jumpsuit.set_sensor_mode(new_mode))) // takes the same amount of time as adjusting it
        {
            source.balloon_alert(user, I18n.T("failed!"));
            return;
        }
        source.balloon_alert(user, I18n.T("changed sensors"));
        to_chat(source, span_notice(I18n.T("[] successfully adjusted your []'s sensor.", user, jumpsuit.name)));
        user.log_message(Dm.text("changed suit sensors of [] to []", key_name(source), new_mode_str), LOG_ATTACK, color: "red");
        source.log_message(Dm.text("suit sensors changed to [] by []", new_mode_str, key_name(user)), LOG_VICTIM, color: "orange", log_globally: FALSE);
    }

    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:127
    public virtual async DmTask do_strip_accessory(atom source = null, mob user = null, obj_item_clothing_under jumpsuit = null)
    {
        DmList accessory_choices = Dm.list();
        foreach (obj_item_clothing_accessory jumpsuit_accessory in Dm.each_cast<obj_item_clothing_accessory>(jumpsuit.attached_accessories))
        {
            if (!istype(jumpsuit_accessory, typeof(obj_item_clothing_accessory)))
                continue;
            Dm.append_at(accessory_choices, jumpsuit_accessory.name, jumpsuit_accessory);
        }

        object chosen_accessory_name = await tgui_input_list(user, I18n.T("Select which accessory to strip"), I18n.T("Select Accessory"), accessory_choices);
        obj_item_clothing_accessory chosen_accessory = accessory_choices[chosen_accessory_name] as obj_item_clothing_accessory;
        if (chosen_accessory == null)
            return;

        if (!Dm.truthy(await user.Adjacent(source)))
        {
            source.balloon_alert(user, I18n.T("can't reach!"));
            return;
        }

        to_chat(source, span_notice(I18n.T("[] is trying to take [] off of []!", user, chosen_accessory, jumpsuit)));
        if (!Dm.truthy(await do_after(user, chosen_accessory.strip_delay, source)))
        {
            source.balloon_alert(user, I18n.T("failed!"));
            return;
        }

        to_chat(source, span_notice(I18n.T("[] has taken [] off of [].", user, chosen_accessory, jumpsuit)));
        jumpsuit.remove_accessory(chosen_accessory);
        jumpsuit.update_appearance();
        await chosen_accessory.forceMove(jumpsuit.drop_location() as atom);

        if (!ismob(source))
            return;

        mob mob_source = source as mob;
        await mob_source.update_worn_undersuit();
    }
}

// tg: code/modules/mob/living/carbon/human/human_stripping.dm:167
public partial class datum_strippable_item_mob_item_slot_feet : datum_strippable_item_mob_item_slot
{
    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:171
    public override object get_alternate_actions(atom source = null, mob user = null, obj_item item = null)
    {
        DmList dot = base.get_alternate_actions(source, user, item) as DmList;
        obj_item_clothing_shoes shoes = item as obj_item_clothing_shoes;
        if (shoes == null || shoes.fastening_type == SHOES_SLIPON)
            return dot;

        if (shoes.tied == SHOES_UNTIED)
            Dm.append(ref dot, "knot");
        else if (shoes.tied == SHOES_TIED)
            Dm.append(ref dot, "untie");
        else if (shoes.tied == SHOES_KNOTTED)
            Dm.append(ref dot, "unknot");
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:185
    public override async DmTask<object> perform_alternate_action(atom source = null, mob user = null, object action_key = null, obj_item item = null)
    {
        if (!Dm.truthy(await base.perform_alternate_action(source, user, action_key, item)))
            return null;

        obj_item_clothing_shoes shoes = item as obj_item_clothing_shoes;
        if (shoes == null)
            return null;
        if (Dm.eq(action_key, "knot") || Dm.eq(action_key, "untie") || Dm.eq(action_key, "unknot"))
            await shoes.handle_tying(user as mob_living);
        else
            stack_trace(Dm.text("Unknown action key: [] for []", action_key, type), "code/modules/mob/living/carbon/human/human_stripping.dm", 194);
        return null;
    }
}

// tg: code/modules/mob/living/carbon/human/human_stripping.dm:198
public partial class datum_strippable_item_mob_item_slot_suit_storage : datum_strippable_item_mob_item_slot
{
    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:202
    public override object get_alternate_actions(atom source = null, mob user = null, obj_item item = null)
    {
        DmList dot = base.get_alternate_actions(source, user, item) as DmList;
        Dm.append(ref dot, get_strippable_alternate_action_internals(item, source));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:206
    public override async DmTask<object> perform_alternate_action(atom source = null, mob user = null, object action_key = null, obj_item item = null)
    {
        if (!Dm.truthy(await base.perform_alternate_action(source, user, action_key, item)))
            return null;
        if (Dm.@in(action_key, get_strippable_alternate_action_internals(item, source)))
            await strippable_alternate_action_internals(item, source, user);
        return null;
    }
}

// tg: code/modules/mob/living/carbon/human/human_stripping.dm:216
public partial class datum_strippable_item_mob_item_slot_belt : datum_strippable_item_mob_item_slot
{
    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:220
    public override object get_alternate_actions(atom source = null, mob user = null, obj_item item = null)
    {
        DmList dot = base.get_alternate_actions(source, user, item) as DmList;
        Dm.append(ref dot, get_strippable_alternate_action_internals(item, source));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:224
    public override async DmTask<object> perform_alternate_action(atom source = null, mob user = null, object action_key = null, obj_item item = null)
    {
        if (!Dm.truthy(await base.perform_alternate_action(source, user, action_key, item)))
            return null;
        if (Dm.@in(action_key, get_strippable_alternate_action_internals(item, source)))
            await strippable_alternate_action_internals(item, source, user);
        return null;
    }
}

// tg: code/modules/mob/living/carbon/human/human_stripping.dm:230
public partial class datum_strippable_item_mob_item_slot_pocket : datum_strippable_item_mob_item_slot
{
    private const float POCKET_EQUIP_DELAY = 10; // (1 SECONDS), human_stripping.dm:2

    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:234
    public override object get_obscuring(atom source = null)
    {
        return get_item(source) == null
            ? STRIPPABLE_OBSCURING_NONE
            : STRIPPABLE_OBSCURING_HIDDEN;
    }

    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:239
    public override object get_equip_delay(obj_item equipping = null)
    {
        return POCKET_EQUIP_DELAY;
    }

    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:242
    public override async DmTask<object> start_equip(atom source = null, obj_item equipping = null, mob user = null)
    {
        object dot = await base.start_equip(source, equipping, user);
        if (!Dm.truthy(dot))
            warn_owner(source);
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:247
    public override async DmTask<object> start_unequip(atom source = null, mob user = null)
    {
        obj_item item = get_item(source) as obj_item;
        if (item == null)
            return FALSE;

        to_chat(user, span_notice(I18n.T("You try to empty []'s [] pocket.", source, pocket_side)));

        user.log_message(Dm.text("is pickpocketing [] of [] ([])", key_name(source), item, pocket_side), LOG_ATTACK, color: "red");
        source.log_message(Dm.text("is being pickpocketed of [] by [] ([])", item, key_name(user), pocket_side), LOG_VICTIM, color: "orange", log_globally: FALSE);
        item.add_fingerprint(null); // tg passes src (this datum), which is not a mob

        object result = await start_unequip_mob(item, source as mob, user, strip_delay: POCKET_STRIP_DELAY, hidden: TRUE);

        if (!Dm.truthy(result))
            warn_owner(source);

        return result;
    }

    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:265
    public virtual void warn_owner(atom owner = null)
    {
        to_chat(owner, span_warning(I18n.T("You feel your [] pocket being fumbled with!", pocket_side)));
    }
}

public static partial class GlobalProcs
{
    private const float INTERNALS_TOGGLE_DELAY = 40; // (4 SECONDS), human_stripping.dm:1

    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:278
    public static object get_strippable_alternate_action_internals(obj_item item = null, atom source = null)
    {
        if (!iscarbon(source))
            return null;

        mob_living_carbon carbon_source = source as mob_living_carbon;
        if (Dm.truthy(carbon_source.can_breathe_internals()) && istype(item, typeof(obj_item_tank)))
        {
            if (carbon_source.@internal != item)
                return Dm.list("enable_internals");
            else
                return Dm.list("disable_internals");
        }
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:289
    public static async DmTask strippable_alternate_action_internals(obj_item item = null, atom source = null, mob user = null)
    {
        obj_item_tank tank = item as obj_item_tank;
        if (tank == null)
            return;

        mob_living_carbon carbon_source = source as mob_living_carbon;
        if (carbon_source == null)
            return;

        if (!Dm.truthy(carbon_source.can_breathe_internals()))
            return;

        carbon_source.visible_message(
            span_danger(I18n.T("[] tries to [] the valve on []'s [].", user, (carbon_source.@internal != item) ? I18n.T("open") : I18n.T("close"), source, item.name)),
            span_userdanger(I18n.T("[] tries to [] the valve on your [].", user, (carbon_source.@internal != item) ? I18n.T("open") : I18n.T("close"), item.name)),
            ignored_mobs: user);

        to_chat(user, span_notice(I18n.T("You try to [] the valve on []'s []...", (carbon_source.@internal != item) ? I18n.T("open") : I18n.T("close"), source, item.name)));

        if (!Dm.truthy(await do_after(user, INTERNALS_TOGGLE_DELAY, carbon_source)))
            return;

        if (carbon_source.@internal == item)
            await carbon_source.close_internals();
        // This isn't meant to be FALSE, it correlates to the item's name.
        else if (!QDELETED(item))
        {
            if (!Dm.truthy(await carbon_source.try_open_internals(tank)))
                return;
        }

        carbon_source.visible_message(
            span_danger(I18n.T("[] [] the valve on []'s [].", user, carbon_source.@internal == null ? I18n.T("closes") : I18n.T("opens"), source, item.name)),
            span_userdanger(I18n.T("[] [] the valve on your [].", user, carbon_source.@internal == null ? I18n.T("closes") : I18n.T("opens"), item.name)),
            ignored_mobs: user);

        to_chat(user, span_notice(I18n.T("You [] the valve on []'s [].", carbon_source.@internal == null ? I18n.T("close") : I18n.T("open"), source, item.name)));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/carbon/human/human_stripping.dm:93  var/static/list/sensor_mode_text_to_num, senor_mode_num_to_text (proc-level statics, rules 10.6)
    internal DmList datum_strippable_item_mob_item_slot_jumpsuit__do_adjust_sensor__sensor_mode_text_to_num;
    internal DmList datum_strippable_item_mob_item_slot_jumpsuit__do_adjust_sensor__senor_mode_num_to_text;
}
