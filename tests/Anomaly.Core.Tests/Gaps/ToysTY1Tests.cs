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
/// Gaps package TY1 (D333): the toys, in a booted toy room with Bob as a player: the toy sword (flicked out in its colour, turned to
/// another colour by a screwdriver, made rainbow by a multitool, joined with a second into the double-bladed toy), the talking owl
/// (its line and its chatter voice), the toy AI and the action figures, the snap pop (stepped on, its ash; the phoenix one comes
/// back), the water balloon (filled at a water tank, burst by a knife), the cap gun (fired, its click when empty, reloaded), the
/// windup toolbox, the toy Codex Cicatrix (its opening and closing flicks), the toy nuke, the xeno figure, the small toys' sounds,
/// the ventriloquist dummy's name, the steampunk watch, the Intento game, a toy mech battle, the latex glove balloon and the
/// hourglass; every type of the package spawns live.
/// </summary>
public class ToysTY1Tests
{
    /// <summary>Every type the package translated (toys.dm, toy_mechs.dm, latexballoon.dm, hourglass.dm), the abstract ones out.</summary>
    internal static readonly Type[] AllTypes =
    {
        typeof(obj_item_toy_mecha), typeof(obj_item_toy_mecha_ripley), typeof(obj_item_toy_mecha_ripleymkii), typeof(obj_item_toy_mecha_hauler),
        typeof(obj_item_toy_mecha_clarke), typeof(obj_item_toy_mecha_odysseus), typeof(obj_item_toy_mecha_gygax), typeof(obj_item_toy_mecha_durand),
        typeof(obj_item_toy_mecha_savannahivanov), typeof(obj_item_toy_mecha_phazon), typeof(obj_item_toy_mecha_honk), typeof(obj_item_toy_mecha_darkgygax),
        typeof(obj_item_toy_mecha_mauler), typeof(obj_item_toy_mecha_darkhonk), typeof(obj_item_toy_mecha_deathripley), typeof(obj_item_toy_mecha_reticence),
        typeof(obj_item_toy_mecha_marauder), typeof(obj_item_toy_mecha_seraph), typeof(obj_item_toy_mecha_firefighter), typeof(obj_item_toy_waterballoon),
        typeof(obj_item_toy_balloon), typeof(obj_item_toy_balloon_long), typeof(obj_item_toy_balloon_corgi), typeof(obj_item_toy_balloon_heart),
        typeof(obj_item_toy_balloon_syndicate), typeof(obj_item_toy_balloon_arrest), typeof(obj_item_toy_balloon_animal_guy), typeof(obj_item_toy_balloon_animal_nukie),
        typeof(obj_item_toy_balloon_animal_clown), typeof(obj_item_toy_balloon_animal_cat), typeof(obj_item_toy_balloon_animal_fly), typeof(obj_item_toy_balloon_animal_podguy),
        typeof(obj_item_toy_balloon_animal_ai), typeof(obj_item_toy_balloon_animal_dog), typeof(obj_item_toy_balloon_animal_xeno), typeof(obj_item_toy_balloon_animal_banana),
        typeof(obj_item_toy_balloon_animal_lizard), typeof(obj_item_toy_balloon_animal_slime), typeof(obj_item_toy_balloon_animal_moth), typeof(obj_item_toy_balloon_animal_ethereal),
        typeof(obj_item_toy_balloon_animal_plasmaman), typeof(obj_item_toy_captainsaid), typeof(obj_item_toy_captainsaid_collector), typeof(obj_item_toy_spinningtoy),
        typeof(obj_item_toy_spinningtoy_dark_matter), typeof(obj_item_toy_gun), typeof(obj_item_toy_ammo_gun), typeof(obj_item_toy_sword),
        typeof(obj_item_toy_foamblade), typeof(obj_item_toy_windup_toolbox), typeof(obj_item_toy_katana), typeof(obj_item_toy_snappop),
        typeof(obj_item_toy_snappop_phoenix), typeof(obj_item_toy_talking), typeof(obj_item_toy_talking_ai), typeof(obj_item_toy_talking_codex_gigas),
        typeof(obj_item_toy_talking_owl), typeof(obj_item_toy_talking_griffin), typeof(obj_item_toy_nuke), typeof(obj_item_toy_minimeteor),
        typeof(obj_item_toy_redbutton), typeof(obj_item_toy_snowball), typeof(obj_item_toy_beach_ball), typeof(obj_item_toy_beach_ball_branded),
        typeof(obj_item_toy_beach_ball_baseball), typeof(obj_item_toy_clockwork_watch), typeof(obj_item_toy_toy_dagger), typeof(obj_item_toy_toy_xeno),
        typeof(obj_item_toy_cattoy), typeof(obj_item_toy_figure), typeof(obj_item_toy_figure_cmo), typeof(obj_item_toy_figure_assistant),
        typeof(obj_item_toy_figure_atmos), typeof(obj_item_toy_figure_bartender), typeof(obj_item_toy_figure_bitrunner), typeof(obj_item_toy_figure_borg),
        typeof(obj_item_toy_figure_botanist), typeof(obj_item_toy_figure_captain), typeof(obj_item_toy_figure_cargotech), typeof(obj_item_toy_figure_ce),
        typeof(obj_item_toy_figure_chaplain), typeof(obj_item_toy_figure_chef), typeof(obj_item_toy_figure_chemist), typeof(obj_item_toy_figure_clown),
        typeof(obj_item_toy_figure_ian), typeof(obj_item_toy_figure_detective), typeof(obj_item_toy_figure_dsquad), typeof(obj_item_toy_figure_engineer),
        typeof(obj_item_toy_figure_geneticist), typeof(obj_item_toy_figure_hop), typeof(obj_item_toy_figure_hos), typeof(obj_item_toy_figure_qm),
        typeof(obj_item_toy_figure_janitor), typeof(obj_item_toy_figure_lawyer), typeof(obj_item_toy_figure_curator), typeof(obj_item_toy_figure_md),
        typeof(obj_item_toy_figure_coroner), typeof(obj_item_toy_figure_paramedic), typeof(obj_item_toy_figure_psychologist), typeof(obj_item_toy_figure_prisoner),
        typeof(obj_item_toy_figure_mime), typeof(obj_item_toy_figure_miner), typeof(obj_item_toy_figure_ninja), typeof(obj_item_toy_figure_wizard),
        typeof(obj_item_toy_figure_wizard_special), typeof(obj_item_toy_figure_rd), typeof(obj_item_toy_figure_roboticist), typeof(obj_item_toy_figure_scientist),
        typeof(obj_item_toy_figure_syndie), typeof(obj_item_toy_figure_secofficer), typeof(obj_item_toy_figure_warden), typeof(obj_item_toy_dummy),
        typeof(obj_item_toy_seashell), typeof(obj_item_toy_brokenradio), typeof(obj_item_toy_braintoy), typeof(obj_item_toy_eldritch_book),
        typeof(obj_item_toy_reality_pierce), typeof(obj_item_toy_foamfinger), typeof(obj_item_toy_intento), typeof(obj_item_toy_foam_runic_scepter),
        typeof(obj_item_hourglass), typeof(obj_item_hourglass_admin), typeof(obj_item_latexballoon), typeof(obj_item_dualsaber_toy),
        typeof(obj_item_extendohand), typeof(obj_item_extendohand_acme), typeof(obj_item_banhammer), typeof(obj_effect_decal_cleanable_ash_snappop_phoenix),
    };

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

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target, string modifiers = "")
    {
        r.Step(Second);
        Click(r, who, target, modifiers);
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static string Examine(AudioRoom r, atom a, mob who) => r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(a.examine(who))).Select(o => Dm.str(o))));

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    private static int HeardCount(AudioRoom r, string file) => r.Sounds.Heard.Count(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    private static string Said(Feed feed) => string.Join(" | ", feed.Chat.Select(c => c.Html));

    private static turf At(AudioRoom r, int x, int y) => r.In(() => (turf)locate(x, y, 1));

    /// <summary>The flicks and the animated icon states (BYOND animate's icon_state steps) the toys show.</summary>
    private sealed class Recorder : IAppearanceSink
    {
        public List<(object Target, string State)> Flicks { get; } = new();

        public List<string> AnimatedStates { get; } = new();

        public void Changed(object atom, AppearanceAspect aspect) { }

        public void Animate(object target, DmList arguments)
        {
            if (arguments?["icon_state"] is string state)
                AnimatedStates.Add(state);
        }

        public void Flick(object target, object state) => Flicks.Add((target, Dm.str(state)));

        public void LightChanged(object atom) { }

        public void VisibilityChanged(object atom) { }
    }

    private static Recorder Record(AudioRoom r)
    {
        var recorder = new Recorder();
        PresentationSinks.Of(r.World).Appearance = recorder;
        return recorder;
    }

    [Fact]
    public void EveryTypeSpawnsLiveIsExaminedAndGoes()
    {
        var (r, _, bob) = Room(9901);
        var failures = new List<string>();
        foreach (Type type in AllTypes)
        {
            if (InertSeam.IsInertType(type))
                failures.Add(type.Name + " is inert");
            atom thing = r.In(() => (atom)Dm.@new(type, locate(5, 4, 1)));
            r.Step(2);
            r.In(() => Waiting.NoSleep(thing.examine(bob)));
            r.In(() => { if (!QDELETED(thing)) qdel(thing); });
            r.Step(2);
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        r.Step(2 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheToySwordFlicksOutChangesColourGoesRainbowAndJoinsASecondIntoTheDoubleBladedToy()
    {
        var (r, feed, bob) = Room(9902);
        var sword = Hold<obj_item_toy_sword>(r, bob);
        r.In(() => Assert.Equal("e_sword", sword.icon_state));

        Use(r, bob, sword); // in hand: attack_self, the transforming component
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(sword, TRAIT_TRANSFORM_ACTIVE));
            Assert.Equal("e_sword_on_blue", sword.icon_state);
            Assert.Equal("e_sword_on_blue", sword.inhand_icon_state);
        });
        Assert.True(feed.Any("balloon: flicked out"), Said(feed));
        Assert.True(Heard(r, "weapons/saberon"), r.Sounds.Dump());

        Use(r, bob, sword);
        r.In(() => Assert.Equal("e_sword", sword.icon_state));
        Assert.True(feed.Any("balloon: pushed in"), Said(feed));
        Assert.True(Heard(r, "weapons/saberoff"), r.Sounds.Dump());

        // on the floor: a screwdriver turns its lighting dial, a multitool makes it fabulous
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(sword)));
        Hold<obj_item_screwdriver>(r, bob);
        Use(r, bob, sword);
        r.In(() => Assert.Equal("green", sword.saber_color));
        Assert.True(feed.Any("balloon: changed to green"), Said(feed));
        Assert.Contains("set to green", Examine(r, sword, bob), StringComparison.Ordinal);
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(bob.get_active_held_item() as obj_item)));
        Hold<obj_item_multitool>(r, bob);
        Use(r, bob, sword);
        r.In(() =>
        {
            Assert.Equal("rainbow", sword.saber_color);
            Assert.True(Dm.truthy(sword.hacked));
        });
        Assert.True(feed.Any("RNBW_ENGAGE"), Said(feed));
        Use(r, bob, sword);
        Assert.True(feed.Any("already fabulous"), Said(feed));

        // a second toy sword used on it: the double-bladed toy, rainbow as the hacked one was
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(bob.get_active_held_item() as obj_item)));
        var second = Hold<obj_item_toy_sword>(r, bob);
        Use(r, bob, sword);
        r.In(() =>
        {
            Assert.True(QDELETED(sword));
            Assert.True(QDELETED(second));
            var joined = Assert.IsType<obj_item_dualsaber_toy>(bob.get_active_held_item());
            Assert.Equal("rainbow", joined.saber_color);
        });
        Assert.True(feed.Any("You're fake-cool"), Said(feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTalkingOwlSaysALineInItsChatterVoiceAndRecharges()
    {
        var (r, feed, bob) = Room(9903);
        var owl = Hold<obj_item_toy_talking_owl>(r, bob);
        List<string> lines = r.In(() => Dm.each(owl.messages).Select(m => Dm.str(m)).ToList());

        Use(r, bob, owl);
        Assert.True(feed.Any("You pull the string"), Said(feed));
        Assert.True(Heard(r, "machines/click"), r.Sounds.Dump());
        r.Step(Second);
        // (up to an apostrophe: the speech is html-encoded, "won&#39;t")
        Assert.True(lines.Any(l => feed.Any(l.Split('\'')[0])), "the owl said none of its lines: " + Said(feed));
        r.Step(3 * Second);
        // its chatter: a short owl sound a word, at the pace of the letters (at least two words a line)
        Assert.True(HeardCount(r, "chatter/owl_") >= 2, r.Sounds.Dump());
        Assert.False(Heard(r, "chatter/griffin_"), r.Sounds.Dump());

        // on its cooldown the string does nothing; recharged, it talks again
        r.In(() => Assert.Equal(0f, owl.cooldown));
        int pulled = feed.Chat.Count(c => c.Html.Contains("You pull the string", StringComparison.Ordinal));
        Click(r, bob, owl);
        r.In(() => Assert.Equal(1f, owl.cooldown));
        Click(r, bob, owl);
        Assert.Equal(pulled + 1, feed.Chat.Count(c => c.Html.Contains("You pull the string", StringComparison.Ordinal)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheToyAiAnnouncesALawAndTheFiguresSayTheirLines()
    {
        var (r, feed, bob) = Room(9904);
        var ai = Hold<obj_item_toy_talking_ai>(r, bob);
        int before = feed.Chat.Count;
        Use(r, bob, ai);
        r.Step(2 * Second);
        Assert.True(feed.Chat.Skip(before).Any(c => c.Html.Contains("toy AI", StringComparison.Ordinal) && !c.Html.Contains("pull", StringComparison.Ordinal)),
            "the toy AI said no law: " + Said(feed));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(ai)));

        var captain = Hold<obj_item_toy_figure_captain>(r, bob);
        string toysay = r.In(() => captain.toysay);
        r.In(() => Assert.Contains("\"Space Life\" brand", captain.desc, StringComparison.Ordinal));
        Use(r, bob, captain);
        Assert.True(feed.Any(toysay), Said(feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASnapPopSteppedOnSnapsIntoAshAndThePhoenixOneComesBack()
    {
        var (r, feed, _) = Room(9905);
        var pop = Place<obj_item_toy_snappop>(r, 6, 4);
        turf there = At(r, 6, 4);
        mob_living_carbon_human dummy = r.Dummy(7, 4);
        r.In(() => Assert.Equal(MOVE_INTENT_RUN, dummy.move_intent));
        r.In(() => Waiting.NoSleep(dummy.Move(there, WEST)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(there, dummy.loc);
            Assert.True(QDELETED(pop));
            Assert.Single(Dm.each<obj_effect_decal_cleanable_ash>(there.contents));
        });
        Assert.True(feed.Any("snap pop explodes!"), Said(feed));
        Assert.True(Heard(r, "effects/snap"), r.Sounds.Dump());

        // the phoenix snap pop leaves its own ash, which grows a new one in 30 seconds
        var phoenix = Place<obj_item_toy_snappop_phoenix>(r, 8, 4);
        turf nest = At(r, 8, 4);
        r.In(() => Waiting.NoSleep(phoenix.fire_act(1000, 100)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(QDELETED(phoenix));
            Assert.Single(Dm.each<obj_effect_decal_cleanable_ash_snappop_phoenix>(nest.contents));
        });
        r.Step(31 * Second);
        r.In(() =>
        {
            Assert.Single(Dm.each<obj_item_toy_snappop_phoenix>(nest.contents));
            Assert.Empty(Dm.each<obj_effect_decal_cleanable_ash_snappop_phoenix>(nest.contents));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWaterBalloonFillsAtAWaterTankAndAKnifeBurstsIt()
    {
        var (r, feed, bob) = Room(9906);
        var tank = Place<obj_structure_reagent_dispensers_watertank>(r, 5, 4);
        var balloon = Hold<obj_item_toy_waterballoon>(r, bob);
        r.In(() => Assert.Equal("balloon_red-e", balloon.icon_state));
        Use(r, bob, tank);
        r.In(() =>
        {
            Assert.Equal(10f, ((datum_reagents)balloon.reagents).total_volume);
            Assert.Equal("waterballoon", balloon.icon_state);
            Assert.Equal("balloon", balloon.inhand_icon_state);
            Assert.Contains("sloshing", balloon.desc, StringComparison.Ordinal);
        });
        Assert.True(feed.Any("You fill the balloon"), Said(feed));

        r.In(() => Waiting.NoSleep(bob.dropItemToGround(balloon)));
        Hold<obj_item_knife_kitchen>(r, bob);
        Use(r, bob, balloon);
        r.In(() => Assert.True(QDELETED(balloon)));
        Assert.True(feed.Any("bursts!"), Said(feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCapGunFiresClicksWhenEmptyAndReloads()
    {
        var (r, feed, bob) = Room(9907);
        var gun = Hold<obj_item_toy_gun>(r, bob);
        Use(r, bob, At(r, 9, 4));
        r.In(() => Assert.Equal(6f, gun.bullets));
        Assert.True(feed.Any("You fire the cap gun"), Said(feed));
        Assert.True(Heard(r, "revolver/shot"), r.Sounds.Dump());
        Assert.Contains("There are 6 caps left.", Examine(r, gun, bob), StringComparison.Ordinal);

        r.In(() => gun.bullets = 0);
        Use(r, bob, At(r, 9, 4));
        Assert.True(feed.Any("*click*"), Said(feed));
        Assert.True(Heard(r, "revolver/dry_fire"), r.Sounds.Dump());

        // the caps: the gun on the floor, the box used on it
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(gun)));
        var caps = Hold<obj_item_toy_ammo_gun>(r, bob);
        Use(r, bob, gun);
        r.In(() =>
        {
            Assert.Equal(7f, gun.bullets);
            Assert.Equal(0f, caps.amount_left);
            Assert.Equal("357OLD-0", caps.icon_state);
        });
        Assert.True(feed.Any("You reload 7 caps."), Said(feed));
        Assert.Contains("There are 0 caps left.", Examine(r, caps, bob), StringComparison.Ordinal);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWindupToolboxRumblesOpenForAMinuteAndSnapsShut()
    {
        var (r, feed, bob) = Room(9908);
        var toolbox = Hold<obj_item_toy_windup_toolbox>(r, bob);
        Assert.Contains("single_latch", Overlays(r, toolbox));
        Use(r, bob, toolbox);
        r.In(() => Assert.True(Dm.truthy(toolbox.active)));
        Assert.Contains("single_latch_open", Overlays(r, toolbox));
        Assert.True(Heard(r, "effects/pope_entry"), r.Sounds.Dump());
        Use(r, bob, toolbox);
        Assert.True(feed.Any("is already active!"), Said(feed));

        r.Step(61 * Second);
        r.In(() => Assert.False(Dm.truthy(toolbox.active)));
        Assert.Contains("single_latch", Overlays(r, toolbox));
        Assert.DoesNotContain("single_latch_open", Overlays(r, toolbox));
        Assert.True(feed.Any("falls still"), Said(feed));
        Assert.True(Heard(r, "weapons/batonextend"), r.Sounds.Dump());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheToyCodexOpensWithItsFlickAndClosesByItself()
    {
        var (r, _, bob) = Room(9909);
        Recorder recorder = Record(r);
        var book = Hold<obj_item_toy_eldritch_book>(r, bob);
        Use(r, bob, book);
        r.In(() =>
        {
            Assert.Equal("book_open", book.icon_state);
            Assert.True(Dm.truthy(book.book_open));
        });
        Assert.Contains(recorder.Flicks, f => ReferenceEquals(f.Target, book) && f.State == "book_opening");
        r.Step(6 * Second);
        r.In(() =>
        {
            Assert.Equal("book", book.icon_state);
            Assert.False(Dm.truthy(book.book_open));
        });
        Assert.Contains(recorder.Flicks, f => ReferenceEquals(f.Target, book) && f.State == "book_closing");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheToyNukeCountsDownCoolsAndIdles()
    {
        var (r, feed, bob) = Room(9910);
        var nuke = Hold<obj_item_toy_nuke>(r, bob);
        r.In(() => Assert.Equal("nuketoyidle", nuke.icon_state));
        Use(r, bob, nuke);
        r.Step(Second);
        r.In(() => Assert.Equal("nuketoy", nuke.icon_state));
        Assert.True(Heard(r, "alarm/nuke_alarm"), r.Sounds.Dump());
        Assert.True(feed.Any("it plays a loud noise!"), Said(feed));
        r.Step(14 * Second);
        r.In(() => Assert.Equal("nuketoycool", nuke.icon_state));
        Click(r, bob, nuke);
        Assert.True(feed.Any("Nothing happens"), Said(feed));
        r.Step(50 * Second);
        r.In(() => Assert.Equal("nuketoyidle", nuke.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSmallToysPlayTheirSounds()
    {
        var (r, feed, bob) = Room(9911);

        var xeno = Hold<obj_item_toy_toy_xeno>(r, bob);
        Use(r, bob, xeno);
        r.In(() => Assert.Equal("toy_xeno_used", xeno.icon_state));
        r.Step(Second);
        Assert.True(feed.Any("Hiss!"), Said(feed));
        Assert.True(Heard(r, "hiss/hiss"), r.Sounds.Dump());
        r.Step(5 * Second);
        r.In(() => Assert.Equal("toy_xeno", xeno.icon_state));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(xeno)));

        var aid = Hold<obj_item_toy_captainsaid>(r, bob);
        Use(r, bob, aid);
        r.In(() =>
        {
            Assert.Equal(2f, aid.current_mode);
            Assert.Equal("captainsaid_" + Dm.str(aid.modes[2]), aid.icon_state);
        });
        Assert.True(feed.Any("balloon: set to 2"), Said(feed));
        Assert.True(Heard(r, "tools/screwdriver2"), r.Sounds.Dump());
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(aid)));

        var button = Hold<obj_item_toy_redbutton>(r, bob);
        Use(r, bob, button);
        Assert.True(Heard(r, "explosion/explosionfar"), r.Sounds.Dump());
        Use(r, bob, button);
        Assert.True(feed.Any("Nothing happens."), Said(feed));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(button)));

        var radio = Hold<obj_item_toy_brokenradio>(r, bob);
        Use(r, bob, radio);
        r.Step(Second);
        Assert.True(Heard(r, "items/radiostatic"), r.Sounds.Dump());
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(radio)));

        var brain = Hold<obj_item_toy_braintoy>(r, bob);
        Use(r, bob, brain);
        r.Step(Second);
        Assert.True(Heard(r, "blob/blobattack"), r.Sounds.Dump());
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(brain)));

        var finger = Hold<obj_item_toy_foamfinger>(r, bob);
        Use(r, bob, finger);
        Assert.True(feed.Any("waves around the foam finger."), Said(feed));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(finger)));

        // the steampunk watch: its cogwheels' noise, the station time on examine
        var watch = Hold<obj_item_toy_clockwork_watch>(r, bob);
        Use(r, bob, watch);
        Assert.True(Heard(r, "clockwork/ark_activation"), r.Sounds.Dump());
        Use(r, bob, watch);
        Assert.True(feed.Any("The cogwheels are already turning!"), Said(feed));
        string examined = Examine(r, watch, bob);
        Assert.Contains("Station Time:", examined, StringComparison.Ordinal);
        Assert.Contains("into the shift", examined, StringComparison.Ordinal);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheVentriloquistDummyIsNamed()
    {
        var (r, feed, bob) = Room(9912);
        var doll = Hold<obj_item_toy_dummy>(r, bob);
        Use(r, bob, doll);
        r.Step(2);
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(bob.tgui_open_uis).Last(u => Dm.str(u.@interface) == "TextInputModal").window.id));
        r.Command(() => ClientSeam.TguiMessage(bob.client, window, "act/submit", Dm.list(Dm.kv("entry", "Mr Wiggles"))));
        r.Step(4);
        r.In(() =>
        {
            Assert.Equal("Mr Wiggles", doll.doll_name);
            Assert.Equal("ventriloquist dummy - Mr Wiggles", doll.name);
            Assert.Equal("Mr Wiggles", Dm.str(doll.get_voice()));
        });
        Assert.True(feed.Any("You name the dummy as"), Said(feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheIntentoPlaysASequenceScoresAndEndsTheGame()
    {
        var (r, feed, bob) = Room(9913);
        var intento = Hold<obj_item_toy_intento>(r, bob);
        Use(r, bob, intento);
        Assert.True(feed.Any("Game starting!"), Said(feed));
        Assert.True(Heard(r, "synth/synth_yes"), r.Sounds.Dump());
        r.In(() => Assert.Equal("starting", intento.state));

        // its demo shows the first intent (its icon state and its sound), then it waits for the player
        r.Step(4 * Second);
        string first = r.In(() =>
        {
            Assert.Equal("awaiting_player_input", intento.state);
            Assert.Equal(1, (int)intento.current_sequence.len);
            return Dm.str(intento.current_sequence[1]);
        });
        Assert.True(Heard(r, "intents/" + char.ToUpperInvariant(first[0]) + first[1..]), r.Sounds.Dump());

        // the player clicks the quadrant of the held toy: help top left, disarm top right, grab bottom right, harm bottom left
        Press(r, bob, intento, first);
        r.In(() =>
        {
            Assert.Equal(1f, intento.score);
            Assert.Equal("starting", intento.state);
        });

        // the second round: a wrong first intent ends the game with the score
        r.Step(5 * Second);
        r.In(() => Assert.Equal("awaiting_player_input", intento.state));
        string wrong = new[] { "help", "disarm", "grab", "harm" }.First(i => i != first);
        Press(r, bob, intento, wrong);
        r.Step(2 * Second);
        Assert.True(feed.Any("GAME OVER. Your score was 1!"), Said(feed));
        Assert.True(Heard(r, "synth/synth_no"), r.Sounds.Dump());
        r.In(() =>
        {
            Assert.Equal("off", intento.state);
            Assert.Equal(0f, intento.score);
            Assert.Equal(1f, Dm.num(GLOB.intento_players["bob"]));
        });
        r.AssertNoRuntimes();
    }

    private static void Press(AudioRoom r, mob who, atom toy, string intent)
    {
        (int x, int y) = intent switch
        {
            "help" => (8, 24),
            "disarm" => (24, 24),
            "grab" => (24, 8),
            _ => (8, 8),
        };
        r.Command(() => ClientSeam.Click(who.client, toy, $"icon-x={x};icon-y={y};left=1;button=left"));
        r.Step(2);
    }

    [Fact]
    public void TwoToyMechsFightABattleToItsEnd()
    {
        var (r, feed, bob) = Room(9914);
        var defender = Place<obj_item_toy_mecha_ripley>(r, 5, 4);
        var challenger = Hold<obj_item_toy_mecha_honk>(r, bob);
        Use(r, bob, defender);
        Assert.True(feed.Any("sparking a fierce battle!"), Said(feed));
        r.In(() =>
        {
            Assert.True(Dm.truthy(defender.in_combat));
            Assert.True(Dm.truthy(challenger.in_combat));
        });
        r.Step(90 * Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(defender.in_combat));
            Assert.False(Dm.truthy(challenger.in_combat));
            Assert.Equal(defender.max_combat_health, defender.combat_health);
            Assert.Equal(challenger.max_combat_health, challenger.combat_health);
            Assert.Equal(challenger.wins, defender.losses);
            Assert.Equal(defender.wins, challenger.losses);
        });
        Assert.True(feed.Any("victorious") || feed.Any("proudly over") || feed.Any("destroyed") || feed.Any("NEXT TIME"), Said(feed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALatexGloveBlowsUpFromATankAndBurstsOnASecondBlow()
    {
        var (r, feed, bob) = Room(9915);
        var balloon = Place<obj_item_latexballoon>(r, 5, 4);
        Hold<obj_item_tank_internals_oxygen>(r, bob);
        Use(r, bob, balloon);
        r.In(() =>
        {
            Assert.Equal("inflated", balloon.state);
            Assert.Equal("latexballoon_blow", balloon.icon_state);
            Assert.Equal("latexballoon", balloon.inhand_icon_state);
            Assert.Equal("It's a blown up latex glove on a string.", balloon.desc);
            Assert.NotNull(balloon.air_contents);
        });
        Assert.True(feed.Any("balloon: you blow up the balloon!"), Said(feed));
        Assert.True(Heard(r, "modsuit/inflate_bloon"), r.Sounds.Dump());

        Use(r, bob, balloon);
        r.In(() =>
        {
            Assert.Equal("popped", balloon.state);
            Assert.Equal("latexballoon_bursted", balloon.icon_state);
            Assert.Equal("The remains of a latex glove.", balloon.desc);
        });
        Assert.True(Heard(r, "items/balloon_pop"), r.Sounds.Dump());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHourglassRunsItsSandForAMinuteAndStops()
    {
        var (r, feed, bob) = Room(9916);
        Recorder recorder = Record(r);
        var glass = Hold<obj_item_hourglass>(r, bob);
        r.In(() => Assert.Equal("hourglass_idle", glass.icon_state));
        Use(r, bob, glass);
        Assert.True(feed.Any("You flip the"), Said(feed)); // tg's "You flip the [src].": DM puts its own "the" before the name too
        Assert.Contains(recorder.Flicks, f => ReferenceEquals(f.Target, glass) && f.State == "hourglass_flip");
        // the sand runs down its seven states over the minute
        Assert.Equal(new[] { "hourglass_1", "hourglass_2", "hourglass_3", "hourglass_4", "hourglass_5", "hourglass_6", "hourglass_7" }, recorder.AnimatedStates.Where(s => s.StartsWith("hourglass_", StringComparison.Ordinal)).ToArray());
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.NotNull(glass.timing_id);
            float shown = Dm.num(glass.countdown.displayed_text);
            Assert.True(shown >= 50 && shown <= 60, "the countdown shows " + shown);
        });

        r.Step(60 * Second);
        Assert.True(feed.Any("hourglass stops."), Said(feed));
        r.In(() =>
        {
            Assert.Null(glass.timing_id);
            Assert.Null(glass.finish_time);
            Assert.Equal("hourglass_idle", glass.icon_state);
        });

        // flipped again it runs, flipped once more it stops at once
        Use(r, bob, glass);
        r.In(() => Assert.NotNull(glass.timing_id));
        Use(r, bob, glass);
        Assert.True(feed.Any("You stop the"), Said(feed));
        r.In(() => Assert.Null(glass.timing_id));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBanhammerBansInsteadOfHitting()
    {
        var (r, feed, bob) = Room(9917);
        mob_living_carbon_human dummy = r.Dummy(5, 4);
        Hold<obj_item_banhammer>(r, bob);
        Use(r, bob, dummy);
        Assert.True(feed.Any("has been banned FOR NO REISIN by"), Said(feed)); // aimed below the head (tg's meme grammar)
        Assert.True(Heard(r, "effects/adminhelp"), r.Sounds.Dump());
        r.In(() => Assert.Equal(0f, Dm.num(dummy.get_brute_loss())));
        r.AssertNoRuntimes();
    }
}
