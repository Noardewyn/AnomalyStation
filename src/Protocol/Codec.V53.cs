namespace Anomaly.Protocol;

// Version 53 (MessagesV53.cs, docs/ai/specs/protocol.md section 57):
//
//   BugReport (client to server, State): [u8 id = 23] [varuint total 1..MaxBugReportBodyBytes] [varuint offset < total]
//     [varuint length 1..4096, offset + length <= total] [bytes]: a part of a report's body, sent in order from offset 0.
//   The body: [str(1..300) title] [str(1..8000) what happened] [str(0..8000) steps or none] [str(0..1024) client or none]
//     [varuint image length 0..2 MiB, 0 for none] [image bytes]
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    /// <summary>The encoded body of a <see cref="BugReportMessage"/> (the parts carry it).</summary>
    public static byte[] EncodeBugReportBody(BugReportMessage message)
    {
        if (string.IsNullOrEmpty(message.Title) || string.IsNullOrEmpty(message.WhatHappened))
            throw Bad("a bug report without a title or what happened");
        if (message.Image != null && (message.Image.Length == 0 || message.Image.Length > Limits.MaxBugReportImageBytes))
            throw new ProtocolException(ProtocolError.TooLong, "a bug report's image of " + message.Image.Length + " bytes, not 1 to " + Limits.MaxBugReportImageBytes);
        byte[] buffer = new byte[Limits.MaxBugReportBodyBytes];
        var w = new WireWriter(buffer);
        w.WriteString(message.Title, Limits.MaxBugReportTitleBytes);
        w.WriteString(message.WhatHappened, Limits.MaxBugReportTextBytes);
        w.WriteString(message.Steps, Limits.MaxBugReportTextBytes);
        w.WriteString(message.Client, Limits.MaxBugReportClientBytes);
        w.WriteVarUInt((ulong)(message.Image?.Length ?? 0));
        if (message.Image != null)
            w.WriteBytes(message.Image);
        return buffer.AsSpan(0, Finish(ref w, Limits.MaxBugReportBodyBytes)).ToArray();
    }

    /// <summary>The <see cref="BugReportPart"/> messages of a report, in the order they are sent.</summary>
    public static List<byte[]> EncodeBugReportParts(BugReportMessage message)
    {
        byte[] body = EncodeBugReportBody(message);
        var parts = new List<byte[]>();
        for (int offset = 0; offset < body.Length; offset += Limits.MaxBugReportPartBytes)
            parts.Add(Encode(new BugReportPart(body.Length, offset, body.AsSpan(offset, Math.Min(Limits.MaxBugReportPartBytes, body.Length - offset)).ToArray())));
        return parts;
    }

    /// <summary>Encodes a <see cref="BugReportPart"/> for the State channel.</summary>
    public static byte[] Encode(BugReportPart part)
    {
        if (!ValidPart(part.Total, part.Offset, part.Bytes?.Length ?? 0))
            throw Bad("a bug report part out of range");
        Span<byte> buffer = stackalloc byte[Limits.MaxBugReportPartMessageBytes];
        var w = new WireWriter(buffer);
        w.WriteMessageId(MessageId.BugReport);
        w.WriteVarUInt((ulong)part.Total);
        w.WriteVarUInt((ulong)part.Offset);
        w.WriteVarUInt((ulong)part.Bytes.Length);
        w.WriteBytes(part.Bytes);
        return buffer.Slice(0, Finish(ref w, Limits.MaxBugReportPartMessageBytes)).ToArray();
    }

    private static bool ValidPart(long total, long offset, long length)
        => total >= 1 && total <= Limits.MaxBugReportBodyBytes && offset >= 0 && offset < total && length >= 1 && length <= Limits.MaxBugReportPartBytes && offset + length <= total;

    /// <summary>Decodes a <see cref="BugReportPart"/>: a total within the body's cap, the part inside it, at most 4096 bytes.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out BugReportPart value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.BugReport, Limits.MaxBugReportPartMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            ulong total = r.ReadVarUInt(), offset = r.ReadVarUInt(), length = r.ReadVarUInt();
            if (total > int.MaxValue || offset > int.MaxValue || length > int.MaxValue || !ValidPart((long)total, (long)offset, (long)length))
                throw Bad("a bug report part out of range");
            byte[] bytes = r.ReadBytes((int)length).ToArray();
            r.ExpectEnd();
            value = new BugReportPart((int)total, (int)offset, bytes);
            return true;
        }
        catch (ProtocolException e)
        {
            value = null;
            error = e.Error;
            return false;
        }
    }

    /// <summary>Decodes a reassembled body (<see cref="EncodeBugReportBody"/>): a title and what happened, every field within its cap.</summary>
    public static bool TryDecodeBugReportBody(ReadOnlySpan<byte> body, out BugReportMessage value, out ProtocolError error)
    {
        value = null;
        error = ProtocolError.None;
        if (body.Length > Limits.MaxBugReportBodyBytes)
        {
            error = ProtocolError.TooLong;
            return false;
        }
        try
        {
            var r = new WireReader(body);
            string title = r.ReadString(Limits.MaxBugReportTitleBytes);
            string what = r.ReadString(Limits.MaxBugReportTextBytes);
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(what))
                throw Bad("a bug report without a title or what happened");
            string steps = r.ReadString(Limits.MaxBugReportTextBytes);
            string client = r.ReadString(Limits.MaxBugReportClientBytes);
            ulong length = r.ReadVarUInt();
            if (length > Limits.MaxBugReportImageBytes)
                throw new ProtocolException(ProtocolError.TooLong, "a bug report's image over " + Limits.MaxBugReportImageBytes + " bytes");
            byte[] image = length == 0 ? null : r.ReadBytes((int)length).ToArray();
            r.ExpectEnd();
            value = new BugReportMessage(title, what, steps, client, image);
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
