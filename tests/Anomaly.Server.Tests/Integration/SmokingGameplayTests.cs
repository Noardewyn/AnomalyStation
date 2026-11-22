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
/// Gaps package J3 (D298) through the real server on MetaStation: a player puts a cigarette in the mouth with E, flips a zippo open with Z (tg's "zippo-on" and the flame's light reach the client),
/// clicks himself with it and the cigarette lights: the client sees "cigon" on the body and the smoker's own light, and the burning cigarette feeds him nicotine.
/// </summary>
public class SmokingGameplayTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public SmokingGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static List<string> BodyStates(ReplicaWorld replica)
    {
        Assert.True(replica.TryGet(replica.OwnEntity, out ReplicaEntity own));
        return CrewOutfitTests.Icons(replica, own.State.Appearance).Select(i => i.State ?? "").ToList();
    }

    [MetaServerFact]
    public void A_player_lights_a_cigarette_in_his_mouth_with_a_zippo_and_the_client_sees_it_lit()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 15, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int bootRuntimes = w.World.RuntimeErrorCount;

        // the cigarette into the mouth
        w.Host.EnqueueConsoleLine("give Assistant /obj/item/cigarette/robust");
        w.Run(5);
        var cig = In(w, () => me.get_active_held_item()) as obj_item_cigarette_robust;
        Assert.NotNull(cig);
        p.Session.Keybinding(KeybindingCode.QuickEquip);
        w.Run(Second);
        Assert.Same(cig, In(w, () => me.wear_mask));
        List<string> unlit = BodyStates(replica);
        _output.WriteLine("unlit: " + string.Join(", ", unlit));
        Assert.Contains("cigoff", unlit);

        // the zippo: Z flips it open, the client sees the open zippo with its flame's light on the holder
        w.Host.EnqueueConsoleLine("give Assistant /obj/item/lighter");
        w.Run(5);
        var zippo = In(w, () => me.get_active_held_item()) as obj_item_lighter;
        Assert.NotNull(zippo);
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(zippo.lit)));
        Assert.Equal("zippo-on", In(w, () => zippo.icon_state));
        Assert.True(replica.TryGet(replica.OwnEntity, out ReplicaEntity own));
        Assert.True(own.Light.On, "the player carries the zippo's flame");

        // a click on yourself with the open zippo lights the cigarette in your mouth
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, me.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(cig.lit)));
        List<string> lit = BodyStates(replica);
        _output.WriteLine("lit: " + string.Join(", ", lit));
        Assert.Contains("cigon", lit);
        Assert.DoesNotContain("cigoff", lit);

        // the smoke reaches the smoker
        w.Run(15 * Second);
        Assert.True(In(w, () => Dm.num(((datum_reagents)me.reagents).get_reagent_amount(typeof(datum_reagent_drug_nicotine)))) > 0, "no nicotine in the smoker");

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
