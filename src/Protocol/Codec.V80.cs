using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

// Protocol version 80 (DV403, Anomaly.Core.Motion.TramRide).
// - Tram (NetField.Tram): [varuint steps] and, when steps is not 0, [zigzag varint start tick - message tick] [varuint ticks per step]
//   [u8 dir] [varuint x0] [varuint y0] [u8 width] [u8 height]. An entity record flags it in its extension byte (bit 8).
public static partial class Codec
{
    private const byte EntityExtTram = 8;
    private const int MaxTramBytes = 3 + 10 + 3 + 1 + 3 + 3 + 1 + 1;

    private static void WriteTram(ref WireWriter w, TramRide ride, long tick)
    {
        w.WriteVarUInt(ride.Steps);
        if (ride.IsNone)
            return;
        if (ride.TicksPerStep == 0 || ride.Width == 0 || ride.Height == 0 || ride.X0 < 0 || ride.Y0 < 0 || !OneCardinal(ride.Dir))
            throw Bad("a tram trip takes no time, has no footprint or goes no single cardinal way");
        long ahead = ride.StartTick - tick;
        w.WriteVarUInt((ulong)((ahead << 1) ^ (ahead >> 63)));
        w.WriteVarUInt(ride.TicksPerStep);
        w.WriteByte(ride.Dir);
        w.WriteVarUInt((ulong)ride.X0);
        w.WriteVarUInt((ulong)ride.Y0);
        w.WriteByte(ride.Width);
        w.WriteByte(ride.Height);
    }

    private static TramRide ReadTram(ref WireReader r, long tick)
    {
        ulong steps = r.ReadVarUInt();
        if (steps == 0)
            return TramRide.None;
        ulong zig = r.ReadVarUInt();
        long ahead = (long)(zig >> 1) ^ -(long)(zig & 1);
        ulong perStep = r.ReadVarUInt();
        byte dir = r.ReadByte();
        ulong x0 = r.ReadVarUInt();
        ulong y0 = r.ReadVarUInt();
        byte width = r.ReadByte();
        byte height = r.ReadByte();
        if (steps > ushort.MaxValue || perStep == 0 || perStep > ushort.MaxValue || Math.Abs(ahead) > 1_000_000 || x0 > (ulong)short.MaxValue
            || y0 > (ulong)short.MaxValue || width == 0 || height == 0 || !OneCardinal(dir))
            throw Bad("a tram trip is out of range");
        return new TramRide(tick + ahead, (ushort)perStep, (ushort)steps, dir, (short)x0, (short)y0, width, height);
    }

    private static bool OneCardinal(byte dir) => dir is MotionDir.North or MotionDir.South or MotionDir.East or MotionDir.West;
}
