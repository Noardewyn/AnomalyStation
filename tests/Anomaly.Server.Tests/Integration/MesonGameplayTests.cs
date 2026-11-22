using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M7 slice T3 (D164, protocol version 31, DV81): meson goggles on MetaStation through the real server. An engineer takes optical mesons
/// and puts them on (E): his client gets tg's sight (SEE_TURFS) and the glasses' lighting floor (the colour cutoffs 5, 15, 5 of a percent),
/// and the turfs around him are the ones the client draws through the walls (walls and floors within seven tiles); the light green tint is
/// off until he toggles it (tg's right alt-click), then the game plane carries the colour matrix that turns white light green; taking
/// the goggles off puts all of it back.
/// </summary>
public class MesonGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static RealHeadlessWorld Meta(int seed) => new(new LoopbackOptions { Seed = (ulong)seed, TimeoutSteps = 12000 },
        MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);

    [MetaServerFact]
    public void An_engineer_puts_on_mesons_and_his_client_gets_the_sight_the_lighting_floor_and_the_tint_and_loses_them_with_the_goggles()
    {
        using RealHeadlessWorld w = Meta(72);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        In(w, () => Waiting.NoSleep(me.forceMove(locate(46, 122, 1))));
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        Assert.Equal(VisionState.Normal, replica.OwnVision);
        Assert.Null(ViewBlur.ColorMatrix(replica.GamePlaneFilters));

        w.Host.EnqueueConsoleLine("give Engineer /obj/item/clothing/glasses/meson");
        w.Run(5);
        var meson = In(w, () => me.get_active_held_item() as obj_item_clothing_glasses_meson);
        Assert.NotNull(meson);
        Assert.Equal(VisionState.Normal, replica.OwnVision); // in the hand they do nothing
        p.Session.Keybinding(KeybindingCode.QuickEquip); // E: the goggles to the eyes
        w.Run(Second);
        Assert.Same(meson, In(w, () => me.glasses));

        // the sight and the floor the client gets (tg: SEE_TURFS from TRAIT_MESON_VISION, color_cutoffs list(5, 15, 5))
        VisionState vision = replica.OwnVision;
        output.WriteLine(vision.ToString());
        Assert.True(vision.SeesTurfs);
        Assert.Equal(In(w, () => (int)me.sight), vision.Sight);
        Assert.Equal(0f, vision.LightingCutoff, 3);
        (float r, float g, float b) = vision.LightFloor;
        Assert.Equal((0.05f, 0.15f, 0.05f), (r, g, b), new TupleTolerance());
        Assert.True(In(w, () => HAS_TRAIT(me, TRAIT_MESON_VISION) && HAS_TRAIT(me, TRAIT_MADNESS_IMMUNE)));

        // the turfs the client draws through the walls: walls and floors round him, more than the ones he has in sight
        var ownEntity = replica.Entities.First(e => e.Id == replica.OwnEntity);
        int x = ownEntity.State.X, y = ownEntity.State.Y, z = ownEntity.State.Z;
        MesonCell[] plan = MesonView.Plan(replica, x, y, z);
        int walls = plan.Count(c => c == MesonCell.Wall), floors = plan.Count(c => c == MesonCell.Floor);
        output.WriteLine($"round ({x}, {y}) the client draws {walls} walls and {floors} floors of {plan.Length} cells");
        Assert.True(walls >= 10, "walls: " + walls);
        Assert.True(floors >= 10, "floors: " + floors);
        Assert.Equal(MesonView.Radius * 2 + 1, (int)Math.Sqrt(plan.Length));
        // a wall cell of the plan is a wall on the server too, a floor cell an open turf
        In(w, () =>
        {
            for (int i = 0; i < plan.Length; i++)
            {
                int side = MesonView.Radius * 2 + 1, tx = x + i % side - MesonView.Radius, ty = y + i / side - MesonView.Radius;
                if (plan[i] == MesonCell.Wall)
                    Assert.True(CellState.IsOpaque(w.World, tx, ty, z), $"wall ({tx}, {ty}) is {locate(tx, ty, z)?.GetType().Name}");
                else if (plan[i] == MesonCell.Floor)
                    Assert.True(locate(tx, ty, z) is turf_open && !CellState.IsOpaque(w.World, tx, ty, z), $"floor ({tx}, {ty}) is {locate(tx, ty, z)?.GetType().Name}");
            }
        });

        // the tint is off until he toggles it (tg's right alt-click on the goggles, wearable_client_colour.dm:109)
        Assert.Null(ViewBlur.ColorMatrix(replica.GamePlaneFilters));
        In(w, () => meson._SendSignal(COMSIG_CLICK_ALT_SECONDARY, Dm.list(meson, me)));
        w.Run(Second);
        float[] matrix = ViewBlur.ColorMatrix(replica.GamePlaneFilters);
        Assert.NotNull(matrix);
        (r, g, b) = ViewBlur.Apply(matrix, 1f, 1f, 1f);
        output.WriteLine($"white light through the tint: {r}, {g}, {b}");
        Assert.Equal((0.8f, 1f, 0.8f), (r, g, b), new TupleTolerance());

        // taken off: the sight, the floor and the tint go
        In(w, () => Waiting.NoSleep(me.dropItemToGround(meson)));
        w.Run(Second);
        Assert.Equal(VisionState.Normal, replica.OwnVision);
        Assert.Null(ViewBlur.ColorMatrix(replica.GamePlaneFilters));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    private sealed class TupleTolerance : IEqualityComparer<(float, float, float)>
    {
        public bool Equals((float, float, float) a, (float, float, float) b) => Math.Abs(a.Item1 - b.Item1) < 0.01f && Math.Abs(a.Item2 - b.Item2) < 0.01f && Math.Abs(a.Item3 - b.Item3) < 0.01f;

        public int GetHashCode((float, float, float) t) => 0;
    }
}
