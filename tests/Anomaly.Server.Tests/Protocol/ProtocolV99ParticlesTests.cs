using System.Text.Json;
using Anomaly.Client.Shared.Play;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Server.Tests.Integration;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 99 (DV687, DV688; docs/ai/specs/protocol.md section 95): an appearance's particles round trip as a string table
/// entry; a light's flicker bit round trips and a light flags byte past it is refused; the server writes a particle set's vars as JSON,
/// the client reads it into its emitter's sums, and the flame flicker stays between tg's two alphas.
/// </summary>
public class ProtocolV99ParticlesTests
{
    [Fact]
    public void TheVersionIsNinetyNine() => Assert.True(ProtocolVersion.Current >= 99);

    [Fact]
    public void AnAppearancesParticlesRoundTrip()
    {
        Delta d = Msg.Delta(10, 4, 5);
        d.FirstNewString = 1;
        d.NewStrings.Add("{\"spawning\":0.05}");
        d.FirstNewAppearance = 1;
        d.NewAppearances.Add(new AppearanceEntry { Particles = TableRef.Public(1) });
        d.NewAppearances.Add(new AppearanceEntry());
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
        Assert.Equal(TableRef.Public(1), back.NewAppearances[0].Particles);
        Assert.True(back.NewAppearances[1].Particles.IsNone);
    }

    [Fact]
    public void ALightsFlickerRoundTrips()
    {
        MapGrid grid = Msg.FlatMap(1, 1);
        grid.FlagRuns.Add(new TurfRun(1, 0));
        grid.BaseLightRuns.Add(new TurfRun(1, 0));
        grid.Starlight = new LightState(2f, 1f, 0xFFA040FFu, true, 360f, 0, 1f, 0f, 0f, 2, Flicker: true);
        Assert.True(Codec.TryDecode(Codec.Encode(grid), out MapGrid back, out ProtocolError error), error.ToString());
        Assert.True(back.Starlight.Flicker);
        Assert.True(back.Starlight.On);
    }

    [Fact]
    public void TheServerWritesACigarettesSmokeAsJsonAndTheClientReadsIt()
    {
        using var w = new RealHeadlessWorld(config: new ServerConfig { Seed = 15331, RoomWidth = 16, RoomHeight = 16, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        w.Run(20);
        mob_living alices = w.MobOf(alice);
        obj_item_cigarette cig;
        string json;
        using (w.World.Enter())
        {
            cig = Dm.@new<obj_item_cigarette>(alices.loc);
            Waiting.NoSleep(cig.light__proc());
            json = ParticleSets.Text(cig.cig_smoke.particles);
        }
        Assert.NotNull(json);
        using (JsonDocument document = JsonDocument.Parse(json))
        {
            JsonElement root = document.RootElement;
            Assert.Equal("icons/effects/particles/smoke.dmi", root.GetProperty("icon").GetString());
            Assert.Equal(0.05, root.GetProperty("spawning").GetDouble(), 3);
            Assert.Equal(7.5, root.GetProperty("lifespan").GetDouble(), 3);
            Assert.Equal(2, root.GetProperty("states").GetProperty("steam_1").GetDouble());
        }

        // the client's replica: the cigarette's picture carries its smoke holder, whose appearance names the set
        w.Run(20);
        ReplicaWorld replica = alice.Session.Replica;
        Assert.True(replica.TryGet(cig.dm_id, out ReplicaEntity seen), "the client sees the cigarette");
        Assert.Contains(Tree(replica, seen.State.Appearance), e => !e.Particles.IsNone && replica.ResolveString(e.Particles) == json);

        ParticleSet set = ParticleSet.Parse(json);
        Assert.NotNull(set);
        Assert.Equal("steam_1", set.State); // the heaviest of tg's states
        Assert.True(set.Emits);
        Assert.Equal(1, set.Amount); // tg's count of 1
        Assert.Equal(0.75, set.LifetimeSeconds, 3);
        Assert.Equal(0.75, set.StartScale, 3);
        Assert.Equal(0.2 * ParticleSet.PixelMetres / ParticleSet.TickSeconds, set.VelocityMetres.Y, 6);
        Assert.Equal(0xD0D0D09Du, set.Rgba);
        Assert.Equal(0, set.AlphaAt(1), 6); // faded out over its whole life (fade = lifespan)
        Assert.True(set.AlphaAt(0) > 0.6);
    }

    private static IEnumerable<AppearanceEntry> Tree(ReplicaWorld replica, TableRef root, int depth = 0)
    {
        if (depth > 12 || !replica.TryGetAppearance(root, out AppearanceEntry e))
            yield break;
        yield return e;
        foreach (TableRef child in e.Underlays.Concat(e.Overlays))
            foreach (AppearanceEntry below in Tree(replica, child, depth + 1))
                yield return below;
    }

    [Fact]
    public void ASetThatSpawnsNothingDoesNotEmitAndABadOneIsNoSet()
    {
        Assert.False(ParticleSet.Parse("{\"spawning\":0}").Emits);
        Assert.Null(ParticleSet.Parse("not json"));
        Assert.Null(ParticleSet.Parse("[1,2]"));
    }

    [Fact]
    public void TheFlameFlickerStaysBetweenTgsAlphas()
    {
        var flicker = new FlameFlicker(42);
        double low = 1, high = 0;
        for (double t = 0; t < 20; t += 0.01)
        {
            double at = flicker.At(t);
            low = Math.Min(low, at);
            high = Math.Max(high, at);
        }
        Assert.InRange(low, 235.0 / 255.0 - 1e-9, 235.0 / 255.0 + 0.01);
        Assert.Equal(1.0, high, 6);
    }
}
