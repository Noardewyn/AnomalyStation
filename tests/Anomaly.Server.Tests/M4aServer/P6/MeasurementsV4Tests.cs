using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Server.Tests.Bots;
using Anomaly.Server.Tests.Replication;
using Anomaly.Tools.Bots;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.M4aServer.P6;

/// <summary>The bytes of version 4 (protocol.md 14.5, replication.md 14): an open backpack's private block, a running progress bar, the Thrown flag, and a fiddling soak. The numbers are printed; the assertions hold them to sane bounds.</summary>
public class MeasurementsV4Tests
{
    private readonly ITestOutputHelper _output;

    public MeasurementsV4Tests(ITestOutputHelper output) => _output = output;

    private static mob_living_carbon_human NewHuman(V2Run run, int x, int y) => run.In(() => Dm.@new<mob_living_carbon_human>(run.Turf(x, y)));

    [Fact]
    public void AnOpenBackpackAndARunningProgressBarCostWhatTheDocumentationSays()
    {
        V2Run run = V2Run.Quiet();
        SessionView me = run.Connect("Me", NewHuman(run, 4, 4));
        for (int i = 0; i < 3; i++) run.Step();
        int quiet = me.Bytes[^1].Length;

        // tg's backpack: 21 slots. show_contents puts 21 cells (the slot sprites), the closer, the three edge pieces and the items
        // (21, with their own icons) into client.screen; the toggleable layout is orient_storage's, one row of 7 per line.
        var members = new List<atom_movable>();
        run.In(() =>
        {
            for (int i = 0; i < 21 + 1 + 3 + 21; i++)
            {
                var o = Dm.@new<obj_item>(null);
                bool item = i >= 25;
                o.icon = Dm.resource(item ? "icons/obj/tools.dmi" : "icons/hud/storage.dmi");
                o.icon_state = item ? "item_" + (i % 9) : (i < 21 ? "slot" : "edge_" + (i - 21));
                o.screen_loc = "CENTER+" + (i % 7 - 3) + ",SOUTH+" + (2 + i / 7);
                me.Client.screen.Add(o);
                members.Add(o);
            }
            me.Avatar.hud_used.screen_groups[(float)HUD_GROUP_STORAGE] = Dm.list(members.Cast<object>().ToArray());
        });
        run.Step();
        int opening = me.Bytes[^1].Length;
        run.Step();
        int steady = me.Bytes[^1].Length;
        _output.WriteLine($"quiet Delta {quiet} B; the Delta that opens a 21-slot backpack (46 screen entries, {members.Select(m => m.icon_state).Distinct().Count()} distinct looks) {opening} B; the next Delta {steady} B");
        Assert.InRange(opening - quiet, 200, 2500);
        Assert.True(steady <= quiet + 2);

        // The Snapshot of a late join with it open.
        JoinMessages join = run.Replicator.BuildJoin(run.World.World.Tick, run.Inputs(me));
        _output.WriteLine($"a rejoin Snapshot with it open: {join.Snapshot.Length} B");

        // A progress bar: its icon_state changes each tick, 21 distinct states over 4 seconds of welding.
        obj_item target = run.In(() => Dm.@new<obj_item>(run.Turf(5, 4)));
        image bar = new() { icon = Dm.resource("icons/effects/progressbar.dmi"), icon_state = "prog_bar_0", loc = target, plane = 19 };
        run.In(() => me.Client.images.Add(bar));
        run.Step();
        int barAdded = me.Bytes[^1].Length;
        var sizes = new List<int>();
        for (int s = 1; s <= 20; s++)
        {
            run.In(() => bar.icon_state = "prog_bar_" + s * 5);
            run.Step();
            sizes.Add(me.Bytes[^1].Length);
        }
        run.Step();
        int idle = me.Bytes[^1].Length;
        // The same states again (a second weld): the strings and appearances are in the private tables already.
        var again = new List<int>();
        for (int s = 1; s <= 20; s++)
        {
            run.In(() => bar.icon_state = "prog_bar_" + s * 5);
            run.Step();
            again.Add(me.Bytes[^1].Length);
        }
        run.Step();
        _output.WriteLine($"a progress bar: the Delta that adds it {barAdded} B; a Delta that changes its state {sizes.Min()}..{sizes.Max()} B (first sight of a state costs its string and appearance once), {again.Min()}..{again.Max()} B for a state seen before; the idle Delta {idle} B");
        Assert.InRange(sizes.Max() - idle, 4, 80);
        // silicons SI6c: SSinstruments' ~4,500 instrument key datums made at init push the later ids past a varint byte (19 -> 21 B)
        Assert.InRange(again.Max() - idle, 4, 24);

        // The Thrown flag: one byte in a record, none in the mask's first byte.
        obj_item thrown = run.In(() => Dm.@new<obj_item>(run.Turf(6, 6)));
        run.Step();
        run.In(() => thrown.throwing = Dm.@new<datum_thrownthing>());
        for (int i = 0; i < 40 && !(me.Replica.TryGet(thrown.dm_id, out ReplicaEntity e) && e.IsThrown); i++) run.Step();
        int withFlag = me.Bytes[^1].Length;
        _output.WriteLine($"a Delta that flags one entity as thrown (the change record is its id, a mask of 2 bytes and the flags byte): {withFlag} B against {idle} B idle");
    }

    [Fact]
    public void TwentyFiddlingBotsCostWhatTheDocumentationSays()
    {
        using var rig = new BotServerRig(BotServerRig.DefaultConfig(size: 48), strict: false);
        const int bots = 20;
        for (int i = 0; i < bots; i++) rig.AddBot("Fid" + i, BotBehaviour.Fiddle, seed: (ulong)(i + 1) * 104729);
        for (int i = 0; i < 100 && rig.Bots.Any(b => b.State != BotState.Joined); i++) rig.Tick();
        Assert.All(rig.Bots, b => Assert.Equal(BotState.Joined, b.State));
        long sentBefore = rig.Bots.Sum(b => b.Stats.BytesIn), recvBefore = rig.Bots.Sum(b => b.Stats.BytesOut);
        rig.Tick(1200); // a minute
        double seconds = 60;
        double down = (rig.Bots.Sum(b => b.Stats.BytesIn) - sentBefore) / seconds / bots;
        double up = (rig.Bots.Sum(b => b.Stats.BytesOut) - recvBefore) / seconds / bots;
        long fiddles = rig.Bots.Sum(b => b.Stats.FiddleActions);
        _output.WriteLine($"{bots} fiddling bots for one minute on the 48 x 48 room: {down:0} B/s to each, {up:0} B/s from each; {fiddles} interactions; rejected {rig.Host.Sessions.Sum(s => s.ClicksRejected)}, applied hover {rig.Host.Sessions.Sum(s => s.HoversApplied)}, keys {rig.Host.Sessions.Sum(s => s.KeybindingsApplied)}; click_ms (server part) mean {(rig.Host.ClicksTimed == 0 ? 0 : rig.Host.ClickLatency.Mean / 1000.0):0.0} ms over {rig.Host.ClicksTimed} clicks");
        Assert.All(rig.Host.Sessions, s => Assert.Equal(0, s.Strikes));
        Assert.True(fiddles > bots * 100);
    }
}
