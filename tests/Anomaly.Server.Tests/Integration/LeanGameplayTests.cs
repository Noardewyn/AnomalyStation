using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Leaning on MetaStation (gaps L1): a crewman facing a wall drags his health doll onto it (DV417: the drop of his own mob, as the
/// client sends it), turns his back to it and leans against it (DV421); looking around keeps the lean, a step ends it.
/// </summary>
public class LeanGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f) { using (w.World.Enter()) return f(); }

    [MetaServerFact]
    public void A_crewman_leans_on_a_wall_by_dragging_himself_onto_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 65, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        // an open floor with a wall to its north, near the arrivals
        turf_closed_wall wall = In(w, () =>
        {
            foreach (turf_open floor in Dm.each<turf>(orange(12, me)).OfType<turf_open>())
            {
                if (Dm.truthy(floor.density) || Dm.truthy(Waiting.NoSleep(floor.is_blocked_turf())))
                    continue;
                if (get_step(floor, NORTH) is turf_closed_wall north)
                {
                    Waiting.NoSleep(me.forceMove(floor));
                    Waiting.NoSleep(me.setDir(NORTH)); // facing the wall: the view turns the mob (DV15), the drop turns it around (DV421)
                    return north;
                }
            }
            return null;
        });
        Assert.NotNull(wall);
        w.Run(Second);

        var release = new DragRelease(DragGestureGameplayTests.Entity(In(w, () => me.dm_id)), new ClickTarget(TargetKind.Turf, 0, In(w, () => (int)wall.x), In(w, () => (int)wall.y), In(w, () => (int)wall.z)), ClickButton.Left, ClickModifiers.None, 5);
        Assert.True(alice.Session.Drop(release));
        w.Run(Second);
        Assert.True(In(w, () => HAS_TRAIT_FROM(me, TRAIT_UNDENSE, LEANING_TRAIT)), "leaning on the wall: component " + In(w, () => wall.GetComponent(typeof(datum_component_leanable)) != null) + " dir " + In(w, () => me.dir) + " at " + In(w, () => me.x + "," + me.y) + " wall " + In(w, () => wall.x + "," + wall.y) + " drag_start " + In(w, () => me.client.drag_start));
        Assert.Equal(11f, In(w, () => Dm.num(me.pixel_y)));
        Assert.Equal(SOUTH, In(w, () => me.dir)); // his back to the wall
        w.Run(2 * Second); // the view still faces the wall: the lean holds
        Assert.True(In(w, () => HAS_TRAIT_FROM(me, TRAIT_UNDENSE, LEANING_TRAIT)), "still leaning");

        // a turn ends it (tg's lean_dir_changed)
        In(w, () => { Waiting.NoSleep(me.setDir(EAST)); return 0; });
        w.Run(2);
        Assert.False(In(w, () => HAS_TRAIT(me, TRAIT_UNDENSE)), "no longer leaning");
    }
}
