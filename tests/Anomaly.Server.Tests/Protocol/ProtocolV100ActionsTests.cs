using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 100 also (ui-overhaul.md section 15, DV698; MessagesV100Actions.cs, Codec.V100Actions.cs): the Actions message, the own action
/// buttons with their names, active states and cooldowns, and tg's palette objects; every replica keeps the last one.
/// </summary>
public class ProtocolV100ActionsTests
{
    private static ActionsMessage Sample() => new()
    {
        Buttons = new[]
        {
            new ActionButtonState(12, "Toggle Light", false),
            new ActionButtonState(40, "Set Internals", true),
            new ActionButtonState(300000, "Fireball", false, CooldownEnd: 5120, CooldownTicks: 200),
            new ActionButtonState(300001, null, true, 10, 1),
        },
        Palette = new long[] { 7, 9 },
    };

    private static ActionsMessage RoundTrip(ActionsMessage message)
    {
        byte[] bytes = Codec.Encode(message);
        Assert.True(bytes.Length <= Limits.MaxActionsBytes);
        Assert.True(Codec.TryDecode(bytes, out ActionsMessage read, out ProtocolError error), error.ToString());
        Assert.Equal(bytes, Codec.Encode(read));
        return read;
    }

    [Fact]
    public void TheActionButtonsTheirStatesAndThePaletteRoundTrip()
    {
        ActionsMessage read = RoundTrip(Sample());
        Assert.Equal(Sample(), read);
        Assert.Equal("Set Internals", read.Find(40)?.Name);
        Assert.True(read.Find(40)?.Active);
        Assert.Equal((5120L, 200), (read.Find(300000).Value.CooldownEnd, read.Find(300000).Value.CooldownTicks));
        Assert.Null(read.Find(300001)?.Name);
        Assert.Null(read.Find(8));
        Assert.Equal(new long[] { 7, 9 }, read.Palette);
        Assert.NotEqual(Sample(), new ActionsMessage { Buttons = Sample().Buttons.Select((b, i) => i == 0 ? b with { Active = true } : b).ToArray(), Palette = Sample().Palette });
        // the bytes: the id, two buttons ... the first one: its id 12, its name, no flags
        byte[] bytes = Codec.Encode(Sample());
        Assert.Equal(new byte[] { 36, 4, 12, 13 }, bytes[..4]);
        Assert.Equal(MessageId.Actions, (MessageId)bytes[0]);
        Assert.Equal(new byte[] { 36, 0, 0 }, Codec.Encode(ActionsMessage.None));
        Assert.Equal(ActionsMessage.None, RoundTrip(ActionsMessage.None));
        // the widest message fits its cap
        var widest = new ActionsMessage
        {
            Buttons = Enumerable.Range(1, Limits.MaxActionButtons).Select(i => new ActionButtonState(long.MaxValue - Limits.MaxActionButtons + i, new string('x', Limits.MaxActionNameBytes), true, long.MaxValue, Limits.MaxActionCooldownTicks)).ToArray(),
            Palette = Enumerable.Range(1, Limits.MaxActionPalette).Select(i => (long)i).ToArray(),
        };
        Assert.Equal(widest, RoundTrip(widest));
    }

    [Fact]
    public void AMessageOutOfOrderOrWithAWrongCooldownOrPaletteIsRefused()
    {
        // out of order, twice, a palette object that is a button, a cooldown of 0 ticks with an end, past the cap, too many buttons: the encoder throws
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(new ActionsMessage { Buttons = new[] { new ActionButtonState(5, "a", false), new ActionButtonState(4, "b", false) } }));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(new ActionsMessage { Buttons = new[] { new ActionButtonState(5, "a", false), new ActionButtonState(5, "b", false) } }));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(new ActionsMessage { Buttons = new[] { new ActionButtonState(5, "a", false) }, Palette = new long[] { 5 } }));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(new ActionsMessage { Buttons = new[] { new ActionButtonState(5, "a", false, 10, 0) } }));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(new ActionsMessage { Buttons = new[] { new ActionButtonState(5, "a", false, 10, Limits.MaxActionCooldownTicks + 1) } }));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(new ActionsMessage { Buttons = Enumerable.Range(1, Limits.MaxActionButtons + 1).Select(i => new ActionButtonState(i, null, false)).ToArray() }));
        Assert.ThrowsAny<ProtocolException>(() => Codec.Encode(new ActionsMessage { Buttons = new[] { new ActionButtonState(1, new string('x', Limits.MaxActionNameBytes + 1), false) } }));
        // the decoder: an unknown flag bit, a cooldown of 0 ticks, a palette object out of order, bytes left over, cut short
        byte[] good = Codec.Encode(new ActionsMessage { Buttons = new[] { new ActionButtonState(5, "a", false, 10, 20) }, Palette = new long[] { 6, 8 } });
        Assert.True(Codec.TryDecode(good, out ActionsMessage _, out _));
        byte[] flags = (byte[])good.Clone();
        flags[5] |= 4; // [36][1][5][len 2]['a'][flags]
        Assert.False(Codec.TryDecode(flags, out ActionsMessage _, out ProtocolError error));
        Assert.Equal(ProtocolError.BadValue, error);
        byte[] zero = (byte[])good.Clone();
        zero[7] = 0; // the cooldown's length
        Assert.False(Codec.TryDecode(zero, out ActionsMessage _, out _));
        byte[] order = (byte[])good.Clone();
        (order[^2], order[^1]) = (8, 6);
        Assert.False(Codec.TryDecode(order, out ActionsMessage _, out _));
        Assert.False(Codec.TryDecode(good.Concat(new byte[] { 0 }).ToArray(), out ActionsMessage _, out _));
        Assert.False(Codec.TryDecode(good[..^1], out ActionsMessage _, out _));
    }

    [Fact]
    public void EveryReplicaTakesTheActionsAndTheCooldownShareRunsDownWithTheTick()
    {
        var replica = new ReplicaWorld();
        Assert.Same(ActionsMessage.None, replica.Actions);
        Assert.True(replica.TryApply(Codec.Encode(Sample()), out MessageId id, out ProtocolError error), error.ToString());
        Assert.Equal(MessageId.Actions, id);
        Assert.Equal(Sample(), replica.Actions);
        // the wheel: the whole of it when the cooldown starts, half at its middle, none at its end or past it, none without a cooldown
        ActionButtonState fireball = replica.Actions.Find(300000).Value;
        Assert.Equal(1f, fireball.CooldownLeft(5120 - 200));
        Assert.Equal(0.5f, fireball.CooldownLeft(5120 - 100), 3);
        Assert.Equal(0f, fireball.CooldownLeft(5120));
        Assert.Equal(0f, fireball.CooldownLeft(6000));
        Assert.Equal(0f, replica.Actions.Find(12).Value.CooldownLeft(0));
    }
}
