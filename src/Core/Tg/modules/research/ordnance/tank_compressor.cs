// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/ordnance/tank_compressor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the tank compressor pumps its input port's gas into an inserted tank past the safe limit, keeps what the tank
// leaks in a buffer and records it as a gas record when the tank is destroyed or ejected (ten moles or more), absorbs the tank's
// own blast, and prints a record to an inserted disk as a gaseous ordnance file with the experiments it can complete (the NT
// Frontier app publishes it). Its TankCompressor window, its open and closed states with the doors, pipes, cables and maintenance
// overlays. The var block is generated (D111).

// tg: code/modules/research/ordnance/tank_compressor.dm:5
public partial class obj_machinery_atmospherics_components_binary_tank_compressor : obj_machinery_atmospherics_components_binary
{
    // tg: code/modules/research/ordnance/tank_compressor.dm:1  #define TANK_COMPRESSOR_PRESSURE_LIMIT 5000 (file-local)
    private const float TANK_COMPRESSOR_PRESSURE_LIMIT = 5000;
    // tg: code/modules/research/ordnance/tank_compressor.dm:2  #define TANK_COMPRESSOR_MAX_TRANSFER_RATE 20 (file-local)
    private const float TANK_COMPRESSOR_MAX_TRANSFER_RATE = 20;
    // tg: code/modules/research/ordnance/tank_compressor.dm:3  #define SIGNIFICANT_AMOUNT_OF_MOLES 10 (file-local)
    private const float SIGNIFICANT_AMOUNT_OF_MOLES = 10;

    // tg: code/modules/research/ordnance/tank_compressor.dm:26
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        leaked_gas_buffer = Dm.@new<datum_gas_mixture>(200);

        RegisterSignal(this, COMSIG_ATOM_INTERNAL_EXPLOSION, nameof(explosion_handle));
        return dot;
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:32
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("This one is rated for up to [] kPa.", TANK_COMPRESSOR_PRESSURE_LIMIT));
        Dm.append(ref dot, I18n.T("Can be opened with a screwdriver and rotated with a wrench. The green port is the input, the red one is the output."));
        return dot;
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:45
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode) || Dm.truthy(panel_open))
            return NONE;

        if (istype(tool, typeof(obj_item_disk_computer)))
        {
            await eject_disk(user);
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                balloon_alert(user, I18n.T("it's stuck to your hand!"));
                return ITEM_INTERACT_BLOCKING;
            }
            inserted_disk = tool as obj_item_disk_computer;
            return ITEM_INTERACT_SUCCESS;
        }

        if (!istype(tool, typeof(obj_item_tank)))
            return NONE;

        if (inserted_tank != null)
        {
            if (!Dm.truthy(await eject_tank(user)))
            {
                balloon_alert(user, I18n.T("it's stuck inside!"));
                return ITEM_INTERACT_BLOCKING;
            }
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            balloon_alert(user, I18n.T("it's stuck to your hand!"));
            return ITEM_INTERACT_BLOCKING;
        }

        inserted_tank = tool as obj_item_tank;
        last_recorded_pressure = 0;
        RegisterSignal(inserted_tank, COMSIG_QDELETING, nameof(tank_destruction));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:75
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(active) || inserted_tank != null)
            return FALSE;
        if (!Dm.truthy(await default_change_direction_wrench(user, tool)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:82
    public override async DmTask<object> default_change_direction_wrench(mob user = null, obj_item I = null)
    {
        if (!Dm.truthy(await base.default_change_direction_wrench(user, I)))
            return FALSE;
        set_init_directions();
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:89
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(active) || inserted_tank != null)
            return NONE;

        object dot = default_deconstruction_screwdriver(user, tool);
        change_nodes_connection(panel_open);
        return dot;
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:97
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(active) || inserted_tank != null)
            return NONE;

        return await default_deconstruction_crowbar(user, tool);
    }

    /// Glorified volume pump.
    // tg: code/modules/research/ordnance/tank_compressor.dm:104
    public override object process_atmos(object seconds_per_tick = null)
    {
        return Waiting.WaitforFalse(process_atmos__body(), null); // the slot's set waitfor = FALSE (_machinery.dm:322)
    }

    private async DmTask<object> process_atmos__body()
    {
        datum_gas_mixture input_air = airs[2] as datum_gas_mixture;
        if (!Dm.truthy(input_air?.total_moles()) || !Dm.truthy(active) || !Dm.truthy(transfer_rate) || inserted_tank == null)
            return null;

        datum_gas_mixture tank_air = await inserted_tank.return_air() as datum_gas_mixture;
        if (tank_air == null)
            return null;

        if (Dm.num(input_air.return_pressure()) < 0.01f || Dm.num(tank_air.return_pressure()) > TANK_COMPRESSOR_PRESSURE_LIMIT)
            return null;

        /// Prevent pumping if tank is taking damage but still below pressure limit. Here to prevent exploiting the buffer system.
        if (Dm.truthy(inserted_tank.leaking) && (Dm.num(tank_air.return_pressure()) <= TANK_LEAK_PRESSURE))
        {
            active = FALSE;
            return null;
        }

        datum_gas_mixture removed = input_air.remove_ratio(transfer_rate / Dm.num(input_air.volume)) as datum_gas_mixture;
        if (removed == null)
            return null;

        tank_air.merge(removed);
        update_parents();
        return null;
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:128
    public override async DmTask<object> assume_air(datum_gas_mixture giver = null)
    {
        if (leaked_gas_buffer == null)
            return await base.assume_air(giver);
        leaked_gas_buffer.merge(giver);
        return TRUE;
    }

    /// Recording of last pressure of the tank. Ran when a tank is about to explode or disintegrate. We dont care about last pressure if the tank is ejected.
    // tg: code/modules/research/ordnance/tank_compressor.dm:135
    [SignalHandler]
    public virtual void tank_destruction()
    {
        if (Dm.num(inserted_tank.get_integrity()) > 0)
            return;
        Waiting.NoSleep(flush_buffer());
        datum_gas_mixture tank_air = Waiting.NoSleep(inserted_tank.return_air()) as datum_gas_mixture;
        last_recorded_pressure = Dm.num(tank_air.return_pressure());
        active = FALSE;
        return;
    }

    /// Use this to absorb explosions.
    // tg: code/modules/research/ordnance/tank_compressor.dm:146
    [SignalHandler]
    public virtual object explosion_handle(atom source = null, DmList arguments = null)
    {
        Waiting.NoSleep(say(I18n.T("Internal explosion detected and absorbed.")));
        SSexplosions.shake_the_room(get_turf(this), 1, 8, 0.5f, 0.25f, FALSE);
        return COMSIG_CANCEL_EXPLOSION;
    }

    /**
     * Everytime a tank is destroyed or a new tank is inserted, our buffer is flushed.
     * Mole requirements in experiments are tracked by buffer data.
     */
    // tg: code/modules/research/ordnance/tank_compressor.dm:156
    public virtual async DmTask flush_buffer()
    {
        if (!Dm.truthy(leaked_gas_buffer.total_moles()))
            return;
        if (Dm.num(leaked_gas_buffer.total_moles()) > SIGNIFICANT_AMOUNT_OF_MOLES)
            await record_data();
        else
            await say(I18n.T("Buffer data discarded. Required moles for storage: [] moles.", SIGNIFICANT_AMOUNT_OF_MOLES));
        datum_gas_mixture removed = leaked_gas_buffer.remove_ratio(1) as datum_gas_mixture;
        (airs[1] as datum_gas_mixture).merge(removed);
        await say(I18n.T("Gas stored in buffer flushed to output port. Compressor ready to start the next experiment."));
    }

    /// This proc should be called whenever we want to store our buffer data.
    // tg: code/modules/research/ordnance/tank_compressor.dm:168
    public virtual async DmTask record_data()
    {
        datum_data_compressor_record new_record = Dm.@new<datum_data_compressor_record>();
        new_record.name = I18n.T("Log Recording #[]", record_number);
        new_record.experiment_source = inserted_tank.name;
        new_record.timestamp = Dm.text("[] (PT: [])", server_timestamp(ic_time: TRUE), round_timestamp());
        foreach ((object gas_path, object amount) in Dm.pairs(leaked_gas_buffer.moles))
            new_record.gas_data[gas_path] = amount;

        Dm.append(ref compressor_record, new_record);
        record_number += 1;
        await say(I18n.T("Buffer data stored."));
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:180
    public virtual object apply_experiments(datum_data_compressor_record record = null)
    {
        DmList passed_experiments = Dm.list();
        DmList gas_data = record.gas_data;

        foreach (datum_experiment_ordnance_gaseous experiment in Dm.each<datum_experiment_ordnance_gaseous>(SSresearch.ordnance_experiments))
        {
            if (Dm.@in(experiment.required_gas, gas_data))
            {
                if (Dm.num(gas_data[experiment.required_gas]) > MINIMUM_MOLE_COUNT)
                    Dm.append(ref passed_experiments, Dm.list(Dm.kv(experiment.type, gas_data[experiment.required_gas])));
            }
        }

        return passed_experiments;
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:191
    public virtual void print(mob user = null, datum_data_compressor_record record = null)
    {
        if (record == null || inserted_disk == null)
            return;

        datum_computer_file_data_ordnance_gaseous record_data = Dm.@new<datum_computer_file_data_ordnance_gaseous>();
        record_data.filename = Dm.text("Gas Compressor []", record.name); //Gas Compressor Log Recording #x
        record_data.gas_record = record;
        record_data.possible_experiments = apply_experiments(record) as DmList;

        if (Dm.truthy(inserted_disk.add_file(record_data)))
            playsound(this, Dm.resource("sound/machines/ping.ogg"), 25);
        else
            playsound(this, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 25);
    }

    /// Ejecting a tank. Also called on insertion to clear previous tanks.
    // tg: code/modules/research/ordnance/tank_compressor.dm:206
    public virtual async DmTask<object> eject_tank(mob user = null)
    {
        if (inserted_tank == null)
            return FALSE;
        datum_gas_mixture tank_air = await inserted_tank.return_air() as datum_gas_mixture;
        if (Dm.num(tank_air.return_pressure()) >= (PUMP_MAX_PRESSURE + ONE_ATMOSPHERE))
            return FALSE;
        await flush_buffer();
        if (user != null)
            await user.put_in_hands(inserted_tank);
        else
            await inserted_tank.forceMove(drop_location() as atom);
        active = FALSE;
        return TRUE;
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:220
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
    // tg: code/modules/research/ordnance/tank_compressor.dm:231
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == inserted_disk)
            inserted_disk = null;
        if (gone == inserted_tank)
        {
            UnregisterSignal(inserted_tank, COMSIG_QDELETING);
            inserted_tank = null;
            update_appearance();
        }
        return await base.Exited(gone, direction);
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:240
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        await eject_tank();
        await eject_disk();
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:245
    public override object Destroy(float? force = null)
    {
        inserted_tank = null;
        inserted_disk = null;
        leaked_gas_buffer = null;
        compressor_record.Cut(); // We only want to clear the list itself, not delete its contents.
        return base.Destroy(force);
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:252
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (istype(inserted_tank, typeof(obj_item_tank)))
            icon_state = Dm.text("[]-closed", base_icon_state);
        else
            icon_state = Dm.text("[]-open", base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:259
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, get_pipe_image(icon, Dm.text("[]-pipe", base_icon_state), dir, COLOR_VIBRANT_LIME, piping_layer));
        Dm.append(ref dot, get_pipe_image(icon, Dm.text("[]-pipe", base_icon_state), REVERSE_DIR(dir), COLOR_RED, piping_layer));
        if (!istype(inserted_tank, typeof(obj_item_tank)))
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-doors", base_icon_state)));
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-maintenance", base_icon_state)));
        else
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]-cables", base_icon_state)));
        return dot;
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:270
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "TankCompressor");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:277
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.eq(action, "change_rate"))
            transfer_rate = clamp(Dm.num(@params["target"]), 0, TANK_COMPRESSOR_MAX_TRANSFER_RATE);
        else if (Dm.eq(action, "toggle_injection"))
            active = Dm.not(active);
        else if (Dm.eq(action, "eject_tank"))
            await eject_tank(usr);
        else if (Dm.eq(action, "eject_disk"))
            await eject_disk(usr);
        else if (Dm.eq(action, "delete_record"))
        {
            datum_data_compressor_record record = Dm.locate_ref(@params["ref"]) is datum_data_compressor_record found && Dm.@in(found, compressor_record) ? found : null;
            if (compressor_record == null || !Dm.@in(record, compressor_record))
                return dot;
            Dm.remove(ref compressor_record, record);
            return TRUE;
        }
        else if (Dm.eq(action, "save_record"))
        {
            datum_data_compressor_record record = Dm.locate_ref(@params["ref"]) is datum_data_compressor_record found && Dm.@in(found, compressor_record) ? found : null;
            if (compressor_record == null || !Dm.@in(record, compressor_record))
                return dot;
            print(usr, record);
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:303
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list(
            Dm.kv("maxTransfer", TANK_COMPRESSOR_MAX_TRANSFER_RATE),
            Dm.kv("leakPressure", round(TANK_LEAK_PRESSURE)),
            Dm.kv("fragmentPressure", round(TANK_FRAGMENT_PRESSURE)),
            Dm.kv("ejectPressure", PUMP_MAX_PRESSURE + ONE_ATMOSPHERE)
        );
        return data;
    }

    // tg: code/modules/research/ordnance/tank_compressor.dm:312
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["tankPresent"] = inserted_tank != null ? TRUE : FALSE;
        datum_gas_mixture tank_air = inserted_tank == null ? null : await inserted_tank.return_air() as datum_gas_mixture;
        data["tankPressure"] = tank_air?.return_pressure();
        data["leaking"] = inserted_tank?.leaking;

        data["active"] = active;
        data["transferRate"] = transfer_rate;
        data["lastPressure"] = last_recorded_pressure;

        data["disk"] = inserted_disk?.name;
        data["storage"] = Dm.text("[] / [] GQ", inserted_disk?.used_capacity, inserted_disk?.max_capacity);
        data["records"] = Dm.list();
        foreach (datum_data_compressor_record record in Dm.each<datum_data_compressor_record>(compressor_record))
        {
            DmList single_record_data = Dm.list(
                Dm.kv("ref", REF(record)),
                Dm.kv("name", record.name),
                Dm.kv("source", record.experiment_source),
                Dm.kv("timestamp", record.timestamp),
                Dm.kv("gases", Dm.list())
            );
            foreach (object path in Dm.each(record.gas_data))
            {
                Type gas_path = path as Type;
                Dm.append(single_record_data["gases"] as DmList, Dm.list(Dm.kv(Dm.initial(gas_path, (datum_gas p) => p.name), record.gas_data[gas_path])));
            }
            Dm.append(data["records"] as DmList, Dm.list(single_record_data));
        }
        return data;
    }
}
