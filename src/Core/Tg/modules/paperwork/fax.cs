// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/fax.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the screentips (add_context: a live-partial row, D264), gaps S3 (D298). A fax sent to CentCom or the Syndicate
// leaves the machine as tg's does; the admins' request manager and their notification of it are Deferred leaves (admin tooling). A
// forced fax to an area without a fax machine drops one in a supply pod (gaps P4d). The var blocks are generated; the type-level static
// type lists are in the TgState part below.

// tg: code/modules/paperwork/fax.dm:1  GLOBAL_VAR_INIT(nt_fax_department, pick(...)), :2 GLOBAL_VAR_INIT(fax_autoprinting, FALSE)
public partial class datum_controller_global_vars
{
    // tg: code/modules/paperwork/fax.dm:1
    public object nt_fax_department;

    // tg: code/modules/paperwork/fax.dm:1  (GLOBAL_MANAGED(nt_fax_department, pick(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnt_fax_department()
    {
        nt_fax_department = pick("NT HR Department", "NT Legal Department", "NT Complaint Department", "NT Customer Relations", "Nanotrasen Tech Support", "NT Internal Affairs Dept");
        Dm.append(ref gvars_datum_init_order, "nt_fax_department");
    }

    // tg: code/modules/paperwork/fax.dm:2
    public object fax_autoprinting;

    // tg: code/modules/paperwork/fax.dm:2  (GLOBAL_MANAGED(fax_autoprinting, FALSE), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfax_autoprinting()
    {
        fax_autoprinting = FALSE;
        Dm.append(ref gvars_datum_init_order, "fax_autoprinting");
    }
}

// tg: code/modules/paperwork/fax.dm:4
public partial class obj_machinery_fax : obj_machinery
{
    /// List of types which should always be allowed to be faxed
    // tg: code/modules/paperwork/fax.dm:38  var/static/list/allowed_types (rules 10.6)
    public static ref DmList allowed_types => ref TgState.Current.obj_machinery_fax__allowed_types;
    /// List of types which should be allowed to be faxed if hacked
    // tg: code/modules/paperwork/fax.dm:46  var/static/list/exotic_types (rules 10.6)
    public static ref DmList exotic_types => ref TgState.Current.obj_machinery_fax__exotic_types;

    // tg: code/modules/paperwork/fax.dm:132
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(fax_id))
            fax_id = assign_random_name();
        if (!Dm.truthy(fax_name))
            fax_name = Dm.text("Unregistered fax []", fax_id);
        set_wires(Dm.@new<datum_wires_fax>(this));
        register_context();
        (special_networks["nanotrasen"] as DmList)["fax_name"] = GLOB.nt_fax_department;
        return dot;
    }

    // tg: code/modules/paperwork/fax.dm:142
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref loaded_item_ref);
        return base.Destroy(force);
    }

    // tg: code/modules/paperwork/fax.dm:146
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, "fax_panel");
        obj_item loaded = loaded_item_ref?.resolve() as obj_item;
        if (loaded != null)
            Dm.append(ref dot, mutable_appearance(icon, find_overlay_state(loaded, "contain") as string));
        return dot;
    }

    // tg: code/modules/paperwork/fax.dm:154
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(jammed))
            Dm.append(ref dot, span_notice(I18n.T("Its output port is jammed and needs cleaning.")));
        return dot;
    }

    // tg: code/modules/paperwork/fax.dm:160
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        if (!Dm.truthy(old_value))
        {
            START_PROCESSING(SSmachines, this);
            return;
        }
        STOP_PROCESSING(SSmachines, this);
    }

    // tg: code/modules/paperwork/fax.dm:166
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (seconds_electrified > MACHINE_NOT_ELECTRIFIED)
            seconds_electrified -= Dm.num(seconds_per_tick);
        return null;
    }

    // tg: code/modules/paperwork/fax.dm:170
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (seconds_electrified != 0 && !Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            if (Dm.truthy(await shock__proc(user as mob_living, 100)))
                return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    /***
     * Emag the device if the panel is open.
     * Emag does not bring you into the syndicate network, but makes it visible to you.
     */
    // tg: code/modules/paperwork/fax.dm:180
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (!Dm.truthy(panel_open) && !Dm.truthy(allow_exotic_faxes))
        {
            balloon_alert(user, I18n.T("open panel first!"));
            return FALSE;
        }
        if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            obj_flags = Dm.bitor(obj_flags, EMAGGED);
            playsound(this, Dm.resource("sound/mobs/non-humanoids/dog/growl2.ogg"), 50, FALSE);
            balloon_alert(user, I18n.T("migrated to syndienet 2.0"));
            to_chat(user, span_warning(I18n.T("An image appears on [] screen for a moment with Ian in the cap of a Syndicate officer.", this)));
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/paperwork/fax.dm:193
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Open and close the wire panel.
     */
    // tg: code/modules/paperwork/fax.dm:201
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    /**
     * Using the multi-tool with the panel closed causes the fax network name to be renamed.
     */
    // tg: code/modules/paperwork/fax.dm:207
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(panel_open))
            return null;
        object new_fax_name = await tgui_input_text(user, I18n.T("Enter a new name for the fax machine."), I18n.T("New Fax Name"), max_length: 128);
        if (!Dm.truthy(new_fax_name))
            return ITEM_INTERACT_SUCCESS;
        if (!Dm.eq(new_fax_name, fax_name))
        {
            if (Dm.truthy(fax_name_exist(new_fax_name)))
            {
                // Being able to set the same name as another fax machine will give a lot of gimmicks for the traitor.
                if (syndicate_network != TRUE && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                {
                    to_chat(user, span_warning(I18n.T("There is already a fax machine with this name on the network.")));
                    return ITEM_INTERACT_SUCCESS;
                }
            }
            user.log_message(Dm.text("renamed [] (fax machine) to [].", fax_name, new_fax_name), LOG_GAME);
            fax_name = new_fax_name;
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/paperwork/fax.dm:223
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return ITEM_INTERACT_SKIP_TO_ATTACK;
        if (Dm.truthy(jammed) && Dm.truthy(await clear_jam(tool, user)))
            return ITEM_INTERACT_SUCCESS;
        if (Dm.truthy(panel_open) && Dm.truthy(is_wire_tool(tool)))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy(can_load_item(tool)))
        {
            if (loaded_item_ref?.resolve() != null)
            {
                balloon_alert(user, I18n.T("item already loaded!"));
                return ITEM_INTERACT_BLOCKING;
            }
            loaded_item_ref = WEAKREF(tool) as datum_weakref;
            await tool.forceMove(this);
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    /**
     * Attempts to clean out a jammed machine using a passed item.
     * Returns true if successful.
     */
    // tg: code/modules/paperwork/fax.dm:245
    public virtual async DmTask<object> clear_jam(obj_item item = null, mob user = null)
    {
        if (istype(item, typeof(obj_item_reagent_containers_spray)))
        {
            obj_item_reagent_containers_spray clean_spray = (obj_item_reagent_containers_spray)item;
            datum_reagents spray_reagents = clean_spray.reagents as datum_reagents;
            if (!Dm.truthy(spray_reagents.has_reagent(typeof(datum_reagent_space_cleaner), Dm.num(clean_spray.amount_per_transfer_from_this))))
                return FALSE;
            await spray_reagents.remove_reagent(typeof(datum_reagent_space_cleaner), clean_spray.amount_per_transfer_from_this);
            playsound(loc as atom, Dm.resource("sound/effects/spray3.ogg"), 50, TRUE, MEDIUM_RANGE_SOUND_EXTRARANGE);
            user.visible_message(span_notice(I18n.T("[] cleans \\the [].", user, this)), span_notice(I18n.T("You clean \\the [].", this)));
            jammed = FALSE;
            return TRUE;
        }
        if (istype(item, typeof(obj_item_soap)) || istype(item, typeof(obj_item_rag)))
        {
            float cleanspeed = 50;
            if (istype(item, typeof(obj_item_soap)))
            {
                obj_item_soap used_soap = (obj_item_soap)item;
                cleanspeed = used_soap.cleanspeed;
            }
            user.visible_message(span_notice(I18n.T("[] starts to clean \\the [].", user, this)), span_notice(I18n.T("You start to clean \\the []...", this)));
            if (Dm.truthy(await do_after(user, cleanspeed, target: this)))
            {
                user.visible_message(span_notice(I18n.T("[] cleans \\the [].", user, this)), span_notice(I18n.T("You clean \\the [].", this)));
                jammed = FALSE;
            }
            return TRUE;
        }
        return FALSE;
    }

    /**
     * Returns true if an item can be loaded into the fax machine.
     */
    // tg: code/modules/paperwork/fax.dm:273
    public virtual object can_load_item(obj_item item = null)
    {
        if (!Dm.truthy(is_allowed_type(item)))
            return FALSE;
        if (!istype(item, typeof(obj_item_stack)))
            return TRUE;
        obj_item_stack stack_item = (obj_item_stack)item;
        return Dm.num(Dm.num(stack_item.amount) == 1);
    }

    /**
     * Returns true if an item is of a type which can currently be loaded into this fax machine.
     * This list expands if you snip a particular wire.
     */
    // tg: code/modules/paperwork/fax.dm:285
    public virtual object is_allowed_type(obj_item item = null)
    {
        DmList checked_list = Dm.truthy(allow_exotic_faxes) ? Dm.bitor(allowed_types, exotic_types) as DmList : allowed_types;
        foreach (atom_movable thing in Dm.each<atom_movable>(item.get_all_contents()))
        {
            if (!Dm.truthy(is_type_in_list(thing, checked_list)))
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/paperwork/fax.dm:292
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "Fax");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/paperwork/fax.dm:298
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        //Record a list of all existing faxes.
        foreach (obj_machinery_fax FAX in Dm.each<obj_machinery_fax>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_fax))))
        {
            if (Dm.eq(FAX.fax_id, fax_id)) //skip yourself
                continue;
            DmList fax_data = Dm.list();
            fax_data["fax_name"] = FAX.fax_name;
            fax_data["fax_id"] = FAX.fax_id;
            fax_data["visible"] = FAX.visible_to_network;
            fax_data["has_paper"] = Dm.num(FAX.loaded_item_ref?.resolve() != null);
            // Hacked doesn't mean on the syndicate network.
            fax_data["syndicate_network"] = FAX.syndicate_network;
            if (data["faxes"] is DmList faxes)
                Dm.append(faxes, Dm.list(fax_data));
            else
                data["faxes"] = Dm.list(fax_data); // DM: null += list(fax_data)
        }

        // Own data
        data["fax_id"] = fax_id;
        data["fax_name"] = fax_name;
        data["visible"] = visible_to_network;
        // In this case, we don't care if the fax is hacked or in the syndicate's network. The main thing is to check the visibility of other faxes.
        data["syndicate_network"] = Dm.num(Dm.truthy(syndicate_network) || Dm.truthy(Dm.bitand(obj_flags, EMAGGED)));
        data["has_paper"] = Dm.num(loaded_item_ref?.resolve() != null);
        data["fax_history"] = fax_history;
        DmList special_networks_data = Dm.list();
        foreach (object key in Dm.each(special_networks))
        {
            if (Dm.eq((special_networks[key] as DmList)["fax_id"], fax_id))
                continue;
            Dm.append(special_networks_data, Dm.list(special_networks[key]));
        }
        data["special_faxes"] = special_networks_data;
        return data;
    }

    // tg: code/modules/paperwork/fax.dm:325
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            // Pulls the paper out of the fax machine
            case "remove":
            {
                obj_item loaded = loaded_item_ref?.resolve() as obj_item;
                if (loaded == null)
                    return dot;
                await loaded.forceMove(drop_location() as atom);
                loaded_item_ref = null;
                playsound(this, Dm.resource("sound/machines/eject.ogg"), 50, FALSE);
                update_appearance();
                return TRUE;
            }

            case "send":
            {
                obj_item loaded = loaded_item_ref?.resolve() as obj_item;
                if (loaded == null)
                    return dot;
                object destination = @params["id"];
                if (Dm.truthy(await send(loaded, destination)))
                {
                    log_fax(loaded, destination, @params["name"]);
                    loaded_item_ref = null;
                    update_appearance();
                    return TRUE;
                }
                break;
            }

            case "send_special":
            {
                obj_item_paper fax_paper = loaded_item_ref?.resolve() as obj_item_paper;
                if (fax_paper == null)
                {
                    to_chat(usr, span_warning(I18n.T("Fax cannot send all above paper on this protected network, sorry."))); // icon2html(src.icon, usr): the chat is text
                    return dot;
                }

                fax_paper.request_state = TRUE;
                Waiting.NoSleep(fax_paper.abstract_move(null)); // fax_paper.loc = null

                Waiting.DetachSync(() => animate_object_travel(fax_paper, "fax_receive", find_overlay_state(fax_paper, "send")));
                playsound(this, Dm.resource("sound/machines/high_tech_confirm.ogg"), 50, vary: FALSE);

                history_add("Send", @params["name"]);

                // GLOB.requests.fax_request(...), the admins' FAX REQUEST message with its links and the comms notification sound: the
                // request manager and the admin tools are not ported
                Dm.Deferred("code/modules/paperwork/fax.dm:366", "ui_act: send_special's request manager entry and the admins' fax request message (admin tooling)");

                if (Dm.truthy(GLOB.fax_autoprinting))
                {
                    foreach (obj_machinery_fax_admin FAX in Dm.each<obj_machinery_fax_admin>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_fax_admin))))
                    {
                        if (!Dm.eq(FAX.fax_id, @params["id"]))
                            continue;
                        await FAX.receive(fax_paper, fax_name);
                        break;
                    }
                }

                log_fax(fax_paper, @params["id"], @params["name"]);
                loaded_item_ref = null;
                update_appearance();
                break;
            }

            case "history_clear":
                history_clear();
                return TRUE;
        }
        return dot;
    }

    /**
     * Records logs of bureacratic action
     * Arguments:
     * * sent - The object being sent
     * * destination_id - The unique ID of the fax machine
     * * name - The friendly name of the fax machine, but these can be spoofed so the ID is also required
     */
    // tg: code/modules/paperwork/fax.dm:396
    public virtual void log_fax(obj_item sent = null, object destination_id = null, object name = null)
    {
        if (istype(sent, typeof(obj_item_paper)))
        {
            obj_item_paper sent_paper = (obj_item_paper)sent;
            log_paper(Dm.text("[] has sent a fax with the message \"[]\" to []/[].", usr, sent_paper.get_raw_text(), name, destination_id));
            return;
        }
        log_game(Dm.text("[] has faxed [] to []/[].]", usr, sent, name, destination_id));
    }

    /**
     * The procedure for sending a paper to another fax machine.
     *
     * The object is called inside /obj/machinery/fax to send the thing to another fax machine.
     * The procedure searches among all faxes for the desired fax ID and calls proc/receive() on that fax.
     * If the item is sent successfully, it returns TRUE.
     * Arguments:
     * * loaded - The object to be sent.
     * * id - The network ID of the fax machine you want to send the item to.
     */
    // tg: code/modules/paperwork/fax.dm:414
    public virtual async DmTask<object> send(obj_item loaded = null, object id = null)
    {
        foreach (obj_machinery_fax FAX in Dm.each<obj_machinery_fax>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_fax))))
        {
            if (!Dm.eq(FAX.fax_id, id))
                continue;
            if (Dm.truthy(FAX.jammed))
            {
                do_sparks(5, TRUE, this);
                balloon_alert(usr, I18n.T("destination port jammed"));
                playsound(this, Dm.resource("sound/machines/scanner/scanbuzz.ogg"), 25, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
                return FALSE;
            }
            await FAX.receive(loaded, fax_name);
            history_add("Send", FAX.fax_name);
            Waiting.DetachSync(() => animate_object_travel(loaded, "fax_receive", find_overlay_state(loaded, "send")));
            playsound(this, Dm.resource("sound/machines/high_tech_confirm.ogg"), 50, FALSE);
            return TRUE;
        }
        return FALSE;
    }

    /**
     * Procedure for accepting papers from another fax machine.
     *
     * The procedure is called in proc/send() of the other fax. It receives a paper-like object and "prints" it.
     * Arguments:
     * * loaded - The object to be printed.
     * * sender_name - The sender's name, which will be displayed in the message and recorded in the history of operations.
     */
    // tg: code/modules/paperwork/fax.dm:439
    public virtual async DmTask receive(obj_item loaded = null, object sender_name = null)
    {
        playsound(this, Dm.resource("sound/machines/printer.ogg"), 50, FALSE);
        Waiting.DetachSync(() => animate_object_travel(loaded, "fax_receive", find_overlay_state(loaded, "receive")));
        await say(I18n.T("Received correspondence from [].", sender_name));
        history_add("Receive", sender_name);
        _addtimer(Dm.@new<datum_callback>(this, nameof(vend_item), loaded), 1.9f * SECONDS, file: "code/modules/paperwork/fax.dm", line: 444);
        if (Dm.truthy(this._listen_lookup?[COMSIG_FAX_MESSAGE_RECEIVED]))
            this._SendSignal(COMSIG_FAX_MESSAGE_RECEIVED, Dm.list(this, sender_name));
        if (Dm.truthy(LAZYLEN(fax_listeners)))
            alert_listeners(sender_name);
    }

    /**
     * Called when a fax is received, iterates through subscribed Faxbonds and notifies them.
     * Arguments:
     * * sender_name - Name of fax sender, used in the PDA message.
    */
    // tg: code/modules/paperwork/fax.dm:454  (set waitfor = FALSE: the body cannot sleep, a plain method)
    public virtual void alert_listeners(object sender_name = null)
    {
        DmList targets = Dm.list();
        foreach (object refstring in Dm.each(fax_listeners?.Copy()))
        {
            datum_weakref app_ref = fax_listeners[refstring] as datum_weakref;
            datum_computer_file_program_faxbond app = app_ref?.resolve() as datum_computer_file_program_faxbond;
            if (app == null || Dm.truthy((app.connected_faxes[fax_id] as DmList)?["muted"]))
                continue;
            datum_computer_file_program_messenger messenger = Dm.locate<datum_computer_file_program_messenger>(app.computer.stored_files);
            if (messenger != null)
                Dm.append(targets, messenger);
        }
        if (!Dm.truthy(length(targets)))
            return;
        datum_signal_subspace_messaging_tablet_message signal = Dm.@new<datum_signal_subspace_messaging_tablet_message>(this, Dm.list(
            Dm.kv("fakename", "Fax Notificator"),
            Dm.kv("fakejob", "PDA Program"),
            Dm.kv("message", Dm.text("Your fax [] has received a new message from []", fax_name, sender_name)),
            Dm.kv("targets", targets),
            Dm.kv("automated", TRUE)));
        signal.send_to_receivers();
    }

    /**
     * Procedure for animating an object entering or leaving the fax machine.
     * Arguments:
     * * item - The object which is travelling.
     * * animation_state - An icon state to apply to the fax machine.
     * * overlay_state - An icon state to apply as an overlay to the fax machine.
     */
    // tg: code/modules/paperwork/fax.dm:481
    public virtual void animate_object_travel(obj_item item = null, object animation_state = null, object overlay_state = null)
    {
        icon_state = animation_state as string;
        mutable_appearance overlay = mutable_appearance(icon, overlay_state as string) as mutable_appearance;
        Dm.append(overlays, overlay);
        _addtimer(Dm.@new<datum_callback>(this, nameof(travel_animation_complete), overlay), 2 * SECONDS, file: "code/modules/paperwork/fax.dm", line: 485);
    }

    /**
     * Called when the travel animation should end. Reset animation and overlay states.
     * Arguments:
     * * remove_overlay - Overlay to remove.
     */
    // tg: code/modules/paperwork/fax.dm:492
    public virtual void travel_animation_complete(mutable_appearance remove_overlay = null)
    {
        icon_state = "fax";
        Dm.remove(overlays, remove_overlay);
    }

    /**
     * Returns an appropriate icon state to represent a passed item.
     * Arguments:
     * * item - Item to interrogate.
     * * state_prefix - Icon state action prefix to mutate.
     */
    // tg: code/modules/paperwork/fax.dm:502
    public virtual object find_overlay_state(obj_item item = null, object state_prefix = null)
    {
        if (istype(item, typeof(obj_item_paper)))
            return Dm.text("[]_paper", state_prefix);
        if (istype(item, typeof(obj_item_photo)))
            return Dm.text("[]_photo", state_prefix);
        if (iscash(item))
            return Dm.text("[]_cash", state_prefix);
        if (istype(item, typeof(obj_item_card)))
            return Dm.text("[]_id", state_prefix);
        if (istype(item, typeof(obj_item_food)))
            return Dm.text("[]_food", state_prefix);
        if (istype(item, typeof(obj_item_throwing_star)))
            return Dm.text("[]_star", state_prefix);
        if (istype(item, typeof(obj_item_tcgcard)))
            return Dm.text("[]_tcg", state_prefix);
        if (istype(item, typeof(obj_item_folder_biscuit)))
            return Dm.text("[]_pbiscuit", state_prefix);
        return Dm.text("[]_paper", state_prefix);
    }

    /**
     * Actually vends an item out of the fax machine.
     * Moved into its own proc to allow a delay for the animation.
     * This will either deposit the item on the fax machine, or throw it if you have hacked a wire.
     * Arguments:
     * * vend - Item to vend from the fax machine.
     */
    // tg: code/modules/paperwork/fax.dm:529
    public virtual async DmTask vend_item(obj_item vend = null)
    {
        await vend.forceMove(drop_location() as atom);
        if (Dm.truthy(hurl_contents))
            await vend.throw_at(get_edge_target_turf(drop_location() as atom, pick(GLOB.alldirs)) as atom, rand(1, 4), EMBED_THROWSPEED_THRESHOLD);
        if (Dm.truthy(is_type_in_list(vend, exotic_types)) && prob(20))
        {
            do_sparks(5, TRUE, this);
            jammed = TRUE;
        }
    }

    /**
     * A procedure that makes entries in the history of fax transactions.
     *
     * Called to record the operation in the fax history list.
     * Records the type of operation, the name of the fax machine with which the operation was performed, and the station time.
     * Arguments:
     * * history_type - Type of operation. By default, "Send" and "Receive" should be used.
     * * history_fax_name - The name of the fax machine that performed the operation.
     */
    // tg: code/modules/paperwork/fax.dm:547
    public virtual void history_add(string history_type = null, object history_fax_name = null)
    {
        history_type ??= "Send";
        DmList history_data = Dm.list();
        history_data["history_type"] = history_type;
        history_data["history_fax_name"] = history_fax_name;
        history_data["history_time"] = round_timestamp();
        if (fax_history == null)
            fax_history = Dm.list(); // DM: null += list(history_data)
        Dm.append(fax_history, Dm.list(history_data));
    }

    /// Clears the history of fax operations.
    // tg: code/modules/paperwork/fax.dm:555
    public virtual void history_clear()
    {
        fax_history = null;
    }

    /**
     * Checks fax names for a match.
     *
     * Called to check the new fax name against the names of other faxes to prevent the use of identical names.
     * Arguments:
     * * new_fax_name - The text of the name to be checked for a match.
     */
    // tg: code/modules/paperwork/fax.dm:565
    public virtual object fax_name_exist(object new_fax_name = null)
    {
        foreach (obj_machinery_fax FAX in Dm.each<obj_machinery_fax>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_fax))))
        {
            if (Dm.eq(FAX.fax_name, new_fax_name))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/paperwork/fax.dm:571
    public override async DmTask<object> shock__proc(mob_living shocking = null, float? chance = null, object shock_source = null, float? siemens_coeff = null)
    {
        chance ??= 100;
        siemens_coeff ??= 1;
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
            return FALSE;
        // tg's isnull(siemens_coeff) check: never true after the default (an explicit null takes the default in DM)
        return await base.shock__proc(shocking, chance, shock_source, siemens_coeff);
    }
}

// tg: code/modules/paperwork/fax.dm:67
public partial class obj_machinery_fax_auto_name : obj_machinery_fax
{
    // tg: code/modules/paperwork/fax.dm:70
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        area current_area = get_area(this) as area;
        name = I18n.T("[]'s Fax Machine", current_area.name);
        fax_name = Dm.text("[]", current_area.name);
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/paperwork/fax.dm:76
public partial class obj_machinery_fax_heads : obj_machinery_fax
{
    // tg: code/modules/paperwork/fax.dm:79  (REGISTER_REQUIRED_MAP_ITEM is a unit test's)
    public override object Initialize(object mapload = null, params object[] __extra) => base.Initialize(mapload, __extra);
}

// tg: code/modules/paperwork/fax.dm:112
public partial class obj_machinery_fax_admin_syndicate : obj_machinery_fax_admin
{
    // tg: code/modules/paperwork/fax.dm:115
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        fax_name = Dm.text("[]", (special_networks["syndicate"] as DmList)["fax_name"]);
        fax_id = (special_networks["syndicate"] as DmList)["fax_id"];
        syndicate_network = TRUE;
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/paperwork/fax.dm:120
public partial class obj_machinery_fax_admin : obj_machinery_fax
{
    // tg: code/modules/paperwork/fax.dm:123
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!Dm.truthy(fax_name))
            fax_name = Dm.text("[]", GLOB.nt_fax_department);
        if (!Dm.truthy(fax_id))
            fax_id = (special_networks["nanotrasen"] as DmList)["fax_id"];
        name = I18n.T("[] Fax Machine", fax_name);
        visible_to_network = FALSE;
        return base.Initialize(mapload, __extra);
    }
}

public static partial class GlobalProcs
{
    /// Sends a fax to a fax machine in an area! fax_area is a type, where all subtypes are also queried. If multiple machines, one is randomly picked
    /// If force is TRUE, we send a droppod with a fax machine and fax the message to that fax machine
    // tg: code/modules/paperwork/fax.dm:633
    public static async DmTask<object> send_fax_to_area(obj_item fax_item = null, object area_type = null, object sender = null, float? force = null, object force_pod_type = null)
    {
        force ??= FALSE;
        object fax_machines = SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_fax));
        DmList valid_fax_machines = Dm.list();

        foreach (obj_machinery fax in Dm.each<obj_machinery>(fax_machines)) //get valid fax machines
        {
            area fax_area = get_area(fax) as area;
            if (istype(fax_area, area_type as Type))
                Dm.append(valid_fax_machines, fax);
        }

        // Pick a fax machine and send the fax
        if (Dm.truthy(length(valid_fax_machines)))
        {
            obj_machinery_fax target_fax = pick(valid_fax_machines) as obj_machinery_fax;
            await target_fax.receive(fax_item, sender);
        }
        else if (Dm.truthy(force)) //no fax machines but we really gotte send? SEND A FAX MACHINE
        {
            var new_fax_machine = Dm.@new<obj_machinery_fax>();
            if (!send_supply_pod_to_area(new_fax_machine, area_type, force_pod_type as Type ?? typeof(obj_structure_closet_supplypod)))
            {
                stack_trace(Dm.text("Attempted to forcibly send a fax to [], however the area does not exist or has no valid dropoff spot for a fax machine", area_type), "code/modules/paperwork/fax.dm", 651);
                return FALSE;
            }
            _addtimer(Dm.@new<datum_callback>(new_fax_machine, nameof(obj_machinery_fax.receive), fax_item, sender), 10 * SECONDS, file: "code/modules/paperwork/fax.dm", line: 653);
        }
        else
            return FALSE;
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/paperwork/fax.dm:38  /obj/machinery/fax's var/static/list/allowed_types
    internal DmList obj_machinery_fax__allowed_types = Dm.list(
        typeof(obj_item_canvas),
        typeof(obj_item_paper),
        typeof(obj_item_photo),
        typeof(obj_item_tcgcard),
        typeof(obj_item_paperplane));

    // tg: code/modules/paperwork/fax.dm:46  /obj/machinery/fax's var/static/list/exotic_types
    internal DmList obj_machinery_fax__exotic_types = Dm.list(
        typeof(obj_item_card),
        typeof(obj_item_folder_biscuit),
        typeof(obj_item_food_breadslice),
        typeof(obj_item_food_chapslice),
        typeof(obj_item_food_cookie),
        typeof(obj_item_food_grilled_chapslice),
        typeof(obj_item_food_pizza_flatbread),
        typeof(obj_item_food_pizzaslice),
        typeof(obj_item_food_root_flatbread),
        typeof(obj_item_food_salami),
        typeof(obj_item_holochip),
        typeof(obj_item_stack_spacecash),
        typeof(obj_item_throwing_star));
}
