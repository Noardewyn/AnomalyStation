namespace Anomaly.Protocol;

// Version 42 (MessagesV42.cs, docs/ai/specs/protocol.md section 51):
//
//   Motion (server to client, Input): [u8 id = 22] [varuint tick] [varuint count]
//                                     { [varuint id step] [u16 pos x] [u16 pos y] [u8 yaw] }*
//
// The id step is the id minus the previous entry's (the first entry's is the id itself), so ids are positive and strictly increasing.
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    /// <summary>Encodes a <see cref="MotionMessage"/>; its entries must fit <see cref="Limits.MaxMotionBytes"/>.</summary>
    public static int Encode(MotionMessage message, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(message);
        var w = new WireWriter(destination);
        CheckNonNegative(message.Tick, "a Motion tick");
        w.WriteMessageId(MessageId.Motion);
        w.WriteVarUInt((ulong)message.Tick);
        w.WriteVarUInt((ulong)message.Entries.Count);
        long previous = 0;
        foreach (MotionEntry e in message.Entries)
        {
            if (e.Id <= previous)
                throw Bad("motion entries that are not in increasing id order");
            w.WriteVarUInt((ulong)(e.Id - previous));
            WritePos(ref w, e.PosX, e.PosY);
            w.WriteByte(e.Yaw);
            previous = e.Id;
        }
        return Finish(ref w, Limits.MaxMotionBytes);
    }

    /// <summary>The encoded <see cref="MotionMessage"/>.</summary>
    public static byte[] Encode(MotionMessage message)
    {
        Span<byte> buffer = stackalloc byte[Limits.MaxMotionBytes + Limits.MaxMotionEntryBytes];
        return buffer.Slice(0, Encode(message, buffer)).ToArray();
    }

    /// <summary>Decodes a <see cref="MotionMessage"/>: increasing positive ids, positions on the map.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out MotionMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.Motion, Limits.MaxMotionBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            ulong tick = r.ReadVarUInt();
            if (tick > long.MaxValue)
                throw Bad("a Motion tick over the range");
            var decoded = new MotionMessage { Tick = (long)tick };
            int count = r.ReadCount(Limits.MaxMotionBytes / 6, 6);
            long id = 0;
            for (int i = 0; i < count; i++)
            {
                ulong step = r.ReadVarUInt();
                if (step == 0 || step > (ulong)(long.MaxValue - id))
                    throw Bad("a motion entry whose id does not increase");
                id += (long)step;
                ReadPos(ref r, out int x, out int y);
                decoded.Entries.Add(new MotionEntry(id, x, y, r.ReadByte()));
            }
            r.ExpectEnd();
            value = decoded;
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
