using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps package J1 (D298) through the real server on MetaStation: the captain's winter coat goes on with E and brings tg's "Toggle Hood"
/// action button (toggle_attached_clothing.dm); the client sees the coat drawn with the hood hanging down on its back (the undeployed
/// overlay, wintercoats.dm:17) and no hood on the head; a click on the button puts the hood up (the head's hood and the coat's zipped
/// "_t" picture, the hanging hood gone), and a second click takes it down again.
/// </summary>
public class JobClothingGameplayTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public JobClothingGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    // The icon states the client composes the player's body from (its appearance with the overlays and underlays).
    private static List<string> BodyStates(ReplicaWorld replica)
    {
        Assert.True(replica.TryGet(replica.OwnEntity, out ReplicaEntity own));
        return CrewOutfitTests.Icons(replica, own.State.Appearance).Select(i => i.State ?? "").ToList();
    }

    [MetaServerFact]
    public void The_hood_of_the_captains_winter_coat_comes_up_and_down_with_its_action_button_and_the_client_sees_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int bootRuntimes = w.World.RuntimeErrorCount;

        w.Host.EnqueueConsoleLine("give Assistant /obj/item/clothing/suit/hooded/wintercoat/captain");
        w.Run(5);
        var coat = In(w, () => me.get_active_held_item()) as obj_item_clothing_suit_hooded_wintercoat_captain;
        Assert.NotNull(coat);
        p.Session.Keybinding(KeybindingCode.QuickEquip);
        w.Run(Second);
        Assert.Same(coat, In(w, () => me.wear_suit));
        Assert.NotNull(In(w, () => coat.hood));
        Assert.Null(In(w, () => me.head));

        // the coat's own button: "Toggle Hood"
        datum_action toggle = In(w, () => Assert.Single(Dm.each_cast<datum_action>(coat.actions)));
        Assert.Equal("Toggle Hood", In(w, () => toggle.name));
        var button = In(w, () => toggle.viewers[me.hud_used] as atom_movable_screen_movable_action_button);
        Assert.NotNull(button);

        List<string> down = BodyStates(replica);
        _output.WriteLine("hood down: " + string.Join(", ", down));
        Assert.Contains("coatcaptain_hood", down);   // the hood hanging on the back (down_overlay_state_suffix)
        Assert.DoesNotContain("coatcaptain_t", down);
        Assert.DoesNotContain("hood_captain", down);

        // a click on the button puts the hood up: the hood on the head, the coat zipped, the hanging hood gone
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, button.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(In(w, () => coat.hood), In(w, () => me.head));
        Assert.True(In(w, () => Dm.truthy(coat.zipped)));
        List<string> up = BodyStates(replica);
        _output.WriteLine("hood up: " + string.Join(", ", up));
        Assert.Contains("coatcaptain_t", up);
        Assert.DoesNotContain("coatcaptain_hood", up);
        Assert.Contains("hood_captain", up);            // the hood on the head (winterhood/captain)
        Assert.DoesNotContain("headset", up);           // which covers the ears (HIDEEARS), as in tg

        // and a second click takes it down again
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, button.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Null(In(w, () => me.head));
        Assert.False(In(w, () => Dm.truthy(coat.zipped)));
        List<string> again = BodyStates(replica);
        Assert.Contains("coatcaptain_hood", again);
        Assert.DoesNotContain("hood_captain", again);
        Assert.Contains("headset", again);

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
