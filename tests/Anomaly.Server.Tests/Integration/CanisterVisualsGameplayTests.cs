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
/// What a canister's 3D model reads of its appearance, through the real server on MetaStation into the client's replica: the gases inside its
/// window (tg's update_window: window-base's visuals, plasma's picture by its alpha), the held tank's own picture (DV73: an invisible overlay of the
/// tank's icon, which the model's tank takes its colour from) and the open panel (a screwdriver: cell_hatch).
/// </summary>
public class CanisterVisualsGameplayTests
{
    private const int Second = 20; // ticks per second
    private const int StandX = 45, StandY = 154; // the Auxiliary Base Construction zone's plating (FurnitureGameplayTests)

    private readonly ITestOutputHelper _output;

    public CanisterVisualsGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    // The canister's overlays as the client's replica has them: each one's icon, icon state, alpha and its own overlays'.
    private static List<(string Icon, string State, byte Alpha, List<(string Icon, string State, byte Alpha)> Inner)> Overlays(ReplicaWorld replica, long id)
    {
        var list = new List<(string, string, byte, List<(string, string, byte)>)>();
        if (!replica.TryGetAppearance(replica.Entities.Single(e => e.Id == id).State.Appearance, out AppearanceEntry root))
            return list;
        foreach (TableRef r in root.Overlays)
        {
            if (!replica.TryGetAppearance(r, out AppearanceEntry o))
                continue;
            var inner = new List<(string, string, byte)>();
            foreach (TableRef v in o.Overlays)
                if (replica.TryGetAppearance(v, out AppearanceEntry i))
                    inner.Add((replica.ResolveResource(i.Icon), replica.ResolveString(i.IconState), i.Alpha));
            list.Add((replica.ResolveResource(o.Icon), replica.ResolveString(o.IconState), o.Alpha, inner));
        }
        return list;
    }

    private static void Use(RealHeadlessWorld w, RealHeadlessWorld.Player p, mob_living_carbon_human me, string tool, atom target)
    {
        using (w.World.Enter())
        {
            foreach (object held in Dm.each(me.held_items.Copy()))
                if (held is obj_item old)
                    Waiting.NoSleep(me.dropItemToGround(old));
            DevCommands.Give(me, tool);
        }
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
    }

    [MetaServerFact]
    public void A_plasma_canister_shows_its_gas_its_held_tank_and_its_open_panel()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 63, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int z = In(w, () => (int)me.z);
        using (w.World.Enter())
            Waiting.NoSleep(me.forceMove(Builtins.locate(StandX, StandY, z)));
        var canister = In(w, () => Dm.@new<obj_machinery_portable_atmospherics_canister_plasma>(Builtins.locate(StandX + 1, StandY, z)));
        w.Run(2 * Second);

        var seen = Overlays(replica, canister.dm_id);
        _output.WriteLine(string.Join(" | ", seen.Select(o => $"{o.Icon}:{o.State}@{o.Alpha}[{string.Join(",", o.Inner.Select(i => $"{i.Icon}:{i.State}@{i.Alpha}"))}]")));
        var window = seen.Single(o => o.State == "window-base");
        Assert.Contains(window.Inner, i => i.Icon == "icons/effects/atmospherics.dmi" && i.State == "plasma" && i.Alpha > 0);

        Use(w, p, me, "/obj/item/tank/internals/oxygen", canister);
        seen = Overlays(replica, canister.dm_id);
        Assert.Contains(seen, o => o.State == "can-open");
        Assert.Contains(seen, o => o.Icon == "icons/obj/canisters.dmi" && o.State == "oxygen" && o.Alpha == 0);

        Use(w, p, me, "/obj/item/screwdriver", canister);
        Assert.Contains(Overlays(replica, canister.dm_id), o => o.State == "cell_hatch");
    }
}
