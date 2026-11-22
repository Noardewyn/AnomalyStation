using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

// Protocol version 81 (DV416, Anomaly.Core.Motion.FlightArc).
// - Flight (NetField.Flight): [varuint z] and, when z is not 0, [f32 start tick - message tick] [f32 x0] [f32 y0] [f32 lift0] [u8 moving]
//   and, when moving is 1, [f32 vx] [f32 vy] [f32 vlift] [f32 gravity] [f32 friction]. An entity record flags it in its extension byte (bit 16).
public static partial class Codec
{
    private const byte EntityExtFlight = 16;
    private const int MaxFlightBytes = 5 + 4 * 4 + 1 + 5 * 4;

    private static void WriteFlight(ref WireWriter w, FlightArc arc, long tick)
    {
        if (arc.Z < 0)
            throw Bad("a flight's level is negative");
        w.WriteVarUInt((ulong)arc.Z);
        if (arc.IsNone)
            return;
        float ahead = (float)(arc.StartTick - tick);
        if (!FlightValues(ahead, arc.X0, arc.Y0, arc.Lift0, arc.VX, arc.VY, arc.VLift, arc.Gravity, arc.Friction))
            throw Bad("a flight is out of range");
        w.WriteSingle(ahead);
        w.WriteSingle(arc.X0);
        w.WriteSingle(arc.Y0);
        w.WriteSingle(arc.Lift0);
        w.WriteByte(arc.IsMoving ? (byte)1 : (byte)0);
        if (!arc.IsMoving)
            return;
        w.WriteSingle(arc.VX);
        w.WriteSingle(arc.VY);
        w.WriteSingle(arc.VLift);
        w.WriteSingle(arc.Gravity);
        w.WriteSingle(arc.Friction);
    }

    private static FlightArc ReadFlight(ref WireReader r, long tick)
    {
        ulong z = r.ReadVarUInt();
        if (z == 0)
            return FlightArc.None;
        if (z > ushort.MaxValue)
            throw Bad("a flight's level is out of range");
        float ahead = r.ReadSingle(), x0 = r.ReadSingle(), y0 = r.ReadSingle(), lift0 = r.ReadSingle();
        byte moving = r.ReadByte();
        float vx = 0, vy = 0, vl = 0, g = 0, f = 0;
        if (moving > 1)
            throw Bad("a flight's moving byte is not 0 or 1");
        if (moving == 1)
        {
            vx = r.ReadSingle();
            vy = r.ReadSingle();
            vl = r.ReadSingle();
            g = r.ReadSingle();
            f = r.ReadSingle();
        }
        if (!FlightValues(ahead, x0, y0, lift0, vx, vy, vl, g, f))
            throw Bad("a flight is out of range");
        return new FlightArc(tick + (double)ahead, x0, y0, (int)z, lift0, vx, vy, vl, g, f);
    }

    // Finite, on the map's scale (tiles under 4096, lifts within 64 levels), speeds under 64 tiles or levels a tick, no negative gravity or friction
    private static bool FlightValues(float ahead, float x0, float y0, float lift0, float vx, float vy, float vl, float g, float f)
        => float.IsFinite(ahead) && Math.Abs(ahead) <= 1_000_000f && float.IsFinite(x0) && float.IsFinite(y0) && x0 >= -1 && y0 >= -1 && x0 <= 4096 && y0 <= 4096
            && float.IsFinite(lift0) && Math.Abs(lift0) <= 2048 && float.IsFinite(vx) && float.IsFinite(vy) && float.IsFinite(vl) && Math.Abs(vx) <= 64 && Math.Abs(vy) <= 64
            && Math.Abs(vl) <= 2048 && float.IsFinite(g) && g >= 0 && g <= 64 && float.IsFinite(f) && f >= 0 && f <= 64;
}
