using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner's M5 play test (2026-10-16): "the welder welds airlocks but not the other doors and the lockers". Through the real
/// server on MetaStation with the client's own click messages: a lit welder's left click on one of Meta's emergency closets and on
/// a crate welds them shut (closets.dm:924), a right click on one of Meta's firelocks welds it (firedoor.dm:533), and the client
/// sees each one's welded overlay.
/// </summary>
public class WeldingGameplayTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public WeldingGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    // An open, airy floor next to the thing for the welder to stand on.
    private static void StandBy(RealHeadlessWorld w, mob me, atom target)
    {
        using (w.World.Enter())
        {
            foreach (float dir in new float[] { 1, 2, 4, 8 })
            {
                turf t = Builtins.get_step(target, dir) as turf;
                if (t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density)))
                {
                    Waiting.NoSleep(me.forceMove(t));
                    break;
                }
            }
        }
        w.Run(Second);
    }

    private static bool WeldedOverlay(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == a.dm_id);
        return CrewOutfitTests.Icons(replica, seen.State.Appearance).Any(i => i.State != null && i.State.Contains("welded", StringComparison.Ordinal));
    }

    [MetaServerFact]
    public void A_lit_welder_welds_a_closet_and_a_crate_with_the_left_button_and_a_firelock_with_the_right()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 61, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z);
        using (w.World.Enter())
        {
            DevCommands.Give(me, "/obj/item/weldingtool");
            Waiting.NoSleep(((obj_item)me.get_active_held_item()).attack_self(me));
        }
        w.Run(Second);

        var closet = In(w, () => Dm.each(Dm.world).OfType<obj_structure_closet_emcloset>().First(c => c.z == z && !Dm.truthy(c.opened)));
        var crate = In(w, () => Dm.each(Dm.world).OfType<obj_structure_closet_crate>().First(c => c.z == z && !Dm.truthy(c.opened) && c.GetType() == typeof(obj_structure_closet_crate)));
        var firelock = In(w, () => Dm.each(Dm.world).OfType<obj_machinery_door_firedoor>().First(d => d.z == z && d.GetType() == typeof(obj_machinery_door_firedoor)));
        foreach ((atom target, ClickButton button) in new (atom, ClickButton)[] { (closet, ClickButton.Left), (crate, ClickButton.Left), (firelock, ClickButton.Right) })
        {
            StandBy(w, me, target);
            Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), button, ClickModifiers.None));
            w.Run(7 * Second);
            bool welded = In(w, () => Dm.truthy(target is obj_structure_closet c ? c.welded : ((obj_machinery_door)target).welded));
            _output.WriteLine($"{In(w, () => DmTypes.PathOf(target.GetType()))} at ({In(w, () => target.x)},{In(w, () => target.y)}) from ({In(w, () => me.x)},{In(w, () => me.y)}): welded {welded}, the client's overlay {WeldedOverlay(p.Session.Replica, target)}");
            Assert.True(welded, $"{target.GetType().Name} welded");
            Assert.True(WeldedOverlay(p.Session.Replica, target), $"{target.GetType().Name}: the client sees the weld");
        }
    }
}
