// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/rcd/RWD.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The rapid wiring device: cable coils fed in (up to 210 pieces, drawn as its "rwd-10/20/30" fill
// pictures by layer), wielded in both hands it lays cable on the chosen layer (Alt-click: tg's radial of the red, yellow and blue
// coils) on every tile its holder walks over and picks up the stray pieces there; a right click in hand dispenses a coil of a
// chosen length. The var block is generated (D111).

// tg: code/game/objects/items/rcd/RWD.dm:5
public partial class obj_item_rwd : obj_item
{
    // tg: code/game/objects/items/rcd/RWD.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("wield_callback", Dm.@new<datum_callback>(this, nameof(on_wield))), Dm.named("unwield_callback", Dm.@new<datum_callback>(this, nameof(on_unwield)))));
        update_appearance(UPDATE_ICON_STATE);
        return dot;
    }

    // tg: code/game/objects/items/rcd/RWD.dm:46
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        if (!QDELETED(cable))
            QDEL_NULL(ref cable);
        return dot;
    }

    // tg: code/game/objects/items/rcd/RWD.dm:51
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("Dual wield & walk over floors to lay cable."));
        Dm.append(ref dot, I18n.T("It has [] pieces remaining.", current_amount));
        Dm.append(ref dot, I18n.T("Right click on it to dispense a custom amount of cable."));
        Dm.append(ref dot, I18n.T("Alt click to change cable layer."));
        return dot;
    }

    // tg: code/game/objects/items/rcd/RWD.dm:58
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (current_amount >= 61)
        {
            icon_state = Dm.text("rwd-30-layer[]", cable_layer);
            inhand_icon_state = Dm.text("rwd-layer[]", cable_layer);
        }
        else if (current_amount >= 31 && current_amount <= 60)
        {
            icon_state = Dm.text("rwd-20-layer[]", cable_layer);
            inhand_icon_state = Dm.text("rwd-layer[]", cable_layer);
        }
        else if (current_amount >= 1 && current_amount <= 30)
        {
            icon_state = Dm.text("rwd-10-layer[]", cable_layer);
            inhand_icon_state = Dm.text("rwd-layer[]", cable_layer);
        }
        else
        {
            icon_state = "rcl-0";
            inhand_icon_state = "rcl-0";
        }
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/rcd/RWD.dm:74
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        if (current_amount <= 0)
        {
            balloon_alert(user, I18n.T("nothing to dispense!"));
            return null;
        }

        object amount = await tgui_input_number(user: user, message: I18n.T("Enter amount to dispense"), title: I18n.T("Custom cable"), @default: 0, max_value: min(30, current_amount), min_value: min(1, current_amount), timeout: 0, round_value: TRUE);
        if (amount == null || Dm.num(amount) > current_amount)
            return null;

        /**
         * if the user say requested 22 pieces but the cached cable reference has only 5 pieces then it wont be an exact multiple
         * So the while loop runs twice i.e 1st iteration it uses 5 pieces and it has 22-5= 17 pieces left to consume from this device
         * Finally 2nd iteration it creates a new cached cable & consumes the remaining 17 pieces and now the device will use the cached cable containing 30-17 = 30 pieces.
         */
        float amount_to_consume = Dm.num(amount);
        while (Dm.truthy(amount_to_consume))
        {
            obj_item_stack_cable_coil the_cable = get_cable() as obj_item_stack_cable_coil;
            if (!Dm.truthy(the_cable))
                return null;
            float consumed = min(amount_to_consume, Dm.num(the_cable.amount));
            if (!Dm.truthy(await the_cable.use(consumed)))
                return null;
            delta_cable(consumed, decrement: TRUE);
            amount_to_consume -= consumed;
        }

        //spawn the cable. if it merged with the stak below then you pick that up else put it in the user's hand
        obj_item_stack_cable_coil new_cable = Dm.@new<obj_item_stack_cable_coil>(user.drop_location(), amount);
        if (QDELETED(new_cable))
            balloon_alert(user, I18n.T("merged with stack below!"));
        else
            await user.put_in_active_hand(modify_cable(new_cable) as obj_item);

        update_appearance(UPDATE_ICON_STATE);
        return null;
    }

    /// triggered on wield of two handed item
    // tg: code/game/objects/items/rcd/RWD.dm:109
    public virtual void on_wield(obj_item source = null, mob user = null)
    {
        active = TRUE;
    }

    /// triggered on unwield of two handed item
    // tg: code/game/objects/items/rcd/RWD.dm:113
    public virtual void on_unwield(obj_item source = null, mob user = null)
    {
        active = FALSE;
    }

    // tg: code/game/objects/items/rcd/RWD.dm:116
    public override async DmTask<object> pickup(mob user = null)
    {
        mob to_hook = user;
        object dot = await base.pickup(to_hook);
        if (listeningTo == to_hook)
            return dot;
        if (Dm.truthy(listeningTo))
            UnregisterSignal(listeningTo, COMSIG_MOVABLE_MOVED);
        RegisterSignal(to_hook, COMSIG_MOVABLE_MOVED, nameof(on_move));
        listeningTo = to_hook;
        return dot;
    }

    // tg: code/game/objects/items/rcd/RWD.dm:125
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        mob wearer = user;
        object dot = await base.dropped(wearer, silent, __extra);
        UnregisterSignal(wearer, COMSIG_MOVABLE_MOVED);
        listeningTo = null;
        return dot;
    }

    /// for inserting cable into the rwd
    // tg: code/game/objects/items/rcd/RWD.dm:131
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_stack_cable_coil)
            return NONE;
        await add_cable(user, (obj_item_stack_cable_coil)tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/rcd/RWD.dm:137
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(radial_menu))
        {
            radial_menu = Dm.list(
                Dm.kv("Layer 1", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "coil-red")),
                Dm.kv("Layer 2", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "coil-yellow")),
                Dm.kv("Layer 3", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "coil-blue")));
        }

        object layer_result = await show_radial_menu(user, this, radial_menu, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(await check_menu(user as mob_living)))
            return CLICK_ACTION_BLOCKING;
        object sw__1 = layer_result;
        if (Dm.eq(sw__1, "Layer 1"))
            cable_layer = CABLE_LAYER_1;
        else if (Dm.eq(sw__1, "Layer 2"))
            cable_layer = CABLE_LAYER_2;
        else if (Dm.eq(sw__1, "Layer 3"))
            cable_layer = CABLE_LAYER_3;
        update_appearance(UPDATE_ICON_STATE);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/rcd/RWD.dm:158
    public virtual async DmTask<object> check_menu(mob_living user = null)
    {
        if (user is not mob_living)
            return FALSE;
        if (!ISADVANCEDTOOLUSER(user))
        {
            to_chat(user, span_warning(I18n.T("You don't have the dexterity to do this!")));
            return FALSE;
        }
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(await user.Adjacent(this)))
            return FALSE;
        return TRUE;
    }

    /// insert cable into the rwd
    // tg: code/game/objects/items/rcd/RWD.dm:169
    public virtual async DmTask add_cable(mob user = null, obj_item_stack_cable_coil cable = null)
    {
        if (current_amount == max_amount)
        {
            balloon_alert(user, I18n.T("device is full!"));
            return;
        }

        float insert_amount = min(Dm.num(cable.amount), max_amount - current_amount);
        if (!Dm.truthy(await cable.use(insert_amount)))
            return;

        delta_cable(insert_amount, decrement: FALSE);
        update_appearance(UPDATE_ICON_STATE);
        balloon_alert(user, I18n.T("inserted [] cable", insert_amount));
    }

    /// modify cable properties according to its layer
    // tg: code/game/objects/items/rcd/RWD.dm:183
    public virtual object modify_cable(obj_item_stack_cable_coil target_cable = null)
    {
        object sw__1 = cable_layer;
        if (Dm.eq(sw__1, CABLE_LAYER_1))
        {
            target_cable.set_cable_color(CABLE_COLOR_RED);
            target_cable.target_type = typeof(obj_structure_cable_layer1);
            target_cable.target_layer = CABLE_LAYER_1;
        }
        else if (Dm.eq(sw__1, CABLE_LAYER_2))
        {
            target_cable.set_cable_color(CABLE_COLOR_YELLOW);
            target_cable.target_type = typeof(obj_structure_cable);
            target_cable.target_layer = CABLE_LAYER_2;
        }
        else
        {
            target_cable.set_cable_color(CABLE_COLOR_BLUE);
            target_cable.target_type = typeof(obj_structure_cable_layer3);
            target_cable.target_layer = CABLE_LAYER_3;
        }
        return target_cable;
    }

    /// get cached reference of cable which gets used over time
    // tg: code/game/objects/items/rcd/RWD.dm:200
    public virtual object get_cable()
    {
        if (QDELETED(cable))
        {
            float create_amount = min(30, current_amount);
            if (create_amount <= 0)
                return null;
            cable = Dm.@new<obj_item_stack_cable_coil>(this, create_amount);
        }
        return modify_cable(cable);
    }

    /// check if the turf has the same cable layer as this design. If it does don't put cable here
    // tg: code/game/objects/items/rcd/RWD.dm:209
    public virtual object cable_allowed_here(turf the_turf = null)
    {
        // infer our intended cable design from the layer
        Type design_type;
        object sw__1 = cable_layer;
        if (Dm.eq(sw__1, CABLE_LAYER_1))
            design_type = typeof(obj_structure_cable_layer1);
        else if (Dm.eq(sw__1, CABLE_LAYER_2))
            design_type = typeof(obj_structure_cable);
        else
            design_type = typeof(obj_structure_cable_layer3);

        foreach (object thing in Dm.each(the_turf))
        {
            // cable layer on the turf is the same as our intended design layer so nope
            if (thing is obj_structure_cable cable__1 && cable__1.type == design_type)
                return FALSE;
        }

        return TRUE;
    }

    /// extra safe modify just to be sure
    // tg: code/game/objects/items/rcd/RWD.dm:228
    public virtual void delta_cable(object amount = null, object decrement = null)
    {
        if (Dm.truthy(decrement))
            current_amount -= Dm.num(amount);
        else
            current_amount += Dm.num(amount);
        current_amount = clamp(current_amount, 0, max_amount);
    }

    /// stuff to do when moving
    // tg: code/game/objects/items/rcd/RWD.dm:236
    [SignalHandler]
    public virtual void on_move(mob user = null)
    {
        if (!isturf(user.loc))
            return;
        turf the_turf = (turf)user.loc;
        /**
         * Lay cable only if
         * - device is active
         * - the turf can hold cable
         * - there is no cable on the turf or there is cable on the turf but its not the same layer we are gonna put on the turf
         */
        if (Dm.truthy(active) && Dm.truthy(the_turf.can_have_cabling()) && Dm.truthy(the_turf.can_lay_cable()) && Dm.truthy(cable_allowed_here(the_turf)))
        {
            obj_item_stack_cable_coil coil = get_cable() as obj_item_stack_cable_coil;
            if (!Dm.truthy(coil))
                return;

            Waiting.NoSleep(coil.place_turf(the_turf, user));
            delta_cable(1, decrement: TRUE);
            update_appearance(UPDATE_ICON_STATE);
        }

        // pick up any stray cable pieces lying on the floor
        foreach (obj_item_stack_cable_coil cable_piece in Dm.each<obj_item_stack_cable_coil>(the_turf))
            Waiting.NoSleep(add_cable(user, cable_piece));
    }
}
