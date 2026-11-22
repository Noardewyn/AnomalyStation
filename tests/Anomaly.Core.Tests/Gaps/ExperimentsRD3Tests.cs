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
/// Gaps package RD3 (D333, the experiments) in a booted toy room with Bob as a player: the experi-scanner links the station's techweb,
/// scans a bluespace crystal for its experiment (the node it discounts gets cheaper) and tracks a meat wall that an emitter's beam then
/// cooks; its ExperimentConfigure window lists the web's servers and selects an experiment; the destructive scanner takes a harvested
/// plant into its tube, scans it and destroys it for a destructive experiment; the destructive analyzer takes an item, shows it in its
/// window and destroys it to reveal a hidden node, or for research points; the operating computer catalogues an autopsy.
/// </summary>
public class ExperimentsRD3Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob, datum_techweb Web) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        datum_techweb web = r.In(() => Dm.each<datum_techweb>(SSresearch.techwebs).OfType<datum_techweb_science>().FirstOrDefault());
        Assert.NotNull(web);
        return (r, feed, bob, web);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target)
    {
        r.Step(Second);
        Click(r, who, target);
    }

    private static datum_component_experiment_handler Handler(AudioRoom r, datum device)
        => r.In(() => device.GetComponent(typeof(datum_component_experiment_handler)) as datum_component_experiment_handler);

    /// The experiment of a type on the web, added to its available experiments when the web has not unlocked it yet.
    private static T Experiment<T>(AudioRoom r, datum_techweb web) where T : datum_experiment
        => r.In(() =>
        {
            web.add_experiment(typeof(T));
            return Dm.each<datum_experiment>(web.available_experiments).OfType<T>().Single();
        });

    /// An R&D server of the web: the handler's window lists only webs with a server, and unlinks its device from a web that has none.
    /// A server joins the station's science web at its Initialize (RD2, server.dm:38); a web of the test's own takes it over.
    private static obj_machinery_rnd_server Server(AudioRoom r, datum_techweb web, int x, int y)
    {
        var server = Place<obj_machinery_rnd_server>(r, x, y);
        r.In(() =>
        {
            if (server.stored_research != null && server.stored_research != web)
                Dm.remove(ref server.stored_research.techweb_servers, server);
            server.stored_research = web;
            Dm.combine(ref web.techweb_servers, server);
        });
        return server;
    }

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => h.File?.Contains(file) == true);

    [Fact]
    public void TheExperiScannerScansABluespaceCrystalCompletingItsExperimentAndDiscountingItsNode()
    {
        var (r, feed, bob, web) = Room(7601);
        var scanner = Hold<obj_item_experi_scanner>(r, bob);
        datum_component_experiment_handler handler = Handler(r, scanner);
        Assert.NotNull(handler);
        r.In(() =>
        {
            Assert.Same(web, handler.linked_web); // CONNECT_TO_RND_SERVER_ROUNDSTART: no server, the station's science web
            Assert.Contains(handler, Dm.each<object>(GLOB.experiment_handlers));
        });

        var experiment = Experiment<datum_experiment_scanning_points_bluespace_crystal>(r, web);
        datum_techweb_node node = r.In(() => SSresearch.techweb_nodes[typeof(datum_techweb_node_applied_bluespace)] as datum_techweb_node);
        float Price() => r.In(() => Dm.num(node.get_price(web)[TECHWEB_POINT_TYPE_GENERIC]));
        Assert.Equal(TECHWEB_TIER_2_POINTS, Price());

        // nothing selected: no scan is run (and the handler stays quiet, tg's ignored attempt with no experiment)
        var crystal = Place<obj_item_stack_ore_bluespace_crystal>(r, 5, 4);
        Use(r, bob, crystal);
        r.Step(Second);
        Assert.Equal(0f, r.In(() => experiment.completed));

        r.In(() => handler.link_experiment(experiment));
        r.In(() => Assert.Same(experiment, handler.selected_experiment));
        Use(r, bob, crystal);
        r.Step(2 * Second); // the scan's do_after of 1 second
        r.In(() =>
        {
            Assert.Equal(TRUE, Dm.num(experiment.completed));
            Assert.Same(experiment, web.completed_experiments[typeof(datum_experiment_scanning_points_bluespace_crystal)]);
            Assert.False(Dm.@in(experiment, web.available_experiments));
            Assert.Null(handler.selected_experiment);
            Assert.False(QDELETED(crystal), "a scan is not destructive");
        });
        Assert.Equal(0f, Price()); // the node's discount for the experiment
        Assert.True(feed.Any("You scan"), "no scan message");
        Assert.True(Heard(r, "sound/machines/ping.ogg"), "no ping");
        Assert.True(feed.Any("Bluespace Crystal Sampling has been completed"), "the scanner did not announce it");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheExperiScannerTracksAMeatWallThatAnEmitterBeamCooks()
    {
        var (r, feed, bob, web) = Room(7602);
        var scanner = Hold<obj_item_experi_scanner>(r, bob);
        datum_component_experiment_handler handler = Handler(r, scanner);
        var experiment = Experiment<datum_experiment_physical_meat_wall_explosion>(r, web);
        r.In(() => handler.link_experiment(experiment));

        turf wall = r.In(() => Waiting.NoSleep(locate(5, 4, 1).ChangeTurf(typeof(turf_closed_wall))) as turf);
        r.Step(2);
        // a wall of iron is refused
        Use(r, bob, wall);
        r.Step(2 * Second);
        Assert.True(feed.Any("Object is not made out of the correct materials."), "the wall's material was not checked");
        Assert.Null(r.In(() => experiment.currently_scanned_atom));

        r.In(() => Waiting.NoSleep(wall.set_custom_materials(Dm.list(Dm.kv(typeof(datum_material_meat), SHEET_MATERIAL_AMOUNT)))));
        Use(r, bob, wall);
        r.Step(2 * Second);
        Assert.True(feed.Any("Experiment ready to start."), "the meat wall was not tracked");
        Assert.Same(wall, r.In(() => experiment.currently_scanned_atom));

        // an emitter's beam hits it
        r.In(() =>
        {
            obj_projectile_beam_emitter beam = Dm.@new<obj_projectile_beam_emitter>(locate(4, 4, 1));
            wall._SendSignal(COMSIG_ATOM_BULLET_ACT, Dm.list(wall, beam));
        });
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(TRUE, Dm.num(experiment.completed));
            Assert.Same(experiment, web.completed_experiments[typeof(datum_experiment_physical_meat_wall_explosion)]);
            turf now = locate(5, 4, 1);
            Assert.False(now is turf_closed_wall, "the meat wall still stands");
            Assert.NotNull(Dm.locate<obj_item_food_meat_steak_plain>(now));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheExperimentConfigureWindowListsTheServerAndSelectsAndClearsAnExperiment()
    {
        var (r, _, bob, web) = Room(7603);
        Server(r, web, 8, 4);
        var scanner = Hold<obj_item_experi_scanner>(r, bob);
        datum_component_experiment_handler handler = Handler(r, scanner);
        var experiment = Experiment<datum_experiment_scanning_points_bluespace_crystal>(r, web);
        string expRef = r.In(() => (string)REF(experiment));

        Take(bob);
        r.Command(() => ClientSeam.Keybinding(bob.client, 4, true)); // activate_inhand: attack_self opens the window
        r.Step(4);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("ExperimentConfigure", InterfaceOf(payload));
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(0, data.GetProperty("has_start_callback").GetDouble());
        JsonElement webEntry = data.GetProperty("techwebs").EnumerateArray().Single();
        Assert.Equal(r.In(() => web.id), webEntry.GetProperty("web_id").GetString());
        Assert.Equal(1, webEntry.GetProperty("selected").GetDouble());
        Assert.Single(webEntry.GetProperty("all_servers").EnumerateArray());
        JsonElement listed = data.GetProperty("experiments").EnumerateArray().Single(e => e.GetProperty("ref").GetString() == expRef);
        Assert.Equal("Bluespace Crystal Sampling", listed.GetProperty("name").GetString());
        Assert.Equal(0, listed.GetProperty("selected").GetDouble());
        Assert.Equal("integer", listed.GetProperty("progress")[0][0].GetString());
        // the destructive experiments are not offered to the hand scanner
        Assert.DoesNotContain(data.GetProperty("experiments").EnumerateArray(), e => e.GetProperty("tag").GetString().StartsWith("Destructive", StringComparison.Ordinal));

        Act(r, bob, window, "select_experiment", ("ref", expRef));
        Assert.Same(experiment, r.In(() => handler.selected_experiment));
        data = Data(r, bob, out _);
        Assert.Equal(1, data.GetProperty("experiments").EnumerateArray().Single(e => e.GetProperty("ref").GetString() == expRef).GetProperty("selected").GetDouble());

        Act(r, bob, window, "clear_experiment");
        Assert.Null(r.In(() => handler.selected_experiment));
        Act(r, bob, window, "clear_server");
        Assert.Null(r.In(() => handler.linked_web));
        Act(r, bob, window, "select_server", ("ref", r.In(() => (string)REF(web))));
        Assert.Same(web, r.In(() => handler.linked_web));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDestructiveScannerTakesAPlantIntoItsTubeScansItAndDestroysIt()
    {
        var (r, feed, bob, web) = Room(7604);
        Server(r, web, 8, 4);
        var machine = Place<obj_machinery_destructive_scanner>(r, 5, 4);
        datum_component_experiment_handler handler = Handler(r, machine);
        Assert.NotNull(handler);
        var experiment = Experiment<datum_experiment_scanning_random_plants>(r, web);
        Assert.Equal(TRUE, r.In(() => Dm.num(Dm.bitand(experiment.traits, EXPERIMENT_TRAIT_DESTRUCTIVE))));
        var banana = r.In(() => Dm.@new<obj_item_food_grown_banana>(machine.loc));
        r.Step(2);

        // a click opens the experiment window (EXPERIMENT_CONFIG_CLICK); its Perform Experiment button starts the scan
        Take(bob);
        Use(r, bob, machine);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("ExperimentConfigure", InterfaceOf(payload));
        Assert.Equal(1, payload.GetProperty("data").GetProperty("has_start_callback").GetDouble());
        r.In(() => handler.link_experiment(experiment));
        Act(r, bob, window, "start_experiment_callback");
        r.In(() =>
        {
            Assert.Equal(TRUE, Dm.num(machine.scanning));
            Assert.Same(machine, banana.loc);
            Assert.Equal("tube_on", machine.icon_state);
        });
        Assert.True(Heard(r, "TubeDown.ogg"), "no tube sound");

        r.Step(2 * Second); // 1.2 s: the scan
        Assert.True(Heard(r, "ScanSafe.ogg"), "no scan sound");
        r.Step(6 * Second); // 6 s: the tube rises
        Assert.Equal("tube_open", r.In(() => machine.icon_state));
        Assert.True(Heard(r, "TubeUp.ogg"), "no tube sound");
        r.Step(2 * Second); // 1.2 s: it opens and the handler scans what it held
        r.In(() =>
        {
            Assert.True(QDELETED(banana), "a destructive scan destroys the sample");
            Assert.Equal(TRUE, Dm.num(experiment.completed));
            Assert.Same(experiment, web.completed_experiments[typeof(datum_experiment_scanning_random_plants)]);
        });
        Assert.True(Heard(r, "sound/machines/ping.ogg"), "no ping");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDestructiveAnalyzerDestroysAnAlienToolToRevealItsNodeAndAnAnomalyCoreForPoints()
    {
        var (r, feed, bob, web) = Room(7605);
        var analyzer = Place<obj_machinery_rnd_destructive_analyzer>(r, 5, 4);
        Assert.Same(web, r.In(() => analyzer.stored_research)); // linked at round start (no server: the station's web)
        Type alienBase = typeof(datum_techweb_node_alien_base);
        Assert.True(r.In(() => Dm.truthy(web.hidden_nodes[alienBase])));

        // an alien cautery is loaded with a left click
        var cautery = Hold<obj_item_cautery_alien>(r, bob);
        Use(r, bob, analyzer);
        r.In(() =>
        {
            Assert.Same(cautery, analyzer.loaded_item);
            Assert.Same(analyzer, cautery.loc);
            Assert.True(Dm.truthy(analyzer.busy));
        });
        Assert.True(feed.Any("You place"), "no load message");
        r.Step(2 * Second); // finish_loading
        r.In(() =>
        {
            Assert.False(Dm.truthy(analyzer.busy));
            Assert.Equal("d_analyzer_l", analyzer.icon_state);
        });

        // its window shows the item and the node it reveals
        Take(bob);
        Use(r, bob, analyzer);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("DestructiveAnalyzer", InterfaceOf(payload));
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(1, data.GetProperty("server_connected").GetDouble());
        Assert.Equal(1, data.GetProperty("indestructible").GetDouble()); // tg's name: "not indestructible"
        Assert.Equal("icons/obj/antags/abductor.dmi", data.GetProperty("item_icon").GetProperty("icon").GetString());
        JsonElement nodeEntry = data.GetProperty("node_data").EnumerateArray().Single(n => n.GetProperty("node_path").GetString() == "/datum/techweb_node/alien/base");
        Assert.Equal(1, nodeEntry.GetProperty("node_hidden").GetDouble());

        Act(r, bob, window, "deconstruct", ("deconstruct_path", "/datum/techweb_node/alien/base"));
        r.In(() =>
        {
            Assert.True(QDELETED(cautery), "the item was not destroyed");
            Assert.Null(analyzer.loaded_item);
            Assert.False(Dm.truthy(web.hidden_nodes[alienBase]), "the node was not revealed");
            Assert.True(Dm.truthy(analyzer.busy));
            Assert.Equal("d_analyzer", analyzer.icon_state);
        });
        r.Step(3 * Second); // 2.4 s: no longer busy
        Assert.False(r.In(() => Dm.truthy(analyzer.busy)));

        // an anomaly core is worth research points once
        float points = r.In(() => Dm.num(web.research_points[TECHWEB_POINT_TYPE_GENERIC]));
        var core = r.In(() =>
        {
            var c = Dm.@new<obj_item_assembly_signaler_anomaly>(analyzer);
            analyzer.loaded_item = c;
            return c;
        });
        Act(r, bob, window, "deconstruct");
        r.In(() =>
        {
            Assert.True(QDELETED(core));
            Assert.Equal(points + TECHWEB_TIER_5_POINTS, Dm.num(web.research_points[TECHWEB_POINT_TYPE_GENERIC]));
            Assert.True(Dm.truthy(web.deconstructed_items[typeof(obj_item_assembly_signaler_anomaly)]));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheOperatingComputerCataloguesAnAutopsyForItsAlwaysActiveExperiments()
    {
        var (r, feed, _, web) = Room(7606);
        var computer = Place<obj_machinery_computer_operating>(r, 5, 4);
        datum_component_experiment_handler handler = r.In(() => computer.experiment_handler);
        Assert.NotNull(handler);
        Assert.Same(handler, Handler(r, computer));
        var experiment = Experiment<datum_experiment_autopsy_human>(r, web);
        var corpse = r.In(() => Dm.@new<mob_living_carbon_human>(locate(6, 5, 1)));
        r.Step(2);
        r.In(() => computer._SendSignal(COMSIG_OPERATING_COMPUTER_AUTOPSY_COMPLETE, Dm.list(computer, corpse)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(TRUE, Dm.num(experiment.completed));
            Assert.Same(experiment, web.completed_experiments[typeof(datum_experiment_autopsy_human)]);
        });
        Assert.True(feed.Any("New unique autopsy successfully catalogued."), "the computer did not say it");
        r.AssertNoRuntimes();
    }
}
