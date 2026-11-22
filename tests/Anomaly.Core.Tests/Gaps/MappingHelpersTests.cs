using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package G3: Meta's mapping helpers and spawners do their work at map load in a booted toy room, as tg's do, and leave
/// nothing behind: the request console helpers (mapping_helpers.dm:1371) fill the console and the department lists, the consoles
/// name themselves and register (requests_console.dm:103); the mail sorting helpers tag their sorting pipe
/// (mail_sorting_helpers.dm:6); the APC helpers' payloads (:544); the lights-off helper flips its area's switch (:678); Ian's
/// birthday and new year helpers and the christmas tree spawner do nothing on a normal day (check_holidays finds no holiday), and
/// the birthday and the fireworks lay tg's things when run (:1042, :1116); the costume, surgery tray, default lawset and
/// maintenance MOD spawners make their items; the dead body placer deletes itself (its bodies wait for the morgue).
/// </summary>
public class MappingHelpersTests
{
    private static List<T> On<T>(AudioRoom r, int x, int y) where T : class => r.In(() => Dm.each<T>(locate(x, y, 1)).ToList());

    private static List<T> All<T>(AudioRoom r) where T : class => r.In(() => Dm.each<T>().ToList());

    private static bool Leftovers(AudioRoom r)
        => r.In(() => Dm.each<obj_effect_mapping_helpers>().Any(h => !QDELETED(h)) || Dm.each<obj_effect_spawner>().Any(s => !QDELETED(s)));

    [Fact]
    public void TheRequestConsoleHelpersFillTheirConsoleAndTheDepartmentListsAndTheConsolesNameThemselves()
    {
        obj_machinery_requests_console bar = null, atmos = null, auto = null, unknown = null;
        var r = new AudioRoom(13, 9, seed: 9701, mapExtras: w =>
        {
            bar = Dm.@new<obj_machinery_requests_console_directional_south>(locate(3, 2, 1));
            bar.department = "Bar"; // a map-set department
            Dm.@new<obj_effect_mapping_helpers_requests_console_announcement>(locate(3, 2, 1));
            Dm.@new<obj_effect_mapping_helpers_requests_console_assistance>(locate(3, 2, 1));
            Dm.@new<obj_effect_mapping_helpers_requests_console_supplies>(locate(3, 2, 1));
            atmos = Dm.@new<obj_machinery_requests_console_directional_south>(locate(5, 2, 1));
            atmos.name = "Atmospherics"; // a map-set name and no department: the name is the department
            Dm.@new<obj_effect_mapping_helpers_requests_console_information>(locate(5, 2, 1));
            Dm.@new<obj_effect_mapping_helpers_requests_console_ore_update>(locate(5, 2, 1));
            auto = Dm.@new<obj_machinery_requests_console_auto_name_directional_south>(locate(7, 2, 1));
            unknown = Dm.@new<obj_machinery_requests_console_directional_south>(locate(9, 2, 1));
            Dm.@new<obj_effect_mapping_helpers_requests_console_assistance>(locate(11, 5, 1)); // no console on its turf
        });
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(1f, bar.can_send_announcements);
            Assert.Equal(0f, bar.receive_ore_updates);
            Assert.Equal(1f, atmos.receive_ore_updates);
            Assert.Equal(0f, atmos.can_send_announcements);
            Assert.Equal(new object[] { "Bar" }, Dm.each(GLOB.req_console_assistance).ToArray());
            Assert.Equal(new object[] { "Bar" }, Dm.each(GLOB.req_console_supplies).ToArray());
            Assert.Equal(new object[] { "Atmospherics" }, Dm.each(GLOB.req_console_information).ToArray());

            // the consoles' own init (requests_console.dm:103-132)
            Assert.Equal("Atmospherics", atmos.department);
            Assert.Equal("Unknown", unknown.department);
            Assert.Equal(Dm.str(get_area_name((atom)get_area(auto), TRUE)), auto.department);
            Assert.EndsWith("Bar requests console", Dm.str(bar.name));
            Assert.EndsWith("Unknown requests console", Dm.str(unknown.name));
            Assert.Same(get_area(bar), bar.area);
            foreach (var console in new[] { bar, atmos, auto, unknown })
                Assert.True(Dm.@in(console, GLOB.req_console_all));
            Assert.Equal("Bar", GLOB.req_console_ckey_departments["bar"]);
            Assert.Equal("Engineering", reta_get_user_department_by_name("Atmospherics")); // reta_system.dm:195
            Assert.Equal(new object[] { atmos }, Dm.each((DmList)GLOB.reta_consoles_by_origin["Engineering"]).ToArray());
            Assert.Equal("Service", reta_get_user_department_by_name("Bar"));
        });
        Assert.Contains(r.Room.Log.Lines, l => l.Contains("failed to find a requests console", StringComparison.Ordinal));
        Assert.False(Leftovers(r));

        // a console's teardown leaves the lists (requests_console.dm:134)
        r.In(() => qdel(atmos));
        r.In(() =>
        {
            Assert.False(Dm.@in(atmos, GLOB.req_console_all));
            Assert.Null(GLOB.reta_consoles_by_origin["Engineering"]);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARequestConsoleShowsItsScreenItsPanelAndItsExamineAndAHelperMadeAfterTheMapLoadDeletesItself()
    {
        obj_machinery_requests_console console = null;
        var r = new AudioRoom(13, 9, seed: 9702, mapExtras: w =>
        {
            console = Dm.@new<obj_machinery_requests_console_directional_south>(locate(4, 2, 1));
            console.department = "Kitchen";
        });
        mob_living_carbon_human bob = r.Join(4, 3, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        List<string> States() => r.In(() => Dm.each(console.managed_overlays is DmList l ? l : Dm.list(console.managed_overlays))
            .Select(o => o is mutable_appearance m ? m.icon_state : (o as DmAppearance)?.icon_state).Where(s => s != null).ToList());
        string Examine() => r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(console.examine(bob))).Select(Dm.str)));

        Assert.Contains("req_comp0", States()); // no new message (requests_console.dm:98)
        Assert.Equal(1.5f, r.In(() => Dm.num(console.light_range)));
        Assert.Contains("pry open the panel with <b>a crowbar</b>", Examine(), StringComparison.Ordinal);

        var crowbar = r.In(() => Dm.@new<obj_item_crowbar>(bob));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(crowbar)));
        Click(r, bob, console);
        r.Step(Second);
        Assert.Equal(1f, r.In(() => Dm.num(console.open)));
        Assert.Equal(new[] { "req_comp_open" }, States()); // an open panel shows no screen (:83-87)
        Assert.Contains("The panel was pried open", Examine(), StringComparison.Ordinal);

        var screwdriver = r.In(() => Dm.@new<obj_item_screwdriver>(bob));
        r.In(() => { Waiting.NoSleep(bob.dropItemToGround(crowbar)); Waiting.NoSleep(bob.put_in_hands(screwdriver)); });
        Click(r, bob, console);
        r.Step(Second);
        Assert.Equal(1f, r.In(() => console.hack_state));
        Assert.Contains("tampered with", Examine(), StringComparison.Ordinal);

        // a helper spawned outside the map load logs and deletes itself (mapping_helpers.dm:1377)
        var late = r.In(() => Dm.@new<obj_effect_mapping_helpers_requests_console_announcement>(locate(4, 2, 1)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(late)));
        Assert.Contains(r.Room.Log.Lines, l => l.Contains("spawned outside of mapload!", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMailSortingHelpersTagTheSortingPipeOnTheirTurf()
    {
        obj_structure_disposalpipe_sorting_mail first = null, second = null;
        var r = new AudioRoom(13, 9, seed: 9703, mapExtras: w =>
        {
            first = Dm.@new<obj_structure_disposalpipe_sorting_mail>(locate(3, 4, 1));
            Dm.@new<obj_effect_mapping_helpers_mail_sorting_supply_disposals>(locate(3, 4, 1));
            Dm.@new<obj_effect_mapping_helpers_mail_sorting_service_bar>(locate(3, 4, 1));
            second = Dm.@new<obj_structure_disposalpipe_sorting_mail>(locate(6, 4, 1));
            Dm.@new<obj_effect_mapping_helpers_mail_sorting_medbay_chemistry>(locate(6, 4, 1));
            Dm.@new<obj_effect_mapping_helpers_mail_sorting_medbay_chemistry>(locate(6, 4, 1)); // |= : a tag once
            Dm.@new<obj_effect_mapping_helpers_mail_sorting_service_bar>(locate(9, 4, 1)); // no pipe
        });
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(new object[] { (float)SORT_TYPE_DISPOSALS, (float)SORT_TYPE_BAR }, Dm.each(first.sortTypes).Select(t => (object)Dm.num(t)).ToArray());
            Assert.Equal(new object[] { (float)SORT_TYPE_CHEMISTRY }, Dm.each(second.sortTypes).Select(t => (object)Dm.num(t)).ToArray());
        });
        Assert.Contains(r.Room.Log.Lines, l => l.Contains("failed to find a mail sorting disposal pipe", StringComparison.Ordinal));
        Assert.False(Leftovers(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheApcHelpersInstallTheBigCellAndUnlockTheApc()
    {
        obj_machinery_power_apc apc = null;
        var r = new AudioRoom(13, 9, seed: 9704, mapExtras: w =>
        {
            apc = Dm.@new<obj_machinery_power_apc_auto_name_directional_south>(locate(4, 2, 1));
            Dm.@new<obj_effect_mapping_helpers_apc_cell_10k>(locate(4, 2, 1));
            Dm.@new<obj_effect_mapping_helpers_apc_unlocked>(locate(4, 2, 1));
        });
        r.Step(Second);
        r.In(() =>
        {
            Assert.IsType<obj_item_stock_parts_power_store_battery_high>(apc.cell); // install_cell_10k, apc_main.dm:781
            Assert.Equal(1f, apc.cell_10k);
            Assert.Equal(0f, apc.locked); // unlock, :786
        });
        Assert.False(Leftovers(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheLightsOffHelperTurnsItsAreasLightsOffWithTheSwitch()
    {
        obj_machinery_light_switch sw = null;
        obj_machinery_light light = null;
        var r = new AudioRoom(13, 9, seed: 9705, mapExtras: w =>
        {
            sw = Dm.@new<obj_machinery_light_switch_directional_south>(locate(5, 2, 1));
            light = Dm.@new<obj_machinery_light_directional_south>(locate(8, 2, 1));
            light.allow_break_on_init = FALSE;
            Dm.@new<obj_effect_mapping_helpers_turn_off_lights_with_lightswitch>(locate(3, 5, 1));
        });
        r.Step(Second);
        r.In(() =>
        {
            var room = (area)get_area(sw);
            Assert.False(Dm.truthy(room.lightswitch));
            Assert.Equal("light-off", sw.icon_state);
            Assert.False(Dm.truthy(light.on));
        });
        Assert.False(Leftovers(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void OnANormalDayIansHelpersAndTheChristmasTreeSpawnerLeaveNothing()
    {
        var r = new AudioRoom(13, 9, seed: 9706, mapExtras: w =>
        {
            Dm.@new<obj_structure_bed_dogbed_ian>(locate(6, 5, 1));
            Dm.@new<obj_structure_table_reinforced>(locate(8, 5, 1));
            Dm.@new<obj_effect_mapping_helpers_ianbirthday>(locate(3, 3, 1));
            Dm.@new<obj_effect_mapping_helpers_iannewyear>(locate(3, 4, 1));
            Dm.@new<obj_effect_spawner_xmastree>(locate(10, 6, 1));
            Dm.@new<obj_effect_spawner_xmastree_rdrod>(locate(10, 4, 1));
        });
        r.Step(Second);
        Assert.Null(r.In(() => check_holidays(IAN_HOLIDAY)));
        Assert.Empty(All<obj_effect_decal_cleanable_confetti>(r));
        Assert.Empty(All<obj_item_toy_balloon>(r));
        Assert.Empty(All<obj_item_clothing_head_costume_festive>(r));
        Assert.Empty(All<obj_structure_flora_tree_pine_xmas>(r));
        Assert.Empty(All<obj_structure_festivus>(r));
        Assert.False(Leftovers(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBirthdayDecoratesTheRoomAsTgsAndTheNewYearPutsTheFireworksOnTheTable()
    {
        obj_effect_mapping_helpers_iannewyear newyear = null;
        var r = new AudioRoom(13, 9, seed: 9707, mapExtras: w =>
        {
            Dm.@new<obj_structure_bed_dogbed_ian>(locate(6, 5, 1));
            Dm.@new<obj_structure_table_reinforced>(locate(8, 5, 1));
            Dm.@new<obj_effect_mapping_helpers_ianbirthday_admin>(locate(3, 3, 1)); // birthday() whatever the date (:1101)
            newyear = Dm.@new<obj_effect_mapping_helpers_iannewyear>(locate(3, 4, 1));
            newyear.fireworks(); // what the helper does on New Year (:1116)
        });
        r.Step(Second);
        r.In(() =>
        {
            int open = Dm.each_cast<DmList>(((area)get_area(locate(3, 3, 1))).get_zlevel_turf_lists()).Sum(l => Dm.each_cast<turf>(l).Count(isopenturf));
            Assert.Equal(open, Dm.each<obj_effect_decal_cleanable_confetti>().Count()); // confetti on every open turf of the area
        });
        Assert.Single(On<obj_item_toy_balloon_corgi>(r, 6, 5));
        var knife = Assert.Single(On<obj_item_knife_kitchen>(r, 8, 5)); // the only reinforced table
        var cake = Assert.Single(On<obj_item_food_cake_birthday>(r, 8, 5));
        Assert.Equal("Happy birthday, Ian!", r.In(() => Dm.str(cake.desc)));
        // two clusters of four balloons: one on the spot and three around it (or on the spot when blocked)
        Assert.Equal(8, All<obj_item_toy_balloon>(r).Count(b => b is not obj_item_toy_balloon_corgi));
        r.In(() =>
        {
            foreach (obj_item_toy_balloon b in Dm.each<obj_item_toy_balloon>().Where(b => b is not obj_item_toy_balloon_corgi))
                Assert.Contains(Math.Abs(b.pixel_x) + Math.Abs(b.pixel_y), new[] { 0f, 10f });
        });

        var hat = Assert.Single(On<obj_item_clothing_head_costume_festive>(r, 6, 5));
        var drink = Assert.Single(On<obj_item_reagent_containers_cup_glass_bottle_champagne>(r, 6, 5));
        Assert.Equal("dog champagne", r.In(() => Dm.str(drink.name)));
        Assert.Equal((8f, 8f), r.In(() => (drink.pixel_x, drink.pixel_y)));
        var matches = Assert.Single(On<obj_item_storage_box_matches>(r, 8, 5));
        Assert.Equal((-3f, 8f), r.In(() => (matches.pixel_x, matches.pixel_y)));
        Assert.Single(On<obj_item_storage_box_fireworks_dangerous>(r, 8, 5));
        Assert.False(Leftovers(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCostumeSurgeryTrayLawsetAndMaintenanceModSpawnersMakeTheirItemsAndTheDeadBodyPlacerWithoutAMorgueStays()
    {
        var r = new AudioRoom(13, 9, seed: 9708, mapExtras: w =>
        {
            Dm.@new<obj_effect_spawner_costume_chicken>(locate(3, 3, 1));
            Dm.@new<obj_effect_spawner_surgery_tray_full>(locate(5, 3, 1));
            Dm.@new<obj_effect_spawner_round_default_module>(locate(7, 3, 1));
            Dm.@new<obj_effect_spawner_random_mod_maint>(locate(9, 3, 1));
            Dm.@new<obj_effect_mapping_helpers_dead_body_placer>(locate(11, 3, 1));
        });
        r.Step(Second);
        Assert.Single(On<obj_item_clothing_suit_costume_chickensuit>(r, 3, 3));
        Assert.Single(On<obj_item_clothing_head_costume_chicken>(r, 3, 3));
        Assert.Single(On<obj_item_food_egg>(r, 3, 3));
        // surgery_tray.dm:239: the full tray, or one time in a hundred the full medical toolbox
        Assert.Equal(1, On<obj_item_surgery_tray_full>(r, 5, 3).Count + On<obj_item_storage_toolbox_medical_full>(r, 5, 3).Count);
        // tg's default config (DEFAULT_LAWS 0) makes asimov the round's lawset: its core board (_AI_modules.dm:245)
        Assert.Equal(typeof(datum_ai_laws_default_asimov), r.In(() => get_round_default_lawset()));
        Assert.Single(On<obj_item_ai_module_law_core_full_asimov>(r, 7, 3));
        var maint = new[]
        {
            typeof(obj_item_mod_module_springlock), typeof(obj_item_mod_module_visor_rave), typeof(obj_item_mod_module_tanner),
            typeof(obj_item_mod_module_balloon), typeof(obj_item_mod_module_paper_dispenser), typeof(obj_item_mod_module_hat_stabilizer),
            typeof(obj_item_mod_module_stamp), typeof(obj_item_mod_module_recycler_donk_safe),
        };
        var module = Assert.Single(On<obj_item>(r, 9, 3)); // one level: no atrocinator (mod.dm:20)
        Assert.Contains(module.GetType(), maint);
        // the dead body placer with no closed morgue tray in its area makes no body and stays (tg returns before its qdel,
        // mapping_helpers.dm:1007; live since gaps OG1: OrgansOG1MetaTests has Meta's morgue)
        Assert.Empty(All<obj_structure_closet_body_bag>(r));
        Assert.Single(All<obj_effect_mapping_helpers_dead_body_placer>(r));
        Assert.False(r.In(() => Dm.each<obj_effect_mapping_helpers>().Any(h => !QDELETED(h) && h is not obj_effect_mapping_helpers_dead_body_placer)
            || Dm.each<obj_effect_spawner>().Any(s => !QDELETED(s))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGrimeAndMoistureSpawnersScatterTheirLootAndTheirInertMobsCanBeDeleted()
    {
        var r = new AudioRoom(13, 9, seed: 9709, mapExtras: w =>
        {
            Dm.@new<obj_effect_spawner_random_trash_grime>(locate(4, 4, 1));
            Dm.@new<obj_effect_spawner_random_trash_moisture>(locate(9, 4, 1));
        });
        r.Step(Second);
        Assert.False(Leftovers(r));
        // trash.dm:212-247: five things within two tiles of the grime, two within one of the wet floor (the frog spawner lays its frog)
        int Near(int cx, int cy, int radius) => r.In(() =>
        {
            int n = 0;
            for (int x = cx - radius; x <= cx + radius; x++)
                for (int y = cy - radius; y <= cy + radius; y++)
                    n += Dm.each_cast<atom_movable>(locate(x, y, 1).contents).Count(m => m is not mob_living_carbon_human);
            return n;
        });
        Assert.InRange(Near(4, 4, 2), 1, 5 * 5);
        Assert.InRange(Near(9, 4, 1), 1, 2 * 5);
        // the cockroaches, frogs and axolotls are inert props, and they go as tg's do
        r.In(() => Dm.@new<mob_living_basic_cockroach>(locate(7, 7, 1)));
        r.In(() =>
        {
            foreach (mob_living_basic m in Dm.each<mob_living_basic>().ToList())
                qdel(m);
        });
        r.Step(Second);
        Assert.Empty(r.In(() => Dm.each<mob_living_basic>().Where(m => !QDELETED(m)).ToList()));
        r.AssertNoRuntimes();
    }
}
