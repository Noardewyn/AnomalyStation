using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// D296 on MetaStation through the real server and a client's own stack: a crew member walking into a powered maintenance area
/// hears its buzz (tg's source_corridor2 on CHANNEL_BUZZ, looping), back in the station the ship's hum (shipambience); standing
/// there, SSambience plays one of the area's ambience files on CHANNEL_AMBIENCE within its first cooldown; a sound in the
/// maintenance tunnel carries tg's tunnel environment (stone room) for the client's reverb; the maintenance APC's environment
/// channel cut silences the buzz.
/// </summary>
public class AmbienceGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const ushort ChannelAmbience = 1018, ChannelBuzz = 1017;
    private const string ShipHum = "sound/ambience/general/shipambience.ogg";
    private const string TunnelHum = "sound/ambience/maintenance/source_corridor2.ogg";

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

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    // A clear floor tile of the first area on the level that the test accepts.
    private static turf FloorOf(int z, Func<area, bool> accept)
    {
        for (int x = 1; x <= (int)world.maxx; x++)
            for (int y = 1; y <= (int)world.maxy; y++)
                if (locate(x, y, z) is turf t && get_area(t) is area a && accept(a) && Clear(t))
                    return t;
        return null;
    }

    [MetaServerFact]
    public void A_crew_member_hears_the_hum_of_each_area_its_ambience_and_the_tunnel_echo()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 296, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Crew");
        var me = (mob_living_carbon_human)w.MobOf(p);
        var heard = new List<SoundEvent>();
        p.Session.Replica.SoundsReceived += events => heard.AddRange(events);

        int z = In(w, () => (int)me.z);
        turf tunnel = In(w, () => FloorOf(z, a => a is area_station_maintenance && a.apc != null && Dm.truthy(a.power_environ)));
        turf hall = In(w, () => FloorOf(z, a => a is area_station_hallway && a.apc != null && Dm.truthy(a.power_environ)));
        Assert.NotNull(tunnel);
        Assert.NotNull(hall);
        output.WriteLine($"tunnel {In(w, () => ((area)get_area(tunnel)).type.Name)} at {In(w, () => tunnel.x)},{In(w, () => tunnel.y)}; hall {In(w, () => ((area)get_area(hall)).type.Name)} at {In(w, () => hall.x)},{In(w, () => hall.y)}");

        // into the tunnel: its own buzz, looping (maintenance.dm:8)
        In(w, () => Waiting.NoSleep(me.forceMove(tunnel)));
        w.Run(Second);
        SoundEvent tunnelHum = Assert.Single(heard, e => e.Channel == ChannelBuzz && e.Kind == SoundKind.Play);
        Assert.Equal(TunnelHum, tunnelHum.File);
        Assert.True(tunnelHum.Repeat);
        Assert.Equal(40, tunnelHum.Volume); // maintenance's ambient_buzz_vol 20 (maintenance.dm:9), in half units

        // a door's sound in the tunnel: tg's SOUND_AREA_TUNNEL_ENCLOSED (stone room) for the reverb
        heard.Clear();
        In(w, () => playsound(get_step(tunnel, NORTH) ?? tunnel, "sound/machines/airlock/airlock.ogg", 30, FALSE));
        w.Run(2);
        SoundEvent door = Assert.Single(heard, e => e.File == "sound/machines/airlock/airlock.ogg");
        Assert.Equal(In(w, () => (int)((area)get_area(tunnel)).sound_environment) + 1, door.Environment);
        Assert.Equal(SOUND_ENVIRONMENT_STONEROOM + 1, door.Environment);

        // the tunnel's APC cuts its environment channel: the buzz stops (ambience.dm:136)
        heard.Clear();
        In(w, () =>
        {
            obj_machinery_power_apc apc = ((area)get_area(tunnel)).apc;
            apc.environ = APC_CHANNEL_OFF;
            apc.update();
        });
        w.Run(Second);
        Assert.Contains(heard, e => e.Channel == ChannelBuzz && e.Kind == SoundKind.Stop);

        // out into a hallway: the ship's hum
        heard.Clear();
        In(w, () => Waiting.NoSleep(me.forceMove(hall)));
        w.Run(Second);
        Assert.Equal(ShipHum, Assert.Single(heard, e => e.Channel == ChannelBuzz && e.Kind == SoundKind.Play).File);

        // standing in the hall: SSambience plays it a file of the hall's list within the longest cooldown (a file's length + 10 s)
        HashSet<string> files = In(w, () => Dm.each(((area)get_area(hall)).ambientsounds).Select(f => Dm.str(f)).ToHashSet());
        w.Run(60 * Second);
        List<SoundEvent> ambience = heard.Where(e => e.Channel == ChannelAmbience && e.Kind == SoundKind.Play).ToList();
        Assert.NotEmpty(ambience);
        Assert.All(ambience, e => Assert.Contains(e.File, files));
        Assert.All(ambience, e => Assert.Equal(54, e.Volume)); // 27, in half units
        Assert.All(ambience, e => Assert.False(e.Positional));
    }
}
