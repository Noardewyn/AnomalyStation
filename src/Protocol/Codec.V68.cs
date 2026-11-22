namespace Anomaly.Protocol;

// Version 68 (MessagesV68.cs, docs/ai/specs/protocol.md section 69):
//
//   MinimapMobs (server to client, State): [u8 id = 27] [varuint z] [count] ([varuint x] [varuint y] [u8 player 0|1] [string name])...
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    /// <summary>Encodes a <see cref="MinimapMobsMessage"/> for the State channel.</summary>
    public static byte[] Encode(MinimapMobsMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        long bound = 1 + 5 + 5 + (long)message.Mobs.Count * (5 + 5 + 1 + 5 + Limits.MaxMinimapMobNameBytes);
        return EncodeToArray(bound, (Span<byte> buffer) => Encode(message, buffer));
    }

    private static int Encode(MinimapMobsMessage m, Span<byte> destination)
    {
        if (m.Z is < 1 or > Limits.MaxMapLevels)
            throw Bad("a MinimapMobs level outside the map");
        var w = new WireWriter(destination);
        w.WriteMessageId(MessageId.MinimapMobs);
        w.WriteVarUInt((ulong)m.Z);
        w.WriteCount(m.Mobs.Count, Limits.MaxMinimapMobs);
        foreach (MinimapMob mob in m.Mobs)
        {
            if (mob.X is < 1 or > Limits.MaxMapSize || mob.Y is < 1 or > Limits.MaxMapSize)
                throw Bad("a MinimapMobs mob outside the map");
            w.WriteVarUInt((ulong)mob.X);
            w.WriteVarUInt((ulong)mob.Y);
            w.WriteByte(mob.Player ? (byte)1 : (byte)0);
            w.WriteString(mob.Name, Limits.MaxMinimapMobNameBytes);
        }
        return Finish(ref w, Limits.MaxOutboundMessageBytes);
    }

    /// <summary>Decodes a <see cref="MinimapMobsMessage"/>: a level and mobs on the map.</summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out MinimapMobsMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.MinimapMobs, Limits.MaxOutboundMessageBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            var m = new MinimapMobsMessage { Z = ReadRanged(ref r, 1, Limits.MaxMapLevels, "a MinimapMobs level") };
            int count = r.ReadCount(Limits.MaxMinimapMobs, 4);
            for (int i = 0; i < count; i++)
            {
                int x = ReadRanged(ref r, 1, Limits.MaxMapSize, "a MinimapMobs mob's x");
                int y = ReadRanged(ref r, 1, Limits.MaxMapSize, "a MinimapMobs mob's y");
                byte player = r.ReadByte();
                if (player > 1)
                    throw Bad("a MinimapMobs mob's player flag is not 0 or 1");
                m.Mobs.Add(new MinimapMob(x, y, player == 1, r.ReadString(Limits.MaxMinimapMobNameBytes)));
            }
            r.ExpectEnd();
            value = m;
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
