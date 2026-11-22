using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.T1;

// T1: tg's access rules (modules/jobs/access.dm: allowed, check_access, check_access_list) on a door, and the dev access of
// D117/DV23 that stands in for the ID card.

public class AccessTests
{
    private const string Eng = "engineering", Maint = "maint_tunnels", Cargo = "cargo";

    // (req_access, req_one_access, the player's access or null, expected) evaluated by hand from access.dm:39-55
    public static IEnumerable<object[]> Table() => new[]
    {
        Row(null, null, null, true), // `!length(req_access) && !length(req_one_access)`: free
        Row(null, null, new string[0], true),
        Row(new string[0], new string[0], null, true), // empty lists are no requirement
        Row(new[] { Eng }, null, null, false), // `!length(access_list)`
        Row(new[] { Eng }, null, new string[0], false),
        Row(new[] { Eng }, null, new[] { Eng }, true),
        Row(new[] { Eng }, null, new[] { Maint }, false),
        Row(new[] { Eng, Maint }, null, new[] { Eng }, false), // all of req_access
        Row(new[] { Eng, Maint }, null, new[] { Cargo, Maint, Eng }, true),
        Row(null, new[] { Eng, Maint }, new[] { Maint }, true), // any of req_one_access
        Row(null, new[] { Eng, Cargo }, new[] { Maint }, false),
        Row(null, new[] { Eng, Cargo }, new string[0], false),
        Row(new[] { Eng }, new[] { Maint, Cargo }, new[] { Eng }, false), // both lists set: all of one AND any of the other
        Row(new[] { Eng }, new[] { Maint, Cargo }, new[] { Eng, Cargo }, true),
        Row(new[] { Eng }, new[] { Maint, Cargo }, new[] { Maint, Cargo }, false),
    };

    private static object[] Row(string[] all, string[] any, string[] player, bool expected) => new object[] { all, any, player, expected };

    private static DmList ToList(string[] a) => a == null ? null : Dm.list(a.Cast<object>().ToArray());

    [Theory]
    [MemberData(nameof(Table))]
    public void CheckAccessListFollowsTgsRules(string[] req_all, string[] req_any, string[] player, bool expected)
    {
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_airlock_public>(room, 4, 4);
        room.In(() =>
        {
            door.req_access = ToList(req_all);
            door.req_one_access = ToList(req_any);
            Assert.Equal(expected, Dm.truthy(door.check_access_list(ToList(player))));
            // check_access(I) asks the item's GetAccess (access.dm:36)
            var card = Dm.@new<obj_item_t1_keycard>(locate(3, 3, 1));
            card.t1_access = ToList(player) ?? Dm.list();
            Assert.Equal(expected, Dm.truthy(door.check_access(card)));
            Assert.Equal(req_all == null && req_any == null || (req_all?.Length ?? 0) + (req_any?.Length ?? 0) == 0, Dm.truthy(door.check_access(null)));
        });
    }

    [Fact]
    public void AllowedGivesAFreeDoorToAnyoneAndAKeyedDoorOnlyToTheRightMob()
    {
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_airlock_public>(room, 4, 4);
        var nobody = T1.Human(room, 3, 3);
        var holder = T1.Human(room, 5, 5);
        T1.Access(room, holder, ACCESS_ENGINEERING);
        room.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(door.allowed__proc(nobody)))); // access.dm:8, check_access(null)
            Assert.True(Dm.truthy(Waiting.NoSleep(door.allowed__proc(null)))); // a telekinesis user: free door
            door.req_access = T1.L(ACCESS_ENGINEERING);
            Assert.False(Dm.truthy(Waiting.NoSleep(door.allowed__proc(nobody))));
            Assert.False(Dm.truthy(Waiting.NoSleep(door.allowed__proc(null)))); // access.dm:11
            Assert.True(Dm.truthy(Waiting.NoSleep(door.allowed__proc(holder))));
            door.req_access = T1.L(ACCESS_ENGINEERING, ACCESS_CARGO);
            Assert.False(Dm.truthy(Waiting.NoSleep(door.allowed__proc(holder))));
            // door.dm:327: `..() || emergency`
            door.emergency = TRUE;
            Assert.True(Dm.truthy(Waiting.NoSleep(door.allowed__proc(nobody))));
            Assert.True(Dm.truthy(Waiting.NoSleep(door.allowed__proc(null))));
        });
    }

    [Fact]
    public void TheIdScannerOffMeansNoAccessIsRequiredByRequiresId()
    {
        // door.dm:331 `requiresID() && user_can_activate_door`: an airlock whose scanner is disabled (aiDisabledIdScanner)
        // refuses every bump (airlock.dm:355 requiresID = !(wires.is_cut(WIRE_IDSCAN) || aiDisabledIdScanner))
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_airlock_public>(room, 4, 4);
        var human = T1.Human(room, 4, 3);
        T1.AllAccess(room, human);
        room.In(() =>
        {
            Assert.True(Dm.truthy(door.requiresID()));
            door.aiDisabledIdScanner = TRUE;
            Assert.False(Dm.truthy(door.requiresID()));
        });
        T1.Bump(room, human, door);
        room.Step(30);
        room.In(() => Assert.True(T1.Dense(door)));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AMobsAccessIsItsHeldCardsAndIsCopiedOnEveryAsk()
    {
        // M4a P5: /mob/living/get_access is the list COMSIG_MOB_RETRIEVE_ACCESS fills (living.dm:649), from the card's
        // GetAccess (a copy, cards_ids.dm:951); M3's dev access list (DV23) is retired.
        var room = T1.Room();
        var human = T1.Human(room, 3, 3);
        room.In(() =>
        {
            Assert.Equal(0f, Dm.num(((DmList)human.get_access()).len)); // no ID card: an empty list
            obj_item_card_id card = Anomaly.Core.Tests.Tg.M4aAccess.P5.IdCards.Hold(human, ACCESS_ENGINEERING, ACCESS_CARGO);
            DmList first = (DmList)human.get_access();
            Assert.Equal(2f, Dm.num(first.len));
            Assert.True(Dm.@in(ACCESS_ENGINEERING, first));
            Assert.True(Dm.@in(ACCESS_CARGO, first));
            Dm.append(ref first, ACCESS_CAPTAIN); // a caller's edit must not reach the card
            Assert.Equal(2f, Dm.num(((DmList)human.get_access()).len));
            Waiting.NoSleep(human.dropItemToGround(card)); // a dropped card gives nothing (dropped unregisters the signal)
            Assert.Equal(0f, Dm.num(((DmList)human.get_access()).len));
        });
    }

    [Fact]
    public void ADevAccessMobOpensAnAirlockKeyedToExactlyThoseAccessesAndNoOtherDoor()
    {
        var room = T1.Room();
        var eng = T1.At<obj_machinery_door_airlock_public>(room, 3, 4);
        var cargo = T1.At<obj_machinery_door_airlock_public>(room, 5, 4);
        var human = T1.Human(room, 4, 3);
        room.In(() =>
        {
            eng.req_access = T1.L(ACCESS_ENGINEERING);
            cargo.req_one_access = T1.L(ACCESS_CARGO, ACCESS_SHIPPING);
        });
        T1.Access(room, human, ACCESS_ENGINEERING);
        room.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(eng.allowed__proc(human))));
            Assert.False(Dm.truthy(Waiting.NoSleep(cargo.allowed__proc(human))));
        });
        T1.Access(room, human, ACCESS_SHIPPING);
        room.In(() =>
        {
            Assert.False(Dm.truthy(Waiting.NoSleep(eng.allowed__proc(human))));
            Assert.True(Dm.truthy(Waiting.NoSleep(cargo.allowed__proc(human))));
        });
    }

    [TgFact]
    public void AccessAllIsExactlyTgsEveryAccessRegion()
    {
        // __DEFINES/access.dm:428-429: REGION_ACCESS_ALL_GLOBAL, "a list of every single access in the game" (it leaves out
        // the accesses no region lists: roroco, hunter, heretic, inaccessible), the dev command's `all`
        var defined = TgText.AccessDefines().ToDictionary(d => d.Name, d => d.Value);
        var expected = TgText.AllGlobalAccessNames().Select(n => defined[n]).ToHashSet();
        Assert.True(expected.Count >= 90, "the parse of the region lists found too few accesses");
        var room = T1.Room();
        var human = T1.Human(room, 3, 3);
        T1.AllAccess(room, human);
        room.In(() =>
        {
            var got = new List<string>();
            foreach (object a in Dm.each((DmList)human.get_access())) got.Add((string)a);
            Assert.Equal(got.Count, got.Distinct().Count()); // each access once
            Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal).ToArray(), got.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        });
    }

    [TgFact]
    public void EveryAccessConstantOfThePortEqualsTgsDefineText()
    {
        // The port's Defines.ACCESS_* are what the access helpers and req_access lists use; they must be tg's strings.
        var wrong = new List<string>();
        foreach (var (name, value) in TgText.AccessDefines())
        {
            var field = typeof(Defines).GetField(name);
            if (field == null) wrong.Add($"{name} missing");
            else if ((string)field.GetRawConstantValue() != value) wrong.Add($"{name} = {field.GetRawConstantValue()} (tg \"{value}\")");
        }
        Assert.Empty(wrong);
    }
}
