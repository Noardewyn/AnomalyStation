using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>Round trips, documented bytes and refusals of protocol version 2 (docs/ai/specs/protocol.md): MotionInput, Action, appearances, the private block, MapGrid's cells, the v2 records.</summary>
public class CodecV2Tests
{
    /// <summary>A MotionInput message of <paramref name="count"/> frames ending at seq <paramref name="newest"/>.</summary>
    internal static MotionInputMessage Input(uint newest, int count)
    {
        var m = new MotionInputMessage();
        for (int i = 0; i < count; i++)
        {
            uint seq = newest - (uint)(count - 1 - i);
            m.Frames.Add(new MotionInput(seq, (sbyte)(i * 10 - 20), (sbyte)(127 - i), (ushort)(seq * 100), MotionInputFlags.None));
        }

        return m;
    }

    /// <summary>An appearance entry that sets every field.</summary>
    internal static AppearanceEntry Rich(bool isPrivate = false, uint overlay = 0)
    {
        var e = new AppearanceEntry
        {
            Icon = new TableRef(2, isPrivate),
            IconState = new TableRef(3, isPrivate),
            Dir = 4,
            Rgba = 0x11223344,
            Alpha = 128,
            Layer = -1f,
            Plane = -32767,
            PixelX = -3,
            PixelY = 7,
            PixelW = 1,
            PixelZ = -1,
            Transform = new[] { 0f, 1f, 0f, -1f, 0f, 0.5f },
            AppearanceFlags = 0x1021,
            BlendMode = 2,
        };
        if (overlay != 0)
        {
            e.Overlays.Add(new TableRef(overlay, isPrivate));
            e.Underlays.Add(new TableRef(overlay, isPrivate));
        }

        return e;
    }

    // ---- MotionInput ----

    [Theory]
    [InlineData(1u, 1)]
    [InlineData(4u, 4)]
    [InlineData(1000u, 3)]
    [InlineData(uint.MaxValue, 4)]
    public void MotionInputRoundTripsAndFitsItsCap(uint newest, int count)
    {
        MotionInputMessage m = Input(newest, count);
        byte[] bytes = Codec.Encode(m);
        Assert.True(bytes.Length <= Codec.MaxMotionInputBytes && bytes.Length <= Limits.MaxMotionInputMessageBytes);
        Assert.Equal((byte)MessageId.MotionInput, bytes[0]);
        Assert.True(Codec.TryDecode(bytes, out MotionInputMessage decoded, out ProtocolError error), error.ToString());
        Assert.Equal(m.Frames, decoded.Frames);
    }

    [Fact]
    public void MotionInputHasTheDocumentedBytes()
    {
        var m = new MotionInputMessage();
        m.Frames.Add(new MotionInput(300, -127, 127, 0x1234, MotionInputFlags.None));
        m.Frames.Add(new MotionInput(301, 0, 5, 0xFFFF, MotionInputFlags.None));
        Assert.Equal(new byte[] { 9, 2, 0xAC, 0x02, 0x81, 127, 0x34, 0x12, 0, 0, 5, 0xFF, 0xFF, 0 }, Codec.Encode(m));
    }

    [Fact]
    public void MotionInputRefusesWhatTheDocumentationForbids()
    {
        var none = new MotionInputMessage();
        Assert.Throws<ProtocolException>(() => Codec.Encode(none));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Input(10, 5)) );
        var gap = new MotionInputMessage();
        gap.Frames.Add(new MotionInput(5, 0, 0, 0, MotionInputFlags.None));
        gap.Frames.Add(new MotionInput(7, 0, 0, 0, MotionInputFlags.None));
        Assert.Throws<ProtocolException>(() => Codec.Encode(gap));
        var zero = new MotionInputMessage();
        zero.Frames.Add(new MotionInput(0, 0, 0, 0, MotionInputFlags.None));
        Assert.Throws<ProtocolException>(() => Codec.Encode(zero));
        var min = new MotionInputMessage();
        min.Frames.Add(new MotionInput(1, -128, 0, 0, MotionInputFlags.None));
        Assert.Throws<ProtocolException>(() => Codec.Encode(min));
        var flags = new MotionInputMessage();
        flags.Frames.Add(new MotionInput(1, 0, 0, 0, (MotionInputFlags)1));
        Assert.Throws<ProtocolException>(() => Codec.Encode(flags));

        byte[] Craft(Action<Msg.RawWriter> body) => Msg.Raw(MessageId.MotionInput, body);
        Assert.True(Codec.TryDecode(Craft(r => r.Byte(1).VarUInt(1).Bytes(0, 0, 0, 0, 0)), out MotionInputMessage _, out ProtocolError error), error.ToString());
        foreach (byte[] bad in new[]
                 {
                     Craft(r => r.Byte(0).VarUInt(1)),
                     Craft(r => r.Byte(5).VarUInt(1).Bytes(new byte[25])),
                     Craft(r => r.Byte(1).VarUInt(0).Bytes(0, 0, 0, 0, 0)),
                     Craft(r => r.Byte(1).VarUInt(1).Bytes(0x80, 0, 0, 0, 0)), // move -128
                     Craft(r => r.Byte(1).VarUInt(1).Bytes(0, 0x80, 0, 0, 0)),
                     Craft(r => r.Byte(1).VarUInt(1).Bytes(0, 0, 0, 0, 1)), // a flag bit
                     Craft(r => r.Byte(2).VarUInt(uint.MaxValue).Bytes(new byte[10])), // seqs past 32 bits
                     Craft(r => r.Byte(2).VarUInt(1).Bytes(0, 0, 0, 0, 0)), // one frame short
                     Craft(r => r.Byte(1).VarUInt(1).Bytes(0, 0, 0, 0, 0, 0)), // a byte too many
                 })
        {
            Assert.False(Codec.TryDecode(bad, out MotionInputMessage value, out error));
            Assert.Null(value);
            Assert.NotEqual(ProtocolError.None, error);
        }

        // Over the cap, unread.
        Assert.False(Codec.TryDecode(new byte[Limits.MaxMotionInputMessageBytes + 1].Select((_, i) => i == 0 ? (byte)9 : (byte)0).ToArray(), out MotionInputMessage _, out error));
        Assert.Equal(ProtocolError.TooLong, error);
    }

    // ---- Action ----

    [Theory]
    [InlineData(ActionKind.Rest)]
    [InlineData(ActionKind.ToggleMoveIntent)]
    [InlineData(ActionKind.ToggleCombatMode)]
    public void SimpleActionsRoundTrip(ActionKind kind)
    {
        var a = new ActionMessage(123456, kind, 0, ClickButton.None, ClickModifiers.None, null);
        byte[] bytes = Codec.Encode(a);
        Assert.Equal(new byte[] { 10, 0xC0, 0xC4, 0x07, (byte)kind }, bytes);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage decoded, out ProtocolError error), error.ToString());
        Assert.Equal(a, decoded);
    }

    [Fact]
    public void ClickAndDevCommandRoundTripAtTheirExtremes()
    {
        foreach (ClickButton button in new[] { ClickButton.Left, ClickButton.Right, ClickButton.Middle })
        {
            var click = new ActionMessage(uint.MaxValue, ActionKind.Click, long.MaxValue, button, ClickModifiers.All, null) { TargetKind = TargetKind.Entity };
            byte[] bytes = Codec.Encode(click);
            Assert.True(bytes.Length <= Limits.MaxActionMessageBytes);
            Assert.True(Codec.TryDecode(bytes, out ActionMessage decoded, out ProtocolError error), error.ToString());
            Assert.Equal(click, decoded);
        }

        // A version-3 click (kind None, a target) is written as an entity click: [target kind 1] [target 7] [button] [modifiers] [icon-x 0] [icon-y 0].
        Assert.Equal(new byte[] { 10, 5, 4, 1, 7, 2, 4, 0, 0 }, Codec.Encode(new ActionMessage(5, ActionKind.Click, 7, ClickButton.Right, ClickModifiers.Alt, null)));

        string longest = new('x', Limits.MaxDevCommandBytes);
        var dev = new ActionMessage(9, ActionKind.DevCommand, 0, ClickButton.None, ClickModifiers.None, longest);
        byte[] devBytes = Codec.Encode(dev);
        Assert.True(devBytes.Length <= Limits.MaxActionMessageBytes && devBytes.Length <= Limits.MaxInboundMessageBytes);
        Assert.True(Codec.TryDecode(devBytes, out ActionMessage decodedDev, out _));
        Assert.Equal(dev, decodedDev);
        Assert.Throws<ProtocolException>(() => Codec.Encode(dev with { Text = longest + "x" }));
        var empty = dev with { Text = "" };
        Assert.True(Codec.TryDecode(Codec.Encode(empty), out decodedDev, out _));
        Assert.Equal("", decodedDev.Text);
    }

    [Fact]
    public void ActionRefusesWhatTheDocumentationForbids()
    {
        var rest = new ActionMessage(1, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null);
        Assert.Throws<ProtocolException>(() => Codec.Encode(rest with { Kind = ActionKind.None }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(rest with { Kind = ActionKinds.Last + 1 })); // version 24: Topic (14) is the last kind
        Assert.Throws<ProtocolException>(() => Codec.Encode(rest with { Target = 5 }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(rest with { Button = ClickButton.Left }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(rest with { Text = "x" }));
        var click = new ActionMessage(1, ActionKind.Click, 5, ClickButton.Left, ClickModifiers.None, null);
        Assert.Throws<ProtocolException>(() => Codec.Encode(click with { Target = 0 }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(click with { Target = -3 }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(click with { Button = ClickButton.None }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(click with { Button = (ClickButton)4 }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(click with { Modifiers = (ClickModifiers)8 }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(click with { Text = "x" }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new ActionMessage(1, ActionKind.DevCommand, 0, ClickButton.None, ClickModifiers.None, null)));

        byte[] Craft(Action<Msg.RawWriter> body) => Msg.Raw(MessageId.Action, body);
        foreach (byte[] bad in new[]
                 {
                     Craft(r => r.VarUInt(1).Byte(0)),
                     Craft(r => r.VarUInt(1).Byte(6)),
                     Craft(r => r.VarUInt(1UL << 32).Byte(1)),
                     Craft(r => r.VarUInt(1).Byte(1).Byte(0)), // a trailing byte on a rest
                     Craft(r => r.VarUInt(1).Byte(4).Byte(1).VarUInt(0).Byte(1).Byte(0).Byte(0).Byte(0)), // a click on target 0
                     Craft(r => r.VarUInt(1).Byte(4).Byte(1).VarUInt(5).Byte(0).Byte(0).Byte(0).Byte(0)), // button none
                     Craft(r => r.VarUInt(1).Byte(4).Byte(1).VarUInt(5).Byte(4).Byte(0).Byte(0).Byte(0)),
                     Craft(r => r.VarUInt(1).Byte(4).Byte(1).VarUInt(5).Byte(1).Byte(0x28).Byte(0).Byte(0)), // zone 10 (version 100: bit 3 alone, 0x08, is the mouth)
                     Craft(r => r.VarUInt(1).Byte(4).Byte(1).VarUInt(1UL << 63).Byte(1).Byte(0).Byte(0).Byte(0)),
                     Craft(r => r.VarUInt(1).Byte(4).VarUInt(5).Byte(1).Byte(0)), // a version-3 click: no target kind
                     Craft(r => r.VarUInt(1).Byte(5).Byte(0)), // a null command
                     Craft(r => r.VarUInt(1).Byte(5).String(new string('x', Limits.MaxDevCommandBytes + 1))),
                     Craft(r => r.VarUInt(1).Byte(5).Byte(2).Byte(0xFF)), // invalid UTF-8
                 })
        {
            Assert.False(Codec.TryDecode(bad, out ActionMessage _, out ProtocolError error), "accepted " + BitConverter.ToString(bad));
            Assert.NotEqual(ProtocolError.None, error);
        }
    }

    // ---- the appearance entries, in a Snapshot's public table ----

    [Fact]
    public void AnAppearanceEntryWithEveryFieldRoundTrips()
    {
        var plain = new AppearanceEntry { Icon = TableRef.Public(1) };
        var matrix = new AppearanceEntry { IconState = TableRef.Public(1), ColorMatrix = Enumerable.Range(0, 20).Select(i => i * 0.25f - 1f).ToArray() };
        AppearanceEntry rich = Rich(false, overlay: 1);
        Snapshot s = Msg.Snapshot(5, 0, 0, new string[0]);
        s.Resources.AddRange(new[] { "icons/mob/human.dmi", "icons/obj/items.dmi", "gags:/datum/greyscale_config/jumpsuit|#ff0000" });
        s.Strings.AddRange(new[] { "head_m", "l_arm", "torso" });
        s.Appearances.Add(plain);
        s.Appearances.Add(matrix);
        s.Appearances.Add(rich);
        Assert.True(Codec.TryDecode(Codec.Encode(s), out Snapshot decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(s, decoded);
    }

    [Fact]
    public void AnEntryWithNothingSetIsOneByteAndAnAlphaOf255IsNotSent()
    {
        Snapshot s = Msg.Snapshot(5, 0, 0, new string[0]);
        s.Appearances.Add(new AppearanceEntry());
        byte[] bytes = Codec.Encode(s);
        // id, tick 5, seq 0, own 0, 0 types, 0 resources, 0 strings, 1 appearance (presence 0), 0 entities, the empty private block
        Assert.Equal(new byte[] { 4, 5, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 }, bytes);
        s.Appearances[0] = new AppearanceEntry { Alpha = 254 };
        Assert.Equal(new byte[] { 4, 5, 0, 0, 0, 0, 0, 1, 0x20, 254, 0, 0, 0, 0, 0 }, Codec.Encode(s));
    }

    [Fact]
    public void AppearanceEntriesAreRefusedWhenTheyBreakTheRules()
    {
        Snapshot With(AppearanceEntry e, params AppearanceEntry[] before)
        {
            Snapshot s = Msg.Snapshot(5, 0, 0, new string[0]);
            s.Appearances.AddRange(before);
            s.Appearances.Add(e);
            return s;
        }

        var ok = new AppearanceEntry { Icon = TableRef.Public(1) };
        // An overlay names an earlier entry only; a public entry never names a private one (D40); references have a range.
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new AppearanceEntry { Overlays = { TableRef.Public(1) } })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new AppearanceEntry { Overlays = { TableRef.Public(2) } }, ok)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new AppearanceEntry { Overlays = { TableRef.None } }, ok)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new AppearanceEntry { Underlays = { new TableRef(1, true) } }, ok)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new AppearanceEntry { Icon = new TableRef(1, true) })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new AppearanceEntry { IconState = new TableRef(1, true) })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new AppearanceEntry { Icon = new TableRef((uint)Limits.MaxResourceTableEntries + 1, false) })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new AppearanceEntry { Dir = 3 })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new AppearanceEntry { ColorMatrix = new float[19] })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new AppearanceEntry { Transform = new float[5] })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new AppearanceEntry { Layer = float.NaN })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new AppearanceEntry { Transform = new[] { 1f, 0f, 0f, 0f, float.PositiveInfinity, 0f } })));
        var many = new AppearanceEntry();
        for (int i = 0; i <= Limits.MaxAppearanceOverlays; i++) many.Overlays.Add(TableRef.Public(1));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(many, ok)));
        many.Overlays.RemoveAt(0);
        Assert.True(Codec.TryDecode(Codec.Encode(With(many, ok)), out Snapshot _, out _));

        // The decoder holds the same rules, and refuses a field that is present at its default (one encoding per entry).
        byte[] Craft(Action<Msg.RawWriter> entries, int count = 1) => Msg.Raw(MessageId.Snapshot, r =>
        {
            r.VarUInt(5).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(1).String("a").VarUInt(1).String("b").VarUInt((ulong)count);
            entries(r);
            r.VarUInt(0).Byte(0).Byte(0).VarUInt(0).Byte(0);
        });

        Assert.True(Codec.TryDecode(Craft(r => r.VarUInt(1).VarUInt(2)), out Snapshot _, out ProtocolError error), error.ToString());
        foreach (byte[] bad in new[]
                 {
                     Craft(r => r.VarUInt(1 << 15)), // an undefined presence bit
                     Craft(r => r.VarUInt(1).VarUInt(0)), // icon present and none
                     Craft(r => r.VarUInt(1).VarUInt(1)), // a private icon in a public table
                     Craft(r => r.VarUInt(1).VarUInt(2UL * 65537)), // an icon index past the cap
                     Craft(r => r.VarUInt(1 << 2).Byte(0)), // dir 0 present
                     Craft(r => r.VarUInt(1 << 2).Byte(3)),
                     Craft(r => r.VarUInt(1 << 3).Bytes(0xFF, 0xFF, 0xFF, 0xFF)), // white present
                     Craft(r => r.VarUInt(1 << 5).Byte(255)), // alpha 255 present
                     Craft(r => r.VarUInt(1 << 6).Bytes(0, 0, 0, 0)), // layer +0 present
                     Craft(r => r.VarUInt(1 << 6).Bytes(0, 0, 0xC0, 0x7F)), // layer NaN
                     Craft(r => r.VarUInt(1 << 7).VarInt(0)), // plane 0 present
                     Craft(r => r.VarUInt(1 << 7).VarInt(70000)), // plane past 16 bits
                     Craft(r => r.VarUInt(1 << 8).VarInt(0).VarInt(0)),
                     Craft(r => r.VarUInt(1 << 9).VarInt(0).VarInt(0)),
                     Craft(r => r.VarUInt(1 << 11).VarUInt(0)), // flags 0 present
                     Craft(r => r.VarUInt(1 << 11).VarUInt(70000)),
                     Craft(r => r.VarUInt(1 << 12).Byte(0)),
                     Craft(r => r.VarUInt(1 << 13).VarUInt(0)), // an empty overlay list present
                     Craft(r => r.VarUInt(1 << 13).VarUInt(1).VarUInt(2)), // an overlay naming the entry itself (index 1 of a 1-entry table)
                     Craft(r => r.VarUInt(1 << 13).VarUInt(1).VarUInt(3)), // an overlay naming a private entry
                     Craft(r => r.VarUInt(1 << 13).VarUInt(1).VarUInt(0)),
                     Craft(r => r.VarUInt(1 << 13).VarUInt(1000)), // a count the bytes cannot hold
                 })
        {
            Assert.False(Codec.TryDecode(bad, out Snapshot value, out error), "accepted " + BitConverter.ToString(bad));
            Assert.Null(value);
        }
    }

    // ---- the private block ----

    internal static OwnBlock RichOwn()
    {
        var own = new OwnBlock
        {
            HasMotion = true,
            Motion = new MotionState
            {
                X = 10 * 256 + 17, Y = 4 * 256 - 100, Z = 2, Yaw = 0xC000, Posture = Posture.Lying, Driver = MotionDriver.Prone,
                Slide = new Slide(11 * 256, 4 * 256, 3), PressTimer = -2, VelX = -12345, VelY = 678, // version 66 (DV263)
            },
            Params = new MotionParams(1f / 3f, 0.175f, true, 3, Feel: MoveFeel.Default),
            InputBufferDepth = 2,
            FirstNewPrivateString = 4,
            FirstNewPrivateResource = 2,
            FirstNewPrivateAppearance = 5,
        };
        own.NewPrivateStrings.AddRange(new[] { "head3", "chest0" });
        own.NewPrivateResources.Add("icons/hud/screen_gen.dmi");
        own.NewPrivateAppearances.Add(Rich(true));
        own.NewPrivateAppearances.Add(Rich(true, overlay: 5));
        own.ScreenAdded.Add(new ScreenEntry(30, new TableRef(5, true), "WEST,CENTER-2"));
        own.ScreenAdded.Add(new ScreenEntry(40, TableRef.Public(7), null));
        own.ScreenChanged.Add(new ScreenEntry(12, new TableRef(6, true), "EAST-1:28,CENTER-1:15"));
        own.ScreenRemoved.AddRange(new long[] { 3, 9 });
        return own;
    }

    [Fact]
    public void ThePrivateBlockRoundTripsInASnapshotAndADelta()
    {
        OwnBlock own = RichOwn();
        Snapshot s = Msg.Snapshot(9, 4, 5, new string[0]);
        s.Own = own;
        // A Snapshot lists nothing as changed or removed (the replica enforces it, the codec does not know the message's role).
        Assert.True(Codec.TryDecode(Codec.Encode(s), out Snapshot decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(s, decoded);

        Delta d = Msg.Delta(10, 4, 5);
        d.Own = own;
        byte[] whole = Codec.Encode(d);
        Assert.True(Codec.TryDecode(whole, out Delta decodedDelta, out error), error.ToString());
        Msg.AssertEqual(d, decodedDelta);
        Msg.AssertEqual(d.Own, decodedDelta.Own);

        // The host's way: the shared body, the session's block encoded on its own, header and both put together.
        byte[] body = Codec.EncodeDeltaBody(d);
        var ownBytes = new byte[Codec.MaxOwnBlockSize(own)];
        int ownLength = Codec.EncodeOwnBlock(own, ownBytes);
        var message = new byte[Codec.MaxDeltaHeaderBytes + body.Length + ownLength];
        int length = Codec.EncodeDelta(d.Tick, d.LastInputSeq, d.OwnEntity, body, ownBytes.AsSpan(0, ownLength), message);
        Assert.Equal(whole, message.AsSpan(0, length).ToArray());
        var viaObject = new byte[Codec.MaxDeltaHeaderBytes + body.Length + Codec.MaxOwnBlockSize(own)];
        length = Codec.EncodeDelta(d.Tick, d.LastInputSeq, d.OwnEntity, body, own, viaObject);
        Assert.Equal(whole, viaObject.AsSpan(0, length).ToArray());
        Assert.True(Codec.MaxOwnBlockSize(own) >= ownLength);
    }

    // The owner's play test (2026-10-09): the body zone a click aims at (DV30) is bits 4-6 of the modifiers byte.
    [Fact]
    public void TheAimZoneRoundTripsInTheModifiersByteAndAnUnknownOneIsRefused()
    {
        var click = new ActionMessage(5, ActionKind.Click, 7, ClickButton.Left, ClickModifiers.Shift, null) { TargetKind = TargetKind.Entity, IconX = 16, IconY = 27, AimZone = AimZone.Head };
        byte[] bytes = Codec.Encode(click);
        Assert.Equal(new byte[] { 10, 5, 4, 1, 7, 1, 0x11, 16, 27 }, bytes);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage decoded, out ProtocolError error), error.ToString());
        Assert.Equal(click, decoded);
        // version 100 (slice T4a): zone 7 is the eyes, and bit 3 is no modifier but the zone's high bit (0x09: shift, zone 8, the mouth)
        bytes[6] = 0x71;
        Assert.True(Codec.TryDecode(bytes, out decoded, out error), error.ToString());
        Assert.Equal((ClickModifiers.Shift, AimZone.Eyes), (decoded.Modifiers, decoded.AimZone));
        bytes[6] = 0x09;
        Assert.True(Codec.TryDecode(bytes, out decoded, out error), error.ToString());
        Assert.Equal((ClickModifiers.Shift, AimZone.Mouth), (decoded.Modifiers, decoded.AimZone));
        bytes[6] = 0x29; // zone 10, past the last
        Assert.False(Codec.TryDecode(bytes, out ActionMessage _, out _));
        bytes[6] = 0x79; // zone 15
        Assert.False(Codec.TryDecode(bytes, out ActionMessage _, out _));
        // corrected by slice T4b (version 100 also): a hover on an entity carries a zone now; a turf hover's zone is still refused, as is one on a keybinding
        Assert.ThrowsAny<Exception>(() => Codec.Encode(new ActionMessage(5, ActionKind.Hover, 0, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Turf, TurfX = 3, TurfY = 3, TurfZ = 1, AimZone = AimZone.Head }));
        Assert.ThrowsAny<Exception>(() => Codec.Encode(new ActionMessage(5, ActionKind.Keybinding, 0, ClickButton.None, ClickModifiers.None, null) { Keybinding = KeybindingCode.SwapHands, AimZone = AimZone.Head }));
    }

    // The owner's play test (2026-10-09): the dev noclip reaches the predictor as the motion byte's bit 32 (DV29).
    [Fact]
    public void TheNoclipFlagRoundTripsInThePrivateBlock()
    {
        OwnBlock own = RichOwn();
        own.Params = own.Params with { NoClip = true };
        Delta d = Msg.Delta(10, 4, 5);
        d.Own = own;
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out ProtocolError error), error.ToString());
        Assert.True(decoded.Own.Params.NoClip);
        Msg.AssertEqual(d.Own, decoded.Own);
    }

    [Fact]
    public void TheEmptyPrivateBlockIsFourBytesAndAMotionBlockIsSmall()
    {
        var empty = new OwnBlock();
        var buffer = new byte[64];
        Assert.Equal(Codec.EmptyOwnBlockBytes, Codec.EncodeOwnBlock(empty, buffer));
        var motion = new OwnBlock { HasMotion = true, Motion = MotionState.AtTile(10, 10, 1), Params = MotionParams.DefaultRun, InputBufferDepth = 1 };
        int length = Codec.EncodeOwnBlock(motion, buffer);
        Assert.InRange(length, 20, 47); // about 16 bytes of state and 8 of parameters (scope 2d), the velocity and the feel's 10 (DV263), the flaps byte (DV360), the handhold halt byte (version 89)
        Delta withMotion = Msg.Delta(1);
        withMotion.Own = motion;
        Assert.True(Codec.TryDecode(Codec.Encode(withMotion), out Delta d, out _));
        Assert.Equal(MotionState.AtTile(10, 10, 1), d.Own.Motion);
        Assert.Equal(MotionParams.DefaultRun, d.Own.Params);
        Assert.Equal(1, d.Own.InputBufferDepth);
    }

    [Fact]
    public void ThePrivateBlockRefusesWhatTheDocumentationForbids()
    {
        OwnBlock Good() => new() { HasMotion = true, Motion = MotionState.AtTile(5, 5, 1), Params = MotionParams.DefaultRun };
        void Encodes(OwnBlock own) => Assert.Throws<ProtocolException>(() => Codec.EncodeOwnBlock(own, new byte[4096]));
        OwnBlock WithMotion(Func<MotionState, MotionState> change)
        {
            OwnBlock block = Good();
            block.Motion = change(block.Motion);
            return block;
        }

        Encodes(WithMotion(m => m with { Z = 0 }));
        Encodes(WithMotion(m => m with { X = -1 }));
        Encodes(WithMotion(m => m with { X = 256 * 300 }));
        Encodes(WithMotion(m => m with { Posture = (Posture)2 }));
        Encodes(WithMotion(m => m with { Driver = (MotionDriver)9 }));
        Encodes(WithMotion(m => m with { Slide = new Slide(5, 0, 0) }));
        OwnBlock o = Good();
        o.Params = o.Params with { TilesPerTick = float.NaN };
        Encodes(o);
        o = Good();
        o.Params = o.Params with { TilesPerTick = Limits.MaxTilesPerTick + 1 };
        Encodes(o);
        o = Good();
        o.Params = o.Params with { Radius = -1f };
        Encodes(o);
        o = Good();
        o.Params = o.Params with { MoveDelayTicks = -1 };
        Encodes(o);
        o = Good();
        o.FirstNewPrivateString = 0;
        o.NewPrivateStrings.Add("x");
        Encodes(o);
        o = Good();
        o.FirstNewPrivateString = 1;
        o.NewPrivateStrings.Add(null);
        Encodes(o);
        o = Good();
        o.ScreenAdded.Add(new ScreenEntry(5, TableRef.None, "a"));
        o.ScreenAdded.Add(new ScreenEntry(5, TableRef.None, "a")); // not strictly increasing
        Encodes(o);
        o = Good();
        o.ScreenAdded.Add(new ScreenEntry(0, TableRef.None, "a"));
        Encodes(o);
        o = Good();
        o.ScreenAdded.Add(new ScreenEntry(5, new TableRef(0, true), "a"));
        Encodes(o);
        o = Good();
        o.ScreenAdded.Add(new ScreenEntry(5, TableRef.None, new string('x', Limits.MaxScreenLocBytes + 1)));
        Encodes(o);
        o = Good();
        for (int i = 1; i <= Limits.MaxScreenEntries + 1; i++) o.ScreenAdded.Add(new ScreenEntry(i, TableRef.None, null));
        Encodes(o);
        o = Good();
        o.NewPrivateAppearances.Add(Rich(true));
        o.FirstNewPrivateAppearance = 1; // its overlay-free entry is fine, but an overlay naming itself is not
        o.NewPrivateAppearances[0].Overlays.Add(new TableRef(1, true));
        Encodes(o);

        // The decoder: undefined flag bits, a flagged part that is empty, an invalid motion.
        byte[] Own(Action<Msg.RawWriter> r) => Msg.Raw(MessageId.Delta, w => { w.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0); r(w); }); // ... ceilings, sun, turf lights (version 96)
        Assert.True(Codec.TryDecode(Own(r => r.Byte(0).Byte(0).VarUInt(0).Byte(0)), out Delta _, out ProtocolError error), error.ToString());
        foreach (byte[] bad in new[]
                 {
                     Own(r => r.Byte(8).Byte(0)),
                     Own(r => r.Byte(0).Byte(0).VarUInt(0).Byte(2)), // version 43: an undefined flag bit
                     Own(r => r.Byte(0).Byte(0).VarUInt(0).Byte(1).VarUInt(1).VarUInt(0)), // version 43: an empty access
                     Own(r => r.Byte(2).Byte(0).VarUInt(0).VarUInt(0).VarUInt(0)), // tables flagged, none added
                     Own(r => r.Byte(4).Byte(0).VarUInt(0).VarUInt(0).VarUInt(0)), // screen flagged, none listed
                     Own(r => r.Byte(1).Bytes(0, 0, 0, 0)), // motion at x = 0: tile 0 is outside the map
                     Own(r => r.Byte(1).Bytes(0, 5, 0, 5).VarUInt(0)), // z = 0
                     Own(r => r.Byte(1).Bytes(0, 5, 0, 5).VarUInt(1).Bytes(0, 0).Byte(0x40).VarInt(0).Bytes(0, 0, 0, 0, 0, 0, 0, 0).VarUInt(3).Byte(0)), // undefined packed bit (bit 5 is the noclip)
                     Own(r => r.Byte(1).Bytes(0, 5, 0, 5).VarUInt(1).Bytes(0, 0).Byte(0x10).VarInt(0).Bytes(0, 5, 0, 5).VarUInt(0)), // an active slide with no ticks
                     Own(r => r.Byte(1).Bytes(0, 5, 0, 5).VarUInt(1).Bytes(0, 0).Byte(0).VarInt(100000)), // a press timer past 16 bits
                 })
        {
            Assert.False(Codec.TryDecode(bad, out Delta value, out error), "accepted " + BitConverter.ToString(bad));
            Assert.Null(value);
        }
    }

    // ---- MapGrid version 2 ----

    [Fact]
    public void MapGridWithTurfAppearancesAndSolidityRoundTrips()
    {
        MapGrid grid = Msg.Map(4, 3, 1, new[] { "/turf/closed/wall", "/turf/open/floor" }, new TurfRun(4, 0), new TurfRun(4, 1), new TurfRun(4, 0));
        grid.AppearanceRuns.Add(new TurfRun(4, 3));
        grid.AppearanceRuns.Add(new TurfRun(4, 1));
        grid.AppearanceRuns.Add(new TurfRun(4, 3));
        grid.SolidBits = new byte[] { 0x0F, 0x0F };
        byte[] bytes = Codec.Encode(grid);
        Assert.True(Codec.TryDecode(bytes, out MapGrid decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(grid, decoded);
        Assert.True(decoded.IsSolid(1, 1, 1) && decoded.IsSolid(4, 1, 1));
        Assert.False(decoded.IsSolid(1, 2, 1));
        Assert.True(decoded.IsSolid(1, 3, 1));
        Assert.True(decoded.IsSolid(5, 1, 1)); // outside the map

        // Without them (a map built the version-1 way) both parts are absent: the map has no appearances and nothing solid.
        MapGrid plain = Msg.FlatMap(3, 3);
        Assert.True(Codec.TryDecode(Codec.Encode(plain), out decoded, out _));
        Assert.Empty(decoded.AppearanceRuns);
        Assert.Empty(decoded.SolidBits);
        Assert.False(decoded.IsSolid(2, 2, 1));
    }

    [Fact]
    public void MapGridV2RefusesWhatTheDocumentationForbids()
    {
        MapGrid Base()
        {
            MapGrid g = Msg.FlatMap(3, 3);
            g.AppearanceRuns.Add(new TurfRun(9, 1));
            g.SolidBits = new byte[2];
            return g;
        }

        MapGrid g = Base();
        g.AppearanceRuns.Clear();
        g.AppearanceRuns.Add(new TurfRun(8, 1)); // does not cover the map
        Assert.Throws<ProtocolException>(() => Codec.Encode(g));
        g = Base();
        g.AppearanceRuns[0] = new TurfRun(10, 1);
        Assert.Throws<ProtocolException>(() => Codec.Encode(g));
        g = Base();
        g.AppearanceRuns[0] = new TurfRun(9, Limits.MaxAppearanceTableEntries + 1);
        Assert.Throws<ProtocolException>(() => Codec.Encode(g));
        g = Base();
        g.SolidBits = new byte[3];
        Assert.Throws<ProtocolException>(() => Codec.Encode(g));
        g = Base();
        g.SolidBits = new byte[] { 0, 0x02 }; // cell 10 does not exist
        Assert.Throws<ProtocolException>(() => Codec.Encode(g));

        byte[] Craft(Action<Msg.RawWriter> tail) => Msg.Raw(MessageId.MapGrid, r =>
        {
            r.VarUInt(0).VarUInt(3).VarUInt(3).VarUInt(1).VarUInt(1).String("/t").VarUInt(1).VarUInt(9).VarUInt(0);
            tail(r);
            r.VarUInt(0).VarUInt(0).VarUInt(0).Bytes(0, 0, 0, 0); // version 3: no flag runs, no base lights, no base-light runs, no starlight
            r.VarUInt(0).VarUInt(0); // version 5: no gas kinds, no gas cells
            r.VarUInt(0); // version 44: no ceiling cells
            r.VarUInt(0); // version 60: no levels without parallax
            r.VarUInt(0); // version 76: no linked levels
            r.VarUInt(0); // version 87: the game's own sky and sun
            r.Bytes(0xD0, 0x07); // version 91: walls and ceiling 2000 mm high
            r.VarUInt(0); // version 96: no turf lights
        });

        Assert.True(Codec.TryDecode(Craft(r => r.VarUInt(1).VarUInt(9).VarUInt(1).VarUInt(2).Bytes(0xFF, 0x01)), out MapGrid _, out ProtocolError error), error.ToString());
        foreach (byte[] bad in new[]
                 {
                     Craft(r => r.VarUInt(1).VarUInt(8).VarUInt(1).VarUInt(0)),
                     Craft(r => r.VarUInt(1).VarUInt(10).VarUInt(1).VarUInt(0)),
                     Craft(r => r.VarUInt(1).VarUInt(9).VarUInt(1UL << 30).VarUInt(0)),
                     Craft(r => r.VarUInt(0).VarUInt(1).Byte(0)), // one byte of bits for nine cells
                     Craft(r => r.VarUInt(0).VarUInt(3).Bytes(0, 0, 0)),
                     Craft(r => r.VarUInt(0).VarUInt(2).Bytes(0, 0xFE)), // padding bits set
                     Craft(r => r.VarUInt(0).VarUInt(2).Byte(0)), // truncated
                     Craft(r => r.VarUInt(1).VarUInt(0).VarUInt(1).VarUInt(0)), // a run of no cells
                 })
        {
            Assert.False(Codec.TryDecode(bad, out MapGrid value, out error), "accepted " + BitConverter.ToString(bad));
            Assert.Null(value);
        }
    }

    // ---- the records ----

    [Fact]
    public void CapsuleEntitiesPostureAndAppearanceRoundTripInEveryRecord()
    {
        EntityState capsule = Msg.State(5, 1, 3, 4, 1, 4, "Al", 0f, 90) with
        {
            HasPos = true, PosX = 3 * 256 + 40, PosY = 4 * 256 - 17, Yaw = 200, Posture = PostureBits.Encode(true, 270), Appearance = TableRef.Public(2),
        };
        EntityState prop = Msg.State(6, 0, 255, 255, 16, 0, null, 32f, 100) with { Appearance = TableRef.Public(1), FlickAppearance = TableRef.Public(2), FlickTick = 97, Animation = AnimationCodecTests.Sample(95), IsVisual = true, VisHost = 5 }; // version 8: a flick 3 ticks ago; version 9: an animation; version 10: a visual on entity 5
        Snapshot s = Msg.Snapshot(100, 5, 3, new[] { "/mob", "/obj" }, capsule, prop);
        s.Resources.Add("icons/a.dmi");
        s.Strings.Add("s");
        s.Appearances.Add(new AppearanceEntry { Icon = TableRef.Public(1) });
        s.Appearances.Add(new AppearanceEntry { IconState = TableRef.Public(1), Overlays = { TableRef.Public(1) } });
        Assert.True(Codec.TryDecode(Codec.Encode(s), out Snapshot decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(s, decoded);
        Assert.True(decoded.Entities[0].HasPos);
        Assert.False(decoded.Entities[1].HasPos);
        Assert.Equal(255 * 256, decoded.Entities[1].PosX);

        Delta d = Msg.Delta(200, spawned: new[] { capsule with { Id = 70, LastMoveTick = 199 } });
        d.Changed.Add(new EntityChange { Id = 5, Mask = NetField.Changeable & ~NetField.Light, X = 4, Y = 4, Z = 2, Dir = 8, Name = "Bo", GlideSize = 4f, LastMoveTick = 150, PosX = 4 * 256, PosY = 4 * 256, Yaw = 1, Posture = 0, Appearance = TableRef.Public(2), Flags = EntityFlags.Thrown, FlickAppearance = TableRef.Public(1), FlickTick = 200, Animation = AnimationCodecTests.Sample(190), VisHost = 9, Door = new DoorState(DoorFlags.Openable, 2, new[] { "engineering" }, new string[0]) });
        d.Changed.Add(new EntityChange { Id = 6, Mask = NetField.Posture, Posture = 0b11111 });
        d.Changed.Add(new EntityChange { Id = 9, Mask = NetField.Appearance, Appearance = TableRef.None });
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decodedDelta, out error), error.ToString());
        Msg.AssertEqual(d, decodedDelta);
    }

    [Fact]
    public void TheChangeMaskIsAVaruintWithTheCommonFieldsInTheFirstByte()
    {
        // A mover's usual change (Pos and Yaw) takes one mask byte; X, Y and the move tick the same; Appearance the second.
        Delta d = Msg.Delta(50, changed: new[]
        {
            new EntityChange { Id = 3, Mask = NetField.Pos | NetField.Yaw, PosX = 1000, PosY = 2000, Yaw = 7 },
            new EntityChange { Id = 4, Mask = NetField.Appearance, Appearance = TableRef.Public(300) },
        });
        byte[] body = Codec.EncodeDeltaBody(d);
        // [0 first type] [0 types] [0 parts] [0 spawned] [2 changed] id 3 (zigzag 6), mask 0b11, pos 1000 and 2000 as u16 pairs, yaw 7;
        // id 4 (diff +1: zigzag 2), mask Appearance (wire bit 7 = 0x80: two bytes 0x80 0x01), appearance 300 (0xAC 0x02); [0 despawned]
        Assert.Equal(new byte[] { 0, 0, 0, 0, 2, 6, 0b11, 0xE8, 0x03, 0xD0, 0x07, 7, 2, 0x80, 0x01, 0xAC, 0x02, 0, 0, 0, 0 }, body); // ... [0 ceilings] [0 sun] [0 turf lights]
    }

    [Fact]
    public void RecordsOfVersion2RefuseWhatTheDocumentationForbids()
    {
        string[] types = { "/mob" };
        EntityState capsule = Msg.State(1) with { HasPos = true, PosX = 256, PosY = 256 };
        foreach (EntityState bad in new[]
                 {
                     capsule with { PosX = 0 }, capsule with { PosX = 127 }, capsule with { PosY = 256 * 255 + 128 }, capsule with { PosX = -5 },
                     // version 100 (DV690) defines bits 6 and 7, so 255 is a valid byte now; a turned side on a standing body or face up without a fixed side is not
                     capsule with { PosX = 70000 }, Msg.State(1) with { Posture = 64 }, Msg.State(1) with { Posture = 128 | 1 }, Msg.State(1) with { Posture = 254 },
                     Msg.State(1) with { Appearance = new TableRef(1, true) }, Msg.State(1) with { Appearance = TableRef.Public((uint)Limits.MaxAppearanceTableEntries + 1) },
                 })
        {
            Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Snapshot(5, 0, 0, types, bad)));
        }

        Delta WithChange(EntityChange c) => Msg.Delta(5, changed: new[] { c });
        Assert.Throws<ProtocolException>(() => Codec.Encode(WithChange(new EntityChange { Id = 1, Mask = NetField.Pos, PosX = 0, PosY = 256 })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(WithChange(new EntityChange { Id = 1, Mask = NetField.Posture, Posture = 64 })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(WithChange(new EntityChange { Id = 1, Mask = NetField.Appearance, Appearance = new TableRef(2, true) })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(WithChange(new EntityChange { Id = 1, Mask = NetField.LastMoveTick, LastMoveTick = 6 })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(WithChange(new EntityChange { Id = 1, Mask = NetField.LastMoveTick, LastMoveTick = -1 })));

        byte[] Entity(Action<Msg.RawWriter> tail) => Msg.Raw(MessageId.Snapshot, r =>
        {
            r.VarUInt(5).VarUInt(0).VarUInt(0).VarUInt(1).String("/mob").VarUInt(0).VarUInt(0).VarUInt(1).Byte(0);
            r.VarUInt(1).VarInt(1).VarUInt(0).VarUInt(1).VarUInt(1).VarUInt(1).Byte(2).String("").Single(0f);
            tail(r);
            r.Byte(0).Byte(0).VarUInt(0).Byte(0);
        });

        Assert.True(Codec.TryDecode(Entity(r => r.VarUInt(5).Byte(0).VarUInt(1)), out Snapshot _, out ProtocolError error), error.ToString());
        foreach (byte[] bad in new[]
                 {
                     Entity(r => r.VarUInt(6).Byte(0).VarUInt(0)), // moved 6 ticks ago at tick 5
                     Entity(r => r.VarUInt(0).Byte(4).VarUInt(0)), // an undefined record flag
                     Entity(r => r.VarUInt(0).Byte(2).Byte(0).VarUInt(0)), // a posture flagged and 0
                     // version 100 (DV690) defines bits 6 and 7, so 65 (lying, turned face down) is valid now; these two are not
                     Entity(r => r.VarUInt(0).Byte(2).Byte(129).VarUInt(0)), // face up without a fixed side
                     Entity(r => r.VarUInt(0).Byte(2).Byte(64).VarUInt(0)), // a turned side on a standing body
                     Entity(r => r.VarUInt(0).Byte(1).Bytes(0, 0, 0, 1).Byte(0).VarUInt(0)), // a position in tile 0
                     Entity(r => r.VarUInt(0).Byte(1).Bytes(0, 1, 0, 1).VarUInt(0)), // truncated
                     Entity(r => r.VarUInt(0).Byte(0).VarUInt(2)), // an appearance past the Snapshot's table
                     Entity(r => r.VarUInt(0).Byte(0).VarUInt(1UL << 40)),
                 })
        {
            Assert.False(Codec.TryDecode(bad, out Snapshot value, out error), "accepted " + BitConverter.ToString(bad));
            Assert.Null(value);
        }
    }

    // ---- the tables of a Delta ----

    [Fact]
    public void ADeltaCarriesNewTableEntriesOnlyWhenThereAreAnd()
    {
        Delta d = Msg.Delta(10);
        Assert.Equal(new byte[] { 5, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, Codec.Encode(d));
        d.FirstNewResource = 40;
        d.NewResources.Add("icons/x.dmi");
        d.FirstNewAppearance = 7;
        d.NewAppearances.Add(new AppearanceEntry { Icon = TableRef.Public(40) });
        d.NewAppearances.Add(new AppearanceEntry { Overlays = { TableRef.Public(7) } });
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d, decoded);
        Assert.Equal(0u, decoded.FirstNewString);

        // A table that would pass its cap, a first index of 0, an overlay naming a later entry.
        var over = Msg.Delta(10);
        over.FirstNewResource = (uint)Limits.MaxResourceTableEntries;
        over.NewResources.AddRange(new[] { "a", "b" });
        Assert.Throws<ProtocolException>(() => Codec.Encode(over));
        var zero = Msg.Delta(10);
        zero.NewStrings.Add("a");
        Assert.Throws<ProtocolException>(() => Codec.Encode(zero));
        var later = Msg.Delta(10);
        later.FirstNewAppearance = 1;
        later.NewAppearances.Add(new AppearanceEntry { Overlays = { TableRef.Public(2) } });
        later.NewAppearances.Add(new AppearanceEntry());
        Assert.Throws<ProtocolException>(() => Codec.Encode(later));
    }

    [Fact]
    public void ADeltaCarriesCellChangesAndRefusesBadOnes()
    {
        Delta d = Msg.Delta(10);
        d.SolidChanged.Add(new CellSolidChange(255, 255, 16, true));
        d.SolidChanged.Add(new CellSolidChange(1, 1, 1, false));
        d.TurfAppearanceChanged.Add(new CellAppearanceChange(3, 3, 1, TableRef.Public(12)));
        d.TurfAppearanceChanged.Add(new CellAppearanceChange(3, 4, 1, TableRef.None));
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d, decoded);

        var zero = Msg.Delta(10);
        zero.SolidChanged.Add(new CellSolidChange(0, 1, 1, true));
        Assert.Throws<ProtocolException>(() => Codec.Encode(zero));
        var high = Msg.Delta(10);
        high.SolidChanged.Add(new CellSolidChange(1, 1, Limits.MaxMapLevels + 1, true));
        Assert.Throws<ProtocolException>(() => Codec.Encode(high));
        var priv = Msg.Delta(10);
        priv.TurfAppearanceChanged.Add(new CellAppearanceChange(1, 1, 1, new TableRef(1, true)));
        Assert.Throws<ProtocolException>(() => Codec.Encode(priv));
        var many = Msg.Delta(10);
        for (int i = 0; i <= Limits.MaxCellChangesPerDelta; i++) many.SolidChanged.Add(new CellSolidChange(1 + i % 255, 1, 1, true));
        Assert.Throws<ProtocolException>(() => Codec.Encode(many));

        byte[] Cells(Action<Msg.RawWriter> tail, byte parts) => Msg.Raw(MessageId.Delta, r =>
        {
            r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(parts).VarUInt(0).VarUInt(0).VarUInt(0);
            tail(r);
            r.VarUInt(0); // version 44: no ceiling cells
            r.VarUInt(0); // version 47: no sun
            r.VarUInt(0); // version 96: no turf lights
            r.Byte(0).Byte(0).VarUInt(0).Byte(0);
        });

        Assert.True(Codec.TryDecode(Cells(r => r.VarUInt(1).VarUInt(2).VarUInt(2).VarUInt(1).Byte(1), 8), out Delta _, out error), error.ToString());
        foreach (byte[] craft in new[]
                 {
                     Cells(r => r.VarUInt(1).VarUInt(2).VarUInt(2).VarUInt(1).Byte(2), 8), // a solidity flag of 2
                     Cells(r => r.VarUInt(0), 8), // flagged and empty
                     Cells(r => r.VarUInt(0), 16),
                     Cells(r => { }, 32), // an undefined part
                     Cells(r => r.VarUInt(1).VarUInt(0).VarUInt(2).VarUInt(1).Byte(1), 8),
                     Cells(r => r.VarUInt(1).VarUInt(2).VarUInt(2).VarUInt(1).VarUInt(1UL << 40), 16),
                 })
        {
            Assert.False(Codec.TryDecode(craft, out Delta value, out error), "accepted " + BitConverter.ToString(craft));
            Assert.Null(value);
        }
    }
}
