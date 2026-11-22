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
/// Gaps package MD2 (D333): the morgue and the crematorium in a booted toy room with Bob as a player: the morgue's tray sliding out
/// and back in with what lies on it, its lights (empty, things, a dead body, a revivable one) and the revivable's beep, the beeper
/// toggle, a body bag's tag as its label, the cold inside; the crematorium burning its tray's load to ash at its button.
/// </summary>
public class MortuaryMD2Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static void Use(AudioRoom r, mob who, atom target)
    {
        r.Step(Second);
        Click(r, who, target);
    }

    [Fact]
    public void The_morgue_slides_its_tray_out_and_in_and_lights_by_what_it_holds()
    {
        var (r, feed, bob) = Room(9801);
        var morgue = Place<obj_structure_bodycontainer_morgue>(r, 4, 4); // faces east: its tray comes out on (5, 4)
        r.In(() => Waiting.NoSleep(bob.Move(locate(4, 5, 1), NORTH)));
        var tray = r.In(() => morgue.connected_value);
        Assert.IsType<obj_structure_tray_m_tray>(tray);
        r.In(() =>
        {
            // the tray keeps tg's defaults (its class is hand-written: Hand mode drops the generated constructor)
            Assert.Equal("icons/obj/structures.dmi", Dm.str(tray.icon)); // the morgue tray's own; the tray's base gives the integrity
            Assert.Equal(350f, Dm.num(tray.max_integrity));
            Assert.True(Dm.truthy(tray.anchored));
        });
        Assert.Same(morgue, r.In(() => tray.loc));
        Assert.Equal("morgue1", r.In(() => Dm.str(morgue.icon_state)));

        // open: the tray slides out east
        Use(r, bob, morgue);
        Assert.Same(r.In(() => locate(5, 4, 1)), r.In(() => tray.loc));
        Assert.Equal("morgue0", r.In(() => Dm.str(morgue.icon_state)));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("roll.ogg", StringComparison.Ordinal));

        // a toolbox on the tray: closed, the yellow light
        var box = r.In(() => Dm.@new<obj_item_storage_toolbox>(locate(5, 4, 1)));
        Use(r, bob, tray);
        Assert.Same(morgue, r.In(() => box.loc));
        Assert.Same(morgue, r.In(() => tray.loc));
        Assert.Equal("morgue3", r.In(() => Dm.str(morgue.icon_state)));

        // a dead body with no ghost: the red light
        Use(r, bob, morgue);
        var body = r.Dummy(5, 4);
        r.In(() => Waiting.NoSleep(body.death()));
        Use(r, bob, tray);
        Assert.Same(morgue, r.In(() => body.loc));
        r.Step(3 * Second);
        Assert.Equal("morgue2", r.In(() => Dm.str(morgue.icon_state)));

        // a living one counts as revivable: the green light and the beep
        Use(r, bob, morgue);
        Assert.Same(r.In(() => locate(5, 4, 1)), r.In(() => body.loc));
        var living = r.Dummy(5, 4);
        r.In(() => Waiting.NoSleep(living.Paralyze(100 * SECONDS)));
        Use(r, bob, tray);
        r.Step(3 * Second);
        Assert.Equal("morgue4", r.In(() => Dm.str(morgue.icon_state)));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("empty_alarm.ogg", StringComparison.Ordinal));
        Assert.True(r.In(() => morgue.internal_air.temperature) < T20C, "the morgue cools its air while closed");

        // alt-click turns the beeper off
        r.In(() => Waiting.NoSleep(morgue.click_alt(bob)));
        Assert.Equal(0f, r.In(() => morgue.beeper));
        Assert.True(feed.Any("You turn the speaker function off."), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_tagged_body_bag_labels_the_morgue_and_the_crematorium_burns_its_load_at_its_button()
    {
        var (r, feed, bob) = Room(9802);
        var morgue = Place<obj_structure_bodycontainer_morgue>(r, 4, 4);
        var bag = r.In(() => Dm.@new<obj_structure_closet_body_bag>(locate(5, 4, 1)));
        r.In(() => bag.tag_name = "John Doe");
        r.In(() => Waiting.NoSleep(morgue.open()));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bag.forceMove(locate(5, 4, 1))));
        r.In(() => Waiting.NoSleep(morgue.close()));
        Assert.Equal("morgue - (John Doe)", r.In(() => Dm.str(morgue.name)));
        Assert.Contains(r.In(() => Dm.each(morgue.overlays).Select(o => o is IDmAppearance m ? Dm.str(m.icon_state) : Dm.str(o)).ToList()), o => o == "morgue_label");

        // the crematorium south of (8, 6): its tray comes out on (8, 5)
        var crematorium = Place<obj_structure_bodycontainer_crematorium>(r, 8, 6);
        Assert.Contains(crematorium, r.In(() => Dm.each(GLOB.crematoriums).ToList()));
        r.In(() => Waiting.NoSleep(crematorium.open()));
        r.Step(Second);
        var victim = r.Dummy(8, 5);
        var junk = r.In(() => Dm.@new<obj_item_storage_toolbox>(locate(8, 5, 1)));
        r.In(() => Waiting.NoSleep(crematorium.close()));
        Assert.Equal("crema2", r.In(() => Dm.str(crematorium.icon_state)));

        var button = r.In(() => Dm.@new<obj_item_assembly_control_crematorium>(locate(9, 6, 1)));
        r.In(() => button.id = crematorium.id);
        r.In(() =>
        {
            World.Current.usr = bob;
            Waiting.NoSleep(button.activate(bob));
        });
        Assert.True(r.In(() => QDELETED(victim)), "the victim burned");
        Assert.True(r.In(() => QDELETED(junk)), "the junk burned");
        Assert.Equal(1f, r.In(() => crematorium.locked));
        Assert.Equal("crema_active", r.In(() => Dm.str(crematorium.icon_state)));
        Assert.True(feed.Any("You hear a roar as the crematorium activates."), Diag(feed, r));

        r.Step(4 * Second);
        Assert.Equal(0f, r.In(() => crematorium.locked));
        Assert.NotNull(r.In(() => Dm.locate<obj_effect_decal_cleanable_ash>(crematorium)));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("ding.ogg", StringComparison.Ordinal));
    }
}
