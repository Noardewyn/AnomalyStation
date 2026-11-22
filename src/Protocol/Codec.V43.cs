namespace Anomaly.Protocol;

// Protocol version 43 (M10 slice E, protocol.md section 52; the fields: MessagesV43.cs, ScreenEntry in MessagesV2.cs).
// - Door (NetField.Door): [u8 flags] [u8 unres_sides] [count] {[string]}* (req_access) [count] {[string]}* (req_one_access).
//   An entity record flags it in its extension byte (bit 128 of the record's flags, then [u8 ext: 1 shot, 2 door]).
// - The private block: [varuint actions done] [u8 v43: bit 0 access] after the input buffer depth; with bit 0
//   [count] {[string]}* the own access list.
// - A screen entry's look: bit 4 [varuint slot_id] [u8 held_index] last; bit 5: each vis child's look is followed by [varuint slot_flags << 3 | storage << 2 | equip delay << 1 | item].
public static partial class Codec
{
    private const byte EntityExtShot = 1;
    private const byte EntityExtDoor = 2;
    private const byte OwnV43Access = 1;
    private const byte OwnV51Eye = 2; // version 51 (MessagesV51.cs): [varuint eye] after the access list
    private const byte ScreenLookSlot = 16;
    private const byte ScreenLookItems = 32;

    private static void WriteDoor(ref WireWriter w, DoorState door)
    {
        if (door == null || ((byte)door.Flags & ~(byte)DoorFlags.All) != 0 || door.UnresSides > 15 || door.ReqAll == null || door.ReqAny == null)
            throw Bad("a door state that is null, holds undefined flags or sides, or has no lists");
        w.WriteByte((byte)door.Flags);
        w.WriteByte(door.UnresSides);
        WriteAccessList(ref w, door.ReqAll, Limits.MaxDoorAccessEntries);
        WriteAccessList(ref w, door.ReqAny, Limits.MaxDoorAccessEntries);
    }

    private static DoorState ReadDoor(ref WireReader r)
    {
        byte flags = r.ReadByte();
        if ((flags & ~(byte)DoorFlags.All) != 0)
            throw Bad("a door state sets flag bits that are not defined");
        byte sides = r.ReadByte();
        if (sides > 15)
            throw Bad("a door's unrestricted sides are not directions");
        return new DoorState((DoorFlags)flags, sides, ReadAccessList(ref r, Limits.MaxDoorAccessEntries).ToArray(), ReadAccessList(ref r, Limits.MaxDoorAccessEntries).ToArray());
    }

    private static long MaxDoorBytes(DoorState door)
        => door == null ? 0 : 2 + MaxAccessListBytes(door.ReqAll) + MaxAccessListBytes(door.ReqAny);

    private static void WriteAccessList(ref WireWriter w, IReadOnlyList<string> list, int max)
    {
        w.WriteCount(list.Count, max);
        foreach (string s in list)
        {
            if (string.IsNullOrEmpty(s))
                throw Bad("an empty access");
            w.WriteString(s, Limits.MaxAccessBytes);
        }
    }

    private static List<string> ReadAccessList(ref WireReader r, int max)
    {
        int count = r.ReadCount(max, 2);
        var list = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            string s = r.ReadString(Limits.MaxAccessBytes);
            if (string.IsNullOrEmpty(s))
                throw Bad("an empty access");
            list.Add(s);
        }
        return list;
    }

    private static long MaxAccessListBytes(IReadOnlyList<string> list)
    {
        long size = 5;
        if (list != null)
            foreach (string s in list)
                size += 5 + MaxUtf8(s, Limits.MaxAccessBytes);
        return size;
    }

    private static void WriteOwnV43(ref WireWriter w, OwnBlock own)
    {
        w.WriteVarUInt(own.ActionsDone);
        if (own.Eye < 0)
            throw Bad("a negative eye entity");
        w.WriteByte((byte)((own.Access != null ? OwnV43Access : 0) | (own.Eye != null ? OwnV51Eye : 0) | OwnV78Flags(own)));
        if (own.Access != null)
            WriteAccessList(ref w, own.Access, Limits.MaxAccessEntries);
        if (own.Eye != null)
            w.WriteVarUInt((ulong)own.Eye.Value);
        WriteOwnV78(ref w, own); // version 78
    }

    private static void ReadOwnV43(ref WireReader r, OwnBlock own)
    {
        ulong done = r.ReadVarUInt();
        if (done > uint.MaxValue)
            throw Bad("a private block's actions done is over 32 bits");
        own.ActionsDone = (uint)done;
        byte flags = r.ReadByte();
        if ((flags & ~OwnV43FlagsAll) != 0)
            throw Bad("a private block's version 43 flags set bits that are not defined");
        own.Access = (flags & OwnV43Access) != 0 ? ReadAccessList(ref r, Limits.MaxAccessEntries) : null;
        own.Eye = (flags & OwnV51Eye) != 0 ? ReadNonNegativeLong(ref r, "a private block's eye") : null;
        own.HandsBlocked = (flags & OwnV89HandsBlocked) != 0; // version 89
        ReadOwnV78(ref r, own, flags); // version 78
    }

    private static long MaxOwnV43Bytes(OwnBlock own) => 5 + 1 + (own.Access == null ? 0 : MaxAccessListBytes(own.Access)) + (own.Eye == null ? 0 : 10) + MaxOwnV78Bytes(own);
}
