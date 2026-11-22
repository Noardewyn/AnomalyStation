using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M1Client.P1;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.I1d;

/// <summary>
/// M3 I1d (the join stall): the /client engine part finds the mobs of a key through an index of keyed mobs instead of a
/// scan of every atom of the world (<c>client.dm_mobs_with_key</c>). The index must find what the scan found: BYOND's
/// rule that a key is on one mob at most (<c>/mob/var/key</c>) holds after any run of assignments, a connecting player
/// is attached to the mob that has its key now, and a deleted mob is never found.
/// </summary>
public class KeyedMobIndexTests
{
    private static List<mob> ScanForKey(string ckey)
    {
        var found = new List<mob>();
        foreach (mob m in Dm.each<mob>(Dm.world))
        {
            if (m.ckey == ckey)
                found.Add(m);
        }
        return found;
    }

    [Fact]
    public void ARandomRunOfKeyAssignmentsLeavesEachKeyOnOneMobAtMost()
    {
        var room = new ClientRoom();
        var mobs = new List<mob>();
        for (int i = 0; i < 8; i++)
            mobs.Add(room.NewMob(2 + i, 3));
        string[] keys = { "Alpha", "Beta", "Gamma" };
        var rng = new System.Random(7);
        room.In(() =>
        {
            for (int op = 0; op < 400; op++)
            {
                mob target = mobs[rng.Next(mobs.Count)];
                string key = keys[rng.Next(keys.Length)];
                switch (rng.Next(4))
                {
                    case 0: target.key = key; break;
                    case 1: target.ckey = Dm.ckey(key); break;
                    case 2: target.key = null; break;
                    default: target.key = key.ToUpperInvariant(); break; // the same canonical key
                }
                foreach (string k in keys)
                {
                    List<mob> holders = ScanForKey(Dm.ckey(k));
                    Assert.True(holders.Count <= 1, $"op {op}: {holders.Count} mobs have the key {k}");
                }
            }
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AConnectingPlayerIsAttachedToTheMobThatHasItsKeyNow()
    {
        var room = new ClientRoom();
        mob a = room.NewMob(5, 5);
        mob b = room.NewMob(6, 6);
        mob c = room.NewMob(7, 7);
        room.In(() =>
        {
            a.key = "Wanderer";
            b.key = "Wanderer"; // a loses it
            c.ckey = "wanderer"; // b loses it
            Assert.Null(a.ckey);
            Assert.Null(b.ckey);
        });
        ClientConnection connection = null;
        room.Command(() => connection = ClientSeam.Connect("Wanderer"));
        room.Step();
        Assert.True(connection.Accepted);
        Assert.Same(c, connection.Mob);
        room.In(() =>
        {
            Assert.Same(connection.Client, c.client);
            Assert.Equal(new[] { c }, ScanForKey("wanderer"));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ADeletedMobThatHadTheKeyIsNotFoundAndTheKeyCanBeGivenAgain()
    {
        var room = new ClientRoom();
        mob a = room.NewMob(5, 5);
        room.In(() =>
        {
            a.key = "Ghost";
            a.key = null; // tg's mob/Destroy ghostizes a mob that still has a key (M4+); the index keeps a's id until a read
        });
        room.Command(() => qdel(a));
        room.Step(3);
        mob b = room.NewMob(6, 6);
        room.In(() =>
        {
            b.key = "Ghost";
            Assert.Equal("ghost", b.ckey);
        });
        ClientConnection connection = null;
        room.Command(() => connection = ClientSeam.Connect("Ghost"));
        room.Step();
        Assert.True(connection.Accepted);
        Assert.Same(b, connection.Mob);
        room.AssertNoRuntimes();
    }
}
