using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 96: the turfs' and ceilings' own lights in MapGrid and the Delta, and the replica's copy of them.</summary>
public class ProtocolV96TurfLightTests
{
    private static readonly LightState Cyan = new(3f, 1f, 0x7DE1E1FF, true, 360f, 0, 1f, 0f, 0f, 1);

    private static MapGrid Grid()
    {
        var grid = new MapGrid { Tick = 5, Width = 8, Height = 8, Levels = 1 };
        grid.TurfTypes.Add("/turf/open/floor");
        grid.Runs.Add(new TurfRun(grid.CellCount, 0));
        return grid;
    }

    [Fact]
    public void TurfLightsRoundTripAndTheReplicaFollowsThem()
    {
        MapGrid grid = Grid();
        grid.TurfLights.Add(new TurfLightChange(2, 3, 1, false, Cyan));
        grid.TurfLights.Add(new TurfLightChange(2, 3, 1, true, Cyan));
        byte[] bytes = Codec.Encode(grid);
        Assert.True(Codec.TryDecode(bytes, out MapGrid decoded, out ProtocolError error), error.ToString());
        Assert.Equal(grid.TurfLights, decoded.TurfLights);
        Assert.Equal(bytes, Codec.Encode(decoded));

        var delta = new Delta { Tick = 6 };
        delta.TurfLightChanged.Add(new TurfLightChange(2, 3, 1, false, LightState.None));
        delta.TurfLightChanged.Add(new TurfLightChange(5, 5, 1, false, Cyan));
        bytes = Codec.Encode(delta);
        Assert.True(Codec.TryDecode(bytes, out Delta decodedDelta, out error), error.ToString());
        Assert.Equal(delta.TurfLightChanged, decodedDelta.TurfLightChanged);

        var replica = new ReplicaWorld();
        replica.Apply(decoded);
        Assert.Equal(Cyan, replica.TurfLightAt(2, 3, 1, false));
        Assert.Equal(Cyan, replica.TurfLightAt(2, 3, 1, true));
        var changed = new List<TurfLightChange>();
        replica.TurfLightChanged += c => changed.AddRange(c);
        replica.Apply(new Snapshot { Tick = 5 });
        replica.Apply(decodedDelta);
        Assert.Equal(2, changed.Count);
        Assert.True(replica.TurfLightAt(2, 3, 1, false).IsNone);
        Assert.Equal(Cyan, replica.TurfLightAt(2, 3, 1, true));
        Assert.Equal(Cyan, replica.TurfLightAt(5, 5, 1, false));
    }

    [Fact]
    public void AMapGridRefusesAnUnlitOrRepeatedTurfLight()
    {
        MapGrid grid = Grid();
        grid.TurfLights.Add(new TurfLightChange(2, 3, 1, false, LightState.None));
        Assert.Throws<ProtocolException>(() => Codec.Encode(grid));
        grid.TurfLights.Clear();
        grid.TurfLights.Add(new TurfLightChange(2, 3, 1, false, Cyan));
        grid.TurfLights.Add(new TurfLightChange(2, 3, 1, false, Cyan));
        Assert.Throws<ProtocolException>(() => Codec.Encode(grid));
    }
}
