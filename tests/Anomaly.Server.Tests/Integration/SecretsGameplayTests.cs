using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The Secrets panel through the real server on MetaStation: an admin of the Host rank opens it from the console (secrets), takes the
/// station's power away and gives it back, breaks and fixes the lights; a moderator without +FUN gets the window with Fun locked, and a
/// player who is no admin gets no window.
/// </summary>
public sealed class SecretsGameplayTests : IDisposable
{
    private const int Second = 20;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "anomaly-secrets-" + Guid.NewGuid().ToString("N"));

    public SecretsGameplayTests()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "admin_ranks.txt"), """
            Name = Moderator
            Include = ADMIN
            Exclude =
            Edit =

            Name = Host
            Include = EVERYTHING
            Exclude =
            Edit = EVERYTHING
            """.Replace("\r\n", "\n"));
        File.WriteAllText(Path.Combine(_folder, "admins.txt"), "Boss = Host\nMod = Moderator\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); }
        catch (IOException) { }
    }

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static TguiWindows WindowsOf(RealHeadlessWorld.Player p) => new()
    {
        SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
        SendClose = window => p.Session.UiClose(window),
    };

    private static TguiWindow Panel(TguiWindows windows, RealHeadlessWorld.Player p)
    {
        windows.Sync(p.Session.Replica.UiWindows, 1280, 720, 1);
        return windows.Open.FirstOrDefault(x => x.Interface == "Secrets");
    }

    private static void Press(RealHeadlessWorld w, TguiWindows windows, RealHeadlessWorld.Player p, string text)
    {
        Panel(windows, p).Content.Walk().Single(n => n.Text == text).OnClick();
        w.Run(Second / 2);
    }

    // the station's APCs that have a cell
    private static List<obj_machinery_power_apc> Apcs(RealHeadlessWorld w) => In(w, () => Dm.each<obj_machinery_power_apc>(GlobalProcs.SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc)))
        .Where(a => a.cell != null && Dm.truthy(GlobalProcs.is_station_level(a.z))).ToList());

    private static List<obj_machinery_light> Lights(RealHeadlessWorld w) => In(w, () => Dm.each<obj_machinery_light>(GlobalProcs.SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_light))).ToList());

    [MetaServerFact]
    public void A_host_cuts_and_restores_the_stations_power_and_breaks_and_fixes_its_lights()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f,
            DevCommands = false, ConfigFile = Path.Combine(_folder, "server.txt"),
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 351, TimeoutSteps = 40000 }, config, strict: false);
        RealHeadlessWorld.Player boss = w.Join("Boss");
        RealHeadlessWorld.Player mod = w.Join("Mod");
        RealHeadlessWorld.Player pleb = w.Join("Pleb");
        w.Run(Second);

        TguiWindows plebWindows = WindowsOf(pleb);
        pleb.Session.DevCommand("secrets");
        w.Run(Second / 2);
        Assert.Null(Panel(plebWindows, pleb));

        TguiWindows modWindows = WindowsOf(mod);
        mod.Session.DevCommand("secrets");
        w.Run(Second / 2);
        Assert.True(Panel(modWindows, mod).Content.Walk().Single(n => n.Text == "Fun").Disabled, "a moderator has no +FUN");

        TguiWindows windows = WindowsOf(boss);
        boss.Session.DevCommand("secrets");
        w.Run(Second / 2);
        Assert.NotNull(Panel(windows, boss));

        List<obj_machinery_power_apc> apcs = Apcs(w);
        Assert.True(apcs.Count > 50, "found " + apcs.Count + " station APCs");

        Press(w, windows, boss, "Fun");
        Press(w, windows, boss, "All areas unpowered");
        Assert.True(In(w, () => apcs.Count(a => a.cell.charge == 0)) > apcs.Count / 2, "the APCs kept their charge");

        Press(w, windows, boss, "All areas powered");
        Assert.All(apcs, a => Assert.True(In(w, () => a.cell.charge > 0), a.name as string));

        Press(w, windows, boss, "Helpful");
        Press(w, windows, boss, "Break all lights");
        List<obj_machinery_light> lights = Lights(w);
        Assert.True(lights.Count > 100, "found " + lights.Count + " lights");
        Assert.All(lights, l => Assert.NotEqual(Defines.LIGHT_OK, In(w, () => Dm.num(l.status))));

        Press(w, windows, boss, "Fix all lights");
        w.Run(3 * Second); // the loop CHECK_TICKs over the ticks
        // not every one: a tube that comes on may burn out at once (light.dm: update, its switchcount), and the world goes on breaking things
        Assert.True(In(w, () => lights.Count(l => Dm.num(l.status) == Defines.LIGHT_OK)) > lights.Count * 0.9, "the lights stayed broken");
    }
}
