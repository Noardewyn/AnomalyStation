namespace Anomaly.Protocol;

// Protocol version 12 (M4a): a screen entry's clickable vis_contents children (docs/ai/specs/protocol.md section 21; the field:
// ScreenEntry.VisChildren in MessagesV2.cs, the types in MessagesV12.cs).
public static partial class Codec
{
    // The last part of a screen entry's look when its bit 2 is set: [count 1..MaxScreenChildren] then, bottom to top,
    // { [varuint id >= 1] [varuint (appearance index << 2) | (mouse opaque << 1) | private] } (the appearance never none).
    private const int MinScreenChildBytes = 2;

    private static void WriteScreenChildren(ref WireWriter w, ScreenChildren children, bool items)
    {
        ScreenChild[] list = children.Items ?? Array.Empty<ScreenChild>();
        if (list.Length == 0)
            throw Bad("a screen entry's vis children list of 0 children");
        w.WriteCount(list.Length, Limits.MaxScreenChildren);
        foreach (ScreenChild c in list)
        {
            if (c.Id < 1)
                throw Bad("a screen entry's vis child with an id that is not positive");
            if (c.Appearance.IsNone || c.Appearance.Index > (uint)Limits.MaxAppearanceTableEntries)
                throw Bad("a screen entry's vis child names no appearance of its table");
            w.WriteVarUInt((ulong)c.Id);
            w.WriteVarUInt(((ulong)c.Appearance.Index << 2) | (c.MouseOpaque ? 2UL : 0UL) | (c.Appearance.Private ? 1UL : 0UL));
            if (!c.IsItem && (c.ItemSlots != 0 || c.EquipDelay || c.HasStorage))
                throw Bad("a screen entry's vis child that is no item has an item's slots");
            if (items) // version 43: [varuint slot_flags << 3 | storage << 2 | delay << 1 | item]
                w.WriteVarUInt(((ulong)c.ItemSlots << 3) | (c.HasStorage ? 4UL : 0UL) | (c.EquipDelay ? 2UL : 0UL) | (c.IsItem ? 1UL : 0UL));
        }
    }

    private static ScreenChildren ReadScreenChildren(ref WireReader r, bool items)
    {
        int count = r.ReadCount(Limits.MaxScreenChildren, MinScreenChildBytes);
        if (count == 0)
            throw Bad("a screen entry's vis children list of 0 children");
        var children = new ScreenChild[count];
        for (int i = 0; i < count; i++)
        {
            long id = ReadNonNegativeLong(ref r, "a screen entry's vis child");
            if (id < 1)
                throw Bad("a screen entry's vis child with an id that is not positive");
            ulong look = r.ReadVarUInt();
            ulong index = look >> 2;
            if (index == 0 || index > (ulong)Limits.MaxAppearanceTableEntries)
                throw Bad("a screen entry's vis child names no appearance of its table");
            var child = new ScreenChild(id, new TableRef((uint)index, (look & 1) != 0), (look & 2) != 0);
            if (items)
            {
                ulong item = r.ReadVarUInt();
                if (item >> 3 > uint.MaxValue || ((item & 1) == 0 && item != 0))
                    throw Bad("a screen entry's vis child has an item's slots and is no item, or slots over 32 bits");
                child = child with { IsItem = (item & 1) != 0, EquipDelay = (item & 2) != 0, HasStorage = (item & 4) != 0, ItemSlots = (uint)(item >> 3) };
            }
            children[i] = child;
        }
        return new ScreenChildren { Items = children };
    }

    // The most bytes an entry's children take (the encoder's size estimate).
    private static long MaxScreenChildrenBytes(ScreenChildren children)
        => children == null ? 0 : 5 + (children.Items?.Length ?? 0) * (10L + 10 + 7);
}
