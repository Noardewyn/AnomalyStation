namespace Anomaly.Protocol;

// Version 95 (package T, slice T4a; docs/ai/specs/protocol.md section 90): a click's, a throw's and a put-down's aim zone gains
// AimZone.Eyes (7), Mouth (8) and Groin (9). The modifiers byte's spare bit 3 is the zone's high bit:
//
//   [u8 modifiers: shift 1, ctrl 2, alt 4, bit 3 the zone's high bit, bits 4-6 the zone's low bits, bit 7 aim]
//   zone = ((mods >> 4) & 7) | (mods & 8), 0..9 (10 to 15 refused)
//
// so every byte version 94 sent (bit 3 zero, a zone of 0 to 6) keeps its meaning.

public static partial class Codec
{
    private const byte ModifierZoneHighBit = 0x08;

    // The zone's bits in a click's modifiers byte.
    private static byte ZoneBits(AimZone zone) => (byte)((((byte)zone & 7) << 4) | ((byte)zone & ModifierZoneHighBit));

    // The zone a click's modifiers byte names (0 to 15; the caller refuses one above AimZone.Last).
    private static int ZoneOfModifiers(byte mods) => ((mods >> 4) & 7) | (mods & ModifierZoneHighBit);
}
