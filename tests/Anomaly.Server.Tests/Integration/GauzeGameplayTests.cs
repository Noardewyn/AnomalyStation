using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M6 slice 6 (gauze) through the real server on MetaStation with a headless client, in the corridor east of the wall at
/// (42,155): the player holds tg's medical gauze and clicks a person with a cut left arm aiming at the arm; the client hears
/// tg's bandage sounds, the arm is wrapped with one gauze off the stack, and the client sees tg's bandage overlay on the
/// person's arm.
/// </summary>
public class GauzeGameplayTests
{
    private const int Second = 20;
    private const int X = 43, Y = 155;

    private readonly ITestOutputHelper _output;

    public GauzeGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static turf At(int x, int y) => Builtins.locate(x, y, 1);

    [MetaServerFact]
    public void The_player_wraps_a_cut_arm_with_gauze_and_the_client_sees_the_bandage()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 67, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        int bootRuntimes = w.World.RuntimeErrorCount;

        (mob_living_carbon_human bob, obj_item_stack_medical_wrap_gauze stack, obj_item_bodypart arm) = In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(At(X, Y)));
            var b = Dm.@new<mob_living_carbon_human>(At(X + 1, Y));
            var g = Dm.@new<obj_item_stack_medical_wrap_gauze>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(g))));
            var limb = (obj_item_bodypart)b.get_bodypart(BODY_ZONE_L_ARM);
            Assert.NotNull(Waiting.NoSleep(b.cause_wound_of_type_and_severity(WOUND_SLASH, limb, WOUND_SEVERITY_MODERATE)));
            return (b, g, limb);
        });
        w.Run(Second);
        Assert.True(replica.TryGet(bob.dm_id, out _), "the client sees Bob");

        // the click on Bob's left arm (the body zone under the point, D-aim)
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, bob.dm_id, 0, 0, 0, 22, 15, AimZone.LeftArm), ClickButton.Left, ClickModifiers.None));
        w.Run(4 * Second); // other_delay 2 s with a wound that takes gauze

        obj_item_stack_medical_wrap_gauze wrapped = In(w, () => LAZYACCESS(arm.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap_gauze);
        Assert.NotNull(wrapped);
        Assert.Equal(5f, In(w, () => Dm.num(stack.amount)));
        Assert.Contains(heard, h => (h.File ?? "").StartsWith("sound/items/gauze/bandage_begin", StringComparison.Ordinal));
        Assert.Contains(heard, h => (h.File ?? "").StartsWith("sound/items/gauze/bandage_end", StringComparison.Ordinal));
        Assert.True(replica.TryGet(bob.dm_id, out ReplicaEntity seen));
        List<(string Icon, string State, float Layer)> looks = CrewOutfitTests.Icons(replica, seen.State.Appearance);
        _output.WriteLine(string.Join(", ", looks.Select(l => l.Icon + ":" + l.State)));
        Assert.Contains(looks, l => l.Icon == "icons/mob/human/bandage.dmi" && l.State != null && l.State.StartsWith("l_arm", StringComparison.Ordinal));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
