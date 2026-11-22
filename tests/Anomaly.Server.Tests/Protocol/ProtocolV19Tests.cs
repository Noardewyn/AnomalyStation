using System.Text;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 19 (tg's tooltips, docs/ai/specs/protocol.md section 28, DV47): the Tooltip message (server to client, State) shows tg's
/// HTML text in a theme, or hides it. Bytes, round trips, refusals, and the replica's tooltip.
/// </summary>
public class ProtocolV19Tests
{
    private static TooltipMessage Show(string text = "<h1>Choking (No O2)</h1><p>You're not getting enough oxygen.</p>", string theme = "default")
        => new() { Kind = TooltipKind.Show, Text = text, Theme = theme };

    private static TooltipMessage Hide() => new() { Kind = TooltipKind.Hide };

    private static ReplicaWorld ReplicaWithSnapshot()
    {
        var replica = new ReplicaWorld();
        var grid = new MapGrid { Tick = 5, Width = 8, Height = 8, Levels = 1 };
        grid.TurfTypes.Add("/turf/open/floor");
        grid.Runs.Add(new TurfRun(grid.CellCount, 0));
        replica.Apply(grid);
        replica.Apply(new Snapshot { Tick = 6 });
        return replica;
    }

    [Fact]
    public void TheVersionIsAtLeastNineteenAndTheTooltipIsMessageSeventeen()
    {
        Assert.True(ProtocolVersion.Current >= 19); // version 20 (pulling) adds a keybinding code only
        Assert.Equal((byte)17, (byte)MessageId.Tooltip);
        Assert.True(WireReader.IsKnownMessageId(17));
        Assert.True(WireReader.IsKnownMessageId(18)); // version 21: the camera shake
    }

    [Fact]
    public void AShowAndAHideHaveTheirBytes()
    {
        byte[] show = Codec.Encode(Show("<p>x</p>", "midnight"));
        var expected = new List<byte> { 17, 0, 9 };
        expected.AddRange(Encoding.UTF8.GetBytes("<p>x</p>"));
        expected.Add(9);
        expected.AddRange(Encoding.UTF8.GetBytes("midnight"));
        Assert.Equal(expected.ToArray(), show);
        Assert.Equal(new byte[] { 17, 1 }, Codec.Encode(Hide()));
    }

    [Fact]
    public void ShowsAndHidesRoundTrip()
    {
        foreach (TooltipMessage message in new[] { Show(), Show("<p>Only a title</p>", "trasen-knox"), Show("<h1>A</h1><p>" + new string('b', 4000) + "</p>", "hisgrace"), Hide() })
        {
            byte[] bytes = Codec.Encode(message);
            Assert.True(bytes.Length <= Limits.MaxTooltipBytes);
            Assert.True(Codec.TryDecode(bytes, out TooltipMessage back, out ProtocolError error), error.ToString());
            Assert.Equal((message.Kind, message.Text, message.Theme), (back.Kind, back.Text, back.Theme));
            Assert.Equal(bytes, Codec.Encode(back));
        }
    }

    [Fact]
    public void TheCodecRefusesWhatTheWireDoesNotCarry()
    {
        // Encoding: an unknown kind, a show without a text, a theme that is no class name, a hide with a show's fields, a text over its cap.
        Assert.Throws<ProtocolException>(() => Codec.Encode(new TooltipMessage { Kind = (TooltipKind)2 }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Show("", "default")));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Show(null, "default")));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Show("<p>x</p>", "")));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Show("<p>x</p>", "Midnight")));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Show("<p>x</p>", "a b")));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Show("<p>x</p>", new string('a', Limits.MaxTooltipThemeBytes + 1))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new TooltipMessage { Kind = TooltipKind.Hide, Text = "<p>x</p>" }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new TooltipMessage { Kind = TooltipKind.Hide, Theme = "default" }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Show(new string('a', Limits.MaxTooltipTextBytes + 1))));

        // Decoding.
        Assert.False(Codec.TryDecode(new byte[] { 17 }, out TooltipMessage none, out _)); // no kind
        Assert.Null(none);
        Assert.False(Codec.TryDecode(new byte[] { 17, 2 }, out TooltipMessage _, out _)); // an unknown kind
        Assert.False(Codec.TryDecode(new byte[] { 17, 1, 0 }, out TooltipMessage _, out _)); // a hide with a byte more
        Assert.False(Codec.TryDecode(new byte[] { 17, 0, 1, 2, (byte)'a', (byte)'b' }, out TooltipMessage _, out _)); // an empty text
        Assert.False(Codec.TryDecode(new byte[] { 17, 0, 0, 2, (byte)'a', (byte)'b' }, out TooltipMessage _, out _)); // no text
        Assert.False(Codec.TryDecode(new byte[] { 17, 0, 2, (byte)'x', 0 }, out TooltipMessage _, out _)); // no theme
        Assert.False(Codec.TryDecode(new byte[] { 17, 0, 2, (byte)'x', 1 }, out TooltipMessage _, out _)); // an empty theme
        Assert.False(Codec.TryDecode(new byte[] { 17, 0, 2, (byte)'x', 2, (byte)'A' }, out TooltipMessage _, out _)); // an upper case theme
        Assert.False(Codec.TryDecode(new byte[] { 17, 0, 2, (byte)'x', 2, (byte)'a', 0 }, out TooltipMessage _, out _)); // a byte more
        Assert.False(Codec.TryDecode(new byte[] { 17, 0, 2, (byte)'x' }, out TooltipMessage _, out _)); // truncated
        Assert.False(Codec.TryDecode(new byte[] { 16, 1, 1 }, out TooltipMessage _, out ProtocolError wrong));
        Assert.Equal(ProtocolError.WrongMessage, wrong);
    }

    [Fact]
    public void StructuredNoiseNeverEscapesTheDecoder()
    {
        var rng = new Random(19);
        for (int i = 0; i < 20_000; i++)
        {
            byte[] input = new byte[rng.Next(1, 60)];
            for (int j = 0; j < input.Length; j++)
                input[j] = rng.Next(3) == 0 ? (byte)rng.Next(256) : (byte)rng.Next(0, 'z' + 1);
            input[0] = 17;
            if (input.Length > 1 && rng.Next(2) == 0) input[1] = (byte)rng.Next(0, 2);
            if (Codec.TryDecode(input, out TooltipMessage value, out _))
                Assert.Equal(input, Codec.Encode(value));
            else
                Assert.Null(value);
        }
    }

    [Fact]
    public void TheReplicaKeepsTheTooltipAndForgetsItAtAJoin()
    {
        var early = new ReplicaWorld();
        Assert.Throws<ProtocolException>(() => early.Apply(Show()));

        ReplicaWorld replica = ReplicaWithSnapshot();
        var seen = new List<TooltipMessage>();
        replica.TooltipChanged += t => seen.Add(t);
        Assert.Null(replica.Tooltip);
        Assert.Throws<ProtocolException>(() => replica.Apply(Hide())); // the server sends changes only: nothing to hide

        replica.Apply(Show());
        Assert.Equal("default", replica.Tooltip.Theme);
        replica.Apply(Show("<p>Another</p>", "midnight")); // a new one replaces the shown one
        Assert.Equal("<p>Another</p>", replica.Tooltip.Text);
        replica.Apply(Hide());
        Assert.Null(replica.Tooltip);
        Assert.Equal(3, seen.Count);
        Assert.Null(seen[^1]);

        Assert.True(replica.TryApply(Codec.Encode(Show()), out MessageId id, out _));
        Assert.Equal(MessageId.Tooltip, id);
        Assert.NotNull(replica.Tooltip);
        var grid = new MapGrid { Tick = 9, Width = 8, Height = 8, Levels = 1 };
        grid.TurfTypes.Add("/turf/open/floor");
        grid.Runs.Add(new TurfRun(grid.CellCount, 0));
        replica.Apply(grid); // a join forgets it
        Assert.Null(replica.Tooltip);
    }
}
