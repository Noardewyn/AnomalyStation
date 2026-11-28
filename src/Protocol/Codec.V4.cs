namespace Anomaly.Protocol;

// Owner: M4a P6 (the contract, MessagesV4.cs, the version-4 members of MessagesV2.cs and Messages.cs, Limits.cs, is the M4a
// contracts step's). The version-4 layout behind the existing entry points (docs/ai/specs/protocol.md section 14): the
// ActionMessage's target kind, turf coordinates, icon-x and icon-y, keybinding code and key-up flag (Codec.V2.cs, with the
// target helpers below); the screen entry's group byte (Codec.V2.cs) and the private images in the private block (below);
// the entity flags byte (Codec.cs, NetField.Flags); and the ContextHints message (below). Every rule of the earlier codecs
// holds: TryDecode never throws, every count and size is capped by Limits before anything is allocated, and a message that
// decodes encodes back to the same bytes (a field is present exactly when it differs from its default).

public static partial class Codec
{
    // ---- targets (Click, Hover, ContextHints) ----

    // [u8 target kind: 1 entity, 2 turf, 3 screen, 4 ceiling]
    //   Entity, Screen: [varuint id >= 1]
    //   Turf, Ceiling:  [u8 x 1..255] [u8 y 1..255] [varuint z 1..16]

    private static void WriteTarget(ref WireWriter w, TargetKind kind, long target, int x, int y, int z, string what)
    {
        switch (kind)
        {
            case TargetKind.Entity:
            case TargetKind.Screen:
                if (target < 1)
                    throw Bad(what + " names a target that is not a positive id");
                if (x != 0 || y != 0 || z != 0)
                    throw Bad(what + " names an entity or screen target and turf coordinates");
                w.WriteByte((byte)kind);
                w.WriteVarUInt((ulong)target);
                break;
            case TargetKind.Ceiling:
            case TargetKind.Turf:
                if (target != 0)
                    throw Bad(what + " names a turf target and an id");
                if (x < 1 || x > Limits.MaxMapSize || y < 1 || y > Limits.MaxMapSize || z < 1 || z > Limits.MaxMapLevels)
                    throw Bad(what + " names a turf outside the map's range");
                w.WriteByte((byte)kind);
                w.WriteByte((byte)x);
                w.WriteByte((byte)y);
                w.WriteVarUInt((ulong)z);
                break;
            default:
                throw Bad(what + " has no target of a known kind");
        }
    }

    private static void ReadTarget(ref WireReader r, out TargetKind kind, out long target, out int x, out int y, out int z, string what)
    {
        byte kindByte = r.ReadByte();
        target = 0;
        x = y = z = 0;
        switch ((TargetKind)kindByte)
        {
            case TargetKind.Entity:
            case TargetKind.Screen:
                kind = (TargetKind)kindByte;
                target = ReadNonNegativeLong(ref r, what + "'s target");
                if (target < 1)
                    throw Bad(what + " names a target that is not a positive id");
                break;
            case TargetKind.Ceiling:
            case TargetKind.Turf:
                kind = (TargetKind)kindByte;
                x = r.ReadByte();
                y = r.ReadByte();
                z = ReadRanged(ref r, 1, Limits.MaxMapLevels, what + "'s turf z");
                if (x < 1 || x > Limits.MaxMapSize || y < 1 || y > Limits.MaxMapSize)
                    throw Bad(what + " names a turf outside the map's range");
                break;
            default:
                throw Bad(what + " has an unknown target kind " + kindByte);
        }
    }

    // ---- ContextHints (server to client, Control channel) ----

    // [u8 id = 11] [target: as above] [str(256) name, null for no screentip]
    // [count <= 10] { [u8 button 1..3] [u8 modifiers 0..7] [u8 action 0..2, version 36] [str(64) label] }*

    private const int MinContextHintBytes = 1 + 1 + 1 + 1;

    /// <summary>Encodes a <see cref="ContextHintsMessage"/> for the Control channel (at most <see cref="Limits.MaxContextHintsMessageBytes"/>).</summary>
    public static int Encode(ContextHintsMessage message, Span<byte> destination)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.ContextHints);
        WriteTarget(ref w, message.TargetKind, message.Target, message.TurfX, message.TurfY, message.TurfZ, "a context hints message");
        w.WriteString(message.Name, Limits.MaxContextNameBytes);
        w.WriteCount(message.Hints.Count, Limits.MaxContextHints);
        foreach (ContextHint hint in message.Hints)
        {
            if (hint.Button < ClickButton.Left || hint.Button > ClickButton.Middle)
                throw Bad("a context hint with an unknown button");
            if ((hint.Modifiers & ~ClickModifiers.All) != 0)
                throw Bad("a context hint with unknown modifier bits");
            if (hint.Label == null)
                throw Bad("a context hint without a label");
            if (hint.Action > ContextHintAction.Last)
                throw Bad("a context hint with an unknown action");
            w.WriteByte((byte)hint.Button);
            w.WriteByte((byte)hint.Modifiers);
            w.WriteByte((byte)hint.Action);
            w.WriteString(hint.Label, Limits.MaxContextLabelBytes);
        }

        // version 100 also: the tail, present only when one of its fields is set
        byte tail = ContextTailFlags(message);
        if (tail != 0)
        {
            w.WriteByte(tail);
            WriteContextTailLabel(ref w, message.EmptyHand);
            WriteContextTailLabel(ref w, message.HeldUse);
            WriteContextTailLabel(ref w, message.HeldAlt);
        }

        return Finish(ref w, Limits.MaxContextHintsMessageBytes);
    }

    /// <summary>The encoded <see cref="ContextHintsMessage"/>.</summary>
    public static byte[] Encode(ContextHintsMessage message)
    {
        Span<byte> buffer = stackalloc byte[Limits.MaxContextHintsMessageBytes + 8];
        return buffer.Slice(0, Encode(message, buffer)).ToArray();
    }

    /// <summary>Decodes a <see cref="ContextHintsMessage"/>: a known target kind, at most <see cref="Limits.MaxContextHints"/> hints with a known button and modifier bits, the name and labels within their caps.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out ContextHintsMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.ContextHints, Limits.MaxContextHintsMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            ReadTarget(ref r, out TargetKind kind, out long target, out int x, out int y, out int z, "a context hints message");
            var result = new ContextHintsMessage { TargetKind = kind, Target = target, TurfX = x, TurfY = y, TurfZ = z };
            result.Name = r.ReadString(Limits.MaxContextNameBytes);
            int count = r.ReadCount(Limits.MaxContextHints, MinContextHintBytes);
            for (int i = 0; i < count; i++)
            {
                byte button = r.ReadByte();
                if (button < (byte)ClickButton.Left || button > (byte)ClickButton.Middle)
                    throw Bad("a context hint with an unknown button");
                byte mods = r.ReadByte();
                if ((mods & ~(byte)ClickModifiers.All) != 0)
                    throw Bad("a context hint with unknown modifier bits");
                byte action = r.ReadByte();
                if (action > (byte)ContextHintAction.Last)
                    throw Bad("a context hint with an unknown action");
                string label = r.ReadString(Limits.MaxContextLabelBytes);
                if (label == null)
                    throw Bad("a context hint without a label");
                result.Hints.Add(new ContextHint((ClickButton)button, (ClickModifiers)mods, label, (ContextHintAction)action));
            }

            if (!r.End)
            {
                byte tail = r.ReadByte();
                if (tail == 0) // no tail is written without a field: a zero byte is one past the end
                    throw new ProtocolException(ProtocolError.TrailingBytes, "a byte follows the message");
                if ((tail & ~ContextTailAll) != 0)
                    throw Bad("a context hints tail with unknown flag bits");
                result.EmptyHand = ReadContextTailLabel(ref r, tail, ContextTailEmptyHand);
                result.HeldUse = ReadContextTailLabel(ref r, tail, ContextTailHeldUse);
                result.HeldAlt = ReadContextTailLabel(ref r, tail, ContextTailHeldAlt);
                result.HeldEquips = (tail & ContextTailEquips) != 0;
                result.MoveHere = (tail & ContextTailMoveHere) != 0;
            }

            r.ExpectEnd();
            value = result;
            return true;
        }
        catch (ProtocolException e)
        {
            value = null;
            error = e.Error;
            return false;
        }
    }

    // Version 100 also (protocol.md section 100, "the hints, improved"): after the hints, only when one is set,
    // [u8 flags: bit 0 empty-hand label, 1 held item's use label, 2 its alt label, 3 quick equip goes somewhere, 4 move here; bits 5-7 zero, never 0]
    // then each label whose bit is set, in that order: [str(1..64)].
    private const byte ContextTailEmptyHand = 1, ContextTailHeldUse = 2, ContextTailHeldAlt = 4, ContextTailEquips = 8, ContextTailMoveHere = 16;
    private const byte ContextTailAll = ContextTailEmptyHand | ContextTailHeldUse | ContextTailHeldAlt | ContextTailEquips | ContextTailMoveHere;

    private static byte ContextTailFlags(ContextHintsMessage m) => (byte)((m.EmptyHand != null ? ContextTailEmptyHand : 0) | (m.HeldUse != null ? ContextTailHeldUse : 0)
        | (m.HeldAlt != null ? ContextTailHeldAlt : 0) | (m.HeldEquips ? ContextTailEquips : 0) | (m.MoveHere ? ContextTailMoveHere : 0));

    private static void WriteContextTailLabel(ref WireWriter w, string label)
    {
        if (label == null)
            return;
        if (label.Length == 0)
            throw Bad("an empty context hints tail label");
        w.WriteString(label, Limits.MaxContextLabelBytes);
    }

    private static string ReadContextTailLabel(ref WireReader r, byte tail, byte bit)
    {
        if ((tail & bit) == 0)
            return null;
        string label = r.ReadString(Limits.MaxContextLabelBytes);
        if (string.IsNullOrEmpty(label))
            throw Bad("an empty context hints tail label");
        return label;
    }

    // ---- private images (the private block) ----

    private const int MinImageBytes = 9; // version 27: and the holder and its name; version 30: and the turf's three coordinates; version 35: and the override flag

    private static long MaxImagesSize(OwnBlock own)
        => 3L * 5 + MaxImageListBytes(own.ImagesAdded) + MaxImageListBytes(own.ImagesChanged) + own.ImagesRemoved.Count * 10L;

    private static long MaxImageListBytes(List<ImageEntry> list)
    {
        long size = 0;
        foreach (ImageEntry e in list)
            size += 10 + 5 + 10 + 10 + 5 + MaxUtf8(e.HolderName, Limits.MaxImageHolderNameBytes) + 3 * 5;
        return size;
    }

    private static void WriteImages(ref WireWriter w, OwnBlock own)
    {
        if ((long)own.ImagesAdded.Count + own.ImagesChanged.Count > Limits.MaxPrivateImages * 2L)
            throw new ProtocolException(ProtocolError.CountTooLarge, "a private block of more than " + Limits.MaxPrivateImages + " images");
        WriteImageList(ref w, own.ImagesAdded);
        WriteImageList(ref w, own.ImagesChanged);
        w.WriteCount(own.ImagesRemoved.Count, Limits.MaxPrivateImages);
        long previous = 0;
        foreach (long id in own.ImagesRemoved)
        {
            WriteId(ref w, id, previous, true);
            previous = id;
        }
    }

    private static void WriteImageList(ref WireWriter w, List<ImageEntry> list)
    {
        w.WriteCount(list.Count, Limits.MaxPrivateImages);
        long previous = 0;
        foreach (ImageEntry e in list)
        {
            WriteId(ref w, e.Id, previous, true);
            previous = e.Id;
            // version 35: an override image may draw nothing (index 0): it hides the entity it hangs on
            if ((e.Appearance.Index == 0 && !e.Override) || e.Appearance.Index > (uint)Limits.MaxAppearanceTableEntries)
                throw Bad("a private image's appearance is outside its table");
            w.WriteVarUInt(((ulong)e.Appearance.Index << 1) | (e.Appearance.Private ? 1UL : 0UL));
            if (e.AttachedTo < 0)
                throw Bad("a private image attached to a negative id");
            w.WriteVarUInt((ulong)e.AttachedTo);
            // version 27 (section 36): [varuint holder] [string holder name]
            if (e.Holder < 0)
                throw Bad("a private image held by a negative id");
            w.WriteVarUInt((ulong)e.Holder);
            w.WriteString(e.HolderName, Limits.MaxImageHolderNameBytes);
            // version 30 (section 39): [varuint turf x] [varuint turf y] [varuint turf z], all 0 for none
            if (e.TurfX < 0 || e.TurfY < 0 || e.TurfZ < 0 || (e.TurfX == 0) != (e.TurfY == 0) || (e.TurfX == 0) != (e.TurfZ == 0))
                throw Bad("a private image on a turf names a partial or a negative position");
            w.WriteVarUInt((ulong)e.TurfX);
            w.WriteVarUInt((ulong)e.TurfY);
            w.WriteVarUInt((ulong)e.TurfZ);
            // version 35 (section 44): [u8 override 0 or 1]
            w.WriteByte(e.Override ? (byte)1 : (byte)0);
        }
    }

    private static void ReadImages(ref WireReader r, OwnBlock own)
    {
        ReadImageList(ref r, own.ImagesAdded);
        ReadImageList(ref r, own.ImagesChanged);
        int removed = r.ReadCount(Limits.MaxPrivateImages, 1);
        own.ImagesRemoved.Capacity = removed;
        long previous = 0;
        for (int i = 0; i < removed; i++)
        {
            long id = ReadId(ref r, previous, true);
            previous = id;
            own.ImagesRemoved.Add(id);
        }

        if (own.ImagesAdded.Count + own.ImagesChanged.Count + removed == 0)
            throw Bad("a private block flags images and has none");
    }

    private static void ReadImageList(ref WireReader r, List<ImageEntry> into)
    {
        int count = r.ReadCount(Limits.MaxPrivateImages, MinImageBytes);
        into.Capacity = count;
        long previous = 0;
        for (int i = 0; i < count; i++)
        {
            long id = ReadId(ref r, previous, true);
            previous = id;
            TableRef appearance = ReadPlainRef(ref r, true, Limits.MaxAppearanceTableEntries, "a private image's appearance");
            long attached = ReadNonNegativeLong(ref r, "a private image's attachment");
            long holder = ReadNonNegativeLong(ref r, "a private image's holder");
            string holderName = r.ReadString(Limits.MaxImageHolderNameBytes);
            long turfX = ReadNonNegativeLong(ref r, "a private image's turf x");
            long turfY = ReadNonNegativeLong(ref r, "a private image's turf y");
            long turfZ = ReadNonNegativeLong(ref r, "a private image's turf z");
            if ((turfX == 0) != (turfY == 0) || (turfX == 0) != (turfZ == 0) || turfX > Limits.MaxMapSize || turfY > Limits.MaxMapSize || turfZ > Limits.MaxMapLevels)
                throw Bad("a private image names a turf outside the limits");
            byte overrideFlag = r.ReadByte();
            if (overrideFlag > 1)
                throw Bad("a private image's override flag is " + overrideFlag);
            if (appearance.IsNone && overrideFlag == 0)
                throw Bad("a private image names no appearance");
            into.Add(new ImageEntry(id, appearance, attached) { Holder = holder, HolderName = holderName, TurfX = (int)turfX, TurfY = (int)turfY, TurfZ = (int)turfZ, Override = overrideFlag == 1 });
        }
    }
}
