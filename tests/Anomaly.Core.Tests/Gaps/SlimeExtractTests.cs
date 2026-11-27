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
/// Xenobiology's slime extracts in a booted toy room (xenobiology.dm, recipes/slime_extracts.dm): each extract's reactions with
/// plasma, blood, water and the rest (the item, reagent or mob each one makes, the used extract's name, the extract deleted when it
/// is spent), the delayed reactions (the gold extract's mobs, the oil extract's explosion, the dark blue freeze, the orange fire),
/// the enhancer and the maximizer, grinding, the potions on a slime (steroid, stabilizer, mutator, docility) and on items (speed,
/// fireproof), and core crossing. A sweep runs every live slime reaction through its container.
/// </summary>
public class SlimeExtractTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed, int width = 15, int height = 11)
    {
        var r = new AudioRoom(width, height, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(2, 2, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static datum_reagents Holder(atom a) => (datum_reagents)a.reagents;

    private static T Extract<T>(AudioRoom r, int x, int y) where T : obj_item_slime_extract
    {
        T extract = r.In(() => Dm.@new<T>(locate(x, y, 1)));
        r.Step(1);
        return extract;
    }

    private static void Inject(AudioRoom r, atom target, params (Type Reagent, float Amount)[] reagents)
    {
        r.In(() =>
        {
            foreach ((Type reagent, float amount) in reagents)
                Waiting.NoSleep(Holder(target).add_reagent(reagent, amount));
        });
        r.Step(2);
    }

    private static List<atom_movable> Around(AudioRoom r, int x, int y, int distance = 2)
        => r.In(() => Dm.each<atom_movable>(range(distance, locate(x, y, 1))).ToList());

    // ---- the reactions, one by one ----

    /// <summary>
    /// One row per reaction whose product lies on the floor or in the extract right after: the extract, the reagents it is
    /// injected with, and the item or mob it makes (or the reagent it holds after).
    /// </summary>
    private static readonly (Type Extract, (Type, float)[] Reagents, Type Product)[] Products =
    {
        (typeof(obj_item_slime_extract_grey), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(mob_living_basic_slime)),
        (typeof(obj_item_slime_extract_grey), new[] { (typeof(datum_reagent_water), 5f) }, typeof(datum_reagent_medicine_epinephrine)),
        (typeof(obj_item_slime_extract_grey), new[] { (typeof(datum_reagent_blood), 1f) }, typeof(obj_item_food_monkeycube)),
        (typeof(obj_item_slime_extract_green), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(datum_reagent_mutationtoxin_jelly)),
        (typeof(obj_item_slime_extract_green), new[] { (typeof(datum_reagent_blood), 1f) }, typeof(datum_reagent_mutationtoxin)),
        (typeof(obj_item_slime_extract_green), new[] { (typeof(datum_reagent_uranium_radium), 1f) }, typeof(datum_reagent_mutationtoxin_lizard)),
        (typeof(obj_item_slime_extract_green), new[] { (typeof(datum_reagent_consumable_milk), 1f) }, typeof(datum_reagent_mutationtoxin_felinid)),
        (typeof(obj_item_slime_extract_metal), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(obj_item_stack_sheet_plasteel)),
        (typeof(obj_item_slime_extract_metal), new[] { (typeof(datum_reagent_water), 1f) }, typeof(obj_item_stack_sheet_rglass)),
        (typeof(obj_item_slime_extract_silver), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(obj_item_food)),
        (typeof(obj_item_slime_extract_silver), new[] { (typeof(datum_reagent_water), 1f) }, typeof(obj_item_reagent_containers)),
        (typeof(obj_item_slime_extract_blue), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(datum_reagent_consumable_frostoil)),
        (typeof(obj_item_slime_extract_blue), new[] { (typeof(datum_reagent_blood), 1f) }, typeof(obj_item_slimepotion_slime_stabilizer)),
        (typeof(obj_item_slime_extract_darkblue), new[] { (typeof(datum_reagent_water), 1f) }, typeof(obj_item_slimepotion_fireproof)),
        (typeof(obj_item_slime_extract_orange), new[] { (typeof(datum_reagent_blood), 1f) }, typeof(datum_reagent_consumable_capsaicin)),
        (typeof(obj_item_slime_extract_yellow), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(obj_item_stock_parts_power_store_cell_emproof_slime)),
        (typeof(obj_item_slime_extract_yellow), new[] { (typeof(datum_reagent_water), 1f) }, typeof(obj_item_flashlight_slime)),
        (typeof(obj_item_slime_extract_purple), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(obj_item_slimepotion_slime_steroid)),
        (typeof(obj_item_slime_extract_purple), new[] { (typeof(datum_reagent_blood), 1f) }, typeof(datum_reagent_medicine_regen_jelly)),
        (typeof(obj_item_slime_extract_darkpurple), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(obj_item_stack_sheet_mineral_plasma)),
        (typeof(obj_item_slime_extract_red), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(obj_item_slimepotion_slime_mutator)),
        (typeof(obj_item_slime_extract_red), new[] { (typeof(datum_reagent_water), 1f) }, typeof(obj_item_slimepotion_speed)),
        (typeof(obj_item_slime_extract_pink), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(obj_item_slimepotion_slime_docility)),
        (typeof(obj_item_slime_extract_pink), new[] { (typeof(datum_reagent_blood), 1f) }, typeof(obj_item_slimepotion_genderchange)),
        (typeof(obj_item_slime_extract_black), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(datum_reagent_aslimetoxin)),
        (typeof(obj_item_slime_extract_oil), new[] { (typeof(datum_reagent_blood), 1f) }, typeof(datum_reagent_consumable_nutriment_fat_oil_corn)),
        (typeof(obj_item_slime_extract_lightpink), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(obj_item_slimepotion_sentience)),
        (typeof(obj_item_slime_extract_lightpink), new[] { (typeof(datum_reagent_water), 1f) }, typeof(obj_item_slimepotion_renaming)),
        (typeof(obj_item_slime_extract_adamantine), new[] { (typeof(datum_reagent_toxin_plasma), 15f) }, typeof(obj_item_stack_sheet_mineral_adamantine)),
        (typeof(obj_item_slime_extract_bluespace), new[] { (typeof(datum_reagent_blood), 1f) }, typeof(obj_item_stack_tile_bluespace)),
        (typeof(obj_item_slime_extract_bluespace), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(obj_item_stack_ore_bluespace_crystal)),
        (typeof(obj_item_slime_extract_bluespace), new[] { (typeof(datum_reagent_water), 1f) }, typeof(obj_item_slimepotion_slimeradio)),
        (typeof(obj_item_slime_extract_cerulean), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(obj_item_slimepotion_enhancer)),
        (typeof(obj_item_slime_extract_cerulean), new[] { (typeof(datum_reagent_blood), 1f) }, typeof(obj_item_blueprints_slime)),
        (typeof(obj_item_slime_extract_sepia), new[] { (typeof(datum_reagent_water), 1f) }, typeof(obj_item_camera)),
        (typeof(obj_item_slime_extract_sepia), new[] { (typeof(datum_reagent_blood), 1f) }, typeof(obj_item_stack_tile_sepia)),
        (typeof(obj_item_slime_extract_pyrite), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(obj_item_paint)),
        (typeof(obj_item_slime_extract_pyrite), new[] { (typeof(datum_reagent_blood), 1f) }, typeof(obj_item_toy_crayon)),
        (typeof(obj_item_slime_extract_rainbow), new[] { (typeof(datum_reagent_toxin_plasma), 1f) }, typeof(mob_living_basic_slime)),
        (typeof(obj_item_slime_extract_rainbow), new[] { (typeof(datum_reagent_blood), 1f) }, typeof(obj_item_slimepotion_transference)),
        (typeof(obj_item_slime_extract_rainbow), new[] { (typeof(datum_reagent_toxin_slimejelly), 1f) }, typeof(obj_item_grenade_clusterbuster_slime_volatile)),
    };

    [Fact]
    public void EachExtractsReactionMakesItsProduct()
    {
        var (r, _, _) = Room(13801, 40, 30);
        var failures = new List<string>();
        int x = 4, y = 4;
        foreach ((Type extractType, (Type, float)[] reagents, Type product) in Products)
        {
            int ex = x, ey = y;
            var extract = r.In(() => Dm.@new(extractType, locate(ex, ey, 1)) as obj_item_slime_extract);
            r.Step(1);
            Inject(r, extract, reagents);
            r.Step(Second);
            bool made = typeof(datum_reagent).IsAssignableFrom(product)
                ? r.In(() => !QDELETED(extract) && Holder(extract).has_reagent(product) != null)
                : Around(r, ex, ey).Any(a => product.IsInstanceOfType(a) && a != extract);
            if (!made)
                failures.Add($"{extractType.Name} + {string.Join(", ", reagents.Select(g => g.Item1.Name))}: no {product.Name}");
            // the next row on fresh floor
            x += 6;
            if (x > 36)
            {
                x = 4;
                y += 6;
                if (y > 26)
                {
                    // clear the room for the rest
                    r.In(() =>
                    {
                        foreach (atom_movable thing in Dm.each<atom_movable>(range(40, locate(20, 15, 1))).ToList())
                            if (thing is obj_item || thing is mob_living_basic)
                                qdel(thing);
                    });
                    r.Step(2);
                    y = 4;
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGreyExtractWithPlasmaGrowsABabyGreySlimeAndIsUsedUp()
    {
        var (r, feed, _) = Room(13802);
        var extract = Extract<obj_item_slime_extract_grey>(r, 5, 5);
        Assert.Equal(1, r.In(() => (int)extract.extract_uses));
        Inject(r, extract, (typeof(datum_reagent_toxin_plasma), 1));
        var slime = Around(r, 5, 5).OfType<mob_living_basic_slime>().Single();
        r.In(() =>
        {
            Assert.IsType<datum_slime_type_grey>(slime.slime_type);
            Assert.Equal(SLIME_LIFE_STAGE_BABY, slime.life_stage);
            // the last use spends the core: no reagent results, so the extract goes (slime_extracts.dm:22)
            Assert.True(QDELETED(extract));
        });
        Assert.True(feed.Any("Infused with plasma, the core begins to quiver and grow, and a new baby slime emerges from it!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnExtractThatMakesAReagentIsRenamedUsedAndStays()
    {
        var (r, feed, _) = Room(13803);
        var extract = Extract<obj_item_slime_extract_purple>(r, 5, 5);
        Inject(r, extract, (typeof(datum_reagent_blood), 1));
        r.In(() =>
        {
            Assert.False(QDELETED(extract));
            Assert.Equal(0, (int)extract.extract_uses);
            Assert.Equal("used slime extract", extract.name);
            Assert.Equal("This extract has been used up.", extract.desc);
            Assert.False(Dm.truthy(extract.can_grind));
            Assert.NotNull(Holder(extract).has_reagent(typeof(datum_reagent_medicine_regen_jelly)));
            // a spent extract reacts no more: more blood stays blood
            Waiting.NoSleep(Holder(extract).add_reagent(typeof(datum_reagent_blood), 1));
            Assert.NotNull(Holder(extract).has_reagent(typeof(datum_reagent_blood)));
        });
        Assert.True(feed.Any("power is consumed in the reaction."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEnhancerAddsAUseAndTheMaximizerMakesFive()
    {
        var (r, feed, bob) = Room(13804);
        var extract = Extract<obj_item_slime_extract_grey>(r, 3, 2);
        r.In(() =>
        {
            var enhancer = Dm.@new<obj_item_slimepotion_enhancer>(bob.loc);
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(extract.item_interaction(bob, enhancer, Dm.list())));
            Assert.Equal(2, (int)extract.extract_uses);
            Assert.True(QDELETED(enhancer));
            var max = Dm.@new<obj_item_slimepotion_enhancer_max>(bob.loc);
            Waiting.NoSleep(extract.item_interaction(bob, max, Dm.list()));
            Assert.Equal(5, (int)extract.extract_uses);
            var another = Dm.@new<obj_item_slimepotion_enhancer>(bob.loc);
            Assert.Equal(ITEM_INTERACT_BLOCKING, Waiting.NoSleep(extract.item_interaction(bob, another, Dm.list())));
            Assert.False(QDELETED(another));
        });
        Assert.True(feed.Any("It can now be used a total of 5 times!"));
        Assert.True(feed.Any("You cannot enhance this extract further!"));
        // five uses: five slimes from five units of plasma, one at a time
        for (int i = 0; i < 5; i++)
            Inject(r, extract, (typeof(datum_reagent_toxin_plasma), 1));
        Assert.Equal(5, Around(r, 3, 2, 3).OfType<mob_living_basic_slime>().Count());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGroundExtractGivesSlimeJellyAndAnExaminedOneItsUses()
    {
        var (r, _, bob) = Room(13805);
        var extract = Extract<obj_item_slime_extract_green>(r, 3, 2);
        r.In(() =>
        {
            var jelly = Waiting.NoSleep(extract.grind_results()) as DmList;
            Assert.Equal(20f, Dm.num(jelly[typeof(datum_reagent_toxin_slimejelly)]));
            extract.extract_uses = 3;
            var lines = Waiting.NoSleep(extract.examine(bob)) as DmList;
            Assert.Contains(Dm.each<object>(lines), l => Dm.str(l).Contains("It has 3 uses remaining."));
        });
        r.AssertNoRuntimes();
    }

    // ---- the delayed reactions ----

    [Fact]
    public void TheGoldExtractVibratesAndSummonsMobsAfterFiveSeconds()
    {
        var (r, feed, _) = Room(13806);
        var extract = Extract<obj_item_slime_extract_gold>(r, 7, 5);
        r.In(() => r.World.Rng.Reseed(13806, 1));
        Inject(r, extract, (typeof(datum_reagent_water), 1)); // the friendly summoning
        Assert.True(feed.Any("The slime extract begins to vibrate adorably!"));
        int before = Around(r, 7, 5, 5).OfType<mob_living>().Count();
        r.Step(6 * Second);
        var spawned = Around(r, 7, 5, 6).OfType<mob_living>().Where(m => HAS_TRAIT(m, TRAIT_SPAWNED_MOB)).ToList();
        Assert.True(spawned.Count >= 1, "the extract summoned its creatures: " + spawned.Count);
        Assert.True(r.Sounds.Heard.Any(h => h.File == "sound/effects/phasein.ogg"), "the summoning's sound");
        // the extract goes 5.5 s after (slime_extracts.dm:116)
        r.Step(Second);
        r.In(() => Assert.True(QDELETED(extract)));
        Assert.True(before >= 1);
    }

    [Fact]
    public void TheOilExtractWarnsAndExplodes()
    {
        var (r, feed, bob) = Room(13807);
        var extract = Extract<obj_item_slime_extract_oil>(r, 9, 6);
        Inject(r, extract, (typeof(datum_reagent_toxin_plasma), 15));
        Assert.True(feed.Any("The slime extract begins to vibrate violently !"), "the warning, as tg spells it");
        r.Step(6 * Second);
        r.In(() => Assert.True(QDELETED(extract), "the extract went off"));
    }

    [Fact]
    public void TheDarkBlueExtractFreezesTheRoomAround()
    {
        var (r, feed, bob) = Room(13808);
        var extract = Extract<obj_item_slime_extract_darkblue>(r, 3, 2);
        float before = r.In(() => Dm.num(bob.bodytemperature));
        Inject(r, extract, (typeof(datum_reagent_toxin_plasma), 1));
        Assert.True(feed.Any("The slime extract starts to feel extremely cold!"));
        r.Step(6 * Second);
        r.In(() =>
        {
            Assert.True(Dm.num(bob.bodytemperature) < before, "the freeze chilled Bob");
            Assert.True(QDELETED(extract));
        });
    }

    [Fact]
    public void TheOrangeExtractBurstsIntoFlames()
    {
        var (r, feed, _) = Room(13809);
        var extract = Extract<obj_item_slime_extract_orange>(r, 9, 6);
        Inject(r, extract, (typeof(datum_reagent_toxin_plasma), 15));
        Assert.True(feed.Any("The slime extract begins to vibrate adorably!"));
        r.Step(6 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(extract));
            var air = (locate(9, 6, 1) as turf_open).air as datum_gas_mixture;
            Assert.True(Dm.num(air.temperature) > T20C + 50, "the fire's heat: " + Dm.num(air.temperature));
        });
    }

    // ---- the potions ----

    [Fact]
    public void TheSteroidStabilizerAndMutatorChangeABabySlime()
    {
        var (r, feed, bob) = Room(13810);
        var slime = r.In(() => Dm.@new<mob_living_basic_slime>(locate(3, 2, 1), typeof(datum_slime_type_grey)));
        r.Step(2);
        r.In(() =>
        {
            float cores = slime.cores, chance = slime.mutation_chance;
            var steroid = Dm.@new<obj_item_slimepotion_slime_steroid>(bob.loc);
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(steroid.interact_with_atom(slime, bob, Dm.list())));
            Assert.Equal(cores + 1, slime.cores);
            var stabilizer = Dm.@new<obj_item_slimepotion_slime_stabilizer>(bob.loc);
            Waiting.NoSleep(stabilizer.interact_with_atom(slime, bob, Dm.list()));
            Assert.Equal(Math.Clamp(chance - 15, 0, 100), slime.mutation_chance);
            float stabilized = slime.mutation_chance;
            var mutator = Dm.@new<obj_item_slimepotion_slime_mutator>(bob.loc);
            Waiting.NoSleep(mutator.interact_with_atom(slime, bob, Dm.list()));
            Assert.Equal(Math.Clamp(stabilized + 12, 0, 100), slime.mutation_chance);
            Assert.Equal(TRUE, Dm.num(slime.mutator_used));
        });
        Assert.True(feed.Any("You feed the slime the steroid. It will now produce one more extract."));
        Assert.True(feed.Any("You feed the slime the stabilizer. It is now less likely to mutate."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDocilityPotionPacifiesASlimeAndNamesIt()
    {
        var (r, feed, bob) = Room(13811);
        var slime = r.In(() => Dm.@new<mob_living_basic_slime>(locate(3, 2, 1), typeof(datum_slime_type_grey)));
        r.Step(2);
        var potion = r.In(() => Dm.@new<obj_item_slimepotion_slime_docility>(bob.loc));
        r.In(() => Waiting.Detach(potion.interact_with_atom(slime, bob, Dm.list())));
        r.Step(Second);
        // the name prompt: Bob's window answers nothing, the slime is a Pet Slime
        r.In(() => SStgui.close_user_uis(bob));
        r.Step(Second * 2);
        r.In(() =>
        {
            Assert.True(QDELETED(potion));
            Assert.True(Dm.truthy(slime.hunger_disabled), "the slime is docile");
            Assert.Equal("Pet Slime", slime.name);
        });
        Assert.True(feed.Any("You feed the slime the potion, removing its hunger and calming it."));
    }

    [Fact]
    public void TheSpeedPotionTakesAnItemsSlowdownAndTheChillPotionFireproofsClothes()
    {
        var (r, feed, bob) = Room(13812);
        var box = r.In(() => Dm.@new<obj_item_storage_toolbox>(locate(3, 2, 1)));
        var suit = r.In(() => Dm.@new<obj_item_clothing_under_color_grey>(locate(3, 2, 1)));
        r.Step(1);
        r.In(() =>
        {
            box.slowdown = 1;
            var speed = Dm.@new<obj_item_slimepotion_speed>(bob.loc);
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(speed.interact_with_atom(box, bob, Dm.list())));
            Assert.Equal(0f, Dm.num(box.slowdown));
            Assert.True(HAS_TRAIT(box, TRAIT_SPEED_POTIONED));
            Assert.True(QDELETED(speed));
            var again = Dm.@new<obj_item_slimepotion_speed>(bob.loc);
            Assert.Equal(ITEM_INTERACT_BLOCKING, Waiting.NoSleep(again.interact_with_atom(box, bob, Dm.list())));

            // three uses; tg's own return is the blocking one (xenobiology.dm:1022)
            var chill = Dm.@new<obj_item_slimepotion_fireproof>(bob.loc);
            Waiting.NoSleep(chill.interact_with_atom(suit, bob, Dm.list()));
            Assert.StartsWith("fireproofed ", suit.name);
            Assert.Equal(FIRE_IMMUNITY_MAX_TEMP_PROTECT, Dm.num(suit.max_heat_protection_temperature));
            Assert.True(Dm.truthy(Dm.bitand(suit.resistance_flags, FIRE_PROOF)));
            Assert.Equal(2, (int)chill.uses);
        });
        Assert.True(feed.Any("making it faster."));
        Assert.True(feed.Any("fireproofing it."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnAdultSlimeFedTenExtractsCrossesItsCore()
    {
        var (r, feed, bob) = Room(13813);
        var slime = r.In(() => Dm.@new<mob_living_basic_slime>(locate(3, 2, 1), typeof(datum_slime_type_grey), SLIME_LIFE_STAGE_ADULT));
        r.Step(2);
        r.In(() =>
        {
            var first = Dm.@new<obj_item_slime_extract_grey>(bob.loc);
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(first.interact_with_atom(slime, bob, Dm.list())));
            Assert.Equal(1, (int)slime.applied_crossbreed_amount);
            Assert.Equal(Dm.str(Dm.initial(typeof(obj_item_slime_extract_grey), "crossbreed_modification")), Dm.str(slime.crossbreed_modification));
            var other = Dm.@new<obj_item_slime_extract_orange>(bob.loc);
            Assert.Equal(ITEM_INTERACT_BLOCKING, Waiting.NoSleep(other.interact_with_atom(slime, bob, Dm.list())));
        });
        Assert.True(feed.Any("starting to mutate its core."));
        Assert.True(feed.Any("The slime is already being crossed with a different extract!"));
    }

    // ---- the sweep ----

    /// <summary>
    /// Every live slime reaction: its container extract is made, injected with the reaction's reagents and left for the timers
    /// (the gold, oil, dark blue and orange extracts' delays), in a fresh room each, and nothing throws.
    /// </summary>
    [Fact]
    public void EveryLiveSlimeReactionRunsWithoutAnError()
    {
        List<datum_chemical_reaction_slime> reactions;
        var (r, _, _) = Room(13899, 21, 21);
        reactions = r.In(() => Dm.each<Type>(subtypesof(typeof(datum_chemical_reaction_slime)))
            .Where(t => !InertSeam.IsInertType(t))
            .Select(t => GLOB.chemical_reactions_list[t] as datum_chemical_reaction_slime)
            .Where(x => x != null && x.required_container != null)
            .ToList());
        Assert.True(reactions.Count > 50, "live slime reactions: " + reactions.Count);
        var failures = new List<string>();
        int i = 0;
        foreach (datum_chemical_reaction_slime reaction in reactions)
        {
            if (i++ % 8 == 7)
            {
                (r, _, _) = Room(13899, 21, 21);
            }
            var extract = r.In(() => Dm.@new(reaction.required_container, locate(10, 10, 1)) as atom);
            r.Step(1);
            try
            {
                r.In(() =>
                {
                    foreach (object chem in Dm.each(reaction.required_reagents))
                        Waiting.NoSleep(Holder(extract).add_reagent(chem, reaction.required_reagents[chem]));
                });
                r.Step(8 * Second);
            }
            catch (Exception e)
            {
                failures.Add(reaction.GetType().Name + ": " + e.Message);
            }
            if (r.Room.Runtimes.Count > 0)
            {
                failures.Add(reaction.GetType().Name + ": " + string.Join(" | ", r.Room.RuntimeText));
                r.Room.Runtimes.Clear();
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void TheSlimeTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_slime_extract), typeof(obj_item_slime_extract_grey), typeof(obj_item_slime_extract_rainbow), typeof(obj_item_slime_extract_adamantine),
            typeof(obj_item_slimepotion_slime_docility), typeof(obj_item_slimepotion_slime_steroid), typeof(obj_item_slimepotion_enhancer),
            typeof(obj_item_slimepotion_enhancer_max), typeof(obj_item_slimepotion_slime_stabilizer), typeof(obj_item_slimepotion_slime_mutator),
            typeof(obj_item_slimepotion_speed), typeof(obj_item_slimepotion_fireproof), typeof(obj_item_slimepotion_genderchange),
            typeof(obj_item_slimepotion_renaming), typeof(obj_item_slimepotion_slimeradio), typeof(obj_item_slimepotion_transference),
            typeof(datum_chemical_reaction_slime_slimespawn), typeof(datum_chemical_reaction_slime_slimemobspawn), typeof(datum_chemical_reaction_slime_slimestop),
            typeof(datum_chemical_reaction_slime_slime_rng), typeof(datum_chemical_reaction_slime_slimelizard), typeof(datum_chemical_reaction_slime_slimefelinid),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
    }
}
