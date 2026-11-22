using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Protocol;

/// <summary>
/// Protocol version 43 (M10 slice E, docs/ai/specs/protocol.md section 52): the private block's settled actions and access list, an airlock's
/// Door field, the screen entries' inventory boxes and items; and the replica's guessed layer (the client's own table entries, a guessed
/// screen entry, a hidden entity, a cell passable from a frame on), which the server's messages do not disturb.
/// </summary>
public class ProtocolV43Tests
{
    private static readonly DoorState Engineering = new(DoorFlags.Openable, 8, new[] { "engineering" }, new[] { "maint_tunnels", "atmospherics" });

    [Fact]
    public void TheOwnBlocksActionsAndAccessRoundTripAndAnEmptyBlockIsFourBytes()
    {
        Delta d = Msg.Delta(9);
        d.Own.ActionsDone = 300;
        d.Own.Access = new List<string> { "engineering", "maint_tunnels" };
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
        Msg.AssertEqual(d.Own, back.Own);
        var buffer = new byte[64];
        // flags 0, depth 0, 300 settled (varuint 0xAC 0x02), no access
        Assert.Equal(new byte[] { 0, 0, 0xAC, 0x02, 0 }, buffer.AsSpan(0, Codec.EncodeOwnBlock(new OwnBlock { ActionsDone = 300 }, buffer)).ToArray());
        Assert.Equal(Codec.EmptyOwnBlockBytes, Codec.EncodeOwnBlock(new OwnBlock(), buffer));
        // an empty list is a change to no access, distinct from no change
        d.Own.Access = new List<string>();
        Assert.True(Codec.TryDecode(Codec.Encode(d), out back, out _));
        Assert.NotNull(back.Own.Access);
        Assert.Empty(back.Own.Access);
        d.Own.Access = new List<string> { "" };
        Assert.Throws<ProtocolException>(() => Codec.Encode(d));
    }

    [Fact]
    public void AnAirlocksDoorRoundTripsInASpawnAndAChangeAndIsCheckedBothWays()
    {
        Snapshot s = Msg.Snapshot(10, 0, 0, new[] { "/obj/machinery/door/airlock" }, Msg.State(4, 0, 2, 2) with { Door = Engineering });
        Assert.True(Codec.TryDecode(Codec.Encode(s), out Snapshot back, out ProtocolError error), error.ToString());
        Assert.Equal(Engineering, back.Entities[0].Door);
        Delta d = Msg.Delta(11, changed: new[] { new EntityChange { Id = 4, Mask = NetField.Door, Door = Engineering with { Flags = DoorFlags.Emergency } } });
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta changed, out error), error.ToString());
        Msg.AssertEqual(d, changed);

        foreach (DoorState bad in new[] { null, Engineering with { UnresSides = 16 }, Engineering with { Flags = (DoorFlags)4 }, Engineering with { ReqAll = new[] { "" } },
                     Engineering with { ReqAny = Enumerable.Repeat("a", Limits.MaxDoorAccessEntries + 1).ToArray() } })
            Assert.Throws<ProtocolException>(() => Codec.Encode(Msg.Delta(11, changed: new[] { new EntityChange { Id = 4, Mask = NetField.Door, Door = bad } })));
        // the record's extension byte: flagged and empty, or an undefined bit
        byte[] good = Codec.Encode(s);
        int ext = Array.IndexOf(good, (byte)128, 10) + 2; // the record's flags byte (128: the extension), its appearance, then the extension
        Assert.Equal(2, good[ext]);
        foreach (byte value in new byte[] { 0, 8 }) // version 77: bit 4 is the ride
        {
            byte[] bad = (byte[])good.Clone();
            bad[ext] = value;
            Assert.False(Codec.TryDecode(bad, out Snapshot _, out _), "extension " + value);
        }
    }

    [Fact]
    public void ScreenEntriesCarryTheirBoxAndTheirItemsSlots()
    {
        var own = new OwnBlock();
        own.ScreenAdded.Add(new ScreenEntry(10, TableRef.Public(1), "hand_l") { HandIndex = 1 });
        own.ScreenAdded.Add(new ScreenEntry(11, TableRef.Public(1), "belt") { SlotId = 1u << 9,
            VisChildren = new ScreenChildren { Items = new[] { new ScreenChild(300, new TableRef(3, true)) { IsItem = true, ItemSlots = 1u << 9, EquipDelay = true } } } });
        own.ScreenAdded.Add(new ScreenEntry(12, TableRef.Public(1), "x") { VisChildren = new ScreenChildren { Items = new[] { new ScreenChild(301, new TableRef(3, true)) } } });
        Delta d = Msg.Delta(5);
        d.Own = own;
        Assert.True(Codec.TryDecode(Codec.Encode(d), out Delta back, out ProtocolError error), error.ToString());
        Assert.Equal(own.ScreenAdded, back.Own.ScreenAdded);
        Assert.Equal(own.ScreenAdded[1].VisChildren.Items, back.Own.ScreenAdded[1].VisChildren.Items);
        var buffer = new byte[256];
        int n = Codec.EncodeOwnBlock(new OwnBlock { ScreenAdded = { own.ScreenAdded[1] } }, buffer);
        // ... group 0, look 4 | 16 | 32 (children with items, a slot), 1 child: id 300 (0xAC 0x02), private ref 3 (13), the item: slots 512 << 3 | delay 2 | item 1
        // (4099: 0x83 0x20), then the slot 512 (0x80 0x04) and hand 0; 0 changed, 0 removed
        Assert.Equal(new byte[] { 0, 52, 1, 0xAC, 0x02, 13, 0x83, 0x20, 0x80, 0x04, 0, 0, 0 }, buffer.AsSpan(n - 13, 13).ToArray());
        Assert.Throws<ProtocolException>(() => Codec.EncodeOwnBlock(new OwnBlock { ScreenAdded = { new ScreenEntry(1, TableRef.None, null)
            { VisChildren = new ScreenChildren { Items = new[] { new ScreenChild(2, new TableRef(3, true)) { ItemSlots = 2 } } } } } }, buffer));
    }

    // ---- the replica's guessed layer ----

    private static ReplicaWorld Replica()
    {
        var replica = new ReplicaWorld();
        replica.Apply(Msg.FlatMap(10, 10));
        Snapshot s = Msg.Snapshot(10, 0, 0, new[] { "/obj/item/wrench", "/obj/machinery/door/airlock" },
            Msg.State(4, 0, 2, 2), Msg.State(5, 1, 3, 3) with { Door = Engineering });
        s.Strings.Add("hand_l");
        s.Appearances.Add(new AppearanceEntry { IconState = TableRef.Public(1) });
        s.Own.ScreenAdded.Add(new ScreenEntry(20, TableRef.Public(1), "WEST,SOUTH") { HandIndex = 1 });
        replica.Apply(s);
        return replica;
    }

    [Fact]
    public void TheClientsOwnEntriesResolveBesideTheServersTables()
    {
        ReplicaWorld replica = Replica();
        TableRef state = replica.LocalString("rhandactive");
        Assert.Equal(state, replica.LocalString("rhandactive"));
        Assert.True(state.Private && state.Index > ReplicaWorld.LocalBase);
        Assert.Equal("rhandactive", replica.ResolveString(state));
        TableRef look = replica.LocalAppearance(new AppearanceEntry { IconState = state });
        Assert.True(replica.TryGetAppearance(look, out AppearanceEntry entry));
        Assert.Equal(state, entry.IconState);
        Assert.Equal("hand_l", replica.ResolveString(TableRef.Public(1)));
        Assert.False(replica.TryGetAppearance(new TableRef(ReplicaWorld.LocalBase + 9, true), out _));
    }

    [Fact]
    public void AGuessedScreenEntryStandsOverTheServersUntilCleared()
    {
        ReplicaWorld replica = Replica();
        var changed = new List<long>();
        var removed = new List<long>();
        replica.ScreenEntryChanged += e => changed.Add(e.Id);
        replica.ScreenEntryRemoved += id => removed.Add(id);
        replica.GuessScreen(new ScreenEntry(4, TableRef.Public(1), "WEST,SOUTH")); // a guessed entry the server does not have
        replica.GuessScreenRemoved(20);
        Assert.Equal(new long[] { 4 }, changed);
        Assert.Equal(new long[] { 20 }, removed);
        Assert.True(replica.Screen.ContainsKey(4));
        Assert.False(replica.Screen.ContainsKey(20));

        // the server's word on a guessed entry changes nothing the client sees, and raises nothing
        Delta d = Msg.Delta(11);
        d.Own.ScreenChanged.Add(new ScreenEntry(20, TableRef.Public(1), "EAST,SOUTH") { HandIndex = 1 });
        replica.Apply(d);
        Assert.False(replica.Screen.ContainsKey(20));
        Assert.Single(changed);

        // cleared: the server's entry, or its absence, shows again
        replica.ClearScreenGuess(20);
        replica.ClearScreenGuess(4);
        Assert.Equal("EAST,SOUTH", replica.Screen[20].ScreenLoc);
        Assert.False(replica.Screen.ContainsKey(4));
        Assert.Equal(new long[] { 4, 20 }, changed);
        Assert.Equal(new long[] { 20, 4 }, removed);
        Assert.False(replica.HasGuesses);
    }

    [Fact]
    public void AHiddenEntityLeavesTheViewOnceAndComesBackOrGoesForGood()
    {
        ReplicaWorld replica = Replica();
        var spawned = new List<long>();
        var despawned = new List<long>();
        replica.Spawned += e => spawned.Add(e.Id);
        replica.Despawned += e => despawned.Add(e.Id);
        replica.GuessHidden(4);
        Assert.False(replica.TryGet(4, out _));
        Assert.DoesNotContain(replica.Entities, e => e.Id == 4);
        Assert.Equal(new long[] { 4 }, despawned);
        replica.ClearHidden(4); // refused: back in the world
        Assert.True(replica.TryGet(4, out _));
        Assert.Equal(new long[] { 4 }, spawned);

        replica.GuessHidden(4);
        replica.Apply(Msg.Delta(11, despawned: new long[] { 4 })); // accepted: the real despawn raises nothing more
        Assert.Equal(new long[] { 4, 4 }, despawned);
        replica.ClearHidden(4);
        Assert.Equal(new long[] { 4 }, spawned);
        Assert.False(replica.HasGuesses);
    }

    [Fact]
    public void AGuessedDoorsCellIsPassableFromItsFrameAndTheServersStateStaysUnderneath()
    {
        ReplicaWorld replica = Replica();
        var solid = Msg.Delta(11);
        solid.SolidChanged.Add(new CellSolidChange(3, 3, 1, true));
        replica.Apply(solid);
        Assert.True(replica.IsSolid(3, 3, 1));
        replica.GuessDoorOpening(5, 3, 3, 1, passableFrom: 40);
        Assert.Equal(ReplicaWorld.DoorOpeningState, replica.GuessedDoorState(5));
        replica.GuessFrame = 39;
        Assert.True(replica.IsSolid(3, 3, 1));
        replica.CollisionView.SteppingFrame = 40; // the predictor tells the view which frame it steps
        Assert.False(replica.IsSolid(3, 3, 1));
        Assert.False(replica.CollisionView.IsSolid(3, 3, 1));
        Assert.True(replica.IsSolidServer(3, 3, 1));
        replica.ClearDoorGuess(5, keepPassable: true); // the server opened it: the picture is the server's, the cell stays passable until it says so
        Assert.Null(replica.GuessedDoorState(5));
        Assert.False(replica.IsSolid(3, 3, 1));
        replica.ClearPassable(3, 3, 1);
        Assert.True(replica.IsSolid(3, 3, 1));
        Assert.False(replica.HasGuesses);
    }

    [Fact]
    public void AnAccessCheckIsTgsCheckAccessList()
    {
        Assert.True(OwnGuessesAccess(new DoorState(DoorFlags.Openable, 0, new string[0], new string[0]), new string[0]));
        Assert.True(OwnGuessesAccess(Engineering, new[] { "engineering", "atmospherics" }));
        Assert.False(OwnGuessesAccess(Engineering, new[] { "engineering" })); // none of req_one_access
        Assert.False(OwnGuessesAccess(Engineering, new[] { "maint_tunnels" })); // not every req_access
        Assert.False(OwnGuessesAccess(Engineering, new string[0]));
    }

    private static bool OwnGuessesAccess(DoorState door, string[] access) => Anomaly.Client.Shared.OwnGuesses.HasAccess(door, access);
}
