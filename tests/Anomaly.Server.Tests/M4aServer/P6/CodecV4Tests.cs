using Anomaly.Server.Tests.Protocol;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.M4aServer.P6;

/// <summary>Protocol version 4's byte layouts (docs/ai/specs/protocol.md section 14): the Action's targets, icon pixel, keybinding and hover, ContextHints, the screen entries' group, the private images and the entity flags.</summary>
public class CodecV4Tests
{
    private static ActionMessage Click(TargetKind kind, long target = 0, int x = 0, int y = 0, int z = 0, byte iconX = 0, byte iconY = 0,
        ClickButton button = ClickButton.Left, ClickModifiers modifiers = ClickModifiers.None)
        => new(7, ActionKind.Click, target, button, modifiers, null) { TargetKind = kind, TurfX = x, TurfY = y, TurfZ = z, IconX = iconX, IconY = iconY };

    private static ActionMessage Hover(TargetKind kind, long target = 0, int x = 0, int y = 0, int z = 0)
        => new(7, ActionKind.Hover, target, ClickButton.None, ClickModifiers.None, null) { TargetKind = kind, TurfX = x, TurfY = y, TurfZ = z };

    private static ActionMessage Key(KeybindingCode code, bool up)
        => new(7, ActionKind.Keybinding, 0, ClickButton.None, ClickModifiers.None, null) { Keybinding = code, KeyUp = up };

    private static void RoundTrips(ActionMessage message, byte[] expectedBytes = null)
    {
        byte[] bytes = Codec.Encode(message);
        if (expectedBytes != null) Assert.Equal(expectedBytes, bytes);
        Assert.True(bytes.Length <= Limits.MaxActionMessageBytes && bytes.Length <= Limits.MaxInboundMessageBytes);
        Assert.True(Codec.TryDecode(bytes, out ActionMessage decoded, out ProtocolError error), error.ToString());
        Assert.Equal(message, decoded);
        Assert.Equal(bytes, Codec.Encode(decoded));
    }

    // ---- the Action ----

    [Fact]
    public void EveryTargetKindOfAClickRoundTripsWithItsLayout()
    {
        //                     id  frame kind Click kind  target  button mods ix iy
        RoundTrips(Click(TargetKind.Entity, 300, iconX: 16, iconY: 32, button: ClickButton.Right, modifiers: ClickModifiers.Ctrl),
            new byte[] { 10, 7, 4, 1, 0xAC, 0x02, 2, 2, 16, 32 });
        RoundTrips(Click(TargetKind.Screen, 9), new byte[] { 10, 7, 4, 3, 9, 1, 0, 0, 0 });
        //                     id  frame kind Click kind  x    y    z   button mods ix iy
        RoundTrips(Click(TargetKind.Turf, x: 255, y: 1, z: 16, iconX: 1, iconY: 1, modifiers: ClickModifiers.Shift | ClickModifiers.Alt),
            new byte[] { 10, 7, 4, 2, 255, 1, 16, 1, 5, 1, 1 });
        RoundTrips(Click(TargetKind.Entity, long.MaxValue, button: ClickButton.Middle, modifiers: ClickModifiers.All));
    }

    [Fact]
    public void HoverAndKeybindingRoundTripWithTheirLayouts()
    {
        RoundTrips(Hover(TargetKind.Entity, 44), new byte[] { 10, 7, 7, 1, 44 });
        RoundTrips(Hover(TargetKind.Screen, 44), new byte[] { 10, 7, 7, 3, 44 });
        RoundTrips(Hover(TargetKind.Turf, x: 12, y: 200, z: 3), new byte[] { 10, 7, 7, 2, 12, 200, 3 });
        RoundTrips(Key(KeybindingCode.SwapHands, false), new byte[] { 10, 7, 6, 1, 0 });
        RoundTrips(Key(KeybindingCode.HoldThrowMode, true), new byte[] { 10, 7, 6, 8, 1 });
        foreach (KeybindingCode code in Enum.GetValues<KeybindingCode>().Where(c => c != KeybindingCode.None && c != KeybindingCode.Last))
            foreach (bool up in new[] { false, true })
                RoundTrips(Key(code, up));
    }

    [Fact]
    public void AVersionThreeClickIsWrittenAsAnEntityClick()
    {
        var v3 = new ActionMessage(7, ActionKind.Click, 77, ClickButton.Left, ClickModifiers.None, null);
        Assert.Equal(TargetKind.None, v3.TargetKind);
        Assert.Equal(TargetKind.Entity, v3.EffectiveTargetKind);
        Assert.True(Codec.TryDecode(Codec.Encode(v3), out ActionMessage decoded, out _));
        Assert.Equal(v3 with { TargetKind = TargetKind.Entity }, decoded);
    }

    [Fact]
    public void TheLargestActionFitsItsCap()
    {
        foreach (ActionMessage message in new[]
                 {
                     Click(TargetKind.Entity, long.MaxValue, iconX: 32, iconY: 32, modifiers: ClickModifiers.All) with { FrameSeq = uint.MaxValue },
                     Click(TargetKind.Turf, x: 255, y: 255, z: 16, iconX: 32, iconY: 32) with { FrameSeq = uint.MaxValue },
                 })
            Assert.True(Codec.Encode(message).Length <= Limits.MaxActionMessageBytes - Limits.MaxDevCommandBytes - 2 + 0, "a click is far under the cap");
        string text = new('x', Limits.MaxDevCommandBytes);
        Assert.True(Codec.Encode(new ActionMessage(uint.MaxValue, ActionKind.DevCommand, 0, ClickButton.None, ClickModifiers.None, text)).Length <= Limits.MaxActionMessageBytes);
        Assert.True(Limits.MaxActionMessageBytes <= Limits.MaxInboundMessageBytes);
    }

    [Fact]
    public void TheEncoderRefusesWhatTheDocumentationForbids()
    {
        void Refused(ActionMessage message) => Assert.Throws<ProtocolException>(() => Codec.Encode(message));
        Refused(Click(TargetKind.Entity, 0));                                          // an id target needs an id
        Refused(Click(TargetKind.Entity, -1));
        Refused(Click(TargetKind.Entity, 5, x: 3));                                    // and no turf
        Refused(Click(TargetKind.Turf, 5, 3, 3, 1));                                   // a turf has no id
        Refused(Click(TargetKind.Turf, x: 0, y: 3, z: 1));
        Refused(Click(TargetKind.Turf, x: 3, y: 256, z: 1));
        Refused(Click(TargetKind.Turf, x: 3, y: 3, z: 0));
        Refused(Click(TargetKind.Turf, x: 3, y: 3, z: Limits.MaxMapLevels + 1));
        Refused(Click(TargetKind.None));                                               // no target at all
        Refused(Click(TargetKind.Entity, 5, iconX: 33));
        Refused(Click(TargetKind.Entity, 5, iconY: 33));
        Refused(Hover(TargetKind.None));
        Refused(Hover(TargetKind.Entity, 5) with { Button = ClickButton.Left });       // a hover has no button, modifiers or icon
        Refused(Hover(TargetKind.Entity, 5) with { Modifiers = ClickModifiers.Shift });
        Refused(Hover(TargetKind.Entity, 5) with { IconX = 1 });
        Refused(Key(KeybindingCode.None, false));
        Refused(Key((KeybindingCode)25, false)); // past the last (version 100 also, slice 4E: 24 is the suit storage quick equip; version 93: 19 the scoreboard)
        Refused(Key(KeybindingCode.DropItem, false) with { Target = 5 });
        Refused(Key(KeybindingCode.DropItem, false) with { TargetKind = TargetKind.Entity });
        Refused(new ActionMessage(1, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null) { Keybinding = KeybindingCode.DropItem });
        Refused(new ActionMessage(1, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null) { KeyUp = true });
        Refused(new ActionMessage(1, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null) { TurfX = 3 });
        Refused(new ActionMessage(1, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null) { IconX = 3 });
        Refused(new ActionMessage(1, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null) { TargetKind = TargetKind.Turf });
        Refused(new ActionMessage(1, (ActionKind)8, 0, ClickButton.None, ClickModifiers.None, null));
    }

    [Fact]
    public void TheDecoderRefusesCraftedBytes()
    {
        byte[] Craft(Action<Msg.RawWriter> body) => Msg.Raw(MessageId.Action, body);
        foreach (byte[] bad in new[]
                 {
                     Craft(r => r.VarUInt(1).Byte(4).Byte(0).VarUInt(5).Byte(1).Byte(0).Byte(0).Byte(0)),                  // target kind none
                     Craft(r => r.VarUInt(1).Byte(4).Byte(5).VarUInt(5).Byte(1).Byte(0).Byte(0).Byte(0)),                  // target kind 5
                     Craft(r => r.VarUInt(1).Byte(4).Byte(255).VarUInt(5).Byte(1).Byte(0).Byte(0).Byte(0)),
                     Craft(r => r.VarUInt(1).Byte(4).Byte(2).Byte(0).Byte(5).VarUInt(1).Byte(1).Byte(0).Byte(0).Byte(0)),  // turf x 0
                     Craft(r => r.VarUInt(1).Byte(4).Byte(2).Byte(5).Byte(0).VarUInt(1).Byte(1).Byte(0).Byte(0).Byte(0)),  // turf y 0
                     Craft(r => r.VarUInt(1).Byte(4).Byte(2).Byte(5).Byte(5).VarUInt(0).Byte(1).Byte(0).Byte(0).Byte(0)),  // turf z 0
                     Craft(r => r.VarUInt(1).Byte(4).Byte(2).Byte(5).Byte(5).VarUInt(Limits.MaxMapLevels + 1).Byte(1).Byte(0).Byte(0).Byte(0)), // turf z over the levels
                     Craft(r => r.VarUInt(1).Byte(4).Byte(1).VarUInt(5).Byte(1).Byte(0).Byte(33).Byte(1)),                 // icon-x 33
                     Craft(r => r.VarUInt(1).Byte(4).Byte(1).VarUInt(5).Byte(1).Byte(0).Byte(1).Byte(255)),                // icon-y 255
                     Craft(r => r.VarUInt(1).Byte(4).Byte(1).VarUInt(5).Byte(1).Byte(0).Byte(1)),                          // icon-y missing
                     // corrected by protocol 100's slice T4b: one byte after an entity hover's target is its zone now (1 the head), so a
                     // click's button and modifiers after it are what stays refused (the 0 trails the zone)
                     Craft(r => r.VarUInt(1).Byte(7).Byte(1).VarUInt(5).Byte(1).Byte(0)),                                  // a hover with a button and modifiers
                     Craft(r => r.VarUInt(1).Byte(7).Byte(2).Byte(5).Byte(5)),                                             // a turf hover cut short
                     Craft(r => r.VarUInt(1).Byte(6).Byte(0).Byte(0)),                                                     // keybinding code 0
                     Craft(r => r.VarUInt(1).Byte(6).Byte(25).Byte(0)),                                                    // past the last (24 since slice 4E; 19 since version 93)
                     Craft(r => r.VarUInt(1).Byte(6).Byte(255).Byte(0)),
                     Craft(r => r.VarUInt(1).Byte(6).Byte(5).Byte(2)),                                                     // key-up flag 2
                     Craft(r => r.VarUInt(1).Byte(6).Byte(5)),                                                             // cut short
                     Craft(r => r.VarUInt(1).Byte(6).Byte(5).Byte(0).Byte(0)),                                             // a trailing byte
                     Craft(r => r.VarUInt(1).Byte(8)),                                                                     // an action kind past the last
                 })
        {
            Assert.False(Codec.TryDecode(bad, out ActionMessage _, out ProtocolError error), "accepted " + BitConverter.ToString(bad));
            Assert.NotEqual(ProtocolError.None, error);
        }
    }

    // ---- ContextHints ----

    private static ContextHintsMessage Hints(TargetKind kind, string name, params ContextHint[] hints)
    {
        var m = new ContextHintsMessage { TargetKind = kind, Name = name };
        switch (kind)
        {
            case TargetKind.Turf:
                m.TurfX = 12;
                m.TurfY = 34;
                m.TurfZ = 2;
                break;
            default:
                m.Target = 5000;
                break;
        }

        m.Hints.AddRange(hints);
        return m;
    }

    private static void AssertEqual(ContextHintsMessage e, ContextHintsMessage a)
    {
        Assert.Equal((e.TargetKind, e.Target, e.TurfX, e.TurfY, e.TurfZ, e.Name), (a.TargetKind, a.Target, a.TurfX, a.TurfY, a.TurfZ, a.Name));
        Assert.Equal(e.Hints, a.Hints);
    }

    [Fact]
    public void ContextHintsRoundTripWithTheirLayout()
    {
        ContextHintsMessage m = Hints(TargetKind.Entity, "airlock",
            new ContextHint(ClickButton.Left, ClickModifiers.None, "Open"), new ContextHint(ClickButton.Right, ClickModifiers.Alt, "Weld shut"));
        byte[] bytes = Codec.Encode(m);
        // id 11, entity, id 5000 (varuint 0xA8 0x27), name "airlock" (length + 1 = 8), 2 hints
        Assert.Equal(new byte[] { 11, 1, 0x88, 0x27, 8 }.Concat(System.Text.Encoding.UTF8.GetBytes("airlock"))
            .Concat(new byte[] { 2, 1, 0, 0, 5 }).Concat(System.Text.Encoding.UTF8.GetBytes("Open")) // version 36: the action byte
            .Concat(new byte[] { 2, 4, 0, 10 }).Concat(System.Text.Encoding.UTF8.GetBytes("Weld shut")).ToArray(), bytes);
        Assert.True(Codec.TryDecode(bytes, out ContextHintsMessage decoded, out ProtocolError error), error.ToString());
        AssertEqual(m, decoded);
        Assert.Equal(bytes, Codec.Encode(decoded));

        foreach (ContextHintsMessage other in new[]
                 {
                     Hints(TargetKind.Turf, null),
                     Hints(TargetKind.Screen, ""),
                     Hints(TargetKind.Turf, "Floor", new ContextHint(ClickButton.Middle, ClickModifiers.All, "")),
                 })
        {
            Assert.True(Codec.TryDecode(Codec.Encode(other), out decoded, out error), error.ToString());
            AssertEqual(other, decoded);
        }
    }

    [Fact]
    public void ContextHintsTailRoundTripsAndIsAbsentWhenNothingIsSet()
    {
        // version 100 also (protocol.md section 100, the hints, improved): [u8 flags] then the labels whose bit is set, only when a field is set
        ContextHintsMessage m = Hints(TargetKind.Entity, "airlock", new ContextHint(ClickButton.Left, ClickModifiers.None, "Pry open"));
        byte[] plain = Codec.Encode(m);
        m.EmptyHand = "Open";
        m.HeldAlt = "Toggle";
        m.HeldEquips = true;
        byte[] bytes = Codec.Encode(m);
        Assert.Equal(plain.Concat(new byte[] { 1 | 4 | 8, 5 }).Concat(System.Text.Encoding.UTF8.GetBytes("Open"))
            .Concat(new byte[] { 7 }).Concat(System.Text.Encoding.UTF8.GetBytes("Toggle")).ToArray(), bytes);
        Assert.True(Codec.TryDecode(bytes, out ContextHintsMessage decoded, out ProtocolError error), error.ToString());
        AssertEqual(m, decoded);
        Assert.Equal(("Open", (string)null, "Toggle", true, false), (decoded.EmptyHand, decoded.HeldUse, decoded.HeldAlt, decoded.HeldEquips, decoded.MoveHere));
        Assert.Equal(bytes, Codec.Encode(decoded));

        var turf = Hints(TargetKind.Turf, null);
        turf.MoveHere = true;
        turf.HeldUse = "Use";
        Assert.True(Codec.TryDecode(Codec.Encode(turf), out decoded, out error), error.ToString());
        Assert.Equal(((string)null, "Use", (string)null, false, true), (decoded.EmptyHand, decoded.HeldUse, decoded.HeldAlt, decoded.HeldEquips, decoded.MoveHere));

        // without a set field there is no tail: the version-36 bytes
        Assert.True(Codec.TryDecode(plain, out decoded, out error), error.ToString());
        Assert.Equal(((string)null, (string)null, (string)null, false, false), (decoded.EmptyHand, decoded.HeldUse, decoded.HeldAlt, decoded.HeldEquips, decoded.MoveHere));

        // refused: an empty or too long label, unknown or no flag bits, a label bit without its label
        var empty = Hints(TargetKind.Entity, "x");
        empty.HeldUse = "";
        Assert.Throws<ProtocolException>(() => Codec.Encode(empty));
        var tooLong = Hints(TargetKind.Entity, "x");
        tooLong.EmptyHand = new string('l', Limits.MaxContextLabelBytes + 1);
        Assert.Throws<ProtocolException>(() => Codec.Encode(tooLong));
        foreach (byte[] bad in new[]
                 {
                     Msg.Raw(MessageId.ContextHints, r => r.Byte(1).VarUInt(5).String("x").VarUInt(0).Byte(32)),               // unknown bit
                     Msg.Raw(MessageId.ContextHints, r => r.Byte(1).VarUInt(5).String("x").VarUInt(0).Byte(0)),                // no bits
                     Msg.Raw(MessageId.ContextHints, r => r.Byte(1).VarUInt(5).String("x").VarUInt(0).Byte(2)),                // a label missing
                     Msg.Raw(MessageId.ContextHints, r => r.Byte(1).VarUInt(5).String("x").VarUInt(0).Byte(2).String("")),     // an empty label
                     Msg.Raw(MessageId.ContextHints, r => r.Byte(1).VarUInt(5).String("x").VarUInt(0).Byte(8).Byte(0)),        // a byte after the tail
                 })
        {
            Assert.False(Codec.TryDecode(bad, out ContextHintsMessage _, out ProtocolError refused), "accepted " + BitConverter.ToString(bad));
            Assert.NotEqual(ProtocolError.None, refused);
        }
    }

    [Fact]
    public void TheLargestContextHintsMessageFitsItsCap()
    {
        var m = Hints(TargetKind.Turf, new string('n', Limits.MaxContextNameBytes));
        m.TurfX = 255;
        m.TurfY = 255;
        m.TurfZ = 16;
        for (int i = 0; i < Limits.MaxContextHints; i++)
            m.Hints.Add(new ContextHint(ClickButton.Right, ClickModifiers.All, new string('l', Limits.MaxContextLabelBytes)));
        (m.EmptyHand, m.HeldUse, m.HeldAlt, m.HeldEquips, m.MoveHere) = (new string('e', Limits.MaxContextLabelBytes), new string('u', Limits.MaxContextLabelBytes),
            new string('a', Limits.MaxContextLabelBytes), true, true); // version 100 also: the whole tail
        byte[] bytes = Codec.Encode(m);
        Assert.True(bytes.Length <= Limits.MaxContextHintsMessageBytes, bytes.Length + " bytes");
        Assert.True(Limits.MaxContextHintsMessageBytes <= Limits.MaxOutboundMessageBytes);
        Assert.True(Codec.TryDecode(bytes, out ContextHintsMessage decoded, out _));
        AssertEqual(m, decoded);
        // And the entity form with the largest id.
        var entity = Hints(TargetKind.Entity, new string('n', Limits.MaxContextNameBytes));
        entity.Target = long.MaxValue;
        entity.Hints.AddRange(m.Hints);
        Assert.True(Codec.Encode(entity).Length <= Limits.MaxContextHintsMessageBytes);
    }

    [Fact]
    public void ContextHintsRefusesWhatTheDocumentationForbids()
    {
        void Refused(ContextHintsMessage m) => Assert.Throws<ProtocolException>(() => Codec.Encode(m));
        Refused(Hints(TargetKind.None, "x"));
        var noId = Hints(TargetKind.Entity, "x");
        noId.Target = 0;
        Refused(noId);
        var turfAndId = Hints(TargetKind.Turf, "x");
        turfAndId.Target = 4;
        Refused(turfAndId);
        var outside = Hints(TargetKind.Turf, "x");
        outside.TurfX = 0;
        Refused(outside);
        Refused(Hints(TargetKind.Entity, new string('n', Limits.MaxContextNameBytes + 1)));
        Refused(Hints(TargetKind.Entity, "x", new ContextHint(ClickButton.None, ClickModifiers.None, "a")));
        Refused(Hints(TargetKind.Entity, "x", new ContextHint(ClickButton.Left, (ClickModifiers)8, "a")));
        Refused(Hints(TargetKind.Entity, "x", new ContextHint(ClickButton.Left, ClickModifiers.None, null)));
        Refused(Hints(TargetKind.Entity, "x", new ContextHint(ClickButton.Left, ClickModifiers.None, new string('l', Limits.MaxContextLabelBytes + 1))));
        var many = Hints(TargetKind.Entity, "x");
        for (int i = 0; i <= Limits.MaxContextHints; i++) many.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.None, "a"));
        Refused(many);

        byte[] Craft(Action<Msg.RawWriter> body) => Msg.Raw(MessageId.ContextHints, body);
        foreach (byte[] bad in new[]
                 {
                     Craft(r => r.Byte(0).VarUInt(5).String("x").VarUInt(0)),                                    // target kind none
                     Craft(r => r.Byte(1).VarUInt(0).String("x").VarUInt(0)),                                    // id 0
                     Craft(r => r.Byte(2).Byte(0).Byte(1).VarUInt(1).String("x").VarUInt(0)),                    // turf x 0
                     Craft(r => r.Byte(1).VarUInt(5).String("x").VarUInt(11)),                                   // 11 hints
                     Craft(r => r.Byte(1).VarUInt(5).String("x").VarUInt(1).Byte(0).Byte(0).String("a")),        // button 0
                     Craft(r => r.Byte(1).VarUInt(5).String("x").VarUInt(1).Byte(4).Byte(0).String("a")),        // button 4
                     Craft(r => r.Byte(1).VarUInt(5).String("x").VarUInt(1).Byte(1).Byte(8).String("a")),        // modifiers 8
                     Craft(r => r.Byte(1).VarUInt(5).String("x").VarUInt(1).Byte(1).Byte(0).Byte(0)),            // a null label
                     Craft(r => r.Byte(1).VarUInt(5).String(new string('n', Limits.MaxContextNameBytes + 1)).VarUInt(0)),
                     Craft(r => r.Byte(1).VarUInt(5).String("x").VarUInt(1).Byte(1).Byte(0).String(new string('l', Limits.MaxContextLabelBytes + 1))),
                     Craft(r => r.Byte(1).VarUInt(5).String("x").VarUInt(0).Byte(0)),                            // a trailing byte
                     Craft(r => r.Byte(1).VarUInt(5)),                                                            // cut short
                 })
        {
            Assert.False(Codec.TryDecode(bad, out ContextHintsMessage _, out ProtocolError error), "accepted " + BitConverter.ToString(bad));
            Assert.NotEqual(ProtocolError.None, error);
        }
    }

    // ---- the private block: the screen group and the images ----

    private static Snapshot SnapshotWith(OwnBlock own)
    {
        Snapshot s = Msg.Snapshot(5, 0, 0, new string[0]);
        s.Appearances.Add(new AppearanceEntry { Icon = TableRef.Public(1) });
        s.Resources.Add("icons/a.dmi");
        s.Own = own;
        return s;
    }

    [Fact]
    public void ScreenEntriesCarryTheirHudGroup()
    {
        var own = new OwnBlock();
        for (byte g = 0; g <= HudGroup.Last; g++)
            own.ScreenAdded.Add(new ScreenEntry(g + 1, TableRef.Public(1), "WEST," + g) { Group = g });
        own.ScreenChanged.Add(new ScreenEntry(40, TableRef.Public(1), null) { Group = HudGroup.Storage });
        Snapshot s = SnapshotWith(own);
        s.Own.ScreenChanged.Clear(); // a Snapshot lists every entry as added; the codec itself does not mind
        Assert.True(Codec.TryDecode(Codec.Encode(s), out Snapshot decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(s, decoded);
        Assert.Equal(Enumerable.Range(0, HudGroup.Last + 1).Select(g => (byte)g), decoded.Own.ScreenAdded.Select(e => e.Group));

        var changed = new OwnBlock();
        changed.ScreenChanged.Add(new ScreenEntry(40, TableRef.Public(1), null) { Group = HudGroup.Storage });
        byte[] block = new byte[64];
        int n = Codec.EncodeOwnBlock(changed, block);
        // flags (screen), depth, version 43's actions done and flags, 0 added, 1 changed: id 40 (zigzag 80), appearance (1 << 1), a null
        // screenLoc, group 6, no flick or animation (version 11), 0 removed
        Assert.Equal(new byte[] { 4, 0, 0, 0, 0, 1, 80, 2, 0, 6, 0, 0 }, block.AsSpan(0, n).ToArray());
    }

    [Fact]
    public void AHudGroupPastTheLastIsRefused()
    {
        var own = new OwnBlock();
        own.ScreenAdded.Add(new ScreenEntry(1, TableRef.Public(1), null) { Group = HudGroup.Last + 1 });
        Assert.Throws<ProtocolException>(() => Codec.EncodeOwnBlock(own, new byte[64]));
        // Crafted: the same entry with group 7: flags (screen), depth, version 43's actions done and flags (protocol 51 gave bit 1 a
        // meaning, so the old bytes, which put a 2 there, no longer reached the group), 1 added (id 1, appearance 1, no screenLoc, group 7,
        // no look), 0 changed, 0 removed.
        byte[] bytes = { 4, 0, 0, 0, 1, 2, 2, 0, 7, 0, 0, 0 };
        var snapshot = new Delta { Tick = 1 };
        byte[] delta = Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Bytes(bytes));
        Assert.False(Codec.TryDecode(delta, out Delta _, out ProtocolError error));
        Assert.Equal(ProtocolError.BadValue, error);
        GC.KeepAlive(snapshot);
    }

    [Fact]
    public void PrivateImagesRoundTripWithTheirLayout()
    {
        var own = new OwnBlock();
        own.ImagesAdded.Add(new ImageEntry(1, new TableRef(1, true), 4000) { Holder = 4000, HolderName = "tank" });
        own.ImagesAdded.Add(new ImageEntry(3, TableRef.Public(1), 0));
        own.ImagesChanged.Add(new ImageEntry(2, new TableRef(2, true), long.MaxValue));
        own.ImagesRemoved.Add(9);
        own.ImagesRemoved.Add(10);
        var buffer = new byte[256];
        int n = Codec.EncodeOwnBlock(own, buffer);
        // flags (images, bit 3), depth 0, version 43's actions done and flags (0, 0), 2 added: id 1 (zigzag 2), private ref 1 (3), attached 4000, version 27's holder 4000 and its
        // name "tank" (length + 1 = 5); id +2 (4), public ref 1 (2), 0, holder 0, no name (0); 1 changed: id 2 (4), private ref 2 (5),
        // long.MaxValue, holder 0, no name; 2 removed: 9 (18) and +1 (2). Version 30 ends each image with its turf's x, y and z (0, 0, 0 for none); version 35 adds the override flag (0).
        byte[] expectedStart = { 8, 0, 0, 0, 2, 2, 3, 0xA0, 0x1F, 0xA0, 0x1F, 5, (byte)'t', (byte)'a', (byte)'n', (byte)'k', 0, 0, 0, 0, 4, 2, 0, 0, 0, 0, 0, 0, 0,
            1, 4, 5, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0, 0, 0, 0, 0, 0, 2, 18, 2 };
        Assert.Equal(expectedStart, buffer.AsSpan(0, n).ToArray());

        Snapshot s = SnapshotWith(null);
        s.Own = new OwnBlock();
        s.Own.NewPrivateAppearances.Add(new AppearanceEntry { Icon = TableRef.Public(1) });
        s.Own.NewPrivateAppearances.Add(new AppearanceEntry { Icon = TableRef.Public(1), Alpha = 128 });
        s.Own.FirstNewPrivateAppearance = 1;
        s.Own.ImagesAdded.AddRange(own.ImagesAdded);
        Assert.True(Codec.TryDecode(Codec.Encode(s), out Snapshot decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(s, decoded);
        Assert.Equal(s.Own.ImagesAdded, decoded.Own.ImagesAdded);

        Delta d = Msg.Delta(8);
        d.Own = own;
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta decodedDelta, out error), error.ToString());
        Msg.AssertEqual(d, decodedDelta);
        Assert.Equal(own.ImagesChanged, decodedDelta.Own.ImagesChanged);
        Assert.Equal(own.ImagesRemoved, decodedDelta.Own.ImagesRemoved);
    }

    [Fact]
    public void PrivateImagesAreRefusedWhenTheyBreakTheRules()
    {
        void Refused(Action<OwnBlock> fill)
        {
            var own = new OwnBlock();
            fill(own);
            Assert.Throws<ProtocolException>(() => Codec.EncodeOwnBlock(own, new byte[8192]));
        }

        Refused(o => o.ImagesAdded.Add(new ImageEntry(0, TableRef.Public(1), 0)));                                  // id 0
        Refused(o => { o.ImagesAdded.Add(new ImageEntry(5, TableRef.Public(1), 0)); o.ImagesAdded.Add(new ImageEntry(5, TableRef.Public(1), 0)); }); // a repeated id
        Refused(o => { o.ImagesAdded.Add(new ImageEntry(5, TableRef.Public(1), 0)); o.ImagesAdded.Add(new ImageEntry(4, TableRef.Public(1), 0)); }); // not increasing
        Refused(o => o.ImagesAdded.Add(new ImageEntry(1, TableRef.None, 0)));                                       // no appearance
        Refused(o => o.ImagesAdded.Add(new ImageEntry(1, new TableRef(0, true), 0)));
        Refused(o => o.ImagesAdded.Add(new ImageEntry(1, TableRef.Public((uint)Limits.MaxAppearanceTableEntries + 1), 0)));
        Refused(o => o.ImagesAdded.Add(new ImageEntry(1, TableRef.Public(1), -1)));                                 // attached to a negative id
        Refused(o => { for (int i = 1; i <= Limits.MaxPrivateImages + 1; i++) o.ImagesChanged.Add(new ImageEntry(i, TableRef.Public(1), 0)); });
        Refused(o => { for (int i = 1; i <= Limits.MaxPrivateImages + 1; i++) o.ImagesRemoved.Add(i); });

        // Crafted: flagged and empty, an id of 0 (zigzag 0), one more than the cap of removed ids.
        foreach (byte[] bad in new[]
                 {
                     new byte[] { 8, 0, 0, 0, 0 },
                     new byte[] { 8, 0, 1, 0, 2, 0, 0, 0 },
                     new byte[] { 8, 0, 1, 2, 0, 0, 0, 0, 0 },
                 })
        {
            byte[] delta = Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Bytes(bad));
            Assert.False(Codec.TryDecode(delta, out Delta _, out ProtocolError error), "accepted " + BitConverter.ToString(bad));
            Assert.NotEqual(ProtocolError.None, error);
        }

        var many = new List<byte> { 8, 0, 0, 0, 0x81, 0x04 }; // 513 removed ids: a count of one over the cap
        for (int i = 0; i < Limits.MaxPrivateImages + 1; i++) many.Add(2);
        byte[] tooMany = Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Bytes(many.ToArray()));
        Assert.False(Codec.TryDecode(tooMany, out Delta _, out ProtocolError e2));
        Assert.Equal(ProtocolError.CountTooLarge, e2);
    }

    // ---- the entity flags ----

    [Fact]
    public void TheEntityFlagsRideTheRecordAndTheChange()
    {
        EntityState thrown = Msg.State(5, flagsOf: EntityFlags.Thrown);
        EntityState plain = Msg.State(6);
        Snapshot s = Msg.Snapshot(5, 0, 0, new[] { "/obj" }, thrown, plain);
        byte[] bytes = Codec.Encode(s);
        Assert.True(Codec.TryDecode(bytes, out Snapshot decoded, out ProtocolError error), error.ToString());
        Msg.AssertEqual(s, decoded);
        Assert.Equal(EntityFlags.Thrown, decoded.Entities[0].Flags);
        Assert.Equal(EntityFlags.None, decoded.Entities[1].Flags);
        // A record without flags is one byte shorter: the flags byte is present exactly when it is not 0.
        Snapshot without = Msg.Snapshot(5, 0, 0, new[] { "/obj" }, plain with { Id = 5 }, plain);
        Assert.Equal(1, bytes.Length - Codec.Encode(without).Length);

        Delta d = Msg.Delta(9, changed: new[]
        {
            new EntityChange { Id = 5, Mask = NetField.Flags, Flags = EntityFlags.Thrown },
            new EntityChange { Id = 6, Mask = NetField.Flags | NetField.Dir, Dir = 2, Flags = EntityFlags.None },
        });
        byte[] deltaBytes = Codec.Encode(d);
        Assert.True(Codec.TryDecode(deltaBytes, out Delta decodedDelta, out error), error.ToString());
        Msg.AssertEqual(d, decodedDelta);
        Assert.Equal(new[] { EntityFlags.Thrown, EntityFlags.None }, decodedDelta.Changed.Select(c => c.Flags));
    }

    [Fact]
    public void TheFlagsFieldIsTheThirteenthBitOfTheChangeMask()
    {
        // A change of only the flags: the wire mask has bit 12 (Pos, Yaw, X, Y, Z, Dir, LastMoveTick, Appearance, Posture, Name, GlideSize, Light, Flags).
        byte[] bytes = Codec.Encode(Msg.Delta(1, changed: new[] { new EntityChange { Id = 4, Mask = NetField.Flags, Flags = EntityFlags.Thrown } }));
        // id 5, tick 1, seq 0, own 0, first type 0, 0 types, parts 0, 0 spawned, 1 changed: id 4 (zigzag 8), mask 4096 (0x80 0x20), flags 1; 0 despawned; the empty private block
        Assert.Equal(new byte[] { 5, 1, 0, 0, 0, 0, 0, 0, 1, 8, 0x80, 0x20, 1, 0, 0, 0, 0, 0, 0, 0, 0 }, bytes);
    }

    [Fact]
    public void ThePushableBitIsDecoded()
    {
        // version 56: bit 3 (Pushable) was the byte's one undefined bit, refused before
        byte[] craftedChange = Msg.Raw(MessageId.Delta, r => r.VarUInt(1).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).VarUInt(0).VarUInt(1).VarInt(4).VarUInt(4096).Byte(8).VarUInt(0).VarUInt(0).VarUInt(0).VarUInt(0).Byte(0).Byte(0).VarUInt(0).Byte(0));
        Assert.True(Codec.TryDecode(craftedChange, out Delta delta, out ProtocolError error), error.ToString());
        Assert.Equal(EntityFlags.Pushable, delta.Changed[0].Flags);
        Assert.Equal(craftedChange, Codec.Encode(delta));
    }
}
