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
/// Gaps package DC1 (D333, the station's wall clocks) in a booted toy room. The digital clock processes on its own subsystem every
/// second and shows the round's hours and minutes as tg's digit overlays at tg's offsets, lit blue; a minute later its minute digit
/// turns. An emag scrambles it (with sparks) until a multitool resets it; a broken clock goes dark and a welder repairs it; a
/// right-click wrench takes it off the wall as its frame.
/// </summary>
public class DigitalClockDC1Tests
{
    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    // the digits as (icon_state, pixel_w)
    private static List<(string State, float W)> Digits(AudioRoom r, atom a) => r.In(() =>
        Dm.each(a.overlays).OfType<IDmAppearance>().Where(o => o.icon_state != null && o.icon_state.StartsWith("+", StringComparison.Ordinal))
            .Select(o => (o.icon_state, o.pixel_w)).ToList());

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() =>
        {
            Waiting.NoSleep(who.drop_all_held_items());
            Waiting.NoSleep(who.put_in_active_hand(item));
        });
        return item;
    }

    [Fact]
    public void The_clock_shows_the_round_time_and_turns_its_minute()
    {
        var r = new AudioRoom(13, 9, seed: 9981);
        Feed feed = Listen(r);
        var clock = Place<obj_machinery_digital_clock>(r, 6, 4);
        r.Step(2 * Second);
        Assert.False(r.In(() => InertSeam.IsInert(clock)));
        Assert.NotNull(r.In(() => SSdigital_clock));
        Assert.True(r.In(() => Dm.@in(clock, SSdigital_clock.processing)), "the clock does not process on its subsystem");

        var digits = Digits(r, clock);
        Assert.Contains(("+separator", 0f), digits);
        string mm = r.In(() => round_timestamp(format: "mm"));
        Assert.Contains(("+" + mm[1], 0f), digits);
        Assert.Contains(("+" + mm[0], -4f), digits);
        Assert.Contains(digits, d => d.W == -10f);
        Assert.Contains(digits, d => d.W == -14f);
        Assert.Equal(5, digits.Count);
        r.In(() =>
        {
            Assert.Equal(1.5f, Dm.num(clock.light_range));
            Assert.Equal(LIGHT_COLOR_BLUE, Dm.str(clock.light_color));
        });

        // a minute later the minute digit has turned
        string before = r.In(() => round_timestamp(format: "mm"));
        r.Step(61 * Second);
        string after = r.In(() => round_timestamp(format: "mm"));
        Assert.NotEqual(before, after);
        Assert.Contains(("+" + after[1], 0f), Digits(r, clock));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_emag_scrambles_it_a_multitool_resets_it_a_welder_repairs_it_and_a_wrench_takes_it_down()
    {
        var r = new AudioRoom(13, 9, seed: 9982);
        Feed feed = Listen(r);
        var bob = r.Join(5, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var clock = Place<obj_machinery_digital_clock>(r, 6, 4);
        r.Step(2 * Second);

        // the emag: sparks, and the clock stays scrambled
        r.In(() => Assert.Equal(TRUE, Waiting.NoSleep(clock.emag_act(bob, Dm.@new<obj_item_card_emag>(bob.loc)))));
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(clock.obj_flags, EMAGGED))));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").Contains("sparks", StringComparison.Ordinal));
        r.In(() => Assert.Equal(FALSE, Waiting.NoSleep(clock.emag_act(bob, null))));
        r.Step(2 * Second);
        Assert.Equal(5, Digits(r, clock).Count);

        // the multitool resets it in six seconds
        var multitool = Hold<obj_item_multitool>(r, bob);
        r.In(() => Waiting.Detach(clock.multitool_act(bob, multitool)));
        r.Step(7 * Second);
        Assert.False(r.In(() => Dm.truthy(Dm.bitand(clock.obj_flags, EMAGGED))), Diag(feed, r));
        Assert.True(feed.Any("balloon: reset"), Diag(feed, r));

        // broken, it goes dark; the welder repairs the display
        r.In(() =>
        {
            clock.atom_integrity = 10f;
            Waiting.NoSleep(clock.set_machine_stat(Dm.bitor(clock.machine_stat, BROKEN)));
            clock.update_appearance();
        });
        r.Step(2 * Second);
        Assert.Empty(Digits(r, clock));
        Assert.Equal(0f, r.In(() => Dm.num(clock.light_range)));
        var welder = Hold<obj_item_weldingtool_largetank>(r, bob);
        r.In(() => welder.switched_on(bob));
        r.In(() => Waiting.Detach(clock.welder_act(bob, welder)));
        r.Step(5 * Second);
        Assert.Equal(r.In(() => clock.max_integrity), r.In(() => Dm.num(clock.atom_integrity)));
        Assert.False(r.In(() => Dm.truthy(Dm.bitand(clock.machine_stat, BROKEN))));
        r.Step(2 * Second);
        Assert.Equal(5, Digits(r, clock).Count);
        Assert.True(feed.Any("balloon: repaired"), Diag(feed, r));

        // the right-click wrench takes it down as its frame
        r.In(() => welder.switched_off());
        var wrench = Hold<obj_item_wrench>(r, bob);
        r.In(() => Waiting.Detach(clock.wrench_act_secondary(bob, wrench)));
        r.Step(7 * Second);
        Assert.True(r.In(() => QDELETED(clock)), Diag(feed, r));
        Assert.True(r.In(() => Dm.each<obj_item_wallframe_digital_clock>().Any()), "no clock frame dropped");
        r.AssertNoRuntimes();
    }
}
