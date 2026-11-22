using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.M1Client.T1.Dirs;

namespace Anomaly.Core.Tests.Tg.M1Client.T1;

/// <summary>
/// T1: the keysend flood check of <c>keyDown</c> (code/modules/keybindings/bindings_client.dm:6-28, MAX_KEYPRESS_AUTOKICK = 50
/// in __DEFINES/admin.dm:137). Every expected number is traced by hand from the DM, which counts in an odd way: the first press
/// of a one-second window is counted into <c>cache</c> and then <c>client_keysend_amount</c> is reset to 0, so press k of a
/// window has <c>cache = k - 1</c> (k >= 2): the 51st press of a window is the first with <c>cache >= 50</c> (it trips), the 52nd
/// kicks. A second is 10 deciseconds (20 ticks); the trip lasts 2 seconds.
/// </summary>
public class KeysendFloodTests
{
    private static float N(object v) => Dm.num(v);

    private static void Press(T1Room room, client c, int times, string key = "Space")
    {
        for (int i = 0; i < times; i++)
            room.Down(c, key);
    }

    [Fact]
    public void FiftyPressesInOneTickAreNotAFlood()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        float t0 = 0;
        room.Act(() => { t0 = world.time; Press(room, c, 50); });
        room.Step();
        room.In(() =>
        {
            Assert.Null(c.gc_destroyed);
            Assert.Equal(49f, c.client_keysend_amount); // press 1 counted and reset the amount, then 49 more
            Assert.False(Dm.truthy(c.keysend_tripped));
            Assert.Equal(t0 + 10, c.next_keysend_reset);
        });
        Assert.Empty(room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void The51stPressInAWindowTripsTheCheckAndDoesNotKick()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        float t0 = 0;
        room.Act(() => { t0 = world.time; Press(room, c, 51); });
        room.Step();
        room.In(() =>
        {
            Assert.Null(c.gc_destroyed);
            Assert.True(Dm.truthy(c.keysend_tripped));
            Assert.Equal(t0 + 20, c.next_keysend_trip_reset); // 2 SECONDS
            Assert.Equal(50f, c.client_keysend_amount);
            // "the warning" of the task is the silent flag: no message yet (but client/New's unread changelog notice, M10x slice H)
            Assert.All(room.Chat.To(c), line => Assert.Contains("unread updates in the changelog", line.ToString(), StringComparison.Ordinal));
        });
        Assert.Empty(room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void The52ndPressKicksTheClientWithTheMessageAndTheAdminLog()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() =>
        {
            Press(room, c, 51);
            room.Down(c, "Enter"); // the 52nd: cache 51 >= 50 with the check already tripped
        });
        room.Step();
        room.In(() =>
        {
            Assert.NotNull(c.gc_destroyed);
            Assert.False(Dm.truthy(c.keys_held["Enter"])); // the kick returns before the key is recorded (:28)
            Assert.Null(avatar.client);
            Assert.Equal("Tester", avatar.key); // the mob keeps its key
            Assert.Contains(room.Chat.To(c), t => t.Contains("Flooding keysends!", StringComparison.Ordinal));
            Assert.Contains(Dm.each(GLOB.admin_activities), a => Dm.str(a) == "Client tester was just autokicked for flooding keysends; likely abuse but potentially lagspike.");
        });
        Assert.Equal(new[] { c }, room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void PressesSpreadOverTimeNeverTrip()
    {
        // one press a tick for 100 ticks: at most 20 presses fall into a one-second window
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        for (int i = 0; i < 100; i++)
        {
            room.Act(() => room.Down(c, "Space"));
            room.Step();
        }
        room.In(() =>
        {
            Assert.Null(c.gc_destroyed);
            Assert.False(Dm.truthy(c.keysend_tripped));
            Assert.True(c.client_keysend_amount <= 20);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TwoBatchesOfFortyAFullSecondApartAreTwoWindowsAndDoNotTrip()
    {
        // second batch, press 1 at time T0 + 10: next_keysend_reset (T0 + 10) <= world.time resets the window (:20-22)
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        room.Act(() => Press(room, c, 40));
        room.Step(20);
        room.Act(() => Press(room, c, 40));
        room.Step();
        room.In(() =>
        {
            Assert.Null(c.gc_destroyed);
            Assert.False(Dm.truthy(c.keysend_tripped));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TwoBatchesOfFortyLessThanASecondApartShareAWindowAndTripOnTheEleventhPressOfTheSecond()
    {
        // One tick short of a second (9.5 ds): the window is still open. Batch 1 leaves amount = 39 (its first press was reset),
        // so press n of batch 2 reads cache = 39 + n: press 10 reads 49, press 11 reads 50 (trips), press 12 reads 51 (kicks).
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var seen = new List<string>();
        room.Act(() => Press(room, c, 40));
        room.Step(19);
        room.Act(() =>
        {
            Press(room, c, 10);
            seen.Add("after 10: tripped=" + Dm.truthy(c.keysend_tripped) + " destroyed=" + (c.gc_destroyed != null));
            room.Down(c, "Space");
            seen.Add("after 11: tripped=" + Dm.truthy(c.keysend_tripped) + " destroyed=" + (c.gc_destroyed != null));
            room.Down(c, "Space");
            seen.Add("after 12: destroyed=" + (c.gc_destroyed != null));
        });
        room.Step();
        Assert.Equal(new[]
        {
            "after 10: tripped=False destroyed=False",
            "after 11: tripped=True destroyed=False",
            "after 12: destroyed=True",
        }, seen);
        Assert.Equal(new[] { c }, room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void BatchesInTheSameTickShareTheWindowAndTheFiftyFirstPressOverallTrips()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var seen = new List<string>();
        room.Act(() =>
        {
            Press(room, c, 40);
            Press(room, c, 11); // overall press 51
            seen.Add("after 51: tripped=" + Dm.truthy(c.keysend_tripped) + " destroyed=" + (c.gc_destroyed != null));
            Press(room, c, 1); // overall press 52
            seen.Add("after 52: destroyed=" + (c.gc_destroyed != null));
        });
        room.Step();
        Assert.Equal(new[] { "after 51: tripped=True destroyed=False", "after 52: destroyed=True" }, seen);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TgQuirkALonePressInsideTheTripWindowKicksTheClient()
    {
        // After the burst `client_keysend_amount` stays 50 until a press resets it. A press one second later (T0 + 10 < the trip
        // reset T0 + 20): amount 51 = cache, the trip flag is still set, so the check kicks although the player pressed once.
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => Press(room, c, 51));
        room.Step(20);
        room.In(() => Assert.True(Dm.truthy(c.keysend_tripped)));
        room.Act(() => room.Down(c, "Space"));
        room.Step();
        room.In(() => Assert.NotNull(c.gc_destroyed));
        Assert.Equal(new[] { c }, room.Host.Deleted);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TgQuirkALonePressRightAfterTheTripWindowTripsAgainButDoesNotKick()
    {
        // At T0 + 20 the press first clears the trip flag (:12-13, next_keysend_trip_reset <= world.time), then still reads
        // cache = 51: no kick, but the flag is set again for another 2 seconds.
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => Press(room, c, 51));
        room.Step(40);
        float t = 0;
        room.Act(() => { t = world.time; room.Down(c, "Space"); });
        room.Step();
        room.In(() =>
        {
            Assert.Null(c.gc_destroyed);
            Assert.True(Dm.truthy(c.keysend_tripped));
            Assert.Equal(t + 20, c.next_keysend_trip_reset);
            Assert.Equal(0f, c.client_keysend_amount); // the window was reset by the same press
        });
        room.Act(() => room.Down(c, "Space"));
        room.Step();
        room.In(() => Assert.Null(c.gc_destroyed)); // the next press starts from amount 1
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheTripFlagClearsOnTheFirstPressTwoSecondsAfterItWasSet()
    {
        // :12-13  if(keysend_tripped && next_keysend_trip_reset <= world.time) keysend_tripped = FALSE. Burst at T0 (trips), a lone
        // press at T0 + 20.5 re-trips (above, trip reset T0 + 40.5), a press at T0 + 41 finds amount 1: the flag is cleared and
        // stays clear, and a burst of 50 more does not kick.
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() => Press(room, c, 51));
        room.Step(41);
        room.Act(() => room.Down(c, "Space"));
        room.Step(41);
        room.In(() => Assert.True(Dm.truthy(c.keysend_tripped)));
        room.Act(() => room.Down(c, "Space"));
        room.Step();
        room.In(() =>
        {
            Assert.False(Dm.truthy(c.keysend_tripped));
            Assert.Null(c.gc_destroyed);
        });
        room.Act(() => Press(room, c, 49));
        room.Step();
        room.In(() => Assert.Null(c.gc_destroyed));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void KeyUpEventsAreNotCounted()
    {
        // only keyDown reads client_keysend_amount
        var room = new T1Room();
        var (_, c) = room.JoinNew("Tester");
        room.Act(() =>
        {
            for (int i = 0; i < 300; i++)
                room.Up(c, "Space");
        });
        room.Step();
        room.In(() =>
        {
            Assert.Null(c.gc_destroyed);
            Assert.Equal(0f, c.client_keysend_amount);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AMovementFloodThroughTheHostSeamKicksOnThe52ndKeyDownAndTheSeamThenDoesNothing()
    {
        // ClientSeam.SetHeldDirections sends keyUp then keyDown per changed direction (a toggle of NORTH is one keyDown and one
        // keyUp); 51 keyDowns pass, the 52nd kicks. After the kick the seam ignores the dead client.
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        int survived = 0;
        room.Act(() =>
        {
            for (int i = 0; i < 80; i++)
            {
                ClientSeam.SetHeldDirections(c, North);
                if (c.gc_destroyed == null)
                    survived++;
                ClientSeam.SetHeldDirections(c, 0);
            }
        });
        room.Step();
        Assert.Equal(51, survived);
        Assert.Equal(new[] { c }, room.Host.Deleted);
        room.Act(() => ClientSeam.SetHeldDirections(c, East)); // no exception, no effect
        room.Step();
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheKickedClientsMobIsNotMovedAnyMore()
    {
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        room.Act(() =>
        {
            room.Down(c, "W");
            Press(room, c, 60);
        });
        room.Step(6);
        room.In(() =>
        {
            Assert.NotNull(c.gc_destroyed);
            Assert.Null(avatar.client);
            Assert.Equal("8,8", T1Room.Where(avatar)); // the client was gone before the first fire
            Assert.False(Dm.@in(avatar, GLOB.keyloop_list));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AQdeletedClientIsGoneFromItsMobAndLoggedOutInsideQdel()
    {
        // BYOND: qdel -> Destroy -> HARDDEL_NOW -> del (nulls every reference to the client) -> client/Del -> Logout, all inside
        // qdel. The port's SSgarbage.HardDelete runs BYOND's client/Del for a /client inside qdel too (M1 integration), so by
        // the time qdel returns the mob has no client and Logout has taken it out of GLOB.player_list.
        var room = new T1Room();
        var (avatar, c) = room.JoinNew("Tester");
        var seen = new List<string>();
        room.Act(() =>
        {
            Press(room, c, 52);
            seen.Add("destroyed=" + (c.gc_destroyed != null));
            seen.Add("in GLOB.clients=" + Dm.truthy(Dm.@in(c, GLOB.clients)));
            seen.Add("directory=" + (GLOB.directory["tester"] == null ? "none" : "set"));
            seen.Add("mob.client=" + (avatar.client == null ? "null" : "set"));
            seen.Add("in player_list=" + Dm.truthy(Dm.@in(avatar, GLOB.player_list)));
        });
        room.Step();
        Assert.Equal(new[] { "destroyed=True", "in GLOB.clients=False", "directory=none", "mob.client=null", "in player_list=False" }, seen);
        room.AssertNoRuntimes();
    }
}
