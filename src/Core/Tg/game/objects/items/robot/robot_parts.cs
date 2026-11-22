// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/robot/robot_parts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326): the cyborg endoskeleton that becomes a cyborg, its window CyborgBootDebug. The var block is
// generated (D111) but the head, declared here.

//The robot bodyparts have been moved to code/module/surgery/bodyparts/robot_bodyparts.dm

// tg: code/game/objects/items/robot/robot_parts.dm:3
public partial class obj_item_robot_suit : obj_item
{
    /// Head part of this endoskeleton (declared here: the generator reads a var named head as a typepath)
    // tg: code/game/objects/items/robot/robot_parts.dm:20
    public obj_item_bodypart_head_robot head;

    // The type's overrides of inherited vars (the Hand mode, types-converter.md 3.2: the class declares head, so the generated
    // part holds only the vars it does not declare)
    // tg: code/game/objects/items/robot/robot_parts.dm:4
    public obj_item_robot_suit()
    {
        name = "cyborg endoskeleton";
        desc = "A complex metal backbone with standard limb sockets and pseudomuscle anchors.";
        icon = Dm.resource("icons/mob/augmentation/augments.dmi");
        icon_state = "robo_suit";
    }

    // tg: code/game/objects/items/robot/robot_parts.dm:8
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_iron), 750));
    }

    // tg: code/game/objects/items/robot/robot_parts.dm:39
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_parts.dm:43
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref l_arm);
        QDEL_NULL(ref r_arm);
        QDEL_NULL(ref l_leg);
        QDEL_NULL(ref r_leg);
        QDEL_NULL(ref chest);
        QDEL_NULL(ref head);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/robot/robot_parts.dm:52
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == l_arm)
            l_arm = null;
        if (gone == r_arm)
            r_arm = null;
        if (gone == l_leg)
            l_leg = null;
        if (gone == r_leg)
            r_leg = null;
        if (gone == chest)
            chest = null;
        if (gone == head)
            head = null;
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_parts.dm:81
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (l_arm != null)
            Dm.append(ref dot, Dm.text("[]+o", Dm.initial(l_arm, "icon_state")));
        if (r_arm != null)
            Dm.append(ref dot, Dm.text("[]+o", Dm.initial(r_arm, "icon_state")));
        if (chest != null)
            Dm.append(ref dot, Dm.text("[]+o", Dm.initial(chest, "icon_state")));
        if (l_leg != null)
            Dm.append(ref dot, Dm.text("[]+o", Dm.initial(l_leg, "icon_state")));
        if (r_leg != null)
            Dm.append(ref dot, Dm.text("[]+o", Dm.initial(r_leg, "icon_state")));
        if (head != null)
            Dm.append(ref dot, Dm.text("[]+o", Dm.initial(head, "icon_state")));
        return dot;
    }

    // tg: code/game/objects/items/robot/robot_parts.dm:96
    public virtual object check_completion()
    {
        if (l_arm != null && r_arm != null && l_leg != null && r_leg != null && head != null && head.flash1 != null && head.flash2 != null && chest != null && Dm.truthy(chest.wired) && chest.cell != null)
        {
            SSblackbox.record_feedback("amount", "cyborg_frames_built", 1);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/items/robot/robot_parts.dm:102
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item I = null) //Deconstucts empty borg shell. Flashes remain unbroken because they haven't been used yet
    {
        object dot = await base.wrench_act(user, I);
        turf T = get_turf(this);
        if (l_leg != null || r_leg != null || chest != null || l_arm != null || r_arm != null || head != null)
        {
            if (Dm.truthy(await I.use_tool(this, user, 5, volume: 50)))
            {
                await drop_all_parts(T);
                to_chat(user, span_notice(I18n.T("You disassemble the cyborg shell.")));
            }
        }
        else
            to_chat(user, span_warning(I18n.T("There is nothing to remove from the endoskeleton!")));
        update_appearance();
        return dot;
    }

    /// Drops all included parts to the passed location
    /// This will also dissassemble the parts being dropped into components as well
    // tg: code/game/objects/items/robot/robot_parts.dm:115
    public virtual async DmTask drop_all_parts(atom drop_to = null)
    {
        drop_to ??= drop_location() as atom;
        if (l_leg != null)
            await l_leg.forceMove(drop_to);
        if (r_leg != null)
            await r_leg.forceMove(drop_to);
        if (l_arm != null)
            await l_arm.forceMove(drop_to);
        if (r_arm != null)
            await r_arm.forceMove(drop_to);

        if (chest != null)
        {
            obj_item_bodypart_chest_robot dropped_chest = chest;
            await dropped_chest.forceMove(drop_to);
            await dropped_chest.drop_organs();
        }

        if (head != null)
        {
            obj_item_bodypart_head_robot dropped_head = head;
            await dropped_head.forceMove(drop_to);
            await dropped_head.drop_organs();
        }
    }

    // tg: code/game/objects/items/robot/robot_parts.dm:129
    public virtual async DmTask<object> put_in_hand_or_drop(mob_living user = null, obj_item I = null) //normal put_in_hands() drops the item ontop of the player, this drops it at the suit's loc
    {
        if (!Dm.truthy(await user.put_in_hands(I)))
        {
            await I.forceMove(drop_location() as atom);
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/game/objects/items/robot/robot_parts.dm:135
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item I = null) //Swaps the power cell if you're holding a new one in your other hand.
    {
        object dot = await base.screwdriver_act(user, I);
        if (Dm.truthy(dot))
            return TRUE;

        if (chest == null) //can't remove a cell if there's no chest to remove it from.
        {
            to_chat(user, span_warning(I18n.T("[] has no attached torso!", this)));
            return dot;
        }

        obj_item_stock_parts_power_store_cell temp_cell = user.is_holding_item_of_type(typeof(obj_item_stock_parts_power_store_cell)) as obj_item_stock_parts_power_store_cell;
        bool swap_failed = false;
        if (temp_cell == null) //if we're not holding a cell
            swap_failed = true;
        else if (!Dm.truthy(await user.transferItemToLoc(temp_cell, chest)))
        {
            swap_failed = true;
            to_chat(user, span_warning(I18n.T("[] is stuck to your hand, you can't put it in []!", temp_cell, this)));
        }

        if (chest.cell != null) //drop the chest's current cell no matter what.
            await put_in_hand_or_drop(user, chest.cell);

        if (swap_failed) //we didn't transfer any new items.
        {
            if (chest.cell != null) //old cell ejected, nothing inserted.
            {
                to_chat(user, span_notice(I18n.T("You remove [] from [].", chest.cell, this)));
                chest.cell = null;
            }
            else
                to_chat(user, span_warning(I18n.T("The power cell slot in []'s torso is empty!", this)));
            return dot;
        }

        to_chat(user, span_notice(chest.cell != null ? I18n.T("You replace []'s [] with [].", this, chest.cell.name, temp_cell) : I18n.T("You insert [] into [].", temp_cell, this)));
        chest.cell = temp_cell;
        return TRUE;
    }

    //ADD <-- what is the purpose of this code comment? is it an abbreviation?
    // tg: code/game/objects/items/robot/robot_parts.dm:168
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stack_sheet_iron iron_sheet)
        {
            if (l_arm != null || r_arm != null || l_leg != null || r_leg != null || chest != null || head != null)
                return ITEM_INTERACT_BLOCKING;
            if (!Dm.truthy(await iron_sheet.use(1)))
            {
                to_chat(user, span_warning(I18n.T("You need one sheet of iron to start building ED-209!")));
                return ITEM_INTERACT_BLOCKING;
            }
            obj_item_bot_assembly_ed209 assembly = Dm.@new<obj_item_bot_assembly_ed209>(drop_location() as atom);
            to_chat(user, span_notice(I18n.T("You arm the robot frame.")));
            object held_index = user.is_holding(this);
            qdel(this);
            if (Dm.truthy(held_index))
                await user.put_in_hand(assembly, held_index);
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_bodypart_leg_left_robot)
        {
            if (l_leg != null)
                return ITEM_INTERACT_BLOCKING;
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            tool.icon_state = Dm.str(Dm.initial(tool, "icon_state"));
            tool.cut_overlays();
            l_leg = tool as obj_item_bodypart_leg_left_robot;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_bodypart_leg_right_robot)
        {
            if (r_leg != null)
                return ITEM_INTERACT_BLOCKING;
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            tool.icon_state = Dm.str(Dm.initial(tool, "icon_state"));
            tool.cut_overlays();
            r_leg = tool as obj_item_bodypart_leg_right_robot;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_bodypart_arm_left_robot)
        {
            if (l_arm != null)
                return ITEM_INTERACT_BLOCKING;
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            tool.icon_state = Dm.str(Dm.initial(tool, "icon_state"));
            tool.cut_overlays();
            l_arm = tool as obj_item_bodypart_arm_left_robot;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_bodypart_arm_right_robot)
        {
            if (r_arm != null)
                return ITEM_INTERACT_BLOCKING;
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            tool.icon_state = Dm.str(Dm.initial(tool, "icon_state")); //in case it is a dismembered robotic limb
            tool.cut_overlays();
            r_arm = tool as obj_item_bodypart_arm_right_robot;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_bodypart_chest_robot new_chestpiece)
        {
            if (chest != null)
                return ITEM_INTERACT_BLOCKING;

            if (!Dm.truthy(new_chestpiece.wired))
            {
                to_chat(user, span_warning(I18n.T("You need to attach wires to it first!")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (new_chestpiece.cell == null)
            {
                to_chat(user, span_warning(I18n.T("You need to attach a cell to it first!")));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(new_chestpiece, this)))
                return ITEM_INTERACT_BLOCKING;

            new_chestpiece.icon_state = Dm.str(Dm.initial(new_chestpiece, "icon_state")); //in case it is a dismembered robotic limb
            new_chestpiece.cut_overlays();
            chest = new_chestpiece;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_bodypart_head_robot new_headpiece)
        {
            if (Dm.locate<obj_item_organ>(new_headpiece) != null)
            {
                to_chat(user, span_warning(I18n.T("There are organs inside []!", new_headpiece)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (head != null)
                return ITEM_INTERACT_BLOCKING;
            if (new_headpiece.flash2 == null || new_headpiece.flash1 == null)
            {
                to_chat(user, span_warning(I18n.T("You need to attach a flash to it first!")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(new_headpiece, this)))
                return ITEM_INTERACT_BLOCKING;
            new_headpiece.icon_state = Dm.str(Dm.initial(new_headpiece, "icon_state")); //in case it is a dismembered robotic limb
            new_headpiece.cut_overlays();
            head = new_headpiece;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_brain_processor potential_brain)
        {
            if (!Dm.truthy(check_completion()))
            {
                to_chat(user, span_warning(I18n.T("The MMI must go in after everything else!")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (chest.cell == null)
            {
                to_chat(user, span_warning(I18n.T("The endoskeleton still needs a power cell!")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!isturf(loc))
            {
                to_chat(user, span_warning(I18n.T("You can't put [] in, the frame has to be standing on the ground to be perfectly precise!", potential_brain)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(potential_brain.brain_check(user)))
                return ITEM_INTERACT_BLOCKING;

            mob_living_brain brainmob = potential_brain.brainmob;
            if (Dm.truthy(is_banned_from(brainmob.ckey, JOB_CYBORG)) || QDELETED(this) || QDELETED(brainmob) || QDELETED(user) || QDELETED(potential_brain) || !Dm.truthy(await Adjacent(user)))
            {
                if (!QDELETED(potential_brain))
                    to_chat(user, span_warning(I18n.T("This [] does not seem to fit!", potential_brain.name)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
                return ITEM_INTERACT_BLOCKING;

            mob_living_silicon_robot_nocell final_product = Dm.@new<mob_living_silicon_robot_nocell>(get_turf(loc), potential_brain.laws, forced_ai, potential_brain.laws == null && Dm.truthy(aisync) ? TRUE : FALSE, potential_brain.laws == null && Dm.truthy(lawsync) ? TRUE : FALSE);
            if (final_product == null)
                return ITEM_INTERACT_BLOCKING;

            final_product.SetInvisibility(INVISIBILITY_NONE);
            //Transfer debug settings to new mob
            final_product.custom_name = created_name;
            final_product.locked = panel_locked;
            if (final_product.connected_ai != null)
                final_product.notify_ai(AI_NOTIFICATION_NEW_BORG);

            if (brainmob.mind != null)
                await brainmob.mind.remove_antags_for_borging();
            final_product.job = JOB_CYBORG;

            final_product.cell = chest.cell;
            await chest.cell.forceMove(final_product);

            await tool.forceMove(final_product); //Should fix cybros run time erroring when blown up. It got deleted before, along with the frame.
            QDEL_NULL(ref final_product.mmi); //we delete the mmi created by robot/New()
            final_product.mmi = potential_brain; //and give the real mmi to the borg.
            await final_product.updatename(brainmob.client);
            // This canonizes that MMI'd cyborgs have memories of their previous life
            brainmob.add_mob_memory(typeof(datum_memory_was_cyborged), Dm.named("protagonist", brainmob.mind), Dm.named("deuteragonist", user));
            await brainmob.mind.transfer_to(final_product);
            playsound(final_product.loc, Dm.resource("sound/mobs/non-humanoids/cyborg/liveagain.ogg"), 75, TRUE);

            if (Dm.truthy(final_product.is_antag()))
            {
                to_chat(final_product, span_userdanger(I18n.T("You have been robotized!")));
                to_chat(final_product, span_danger(I18n.T("You must obey your silicon laws and master AI above all else. Your objectives will consider you to be dead.")));
            }

            SSblackbox.record_feedback("amount", "cyborg_birth", 1);
            await forceMove(final_product);
            final_product.robot_suit = this;

            user.log_message(Dm.text("put the MMI/posibrain of [] into a cyborg shell", key_name(brainmob)), LOG_GAME);
            brainmob.log_message(Dm.text("was put into a cyborg shell by []", key_name(user)), LOG_GAME, log_globally: FALSE);

            if (!Dm.truthy(locomotion))
            {
                await final_product.set_lockcharge(TRUE);
                to_chat(final_product, span_warning(I18n.T("Error: Servo motors unresponsive.")));
            }
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_borg_upgrade_ai boris_module)
        {
            if (!Dm.truthy(check_completion()))
                return ITEM_INTERACT_BLOCKING;
            if (!isturf(loc))
            {
                to_chat(user, span_warning(I18n.T("You cannot install [], the frame has to be standing on the ground to be perfectly precise!", boris_module)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(boris_module)))
            {
                to_chat(user, span_warning(I18n.T("[] is stuck to your hand!", boris_module)));
                return ITEM_INTERACT_BLOCKING;
            }
            qdel(boris_module);
            mob_living_silicon_robot_shell final_product = Dm.@new<mob_living_silicon_robot_shell>(get_turf(this), null, forced_ai, aisync, lawsync);
            if (final_product == null)
                return ITEM_INTERACT_BLOCKING;

            if (final_product.connected_ai != null)
                final_product.notify_ai(AI_NOTIFICATION_AI_SHELL);

            final_product.cell = chest.cell;
            await chest.cell.forceMove(final_product);

            final_product.locked = panel_locked;
            final_product.job = JOB_CYBORG;
            await forceMove(final_product);
            final_product.robot_suit = this;
            if (!Dm.truthy(locomotion))
                await final_product.set_lockcharge(TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        if (IS_WRITING_UTENSIL(tool))
        {
            to_chat(user, span_warning(I18n.T("You need to use a multitool to name []!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        return NONE;
    }

    // tg: code/game/objects/items/robot/robot_parts.dm:368
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(check_completion()))
        {
            to_chat(user, span_warning(I18n.T("The endoskeleton must be assembled before debugging can begin!")));
            return ITEM_INTERACT_SKIP_TO_ATTACK;
        }
        await ui_interact(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/robot/robot_parts.dm:375
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (isobserver(user))
            return await base.ui_status(user, state);
        obj_item held_item = user.get_active_held_item() as obj_item;
        if (Dm.eq(held_item?.tool_behaviour, TOOL_MULTITOOL))
            return await base.ui_status(user, state);
        to_chat(user, span_warning(I18n.T("You need a multitool to access debug settings!")));
        return UI_CLOSE;
    }

    // tg: code/game/objects/items/robot/robot_parts.dm:384
    public override object ui_state(mob user = null)
    {
        return GLOB.physical_state;
    }

    // tg: code/game/objects/items/robot/robot_parts.dm:387
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "CyborgBootDebug", "Cyborg Boot Debug");
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/robot/robot_parts.dm:393
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["designation"] = created_name;
        data["locomotion"] = locomotion;
        data["panel"] = panel_locked;
        data["aisync"] = aisync;
        data["master"] = forced_ai_name;
        data["lawsync"] = lawsync;
        return data;
    }

    // tg: code/game/objects/items/robot/robot_parts.dm:403
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob_living user = usr as mob_living;

        switch (action as string)
        {
            case "rename":
                object new_name = reject_bad_name(html_encode(@params["new_name"]), TRUE);
                if (!Dm.truthy(new_name))
                {
                    created_name = "";
                    return dot;
                }
                created_name = Dm.str(new_name);
                log_silicon(Dm.text("[] has set \"[]\" as a cyborg shell name at []", key_name(user), new_name, loc_name(user)));
                return TRUE;
            case "locomotion":
                locomotion = Dm.not(locomotion);
                log_silicon(Dm.text("[] has [] movement on a cyborg shell at []", key_name(user), Dm.truthy(locomotion) ? "enabled" : "disabled", loc_name(user)));
                return TRUE;
            case "panel":
                panel_locked = Dm.not(panel_locked);
                log_silicon(Dm.text("[] has [] the panel on a cyborg shell at []", key_name(user), Dm.truthy(panel_locked) ? "locked" : "unlocked", loc_name(user)));
                return TRUE;
            case "aisync":
                aisync = Dm.not(aisync);
                log_silicon(Dm.text("[] has [] the AI sync for a cyborg shell at []", key_name(user), Dm.truthy(aisync) ? "enabled" : "disabled", loc_name(user)));
                return TRUE;
            case "set_ai":
                if (length(active_ais(check_mind: FALSE, z: z)) <= 0)
                {
                    to_chat(user, span_alert(I18n.T("No active AIs detected.")));
                    return dot;
                }

                object selected_ai = await select_active_ai(user, z); // this one runs input()
                if (!in_range(this, user) && loc != user)
                    return dot;
                if (!Dm.truthy(selected_ai)) // null = clear
                {
                    clear_forced_ai();
                    return TRUE;
                }
                if (forced_ai == selected_ai) // same AI = clear
                {
                    clear_forced_ai();
                    to_chat(user, span_notice(I18n.T("You reset []'s AI setting.", this)));
                    return TRUE;
                }

                set_forced_ai(selected_ai as mob_living_silicon_ai);
                to_chat(user, span_notice(I18n.T("You set []'s AI setting to [].", this, forced_ai_name)));
                log_silicon(Dm.text("[] set the default AI for a cyborg shell to [] at []", key_name(user), key_name(selected_ai), loc_name(user)));
                return TRUE;

            case "lawsync":
                lawsync = Dm.not(lawsync);
                log_silicon(Dm.text("[] has [] the law sync for a cyborg shell at []", key_name(user), Dm.truthy(lawsync) ? "enabled" : "disabled", loc_name(user)));
                return TRUE;
        }
        return dot;
    }

    /// Sets [forced_ai] and [forced_ai_name] to the passed AI
    // tg: code/game/objects/items/robot/robot_parts.dm:458
    public virtual void set_forced_ai(mob_living_silicon_ai ai = null)
    {
        forced_ai = ai;
        forced_ai_name = ai.name;
        RegisterSignal(ai, COMSIG_QDELETING, nameof(ai_die));
    }

    /// Clears [forced_ai] and [forced_ai_name]
    // tg: code/game/objects/items/robot/robot_parts.dm:464
    public virtual void clear_forced_ai()
    {
        if (forced_ai != null)
        {
            UnregisterSignal(forced_ai, COMSIG_QDELETING);
            forced_ai = null;
        }
        forced_ai_name = null;
    }

    /// Clears the forced_ai ref
    // tg: code/game/objects/items/robot/robot_parts.dm:471
    [SignalHandler]
    public virtual void ai_die(datum source = null)
    {
        // Does not use [proc/clear_forced_ai] because we'd like to keep the AI name tracked for metagaming purposes
        UnregisterSignal(forced_ai, COMSIG_QDELETING);
        forced_ai = null;
    }
}

// tg: code/game/objects/items/robot/robot_parts.dm:67
public partial class obj_item_robot_suit_prebuilt : obj_item_robot_suit
{
    // tg: code/game/objects/items/robot/robot_parts.dm:67
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        l_arm = Dm.@new<obj_item_bodypart_arm_left_robot>(this);
        r_arm = Dm.@new<obj_item_bodypart_arm_right_robot>(this);
        l_leg = Dm.@new<obj_item_bodypart_leg_left_robot>(this);
        r_leg = Dm.@new<obj_item_bodypart_leg_right_robot>(this);
        head = Dm.@new<obj_item_bodypart_head_robot>(this);
        head.flash1 = Dm.@new<obj_item_assembly_flash_handheld>(head);
        head.flash2 = Dm.@new<obj_item_assembly_flash_handheld>(head);
        chest = Dm.@new<obj_item_bodypart_chest_robot>(this);
        chest.wired = TRUE;
        chest.cell = Dm.@new<obj_item_stock_parts_power_store_cell_high>(chest);
        update_appearance();
        return dot;
    }
}
