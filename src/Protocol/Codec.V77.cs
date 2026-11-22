namespace Anomaly.Protocol;

// Protocol version 77 (DV371, MessagesV77.cs).
// - Ride (NetField.Ride): [s8 dz] and, when dz is not 0, [zigzag varint end tick - message tick] [varuint ticks].
//   An entity record flags it in its extension byte (bit 4).
public static partial class Codec
{
    private const byte EntityExtRide = 4;
    private const int MaxRideBytes = 1 + 10 + 5;

    private static void WriteRide(ref WireWriter w, RideState ride, long tick)
    {
        if (ride.Dz < -1 || ride.Dz > 1 || (ride.Dz != 0 && ride.Ticks == 0))
            throw Bad("a ride crosses more than one level or takes no time");
        w.WriteByte((byte)ride.Dz);
        if (ride.Dz == 0)
            return;
        long ahead = ride.EndTick - tick;
        w.WriteVarUInt((ulong)((ahead << 1) ^ (ahead >> 63)));
        w.WriteVarUInt(ride.Ticks);
    }

    private static RideState ReadRide(ref WireReader r, long tick)
    {
        sbyte dz = (sbyte)r.ReadByte();
        if (dz < -1 || dz > 1)
            throw Bad("a ride crosses more than one level");
        if (dz == 0)
            return RideState.None;
        ulong zig = r.ReadVarUInt();
        long ahead = (long)(zig >> 1) ^ -(long)(zig & 1);
        ulong ticks = r.ReadVarUInt();
        if (ticks == 0 || ticks > ushort.MaxValue || Math.Abs(ahead) > 1_000_000)
            throw Bad("a ride's length or end is out of range");
        return new RideState(dz, tick + ahead, (ushort)ticks);
    }
}
