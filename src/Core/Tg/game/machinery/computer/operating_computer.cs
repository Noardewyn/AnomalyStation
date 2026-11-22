// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/operating_computer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The operating computer finds the table beside it, shows its patient's vitals and the
// surgery state of the selected zone, lists the operations it knows (tg's unlocked ones and those loaded from a disk) with the
// next steps marked, and loads surgery disks. Silicons SI3 (D326): the techweb link (the station's science techweb, its researched
// surgery designs). Gaps RD3 (D333): the autopsy experiments' always-active handler, their list and their window.
// The var block is generated.

// tg: code/game/machinery/computer/operating_computer.dm:4
public partial class obj_machinery_computer_operating : obj_machinery_computer
{
    // tg: code/game/machinery/computer/operating_computer.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        find_table();
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/machinery/computer/operating_computer.dm:34
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_no_default_techweb_link))) && linked_techweb == null) /* CONFIG_GET(flag/no_default_techweb_link) */
            CONNECT_TO_RND_SERVER_ROUNDSTART(ref linked_techweb, this);

        if (linked_techweb != null)
        {
            RegisterSignal(linked_techweb, COMSIG_TECHWEB_ADD_DESIGN, nameof(on_techweb_research));
            RegisterSignal(linked_techweb, COMSIG_TECHWEB_REMOVE_DESIGN, nameof(on_techweb_unresearch));
            RegisterSignal(linked_techweb, COMSIG_TECHWEB_EXPERIMENT_COMPLETED, nameof(update_static_data_batched));

            foreach (datum_design_surgery design in Dm.each<datum_design_surgery>(linked_techweb.get_researched_design_datums()))
                advanced_surgeries = Dm.bitor(advanced_surgeries, design.surgery) as DmList;
        }

        DmList operating_signals = Dm.list(
            Dm.kv(COMSIG_OPERATING_COMPUTER_AUTOPSY_COMPLETE, nameof(datum_component_experiment_handler.try_run_autopsy_experiment)));
        experiment_handler = await AddComponent(
            typeof(datum_component_experiment_handler),
            Dm.named("allowed_experiments", Dm.list(typeof(datum_experiment_autopsy))),
            Dm.named("config_flags", EXPERIMENT_CONFIG_ALWAYS_ACTIVE),
            Dm.named("config_mode", EXPERIMENT_CONFIG_ALTCLICK),
            Dm.named("experiment_signals", operating_signals)) as datum_component_experiment_handler;
    }

    // tg: code/game/machinery/computer/operating_computer.dm:58
    public override object Destroy(float? force = null)
    {
        foreach (object direction in Dm.each(GLOB.alldirs))
        {
            table = Dm.locate<obj_structure_table_optable>(get_step(this, Dm.num(direction)));
            if (table != null && table.computer == this)
                table.computer = null;
        }
        QDEL_NULL(ref experiment_handler);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/computer/operating_computer.dm:66
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        obj_item_multitool multitool = tool as obj_item_multitool;
        if (!QDELETED(multitool?.buffer) && istype(multitool.buffer, typeof(datum_techweb)))
            linked_techweb = (datum_techweb)multitool.buffer;
        return TRUE;
    }

    // tg: code/game/machinery/computer/operating_computer.dm:71
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_disk_surgery)))
        {
            user.visible_message(
                span_notice(I18n.T("[] begins to load [] in []...", user, tool, this)),
                span_notice(I18n.T("You begin to load a surgery protocol from []...", tool)),
                span_hear(I18n.T("You hear the chatter of a floppy drive.")));
            obj_item_disk_surgery disky = (obj_item_disk_surgery)tool;
            if (!Dm.truthy(await do_after(user, 1 * SECONDS, this)))
                return ITEM_INTERACT_BLOCKING;
            Dm.combine(ref advanced_surgeries, disky.surgeries);
            await update_static_data_for_all_viewers();
            playsound(this, Dm.resource("sound/machines/compiler/compiler-stage2.ogg"), 50, FALSE, SILENCED_SOUND_EXTRARANGE);
            balloon_alert(user, I18n.T("surgeries loaded"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(Dm.bitand(tool.item_flags, SURGICAL_TOOL)) && !Dm.truthy(user.combat_mode))
        {
            await ui_interact(user);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/machinery/computer/operating_computer.dm:93
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        await update_static_data_for_all_viewers();
    }

    // tg: code/game/machinery/computer/operating_computer.dm:96
    public virtual void find_table()
    {
        foreach (object direction in Dm.each(GLOB.alldirs))
        {
            table = Dm.locate<obj_structure_table_optable>(get_step(this, Dm.num(direction)));
            if (table != null)
            {
                table.computer = this;
                break;
            }
        }
    }

    // tg: code/game/machinery/computer/operating_computer.dm:103
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        object dot = await base.ui_status(user, state);
        if (isliving(user) && !issilicon(user))
            dot = min(dot, ui_check((mob_living)user));
        return dot;
    }

    /// Checks for special ui state conditions
    // tg: code/game/machinery/computer/operating_computer.dm:109
    public virtual object ui_check(mob_living user = null)
    {
        // lower states should be checked first

        // normally machines revert to "disabled" when non-operational,
        // but here we outright close it so people can't gleam extra info
        if (!Dm.truthy(is_operational))
            return UI_CLOSE;
        // if you're knocked out, ie anesthetic... definitely a no-go
        if (IS_UNCONSCIOUS(user))
            return UI_CLOSE;
        // the patient itself should be blocked from viewing the computer
        if (Dm.eq(user.body_position, LYING_DOWN))
            return (user.loc == table?.loc) ? UI_CLOSE : UI_DISABLED;
        // we have a tight range check so people can't spy on surgeries from across the room
        // likewise it'd be pretty lame if you could see what was going on while incapacitated
        if (get_dist(user, this) > 2 || Dm.truthy(user.incapacitated))
            return UI_DISABLED;

        return UI_INTERACTIVE;
    }

    // tg: code/game/machinery/computer/operating_computer.dm:129
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (Dm.truthy(ui))
            return dot;

        if (ishuman(user))
        {
            // if you are the first human to open the ui, it changes to your active zone
            if (!Dm.truthy(LAZYLEN(zone_on_open)))
                target_zone = user.zone_selected;
            // then we record what you were looking at for when you close it
            LAZYSET(ref zone_on_open, WEAKREF(user), user.zone_selected);
        }

        ui = Dm.@new<datum_tgui>(user, this, "OperatingComputer", name);
        await ui.open();
        return dot;
    }

    // tg: code/game/machinery/computer/operating_computer.dm:145
    public override void ui_close(mob user = null)
    {
        base.ui_close(user);
        // reverts zone to whatever you had going into it, typically chest, so you don't have to mess around with it
        object zone_found = LAZYACCESS(zone_on_open, WEAKREF(user));
        if (!Dm.truthy(zone_found))
            return;

        atom_movable_screen_zone_sel selector = user.hud_used?.screen_objects?[HUD_MOB_ZONE_SELECTOR] as atom_movable_screen_zone_sel;
        selector?.set_selected_zone(zone_found, user, FALSE);
        LAZYREMOVE(ref zone_on_open, WEAKREF(user));
        if (!Dm.truthy(LAZYLEN(zone_on_open)))
            zone_on_open = (DmList)Dm.initial(this, (obj_machinery_computer_operating o) => o.zone_on_open);
    }

    // tg: code/game/machinery/computer/operating_computer.dm:158
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // . += get_asset_datum(/datum/asset/simple/body_zones): the asset transport is the client's (DV44); the client draws tg's body
        // zone pictures (icons/hud/screen_gen.dmi's zone states) itself
        return await base.ui_assets(user);
    }

    // tg: code/game/machinery/computer/operating_computer.dm:162
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["has_table"] = Dm.num(Dm.truthy(table));
        data["target_zone"] = target_zone;
        if (table?.patient == null)
            return data;

        data["patient"] = Dm.list();
        mob_living patient = table.patient;
        DmList patient_data = (DmList)data["patient"];

        if (Dm.eq(patient.stat, DEAD))
        {
            patient_data["stat"] = "Dead";
            patient_data["statstate"] = "bad";
        }
        else if (Dm.eq(patient.stat, HARD_CRIT) || Dm.eq(patient.stat, SOFT_CRIT))
        {
            patient_data["stat"] = "Critical";
            patient_data["statstate"] = Dm.eq(patient.stat, HARD_CRIT) ? "bad" : "average";
        }
        else if (IS_UNCONSCIOUS(patient))
        {
            patient_data["stat"] = "Unconscious";
            patient_data["statstate"] = "average";
        }
        else
        {
            patient_data["stat"] = "Stable";
            patient_data["statstate"] = "good";
        }

        patient_data["health"] = patient.health;
        patient_data["blood_type"] = (patient.get_bloodtype() as datum_blood_type)?.name is var blood_type_name && Dm.truthy(blood_type_name) ? blood_type_name : "UNKNOWN";
        patient_data["maxHealth"] = patient.maxHealth;
        patient_data["minHealth"] = patient.dead_threshold;
        patient_data["bruteLoss"] = patient.get_brute_loss();
        patient_data["fireLoss"] = patient.get_fire_loss();
        patient_data["toxLoss"] = patient.get_tox_loss();
        patient_data["oxyLoss"] = patient.get_oxy_loss();
        patient_data["blood_level"] = patient.blood_volume;
        patient_data["standard_blood_level"] = BLOOD_VOLUME_NORMAL;
        patient_data["surgery_state"] = patient.get_surgery_state_as_list(deprecise_zone(target_zone));
        return data;
    }

    // tg: code/game/machinery/computer/operating_computer.dm:199
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        data["experiments"] = Dm.list();
        data["techwebs"] = Dm.list();
        data["surgeries"] = Dm.list();
        if (!Dm.truthy(is_operational))
            return data;

        if (linked_techweb != null)
        {
            (data["techwebs"] as DmList).Add(Dm.list(Dm.list(
                Dm.kv("web_id", linked_techweb.id),
                Dm.kv("web_org", linked_techweb.organization),
                Dm.kv("selected", TRUE),
                Dm.kv("ref", REF(linked_techweb)),
                Dm.kv("all_servers", linked_techweb.techweb_servers))));

            foreach (datum_experiment experiment in Dm.each_cast<datum_experiment>(linked_techweb.available_experiments))
            {
                if (experiment is datum_experiment_autopsy)
                    Dm.append_at(data, "experiments", Dm.list(await experiment.to_ui_data()));
            }
        }

        DmList operations = (DmList)GLOB.operations.get_instances_from((DmList)Dm.bitor(GLOB.operations.unlocked, advanced_surgeries));
        bool any_recommended = false;
        foreach (datum_surgery_operation operation in Dm.each_cast<datum_surgery_operation>(operations))
        {
            bool recommend = false;
            if (table?.patient != null && Dm.truthy(operation.show_as_next_step(table.patient, target_zone)))
            {
                recommend = true;
                any_recommended = true;
            }

            Dm.append((DmList)data["surgeries"], Dm.list(Dm.list(
                Dm.kv("name", Dm.truthy(operation.rnd_name) ? operation.rnd_name : operation.name),
                Dm.kv("desc", Dm.truthy(operation.rnd_desc) ? operation.rnd_desc : operation.desc),
                Dm.kv("tool_rec", operation.get_recommended_tool() is var tool_rec && Dm.truthy(tool_rec) ? tool_rec : "error"),
                Dm.kv("requirements", operation.get_requirements()),
                Dm.kv("show_as_next", Dm.num(recommend)),
                Dm.kv("show_in_list", TRUE),
                Dm.kv("priority", Dm.bitand(operation.operation_flags, OPERATION_PRIORITY_NEXT_STEP)),
                Dm.kv("mechanic", Dm.bitand(operation.operation_flags, OPERATION_MECHANIC)))));
        }

        if (!any_recommended && table?.patient != null && !HAS_TRAIT(table.patient, TRAIT_READY_TO_OPERATE))
        {
            Dm.append((DmList)data["surgeries"], Dm.list(Dm.list(
                Dm.kv("name", I18n.T("Prepare for surgery")),
                Dm.kv("desc", I18n.T("Begin surgery by applying surgical drapes to the patient or by buckling the patient to the surgical table.")),
                Dm.kv("tool_rec", Dm.initial(typeof(obj_item_surgical_drapes), (obj_item o) => o.name)),
                Dm.kv("show_as_next", TRUE),
                Dm.kv("show_in_list", FALSE))));
        }

        return data;
    }

    // tg: code/game/machinery/computer/operating_computer.dm:251
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        object sw__1 = action;
        if (Dm.eq(sw__1, "open_experiments"))
            await experiment_handler.ui_interact(usr);
        else if (Dm.eq(sw__1, "change_zone"))
        {
            if (Dm.@in(@params["new_zone"], Dm.add(GLOB.all_body_zones, GLOB.all_precise_body_zones)))
            {
                target_zone = (string)@params["new_zone"];
                atom_movable_screen_zone_sel selector = ui.user.hud_used?.screen_objects?[HUD_MOB_ZONE_SELECTOR] as atom_movable_screen_zone_sel;
                selector?.set_selected_zone(@params["new_zone"], ui.user, FALSE);
            }
            await update_static_data_for_all_viewers();
        }
        return TRUE;
    }

    // tg: code/game/machinery/computer/operating_computer.dm:266
    [SignalHandler]
    public virtual void on_techweb_research(datum source = null, datum_design_surgery design = null)
    {
        if (!istype(design, typeof(datum_design_surgery)))
            return;

        Dm.combine(ref advanced_surgeries, design.surgery);
        update_static_data_batched();
    }

    // tg: code/game/machinery/computer/operating_computer.dm:275
    [SignalHandler]
    public virtual void on_techweb_unresearch(datum source = null, datum_design_surgery design = null)
    {
        if (!istype(design, typeof(datum_design_surgery)))
            return;

        Dm.remove(ref advanced_surgeries, design.surgery);
        update_static_data_batched();
    }

    /// Updates static data for all viewers after a miniscule delay (to batch multiple updates together)
    // tg: code/game/machinery/computer/operating_computer.dm:285
    [SignalHandler]
    public virtual void update_static_data_batched(params object[] __extra)
    {
        _addtimer(Dm.@new<datum_callback>(this, nameof(update_static_data_for_all_viewers)), 0.1f * SECONDS, TIMER_UNIQUE, file: "code/game/machinery/computer/operating_computer.dm", line: 288);
    }
}
