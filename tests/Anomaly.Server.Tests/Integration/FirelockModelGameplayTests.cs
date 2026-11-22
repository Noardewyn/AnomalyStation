using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The 3D firelock (docs/ai/specs/models.md) slides by what the client sees of tg's firedoor (firedoor.dm): its icon state is <c>door_closing</c> (or
/// <c>door_opening</c>) while it moves and <c>door_closed</c> (<c>door_open</c>) after. The renderer starts the leaves on the moving state
/// (<see cref="Anomaly.Client.Shared.Models.ModelRules.SlidesOpen"/>), so they move with tg's animation, not after it.
/// </summary>
public class FirelockModelGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20; // ticks per second

    [Fact]
    public void A_firelock_shows_its_moving_state_while_it_closes_and_opens()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 4000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        mob_living me = w.MobOf(alice);
        w.Run(10);
        obj_machinery_door_firedoor firelock;
        using (w.World.Enter())
            firelock = Dm.@new<obj_machinery_door_firedoor>(get_step(me, 4));
        w.Run(Second);

        // What the client sees, tick by tick: the flicked state when a new flick arrives, the state when it changes.
        var seen = new List<string>();
        long flickTick = 0;
        string state = null;
        void Watch(long _)
        {
            ReplicaEntity e = alice.Session.Replica.Entities.SingleOrDefault(x => x.Id == firelock.dm_id);
            if (e == null)
                return;
            if (!e.FlickAppearance.IsNone && e.FlickTick != flickTick && alice.Session.Replica.TryGetAppearance(e.FlickAppearance, out AppearanceEntry flick))
            {
                flickTick = e.FlickTick;
                seen.Add("flick " + alice.Session.Replica.ResolveString(flick.IconState));
            }
            if (alice.Session.Replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry now) && alice.Session.Replica.ResolveString(now.IconState) is string s && s != state)
            {
                state = s;
                seen.Add("state " + s);
            }
        }

        w.Run(1, Watch);
        using (w.World.Enter())
            Waiting.Detach(firelock.close());
        w.Run(3 * Second, Watch);
        using (w.World.Enter())
            Waiting.Detach(firelock.open());
        w.Run(3 * Second, Watch);
        output.WriteLine(string.Join(" | ", seen));

        Assert.Equal(new[] { "state door_open", "state door_closing", "state door_closed", "state door_opening", "state door_open" }, seen);
        // Each state as the renderer reads it: the leaves turn toward shut on door_closing and toward open on door_opening, as tg's animation starts.
        Assert.Equal(new[] { true, false, false, true, true }, seen.Select(x => Anomaly.Client.Shared.Models.ModelRules.SlidesOpen(x["state ".Length..])));
    }
}
