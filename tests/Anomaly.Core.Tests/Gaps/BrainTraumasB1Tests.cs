using System.Reflection;
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
/// Gaps package B1 (D327): the brain traumas (code/datums/brain_damage/*) in a booted toy room with Bob as a player. Every trauma of the
/// folder is live; the brain's random roll gives a trauma of its group; the mild, severe, special and magic traumas do what tg's do (the
/// traits, languages, status effects, components, the speech and hearing changes, the images only their owner sees, the teleports); the
/// split personality and the imaginary friend take a ghost from the poll; the obsession makes its owner the obsessed antagonist; the
/// hypnosis makes the hypnotized victim; surgery cures what its resilience allows; the voice of god commands its listeners.
/// </summary>
public class BrainTraumasB1Tests
{
    private const int W = 17, H = 11;

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(W, H, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        // the traumas' lines (tg's strings/traumas.json: the dumbness, the godwoken, the voice of god's speak), which the test world's files
        // do not hold (as HeadTraumaTests)
        string tg = Anomaly.Core.Tests.Tg.M3Maps.P2.MapFixtures.TgClone();
        Assert.NotNull(tg);
        r.In(() =>
        {
            GLOB.string_cache ??= Dm.list();
            GLOB.string_cache[BRAIN_DAMAGE_FILE] = json_decode(System.IO.File.ReadAllText(System.IO.Path.Combine(tg, "strings", BRAIN_DAMAGE_FILE)));
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Gain<T>(AudioRoom r, mob_living_carbon who, object resilience = null, params object[] args) where T : datum_brain_trauma
        => r.In(() => Waiting.NoSleep(who.gain_trauma(typeof(T), resilience ?? TRAUMA_RESILIENCE_SURGERY, args)) as T);

    private static bool Has(AudioRoom r, object who, string trait) => r.In(() => HAS_TRAIT(who, trait));

    /// <summary>A player who joins on (x, y) and ghosts: their client is on the observer.</summary>
    private static mob_dead_observer Ghost(AudioRoom r, int x, int y, string key)
    {
        mob_living_carbon_human body = r.Join(x, y, key);
        client player = body.client;
        r.In(() => body.ghostize(TRUE));
        r.Step(2);
        return Assert.IsType<mob_dead_observer>(player.mob);
    }

    private static atom_movable_screen_alert_poll_alert AlertOf(AudioRoom r, mob who)
        => r.In(() => Dm.each(who.alerts).Select(k => who.alerts[k]).OfType<atom_movable_screen_alert_poll_alert>().SingleOrDefault());

    [Fact]
    public void EveryTraumaOfTheBrainDamageFolderIsLive()
    {
        var notLive = new List<string>();
        foreach (Type type in typeof(datum_brain_trauma).Assembly.GetTypes())
        {
            string path = type.GetCustomAttribute<DmPathAttribute>()?.Path;
            if (path == null || !path.StartsWith("/datum/brain_trauma", StringComparison.Ordinal))
                continue;
            // the religions' and the voidwalker's traumas come with their systems (none is gained at random)
            if (path.Contains("burdened") || path.Contains("honorbound") || path.Contains("voided"))
                continue;
            if (Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false))
                notLive.Add(path);
        }
        foreach (Type type in new[]
        {
            typeof(mob_living_split_personality), typeof(mob_living_split_personality_traitor), typeof(mob_living_split_personality_blackout),
            typeof(mob_eye_imaginary_friend), typeof(mob_eye_imaginary_friend_trapped), typeof(datum_emote_imaginary_friend),
            typeof(datum_emote_imaginary_friend_point), typeof(datum_emote_imaginary_friend_custom), typeof(datum_action_innate_imaginary_join),
            typeof(datum_action_innate_imaginary_hide), typeof(datum_action_personality_commune), typeof(obj_effect_client_image_holder_bluespace_stream),
            typeof(obj_effect_client_image_holder_securitron), typeof(obj_effect_client_image_holder_stalker_phantom),
            typeof(obj_effect_client_image_holder_stalker_phantom_cosmic), typeof(obj_effect_abstract_sync_holder), typeof(obj_effect_abstract_sync_holder_veil),
            typeof(obj_effect_temp_visual_bluespace_fissure), typeof(datum_antagonist_obsessed), typeof(datum_antagonist_hypnotized),
            typeof(datum_objective_assassinate_obsessed), typeof(datum_objective_assassinate_jealous), typeof(datum_objective_spendtime),
            typeof(datum_objective_hug), typeof(datum_objective_polaroid), typeof(datum_objective_steal_heirloom_thief), typeof(datum_outfit_obsessed),
            typeof(datum_status_effect_desensitized), typeof(datum_status_effect_discoordinated), typeof(datum_voice_of_god_command_walk),
            typeof(datum_voice_of_god_command_emote_flip), typeof(datum_voice_of_god_command_knockdown),
        })
        {
            if (Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false))
                notLive.Add(type.Name);
        }
        Assert.True(notLive.Count == 0, "inert: " + string.Join(", ", notLive));
    }

    [Fact]
    public void TheBrainsRandomRollGivesARandomTraumaOfItsGroup()
    {
        var (r, _, bob) = Room(7101);
        foreach (Type group in new[] { typeof(datum_brain_trauma_mild), typeof(datum_brain_trauma_severe), typeof(datum_brain_trauma_special) })
        {
            int gained = 0;
            for (int i = 0; i < 6; i++)
            {
                // a clientless human: the traumas that need a player (the split personality, the imaginary friend) refuse it and nothing is gained
                mob_living_carbon_human dummy = r.Dummy(10, 5);
                var trauma = r.In(() => Waiting.NoSleep(dummy.gain_trauma_type(group, TRAUMA_RESILIENCE_MAGIC)) as datum_brain_trauma);
                if (trauma == null)
                    continue;
                gained++;
                Assert.IsAssignableFrom(group, trauma);
                Assert.True(r.In(() => Dm.truthy(trauma.random_gain)), trauma.GetType().Name + " is not a random trauma");
                Assert.Equal(TRAUMA_RESILIENCE_MAGIC, r.In(() => Dm.num(trauma.resilience)));
                Assert.Same(trauma, r.In(() => dummy.has_trauma_type(group)));
                r.In(() => qdel(dummy));
            }
            Assert.True(gained > 0, "the roll never gave a " + group.Name);
        }
        r.Step(2);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMildTraumasChangeSpeechSightAndSenses()
    {
        var (r, feed, bob) = Room(7102);
        var dumb = Gain<datum_brain_trauma_mild_dumbness>(r, bob);
        Assert.True(feed.Any("You feel dumber."));
        Assert.True(Has(r, bob, TRAIT_DUMB));
        var impediment = Gain<datum_brain_trauma_mild_speech_impediment>(r, bob);
        Assert.True(Has(r, bob, TRAIT_UNINTELLIGIBLE_SPEECH));
        var hallucinations = Gain<datum_brain_trauma_mild_hallucinations>(r, bob);
        var stutter = Gain<datum_brain_trauma_mild_stuttering>(r, bob);
        var healthy = Gain<datum_brain_trauma_mild_healthy>(r, bob);
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_grouped_screwy_hud_fake_healthy))));
        var spasms = Gain<datum_brain_trauma_mild_muscle_spasms>(r, bob);
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_spasms))));
        var colorless = Gain<datum_brain_trauma_mild_color_blindness>(r, bob);
        Assert.True(feed.Any("The world around you seems to lose its color."));

        r.Step(3 * Second); // the carbon's Life ticks the traumas (carbon/life.dm:759)
        Assert.True(r.In(() => Dm.truthy(bob.has_language(typeof(datum_language_aphasia)))), "the hallucinations' aphasia language");
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_hallucination))));
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_speech_stutter))));
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_speech_stutter_derpspeech))));

        foreach (datum_brain_trauma t in new datum_brain_trauma[] { dumb, impediment, hallucinations, stutter, healthy, spasms, colorless })
            r.In(() => qdel(t));
        r.Step(2);
        Assert.False(Has(r, bob, TRAIT_DUMB));
        Assert.False(Has(r, bob, TRAIT_UNINTELLIGIBLE_SPEECH));
        Assert.Null(r.In(() => bob.has_status_effect(typeof(datum_status_effect_hallucination))));
        Assert.Null(r.In(() => bob.has_status_effect(typeof(datum_status_effect_spasms))));
        Assert.True(feed.Any("You feel smart again."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheExpressiveAphasiaHalvesTheHardWordsAndTheMindEchoRepeats()
    {
        var (r, _, bob) = Room(7103);
        var aphasia = Gain<datum_brain_trauma_mild_expressive_aphasia>(r, bob);
        DmList speech = r.In(() => Dm.list("the xylophone!"));
        r.In(() => aphasia.handle_speech(bob, speech));
        string said = r.In(() => Dm.str(speech[SPEECH_MESSAGE]));
        // "the" is a common word and stays; "xylophone" keeps a shuffled half of its letters (round(9 * 0.5)) and its suffix
        Assert.StartsWith("the ", said, StringComparison.Ordinal);
        Assert.EndsWith("!", said, StringComparison.Ordinal);
        Assert.Equal("the ".Length + 5 + 1, said.Length);
        Assert.Equal("lopxy", new string(said.Substring(4, 5).OrderBy(c => c).ToArray()));

        var echo = Gain<datum_brain_trauma_mild_mind_echo>(r, bob);
        for (int i = 0; i < 5; i++)
        {
            DmList line = r.In(() => Dm.list("line " + i));
            r.In(() => echo.handle_speech(bob, line));
        }
        Assert.Equal(5, r.In(() => (int)echo.speak_dejavu.len));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSevereTraumasMuteBlindAndParalyse()
    {
        var (r, feed, bob) = Room(7104);
        var mute = Gain<datum_brain_trauma_severe_mute>(r, bob);
        Assert.True(Has(r, bob, TRAIT_MUTE));
        var blind = Gain<datum_brain_trauma_severe_blindness>(r, bob);
        Assert.True(r.In(() => Dm.truthy(bob.is_blind())));
        var legs = Gain<datum_brain_trauma_severe_paralysis>(r, bob, TRAUMA_RESILIENCE_SURGERY, "legs");
        Assert.True(feed.Any("You can't feel your legs anymore!"));
        Assert.True(Has(r, bob, TRAIT_PARALYSIS_L_LEG) && Has(r, bob, TRAIT_PARALYSIS_R_LEG));
        Assert.False(Has(r, bob, TRAIT_PARALYSIS_L_ARM));
        var dyslexia = Gain<datum_brain_trauma_severe_dyslexia>(r, bob);
        Assert.True(Has(r, bob, TRAIT_ILLITERATE));
        var discoordination = Gain<datum_brain_trauma_severe_discoordination>(r, bob);
        Assert.True(Has(r, bob, TRAIT_DISCOORDINATED_TOOL_USER));
        var monophobia = Gain<datum_brain_trauma_severe_monophobia>(r, bob);
        Assert.NotNull(r.In(() => bob.GetComponent(typeof(datum_component_fearful))));
        var aphasia = Gain<datum_brain_trauma_severe_aphasia>(r, bob);
        Assert.True(r.In(() => Dm.truthy(bob.has_language(typeof(datum_language_aphasia)))));
        Assert.False(r.In(() => Dm.truthy(bob.can_speak_language(typeof(datum_language_common)))), "every other language is blocked");

        // surgery cures the surgery-level traumas (organ repair's brain surgery, cure_all_traumas(TRAUMA_RESILIENCE_SURGERY))
        var kept = Gain<datum_brain_trauma_severe_pacifism>(r, bob, TRAUMA_RESILIENCE_LOBOTOMY);
        float cured = r.In(() => Dm.num(bob.cure_all_traumas(TRAUMA_RESILIENCE_SURGERY)));
        r.Step(2);
        Assert.Equal(7f, cured);
        Assert.False(Has(r, bob, TRAIT_MUTE));
        Assert.False(r.In(() => Dm.truthy(bob.is_blind())));
        Assert.False(Has(r, bob, TRAIT_PARALYSIS_L_LEG));
        Assert.False(Has(r, bob, TRAIT_ILLITERATE));
        Assert.False(Has(r, bob, TRAIT_DISCOORDINATED_TOOL_USER));
        Assert.Null(r.In(() => bob.GetComponent(typeof(datum_component_fearful))));
        Assert.True(r.In(() => Dm.truthy(bob.can_speak_language(typeof(datum_language_common)))));
        Assert.True(Has(r, bob, TRAIT_PACIFISM), "the lobotomy-level trauma stays");
        Assert.Same(kept, r.In(() => bob.has_trauma_type(typeof(datum_brain_trauma_severe_pacifism))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void NarcolepsyFirstTiresThenPutsToSleepAndTheTriggerPhraseEntrances()
    {
        var (r, feed, bob) = Room(7105);
        var narcolepsy = Gain<datum_brain_trauma_severe_narcolepsy>(r, bob);
        // a long tick makes tg's per-second odds a certainty
        r.In(() => Waiting.NoSleep(narcolepsy.on_life(1000)));
        Assert.True(feed.Any("You feel tired..."));
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_drowsiness))));
        r.In(() => Waiting.NoSleep(narcolepsy.on_life(1000)));
        Assert.True(feed.Any("You fall asleep."));
        Assert.True(r.In(() => Dm.truthy(bob.IsSleeping())));
        r.In(() => qdel(narcolepsy));
        r.In(() => Waiting.NoSleep(bob.SetSleeping(0)));

        var trigger = Gain<datum_brain_trauma_severe_hypnotic_trigger>(r, bob, TRAUMA_RESILIENCE_SURGERY, "banana");
        mob_living_carbon_human carol = r.Dummy(6, 5);
        DmList hearing = r.In(() => Dm.list(carol, null, "I love a banana split", null, null, null, Dm.list(), Dm.list(), 7));
        r.In(() => trigger.handle_hearing(bob, hearing));
        Assert.DoesNotContain("banana", r.In(() => Dm.str(hearing[HEARING_RAW_MESSAGE])), StringComparison.Ordinal);
        Assert.Contains("*********", r.In(() => Dm.str(hearing[HEARING_RAW_MESSAGE])), StringComparison.Ordinal);
        r.Step(2 * Second);
        Assert.True(feed.Any("The words trigger something deep within you"));
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_trance))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSpecialTraumasGiveTheirGiftsAndCurses()
    {
        var (r, feed, bob) = Room(7106);
        var tenacity = Gain<datum_brain_trauma_special_tenacity>(r, bob);
        Assert.True(Has(r, bob, TRAIT_NOSOFTCRIT) && Has(r, bob, TRAIT_NOHARDCRIT) && Has(r, bob, TRAIT_ANALGESIA));
        var whispers = Gain<datum_brain_trauma_special_death_whispers>(r, bob);
        r.In(() => whispers.whispering());
        Assert.True(Has(r, bob, TRAIT_SIXTHSENSE));
        r.In(() => whispers.cease_whispering());
        Assert.False(Has(r, bob, TRAIT_SIXTHSENSE));
        var godwoken = Gain<datum_brain_trauma_special_godwoken>(r, bob);
        Assert.True(Has(r, bob, TRAIT_HOLY));
        var ptsd = Gain<datum_brain_trauma_special_ptsd>(r, bob, TRAUMA_RESILIENCE_ABSOLUTE);
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_desensitized))));
        Assert.Equal(DESENSITIZED_THRESHOLD, r.In(() => bob.mind.desensitized_level), 3);
        var primal = Gain<datum_brain_trauma_special_primal_instincts>(r, bob);
        Assert.IsType<datum_ai_controller_monkey>(r.In(() => bob.ai_controller));
        var axe = Gain<datum_brain_trauma_special_axedoration>(r, bob, TRAUMA_RESILIENCE_ABSOLUTE);
        Assert.True(feed.Any("You feel a great disturbance in the force."), "no bridge axe in the room");

        foreach (datum_brain_trauma t in new datum_brain_trauma[] { tenacity, godwoken, ptsd, primal, axe })
            r.In(() => qdel(t));
        r.Step(2);
        Assert.False(Has(r, bob, TRAIT_NOSOFTCRIT));
        Assert.False(Has(r, bob, TRAIT_HOLY));
        Assert.Null(r.In(() => bob.has_status_effect(typeof(datum_status_effect_desensitized))));
        Assert.Equal(1f, r.In(() => bob.mind.desensitized_level), 3);
        Assert.Null(r.In(() => bob.ai_controller));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheVoiceOfGodCommandsItsListenersAndNamesTheJobs()
    {
        var (r, feed, bob) = Room(7107);
        mob_living_carbon_human carol = r.Dummy(7, 5);
        mob_living_carbon_human dave = r.Dummy(8, 6);
        r.Step(Second);
        object cooldown = r.In(() => Waiting.NoSleep(voice_of_god("DROP!", bob, Dm.list("colossus", "yell"), 1, FALSE)));
        Assert.Equal(120f * SECONDS, Dm.num(cooldown));
        Assert.NotNull(r.In(() => carol.has_status_effect(typeof(datum_status_effect_incapacitating_knockdown))));
        Assert.NotNull(r.In(() => dave.has_status_effect(typeof(datum_status_effect_incapacitating_knockdown))));
        Assert.Null(r.In(() => bob.has_status_effect(typeof(datum_status_effect_incapacitating_knockdown))));
        Assert.True(feed.Any("DROP!"), "the shout");

        Assert.Equal("head of personnel", r.In(() => Dm.str(get_full_job_name("HoP"))));
        Assert.Equal("the quartermaster and the cook", r.In(() => Dm.str(get_full_job_name("the QM and the chef"))));
        // tg's expansions run in order over the whole text: the "ce" of "officer" becomes the chief engineer
        Assert.Equal("chief medical offichief station engineerr", r.In(() => Dm.str(get_full_job_name("cmo"))));

        // the godwoken speak through it, with tg's sound
        var godwoken = Gain<datum_brain_trauma_special_godwoken>(r, bob);
        r.In(() => Waiting.NoSleep(godwoken.speak("neutral")));
        Assert.NotEmpty(r.Sounds.Of(bob, "sound/effects/magic/clockwork/invoke_general.ogg"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheVisionsOnlyTheirOwnerSees()
    {
        var (r, _, bob) = Room(7108);
        var prophet = Gain<datum_brain_trauma_special_bluespace_prophet>(r, bob);
        r.In(() => Waiting.NoSleep(prophet.on_life(2)));
        var streams = r.In(() => Dm.each<obj_effect_client_image_holder_bluespace_stream>(Dm.world).ToList());
        Assert.Equal(2, streams.Count);
        Assert.Same(streams[1], r.In(() => streams[0].linked_to));
        Assert.True(r.In(() => Dm.@in(streams[0].shown_image, bob.client.images)), "Bob sees the stream");

        var beepsky = Gain<datum_brain_trauma_special_beepsky>(r, bob);
        Assert.NotNull(r.In(() => beepsky.beepsky));
        Assert.True(r.In(() => Dm.@in(beepsky.beepsky.shown_image, bob.client.images)), "Bob sees the securitron");

        var stalker = Gain<datum_brain_trauma_magic_stalker>(r, bob);
        var phantom = r.In(() => stalker.stalker);
        Assert.NotNull(phantom);
        Assert.True(r.In(() => Dm.@in(phantom.shown_image, bob.client.images)), "Bob sees the phantom");
        r.In(() => qdel(stalker));
        r.In(() => qdel(beepsky));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(phantom)));
        Assert.False(r.In(() => Dm.each<obj_effect_client_image_holder_securitron>(Dm.world).Any(s => !QDELETED(s))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheQuantumAlignmentSnapsBackAndTheCrisisFadesOut()
    {
        var (r, feed, bob) = Room(7109);
        var quantum = Gain<datum_brain_trauma_special_quantum_alignment>(r, bob);
        turf there = r.In(() => locate(12, 7, 1));
        r.In(() => quantum.entangle(there));
        Assert.True(feed.Any("You start feeling a strong sense of connection to"));
        r.In(() => quantum.start_snapback());
        Assert.NotEmpty(r.Sounds.Of(bob, "sound/effects/magic/lightning_chargeup.ogg"));
        r.Step(11 * Second);
        Assert.True(feed.Any("You're pulled through spacetime!"));
        Assert.Same(there, r.In(() => get_turf(bob)));
        r.In(() => qdel(quantum));

        var crisis = Gain<datum_brain_trauma_special_existential_crisis>(r, bob);
        r.In(() => Waiting.NoSleep(crisis.fade_out()));
        Assert.IsType<obj_effect_abstract_sync_holder_veil>(r.In(() => bob.loc));
        r.In(() => crisis.fade_in());
        r.Step(2);
        Assert.Same(there, r.In(() => bob.loc));
        Assert.True(feed.Any("You fade back into reality."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheImaginaryFriendIsAGhostOnlyItsOwnerSeesAndHears()
    {
        var (r, feed, bob) = Room(7110);
        mob_dead_observer gina = Ghost(r, 10, 8, "Gina");
        // the trauma's poll waits for its 20 seconds (gaining it sleeps, so it runs detached); Gina signs up through her alert
        r.In(() => Waiting.Detach(bob.gain_trauma(typeof(datum_brain_trauma_special_imaginary_friend), TRAUMA_RESILIENCE_SURGERY)));
        r.Step(2);
        var friendTrauma = r.In(() => bob.has_trauma_type(typeof(datum_brain_trauma_special_imaginary_friend)) as datum_brain_trauma_special_imaginary_friend);
        Assert.NotNull(friendTrauma);
        var alert = AlertOf(r, gina);
        Assert.NotNull(alert);
        Click(r, gina, alert);
        r.Step(21 * Second);
        mob_eye_imaginary_friend friend = r.In(() => friendTrauma.friend);
        Assert.True(friend != null, "no friend: " + string.Join("\n", r.Room.RuntimeText) + " | " + string.Join(" | ", feed.Chat.Select(c => c.Html).TakeLast(8)));
        Assert.Equal("gina", r.In(() => friend.ckey));
        Assert.True(r.In(() => Dm.@in(friend, bob.imaginary_group)));
        Assert.True(feed.Any("You are the imaginary friend of"));
        r.Step(2 * Second); // setup_friend is detached: the dressed dummy's flat picture
        Assert.True(r.In(() => friend.current_image) != null, "no picture: " + string.Join("\n", r.Room.RuntimeText) + " | client " + r.In(() => friend.client != null) + " owner " + r.In(() => friend.owner != null) + " icon " + r.In(() => friend.human_icon != null));
        Assert.True(r.In(() => Dm.@in(friend.current_image, bob.client.images)), "Bob sees his friend");
        Assert.True(r.In(() => Dm.truthy(friend.current_image.@override)));

        // the friend talks to Bob alone
        r.In(() => Waiting.NoSleep(friend.say("Psst, over here")));
        r.Step(2);
        Assert.True(feed.Chat.Any(c => (c.Target == bob || c.Target == r.In(() => bob.client)) && c.Html.Contains("Psst, over here")), "Bob hears his friend");
        // hiding takes the picture from Bob's sight
        var hide = r.In(() => Dm.locate<datum_action_innate_imaginary_hide>(friend.actions));
        r.In(() => Waiting.NoSleep(hide.Activate()));
        Assert.True(r.In(() => Dm.truthy(friend.hidden)));
        Assert.False(r.In(() => Dm.@in(friend.current_image, bob.client.images)), "the hidden friend");

        // the friend's player leaves first: tg deletes the friend with the trauma, and deleting a mob that still has its client is tg's
        // own stack trace ("Mob with client has been deleted.", mob.dm:28)
        r.In(() => friend.ghostize(FALSE));
        r.Step(2);
        r.In(() => qdel(friendTrauma));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(friend)));
        Assert.True(feed.Any("You feel lonely again."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSplitPersonalityTakesAGhostAndSwapsTheBody()
    {
        var (r, feed, bob) = Room(7111);
        mob_dead_observer gina = Ghost(r, 10, 8, "Gina");
        var split = r.In(() => (datum_brain_trauma_severe_split_personality)null);
        r.In(() => Waiting.Detach(bob.gain_trauma(typeof(datum_brain_trauma_severe_split_personality), TRAUMA_RESILIENCE_SURGERY)));
        r.Step(2);
        Click(r, gina, AlertOf(r, gina));
        r.Step(21 * Second);
        split = r.In(() => bob.has_trauma_type(typeof(datum_brain_trauma_severe_split_personality)) as datum_brain_trauma_severe_split_personality);
        Assert.NotNull(split);
        Assert.Equal("gina", r.In(() => split.stranger_backseat.ckey));
        Assert.Equal("bob", r.In(() => bob.ckey));
        Assert.Same(bob, r.In(() => split.stranger_backseat.loc));

        r.In(() => split.switch_personalities());
        r.Step(2);
        Assert.Equal("gina", r.In(() => bob.ckey));
        Assert.Equal("bob", r.In(() => split.owner_backseat.ckey));
        Assert.True(feed.Any("You manage to take control of your body!"));

        // curing gives the body back to its owner
        r.In(() => qdel(split));
        r.Step(2);
        Assert.Equal("bob", r.In(() => bob.ckey));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheObsessionMakesTheObsessedAntagonist()
    {
        var (r, feed, bob) = Room(7112);
        mob_living_carbon_human carol = r.Join(8, 5, "Carol");
        r.In(() =>
        {
            bob.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_assistant)) as datum_job);
            carol.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_cook)) as datum_job);
            // the crew are the manifest's locked records (get_crewmember_minds, _globalvars/lists/mobs.dm:143)
            GLOB.manifest.inject(bob);
            GLOB.manifest.inject(carol);
        });
        var obsessed = Gain<datum_brain_trauma_special_obsessed>(r, bob, TRAUMA_RESILIENCE_LOBOTOMY);
        Assert.NotNull(obsessed);
        Assert.Same(carol, r.In(() => obsessed.obsession));
        var antag = r.In(() => bob.mind.has_antag_datum(typeof(datum_antagonist_obsessed)) as datum_antagonist_obsessed);
        Assert.NotNull(antag);
        Assert.Same(obsessed, r.In(() => antag.trauma));
        Assert.Equal(4, r.In(() => (int)antag.objectives.len));
        Assert.IsType<datum_objective_assassinate_obsessed>(r.In(() => antag.objectives[(float)antag.objectives.len]));
        Assert.True(feed.Any("You hear a sickening, raspy voice in your head."));
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_speech_stutter_obsession))));

        r.Step(3 * Second);
        Assert.True(r.In(() => obsessed.total_time_creeping) > 0, "Bob is near Carol");

        r.In(() => qdel(obsessed));
        r.Step(2);
        Assert.Null(r.In(() => bob.mind.has_antag_datum(typeof(datum_antagonist_obsessed))));
        Assert.NotNull(r.In(() => bob.mind.has_antag_datum(typeof(datum_antagonist_former_obsessed))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHypnosisMakesTheHypnotizedVictim()
    {
        var (r, feed, bob) = Room(7113);
        var hypnosis = Gain<datum_brain_trauma_hypnosis>(r, bob, TRAUMA_RESILIENCE_SURGERY, "Obey the clown");
        var antag = r.In(() => bob.mind.has_antag_datum(typeof(datum_antagonist_hypnotized)) as datum_antagonist_hypnotized);
        Assert.NotNull(antag);
        Assert.Same(hypnosis, r.In(() => antag.trauma));
        Assert.Equal("Obey the clown", r.In(() => (antag.objectives[1f] as datum_objective).explanation_text));
        r.In(() => qdel(hypnosis));
        r.Step(2);
        Assert.Null(r.In(() => bob.mind.has_antag_datum(typeof(datum_antagonist_hypnotized))));
        Assert.True(feed.Any("You suddenly snap out of your hypnosis."));
        r.AssertNoRuntimes();
    }
}
