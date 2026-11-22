using Anomaly.Core.Motion;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Bots;

/// <summary>Each abuse payload is exactly the violation it claims (scope 2j), so a server test can name the strike it expects.</summary>
public class AbusePayloadsTests
{
    [Fact]
    public void GarbageIsNeverAValidClientMessage()
    {
        var random = new BotRandom(77);
        var firstBytes = new HashSet<byte>();
        for (int i = 0; i < 2000; i++)
        {
            byte[] garbage = AbusePayloads.Garbage(random);
            Assert.InRange(garbage.Length, 1, AbusePayloads.MaxGarbageBytes);
            Assert.False(Codec.TryDecode(garbage, out MotionInputMessage _, out _), "garbage that decodes as a MotionInput");
            Assert.False(Codec.TryDecode(garbage, out ActionMessage _, out _), "garbage that decodes as an Action");
            Assert.False(Codec.TryDecode(garbage, out Hello _, out _), "garbage that decodes as a Hello");
            firstBytes.Add(garbage[0]);
        }

        // It varies: unknown ids, server ids, and the ids of the two commands cut short.
        Assert.Contains((byte)MessageId.MotionInput, firstBytes);
        Assert.Contains((byte)MessageId.Action, firstBytes);
        Assert.Contains((byte)MessageId.Delta, firstBytes);
        Assert.True(firstBytes.Count >= 6);
    }

    [Fact]
    public void GarbageNeverStartsWithAHello()
    {
        var random = new BotRandom(5);
        for (int i = 0; i < 2000; i++)
        {
            byte[] garbage = AbusePayloads.Garbage(random);
            Assert.NotEqual((byte)MessageId.Hello, garbage[0]);
            if (garbage[0] is (byte)MessageId.MotionInput or (byte)MessageId.Action)
                Assert.True(garbage.Length == 1 || garbage.Skip(1).All(b => b == 0xFF), "a command id is followed by nothing or by a varint that never ends");
        }
    }

    [Fact]
    public void AMessageThatStartsLikeAMotionInputTravelsOnTheInputChannel()
    {
        var random = new BotRandom(8);
        for (int i = 0; i < 200; i++)
        {
            byte[] garbage = AbusePayloads.Garbage(random);
            Assert.Equal(garbage[0] == (byte)MessageId.MotionInput ? Channel.Input : Channel.Control, AbusePayloads.ChannelFor(garbage));
        }
        Assert.Equal(Channel.Control, AbusePayloads.ChannelFor(null));
        Assert.Equal(Channel.Control, AbusePayloads.ChannelFor(Array.Empty<byte>()));
    }

    [Fact]
    public void GarbageIsDeterministicForASeed()
    {
        byte[][] Make(ulong seed)
        {
            var random = new BotRandom(seed);
            return Enumerable.Range(0, 50).Select(_ => AbusePayloads.Garbage(random)).ToArray();
        }

        Assert.Equal(Make(3), Make(3));
        Assert.NotEqual(Make(3), Make(4));
    }

    [Fact]
    public void OversizedIsOverTheCapAndNothingElseIsWrongWithItsId()
    {
        var random = new BotRandom(12);
        var lengths = new HashSet<int>();
        for (int i = 0; i < 500; i++)
        {
            byte[] message = AbusePayloads.Oversized(random);
            Assert.InRange(message.Length, Limits.MaxSayActionBytes + 1, Limits.MaxSayActionBytes + 1000);
            Assert.Equal((byte)MessageId.Action, message[0]);
            lengths.Add(message.Length);
        }
        Assert.True(lengths.Count > 50);
    }

    [Fact]
    public void ValidMotionInputBytesDecodeToTheFramesAsked()
    {
        byte[] bytes = AbusePayloads.MotionInputBytes(10, 3, 100, -20, 4321);
        Assert.True(Codec.TryDecode(bytes, out MotionInputMessage message, out _));
        Assert.Equal(new uint[] { 10, 11, 12 }, message.Frames.Select(f => f.Seq));
        Assert.All(message.Frames, f => Assert.Equal((100, -20, 4321), (f.MoveX, f.MoveY, f.Yaw)));
        Assert.True(bytes.Length <= Limits.MaxMotionInputMessageBytes);
    }

    [Fact]
    public void AForgedSeqIsAValidMessageMoreThanTheLeadAhead()
    {
        var random = new BotRandom(3);
        for (int i = 0; i < 200; i++)
        {
            uint last = (uint)random.Next(0, 5000);
            byte[] bytes = AbusePayloads.ForgedSeq(last, random);
            Assert.True(Codec.TryDecode(bytes, out MotionInputMessage message, out _));
            MotionInput frame = Assert.Single(message.Frames);
            Assert.True(frame.Seq > last + Limits.MaxInputSeqLead, $"{frame.Seq} is not past {last} + the lead");
            Assert.Equal(Channel.Input, AbusePayloads.ChannelFor(bytes));
        }
    }

    [Fact]
    public void ABadActionIsTheActionIdAndNothingTheCodecAccepts()
    {
        var random = new BotRandom(21);
        for (int i = 0; i < 1000; i++)
        {
            byte[] bytes = AbusePayloads.BadAction(random);
            Assert.InRange(bytes.Length, 1, AbusePayloads.MaxGarbageBytes);
            Assert.Equal((byte)MessageId.Action, bytes[0]);
            Assert.False(Codec.TryDecode(bytes, out ActionMessage _, out _));
        }
    }

    [Fact]
    public void AClickOnAMissingEntityIsAValidActionOnAnEntityNoWorldHas()
    {
        var random = new BotRandom(4);
        byte[] bytes = AbusePayloads.MissingTargetClick(7, random);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage action, out _));
        Assert.Equal((7u, ActionKind.Click, ClickButton.Right), (action.FrameSeq, action.Kind, action.Button));
        Assert.True(action.Target >= AbusePayloads.MissingEntityBase);
    }

    [Fact]
    public void ADevCommandAtTheCapIsValidAndOneOverItIsNot()
    {
        byte[] atCap = AbusePayloads.DevCommandAtCap(3);
        Assert.True(Codec.TryDecode(atCap, out ActionMessage action, out _));
        Assert.Equal(ActionKind.DevCommand, action.Kind);
        Assert.Equal(Limits.MaxDevCommandBytes, System.Text.Encoding.UTF8.GetByteCount(action.Text));
        Assert.StartsWith("abuse ", action.Text);

        var random = new BotRandom(9);
        for (int i = 0; i < 100; i++)
        {
            byte[] over = AbusePayloads.DevCommandOverCap(3, random);
            Assert.True(over.Length <= Limits.MaxInboundMessageBytes, "the message stays under the inbound cap, so only the text length is wrong");
            Assert.False(Codec.TryDecode(over, out ActionMessage _, out _));
            Assert.Equal((byte)MessageId.Action, over[0]);
        }
    }

    [Fact]
    public void DevTextHasTheLengthAsked()
    {
        foreach (int n in new[] { 0, 3, 6, 7, 100, 200 })
            Assert.Equal(n, AbusePayloads.DevText(n).Length);
    }
}
