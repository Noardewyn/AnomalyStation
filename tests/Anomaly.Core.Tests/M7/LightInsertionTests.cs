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
/// M7 slice T3 (D164): a tube or bulb put into a light fixture by hand (light.dm:385-416, /obj/machinery/light/item_interaction).
/// The light items (/obj/item/light, light_items.dm) are another slice's: the type here is the generated one, inert, and a fixture only
/// reads its status, switchcount and brightness, so these tests hold with it inert and with it live.
/// </summary>
public class LightInsertionTests
{
    private const int Second = 20;
    private const string Left = "icon-x=16;icon-y=16;left=1;button=left";

    private static (AudioRoom R, mob_living_carbon_human Bob, obj_machinery_light Lamp, ChatCapture Chat) Build(ulong seed)
    {
        obj_machinery_light lamp = null;
        var r = new AudioRoom(13, 10, seed: seed, mapExtras: w =>
        {
            lamp = Dm.@new<obj_machinery_light_directional_south>(locate(4, 2, 1));
            lamp.allow_break_on_init = FALSE;
        });
        mob_living_carbon_human bob = r.Join(4, 3, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        r.Step(Second);
        r.In(() =>
        {
            if (!Dm.eq(lamp.status, LIGHT_OK))
                Waiting.NoSleep(lamp.fix());
        });
        return (r, bob, lamp, chat);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        foreach (object held in Dm.each(who.held_items.Copy()))
        {
            if (held is obj_item old)
                qdel(old);
        }
        T item = Dm.@new<T>(get_turf(who));
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(item, merge_stacks: FALSE))), "the item reaches a hand");
        return item;
    });

    private static void Click(AudioRoom r, mob who, atom target)
    {
        r.Command(() => ClientSeam.Click(who.client, target, Left));
        r.Step(Second);
    }

    private static void Empty(AudioRoom r, obj_machinery_light lamp) => r.In(() =>
    {
        Waiting.NoSleep(lamp.drop_light_tube());
        foreach (obj_item_light dropped in Dm.each(get_turf(lamp).contents).OfType<obj_item_light>().ToList())
            qdel(dropped);
    });

    [Fact]
    public void ATubePutIntoAnEmptyFixtureLightsItAndTheTubeIsConsumed()
    {
        var (r, bob, lamp, chat) = Build(8301);
        Empty(r, lamp);
        r.In(() =>
        {
            Assert.Equal(LIGHT_EMPTY, Dm.num(lamp.status));
            Assert.False(Dm.truthy(lamp.on));
        });
        var tube = Hold<obj_item_light_tube>(r, bob);
        Click(r, bob, lamp);
        r.In(() =>
        {
            Assert.Equal(LIGHT_OK, Dm.num(lamp.status));
            Assert.True(Dm.truthy(lamp.on), "the new tube lights the fixture (on = has_power())");
            Assert.Equal(8f, Dm.num(lamp.brightness)); // the tube's brightness (light_items.dm:59)
            Assert.True(QDELETED(tube));
            Assert.Null(bob.get_active_held_item());
            Assert.True(Dm.truthy(lamp.light_on));
        });
        Assert.Contains(chat.Html, h => h.Contains("You insert", StringComparison.Ordinal));
        Assert.DoesNotContain(chat.Html, h => h.Contains("You replace", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABurnedTubeIsReplacedAndTheOldOneComesBackInTheHand()
    {
        var (r, bob, lamp, chat) = Build(8302);
        r.In(() =>
        {
            Waiting.NoSleep(lamp.burn_out());
            Assert.Equal(LIGHT_BURNED, Dm.num(lamp.status));
            lamp.switchcount = 7;
        });
        var tube = Hold<obj_item_light_tube>(r, bob);
        Click(r, bob, lamp);
        r.In(() =>
        {
            Assert.Equal(LIGHT_OK, Dm.num(lamp.status));
            Assert.True(Dm.truthy(lamp.on));
            Assert.Equal(1f, Dm.num(lamp.switchcount)); // the new tube's 0 (drop_light_tube zeroed the fixture's), counted once by the update that lit it
            // the burned tube went to the hand (drop_light_tube puts it in the active hand), keeping the fixture's switchcount
            var old = Assert.IsAssignableFrom<obj_item_light_tube>(bob.get_active_held_item());
            Assert.NotSame(tube, old);
            Assert.Equal(LIGHT_BURNED, Dm.num(old.status));
            Assert.Equal(7f, Dm.num(old.switchcount));
        });
        Assert.Contains(chat.Html, h => h.Contains("You replace", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWorkingTubeAlreadyInsertedAndTheWrongFittingAreRefusedAndTheItemStaysInTheHand()
    {
        var (r, bob, lamp, chat) = Build(8303);
        var tube = Hold<obj_item_light_tube>(r, bob);
        Click(r, bob, lamp); // status LIGHT_OK
        Assert.Contains(chat.Html, h => h.Contains("There is a tube already inserted!", StringComparison.Ordinal));
        r.In(() =>
        {
            Assert.Same(tube, bob.get_active_held_item());
            Assert.False(QDELETED(tube));
        });

        chat.Html.Clear();
        Empty(r, lamp);
        var bulb = Hold<obj_item_light_bulb>(r, bob);
        Click(r, bob, lamp);
        Assert.Contains(chat.Html, h => h.Contains("This type of light requires a tube!", StringComparison.Ordinal));
        r.In(() =>
        {
            Assert.Equal(LIGHT_EMPTY, Dm.num(lamp.status));
            Assert.Same(bulb, bob.get_active_held_item());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABrokenTubeFitsButGivesNoLight()
    {
        var (r, bob, lamp, _) = Build(8304);
        Empty(r, lamp);
        Hold<obj_item_light_tube_broken>(r, bob);
        Click(r, bob, lamp);
        r.In(() =>
        {
            Assert.Equal(LIGHT_BROKEN, Dm.num(lamp.status));
            Assert.False(Dm.truthy(lamp.on)); // set_on-style rule: update() turns a non-OK fixture off
        });
        r.AssertNoRuntimes();
    }
}
