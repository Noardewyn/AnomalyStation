namespace Anomaly.Protocol;

// Version 100 also (MessagesV100Storages.cs, docs/ai/specs/protocol.md section 100; ui-overhaul.md section 12, slice 4B-1, DV696):
//
//   Storages (server to client, State): [u8 id = 35] [varuint count 0..9] then each entry, in the order of their places (and hands):
//     [varuint entity >= 1] [u8 place 1..8] ([u8 hand 1..8] when the place is 7, a hand) [str(1..128) name]
//     [varuint slots 0..65535] [varuint used 0..65535] [varuint limit 0..65535] [u8 flags: bit 0 locked, bit 1 hidden, bit 2 cut; bits 3-7 zero]
//     [varuint item count 0..64] then each item: [varuint entity >= 1] [str(1..128) name] [tableref picture (may be none)] [varuint count 0..65535]
//   then [varuint refused item, 0 for none].
//   a locked or hidden storage has no items; cut only with 64 items; every entity (storage or item) once, but the other container's own entity may
//   also be an item of a carried storage (a box opened inside the backpack); the places strictly in order, a place other than a hand once, hands by
//   increasing index; at most 8 carried storages (places 1 to 7) and one other container (place 8, so last), which is neither locked nor hidden.
//   A tableref is a varuint (appearance index << 1) | private, as everywhere (Codec.V2).
//
//   InventoryMove (Action kind 29, client to server, Control): [varuint item >= 1] [u8 target 1..4] then by target:
//     1 storage: [varuint storage >= 1] [u8 cell 0..64, 0 for none]; 2 slot: [varuint slot id, one of InventorySlots.Worn];
//     3 hand: [u8 held index 1..8]; 4 floor: nothing (Codec.V2's action).
//   InventoryOpen (Action kind 30): nothing.
//
// Every rule of the earlier codecs holds: TryDecode never throws, and a message that decodes encodes back to the same bytes. Whether the
// appearances exist is the replica's check; whether the entities are the sender's is the server's.

public static partial class Codec
{
    private const byte StorageFlagLocked = 1;
    private const byte StorageFlagHidden = 2;
    private const byte StorageFlagCut = 4;

    /// <summary>Encodes a <see cref="StoragesMessage"/> for the State channel (at most <see cref="Limits.MaxStoragesBytes"/>).</summary>
    public static byte[] Encode(StoragesMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return EncodeToArray(Limits.MaxStoragesBytes, buffer =>
        {
            var w = new WireWriter(buffer);
            w.WriteMessageId(MessageId.Storages);
            IReadOnlyList<CarriedStorage> storages = message.Storages ?? throw Bad("a storages message without its list");
            w.WriteCount(storages.Count, Limits.MaxStorageEntries);
            var seen = new HashSet<long>();
            var storageEntities = new HashSet<long>();
            int lastOrder = 0;
            foreach (CarriedStorage s in storages)
            {
                if (s == null)
                    throw Bad("a null carried storage");
                CheckStorageHeader(s.Entity, s.Place, s.Hand, s.Name, s.Slots, s.Used, s.Limit, s.Locked, s.Hidden, s.Cut, s.Items?.Count ?? -1, seen, storageEntities, ref lastOrder);
                w.WriteVarUInt((ulong)s.Entity);
                w.WriteByte((byte)s.Place);
                if (s.Place == StoragePlace.Hand)
                    w.WriteByte(s.Hand);
                w.WriteString(s.Name, Limits.MaxStorageNameBytes);
                w.WriteVarUInt((ulong)s.Slots);
                w.WriteVarUInt((ulong)s.Used);
                w.WriteVarUInt((ulong)s.Limit);
                w.WriteByte((byte)((s.Locked ? StorageFlagLocked : 0) | (s.Hidden ? StorageFlagHidden : 0) | (s.Cut ? StorageFlagCut : 0)));
                w.WriteCount(s.Items.Count, Limits.MaxStorageItems);
                foreach (StorageItem item in s.Items)
                {
                    CheckStorageItem(item.Entity, item.Name, item.Count, seen);
                    w.WriteVarUInt((ulong)item.Entity);
                    w.WriteString(item.Name, Limits.MaxStorageNameBytes);
                    WritePlainRef(ref w, item.Appearance, true, Limits.MaxAppearanceTableEntries, "a stored item's picture");
                    w.WriteVarUInt((ulong)item.Count);
                }
            }
            if (message.RefusedItem < 0)
                throw Bad("a negative refused item");
            w.WriteVarUInt((ulong)message.RefusedItem);
            return Finish(ref w, Limits.MaxStoragesBytes);
        });
    }

    /// <summary>
    /// Decodes a <see cref="StoragesMessage"/>: at most eight carried storages and then one other container in the order of their places, each named,
    /// with numbers within the cap, known flags and at most 64 items (none when locked or hidden), every entity once, and the refused item.
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> message, out StoragesMessage value, out ProtocolError error)
    {
        value = null;
        if (!Begin(message, MessageId.Storages, Limits.MaxStoragesBytes, out error))
            return false;
        try
        {
            var r = new WireReader(message);
            r.ReadMessageId();
            int count = r.ReadCount(Limits.MaxStorageEntries, 1);
            var storages = new CarriedStorage[count];
            var seen = new HashSet<long>();
            var storageEntities = new HashSet<long>();
            int lastOrder = 0;
            for (int i = 0; i < count; i++)
            {
                long entity = ReadNonNegativeLong(ref r, "a carried storage's entity");
                byte placeByte = r.ReadByte();
                if (placeByte < (byte)StoragePlace.Back || placeByte > (byte)StoragePlace.Other)
                    throw Bad("a carried storage of an unknown place " + placeByte);
                var place = (StoragePlace)placeByte;
                byte hand = place == StoragePlace.Hand ? r.ReadByte() : (byte)0;
                string name = r.ReadString(Limits.MaxStorageNameBytes);
                int slots = ReadStorageNumber(ref r, "a storage's slots");
                int used = ReadStorageNumber(ref r, "a storage's room");
                int limit = ReadStorageNumber(ref r, "a storage's limit");
                byte flags = r.ReadByte();
                if ((flags & ~(StorageFlagLocked | StorageFlagHidden | StorageFlagCut)) != 0)
                    throw Bad("a carried storage with unknown flag bits");
                bool locked = (flags & StorageFlagLocked) != 0, hidden = (flags & StorageFlagHidden) != 0, cut = (flags & StorageFlagCut) != 0;
                int itemCount = r.ReadCount(Limits.MaxStorageItems, 4);
                CheckStorageHeader(entity, place, hand, name, slots, used, limit, locked, hidden, cut, itemCount, seen, storageEntities, ref lastOrder);
                var items = new StorageItem[itemCount];
                for (int k = 0; k < itemCount; k++)
                {
                    long itemEntity = ReadNonNegativeLong(ref r, "a stored item's entity");
                    string itemName = r.ReadString(Limits.MaxStorageNameBytes);
                    TableRef picture = ReadPlainRef(ref r, true, Limits.MaxAppearanceTableEntries, "a stored item's picture");
                    int itemCountValue = ReadStorageNumber(ref r, "a stored item's count");
                    CheckStorageItem(itemEntity, itemName, itemCountValue, seen);
                    items[k] = new StorageItem(itemEntity, itemName, picture, itemCountValue);
                }
                storages[i] = new CarriedStorage
                {
                    Entity = entity, Place = place, Hand = hand, Name = name, Slots = slots, Used = used, Limit = limit,
                    Locked = locked, Hidden = hidden, Cut = cut, Items = items,
                };
            }
            long refused = ReadNonNegativeLong(ref r, "a storages message's refused item");
            r.ExpectEnd();
            value = new StoragesMessage { Storages = storages, RefusedItem = refused };
            return true;
        }
        catch (ProtocolException e)
        {
            value = null;
            error = e.Error;
            return false;
        }
    }

    private static int ReadStorageNumber(ref WireReader r, string what)
    {
        ulong value = r.ReadVarUInt();
        if (value > Limits.MaxStorageNumber)
            throw Bad(what + " is over " + Limits.MaxStorageNumber);
        return (int)value;
    }

    // One entry's header: a positive entity seen once (the other container's may also be an item of a carried storage, never another entry's), a known
    // place after the last one (a hand by its index; the other container, place 8, so last and once), at most 8 carried storages, a name, numbers
    // within the cap, and items only when the storage shows them (cut only when full; the other container is never locked or hidden).
    private static void CheckStorageHeader(long entity, StoragePlace place, byte hand, string name, int slots, int used, int limit, bool locked, bool hidden,
        bool cut, int itemCount, HashSet<long> seen, HashSet<long> storageEntities, ref int lastOrder)
    {
        if (entity < 1 || !storageEntities.Add(entity) || (!seen.Add(entity) && place != StoragePlace.Other))
            throw Bad("a carried storage's entity that is not positive or is named twice");
        if (place < StoragePlace.Back || place > StoragePlace.Other)
            throw Bad("a carried storage of an unknown place " + (byte)place);
        if (place != StoragePlace.Other && storageEntities.Count > Limits.MaxStorages)
            throw Bad("more than " + Limits.MaxStorages + " carried storages");
        if (place == StoragePlace.Other && (locked || hidden))
            throw Bad("a locked or hidden other container");
        if (place == StoragePlace.Hand ? hand < 1 || hand > Limits.MaxStorageHands : hand != 0)
            throw Bad("a carried storage's hand " + hand + " for its place");
        int order = (int)place * (Limits.MaxStorageHands + 1) + hand;
        if (order <= lastOrder)
            throw Bad("a carried storage out of the places' order or at a place taken");
        lastOrder = order;
        if (string.IsNullOrEmpty(name))
            throw Bad("a carried storage without a name");
        if (slots < 0 || slots > Limits.MaxStorageNumber || used < 0 || used > Limits.MaxStorageNumber || limit < 0 || limit > Limits.MaxStorageNumber)
            throw Bad("a carried storage's number out of range");
        if (itemCount < 0 || itemCount > Limits.MaxStorageItems)
            throw Bad("a carried storage's items over the cap or none");
        if ((locked || hidden) && itemCount != 0)
            throw Bad("a locked or hidden storage's items");
        if (cut && itemCount != Limits.MaxStorageItems)
            throw Bad("a cut storage that is not full");
    }

    private static void CheckStorageItem(long entity, string name, int count, HashSet<long> seen)
    {
        if (entity < 1 || !seen.Add(entity))
            throw Bad("a stored item's entity that is not positive or is named twice");
        if (string.IsNullOrEmpty(name))
            throw Bad("a stored item without a name");
        if (count < 0 || count > Limits.MaxStorageNumber)
            throw Bad("a stored item's count out of range");
    }

    // An InventoryMove's fields after the kind (Codec.V2's Encode).
    private static void WriteInventoryMove(ref WireWriter w, in ActionMessage message)
    {
        if (message.MoveItem < 1)
            throw Bad("an inventory move without an item");
        CheckInventoryMoveTarget(message.MoveTo, message.MoveStorage, message.MoveIndex);
        w.WriteVarUInt((ulong)message.MoveItem);
        w.WriteByte((byte)message.MoveTo);
        switch (message.MoveTo)
        {
            case InventoryTarget.Storage:
                w.WriteVarUInt((ulong)message.MoveStorage);
                w.WriteByte((byte)message.MoveIndex);
                break;
            case InventoryTarget.Slot:
                w.WriteVarUInt((ulong)message.MoveIndex);
                break;
            case InventoryTarget.Hand:
                w.WriteByte((byte)message.MoveIndex);
                break;
        }
    }

    // An InventoryMove's fields after the kind (Codec.V2's TryDecode).
    private static void ReadInventoryMove(ref WireReader r, out long item, out InventoryTarget to, out long storage, out int index)
    {
        item = ReadNonNegativeLong(ref r, "an inventory move's item");
        if (item < 1)
            throw Bad("an inventory move without an item");
        byte toByte = r.ReadByte();
        to = (InventoryTarget)toByte;
        storage = 0;
        index = 0;
        switch (to)
        {
            case InventoryTarget.Storage:
                storage = ReadNonNegativeLong(ref r, "an inventory move's storage");
                index = r.ReadByte();
                break;
            case InventoryTarget.Slot:
                ulong slot = r.ReadVarUInt();
                index = slot > int.MaxValue ? -1 : (int)slot;
                break;
            case InventoryTarget.Hand:
                index = r.ReadByte();
                break;
            case InventoryTarget.Floor:
                break;
            default:
                throw Bad("an inventory move to an unknown target " + toByte);
        }
        CheckInventoryMoveTarget(to, storage, index);
    }

    /// <summary>Whether an inventory move's target fields are in range (the codec's and the server's check, <c>ValidAction</c>).</summary>
    public static bool IsValidInventoryMoveTarget(InventoryTarget to, long storage, int index) => to switch
    {
        InventoryTarget.Storage => storage >= 1 && index >= 0 && index <= Limits.MaxStorageItems,
        InventoryTarget.Slot => storage == 0 && InventorySlots.IsWorn(index),
        InventoryTarget.Hand => storage == 0 && index >= 1 && index <= Limits.MaxStorageHands,
        InventoryTarget.Floor => storage == 0 && index == 0,
        _ => false,
    };

    private static void CheckInventoryMoveTarget(InventoryTarget to, long storage, int index)
    {
        if (!IsValidInventoryMoveTarget(to, storage, index))
            throw Bad("an inventory move's target out of range (" + to + ", " + storage + ", " + index + ")");
    }
}
