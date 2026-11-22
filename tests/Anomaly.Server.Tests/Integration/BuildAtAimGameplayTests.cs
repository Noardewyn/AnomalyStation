using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner (2026-10-18), DV65: what a player builds from a stack goes where the crosshair aims, not under their feet. The
/// client's hover of the floor tile ahead reaches the server, and the stool built from iron sheets stands on that tile, also
/// after the pointer left the map for the recipe window; a tile out of reach builds on the open neighbour toward it.
/// </summary>
public class BuildAtAimGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const int X = 46, Y = 122; // the arrivals corridor, open floor east and west

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static int StoolsAt(RealHeadlessWorld w, int x, int y, int z) =>
        In(w, () => Dm.each_cast<atom>(locate(x, y, z).contents).OfType<obj_structure_chair_stool>().Count());

    [MetaServerFact]
    public void A_stool_is_built_on_the_tile_the_crosshair_aims_at()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 21, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z);
        In(w, () => Waiting.NoSleep(me.forceMove(locate(X, Y, z))));
        w.Run(Second);
        var iron = In(w, () => (obj_item_stack_sheet_iron)Dm.@new(typeof(obj_item_stack_sheet_iron), me.loc, 20f, FALSE));
        In(w, () => Waiting.NoSleep(me.put_in_hands(iron, merge_stacks: FALSE)));
        datum_stack_recipe stool = In(w, () => Dm.each(iron.recipes).OfType<datum_stack_recipe>().Single(r => r.title == "stool"));
        int before = StoolsAt(w, X + 1, Y, z);

        // the crosshair on the floor east of the player, then on the recipe window (nothing on the map)
        Assert.True(p.Session.Hover(new ClickTarget(TargetKind.Turf, 0, X + 1, Y, z), 100.0));
        w.Run(3);
        Assert.True(p.Session.Hover(ClickTarget.None, 101.0));
        w.Run(3);
        Assert.Equal(TRUE, Dm.num(In(w, () => Waiting.NoSleep(iron.make_item(me, stool, 1)))));
        w.Run(2);
        output.WriteLine($"stools east {StoolsAt(w, X + 1, Y, z)}, under the player {StoolsAt(w, X, Y, z)}");
        Assert.Equal(before + 1, StoolsAt(w, X + 1, Y, z));

        // three tiles away is out of reach: the open neighbour toward it
        int west = StoolsAt(w, X - 1, Y, z);
        Assert.True(p.Session.Hover(new ClickTarget(TargetKind.Turf, 0, X - 3, Y, z), 102.0));
        w.Run(3);
        Assert.Equal(TRUE, Dm.num(In(w, () => Waiting.NoSleep(iron.make_item(me, stool, 1)))));
        w.Run(2);
        Assert.Equal(west + 1, StoolsAt(w, X - 1, Y, z));
    }
}
