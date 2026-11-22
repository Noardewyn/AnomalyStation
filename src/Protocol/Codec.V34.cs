namespace Anomaly.Protocol;

// Version 34 (M7x slice A, MessagesV34.cs, docs/ai/specs/protocol.md section 43):
//
//   aim (a Click's or a mouse action's, its modifiers byte bit 7): [varint east 0..65536] [varint north 0..65536] [u16 up mm] [u16 yaw]
//                                                                [i16 pitch -16384..16384] [varuint seen: the render tick, 1/16 tick (version 88)]
//   MouseDown (15), MouseDrag (16), MouseUp (17): [u8 target kind 0..3] [target unless 0] [u8 button 1..3]
//                                                [u8 modifiers: shift 1, ctrl 2, alt 4, bit 7 aim] [u8 icon-x 0..32] [u8 icon-y 0..32] [aim]
//   entity record flags bit 7, change mask bit 16: a shot: [varint x 0..8192] [varint y 0..8192] [i16 height 1/16 px] [i16 slope 1/4096] [u16 angle]
//   CameraShake: a kind byte after the id (0 shake, 1 recoil)
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes.

public static partial class Codec
{
    private const byte ModifierAimBit = 0x80;
    private const int MaxShotBytes = 3 + 3 + 2 + 2 + 2;

    private static void WriteAim(ref WireWriter w, in ShotAim aim)
    {
        if (!aim.InRange)
            throw Bad("an aim out of range");
        w.WriteVarInt(aim.East);
        w.WriteVarInt(aim.North);
        w.WriteUInt16(aim.Up);
        w.WriteUInt16(aim.Yaw);
        w.WriteUInt16(unchecked((ushort)aim.Pitch));
        w.WriteVarUInt((ulong)aim.Seen); // version 88 (DV527)
    }

    private static ShotAim ReadAim(ref WireReader r)
    {
        long east = r.ReadVarInt(), north = r.ReadVarInt();
        ushort up = r.ReadUInt16(), yaw = r.ReadUInt16();
        short pitch = unchecked((short)r.ReadUInt16());
        if (east < 0 || east > Limits.MaxAimCoordinate || north < 0 || north > Limits.MaxAimCoordinate || pitch < -ShotAim.MaxPitch || pitch > ShotAim.MaxPitch)
            throw Bad("an aim out of range");
        ulong seen = r.ReadVarUInt();
        if (seen > long.MaxValue)
            throw Bad("an aim's seen tick out of range");
        return new ShotAim((int)east, (int)north, up, yaw, pitch, (long)seen);
    }

    private static void WriteMouseAction(ref WireWriter w, in ActionMessage message, TargetKind targetKind)
    {
        if (message.AimZone != AimZone.None)
            throw Bad("an aim zone on a mouse action");
        if (targetKind == TargetKind.None)
        {
            if (message.Target != 0 || message.TurfX != 0 || message.TurfY != 0 || message.TurfZ != 0)
                throw Bad("a mouse action over nothing names a target");
            w.WriteByte(0);
        }
        else
        {
            WriteTarget(ref w, targetKind, message.Target, message.TurfX, message.TurfY, message.TurfZ, "a mouse action");
        }
        if (message.Button < ClickButton.Left || message.Button > ClickButton.Middle)
            throw Bad("a mouse action with an unknown button");
        if ((message.Modifiers & ~ClickModifiers.All) != 0)
            throw Bad("a mouse action with unknown modifier bits");
        if (message.IconX > Limits.MaxIconPixel || message.IconY > Limits.MaxIconPixel)
            throw Bad("a mouse action with an icon pixel over " + Limits.MaxIconPixel);
        w.WriteByte((byte)message.Button);
        w.WriteByte((byte)((byte)message.Modifiers | (message.Aim.HasValue ? ModifierAimBit : 0)));
        w.WriteByte(message.IconX);
        w.WriteByte(message.IconY);
        if (message.Aim.HasValue)
            WriteAim(ref w, message.Aim.Value);
    }

    private static void ReadMouseAction(ref WireReader r, out TargetKind kind, out long target, out int x, out int y, out int z, out ClickButton button,
        out ClickModifiers modifiers, out byte iconX, out byte iconY, out ShotAim? aim)
    {
        kind = TargetKind.None;
        target = 0;
        x = y = z = 0;
        if (r.PeekByte() == 0)
            r.ReadByte();
        else
            ReadTarget(ref r, out kind, out target, out x, out y, out z, "a mouse action");
        byte buttonByte = r.ReadByte();
        if (buttonByte < (byte)ClickButton.Left || buttonByte > (byte)ClickButton.Middle)
            throw Bad("a mouse action with an unknown button");
        button = (ClickButton)buttonByte;
        byte mods = r.ReadByte();
        if ((mods & ~(ModifierAimBit | (byte)ClickModifiers.All)) != 0)
            throw Bad("a mouse action with unknown modifier bits");
        modifiers = (ClickModifiers)(mods & (byte)ClickModifiers.All);
        iconX = r.ReadByte();
        iconY = r.ReadByte();
        if (iconX > Limits.MaxIconPixel || iconY > Limits.MaxIconPixel)
            throw Bad("a mouse action with an icon pixel over " + Limits.MaxIconPixel);
        aim = (mods & ModifierAimBit) != 0 ? ReadAim(ref r) : null;
    }

    private static void WriteShot(ref WireWriter w, in ShotState shot)
    {
        if (shot.OriginX < 0 || shot.OriginX > Limits.MaxShotCoordinate || shot.OriginY < 0 || shot.OriginY > Limits.MaxShotCoordinate)
            throw Bad("a shot's origin outside the map");
        w.WriteVarInt(shot.OriginX);
        w.WriteVarInt(shot.OriginY);
        w.WriteUInt16(unchecked((ushort)shot.OriginH));
        w.WriteUInt16(unchecked((ushort)shot.Slope));
        w.WriteUInt16(shot.Angle);
    }

    private static ShotState ReadShot(ref WireReader r)
    {
        long x = r.ReadVarInt(), y = r.ReadVarInt();
        if (x < 0 || x > Limits.MaxShotCoordinate || y < 0 || y > Limits.MaxShotCoordinate)
            throw Bad("a shot's origin outside the map");
        short height = unchecked((short)r.ReadUInt16()), slope = unchecked((short)r.ReadUInt16());
        return new ShotState((int)x, (int)y, height, slope, r.ReadUInt16());
    }
}
