// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/ordnance/doppler_array.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the tachyon-doppler array records every explosion within its range in the direction it faces (its tachyon
// records with the factual and theoretical radii and the exploding tank's reactions), states them aloud, and prints a record to
// an inserted disk as an explosive ordnance file with the experiments it can complete (the NT Frontier app publishes it), with its
// DopplerArray window, its red directional light while powered and its panel and screen overlays. The var block is generated (D111).

// tg: code/modules/research/ordnance/doppler_array.dm:1
public partial class obj_machinery_doppler_array : obj_machinery
{
    // tg: code/modules/research/ordnance/doppler_array.dm:26
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(SSdcs, COMSIG_GLOB_EXPLOSION, nameof(sense_explosion));
        RegisterSignal(this, COMSIG_MACHINERY_POWER_LOST, nameof(update_doppler_light));
        RegisterSignal(this, COMSIG_MACHINERY_POWER_RESTORED, nameof(update_doppler_light));
        update_doppler_light();

        // Rotation determines the detectable direction.
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        return dot;
    }

    // tg: code/modules/research/ordnance/doppler_array.dm:47
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It is currently facing []", dir2text(dir))));
        return dot;
    }

    // tg: code/modules/research/ordnance/doppler_array.dm:51
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_disk_computer)))
            return NONE;
        await eject_disk(user);
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            balloon_alert(user, I18n.T("it's stuck to your hand!"));
            return ITEM_INTERACT_BLOCKING;
        }
        inserted_disk = tool as obj_item_disk_computer;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/research/ordnance/doppler_array.dm:61
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/research/ordnance/doppler_array.dm:65
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        object dot = default_deconstruction_screwdriver(user, tool);
        power_change();
        return dot;
    }

    // tg: code/modules/research/ordnance/doppler_array.dm:70
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    /// Printing of a record into a disk.
    // tg: code/modules/research/ordnance/doppler_array.dm:74
    public virtual void print(mob user = null, datum_data_tachyon_record record = null)
    {
        if (record == null || inserted_disk == null)
            return;

        datum_computer_file_data_ordnance_explosive record_data = Dm.@new<datum_computer_file_data_ordnance_explosive>();
        record_data.filename = Dm.text("Doppler Array []", record.name); //Doppler Array Log Recording #x
        record_data.explosion_record = record;
        record_data.possible_experiments = apply_experiments(record) as DmList;

        if (Dm.truthy(inserted_disk.add_file(record_data)))
            playsound(this, Dm.resource("sound/machines/ping.ogg"), 25);
        else
            playsound(this, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 25);
    }

    /**
     * Checks a specified tachyon record for fitting reactions, then returns a list with
     * the experiment typepath as key and score as value.
     * The score is the same for all explosive experiments (light radius).
     */
    // tg: code/modules/research/ordnance/doppler_array.dm:93
    public virtual object apply_experiments(datum_data_tachyon_record record = null)
    {
        DmList passed_experiments = Dm.list();
        DmList record_reactions = record.reaction_results;

        DmList radius_used = Dm.truthy(length(record.theory_radius)) ? record.theory_radius : record.factual_radius;
        object explosion_score = radius_used["shockwave_radius"];
        if (!Dm.truthy(explosion_score)) // Dont even bother.
            return passed_experiments;

        /// Early return for explosions without proper reaction_results. We will append experiments that are always fair game.
        if (!Dm.truthy(record_reactions.len))
        {
            foreach (datum_experiment_ordnance_explosive experiment in Dm.each<datum_experiment_ordnance_explosive>(SSresearch.ordnance_experiments))
            {
                if (Dm.truthy(experiment.allow_any_source))
                    Dm.append(ref passed_experiments, Dm.list(Dm.kv(experiment.type, explosion_score)));
            }
            return passed_experiments;
        }

        foreach (datum_experiment_ordnance_explosive experiment in Dm.each<datum_experiment_ordnance_explosive>(SSresearch.ordnance_experiments))
        {
            DmList experiment_reactions = experiment.required_reactions;
            /// The reactions in record.reaction_results that corresponds to the required_reactions list.
            DmList fitting_reactions = Dm.list();
            /// The reactions in required_reactions list that are missing in record.reaction_results.
            DmList missing_reactions = Dm.list();
            /// The reactions in record.reaction_results that are not present in required_reactions list.
            DmList extra_reactions = Dm.list();

            foreach (object reaction in Dm.each(experiment_reactions))
            {
                if (Dm.@in(reaction, record_reactions[TANK_RESULTS_REACTION]))
                    Dm.append(ref fitting_reactions, reaction);
                else
                    Dm.append(ref missing_reactions, reaction);
            }

            foreach (object reaction in Dm.each(record_reactions[TANK_RESULTS_REACTION]))
            {
                if (!Dm.@in(reaction, fitting_reactions))
                    Dm.append(ref extra_reactions, reaction);
            }

            // Check for extra reactions.
            if (Dm.truthy(experiment.sanitized_reactions) && Dm.truthy(extra_reactions.len))
                continue;
            // Check for misc properties
            if (Dm.truthy(experiment.sanitized_misc) && Dm.truthy(length(record_reactions[TANK_RESULTS_MISC])))
                continue;
            // Check for missing reactions for those who require all.
            if (Dm.truthy(experiment.require_all) && Dm.truthy(missing_reactions.len))
                continue;
            // For those who only need one, check if there is actually one.
            if (!Dm.truthy(experiment.require_all) && Dm.truthy(experiment_reactions.len))
            {
                if (!Dm.truthy(fitting_reactions.len))
                    continue;
            }

            // Suceeded all check, append to the returned list.
            Dm.append(ref passed_experiments, Dm.list(Dm.kv(experiment.type, explosion_score)));
        }

        return passed_experiments;
    }

    /// Sensing, recording, and broadcasting of explosion
    // tg: code/modules/research/ordnance/doppler_array.dm:148
    [SignalHandler]
    public virtual object sense_explosion(datum source = null, turf epicenter = null, object devastation_range = null, object heavy_impact_range = null, object light_impact_range = null, object took = null, object orig_dev_range = null, object orig_heavy_range = null, object orig_light_range = null, object explosion_cause = null, object explosion_index = null)
    {
        object fetched_reaction_results = Dm.list();

        if (istype(explosion_cause, typeof(obj_item_tank)))
        {
            obj_item_tank exploding_tank = explosion_cause as obj_item_tank;
            fetched_reaction_results = exploding_tank.explosion_information();
        }

        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            return FALSE;
        turf zone = get_turf(this);
        if (!Dm.truthy(is_valid_z_level(zone, epicenter)))
            return FALSE;

        if (next_announce > Dm.num(world.time))
            return FALSE;
        next_announce = Dm.num(world.time) + cooldown;

        if ((get_dist(epicenter, zone) > max_dist) || !Dm.truthy(Dm.bitand(get_dir(zone, epicenter), dir)))
            return FALSE;

        datum_data_tachyon_record new_record = Dm.@new<datum_data_tachyon_record>();
        new_record.name = I18n.T("Log Recording #[]", record_number);
        new_record.timestamp = Dm.text("[] (PT: [])", server_timestamp(ic_time: TRUE), round_timestamp());
        new_record.coordinates = Dm.text("[], []", epicenter.x, epicenter.y);
        new_record.displacement = Dm.num(took);
        new_record.factual_radius["epicenter_radius"] = devastation_range;
        new_record.factual_radius["outer_radius"] = heavy_impact_range;
        new_record.factual_radius["shockwave_radius"] = light_impact_range;
        new_record.reaction_results = fetched_reaction_results as DmList;
        new_record.explosion_identifier = explosion_index;

        DmList messages = Dm.list(I18n.T("Explosive disturbance detected."),
            I18n.T("Epicenter at: grid ([], []). Temporal displacement of tachyons: [] seconds.", epicenter.x, epicenter.y, took),
            I18n.T("Factual: Epicenter radius: []. Outer radius: []. Shockwave radius: [].", devastation_range, heavy_impact_range, light_impact_range)
        );

        // If the bomb was capped, say its theoretical size.
        if (Dm.lt(devastation_range, orig_dev_range) || Dm.lt(heavy_impact_range, orig_heavy_range) || Dm.lt(light_impact_range, orig_light_range))
        {
            Dm.append(ref messages, I18n.T("Theoretical: Epicenter radius: []. Outer radius: []. Shockwave radius: [].", orig_dev_range, orig_heavy_range, orig_light_range));
            new_record.theory_radius["epicenter_radius"] = orig_dev_range;
            new_record.theory_radius["outer_radius"] = orig_heavy_range;
            new_record.theory_radius["shockwave_radius"] = orig_light_range;
        }

        foreach (object message in Dm.each(messages))
            Waiting.NoSleep(say(message));

        record_number++;
        Dm.append(ref records, new_record);

        return TRUE;
    }

    // tg: code/modules/research/ordnance/doppler_array.dm:201
    public virtual async DmTask<object> eject_disk(mob user = null)
    {
        if (inserted_disk == null)
            return FALSE;
        if (user == null || !Dm.truthy(await Adjacent(user)))
            await inserted_disk.forceMove(drop_location() as atom);
        else
            await user.put_in_hands(inserted_disk);
        playsound(this, Dm.resource("sound/machines/card_slide.ogg"), 50);
        return TRUE;
    }

    /// We rely on exited to clear references.
    // tg: code/modules/research/ordnance/doppler_array.dm:212
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == inserted_disk)
            inserted_disk = null;
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/research/ordnance/doppler_array.dm:217
    public override object powered__proc(object chan = null, float? ignore_use_power = null)
    {
        if (Dm.truthy(panel_open))
            return FALSE;
        return base.powered__proc(chan, ignore_use_power);
    }

    // tg: code/modules/research/ordnance/doppler_array.dm:222
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_open", base_icon_state)));
        else
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_cable", base_icon_state)));

        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN))) // Probably meant to be used on an indestructible doppler, but this is not implemented.
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_screen-broken", base_icon_state)));
        else if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_screen-off", base_icon_state)));
        return dot;
    }

    // tg: code/modules/research/ordnance/doppler_array.dm:234
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        await eject_disk();
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/research/ordnance/doppler_array.dm:238
    public override object Destroy(float? force = null)
    {
        inserted_disk = null;
        records.Cut(); // We only want to clear the list itself, not delete its contents.
        return base.Destroy(force);
    }

    // tg: code/modules/research/ordnance/doppler_array.dm:243
    [SignalHandler]
    public virtual void update_doppler_light()
    {
        Waiting.NoSleep(set_light_on(Dm.not(Dm.bitand(machine_stat, NOPOWER))));
    }

    // tg: code/modules/research/ordnance/doppler_array.dm:248
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "DopplerArray", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/research/ordnance/doppler_array.dm:254
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        data["records"] = Dm.list();
        data["disk"] = inserted_disk?.name;
        data["storage"] = Dm.text("[] / [] GQ", inserted_disk?.used_capacity, inserted_disk?.max_capacity);
        foreach (datum_data_tachyon_record singular_record in Dm.each<datum_data_tachyon_record>(records))
        {
            DmList record_data = Dm.list(
                Dm.kv("name", singular_record.name),
                Dm.kv("timestamp", singular_record.timestamp),
                Dm.kv("coordinates", singular_record.coordinates),
                Dm.kv("displacement", singular_record.displacement),
                Dm.kv("factual_epicenter_radius", singular_record.factual_radius["epicenter_radius"]),
                Dm.kv("factual_outer_radius", singular_record.factual_radius["outer_radius"]),
                Dm.kv("factual_shockwave_radius", singular_record.factual_radius["shockwave_radius"]),
                Dm.kv("theory_epicenter_radius", singular_record.theory_radius["epicenter_radius"]),
                Dm.kv("theory_outer_radius", singular_record.theory_radius["outer_radius"]),
                Dm.kv("theory_shockwave_radius", singular_record.theory_radius["shockwave_radius"]),
                Dm.kv("reaction_results", Dm.list()),
                Dm.kv("ref", REF(singular_record))
            );

            DmList reaction_data = singular_record.reaction_results;
            // Make sure the list is indexed first.
            if (Dm.truthy(reaction_data.len))
            {
                foreach (object path in Dm.each(reaction_data[TANK_RESULTS_REACTION]))
                {
                    Type reaction_path = path as Type;
                    Dm.append(record_data["reaction_results"] as DmList, Dm.initial(reaction_path, (datum_gas_reaction p) => p.name));
                }
                if (Dm.@in(TANK_MERGE_OVERPRESSURE, reaction_data[TANK_RESULTS_MISC]))
                    Dm.append(record_data["reaction_results"] as DmList, I18n.T("Tank overpressurized before reaction"));
            }

            Dm.append(data["records"] as DmList, Dm.list(record_data));
        }
        return data;
    }

    // tg: code/modules/research/ordnance/doppler_array.dm:287
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "delete_record"))
        {
            datum_data_tachyon_record record = Dm.locate_ref(@params["ref"]) is datum_data_tachyon_record found && Dm.@in(found, records) ? found : null;
            if (records == null || !Dm.@in(record, records))
                return dot;
            Dm.remove(ref records, record);
            return TRUE;
        }
        else if (Dm.eq(action, "save_record"))
        {
            datum_data_tachyon_record record = Dm.locate_ref(@params["ref"]) is datum_data_tachyon_record found && Dm.@in(found, records) ? found : null;
            if (records == null || !Dm.@in(record, records))
                return dot;
            print(usr, record);
            return TRUE;
        }
        else if (Dm.eq(action, "eject_disk"))
            await eject_disk(usr);
        return dot;
    }
}
