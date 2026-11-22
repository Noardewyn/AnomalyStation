using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// DV348 on a real server: Alice holds a pick and the left button on a rock: she hears the pick strike it over and over and the rock breaks in
/// tg's four seconds. Released halfway the dig stops. The press and the click go out as the client sends them, in that order.
/// </summary>
public class HoldMiningGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static (RealHeadlessWorld.Player Alice, ClickTarget Rock, int X, int Y, int Z, List<SoundEvent> Heard) Setup(RealHeadlessWorld w)
    {
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        var heard = new List<SoundEvent>();
        alice.Session.Replica.SoundsReceived += events => heard.AddRange(events);
        w.Run(10);
        (int x, int y, int z) = In(w, () =>
        {
            ADD_TRAIT(me, TRAIT_ADVANCEDTOOLUSER, "test");
            Waiting.NoSleep(me.put_in_active_hand(Dm.@new<obj_item_pickaxe>(get_turf(me))));
            foreach (int dir in new[] { EAST, WEST, NORTH, SOUTH })
                if (get_step(me, dir) is turf_open t && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density)))
                {
                    (int, int, int) cell = ((int)t.x, (int)t.y, (int)t.z); // a changed turf is a new one
                    Waiting.NoSleep(t.ChangeTurf(typeof(turf_closed_mineral_iron)));
                    return cell;
                }
            throw new Xunit.Sdk.XunitException("no open tile beside Alice");
        });
        w.Run(Second);
        return (alice, new ClickTarget(TargetKind.Turf, 0, x, y, z), x, y, z, heard);
    }

    private static void Press(RealHeadlessWorld w, RealHeadlessWorld.Player p, ClickTarget rock)
    {
        var aim = new ShotAim(0, 0, 0, 0, 0);
        p.Session.MouseDown(rock, ClickButton.Left, ClickModifiers.None, aim, w.Now);
        Assert.True(p.Session.Click(rock, ClickButton.Left, ClickModifiers.None, aim));
    }

    [Fact]
    public void Held_the_pick_strikes_the_rock_over_and_over_and_breaks_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 63, TimeoutSteps = 9000 });
        var (alice, rock, x, y, z, heard) = Setup(w);
        heard.Clear();
        Press(w, alice, rock);
        w.Run(5 * Second);
        Assert.False(In(w, () => locate(x, y, z) is turf_closed_mineral), "the rock did not break");
        Assert.InRange(heard.Count(e => e.File.StartsWith("sound/effects/pickaxe/", StringComparison.Ordinal)), 5, 8); // tg's first and last, and the strikes between
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void Released_the_dig_stops()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 64, TimeoutSteps = 9000 });
        var (alice, rock, x, y, z, _) = Setup(w);
        Press(w, alice, rock);
        w.Run(2 * Second);
        alice.Session.MouseUp(rock, ClickModifiers.None, new ShotAim(0, 0, 0, 0, 0));
        w.Run(4 * Second);
        Assert.True(In(w, () => locate(x, y, z) is turf_closed_mineral), "the rock broke with the button up");
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
