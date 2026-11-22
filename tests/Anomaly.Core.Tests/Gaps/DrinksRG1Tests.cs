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
/// Gaps package RG1 (D333, D339): the drinks in a booted toy room. The ethanol base (alcohol_reagents.dm:55): a human who drinks
/// beer grows tipsy, and with a strong drink drunk (drunk.dm: the alert, the psychotic reverb, the slurring from 16, the dizziness
/// from 41, the fearlessness from 51, the blurry eyes from 71, the toxins from 81, the brain damage and the blackout from 91); the
/// drinker traits, water and the liver change it. Job and healing drinks, the Gargle Blaster by its cycle; ethanol washing paper and
/// feeding a fire; coffee, milk and the juices; hot cooking oil frying a mob; capsaicin, frost oil, salt and flour.
/// </summary>
public class DrinksRG1Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static datum_reagents Blood(mob_living who) => (datum_reagents)who.reagents;

    /// <summary>Puts <paramref name="amount"/> of <typeparamref name="T"/> into the mob's blood and returns the reagent.</summary>
    private static T Dose<T>(mob_living who, float amount) where T : datum_reagent
    {
        Waiting.NoSleep(Blood(who).add_reagent(typeof(T), amount));
        return (T)Blood(who).has_reagent(typeof(T));
    }

    private static datum_status_effect_inebriated Inebriation(mob_living who) =>
        who.has_status_effect(typeof(datum_status_effect_inebriated)) as datum_status_effect_inebriated;

    // a timed status effect's duration is the deciseconds it has left (_status_effect.dm, process)
    private static float Remaining(mob_living who, Type effect)
    {
        var found = who.has_status_effect(effect) as datum_status_effect;
        return found == null ? 0 : Dm.num(found.duration);
    }

    private static void Sober(mob_living who)
    {
        who.remove_status_effect(typeof(datum_status_effect_inebriated));
        Waiting.NoSleep(Blood(who).clear_reagents());
    }

    [Fact]
    public void BeerMakesAHumanTipsyAndAStrongDrinkDrunkWithTheAlertAndTheReverb()
    {
        var (r, _, bob) = Room(9901);
        r.In(() => Dose<datum_reagent_consumable_ethanol_beer>(bob, 30));
        r.Step(6 * Second); // three Life ticks of 0.2 u of beer each
        r.In(() =>
        {
            var tipsy = Inebriation(bob);
            Assert.IsType<datum_status_effect_inebriated_tipsy>(tipsy);
            Assert.InRange(tipsy.drunk_value, 0.5f, 5.99f);
            Assert.Null(bob.alerts?["drunk"]);
            Assert.Equal(SOUND_ENVIRONMENT_NONE, bob.sound_environment_override);
        });

        r.In(() => Dose<datum_reagent_consumable_ethanol_vodka>(bob, 40));
        r.Step(12 * Second);
        r.In(() =>
        {
            var drunk = Inebriation(bob);
            Assert.IsType<datum_status_effect_inebriated_drunk>(drunk);
            Assert.True(drunk.drunk_value >= 6, "drunk value " + drunk.drunk_value);
            Assert.NotNull(bob.alerts["drunk"]); // drunk.dm:242, "drunk" over the user's HUD style
            Assert.Equal(SOUND_ENVIRONMENT_PSYCHOTIC, bob.sound_environment_override);
            // blood.dm:815: the drunk value times DRUNK_POWER_TO_BLOOD_ALCOHOL, to the hundredth
            Assert.Equal(round(drunk.drunk_value * 0.003f, 0.01f), Dm.num(bob.get_blood_alcohol_content()), 3);
            Assert.Equal(drunk.drunk_value, Dm.num(bob.get_drunk_amount()), 3);
        });

        // sobering below 6 goes back to tipsy: the alert and the reverb go with the drunk stage
        r.In(() =>
        {
            Waiting.NoSleep(Blood(bob).clear_reagents());
            Waiting.NoSleep(bob.set_drunk_effect(5));
            Assert.IsType<datum_status_effect_inebriated_tipsy>(Inebriation(bob));
            Assert.Null(bob.alerts["drunk"]);
            Assert.Equal(SOUND_ENVIRONMENT_NONE, bob.sound_environment_override);
        });
        // a tick takes 4% and 0.01 off: 5 - (0.01 + 0.2) = 4.79 (drunk.dm:70)
        r.Step(2 * Second + 2);
        r.In(() => Assert.InRange(Inebriation(bob).drunk_value, 4.5f, 4.79f + 0.001f));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDrunkStagesSlurDizzyMakeFearlessBlurAndPoison()
    {
        var (r, feed, bob) = Room(9902);
        datum_status_effect_inebriated_drunk Drunk(float value)
        {
            Waiting.NoSleep(bob.set_drunk_effect(value));
            var drunk = (datum_status_effect_inebriated_drunk)Inebriation(bob);
            Waiting.NoSleep(drunk.on_tick_effects());
            return drunk;
        }
        r.In(() =>
        {
            Waiting.NoSleep(bob.set_jitter(30 * SECONDS));
            Drunk(20);
            // from 16 the drunk slurring (speech_debuffs.dm), and a drunk always loses 6 s of jitter
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_speech_slurring_drunk)));
            Assert.InRange(Remaining(bob, typeof(datum_status_effect_jitter)), 24 * SECONDS - 1, 24 * SECONDS + 1);
            Assert.Null(bob.has_status_effect(typeof(datum_status_effect_dizziness)));
            Assert.Contains("flushed", Dm.str(Inebriation(bob).get_examine_text(bob)));

            Drunk(45); // from 41: at least 20 s of dizziness
            Assert.True(Remaining(bob, typeof(datum_status_effect_dizziness)) >= 20 * SECONDS - 1);
            Assert.False(HAS_TRAIT(bob, TRAIT_FEARLESS));

            Drunk(55); // from 51: at least 50 s of dizziness and fearless
            Assert.True(Remaining(bob, typeof(datum_status_effect_dizziness)) >= 50 * SECONDS - 1);
            Assert.True(HAS_TRAIT(bob, TRAIT_FEARLESS));
            Assert.Null(bob.has_status_effect(typeof(datum_status_effect_eye_blur)));

            Drunk(75); // from 71: blurry eyes of (75 * 2 - 140) seconds
            Assert.InRange(Remaining(bob, typeof(datum_status_effect_eye_blur)), 10 * SECONDS - 1, 10 * SECONDS + 1);

            float tox = Dm.num(bob.get_tox_loss());
            Drunk(85); // from 81: a point of toxins a tick
            Assert.Equal(tox + 1, Dm.num(bob.get_tox_loss()), 3);

            Drunk(40); // under 51 the fearlessness goes
            Assert.False(HAS_TRAIT(bob, TRAIT_FEARLESS));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Over91TheLiverlessDrinkerTakesBrainDamageAndSleepsItOff()
    {
        var (r, _, _) = Room(9903);
        // a body with no client cannot split into a blackout personality (split_personality.dm:24), so it falls asleep
        var drinker = r.Dummy(6, 4);
        r.Step(2);
        r.In(() =>
        {
            Waiting.NoSleep(drinker.set_drunk_effect(95));
            var drunk = (datum_status_effect_inebriated_drunk)Inebriation(drinker);
            float tox = Dm.num(drinker.get_tox_loss());
            float brain = Dm.num(drinker.get_organ_loss(ORGAN_SLOT_BRAIN));
            Waiting.NoSleep(drunk.on_tick_effects());
            Assert.Equal(tox + 2, Dm.num(drinker.get_tox_loss()), 3); // 81 and 91
            Assert.Equal(brain + 0.4f, Dm.num(drinker.get_organ_loss(ORGAN_SLOT_BRAIN)), 3);
            Assert.NotNull(drinker.has_status_effect(typeof(datum_status_effect_incapacitating_sleeping)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDrinkerTraitsWaterAndTheLiverChangeHowDrunkAndHowHurt()
    {
        var (r, _, _) = Room(9904);
        var drinker = r.Dummy(6, 4);
        r.Step(2);
        r.In(() =>
        {
            // 1 * sqrt(25) * 25 * ALCOHOL_RATE * ratio 1 * 2 s = 1.25 (alcohol_reagents.dm:79)
            var beer = Dose<datum_reagent_consumable_ethanol_beer>(drinker, 25);
            Waiting.NoSleep(beer.on_mob_life(drinker, 2, 1));
            Assert.Equal(1.25f, Dm.num(drinker.get_drunk_amount()), 3);
            Sober(drinker);

            ADD_TRAIT(drinker, TRAIT_ALCOHOL_TOLERANCE, "test"); // an accomplished drinker: 0.7 of it
            beer = Dose<datum_reagent_consumable_ethanol_beer>(drinker, 25);
            Waiting.NoSleep(beer.on_mob_life(drinker, 2, 1));
            Assert.Equal(0.875f, Dm.num(drinker.get_drunk_amount()), 3);
            Sober(drinker);
            REMOVE_TRAIT(drinker, TRAIT_ALCOHOL_TOLERANCE, "test");

            ADD_TRAIT(drinker, TRAIT_LIGHT_DRINKER, "test"); // twice
            beer = Dose<datum_reagent_consumable_ethanol_beer>(drinker, 25);
            Waiting.NoSleep(beer.on_mob_life(drinker, 2, 1));
            Assert.Equal(2.5f, Dm.num(drinker.get_drunk_amount()), 3);
            Sober(drinker);
            REMOVE_TRAIT(drinker, TRAIT_LIGHT_DRINKER, "test");

            // as much water as beer halves it
            beer = Dose<datum_reagent_consumable_ethanol_beer>(drinker, 25);
            Dose<datum_reagent_water>(drinker, 25);
            Waiting.NoSleep(beer.on_mob_life(drinker, 2, 1));
            Assert.Equal(0.625f, Dm.num(drinker.get_drunk_amount()), 3);
            Sober(drinker);

            // the liver: sqrt(25) * 65 ** 1.6 * its tolerance (ALCOHOL_RATE) * 2 s / 150, halved for a heavy drinker
            var liver = (obj_item_organ_liver)drinker.get_organ_slot(ORGAN_SLOT_LIVER);
            float expected = 5 * Dm.pow(65f, 1.6f) * ALCOHOL_RATE * 2 / 150;
            float before = Dm.num(liver.damage);
            var vodka = Dose<datum_reagent_consumable_ethanol_vodka>(drinker, 25);
            Waiting.NoSleep(vodka.on_mob_life(drinker, 2, 1));
            Assert.Equal(before + expected, Dm.num(liver.damage), 3);
            Sober(drinker);
            ADD_TRAIT(drinker, TRAIT_HEAVY_DRINKER, "test");
            before = Dm.num(liver.damage);
            vodka = Dose<datum_reagent_consumable_ethanol_vodka>(drinker, 25);
            Waiting.NoSleep(vodka.on_mob_life(drinker, 2, 1));
            Assert.Equal(before + expected / 2, Dm.num(liver.damage), 3);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void JobDrinksHealTheirJobAndTheGargleBlasterHitsByItsCycle()
    {
        var (r, _, _) = Room(9905);
        var drinker = r.Dummy(6, 4);
        r.Step(2);
        r.In(() =>
        {
            var liver = (obj_item_organ_liver)drinker.get_organ_slot(ORGAN_SLOT_LIVER);
            // on one limb: heal_bodypart_damage heals one picked damaged part (carbon/damage_procs.dm)
            Waiting.NoSleep(drinker.apply_damage(20, BRUTE, BODY_ZONE_CHEST, wound_bonus: CANT_WOUND));

            // the quadruple sec heals a security liver a point of brute and burn a second (alcohol_reagents.dm:1687)
            var sec = Dose<datum_reagent_consumable_ethanol_quadruple_sec>(drinker, 10);
            Waiting.NoSleep(sec.on_mob_life(drinker, 2, 1));
            Assert.Equal(20f, Dm.num(drinker.get_brute_loss()), 3); // no security liver, no healing
            ADD_TRAIT(liver, TRAIT_LAW_ENFORCEMENT_METABOLISM, "test");
            Waiting.NoSleep(sec.on_mob_life(drinker, 2, 1));
            Assert.Equal(18f, Dm.num(drinker.get_brute_loss()), 3);
            Sober(drinker);

            // the bartender's handshake heals a bartender's liver and disgusts the rest: 2 * REM * 2 s (alcohol_reagents.dm:3297)
            float disgust = drinker.disgust;
            var handshake = Dose<datum_reagent_consumable_ethanol_bartenders_handshake>(drinker, 10);
            Waiting.NoSleep(handshake.on_mob_life(drinker, 2, 1));
            Assert.Equal(disgust + 2 * REAGENTS_EFFECT_MULTIPLIER() * 2, drinker.disgust, 3);
            ADD_TRAIT(liver, TRAIT_BARTENDER_METABOLISM, "test");
            float brute = Dm.num(drinker.get_brute_loss());
            Waiting.NoSleep(handshake.on_mob_life(drinker, 2, 1));
            Assert.Equal(brute - 1 * REAGENTS_EFFECT_MULTIPLIER() * 2, Dm.num(drinker.get_brute_loss()), 3);
            Sober(drinker);

            // the Pan-Galactic Gargle Blaster has no boozepwr of its own: dizziness, then slurring from cycle 16, the high from 56
            var gargle = Dose<datum_reagent_consumable_ethanol_gargle_blaster>(drinker, 10);
            Waiting.NoSleep(gargle.on_mob_life(drinker, 2, 1));
            Assert.InRange(Remaining(drinker, typeof(datum_status_effect_dizziness)), 6 * SECONDS - 1, 6 * SECONDS + 1);
            Assert.Null(Inebriation(drinker));
            gargle.current_cycle = 20;
            Waiting.NoSleep(gargle.on_mob_life(drinker, 2, 1));
            Assert.NotNull(drinker.has_status_effect(typeof(datum_status_effect_speech_slurring_generic)));
            gargle.current_cycle = 60;
            Waiting.NoSleep(gargle.on_mob_life(drinker, 2, 1));
            Assert.NotNull(drinker.has_status_effect(typeof(datum_status_effect_drugginess)));
            Sober(drinker);

            // the hearty punch heals the dying: 3.75 brute a second while the health is at 0 or less (alcohol_reagents.dm:1442)
            Waiting.NoSleep(drinker.adjust_brute_loss(200));
            Waiting.NoSleep(drinker.updatehealth());
            Assert.True(drinker.health <= 0);
            brute = Dm.num(drinker.get_brute_loss());
            var punch = Dose<datum_reagent_consumable_ethanol_hearty_punch>(drinker, 10);
            Waiting.NoSleep(punch.on_mob_life(drinker, 2, 1));
            Assert.Equal(brute - 7.5f, Dm.num(drinker.get_brute_loss()), 3);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EthanolWashesPaperAndABookAndFeedsAFire()
    {
        var (r, feed, bob) = Room(9906);
        var paper = Place<obj_item_paper>(r, 5, 4);
        var book = Place<obj_item_book>(r, 6, 4);
        r.In(() =>
        {
            paper.add_raw_text("Remember the beer.");
            Assert.True(Dm.num(paper.get_total_length()) > 0);
            book.book_data.set_content("A Manual of Mixology");
            var vodka = Dm.@new<datum_reagent_consumable_ethanol_vodka>();
            Waiting.NoSleep(vodka.expose_obj(paper, 5, TOUCH));
            Assert.Equal(0f, Dm.num(paper.get_total_length()));
            Waiting.NoSleep(vodka.expose_obj(book, 5, TOUCH));
            Assert.Equal("N/A", Dm.str(book.book_data.get_content())); // an empty book's default (book_info.dm)

            // a splash on a burning mob: reac_volume / 15 more fire stacks (alcohol_reagents.dm:109) and a sterilizing surgery mod
            Waiting.NoSleep(bob.adjust_fire_stacks(1));
            bob.ignite_mob();
            float stacks = bob.fire_stacks;
            var holder = Dm.@new<datum_reagents>(30);
            Waiting.NoSleep(holder.add_reagent(typeof(datum_reagent_consumable_ethanol_vodka), 30));
            Waiting.NoSleep(holder.expose(bob, TOUCH));
            Assert.Equal(stacks + 2, bob.fire_stacks, 3);
            Assert.NotNull(LAZYACCESS(bob.mob_surgery_speed_mods, "alcohol"));
        });
        // the paper's "ink washes away" goes to usr, the splasher (none here); the book's is seen around it
        Assert.True(feed.Any("writing is washed away"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void CoffeeWakesMilkCalmsTheBurnAndCarrotJuiceClearsTheEyes()
    {
        var (r, _, _) = Room(9907);
        var drinker = r.Dummy(6, 4);
        r.Step(2);
        r.In(() =>
        {
            Waiting.NoSleep(drinker.set_dizzy(20 * SECONDS));
            Waiting.NoSleep(drinker.set_drowsiness(20 * SECONDS));
            Waiting.NoSleep(drinker.set_eye_blur(20 * SECONDS));
            float temperature = drinker.bodytemperature = 260; // under the normal temperature it warms toward
            // coffee: -5 s of dizziness, -3 s of drowsiness a second, and warms toward the normal body temperature (drink_reagents.dm:298)
            var coffee = Dose<datum_reagent_consumable_coffee>(drinker, 10);
            Waiting.NoSleep(coffee.on_mob_life(drinker, 2, 1));
            Assert.InRange(Remaining(drinker, typeof(datum_status_effect_dizziness)), 10 * SECONDS - 1, 10 * SECONDS + 1);
            Assert.InRange(Remaining(drinker, typeof(datum_status_effect_drowsiness)), 14 * SECONDS - 1, 14 * SECONDS + 1);
            Assert.Equal(temperature + 12.5f * TEMPERATURE_DAMAGE_COEFFICIENT * 2, drinker.bodytemperature, 2);
            // an overdose jitters: at least 5 s a second
            Waiting.NoSleep(coffee.overdose_process(drinker, 2, 1));
            Assert.True(Remaining(drinker, typeof(datum_status_effect_jitter)) >= 10 * SECONDS - 1);

            // milk takes a unit of capsaicin a second out of the blood (drink_reagents.dm:243); the coffee goes first, or the milk
            // would make a cafe latte with it
            Sober(drinker);
            Dose<datum_reagent_consumable_capsaicin>(drinker, 10);
            var milk = Dose<datum_reagent_consumable_milk>(drinker, 10);
            Waiting.NoSleep(milk.on_mob_life(drinker, 2, 1));
            Assert.Equal(8f, Dm.num(Blood(drinker).get_reagent_amount(typeof(datum_reagent_consumable_capsaicin))), 3);

            // carrot juice: a second of blur less a second (drink_reagents.dm:56)
            Sober(drinker);
            var carrot = Dose<datum_reagent_consumable_carrotjuice>(drinker, 10);
            Waiting.NoSleep(carrot.on_mob_life(drinker, 2, 1));
            Assert.InRange(Remaining(drinker, typeof(datum_status_effect_eye_blur)), 18 * SECONDS - 1, 18 * SECONDS + 1);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void HotCookingOilFriesAMobAndColdOilDoesNot()
    {
        var (r, feed, _) = Room(9908);
        var drinker = r.Dummy(6, 4);
        r.Step(2);
        r.In(() =>
        {
            // cold oil coats and does nothing
            var cold = Dm.@new<datum_reagents>(50);
            Waiting.NoSleep(cold.add_reagent(typeof(datum_reagent_consumable_nutriment_fat_oil), 20, reagtemp: 300));
            Waiting.NoSleep(cold.expose(drinker, TOUCH));
            Assert.Equal(0f, Dm.num(drinker.get_fire_loss()), 3);

            // 600 K, over the fry temperature of 450: (600 / 450) * 0.33 burn a unit, 20 u: round(8.8), DM's floor, 8 (food_reagents.dm:197)
            var hot = Dm.@new<datum_reagents>(50);
            Waiting.NoSleep(hot.add_reagent(typeof(datum_reagent_consumable_nutriment_fat_oil), 20, reagtemp: 600));
            Waiting.NoSleep(hot.expose(drinker, TOUCH));
            Assert.Equal(8f, Dm.num(drinker.get_fire_loss()), 3);
            Assert.True(HAS_TRAIT(drinker, TRAIT_OIL_FRIED));
            // fried, a second splash within two seconds does nothing more
            Waiting.NoSleep(hot.expose(drinker, TOUCH));
            Assert.Equal(8f, Dm.num(drinker.get_fire_loss()), 3);
        });
        Assert.True(feed.Any("The boiling oil sizzles as it covers"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/fryer/deep_fryer_emerge.ogg");
        r.Step(2 * Second + 2);
        r.In(() => Assert.False(HAS_TRAIT(drinker, TRAIT_OIL_FRIED))); // unfry_mob after 2 s
        r.AssertNoRuntimes();
    }

    [Fact]
    public void CapsaicinWarmsFrostOilChillsAndSaltAndFlourPileUpAndSoakAWound()
    {
        var (r, feed, _) = Room(9909);
        var eater = r.Dummy(6, 4);
        r.Step(2);
        r.In(() =>
        {
            // the first cycles: capsaicin 0.5 * 1.5 * 5 a second, frost oil 0.5 * 1.5 * -10 (food_reagents.dm:395, :423)
            eater.bodytemperature = 310;
            var capsaicin = Dose<datum_reagent_consumable_capsaicin>(eater, 10);
            capsaicin.current_cycle = 1;
            Waiting.NoSleep(capsaicin.on_mob_life(eater, 2, 1));
            Assert.Equal(310 + 0.5f * TEMPERATURE_DAMAGE_COEFFICIENT * 5 * 2, eater.bodytemperature, 2);
            Sober(eater);
            eater.bodytemperature = 310;
            var frost = Dose<datum_reagent_consumable_frostoil>(eater, 10);
            frost.current_cycle = 1;
            Waiting.NoSleep(frost.on_mob_life(eater, 2, 1));
            Assert.Equal(310 - 0.5f * TEMPERATURE_DAMAGE_COEFFICIENT * 10 * 2, eater.bodytemperature, 2);
            Sober(eater);

            // salt and flour on a floor leave their piles (food_reagents.dm:518, :733)
            turf floor = locate(7, 5, 1) as turf;
            Waiting.NoSleep(Dm.@new<datum_reagent_consumable_salt>().expose_turf(floor, 5));
            Assert.NotNull(Dm.locate<obj_effect_decal_cleanable_food_salt>(floor));
            turf other = locate(8, 5, 1) as turf;
            Waiting.NoSleep(Dm.@new<datum_reagent_consumable_flour>().expose_turf(other, 5));
            Assert.NotNull(Dm.locate<obj_effect_decal_cleanable_food_flour>(other));

            // salt on a cut soaks up its blood: -0.1 a unit, down to half its first flow at best (food_reagents.dm:545)
            var cut = (datum_wound_slash_flesh)Waiting.NoSleep(Dm.new_async(typeof(datum_wound_slash_flesh_moderate)));
            Waiting.NoSleep(cut.apply_wound((obj_item_bodypart)eater.get_bodypart(BODY_ZONE_CHEST)));
            float flow = Dm.num(cut.blood_flow);
            float clot = Dm.num(cut.clot_rate);
            var salt = Dm.@new<datum_reagents>(50);
            Waiting.NoSleep(salt.add_reagent(typeof(datum_reagent_consumable_salt), 20));
            Waiting.NoSleep(salt.expose(eater, TOUCH));
            Assert.Equal(max(flow - 2, Dm.num(cut.initial_flow) * 0.5f), Dm.num(cut.blood_flow), 3);
            Assert.Equal(clot * 0.75f, Dm.num(cut.clot_rate), 3);
        });
        Assert.True(feed.Any("The salt bits seep in"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.AssertNoRuntimes();
    }
    /// <summary>
    /// A sweep over every drink and food reagent (the 500-odd live subtypes of /datum/reagent/consumable): each is put in a
    /// fresh body's blood and runs its tg procs once (metabolize, life, overdose, end of metabolism, the ingestion and the splash
    /// on the body, a splash on a floor and on an item), so that no translated proc throws (a cast, a null, an untranslated
    /// leaf) on the paths a drink takes.
    /// </summary>
    [Fact]
    public void EveryDrinkAndFoodReagentRunsItsProcsWithoutAnError()
    {
        var (r, _, _) = Room(9910);
        var failures = new List<string>();
        int ran = 0;
        List<Type> types = r.In(() => Dm.each<Type>(subtypesof(typeof(datum_reagent_consumable))).Where(t => !InertSeam.IsInertType(t)).ToList());
        Assert.True(types.Count > 450, "live consumables: " + types.Count);
        mob_living_carbon_human body = null;
        foreach (Type type in types)
        {
            if (body == null || r.In(() => QDELETED(body) || body.stat == DEAD || !ishuman(body)))
            {
                body = r.Dummy(5, 5);
                r.Step(1);
            }
            var who = body;
            r.In(() =>
            {
                string step = "add";
                try
                {
                    Waiting.NoSleep(Blood(who).clear_reagents());
                    Waiting.NoSleep(Blood(who).add_reagent(type, 10));
                    if (Blood(who).has_reagent(type) is not datum_reagent reagent)
                        return; // it reacted away at once
                    ran++;
                    step = "on_mob_add";
                    Waiting.NoSleep(reagent.on_mob_add(who, 10));
                    step = "on_mob_metabolize";
                    Waiting.NoSleep(reagent.on_mob_metabolize(who));
                    step = "on_mob_life";
                    reagent.current_cycle = 20;
                    Waiting.NoSleep(reagent.on_mob_life(who, 2, 1));
                    step = "overdose";
                    Waiting.NoSleep(reagent.overdose_start(who, 1));
                    Waiting.NoSleep(reagent.overdose_process(who, 2, 1));
                    step = "on_mob_end_metabolize";
                    Waiting.NoSleep(reagent.on_mob_end_metabolize(who, 1));
                    step = "expose_mob";
                    Waiting.NoSleep(reagent.expose_mob(who, INGEST, 5));
                    Waiting.NoSleep(reagent.expose_mob(who, TOUCH, 5));
                    step = "expose_turf";
                    Waiting.NoSleep(reagent.expose_turf(locate(7, 6, 1) as turf, 5));
                    step = "expose_obj";
                    var target = Dm.@new<obj_item_toy_plush_carpplushie>(locate(7, 6, 1));
                    Waiting.NoSleep(reagent.expose_obj(target, 5, TOUCH));
                    step = "on_mob_delete";
                    Waiting.NoSleep(Blood(who).clear_reagents());
                    who.remove_status_effect(typeof(datum_status_effect_inebriated));
                    Waiting.NoSleep(who.fully_heal(HEAL_ALL));
                }
                catch (Exception e)
                {
                    failures.Add(DmTypes.PathOf(type) + " " + step + ": " + e.GetType().Name + " " + e.Message.Split('\n')[0]);
                }
            });
        }
        r.Step(2 * Second);
        var runtimes = r.Room.RuntimeText.Select(t => t.Split('\n')[0]).Distinct().ToList();
        Assert.True(ran > 400, ran + " of " + types.Count + " reagents stayed in the blood to run");
        Assert.True(failures.Count == 0 && runtimes.Count == 0, $"{ran} reagents ran; failures:\n" + string.Join("\n", failures) + "\nruntimes:\n" + string.Join("\n", runtimes));
    }
}

