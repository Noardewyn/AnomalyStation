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
/// Gaps package AS1 (D333, the assemblies) in a booted toy room with Bob as a player: two unsecured assemblies clicked together make
/// an assembly holder (its name, its left and mirrored right overlays), the timer's pulse through it sets off the igniter, a screwdriver
/// takes it apart; the timer, health sensor and infrared emitter windows; the infrared beam (invisible, then visible, stopped by a
/// closet, crossed by Bob); the mousetrap armed in hand and snapping on a running barefoot human and a mouse; the voice analyzer that
/// records a phrase and clicks when it hears it; and the things a holder is attached to: gibtonite primed by its rig, a fuel tank
/// rigged to blow, a plasma tank made a bomb that a signal sets off, the shock kit's electric chair toggled by its electropack.
/// </summary>
public class AssembliesAS1Tests
{
    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() =>
        {
            ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test");
            (get_area(locate(4, 4, 1)) as area).requires_power = FALSE; // the toy room has no APC
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() =>
        {
            Waiting.NoSleep(who.drop_all_held_items());
            Waiting.NoSleep(who.put_in_active_hand(item));
        });
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target)
    {
        r.Step(Second);
        Click(r, who, target);
    }

    private static List<string> OverlayStates(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(s => (s.File ?? "").Contains(file, StringComparison.Ordinal));

    private static void RunUntil(AudioRoom r, int ticks, Func<bool> done)
    {
        for (int i = 0; i < ticks && !r.In(done); i++)
            r.Step();
    }

    // the window of that interface the player just opened by using the item in hand: its id and its data
    private static string OpenInHand(AudioRoom r, mob who, string iface, out JsonElement data)
    {
        Take(who);
        r.Command(() => ClientSeam.Keybinding(who.client, 4, true)); // activate_inhand: attack_self opens the window
        r.Step(4);
        JsonElement payload = LastUpdate(Take(who), out string window);
        Assert.Equal(iface, InterfaceOf(payload));
        data = payload.GetProperty("data");
        return window;
    }

    // two assemblies made into a holder in code (the click path is the first test's)
    private static obj_item_assembly_holder Holder(AudioRoom r, mob_living_carbon_human who, obj_item_assembly a, obj_item_assembly b) => r.In(() =>
    {
        var holder = Dm.@new<obj_item_assembly_holder>(who.loc);
        Waiting.NoSleep(a.toggle_secure());
        Waiting.NoSleep(b.toggle_secure());
        a.holder = holder;
        Waiting.NoSleep(holder.assemble(a, b, who));
        return holder;
    });

    [Fact]
    public void Two_assemblies_clicked_together_make_a_holder_whose_timer_sets_off_its_igniter_and_a_screwdriver_takes_it_apart()
    {
        var (r, feed, bob) = Room(5201);
        var timer = Place<obj_item_assembly_timer>(r, 5, 4);
        var igniter = Hold<obj_item_assembly_igniter>(r, bob);

        // a secured part is not attachable
        Use(r, bob, timer);
        Assert.True(feed.Any("balloon: igniter is not attachable!"), Diag(feed, r));
        r.In(() =>
        {
            Waiting.NoSleep(timer.toggle_secure());
            Waiting.NoSleep(igniter.toggle_secure());
        });

        Use(r, bob, timer);
        obj_item_assembly_holder holder = r.In(() => timer.holder as obj_item_assembly_holder);
        Assert.NotNull(holder);
        Assert.True(feed.Any("balloon: parts combined"), Diag(feed, r));
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(holder));
            Assert.Equal("timer-igniter assembly", holder.name);
            Assert.Same(locate(5, 4, 1), holder.loc);
            Assert.Same(holder, timer.loc);
            Assert.Same(holder, igniter.loc);
            Assert.Same(holder, igniter.holder);
            Assert.Equal(1f, timer.secured);
            Assert.Equal(1f, igniter.secured);
            Assert.Equal(new object[] { timer, igniter }, Dm.each(holder.assemblies).ToArray());
            Assert.Null(bob.get_active_held_item());
        });

        // the left part's own left sprite, the right one mirrored
        var overlays = r.In(() => Dm.each(holder.overlays).OfType<IDmAppearance>().Select(o => (o.icon_state, (o.transform as matrix)?.a ?? 1f)).ToList());
        Assert.Contains(("timer_left", 1f), overlays);
        Assert.Contains(("igniter_left", -1f), overlays);

        // the timer counting down shows on the holder; at its end it beeps and pulses the igniter
        r.In(() =>
        {
            timer.time = 4;
            timer.timing = 1;
            timer.update_appearance();
        });
        Assert.Contains("timer_timing_l", OverlayStates(r, holder));
        RunUntil(r, 8 * Second, () => igniter.next_activate > 0);
        Assert.True(r.In(() => igniter.next_activate > 0), "the igniter was not pulsed: " + Diag(feed, r));
        Assert.True(Heard(r, "triple_beep"), r.Sounds.Dump());
        Assert.True(feed.Any("*beep* *beep* *beep*"), Diag(feed, r));
        r.In(() =>
        {
            Assert.Equal(0f, timer.timing);
            Assert.Equal(10f, timer.time); // back to its set time (the window's input sets it; 10 by default)
        });
        Assert.DoesNotContain("timer_timing_l", OverlayStates(r, holder));

        // the screwdriver takes it apart: the parts drop where it was
        Hold<obj_item_screwdriver>(r, bob);
        Use(r, bob, holder);
        r.Step(2);
        r.In(() =>
        {
            Assert.True(QDELETED(holder));
            Assert.Same(locate(5, 4, 1), timer.loc);
            Assert.Same(locate(5, 4, 1), igniter.loc);
            Assert.Null(timer.holder);
        });
        Assert.True(feed.Any("balloon: disassembled"), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_timer_window_sets_the_time_repeat_and_start_and_the_timer_lights_and_beeps()
    {
        var (r, feed, bob) = Room(5202);
        var timer = Hold<obj_item_assembly_timer>(r, bob);
        Assert.False(r.In(() => InertSeam.IsInert(timer)));
        Assert.True(r.In(() => Dm.@in(timer, SSobj.processing)), "the timer does not process");

        string window = OpenInHand(r, bob, "Timer", out JsonElement data);
        Assert.Equal(0, data.GetProperty("minutes").GetDouble());
        Assert.Equal(10, data.GetProperty("seconds").GetDouble());
        Assert.Equal(0, data.GetProperty("timing").GetDouble());
        Assert.Equal(0, data.GetProperty("loop").GetDouble());

        Act(r, bob, window, "input", ("adjust", 30));
        data = Data(r, bob, out _);
        Assert.Equal(40, data.GetProperty("seconds").GetDouble());
        r.In(() => Assert.Equal(40f, timer.saved_time));
        Act(r, bob, window, "input", ("adjust", 600));
        r.In(() => Assert.Equal(600f, timer.time)); // clamped to ten minutes
        data = Data(r, bob, out _);
        Assert.Equal(10, data.GetProperty("minutes").GetDouble());
        Act(r, bob, window, "input", ("adjust", -597));
        r.In(() => Assert.Equal(3f, timer.time));
        Act(r, bob, window, "repeat");
        r.In(() => Assert.Equal(1f, timer.loop));

        // started: one light per ten seconds left, at least one; the examine line counts down
        Act(r, bob, window, "time");
        r.In(() => Assert.Equal(1f, timer.timing));
        var lights = r.In(() => Dm.each(timer.overlays).OfType<IDmAppearance>().Where(o => o.icon_state == "timer_light").Select(o => o.pixel_w).ToList());
        Assert.Equal(new[] { 0f }, lights);
        string examine = r.In(() => string.Join(" ", Dm.each(Waiting.NoSleep(timer.examine(bob))).Select(Dm.str)));
        Assert.Contains("The timer is counting down from", examine);

        // it ends: the triple beep in Bob's ears, and the repeat starts it again from its set time
        RunUntil(r, 6 * Second, () => Heard(r, "triple_beep"));
        Assert.True(Heard(r, "triple_beep"), r.Sounds.Dump());
        r.In(() =>
        {
            Assert.Equal(1f, timer.timing);
            Assert.True(timer.time >= 1f);
        });

        // unsecured, it stops and closes its window
        r.In(() => Waiting.NoSleep(timer.toggle_secure()));
        r.In(() =>
        {
            Assert.Equal(0f, timer.timing);
            Assert.False(Dm.@in(timer, SSobj.processing));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_health_sensor_shows_its_holders_health_and_beeps_when_he_falls_into_crit()
    {
        var (r, feed, bob) = Room(5203);
        var sensor = Hold<obj_item_assembly_health>(r, bob);
        Assert.False(r.In(() => InertSeam.IsInert(sensor)));
        Assert.Contains("HP: 100%", r.In(() => sensor.maptext));

        string window = OpenInHand(r, bob, "HealthSensor", out JsonElement data);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("health").ValueKind);
        Assert.Equal(0, data.GetProperty("scanning").GetDouble());
        Assert.Equal(HEALTH_THRESHOLD_CRIT, data.GetProperty("target").GetDouble());
        Act(r, bob, window, "target");
        r.In(() => Assert.Equal((float)HEALTH_THRESHOLD_DEAD, sensor.health_target));
        Act(r, bob, window, "target");
        r.In(() => Assert.Equal((float)HEALTH_THRESHOLD_CRIT, sensor.health_target));
        Act(r, bob, window, "scanning");
        r.In(() =>
        {
            Assert.Equal(1f, sensor.scanning);
            Assert.True(Dm.@in(sensor, SSobj.processing));
        });
        r.Step(3 * Second);
        Assert.Equal(100f, r.In(() => Dm.num(sensor.health_scan)));
        Assert.False(Heard(r, "triple_beep"));

        // in his pocket (a hand drops what it holds as he falls), into crit: the maptext follows, the sensor beeps and turns itself off
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_under_color_grey>(bob.loc), ITEM_SLOT_ICLOTHING))));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(sensor, ITEM_SLOT_LPOCKET))));
        });
        Assert.Same(bob, r.In(() => sensor.loc));
        r.In(() => Waiting.NoSleep(bob.adjust_oxy_loss(110))); // brute would spread over the limbs, each capped
        Assert.Contains("HP: -10%", r.In(() => sensor.maptext));
        RunUntil(r, 4 * Second, () => sensor.scanning == 0);
        r.In(() =>
        {
            Assert.Equal(0f, sensor.scanning);
            Assert.True(Dm.num(sensor.health_scan) <= 0);
        });
        Assert.True(Heard(r, "triple_beep"), r.Sounds.Dump());

        // dropped, its maptext goes
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(sensor)));
        Assert.Null(r.In(() => sensor.maptext));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_infrared_beam_runs_to_the_first_blocker_shows_when_visible_and_beeps_when_crossed()
    {
        var (r, feed, bob) = Room(5204);
        var emitter = Place<obj_item_assembly_infra>(r, 2, 6);
        var closet = Place<obj_structure_closet>(r, 8, 6);
        r.In(() => Waiting.NoSleep(emitter.setDir(EAST)));
        Assert.False(r.In(() => InertSeam.IsInert(emitter)));
        Assert.Null(r.In(() => emitter.active_beam));

        r.In(() => emitter.toggle_on());
        List<(int X, float Invisibility)> Segments() => r.In(() => Dm.each_cast<obj_effect_ebeam>(emitter.active_beam?.elements)
            .Select(b => ((int)b.x, b.invisibility)).ToList());
        var segments = Segments();
        Assert.NotEmpty(segments);
        Assert.All(segments, s => Assert.Equal((float)INVISIBILITY_ABSTRACT, s.Invisibility));
        Assert.Equal(2, segments.Min(s => s.X));
        Assert.Equal(6, segments.Max(s => s.X)); // the closet at 8 stops it: the beam runs to 7, its last segment drawn on 6
        Assert.Contains("infrared_on", OverlayStates(r, emitter));
        r.In(() => Assert.Same(locate(8, 6, 1), emitter.buffer_turf));

        // visible: the segments show
        r.In(() => emitter.toggle_visible());
        Assert.All(Segments(), s => Assert.Equal(0f, s.Invisibility));

        // the closet moved off: the beam grows back to its full length
        r.In(() => Waiting.NoSleep(closet.forceMove(locate(8, 8, 1))));
        Assert.True(Segments().Max(s => s.X) > 6);

        // Bob walks into it: the beep, and the emitter's three seconds of cooldown
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.Move(locate(4, 5, 1), NORTH)));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.Move(locate(4, 6, 1), NORTH)));
        r.Step(4);
        Assert.True(Heard(r, "triple_beep"), r.Sounds.Dump() + Diag(feed, r));
        r.In(() => Assert.True(emitter.next_activate > world.time));
        // standing in it, he blocks it: the beam now runs to the turf before him, drawn on its first
        Assert.Equal(2, Segments().Max(s => s.X));

        // switched off, it is gone
        r.In(() => emitter.toggle_on());
        Assert.Null(r.In(() => emitter.active_beam));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_infrared_window_switches_the_emitter_and_its_visibility()
    {
        var (r, feed, bob) = Room(5205);
        var emitter = Hold<obj_item_assembly_infra>(r, bob);
        string window = OpenInHand(r, bob, "InfraredEmitter", out JsonElement data);
        Assert.Equal(0, data.GetProperty("on").GetDouble());
        Assert.Equal(0, data.GetProperty("visible").GetDouble());
        Act(r, bob, window, "power");
        Act(r, bob, window, "visibility");
        data = Data(r, bob, out _);
        Assert.Equal(1, data.GetProperty("on").GetDouble());
        Assert.Equal(1, data.GetProperty("visible").GetDouble());
        Assert.Null(r.In(() => emitter.active_beam)); // in a hand: no beam
        string examine = r.In(() => string.Join(" ", Dm.each(Waiting.NoSleep(emitter.examine(bob))).Select(Dm.str)));
        Assert.Contains("The infrared trigger is on.", examine);

        // dropped facing its way, it beams
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(emitter)));
        r.In(() => Waiting.NoSleep(emitter.setDir(WEST)));
        Assert.NotNull(r.In(() => emitter.active_beam));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_armed_mousetrap_snaps_on_a_running_barefoot_human_and_splats_a_mouse()
    {
        var (r, feed, bob) = Room(5206);
        var trap = Hold<obj_item_assembly_mousetrap>(r, bob);
        Assert.False(r.In(() => InertSeam.IsInert(trap)));
        r.In(() => Waiting.NoSleep(trap.attack_self(bob)));
        r.In(() =>
        {
            Assert.Equal(1f, trap.armed);
            Assert.Equal("mousetraparmed", trap.icon_state);
        });
        Assert.True(feed.Any("You arm the mousetrap."), Diag(feed, r));
        Assert.True(Heard(r, "handcuffs"), r.Sounds.Dump());
        string examine = r.In(() => string.Join(" ", Dm.each(Waiting.NoSleep(trap.examine(bob))).Select(Dm.str)));
        Assert.Contains("The pressure plate is primed.", examine);

        // set down on the floor, it watches its turf
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(trap)));
        r.In(() => Waiting.NoSleep(trap.forceMove(locate(6, 4, 1))));
        r.In(() => Assert.Same(locate(6, 4, 1), trap.host_turf));

        // a barefoot human running onto it: a leg hurt, paralysed, the snap
        var dummy = r.Dummy(6, 5);
        r.Step(Second);
        r.In(() => Waiting.NoSleep(dummy.Move(locate(6, 4, 1), SOUTH)));
        r.Step(4);
        r.In(() =>
        {
            Assert.Equal(0f, trap.armed);
            Assert.Equal("mousetrap", trap.icon_state);
            Assert.True(Dm.truthy(dummy.IsParalyzed()));
            Assert.Equal(1f, Dm.num(dummy.get_total_damage()));
        });
        Assert.True(Heard(r, "snap"), r.Sounds.Dump());

        // a mapped armed trap and a mouse: splat
        var armed = Place<obj_item_assembly_mousetrap_armed>(r, 9, 4);
        var mouse = r.In(() =>
        {
            var m = Dm.@new<mob_living_basic_mouse>(locate(9, 5, 1));
            Waiting.NoSleep(m.Move(locate(9, 4, 1), SOUTH));
            return m;
        });
        r.Step(4);
        r.In(() =>
        {
            Assert.Equal(0f, armed.armed);
            Assert.Equal((float)DEAD, Dm.num(mouse.stat));
        });
        Assert.True(feed.Any("SPLAT!"), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_voice_analyzer_records_a_phrase_and_clicks_when_it_hears_it_again()
    {
        var (r, feed, bob) = Room(5207);
        var analyzer = Place<obj_item_assembly_voice>(r, 5, 4);
        Assert.False(r.In(() => InertSeam.IsInert(analyzer)));

        r.In(() => Waiting.NoSleep(analyzer.attack_self(bob)));
        r.In(() => Assert.Equal(1f, analyzer.listening));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.say("open sesame")));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Equal(0f, analyzer.listening);
            Assert.Equal("open sesame", analyzer.recorded, ignoreCase: true); // the speaker's capitalized line
        });
        Assert.True(feed.Any("Activation message is"), Diag(feed, r));

        // said again within a line (the inclusive mode): the click, and its pulse two seconds later
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.say("please open sesame now")));
        r.Step(4);
        Assert.True(Heard(r, "whirthunk"), r.Sounds.Dump());
        Assert.True(feed.Any("clicks."), Diag(feed, r));

        // a multitool turns it to the exclusive mode and forgets the phrase
        var multitool = Hold<obj_item_multitool>(r, bob);
        r.In(() => Waiting.NoSleep(analyzer.multitool_act(bob, multitool)));
        r.In(() =>
        {
            Assert.Equal(2f, analyzer.mode);
            Assert.Equal("", analyzer.recorded);
        });
        Assert.True(feed.Any("into exclusive mode"), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_rig_on_gibtonite_primes_it_when_its_igniter_fires()
    {
        var (r, feed, bob) = Room(5208);

        // the gibtonite: rigged with a timer and an igniter, the igniter's pulse primes it
        var gib = Place<obj_item_gibtonite>(r, 5, 4);
        obj_item_assembly_timer timer = r.In(() => Dm.@new<obj_item_assembly_timer>(bob.loc));
        obj_item_assembly_igniter igniter = r.In(() => Dm.@new<obj_item_assembly_igniter>(bob.loc));
        var rig = Holder(r, bob, timer, igniter);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(rig)));
        Use(r, bob, gib);
        r.In(() =>
        {
            Assert.Same(rig, gib.rig);
            Assert.Same(gib, rig.loc);
            Assert.Same(gib, rig.master);
        });
        Assert.True(feed.Any("attached rig"), Diag(feed, r));
        r.In(() => rig.process_activation(timer));
        r.Step(4);
        r.In(() => Assert.Equal(1f, gib.primed));
        r.In(() => gib.defuse(bob)); // no blast in this room
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_rigged_fuel_tank_blows_when_its_igniter_fires()
    {
        var (r, feed, bob) = Room(5211);
        // the fuel tank: two seconds to rig it, and the igniter's pulse blows it
        var tank = Place<obj_structure_reagent_dispensers_fueltank>(r, 5, 6);
        obj_item_assembly_timer timer2 = r.In(() => Dm.@new<obj_item_assembly_timer>(bob.loc));
        obj_item_assembly_igniter igniter2 = r.In(() => Dm.@new<obj_item_assembly_igniter>(bob.loc));
        var rig2 = Holder(r, bob, timer2, igniter2);
        r.In(() =>
        {
            Waiting.NoSleep(bob.drop_all_held_items());
            Waiting.NoSleep(bob.forceMove(locate(5, 5, 1)));
            Waiting.NoSleep(bob.put_in_active_hand(rig2));
            Assert.True(Dm.truthy(tank.IsSpecialAssembly()));
        });
        Use(r, bob, tank);
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.Same(rig2, tank.rig);
            Assert.Same(tank, rig2.loc);
        });
        r.In(() => rig2.process_activation(timer2));
        r.Step(Second);
        Assert.True(r.In(() => QDELETED(tank)), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_signalled_tank_bomb_beeps_and_an_unwelded_one_lets_its_gas_out()
    {
        var (r, feed, bob) = Room(5209);
        var tank = Place<obj_item_tank_internals_plasma>(r, 5, 4);
        obj_item_assembly_signaler signaler = r.In(() => Dm.@new<obj_item_assembly_signaler>(bob.loc));
        obj_item_assembly_igniter igniter = r.In(() => Dm.@new<obj_item_assembly_igniter>(bob.loc));
        var bomb = Holder(r, bob, signaler, igniter);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(bomb)));
        Use(r, bob, tank);
        r.In(() =>
        {
            Assert.Same(bomb, tank.tank_assembly);
            Assert.Same(tank, bomb.master);
            Assert.Equal((float)WEIGHT_CLASS_BULKY, tank.w_class);
        });
        Assert.True(feed.Any("balloon: bomb assembled"), Diag(feed, r));
        Assert.Contains("bomb_assembly", OverlayStates(r, tank));

        // the signaler's pulse reaches the tank through the holder: the beep, a second, and the gas let out
        float before = r.In(() => Dm.num(((datum_gas_mixture)Waiting.NoSleep(tank.return_air())).total_moles()));
        Assert.True(before > 0);
        r.In(() => bomb.process_activation(signaler));
        r.Step(2);
        Assert.True(Heard(r, "triple_beep"), r.Sounds.Dump());
        r.Step(2 * Second);
        float after = r.In(() => Dm.num(((datum_gas_mixture)Waiting.NoSleep(tank.return_air())).total_moles()));
        Assert.True(after < before / 100, $"the tank kept {after} of {before} moles");
        Assert.False(r.In(() => QDELETED(tank)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_shock_kit_makes_an_electric_chair_whose_loop_its_electropack_toggles()
    {
        var (r, feed, bob) = Room(5210);
        var chair = Place<obj_structure_chair>(r, 5, 4);
        var kit = Hold<obj_item_assembly_shock_kit>(r, bob);
        r.Command(() => Waiting.Detach(chair.attackby(kit, bob)));
        r.Step(4);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(chair, TRAIT_ELECTRIFIED_BUCKLE));
            Assert.Equal("electrified chair", chair.name);
            Assert.Same(chair, kit.loc);
            Assert.NotNull(chair.GetComponent(typeof(datum_component_electrified_buckle)));
        });
        Assert.Contains("echair_over", OverlayStates(r, chair));
        Assert.True(feed.Any("You connect the shock kit to"), Diag(feed, r));

        // the electropack's signal pulses the kit: the circuit opens, then closes
        r.In(() => kit.pulsed());
        r.Step(2);
        Assert.True(feed.Any("emits a snap as its circuit opens"), Diag(feed, r));
        r.In(() => kit.pulsed());
        r.Step(2);
        Assert.True(feed.Any("electric circuit closing"), Diag(feed, r));

        // the screwdriver takes the kit out again
        var screwdriver = Hold<obj_item_screwdriver>(r, bob);
        Use(r, bob, chair);
        r.Step(4);
        r.In(() =>
        {
            Assert.False(HAS_TRAIT(chair, TRAIT_ELECTRIFIED_BUCKLE));
            Assert.Equal("chair", chair.name);
            Assert.Same(locate(5, 4, 1), kit.loc);
        });
        r.AssertNoRuntimes();
    }
}
