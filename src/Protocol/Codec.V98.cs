namespace Anomaly.Protocol;

// Version 98 (package T, slice T4a; docs/ai/specs/protocol.md section 93): a click's, a throw's and a put-down's aim zone gains
// AimZone.Eyes (7), Mouth (8) and Groin (9). The modifiers byte's spare bit 3 is the zone's high bit:
//
//   [u8 modifiers: shift 1, ctrl 2, alt 4, bit 3 the zone's high bit, bits 4-6 the zone's low bits, bit 7 aim]
//   zone = ((mods >> 4) & 7) | (mods & 8), 0..9 (10 to 15 refused)
//
// so every byte version 97 sent (bit 3 zero, a zone of 0 to 6) keeps its meaning.
//
// Version 98 also (slice T4b, section 94): a hover on an entity may end with the zone looked at, one byte of 1 to 9 after its target; a
// hover without a zone has no such byte, so it is the bytes of version 97. A zone byte of 0 or above AimZone.Last, or after a turf, a
// ceiling or a screen target, is refused (BadValue).

public static partial class Codec
{
    private const byte ModifierZoneHighBit = 0x08;

    // The zone's bits in a click's modifiers byte.
    private static byte ZoneBits(AimZone zone) => (byte)((((byte)zone & 7) << 4) | ((byte)zone & ModifierZoneHighBit));

    // The zone a click's modifiers byte names (0 to 15; the caller refuses one above AimZone.Last).
    private static int ZoneOfModifiers(byte mods) => ((mods >> 4) & 7) | (mods & ModifierZoneHighBit);

    // Slice T4b: a hover's zone, written after its target only when there is one.
    private static void WriteHoverZone(ref WireWriter w, AimZone zone, TargetKind targetKind)
    {
        if (zone == AimZone.None)
            return;
        if (zone > AimZone.Last)
            throw Bad("a hover with an unknown aim zone");
        if (targetKind != TargetKind.Entity)
            throw Bad("an aim zone on a hover whose target is not an entity");
        w.WriteByte((byte)zone);
    }

    // Slice T4b: the zone byte that may end a hover (none when the message ends after the target).
    private static AimZone ReadHoverZone(ref WireReader r, TargetKind targetKind)
    {
        if (r.End)
            return AimZone.None;
        byte zone = r.ReadByte();
        if (zone == (byte)AimZone.None || zone > (byte)AimZone.Last)
            throw Bad("a hover with an unknown aim zone");
        if (targetKind != TargetKind.Entity)
            throw Bad("an aim zone on a hover whose target is not an entity");
        return (AimZone)zone;
    }
}
