using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 62 (D306): the host's lobby between rounds rides on <see cref="ServerText"/> types.</summary>
public class ProtocolV62Tests
{
    [Theory]
    [InlineData(BetweenRounds.Type, "The server is restarting...")]
    [InlineData(BetweenRounds.MusicType, "config/title_music/sounds/title1.ogg")]
    public void TheBetweenRoundsTextsRoundTrip(string type, string html)
    {
        byte[] bytes = Codec.Encode(new ServerText(type, html));
        Assert.True(Codec.TryDecode(bytes, out ServerText decoded, out ProtocolError error), error.ToString());
        Assert.Equal(new ServerText(type, html), decoded);
        Assert.Equal(bytes, Codec.Encode(decoded));
    }

    [Fact]
    public void TheTypesFitTheTypeCap()
    {
        foreach (string type in new[] { BetweenRounds.Type, BetweenRounds.MusicType })
            Assert.True(System.Text.Encoding.UTF8.GetByteCount(type) <= Limits.MaxServerTextTypeBytes);
        Assert.True(ProtocolVersion.Current >= 62);
    }
}
