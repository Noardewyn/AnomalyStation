// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/PDApainter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The screentip context (add_context) is a live-partial stub, as every machine's (D264). The departmental variants are data (their
// var blocks are generated).

/// Basic machine used to paint PDAs and re-trim ID cards.
// tg: code/game/machinery/PDApainter.dm:2
public partial class obj_machinery_pdapainter : obj_machinery
{
    // tg: code/game/machinery/PDApainter.dm:22
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            icon_state = Dm.text("[]-broken", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(powered__proc()) ? null : "-off");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/PDApainter.dm:29
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;

        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return dot;

        if (stored_pda != null || stored_id_card != null)
            Dm.append(ref dot, Dm.text("[]-closed", Dm.initial(this, (obj_machinery_pdapainter p) => p.icon_state)));
        return dot;
    }

    // tg: code/game/machinery/PDApainter.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/game/machinery/PDApainter.dm:40", "Initialize: register_context(): screentip contexts (M10+)");

        if (!Dm.truthy(target_dept))
        {
            // SS?. (rules 10.6): the hand-built test worlds have no SSid_access
            pda_types = SSid_access?.station_pda_templates?.Copy() ?? pda_types;
            card_trims = SSid_access?.station_job_templates?.Copy() ?? card_trims;
            return dot;
        }

        // Cache the manager list, then check through each manager.
        // If we get a region match, add their trim templates and PDA paths to our lists.
        DmList manager_cache = SSid_access?.sub_department_managers_tgui;
        foreach (object access_txt in Dm.each(manager_cache))
        {
            DmList manager_info = manager_cache[access_txt] as DmList;
            DmList manager_regions = manager_info["regions"] as DmList;
            if (Dm.@in(target_dept, manager_regions))
            {
                DmList pda_list = manager_info["pdas"] as DmList;
                DmList trim_list = manager_info["templates"] as DmList;
                Dm.combine(ref pda_types, pda_list);
                Dm.combine(ref card_trims, trim_list);
            }
        }
        return dot;
    }

    // tg: code/game/machinery/PDApainter.dm:59
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref stored_pda);
        QDEL_NULL(ref stored_id_card);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/PDApainter.dm:64
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        // Don't use ejection procs as we're gonna be destroyed anyway, so no need to update icons or anything.
        if (stored_pda != null)
        {
            await stored_pda.forceMove(loc);
            stored_pda = null;
        }
        if (stored_id_card != null)
        {
            await stored_id_card.forceMove(loc);
            stored_id_card = null;
        }
        return null;
    }

    // tg: code/game/machinery/PDApainter.dm:73
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        object sw__1 = severity;
        if (Dm.eq(sw__1, EXPLODE_DEVASTATE))
        {
            if (stored_pda != null)
                Dm.append(ref SSexplosions.high_mov_atom, stored_pda);
            if (stored_id_card != null)
                Dm.append(ref SSexplosions.high_mov_atom, stored_id_card);
        }
        else if (Dm.eq(sw__1, EXPLODE_HEAVY))
        {
            if (stored_pda != null)
                Dm.append(ref SSexplosions.med_mov_atom, stored_pda);
            if (stored_id_card != null)
                Dm.append(ref SSexplosions.med_mov_atom, stored_id_card);
        }
        else if (Dm.eq(sw__1, EXPLODE_LIGHT))
        {
            if (stored_pda != null)
                Dm.append(ref SSexplosions.low_mov_atom, stored_pda);
            if (stored_id_card != null)
                Dm.append(ref SSexplosions.low_mov_atom, stored_id_card);
        }
        await DmTask.CompletedTask;
    }

    // tg: code/game/machinery/PDApainter.dm:91
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == stored_pda)
        {
            stored_pda = null;
            update_appearance(UPDATE_ICON);
        }
        if (gone == stored_id_card)
        {
            stored_id_card = null;
            update_appearance(UPDATE_ICON);
        }
        return dot;
    }

    // tg: code/game/machinery/PDApainter.dm:100  add_context: live-partial (the screentips, D264).

    // tg: code/game/machinery/PDApainter.dm:125
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        if (Dm.truthy(await default_unfasten_wrench(user, tool)))
            power_change();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/PDApainter.dm:131
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN)) && (Dm.num(atom_integrity) >= Dm.num(max_integrity)))
        {
            balloon_alert(user, I18n.T("isn't broken!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
        {
            balloon_alert(user, I18n.T("not enough fuel!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(BROKEN)));
        atom_integrity = max_integrity;
        balloon_alert(user, I18n.T("repaired"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/PDApainter.dm:147
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_modular_computer_pda pda)
            return Dm.truthy(await insert_pda(pda, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
        if (isidcard(tool))
            return Dm.truthy(await insert_id_card(tool as obj_item_card_id, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
        return NONE;
    }

    // tg: code/game/machinery/PDApainter.dm:154
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        if (stored_pda != null)
            await eject_pda(user as mob_living);
        else
            await eject_id_card(user as mob_living);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    /**
     * Insert a PDA into the machine.
     *
     * Will swap PDAs if one is already inside. Attempts to put the PDA into the user's hands if possible.
     * Returns TRUE on success, FALSE otherwise.
     * Arguments:
     * * new_pda - The PDA to insert.
     * * user - The user attempting to insert the PDA.
     */
    // tg: code/game/machinery/PDApainter.dm:173
    public virtual async DmTask<object> insert_pda(obj_item_modular_computer_pda new_pda = null, mob_living user = null)
    {
        if (user != null && !Dm.truthy(await user.transferItemToLoc(new_pda, this, silent: FALSE)))
            return FALSE;
        else
            await new_pda.forceMove(this);

        if (stored_pda != null)
        {
            await eject_pda(user);
            balloon_alert(user, I18n.T("swapped"));
        }

        stored_pda = new_pda;
        new_pda.add_fingerprint(user);
        update_appearance(UPDATE_ICON);
        return TRUE;
    }

    /**
     * Eject the stored PDA into the user's hands if possible, otherwise on the floor.
     *
     * Arguments:
     * * user - The user to try and eject the PDA into the hands of.
     */
    // tg: code/game/machinery/PDApainter.dm:194
    public virtual async DmTask<object> eject_pda(mob_living user = null)
    {
        if (stored_pda == null)
            return null;

        await try_put_in_hand(stored_pda, user);

        stored_pda = null;
        update_appearance(UPDATE_ICON);
        return null;
    }

    /**
     * Insert an ID card into the machine.
     *
     * Will swap ID cards if one is already inside. Attempts to put the card into the user's hands if possible.
     * Returns TRUE on success, FALSE otherwise.
     * Arguments:
     * * new_id_card - The ID card to insert.
     * * user - The user attempting to insert the ID.
     */
    // tg: code/game/machinery/PDApainter.dm:212
    public virtual async DmTask<object> insert_id_card(obj_item_card_id new_id_card = null, mob_living user = null)
    {
        if (!Dm.truthy(new_id_card.trim_changeable))
        {
            balloon_alert(user, I18n.T("rejected!"));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 50, TRUE);
            to_chat(user, span_warning(I18n.T("This ID card does not appear to be compatible with the ID Painter.")));
            return FALSE;
        }

        if (user != null && !Dm.truthy(await user.transferItemToLoc(new_id_card, this, silent: FALSE)))
            return FALSE;
        else
            await new_id_card.forceMove(this);

        if (stored_id_card != null)
        {
            await eject_id_card(user);
            balloon_alert(user, I18n.T("swapped"));
        }

        stored_id_card = new_id_card;
        new_id_card.add_fingerprint(user);
        update_appearance(UPDATE_ICON);
        return TRUE;
    }

    /**
     * Eject the stored ID card into the user's hands if possible, otherwise on the floor.
     *
     * Arguments:
     * * user - The user to try and eject the ID card into the hands of.
     */
    // tg: code/game/machinery/PDApainter.dm:239
    public virtual async DmTask<object> eject_id_card(mob_living user = null)
    {
        if (stored_id_card == null)
            return FALSE;

        GLOB.manifest.modify(stored_id_card.registered_name, stored_id_card.assignment, stored_id_card.get_trim_assignment());
        await try_put_in_hand(stored_id_card, user);

        stored_id_card = null;
        update_appearance(UPDATE_ICON);
        return null;
    }

    // tg: code/game/machinery/PDApainter.dm:249
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "PaintingMachine", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/PDApainter.dm:255
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        if (stored_pda != null)
        {
            data["hasPDA"] = TRUE;
            data["pdaName"] = stored_pda.name;
        }
        else
        {
            data["hasPDA"] = FALSE;
            data["pdaName"] = null;
        }

        if (stored_id_card != null)
        {
            data["hasID"] = TRUE;
            data["idName"] = stored_id_card.name;
        }
        else
        {
            data["hasID"] = FALSE;
            data["idName"] = null;
        }

        return data;
    }

    // tg: code/game/machinery/PDApainter.dm:274
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        data["pdaTypes"] = pda_types;
        data["cardTrims"] = card_trims;

        return data;
    }

    // tg: code/game/machinery/PDApainter.dm:282
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        object sw__1 = action;
        if (Dm.eq(sw__1, "eject_pda"))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
                return TRUE;

            obj_item held_item = usr.get_active_held_item() as obj_item;
            if (held_item is obj_item_modular_computer_pda held_pda)
            {
                // If we successfully inserted, we've ejected the old item. Return early.
                if (Dm.truthy(await insert_pda(held_pda, usr as mob_living)))
                    return TRUE;
            }
            // If we did not successfully insert, try to eject.
            if (stored_pda != null)
            {
                await eject_pda(usr as mob_living);
                return TRUE;
            }

            return TRUE;
        }
        else if (Dm.eq(sw__1, "eject_card"))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
                return TRUE;

            obj_item held_item = usr.get_active_held_item() as obj_item;
            if (isidcard(held_item))
            {
                // If we successfully inserted, we've ejected the old item. Return early.
                if (Dm.truthy(await insert_id_card(held_item as obj_item_card_id, usr as mob_living)))
                    return TRUE;
            }
            // If we did not successfully insert, try to eject.
            if (stored_id_card != null)
            {
                await eject_id_card(usr as mob_living);
                return TRUE;
            }

            return TRUE;
        }
        else if (Dm.eq(sw__1, "trim_pda"))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || stored_pda == null)
                return TRUE;

            object selection = @params["selection"];
            Type pda_path = typeof(obj_item_modular_computer_pda);

            foreach (object path in Dm.each(pda_types))
            {
                if (Dm.eq(pda_types[path], selection))
                {
                    pda_path = path as Type;
                    break;
                }
            }

            object greyscale_config = Dm.initial(pda_path, (obj_item_modular_computer_pda p) => p.greyscale_config);
            object greyscale_colors = Dm.initial(pda_path, (obj_item_modular_computer_pda p) => p.greyscale_colors);
            if (Dm.truthy(greyscale_config) && Dm.truthy(greyscale_colors))
                await stored_pda.set_greyscale(greyscale_colors, greyscale_config);
            else
                stored_pda.icon = Dm.initial(pda_path, (obj_item_modular_computer_pda p) => p.icon);
            object post_init_icon_state = Dm.initial(pda_path, (obj_item_modular_computer_pda p) => p.post_init_icon_state);
            stored_pda.icon_state = (Dm.truthy(post_init_icon_state) ? post_init_icon_state : Dm.initial(pda_path, (obj_item_modular_computer_pda p) => p.icon_state)) as string;
            stored_pda.desc = Dm.initial(pda_path, (obj_item_modular_computer_pda p) => p.desc);

            return TRUE;
        }
        else if (Dm.eq(sw__1, "reset_pda"))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || stored_pda == null)
                return TRUE;

            stored_pda.reset_imprint();
            return TRUE;
        }
        else if (Dm.eq(sw__1, "trim_card"))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || stored_id_card == null)
                return TRUE;

            object selection = @params["selection"];
            foreach (object path in Dm.each(card_trims))
            {
                if (!Dm.eq(card_trims[path], selection))
                    continue;

                if (Dm.truthy(await SSid_access.apply_trim_to_card(stored_id_card, path, copy_access: FALSE)))
                    return TRUE;

                to_chat(usr, span_warning(I18n.T("The trim you selected could not be added to \\the []. You will need a rarer ID card to imprint that trim data.", stored_id_card)));
            }

            return TRUE;
        }
        else if (Dm.eq(sw__1, "reset_card"))
        {
            if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)) || stored_id_card == null)
                return TRUE;

            stored_id_card.clear_account();

            return TRUE;
        }
        return dot;
    }
}
