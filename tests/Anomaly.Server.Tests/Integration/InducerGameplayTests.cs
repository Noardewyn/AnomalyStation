using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Models;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M7 power helpers slice (D164): tg's inducer (inducer.dm) through the real server on MetaStation. An engineer with an inducer
/// (tg's high-capacity battery in it) stands at a room's APC whose cell a test hook drained: the APC's channels are off and its
/// client model says so. He clicks the APC: each second of tg's do_after moves at most rating_base * rating of charge from the
/// inducer's cell into the APC's (the inducer's cell falls by what the APC gains), sparks fly and are heard, and the APC climbs
/// past tg's thresholds, switching its channels back on: his client's APC model and overlays change with the charge.
/// </summary>
public class InducerGameplayTests(ITestOutputHelper output)
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

    private static ModelCatalog Catalog()
    {
        for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "godot", "assets", "models", ModelCatalog.FileName);
            if (File.Exists(candidate))
                return ModelCatalog.Parse(File.ReadAllText(candidate));
        }
        throw new InvalidOperationException("godot/assets/models/models.json not found above the test's folder");
    }

    [MetaServerFact]
    public void An_inducer_charges_a_drained_apc_through_tgs_do_after_and_the_clients_apc_picture_follows()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 91, TimeoutSteps = 12000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        ModelCatalog catalog = Catalog();
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);

        // a station room's APC with a floor in front of it
        obj_machinery_power_apc apc = In(w, () => Dm.each<obj_machinery_power_apc>(Dm.world).OrderBy(a => a.dm_id)
            .First(a => a.area != null && Dm.eq(a.z, 1) && Dm.@in(a.area.type, GLOB.the_station_areas) && Dm.truthy(a.area.requires_power)
                && a.loc is turf_open && a.cell != null && a.terminal?.powernet != null));
        int x = In(w, () => (int)apc.x), y = In(w, () => (int)apc.y), z = In(w, () => (int)apc.z);
        output.WriteLine($"{In(w, () => Dm.str(apc.area.name))}: the APC at {x},{y}");
        In(w, () => Waiting.NoSleep(me.forceMove(locate(x, y, z))));
        var inducer = In(w, () => Dm.@new<obj_item_inducer>(me.loc));
        In(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(inducer)))));
        w.Run(4 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        ReplicaEntity Seen(atom a) => replica.Entities.FirstOrDefault(e => e.Id == a.dm_id);
        AppearanceEntry Look(atom a)
        {
            ReplicaEntity seen = Seen(a);
            return seen != null && replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look) ? look : null;
        }
        List<string> OverlaysOf(AppearanceEntry look) => look.Overlays
            .Select(o => replica.TryGetAppearance(o, out AppearanceEntry e) ? replica.ResolveString(e.IconState) : null).Where(s => s != null).ToList();
        string ModelOf(atom a)
        {
            AppearanceEntry look = Look(a);
            return look == null ? null : catalog.Find(replica.ResolveResource(look.Icon), replica.ResolveString(look.IconState), OverlaysOf(look))?.Id;
        }

        // the cell drained (the test hook leaves 1 J): the equipment and the lights off at tg's thresholds (the environment comes
        // back with the first charge the grid gives), the APC drawn as tg draws it
        In(w, () => apc.cell.charge = 1);
        w.Run(8 * Second);
        In(w, () => Assert.Equal((1f, 1f, 3f), (Dm.num(apc.equipment), apc.lighting, apc.environ)));
        string drainedModel = ModelOf(apc);
        List<string> drainedOverlays = OverlaysOf(Look(apc));
        output.WriteLine($"drained: model {drainedModel}, overlays {string.Join(" ", drainedOverlays)}");
        Assert.Contains("apco0-1", drainedOverlays); // the equipment and lights channel lights: off
        Assert.Contains("apco1-1", drainedOverlays);

        // the click: not a single transfer before the first second of do_after has passed
        (float Own, float PerSecond, float Max) start = In(w, () => (inducer.powerdevice_value.charge, apc.cell.rating_base * (float)Dm.num(apc.cell.rating), apc.cell.maxcharge));
        output.WriteLine($"the inducer's cell {start.Own} J, tg's rate {start.PerSecond} J a second, the APC's cell {start.Max} J");
        Assert.True(start.PerSecond < start.Max);
        float before = In(w, () => apc.cell.charge);
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, apc.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.True(In(w, () => Dm.truthy(inducer.recharging)), "the do_after is running");
        float early = In(w, () => apc.cell.charge) - before;
        // one APC cycle of the grid's own charging (50 kJ) can fall in this half second, depending on the Master's stagger
        Assert.True(early < start.PerSecond / 10, "no transfer yet (only the grid's own charging): " + early);
        heard.Clear();

        // the first second's transfer: the cell's rating (the APC's own charging from the grid adds a little), the inducer's cell
        // falling by what the inducer gave, sparks heard
        w.Run(Second + Second / 4);
        float gained = In(w, () => apc.cell.charge) - before;
        float spent = start.Own - In(w, () => inducer.powerdevice_value.charge);
        output.WriteLine($"gained {gained}, the inducer spent {spent}");
        Assert.Equal(start.PerSecond, spent, 0);
        Assert.InRange(gained, start.PerSecond - 1, start.PerSecond + 100000f);
        Assert.Contains(heard, e => e.File != null && e.File.StartsWith("sound/effects/sparks/", StringComparison.Ordinal));

        // 1 MJ of a 2.5 MJ cell is over 30 %: the lights and the equipment are back, and the client draws them on. The APC
        // updates its channels in its own process, one SSmachines cycle (2 s) apart, and where in that cycle the transfer falls
        // depends on the Master's first-fire stagger (master.dm:610, :621), which a new subsystem (SSlighting, M7 lumcount) shifts: wait a cycle.
        w.Run(3 * Second);
        In(w, () => Assert.Equal((3f, 3f, 3f), (Dm.num(apc.equipment), apc.lighting, apc.environ)));
        string chargedModel = ModelOf(apc);
        List<string> chargedOverlays = OverlaysOf(Look(apc));
        output.WriteLine($"charged: model {chargedModel}, overlays {string.Join(" ", chargedOverlays)}");
        Assert.Contains("apco0-3", chargedOverlays); // automatic and on again
        Assert.Contains("apco1-3", chargedOverlays);
        Assert.NotEqual(drainedOverlays, chargedOverlays);
        Assert.Contains(chargedModel, new[] { "apc_charging", "apc_not_charging", "apc_charged" });

        // the loop goes on until the APC's cell is full (used_charge is 0), then ends
        w.Run(5 * Second);
        In(w, () =>
        {
            Assert.False(Dm.truthy(inducer.recharging));
            Assert.InRange(apc.cell.charge, start.Max - 10000f, start.Max); // full, less the APC's own draw since
            Assert.True(inducer.powerdevice_value.charge < start.Own - start.PerSecond);
        });

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
