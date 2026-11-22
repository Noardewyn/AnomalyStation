using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps package P3b (D327) through the real server on MetaStation: a geneticist at the genetics lab's DNA infuser clicks it with a
/// dead mouse in hand (the mouse goes into the sample slot), a second player is shut in the chamber, the geneticist clicks the
/// machine with an empty hand: the client sees the infuser switch on, the needles prick the subject and the subject grows a rat
/// organ; the machine turns off and lets the subject out.
/// </summary>
public class DnaInfuserGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    private static turf ClearNeighbour(atom a)
        => new[] { SOUTH, NORTH, EAST, WEST }.Select(d => get_step(a, d) as turf)
            .FirstOrDefault(t => t != null && Clear(t) && Dm.truthy(Waiting.NoSleep(a.Adjacent(t))));

    private static string SeenState(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.SingleOrDefault(e => e.Id == a.dm_id);
        return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? replica.ResolveString(look.IconState) : null;
    }

    private static readonly Type[] RatOrgans =
    {
        typeof(obj_item_organ_eyes_night_vision_rat), typeof(obj_item_organ_heart_rat), typeof(obj_item_organ_stomach_rat), typeof(obj_item_organ_tongue_rat),
    };

    [MetaServerFact]
    public void A_geneticist_infuses_a_player_with_a_dead_mouse_and_the_player_grows_a_rat_organ()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 332, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player gene = w.Join("Geneticist");
        RealHeadlessWorld.Player subject = w.Join("Subject");
        var me = (mob_living_carbon_human)w.MobOf(gene);
        var them = (mob_living_carbon_human)w.MobOf(subject);
        ReplicaWorld replica = gene.Session.Replica;
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var runtimes = new List<string>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e =>
        {
            runtimes.Add(e.Message);
            sink?.Invoke(e);
        };

        // Meta's genetics lab has the station's DNA infuser
        var infuser = In(w, () => Dm.each<obj_machinery_dna_infuser>().Where(i => i.z == 1 && ClearNeighbour(i) != null).OrderBy(i => i.dm_id).FirstOrDefault());
        Assert.NotNull(infuser);
        Assert.False(In(w, () => InertSeam.IsInert(infuser)));
        output.WriteLine(In(w, () => $"infuser {infuser.x},{infuser.y}; area {(get_area(infuser) as area)?.name}"));

        // the geneticist beside it with a dead mouse in hand
        obj_item_food_deadmouse mouse = null;
        Do(w, () =>
        {
            Waiting.NoSleep(me.forceMove(ClearNeighbour(infuser)));
            mouse = Dm.@new<obj_item_food_deadmouse>(me.loc);
            Waiting.NoSleep(me.put_in_active_hand(mouse));
        });
        w.Run(Second);
        Assert.StartsWith("infuser", SeenState(replica, infuser)); // the client sees the machine

        // a click with the mouse puts it in the sample slot
        Assert.True(gene.Session.Click(On(infuser), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(mouse, In(w, () => infuser.infusing_from));
        Assert.Same(infuser, In(w, () => mouse.loc));

        // the subject shut in the chamber
        Do(w, () =>
        {
            if (!Dm.truthy(infuser.state_open))
                Waiting.NoSleep(infuser.open_machine(drop: FALSE));
            Waiting.NoSleep(infuser.close_machine(them));
        });
        w.Run(Second);
        Assert.Same(infuser, In(w, () => them.loc));
        float brute = In(w, () => Dm.num(them.get_brute_loss()));

        // an empty-handed click starts the infusion; the client sees the machine switch on
        Assert.True(gene.Session.Click(On(infuser), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Equal(1f, In(w, () => infuser.infusing));
        Assert.Equal("infuser_on", SeenState(replica, infuser));
        Assert.True(In(w, () => Dm.num(them.get_brute_loss()) + Dm.num(them.get_fire_loss())) > brute, "the needles prick the subject");

        // the infusion's four seconds: one rat organ grown, the mouse used up, the machine off and open
        w.Run(5 * Second);
        Assert.Equal(0f, In(w, () => infuser.infusing));
        Assert.Equal(1, In(w, () => RatOrgans.Count(t => them.get_organ_by_type(t) != null)));
        Assert.Null(In(w, () => infuser.infusing_from));
        Assert.NotSame(infuser, In(w, () => them.loc));
        Assert.NotEqual("infuser_on", SeenState(replica, infuser));
        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, "nothing the players did ran into a runtime: " + string.Join("\n", runtimes));
    }
}
