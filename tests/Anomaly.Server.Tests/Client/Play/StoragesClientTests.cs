using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// ui-overhaul.md section 12, slice 4B-1 (DV692) on the client: the carried storages from the server's message as the replica keeps them
/// (<see cref="StoragesModel"/>, the Godot-free model the next slice draws), the last refused move dated once, and the moves and the Tab notice the play
/// session sends (<see cref="PlaySession.InventoryMove"/>, <see cref="PlaySession.InventoryOpened"/>), fields out of range never sent.
/// </summary>
public class StoragesClientTests
{
    private static StoragesMessage Message(long refused = 0) => new()
    {
        Storages = new[]
        {
            new CarriedStorage
            {
                Entity = 100, Place = StoragePlace.Back, Name = "backpack", Slots = 21, Used = 4, Limit = 21,
                Items = new[] { new StorageItem(101, "screwdriver", new TableRef(7, true), 0), new StorageItem(102, "cable coil", new TableRef(8, true), 30) },
            },
            new CarriedStorage { Entity = 200, Place = StoragePlace.Belt, Name = "toolbelt", Slots = 7, Limit = 14 },
            new CarriedStorage { Entity = 300, Place = StoragePlace.Hand, Hand = 2, Name = "box", Slots = 7, Limit = 14, Items = new[] { new StorageItem(301, "gauze", TableRef.None, 5) } },
        },
        RefusedItem = refused,
    };

    [Fact]
    public void TheModelKeepsWhatTheReplicaTookAndFindsAStorageAndAnItemsCell()
    {
        var replica = new ReplicaWorld();
        var model = new StoragesModel();
        Assert.Empty(model.Storages);
        Assert.True(replica.TryApply(Codec.Encode(Message()), out _, out ProtocolError error), error.ToString());
        model.Apply(replica.Storages, replica.StoragesRefusals, replica.StoragesRefusedItem, 1.0);
        Assert.Equal(3, model.Storages.Count);
        Assert.Equal(100, model.At(StoragePlace.Back).Entity);
        Assert.Equal(300, model.At(StoragePlace.Hand, 2).Entity);
        Assert.Null(model.At(StoragePlace.Hand, 1));
        Assert.Null(model.At(StoragePlace.LeftPocket));
        Assert.Same(model.At(StoragePlace.Back), model.Holding(102, out int cell));
        Assert.Equal(1, cell);
        Assert.Equal(30, model.At(StoragePlace.Back).Items[cell].Count);
        Assert.Null(model.Holding(999, out cell));
        Assert.Equal(-1, cell);
        Assert.Equal(0, model.RefusedItem);
        Assert.Equal(double.NegativeInfinity, model.RefusedAt);
    }

    [Fact]
    public void ARefusalIsDatedOnceAndANewFeedCountsAgain()
    {
        var replica = new ReplicaWorld();
        var model = new StoragesModel();
        Assert.True(replica.TryApply(Codec.Encode(Message(refused: 301)), out _, out _));
        model.Apply(replica.Storages, replica.StoragesRefusals, replica.StoragesRefusedItem, 2.0);
        Assert.Equal((301L, 2.0), (model.RefusedItem, model.RefusedAt));
        Assert.True(replica.TryApply(Codec.Encode(Message()), out _, out _)); // a state message is no refusal
        model.Apply(replica.Storages, replica.StoragesRefusals, replica.StoragesRefusedItem, 3.0);
        Assert.Equal(2.0, model.RefusedAt);
        Assert.True(replica.TryApply(Codec.Encode(Message(refused: 101)), out _, out _));
        model.Apply(replica.Storages, replica.StoragesRefusals, replica.StoragesRefusedItem, 4.0);
        Assert.Equal((101L, 4.0), (model.RefusedItem, model.RefusedAt));
        // a new join: the feed counts from 0 again, the old refusal is not replayed
        model.Apply(StoragesMessage.None, 0, 0, 5.0);
        Assert.Empty(model.Storages);
        Assert.Equal(4.0, model.RefusedAt);
        model.Apply(Message(), 1, 102, 6.0);
        Assert.Equal((102L, 6.0), (model.RefusedItem, model.RefusedAt));
        model.Apply(null, 1, 102, 7.0);
        Assert.Empty(model.Storages);
    }

    [Fact]
    public void TheSessionSendsMovesInRangeAndTheNoticeAndNothingOutOfRange()
    {
        var feed = new FakeFeed("Tester", seed: 1, size: 32, bots: 0, inventory: true);
        var rig = new PlayRig(feed);
        rig.Run(0.5);
        Assert.True(rig.Session.InventoryMove(5, InventoryTarget.Storage, 100, 3));
        Assert.True(rig.Session.InventoryMove(5, InventoryTarget.Slot, index: 0x8000));
        Assert.True(rig.Session.InventoryMove(5, InventoryTarget.Hand, index: 2));
        Assert.True(rig.Session.InventoryMove(5, InventoryTarget.Floor));
        Assert.False(rig.Session.InventoryMove(0, InventoryTarget.Floor));
        Assert.False(rig.Session.InventoryMove(5, InventoryTarget.None));
        Assert.False(rig.Session.InventoryMove(5, InventoryTarget.Storage, 0));
        Assert.False(rig.Session.InventoryMove(5, InventoryTarget.Storage, 100, Limits.MaxStorageItems + 1));
        Assert.False(rig.Session.InventoryMove(5, InventoryTarget.Slot, index: 3));
        Assert.False(rig.Session.InventoryMove(5, InventoryTarget.Hand, index: 0));
        Assert.False(rig.Session.InventoryMove(5, InventoryTarget.Floor, index: 1));
        rig.Session.InventoryOpened();
        rig.Run(0.5);
        Assert.Equal(FeedPhase.Playing, feed.Phase); // the offline fake server takes the new actions without a fault
        Assert.Empty(rig.Session.Storages.Storages); // and sends no Storages message: the offline fake has none
    }
}
