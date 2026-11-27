namespace Anomaly.Protocol;

// Protocol version 99 also (ui-overhaul.md section 12, slice 4B-1, DV692; docs/ai/specs/protocol.md section 98; the codec: Codec.V99Storages.cs).
// - Storages (server to client, State channel, after the tick's Delta, when it changed or a move was refused): the storages the own human carries,
//   in the Tab inventory's order (the back's, the belt's, a storage in the suit-storage slot, the pockets and the ID slot, then one held in a hand),
//   each with where it is carried, its name, tg's slot count, its room as tg counts it, whether tg keeps it locked, and its items in tg's order.
// - InventoryMove (client to server, an Action of kind 29): one item of the own inventory to a storage (a cell wished for or not), a worn slot, a
//   hand or the floor (ActionMessage.MoveItem, MoveTo, MoveStorage, MoveIndex, MessagesV2.cs).
// - InventoryOpen (client to server, an Action of kind 30): the Tab inventory was opened (tg's rustle of the back storage), no fields.

/// <summary>Version 99: where a carried storage is (<see cref="CarriedStorage.Place"/>), in the Tab inventory's order.</summary>
public enum StoragePlace : byte
{
    /// <summary>The back slot (tg's <c>ITEM_SLOT_BACK</c>): the backpack.</summary>
    Back = 1,

    /// <summary>The belt slot (<c>ITEM_SLOT_BELT</c>).</summary>
    Belt = 2,

    /// <summary>The suit-storage slot (<c>ITEM_SLOT_SUITSTORE</c>).</summary>
    SuitStorage = 3,

    /// <summary>The left pocket (<c>ITEM_SLOT_LPOCKET</c>).</summary>
    LeftPocket = 4,

    /// <summary>The right pocket (<c>ITEM_SLOT_RPOCKET</c>).</summary>
    RightPocket = 5,

    /// <summary>The ID slot (<c>ITEM_SLOT_ID</c>): a wallet.</summary>
    Id = 6,

    /// <summary>A hand (<see cref="CarriedStorage.Hand"/> says which: tg's held index).</summary>
    Hand = 7,
}

/// <summary>Version 99: what an inventory move puts its item into (<see cref="ActionMessage.MoveTo"/>).</summary>
public enum InventoryTarget : byte
{
    /// <summary>None: every action that is not an inventory move.</summary>
    None = 0,

    /// <summary>A carried storage (<see cref="ActionMessage.MoveStorage"/>), at a wished cell (<see cref="ActionMessage.MoveIndex"/>, 1 to <see cref="Limits.MaxStorageItems"/>) or 0 for none.</summary>
    Storage = 1,

    /// <summary>A worn slot (<see cref="ActionMessage.MoveIndex"/>: tg's slot id, one of <see cref="InventorySlots.Worn"/>).</summary>
    Slot = 2,

    /// <summary>A hand (<see cref="ActionMessage.MoveIndex"/>: tg's held index, 1 to <see cref="Limits.MaxStorageHands"/>).</summary>
    Hand = 3,

    /// <summary>The floor under the player (tg's drop).</summary>
    Floor = 4,
}

/// <summary>Version 99: tg's worn slot ids an inventory move may name (<c>__DEFINES/inventory.dm</c>'s <c>ITEM_SLOT_*</c> bits).</summary>
public static class InventorySlots
{
    /// <summary>
    /// The worn slots: the suit, the uniform, the gloves, the eyes, the ears, the mask, the head, the feet, the ID, the belt, the back (bits 0 to 10),
    /// the neck (bit 12), the suit storage and the two pockets (bits 14 to 16). Not the hands, the handcuffs, the legcuffs or a drone's storage.
    /// </summary>
    public const int Worn = 0x7FF | 0x1000 | 0x4000 | 0x8000 | 0x10000;

    /// <summary>True for one of the <see cref="Worn"/> slot ids (a single bit).</summary>
    public static bool IsWorn(long slot) => slot > 0 && (slot & (slot - 1)) == 0 && (slot & Worn) != 0;
}

/// <summary>Version 99: one item in a carried storage: its entity (the id a move names), its name, its picture (as a slot's item is drawn) and a stack's count (0 for an item that is no stack).</summary>
public readonly record struct StorageItem(long Entity, string Name, TableRef Appearance, int Count);

/// <summary>Version 99: one carried storage of a <see cref="StoragesMessage"/>.</summary>
public sealed class CarriedStorage : IEquatable<CarriedStorage>
{
    private static readonly StorageItem[] NoItems = Array.Empty<StorageItem>();

    /// <summary>The storage's item (the entity a move names as its target).</summary>
    public long Entity { get; init; }

    /// <summary>Where it is carried.</summary>
    public StoragePlace Place { get; init; }

    /// <summary>tg's held index of the hand holding it (1 to <see cref="Limits.MaxStorageHands"/>) when <see cref="Place"/> is a hand, else 0.</summary>
    public byte Hand { get; init; }

    /// <summary>The storage's name, never empty.</summary>
    public string Name { get; init; }

    /// <summary>tg's <c>max_slots</c> (at most <see cref="Limits.MaxStorageNumber"/>: the cap stands for that many or more).</summary>
    public int Slots { get; init; }

    /// <summary>tg's <c>get_total_weight()</c>: the summed weight classes of its contents (capped as <see cref="Slots"/>).</summary>
    public int Used { get; init; }

    /// <summary>tg's <c>max_total_storage</c> (capped as <see cref="Slots"/>).</summary>
    public int Limit { get; init; }

    /// <summary>tg's <c>locked</c>: a locked storage's items are not sent.</summary>
    public bool Locked { get; init; }

    /// <summary>tg's <c>display_contents</c> is off (tg never shows its contents to the living): its items are not sent.</summary>
    public bool Hidden { get; init; }

    /// <summary>It holds more than <see cref="Limits.MaxStorageItems"/> items: only the first ones in tg's order are sent.</summary>
    public bool Cut { get; init; }

    /// <summary>Its items in tg's order (the contents list), none when <see cref="Locked"/> or <see cref="Hidden"/>.</summary>
    public IReadOnlyList<StorageItem> Items { get; init; } = NoItems;

    public bool Equals(CarriedStorage other)
    {
        if (other is null || Entity != other.Entity || Place != other.Place || Hand != other.Hand || Slots != other.Slots || Used != other.Used
            || Limit != other.Limit || Locked != other.Locked || Hidden != other.Hidden || Cut != other.Cut
            || !string.Equals(Name, other.Name, StringComparison.Ordinal) || Items.Count != other.Items.Count)
            return false;
        for (int i = 0; i < Items.Count; i++)
        {
            StorageItem a = Items[i], b = other.Items[i];
            if (a.Entity != b.Entity || a.Appearance != b.Appearance || a.Count != b.Count || !string.Equals(a.Name, b.Name, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    public override bool Equals(object obj) => Equals(obj as CarriedStorage);

    public override int GetHashCode() => HashCode.Combine(Entity, Place, Hand, Used, Items.Count);

    public override string ToString()
        => $"{Place}{(Place == StoragePlace.Hand ? Hand.ToString(System.Globalization.CultureInfo.InvariantCulture) : "")} {Entity} \"{Name}\" {Used}/{Limit} in {Slots}{(Locked ? " locked" : "")}{(Hidden ? " hidden" : "")}{(Cut ? " cut" : "")} [{string.Join(", ", Array.ConvertAll(new List<StorageItem>(Items).ToArray(), i => i.Entity + ":" + i.Name + (i.Count > 0 ? "x" + i.Count : "")))}]";
}

/// <summary>
/// Version 99: the storages the own human carries (DV692), in the Tab inventory's order. Every mob that is not a human sends <see cref="None"/> (no
/// storages). <see cref="RefusedItem"/> is an event, not state: the move of that item the server took this tick did nothing (0: none).
/// </summary>
public sealed class StoragesMessage : IEquatable<StoragesMessage>
{
    private static readonly CarriedStorage[] NoStorages = Array.Empty<CarriedStorage>();

    /// <summary>No storages and nothing refused: what a client starts from.</summary>
    public static readonly StoragesMessage None = new();

    /// <summary>The carried storages, at most <see cref="Limits.MaxStorages"/>.</summary>
    public IReadOnlyList<CarriedStorage> Storages { get; init; } = NoStorages;

    /// <summary>The item whose move was refused this tick (an entity the client named), 0 for none.</summary>
    public long RefusedItem { get; init; }

    /// <summary>The storage of <paramref name="entity"/>, null when it is not listed.</summary>
    public CarriedStorage Find(long entity)
    {
        foreach (CarriedStorage s in Storages)
        {
            if (s.Entity == entity)
                return s;
        }
        return null;
    }

    /// <summary>The same state with no refusal (what the server compares to decide whether the state changed).</summary>
    public StoragesMessage WithoutRefusal() => RefusedItem == 0 ? this : new StoragesMessage { Storages = Storages };

    public bool Equals(StoragesMessage other)
    {
        if (other is null || RefusedItem != other.RefusedItem || Storages.Count != other.Storages.Count)
            return false;
        for (int i = 0; i < Storages.Count; i++)
        {
            if (!Storages[i].Equals(other.Storages[i]))
                return false;
        }
        return true;
    }

    public override bool Equals(object obj) => Equals(obj as StoragesMessage);

    public override int GetHashCode() => HashCode.Combine(Storages.Count, RefusedItem);

    public override string ToString() => $"Storages([{string.Join("; ", Storages)}], refused {RefusedItem})";
}
