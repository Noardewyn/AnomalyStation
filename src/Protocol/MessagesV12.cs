namespace Anomaly.Protocol;

// Protocol version 12 (M4a, the screen objects' vis_contents children: docs/ai/specs/protocol.md section 21; the field: ScreenEntry in MessagesV2.cs).

/// <summary>
/// One clickable <c>vis_contents</c> child of a screen object (version 12): BYOND makes every object in an atom's <c>vis_contents</c> a click
/// target of its own unless its <c>vis_flags</c> has <c>VIS_INHERIT_ID</c> (BYOND ref <c>/atom/var/vis_flags</c>); tg draws the item in a hand
/// or an inventory slot that way (<c>inventory_slot.dm:55</c>, <c>update_slot_icon_contents</c>: <c>slot.vis_contents += slot_item</c>), and a
/// click on it is the item's own <c>Click</c>.
/// </summary>
/// <param name="Id">The child's world id (<c>dm_id</c>): the target of a <see cref="TargetKind.Screen"/> click or hover on its pixels.</param>
/// <param name="Appearance">The child drawn alone, as it is flattened into the holder's appearance (the same table row as its overlay there; private or public, never none).</param>
/// <param name="MouseOpaque">BYOND's <c>mouse_opacity</c> 2 (<c>MOUSE_OPACITY_OPAQUE</c>): the child's whole box takes the click, not only its opaque pixels.</param>
public readonly record struct ScreenChild(long Id, TableRef Appearance, bool MouseOpaque = false)
{
    // ---- protocol version 43 (M10 slice E: docs/ai/specs/protocol.md section 52) ----

    /// <summary>Version 43: the child is an item (<c>/obj/item</c>: the item held in a hand or worn in a slot).</summary>
    public bool IsItem { get; init; }

    /// <summary>Version 43: an item's <c>slot_flags</c> (the <c>ITEM_SLOT_*</c> bits it may be worn in); 0 for no item.</summary>
    public uint ItemSlots { get; init; }

    /// <summary>Version 43: an item with an <c>equip_delay_self</c> (putting it on takes a do_after).</summary>
    public bool EquipDelay { get; init; }

    /// <summary>Version 43: an item with a storage (<c>atom_storage</c>: a hand on it worn or held opens it rather than taking it).</summary>
    public bool HasStorage { get; init; }
}

/// <summary>
/// Version 12: the clickable <c>vis_contents</c> children of a screen entry (<see cref="ScreenEntry.VisChildren"/>), bottom to top in BYOND's
/// draw order; the client hit-tests them topmost first before the holder. Compared by value (a changed list is a changed entry).
/// </summary>
public sealed class ScreenChildren : IEquatable<ScreenChildren>
{
    /// <summary>The children, bottom to top (1 to <see cref="Limits.MaxScreenChildren"/> on the wire).</summary>
    public ScreenChild[] Items { get; init; } = Array.Empty<ScreenChild>();

    /// <summary>True when <paramref name="id"/> is one of the children.</summary>
    public bool Contains(long id)
    {
        foreach (ScreenChild c in Items)
            if (c.Id == id) return true;
        return false;
    }

    public bool Equals(ScreenChildren other)
    {
        if (other == null || other.Items.Length != Items.Length) return false;
        for (int i = 0; i < Items.Length; i++)
            if (!Items[i].Equals(other.Items[i])) return false;
        return true;
    }

    public override bool Equals(object obj) => Equals(obj as ScreenChildren);

    public override int GetHashCode() => Items.Length == 0 ? 0 : HashCode.Combine(Items.Length, Items[0].Id);

    /// <summary>True when both are null or empty, or equal.</summary>
    public static bool Same(ScreenChildren a, ScreenChildren b)
        => (a == null || a.Items.Length == 0) ? (b == null || b.Items.Length == 0) : a.Equals(b);
}
