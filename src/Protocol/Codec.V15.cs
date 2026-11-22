namespace Anomaly.Protocol;

// Version 15 (MessagesV15.cs, docs/ai/specs/protocol.md section 24):
//
//   RadialMenu (server to client, State): [u8 id = 15] [u8 kind: 0 open, 1 close] [varuint menu id >= 1]
//     open:  [u8 flags: bit 0 tooltips, bit 1 close button, bit 2 slide in, bit 3 fade in, bit 4 fade out; bits 5-7 zero]
//            [u8 page 1..pages] [u8 pages 1..255]
//            close button: [tableref centre] [tableref centre hovered]      (both never none)
//            [count 1..16 slices] { [str(1..128) name] [u8 slice flags: bit 0 next page, bit 1 click on hover; bits 2-7 zero]
//                                   [tableref picture (may be none)] [tableref slice (never none)] [tableref slice hovered (never none)] }*
//     close: nothing more
//   a tableref is a varuint (appearance index << 1) | private, as everywhere (Codec.V2).
//
//   RadialPick (Action kind 10, client to server, Control): [varuint menu id >= 1] [varuint element 0..16] (Codec.V2's action).
//
// Every rule of the earlier codecs holds: TryDecode never throws, every count is capped before anything is allocated, and a
// message that decodes encodes back to the same bytes. Whether the appearances exist is the replica's check
// (ReplicaWorld.Apply(RadialMenuMessage)).

public static partial class Codec
{
    private const int MinRadialSliceBytes = 1 + 1 + 1 + 1 + 1 + 1;

    /// <summary>Encodes a <see cref="RadialMenuMessage"/> for the State channel (at most <see cref="Limits.MaxRadialMenuBytes"/>).</summary>
    public static int Encode(RadialMenuMessage message, Span<byte> destination)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.Radial);
        if (message.Kind > RadialMenuKind.Close)
            throw Bad("an unknown radial menu kind " + (byte)message.Kind);
        w.WriteByte((byte)message.Kind);
        if (message.MenuId < 1)
            throw Bad("a radial menu id that is not positive");
        w.WriteVarUInt((ulong)message.MenuId);
        if (message.Kind == RadialMenuKind.Close)
        {
            if (message.Flags != RadialMenuFlags.None || message.Page != 0 || message.Pages != 0 || !message.Center.IsNone || !message.CenterFocus.IsNone || message.Elements.Count != 0)
                throw Bad("a radial menu close with an open's fields");
            return Finish(ref w, Limits.MaxRadialMenuBytes);
        }
        if ((message.Flags & ~RadialMenuFlags.All) != 0)
            throw Bad("a radial menu with unknown flag bits");
        if (message.Pages < 1 || message.Page < 1 || message.Page > message.Pages)
            throw Bad("a radial menu page " + message.Page + " of " + message.Pages);
        w.WriteByte((byte)message.Flags);
        w.WriteByte(message.Page);
        w.WriteByte(message.Pages);
        bool center = (message.Flags & RadialMenuFlags.CloseButton) != 0;
        if (center)
        {
            WriteRadialRef(ref w, message.Center, false, "a radial menu's centre");
            WriteRadialRef(ref w, message.CenterFocus, false, "a radial menu's hovered centre");
        }
        else if (!message.Center.IsNone || !message.CenterFocus.IsNone)
            throw Bad("a radial menu centre without its close button flag");
        if (message.Elements.Count == 0)
            throw Bad("a radial menu open with no slices");
        w.WriteCount(message.Elements.Count, Limits.MaxRadialElements);
        foreach (RadialSlice slice in message.Elements)
        {
            if (string.IsNullOrEmpty(slice.Name))
                throw new ProtocolException(ProtocolError.BadString, "a radial slice without a name");
            w.WriteString(slice.Name, Limits.MaxRadialNameBytes);
            if ((slice.Flags & ~RadialElementFlags.All) != 0)
                throw Bad("a radial slice with unknown flag bits");
            w.WriteByte((byte)slice.Flags);
            WriteRadialRef(ref w, slice.Icon, true, "a radial slice's picture");
            WriteRadialRef(ref w, slice.Background, false, "a radial slice");
            WriteRadialRef(ref w, slice.BackgroundFocus, false, "a hovered radial slice");
        }
        return Finish(ref w, Limits.MaxRadialMenuBytes);
    }

    /// <summary>The encoded <see cref="RadialMenuMessage"/>.</summary>
    public static byte[] Encode(RadialMenuMessage message) =>
        EncodeToArray(Limits.MaxRadialMenuBytes, (Span<byte> buffer) => Encode(message, buffer));

    /// <summary>Decodes a <see cref="RadialMenuMessage"/>: the kind, a positive menu id, the flags, the page within the pages, the centre with its flag, 1 to 16 named slices.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out RadialMenuMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.Radial, Limits.MaxRadialMenuBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            byte kind = r.ReadByte();
            if (kind > (byte)RadialMenuKind.Close)
                throw Bad("an unknown radial menu kind " + kind);
            long id = ReadNonNegativeLong(ref r, "a radial menu id");
            if (id < 1)
                throw Bad("a radial menu id that is not positive");
            var result = new RadialMenuMessage { Kind = (RadialMenuKind)kind, MenuId = id };
            if (result.Kind == RadialMenuKind.Open)
            {
                byte flags = r.ReadByte();
                if ((flags & ~(byte)RadialMenuFlags.All) != 0)
                    throw Bad("a radial menu with unknown flag bits");
                result.Flags = (RadialMenuFlags)flags;
                result.Page = r.ReadByte();
                result.Pages = r.ReadByte();
                if (result.Pages < 1 || result.Page < 1 || result.Page > result.Pages)
                    throw Bad("a radial menu page " + result.Page + " of " + result.Pages);
                if ((result.Flags & RadialMenuFlags.CloseButton) != 0)
                {
                    result.Center = ReadRadialRef(ref r, false, "a radial menu's centre");
                    result.CenterFocus = ReadRadialRef(ref r, false, "a radial menu's hovered centre");
                }
                int count = r.ReadCount(Limits.MaxRadialElements, MinRadialSliceBytes);
                if (count == 0)
                    throw Bad("a radial menu open with no slices");
                for (int i = 0; i < count; i++)
                {
                    string name = r.ReadString(Limits.MaxRadialNameBytes);
                    if (string.IsNullOrEmpty(name))
                        throw new ProtocolException(ProtocolError.BadString, "a radial slice without a name");
                    byte sliceFlags = r.ReadByte();
                    if ((sliceFlags & ~(byte)RadialElementFlags.All) != 0)
                        throw Bad("a radial slice with unknown flag bits");
                    TableRef icon = ReadRadialRef(ref r, true, "a radial slice's picture");
                    TableRef background = ReadRadialRef(ref r, false, "a radial slice");
                    TableRef focus = ReadRadialRef(ref r, false, "a hovered radial slice");
                    result.Elements.Add(new RadialSlice(name, (RadialElementFlags)sliceFlags, icon, background, focus));
                }
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

    // An appearance of the session's tables, private or public; none only where allowed.
    private static void WriteRadialRef(ref WireWriter w, TableRef reference, bool allowNone, string what)
    {
        if (reference.IsNone && !allowNone)
            throw Bad(what + " names no appearance");
        WritePlainRef(ref w, reference, true, Limits.MaxAppearanceTableEntries, what);
    }

    private static TableRef ReadRadialRef(ref WireReader r, bool allowNone, string what)
    {
        TableRef reference = ReadPlainRef(ref r, true, Limits.MaxAppearanceTableEntries, what);
        if (reference.IsNone && !allowNone)
            throw Bad(what + " names no appearance");
        return reference;
    }
}
