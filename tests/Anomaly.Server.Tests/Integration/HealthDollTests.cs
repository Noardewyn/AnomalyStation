using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Bots;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// P8's finding at the M2 integration: tg keeps the health doll's six limbs in the doll's <c>vis_contents</c>
/// (screen_objects.dm:968-979), which the wire has no field for. The replicator draws an atom's vis_contents as part of it
/// (flattened into its overlays, as BYOND draws them), so the real server's private block carries the zones, and the
/// client's HUD (<see cref="HudReader"/>, what P8's doll draws from) reads them.
/// </summary>
public class HealthDollTests
{
    private static List<FoundZone> DollZones(Bot bot)
    {
        foreach (ScreenEntry entry in bot.Replica.Screen.Values)
        {
            var zones = new List<FoundZone>();
            HudReader.Collect(bot.Replica, entry.Appearance, zones);
            if (zones.Count > 0) return zones;
        }
        return new List<FoundZone>();
    }

    [Fact]
    public void TheRealServersPrivateBlockCarriesTheDollsZonesAndTheHudReadsThem()
    {
        using var rig = new BotServerRig(strict: true);
        Bot bot = rig.AddBot("Patient", BotBehaviour.Idle);
        rig.UntilJoined(bot);
        rig.Tick(10);

        List<FoundZone> healthy = DollZones(bot);
        Assert.Equal(HealthZones.Zones.OrderBy(z => z), healthy.Select(f => f.Zone.Zone).OrderBy(z => z));
        Assert.All(healthy, f => Assert.Equal((ZoneKind.Level, 0), (f.Zone.Kind, f.Zone.Level)));

        var human = (mob_living_carbon_human)rig.MobOf("Patient");
        using (rig.World.Enter())
            Waiting.NoSleep(human.apply_damage(60, Defines.BRUTE, Defines.BODY_ZONE_CHEST));
        rig.Tick(4);

        List<FoundZone> hurt = DollZones(bot);
        FoundZone chest = hurt.Single(f => f.Zone.Zone == "chest");
        Assert.Equal(ZoneKind.Level, chest.Zone.Kind);
        Assert.True(chest.Zone.Level >= 1, $"chest level {chest.Zone.Level}");
        Assert.All(hurt.Where(f => f.Zone.Zone != "chest"), f => Assert.Equal(0, f.Zone.Level));
        Assert.Equal(0, rig.World.RuntimeErrorCount);
    }
}
