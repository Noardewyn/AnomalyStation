using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The ordnance lab in toy rooms (research/ordnance/, programs/frontier.dm, machinery/mass_driver.dm, computer/pod.dm): the tank
/// compressor fills an inserted tank from its input port, keeps what the tank leaks and records it as a gas record when the tank
/// comes out, prints the record to a disk with the gas experiment it can complete, and absorbs a blast inside it; the doppler array
/// records a transfer valve's blast in front of it (with the reactions of the exploding tank) and none behind it or unpowered, and
/// prints it; the NT Frontier app writes a paper from a record, publishes it to the science techweb, which completes the ordnance
/// experiment and pays the science budget, and refuses the same record twice; the mass driver throws what lies on it, by its launch
/// control (the pod doors open first), its launch wire and its button.
/// </summary>
public class OrdnanceTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int width = 13, int height = 9)
    {
        var r = new AudioRoom(width, height, seed: seed);
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE); // the toy room has no APC
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who, T item) where T : obj_item
    {
        r.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(who.held_items).ToList())
                Waiting.NoSleep(who.dropItemToGround(held));
            Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(item))));
        });
        r.Step(2);
        return item;
    }

    private static void EmptyHands(AudioRoom r, mob_living_carbon_human who)
    {
        r.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(who.held_items).ToList())
                Waiting.NoSleep(who.dropItemToGround(held));
        });
        r.Step(2);
    }

    private static JsonElement Open(AudioRoom r, mob_living_carbon_human who, atom machine, out string windowId, bool full = true)
    {
        EmptyHands(r, who);
        Take(who);
        r.Step(Second);
        Click(r, who, machine);
        r.Step(4);
        return full ? LastFullUpdate(Take(who), out windowId) : LastUpdate(Take(who), out windowId);
    }

    private static datum_gas_mixture Air(AudioRoom r, atom holder) => r.In(() => Waiting.NoSleep(holder.return_air()) as datum_gas_mixture);

    private static void Fill(datum_gas_mixture mix, Type gas, float moles, float temperature)
    {
        mix.assert_gases(gas);
        mix.moles[gas] = moles;
        mix.temperature = temperature;
    }

    [Fact]
    public void TheCompressorFillsATankAndRecordsWhatItLeaks()
    {
        var (r, feed, bob) = Room(14401);
        var compressor = r.In(() => Dm.@new<obj_machinery_atmospherics_components_binary_tank_compressor>(locate(5, 4, 1)));
        r.Step(2);
        Assert.Equal("tank_compressor-open", r.In(() => Dm.str(compressor.icon_state)));
        r.In(() => Fill(compressor.airs[2] as datum_gas_mixture, typeof(datum_gas_plasma), 30, T20C));

        // a tank goes in: the doors close
        var tank = Hold(r, bob, r.In(() => Dm.@new<obj_item_tank_internals_plasma>(bob.loc)));
        Click(r, bob, compressor);
        r.Step(2);
        Assert.Same(tank, r.In(() => compressor.inserted_tank));
        Assert.Equal("tank_compressor-closed", r.In(() => Dm.str(compressor.icon_state)));
        float before = r.In(() => Dm.num(Air(r, tank).return_pressure()));

        // the window: the tank and the switch
        JsonElement data = Open(r, bob, compressor, out string window);
        Assert.Equal("TankCompressor", InterfaceOf(data));
        Assert.Equal(1, data.GetProperty("data").GetProperty("tankPresent").GetInt32());
        Assert.Equal(20, data.GetProperty("static_data").GetProperty("maxTransfer").GetInt32());
        Act(r, bob, window, "toggle_injection");
        Assert.True(r.In(() => Dm.truthy(compressor.active)));
        r.Step(3 * Second);
        float after = r.In(() => Dm.num(Air(r, tank).return_pressure()));
        Assert.True(after > before + 100, $"the tank was not filled: {before} -> {after} kPa");

        // what the tank leaks goes to the buffer, and comes out as a record when the tank does
        r.In(() =>
        {
            var leak = Dm.@new<datum_gas_mixture>(70);
            Fill(leak, typeof(datum_gas_plasma), 250, T20C);
            Waiting.NoSleep(compressor.assume_air(leak));
        });
        Act(r, bob, window, "eject_tank");
        Assert.Null(r.In(() => compressor.inserted_tank));
        Assert.Same(bob, r.In(() => tank.loc));
        Assert.True(feed.Any("Buffer data stored."));
        Assert.True(feed.Any("Gas stored in buffer flushed to output port."));
        Assert.Equal(1, r.In(() => Dm.length(compressor.compressor_record)));
        var record = r.In(() => compressor.compressor_record[1] as datum_data_compressor_record);
        Assert.Equal(250f, r.In(() => Dm.num(record.gas_data[typeof(datum_gas_plasma)])), 1);
        Assert.Equal("Log Recording #1", r.In(() => Dm.str(record.name)));
        Assert.Equal(250f, r.In(() => Dm.num((compressor.airs[1] as datum_gas_mixture).moles[typeof(datum_gas_plasma)])), 1);

        // a small leak is discarded
        r.In(() =>
        {
            var leak = Dm.@new<datum_gas_mixture>(70);
            Fill(leak, typeof(datum_gas_plasma), 5, T20C);
            Waiting.NoSleep(compressor.assume_air(leak));
            Waiting.NoSleep(compressor.flush_buffer());
        });
        Assert.True(feed.Any("Buffer data discarded. Required moles for storage: 10 moles."));
        Assert.Equal(1, r.In(() => Dm.length(compressor.compressor_record)));

        // a disk takes the record with the plasma gas experiment it completes
        var disk = Hold(r, bob, r.In(() => Dm.@new<obj_item_disk_computer>(bob.loc)));
        Click(r, bob, compressor);
        r.Step(2);
        Assert.Same(disk, r.In(() => compressor.inserted_disk));
        data = Open(r, bob, compressor, out window, full: false);
        JsonElement records = data.GetProperty("data").GetProperty("records");
        Assert.Equal(1, records.GetArrayLength());
        Assert.Equal(250, records[0].GetProperty("gases").GetProperty("Plasma").GetDouble(), 1);
        Act(r, bob, window, "save_record", ("ref", records[0].GetProperty("ref").GetString()));
        var file = r.In(() => Dm.each<datum_computer_file_data_ordnance_gaseous>(disk.stored_files).Single());
        Assert.Equal("Gas Compressor Log Recording #1", r.In(() => Dm.str(file.filename)));
        Assert.Same(record, r.In(() => file.return_data()));
        Assert.Equal(250f, r.In(() => Dm.num(file.possible_experiments[typeof(datum_experiment_ordnance_gaseous_plasma)])), 1);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/ping.ogg");

        Act(r, bob, window, "delete_record", ("ref", records[0].GetProperty("ref").GetString()));
        Assert.Equal(0, r.In(() => Dm.length(compressor.compressor_record)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCompressorAbsorbsABlastInsideIt()
    {
        var (r, feed, bob) = Room(14402);
        var compressor = r.In(() => Dm.@new<obj_machinery_atmospherics_components_binary_tank_compressor>(locate(6, 4, 1)));
        var tank = r.In(() => Dm.@new<obj_item_tank_internals_plasma>(compressor));
        r.In(() => compressor.inserted_tank = tank);
        r.Step(2);
        int bobHealth = r.In(() => (int)Dm.num(bob.health));
        r.In(() => Waiting.NoSleep(explosion(tank, devastation_range: 1, heavy_impact_range: 2, light_impact_range: 3, explosion_cause: tank)));
        r.Step(Second);
        Assert.True(feed.Any("Internal explosion detected and absorbed."));
        Assert.False(r.In(() => QDELETED(compressor)));
        Assert.Equal(bobHealth, r.In(() => (int)Dm.num(bob.health)));
        r.AssertNoRuntimes();
    }

    // A transfer valve whose tanks hold a hot plasma mix and oxygen: open, the merged tank bursts within a few seconds.
    private static obj_item_transfer_valve Bomb(AudioRoom r, turf where)
    {
        return r.In(() =>
        {
            var valve = Dm.@new<obj_item_transfer_valve>(where);
            var one = Dm.@new<obj_item_tank_internals_plasma>(valve);
            var two = Dm.@new<obj_item_tank_internals_oxygen>(valve);
            Fill(Waiting.NoSleep(one.return_air()) as datum_gas_mixture, typeof(datum_gas_plasma), 120, 1500);
            Fill(Waiting.NoSleep(two.return_air()) as datum_gas_mixture, typeof(datum_gas_oxygen), 240, T20C);
            valve.tank_one = one;
            valve.tank_two = two;
            valve.update_appearance();
            return valve;
        });
    }

    [Fact]
    public void TheDopplerArrayRecordsATransferValveBlastInFrontOfIt()
    {
        var (r, feed, bob) = Room(14403, width: 45, height: 9);
        var doppler = r.In(() => Dm.@new<obj_machinery_doppler_array>(locate(5, 4, 1)));
        var behind = r.In(() => Dm.@new<obj_machinery_doppler_array>(locate(6, 6, 1)));
        r.In(() =>
        {
            Waiting.NoSleep(doppler.setDir(EAST));
            Waiting.NoSleep(behind.setDir(WEST));
        });
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(doppler.light_on)), "the powered array's light is on");
        r.In(() => doppler.next_announce = 0);
        var valve = Bomb(r, r.In(() => locate(40, 4, 1)));
        r.In(() => Waiting.NoSleep(valve.toggle_valve()));
        bool recorded = false;
        for (int i = 0; i < 20 * Second && !recorded; i++)
        {
            r.Step(1);
            recorded = r.In(() => Dm.length(doppler.records) > 0);
        }
        Assert.True(recorded, "the doppler array recorded no blast");
        var record = r.In(() => doppler.records[1] as datum_data_tachyon_record);
        Assert.True(feed.Any("Explosive disturbance detected."));
        Assert.True(feed.Any("Epicenter at: grid (40, 4)."));
        Assert.Equal("40, 4", r.In(() => record.coordinates));
        Assert.True(r.In(() => Dm.num(record.factual_radius["shockwave_radius"])) > 0);
        DmList reactions = r.In(() => record.reaction_results[TANK_RESULTS_REACTION] as DmList);
        Assert.NotNull(reactions);
        Assert.Equal(0, r.In(() => Dm.length(behind.records)));

        // its window lists the record with the exploding tank's reactions
        JsonElement data = Open(r, bob, doppler, out string window);
        Assert.Equal("DopplerArray", InterfaceOf(data));
        JsonElement entry = data.GetProperty("data").GetProperty("records")[0];
        Assert.Equal("Log Recording #1", entry.GetProperty("name").GetString());
        Assert.Equal("40, 4", entry.GetProperty("coordinates").GetString());

        // a disk takes it with the explosive experiments its blast fits
        var disk = Hold(r, bob, r.In(() => Dm.@new<obj_item_disk_computer>(bob.loc)));
        Click(r, bob, doppler);
        r.Step(2);
        Assert.Same(disk, r.In(() => doppler.inserted_disk));
        Act(r, bob, window, "save_record", ("ref", entry.GetProperty("ref").GetString()));
        var file = r.In(() => Dm.each<datum_computer_file_data_ordnance_explosive>(disk.stored_files).Single());
        Assert.Equal("Doppler Array Log Recording #1", r.In(() => Dm.str(file.filename)));
        Assert.Same(record, r.In(() => file.return_data()));
        Assert.Contains(typeof(datum_experiment_ordnance_explosive_lowyieldbomb), r.In(() => Dm.each(file.possible_experiments).ToList()));
        Act(r, bob, window, "eject_disk");
        Assert.Same(bob, r.In(() => disk.loc));

        // unpowered, it records nothing and its light goes out
        r.In(() =>
        {
            doppler.next_announce = 0;
            Waiting.NoSleep(doppler.set_machine_stat(Dm.bitor(doppler.machine_stat, NOPOWER)));
            Assert.False(Dm.truthy(Dm.num(doppler.sense_explosion(SSdcs, locate(40, 4, 1), 1, 2, 3, 0, 1, 2, 3, null, 99))));
        });
        Assert.Equal(1, r.In(() => Dm.length(doppler.records)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACappedBlastIsRecordedWithItsTheoreticalRadii()
    {
        var (r, feed, bob) = Room(14404, width: 30);
        var doppler = r.In(() => Dm.@new<obj_machinery_doppler_array>(locate(5, 4, 1)));
        r.In(() => { Waiting.NoSleep(doppler.setDir(EAST)); });
        r.Step(2);
        r.In(() => Assert.Equal(TRUE, doppler.sense_explosion(SSdcs, locate(20, 4, 1), 3, 7, 14, 1.5f, 10, 20, 40, null, 7)));
        var record = r.In(() => doppler.records[1] as datum_data_tachyon_record);
        Assert.Equal(40f, r.In(() => Dm.num(record.theory_radius["shockwave_radius"])));
        Assert.True(feed.Any("Theoretical: Epicenter radius: 10. Outer radius: 20. Shockwave radius: 40."));
        // without a tank's reactions only the experiments that take any source fit, scored by the theoretical shockwave
        DmList passed = r.In(() => doppler.apply_experiments(record) as DmList);
        Assert.Equal(40f, r.In(() => Dm.num(passed[typeof(datum_experiment_ordnance_explosive_lowyieldbomb)])));
        Assert.False(r.In(() => Dm.@in(typeof(datum_experiment_ordnance_explosive_hydrogenbomb), passed)));
        // the cooldown holds the next one back
        r.In(() => Assert.Equal(FALSE, doppler.sense_explosion(SSdcs, locate(20, 4, 1), 1, 1, 1, 0, 1, 1, 1, null, 8)));
        r.AssertNoRuntimes();
    }

    private static object AppAct(AudioRoom r, mob user, datum_computer_file_program program, string action, DmList @params = null) => r.In(() =>
    {
        World w = World.Current;
        object caller = w.usr;
        w.usr = user;
        try
        {
            return Waiting.NoSleep(program.ui_act(action, @params ?? Dm.list(), null));
        }
        finally
        {
            w.usr = caller;
        }
    });

    [Fact]
    public void APublishedPaperCompletesItsExperimentAndPaysTheScienceBudget()
    {
        var (r, feed, bob) = Room(14405);
        datum_techweb web = r.In(() => Dm.each<datum_techweb>(SSresearch.techwebs).OfType<datum_techweb_science>().First());
        var pda = r.In(() => Dm.@new<obj_item_modular_computer_pda_crew_assistant>(bob.loc));
        var app = r.In(() =>
        {
            var made = Dm.@new<datum_computer_file_program_scipaper_program>();
            Assert.True(Dm.truthy(pda.store_file(made)));
            return made;
        });
        Assert.Same(web, r.In(() => app.linked_techweb));

        // a compressor's record of 250 moles of plasma, on the computer
        var record = r.In(() =>
        {
            var rec = Dm.@new<datum_data_compressor_record>();
            rec.name = "Log Recording #1";
            rec.gas_data[typeof(datum_gas_plasma)] = 250;
            var file = Dm.@new<datum_computer_file_data_ordnance_gaseous>();
            file.filename = "Gas Compressor Log Recording #1";
            file.gas_record = rec;
            file.possible_experiments = Dm.list(Dm.kv(typeof(datum_experiment_ordnance_gaseous_plasma), 250));
            Assert.True(Dm.truthy(pda.store_file(file)));
            return file;
        });
        var budget = r.In(() => SSeconomy.get_dep_account(ACCOUNT_SCI) as datum_bank_account);
        float money = r.In(() => Dm.num(budget.account_balance));

        DmList data = r.In(() => Waiting.NoSleep(app.ui_data(bob)) as DmList);
        Assert.Equal(record.uid, r.In(() => (data["fileList"] as DmList)[record.filename]));
        AppAct(r, bob, app, "select_file", Dm.list(Dm.kv("selected_uid", record.uid)));
        Assert.Same(record, r.In(() => app.selected_file));
        AppAct(r, bob, app, "select_experiment", Dm.list(Dm.kv("selected_expath", "/datum/experiment/ordnance/gaseous/plasma")));
        Assert.IsType<datum_scientific_paper_gaseous>(r.In(() => app.paper_to_be));
        Assert.Equal(typeof(datum_experiment_ordnance_gaseous_plasma), r.In(() => app.paper_to_be.experiment_path));
        DmList tiers = r.In(() => app.paper_to_be.calculate_tier() as DmList);
        Assert.Equal(new object[] { 1f, 2f }, r.In(() => Dm.each(tiers).Select(t => (object)Dm.num(t)).ToArray()));
        AppAct(r, bob, app, "select_tier", Dm.list(Dm.kv("selected_tier", 1)));
        AppAct(r, bob, app, "select_partner", Dm.list(Dm.kv("selected_partner", "/datum/scientific_partner/mining")));
        Assert.Equal(typeof(datum_scientific_partner_mining), r.In(() => app.paper_to_be.partner_path));
        // f(250) = 20 / (1 + e^(-ln(19)/200 * 50)): about 13.5, times the Mining Corps' 0.75
        float gain = r.In(() => Dm.num(app.paper_to_be.gains[SCIPAPER_COOPERATION_INDEX]));
        Assert.Equal(20f / (1 + MathF.Exp(-MathF.Log(19) / 200 * 50)) * 0.75f, gain, 2);
        AppAct(r, bob, app, "rewrite", Dm.list(Dm.kv("title", "On the burning of plasma")));
        var paper = r.In(() => app.paper_to_be);

        AppAct(r, bob, app, "publish");
        Assert.True(feed.Any("has been published!"));
        Assert.Same(paper, r.In(() => (web.published_papers[typeof(datum_experiment_ordnance_gaseous_plasma)] as DmList)[1]));
        Assert.True(r.In(() => Dm.@in(typeof(datum_experiment_ordnance_gaseous_plasma), web.completed_experiments)), "the plasma experiment is complete");
        Assert.Equal(gain, r.In(() => Dm.num(web.scientific_cooperation[typeof(datum_scientific_partner_mining)])), 2);
        Assert.Equal(money + gain * SCIPAPER_GAIN_TO_MONEY, r.In(() => Dm.num(budget.account_balance)), 0.5f);
        Assert.Null(r.In(() => app.selected_file));
        Assert.Equal("Unknown", r.In(() => Dm.str(paper.author)));

        // the published paper is listed; the same record cannot be published again
        AppAct(r, bob, app, "change_tab", Dm.list(Dm.kv("new_tab", 2)));
        data = r.In(() => Waiting.NoSleep(app.ui_data(bob)) as DmList);
        Assert.Equal(1, r.In(() => Dm.length(data["publishedPapers"])));
        AppAct(r, bob, app, "change_tab", Dm.list(Dm.kv("new_tab", 1)));
        AppAct(r, bob, app, "select_file", Dm.list(Dm.kv("selected_uid", record.uid)));
        AppAct(r, bob, app, "select_experiment", Dm.list(Dm.kv("selected_expath", "/datum/experiment/ordnance/gaseous/plasma")));
        AppAct(r, bob, app, "select_tier", Dm.list(Dm.kv("selected_tier", 1)));
        AppAct(r, bob, app, "select_partner", Dm.list(Dm.kv("selected_partner", "/datum/scientific_partner/mining")));
        Assert.False(r.In(() => Dm.truthy(app.paper_to_be.allowed_to_publish(web))));
        AppAct(r, bob, app, "publish");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/terminal/terminal_error.ogg");

        // the partners' tab
        AppAct(r, bob, app, "change_tab", Dm.list(Dm.kv("new_tab", 4)));
        data = r.In(() => Waiting.NoSleep(app.ui_data(bob)) as DmList);
        Assert.Equal("Negligible", r.In(() => Dm.str((data["relations"] as DmList)[typeof(datum_scientific_partner_mining)])));
        r.AssertNoRuntimes();
    }

    private static obj_item Crate(AudioRoom r, int x, int y) => r.In(() => Dm.@new<obj_item_toy_crayon_red>(locate(x, y, 1)) as obj_item);

    [Fact]
    public void TheMassDriverThrowsWhatLiesOnItByItsControlItsWireAndItsButton()
    {
        var (r, feed, bob) = Room(14406, width: 30);
        var driver = r.In(() => Dm.@new<obj_machinery_mass_driver_ordnance>(locate(6, 6, 1)));
        var door = r.In(() => Dm.@new<obj_machinery_door_poddoor>(locate(8, 6, 1)));
        r.In(() =>
        {
            Waiting.NoSleep(driver.setDir(EAST));
            door.id = MASSDRIVER_ORDNANCE;
        });
        var control = r.In(() => Dm.@new<obj_machinery_computer_pod_old_mass_driver_controller_ordnancedriver>(locate(5, 5, 1)));
        r.Step(2);
        Assert.Same(driver, r.In(() => control.connected));
        Assert.True(r.In(() => Dm.truthy(door.density)));

        // the window: launch opens the door, fires two seconds later
        var toy = Crate(r, 6, 6);
        r.Step(2);
        JsonElement data = Open(r, bob, control, out string window);
        Assert.Equal("MassDriverControl", InterfaceOf(data));
        Assert.Equal(1, data.GetProperty("data").GetProperty("connected").GetInt32());
        Assert.Equal(1, data.GetProperty("data").GetProperty("poddoor").GetInt32());
        Act(r, bob, window, "set_power", ("power", "2"));
        Assert.Equal(2f, r.In(() => Dm.num(driver.power)));
        Act(r, bob, window, "launch");
        r.Step(Second);
        Assert.False(r.In(() => Dm.truthy(door.density)), "the pod door opened");
        var path = new List<string>();
        for (int i = 0; i < 4 * Second; i++)
        {
            r.Step(1);
            path.Add(r.In(() => Dm.str(toy.x) + (Dm.truthy(door.density) ? "d" : "") + (toy.throwing != null ? "t" : "")));
        }
        Assert.True(r.In(() => toy.x) > 6 + 3, $"the toy was not thrown: {string.Join(" ", path)}");
        r.Step(6 * Second);
        Assert.True(r.In(() => Dm.truthy(door.density)), "the pod door closed again");

        // a pulse on the launch wire fires it too
        r.In(() => Waiting.Detach(door.open()));
        r.Step(2 * Second);
        var toy2 = Crate(r, 6, 6);
        r.In(() => Waiting.NoSleep(driver.wires.on_pulse(WIRE_LAUNCH)));
        r.Step(2 * Second);
        Assert.True(r.In(() => toy2.x) > 6 + 3);
        Assert.True(feed.Any("The drive mechanism activates."));

        // the safeties: pulsed they triple the power, cut they put it back
        r.In(() => Waiting.NoSleep(driver.wires.on_pulse(WIRE_SAFETIES)));
        Assert.Equal(3f, r.In(() => Dm.num(driver.power)));
        r.In(() => Waiting.NoSleep(driver.wires.on_cut(WIRE_SAFETIES)));
        Assert.Equal(1f, r.In(() => Dm.num(driver.power)));

        // the button's controller: doors, driver, doors
        var toy3 = Crate(r, 6, 6);
        var button = r.In(() => Dm.@new<obj_item_assembly_control_massdriver>(locate(4, 6, 1)));
        r.In(() => button.id = MASSDRIVER_ORDNANCE);
        r.In(() => Waiting.NoSleep(button.activate(bob)));
        r.Step(2 * Second);
        Assert.False(r.In(() => Dm.truthy(door.density)));
        Assert.True(r.In(() => toy3.x) > 6 + 3);
        r.Step(8 * Second);
        Assert.True(r.In(() => Dm.truthy(door.density)));
        Assert.False(r.In(() => Dm.truthy(button.cooldown)));

        // the launch flick and the open panel's state
        r.In(() =>
        {
            driver.panel_open = TRUE;
            driver.update_appearance();
        });
        Assert.Equal("mass_driver_o", r.In(() => Dm.str(driver.icon_state)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryNewlyLiveOrdnanceTypeIsCreatedAndDeleted()
    {
        var (r, feed, bob) = Room(14407);
        Type[] types =
        {
            typeof(obj_machinery_atmospherics_components_binary_tank_compressor), typeof(obj_machinery_doppler_array), typeof(obj_machinery_mass_driver),
            typeof(obj_machinery_mass_driver_chapelgun), typeof(obj_machinery_mass_driver_ordnance), typeof(obj_machinery_mass_driver_trash),
            typeof(obj_machinery_mass_driver_shack), typeof(obj_machinery_computer_pod), typeof(obj_machinery_computer_pod_old),
            typeof(obj_machinery_computer_pod_old_mass_driver_controller), typeof(obj_machinery_computer_pod_old_mass_driver_controller_ordnancedriver),
            typeof(obj_machinery_computer_pod_old_mass_driver_controller_ordnancedriver_longrange), typeof(obj_machinery_computer_pod_old_mass_driver_controller_chapelgun),
            typeof(obj_machinery_computer_pod_old_mass_driver_controller_trash), typeof(obj_machinery_computer_pod_old_mass_driver_controller_shack),
            typeof(obj_machinery_computer_pod_old_syndicate), typeof(obj_machinery_computer_pod_old_swf), typeof(obj_machinery_button_massdriver),
            typeof(datum_scientific_paper), typeof(datum_scientific_paper_explosive), typeof(datum_scientific_paper_gaseous),
            typeof(datum_computer_file_data_ordnance), typeof(datum_computer_file_data_ordnance_explosive), typeof(datum_computer_file_data_ordnance_gaseous),
            typeof(datum_computer_file_program_scipaper_program), typeof(datum_wires_mass_driver),
        };
        foreach (Type type in types)
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        foreach (Type type in types.Where(t => t != typeof(datum_wires_mass_driver)))
        {
            var thing = r.In(() => Dm.@new(type, locate(8, 6, 1)) as datum);
            r.Step(2);
            r.In(() => qdel(thing));
            r.Step(2);
        }
        r.AssertNoRuntimes();
    }
}
