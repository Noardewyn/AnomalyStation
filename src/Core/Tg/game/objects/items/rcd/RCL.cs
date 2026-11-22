// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/rcd/RCL.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 power helpers slice (D164): the whole file. At this snapshot tg's /obj/item/rcl is the rapid PIPE CLEANER layer: it loads
// and lays /obj/item/stack/pipe_cleaner_coil (modules/power/pipecleaners.dm), the decorative wire with no power network, and not
// the power cable. The var block is generated (D111). Its radial wiring menu is tg's persistent radial
// (_onclick/hud/screen_objects/radial_persistent.dm); the two item actions (the colour and the menu) are the generated
// action types. The turf's handleRCL and lay_pipe_cleaner (turf.dm:402, :410) are in game/turfs/turf.cs.

//RAPID PIPE CLEANER

// tg: code/game/objects/items/rcd/RCL.dm:3
[DmPath("/obj/item/rcl")]
public partial class obj_item_rcl : obj_item
{
    // tg: code/game/objects/items/rcd/RCL.dm:28
    public object Initialize__d0(object mapload = null, params object[] __extra) // the earlier definition of two (rules 7.5); the later one is :305
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("wield_callback", Dm.@new<datum_callback>(this, nameof(on_wield))), Dm.named("unwield_callback", Dm.@new<datum_callback>(this, nameof(on_unwield)))));
        return dot;
    }

    /// triggered on wield of two handed item
    // tg: code/game/objects/items/rcd/RCL.dm:34
    public virtual void on_wield(obj_item source = null, mob user = null)
    {
        active = TRUE;
    }

    /// triggered on unwield of two handed item
    // tg: code/game/objects/items/rcd/RCL.dm:38
    public virtual void on_unwield(obj_item source = null, mob user = null)
    {
        active = FALSE;
    }

    // tg: code/game/objects/items/rcd/RCL.dm:41
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(loaded))
            return FALSE;
        if (Dm.truthy(ghetto) && prob(10)) //Is it a ghetto RCL? If so, give it a 10% chance to fall apart
        {
            to_chat(user, span_warning(I18n.T("You attempt to loosen the securing screws on the side, but it falls apart!")));
            while (Dm.num(loaded.amount) > 30) //There are only two kinds of situations: "nodiff" (60,90), or "diff" (31-59, 61-89)
            {
                float diff = Dm.mod(Dm.num(loaded.amount), 30);
                if (diff != 0)
                {
                    await loaded.use(diff);
                    Dm.@new<obj_item_stack_pipe_cleaner_coil>(get_turf(user), diff);
                }
                else
                {
                    await loaded.use(30);
                    Dm.@new<obj_item_stack_pipe_cleaner_coil>(get_turf(user), 30);
                }
            }
            qdel(this);
            return TRUE;
        }

        tool.play_tool_sound(this);
        to_chat(user, span_notice(I18n.T("You loosen the securing screws on the side, allowing you to lower the guiding edge and retrieve the wires.")));
        while (Dm.num(loaded.amount) > 30) //There are only two kinds of situations: "nodiff" (60,90), or "diff" (31-59, 61-89)
        {
            float diff = Dm.mod(Dm.num(loaded.amount), 30);
            if (diff != 0)
            {
                await loaded.use(diff);
                Dm.@new<obj_item_stack_pipe_cleaner_coil>(get_turf(user), diff);
            }
            else
            {
                await loaded.use(30);
                Dm.@new<obj_item_stack_pipe_cleaner_coil>(get_turf(user), 30);
            }
        }
        loaded.max_amount = Dm.num(Dm.initial(loaded, (obj_item_stack s) => s.max_amount));
        if (!Dm.truthy(await user.put_in_hands(loaded)))
            await loaded.forceMove(get_turf(user));

        loaded = null;
        update_appearance();
        return TRUE;
    }

    // tg: code/game/objects/items/rcd/RCL.dm:75
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_stack_pipe_cleaner_coil)))
            return NONE;


        if (!Dm.truthy(loaded))
        {
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                to_chat(user, span_warning(I18n.T("[] is stuck to your hand!", this)));
                return ITEM_INTERACT_BLOCKING;
            }

            loaded = tool as obj_item_stack_pipe_cleaner_coil; //tool.loc is src at this point.
            loaded.set_pipecleaner_color(colors[current_color_index]);
            loaded.max_amount = max_amount; //We store a lot.
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        obj_item_stack_pipe_cleaner_coil loading_coil = tool as obj_item_stack_pipe_cleaner_coil;

        if (Dm.num(loaded.amount) >= max_amount)
            return ITEM_INTERACT_BLOCKING;

        float transfer_amount = min(max_amount - Dm.num(loaded.amount), Dm.num(loading_coil.amount));
        await loading_coil.use(transfer_amount);
        loaded.amount = Dm.num(loaded.amount) + transfer_amount;

        update_appearance();
        to_chat(user, span_notice(I18n.T("You add the pipe cleaners to []. It now contains [].", this, loaded.amount)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/rcd/RCL.dm:104
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(loaded))
            Dm.append(ref dot, span_info(I18n.T("It contains []/[] pipe cleaners.", loaded.amount, max_amount)));
        return dot;
    }

    // tg: code/game/objects/items/rcd/RCL.dm:109
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref loaded);
        last = null;
        listeningTo = null;
        QDEL_NULL(ref wiring_gui_menu);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/rcd/RCL.dm:116
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (!Dm.truthy(loaded))
        {
            icon_state = "rcl-0";
            inhand_icon_state = "rcl-0";
            return dot;
        }
        float amount_now = Dm.num(loaded.amount);
        if (amount_now >= 61) // 61 to INFINITY
        {
            icon_state = "rcl-30";
            inhand_icon_state = "rcl";
        }
        else if (amount_now >= 31 && amount_now <= 60) // 31 to 60
        {
            icon_state = "rcl-20";
            inhand_icon_state = "rcl";
        }
        else if (amount_now >= 1 && amount_now <= 30) // 1 to 30
        {
            icon_state = "rcl-10";
            inhand_icon_state = "rcl";
        }
        else
        {
            icon_state = "rcl-0";
            inhand_icon_state = "rcl-0";
        }
        return dot;
    }

    // tg: code/game/objects/items/rcd/RCL.dm:136
    public virtual object is_empty(mob user = null, float? loud = null)
    {
        loud ??= 1;
        update_appearance();
        if (!Dm.truthy(loaded) || !Dm.truthy(loaded.amount))
        {
            if (Dm.truthy(loud))
                to_chat(user, span_notice(I18n.T("The last of the pipe cleaners unreel from [].", this)));
            if (Dm.truthy(loaded))
            {
                QDEL_NULL(ref loaded);
                loaded = null;
            }
            QDEL_NULL(ref wiring_gui_menu);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/items/rcd/RCL.dm:148
    public override async DmTask<object> pickup(mob user = null)
    {
        await base.pickup(user);
        getMobhook(user);
        return null;
    }

    // tg: code/game/objects/items/rcd/RCL.dm:153  (the parameter is the wearer in tg; the slot's name is user)
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        silent ??= FALSE;
        await base.dropped(user, silent, __extra);
        UnregisterSignal(user, COMSIG_MOVABLE_MOVED);
        listeningTo = null;
        last = null;
        QDEL_NULL(ref wiring_gui_menu);
        return null;
    }

    // tg: code/game/objects/items/rcd/RCL.dm:160
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await base.attack_self(user, modifiers, __extra);
        if (!Dm.truthy(active))
            last = null;
        else if (last == null)
        {
            foreach (obj_structure_pipe_cleaner C in Dm.each<obj_structure_pipe_cleaner>(get_turf(user)))
            {
                if (C.d1 == FALSE || C.d2 == FALSE)
                {
                    last = C;
                    break;
                }
            }
        }
        return null;
    }

    // tg: code/game/objects/items/rcd/RCL.dm:170
    public virtual void getMobhook(mob to_hook = null)
    {
        if (listeningTo == to_hook)
            return;
        if (listeningTo != null)
            UnregisterSignal(listeningTo, COMSIG_MOVABLE_MOVED);
        RegisterSignal(to_hook, COMSIG_MOVABLE_MOVED, nameof(trigger));
        listeningTo = to_hook;
    }

    // tg: code/game/objects/items/rcd/RCL.dm:178
    [SignalHandler]
    public virtual void trigger(mob user = null)
    {
        // SIGNAL_HANDLER
        if (Dm.truthy(active))
            Waiting.NoSleep(layCable(user));
        if (Dm.truthy(wiring_gui_menu)) //update the wire options as you move
            wiringGuiUpdate(user);
    }


    //previous contents of trigger(), lays pipe_cleaner each time the player moves
    // tg: code/game/objects/items/rcd/RCL.dm:188
    public virtual async DmTask layCable(mob user = null)
    {
        if (!isturf(user.loc))
            return;
        if (Dm.truthy(is_empty(user, 0)))
        {
            balloon_alert(user, I18n.T("its empty!"));
            return;
        }

        if (prob(2) && Dm.truthy(ghetto)) //Give ghetto RCLs a 2% chance to jam, requiring it to be reactviated manually.
        {
            balloon_alert(user, I18n.T("wires jam!"));
            active = FALSE;
            return;
        }
        else
        {
            if (last != null)
            {
                if (get_dist(last, user) == 1) //hacky, but it works
                {
                    turf T = get_turf(user);
                    if (!Dm.truthy(T.can_have_cabling()))
                    {
                        last = null;
                        return;
                    }
                    if (Dm.eq(get_dir(last, user), last.d2))
                    {
                        //Did we just walk backwards? Well, that's the one direction we CAN'T complete a stub.
                        last = null;
                        return;
                    }
                    await loaded.pipe_cleaner_join(last, user, FALSE);
                    if (Dm.truthy(is_empty(user)))
                        return; //If we've run out, display message and exit
                }
                else
                    last = null;
            }
            last = await loaded.place_turf(get_turf(this), user, REVERSE_DIR(Dm.num(user.dir))) as obj_structure_pipe_cleaner;
            is_empty(user); //If we've run out, display message
        }
        update_appearance();
    }


    //searches the current tile for a stub pipe_cleaner of the same colour
    // tg: code/game/objects/items/rcd/RCL.dm:221
    public virtual obj_structure_pipe_cleaner findLinkingCable(mob user = null)
    {
        turf T = null;
        if (!isturf(user.loc))
            return null;

        T = get_turf(user);
        if (!Dm.truthy(T.can_have_cabling()))
            return null;

        foreach (obj_structure_pipe_cleaner C in Dm.each<obj_structure_pipe_cleaner>(T))
        {
            if (C == null)
                continue;
            if (!Dm.eq(C.color, GLOB.cable_colors[colors[current_color_index]]))
                continue;
            if (C.d1 == 0)
                return C;
        }
        return null;
    }

    // tg: code/game/objects/items/rcd/RCL.dm:238
    public virtual DmList wiringGuiGenerateChoices(mob user = null)
    {
        float fromdir = 0;
        obj_structure_pipe_cleaner linkingCable = findLinkingCable(user);
        if (linkingCable != null)
            fromdir = linkingCable.d2;

        DmList wiredirs = Dm.list("1", "5", "4", "6", "2", "10", "8", "9");
        foreach (object icondir in Dm.each(wiredirs))
        {
            float dirnum = Dm.num(text2num(icondir));
            string pipe_cleanersuffix = Dm.text("[]-[]", min(fromdir, dirnum), max(fromdir, dirnum));
            if (fromdir == dirnum) //pipe_cleaners can't loop back on themselves
                pipe_cleanersuffix = "invalid";
            image img = Dm.image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: Dm.text("cable_[]", pipe_cleanersuffix));
            img.color = GLOB.cable_colors[colors[current_color_index]];
            wiredirs[icondir] = img;
        }
        return wiredirs;
    }

    // tg: code/game/objects/items/rcd/RCL.dm:255
    public virtual void showWiringGui(mob user = null)
    {
        DmList choices = wiringGuiGenerateChoices(user);

        wiring_gui_menu = show_radial_menu_persistent(user, this, choices, select_proc: Dm.@new<datum_callback>(this, nameof(wiringGuiReact), user), radius: 42);
    }

    // tg: code/game/objects/items/rcd/RCL.dm:260
    public virtual void wiringGuiUpdate(mob user = null)
    {
        if (!Dm.truthy(wiring_gui_menu))
            return;

        DmList choices = wiringGuiGenerateChoices(user);

        wiring_gui_menu.change_choices(choices, FALSE);
    }

    //Callback used to respond to interactions with the wiring menu
    // tg: code/game/objects/items/rcd/RCL.dm:269
    public virtual async DmTask wiringGuiReact(mob_living user = null, object choice = null, params object[] __extra)
    {
        if (!Dm.truthy(choice)) //close on a null choice (the center button)
        {
            QDEL_NULL(ref wiring_gui_menu);
            return;
        }

        choice = text2num(choice);

        if (!isturf(user.loc))
            return;
        if (Dm.truthy(is_empty(user, 0)))
        {
            to_chat(user, span_warning(I18n.T("\\The [] is empty!", this)));
            return;
        }

        turf T = get_turf(user);
        if (!Dm.truthy(T.can_have_cabling()))
            return;

        obj_structure_pipe_cleaner linkingCable = findLinkingCable(user);
        if (linkingCable != null)
        {
            if (!Dm.eq(choice, linkingCable.d2))
            {
                await loaded.pipe_cleaner_join(linkingCable, user, FALSE, choice);
                last = null;
            }
        }
        else
            last = await loaded.place_turf(get_turf(this), user, choice) as obj_structure_pipe_cleaner;

        is_empty(user); //If we've run out, display message

        wiringGuiUpdate(user);
    }

    // tg: code/game/objects/items/rcd/RCL.dm:305
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = Initialize__d0(mapload, __extra);
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/rcd/RCL.dm:309
    public override async DmTask<object> ui_action_click(mob user = null, object action = null)
    {
        if (istype(action, typeof(datum_action_item_action_rcl_col)))
        {
            current_color_index++;
            if (current_color_index > colors.len)
                current_color_index = 1;
            object cwname = colors[current_color_index];
            to_chat(user, Dm.text("Color changed to []!", cwname));
            if (Dm.truthy(loaded))
                loaded.set_pipecleaner_color(colors[current_color_index]);
            if (Dm.truthy(wiring_gui_menu))
                wiringGuiUpdate(user);
        }
        else if (istype(action, typeof(datum_action_item_action_rcl_gui)))
        {
            if (Dm.truthy(wiring_gui_menu)) //The menu is already open, close it
                QDEL_NULL(ref wiring_gui_menu);
            else //open the menu
                showWiringGui(user);
        }
        return null;
    }
}

// tg: code/game/objects/items/rcd/RCL.dm:298  (/obj/item/rcl/pre_loaded)
public partial class obj_item_rcl_pre_loaded : obj_item_rcl
{
    // tg: code/game/objects/items/rcd/RCL.dm:298
    public override object Initialize(object mapload = null, params object[] __extra) //Comes preloaded with pipe_cleaner, for testing stuff
    {
        object dot = base.Initialize(mapload, __extra);
        loaded = Dm.@new<obj_item_stack_pipe_cleaner_coil>();
        loaded.max_amount = max_amount;
        loaded.amount = max_amount;
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/items/rcd/RCL.dm:326
public partial class obj_item_rcl_ghetto : obj_item_rcl
{
    // tg: code/game/objects/items/rcd/RCL.dm:333
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(loaded))
        {
            icon_state = "rclg-0";
            inhand_icon_state = "rclg-0";
            return await base.update_icon_state(__extra);
        }
        if (Dm.num(loaded.amount) >= 1) // 1 to INFINITY
        {
            icon_state = "rclg-1";
            inhand_icon_state = "rcl";
        }
        else
        {
            icon_state = "rclg-1";
            inhand_icon_state = "rclg-1";
        }
        return await base.update_icon_state(__extra);
    }
}
