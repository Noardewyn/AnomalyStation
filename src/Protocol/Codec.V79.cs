namespace Anomaly.Protocol;

// Version 79 (MessagesV79.cs):
//
//   RuntimeResource (server to client, State): [u8 id = 29] [str(64) path rsc/<32 hex>.png] [varuint length 1..MaxRuntimeResourceBytes]
//                                              [bytes, a PNG file]
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    /// <summary>Whether <paramref name="path"/> is a runtime resource path: <c>rsc/</c>, 32 lower-case hex digits, <c>.png</c>.</summary>
    public static bool IsRuntimeResourcePath(string path)
    {
        if (path == null || path.Length != 4 + 32 + 4 || !path.StartsWith("rsc/", StringComparison.Ordinal) || !path.EndsWith(".png", StringComparison.Ordinal))
            return false;
        for (int i = 4; i < 36; i++)
        {
            if (path[i] is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
                return false;
        }
        return true;
    }

    private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static byte[] Encode(RuntimeResourceMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!IsRuntimeResourcePath(message.Path))
            throw Bad("a runtime resource path that is not rsc/<md5>.png");
        if (message.Png == null || message.Png.Length < PngSignature.Length || message.Png.Length > Limits.MaxRuntimeResourceBytes
            || !message.Png.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
            throw Bad("a runtime resource that is not a PNG of 1 to " + Limits.MaxRuntimeResourceBytes + " bytes");
        return EncodeToArray(16 + 64 + message.Png.Length, (Span<byte> buffer) =>
        {
            var w = new WireWriter(buffer);
            w.WriteMessageId(MessageId.RuntimeResource);
            w.WriteString(message.Path, 64);
            w.WriteVarUInt((ulong)message.Png.Length);
            w.WriteBytes(message.Png);
            return Finish(ref w, Limits.MaxOutboundMessageBytes);
        });
    }

    public static bool TryDecode(ReadOnlySpan<byte> message, out RuntimeResourceMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.RuntimeResource, Limits.MaxOutboundMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            string path = r.ReadString(64);
            if (!IsRuntimeResourcePath(path))
                throw Bad("a runtime resource path that is not rsc/<md5>.png");
            ulong length = r.ReadVarUInt();
            if (length < (ulong)PngSignature.Length || length > Limits.MaxRuntimeResourceBytes)
                throw Bad("a runtime resource of " + length + " bytes");
            byte[] png = r.ReadBytes((int)length).ToArray();
            if (!png.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
                throw Bad("a runtime resource that is not a PNG");
            r.ExpectEnd();
            value = new RuntimeResourceMessage(path, png);
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
