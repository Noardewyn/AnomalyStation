using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 63 (D307, docs/ai/specs/protocol.md section 63): the TtsMessage, a line the client voices itself.</summary>
public class ProtocolV63Tests
{
    [Fact]
    public void TheIdFollowsVersion54()
    {
        Assert.True(ProtocolVersion.Current >= 63);
        Assert.Equal(25, (int)MessageId.Tts);
        Assert.True(WireReader.IsKnownMessageId(25));
    }

    [Fact]
    public void ALineHasItsBytesAndRoundTrips()
    {
        byte[] bytes = Codec.Encode(new TtsMessage(5, "Hi", "Man 1", -3, 85));
        // id 25, speaker 5, "Hi" and "Man 1" (each its length plus one), pitch -3 (zigzag 5), volume 85; version 64: no filter (0), no flags
        Assert.Equal(new byte[] { 25, 5, 3, (byte)'H', (byte)'i', 6, (byte)'M', (byte)'a', (byte)'n', (byte)' ', (byte)'1', 5, 85, 0, 0 }, bytes);
        Assert.True(Codec.TryDecode(bytes, out TtsMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(new TtsMessage(5, "Hi", "Man 1", -3, 85), back);
        var russian = new TtsMessage(0, "Позовите врача", "Woman 2", 12, 100);
        Assert.True(Codec.TryDecode(Codec.Encode(russian), out back, out _));
        Assert.Equal(russian, back);
    }

    [Fact]
    public void ABrokenLineIsRefused()
    {
        Assert.Throws<ProtocolException>(() => Codec.Encode(new TtsMessage(1, "", "Man 1", 0, 85)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new TtsMessage(1, "Hi", "Man 1", 13, 85)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new TtsMessage(1, "Hi", "Man 1", 0, Limits.MaxTtsVolume + 1)));
        // pitch 13 (zigzag 26) on the wire
        Assert.False(Codec.TryDecode(new byte[] { 25, 1, 2, (byte)'a', 2, (byte)'b', 26, 85, 0, 0 }, out TtsMessage _, out _));
        Assert.False(Codec.TryDecode(new byte[] { 25, 1, 1, 2, (byte)'b', 0, 85, 0, 0 }, out TtsMessage _, out _));
    }

    [Fact]
    public void Version64CarriesTheFilterAndTheFlags()
    {
        Assert.True(ProtocolVersion.Current >= 64);
        var line = new TtsMessage(7, "Over", "Woman 2", 0, 85, "lowpass=f=750,volume=2", TtsFlags.Radio | TtsFlags.Common | TtsFlags.Silicon);
        Assert.True(Codec.TryDecode(Codec.Encode(line), out TtsMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(line, back);
        Assert.True(Codec.TryDecode(Codec.Encode(line with { Flags = TtsFlags.Whisper | TtsFlags.Yell }), out back, out _)); // version 65
        Assert.Equal(TtsFlags.Whisper | TtsFlags.Yell, back.Flags);
        Assert.Throws<ProtocolException>(() => Codec.Encode(line with { Flags = (TtsFlags)128 }));
        byte[] bytes = Codec.Encode(line);
        bytes[^1] = 128; // an unknown flag
        Assert.False(Codec.TryDecode(bytes, out TtsMessage _, out _));
    }
}
