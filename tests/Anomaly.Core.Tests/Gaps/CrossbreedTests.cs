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
/// Xenobiology's crossbreeds in a booted toy room (research/xenobiology/crossbreeding/): an adult slime fed ten extracts of one
/// colour turns into its crossbreed; each family's use with a few colours (burning, charged and chilling squeezed with plasma,
/// consuming fed food and its cookies eaten, industrial fed plasma, prismatic painting, recurring recharging, regenerative
/// healing, reproductive fed monkey cubes, self-sustaining splitting, stabilized carried), the potions, weapons and clothing they
/// make, and the slime form of the burning black extract. Sweeps squeeze, feed or carry every colour of every family once, make
/// and destroy every newly live type and apply every crossbreed status effect, and nothing throws.
/// </summary>
public class CrossbreedTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int width = 21, int height = 21)
    {
        var r = new AudioRoom(width, height, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(width / 2, height / 2, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static datum_reagents Holder(atom a) => (datum_reagents)a.reagents;

    /// <summary>The crossbreed in Bob's active hand, his hands emptied first.</summary>
    private static T InHand<T>(AudioRoom r, mob_living_carbon_human bob) where T : obj_item => (T)InHand(r, bob, typeof(T));

    private static obj_item InHand(AudioRoom r, mob_living_carbon_human bob, Type type)
    {
        obj_item item = r.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(bob.held_items).ToList())
                qdel(held);
            var made = Dm.@new(type, bob.loc) as obj_item;
            if (QDELETED(made))
                return made; // the self-sustaining extracts split as they are made
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_active_hand(made))), type.Name + " is not in Bob's hand");
            return made;
        });
        r.Step(1);
        return item;
    }

    /// <summary>Filled with 10u of plasma and squeezed in Bob's hand.</summary>
    private static void Squeeze(AudioRoom r, mob_living_carbon_human bob, obj_item extract, bool plasma = true)
    {
        r.In(() =>
        {
            if (plasma)
                Waiting.NoSleep(Holder(extract).add_reagent(typeof(datum_reagent_toxin_plasma), 10));
            Waiting.Detach(extract.attack_self(bob));
        });
        r.Step(2);
    }

    /// <summary>
    /// The consuming and reproductive extracts wait their cooldown after world.time 0 (a minute, three seconds), which a toy room
    /// starting at tick 0 has not passed yet; on a station past its first minute they are ready, as here.
    /// </summary>
    private static void Ready(AudioRoom r, obj_item extract) => r.In(() =>
    {
        if (extract is obj_item_slimecross_consuming consuming)
            consuming.last_produced = -consuming.cooldown;
        if (extract is obj_item_slimecross_reproductive reproductive)
            reproductive.last_produce = -reproductive.cooldown;
    });

    private static List<atom_movable> Around(AudioRoom r, atom center, int distance = 2)
        => r.In(() => Dm.each<atom_movable>(range(distance, get_turf(center))).ToList());

    private static void Clear(AudioRoom r, mob_living_carbon_human keep)
    {
        r.In(() =>
        {
            foreach (atom_movable thing in Dm.each<atom_movable>(range(30, get_turf(keep))).ToList())
                if ((thing is obj_item && thing.loc is turf) || (thing is mob_living && thing != keep) || thing is obj_effect || thing is obj_structure)
                    qdel(thing);
        });
        r.Step(2);
    }

    private static Type[] Colours(Type family) => Dm.each_cast<Type>(subtypesof(family)).Where(t => !InertSeam.IsInertType(t)).ToArray();

    // ---- the crossing ----

    [Fact]
    public void AnAdultSlimeFedTenGreyExtractsTurnsIntoAReproductiveGreyExtract()
    {
        var (r, feed, bob) = Room(13901);
        var slime = r.In(() => Dm.@new<mob_living_basic_slime>(get_step(bob, EAST), typeof(datum_slime_type_grey), SLIME_LIFE_STAGE_ADULT));
        r.Step(2);
        turf spot = r.In(() => slime.loc as turf);
        for (int i = 0; i < SLIME_EXTRACT_CROSSING_REQUIRED; i++)
        {
            r.In(() =>
            {
                ADD_TRAIT(slime, TRAIT_IMMOBILIZED, "test");
                var extract = Dm.@new<obj_item_slime_extract_grey>(bob.loc);
                Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(extract.interact_with_atom(slime, bob, Dm.list())));
            });
        }
        r.Step(2);
        var cross = r.In(() => Dm.each<obj_item_slimecross>(range(1, spot)).SingleOrDefault());
        Assert.NotNull(cross);
        r.In(() =>
        {
            Assert.IsType<obj_item_slimecross_reproductive_grey>(cross);
            Assert.True(QDELETED(slime), "the slime's body is consumed");
            Assert.Equal("reproductive grey extract", cross.name);
            Assert.False(InertSeam.IsInert(cross));
        });
        Assert.True(feed.Any("shudders, its mutated core consuming the rest of its body!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EachExtractColourCrossesIntoItsFamily()
    {
        // every extract's crossbreed modification names a family, and every colour of the families that exist has its crossbreed
        var (r, _, _) = Room(13902, 9, 9);
        var missing = new List<string>();
        r.In(() =>
        {
            foreach (Type extract in Dm.each_cast<Type>(subtypesof(typeof(obj_item_slime_extract))))
            {
                string effect = Dm.str(Dm.initial(extract, "crossbreed_modification"));
                Type slimeType = Dm.each_cast<Type>(subtypesof(typeof(datum_slime_type))).FirstOrDefault(s => Dm.eq(Dm.initial(s, "core_type"), extract));
                if (slimeType == null)
                    continue;
                string colour = Dm.str(Dm.initial(slimeType, "colour"));
                bool family = Dm.each_cast<Type>(subtypesof(typeof(obj_item_slimecross))).Any(t => Dm.eq(Dm.initial(t, "effect"), effect));
                if (!family)
                    continue; // tg has no crossbreeds of this modification (the "too unstable" puddle)
                Type cross = Dm.each_cast<Type>(subtypesof(typeof(obj_item_slimecross))).FirstOrDefault(t => Dm.eq(Dm.initial(t, "effect"), effect) && Dm.eq(Dm.initial(t, "colour"), colour));
                if (cross == null)
                    missing.Add(effect + " " + colour);
                else if (InertSeam.IsInertType(cross))
                    missing.Add(cross.Name + " is inert");
            }
        });
        Assert.True(missing.Count == 0, string.Join("\n", missing));
    }

    // ---- burning ----

    [Fact]
    public void ABurningExtractNeedsPlasmaAndTheGreyOneGrowsAFriendlyHungrySlime()
    {
        var (r, feed, bob) = Room(13903);
        var grey = InHand<obj_item_slimecross_burning_grey>(r, bob);
        Squeeze(r, bob, grey, plasma: false);
        Assert.True(feed.Any("This extract needs to be full of plasma to activate!"));
        Assert.False(r.In(() => QDELETED(grey)));
        Squeeze(r, bob, grey);
        Assert.True(feed.Any("You squeeze the extract, and it absorbs the plasma!"));
        var slime = Around(r, bob).OfType<mob_living_basic_slime>().Single();
        r.In(() =>
        {
            Assert.True(QDELETED(grey));
            Assert.True(Dm.truthy(slime.has_ally(bob)), "the slime loves Bob");
            Assert.Equal(SLIME_HUNGER_NUTRITION, Dm.num(slime.nutrition));
        });
        Assert.Contains(r.Sounds.Heard, h => h.File?.EndsWith("fireball.ogg", StringComparison.Ordinal) == true);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BurningMetalBreaksTheWallsAndBurningRainbowAdamantineAndPinkMakeTheirThings()
    {
        var (r, feed, bob) = Room(13904);
        turf wall = r.In(() => get_step(bob, NORTH));
        r.In(() => Waiting.NoSleep(wall.ChangeTurf(typeof(turf_closed_wall))));
        Squeeze(r, bob, InHand<obj_item_slimecross_burning_metal>(r, bob));
        Assert.False(r.In(() => locate(wall.x, wall.y, wall.z) is turf_closed_wall), "the wall still stands");
        Assert.True(feed.Any("pulses violently, and shatters the walls around it!"));
        Squeeze(r, bob, InHand<obj_item_slimecross_burning_rainbow>(r, bob));
        Squeeze(r, bob, InHand<obj_item_slimecross_burning_adamantine>(r, bob));
        Squeeze(r, bob, InHand<obj_item_slimecross_burning_pink>(r, bob));
        Squeeze(r, bob, InHand<obj_item_slimecross_burning_sepia>(r, bob));
        var around = Around(r, bob, 1);
        Assert.Contains(around, a => a is obj_item_knife_rainbowknife);
        Assert.Contains(around, a => a is obj_item_shield_adamantineshield);
        Assert.Contains(around, a => a is obj_item_slimecrossbeaker_pax);
        Assert.Contains(around, a => a is obj_item_camera_rewind);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void BurningOilExplodesAfterFiveSecondsAndBurningBlackTurnsItsUserIntoASlimeAndBack()
    {
        var (r, feed, bob) = Room(13905);
        var oil = InHand<obj_item_slimecross_burning_oil>(r, bob);
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(oil)));
        r.In(() => Waiting.NoSleep(oil.forceMove(locate(2, 2, 1))));
        Squeeze(r, bob, oil);
        Assert.True(feed.Any("It crackles in anticipation"));
        Assert.False(r.In(() => QDELETED(oil)));
        r.Step(6 * Second);
        Assert.True(r.In(() => QDELETED(oil)), "the oil extract went off");

        var bobClient = r.In(() => bob.client);
        Squeeze(r, bob, InHand<obj_item_slimecross_burning_black>(r, bob));
        r.Step(Second);
        var shape = r.In(() => bob.loc as mob_living_basic_slime_transformed_slime);
        Assert.NotNull(shape);
        Assert.True(feed.Any(", transforming ") && feed.Any(" into a slime!"));
        var spell = r.In(() => Dm.locate<datum_action_cooldown_spell_shapeshift_slime_form>(shape.actions));
        Assert.NotNull(spell);
        Assert.Same(bobClient, r.In(() => shape.client));
        // casting it again turns the slime back, and the spell goes with the slime form
        r.In(() => Waiting.Detach(spell.Trigger(shape)));
        r.Step(Second);
        Assert.True(r.In(() => bob.loc is turf), "Bob is himself again");
        Assert.True(r.In(() => QDELETED(shape)));
        Assert.True(r.In(() => QDELETED(spell)), "the slime form removes itself");
        r.AssertNoRuntimes();
    }

    // ---- charged ----

    [Fact]
    public void ChargedExtractsMakeMetalsPlasmaPotionsAndSlimes()
    {
        var (r, feed, bob) = Room(13906);
        Squeeze(r, bob, InHand<obj_item_slimecross_charged_metal>(r, bob));
        Squeeze(r, bob, InHand<obj_item_slimecross_charged_grey>(r, bob));
        Squeeze(r, bob, InHand<obj_item_slimecross_charged_rainbow>(r, bob));
        var around = Around(r, bob, 1);
        Assert.Equal(25, r.In(() => around.OfType<obj_item_stack_sheet_iron>().Sum(s => (int)Dm.num(s.amount))));
        Assert.Equal(10, r.In(() => around.OfType<obj_item_stack_sheet_plasteel>().Sum(s => (int)Dm.num(s.amount))));
        Assert.Contains(around, a => a is obj_item_slimepotion_slime_reviver);
        Assert.Equal(3, around.OfType<mob_living_basic_slime>().Count());
        Assert.True(feed.Any("swells and splits into three new slimes!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ChargedGoldSpawnsTenMobsOverTimeAndThenCollapses()
    {
        var (r, feed, bob) = Room(13907, 31, 31);
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test")); // the monsters it makes must not knock Bob out of seeing it collapse
        var gold = InHand<obj_item_slimecross_charged_gold>(r, bob);
        Squeeze(r, bob, gold);
        Assert.True(feed.Any("starts shuddering violently!"));
        r.Step(5 * Second + 2);
        r.Step(30 * Second);
        Assert.True(r.In(() => QDELETED(gold)), "the gold extract collapsed");
        Assert.True(feed.Any("collapses into a puddle of goo."));
        Assert.True(r.In(() => (int)gold.spawned) == 10);
    }

    [Fact]
    public void ChargedBlackRollsASpeciesAndChargedAdamantineLeavesAGolemShell()
    {
        var (r, feed, bob) = Room(13908);
        Squeeze(r, bob, InHand<obj_item_slimecross_charged_adamantine>(r, bob));
        Assert.Contains(Around(r, bob, 1), a => a is obj_effect_mob_spawn_ghost_role_human_golem_servant);
        // the roll: a ported species changes Bob, a species not ported yet fizzles and keeps the extract
        var black = InHand<obj_item_slimecross_charged_black>(r, bob);
        Type before = r.In(() => (Type)((datum)bob.dna.species).type);
        r.World.Rng.Reseed(13908, 1);
        Squeeze(r, bob, black);
        bool spent = r.In(() => QDELETED(black));
        Type after = r.In(() => (Type)((datum)bob.dna.species).type);
        if (spent)
            Assert.True(feed.Any("You feel very different!"));
        else
        {
            Assert.True(feed.Any("fizzes uselessly."));
            Assert.Equal(before, after);
        }
        r.AssertNoRuntimes();
    }

    // ---- chilling ----

    [Fact]
    public void ChillingGreyMakesBarrierCubesAndChillingMetalWallsItsUserIn()
    {
        var (r, feed, bob) = Room(13909);
        Squeeze(r, bob, InHand<obj_item_slimecross_chilling_grey>(r, bob));
        var cubes = Around(r, bob, 0).OfType<obj_item_barriercube>().ToList();
        Assert.Equal(3, cubes.Count);
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_active_hand(cubes[0]))));
            Waiting.NoSleep(cubes[0].attack_self(bob));
        });
        r.Step(1);
        Assert.Contains(Around(r, bob, 0), a => a is obj_structure_barricade_slime);
        Assert.True(feed.Any("suddenly grows into a large, gelatinous barrier!"));

        Squeeze(r, bob, InHand<obj_item_slimecross_chilling_metal>(r, bob));
        Assert.Equal(8, Around(r, bob, 1).OfType<obj_effect_forcefield_slimewall>().Count());
        r.Step(11 * Second);
        Assert.Empty(Around(r, bob, 1).OfType<obj_effect_forcefield_slimewall>());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ChillingDarkBlueFreezesItsUserAndChillingBluespaceRecallsTheLinked()
    {
        var (r, feed, bob) = Room(13910);
        var alice = r.Dummy(3, 3);
        var blue = InHand<obj_item_slimecross_chilling_bluespace>(r, bob);
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(blue.interact_with_atom(alice, bob, Dm.list()))));
        Assert.True(feed.Any("You link"));
        Squeeze(r, bob, blue);
        Assert.True(r.In(() => Dm.truthy(alice.has_status_effect(typeof(datum_status_effect_slimerecall)))));
        r.Step(11 * Second);
        Assert.True(r.In(() => get_dist(alice, bob) <= 1), "Alice was recalled beside Bob");
        Assert.True(r.In(() => QDELETED(blue)));

        Squeeze(r, bob, InHand<obj_item_slimecross_chilling_darkblue>(r, bob));
        Assert.True(r.In(() => bob.loc is obj_structure_ice_stasis), "Bob is in the ice block");
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_GODMODE)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ChillingRedPacifiesTheSlimesAndChillingCeruleanMakesAControlledClone()
    {
        var (r, feed, bob) = Room(13911);
        var slime = r.In(() => Dm.@new<mob_living_basic_slime>(locate(3, 3, 1), typeof(datum_slime_type_grey)));
        r.Step(1);
        Squeeze(r, bob, InHand<obj_item_slimecross_chilling_red>(r, bob));
        Assert.True(r.In(() => Dm.truthy(slime.hunger_disabled)), "the slime is pacified");
        Assert.True(feed.Any("nearby slimes seem calm."));

        Squeeze(r, bob, InHand<obj_item_slimecross_chilling_cerulean>(r, bob));
        var effect = r.In(() => bob.has_status_effect(typeof(datum_status_effect_slime_clone)) as datum_status_effect_slime_clone);
        Assert.NotNull(effect);
        var clone = r.In(() => effect.clone);
        Assert.NotNull(clone);
        Assert.Same(clone, r.In(() => (object)bob.client?.mob ?? clone.client?.mob));
        Assert.True(r.In(() => Dm.truthy(clone.has_status_effect(typeof(datum_status_effect_slime_clone_decay)))));
        // the clone falls, the player goes back to Bob
        r.In(() => Waiting.NoSleep(clone.death()));
        r.Step(2 * Second);
        Assert.False(r.In(() => Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_slime_clone)))));
        Assert.NotNull(r.In(() => bob.client));
        r.AssertNoRuntimes();
    }

    // ---- consuming ----

    [Fact]
    public void AConsumingExtractEatsFoodIntoCookiesAndThePurpleCookieHeals()
    {
        var (r, feed, bob) = Room(13912);
        var extract = r.In(() => Dm.@new<obj_item_slimecross_consuming_purple>(bob.loc));
        Ready(r, extract);
        r.Step(1);
        int fed = 0;
        while (r.In(() => Dm.each<obj_item_slime_cookie>(bob.loc).Count()) == 0 && fed < 20)
        {
            r.In(() =>
            {
                var food = Dm.@new<obj_item_food_donut_plain>(bob.loc);
                Waiting.NoSleep(extract.item_interaction(bob, food, Dm.list()));
            });
            fed++;
            r.Step(1);
        }
        var cookies = r.In(() => Dm.each<obj_item_slime_cookie_purple>(bob.loc).ToList());
        Assert.Equal(5, cookies.Count);
        Assert.True(feed.Any("swells up and produces a small pile of cookies!"));
        // still digesting a minute
        r.In(() =>
        {
            var food = Dm.@new<obj_item_food_donut_plain>(bob.loc);
            Assert.Equal(ITEM_INTERACT_BLOCKING, Waiting.NoSleep(extract.item_interaction(bob, food, Dm.list())));
        });
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(20)));
        float before = r.In(() => Dm.num(bob.get_brute_loss()));
        r.In(() => Waiting.NoSleep(cookies[0].interact_with_atom(bob, bob, Dm.list())));
        r.Step(1);
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) <= before - 5 + 0.01f);
        Assert.True(r.In(() => QDELETED(cookies[0])));
        Assert.True(feed.Any("You can taste fruit jam and cough medicine."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCookiesGiveTheirEffects()
    {
        var (r, _, bob) = Room(13913);
        void Eat<T>() where T : obj_item_slime_cookie
        {
            r.In(() =>
            {
                var cookie = Dm.@new<T>(bob.loc);
                Waiting.NoSleep(cookie.interact_with_atom(bob, bob, Dm.list()));
            });
            r.Step(1);
        }
        Eat<obj_item_slime_cookie_orange>();
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_RESISTCOLD)));
        Eat<obj_item_slime_cookie_yellow>();
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_SHOCKIMMUNE)));
        Eat<obj_item_slime_cookie_lightpink>();
        r.Step(Second);
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_PACIFISM)));
        Eat<obj_item_slime_cookie_sepia>();
        Assert.True(r.In(() => Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_timecookie)))));
        Eat<obj_item_slime_cookie_black>();
        Assert.True(r.In(() => Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_spookcookie)))));
        r.AssertNoRuntimes();
    }

    // ---- industrial ----

    [Fact]
    public void AnIndustrialExtractTurnsPlasmaIntoItsItems()
    {
        var (r, feed, bob) = Room(13914);
        var grey = r.In(() => Dm.@new<obj_item_slimecross_industrial_grey>(locate(4, 4, 1)));
        var metal = r.In(() => Dm.@new<obj_item_slimecross_industrial_metal>(locate(16, 16, 1)));
        r.In(() =>
        {
            Waiting.NoSleep(Holder(grey).add_reagent(typeof(datum_reagent_toxin_plasma), 2));
            Waiting.NoSleep(Holder(metal).add_reagent(typeof(datum_reagent_toxin_plasma), 3));
        });
        Assert.Equal("industrial_still", r.In(() => Dm.str(grey.icon_state)));
        // the tick it drinks the plasma it bubbles, the next it is still again
        for (int i = 0; i < 4 * Second && r.In(() => Dm.truthy(Holder(grey).has_reagent(typeof(datum_reagent_toxin_plasma)))); i++)
            r.Step(1);
        Assert.Equal("industrial", r.In(() => Dm.str(grey.icon_state)));
        r.Step(4 * Second);
        Assert.Equal("industrial_still", r.In(() => Dm.str(grey.icon_state)));
        Assert.Equal(5, Around(r, grey, 0).OfType<obj_item_food_monkeycube>().Count());
        Assert.Equal(10, r.In(() => Around(r, metal, 0).OfType<obj_item_stack_sheet_iron>().Sum(s => (int)Dm.num(s.amount))));
        r.AssertNoRuntimes();
    }

    // ---- prismatic, recurring, reproductive, self-sustaining ----

    [Fact]
    public void APrismaticExtractPaintsTheFloorAndTheGreyOneWashesIt()
    {
        var (r, _, bob) = Room(13915);
        turf floor = r.In(() => get_step(bob, EAST));
        var red = InHand<obj_item_slimecross_prismatic_red>(r, bob);
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(red.interact_with_atom(floor, bob, Dm.list()))));
        Assert.NotNull(r.In(() => floor.cached_color_filter)); // the paint is a colour filter
        // the grey one washes a plain colour off; tg's paint is a filter, which leaves the colour var alone, so on painted floor
        // the grey one paints grey over it, as in tg
        turf other = r.In(() => get_step(bob, WEST));
        r.In(() => Waiting.NoSleep(other.add_atom_colour("#ff0000", WASHABLE_COLOUR_PRIORITY)));
        Assert.Equal("#ff0000", r.In(() => Dm.str(other.color)));
        var grey = InHand<obj_item_slimecross_prismatic_grey>(r, bob);
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(grey.interact_with_atom(other, bob, Dm.list()))));
        Assert.True(r.In(() => Dm.eq(other.color, Dm.initial(other, "color"))), "the paint is washed off");
        DmList redFilter = r.In(() => floor.cached_color_filter);
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(grey.interact_with_atom(floor, bob, Dm.list()))));
        Assert.NotSame(redFilter, r.In(() => floor.cached_color_filter));
        Assert.Contains(r.Sounds.Heard, h => h.File?.EndsWith("slosh.ogg", StringComparison.Ordinal) == true);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARecurringExtractWrapsAPlainOneAndRechargesItsUses()
    {
        var (r, feed, bob) = Room(13916);
        var recurring = r.In(() => Dm.@new<obj_item_slimecross_recurring_grey>(locate(4, 4, 1)));
        r.Step(1);
        var extract = r.In(() => recurring.extract);
        r.In(() =>
        {
            Assert.IsType<obj_item_slime_extract_grey>(extract);
            Assert.Same(extract, recurring.loc);
            Assert.True(Dm.truthy(extract.recurring));
            Assert.Equal("recurring grey extract", extract.name);
            extract.extract_uses = 1;
            recurring.cooldown = 0;
        });
        r.Step(3 * Second);
        Assert.True(r.In(() => extract.extract_uses) >= 2, "a use came back");
        Assert.True(feed.Any("wraps a layer of goo around itself!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AReproductiveExtractFedThreeMonkeyCubesExpelsExtracts()
    {
        var (r, feed, bob) = Room(13917);
        var repro = r.In(() => Dm.@new<obj_item_slimecross_reproductive_orange>(bob.loc));
        Ready(r, repro);
        r.Step(1);
        for (int i = 0; i < 3; i++)
        {
            r.In(() =>
            {
                var cube = Dm.@new<obj_item_food_monkeycube>(bob.loc);
                Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_active_hand(cube))));
                Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(repro.item_interaction(bob, cube, Dm.list())));
            });
            r.Step(1);
        }
        int made = r.In(() => Dm.each<obj_item_slime_extract_orange>(bob.loc).Count());
        Assert.InRange(made, 1, 4);
        Assert.Equal(0, r.In(() => Dm.length(repro.contents)));
        Assert.True(feed.Any("briefly swells to a massive size, and expels"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASelfSustainingExtractSplitsIntoFourAutoslimes()
    {
        var (r, feed, bob) = Room(13918);
        var self = r.In(() => Dm.@new<obj_item_slimecross_selfsustaining_grey>(bob.loc));
        r.Step(1);
        Assert.True(r.In(() => QDELETED(self)));
        var autos = r.In(() => Dm.each<obj_item_autoslime>(bob.loc).ToList());
        Assert.Equal(4, autos.Count);
        r.In(() =>
        {
            Assert.Equal("self-sustaining grey extract", autos[0].name);
            Assert.IsType<obj_item_slime_extract_grey>(autos[0].extract);
        });
        // in hand: Bob picks the plasma reaction, and a baby slime comes out of the freed extract
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_active_hand(autos[0])))));
        r.In(() => Waiting.Detach(autos[0].attack_self(bob)));
        r.Step(2);
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(bob.tgui_open_uis).Last(u => Dm.str(u.@interface) == "ListInputWindow").window.id));
        string plasma = r.In(() => Dm.str(Dm.initial(typeof(datum_reagent_toxin_plasma), "name")));
        r.Command(() => ClientSeam.TguiMessage(bob.client, window, "act/submit", Dm.list(Dm.kv("entry", plasma))));
        r.Step(Second);
        Assert.True(r.In(() => QDELETED(autos[0])));
        Assert.Contains(Around(r, bob, 2), a => a is mob_living_basic_slime);
        r.AssertNoRuntimes();
    }

    // ---- regenerative ----

    [Fact]
    public void ARegenerativeExtractFullyHealsAndTheColoursAddTheirEffects()
    {
        var (r, feed, bob) = Room(13919);
        var alice = r.Dummy(9, 10);
        void Crush<T>(mob_living target) where T : obj_item_slimecross_regenerative
        {
            var core = InHand<T>(r, bob);
            r.In(() => Waiting.NoSleep(target.adjust_brute_loss(40)));
            r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(core.interact_with_atom(target, bob, Dm.list()))));
            r.Step(1);
            Assert.True(r.In(() => QDELETED(core)));
            Assert.Equal(0, r.In(() => Dm.num(target.get_brute_loss())));
        }
        Crush<obj_item_slimecross_regenerative_grey>(alice);
        Assert.True(feed.Any(", the milky goo regenerating "));
        Crush<obj_item_slimecross_regenerative_purple>(alice);
        Assert.True(r.In(() => Holder(alice).has_reagent(typeof(datum_reagent_medicine_regen_jelly)) != null));
        Crush<obj_item_slimecross_regenerative_silver>(alice);
        Assert.Equal(NUTRITION_LEVEL_FULL - 1, r.In(() => Dm.num(alice.nutrition)));
        Crush<obj_item_slimecross_regenerative_adamantine>(alice);
        Assert.Equal(10, r.In(() => alice.damage_resistance));
        Crush<obj_item_slimecross_regenerative_rainbow>(alice);
        Assert.True(r.In(() => HAS_TRAIT(alice, TRAIT_GODMODE)));
        Crush<obj_item_slimecross_regenerative_cerulean>(alice);
        Assert.True(r.In(() => bob.get_active_held_item() is obj_item_slimecross_regenerative), "a second core in Bob's hand");
        Crush<obj_item_slimecross_regenerative_metal>(alice);
        Assert.True(r.In(() => alice.loc is obj_structure_closet), "Alice is in a slimy closet");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBlackRegenerativeExtractLeavesADeadCopyAndTheLightPinkOneHealsTheUser()
    {
        var (r, feed, bob) = Room(13920);
        var alice = r.Dummy(9, 10);
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(30)));
        var black = InHand<obj_item_slimecross_regenerative_black>(r, bob);
        r.In(() => Waiting.NoSleep(black.interact_with_atom(alice, bob, Dm.list())));
        r.Step(1);
        var copy = Around(r, alice, 0).OfType<mob_living_carbon_human>().Where(h => h != alice).SingleOrDefault();
        Assert.NotNull(copy);
        Assert.Equal(DEAD, r.In(() => copy.stat));
        Assert.Equal(r.In(() => alice.real_name), r.In(() => copy.real_name));
        var pink = InHand<obj_item_slimecross_regenerative_lightpink>(r, bob);
        r.In(() => Waiting.NoSleep(pink.interact_with_atom(alice, bob, Dm.list())));
        r.Step(1);
        Assert.Equal(0, r.In(() => Dm.num(bob.get_brute_loss())));
        Assert.True(feed.Any("Some of the milky goo sprays onto you, as well!"));
        r.AssertNoRuntimes();
    }

    // ---- stabilized ----

    [Fact]
    public void AStabilizedExtractGivesItsEffectWhileCarriedAndFadesWhenDropped()
    {
        var (r, feed, bob) = Room(13921);
        var purple = InHand<obj_item_slimecross_stabilized_purple>(r, bob);
        r.Step(2 * Second + 2); // SSobj fires every two seconds
        Assert.True(r.In(() => Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_stabilized_purple)))));
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(10)));
        r.Step(5 * Second);
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) < 10, "the purple extract heals");
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(purple)));
        r.Step(2 * Second);
        Assert.False(r.In(() => Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_stabilized_purple)))));
        Assert.True(feed.Any("balloon: purple extract faded!"));

        // in a bag on his back it still counts
        var adamantine = r.In(() => Dm.@new<obj_item_slimecross_stabilized_adamantine>(bob.loc));
        var bag = r.In(() => Dm.@new<obj_item_storage_backpack>(bob.loc));
        r.In(() =>
        {
            Waiting.NoSleep(bob.equip_to_slot_or_del(bag, ITEM_SLOT_BACK));
            Waiting.NoSleep(adamantine.forceMove(bag));
        });
        r.Step(2 * Second);
        Assert.True(r.In(() => Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_stabilized_adamantine)))));
        Assert.Equal(5, r.In(() => bob.damage_resistance));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStabilizedRainbowExtractUsesItsStoredRegenerativeCoreAtCrit()
    {
        var (r, feed, bob) = Room(13922);
        var rainbow = InHand<obj_item_slimecross_stabilized_rainbow>(r, bob);
        var core = r.In(() => Dm.@new<obj_item_slimecross_regenerative_grey>(bob.loc));
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(rainbow.item_interaction(bob, core, Dm.list()))));
        Assert.Same(rainbow, r.In(() => core.loc));
        // the extract applies its effect when SSobj next processes it: SSobj fires every 2 s from a random first fire time
        for (int i = 0; i < 12 && r.In(() => bob.has_status_effect(typeof(datum_status_effect_stabilized_rainbow))) == null; i++)
            r.Step(Second / 4);
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_stabilized_rainbow))));
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(150)));
        r.Step(3 * Second);
        Assert.True(r.In(() => Dm.num(bob.health)) > 0, "Bob was healed out of crit");
        Assert.True(r.In(() => QDELETED(rainbow)));
        Assert.True(feed.Any("flashes a rainbow of colors"));
        r.AssertNoRuntimes();
    }

    // ---- potions ----

    [Fact]
    public void TheCrossbreedPotionsWork()
    {
        var (r, feed, bob) = Room(13923);
        // the cloner copies an extract with its uses, not a crossbreed
        var extract = r.In(() => Dm.@new<obj_item_slime_extract_gold>(bob.loc));
        r.In(() => extract.extract_uses = 3);
        var cloner = r.In(() => Dm.@new<obj_item_slimepotion_extract_cloner>(bob.loc));
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(cloner.interact_with_atom(extract, bob, Dm.list()))));
        Assert.Equal(2, r.In(() => Dm.each<obj_item_slime_extract_gold>(bob.loc).Count(e => e.extract_uses == 3)));

        // the reviver revives a dead slime, weaker
        var slime = r.In(() => Dm.@new<mob_living_basic_slime>(get_step(bob, EAST), typeof(datum_slime_type_grey)));
        r.Step(1);
        r.In(() => Waiting.NoSleep(slime.death()));
        float maxHealth = r.In(() => Dm.num(slime.maxHealth));
        var reviver = r.In(() => Dm.@new<obj_item_slimepotion_slime_reviver>(bob.loc));
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(reviver.interact_with_atom(slime, bob, Dm.list()))));
        Assert.NotEqual(DEAD, r.In(() => slime.stat));
        Assert.Equal(maxHealth - 10, r.In(() => Dm.num(slime.maxHealth)));

        // the omnistabilizer stops mutating
        var stab = r.In(() => Dm.@new<obj_item_slimepotion_slime_chargedstabilizer>(bob.loc));
        r.In(() => Waiting.NoSleep(stab.interact_with_atom(slime, bob, Dm.list())));
        Assert.Equal(0, r.In(() => Dm.num(slime.mutation_chance)));

        // pressurization and lavaproofing on clothes
        var gloves = r.In(() => Dm.@new<obj_item_clothing_gloves_color_black>(bob.loc));
        var space = r.In(() => Dm.@new<obj_item_slimepotion_spaceproof>(bob.loc));
        r.In(() => Waiting.NoSleep(space.interact_with_atom(gloves, bob, Dm.list())));
        r.In(() =>
        {
            Assert.True(Dm.truthy(Dm.bitand(gloves.clothing_flags, STOPSPRESSUREDAMAGE)));
            Assert.Equal(SPACE_SUIT_MIN_TEMP_PROTECT, Dm.num(gloves.min_cold_protection_temperature));
            Assert.Equal(1, space.uses);
        });
        var lava = r.In(() => Dm.@new<obj_item_slimepotion_lavaproof>(bob.loc));
        r.In(() => Waiting.NoSleep(lava.interact_with_atom(gloves, bob, Dm.list())));
        r.In(() => Assert.True(Dm.truthy(Dm.bitand(gloves.resistance_flags, LAVA_PROOF))));
        Assert.True(feed.Any("making it lavaproof."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheLoveAndPeacePotionsTakeTimeToFeed()
    {
        var (r, feed, bob) = Room(13924);
        var corgi = r.In(() => Dm.@new<mob_living_basic_pet_dog_corgi>(get_step(bob, EAST)));
        r.In(() => ADD_TRAIT(corgi, TRAIT_IMMOBILIZED, "test"));
        var love = InHand<obj_item_slimepotion_lovepotion>(r, bob);
        r.In(() => Waiting.Detach(love.interact_with_atom(corgi, bob, Dm.list())));
        r.Step(6 * Second);
        Assert.True(r.In(() => Dm.truthy(corgi.has_status_effect(typeof(datum_status_effect_in_love)))));
        Assert.True(r.In(() => Dm.truthy(corgi.has_ally(bob))));
        var peace = InHand<obj_item_slimepotion_peacepotion>(r, bob);
        r.In(() => Waiting.Detach(peace.interact_with_atom(corgi, bob, Dm.list())));
        r.Step(11 * Second);
        Assert.True(r.In(() => HAS_TRAIT(corgi, TRAIT_PACIFISM)));
        Assert.True(feed.Any("You feed"));
        r.AssertNoRuntimes();
    }

    // ---- weapons and clothing ----

    [Fact]
    public void TheBloodChillerShootsItsWieldersBloodAndRechargesFromIt()
    {
        var (r, feed, bob) = Room(13925);
        Squeeze(r, bob, InHand<obj_item_slimecross_chilling_green>(r, bob));
        var gun = r.In(() => bob.get_active_held_item() as obj_item_gun_magic_bloodchill);
        Assert.NotNull(gun);
        Assert.True(r.In(() => HAS_TRAIT(gun, TRAIT_NODROP)));
        float blood = r.In(() => Dm.num(bob.get_blood_volume()));
        r.In(() => gun.charges = 0);
        r.Step(3 * Second);
        Assert.Equal(1, r.In(() => gun.charges));
        // the 20u it took, less what his body regrew meanwhile
        Assert.InRange(r.In(() => Dm.num(bob.get_blood_volume())), blood - 20, blood - 19);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRebreatherMaskTheHeroineBudAndThePrismGlassesWork()
    {
        var (r, feed, bob) = Room(13926);
        var mask = r.In(() => Dm.@new<obj_item_clothing_mask_nobreath>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.equip_to_slot_or_del(mask, ITEM_SLOT_MASK)));
        r.Step(1);
        Assert.True(r.In(() => Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_rebreathing)))));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_NOBREATH)));

        var bud = r.In(() => Dm.@new<obj_item_clothing_head_peaceflower>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.equip_to_slot_or_del(bud, ITEM_SLOT_HEAD)));
        r.Step(1);
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_PACIFISM)));
        Assert.False(r.In(() => Dm.truthy(bud.can_mob_unequip(bob))));
        Assert.True(feed.Any("You feel at peace."));

        var glasses = r.In(() => Dm.@new<obj_item_clothing_glasses_prism_glasses>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.equip_to_slot_or_del(glasses, ITEM_SLOT_EYES)));
        r.Step(1);
        var place = r.In(() => Dm.locate<datum_action_item_action_place_light_prism>(glasses.actions));
        r.In(() => Waiting.NoSleep(place.do_effect()));
        var prism = Around(r, bob, 0).OfType<obj_structure_light_prism>().Single();
        Assert.Equal(5, r.In(() => Dm.num(prism.light_range)));
        r.In(() => Waiting.NoSleep(prism.attack_hand(bob)));
        Assert.True(r.In(() => QDELETED(prism)));
        r.AssertNoRuntimes();
    }

    // ---- the sweeps ----

    private static void SweepFamily(ulong seed, Type family, Action<AudioRoom, mob_living_carbon_human, obj_item> use, int settle = 7 * Second)
    {
        var failures = new List<string>();
        AudioRoom r = null;
        mob_living_carbon_human bob = null;
        int i = 0;
        foreach (Type colour in Colours(family))
        {
            if (i++ % 4 == 0)
                (r, _, bob) = Room(seed, 21, 21);
            try
            {
                r.In(() => Waiting.NoSleep(bob.revive(HEAL_ALL)));
                var item = InHand(r, bob, colour);
                use(r, bob, item);
                r.Step(settle);
                r.In(() => SStgui.close_user_uis(bob));
                r.Step(2);
            }
            catch (Exception e)
            {
                failures.Add(colour.Name + ": " + e.Message);
            }
            if (r.Room.Runtimes.Count > 0)
            {
                failures.Add(colour.Name + ": " + string.Join(" | ", r.Room.RuntimeText));
                r.Room.Runtimes.Clear();
            }
            Clear(r, bob);
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void EveryBurningColourActivates() => SweepFamily(13930, typeof(obj_item_slimecross_burning), (r, bob, x) =>
    {
        Squeeze(r, bob, x);
        // the black one turns Bob into a slime: he casts the slime form again to be himself for the next colour
        r.Step(Second);
        var shape = r.In(() => bob.loc as mob_living_basic_slime_transformed_slime);
        if (shape != null)
        {
            r.In(() => Waiting.Detach(Dm.locate<datum_action_cooldown_spell_shapeshift_slime_form>(shape.actions).Trigger(shape)));
            r.Step(Second);
            Assert.True(r.In(() => bob.loc is turf), "Bob is himself again");
        }
    });

    [Fact]
    public void EveryChargedColourActivates() => SweepFamily(13931, typeof(obj_item_slimecross_charged), (r, bob, x) =>
    {
        Squeeze(r, bob, x);
        // the oil one blows up where it is five seconds later: it goes off in a far corner, not in Bob's hand
        if (x is obj_item_slimecross_charged_oil && !r.In(() => QDELETED(x)))
            r.In(() => Waiting.NoSleep(x.forceMove(locate(2, 2, 1))));
    });

    [Fact]
    public void EveryChillingColourActivates() => SweepFamily(13932, typeof(obj_item_slimecross_chilling), (r, bob, x) => Squeeze(r, bob, x), 12 * Second);

    [Fact]
    public void EveryConsumingColourMakesAndEveryCookieIsEaten()
    {
        SweepFamily(13933, typeof(obj_item_slimecross_consuming), (r, bob, x) =>
        {
            var extract = (obj_item_slimecross_consuming)x;
            Ready(r, extract);
            r.In(() =>
            {
                Waiting.NoSleep(bob.dropItemToGround(extract));
                for (int n = 0; n < 6; n++)
                    Waiting.NoSleep(extract.item_interaction(bob, Dm.@new<obj_item_food_donut_plain>(bob.loc), Dm.list()));
            });
            r.Step(1);
            var cookie = r.In(() => Dm.each<obj_item_slime_cookie>(bob.loc).FirstOrDefault());
            Assert.True(cookie != null, extract.GetType().Name + " made no cookie");
            r.In(() => Waiting.Detach(cookie.interact_with_atom(bob, bob, Dm.list())));
        }, 12 * Second);
    }

    [Fact]
    public void EveryIndustrialColourProduces()
    {
        SweepFamily(13934, typeof(obj_item_slimecross_industrial), (r, bob, x) =>
        {
            r.In(() =>
            {
                Waiting.NoSleep(bob.dropItemToGround(x));
                Waiting.NoSleep(Holder(x).add_reagent(typeof(datum_reagent_toxin_plasma), 12));
            });
            r.Step(12 * Second);
            Assert.True(r.In(() => Dm.each<obj_item>(x.loc).Any(i => i != x && !(i is obj_item_slimecross))), x.GetType().Name + " made nothing");
        }, 1);
    }

    [Fact]
    public void EveryPrismaticColourPaints()
    {
        SweepFamily(13935, typeof(obj_item_slimecross_prismatic), (r, bob, x) =>
        {
            turf floor = r.In(() => get_step(bob, EAST));
            r.In(() => Waiting.NoSleep(x.interact_with_atom(floor, bob, Dm.list())));
            if (x is obj_item_slimecross_prismatic_rainbow)
                r.In(() => Waiting.Detach(x.attack_self(bob)));
        }, 2);
    }

    [Fact]
    public void EveryRecurringColourWrapsItsExtract()
    {
        SweepFamily(13936, typeof(obj_item_slimecross_recurring), (r, bob, x) =>
        {
            Assert.True(r.In(() => ((obj_item_slimecross_recurring)x).extract != null), x.GetType().Name + " has no extract");
        }, 2 * Second);
    }

    [Fact]
    public void EveryRegenerativeColourHeals()
    {
        SweepFamily(13937, typeof(obj_item_slimecross_regenerative), (r, bob, x) =>
        {
            var alice = r.Dummy(9, 10);
            r.In(() => Waiting.NoSleep(alice.adjust_brute_loss(30)));
            r.In(() => Waiting.Detach(x.interact_with_atom(alice, bob, Dm.list())));
            r.Step(2);
            Assert.Equal(0, r.In(() => Dm.num(alice.get_brute_loss())));
        }, 12 * Second);
    }

    [Fact]
    public void EveryReproductiveColourDigestsCubes()
    {
        SweepFamily(13938, typeof(obj_item_slimecross_reproductive), (r, bob, x) =>
        {
            Ready(r, x);
            for (int n = 0; n < 3; n++)
                r.In(() =>
                {
                    var cube = Dm.@new<obj_item_food_monkeycube>(bob.loc);
                    Waiting.NoSleep(bob.put_in_inactive_hand(cube));
                    Waiting.NoSleep(x.item_interaction(bob, cube, Dm.list()));
                });
            r.Step(1);
            Assert.True(r.In(() => Dm.each<obj_item_slime_extract>(range(1, bob)).Any()), x.GetType().Name + " made no extract");
        }, 2);
    }

    [Fact]
    public void EverySelfSustainingColourSplits()
    {
        SweepFamily(13939, typeof(obj_item_slimecross_selfsustaining), (r, bob, x) =>
        {
            Assert.True(r.In(() => QDELETED(x)));
            var auto = r.In(() => Dm.each<obj_item_autoslime>(range(1, bob)).First());
            r.In(() =>
            {
                Waiting.NoSleep(bob.put_in_inactive_hand(auto));
                Waiting.Detach(auto.attack_self(bob));
            });
        }, 2);
    }

    [Fact]
    public void EveryStabilizedColourGivesItsEffect()
    {
        SweepFamily(13940, typeof(obj_item_slimecross_stabilized), (r, bob, x) =>
        {
            r.Step(2 * Second + 2); // SSobj fires every two seconds
            Type effect = r.In(() => Dm.each_cast<Type>(subtypesof(typeof(datum_status_effect_stabilized))).First(e => Dm.eq(Dm.initial(e, "colour"), ((obj_item_slimecross)x).colour)));
            Assert.True(r.In(() => Dm.truthy(bob.has_status_effect(effect))), x.GetType().Name + " gave no " + effect.Name);
            r.Step(5 * Second);
            r.In(() => Waiting.NoSleep(bob.dropItemToGround(x)));
            r.Step(2 * Second);
            // the black one's tick returns before its base unless it drains someone, so in tg its effect never fades
            if (effect != typeof(datum_status_effect_stabilized_black))
                Assert.False(r.In(() => Dm.truthy(bob.has_status_effect(effect))), effect.Name + " stayed after the drop");
            else
                Assert.True(r.In(() => Dm.truthy(bob.has_status_effect(effect))), "the black effect fades, unlike tg's");
        }, 2);
    }

    /// <summary>
    /// Every type the crossbreeds made live is created and destroyed (the abstract recurring base aside, which tg never creates),
    /// and every crossbreed status effect is applied to a human for a few seconds and removed, without a runtime.
    /// </summary>
    [Fact]
    public void EveryCrossbreedTypeMakesAndDestroysAndEveryEffectAppliesAndGoes()
    {
        var (r, _, bob) = Room(13941);
        var roots = new[]
        {
            typeof(obj_item_slimecross), typeof(obj_item_slime_cookie), typeof(obj_item_slimecrossbeaker), typeof(obj_item_autoslime),
            typeof(obj_item_hothands), typeof(obj_item_barriercube), typeof(obj_item_capturedevice), typeof(obj_item_camera_rewind),
            typeof(obj_item_camera_timefreeze), typeof(obj_item_clothing_mask_nobreath), typeof(obj_item_clothing_glasses_prism_glasses),
            typeof(obj_item_clothing_head_peaceflower), typeof(obj_item_clothing_suit_armor_heavy_adamantine), typeof(obj_structure_light_prism),
            typeof(obj_item_melee_arm_blade_slime), typeof(obj_item_knife_rainbowknife), typeof(obj_item_shield_adamantineshield),
            typeof(obj_item_gun_magic_bloodchill), typeof(obj_projectile_magic_bloodchill), typeof(obj_item_slimepotion_extract_cloner),
            typeof(obj_item_slimepotion_peacepotion), typeof(obj_item_slimepotion_lovepotion), typeof(obj_item_slimepotion_spaceproof),
            typeof(obj_item_slimepotion_lavaproof), typeof(obj_item_slimepotion_slime_reviver), typeof(obj_item_slimepotion_slime_chargedstabilizer),
            typeof(obj_item_food_rationpack), typeof(mob_living_basic_slime_transformed_slime),
        };
        var types = roots.SelectMany(t => r.In(() => Dm.each_cast<Type>(typesof(t)).ToList()))
            .Where(t => !r.In(() => Dm.truthy(Dm.@in(t, get_abstract_types())))).Distinct().ToList();
        var failures = new List<string>();
        foreach (Type type in types)
        {
            if (InertSeam.IsInertType(type))
            {
                failures.Add(type.Name + " is inert");
                continue;
            }
            try
            {
                var made = r.In(() => Dm.@new(type, locate(3, 3, 1)));
                r.Step(1);
                r.In(() => qdel(made));
                r.Step(1);
            }
            catch (Exception e)
            {
                failures.Add(type.Name + ": " + e.Message);
            }
            if (r.Room.Runtimes.Count > 0)
            {
                failures.Add(type.Name + ": " + string.Join(" | ", r.Room.RuntimeText));
                r.Room.Runtimes.Clear();
            }
            Clear(r, bob);
        }

        var effects = new[]
        {
            typeof(datum_status_effect_rainbow_protection), typeof(datum_status_effect_slimeskin), typeof(datum_status_effect_slimerecall),
            typeof(datum_status_effect_slime_clone), typeof(datum_status_effect_slime_clone_decay), typeof(datum_status_effect_bloodchill),
            typeof(datum_status_effect_bonechill), typeof(datum_status_effect_rebreathing), typeof(datum_status_effect_firecookie),
            typeof(datum_status_effect_watercookie), typeof(datum_status_effect_metalcookie), typeof(datum_status_effect_sparkcookie),
            typeof(datum_status_effect_toxincookie), typeof(datum_status_effect_timecookie), typeof(datum_status_effect_lovecookie),
            typeof(datum_status_effect_tarcookie), typeof(datum_status_effect_tarfoot), typeof(datum_status_effect_spookcookie),
            typeof(datum_status_effect_peacecookie), typeof(datum_status_effect_plur), typeof(datum_status_effect_adamantinecookie),
            typeof(datum_status_effect_brokenpeace), typeof(datum_status_effect_pinkdamagetracker), typeof(datum_status_effect_bluespacestabilization),
        };
        foreach (Type effect in effects)
        {
            if (InertSeam.IsInertType(effect))
            {
                failures.Add(effect.Name + " is inert");
                continue;
            }
            try
            {
                r.In(() => Waiting.NoSleep(bob.apply_status_effect(effect)));
                r.Step(3 * Second);
                r.In(() => bob.remove_status_effect(effect));
                r.Step(1);
                r.In(() => Waiting.NoSleep(bob.revive(HEAL_ALL)));
            }
            catch (Exception e)
            {
                failures.Add(effect.Name + ": " + e.Message);
            }
            if (r.Room.Runtimes.Count > 0)
            {
                failures.Add(effect.Name + ": " + string.Join(" | ", r.Room.RuntimeText));
                r.Room.Runtimes.Clear();
            }
        }
        foreach (Type t in new[] { typeof(datum_action_cooldown_spell_shapeshift), typeof(datum_action_cooldown_spell_shapeshift_slime_form),
            typeof(datum_status_effect_shapechange_mob), typeof(datum_status_effect_shapechange_mob_from_spell), typeof(datum_storage_extract_inventory),
            typeof(atom_movable_screen_alert_status_effect_shapeshifted), typeof(datum_action_item_action_change_prism_colour),
            typeof(datum_action_item_action_place_light_prism), typeof(datum_status_effect_stabilized_gold), typeof(datum_status_effect_stabilized_black) })
            if (InertSeam.IsInertType(t))
                failures.Add(t.Name + " is inert");
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
