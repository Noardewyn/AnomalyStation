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
/// Gaps package R1 (D327): the station goals in booted toy stations. The round-start command report picks the round's goals, lists their
/// directives and unlocks their cargo packs, and the round-end report scores them. The bluespace artillery's three parts are linked with
/// a multitool, previewed and deployed from its console's window, aimed at a GPS signal once the heads unlock it, and fired: the beam
/// tears down its row and the target blows up, unless something in the way (a supermatter, a singularity) blocks the beam. The DNA probe
/// links to the vault, samples a crewman, an animal and a ripe plant and uploads them; the full vault unlocks the ark and gives each
/// crewman one of two vault mutations. The meteor shield satellites, switched on in space from their console or by hand, shoot down the
/// meteors flying into their range, and the emag doubles the meteor events' weight per tampered satellite.
/// </summary>
public class StationGoalsR1Tests
{
    // ---- the rooms ----

    private const int W = 31, H = 25;

    private static void StationLevel(AudioRoom r, int x1, int y1, int x2, int y2)
    {
        r.In(() =>
        {
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))));
            // the station's area over the floors: change_area keeps the area's lists on the turfs the map extras made
            var station = (area)Dm.@new(typeof(area_station_hallway_primary_central));
            foreach (turf t in Dm.each<turf>(block(x1, y1, 1, x2, y2, 1)))
                t.change_area((area)t.loc, station);
            station.requires_power = FALSE;
            GLOB.the_station_areas = Dm.list(station.type);
        });
    }

    /// <summary>An iron floor everywhere on the station's level (the artillery's 11 by 3 tiles fit).</summary>
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) FloorRoom(ulong seed, int bobX = 13, int bobY = 6)
    {
        var r = new AudioRoom(W, H, seed: seed, mapExtras: _ =>
        {
            for (int y = 2; y < H; y++)
                for (int x = 2; x < W; x++)
                    Dm.@new<turf_open_floor_iron>(locate(x, y, 1));
        });
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(bobX, bobY, "Bob");
        StationLevel(r, 2, 2, W - 1, H - 1);
        r.Step(Second);
        return (r, feed, bob);
    }

    private const int Size = 41;
    private const int Low = 17, High = 25; // the walled station in the middle of space

    /// <summary>A walled room of iron floor in the middle of space, on the station's level (the meteors' toy station of gaps E2b).</summary>
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) SpaceRoom(ulong seed, int bobX = 21, int bobY = 21)
    {
        var r = new AudioRoom(Size, Size, seed: seed, mapExtras: _ =>
        {
            for (int y = 2; y < Size; y++)
            {
                for (int x = 2; x < Size; x++)
                {
                    bool inStation = x >= Low && x <= High && y >= Low && y <= High;
                    if (!inStation)
                        Dm.@new<turf_open_space>(locate(x, y, 1));
                    else if (x == Low || x == High || y == Low || y == High)
                        Dm.@new<turf_closed_wall>(locate(x, y, 1));
                    else
                        Dm.@new<turf_open_floor_iron>(locate(x, y, 1));
                }
            }
        });
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(bobX, bobY, "Bob");
        StationLevel(r, Low, Low, High, High);
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        var item = Dm.@new<T>(who.loc);
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(item))));
        return item;
    });

    /// <summary>A click with what is in hand, then the click's cooldown (CLICK_CD_MELEE) passes.</summary>
    private static void Use(AudioRoom r, mob who, atom target)
    {
        Click(r, who, target);
        r.Step(Second);
    }

    private static string OpenWindow(AudioRoom r, mob who, atom machine, string name, out JsonElement data)
    {
        Take(who);
        Click(r, who, machine);
        JsonElement payload = LastUpdate(Take(who), out string window);
        Assert.Equal(name, InterfaceOf(payload));
        data = payload.GetProperty("data");
        return window;
    }

    private static string ListPrompt(mob who)
    {
        foreach (TguiOutput o in Take(who))
        {
            if (o.Kind != TguiOutputKind.Message)
                continue;
            using JsonDocument doc = JsonDocument.Parse(o.Json);
            if (doc.RootElement.GetProperty("type").GetString() == "update" && InterfaceOf(doc.RootElement.GetProperty("payload")) == "ListInputWindow")
                return o.WindowId;
        }
        return null;
    }

    // ---- the goals ----

    [Fact]
    public void TheStationGoalsAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_station_goal), typeof(datum_station_goal_bluespace_cannon), typeof(datum_station_goal_dna_vault),
            typeof(datum_station_goal_station_shield), typeof(obj_machinery_bsa), typeof(obj_machinery_bsa_back), typeof(obj_machinery_bsa_front),
            typeof(obj_machinery_bsa_middle), typeof(obj_machinery_bsa_full), typeof(obj_structure_filler), typeof(obj_machinery_computer_bsa_control),
            typeof(obj_effect_temp_visual_bsa_splash), typeof(datum_element_bsa_blocker), typeof(obj_machinery_dna_vault), typeof(obj_item_dna_probe),
            typeof(obj_machinery_satellite), typeof(obj_machinery_satellite_meteor_shield), typeof(obj_machinery_computer_sat_control),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
        // the carp DNA sampler is live since gaps OD2 (ItemsOD2Tests); only its space dragon self-mutation is left (not-translated 218)
        Assert.False(InertSeam.IsInertType(typeof(obj_item_dna_probe_carp_scanner)));
    }

    [Fact]
    public void TheRoundStartReportPicksTheGoalsListsThemAndUnlocksTheirPacksAndTheRoundEndScoresThem()
    {
        var (r, _, _) = FloorRoom(5101);
        var console = Place<obj_machinery_computer_communications>(r, 5, 20);
        DmList packs = r.In(() => SSshuttle.supply_packs);
        Type[] goalPacks =
        {
            typeof(datum_supply_pack_engineering_bsa), typeof(datum_supply_pack_engineering_dna_vault), typeof(datum_supply_pack_engineering_dna_probes),
            typeof(datum_supply_pack_engineering_shield_sat), typeof(datum_supply_pack_engineering_shield_sat_control),
        };
        foreach (Type pack in goalPacks)
            Assert.False(r.In(() => Dm.truthy(Dm.bitand(((datum_supply_pack)packs[pack]).order_flags, ORDER_SPECIAL_ENABLED))), pack.Name + " starts locked");

        // a budget of three: every goal (each weighs 1; the toy station is in space, so the shield is not set aside)
        r.In(() =>
        {
            ((datum_config_entry)config.entries_by_type[typeof(datum_config_entry_flag_no_dynamic_report)]).config_entry_value = TRUE;
            ((datum_config_entry)config.entries_by_type[typeof(datum_config_entry_number_station_goal_budget)]).config_entry_value = 3;
            Waiting.NoSleep(GLOB.communications_controller.send_roundstart_report(FALSE));
        });
        r.Step(Second);
        var goals = r.In(() => Dm.each_cast<datum_station_goal>(SSstation.get_station_goals()).ToList());
        Assert.Equal(3, goals.Count);
        Assert.Contains(goals, g => g is datum_station_goal_bluespace_cannon);
        Assert.Contains(goals, g => g is datum_station_goal_dna_vault);
        Assert.Contains(goals, g => g is datum_station_goal_station_shield);
        Assert.Same(goals.OfType<datum_station_goal_dna_vault>().Single(), r.In(() => SSstation.get_station_goal(typeof(datum_station_goal_dna_vault))));
        foreach (Type pack in goalPacks)
            Assert.True(r.In(() => Dm.truthy(Dm.bitand(((datum_supply_pack)packs[pack]).order_flags, ORDER_SPECIAL_ENABLED))), pack.Name + " is unlocked");

        // the console got the report with the three directives
        string report = r.In(() => Dm.each_cast<datum_comm_message>(console.messages).Select(m => Dm.str(m.content)).Last());
        Assert.Contains("Special Orders for", report);
        Assert.Matches("We need you to construct BSA-[0-9]+ Artillery position aboard your station.", report);
        Assert.Contains("We need you to construct a DNA Vault aboard your station.", report);
        var vaultGoal = goals.OfType<datum_station_goal_dna_vault>().Single();
        float animals = r.In(() => Dm.num(vaultGoal.animal_count));
        Assert.InRange(animals, 16, 20);
        Assert.Contains($"* {animals} unique animal data", report);
        Assert.Contains("We have a prototype shielding system you must deploy to reduce collision-related accidents.", report);

        // the round's end: nothing built, every goal failed
        string result = r.In(() => Dm.str(SSticker.goal_report()));
        Assert.Contains("<li>Bluespace Artillery : <span class='redtext'>Failed!</span></li>", result);
        Assert.Contains("<li>DNA Vault : <span class='redtext'>Failed!</span></li>", result);
        Assert.Contains("<li>Station Shield : <span class='redtext'>Failed!</span></li>", result);

        // a goal of a type that exists already is refused, and a deleted goal leaves the subsystem
        r.In(() => qdel(vaultGoal));
        Assert.Equal(2, r.In(() => (int)Dm.length(SSstation.get_station_goals() as DmList)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGreenshiftAuthorizesEverySpecialOrderAndAPlanetaryBudgetOfOnePicksOneGoal()
    {
        var (r, _, _) = FloorRoom(5102);
        var console = Place<obj_machinery_computer_communications>(r, 5, 20);
        r.In(() =>
        {
            ((datum_config_entry)config.entries_by_type[typeof(datum_config_entry_flag_no_dynamic_report)]).config_entry_value = TRUE;
            Waiting.NoSleep(GLOB.communications_controller.send_roundstart_report(TRUE));
        });
        r.Step(Second);
        // a greenshift has the budget of INFINITY: every goal is made, and the report authorizes them all instead of listing them
        Assert.Equal(3, r.In(() => (int)Dm.length(SSstation.get_station_goals() as DmList)));
        string report = r.In(() => Dm.each_cast<datum_comm_message>(console.messages).Select(m => Dm.str(m.content)).Last());
        Assert.Contains("All special orders have been authorized for the shift.", report);
        Assert.DoesNotContain("BSA-", report);

        // tg's budget of one (the default config) makes one goal
        r.In(() =>
        {
            foreach (datum_station_goal g in Dm.each_cast<datum_station_goal>(SSstation.get_station_goals()))
                qdel(g);
            Waiting.NoSleep(SSstation.generate_station_goals(1));
        });
        Assert.Equal(1, r.In(() => (int)Dm.length(SSstation.get_station_goals() as DmList)));
        r.AssertNoRuntimes();
    }

    // ---- the bluespace artillery ----

    [Fact]
    public void TheArtilleryPartsAreLinkedPreviewedAndDeployedFromTheConsoleWindowThenAimedAtAGpsSignalAndFired()
    {
        var (r, feed, bob) = FloorRoom(5103);
        var back = Place<obj_machinery_bsa_back>(r, 8, 12);
        var middle = Place<obj_machinery_bsa_middle>(r, 13, 12);
        var front = Place<obj_machinery_bsa_front>(r, 18, 12);
        var console = Place<obj_machinery_computer_bsa_control>(r, 13, 7);
        var tool = Hold<obj_item_multitool>(r, bob);

        // the multitool saves a part and the fusor links it
        r.In(() =>
        {
            Waiting.NoSleep(back.multitool_act(bob, tool));
            Assert.Same(back, tool.buffer);
            Waiting.NoSleep(middle.multitool_act(bob, tool));
            Assert.Null(tool.buffer);
            Waiting.NoSleep(front.multitool_act(bob, tool));
            Waiting.NoSleep(middle.multitool_act(bob, tool));
        });
        Assert.True(feed.Any("saved to multitool buffer"));
        Assert.True(feed.Any("You link Bluespace Artillery Fusor with Bluespace Artillery Generator.")); // proper names: no article
        Assert.Same(back, r.In(() => middle.back_ref.resolve()));
        Assert.Same(front, r.In(() => middle.front_ref.resolve()));
        Assert.Equal(EAST, r.In(() => (int)Dm.num(middle.get_cannon_direction())));
        Assert.Null(r.In(() => middle.check_completion()));

        // the console's window: no cannon yet
        r.In(() => Waiting.NoSleep(tool.forceMove(bob.loc)));
        string window = OpenWindow(r, bob, console, "BluespaceArtillery", out JsonElement data);
        Assert.False(data.TryGetProperty("connected", out JsonElement connected) && connected.ValueKind == JsonValueKind.String);
        Assert.Equal(0, data.GetProperty("visualizing").GetInt32());
        Assert.Equal(0, data.GetProperty("unlocked").GetInt32());

        // the preview: the 11 by 3 tiles the cannon will take, east of its fusor's offset
        Act(r, bob, window, "visualize");
        data = Data(r, bob, out _, steps: 2 * Second); // tg's visualize act returns nothing: the window's next process update shows it
        Assert.Equal(1, data.GetProperty("visualizing").GetInt32());
        var marks = r.In(() => Dm.each<obj_effect_clear_color_green>(world).ToList());
        Assert.Equal(33, marks.Count);
        Assert.Equal((9f, 19f, 11f, 13f), r.In(() => (marks.Min(m => m.x), marks.Max(m => m.x), marks.Min(m => m.y), marks.Max(m => m.y))));

        // confirmed: smoke, and the cannon stands where the fusor was; the parts and the preview are gone
        Act(r, bob, window, "build");
        data = Data(r, bob, out _);
        var cannon = r.In(() => Dm.locate<obj_machinery_bsa_full>(world));
        Assert.NotNull(cannon);
        Assert.Equal((13f, 12f, (float)EAST), r.In(() => (cannon.x, cannon.y, (float)Dm.num(cannon.dir))));
        Assert.Equal("cannon_east", r.In(() => cannon.icon_state));
        Assert.Equal((-128f, -128f), r.In(() => ((float)Dm.num(cannon.pixel_x), (float)Dm.num(cannon.bound_x))));
        Assert.Contains("top_east", r.In(() => Dm.each(cannon.overlays).Select(o => Dm.str(o is IDmAppearance a ? a.icon_state : o)).ToList()));
        Assert.True(r.In(() => QDELETED(middle) && QDELETED(front) && QDELETED(back)), "the parts are used up");
        Assert.True(r.In(() => marks.All(m => QDELETED(m))), "the preview is cleared");
        Assert.Equal(JsonValueKind.String, data.GetProperty("connected").ValueKind);
        Assert.Equal(0, data.GetProperty("visualizing").GetInt32());

        // locked until two heads swipe at the keycard reader; then the crosshairs list the GPS signals
        Act(r, bob, window, "recalibrate");
        Assert.Null(ListPrompt(bob));
        var gps = Place<obj_item_gps>(r, 27, 21);
        var victim = r.Dummy(27, 21);
        string tag = r.In(() => Dm.str(gps.GetComponent(typeof(datum_component_gps)) is datum_component_gps g ? g.gpstag : null));
        r.In(() => GLOB.bsa_unlock = TRUE);
        Take(bob);
        Act(r, bob, window, "recalibrate");
        string prompt = ListPrompt(bob);
        Assert.NotNull(prompt);
        Act(r, bob, prompt, "submit", ("entry", tag));
        data = Data(r, bob, out _);
        Assert.Equal(tag, data.GetProperty("target").GetString());
        Assert.Equal(1, data.GetProperty("unlocked").GetInt32());

        // fire: the beam runs from the bore's end to the level's edge, the muzzle splashes, and the target blows up
        turf muzzle = r.In(() => locate(20, 12, 1));
        Act(r, bob, window, "fire");
        Assert.True(r.In(() => Dm.each<obj_effect_temp_visual_bsa_splash>(muzzle).Any(s => s.icon_state == "beam_splash_e")), "the muzzle splash faces east");
        Assert.True(r.In(() => Dm.each<datum_beam>().Any(b => b.icon_state == "bsa_beam")), "the beam");
        Assert.True(r.In(() => Dm.each<obj_effect_temp_visual_bsa_impact>(locate(27, 21, 1)).Any()), "the detonation's picture at the target");
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(victim) || victim.stat == DEAD), "the man at the GPS signal is dead");
        Assert.False(r.In(() => locate(25, 12, 1) is turf_open_floor_iron), "the beam's row is torn up");
        Assert.True(r.In(() => bob.stat != DEAD && !QDELETED(console)), "the gunner and his console are far enough");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABlockerInTheBeamStopsTheShotAndAnEmaggedConsoleBlowsItselfUp()
    {
        var (r, feed, bob) = FloorRoom(5104);
        var cannon = r.In(() => Dm.@new<obj_machinery_bsa_full>(locate(13, 12, 1), EAST));
        var console = Place<obj_machinery_computer_bsa_control>(r, 13, 7);
        r.Step(Second);
        // a supermatter or a singularity carries the blocker: here a crate in the beam's row
        var wall = r.In(() =>
        {
            var crate = Dm.@new<obj_structure_closet_crate>(locate(24, 12, 1));
            Waiting.NoSleep(crate.AddElement(typeof(datum_element_bsa_blocker)));
            return crate;
        });
        var victim = r.Dummy(27, 21);
        r.In(() => Waiting.NoSleep(cannon.fire(bob, locate(27, 21, 1))));
        r.Step(2 * Second);
        Assert.True(r.In(() => victim.stat != DEAD && !QDELETED(victim)), "the shot was blocked");
        Assert.False(r.In(() => Dm.each<obj_effect_temp_visual_bsa_impact>(world).Any()));
        Assert.True(r.In(() => locate(26, 12, 1) is turf_open_floor_iron), "the row behind the blocker is untouched");
        Assert.False(r.In(() => locate(22, 12, 1) is turf_open_floor_iron), "the row before it is torn up");

        // emagged, the console aims at itself
        r.In(() => Waiting.NoSleep(console.emag_act(bob, null)));
        Assert.True(feed.Any("rigged to explode"));
        Assert.True(feed.Any("You get the feeling it wouldn't be wise to stand near"));
        Assert.Same(r.In(() => get_turf(console)), r.In(() => console.get_impact_turf()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheArtilleryNeedsAlignedWrenchedPartsAndFreeFloor()
    {
        var (r, _, bob) = FloorRoom(5105);
        var back = Place<obj_machinery_bsa_back>(r, 8, 12);
        var middle = Place<obj_machinery_bsa_middle>(r, 13, 12);
        var front = Place<obj_machinery_bsa_front>(r, 18, 13);
        Assert.Equal("No linked parts detected!", r.In(() => Dm.str(middle.check_completion())));
        r.In(() =>
        {
            middle.back_ref = WEAKREF(back) as datum_weakref;
            middle.front_ref = WEAKREF(front) as datum_weakref;
        });
        Assert.Equal("Parts misaligned!", r.In(() => Dm.str(middle.check_completion())));
        r.In(() => Waiting.NoSleep(front.forceMove(locate(18, 12, 1))));
        Assert.Null(r.In(() => middle.check_completion()));
        r.In(() => front.anchored = FALSE);
        Assert.Equal("Linked parts unwrenched!", r.In(() => Dm.str(middle.check_completion())));
        r.In(() => front.anchored = TRUE);
        // a wall in the cannon's footprint: the blocked tiles are pointed at
        r.In(() => Waiting.NoSleep(locate(15, 13, 1).ChangeTurf(typeof(turf_closed_wall))));
        Assert.Equal("Not enough free space!", r.In(() => Dm.str(middle.check_completion())));
        Assert.True(r.In(() => Dm.each<obj_effect_temp_visual_point>(locate(15, 13, 1)).Any()));
        r.AssertNoRuntimes();
    }

    // ---- the DNA vault ----

    [Fact]
    public void TheProbeFillsTheVaultAndTheFullVaultGivesEachCrewmanOneOfTwoMutations()
    {
        var (r, feed, bob) = FloorRoom(5106, 15, 16);
        // the goal of a round of one: one sample of each kind
        var goal = r.In(() =>
        {
            var g = Dm.@new<datum_station_goal_dna_vault>();
            g.animal_count = 1;
            g.plant_count = 1;
            g.human_count = 1;
            return g;
        });
        var vault = Place<obj_machinery_dna_vault>(r, 15, 15);
        Assert.Equal((1f, 1f, 1f), r.In(() => (vault.animals_max, vault.plants_max, vault.dna_max)));
        Assert.Equal(6, r.In(() => vault.fillers.len));
        Assert.True(r.In(() => Dm.each<obj_structure_filler>(locate(16, 13, 1)).Any(f => f.parent == vault)), "a filler under the vault's south east corner");

        var alice = r.Dummy(14, 16);
        var mouse = r.In(() => Dm.@new<mob_living_basic_mouse>(locate(16, 16, 1)));
        // the mouse stays put to be sampled (its own AI would wander it off: tg's force_ai_off)
        r.In(() => Waiting.NoSleep((mouse.ai_controller as datum_ai_controller).force_ai_off()));
        var tray = Place<obj_machinery_hydroponics_constructable>(r, 16, 17);
        var probe = Hold<obj_item_dna_probe>(r, bob);

        // not linked yet: nothing is taken
        Use(r, bob, alice);
        Assert.True(feed.Any("balloon: need database!"));
        Use(r, bob, vault);
        Assert.True(feed.Any("balloon: vault linked"));
        Assert.Same(vault, r.In(() => probe.dna_vault_ref.resolve()));

        // a crewman, an animal and a ripe plant
        Use(r, bob, alice);
        Use(r, bob, alice);
        Assert.True(feed.Any("balloon: data already in scanner!"));
        Use(r, bob, mouse);
        r.In(() =>
        {
            tray.myseed = Dm.@new<obj_item_seeds_tomato>(tray);
            tray.plant_status = HYDROTRAY_PLANT_GROWING;
        });
        Use(r, bob, tray);
        Assert.True(feed.Any("balloon: plant is not harvestable!"));
        r.In(() => tray.plant_status = HYDROTRAY_PLANT_HARVESTABLE);
        Use(r, bob, tray);
        Assert.Equal((1f, 1f, 1f), r.In(() => ((float)probe.stored_dna_human.len, (float)probe.stored_dna_animal.len, (float)probe.stored_dna_plants.len)));

        // the upload fills the vault: the ark's shuttle is unlocked and the goal is met
        Use(r, bob, vault);
        Assert.True(feed.Any("3 new datapoints uploaded."));
        Assert.Equal(0f, r.In(() => (float)probe.stored_dna_human.len));
        Assert.Equal(1f, r.In(() => vault.completed));
        Assert.True(r.In(() => Dm.truthy(SSshuttle.shuttle_purchase_requirements_met[SHUTTLE_UNLOCK_ARK])));
        Assert.True(r.In(() => Dm.truthy(goal.check_completion())));
        Use(r, bob, alice);
        Assert.True(feed.Any("balloon: data already in vault!"));

        // the window: the samples against the goal and two treatments drawn for Bob
        r.In(() => Waiting.NoSleep(probe.forceMove(bob.loc)));
        string window = OpenWindow(r, bob, vault, "DnaVault", out JsonElement data);
        Assert.Equal((1, 1, 1, 1, 1, 1), (data.GetProperty("dna").GetInt32(), data.GetProperty("dna_max").GetInt32(), data.GetProperty("plants").GetInt32(),
            data.GetProperty("plants_max").GetInt32(), data.GetProperty("animals").GetInt32(), data.GetProperty("animals_max").GetInt32()));
        Assert.Equal((1, 0), (data.GetProperty("completed").GetInt32(), data.GetProperty("used").GetInt32()));
        string choiceA = data.GetProperty("choiceA").GetString(), choiceB = data.GetProperty("choiceB").GetString();
        string[] vaultMutations = { "Breathless", "Dextrous", "Quick", "Fire Immunity", "Plasmocile", "Quick Recovery", "Tough" };
        Assert.Contains(choiceA, vaultMutations);
        Assert.Contains(choiceB, vaultMutations);
        Assert.NotEqual(choiceA, choiceB);

        // a treatment not drawn for him does nothing; the drawn one is given once
        string other = vaultMutations.First(m => m != choiceA && m != choiceB);
        Act(r, bob, window, "gene", ("choice", other));
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_USED_DNA_VAULT)));
        Act(r, bob, window, "gene", ("choice", choiceA));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_USED_DNA_VAULT)));
        Assert.Equal(choiceA, r.In(() => Dm.each<datum_mutation>(bob.dna.mutations).Select(m => Dm.str(m.name)).FirstOrDefault(n => n == choiceA)));
        data = Data(r, bob, out _);
        Assert.Equal(1, data.GetProperty("used").GetInt32());
        Act(r, bob, window, "gene", ("choice", choiceB));
        Assert.Null(r.In(() => Dm.each<datum_mutation>(bob.dna.mutations).Select(m => Dm.str(m.name)).FirstOrDefault(n => n == choiceB)));
        r.AssertNoRuntimes();
    }

    // ---- the meteor shield ----

    [Fact]
    public void TheSatellitesAreSwitchedOnFromTheirConsoleAndShootDownTheMeteorsInTheirRange()
    {
        var (r, feed, bob) = SpaceRoom(5107);
        var goal = r.In(() => Dm.@new<datum_station_goal_station_shield>());
        var sat = Place<obj_machinery_satellite_meteor_shield>(r, 8, 10);
        var console = Place<obj_machinery_computer_sat_control>(r, 21, 22);
        Assert.Equal(("sat_inactive", 0f), r.In(() => (sat.icon_state, Dm.num(sat.active))));
        float id = r.In(() => Dm.num(sat.id));

        string window = OpenWindow(r, bob, console, "SatelliteControl", out JsonElement data);
        JsonElement listed = data.GetProperty("satellites").EnumerateArray().Single();
        Assert.Equal((id, 0, "M-SHIELD"), (listed.GetProperty("id").GetSingle(), listed.GetProperty("active").GetInt32(), listed.GetProperty("mode").GetString()));
        Assert.Equal((1, 500), (data.GetProperty("meteor_shield").GetInt32(), data.GetProperty("meteor_shield_coverage_max").GetInt32()));

        Act(r, bob, window, "toggle", ("id", id));
        data = Data(r, bob, out _);
        Assert.Equal(1, data.GetProperty("satellites").EnumerateArray().Single().GetProperty("active").GetInt32());
        Assert.Equal(("sat_active", 1f, 1f), r.In(() => (sat.icon_state, Dm.num(sat.active), Dm.num(sat.anchored))));
        Assert.Equal(14f, r.In(() => Dm.num(sat.proximity_monitor.current_range)));
        // its coverage: the tiles it sees within its kill range
        float coverage = r.In(() => Dm.num(goal.get_coverage()));
        Assert.True(coverage > 200, "coverage " + coverage);
        Assert.False(r.In(() => Dm.truthy(goal.check_completion())), "one satellite does not cover the goal's 500 tiles");

        // a meteor flying at the station through the satellite's range is shot down with the satellite's beam
        var meteor = r.In(() => Dm.@new<obj_effect_meteor_medium>(locate(8, 38, 1), locate(8, 3, 1)));
        float lastY = 38;
        bool beamed = false;
        for (int i = 0; i < 15 * Second && !r.In(() => QDELETED(meteor)); i++)
        {
            lastY = r.In(() => meteor.y);
            r.Step(1);
            beamed |= r.In(() => Dm.each<datum_beam>().Any(b => b.icon_state == "sat_beam" && b.origin == get_turf(sat)));
        }
        Assert.True(r.In(() => QDELETED(meteor)), "the meteor is gone; " + string.Join("\n", r.Room.RuntimeText));
        Assert.True(beamed, "by the satellite's beam");
        Assert.InRange(lastY, 11, 25); // in range (14 tiles), before it reached the satellite

        // off from the console
        Act(r, bob, window, "toggle", ("id", id));
        r.Step(Second);
        Assert.Equal(("sat_inactive", 0f), r.In(() => (sat.icon_state, Dm.num(sat.active))));
        Assert.Equal(0f, r.In(() => Dm.num(sat.proximity_monitor.current_range)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASatelliteIsSwitchedByHandOnlyInSpaceAndItsEmagDoublesTheMeteorEvents()
    {
        var (r, feed, bob) = SpaceRoom(5108, 9, 10);
        var sat = Place<obj_machinery_satellite_meteor_shield>(r, 10, 10);
        var indoors = Place<obj_machinery_satellite_meteor_shield>(r, 21, 21);
        var other = Place<obj_machinery_satellite_meteor_shield>(r, 12, 30);

        // by hand: looking for the button, then on
        Click(r, bob, sat);
        Assert.True(feed.Any("balloon: looking for on button"));
        r.Step(3 * Second);
        Assert.True(feed.Any("You activate the Meteor Shield Satellite."));
        Assert.Equal(1f, r.In(() => Dm.num(sat.active)));
        // indoors it refuses
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(indoors.toggle()))));
        Assert.Equal(0f, r.In(() => Dm.num(indoors.active)));

        // the multitool's readout
        var tool = Hold<obj_item_multitool>(r, bob);
        r.In(() => Waiting.NoSleep(sat.multitool_act(bob, tool)));
        Assert.True(feed.Any(r.In(() => Dm.text("// NTSAT-[] // Mode : PRIMARY //", sat.id))));

        // the emag doubles the meteor events' weight while the tampered satellite is on, and gives off a GPS signal
        float wave = r.In(() => Dm.each<datum_round_event_control_meteor_wave>(SSevents.control).First().weight);
        float stray = r.In(() => Dm.each<datum_round_event_control_stray_meteor>(SSevents.control).First().weight);
        r.In(() => Assert.Equal(TRUE, Dm.num(Waiting.NoSleep(sat.emag_act(bob, null)))));
        r.Step(Second);
        Assert.True(feed.Any("You access the satellite's debug mode"));
        Assert.True(feed.Any("balloon: meteor probability doubled"));
        Assert.Equal(wave * 2, r.In(() => Dm.each<datum_round_event_control_meteor_wave>(SSevents.control).First().weight));
        Assert.Equal(stray * 2, r.In(() => Dm.each<datum_round_event_control_stray_meteor>(SSevents.control).First().weight));
        Assert.Equal("Corrupted Meteor Shield Attraction Signal", r.In(() => (sat.GetComponent(typeof(datum_component_gps)) as datum_component_gps)?.gpstag));
        Assert.True(r.In(() => Dm.each<string>(Waiting.NoSleep(sat.examine(bob))).Any(l => l.Contains("white noise", StringComparison.Ordinal))), "an emagged satellite hisses");

        // a second satellite within the minute: on cooldown
        r.In(() => Assert.Equal(FALSE, Dm.num(Waiting.NoSleep(other.emag_act(bob, null)))));
        Assert.True(feed.Any("balloon: on cooldown!"));
        Assert.True(feed.Any("to recalibrate first."));

        // the console cannot reach a tampered satellite
        var console = Place<obj_machinery_computer_sat_control>(r, 21, 22);
        r.In(() =>
        {
            usr = bob;
            Waiting.NoSleep(console.toggle(sat.id));
            usr = null;
        });
        Assert.True(feed.Any("The satellite doesn't seem to respond...?"));
        Assert.Equal(1f, r.In(() => Dm.num(sat.active)));

        // destroyed, the tampered satellite stops counting: the weights halve back
        r.In(() => qdel(sat));
        Assert.Equal(wave, r.In(() => Dm.each<datum_round_event_control_meteor_wave>(SSevents.control).First().weight));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDarkMatteorReflectsTheBeamAndBlowsUpTheSatellite()
    {
        var (r, _, _) = SpaceRoom(5109);
        var sat = Place<obj_machinery_satellite_meteor_shield>(r, 8, 10);
        r.In(() => Waiting.NoSleep(sat.toggle()));
        Assert.Equal(1f, r.In(() => Dm.num(sat.active)));
        var matteor = r.In(() => Dm.@new<obj_effect_meteor_dark_matteor>(locate(8, 20, 1), locate(8, 3, 1)));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(sat)), "the satellite is blown up");
        Assert.False(r.In(() => QDELETED(matteor)), "the dark matt-eor flies on");
        r.AssertNoRuntimes();
    }
}
