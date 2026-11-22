using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Protocol version 53 (M10x slice G5, docs/ai/specs/protocol.md section 57): the Esc menu's bug report and the host's WebP check.</summary>
public class ProtocolV53Tests
{
    // a 1 x 1 lossless WebP
    internal static readonly byte[] TinyWebP = Convert.FromBase64String("UklGRhoAAABXRUJQVlA4TA0AAAAvAAAAEAcQERGIiP4HAA==");

    // The parts and their body put back together, as the host does.
    private static BugReportMessage RoundTrip(BugReportMessage report)
    {
        var body = new List<byte>();
        foreach (byte[] message in Codec.EncodeBugReportParts(report))
        {
            Assert.True(message.Length <= 8 * 993, "a part fits the 8 fragments a server takes from a client (LiteNetServerTransport.MaxInboundFragments)");
            Assert.True(Codec.TryDecode(message, out BugReportPart part, out ProtocolError error), error.ToString());
            Assert.Equal(body.Count, part.Offset);
            Assert.Equal(message, Codec.Encode(part));
            body.AddRange(part.Bytes);
        }
        Assert.True(Codec.TryDecodeBugReportBody(body.ToArray(), out BugReportMessage decoded, out ProtocolError bodyError), bodyError.ToString());
        Assert.Equal(Codec.EncodeBugReportBody(report), Codec.EncodeBugReportBody(decoded));
        return decoded;
    }

    [Fact]
    public void ABugReportRoundTripsInItsPartsWithAndWithoutItsOptionalFields()
    {
        foreach (var report in new[]
        {
            new BugReportMessage("Door stuck", "It did not open.", "1. Walk\n2. Click", "OS: macOS", TinyWebP),
            new BugReportMessage("T", "W", null, null, null),
            new BugReportMessage(new string('t', Limits.MaxBugReportTitleBytes), new string('w', Limits.MaxBugReportTextBytes), new string('s', Limits.MaxBugReportTextBytes),
                new string('c', Limits.MaxBugReportClientBytes), new byte[Limits.MaxBugReportImageBytes]),
        })
        {
            BugReportMessage decoded = RoundTrip(report);
            Assert.Equal((report.Title, report.WhatHappened, report.Steps, report.Client), (decoded.Title, decoded.WhatHappened, decoded.Steps, decoded.Client));
            Assert.Equal(report.Image, decoded.Image);
        }
        Assert.Equal(1 + Limits.MaxBugReportBodyBytes / Limits.MaxBugReportPartBytes, Codec.EncodeBugReportParts(new BugReportMessage(new string('t', Limits.MaxBugReportTitleBytes),
            new string('w', Limits.MaxBugReportTextBytes), new string('s', Limits.MaxBugReportTextBytes), new string('c', Limits.MaxBugReportClientBytes), new byte[Limits.MaxBugReportImageBytes])).Count);
    }

    [Fact]
    public void ABugReportWithoutItsTextOrOverItsCapsIsRefused()
    {
        Assert.Throws<ProtocolException>(() => Codec.EncodeBugReportParts(new BugReportMessage("", "W", null, null, null)));
        Assert.Throws<ProtocolException>(() => Codec.EncodeBugReportParts(new BugReportMessage("T", null, null, null, null)));
        Assert.Throws<ProtocolException>(() => Codec.EncodeBugReportParts(new BugReportMessage(new string('t', Limits.MaxBugReportTitleBytes + 1), "W", null, null, null)));
        Assert.Throws<ProtocolException>(() => Codec.EncodeBugReportParts(new BugReportMessage("T", "W", null, null, new byte[Limits.MaxBugReportImageBytes + 1])));
        Assert.Throws<ProtocolException>(() => Codec.EncodeBugReportParts(new BugReportMessage("T", "W", null, null, Array.Empty<byte>())));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new BugReportPart(10, 8, new byte[3]))); // past the total
        Assert.Throws<ProtocolException>(() => Codec.Encode(new BugReportPart(Limits.MaxBugReportBodyBytes + 1, 0, new byte[1])));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new BugReportPart(9000, 0, new byte[Limits.MaxBugReportPartBytes + 1])));

        byte[] bytes = Codec.Encode(new BugReportPart(10, 0, new byte[4]));
        Assert.False(Codec.TryDecode(bytes.AsSpan(0, bytes.Length - 1), out BugReportPart _, out _)); // truncated
        Assert.False(Codec.TryDecode(bytes.Concat(new byte[] { 0 }).ToArray(), out BugReportPart _, out _)); // a trailing byte
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.BugReport, 4, 2, 3, 1, 2, 3 }, out BugReportPart _, out _)); // offset + length past the total
        Assert.False(Codec.TryDecode(new byte[] { (byte)MessageId.BugReport, 4, 0, 0 }, out BugReportPart _, out _)); // an empty part
        Assert.False(Codec.TryDecodeBugReportBody(new byte[] { 0, 2, (byte)'W', 0, 0, 0 }, out _, out _)); // no title
        Assert.True(Codec.TryDecodeBugReportBody(new byte[] { 2, (byte)'T', 2, (byte)'W', 0, 0, 0 }, out _, out _));
    }

    [Fact]
    public void TheHostTakesAWebPAndNothingElse()
    {
        Assert.True(ServerHost.IsWebP(TinyWebP));
        Assert.False(ServerHost.IsWebP(new byte[64]));
        Assert.False(ServerHost.IsWebP(TinyWebP.Concat(new byte[] { 0 }).ToArray())); // the RIFF size is not the file's
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        Assert.False(ServerHost.IsWebP(png));
        byte[] badSignature = (byte[])TinyWebP.Clone();
        badSignature[20] = 0;
        Assert.False(ServerHost.IsWebP(badSignature));
    }
}
