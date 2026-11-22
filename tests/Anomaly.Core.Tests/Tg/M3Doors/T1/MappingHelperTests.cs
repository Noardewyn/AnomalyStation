using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.T1;

// T1: the payloads of tg's airlock mapping helpers (mapping_helpers.dm:130-340, access_helpers.dm), applied the way a map load
// applies them (the atoms exist before SSatoms initializes: Initialize with mapload = TRUE, the payload at once, then
// LateInitialize for cyclelinking, the wire cut and the repaint, then the helper deletes itself).

public class AccessHelperPayloadTests
{
    [TgFact]
    public void EveryAccessHelperGivesTgsAccessListToItsAirlock()
    {
        var helpers = TgAccessHelpers.Parse();
        Assert.True(helpers.Count >= 150, $"the parse found {helpers.Count} access helpers");
        var defined = TgText.AccessDefines().ToDictionary(d => d.Name, d => d.Value);
        var airlocks = new List<obj_machinery_door_airlock>();
        var made = new List<(int X, int Y)>();
        var room = T1.Room(width: 18, height: 18, mapExtras: w =>
        {
            int i = 0;
            foreach (var h in helpers)
            {
                int x = 2 + i % 15, y = 2 + i / 15;
                i++;
                airlocks.Add(Dm.@new<obj_machinery_door_airlock_public>(locate(x, y, 1)));
                Type type = TgAccessHelpers.TypeOf("/obj/effect/mapping_helpers/airlock/access/" + (h.Any ? "any/" : "all/") + h.Path);
                Assert.True(type != null, "no type for " + h.Path);
                Dm.@new(type, locate(x, y, 1));
                made.Add((x, y));
            }
        });
        var wrong = new List<string>();
        room.In(() =>
        {
            for (int i = 0; i < helpers.Count; i++)
            {
                var h = helpers[i];
                string[] expected = h.Names.Select(n => defined[n]).ToArray();
                DmList got = (h.Any ? airlocks[i].req_one_access : airlocks[i].req_access) as DmList;
                DmList other = (h.Any ? airlocks[i].req_access : airlocks[i].req_one_access) as DmList;
                string[] actual = got == null ? new string[0] : Dm.each(got).Select(o => (string)o).ToArray();
                if (!expected.SequenceEqual(actual)) wrong.Add($"{h.Path}: tg [{string.Join(",", expected)}] port [{string.Join(",", actual)}]");
                if (other != null && Dm.truthy(other.len)) wrong.Add($"{h.Path}: the other list is not empty");
                if (Dm.locate<obj_effect_mapping_helpers>(locate(made[i].X, made[i].Y, 1)) != null) wrong.Add($"{h.Path}: the helper is still there");
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong.Take(12)) + "\n(" + wrong.Count + " wrong)");
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AnyAndAllHelpersCannotBeMixedOnOneAirlock()
    {
        // access_helpers.dm:8-23: `any` refuses when req_access is set, `all` refuses when req_one_access is set (log_mapping)
        obj_machinery_door_airlock first = null, second = null;
        var room = T1.Room(mapExtras: w =>
        {
            first = Dm.@new<obj_machinery_door_airlock_public>(locate(3, 3, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_access_all_engineering_general>(locate(3, 3, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_access_any_command_captain>(locate(3, 3, 1));
            second = Dm.@new<obj_machinery_door_airlock_public>(locate(5, 5, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_access_any_engineering_general>(locate(5, 5, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_access_all_command_captain>(locate(5, 5, 1));
        });
        room.In(() =>
        {
            Assert.Equal(new[] { ACCESS_ENGINEERING }, Dm.each((DmList)first.req_access).Cast<string>().ToArray());
            Assert.Null(first.req_one_access);
            Assert.Equal(new[] { ACCESS_ENGINEERING }, Dm.each((DmList)second.req_one_access).Cast<string>().ToArray());
            Assert.Null(second.req_access);
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void TwoAllHelpersAddUpAndTheDoorThenNeedsBoth()
    {
        // `airlock.req_access += access_list` appends (access_helpers.dm:20)
        obj_machinery_door_airlock door = null;
        var room = T1.Room(mapExtras: w =>
        {
            door = Dm.@new<obj_machinery_door_airlock_public>(locate(4, 4, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_access_all_engineering_general>(locate(4, 4, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_access_all_science_robotics>(locate(4, 4, 1));
        });
        var human = T1.Human(room, 4, 3);
        T1.Access(room, human, ACCESS_ENGINEERING);
        room.In(() =>
        {
            Assert.Equal(new[] { ACCESS_ENGINEERING, ACCESS_ROBOTICS }, Dm.each((DmList)door.req_access).Cast<string>().ToArray());
            Assert.False(Dm.truthy(Waiting.NoSleep(door.allowed__proc(human))));
        });
        T1.Access(room, human, ACCESS_ENGINEERING, ACCESS_ROBOTICS);
        room.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(door.allowed__proc(human)))));
    }

    [Fact]
    public void AHelperMadeOutsideMapLoadDoesNotChangeTheAirlockAndRemovesItself()
    {
        // mapping_helpers.dm:136-139: `if(!mapload) log_mapping("... spawned outside of mapload!") return`; LateInitialize
        // (:158) still runs and qdels it
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_airlock_public>(room, 4, 4);
        var helper = T1.At<obj_effect_mapping_helpers_airlock_locked>(room, 4, 4);
        room.In(() =>
        {
            Assert.False(Dm.truthy(door.locked));
            Assert.True(QDELETED(helper));
        });
        T1.NoRuntimes(room);
    }
}

public class AirlockHelperPayloadTests
{
    [Fact]
    public void TheStateHelpersSetTheirFlagsWithoutTheBoltSoundOrSignals()
    {
        // locked :205 (locked = TRUE, "so that we can pretend it was always locked"), welded :326, cutaiwire :336,
        // autoname :343 (the flag), inaccessible :355, red_alert_access :367
        obj_machinery_door_airlock locked = null, welded = null, ai = null, inaccessible = null, red = null;
        obj_machinery_door_window windoor = null, redLocked = null;
        var room = T1.Room(mapExtras: w =>
        {
            locked = Dm.@new<obj_machinery_door_airlock_public>(locate(2, 2, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_locked>(locate(2, 2, 1));
            welded = Dm.@new<obj_machinery_door_airlock_public>(locate(3, 2, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_welded>(locate(3, 2, 1));
            ai = Dm.@new<obj_machinery_door_airlock_public>(locate(4, 2, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_cutaiwire>(locate(4, 2, 1));
            inaccessible = Dm.@new<obj_machinery_door_airlock_public>(locate(5, 2, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_inaccessible>(locate(5, 2, 1));
            red = Dm.@new<obj_machinery_door_airlock_public>(locate(6, 2, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_red_alert_access>(locate(6, 2, 1));
            windoor = Dm.@new<obj_machinery_door_window_left_directional_north>(locate(2, 4, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_red_alert_access>(locate(2, 4, 1)); // apply_to_windoors (:361)
            redLocked = Dm.@new<obj_machinery_door_window_left_directional_north>(locate(3, 4, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_locked>(locate(3, 4, 1)); // no airlock, apply_to_windoors FALSE: nothing
        });
        room.In(() =>
        {
            Assert.True(Dm.truthy(locked.locked));
            Assert.True(Dm.truthy(welded.welded));
            Assert.True(Dm.truthy(ai.cutAiWire));
            Assert.True(Dm.truthy(ai.wires.is_cut(WIRE_AI))); // LateInitialize: airlock.wires.cut(WIRE_AI) (:164)
            Assert.Equal(new[] { ACCESS_INACCESSIBLE }, Dm.each((DmList)inaccessible.req_access).Cast<string>().ToArray());
            Assert.True(Dm.truthy(red.red_alert_access));
            Assert.True(Dm.truthy(windoor.red_alert_access));
            Assert.False(Dm.truthy(redLocked.locked));
            // the airlock redraws after the late payload (airlock.update_appearance(), :172): the bolts light overlay exists
            Assert.True(Dm.truthy(locked.overlays.len));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void UnresHelpersToggleTheSidesWithXorAndSetTheLatch()
    {
        // unres :218: `airlock.unres_sides ^= dir; airlock.unres_latch = TRUE`; delayed :230 adds delayed_unres_open
        obj_machinery_door_airlock one = null, twice = null, delayed = null;
        var room = T1.Room(mapExtras: w =>
        {
            one = Dm.@new<obj_machinery_door_airlock_public>(locate(2, 2, 1));
            var u = Dm.@new<obj_effect_mapping_helpers_airlock_unres>(locate(2, 2, 1));
            u.dir = NORTH;
            twice = Dm.@new<obj_machinery_door_airlock_public>(locate(4, 2, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_unres>(locate(4, 2, 1)).dir = EAST;
            Dm.@new<obj_effect_mapping_helpers_airlock_unres>(locate(4, 2, 1)).dir = EAST;
            Dm.@new<obj_effect_mapping_helpers_airlock_unres>(locate(4, 2, 1)).dir = WEST;
            delayed = Dm.@new<obj_machinery_door_airlock_public>(locate(6, 2, 1));
            Dm.@new<obj_effect_mapping_helpers_airlock_unres_delayed>(locate(6, 2, 1)).dir = SOUTH;
        });
        room.In(() =>
        {
            Assert.True(Dm.eq(one.unres_sides, NORTH));
            Assert.True(Dm.truthy(one.unres_latch));
            Assert.True(Dm.eq(twice.unres_sides, WEST)); // EAST ^ EAST ^ WEST
            Assert.True(Dm.eq(delayed.unres_sides, SOUTH));
            Assert.True(Dm.truthy(delayed.delayed_unres_open));
            Assert.False(Dm.truthy(one.delayed_unres_open));
        });
        T1.NoRuntimes(room);
    }

    // The abandoned helper's weights (mapping_helpers.dm:243-257): a created helper that is not at map load keeps its
    // computed weights, readable after it deleted itself.
    [Fact]
    public void TheAbandonedHelperWeightsAreTgsAndNoWallZeroesTheWallOutcomes()
    {
        var room = T1.Room();
        var abandoned = T1.At<obj_effect_mapping_helpers_airlock_abandoned>(room, 3, 3);
        var nowall = T1.At<obj_effect_mapping_helpers_airlock_abandoned_nowall>(room, 5, 3);
        room.In(() =>
        {
            DmList w = (DmList)abandoned.weights;
            Assert.True(Dm.eq(w["wall"], 8));
            Assert.True(Dm.eq(w["falsewall"], 1));
            Assert.True(Dm.eq(w["locked_no_lights"], 2));
            Assert.True(Dm.eq(w["locked"], 4));
            Assert.True(Dm.eq(w["welded"], 8));
            Assert.True(Dm.eq(w["panel_open"], 8));
            Assert.True(Dm.eq(w["none"], 100 - 31)); // weights[ABANDON_NONE] = 100 - values_sum(weights)
            DmList n = (DmList)nowall.weights;
            Assert.True(Dm.eq(n["wall"], 0));
            Assert.True(Dm.eq(n["falsewall"], 0));
            Assert.True(Dm.eq(n["none"], 100 - 22));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void EachAbandonedOutcomeDoesWhatMappingHelpersDmSays()
    {
        obj_machinery_door_airlock noLight = null, lockedDoor = null, weldedDoor = null, panel = null, none = null;
        var room = T1.Room(mapExtras: w =>
        {
            noLight = Dm.@new<obj_machinery_door_airlock_public>(locate(2, 2, 1));
            Only("locked_no_lights", locate(2, 2, 1));
            lockedDoor = Dm.@new<obj_machinery_door_airlock_public>(locate(3, 2, 1));
            Only("locked", locate(3, 2, 1));
            weldedDoor = Dm.@new<obj_machinery_door_airlock_public>(locate(4, 2, 1));
            Only("welded", locate(4, 2, 1));
            panel = Dm.@new<obj_machinery_door_airlock_public>(locate(5, 2, 1));
            Only("panel_open", locate(5, 2, 1));
            none = Dm.@new<obj_machinery_door_airlock_public>(locate(6, 2, 1));
            Only("none", locate(6, 2, 1));
        });
        room.In(() =>
        {
            Assert.False(Dm.truthy(noLight.feedback)); // :267
            Assert.True(Dm.truthy(noLight.locked));
            Assert.True(Dm.truthy(lockedDoor.locked));
            Assert.True(Dm.truthy(lockedDoor.feedback));
            Assert.True(Dm.truthy(weldedDoor.welded));
            Assert.False(Dm.truthy(weldedDoor.locked));
            Assert.True(Dm.truthy(panel.panel_open)); // :275 set_panel_open(TRUE)
            Assert.False(Dm.truthy(none.locked) || Dm.truthy(none.welded) || Dm.truthy(none.panel_open));
            foreach (var d in new[] { noLight, lockedDoor, weldedDoor, panel, none }) Assert.False(QDELETED(d));
        });
        T1.NoRuntimes(room);
    }

    private static void Only(string outcome, turf at)
    {
        var helper = Dm.@new<obj_effect_mapping_helpers_airlock_abandoned>(at);
        helper.weights = Dm.list(Dm.kv(outcome, 100));
    }
}
