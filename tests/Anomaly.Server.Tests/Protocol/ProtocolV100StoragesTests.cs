using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 100 also (ui-overhaul.md section 12, slice 4B-1, DV696; MessagesV100Storages.cs, Codec.V100Storages.cs): the Storages message, the own
/// human's carried storages and their items, every bound refused at both ends; the InventoryMove action, an item and a target in range and nothing else;
/// the InventoryOpen action, no fields.
/// </summary>
public class ProtocolV100StoragesTests
{
    private static StorageItem Item(long entity, string name = "wrench", int count = 0) => new(entity, name, new TableRef((uint)entity, true), count);

    private static StoragesMessage Sample(long refused = 0) => new()
    {
        Storages = new[]
        {
            new CarriedStorage
            {
                Entity = 100, Place = StoragePlace.Back, Name = "backpack", Slots = 21, Used = 5, Limit = 21,
                Items = new[] { Item(101), Item(102, "cable coil", 30), new StorageItem(103, "nothing drawn", TableRef.None, 0) },
            },
            new CarriedStorage { Entity = 200, Place = StoragePlace.Belt, Name = "toolbelt", Slots = 7, Used = 0, Limit = 14 },
            new CarriedStorage { Entity = 300, Place = StoragePlace.LeftPocket, Name = "lockbox", Slots = 4, Limit = 8, Locked = true },
            new CarriedStorage { Entity = 400, Place = StoragePlace.Id, Name = "cigarette packet", Slots = 6, Limit = 6, Hidden = true },
            new CarriedStorage { Entity = 500, Place = StoragePlace.Hand, Hand = 2, Name = "box", Slots = 7, Used = 2, Limit = 14, Items = new[] { Item(501, "gauze") } },
        },
        RefusedItem = refused,
    };

    private static StoragesMessage RoundTrip(StoragesMessage message)
    {
        byte[] bytes = Codec.Encode(message);
        Assert.True(bytes.Length <= Limits.MaxStoragesBytes);
        Assert.True(Codec.TryDecode(bytes, out StoragesMessage read, out ProtocolError error), error.ToString());
        Assert.Equal(bytes, Codec.Encode(read));
        return read;
    }

    private static StoragesMessage With(CarriedStorage storage) => new() { Storages = new[] { storage } };

    private static CarriedStorage Bag(int items, string name = "backpack", bool cut = false) => new()
    {
        Entity = 1, Place = StoragePlace.Back, Name = name, Slots = items, Used = items, Limit = items, Cut = cut,
        Items = Enumerable.Range(0, items).Select(i => Item(10 + i)).ToArray(),
    };

    [Fact]
    public void TheStoragesTheirItemsAndARefusalRoundTrip()
    {
        StoragesMessage sample = Sample(refused: 102);
        StoragesMessage read = RoundTrip(sample);
        Assert.Equal(sample, read);
        Assert.Equal(5, read.Storages.Count);
        Assert.Equal("cable coil", read.Storages[0].Items[1].Name);
        Assert.Equal(30, read.Storages[0].Items[1].Count);
        Assert.True(read.Storages[0].Items[2].Appearance.IsNone);
        Assert.True(read.Storages[2].Locked);
        Assert.True(read.Storages[3].Hidden);
        Assert.Equal((StoragePlace.Hand, (byte)2), (read.Storages[4].Place, read.Storages[4].Hand));
        Assert.Same(read.Storages[4], read.Find(500));
        Assert.Null(read.Find(501)); // an item is no storage
        Assert.Equal(102, read.RefusedItem);
        Assert.Equal(Sample(), read.WithoutRefusal());
        Assert.NotEqual(Sample(), read);
        Assert.Equal(StoragesMessage.None, RoundTrip(StoragesMessage.None));
        Assert.Equal(new byte[] { 35, 0, 0 }, Codec.Encode(StoragesMessage.None)); // protocol.md section 100
        Assert.Equal(MessageId.Storages, (MessageId)Codec.Encode(sample)[0]);
    }

    [Fact]
    public void EveryReplicaTakesTheStoragesAndCountsEachRefusalOnce()
    {
        var replica = new ReplicaWorld();
        Assert.True(replica.TryApply(Codec.Encode(StoragesMessage.None), out MessageId id, out ProtocolError error), error.ToString());
        Assert.Equal(MessageId.Storages, id);
        Assert.True(replica.TryApply(Codec.Encode(Sample(refused: 501)), out _, out error), error.ToString());
        Assert.Equal(Sample(), replica.Storages);
        Assert.Equal((1, 501L), (replica.StoragesRefusals, replica.StoragesRefusedItem));
        Assert.True(replica.TryApply(Codec.Encode(Sample()), out _, out _));
        Assert.Equal(1, replica.StoragesRefusals); // a state message is no refusal
    }

    [Fact]
    public void AFullMessageAtEveryCapFitsItsBound()
    {
        string longest = new('x', Limits.MaxStorageNameBytes);
        var storages = new CarriedStorage[Limits.MaxStorages];
        for (int s = 0; s < storages.Length; s++)
        {
            storages[s] = new CarriedStorage
            {
                Entity = long.MaxValue - s * 1000, Place = s < 6 ? (StoragePlace)(s + 1) : StoragePlace.Hand, Hand = (byte)(s < 6 ? 0 : Limits.MaxStorageHands - 7 + s),
                Name = longest, Slots = Limits.MaxStorageNumber, Used = Limits.MaxStorageNumber, Limit = Limits.MaxStorageNumber, Cut = true,
                Items = Enumerable.Range(1, Limits.MaxStorageItems).Select(i => new StorageItem(long.MaxValue - s * 1000 - i, longest,
                    new TableRef((uint)Limits.MaxAppearanceTableEntries, true), Limits.MaxStorageNumber)).ToArray(),
            };
        }
        var widest = new StoragesMessage { Storages = storages, RefusedItem = long.MaxValue };
        StoragesMessage read = RoundTrip(widest);
        Assert.Equal(widest, read);
        Assert.True(Codec.Encode(widest).Length <= Limits.MaxStoragesBytes);
    }

    [Fact]
    public void EveryBoundIsRefusedByTheEncoder()
    {
        // too many storages, too many items, a long or empty name, a place out of order or taken, a bad hand, numbers over the cap
        var nine = new CarriedStorage[Limits.MaxStorages + 1];
        for (int i = 0; i < nine.Length; i++)
            nine[i] = new CarriedStorage { Entity = i + 1, Place = StoragePlace.Hand, Hand = (byte)(i + 1), Name = "box" };
        Assert.Throws<ProtocolException>(() => Codec.Encode(new StoragesMessage { Storages = nine }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(Bag(Limits.MaxStorageItems + 1))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(Bag(1, name: new string('x', Limits.MaxStorageNameBytes + 1)))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(Bag(1, name: ""))));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(Bag(1, cut: true)))); // cut but not full
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new CarriedStorage { Entity = 1, Place = StoragePlace.Back, Name = "bag", Items = new[] { Item(2, "") } })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new CarriedStorage { Entity = 1, Place = StoragePlace.Back, Name = "bag", Items = new[] { Item(2, new string('x', Limits.MaxStorageNameBytes + 1)) } })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new CarriedStorage { Entity = 1, Place = (StoragePlace)8, Name = "bag" })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new CarriedStorage { Entity = 1, Place = 0, Name = "bag" })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new CarriedStorage { Entity = 1, Place = StoragePlace.Hand, Hand = 0, Name = "bag" })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new CarriedStorage { Entity = 1, Place = StoragePlace.Hand, Hand = Limits.MaxStorageHands + 1, Name = "bag" })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new CarriedStorage { Entity = 1, Place = StoragePlace.Back, Hand = 1, Name = "bag" })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new CarriedStorage { Entity = 1, Place = StoragePlace.Back, Name = "bag", Slots = Limits.MaxStorageNumber + 1 })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new CarriedStorage { Entity = 1, Place = StoragePlace.Back, Name = "bag", Used = -1 })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new CarriedStorage { Entity = 1, Place = StoragePlace.Back, Name = "bag", Items = new[] { Item(2, count: Limits.MaxStorageNumber + 1) } })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new CarriedStorage { Entity = 0, Place = StoragePlace.Back, Name = "bag" })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new CarriedStorage { Entity = 1, Place = StoragePlace.Back, Name = "bag", Locked = true, Items = new[] { Item(2) } })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new CarriedStorage { Entity = 1, Place = StoragePlace.Back, Name = "bag", Hidden = true, Items = new[] { Item(2) } })));
        Assert.Throws<ProtocolException>(() => Codec.Encode(With(new CarriedStorage { Entity = 1, Place = StoragePlace.Back, Name = "bag", Items = new[] { Item(1) } }))); // an entity twice
        var outOfOrder = new StoragesMessage
        {
            Storages = new[] { new CarriedStorage { Entity = 1, Place = StoragePlace.Belt, Name = "belt" }, new CarriedStorage { Entity = 2, Place = StoragePlace.Back, Name = "bag" } },
        };
        Assert.Throws<ProtocolException>(() => Codec.Encode(outOfOrder));
        var twice = new StoragesMessage
        {
            Storages = new[] { new CarriedStorage { Entity = 1, Place = StoragePlace.Back, Name = "a" }, new CarriedStorage { Entity = 2, Place = StoragePlace.Back, Name = "b" } },
        };
        Assert.Throws<ProtocolException>(() => Codec.Encode(twice));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new StoragesMessage { RefusedItem = -1 }));
    }

    [Fact]
    public void EveryBoundIsRefusedByTheDecoder()
    {
        // [35] [count 1] [entity 1] [place 1] [name: len+1, bytes] [slots] [used] [limit] [flags] [item count] ... [refused]
        StoragesMessage one = With(new CarriedStorage { Entity = 1, Place = StoragePlace.Back, Name = "bag", Slots = 7, Used = 1, Limit = 14, Items = new[] { Item(2, "pen") } });
        byte[] bytes = Codec.Encode(one);
        Assert.Equal(new byte[] { 35, 1, 1, 1, 4, (byte)'b', (byte)'a', (byte)'g', 7, 1, 14, 0, 1, 2, 4, (byte)'p', (byte)'e', (byte)'n', 5, 0, 0 }, bytes);
        Assert.True(Codec.TryDecode(bytes, out StoragesMessage _, out _));
        void Refused(int at, byte value, string why)
        {
            byte[] bad = (byte[])bytes.Clone();
            bad[at] = value;
            Assert.False(Codec.TryDecode(bad, out StoragesMessage _, out _), why);
        }
        Refused(1, (byte)(Limits.MaxStorages + 1), "too many storages");
        Refused(2, 0, "a storage entity of 0");
        Refused(3, 0, "a place of 0");
        Refused(3, 8, "an unknown place");
        Refused(4, 1, "an empty name");
        Refused(11, 8, "an unknown flag bit");
        Refused(11, 1, "a locked storage with items");
        Refused(11, 2, "a hidden storage with items");
        Refused(11, 4, "cut with one item");
        Refused(12, (byte)(Limits.MaxStorageItems + 1), "too many items");
        Refused(13, 1, "an item named twice (the storage's own entity)");
        Refused(14, 1, "an item without a name");
        // a number over the cap: slots of 65536 (varuint 80 80 04)
        byte[] big = bytes[..8].Concat(new byte[] { 0x80, 0x80, 0x04 }).Concat(bytes[9..]).ToArray();
        Assert.False(Codec.TryDecode(big, out StoragesMessage _, out ProtocolError error));
        Assert.Equal(ProtocolError.BadValue, error);
        // an over-long name
        StoragesMessage longName = With(new CarriedStorage { Entity = 1, Place = StoragePlace.Back, Name = new string('x', Limits.MaxStorageNameBytes) });
        byte[] longBytes = Codec.Encode(longName);
        byte[] over = longBytes[..4].Concat(new byte[] { 0x82, 0x01 }).Concat(Enumerable.Repeat((byte)'x', Limits.MaxStorageNameBytes + 1)).Concat(longBytes[(6 + Limits.MaxStorageNameBytes)..]).ToArray();
        Assert.False(Codec.TryDecode(over, out StoragesMessage _, out error));
        Assert.Equal(ProtocolError.BadString, error);
        // the old shape: the version-100 hotbar's id-less sibling with no refused item at the end, a truncation, a trailing byte
        Assert.False(Codec.TryDecode(bytes[..^1], out StoragesMessage _, out error));
        Assert.Equal(ProtocolError.Truncated, error);
        Assert.False(Codec.TryDecode(bytes.Append((byte)0).ToArray(), out StoragesMessage _, out error));
        Assert.Equal(ProtocolError.TrailingBytes, error);
        for (int length = 0; length < bytes.Length; length++)
            Assert.False(Codec.TryDecode(bytes.AsSpan(0, length), out StoragesMessage _, out _), "cut at " + length);
        Assert.False(Codec.TryDecode(Codec.Encode(HotbarMessage.None), out StoragesMessage _, out error));
        Assert.Equal(ProtocolError.WrongMessage, error);
    }

    private static ActionMessage Move(long item, InventoryTarget to, long storage = 0, int index = 0)
        => new(9, ActionKind.InventoryMove, 0, ClickButton.None, ClickModifiers.None, null) { MoveItem = item, MoveTo = to, MoveStorage = storage, MoveIndex = index };

    [Fact]
    public void AnInventoryMoveCarriesItsItemAndTargetAndNothingElse()
    {
        var good = new[]
        {
            Move(5, InventoryTarget.Storage, 100), Move(5, InventoryTarget.Storage, 100, 1), Move(long.MaxValue, InventoryTarget.Storage, long.MaxValue, Limits.MaxStorageItems),
            Move(5, InventoryTarget.Slot, index: 1), Move(5, InventoryTarget.Slot, index: 0x10000), Move(5, InventoryTarget.Slot, index: 0x1000),
            Move(5, InventoryTarget.Hand, index: 1), Move(5, InventoryTarget.Hand, index: Limits.MaxStorageHands), Move(5, InventoryTarget.Floor),
        };
        foreach (ActionMessage move in good)
        {
            byte[] encoded = Codec.Encode(move);
            Assert.True(Codec.TryDecode(encoded, out ActionMessage back, out ProtocolError error), move + ": " + error);
            Assert.Equal(move, back);
        }
        Assert.Equal(new byte[] { 10, 9, 29, 5, 4 }, Codec.Encode(Move(5, InventoryTarget.Floor))); // protocol.md section 100
        Assert.Equal(new byte[] { 10, 9, 29, 5, 1, 100, 3 }, Codec.Encode(Move(5, InventoryTarget.Storage, 100, 3)));
        // every field at a bad value
        var bad = new[]
        {
            Move(0, InventoryTarget.Floor), Move(-1, InventoryTarget.Floor), Move(5, InventoryTarget.None), Move(5, (InventoryTarget)5),
            Move(5, InventoryTarget.Storage), Move(5, InventoryTarget.Storage, 100, Limits.MaxStorageItems + 1), Move(5, InventoryTarget.Storage, 100, -1),
            Move(5, InventoryTarget.Slot), Move(5, InventoryTarget.Slot, index: 3), Move(5, InventoryTarget.Slot, index: 0x2000), Move(5, InventoryTarget.Slot, index: 0x800),
            Move(5, InventoryTarget.Slot, index: 0x20000), Move(5, InventoryTarget.Slot, 100, 1),
            Move(5, InventoryTarget.Hand), Move(5, InventoryTarget.Hand, index: Limits.MaxStorageHands + 1), Move(5, InventoryTarget.Hand, 100, 1),
            Move(5, InventoryTarget.Floor, index: 1), Move(5, InventoryTarget.Floor, 100),
        };
        foreach (ActionMessage move in bad)
            Assert.Throws<ProtocolException>(() => Codec.Encode(move));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Move(5, InventoryTarget.Floor) with { Target = 3, TargetKind = TargetKind.Entity }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(Move(5, InventoryTarget.Floor) with { HotbarCell = 1 }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new ActionMessage(9, ActionKind.Rest, 0, ClickButton.None, ClickModifiers.None, null) { MoveItem = 5 }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(new ActionMessage(9, ActionKind.HotbarPress, 0, ClickButton.None, ClickModifiers.None, null) { HotbarCell = 1, MoveTo = InventoryTarget.Floor }));

        // the decoder: [10] [seq 9] [kind 29] [item] [target] ...
        void Refused(byte[] message, string why) => Assert.False(Codec.TryDecode(message, out ActionMessage _, out _), why);
        Refused(new byte[] { 10, 9, 29, 0, 4 }, "an item of 0");
        Refused(new byte[] { 10, 9, 29, 5, 0 }, "no target");
        Refused(new byte[] { 10, 9, 29, 5, 5 }, "an unknown target kind");
        Refused(new byte[] { 10, 9, 29, 5, 255 }, "an unknown target kind");
        Refused(new byte[] { 10, 9, 29, 5, 1, 0, 0 }, "a storage of 0");
        Refused(new byte[] { 10, 9, 29, 5, 1, 100, 65 }, "a cell out of range");
        Refused(new byte[] { 10, 9, 29, 5, 1, 100, 255 }, "a cell out of range");
        Refused(new byte[] { 10, 9, 29, 5, 2, 3 }, "two slots at once");
        Refused(new byte[] { 10, 9, 29, 5, 2, 0 }, "a slot of 0");
        Refused(new byte[] { 10, 9, 29, 5, 2, 0x80, 0x40 }, "the hands' slot (8192)");
        Refused(new byte[] { 10, 9, 29, 5, 2, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F }, "a slot over 31 bits");
        Refused(new byte[] { 10, 9, 29, 5, 3, 0 }, "a hand of 0");
        Refused(new byte[] { 10, 9, 29, 5, 3, 9 }, "a hand out of range");
        Refused(new byte[] { 10, 9, 29, 5, 4, 0 }, "a trailing byte");
        Refused(new byte[] { 10, 9, 29, 5 }, "truncated");
        Refused(new byte[] { 10, 9, 29, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, 4 }, "an item over 63 bits");
    }

    [Fact]
    public void TheInventoryOpenNoticeHasNoFields()
    {
        Assert.Equal(ActionKind.InventoryOpen, ActionKinds.Last);
        var open = new ActionMessage(9, ActionKind.InventoryOpen, 0, ClickButton.None, ClickModifiers.None, null);
        byte[] encoded = Codec.Encode(open);
        Assert.Equal(new byte[] { 10, 9, 30 }, encoded);
        Assert.True(Codec.TryDecode(encoded, out ActionMessage back, out ProtocolError error), error.ToString());
        Assert.Equal(open, back);
        Assert.Throws<ProtocolException>(() => Codec.Encode(open with { MoveItem = 5, MoveTo = InventoryTarget.Floor }));
        Assert.Throws<ProtocolException>(() => Codec.Encode(open with { Target = 5, TargetKind = TargetKind.Entity }));
        Assert.False(Codec.TryDecode(new byte[] { 10, 9, 30, 0 }, out ActionMessage _, out error));
        Assert.Equal(ProtocolError.TrailingBytes, error);
        Assert.False(Codec.TryDecode(new byte[] { 10, 9, 31 }, out ActionMessage _, out _)); // the next kind is unknown
    }
}
