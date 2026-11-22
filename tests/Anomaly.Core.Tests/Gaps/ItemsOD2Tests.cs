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
/// Gaps package OD2 (D333): the items' odds, part 2, in a booted toy room with Bob as a player: the V8 engine and the House Edge's
/// fire charges and blazing slash, the street drugs (the meth embedding, the raw opium pressed together, the bLaSToFF ampoule), the
/// flatpack deployed with a multitool and the flatpack cart, the piggy bank's cash and rattle, the ethereal disco ball, the debug
/// human spawner and omnitool, the Hippocrates bust's oath and HUD, the Maneki-Neko, the virgin mary's mafia nickname, the rolling
/// table dock, the bot pad and its controller, the carp DNA sampler, the lead-acid battery, the hair dye spray, the gun maintenance
/// kit and the pitchfork.
/// </summary>
public class ItemsOD2Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who, Action<T> setup = null) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        if (setup != null)
            r.In(() => setup(item));
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    // the act in tg's input window of that interface the player has open (AlertModal, ListInputWindow, TextInputModal, ColorPickerModal)
    private static void ActIn(AudioRoom r, mob who, string iface, string action, params (string Key, object Value)[] args)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() =>
        {
            DmList payload = Dm.list();
            foreach ((string key, object value) in args)
                payload[key] = value;
            ClientSeam.TguiMessage(who.client, window, "act/" + action, payload);
        });
        r.Step(4);
    }

    private static void PickRadial(AudioRoom r, mob who, string name)
    {
        r.Step(2);
        (long id, int index) = r.In(() =>
        {
            datum_radial_menu menu = Dm.each_cast<datum_radial_menu>(who.client.dm_radial_menus).SingleOrDefault();
            Assert.True(menu != null, "no radial menu");
            int i = menu.dm_elements.FindIndex(e => e.Name != null && e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            Assert.True(i >= 0, "no slice " + name + " in " + string.Join(", ", menu.dm_elements.Select(e => e.Name)));
            return (menu.dm_id, i + 1);
        });
        r.Command(() => ClientSeam.RadialPick(who.client, id, index));
        r.Step(6);
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static string Examine(AudioRoom r, atom a, mob who) => r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(a.examine(who))).Select(o => Dm.str(o))));

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    private static float Amount(atom holder, Type reagent) => Dm.num(((datum_reagents)holder.reagents).get_reagent_amount(reagent));

    [Fact]
    public void TheV8EngineRoarsAndItsLabelTeachesTheHouseEdge()
    {
        var (r, feed, bob) = Room(9301);
        var engine = Hold<obj_item_v8_engine>(r, bob);
        r.In(() =>
        {
            Assert.NotNull(engine.GetComponent(typeof(datum_component_two_handed)));
            Waiting.NoSleep(engine.attack_self(bob));
        });
        Assert.True(Heard(r, "car_engine_start"));
        Assert.True(feed.Any("too old to keep on without retrofitting"));
        r.In(() => Assert.True(engine.engine_sound_cooldown > world.time));

        r.In(() => Assert.False(Dm.truthy(bob.mind.has_crafting_recipe(typeof(datum_crafting_recipe_house_edge)))));
        r.Command(() => Waiting.Detach(engine.examine_more(bob)));
        r.Step(16 * Second); // fifteen seconds of reading the label
        Assert.True(feed.Any("You learned how to make the House Edge."));
        r.In(() => Assert.True(Dm.truthy(bob.mind.has_crafting_recipe(typeof(datum_crafting_recipe_house_edge)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHouseEdgeCountsFireChargesAndSpendsThemOnABlazingSlash()
    {
        var (r, feed, bob) = Room(9302);
        var edge = Hold<obj_item_house_edge>(r, bob);
        var victim = r.Dummy(9, 4);
        r.Step(2);
        r.In(() =>
        {
            ADD_TRAIT(edge, TRAIT_WIELDED, "test");
            Waiting.NoSleep(edge.afterattack(victim, bob));
            Waiting.NoSleep(edge.afterattack(victim, bob));
            Assert.Equal(2f, edge.fire_charges);
            Assert.Equal("house_edge2", edge.icon_state);
            Assert.Equal("house_edge1", edge.inhand_icon_state);
            for (int i = 0; i < 3; i++)
                Waiting.NoSleep(edge.afterattack(victim, bob));
            Assert.Equal(3f, edge.fire_charges); // at most three
        });

        // a right click at range: two wind-ups, then the slash at three charges, upgraded to a light blast
        r.Command(() => Waiting.Detach(edge.ranged_interact_with_atom_secondary(victim, bob)));
        r.Step(Second / 2);
        Assert.True(feed.Any("You take aim at"));
        r.Step(Second);
        var ring = r.In(() => Dm.each(bob.loc.contents).OfType<obj_effect_temp_visual_focus_ring>().FirstOrDefault());
        Assert.NotNull(ring); // the wind-up's focus ring, fading in
        Assert.False(feed.Any("blazing swipe"), "the slash came before the second wind-up");
        r.Step(3 * Second);
        Assert.True(feed.Any("You take a blazing swipe at"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        // three charges and both wind-ups: the upgraded slash, a light blast with tg's flames (the admin log of the explosion)
        Assert.True(feed.Any("Explosion with size (Devast: 0, Heavy: 0, Light: 1, Flame: 3"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.True(Heard(r, "flamethrower"));
        Assert.True(Heard(r, "weapons/slash"));
        r.In(() =>
        {
            Assert.Equal(0f, edge.fire_charges);
            Assert.Equal("house_edge", edge.icon_state);
            Assert.True(edge.fire_charge_cooldown > 0); // the slash cooldown (1.2 s) was started
        });
        r.Step(3 * Second);
        r.In(() => Assert.True(QDELETED(ring)));

        // dropped, a charged sword loses its charges
        r.In(() =>
        {
            edge.fire_charges = 2;
            edge.fire_charge_cooldown = 0;
            Waiting.NoSleep(bob.dropItemToGround(edge));
            Assert.Equal(0f, edge.fire_charges);
        });
        Assert.True(feed.Any("balloon: charges lost!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFlamingSlashHitsForItsChargesAndBurns()
    {
        var (r, feed, bob) = Room(9303);
        var victim = r.Dummy(9, 4);
        r.Step(2);
        r.In(() =>
        {
            var slash = Dm.@new<obj_projectile_flaming_slash>(locate(8, 4, 1));
            Assert.Equal(10f, Dm.num(slash.damage));
            Assert.Equal("flaming_slash", slash.icon_state);
            Waiting.NoSleep(slash.on_hit(victim, 0));
        });
        r.Step(Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStreetDrugsAreContrabandAndTheMethCrystalInjectsItsMeth()
    {
        var (r, feed, bob) = Room(9304);
        r.In(() =>
        {
            var glob = Dm.@new<obj_item_food_drug_saturnx>(locate(6, 4, 1));
            var rock = Dm.@new<obj_item_food_drug_moon_rock>(locate(6, 4, 1));
            var meth = Dm.@new<obj_item_food_drug_meth_crystal>(locate(6, 4, 1));
            var opium = Dm.@new<obj_item_food_drug_opium>(locate(6, 4, 1));
            foreach (atom drug in new atom[] { glob, rock, meth, opium })
                Assert.True(HAS_TRAIT(drug, TRAIT_CONTRABAND));
            Assert.Matches("^moon_rock[1-3]$", rock.icon_state);
            Assert.Matches("^meth_crystal[1-5]$", meth.icon_state);
            Assert.Matches("^opium[1-5]$", opium.icon_state);
            Assert.Equal(10f, Amount(meth, typeof(datum_reagent_drug_methamphetamine)));
            Assert.Equal(10f, Amount(opium, typeof(datum_reagent_medicine_morphine)));
        });
        var big = r.In(() => Dm.@new<obj_item_food_drug_opium>(locate(6, 4, 1)));
        Assert.Contains("needs no further refinement", Examine(r, big, bob));

        // stuck in an organic limb, the meth crystal injects its meth
        var crystal = r.In(() => Dm.@new<obj_item_food_drug_meth_crystal>(bob.loc));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(crystal.force_embed(bob, BODY_ZONE_CHEST)))));
        r.Step(5 * Second);
        r.In(() =>
        {
            Assert.True(Amount(bob, typeof(datum_reagent_drug_methamphetamine)) > 0, "no meth in Bob");
            Assert.True(Amount(crystal, typeof(datum_reagent_drug_methamphetamine)) < 10);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void RawOpiumIsAsRichAsItsPotencyAndPressesTogether()
    {
        var (r, feed, bob) = Room(9305);
        var chunk = Hold<obj_item_food_drug_opium_raw>(r, bob);
        var other = r.In(() => Dm.@new<obj_item_food_drug_opium_raw>(locate(5, 4, 1), FALSE, 100));
        r.Step(2);
        float before = 0, theirs = 0;
        r.In(() =>
        {
            float rich = Amount(other, typeof(datum_reagent_medicine_morphine));
            Assert.InRange(rich, 0.4f * 5 - 0.01f, 2.5f * 5 + 0.01f); // (4 to 25) / 10 times potency / 20
            float sugar = Amount(other, typeof(datum_reagent_consumable_sugar));
            Assert.InRange(sugar, 0.09f, 0.71f);
            before = Amount(chunk, typeof(datum_reagent_medicine_morphine));
            theirs = rich;
        });
        Assert.Contains("can be pressed together", Examine(r, chunk, bob));
        r.In(() => Assert.True(Dm.eq(TRUE, Waiting.NoSleep(chunk.interact_with_atom(other, bob)))));
        r.In(() =>
        {
            float now = Amount(chunk, typeof(datum_reagent_medicine_morphine));
            Assert.True(now <= 10.01f);
            Assert.True(now > before);
            Assert.Equal(Math.Min(10f, before + theirs), now, 2);
        });
        Assert.True(feed.Any("You press the chunks of opium together"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBlastoffAmpouleSnapsOpenAndShattersWhenThrown()
    {
        var (r, feed, bob) = Room(9306);
        var ampoule = Hold<obj_item_reagent_containers_cup_blastoff_ampoule>(r, bob);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(ampoule, TRAIT_CONTRABAND));
            Assert.Equal("blastoff_ampoule", ampoule.icon_state);
            Assert.Equal(10f, Amount(ampoule, typeof(datum_reagent_drug_blastoff)));
            Waiting.NoSleep(ampoule.attack_self(bob));
            Assert.True(Dm.truthy(ampoule.is_open_container()));
            Assert.Equal("blastoff_ampoule_open", ampoule.icon_state);
        });
        Assert.True(Heard(r, "ampoule_snap"));
        turf floor = r.In(() => locate(7, 4, 1));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(ampoule)));
        r.In(() =>
        {
            Waiting.NoSleep(ampoule.forceMove(floor));
            ampoule.throw_impact(floor, null);
            Assert.True(QDELETED(ampoule));
            Assert.True(Dm.each(floor.contents).OfType<obj_item_shard>().Any());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFlatpackDeploysItsMachineWithAMultitool()
    {
        var (r, feed, bob) = Room(9307);
        var pack = r.In(() => Dm.@new<obj_item_flatpack_flatpacker>(locate(5, 4, 1)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal("flatpack (flatpacker)", pack.name);
            Assert.True(pack.board is obj_item_circuitboard_machine_flatpacker);
            Assert.Same(pack, ((atom)pack.board).loc);
        });
        Assert.DoesNotContain("No space", Examine(r, pack, bob));
        var tool = Hold<obj_item_multitool>(r, bob);
        r.Command(() => Waiting.Detach(pack.multitool_act(bob, tool)));
        r.Step(2 * Second);
        Assert.True(feed.Any("balloon: deploying!"));
        r.In(() =>
        {
            Assert.True(QDELETED(pack));
            Assert.True(Dm.each(locate(5, 4, 1).contents).OfType<obj_machinery_flatpacker>().Any(), "no flatpacker deployed");
        });
        Assert.True(feed.Any("deploys!"));
        Assert.True(Heard(r, "terminal_eject"));

        // a flatpack made without a board is deleted at once (tg's "how")
        r.In(() => Assert.True(QDELETED(Dm.@new<obj_item_flatpack>(locate(6, 4, 1)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFlatpackCartHoldsThreePacksStackedAndHandsBackTheTopmost()
    {
        var (r, feed, bob) = Room(9308);
        var cart = Place<obj_structure_flatpack_cart>(r, 5, 4);
        var packs = r.In(() => Enumerable.Range(0, 4).Select(_ => Dm.@new<obj_item_flatpack_mailsorter>(bob.loc)).ToList());
        r.Step(2);
        for (int i = 0; i < 4; i++)
        {
            int k = i;
            r.In(() => Waiting.NoSleep(bob.put_in_hands(packs[k])));
            object result = r.In(() => Waiting.NoSleep(cart.item_interaction(bob, packs[k])));
            r.In(() => Assert.True(Dm.eq(k < 3 ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING, result)));
            if (k == 3)
                r.In(() => Waiting.NoSleep(bob.dropItemToGround(packs[k])));
        }
        Assert.True(feed.Any("balloon: full!"));
        r.In(() => Assert.Equal(3, Dm.length(cart.contents)));
        Assert.Equal(3, Overlays(r, cart).Count(o => o == "flatcart_flat"));
        string seen = Examine(r, cart, bob);
        Assert.Contains("From bottom to top, this cart contains:", seen);
        Assert.Contains("flatpack (mail sorter)", seen);

        r.In(() => Waiting.NoSleep(cart.attack_hand(bob)));
        r.In(() =>
        {
            Assert.Same(packs[2], bob.get_active_held_item()); // the topmost
            Assert.Equal(2, Dm.length(cart.contents));
        });
        Assert.Equal(2, Overlays(r, cart).Count(o => o == "flatcart_flat"));
        r.In(() => Waiting.NoSleep(cart.atom_deconstruct(TRUE)));
        r.In(() => Assert.Same(cart.loc, packs[0].loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APiggyBankTakesCashRattlesAndKeepsItsLimit()
    {
        var (r, feed, bob) = Room(9309);
        var piggy = Hold<obj_item_piggy_bank>(r, bob);
        r.In(() =>
        {
            Assert.Contains(Dm.str(piggy.greyscale_colors), new[] { COLOR_PINK, COLOR_LIGHT_ORANGE, COLOR_GREEN_GRAY, COLOR_PALE_BLUE_GRAY, COLOR_DARK_MODERATE_LIME_GREEN, COLOR_OFF_WHITE });
            Assert.Equal(0f, Dm.num(piggy.calculate_dosh_amount()));
        });
        r.Command(() => Waiting.Detach(piggy.attack_self(bob)));
        r.Step(Second);
        Assert.True(feed.Any("balloon: rattle rattle..."));
        Assert.True(feed.Any("balloon: it's empty"));

        var chip = r.In(() => Dm.@new<obj_item_holochip>(bob.loc, 1000));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_inactive_hand(chip)));
        r.In(() => bob.swap_hand());
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(piggy.item_interaction(bob, chip)))));
        Assert.True(feed.Any("balloon: inserted 1000 creds"));
        r.In(() =>
        {
            Assert.Same(piggy, chip.loc);
            Assert.Equal(1000f, Dm.num(piggy.calculate_dosh_amount()));
        });
        r.In(() => bob.swap_hand());
        r.Step(Second);
        r.Command(() => Waiting.Detach(piggy.attack_self(bob)));
        r.Step(Second);
        Assert.True(feed.Any("balloon: it feels almost full")); // 1000 of 2000: 50 %
        Assert.True(Heard(r, "rattle"));

        var big = r.In(() => Dm.@new<obj_item_holochip>(bob.loc, 1500));
        r.Step(2);
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_BLOCKING, Waiting.NoSleep(piggy.item_interaction(bob, big)))));
        Assert.True(feed.Any("balloon: too much cash!"));

        // broken, it spills its money
        turf floor = r.In(() => bob.loc as turf);
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(piggy)));
        r.In(() => Waiting.NoSleep(piggy.atom_deconstruct(FALSE)));
        r.In(() => Assert.Same(floor, chip.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APiggyBankSquashesItsChipsPastThirtyFiveAndThePersistentOnesStartWithTheirMoney()
    {
        var (r, feed, bob) = Room(9310);
        var piggy = Place<obj_item_piggy_bank>(r, 6, 4);
        r.In(() =>
        {
            for (int i = 0; i < 40; i++)
                Dm.@new<obj_item_holochip>(piggy, 10);
            piggy.sanitize_piggy_bank_contents_len();
            Assert.True(Dm.length(piggy.contents) <= 35, "contents " + Dm.length(piggy.contents));
            Assert.Equal(400f, Dm.num(piggy.calculate_dosh_amount())); // the squashed chips' credits go into one new chip
        });

        var museum = Place<obj_item_piggy_bank_museum>(r, 7, 4);
        var vault = Place<obj_item_piggy_bank_vault>(r, 8, 4);
        r.In(() =>
        {
            Assert.Equal("#FFC0CB", Dm.str(museum.greyscale_colors));
            Assert.Equal("piggy_bank", museum.icon_state);
            Assert.Equal(400f, Dm.num(museum.calculate_dosh_amount()));
            Assert.NotNull(museum.GetComponent(typeof(datum_component_areabound)));
            Assert.NotNull(museum.persistence_cb);
            Assert.Contains(museum.persistence_cb, Dm.each(SSticker.round_end_events));
            Assert.Equal(50f, Dm.num(vault.calculate_dosh_amount()));
            Assert.Equal(50f + 1600f, vault.maximum_value); // this shift's savings on top of what it holds
            Assert.Contains(DmWorldState.Of(r.World).DeferredSites, site => site.StartsWith("code/game/objects/items/piggy_bank.dm:47", StringComparison.Ordinal));
        });
        r.In(() => qdel(museum));
        r.In(() => Assert.DoesNotContain(museum.persistence_cb, Dm.each(SSticker.round_end_events)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEtherealDiscoBallDeploysAndPartiesInChangingColours()
    {
        var (r, feed, bob) = Room(9311);
        var deployer = Hold<obj_item_etherealballdeployer>(r, bob);
        r.In(() => Waiting.NoSleep(deployer.attack_self(bob)));
        r.Step(2);
        var ball = r.In(() => Dm.each(bob.loc.contents).OfType<obj_structure_etherealball>().FirstOrDefault());
        Assert.NotNull(ball);
        r.In(() =>
        {
            Assert.True(QDELETED(deployer));
            Assert.Equal("ethdisco_head_0", ball.icon_state);
        });
        Assert.Contains("ethdisco_base", Overlays(r, ball));
        Assert.True(feed.Any("You deploy the Ethereal Disco Ball."));

        r.Step(Second);
        r.In(() => Waiting.NoSleep(ball.attack_hand(bob)));
        string first = null;
        r.In(() =>
        {
            Assert.True(Dm.truthy(ball.TurnedOn));
            Assert.Equal("ethdisco_head_1", ball.icon_state);
            Assert.Equal(7f, Dm.num(ball.light_range));
            first = Dm.str(ball.current_color);
            Assert.Equal("#" + first, Dm.str(ball.color));
        });
        Assert.True(feed.Any("You turn the disco ball on!"));
        var colours = new HashSet<string>();
        for (int i = 0; i < 6; i++)
        {
            r.Step(5);
            colours.Add(r.In(() => Dm.str(ball.current_color)));
        }
        Assert.True(colours.Count > 1, "the colour never changed");

        r.Step(Second);
        r.In(() => Waiting.NoSleep(ball.attack_hand(bob)));
        r.In(() =>
        {
            Assert.False(Dm.truthy(ball.TurnedOn));
            Assert.Equal("ethdisco_head_0", ball.icon_state);
            Assert.Equal(0f, Dm.num(ball.light_range));
        });
        string stopped = r.In(() => Dm.str(ball.current_color));
        r.Step(Second);
        r.In(() => Assert.Equal(stopped, Dm.str(ball.current_color))); // the timer is gone

        r.In(() => Waiting.NoSleep(ball.click_alt(bob)));
        r.In(() => Assert.False(Dm.truthy(ball.anchored)));
        Assert.True(feed.Any("You unlock the disco ball."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDebugHumanSpawnerMakesAHumanOfThePickedSpecies()
    {
        var (r, feed, bob) = Room(9312);
        var spawner = Hold<obj_item_debug_human_spawner>(r, bob);
        r.Command(() => Waiting.Detach(spawner.attack_self(bob)));
        ActIn(r, bob, "ListInputWindow", "submit", ("entry", SPECIES_ANDROID));
        r.In(() => Assert.Equal(typeof(datum_species_android), spawner.selected_species));
        turf floor = r.In(() => locate(8, 6, 1));
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(spawner.ranged_interact_with_atom(floor, bob)))));
        r.Step(2);
        r.In(() =>
        {
            var made = Dm.each(floor.contents).OfType<mob_living_carbon_human>().Single();
            Assert.IsType<datum_species_android>(made.dna.species);
        });
        r.In(() => Assert.True(Dm.eq(NONE, Waiting.NoSleep(spawner.interact_with_atom(bob, bob)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheOmnitoolTakesAToolsBehaviourFromItsRadial()
    {
        var (r, feed, bob) = Room(9313);
        var tool = Hold<obj_item_debug_omnitool>(r, bob);
        r.In(() =>
        {
            Assert.Null(tool.tool_behaviour);
            Assert.Equal(20, Dm.length(tool.get_all_tool_behaviours() as DmList));
            Assert.Equal(20, Dm.length(GLOB.tool_to_image));
        });
        r.Command(() => Waiting.Detach(tool.attack_self(bob)));
        PickRadial(r, bob, TOOL_WRENCH);
        r.In(() => Assert.Equal(TOOL_WRENCH, Dm.str(tool.tool_behaviour)));
        Assert.Contains("The mode is: wrench", Examine(r, tool, bob));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSpawntoolWarnsAPlayerWithoutAdminRights()
    {
        var (r, feed, bob) = Room(9314);
        var tool = Hold<obj_item_debug_omnitool_item_spawner>(r, bob);
        r.Command(() => Waiting.Detach(tool.attack_self(bob)));
        ActIn(r, bob, "TextInputModal", "submit", ("entry", "/obj/item/pitchfork"));
        ActIn(r, bob, "AlertModal", "choose", ("choice", "No"));
        Assert.True(feed.Any("you hear strange tearing sounds"));
        Assert.True(Heard(r, "dimensional_rend"));
        r.Step(4 * Second);
        ActIn(r, bob, "AlertModal", "choose", ("choice", "No"));
        r.Step(Second);
        r.In(() =>
        {
            Assert.False(QDELETED(bob));
            Assert.False(Dm.each(bob.loc.contents).OfType<obj_item_pitchfork>().Any());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHippocratesBustGivesAHudAndTakesTheOath()
    {
        var (r, feed, bob) = Room(9315);
        var bust = Hold<obj_item_statuebust_hippocratic>(r, bob, b => b.reference = FALSE);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(bob, TRAIT_MEDICAL_HUD)); // held in a hand
        });
        Assert.Contains("swear or forswear a Hippocratic Oath!", Examine(r, bust, bob));
        r.Command(() => Waiting.Detach(bust.attack_self(bob)));
        r.Step(14 * Second); // 4 + 2 + 3 + 3 seconds of oath
        Assert.True(feed.Any("I swear to fulfill"));
        Assert.True(feed.Any("finest traditions of my calling"));
        Assert.True(feed.Any("You consider for a second the concept of harm"));
        r.In(() => Assert.True(HAS_TRAIT_FROM(bob, TRAIT_PACIFISM, typeof(obj_item_statuebust_hippocratic))));

        // too soon to forswear it
        r.In(() => Waiting.NoSleep(bust.attack_self(bob)));
        Assert.True(feed.Any("too recently to undo your decisions"));

        r.In(() => Waiting.NoSleep(bob.dropItemToGround(bust)));
        r.In(() => Assert.False(HAS_TRAIT(bob, TRAIT_MEDICAL_HUD)));

        // the evil one is the blood-caked Solemn Vow
        r.In(() =>
        {
            var vow = Dm.@new<obj_item_statuebust_hippocratic_evil>(locate(7, 4, 1));
            Assert.Equal("Solemn Vow", vow.name);
            Assert.True(Dm.truthy(vow.reference));
            Assert.Equal((float)CLICK_CD_SLOW, vow.attack_speed);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnOathInterruptedBruisesTheBrain()
    {
        var (r, feed, bob) = Room(9316);
        var bust = Hold<obj_item_statuebust_hippocratic>(r, bob, b => b.reference = FALSE);
        r.Command(() => Waiting.Detach(bust.attack_self(bob)));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.Move(locate(5, 4, 1), EAST))); // moving breaks the do_after
        r.Step(5 * Second);
        Assert.True(feed.Any("You forget what comes next like a dumbass."));
        r.In(() =>
        {
            Assert.True(Dm.num(bob.get_organ_loss(ORGAN_SLOT_BRAIN)) >= 2);
            Assert.Equal(0f, bust.oath_cd); // the cooldown reset
            Assert.False(HAS_TRAIT(bob, TRAIT_PACIFISM));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheManekiNekoBeckonsInItsColour()
    {
        var (r, feed, bob) = Room(9317);
        var neko = Place<obj_item_maneki_neko>(r, 6, 4);
        Assert.Contains("maneki-neko-overlay", Overlays(r, neko));
        r.In(() => Assert.Contains(Dm.str(neko.color), new[] { COLOR_WHITE, COLOR_GOLD, COLOR_DARK }));
        var bust = Place<obj_item_statuebust>(r, 7, 4);
        r.In(() => Assert.Equal("bust", bust.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BurningTheVirginMaryGivesAMafiaNickname()
    {
        var (r, feed, bob) = Room(9318);
        var mary = Place<obj_item_virgin_mary>(r, 5, 4);
        var lighter = Hold<obj_item_lighter>(r, bob);
        r.In(() => bob.real_name = "Bob Smith");
        r.Command(() => mary._SendSignal(COMSIG_ATOM_IGNITED_BY_ITEM, Dm.list(mary, bob, lighter)));
        r.Step(2);
        Assert.True(feed.Any("a nickname comes to mind"));
        ActIn(r, bob, "TextInputModal", "submit", ("entry", "Tony"));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal("Bob \"Tony\"  Smith", Dm.str(bob.real_name)); // tg keeps the space before the last name
            Assert.True(Dm.truthy(mary.used_up));
            Assert.True(HAS_TRAIT(bob, TRAIT_MAFIAINITIATE));
        });
        Assert.True(feed.Any("does not grant antagonist status"));

        // one nickname each
        var second = Place<obj_item_virgin_mary>(r, 6, 4);
        r.Command(() => second._SendSignal(COMSIG_ATOM_IGNITED_BY_ITEM, Dm.list(second, bob, lighter)));
        r.Step(2);
        Assert.True(feed.Any("You have already been initiated into the mafioso life."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRollingTableDockEjectsItsTable()
    {
        var (r, feed, bob) = Room(9319);
        var dock = Hold<obj_item_rolling_table_dock>(r, bob);
        r.In(() => Assert.NotNull(dock.loaded));
        Assert.Contains("The dock is loaded.", Examine(r, dock, bob));
        var table = r.In(() => dock.loaded);
        turf floor = r.In(() => locate(5, 4, 1));
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(dock.interact_with_atom(floor, bob)))));
        r.In(() =>
        {
            Assert.Same(floor, table.loc);
            Assert.Null(dock.loaded);
            Assert.False(QDELETED(dock)); // the later definition keeps the dock
        });
        Assert.Contains("The dock is empty.", Examine(r, dock, bob));
        r.In(() => Waiting.NoSleep(dock.deploy_rolling_table(bob, floor)));
        Assert.True(feed.Any("balloon: the dock is Empty!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBotPadControllerLinksToAPadAndChecksItBeforeALaunch()
    {
        var (r, feed, bob) = Room(9320);
        var pad = Place<obj_machinery_botpad>(r, 7, 4);
        var remote = Hold<obj_item_botpad_remote>(r, bob);
        var multitool = r.In(() => Dm.@new<obj_item_multitool>(bob.loc));
        r.Step(2);
        r.In(() =>
        {
            pad.panel_open = TRUE;
            pad.update_appearance();
            Assert.Equal("botpad-open", pad.icon_state);
            Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(pad.multitool_act(bob, multitool))));
            Assert.Same(pad, multitool.buffer);
            Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(remote.multitool_act(bob, multitool))));
            Assert.Same(pad, remote.connected_botpad);
            Assert.Same(remote, pad.connected_remote);
            Assert.Null(multitool.buffer);
        });
        Assert.True(feed.Any("You connect the controller to the pad"));
        r.In(() => Waiting.NoSleep(remote.attack_self(bob)));
        Assert.True(feed.Any("balloon: close the panel!"));
        Assert.True(Heard(r, "terminal"));
        r.In(() =>
        {
            pad.panel_open = FALSE;
            Waiting.NoSleep(remote.attack_self(bob));
        });
        Assert.True(feed.Any("balloon: no bots detected on the pad!"));
        r.In(() => Waiting.NoSleep(remote.attack_self_secondary(bob)));
        Assert.True(feed.Any("balloon: no bots sent from the pad!"));
        r.In(() => qdel(remote));
        r.In(() => Assert.Null(pad.connected_remote));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCarpSamplerTakesACarpsDna()
    {
        var (r, feed, bob) = Room(9321);
        var sampler = Hold<obj_item_dna_probe_carp_scanner>(r, bob);
        var carp = r.In(() => Dm.@new<mob_living_basic_carp>(locate(5, 4, 1)));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(sampler.valid_scan_target(carp))));
        r.In(() => Waiting.NoSleep(sampler.interact_with_atom(carp, bob)));
        r.In(() => Assert.True(Dm.truthy(sampler.carp_dna_loaded)));
        Assert.True(feed.Any("balloon: dna scanned"));
        string more = r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(sampler.examine_more(bob))).Select(o => Dm.str(o))));
        Assert.Contains("Using this on a Space Carp will harvest its DNA.", more);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheLeadAcidBatteryStartsHalfChargedAndFollowsItsCarrier()
    {
        var (r, feed, bob) = Room(9322);
        var cell = Place<obj_item_stock_parts_power_store_cell_lead>(r, 5, 4);
        var pipe = Place<obj_item_lead_pipe>(r, 5, 4);
        r.In(() =>
        {
            Assert.InRange(cell.charge, 0.4f * cell.maxcharge - 1, 0.6f * cell.maxcharge + 1);
            Assert.True(HAS_TRAIT(cell, TRAIT_FISHING_BAIT));
            Assert.Equal("lead_battery", cell.icon_state);
            DmList ground = Waiting.NoSleep(cell.grind_results()) as DmList;
            Assert.Equal(15f, Dm.num(ground[typeof(datum_reagent_lead)]));
            Assert.Equal(15f, Dm.num(ground[typeof(datum_reagent_toxin_acid)]));
            Assert.Equal(20f, Dm.num(ground[typeof(datum_reagent_water)]));
            Assert.Equal(20f, Dm.num((Waiting.NoSleep(pipe.grind_results()) as DmList)[typeof(datum_reagent_lead)]));
        });
        var gear = r.In(() => cell.GetComponent(typeof(datum_component_loads_avatar_gear)) as datum_component_loads_avatar_gear);
        Assert.NotNull(gear);
        r.In(() => Waiting.NoSleep(bob.put_in_hands(cell)));
        r.Step(2);
        r.In(() => Assert.Same(bob, gear.tracked_human_ref?.resolve()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHairDyeSprayDrawsAGradient()
    {
        var (r, feed, bob) = Room(9323);
        var spray = Hold<obj_item_dyespray>(r, bob);
        string style = r.In(() => Dm.str(Dm.each(SSaccessories.hair_gradients_list).Skip(1).First()));
        r.Command(() => Waiting.Detach(spray.attack_self(bob)));
        ActIn(r, bob, "AlertModal", "choose", ("choice", "Hair"));
        ActIn(r, bob, "ListInputWindow", "submit", ("entry", style));
        ActIn(r, bob, "ColorPickerModal", "submit", ("entry", "#00ff00"));
        Assert.True(feed.Any("You start applying the hair dye..."));
        r.Step(4 * Second);
        r.In(() => Assert.Equal("#00ff00", Dm.str(bob.get_hair_gradient_color(GRADIENT_HAIR_KEY)).ToLowerInvariant()));
        Assert.True(Heard(r, "effects/spray"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGunMaintenanceKitRepairsAGunAndIsUsedUp()
    {
        var (r, feed, bob) = Room(9324);
        var kit = Hold<obj_item_gun_maintenance_supplies_makeshift>(r, bob);
        Assert.Contains("This kit has 1 uses out of 1 left.", Examine(r, kit, bob));
        var gun = r.In(() => Dm.@new<obj_item_gun_ballistic_automatic_pistol>(locate(5, 4, 1)));
        r.Step(2);
        r.In(() =>
        {
            Waiting.NoSleep(gun.take_damage(10, BRUTE, MELEE));
            gun.misfire_probability = 20;
            Assert.True(Dm.num(gun.get_integrity()) < gun.max_integrity);
            Assert.True(Dm.eq(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(kit.interact_with_atom(gun, bob))));
            Assert.Equal(gun.max_integrity, Dm.num(gun.get_integrity()));
            Assert.Equal(0f, gun.misfire_probability);
            Assert.True(QDELETED(kit)); // its one use spent
        });
        Assert.True(feed.Any("balloon: maintenance complete"));

        var full = Hold<obj_item_gun_maintenance_supplies>(r, bob);
        var table = Place<obj_structure_table>(r, 6, 4);
        r.In(() => Assert.True(Dm.eq(ITEM_INTERACT_BLOCKING, Waiting.NoSleep(full.interact_with_atom(table, bob)))));
        Assert.True(feed.Any("balloon: not a gun!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePitchforkIsWieldedInTwoHands()
    {
        var (r, feed, bob) = Room(9325);
        var fork = Hold<obj_item_pitchfork>(r, bob);
        r.In(() =>
        {
            Assert.Equal("pitchfork0", fork.icon_state);
            Assert.NotNull(fork.GetComponent(typeof(datum_component_jousting)));
            Assert.NotNull(fork.GetComponent(typeof(datum_component_walking_aid)));
            Assert.NotNull(fork.GetComponent(typeof(datum_component_two_handed)));
        });
        r.In(() => Waiting.NoSleep(fork.attack_self(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(fork, TRAIT_WIELDED));
            Assert.Equal(15f, fork.force);
        });
        r.AssertNoRuntimes();
    }
}
