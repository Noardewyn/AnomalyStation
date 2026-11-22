using System.Runtime.CompilerServices;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Server.Tests.M3Maps.R4;

namespace Anomaly.Server.Tests.Integration;

/// <summary>The play test of 2026-11-14: after a round restart the server held the old round's world as well (heap 1.6 GB to 3.0 GB), and
/// the second round of each process ran out of memory. A restarted world must be garbage once the next one runs, although the server's
/// Main still holds the first round's replicator (as Program.cs does).</summary>
public class OldWorldFreedTests
{
    [Fact]
    public void TheOldWorldIsCollectedAfterARestart()
    {
        var config = new ServerConfig { Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f, DevCommands = true };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 400 }, config, strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Run(100);
        AssertRestartFreesTheOldWorld(w, () => w.Run(100));
    }

    // The live server's way: MetaStation, the lobby, a crew member who readied and played, tg's restart, the same player starting the next round.
    [MetaServerFact]
    public void TheOldMetaStationIsCollectedAfterALobbyRound()
    {
        var config = new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 200, Map = "metastation", MapsFolder = MetaServer.Folder(), DevCommands = true, Lobby = true, OpenJobs = true, MoveSpeedScale = 0.5f };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 43, TimeoutSteps = 8000 }, config, strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        StartAsCrew(w, alice);
        AssertRestartFreesTheOldWorld(w, () => StartAsCrew(w, alice));
    }

    private static void AssertRestartFreesTheOldWorld(RealHeadlessWorld w, Action nextRound)
    {
        List<(string Name, WeakReference<object> Ref)> old = Sample(w);
        IReplicator mains = w.Host.Replicator; // Program.cs's local keeps the first replicator for the whole process
        w.Host.EnqueueConsoleLine("reboot now");
        for (int i = 0; i < 4000 && !(w.Host.Restarts == 1 && !w.Host.BuildingNextWorld); i++)
            w.Run(1);
        Assert.Equal(1, w.Host.Restarts);
        Assert.False(w.Host.BuildingNextWorld);
        nextRound();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        List<string> alive = old.Where(o => o.Ref.TryGetTarget(out _)).Select(o => o.Name).ToList();
        GC.KeepAlive(mains);
        Assert.True(alive.Count == 0, $"{alive.Count} of the old round's {old.Count} sampled objects are still reachable: " + string.Join(", ", alive.Take(30)));
    }

    // The old world and two objects of each type in it, held weakly; no local of the test keeps them.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<(string, WeakReference<object>)> Sample(RealHeadlessWorld w)
    {
        var refs = new List<(string, WeakReference<object>)> { ("World", new WeakReference<object>(w.Host.World)) };
        var byType = new Dictionary<Type, int>();
        w.World.Objects.ForEachLiveInIdOrder((id, o) =>
        {
            byType.TryGetValue(o.GetType(), out int n);
            if (n < 2) refs.Add((o.GetType().Name + "#" + id, new WeakReference<object>(o)));
            byType[o.GetType()] = n + 1;
        });
        return refs;
    }

    private static void StartAsCrew(RealHeadlessWorld w, RealHeadlessWorld.Player p)
    {
        for (int i = 0; i < 400 && !(MobOf(w, p) is mob_dead_new_player); i++)
            w.Run(1);
        var windows = new TguiWindows { SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters), SendClose = window => p.Session.UiClose(window) };
        TguiWindow lobby = null;
        for (int i = 0; i < 200 && lobby == null; i++)
        {
            w.Run(1);
            windows.Sync(p.Session.Replica.UiWindows, 1280, 720, 1);
            lobby = windows.Open.FirstOrDefault(x => x.Interface == Limits.LobbyUiInterface);
        }
        Assert.NotNull(lobby);
        lobby.Context.Act("ready_toggle");
        w.Run(10);
        p.Session.DevCommand("startnow");
        for (int i = 0; i < 400 && !(MobOf(w, p) is mob_living_carbon_human); i++)
            w.Run(1);
        Assert.IsType<mob_living_carbon_human>(MobOf(w, p));
        w.Run(100);
    }

    private static mob MobOf(RealHeadlessWorld w, RealHeadlessWorld.Player p)
        => w.World.Objects.TryGet(w.Host.AvatarOf(p.Name), out object o) ? o as mob : null;
}
