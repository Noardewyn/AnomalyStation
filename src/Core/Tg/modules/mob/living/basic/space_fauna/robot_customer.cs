// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/robot_customer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the robot tourist that a venue sends through its portal. It takes its kind's sprite, a random tint, a set of
// clothes and a name; it drops grime as it walks, plays its kind's voice when it talks, shows its order to a player who looks at
// it (the food thoughts image) and in its examine, and leaves robot gibs when it dies. The var block is generated.

///Robot customers
// tg: code/modules/mob/living/basic/space_fauna/robot_customer.dm:2
public partial class mob_living_basic_robot_customer : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/robot_customer.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object customer_data = Dm.arg(__extra, 0);
        object attending_venue = Dm.arg(__extra, 1);
        customer_data ??= typeof(datum_customer_data_american);
        attending_venue ??= SSrestaurant.all_venues[typeof(datum_venue_restaurant)];
        datum_customer_data customer_info = SSrestaurant.all_customers[customer_data] as datum_customer_data;
        ai_controller = customer_info.ai_controller_used;

        object dot = base.Initialize(mapload, __extra);

        add_traits(Dm.list(TRAIT_NOMOBSWAP, TRAIT_NO_TELEPORT, TRAIT_STRONG_GRABBER), INNATE_TRAIT); // never suffer a bitch to fuck with you
        // the fifth argument is tg's named sound_vary = TRUE (the element's fourth parameter)
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_OBJ_ROBOT, 1, -6, TRUE));
        if (Dm.truthy(SStts?.tts_enabled))
        {
            // "[customer_data.type]": customer_data holds the kind's typepath here, whose text is the key of tg's robot_voices.json
            voice = Dm.str(pick(strings("robot_voices.json", Dm.text("[]", customer_data), "config")));
        }
        datum_ai_controller controller = (datum_ai_controller)ai_controller;
        controller.set_blackboard_key(BB_CUSTOMER_CUSTOMERINFO, customer_info);
        controller.set_blackboard_key(BB_CUSTOMER_ATTENDING_VENUE, attending_venue);
        controller.set_blackboard_key(BB_CUSTOMER_PATIENCE, customer_info.total_patience);

        icon = customer_info.base_icon;
        icon_state = customer_info.base_icon_state;
        name = Dm.text("[]-bot", pick(customer_info.name_prefixes));
        Waiting.NoSleep(add_atom_colour(rgb(rand(80, 255), rand(80, 255), rand(80, 255)), FIXED_COLOUR_PRIORITY));
        clothes_set = Dm.str(pick(customer_info.clothing_sets));
        update_appearance(UPDATE_ICON);

        grime_carried = prob(2) ? 20 : Dm.num(pick(0, 0, 0, 0, rand(0, 2), rand(0, 3), rand(2, 4), 6));
        return dot;
    }

    ///Clean up on the mobs seat etc when its deleted (Either by murder or because it left)
    // tg: code/modules/mob/living/basic/space_fauna/robot_customer.dm:62
    public override object Destroy(float? force = null)
    {
        datum_ai_controller controller = (datum_ai_controller)ai_controller;
        datum_venue attending_venue = controller.blackboard[BB_CUSTOMER_ATTENDING_VENUE] as datum_venue;
        obj_structure_holosign_robot_seat our_seat = controller.blackboard[BB_CUSTOMER_MY_SEAT] as obj_structure_holosign_robot_seat;
        Dm.remove(attending_venue.current_visitors, this);
        if (Dm.truthy(attending_venue.linked_seats[our_seat]))
            attending_venue.linked_seats[our_seat] = null;
        QDEL_NULL(ref hud_to_show_on_hover);
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/space_fauna/robot_customer.dm:71
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (Dm.truthy(grime_carried) && prob(grime_carried * 20) && !Dm.truthy(throwing) && !Dm.truthy(Dm.bitand(movement_type, MOVETYPES_NOT_TOUCHING_GROUND)) && Dm.truthy(has_gravity()))
        {
            Dm.@new<obj_effect_decal_cleanable_dirt>(old_loc);
            grime_carried--;
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/robot_customer.dm:77
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        await base.MouseEntered(location, control, @params);
        hud_to_show_on_hover?.show_to(usr);
    }

    // tg: code/modules/mob/living/basic/space_fauna/robot_customer.dm:81
    public override void MouseExited(object location = null, object control = null, object @params = null)
    {
        base.MouseExited(location, control, @params);
        hud_to_show_on_hover?.hide_from(usr);
    }

    // tg: code/modules/mob/living/basic/space_fauna/robot_customer.dm:85
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;

        datum_customer_data customer_info = ((datum_ai_controller)ai_controller).blackboard[BB_CUSTOMER_CUSTOMERINFO] as datum_customer_data;

        object new_underlays = customer_info.get_underlays(this);
        if (Dm.truthy(new_underlays))
        {
            underlays.Cut();
            Dm.append(underlays, new_underlays);
        }

        Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_features", icon_state), appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)));
        Dm.append(ref dot, mutable_appearance(icon, clothes_set, appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)));

        object bonus_overlays = customer_info.get_overlays(this);
        if (Dm.truthy(bonus_overlays))
            Dm.append(ref dot, bonus_overlays);
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/robot_customer.dm:102
    public override async DmTask send_speech(object message_raw = null, float? message_range = null, atom_movable source = null, object bubble_type = null, DmList spans = null, object message_language = null, DmList message_mods = null, object forced = null, object tts_message = null, DmList tts_filter = null)
    {
        await base.send_speech(message_raw, message_range, source, bubble_type, spans, message_language, message_mods, forced, tts_message, tts_filter);
        datum_customer_data customer_info = ((datum_ai_controller)ai_controller).blackboard[BB_CUSTOMER_CUSTOMERINFO] as datum_customer_data;
        playsound(this, customer_info.speech_sound, 30, extrarange: MEDIUM_RANGE_SOUND_EXTRARANGE, falloff_distance: 5);
    }

    // tg: code/modules/mob/living/basic/space_fauna/robot_customer.dm:107
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        datum_ai_controller controller = (datum_ai_controller)ai_controller;
        if (controller.blackboard[BB_CUSTOMER_CURRENT_ORDER] == null)
            return dot;

        datum_venue attending_venue = controller.blackboard[BB_CUSTOMER_ATTENDING_VENUE] as datum_venue;
        object wanted_item = controller.blackboard[BB_CUSTOMER_CURRENT_ORDER];
        object order = "nothing";

        if (wanted_item is datum_custom_order)
        {
            datum_custom_order custom_order = (datum_custom_order)wanted_item;
            order = custom_order.get_order_line(attending_venue);
        }
        else
            order = attending_venue.order_food_line(wanted_item);

        Dm.append(ref dot, span_notice(I18n.T("Their order was: \"[].\"", order)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/robot_customer.dm:124
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        Dm.@new<obj_effect_gibspawner_robot>(drop_location());

        return await base.death(gibbed, __extra);
    }
}
