namespace Anomaly.Protocol;

// Protocol version 11 (M4v slice 4, D143): a screen entry's flick and animate() chains (docs/ai/specs/protocol.md section 20; the fields: ScreenEntry in MessagesV2.cs).
// Version 12 adds the look's bit 2, the entry's clickable vis_contents children (section 21, Codec.V12.cs); version 27 bit 3, an
// action button's target (section 36).
public static partial class Codec
{
    // After a screen entry's group: [u8 look: bit 0 flick, bit 1 animation, bit 2 vis children (version 12)] and, when the flick or
    // the animation is there, [varuint reference tick R] (the latest start of the flick and the tracks: the own block is encoded
    // without the message's tick), then the flick [varuint appearance (index << 1 | private)] [varuint R - its tick], then the
    // animation as section 18.1 writes it with R in the message tick's place (never 0 tracks), then the children (section 21).
    private const byte ScreenLookFlick = 1;
    private const byte ScreenLookAnimation = 2;
    private const byte ScreenLookChildren = 4;
    private const byte ScreenLookActionTarget = 8; // version 27: [varuint target id >= 1], last

    private static void WriteScreenLook(ref WireWriter w, in ScreenEntry e)
    {
        bool flick = !e.FlickAppearance.IsNone;
        bool animation = e.Animation != null;
        bool children = e.VisChildren != null;
        if (e.ActionTarget < 0)
            throw Bad("a screen entry's action target is negative");
        bool target = e.ActionTarget > 0;
        bool slot = e.SlotId != 0 || e.HandIndex != 0;
        bool items = children && e.VisChildren.Items != null && Array.Exists(e.VisChildren.Items, c => c.IsItem || c.ItemSlots != 0 || c.EquipDelay || c.HasStorage);
        w.WriteByte((byte)((flick ? ScreenLookFlick : 0) | (animation ? ScreenLookAnimation : 0) | (children ? ScreenLookChildren : 0)
            | (target ? ScreenLookActionTarget : 0) | (slot ? ScreenLookSlot : 0) | (items ? ScreenLookItems : 0)));
        if (!flick && !animation)
        {
            if (children)
                WriteScreenChildren(ref w, e.VisChildren, items);
            if (target)
                w.WriteVarUInt((ulong)e.ActionTarget);
            WriteScreenInventory(ref w, e, slot);
            return;
        }
        long reference = 0;
        if (flick)
        {
            if (e.FlickTick < 0)
                throw Bad("a screen entry's flick starts before tick 0");
            reference = e.FlickTick;
        }
        if (animation && e.Animation.Tracks != null)
            foreach (AnimationTrack t in e.Animation.Tracks)
                if (t != null && t.StartTick > reference) reference = t.StartTick;
        w.WriteVarUInt((ulong)reference);
        if (flick)
        {
            if (e.FlickAppearance.Index > (uint)Limits.MaxAppearanceTableEntries)
                throw Bad("a screen entry's flick is outside its table");
            w.WriteVarUInt(((ulong)e.FlickAppearance.Index << 1) | (e.FlickAppearance.Private ? 1UL : 0UL));
            w.WriteVarUInt((ulong)(reference - e.FlickTick));
        }
        if (animation)
            WriteAnimation(ref w, e.Animation, reference);
        if (children)
            WriteScreenChildren(ref w, e.VisChildren, items);
        if (target)
            w.WriteVarUInt((ulong)e.ActionTarget);
        WriteScreenInventory(ref w, e, slot);
    }

    // Version 43: the inventory box's slot and hand (Codec.V43.cs), last.
    private static void WriteScreenInventory(ref WireWriter w, in ScreenEntry e, bool slot)
    {
        if (slot)
        {
            w.WriteVarUInt(e.SlotId);
            w.WriteByte(e.HandIndex);
        }
    }

    private static ScreenInventory ReadScreenInventory(ref WireReader r, byte look)
    {
        var inventory = new ScreenInventory();
        if ((look & ScreenLookSlot) != 0)
        {
            inventory.SlotId = ReadUInt32(ref r, "a screen entry's slot");
            inventory.HandIndex = r.ReadByte();
            if (inventory.SlotId == 0 && inventory.HandIndex == 0)
                throw Bad("a screen entry flags a slot and has none");
        }
        return inventory;
    }

    private static uint ReadUInt32(ref WireReader r, string what)
    {
        ulong v = r.ReadVarUInt();
        if (v > uint.MaxValue)
            throw Bad(what + " is over 32 bits");
        return (uint)v;
    }

    private struct ScreenInventory
    {
        public uint SlotId;
        public byte HandIndex;
    }

    private static void ReadScreenLook(ref WireReader r, out TableRef flick, out long flickTick, out EntityAnimation animation, out ScreenChildren children,
        out long actionTarget, out ScreenInventory inventory)
    {
        flick = TableRef.None;
        flickTick = 0;
        animation = null;
        children = null;
        actionTarget = 0;
        byte look = r.ReadByte();
        if ((look & ~(ScreenLookFlick | ScreenLookAnimation | ScreenLookChildren | ScreenLookActionTarget | ScreenLookSlot | ScreenLookItems)) != 0)
            throw Bad("a screen entry's look sets bits that are not defined");
        if ((look & ScreenLookItems) != 0 && (look & ScreenLookChildren) == 0)
            throw Bad("a screen entry's look flags its children's items and has no children");
        if ((look & (ScreenLookFlick | ScreenLookAnimation)) == 0)
        {
            if ((look & ScreenLookChildren) != 0)
                children = ReadScreenChildren(ref r, (look & ScreenLookItems) != 0);
            if ((look & ScreenLookActionTarget) != 0)
                actionTarget = ReadActionTarget(ref r);
            inventory = ReadScreenInventory(ref r, look);
            return;
        }
        long reference = ReadNonNegativeLong(ref r, "a screen entry's reference tick");
        if ((look & ScreenLookFlick) != 0)
        {
            flick = ReadPlainRef(ref r, true, Limits.MaxAppearanceTableEntries, "a screen entry's flick");
            if (flick.IsNone)
                throw Bad("a screen entry's flick without an appearance");
            ulong ago = r.ReadVarUInt();
            if (ago > (ulong)reference)
                throw Bad("a screen entry's flick starts before tick 0");
            flickTick = reference - (long)ago;
        }
        if ((look & ScreenLookAnimation) != 0)
        {
            animation = ReadAnimation(ref r, reference);
            if (animation == null)
                throw Bad("a screen entry's animation of 0 tracks");
        }
        if ((look & ScreenLookChildren) != 0)
            children = ReadScreenChildren(ref r, (look & ScreenLookItems) != 0);
        if ((look & ScreenLookActionTarget) != 0)
            actionTarget = ReadActionTarget(ref r);
        inventory = ReadScreenInventory(ref r, look);
    }

    private static long ReadActionTarget(ref WireReader r)
    {
        long id = ReadNonNegativeLong(ref r, "a screen entry's action target");
        if (id < 1)
            throw Bad("a screen entry's action target that is not positive");
        return id;
    }

    // The most bytes a screen entry's look takes (the encoder's size estimate).
    private static long MaxScreenLookBytes(in ScreenEntry e)
        => 1 + 10 + 10 + 5 + 1 + (e.FlickAppearance.IsNone ? 0 : 5 + 10) + (e.Animation == null ? 0 : MaxAnimationBytes(e.Animation)) + MaxScreenChildrenBytes(e.VisChildren);
}
