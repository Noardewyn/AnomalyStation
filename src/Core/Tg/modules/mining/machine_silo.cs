// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/machine_silo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2a (D304): the whole file but the context tips (deferred by the owner's rule of 2026-10-29; register_context is
// called as tg does). The var blocks are generated (D111).

// tg: code/modules/mining/machine_silo.dm:27
public partial class obj_machinery_ore_silo : obj_machinery
{
    ///The machine's internal radio, used to broadcast alerts.
    // tg: code/modules/mining/machine_silo.dm:47  (hand-written: the V3 rule would type it a typepath, types-converter.md 3.5)
    public obj_item_radio radio;

    // The type's overrides of inherited vars (the Hand mode, types-converter.md 3.2: the class declares radio)
    // tg: code/modules/mining/machine_silo.dm:27
    public obj_machinery_ore_silo()
    {
        name = "ore silo";
        desc = "An all-in-one bluespace storage and transmission system for the station's mineral distribution needs.";
        icon = Dm.resource("icons/obj/machines/ore_silo.dmi");
        icon_state = "silo";
        density = 1;
        circuit = typeof(obj_item_circuitboard_machine_ore_silo);
        interaction_flags_machine = 28;
        processing_flags = 0;
    }

    // Always announce this action
    private const int ALWAYS_ANNOUNCE = ALL; // tg: code/modules/mining/machine_silo.dm:2
    // Announced when someone tries to ban someone without QM access
    private const int BAN_ATTEMPT_FAILURE_NO_ACCESS = 1 << 1;
    // Announced when someone tries to ban someone with QM access without being the Captain
    private const int BAN_ATTEMPT_FAILURE_CHALLENGING_DA_CHIEF = 1 << 2;
    // Announced when a silicon tries to ban someone
    private const int BAN_ATTEMPT_FAILURE_SOULLESS_MACHINE = 1 << 3;
    // Announced when a user is banned from the ore silo
    private const int BAN_CONFIRMATION = 1 << 4;
    // Announced when a user is unbanned from the ore silo
    private const int UNBAN_CONFIRMATION = 1 << 5;
    // Announced when a suspicious(chameleon ID worn by user) log is Among the ore silo logs and someone tries to ban them
    private const int FAILED_OPERATION_SUSPICIOUS = 1 << 6;
    // Announced when a user tries to ban someone without a bank account ID
    private const int FAILED_OPERATION_NO_BANK_ID = 1 << 7;
    // Announced when a user tries to unrestrict the ore silo without QM access
    private const int UNRESTRICT_FAILURE_NO_ACCESS = 1 << 8;
    // Announced when a silicon tries to unrestrict the ore silo
    private const int UNRESTRICT_FAILURE_SOULLESS_MACHINE = 1 << 9;
    // Announced when a user removes the worn ID(with valid bank account) requirement from the ore silo
    private const int UNRESTRICT_CONFIRMATION = 1 << 10;
    // Announced when a user restricts the ore silo to require a valid ID with bank account
    private const int RESTRICT_CONFIRMATION = 1 << 11;
    // tg's alist names RESTRICT_FAILURE, which machine_silo.dm never defines: DM reads an undefined name in an alist key as its text
    private const string RESTRICT_FAILURE = "RESTRICT_FAILURE";

    ///List of announcement messages for silo restrictions
    // tg: code/modules/mining/machine_silo.dm:56  var/static/alist/announcement_messages
    private static DmList announcement_messages => TgState.Current.obj_machinery_ore_silo__announcement_messages ??= Dm.alist(
        Dm.kv(BAN_ATTEMPT_FAILURE_NO_ACCESS, "ACCESS ENFORCEMENT FAILURE: $SILO_USER_NAME lacks supply command authority."),
        Dm.kv(BAN_ATTEMPT_FAILURE_CHALLENGING_DA_CHIEF, "ACCESS ENFORCEMENT FAILURE: $SILO_USER_NAME attempting subversion of supply command authority."),
        Dm.kv(BAN_ATTEMPT_FAILURE_SOULLESS_MACHINE, "$SILO_USER_NAME INTERFACE_EXCEPTION -> BANNED_USERS+=[$TARGET_NAME] => NO_OP"),
        Dm.kv(BAN_CONFIRMATION, "ACCESS ENFORCEMENT CONFIRMATION[$SILO_USER_NAME]: $TARGET_NAME banned from ore silo access."),
        Dm.kv(UNBAN_CONFIRMATION, "ACCESS ENFORCEMENT CONFIRMATION[$SILO_USER_NAME]: $TARGET_NAME unbanned from ore silo access."),
        Dm.kv(FAILED_OPERATION_SUSPICIOUS, "NULL_ACCOUNT_RESOLVE_PTR_#?"),
        Dm.kv(FAILED_OPERATION_NO_BANK_ID, "ACCESS ENFORCEMENT FAILURE: No account ID found. Please contact a banker."),
        Dm.kv(UNRESTRICT_FAILURE_NO_ACCESS, "ID ACCESS REQUIREMENT ENFORCED: $SILO_USER_NAME lacks supply command authority; ID ACCESS REQUIREMENT REMOVAL FAILED."),
        Dm.kv(UNRESTRICT_FAILURE_SOULLESS_MACHINE, "$SILO_USER_NAME INTERFACE_EXCEPTION -> ID_ACCESS_REQUIREMENT = !ID_ACCESS_REQUIREMENT => NO_OP"),
        Dm.kv(RESTRICT_CONFIRMATION, "ID ACCESS REQUIREMENT ROUTINE STARTED: $SILO_USER_NAME has enforced ID read requirement for this ore silo."),
        Dm.kv(UNRESTRICT_CONFIRMATION, "ID ACCESS REQUIREMENT ROUTINE SUSPENDED: $SILO_USER_NAME has removed ID read requirement for this ore silo."),
        Dm.kv(RESTRICT_FAILURE, "ID ACCESS REQUIREMENT ROUTINE FAILED TO START: $SILO_USER_NAME()"));

    // tg: code/modules/mining/machine_silo.dm:71
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        materials = Dm.@new<datum_material_container>(
            this,
            SSmaterials.get_materials_by_flag(MATERIAL_SILO_STORED),
            INFINITY,
            MATCONTAINER_EXAMINE,
            Dm.named("container_signals", Dm.list(
                Dm.kv(COMSIG_MATCONTAINER_ITEM_CONSUMED, "/obj/machinery/ore_silo/proc/" + nameof(on_item_consumed)),
                Dm.kv(COMSIG_MATCONTAINER_STACK_RETRIEVED, "/obj/machinery/ore_silo/proc/" + nameof(log_sheets_ejected)))),
            Dm.named("allowed_items", typeof(obj_item_stack)));

        if (GLOB.ore_silo_default == null && Dm.truthy(mapload) && Dm.truthy(is_station_level(z)))
        {
            GLOB.ore_silo_default = this;
            ID_required = TRUE;
        }

        register_context();
        setup_radio();
        configure_default_announcements_policy();
        return dot;
    }

    // tg: code/modules/mining/machine_silo.dm:94
    public override object Destroy(float? force = null)
    {
        if (GLOB.ore_silo_default == this)
            GLOB.ore_silo_default = null;

        foreach (datum_remote_materials mats in Dm.each<datum_remote_materials>(ore_connected_machines?.Copy()))
            Waiting.NoSleep(mats.disconnect());

        ore_connected_machines = null;
        QDEL_NULL(ref materials);
        QDEL_NULL(ref radio);

        return base.Destroy(force);
    }

    // tg: code/modules/mining/machine_silo.dm:107
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/modules/mining/machine_silo.dm:113
    public virtual void setup_radio()
    {
        radio = Dm.@new<obj_item_radio>(this);
        radio.subspace_transmission = TRUE;
        radio.canhear_range = 0;
        radio.set_listening(FALSE);
        var keyslot = Dm.@new<obj_item_encryptionkey>();
        radio.keyslot = keyslot;
        keyslot.channels[RADIO_CHANNEL_COMMON] = TRUE;
        keyslot.channels[RADIO_CHANNEL_COMMAND] = TRUE;
        keyslot.channels[RADIO_CHANNEL_SUPPLY] = TRUE;
        keyslot.channels[RADIO_CHANNEL_SECURITY] = TRUE;
        radio.recalculateChannels();
    }

    // tg: code/modules/mining/machine_silo.dm:125
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(span_notice("It can be linked to techfabs, circuit printers and protolathes with a multitool."));
        dot.Add(span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT("screwed"), Dm.truthy(panel_open) ? "closed" : "open")));
        if (Dm.truthy(panel_open))
            dot.Add(span_notice(I18n.T("The whole machine can be [] apart.", EXAMINE_HINT("pried"))));
        return dot;
    }

    // tg: code/modules/mining/machine_silo.dm:132
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = NONE;
        if ((held_item == null))
            return dot;

        if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] Panel", Dm.truthy(panel_open) ? "Close" : "Open");
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.eq(held_item.tool_behaviour, TOOL_MULTITOOL))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Log Silo";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (Dm.truthy(panel_open) && Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/modules/mining/machine_silo.dm:149
    public virtual void on_item_consumed(datum_material_container container = null, obj_item item_inserted = null, object last_inserted_id = null, object mats_consumed = null, object amount_inserted = null, atom context = null, DmList user_data = null)
    {
        // SIGNAL_HANDLER
        silo_log(context as obj_machinery, "DEPOSITED", Dm.num(amount_inserted), item_inserted.name, mats_consumed as DmList, user_data);

        if (Dm.truthy(context._listen_lookup?[COMSIG_SILO_ITEM_CONSUMED]))
            context._SendSignal(COMSIG_SILO_ITEM_CONSUMED, Dm.list(context, container, item_inserted, last_inserted_id, mats_consumed, amount_inserted));
    }

    // tg: code/modules/mining/machine_silo.dm:156
    public virtual void log_sheets_ejected(datum_material_container container = null, obj_item_stack_sheet sheets = null, atom context = null, DmList user_data = null)
    {
        // SIGNAL_HANDLER
        silo_log(context as obj_machinery, "WITHDRAWN", -Dm.num(sheets.amount) * SHEET_MATERIAL_AMOUNT, Dm.text("[]", sheets.name), sheets.custom_materials, user_data);
    }

    // tg: code/modules/mining/machine_silo.dm:161
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
        => default_deconstruction_screwdriver(user, tool);

    // tg: code/modules/mining/machine_silo.dm:164
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
        => await default_deconstruction_crowbar(user, tool);

    // tg: code/modules/mining/machine_silo.dm:167
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        ((obj_item_multitool)tool).set_buffer(this);
        balloon_alert(user, "saved to multitool buffer");
        await DmTask.CompletedTask;
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * The logic for disconnecting a remote receptacle (RCD, fabricator, etc.) is collected here for sanity's sake
     * rather than being on specific types. Serves to agnosticize the remote_materials component somewhat rather than
     * snowflaking code for silos into the component.
     * * receptacle - The datum/remote_materials component that is getting connected.
     * * physical_receptacle - the actual object in the game world that was connected to our material supply. Typed as atom/movable for
     *   future-proofing against anything that may conceivably one day have remote silo access, such as a cyborg, an implant, structures, vehicles,
     *   and so-on.
     */
    // tg: code/modules/mining/machine_silo.dm:182
    public virtual void connect_receptacle(datum_remote_materials receptacle = null, atom_movable physical_receptacle = null)
    {
        ore_connected_machines.Add(receptacle);
        receptacle.mat_container = materials;
        receptacle.silo = this;
        RegisterSignal(physical_receptacle, COMSIG_ORE_SILO_PERMISSION_CHECKED, nameof(check_permitted));
    }

    /**
     * The logic for disconnecting a remote receptacle (RCD, fabricator, etc.) is collected here for sanity's sake
     * rather than being on specific types. Cleans up references to us and to the receptacle.
     * * receptacle - The datum/remote_materials component that is getting destroyed.
     * * physical_receptacle - the actual object in the game world that was connected to our material supply. Typed as atom/movable for
     *   future-proofing against anything that may conceivably one day have remote silo access, such as a cyborg, an implant, structures, vehicles,
     *   and so-on.
     */
    // tg: code/modules/mining/machine_silo.dm:196
    public virtual void disconnect_receptacle(datum_remote_materials receptacle = null, atom_movable physical_receptacle = null)
    {
        ore_connected_machines?.Remove(receptacle);
        receptacle.mat_container = null;
        receptacle.silo = null;
        holds.Remove(receptacle);
        UnregisterSignal(physical_receptacle, COMSIG_ORE_SILO_PERMISSION_CHECKED);
    }

    // tg: code/modules/mining/machine_silo.dm:203
    public virtual object check_permitted(datum source = null, DmList user_data = null, atom_movable physical_receptacle = null)
    {
        // SIGNAL_HANDLER
        if (!Dm.truthy(ID_required))
            return COMPONENT_ORE_SILO_ALLOW;
        if (!islist(user_data))
        {
            // Just allow to salvage the situation (tg's CRASH ends the proc with its . = COMPONENT_ORE_SILO_ALLOW)
            stack_trace("Invalid data passed to check_permitted", "code/modules/mining/machine_silo.dm", 211);
            return COMPONENT_ORE_SILO_ALLOW;
        }
        if (Dm.truthy(user_data[SILICON_OVERRIDE]) || Dm.truthy(user_data[CHAMELEON_OVERRIDE]) || Dm.truthy((astype(user_data["accesses"], typeof(DmList)) as DmList)?.Find(ACCESS_QM)))
            return COMPONENT_ORE_SILO_ALLOW;
        if (Dm.truthy(user_data[ID_READ_FAILURE]))
        {
            Waiting.NoSleep(physical_receptacle.say("SILO ERR: ID interface failure. Please contact the Head of Personnel."));
            return COMPONENT_ORE_SILO_DENY;
        }
        if (!Dm.truthy(user_data["account_id"]) || !isnum(user_data["account_id"]))
        {
            if (prob(5))
                Waiting.NoSleep(physical_receptacle.say(I18n.T("SILO ERR: Bank account ID not found. Initiating anti-communist silo-access policy.")));
            Waiting.NoSleep(physical_receptacle.say(I18n.T("SILO ERR: No account ID found. Please contact Head of Personnel.")));
            return COMPONENT_ORE_SILO_DENY;
        }
        if (Dm.truthy(banned_users.Find(user_data["account_id"])))
        {
            Waiting.NoSleep(physical_receptacle.say("SILO ERR: You are banned from using this ore silo."));
            return COMPONENT_ORE_SILO_DENY;
        }
        return COMPONENT_ORE_SILO_ALLOW;
    }

    // tg: code/modules/mining/machine_silo.dm:228
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/sheetmaterials)): the asset transport is the client's (DV44)
        await DmTask.CompletedTask;
        return Dm.list();
    }

    // tg: code/modules/mining/machine_silo.dm:233
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "OreSilo", "Ore Silo Control");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mining/machine_silo.dm:239
    public override async DmTask<object> ui_static_data(mob user = null) => await materials.ui_static_data();

    // tg: code/modules/mining/machine_silo.dm:242
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["materials"] = await materials.ui_data();

        DmList machines = Dm.list();
        data["machines"] = machines;
        foreach (datum_remote_materials remote in Dm.each<datum_remote_materials>(ore_connected_machines))
        {
            atom parent = remote.parent;
            // DEVIATION(DV257): tg's "icon" is icon2base64(icon(initial(parent.icon), initial(parent.icon_state), frame = 1)), a picture
            // the server renders; the window draws the machine's own icon file and state from the converted .dmi (as DV150)
            machines.Add(Dm.list(Dm.list(
                Dm.kv("icon", Dm.initial(parent.GetType(), (atom a) => a.icon)),
                Dm.kv("icon_state", Dm.initial(parent.GetType(), (atom a) => a.icon_state)),
                Dm.kv("name", parent.name),
                Dm.kv("on_hold", Dm.truthy(holds[remote]) ? TRUE : FALSE),
                Dm.kv("location", get_area_name(parent, TRUE)))));
        }

        DmList logs = Dm.list();
        data["logs"] = logs;
        foreach (datum_ore_silo_log entry in Dm.each<datum_ore_silo_log>(GLOB.silo_access_logs[REF(this)]))
        {
            logs.Add(Dm.list(Dm.list(
                Dm.kv("raw_materials", entry.get_raw_materials("")),
                Dm.kv("machine_name", entry.machine_name),
                Dm.kv("area_name", entry.area_name),
                Dm.kv("action", entry.action),
                Dm.kv("amount", entry.amount),
                Dm.kv("time", entry.timestamp),
                Dm.kv("noun", entry.noun),
                Dm.kv("user_data", entry.user_data))));
        }
        data["banned_users"] = banned_users;
        data["ID_required"] = ID_required;

        return data;
    }

    // tg: code/modules/mining/machine_silo.dm:278
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action as string)
        {
            case "remove":
            case "hold":
            {
                object index = @params["id"];
                if ((index == null))
                    return dot;

                index = text2num(index);
                if ((index == null))
                    return dot;

                datum_remote_materials remote = ore_connected_machines[index] as datum_remote_materials;
                if ((remote == null))
                    return dot;

                if (Dm.eq(action, "remove"))
                    await remote.disconnect();
                else
                    remote.toggle_holding();
                return TRUE;
            }

            case "eject":
            {
                datum_material ejecting = Dm.locate_ref(@params["ref"]) as datum_material;
                if (ejecting == null)
                    return dot;

                object amount = @params["amount"];
                if ((amount == null))
                    return dot;

                amount = text2num(amount);
                if ((amount == null))
                    return dot;

                await materials.retrieve_stack(amount, ejecting, drop_location() as atom, user_data: ID_DATA(ui.user));
                return TRUE;
            }

            case "toggle_ban":
            {
                DmList banned_user_data = @params["user_data"] as DmList;
                attempt_ban_toggle(ui.user as mob_living, banned_user_data);
                return TRUE;
            }

            case "toggle_restrict":
                attempt_toggle_restrict(ui.user as mob_living);
                return TRUE;
        }
        return dot;
    }

    /**
     * Called from the ore silo's UI, when someone attempts to (un)ban a user from using the ore silo.
     * The person doing the banning should have at least QM access. Unless this is emagged. Not modifiable by silicons unless emagged.
     * Anyone but the Captain attempting to ban someone with QM access from the ore silo gets what is essentially a glorified version
     * of the permission denied result.
     * * user - The person who clicked the ban button in the UI.
     * * target_user_data - Data in the form rendered from ID_DATA(target), passed into the ore silo logs by whatever the target did such
     * 	 as removing/adding sheets, printing items, etc
     */
    // tg: code/modules/mining/machine_silo.dm:349
    public virtual void attempt_ban_toggle(mob_living user = null, DmList target_user_data = null)
    {
        if (user == null || target_user_data == null)
            throw Dm.CRASH("Bad arguments passed to attempt_ban_toggle", "code/modules/mining/machine_silo.dm", 351);
        bool emagged = Dm.truthy(Dm.bitand(obj_flags, EMAGGED));
        if ((isAI(user) || iscyborg(user) || isdrone(user)) && !emagged)
        {
            to_chat(user, span_danger("A scroll of red text occludes your vision: ACCESS ENFORCEMENT _disabled_ for SILICON INTERFACE."));
            Waiting.NoSleep(user.flash_act(intensity: 1, affect_silicon: TRUE));
            handle_access_action_feedback(
                BAN_ATTEMPT_FAILURE_SOULLESS_MACHINE,
                ID_DATA(user),
                target_user_data);
            return;
        }

        object target_bank_id = target_user_data["account_id"];
        object target_is_banned = banned_users.Find(target_bank_id);
        // Agent card bypasses bans but we want to specially handle them anyway
        // so a bunch of random account IDs don't fill the list and do something
        // like ban people who haven't joined the round yet
        bool haxxor_card_ban_immunity = (target_user_data[CHAMELEON_OVERRIDE] != null);

        to_chat(user, span_warning(I18n.T("You press the button to []ban []'s account...", Dm.truthy(target_is_banned) ? "un" : "", target_user_data["name"])));
        // No feedback if emagged
        if (emagged)
        {
            if (!haxxor_card_ban_immunity && isnum(target_bank_id))
            {
                if (Dm.truthy(target_is_banned)) banned_users.Remove(target_bank_id);
                else banned_users.Add(target_bank_id);
            }
            return;
        }

        DmList silo_user_data = ID_DATA(user);
        DmList silo_user_accesses = astype(silo_user_data["accesses"], typeof(DmList)) as DmList;
        DmList target_user_accesses = astype(target_user_data["accesses"], typeof(DmList)) as DmList;

        // Even though QM bypasses the access check (or rather always pases)
        // perhaps the Captain would pre-emptively ban them right before a demotion
        if (Dm.truthy(target_user_accesses?.Find(ACCESS_QM)) && !Dm.truthy(silo_user_accesses?.Find(ACCESS_CAPTAIN)))
        {
            handle_access_action_feedback(BAN_ATTEMPT_FAILURE_CHALLENGING_DA_CHIEF, silo_user_data, target_user_data);
            return;
        }
        if (!Dm.truthy(silo_user_accesses?.Find(ACCESS_QM)))
        {
            handle_access_action_feedback(BAN_ATTEMPT_FAILURE_NO_ACCESS, silo_user_data, target_user_data);
            return;
        }
        if (haxxor_card_ban_immunity)
        {
            handle_access_action_feedback(FAILED_OPERATION_SUSPICIOUS, silo_user_data, target_user_data);
            return;
        }
        if (!Dm.truthy(target_bank_id) || !isnum(target_bank_id))
        {
            handle_access_action_feedback(FAILED_OPERATION_NO_BANK_ID, silo_user_data, target_user_data);
            return;
        }
        if (Dm.truthy(target_is_banned))
        {
            banned_users.Remove(target_bank_id);
            handle_access_action_feedback(UNBAN_CONFIRMATION, silo_user_data, target_user_data);
            return;
        }
        // If we got here, we are banning the user
        banned_users.Add(target_bank_id);
        handle_access_action_feedback(BAN_CONFIRMATION, silo_user_data, target_user_data);
    }

    /**
     * Called from the ore silo tgui interface, for when someone attempts to restrict or unrestrict the ore silo from requiring
     * an ID with an attached bank account (or, a chameleon ID, or, being a silicon)
     * user - the person who tried to toggle the ore silo's access restriction. Needs to be someone with QM access, unless the
     * 	silo is emagged. Shouldn't allow silicons to toggle this unless the silo is emagged.
     *
     */
    // tg: code/modules/mining/machine_silo.dm:434
    public virtual void attempt_toggle_restrict(mob_living user = null)
    {
        if (user == null)
            throw Dm.CRASH("No user to check toggle attempt restrictions. .ID_required is unchanged.", "code/modules/mining/machine_silo.dm", 436);
        bool emagged = Dm.truthy(Dm.bitand(obj_flags, EMAGGED));
        if (emagged)
        {
            ID_required = Dm.truthy(ID_required) ? FALSE : TRUE;
            return;
        }
        DmList silo_user_data = ID_DATA(user);
        object is_a_robot = silo_user_data[SILICON_OVERRIDE];
        if (Dm.truthy(is_a_robot))
        {
            handle_access_action_feedback(UNRESTRICT_FAILURE_SOULLESS_MACHINE, silo_user_data, null);
            return;
        }
        DmList user_accesses = astype(silo_user_data["accesses"], typeof(DmList)) as DmList;
        if (!Dm.truthy(user_accesses?.Find(ACCESS_QM)))
        {
            handle_access_action_feedback(UNRESTRICT_FAILURE_NO_ACCESS, silo_user_data, null);
            return;
        }
        ID_required = Dm.truthy(ID_required) ? FALSE : TRUE;
        handle_access_action_feedback(
            Dm.truthy(ID_required) ? RESTRICT_CONFIRMATION : UNRESTRICT_CONFIRMATION,
            silo_user_data,
            null);
    }

    // I must sacrifice the line diff to the gods of readable code
    /// Set up the default announcement policy for actions
    /// radio_channels[channel_name_key] = policy_bitmask
    /// where channel_name_key is one of RADIO_CHANNEL_(COMMON|COMMAND|SECURITY|SUPPLY)
    /// and policy_bitmask is a bitmask of actions that will be announced on that channel
    /// by default
    // tg: code/modules/mining/machine_silo.dm:470
    public virtual void configure_default_announcements_policy()
    {
        radio_channels[RADIO_CHANNEL_COMMON] = BAN_ATTEMPT_FAILURE_CHALLENGING_DA_CHIEF;
        radio_channels[RADIO_CHANNEL_COMMON] = Dm.bitor(radio_channels[RADIO_CHANNEL_COMMON], RESTRICT_CONFIRMATION);
        radio_channels[RADIO_CHANNEL_COMMON] = Dm.bitor(radio_channels[RADIO_CHANNEL_COMMON], UNRESTRICT_CONFIRMATION);

        // start off with the common channel bitmask policy as a base
        radio_channels[RADIO_CHANNEL_COMMAND] = radio_channels[RADIO_CHANNEL_COMMON];
        foreach (int flag in new[] { BAN_ATTEMPT_FAILURE_NO_ACCESS, BAN_ATTEMPT_FAILURE_SOULLESS_MACHINE, UNRESTRICT_FAILURE_NO_ACCESS,
                     UNRESTRICT_FAILURE_SOULLESS_MACHINE, FAILED_OPERATION_NO_BANK_ID, BAN_CONFIRMATION, UNBAN_CONFIRMATION })
            radio_channels[RADIO_CHANNEL_COMMAND] = Dm.bitor(radio_channels[RADIO_CHANNEL_COMMAND], flag);

        // Security channel is used for security-related announcements
        // but gets less information than command to avoid over-informing them without
        // QM involvement
        radio_channels[RADIO_CHANNEL_SECURITY] = radio_channels[RADIO_CHANNEL_COMMON];
        foreach (int flag in new[] { BAN_ATTEMPT_FAILURE_NO_ACCESS, UNRESTRICT_FAILURE_NO_ACCESS, BAN_CONFIRMATION, UNBAN_CONFIRMATION })
            radio_channels[RADIO_CHANNEL_SECURITY] = Dm.bitor(radio_channels[RADIO_CHANNEL_SECURITY], flag);

        // Supply channel has the same policy by default as the command channel
        // due to their usual purview of the ore silo
        radio_channels[RADIO_CHANNEL_SUPPLY] = radio_channels[RADIO_CHANNEL_COMMAND];
    }

    // tg: code/modules/mining/machine_silo.dm:499
    public virtual void handle_access_action_feedback(int action = 0, DmList silo_user_data = null, DmList target_user_data = null)
    {
        object message = announcement_messages[action];
        message = replacetext(message, "$TARGET_NAME", target_user_data?["name"]);
        message = replacetext(message, "$SILO_USER_NAME", silo_user_data["name"]);
        Waiting.NoSleep(say(message));
        foreach (object channel in Dm.each<object>(radio_channels))
        {
            // Key is the channel name, value is the bitmask of announced actions
            if (Dm.truthy(Dm.bitand(action, radio_channels[channel])))
            {
                float say_cooldown_adherence_timer = 1 * SECONDS * Dm.num(radio_channels.Find(channel)); // * 1, * 2, * 3, etc.
                _addtimer(Dm.@new<datum_callback>(radio, nameof(obj_item.talk_into), this, message, channel), say_cooldown_adherence_timer,
                    file: "code/modules/mining/machine_silo.dm", line: 508);
            }
        }
    }

    /**
     * Creates a log entry for depositing/withdrawing from the silo both ingame and in text based log
     *
     * Arguments:
     * - [M][/obj/machinery]: The machine performing the action.
     * - action: Text that visually describes the action (smelted/deposited/resupplied...)
     * - amount: The amount of sheets/objects deposited/withdrawn by this action. Positive for depositing, negative for withdrawing.
     * - noun: Name of the object the action was performed with (sheet, units, ore...)
     * - [mats][list]: Assoc list in format (material datum = amount of raw materials). Wants the actual amount of raw (iron, glass...) materials involved in this action. If you have 10 metal sheets each worth 100 iron you would pass a list with the iron material datum = 1000
     * - user_data - ID_DATA(user), includes details (not currently) rendered to the player, such as bank account #, see the proc on SSid_access
     */
    // tg: code/modules/mining/machine_silo.dm:534
    public virtual void silo_log(obj_machinery M = null, object action = null, float amount = 0, object noun = null, DmList mats = null, DmList user_data = null)
    {
        if (!Dm.truthy(length(mats)))
            return;

        datum_ore_silo_log entry = Dm.@new<datum_ore_silo_log>(M, action, amount, noun, mats, user_data);
        DmList logs = GLOB.silo_access_logs[REF(this)] as DmList;
        if (!Dm.truthy(LAZYLEN(logs)))
            GLOB.silo_access_logs[REF(this)] = logs = Dm.list(entry);
        else if (!((datum_ore_silo_log)logs[1]).merge(entry))
            logs.Insert(1, entry);

        flick("silo_active", this);
    }
}

///The log entry for an ore silo action
// tg: code/modules/mining/machine_silo.dm:550
public partial class datum_ore_silo_log : datum
{
    ///The actual action performed by the machine
    // tg: code/modules/mining/machine_silo.dm:556  (hand-written: the V3 rule would type it a typepath, types-converter.md 3.5)
    public object action;

    // tg: code/modules/mining/machine_silo.dm:566
    [DmParams("M", "_action", "_amount", "_noun", "mats", "user_data")]
    public override async DmTask New(params object[] __extra)
    {
        obj_machinery M = Dm.arg(__extra, 0) as obj_machinery;
        object _action = Dm.arg(__extra, 1);
        object _amount = Dm.arg(__extra, 2);
        object _noun = Dm.arg(__extra, 3);
        DmList mats = Dm.arg(__extra, 4) as DmList ?? Dm.list();
        DmList user_data = Dm.arg(__extra, 5) as DmList;

        timestamp = round_timestamp();
        machine_name = M.name;
        area_name = get_area_name(M, TRUE);
        action = _action;
        amount = _amount;
        noun = _noun;
        materials = mats.Copy();
        this.user_data = user_data;
        DmList data = Dm.list(
            Dm.kv("machine_name", machine_name),
            Dm.kv("area_name", AREACOORD(M)),
            Dm.kv("action", action),
            Dm.kv("amount", abs(Dm.num(amount))),
            Dm.kv("noun", noun),
            Dm.kv("raw_materials", get_raw_materials("")),
            Dm.kv("direction", Dm.num(amount) < 0 ? "withdrawn" : "deposited"),
            Dm.kv("user_data", user_data));
        logger.Log(
            LOG_CATEGORY_SILO,
            Dm.text("[] in [[]] [] []x [] | [] | []", machine_name, AREACOORD(M), action, abs(Dm.num(amount)), noun, get_raw_materials(""), user_data?["name"]),
            data);
        await DmTask.CompletedTask;
    }

    /**
     * Merges a silo log entry with this one
     * Arguments
     *
     * * datum/ore_silo_log/other - the other silo entry we are trying to merge with this one
     */
    // tg: code/modules/mining/machine_silo.dm:597
    public virtual bool merge(datum_ore_silo_log other = null)
    {
        if (other == this || !Dm.eq(action, other.action) || !Dm.eq(noun, other.noun))
            return false;
        if (!Dm.eq(machine_name, other.machine_name) || !Dm.eq(area_name, other.area_name))
            return false;

        timestamp = other.timestamp;
        amount = Dm.num(amount) + Dm.num(other.amount);
        foreach (object each in Dm.each<object>(other.materials))
            materials[each] = Dm.num(materials[each]) + Dm.num(other.materials[each]);
        return true;
    }

    /**
     * Returns list/materials but with each entry joined by an seperator to create 1 string
     * Arguments
     *
     * * separator - the string used to concatenate all entries in list/materials
     */
    // tg: code/modules/mining/machine_silo.dm:615
    public virtual string get_raw_materials(string separator = null)
    {
        DmList msg = Dm.list();
        foreach (object key in Dm.each<object>(materials))
        {
            datum_material M = (datum_material)key;
            float val = round(Dm.num(materials[key])) / SHEET_MATERIAL_AMOUNT;
            msg.Add(separator);
            separator = ", ";
            msg.Add(Dm.text("[][] []", Dm.num(amount) < 0 ? "-" : "+", val, M.name));
        }
        return msg.Join("") as string;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/machine_silo.dm:56  /obj/machinery/ore_silo's var/static/alist/announcement_messages
    internal DmList obj_machinery_ore_silo__announcement_messages;
}
