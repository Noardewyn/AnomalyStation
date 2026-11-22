using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// DV32 (the plasma fire hitch, 2026-10-18): a turf's gas overlays (tg's update_visuals puts GAS_OVERLAYS in its vis_contents) are not
/// part of the turf's replicated appearance. The gas reaches the client as the cell's gas levels and is drawn as a haze; in the turf's
/// picture every level a spreading gas passed through was a new floor picture, and the client rebuilt the floor's chunk for each.
/// </summary>
public class GasTurfAppearanceTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const int X = 46, Y = 122; // the arrivals corridor (FlashlightGameplayTests' place)

    [MetaServerFact]
    public void Plasma_on_a_floor_reaches_the_client_as_gas_and_leaves_the_floor_picture_alone()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 19, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        w.Run(Second);
        ReplicaWorld replica = p.Session.Replica;
        TableRef before = replica.TurfAppearance(X + 2, Y, 1);
        var changed = new HashSet<(int, int, int)>();
        replica.CellsChanged += cells => { foreach (var c in cells) changed.Add(c); };

        w.Host.EnqueueConsoleLine($"air add plasma 200 {X + 2} {Y} 1");
        w.Run(3 * Second); // the plasma spreads over the corridor's turfs, level after level
        using (w.World.Enter())
            Assert.Contains(Anomaly.Core.DmShim.Dm.each(((turf)Builtins.locate(X + 2, Y, 1)).vis_contents), o => o is obj_effect_overlay_gas); // tg's overlay is there on the server

        Assert.Contains(replica.GasAt(X + 2, Y, 1), g => replica.GasKinds[g.Kind - 1] == "plasma");
        output.WriteLine($"cells changed while the plasma spread: {changed.Count}");
        Assert.Equal(before, replica.TurfAppearance(X + 2, Y, 1));
        Assert.DoesNotContain((X + 2, Y, 1), changed);
    }
}
