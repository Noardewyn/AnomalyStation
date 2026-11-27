using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 97 (DV597): the Hello's flag of a client that loads the world first, and the Loaded action it sends then.</summary>
public class ProtocolV97Tests
{
    [Fact]
    public void AHelloCarriesTheWaitsForLoadFlag()
    {
        var hello = new Hello(ProtocolVersion.Current, "Alice", new SessionToken(5, 6), "token", WaitsForLoad: true);
        Assert.True(Codec.TryDecode(Codec.Encode(hello), out Hello back, out ProtocolError error), error.ToString());
        Assert.Equal(hello, back);
        Assert.True(Codec.TryDecode(Codec.Encode(hello with { WaitsForLoad = false }), out back, out error), error.ToString());
        Assert.False(back.WaitsForLoad);
        Assert.True(ProtocolVersion.Current >= 97);
    }

    [Fact]
    public void LoadedCarriesNothing()
    {
        var loaded = new ActionMessage(3, ActionKind.Loaded, 0, ClickButton.None, ClickModifiers.None, null);
        byte[] bytes = Codec.Encode(loaded);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(loaded, back);
        Assert.True(ActionKinds.Last >= ActionKind.Loaded); // version 100 adds HotbarPress, InventoryMove and InventoryOpen after it
        Assert.Throws<ProtocolException>(() => Codec.Encode(loaded with { Target = 4, TargetKind = TargetKind.Entity }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(loaded with { Text = "x" }));
    }
}
