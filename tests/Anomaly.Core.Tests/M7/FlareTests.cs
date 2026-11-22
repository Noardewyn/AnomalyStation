using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// M7 lighting slice L1 (D168): flashlight.dm's flares (:441-586), candles (:587-725), torches (:727-760) and glowsticks
/// (:870-1058). A flare is lit by hand, burns its fuel down in SSobj's processing, lights what is near it and goes out into
/// its burnt trash; a candle is lit from another fire and snuffed; a glowstick cracks, burns its fluid down in minutes by
/// timers, and dims by tg's thresholds.
/// </summary>
public class FlareTests
{
    private const int Second = 20;
    private const string Left = "icon-x=16;icon-y=16;left=1;button=left";

    private static (AudioRoom R, mob_living_carbon_human Bob, ChatCapture Chat) Build(ulong seed)
    {
        var r = new AudioRoom(13, 10, seed: seed);
        mob_living_carbon_human bob = r.Join(5, 3, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        r.Step(Second);
        return (r, bob, chat);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who, params object[] args) where T : obj_item => r.In(() =>
    {
        foreach (object held in Dm.each(who.held_items.Copy()))
        {
            if (held is obj_item old)
                qdel(old);
        }
        T item = Dm.@new<T>(new object[] { get_turf(who) }.Concat(args).ToArray());
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(item, merge_stacks: FALSE))), "the item reaches a hand");
        return item;
    });

    private static T Floor<T>(AudioRoom r, int x, int y, params object[] args) where T : obj_item
        => r.In(() => Dm.@new<T>(new object[] { locate(x, y, 1) }.Concat(args).ToArray()));

    private static void Click(AudioRoom r, mob who, atom target, float seconds = 1)
    {
        r.Command(() => ClientSeam.Click(who.client, target, Left));
        r.Step(Math.Max(2, (int)(seconds * Second)));
    }

    private static void Use(AudioRoom r, mob_living_carbon_human who, obj_item item) => r.In(() => Waiting.NoSleep(item.attack_self(who)));

    private static bool Processing(AudioRoom r, datum d) => r.In(() => Dm.@in(d, SSobj.processing));

    [Fact]
    public void AFlareLightsInTheHandBurnsItsFuelDownAndGoesOutIntoItsBurntFlare()
    {
        var (r, bob, chat) = Build(9201);
        var flare = Hold<obj_item_flashlight_flare>(r, bob);
        float fuel0 = r.In(() =>
        {
            Assert.InRange(flare.fuel.Value, 10 * MINUTES, 15 * MINUTES); // rand(10 MINUTES, 15 MINUTES), :478
            Assert.False(Dm.truthy(flare.light_on));
            Assert.Equal(0f, Dm.num(flare.get_temperature()));
            return flare.fuel.Value;
        });

        Use(r, bob, flare); // attack_self -> ignition -> toggle_light, :580
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(flare.light_on));
            Assert.Equal("lit flare", Dm.str(flare.name));         // :513
            Assert.Equal(7f, Dm.num(flare.force));                  // on_damage
            Assert.Equal(BURN, Dm.str(flare.damtype));
            Assert.Equal("flare-on", flare.icon_state);              // flashlight.dm:83
            Assert.Equal("flare-on", flare.inhand_icon_state);       // :545
            Assert.Equal(1000f, Dm.num(flare.get_temperature()));    // light_on * heat
            Assert.Equal(7f, Dm.num(flare.light_range));
            Assert.Equal(2f, Dm.num(flare.light_power));
            Assert.Equal(LIGHT_COLOR_FLARE, Dm.str(flare.light_color));
        });
        Assert.True(Processing(r, flare));
        Assert.Contains(chat.Html, h => h.Contains("You light", StringComparison.Ordinal));
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("match_strike", StringComparison.Ordinal)); // sound_on, :458

        // already lit: a second use says so and changes nothing
        Use(r, bob, flare);
        r.In(() => Assert.Equal("lit flare", Dm.str(flare.name)));

        // fuel runs down by the seconds of SSobj's processing (:549)
        r.Step(30 * Second);
        float fuel1 = r.In(() => flare.fuel.Value);
        Assert.InRange(fuel0 - fuel1, 30 * 10 - 2 * 10 - 1, 31 * 10 + 2 * 10); // 30 s of 1 SECONDS each, to one period of SSobj

        // out of fuel: it goes out and becomes the burnt flare in whatever holds it (:553-557)
        r.In(() => flare.fuel = 15);
        r.Step(5 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(flare));
            obj_item_trash_flare trash = Assert.Single(Dm.each(bob.contents).OfType<obj_item_trash_flare>());
            Assert.Equal("burnt flare", Dm.str(trash.name));
            Assert.Equal("flare-empty", trash.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFlareOutOfFuelWillNotLightAndAFlareOnTheFloorLightsFromFire()
    {
        var (r, bob, chat) = Build(9202);
        var spent = Floor<obj_item_flashlight_flare>(r, 6, 3);
        r.In(() => spent.fuel = 0);
        r.In(() => Waiting.NoSleep(spent.attack_self(bob)));
        r.In(() => Assert.False(Dm.truthy(spent.light_on)));

        var flare = Floor<obj_item_flashlight_flare>(r, 7, 3);
        r.In(() => Waiting.NoSleep(flare.fire_act(1000, 100))); // fire_act -> ignition, :576
        r.In(() =>
        {
            Assert.True(Dm.truthy(flare.light_on));
            Assert.Equal("lit flare", Dm.str(flare.name));
        });
        Assert.True(Processing(r, flare));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ATorchBurnsAnEverburningTorchNeverAndARedTorchIsRed()
    {
        var (r, bob, _) = Build(9203);
        var torch = Floor<obj_item_flashlight_flare_torch>(r, 6, 3);
        var on = Floor<obj_item_flashlight_flare_torch_on>(r, 7, 3);
        var ever = Floor<obj_item_flashlight_flare_torch_everburning>(r, 8, 3);
        var red = Floor<obj_item_flashlight_flare_torch_red>(r, 9, 3);
        r.Step(Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(torch.light_on));
            Assert.Equal(4f, Dm.num(torch.light_range));
            Assert.Equal(LIGHT_COLOR_ORANGE, Dm.str(torch.light_color));
            Assert.True(Dm.truthy(on.light_on));                       // start_on
            Assert.Equal(10f, Dm.num(on.force));                         // on_damage = 10 (set in Initialize for a lit torch)
            Assert.True(Dm.truthy(ever.light_on));
            Assert.Equal("everburning torch", Dm.str(ever.name));
            Assert.Equal(INFINITY, ever.fuel.Value);                       // INFINITY (1e31), :749
            Assert.Equal(2f, Dm.num(red.light_range));                   // :757
            Assert.Equal("#ff0000", Dm.str(red.color));
        });
        // the everburning torch is never put out and is not processed
        Use(r, bob, ever);
        r.Step(10 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(ever.light_on));
            Assert.Equal(INFINITY, ever.fuel.Value);
        });
        // a plain torch lit by hand has force 10 and is put out by extinguish() (:537), as it can_be_extinguished
        r.In(() => Waiting.NoSleep(torch.fire_act(1000, 100)));
        r.In(() =>
        {
            Assert.True(Dm.truthy(torch.light_on));
            Assert.Equal(10f, Dm.num(torch.force));
            Waiting.NoSleep(torch.extinguish());
            Assert.False(Dm.truthy(torch.light_on));
            Assert.Equal("torch", Dm.str(torch.name));
            Assert.Equal(Dm.initial(torch, (obj_item_flashlight_flare_torch p) => p.force), torch.force);
        });
        // burnt out, a torch leaves ash (:735)
        r.In(() => { Waiting.NoSleep(torch.fire_act(1000, 100)); torch.fuel = 15; });
        r.Step(5 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(torch));
            Assert.Single(Dm.each(locate(6, 3, 1).contents).OfType<obj_effect_decal_cleanable_ash>());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACandleIsLitFromAnotherFireShowsItsWaxAndIsSnuffedAndMelts()
    {
        var (r, bob, chat) = Build(9204);
        var candle = Floor<obj_item_flashlight_flare_candle>(r, 6, 3);
        r.In(() =>
        {
            Assert.Equal(35 * MINUTES, candle.fuel.Value);
            Assert.Equal(2f, Dm.num(candle.light_range));
            Assert.Equal(1.5f, Dm.num(candle.light_power));
            Assert.Equal("candle1", candle.icon_state);
            Assert.Equal(1f, Dm.num(candle.current_wax_level));
        });

        // a click with a bare hand does nothing; a lit flare lights it (:673, ignition_effect of the item, items.dm:969)
        var flare = Hold<obj_item_flashlight_flare>(r, bob);
        Click(r, bob, candle);
        r.In(() => Assert.False(Dm.truthy(candle.light_on), "an unlit flare is no fire"));
        Use(r, bob, flare);
        Click(r, bob, candle);
        r.In(() =>
        {
            Assert.True(Dm.truthy(candle.light_on));
            Assert.Equal("candle1_lit", candle.icon_state);   // :637
            Assert.Equal("candle_lit", candle.inhand_icon_state);
        });
        Assert.True(Processing(r, candle));

        // the wax level follows the fuel (:621-631)
        r.In(() => candle.fuel = 20 * MINUTES);
        r.Step(4 * Second);
        r.In(() =>
        {
            Assert.Equal(2f, Dm.num(candle.current_wax_level));
            Assert.Equal("candle2_lit", candle.icon_state);
            candle.fuel = 10 * MINUTES;
        });
        r.Step(4 * Second);
        r.In(() =>
        {
            Assert.Equal(3f, Dm.num(candle.current_wax_level));
            Assert.Equal("candle3_lit", candle.icon_state);
        });

        // another lit thing lights a candle: a lit candle's ignition_effect (:704) is a romantic message; an unlit one is none
        r.In(() =>
        {
            Assert.Contains("holds the lit flare in the flame", Dm.str(Waiting.NoSleep(candle.ignition_effect(flare, bob))), StringComparison.Ordinal); // A is an item in the user's hand: the first line, :707
            var other = Dm.@new<obj_item_flashlight_flare_candle>(get_turf(bob));
            Assert.Equal("", Dm.str(Waiting.NoSleep(other.ignition_effect(candle, bob))));
        });

        // snuffed by hand (:711)
        var inHand = Hold<obj_item_flashlight_flare_candle>(r, bob);
        r.In(() => inHand.fuel = 30 * MINUTES);
        Use(r, bob, inHand);
        Assert.False(r.In(() => Dm.truthy(inHand.light_on)));
        r.In(() => Waiting.NoSleep(flare.ignition_effect(inHand, bob)));

        // melted: out of wax, the candle is the melted candle
        r.In(() => candle.fuel = 15);
        r.Step(5 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(candle));
            obj_item_trash_candle trash = Assert.Single(Dm.each(locate(6, 3, 1).contents).OfType<obj_item_trash_candle>());
            Assert.Equal("melted candle", Dm.str(trash.name));
            Assert.Equal("candle4", trash.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnEternalCandleBurnsFromTheStartAndAWelderLightsACandle()
    {
        var (r, bob, chat) = Build(9205);
        var eternal = Floor<obj_item_flashlight_flare_candle_infinite>(r, 7, 3);
        var candle = Floor<obj_item_flashlight_flare_candle>(r, 6, 3);
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(eternal.light_on));
            Assert.Equal("eternal candle", Dm.str(eternal.name));
            Assert.Equal(INFINITY, eternal.fuel.Value);
        });
        // a lit welder lights a candle: weldingtool.dm:332
        var welder = Hold<obj_item_weldingtool>(r, bob);
        r.In(() => Waiting.NoSleep(welder.attack_self(bob)));
        r.In(() => Assert.True(Dm.truthy(welder.welding)));
        Click(r, bob, candle);
        r.In(() =>
        {
            Assert.True(Dm.truthy(candle.light_on));
            Assert.Contains("badass", Dm.str(Waiting.NoSleep(welder.ignition_effect(eternal, bob))), StringComparison.Ordinal); // span_rose, :333
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACandleInAHandIsPlacedOnTheFloorByAClickOnIt()
    {
        var (r, bob, _) = Build(9206);
        var candle = Hold<obj_item_flashlight_flare_candle>(r, bob);
        turf floor = r.In(() => locate(6, 3, 1) as turf);
        Click(r, bob, floor);
        r.In(() =>
        {
            Assert.Same(floor, candle.loc);            // datum/element/floor_placeable
            Assert.Null(bob.get_active_held_item());
        });
        r.AssertNoRuntimes();
    }

    // ---- glowsticks ----

    [Fact]
    public void AGlowstickCracksBurnsItsFluidDownAndDimsByTgsThresholds()
    {
        var (r, bob, chat) = Build(9207);
        var stick = Hold<obj_item_flashlight_glowstick_red>(r, bob, 20f);
        float max = r.In(() =>
        {
            Assert.Equal(20f, Dm.num(stick.max_fuel));                       // fuel_override
            Assert.Equal(20f, Dm.num(stick.get_fuel()));
            Assert.Equal(25f, Dm.num(((datum_reagents)stick.reagents).maximum_volume)); // max_fuel + oxygen_added
            Assert.Equal(typeof(datum_reagent_luminescent_fluid_red), stick.fuel_type);
            Assert.Equal(COLOR_SOFT_RED, Dm.str(stick.color));               // :1030
            Assert.Equal(COLOR_SOFT_RED, Dm.str(stick.light_color));         // set_light_color(color), :907
            Assert.False(Dm.truthy(stick.light_on));
            Assert.Equal("glowstick", stick.icon_state);
            return Dm.num(stick.max_fuel);
        });

        Use(r, bob, stick); // cracked: attack_self -> toggle_light (the cracking sound) -> turn_on
        r.In(() =>
        {
            Assert.True(Dm.truthy(stick.light_on));
            Assert.Equal(25f - 2f, Dm.num(((datum_reagents)stick.reagents).total_volume)); // oxygen_added (:959) and the first burn_loop(round(25 * 0.1)), round(A) being floor, :961
            Assert.True(Dm.truthy(((datum_reagents)stick.reagents).has_reagent(typeof(datum_reagent_oxygen))));
            Assert.False(Dm.eq(stick.timer_id, TIMER_ID_NULL), "the burn loop is scheduled");
            Assert.Null(Dm.locate_type(typeof(datum_action_item_action_toggle_light), stick.actions)); // the toggle button is removed, :962
        });
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("crack2", StringComparison.Ordinal)); // sound_on, :879
        Assert.Contains(chat.Html, h => h.Contains("You crack and shake", StringComparison.Ordinal));

        // the burn: remove_all(3) of the 25 (the first loop is round(25 * 0.1)), light 3 / 1.5 while the fuel is at least 0.4 of the max
        r.Step(4 * 60 * Second);
        r.In(() =>
        {
            float fuel = Dm.num(stick.get_fuel());
            Assert.True(fuel < 20f, "the minutes burned the fluid");
            Assert.True(Dm.truthy(stick.light_on) || fuel <= 0);
        });

        // the thresholds, by direct burn_down calls (:926-949): 0.4 -> 3 / 1.5, 0.3 -> 2 / 1.25, 0.2 -> power 1, 0.1 -> 1.5 / 0.5
        var t = Hold<obj_item_flashlight_glowstick>(r, bob, 40f);
        Use(r, bob, t);
        foreach ((float target, float range, float power) in new[] { (0.45f, 3f, 1.5f), (0.35f, 2f, 1.25f), (0.25f, 2f, 1f), (0.15f, 1.5f, 0.5f) })
        {
            r.In(() =>
            {
                float fuel = Dm.num(t.get_fuel());
                float total = ((datum_reagents)t.reagents).total_volume;
                float amount = total * (1 - 40f * target / fuel); // remove_all takes the same share of every reagent
                Waiting.NoSleep(t.burn_down(amount));
                Assert.Equal(40f * target, Dm.num(t.get_fuel()), 1);
                Assert.Equal(range, Dm.num(t.light_range), 3);
                Assert.Equal(power, Dm.num(t.light_power), 3);
            });
        }
        _ = max;
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASpentGlowstickShowsItsEmptyStateAndRefusesAndEachColourHasItsOwnFluid()
    {
        var (r, bob, chat) = Build(9208);
        var stick = Hold<obj_item_flashlight_glowstick>(r, bob, 20f);
        Use(r, bob, stick);
        r.In(() => Assert.True(Dm.truthy(stick.light_on)));
        // the fluid is gone: the holder's update turns the stick off (on_reagent_change, :977)
        r.In(() => Waiting.NoSleep(((datum_reagents)stick.reagents).remove_reagent(typeof(datum_reagent_luminescent_fluid), 1000)));
        r.In(() =>
        {
            Assert.False(Dm.truthy(stick.light_on));
            Assert.Equal("glowstick-empty", stick.icon_state);   // :983
            Assert.True(Dm.eq(stick.timer_id, TIMER_ID_NULL));
        });
        chat.Html.Clear();
        Use(r, bob, stick);
        r.In(() => Assert.False(Dm.truthy(stick.light_on)));

        foreach ((Type type, string color, Type fluid) in new[]
        {
            (typeof(obj_item_flashlight_glowstick_red), COLOR_SOFT_RED, typeof(datum_reagent_luminescent_fluid_red)),
            (typeof(obj_item_flashlight_glowstick_blue), LIGHT_COLOR_BLUE, typeof(datum_reagent_luminescent_fluid_blue)),
            (typeof(obj_item_flashlight_glowstick_cyan), LIGHT_COLOR_CYAN, typeof(datum_reagent_luminescent_fluid_cyan)),
            (typeof(obj_item_flashlight_glowstick_orange), LIGHT_COLOR_ORANGE, typeof(datum_reagent_luminescent_fluid_orange)),
            (typeof(obj_item_flashlight_glowstick_yellow), LIGHT_COLOR_DIM_YELLOW, typeof(datum_reagent_luminescent_fluid_yellow)),
            (typeof(obj_item_flashlight_glowstick_pink), LIGHT_COLOR_PINK, typeof(datum_reagent_luminescent_fluid_pink)),
        })
        {
            var s = r.In(() => (obj_item_flashlight_glowstick)Dm.@new(type, locate(7, 3, 1)));
            r.In(() =>
            {
                Assert.Equal(color, Dm.str(s.color));
                Assert.Equal(fluid, s.fuel_type);
                Assert.InRange(Dm.num(s.max_fuel), 20f, 25f);                 // rand(20, 25), :900
                Assert.Equal(Dm.num(s.max_fuel), Dm.num(s.get_fuel()));
                Assert.Equal(color, Dm.str(s.light_color));
            });
        }
        r.AssertNoRuntimes();
    }
}
