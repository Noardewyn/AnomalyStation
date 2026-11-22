using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// Syndicate stage G1a (D316): the emag and the doorjack sell; the emag cannot hack an airlock, the doorjack can (the door sparks,
/// opens, bolts and loses power, a charge recharges); the emag breaks a secure locker's lock, scrambles a fire alarm and loads a
/// PDA's syndicate theme; two emags held together stick into one; a real emag rigs a fake one, which then blows up.
/// </summary>
public class SyndicateEmagTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed = 81)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Held<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        T item = Dm.@new<T>(who.loc);
        Waiting.NoSleep(who.put_in_active_hand(item));
        return item;
    });

    [Fact]
    public void TheEmagsAreLiveAndSell()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_card_emag), typeof(obj_item_card_emag_doorjack), typeof(obj_item_card_emag_bluespace), typeof(obj_item_card_emag_blue),
            typeof(obj_item_card_emag_halloween), typeof(obj_item_card_emagfake),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        Assert.True(Attribute.IsDefined(typeof(obj_item_card_emag_battlecruiser), typeof(DmInertAttribute), inherit: false));
        var r = new AudioRoom(5, 5, seed: 3);
        Assert.False(r.In(() => uplink_item_hidden(Dm.@new<datum_uplink_item_device_tools_emag>())), "the emag is hidden");
        Assert.False(r.In(() => uplink_item_hidden(Dm.@new<datum_uplink_item_device_tools_doorjack>())), "the doorjack is hidden");
    }

    [Fact]
    public void TheEmagCannotHackAnAirlockButTheDoorjackOpensBoltsAndUnpowersIt()
    {
        var (r, feed, bob) = Room(81);
        var airlock = r.In(() => Dm.@new<obj_machinery_door_airlock>(locate(4, 4, 1)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(airlock.hasPower())), "the airlock has no power");

        var emag = Held<obj_item_card_emag>(r, bob);
        r.In(() => Waiting.NoSleep(emag.melee_attack_chain(bob, airlock)));
        Assert.True(feed.Any("A more specialized hacking device is required."));
        Assert.Equal(0, r.In(() => Dm.bitand(airlock.obj_flags, EMAGGED)));
        r.In(() => qdel(emag));

        var doorjack = Held<obj_item_card_emag_doorjack>(r, bob);
        r.In(() => Waiting.NoSleep(doorjack.melee_attack_chain(bob, airlock)));
        Assert.True(feed.Any("It now has 2 charges remaining."));
        Assert.Equal(2f, r.In(() => doorjack.charges));
        r.Step(2 * Second);
        Assert.NotEqual(0, r.In(() => Dm.bitand(airlock.obj_flags, EMAGGED)));
        Assert.Equal(0f, r.In(() => (float)Dm.num(airlock.density)));
        Assert.Equal(1f, r.In(() => airlock.locked));
        Assert.False(r.In(() => Dm.truthy(airlock.hasPower())), "the emagged airlock kept its power");

        // the back display counts down the charge; three minutes give it back with a beep
        var lines = r.In(() => Dm.each((DmList)Waiting.NoSleep(doorjack.examine(bob))).Select(Dm.str).ToList());
        Assert.Contains(lines, l => l.Contains("It has 2 charges remaining."));
        Assert.Contains(lines, l => l.Contains("CHARGE #1: ["));
        r.Step(181 * Second);
        Assert.Equal(3f, r.In(() => doorjack.charges));
        Assert.Equal(0, r.In(() => doorjack.charge_timers.len));
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("twobeep"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEmagBreaksALockerScramblesAFireAlarmAndLoadsAPdasSyndicateTheme()
    {
        var (r, feed, bob) = Room(82);
        var locker = r.In(() => Dm.@new<obj_structure_closet_secure_closet_engineering_chief>(locate(4, 4, 1)));
        var alarm = r.In(() => Dm.@new<obj_machinery_firealarm>(locate(3, 5, 1)));
        var pda = r.In(() => Dm.@new<obj_item_modular_computer_pda_crew_assistant>(locate(3, 3, 1)));
        r.Step(2);
        Assert.Equal(1f, r.In(() => locker.locked));
        var emag = Held<obj_item_card_emag>(r, bob);

        r.In(() => Waiting.NoSleep(emag.melee_attack_chain(bob, locker)));
        Assert.Equal(0f, r.In(() => locker.locked));
        Assert.Equal(1f, r.In(() => locker.broken));
        Assert.True(feed.Any("balloon: lock broken open"));
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("sparks"));

        r.In(() => Waiting.NoSleep(emag.melee_attack_chain(bob, alarm)));
        Assert.NotEqual(0, r.In(() => Dm.bitand(alarm.obj_flags, EMAGGED)));
        Assert.True(feed.Any("balloon: circuitry fried"));

        // a PDA must be on first
        r.In(() => Waiting.NoSleep(emag.melee_attack_chain(bob, pda)));
        Assert.True(feed.Any("balloon: turn it on first!"));
        r.In(() => Waiting.NoSleep(pda.turn_on(bob)));
        r.In(() => Waiting.NoSleep(emag.melee_attack_chain(bob, pda)));
        Assert.Equal(PDA_THEME_SYNDICATE, r.In(() => pda.device_theme));
        Assert.True(feed.Any("balloon: syndieOS loaded"));
        r.In(() => Waiting.NoSleep(emag.melee_attack_chain(bob, pda)));
        Assert.True(feed.Any("balloon: already emagged!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TwoEmagsStickTogetherAndARealEmagRigsAFakeOneThatBlowsUp()
    {
        var (r, feed, bob) = Room(83);
        var first = Held<obj_item_card_emag>(r, bob);
        var second = r.In(() =>
        {
            var e = Dm.@new<obj_item_card_emag>(bob.loc);
            Waiting.NoSleep(bob.put_in_inactive_hand(e));
            return e;
        });
        Assert.Equal(TRUE, r.In(() => Waiting.NoSleep(second.emag_act(bob, first))));
        Assert.Same(second, r.In(() => first.loc));
        Assert.Contains(first, r.In(() => Dm.each(second.vis_contents).ToList()));
        Assert.Equal("cryptographic sequencer-cryptographic sequencer", r.In(() => second.name));
        Assert.True(feed.Any("they become stuck together!"));
        r.Step(3 * Second);
        Assert.True(feed.Any("Those are never coming apart now."));

        // the real emag rigs a fake: it heats up on its next use and blows up a second later
        var fake = r.In(() => Dm.@new<obj_item_card_emagfake>(locate(5, 4, 1)));
        Assert.Equal(TRUE, r.In(() => Waiting.NoSleep(fake.emag_act(bob, second))));
        Assert.Equal("It's a card with a magnetic strip attached to some circuitry.", r.In(() => fake.desc));
        Assert.True(feed.Any("balloon: rigged to blow"));
        r.In(() => Waiting.NoSleep(fake.interact_with_atom(bob, bob)));
        Assert.True(feed.Any("begins to heat up!"));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(fake)), "the rigged fake emag did not blow up");
        Assert.True(feed.Any("explodes!"));
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("bikehorn"));
        r.AssertNoRuntimes();
    }
}
