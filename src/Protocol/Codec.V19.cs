namespace Anomaly.Protocol;

// Version 19 (MessagesV19.cs, docs/ai/specs/protocol.md section 28):
//
//   Tooltip (server to client, State): [u8 id = 17] [u8 kind: 0 show, 1 hide]
//     show: [str(1..4096) text] [str(1..32) theme: a-z 0-9 '-' '_']
//     hide: nothing more
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    /// <summary>Encodes a <see cref="TooltipMessage"/> for the State channel (at most <see cref="Limits.MaxTooltipBytes"/>).</summary>
    public static int Encode(TooltipMessage message, Span<byte> destination)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.Tooltip);
        if (message.Kind > TooltipKind.Hide)
            throw Bad("an unknown tooltip kind " + (byte)message.Kind);
        w.WriteByte((byte)message.Kind);
        if (message.Kind == TooltipKind.Hide)
        {
            if (message.Text != null || message.Theme != null)
                throw Bad("a tooltip hide with a show's fields");
            return Finish(ref w, Limits.MaxTooltipBytes);
        }
        if (string.IsNullOrEmpty(message.Text))
            throw new ProtocolException(ProtocolError.BadString, "a tooltip shown without a text");
        w.WriteString(message.Text, Limits.MaxTooltipTextBytes);
        if (!TooltipMessage.IsValidTheme(message.Theme))
            throw new ProtocolException(ProtocolError.BadString, "a tooltip theme that is no class name");
        w.WriteString(message.Theme, Limits.MaxTooltipThemeBytes);
        return Finish(ref w, Limits.MaxTooltipBytes);
    }

    /// <summary>The encoded <see cref="TooltipMessage"/>.</summary>
    public static byte[] Encode(TooltipMessage message) =>
        EncodeToArray(Limits.MaxTooltipBytes, (Span<byte> buffer) => Encode(message, buffer));

    /// <summary>Decodes a <see cref="TooltipMessage"/>: the kind; a show's non-empty text and its theme, a class name.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out TooltipMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.Tooltip, Limits.MaxTooltipBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            byte kind = r.ReadByte();
            if (kind > (byte)TooltipKind.Hide)
                throw Bad("an unknown tooltip kind " + kind);
            var result = new TooltipMessage { Kind = (TooltipKind)kind };
            if (result.Kind == TooltipKind.Show)
            {
                result.Text = r.ReadString(Limits.MaxTooltipTextBytes);
                if (string.IsNullOrEmpty(result.Text))
                    throw new ProtocolException(ProtocolError.BadString, "a tooltip shown without a text");
                result.Theme = r.ReadString(Limits.MaxTooltipThemeBytes);
                if (!TooltipMessage.IsValidTheme(result.Theme))
                    throw new ProtocolException(ProtocolError.BadString, "a tooltip theme that is no class name");
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
}
