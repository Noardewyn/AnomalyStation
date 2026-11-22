using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>A multitool that counts the removals of its buffer (fixture, rules 15).</summary>
[DmPath("/obj/item/multitool/test_t1_listener")]
public class obj_item_multitool_test_t1_listener : obj_item_multitool
{
    public int removed;
    public datum last_removed;

    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_MULTITOOL_REMOVE_BUFFER, nameof(heard));
        return dot;
    }

    [SignalHandler]
    public void heard(datum source = null, datum removed_buffer = null)
    {
        removed++;
        last_removed = removed_buffer;
    }
}

/// <summary>
/// M7 slice T1 (D164): the electrical tool kit. The multitool (multitool.dm: its buffer and the signals of its removal, examine,
/// the APC locator with its arrow, the reading of a cable), the T-ray scanner (t_scanner.dm: its toggle, its process and the
/// pulse of images that show the hidden under-floor objects to its holder only), the utility belts (belt.dm: the storage lists,
/// the slots, the overlays of the tools inside and the contents of the full ones) and the electrical toolbox (service.dm:2).
/// </summary>
public class ToolKitTests
{
    private const int Second = 20;
    private const int Y = 4;

    private static string Said(object examined) => examined is DmList l ? string.Join(" ", Dm.each(l).Select(Dm.str)) : Dm.str(examined);

    private static AudioRoom Room(ulong seed, Action<World> extras = null) => new AudioRoom(13, 10, seed: seed, mapExtras: extras);

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        foreach (object held in Dm.each(who.held_items.Copy()))
        {
            if (held is obj_item old)
                qdel(old);
        }
        T item = Dm.@new<T>(get_turf(who));
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(item, merge_stacks: FALSE))), "the item reaches a hand");
        return item;
    });

    // ---- the multitool ----

    [Fact]
    public void TheMultitoolsBufferHoldsAnObjectShowsInExamineAndIsClearedWithTheRemovalSignalWhenItsObjectIsDeleted()
    {
        var r = Room(8101);
        mob_living_carbon_human bob = r.Join(4, Y, "Bob");
        r.In(() =>
        {
            var tool = Dm.@new<obj_item_multitool_test_t1_listener>(get_turf(bob));
            Assert.Contains("is empty", Said(Waiting.NoSleep(tool.examine(bob))));

            var first = Dm.@new<obj_item_screwdriver>(get_turf(bob));
            var second = Dm.@new<obj_item_wrench>(get_turf(bob));
            tool.set_buffer(first);
            Assert.Same(first, tool.buffer);
            Assert.Contains("contains", Said(Waiting.NoSleep(tool.examine(bob))));
            Assert.Contains(Dm.str(first.name), Said(Waiting.NoSleep(tool.examine(bob))));
            Assert.Equal(0, tool.removed);

            // a new buffer first removes the old one (multitool.dm:131-134): the signal names the old object
            tool.set_buffer(second);
            Assert.Same(second, tool.buffer);
            Assert.Equal(1, tool.removed);
            Assert.Same(first, tool.last_removed);
            Assert.False(Dm.truthy(first._listen_lookup?[COMSIG_QDELETING])); // the old object is no longer watched

            // the object going away (multitool.dm:145): the buffer clears and the signal names the object
            qdel(second);
            Assert.Null(tool.buffer);
            Assert.Equal(2, tool.removed);
            Assert.Same(second, tool.last_removed);
            Assert.Contains("is empty", Said(Waiting.NoSleep(tool.examine(bob))));

            // the multitool itself going away with an object in its buffer (Destroy, :42)
            var third = Dm.@new<obj_item_crowbar>(get_turf(bob));
            tool.set_buffer(third);
            qdel(tool);
            Assert.Equal(3, tool.removed);
            Assert.False(Dm.truthy(third._listen_lookup?[COMSIG_QDELETING]) && Dm.truthy(((DmList)third._listen_lookup[COMSIG_QDELETING]).Count));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMultitoolReadsACablesNetworkThroughTheCablesOwnMultitoolAct()
    {
        var r = Room(8102, w =>
        {
            for (int x = 2; x <= 7; x++)
            {
                Dm.@new<turf_open_floor_plating>(locate(x, Y, 1));
                Dm.@new<obj_structure_cable>(locate(x, Y, 1));
            }
            Dm.@new<obj_machinery_power_test_m7apc_source>(locate(7, Y, 1));
        });
        mob_living_carbon_human bob = r.Join(4, Y + 1, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var tool = Hold<obj_item_multitool>(r, bob);
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        obj_structure_cable cable = r.In(() => Dm.locate<obj_structure_cable>(locate(4, Y, 1)));
        r.Command(() => ClientSeam.Click(bob.client, cable, "left=1;button=left"));
        r.Step(Second);
        Assert.Contains(chat.Html, h => h.Contains("The cable is not powered", StringComparison.Ordinal));

        var source = r.In(() => Dm.locate<obj_machinery_power_test_m7apc_source>(locate(7, Y, 1)));
        r.In(() => source.output_energy = 50000);
        r.Step(Second * 6);
        chat.Html.Clear();
        r.Command(() => ClientSeam.Click(bob.client, cable, "left=1;button=left"));
        r.Step(Second);
        Assert.True(chat.Html.Any(h => h.Contains("Total power:", StringComparison.Ordinal) && h.Contains("Load:", StringComparison.Ordinal)), string.Join("\n", chat.Html) + "\nRUNTIMES: " + string.Join("\n", r.Room.RuntimeText));
        Assert.Same(tool, r.In(() => bob.get_active_held_item()));
        r.AssertNoRuntimes();
    }

    private static (AudioRoom R, mob_living_carbon_human Bob, obj_machinery_power_apc Apc) ApcRoom(ulong seed, int bobX)
    {
        obj_machinery_power_apc apc = null;
        var r = Room(seed, w =>
        {
            ((area)GLOB.areas_by_type[typeof(area_misc_testroom)]).requires_power = TRUE;
            for (int x = 2; x <= 9; x++)
                Dm.@new<obj_structure_cable>(locate(x, 2, 1));
            apc = Dm.@new<obj_machinery_power_apc_auto_name_directional_south>(locate(3, 2, 1));
        });
        mob_living_carbon_human bob = r.Join(bobX, 3, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob, apc);
    }

    [Fact]
    public void TheMultitoolInHandLocatesTheAreasApcWithAnArrowOfTgsColourAndTheTwoSecondCooldown()
    {
        var (r, bob, apc) = ApcRoom(8103, 12);
        var tool = Hold<obj_item_multitool>(r, bob);
        var screen = typeof(atom_movable_screen_multitool_arrow);
        // 9 tiles away: the yellow band (6 to 10)
        r.In(() => Assert.Equal(9f, get_dist(tool, apc)));
        r.Command(() => Waiting.NoSleep(tool.attack_self(bob)));
        r.Step(2);
        atom_movable_screen_multitool_arrow arrow = r.In(() => bob.hud_used.screen_objects[HUD_MULTITOOL_ARROW] as atom_movable_screen_multitool_arrow);
        Assert.NotNull(arrow);
        r.In(() =>
        {
            Assert.Equal(COLOR_YELLOW, Dm.str(arrow.color));
            Assert.Equal(HUD_GROUP_INFO, (float)arrow.hud_group_key);
            Assert.Equal("icons/effects/96x96.dmi", Dm.str(arrow.icon).Replace("\\", "/"));
            Assert.Equal("multitool_arrow", arrow.icon_state);
            Assert.Equal("CENTER-1,CENTER-1", arrow.screen_loc);
            // rotated by the angle of the direction from the player to the APC (multitool.dm:100-101)
            matrix expected = Dm.matrix(Dm.num(dir2angle(get_dir(bob, apc))), MATRIX_ROTATE);
            var actual = (matrix)arrow.transform;
            Assert.Equal(expected.a, actual.a, 3);
            Assert.Equal(expected.b, actual.b, 3);
            Assert.Equal(expected.d, actual.d, 3);
            Assert.Equal(expected.e, actual.e, 3);
        });

        // the cooldown (2 s) swallows the second use; the arrow is deleted after 1.5 s
        r.Command(() => Waiting.NoSleep(tool.attack_self(bob)));
        r.Step(2);
        r.In(() => Assert.Same(arrow, bob.hud_used.screen_objects[HUD_MULTITOOL_ARROW]));
        r.Step((int)(1.5f * Second));
        r.In(() =>
        {
            Assert.True(QDELETED(arrow));
            Assert.Null(bob.hud_used.screen_objects[HUD_MULTITOOL_ARROW]);
        });

        // after the cooldown a use from the tile next to the APC gives the green band, and on it "found apc!"
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(4, 3, 1))));
        r.Command(() => Waiting.NoSleep(tool.attack_self(bob)));
        r.Step(2);
        r.In(() =>
        {
            var green = bob.hud_used.screen_objects[HUD_MULTITOOL_ARROW] as atom_movable_screen_multitool_arrow;
            Assert.NotNull(green);
            Assert.Equal(COLOR_GREEN, Dm.str(green.color));
        });
        _ = screen;
        r.AssertNoRuntimes();
    }

    // ---- the T-ray scanner ----

    private static (AudioRoom R, mob_living_carbon_human Bob, mob_living_carbon_human Eve) TRayRoom(ulong seed)
    {
        var r = Room(seed, w =>
        {
            // iron tiles over the room (the cables and the pipe under them are hidden by tg's undertile element)
            for (int x = 1; x <= 12; x++)
                for (int y = 1; y <= 9; y++)
                    Dm.@new<turf_open_floor_iron>(locate(x, y, 1));
            for (int x = 2; x <= 11; x++)
                Dm.@new<obj_structure_cable>(locate(x, 3, 1));
            Dm.@new<obj_structure_cable>(locate(5, 7, 1));
        });
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        mob_living_carbon_human eve = r.Join(11, 5, "Eve");
        r.Step(Second);
        return (r, bob, eve);
    }

    [Fact]
    public void TheTRayScannerTogglesWithItsSoundAndIconStateAndItsScanShowsTheHiddenCablesInRangeToItsHolderOnlySteadily()
    {
        var (r, bob, eve) = TRayRoom(8104);
        List<obj_structure_cable> cables = r.In(() => Dm.each(world.contents).OfType<obj_structure_cable>().ToList());
        r.In(() =>
        {
            Assert.All(cables, c => Assert.True(Dm.truthy(HAS_TRAIT(c, TRAIT_T_RAY_VISIBLE)), "the undertile element marks a cable under a tile"));
            Assert.All(cables, c => Assert.Equal(INVISIBILITY_MAXIMUM, Dm.num(c.invisibility)));
        });
        var scanner = Hold<obj_item_t_scanner>(r, bob);
        r.Sounds.Heard.Clear();
        r.In(() =>
        {
            Assert.Equal("t-ray0", scanner.icon_state);
            Assert.Equal(0, bob.client.images.Count);
        });

        // attack_self toggles it on: the industrial scan sound, the icon state "t-ray1", processing
        r.Command(() => Waiting.NoSleep(scanner.attack_self(bob)));
        r.Step(Second * 2 + 4);
        r.In(() =>
        {
            Assert.True(Dm.truthy(scanner.on));
            Assert.Equal("t-ray1", scanner.icon_state);
        });
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("industrial_scan", StringComparison.Ordinal));

        // the pulse runs with SSobj (every 2 s): the holder's client holds an image of each hidden cable in range (3 tiles, orange),
        // at the cable's tile, at tg's alpha 128 and with the cable's own icon state; nothing for the far ones or for the other player
        bool seen = false;
        for (int i = 0; i < Second * 3 && !seen; i++)
        {
            r.Step(1);
            seen = r.In(() => bob.client.images.Count > 0);
        }
        Assert.True(seen, "the pulse shows images to the holder");
        r.In(() =>
        {
            var shown = Dm.each_cast<image>(bob.client.images).ToList();
            // Bob stands at (3, 5): the cables of row 3 within x 1-6 (distance 3: x 0-6, y 2-8) and the one at (5, 7)
            var expectedCables = cables.Where(c => Math.Abs(c.x - 3) <= 3 && Math.Abs(c.y - 5) <= 3).ToList();
            Assert.Equal(expectedCables.Count, shown.Count);
            Assert.True(shown.Count >= 5, "cables in range " + shown.Count);
            foreach (obj_structure_cable c in expectedCables)
            {
                image shownAt = shown.SingleOrDefault(i => ReferenceEquals(i.loc, get_turf(c)) && i.icon_state == c.icon_state);
                Assert.NotNull(shownAt);
                Assert.Equal(128f, shownAt.alpha);
                Assert.Equal(c.dir, shownAt.dir);
            }
            Assert.DoesNotContain(shown, i => i.loc is turf t && t.x > 6);
            // Eve, 8 tiles away, is not the holder: her client sees none of it
            Assert.Equal(0, eve.client.images.Count);
        });

        // DV91 / D173: a steady view, not tg's 0.8 s pulse every 2 s: at every tick across several scans the client holds the one set
        // (never none, never two copies of it)
        int n = r.In(() => bob.client.images.Count);
        for (int t = 0; t < Second * 7; t++)
        {
            r.Step(1);
            r.In(() =>
            {
                Assert.Equal(n, bob.client.images.Count);
                Assert.Equal(n, Dm.each_cast<image>(bob.client.images).Distinct().Count());
            });
        }

        // off again: processing stops (PROCESS_KILL on the next process), the last set goes by its own timer within about 2.5 s
        r.Command(() => Waiting.NoSleep(scanner.attack_self(bob)));
        r.Step((int)(2.6f * Second));
        r.In(() =>
        {
            Assert.False(Dm.truthy(scanner.on));
            Assert.Equal("t-ray0", scanner.icon_state);
            Assert.Equal(0, bob.client.images.Count);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTRayScanPulseOfAFloorLaidOverNothingShowsNothingAndOnlyAMobWithAClientScans()
    {
        var (r, bob, eve) = TRayRoom(8105);
        r.In(() =>
        {
            // a scanner on the floor (loc is a turf) or in the hand of a player without a client scans nothing
            var dummy = Dm.@new<mob_living_carbon_human>(locate(5, 5, 1));
            t_ray_scan(dummy);
            t_ray_scan(null);
            Assert.Equal(0, bob.client.images.Count);
            // the pulse in its own right: distance and time as the caller gives them
            t_ray_scan(bob, 4, 2);
            Assert.Equal(5, bob.client.images.Count); // the cables of row 3 at x 2-5 and the one at (5, 7)
            Assert.Equal(0, eve.client.images.Count);
        });
        r.Step(5);
        r.In(() => Assert.NotEqual(0, bob.client.images.Count));
        r.Step(Second);
        r.In(() => Assert.NotEqual(0, bob.client.images.Count)); // DV91: shown until the next scan is due and half a second more
        r.Step(Second * 2);
        r.In(() => Assert.Equal(0, bob.client.images.Count));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTRayScannerRescansTheMomentItsHolderStepsToAnotherTile()
    {
        var (r, bob, _) = TRayRoom(8106);
        var scanner = Hold<obj_item_t_scanner>(r, bob);
        r.Command(() => Waiting.NoSleep(scanner.attack_self(bob)));
        r.Step(Second);
        r.In(() => Assert.DoesNotContain(Dm.each_cast<image>(bob.client.images), i => i.loc is turf t && t.x > 6));
        // DV91 (D289): tg waits for the next SSobj fire (up to 2 s); the port scans on the scanner's next fast process after the step
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(8, 5, 1))));
        r.Step(Second / 2);
        r.In(() => Assert.Contains(Dm.each_cast<image>(bob.client.images), i => i.loc is turf t && t.x == 11));
        r.AssertNoRuntimes();
    }

    // ---- the belts ----

    [Fact]
    public void AFullToolbeltCanBeQuickEquippedOverAUniform()
    {
        var r = Room(8199);
        var bob = r.Join(4, Y, "Bob");
        var belt = r.In(() => Dm.@new<obj_item_storage_belt_utility_full_engi>(get_turf(bob)));
        r.In(() =>
        {
            if (bob.w_uniform == null)
                Waiting.NoSleep(bob.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(bob), ITEM_SLOT_ICLOTHING));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(belt))));
        });
        r.Command(() => ClientSeam.Keybinding(bob.client, 6, true));
        r.Step(10);
        Assert.Same(belt, r.In(() => bob.belt));
        atom_movable_screen_inventory slot = null;
        r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(belt));
            Waiting.NoSleep(bob.put_in_hands(belt));
            slot = bob.hud_used.screen_objects[Dm.text("item_slot:[]", ITEM_SLOT_BELT)] as atom_movable_screen_inventory;
            Assert.NotNull(slot);
        });
        r.Command(() => ClientSeam.Click(bob.client, slot, "left=1;button=left"));
        r.Step(10);
        Assert.Same(belt, r.In(() => bob.belt));
        Assert.True(r.In(() => Dm.@in(belt, slot.vis_contents)));
        r.AssertNoRuntimes();
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o switch
    {
        string t => t,
        mutable_appearance m => m.icon_state,
        DmAppearance d => d.icon_state,
        _ => null,
    }).Where(s => s != null).ToList());

    private static List<Type> Contents(AudioRoom r, atom a) => r.In(() => Dm.each_cast<atom>(a.contents).Select(c => c.GetType()).ToList());

    [Fact]
    public void TheFullEngineeringBeltHoldsTgsSevenToolsShowsThemOnItAndRefusesAnEighthAndWhatTgsListDoesNotHold()
    {
        var r = Room(8106);
        mob_living_carbon_human bob = r.Join(4, Y, "Bob");
        obj_item_storage_belt_utility_full_engi belt = r.In(() => Dm.@new<obj_item_storage_belt_utility_full_engi>(get_turf(bob)));
        r.Step(2);
        Assert.Equal(new[]
        {
            typeof(obj_item_screwdriver), typeof(obj_item_wrench), typeof(obj_item_weldingtool_largetank), typeof(obj_item_crowbar),
            typeof(obj_item_wirecutters), typeof(obj_item_multitool), typeof(obj_item_stack_cable_coil),
        }, Contents(r, belt));
        r.In(() =>
        {
            Assert.Equal("toolbelt", belt.name);
            Assert.Equal(WEIGHT_CLASS_BULKY, (float)belt.w_class);
            Assert.Equal(ITEM_SLOT_BELT, (float)belt.slot_flags);
            Assert.Equal(7f, Dm.num(belt.atom_storage.max_slots));
            Assert.Equal(21f, Dm.num(belt.atom_storage.max_total_storage));
            // the tools show on the belt: one overlay of belt_overlays.dmi for each, in the contents' order
            Assert.Equal(7, belt.overlays.Count);
            // belt_overlays.dmi, or the colour-composed GAGS icon of the screwdriver and the wirecutters (greyscale_config_belt, items.dm:923)
            var icons = Dm.each(belt.overlays).Select(o => Dm.str(((IDmAppearance)o).icon).Replace("\\", "/")).ToList();
            Assert.Equal(5, icons.Count(i => i == "icons/obj/clothing/belt_overlays.dmi"));
            Assert.Equal(2, icons.Count(i => i.StartsWith("gags:/datum/greyscale_config/", StringComparison.Ordinal)));
        });
        List<string> shown = Overlays(r, belt);
        Assert.Contains("multitool", shown);
        Assert.Contains("wrench", shown);
        Assert.Contains("crowbar", shown);
        Assert.Contains("screwdriver", shown);

        r.In(() =>
        {
            // the belt is full (7 slots): a flashlight, which the list holds, is refused for lack of a slot
            var flashlight = Dm.@new<obj_item_flashlight>(get_turf(bob));
            Assert.False(Dm.truthy(belt.atom_storage.can_insert(flashlight, bob, FALSE)));
            // taking the multitool out drops its overlay and makes room for the flashlight
            var multitool = Dm.each(belt.contents).OfType<obj_item_multitool>().Single();
            Assert.True(Dm.truthy(Waiting.NoSleep(belt.atom_storage.remove_single(bob, multitool, get_turf(bob)))));
            Assert.Equal(6, belt.overlays.Count);
            Assert.DoesNotContain("multitool", Overlays(r, belt));
            Assert.True(Dm.truthy(belt.atom_storage.attempt_insert(flashlight, bob)));
            Assert.Equal(7, belt.overlays.Count);
            // what tg's list does not hold is refused: a toolbox, an ID card, a bike horn
            Assert.True(Dm.truthy(Waiting.NoSleep(belt.atom_storage.remove_single(bob, flashlight, get_turf(bob)))));
            Assert.False(Dm.truthy(belt.atom_storage.can_insert(Dm.@new<obj_item_bikehorn>(get_turf(bob)), bob, FALSE)));
            Assert.False(Dm.truthy(belt.atom_storage.can_insert(Dm.@new<obj_item_card_id>(get_turf(bob)), bob, FALSE)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmptyUtilityBeltHoldsTgsToolListWithGlovesRadiosAndSodaCansAndNothingBulkyOrOutsideIt()
    {
        var r = Room(8107);
        mob_living_carbon_human bob = r.Join(4, Y, "Bob");
        r.In(() =>
        {
            var belt = Dm.@new<obj_item_storage_belt_utility>(get_turf(bob));
            Assert.Equal(0, belt.contents.Count);
            Assert.Equal(0, belt.overlays.Count);
            foreach (Type tool in new[]
            {
                typeof(obj_item_multitool), typeof(obj_item_t_scanner), typeof(obj_item_wrench), typeof(obj_item_screwdriver), typeof(obj_item_crowbar),
                typeof(obj_item_wirecutters), typeof(obj_item_weldingtool), typeof(obj_item_stack_cable_coil), typeof(obj_item_flashlight), typeof(obj_item_radio),
                typeof(obj_item_extinguisher_mini), typeof(obj_item_clothing_gloves_color_yellow),
            })
            {
                var item = (obj_item)Dm.@new(tool, get_turf(bob));
                Assert.True(Dm.truthy(belt.atom_storage.can_insert(item, bob, FALSE)), tool.Name + " is held");
            }
            // outside the list: a toolbox, a bag, a bike horn, an ID card
            foreach (Type other in new[] { typeof(obj_item_storage_toolbox_mechanical), typeof(obj_item_bikehorn), typeof(obj_item_card_id), typeof(obj_item_storage_backpack) })
            {
                var item = (obj_item)Dm.@new(other, get_turf(bob));
                Assert.False(Dm.truthy(belt.atom_storage.can_insert(item, bob, FALSE)), other.Name + " is refused");
            }
            // the total of 21: three bulky... a belt holds up to 21 weight (utility_belt, belts.dm:3)
            Assert.Equal(21f, Dm.num(belt.atom_storage.max_total_storage));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFullBeltsAndTheElectricalToolboxHoldTgsContents()
    {
        var r = Room(8108);
        mob_living_carbon_human bob = r.Join(4, Y, "Bob");
        var made = r.In(() => new Dictionary<string, atom>
        {
            ["full"] = Dm.@new<obj_item_storage_belt_utility_full>(get_turf(bob)),
            ["chief"] = Dm.@new<obj_item_storage_belt_utility_chief_full>(get_turf(bob)),
            ["atmos"] = Dm.@new<obj_item_storage_belt_utility_atmostech>(get_turf(bob)),
            ["inducer"] = Dm.@new<obj_item_storage_belt_utility_full_inducer>(get_turf(bob)),
            ["syndicate"] = Dm.@new<obj_item_storage_belt_utility_syndicate>(get_turf(bob)),
            ["powertools"] = Dm.@new<obj_item_storage_belt_utility_full_powertools>(get_turf(bob)),
            ["rcd"] = Dm.@new<obj_item_storage_belt_utility_full_powertools_rcd>(get_turf(bob)),
        });
        r.Step(2);
        r.AssertNoRuntimes();
        Assert.Equal(new[]
        {
            typeof(obj_item_screwdriver), typeof(obj_item_wrench), typeof(obj_item_weldingtool), typeof(obj_item_crowbar),
            typeof(obj_item_wirecutters), typeof(obj_item_multitool), typeof(obj_item_stack_cable_coil),
        }, Contents(r, made["full"]));
        Assert.Equal(new[]
        {
            typeof(obj_item_screwdriver_power), typeof(obj_item_crowbar_power), typeof(obj_item_weldingtool_experimental), typeof(obj_item_multitool),
            typeof(obj_item_stack_cable_coil), typeof(obj_item_extinguisher_mini), typeof(obj_item_analyzer),
        }, Contents(r, made["chief"]));
        Assert.Equal(new[]
        {
            typeof(obj_item_screwdriver), typeof(obj_item_wrench), typeof(obj_item_weldingtool), typeof(obj_item_crowbar),
            typeof(obj_item_wirecutters), typeof(obj_item_t_scanner), typeof(obj_item_extinguisher_mini),
        }, Contents(r, made["atmos"]));
        Assert.Equal(new[]
        {
            typeof(obj_item_screwdriver), typeof(obj_item_wrench), typeof(obj_item_weldingtool), typeof(obj_item_crowbar_red),
            typeof(obj_item_wirecutters), typeof(obj_item_multitool), typeof(obj_item_inducer),
        }, Contents(r, made["inducer"]));
        Assert.Equal(new[]
        {
            typeof(obj_item_screwdriver_nuke), typeof(obj_item_wrench_combat), typeof(obj_item_weldingtool_largetank), typeof(obj_item_crowbar),
            typeof(obj_item_wirecutters), typeof(obj_item_multitool), typeof(obj_item_inducer_syndicate),
        }, Contents(r, made["syndicate"]));
        Assert.Equal(new[]
        {
            typeof(obj_item_screwdriver_power), typeof(obj_item_crowbar_power), typeof(obj_item_weldingtool_experimental), typeof(obj_item_multitool),
            typeof(obj_item_holosign_creator_atmos), typeof(obj_item_extinguisher_mini), typeof(obj_item_stack_cable_coil),
        }, Contents(r, made["powertools"]));
        Assert.Equal(new[]
        {
            typeof(obj_item_screwdriver_power), typeof(obj_item_crowbar_power), typeof(obj_item_weldingtool_experimental), typeof(obj_item_multitool),
            typeof(obj_item_construction_rcd_loaded_upgraded), typeof(obj_item_extinguisher_mini), typeof(obj_item_stack_cable_coil),
        }, Contents(r, made["rcd"]));
        r.In(() =>
        {
            Assert.Equal("chief engineer's toolbelt", made["chief"].name);
            Assert.Equal("utility_ce", made["chief"].icon_state);
            Assert.Equal(7, made["full"].overlays.Count);
            // get_types_to_preload (:88): the wardrobe's list of the same seven types
            var preload = Dm.each(((obj_item_storage_belt_utility_full)made["full"]).get_types_to_preload()).ToList();
            Assert.Equal(7, preload.Count);
            Assert.Contains(typeof(obj_item_multitool), preload);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheElectricalToolboxHoldsAScrewdriverCuttersTRayScannerCrowbarAndTwoOrThreeFullCoilsOfOneColourOrGloves()
    {
        var r = Room(8109);
        mob_living_carbon_human bob = r.Join(4, Y, "Bob");
        int gloves = 0, threeCoils = 0;
        for (int i = 0; i < 80; i++)
        {
            var box = r.In(() => Dm.@new<obj_item_storage_toolbox_electrical>(get_turf(bob)));
            r.In(() =>
            {
                Assert.Equal("electrical toolbox", box.name);
                Assert.Equal("yellow", box.icon_state);
                var types = Dm.each_cast<atom>(box.contents).ToList();
                Assert.Single(types, c => c is obj_item_screwdriver);
                Assert.Single(types, c => c is obj_item_wirecutters);
                Assert.Single(types, c => c is obj_item_t_scanner);
                Assert.Single(types, c => c is obj_item_crowbar);
                var coils = types.OfType<obj_item_stack_cable_coil>().ToList();
                var yellow = types.OfType<obj_item_clothing_gloves_color_yellow>().ToList();
                Assert.All(coils, c => Assert.Equal((float)MAXCOIL, Dm.num(c.amount)));
                Assert.Single(coils.Select(c => Dm.str(c.cable_color)).Distinct());
                if (yellow.Count == 1)
                {
                    gloves++;
                    Assert.Equal(2, coils.Count);
                }
                else
                {
                    Assert.Empty(yellow);
                    Assert.Equal(3, coils.Count);
                    threeCoils++;
                }
                qdel(box);
            });
        }
        // prob(5): gloves instead of the third coil
        Assert.InRange(gloves, 1, 15);
        Assert.Equal(80 - gloves, threeCoils);
        r.AssertNoRuntimes();
    }
}
