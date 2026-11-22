using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps E2a through the real server with a headless client: a weather that starts on the player's level reaches the client (protocol
/// version 84) with its stage's overlay state, its colour and the cells of its areas, the player's own cell among them; it changes stage on
/// the client as on the server, and the client's weather clears when it ends.
/// </summary>
public class WeatherGameplayTests(ITestOutputHelper output)
{
    [Fact]
    public void A_weather_on_the_players_level_reaches_its_client_until_it_ends()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 83, TimeoutSteps = 2000 }, MapJoinTests.MapConfig("p2load", MapJoinTests.FixtureMaps()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Stormchaser");
        w.Run(20);
        mob_living me = w.MobOf(p);
        (int x, int y, int z, area room) = Enter(w, () => get_turf(me) is turf t ? ((int)t.x, (int)t.y, (int)t.z, (area)t.loc) : default);
        datum_weather storm = Enter(w, () => SSweather.run_weather(typeof(datum_weather_rad_storm), z, Dm.list(
            Dm.kv(Defines.WEATHER_FORCED_AREAS, Dm.list(room)),
            Dm.kv(Defines.WEATHER_FORCED_TELEGRAPH, 20f),
            Dm.kv(Defines.WEATHER_FORCED_DURATION, 40f),
            Dm.kv(Defines.WEATHER_FORCED_END, 20f))));
        w.Run(10);

        WeatherMessage weather = p.Client.Weather;
        Assert.NotNull(weather);
        Assert.Equal(z, weather.Z);
        WeatherLayer layer = Assert.Single(weather.Layers);
        Assert.Equal(Defines.STARTUP_STAGE, layer.Stage);

        w.Run(40); // the telegraph's 2 seconds are 40 ticks
        layer = Assert.Single(p.Client.Weather.Layers);
        Assert.Equal(Defines.MAIN_STAGE, layer.Stage);
        Assert.Equal("ash_storm", layer.State);
        Assert.Equal(0x00C000FFu, layer.Colour); // tg's "green", BYOND's #00C000
        int cell = (y - layer.MinY) * layer.Width + (x - layer.MinX);
        Assert.InRange(cell, 0, layer.CellCount - 1);
        Assert.True(layer.ExpandCells()[cell], "the player's cell is covered");

        w.Run(140); // the storm's 4 seconds and its wind down's 2
        Assert.Equal(Defines.END_STAGE, (int)Enter(w, () => storm.stage));
        Assert.Empty(p.Client.Weather.Layers);
        output.WriteLine($"runtimes: {w.World.RuntimeErrorCount}");
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    private static T Enter<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }
}
