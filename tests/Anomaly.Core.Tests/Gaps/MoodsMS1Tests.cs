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
/// Gaps package MS1 (D333, D339), in a booted toy room with Bob as a player: the mood datum on every human (tg's setup_mood), the mood
/// events with their timeouts (a hug, a good meal, a good drink, a death in view), the hunger moodlets read from nutrition, the mood
/// level and the sanity that SSmood moves each second, the sanity's effects (the movespeed and actionspeed modifiers, the crit
/// threshold, the sanity hallucinations), print_mood's text, the mood HUD's face and colour and its click, and every mood event type
/// created, added and removed.
/// </summary>
public class MoodsMS1Tests
{
    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        // a nutrition between hungry and fed carries no hunger moodlet (mood.dm:142); set before SSmood's first fire
        r.In(() => Waiting.NoSleep(bob.set_nutrition(300)));
        r.Step(Second);
        r.In(() => Assert.NotNull(bob.mind));
        r.Step(2);
        return (r, feed, bob);
    }

    private static datum_mood_event Event(AudioRoom r, mob_living who, string category) => r.In(() => who.mob_mood.mood_events[category] as datum_mood_event);

    private static atom_movable_screen_mood Face(AudioRoom r, mob who) => r.In(() => who.hud_used.screen_objects[HUD_MOB_MOOD] as atom_movable_screen_mood);

    [Fact]
    public void AHumansMoodStartsNeutralWithItsFaceOnTheHud()
    {
        var (r, feed, bob) = Room(10201);
        r.In(() =>
        {
            Assert.IsType<datum_mood>(bob.mob_mood);
            Assert.Same(bob, bob.mob_mood.mob_parent);
            Assert.Equal((float)SANITY_NEUTRAL, bob.mob_mood.sanity);
            Assert.Equal((float)SANITY_LEVEL_NEUTRAL, bob.mob_mood.sanity_level);
            Assert.Equal((float)MOOD_LEVEL_NEUTRAL, bob.mob_mood.mood_level);
            Assert.Null(bob.mob_mood.mood_events["nutrition"]);
            Assert.True(HAS_TRAIT(bob, TRAIT_AREA_SENSITIVE), "the mood makes its mob area-sensitive (mood.dm:62)");
            Assert.True(((DmList)SSmood.processing).Find(bob.mob_mood) > 0, "SSmood processes the mood");
        });

        // the face: tg's mood5 in ui_mood, the hunger display beside it at ui_hunger (screen_objects.dm:1176)
        atom_movable_screen_mood face = Face(r, bob);
        Assert.NotNull(face);
        r.In(() =>
        {
            Assert.Equal(ui_mood, face.screen_loc);
            Assert.Equal("mood5", face.icon_state);
            Assert.Equal(ui_hunger, ((atom_movable_screen)bob.hud_used.screen_objects[HUD_MOB_HUNGER]).screen_loc);
            Assert.True(((DmList)bob.client.screen).Find(face) > 0, "the face is on the client's screen");
        });

        // a monkey has no mood (only humans set one up, human.dm:43)
        r.In(() =>
        {
            var monkey = Dm.@new<mob_living_carbon_human_species_monkey>(locate(6, 6, 1));
            Assert.NotNull(monkey.mob_mood); // a monkey is a human of the monkey species: it has a mood as in tg
            var mouse = Dm.@new<mob_living_basic_mouse>(locate(7, 6, 1));
            Assert.Null(mouse.mob_mood);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHugAGoodMealAGoodDrinkAndADeathInViewMoveTheMoodWithTheirTimeouts()
    {
        var (r, feed, bob) = Room(10202);
        mob_living_carbon_human alice = r.Join(5, 4, "Alice");
        r.Step(Second);
        r.In(() => Waiting.NoSleep(alice.set_nutrition(300)));

        // Alice hugs Bob: the hug moodlet (carbon_defense.dm: equal temperatures give the warm hug), 2 minutes
        r.In(() => Waiting.NoSleep(bob.help_shake_act(alice)));
        r.Step(2);
        datum_mood_event hug = Event(r, bob, "hug");
        Assert.IsType<datum_mood_event_warmhug>(hug);
        Assert.Equal(1f, hug.mood_change);
        Assert.Equal((float)(2 * MINUTES), Dm.num(hug.timeout));
        r.In(() => Assert.Equal(1f, Dm.num(bob.mob_mood.mood)));

        // a good meal: the food moodlet scales with the quality (food_events.dm:50: ceil(1 + 1.5 * 3) = 6)
        r.In(() => Waiting.NoSleep(bob.add_mood_event("quality_food", typeof(datum_mood_event_food), (float)FOOD_QUALITY_GOOD, 1f)));
        datum_mood_event meal = Event(r, bob, "quality_food");
        Assert.Equal(6f, meal.mood_change);
        Assert.Equal("That food was good.", Dm.str(meal.description));
        r.In(() => Assert.Equal((float)MOOD_LEVEL_HAPPY2, bob.mob_mood.mood_level)); // 7: between MOOD_HAPPY2 and MOOD_HAPPY3

        // a good drink swallowed: the drink quality moodlet (food_reagents.dm:34)
        r.In(() =>
        {
            var cream = Dm.@new<datum_reagent_consumable_cream>();
            cream.quality = DRINK_GOOD;
            Waiting.NoSleep(cream.expose_mob(bob, INGEST, 5));
        });
        datum_mood_event drink = Event(r, bob, "quality_drink");
        Assert.IsType<datum_mood_event_quality_good>(drink);
        r.In(() => Assert.Equal(11f, Dm.num(bob.mob_mood.mood)));
        r.In(() => Assert.Equal((float)MOOD_LEVEL_HAPPY3, bob.mob_mood.mood_level));

        // Alice dies in Bob's view: the see-death moodlet, -8 for 5 minutes, naming her "someone" (no crew job)
        r.In(() => Waiting.NoSleep(alice.death()));
        r.Step(2);
        datum_mood_event death = Event(r, bob, "saw_death");
        Assert.IsType<datum_mood_event_conditional_see_death>(death);
        Assert.Equal(-8f, death.mood_change);
        Assert.Equal((float)(5 * MINUTES), Dm.num(death.timeout));
        Assert.Equal("I just saw someone die. How horrible...", Dm.str(death.description));
        r.In(() => Assert.Equal(3f, Dm.num(bob.mob_mood.mood)));

        // the hug wears off after its 2 minutes, the meal after 5, the drink after 7; the death moodlet with the meal
        r.Step(121 * Second);
        Assert.Null(Event(r, bob, "hug"));
        Assert.NotNull(Event(r, bob, "quality_food"));
        r.Step(180 * Second);
        Assert.Null(Event(r, bob, "quality_food"));
        Assert.Null(Event(r, bob, "saw_death"));
        Assert.NotNull(Event(r, bob, "quality_drink"));
        r.Step(120 * Second);
        Assert.Null(Event(r, bob, "quality_drink"));
        r.In(() => Assert.Equal(0f, Dm.num(bob.mob_mood.mood)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHungerMoodletsFollowNutrition()
    {
        var (r, _, bob) = Room(10203);
        void At(float nutrition, Type expected)
        {
            r.In(() => Waiting.NoSleep(bob.set_nutrition(nutrition)));
            r.Step(1);
            Assert.Equal(expected, Event(r, bob, "nutrition")?.GetType());
        }
        At(NUTRITION_LEVEL_FULL + 20, typeof(datum_mood_event_too_wellfed));
        At(NUTRITION_LEVEL_WELL_FED + 20, typeof(datum_mood_event_wellfed));
        At(NUTRITION_LEVEL_FED + 20, typeof(datum_mood_event_fed));
        At(NUTRITION_LEVEL_HUNGRY + 20, null);
        At(NUTRITION_LEVEL_VERY_HUNGRY + 20, typeof(datum_mood_event_hungry));
        At(NUTRITION_LEVEL_STARVING + 20, typeof(datum_mood_event_hungry_very));
        At(50, typeof(datum_mood_event_starving));
        // no hunger: no moodlet (mood.dm:118)
        r.In(() => ADD_TRAIT(bob, TRAIT_NOHUNGER, "test"));
        At(NUTRITION_LEVEL_FED + 20, null);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void SSmoodDrainsSanityInABadMoodAndInsanitySlowsAndUnsettles()
    {
        var (r, _, bob) = Room(10204);
        r.In(() => Assert.True(SSmood.wait == 1 * SECONDS && Dm.str(SSmood.name) == "Mood"));
        // a moodlet of -150 (the inverse happiness) is SAD4: sanity falls 0.3 a second (mood.dm:91)
        r.In(() => Waiting.NoSleep(bob.add_mood_event("test", typeof(datum_mood_event_sadness_inverse))));
        r.In(() => Assert.Equal((float)MOOD_LEVEL_SAD4, bob.mob_mood.mood_level));
        float before = r.In(() => bob.mob_mood.sanity);
        r.Step(10 * Second);
        float after = r.In(() => bob.mob_mood.sanity);
        Assert.InRange(before - after, 2.4f, 3.6f); // ten fires of 0.3, give or take one
        Assert.Equal((float)SANITY_LEVEL_DISTURBED, r.In(() => bob.mob_mood.sanity_level));
        r.In(() => Assert.Null(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_sanity_disturbed))));

        // unstable: the disturbed movespeed and the low sanity actionspeed (mood.dm:645, :656)
        r.In(() => Waiting.NoSleep(bob.mob_mood.set_sanity(SANITY_DISTURBED - 5, @override: TRUE)));
        r.In(() =>
        {
            Assert.Equal((float)SANITY_LEVEL_UNSTABLE, bob.mob_mood.sanity_level);
            Assert.IsType<datum_movespeed_modifier_sanity_disturbed>(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_sanity_disturbed)));
            Assert.NotNull(bob.has_actionspeed_modifier(typeof(datum_actionspeed_modifier_low_sanity)));
            Assert.Null(bob.has_status_effect(typeof(datum_status_effect_hallucination_sanity)));
            Assert.Equal("#dfa65b", Face(r, bob).color);
        });

        // insane: the insane movespeed, the crit threshold up by 10 and the sanity hallucinations (mood.dm:614)
        float crit = r.In(() => bob.crit_threshold);
        r.In(() => Waiting.NoSleep(bob.mob_mood.set_sanity(10, @override: TRUE)));
        r.In(() =>
        {
            Assert.Equal((float)SANITY_LEVEL_INSANE, bob.mob_mood.sanity_level);
            // the sanity modifiers share one id (MOVESPEED_ID_SANITY): the insane one replaces the disturbed one
            Assert.IsType<datum_movespeed_modifier_sanity_insane>(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_sanity_disturbed)));
            Assert.Equal(crit + 10, bob.crit_threshold);
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_hallucination_sanity)));
            Assert.Equal("#f15d36", Face(r, bob).color);
            Assert.Equal("mood_happiness_bad", Face(r, bob).icon_state); // the inverse happiness's special face (drug_events.dm)
        });

        // a full heal clears the temporary moods and restores the sanity (mood.dm:569): the effects go
        r.In(() => bob.mob_mood.clear_mood_event("test"));
        r.In(() => Waiting.NoSleep(bob.mob_mood.set_sanity(SANITY_GREAT + 10, maximum: SANITY_MAXIMUM, @override: TRUE)));
        r.In(() =>
        {
            Assert.Equal((float)SANITY_LEVEL_GREAT, bob.mob_mood.sanity_level);
            Assert.Null(bob.has_movespeed_modifier(typeof(datum_movespeed_modifier_sanity_insane)));
            Assert.NotNull(bob.has_actionspeed_modifier(typeof(datum_actionspeed_modifier_high_sanity)));
            Assert.Equal(crit, bob.crit_threshold);
            Assert.Null(bob.has_status_effect(typeof(datum_status_effect_hallucination_sanity)));
            Assert.Equal("#2eeb9a", Face(r, bob).color);
            Assert.Equal("mood5", Face(r, bob).icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMoodHudFaceFollowsTheMoodAndItsClickPrintsTheMood()
    {
        var (r, feed, bob) = Room(10205);
        atom_movable_screen_mood face = Face(r, bob);
        r.In(() => Waiting.NoSleep(bob.add_mood_event("test", typeof(datum_mood_event_hungry_very)))); // -6: SAD1 (-7 to -3)
        Assert.Equal("mood4", r.In(() => face.icon_state));
        r.In(() => Waiting.NoSleep(bob.add_mood_event("test", typeof(datum_mood_event_wellfed)))); // +8: HAPPY2 (6 to 10)
        Assert.Equal("mood7", r.In(() => face.icon_state));
        r.In(() => Waiting.NoSleep(bob.add_mood_event("test", typeof(datum_mood_event_sadness_inverse)))); // the inverse happiness: SAD4
        Assert.Equal("mood_happiness_bad", r.In(() => face.icon_state)); // its special face replaces the level's
        r.In(() => bob.clear_mood_event("test"));
        Assert.Equal("mood5", r.In(() => face.icon_state));

        // the click prints the mental status (mood.dm:384)
        r.In(() => Waiting.NoSleep(bob.add_mood_event("honk", typeof(datum_mood_event_hug))));
        Click(r, bob, face);
        Assert.True(feed.Any("My current mental status:"), Diag(feed, r));
        Assert.True(feed.Any("My hunger: "), Diag(feed, r));
        Assert.True(feed.Any("I could use a bite to eat."), Diag(feed, r));
        Assert.True(feed.Any("I have been feeling great lately!"), Diag(feed, r)); // sanity 100: tg's first matching range (mood.dm:440)
        Assert.True(feed.Any("I'm alright."), Diag(feed, r));
        Assert.True(feed.Any("Moodlets:"), Diag(feed, r));
        Assert.True(feed.Any("Hugs are nice."), Diag(feed, r));
        Assert.True(feed.Any("boxed_message"), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void PrintMoodNamesTheDrunknessTheSanityAndAnEmptyList()
    {
        var (r, feed, bob) = Room(10206);
        r.In(() => Waiting.NoSleep(bob.mob_mood.set_sanity(SANITY_INSANE + 5, @override: TRUE)));
        r.In(() => bob.mob_mood.print_mood(bob));
        r.Step(1);
        Assert.True(feed.Any("AHAHAHAHAHAHAHAHAHAH!!"), Diag(feed, r));
        Assert.True(feed.Any("I don't have much of a reaction to anything right now."), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryLiveMoodEventTypeIsCreatedAddedAndRemoved()
    {
        var (r, _, bob) = Room(10207);
        // the events whose tg callers always pass an argument they read (the hug's friend, the released fish)
        var friend = r.In(() => Dm.@new<mob_living_carbon_human>(locate(6, 6, 1)));
        var fish = r.In(() => Dm.@new<obj_item_fish>(locate(6, 6, 1)));
        var arguments = new Dictionary<Type, object[]>
        {
            [typeof(datum_mood_event_besthug)] = new object[] { friend },
            [typeof(datum_mood_event_betterhug)] = new object[] { friend },
            [typeof(datum_mood_event_fish_released)] = new object[] { FALSE, fish },
        };
        int count = 0;
        var failures = new List<string>();
        foreach (Type type in typeof(datum_mood_event).Assembly.GetTypes().Where(t => typeof(datum_mood_event).IsAssignableFrom(t)).OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            if (InertSeam.IsInertType(type) || type == typeof(datum_mood_event))
                continue;
            if (typeof(datum_mood_event_conditional).IsAssignableFrom(type))
                continue; // the conditional events are added through their base (the see-death test above)
            if (type == typeof(datum_mood_event_brain_damage))
                continue; // its description is a pick from tg's strings/traumas.json, which this base's test world lacks (main 2a7eeaa47 adds tg's strings files)
            count++;
            int runtimes = r.Room.Runtimes.Count;
            r.In(() =>
            {
                try
                {
                    object[] extra = arguments.TryGetValue(type, out object[] a) ? a : Array.Empty<object>();
                    Waiting.NoSleep(bob.add_mood_event("sweep", type, extra));
                    bob.clear_mood_event("sweep");
                }
                catch (Exception e)
                {
                    failures.Add(type.Name + ": " + e.Message);
                }
            });
            if (r.Room.Runtimes.Count > runtimes)
                failures.Add(type.Name + ": " + string.Join(" ", r.Room.RuntimeText.Skip(runtimes)));
        }
        r.In(() => Assert.Null(bob.mob_mood.mood_events["sweep"]));
        Assert.True(count > 350, "live mood event types: " + count);
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void EveryMoodEventTypeIsLive()
    {
        var inert = typeof(datum_mood_event).Assembly.GetTypes()
            .Where(t => typeof(datum_mood_event).IsAssignableFrom(t) && InertSeam.IsInertType(t))
            .Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(inert.Count == 0, string.Join(", ", inert));
        Assert.False(InertSeam.IsInertType(typeof(datum_mood)));
        Assert.False(InertSeam.IsInertType(typeof(datum_controller_subsystem_processing_mood)));
    }

    [Fact]
    public void TheDeathMoodletsFollowTgsPriorities()
    {
        // tg's unit test death_moodlets (death_moodlets.dm): normal, desensitized, cultist, the cultists' friendly fire, a pet
        (Action<mob_living_carbon_human, mob_living> prepare, Func<AudioRoom, mob_living> dying, Type expected)[] cases =
        {
            ((d, m) => { }, null, typeof(datum_mood_event_conditional_see_death)),
            ((d, m) => d.mind.desensitized_level = DESENSITIZED_THRESHOLD, null, typeof(datum_mood_event_conditional_see_death_desensitized)),
            ((d, m) => ADD_TRAIT(d, TRAIT_CULT_HALO, "test"), null, typeof(datum_mood_event_conditional_see_death_cult)),
            ((d, m) => { ADD_TRAIT(d, TRAIT_CULT_HALO, "test"); ADD_TRAIT(m, TRAIT_CULT_HALO, "test"); }, null, typeof(datum_mood_event_conditional_see_death)),
            ((d, m) => { }, rr => rr.In(() => Dm.@new<mob_living_basic_pet_cat>(locate(6, 4, 1))), typeof(datum_mood_event_conditional_see_death_pet)),
            ((d, m) => d.mind.desensitized_level = DESENSITIZED_THRESHOLD, rr => rr.In(() => Dm.@new<mob_living_basic_pet_cat>(locate(6, 4, 1))), typeof(datum_mood_event_conditional_see_death_pet)),
        };
        ulong seed = 10208;
        foreach (var (prepare, dying, expected) in cases)
        {
            var (r, _, bob) = Room(seed++);
            mob_living victim = dying?.Invoke(r) ?? r.Dummy(6, 4);
            r.Step(2);
            r.In(() => prepare(bob, victim));
            r.In(() => Waiting.NoSleep(victim.death()));
            r.Step(2);
            Assert.Equal(expected, Event(r, bob, "saw_death")?.GetType());
            r.AssertNoRuntimes();
        }
    }

    [Fact]
    public void ABeautifulRoomGivesAMoodletAndADirtyOneTakesItAway()
    {
        var (r, _, bob) = Room(10215);
        area room = r.In(() => (area)get_area(bob));
        r.In(() =>
        {
            room.totalbeauty = room.areasize * (BEAUTY_LEVEL_GREAT + 10);
            room.update_beauty();
        });
        Assert.IsType<datum_mood_event_greatroom>(Event(r, bob, "area_beauty"));
        r.In(() =>
        {
            room.totalbeauty = room.areasize * (BEAUTY_LEVEL_DECENT + 10);
            room.update_beauty();
        });
        Assert.IsType<datum_mood_event_decentroom>(Event(r, bob, "area_beauty"));
        // a plain room clears it; a filthy room only bothers a snob (mood.dm:540: tg's switch has no branch below BAD for others)
        r.In(() =>
        {
            room.totalbeauty = 0;
            room.update_beauty();
        });
        Assert.Null(Event(r, bob, "area_beauty"));
        r.In(() =>
        {
            room.totalbeauty = room.areasize * (BEAUTY_LEVEL_HORRID - 10);
            room.update_beauty();
        });
        Assert.Null(Event(r, bob, "area_beauty"));
        r.In(() =>
        {
            ADD_TRAIT(bob, TRAIT_SNOB, "test");
            room.update_beauty();
        });
        Assert.IsType<datum_mood_event_horridroom>(Event(r, bob, "area_beauty"));

        // a blood decal is the area's beauty element now (cleanable.dm:38)
        r.In(() =>
        {
            room.totalbeauty = 0;
            room.update_beauty();
        });
        float empty = r.In(() => room.totalbeauty);
        r.In(() => Dm.@new<obj_effect_decal_cleanable_dirt>(locate(6, 6, 1)));
        r.Step(2);
        r.In(() => Assert.True(room.totalbeauty < empty, "the dirt lowers the room's beauty"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADummyHumanHasAMoodWhoseSanityNeverMoves()
    {
        var (r, _, _) = Room(10216);
        r.In(() =>
        {
            var dummy = Dm.@new<mob_living_carbon_human_dummy>(locate(6, 6, 1));
            Assert.IsType<datum_mood_dummy>(dummy.mob_mood);
            Waiting.NoSleep(dummy.mob_mood.set_sanity(10, @override: TRUE));
            Assert.Equal((float)SANITY_NEUTRAL, dummy.mob_mood.sanity);
            Waiting.NoSleep(dummy.add_mood_event("test", typeof(datum_mood_event_hug)));
            Assert.Equal(1f, Dm.num(dummy.mob_mood.mood));
        });
        r.AssertNoRuntimes();
    }
    [Fact]
    public void TheGonbolasPeaceGivesTheGondolaMoodletUntilItEnds()
    {
        var (r, feed, bob) = Room(10217);
        datum_status_effect effect = null;
        r.In(() =>
        {
            effect = Waiting.NoSleep(bob.apply_status_effect(typeof(datum_status_effect_gonbola_pacify))) as datum_status_effect;
            Assert.NotNull(effect);
            Assert.True(HAS_TRAIT(bob, TRAIT_PACIFISM));
            Assert.True(HAS_TRAIT(bob, TRAIT_MUTE));
            Assert.IsType<datum_mood_event_gondola>(bob.mob_mood.mood_events[REF(effect)]);
        });
        r.Step(2);
        Assert.True(feed.Any("You suddenly feel at peace"), "the peace line");
        r.In(() =>
        {
            object key = REF(effect);
            qdel(effect);
            Assert.False(HAS_TRAIT(bob, TRAIT_PACIFISM));
            Assert.Null(bob.mob_mood.mood_events[key]);
        });
        r.AssertNoRuntimes();
    }
}
