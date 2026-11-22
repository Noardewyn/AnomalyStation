using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.E2;

/// <summary>
/// The engineering package E2 (D287): the supermatter monitor's data (supermatter.dm sm_ui_data, supermatter_gas.dm sm_gas_data), the
/// modular computer console and its presets (computers/machinery/*.dm, processor.dm) with NT CIMS (sm_monitor.dm): its crystals, its
/// focus and delamination alert, its smmon screen; and the delamination incident display (incident_display.dm): its digits, trend
/// arrow, shame screen and advert, and the delamination resetting it.
/// </summary>
public class SupermatterMonitorTests
{
    private const int Second = 20;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(9, 7, seed: seed);
        r.Step(Second);
        return r;
    }

    /// <summary>A crystal frozen out of SSair's run (as SupermatterTests does), so the tests set its state by hand.</summary>
    private static obj_machinery_power_supermatter_crystal Crystal(int x, int y)
    {
        var sm = Dm.@new<obj_machinery_power_supermatter_crystal_engine>(locate(x, y, 1));
        sm.time_frozen();
        sm.activation_logged = TRUE;
        return sm;
    }

    private static string[] States(object overlays) => Dm.each<object>(overlays as DmList)
        .Select(o => o is mutable_appearance ma ? Dm.str(ma.icon_state) : Dm.str(o)).ToArray();

    [Fact]
    public void The_crystals_data_reads_its_integrity_energy_zaps_and_gases()
    {
        AudioRoom r = Room(2801);
        r.In(() =>
        {
            obj_machinery_power_supermatter_crystal sm = Crystal(4, 4);
            sm.damage = 10;
            sm.internal_energy = 6000;
            sm.damage_factors = Dm.list(Dm.kv("Heat Damage", 1.234f), Dm.kv("Healing", 0));
            sm.gas_percentage[typeof(datum_gas_nitrogen)] = 0.75f;
            sm.gas_percentage[typeof(datum_gas_plasma)] = 0.25f;

            var data = (DmList)sm.sm_ui_data();
            Assert.Equal(sm.uid, Dm.num(data["uid"]));
            Assert.Equal(90f, Dm.num(data["integrity"])); // 100 - 10 / 100 * 100 (get_integrity_percent)
            // the factors rounded to 0.01 and negated, the zero ones left out
            var factor = Assert.Single(Dm.each<DmList>(data["integrity_factors"]));
            Assert.Equal("Heat Damage", Dm.str(factor["name"]));
            Assert.Equal(-1.23f, Dm.num(factor["amount"]), 4);
            // 6000 MeV is 6 GeV
            Assert.Equal(6f, Dm.num(data["internal_energy_coefficient"]), 3);
            Assert.Equal(" GeV", Dm.str(data["internal_energy_unit"]));
            // the zaps above 5 GeV: 1040 * 6000 plus the high energy bonus, 1.92e7 * 2 (supermatter.dm:458-470)
            Assert.Equal(6.24e6f + 3.84e7f, Dm.num(data["zap_transmission"]));
            Assert.Equal(44.64f, Dm.num(data["zap_transmission_coefficient"]), 3);
            Assert.Equal(" MW", Dm.str(data["zap_transmission_unit"]));
            var bonus = Assert.Single(Dm.each<DmList>(data["zap_transmission_factors"]));
            Assert.Equal("High Energy Bonus", Dm.str(bonus["name"]));
            Assert.Equal(38.4f, Dm.num(bonus["amount"]), 3);
            // every gas, a share or 0, keyed by its path
            var composition = (DmList)data["gas_composition"];
            Assert.Equal(Dm.each(subtypesof(typeof(datum_gas))).Count(), composition.len);
            Assert.Equal((0.75f, 0.25f, 0f), (Dm.num(composition[typeof(datum_gas_nitrogen)]), Dm.num(composition[typeof(datum_gas_plasma)]), Dm.num(composition[typeof(datum_gas_oxygen)])));

            // the window's data wraps the one crystal; the static data is every supermatter gas's effects
            var ui = (DmList)Waiting.NoSleep(sm.ui_data(null));
            var only = (DmList)ui["sm_data"];
            Assert.Equal(1, only.len); // list(sm_ui_data()): the one crystal's data as the list's element
            Assert.Equal(sm.uid, Dm.num(((DmList)only[1f])["uid"]));
            var metadata = (DmList)sm_gas_data();
            var plasma = (DmList)metadata[typeof(datum_gas_plasma)];
            var effects = Dm.each<DmList>(plasma["numeric_data"]).Select(e => Dm.str(e["name"])).ToArray();
            Assert.Contains("Waste Multiplier", effects);
            Assert.Equal(GLOB.sm_gas_behavior.len, metadata.len);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_engineering_console_runs_nt_cims_which_lists_focuses_and_alerts_on_the_crystal()
    {
        AudioRoom r = Room(2802);
        r.In(() =>
        {
            obj_machinery_power_supermatter_crystal sm = Crystal(6, 4);
            var console = Dm.@new<obj_machinery_modular_computer_preset_engineering>(locate(2, 4, 1));
            obj_item_modular_computer_processor cpu = console.cpu;
            Assert.Same(console, cpu.loc);
            Assert.Equal(("engineering console", PROGRAM_CONSOLE), (cpu.name, cpu.hardware_flag));
            Assert.Equal(new[] { "themeify", "ntsoftwarehub", "filemanager", "ampcheck", "alarmmonitor", "ntcims" },
                Dm.each<datum_computer_file>(cpu.stored_files).Select(f => f.filename));
            // off: the screensaver on the powered console
            Assert.Equal("console-off", console.icon_state);
            Assert.Contains("standby", States(Waiting.NoSleep(console.update_overlays())));

            Assert.True(Dm.truthy(Waiting.NoSleep(cpu.turn_on(null, FALSE))));
            Assert.Equal("console", console.icon_state); // the processor's icon update reaches the console
            Assert.Contains("menu", States(Waiting.NoSleep(console.update_overlays())));

            var cims = Dm.each<datum_computer_file_program_supermatter_monitor>(cpu.stored_files).Single();
            Assert.True(Dm.truthy(Waiting.NoSleep(cpu.open_program(null, cims, FALSE))));
            Assert.Equal(new object[] { sm }, Dm.each<object>(cims.supermatters).ToArray());
            var data = (DmList)Waiting.NoSleep(cims.ui_data(null));
            var smData = Assert.Single(Dm.each<DmList>(data["sm_data"]));
            Assert.Equal(sm.uid, Dm.num(smData["uid"]));
            Assert.Null(data["focus_uid"]);

            // the bell focuses the crystal: its delamination alarm raises the program's alert
            Waiting.NoSleep(cims.ui_act("PRG_focus", Dm.list(Dm.kv("focus_uid", sm.uid))));
            Assert.Same(sm, cims.focused_supermatter);
            sm._SendSignal(COMSIG_SUPERMATTER_DELAM_ALARM, Dm.list(sm));
            Assert.True(Dm.truthy(cims.alert_pending));

            // a damaged crystal: the program's screen (smmon_3, warning) on the console
            sm.damage = sm.warning_point;
            cims.process_tick(2);
            Assert.Equal(("smmon_3", "smmon_3.gif"), (Dm.str(cims.program_open_overlay), Dm.str(cims.ui_header)));
            Assert.Contains("smmon_3", States(Waiting.NoSleep(console.update_overlays())));

            // the bell again unfocuses; closing the program clears its crystals
            Waiting.NoSleep(cims.ui_act("PRG_focus", Dm.list(Dm.kv("focus_uid", sm.uid))));
            Assert.Null(cims.focused_supermatter);
            cims.kill_program();
            Assert.Equal(0, cims.supermatters.len);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_other_presets_store_their_programs_and_the_cargo_consoles_are_live()
    {
        AudioRoom r = Room(2803);
        r.In(() =>
        {
            var id = Dm.@new<obj_machinery_modular_computer_preset_id>(locate(2, 2, 1));
            Assert.Equal(new[] { "themeify", "ntsoftwarehub", "filemanager", "ntnrc_client", "plexagonidwriter", "plexagoncore", "plexagoncrew" },
                Dm.each<datum_computer_file>(id.cpu.stored_files).Select(f => f.filename));
            var away = Dm.@new<obj_machinery_modular_computer_preset_research_away>(locate(3, 2, 1));
            Assert.Equal(PDA_THEME_RETRO, away.cpu.device_theme);
            Assert.False(Anomaly.Core.Tg.InertSeam.IsInertType(typeof(obj_machinery_modular_computer_preset_cargochat_engineering))); // since gaps P4d
            Assert.False(Anomaly.Core.Tg.InertSeam.IsInertType(typeof(obj_machinery_modular_computer_preset_engineering)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_incident_display_shows_the_shifts_since_the_last_delamination_and_its_trend()
    {
        AudioRoom r = Room(2804);
        r.In(() =>
        {
            var display = Dm.@new<obj_machinery_incident_display_delam_directional_north>(locate(4, 3, 1));
            Assert.Contains(display, Dm.each<object>(GLOB.map_incident_displays));
            // SSpersistence's counters without saved files: 0 shifts, the record 0 (the shame screen, the rising arrow)
            Assert.Equal((0f, 0f, "display_shame"), (display.last_delam, display.delam_record, display.icon_state));
            var overlays = Dm.each<object>(Waiting.NoSleep(display.update_overlays()) as DmList).OfType<mutable_appearance>().ToArray();
            Assert.Equal(new[] { "overlay_delam", "num_0", "num_0", "rising" }, overlays.Select(o => Dm.str(o.icon_state)));
            Assert.Equal(COLOR_DISPLAY_GREEN, Dm.str(overlays[3].color));

            // 15 shifts below a record of 20: the digits in yellow at tg's offsets, the falling arrow in red
            display.update_delam_count(15, 20);
            Assert.Equal("display_normal", display.icon_state);
            overlays = Dm.each<object>(Waiting.NoSleep(display.update_overlays()) as DmList).OfType<mutable_appearance>().ToArray();
            Assert.Equal(new[] { "overlay_delam", "num_5", "num_1", "falling" }, overlays.Select(o => Dm.str(o.icon_state)));
            Assert.Equal((21f, -2f, "#FFF743"), (overlays[1].pixel_w, overlays[1].pixel_z, Dm.str(overlays[1].color)));
            Assert.Equal(16f, overlays[2].pixel_w);
            Assert.Equal(COLOR_DISPLAY_RED, Dm.str(overlays[3].color));

            // the SAFETY MOTH advert hides the counter while it plays
            display.process(2);
            Assert.True(Dm.truthy(display.live_display));
            Assert.DoesNotContain("overlay_delam", States(Waiting.NoSleep(display.update_overlays())));

            // a main engine's delamination resets every display (_sm_delam.dm:28)
            obj_machinery_power_supermatter_crystal sm = Crystal(6, 5);
            sm.is_main_engine = TRUE;
            Waiting.NoSleep(Dm.@new<datum_sm_delam>().delaminate(sm));
            Assert.Equal((-1f, "display_shame"), (display.last_delam, display.icon_state));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_delamination_counter_is_loaded_into_the_displays_saved_at_round_end_and_reset_by_a_delamination()
    {
        AudioRoom r = Room(2805);
        r.In(() =>
        {
            var display = Dm.@new<obj_machinery_incident_display_delam_directional_north>(locate(4, 3, 1));
            IDmFileSystem files = DmWorldState.Current.Files;
            const string Count = "data/persistence/rounds_since_delamination.txt", Record = "data/persistence/delamination_highscore.txt";

            // no saved files: the counters stay 0 (counter_delamination.dm:6)
            SSpersistence.load_delamination_counter();
            Assert.Equal(0f, display.last_delam);

            // the saved files reach every display on the map (counter_delamination.dm:11)
            files.WriteText(Count, "12");
            files.WriteText(Record, "30");
            SSpersistence.load_delamination_counter();
            Assert.Equal((12f, 30f), (SSpersistence.rounds_since_engine_exploded, SSpersistence.delam_highscore));
            Assert.Equal((12f, 30f, "display_normal"), (display.last_delam, display.delam_record, display.icon_state));

            // the round's end writes one more shift; the record stays while it is not beaten (counter_delamination.dm:14)
            SSpersistence.collect_data();
            Assert.Equal(("13", "30"), (files.ReadText(Count), files.ReadText(Record)));

            // a main engine's delamination: the record takes the count, the count goes to -1 (_sm_delam.dm:28-29), and the next
            // round's file reads 0
            obj_machinery_power_supermatter_crystal sm = Crystal(6, 5);
            sm.is_main_engine = TRUE;
            Waiting.NoSleep(Dm.@new<datum_sm_delam>().delaminate(sm));
            Assert.Equal((-1f, 12f), (SSpersistence.rounds_since_engine_exploded, SSpersistence.delam_highscore));
            SSpersistence.collect_data();
            Assert.Equal(("0", "30"), (files.ReadText(Count), files.ReadText(Record)));

            // a count past the record raises it
            SSpersistence.rounds_since_engine_exploded = 30;
            SSpersistence.collect_data();
            Assert.Equal(("31", "31"), (files.ReadText(Count), files.ReadText(Record)));
        });
        r.AssertNoRuntimes();
    }
}
