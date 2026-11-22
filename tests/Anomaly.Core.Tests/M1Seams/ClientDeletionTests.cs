using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M1Client.P1;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M1Seams;

/// <summary>
/// tg's qdel of a connected client disconnects the player inside qdel, as BYOND's <c>del</c> does (BYOND ref <c>del</c>:
/// "its Del() procedure is called"; <c>client/Destroy</c> returns <c>QDEL_HINT_HARDDEL_NOW</c>, and
/// <c>SSgarbage.HardDelete</c>'s <c>del(D)</c> calls <c>client/Del</c>, garbage.dm:292): when qdel returns, the mob has lost
/// its client and kept its key, Logout ran, and the host was told.
/// </summary>
public class ClientDeletionTests
{
    [Fact]
    public void QdelOfAConnectedClientRunsClientDelBeforeQdelReturns()
    {
        var room = new ClientRoom();
        mob avatar = room.NewMob(5, 5);
        client player = room.Join(avatar, "Gone").Client;
        bool checkedInside = false;
        room.Command(() =>
        {
            qdel(player);
            Assert.True(player.dm_gone);
            Assert.Null(avatar.client);
            Assert.Equal("Gone", avatar.key);
            Assert.False(Dm.@in(avatar, GLOB.player_list)); // mob/Logout ran
            Assert.Equal(new[] { player }, room.Host.Deleted);
            Assert.False(World.Current.Objects.TryGet(player.dm_id, out _));
            checkedInside = true;
        });
        room.Step();
        Assert.True(checkedInside);
        Assert.Single(room.Host.Deleted);
        room.AssertNoRuntimes();
    }
}
