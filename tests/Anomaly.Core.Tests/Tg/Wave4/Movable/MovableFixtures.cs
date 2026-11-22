using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg.Wave3.Engine;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Movable;

// Fixtures of package A's smoke tests (wave 4, the Move chain). Fixture paths are /<kind>/test_movable_<name>
// (translation-rules.md 15). Their New does not call ..(), so the chain runs without tg's /atom/New (SSatoms). What they
// record goes to the per-world EngineTrace of the wave-3 engine tests.

/// <summary>
/// A map of fixture floors in a room with gravity, as the Move chain needs it: the logger and the garbage and timer
/// subsystems (qdel, GLOB's QDEL_IN), GLOB with tg's InitGlobal procs (angle2dir reads GLOB.modulo_angle_to_dir, the
/// containers' Entered reads nothing of it but the lists exist for every caller), SSmapping with its per-z gravity list grown
/// (has_gravity, plan "After W4.0"), SSchat and the configuration. NotTranslatedThrows stays on: since package D landed,
/// nothing the chain reaches is a skipped skeleton, so a reached NotTranslated is a failure.
/// </summary>
internal static class MovableWorld
{
    public static EngineWorld Create(int maxx = 3, int maxy = 3, int maxz = 1, Type floor = null, Type room = null, ulong seed = 7)
    {
        var map = new EngineWorld(maxx, maxy, maxz, floor ?? typeof(turf_test_movable_floor), room ?? typeof(area_misc_testroom_test_movable_room), seed);
        using (map.World.Enter())
        {
            TgTestGenesis.Install(); // the logger (Genesis), SSchat and the configuration
            Dm.@new<datum_controller_subsystem_garbage>(); // qdel reads SSgarbage (garbage.dm:346)
            Dm.@new<datum_controller_subsystem_timer>(); // GLOB's New queues a QDEL_IN (globals.dm:24)
            Dm.@new<datum_controller_subsystem_materials>(); // GLOB's make_datum_reference_lists reads its recipes (global_lists.dm:93)
            Dm.@new<datum_controller_subsystem_accessories>(); // M10x slice D2: tg makes it at PreInit, before GLOB (sprite_accessories.dm:47); bodies read its lists
            Dm.@new<datum_controller_global_vars>(); // as Master/New creates it (master.dm:128)
            Dm.@new<datum_controller_subsystem_mapping>(); // NEW_SS_GLOBAL(SSmapping)
            Dm.@new<datum_controller_subsystem_spatial_grid>(); // NEW_SS_GLOBAL(SSspatial_grid): hearing sensitivity reads it
            Dm.@new<datum_controller_subsystem_processing_obj>(); // NEW_SS_GLOBAL(SSobj): /obj/Destroy stops processing on it (objs.dm:73)
            SSmapping.gravity_by_z_level.len = Dm.world.maxz; // as manage_z_level grows it (mapping.dm:756)
            ToyRoom.LinkLevels((int)Dm.world.maxz);
        }
        return map;
    }

    /// <summary>
    /// Links the z-levels as tg's <c>manage_z_level</c> does for stacked levels (mapping.dm:230-239): every level has a level
    /// above it and below it except the top and the bottom one. The world must be entered.
    /// </summary>
    public static void LinkZLevels()
    {
        int maxz = (int)Dm.world.maxz;
        SSmapping.multiz_levels.len = maxz;
        for (int z = 1; z <= maxz; z++)
        {
            var levels = Dm.list_n(Defines.LARGEST_Z_LEVEL_INDEX);
            levels[(float)Defines.Z_LEVEL_UP] = z < maxz ? Dm.TRUE : Dm.FALSE;
            levels[(float)Defines.Z_LEVEL_DOWN] = z > 1 ? Dm.TRUE : Dm.FALSE;
            SSmapping.multiz_levels[(float)z] = levels;
        }
    }

    /// <summary>A new area of <typeparamref name="T"/> that takes <paramref name="first"/> and the other turfs (BYOND: a new area at a turf holds it).</summary>
    public static T NewArea<T>(turf first, params turf[] more) where T : area
    {
        var room = Dm.@new<T>(first);
        foreach (turf t in more)
            Dm.append(room.contents, t);
        return room;
    }

    /// <summary>The world raised no runtime error: nothing of the chain reached a skipped skeleton or failed.</summary>
    public static void AssertNoRuntimes(EngineWorld map) => Assert.Empty(map.Runtimes);
}

/// <summary>tg's /area/misc/testroom (STANDARD_GRAVITY) without tg's /area/New.</summary>
[DmPath("/area/misc/testroom/test_movable_room")]
public class area_misc_testroom_test_movable_room : area_misc_testroom
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>A second room, for moves that change area.</summary>
[DmPath("/area/misc/testroom/test_movable_room/other")]
public class area_misc_testroom_test_movable_room_other : area_misc_testroom_test_movable_room
{
}

/// <summary>
/// A floor whose Enter records the call and returns <see cref="enter_result"/>: it stands in for package B's /turf/Enter
/// (turf.dm:434), so the chain is tested alone (BYOND's builtin turf Enter, Cross of every occupant, is not tg's).
/// </summary>
[DmPath("/turf/test_movable_floor")]
public class turf_test_movable_floor : turf
{
    [NotHashed]
    public object enter_result = Dm.TRUE;

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public override async DmTask<object> Enter(atom_movable O = null, atom oldloc = null)
    {
        EngineTrace.Add("Enter " + EngineTrace.D(this) + " " + EngineTrace.D(O));
        return enter_result;
    }
}

/// <summary>A plain movable obj without tg's /atom/New.</summary>
[DmPath("/obj/test_movable_thing")]
public class obj_test_movable_thing : obj
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>A dense obj without tg's /atom/New.</summary>
[DmPath("/obj/test_movable_block")]
public class obj_test_movable_block : obj
{
    public obj_test_movable_block()
    {
        density = Dm.TRUE;
    }

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>A plain mob without tg's /atom/New (only mobs pull in tg: /atom/movable/var/mob/pulledby).</summary>
[DmPath("/mob/test_movable_mob")]
public class mob_test_movable_mob : mob
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>A living mob without tg's /atom/New (some pulling code is typed /mob/living: pulled_mob_stat_change, Move_Pulled).</summary>
[DmPath("/mob/living/test_movable_living")]
public class mob_living_test_movable_living : mob_living
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>
/// tg's /obj/movement_tester (modules/unit_tests/movement_order_sanity.dm:24-38): logs Move, doMove and Moved in tg's words.
/// </summary>
[DmPath("/obj/test_movable_logger")]
public class obj_test_movable_logger : obj
{
    [NotHashed]
    public readonly List<string> movement_order = new();

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    private static string At(atom a) => a != null ? Dm.text("([], [])", a.x, a.y) : "NULL";

    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        movement_order.Add(Dm.text("Moving from ([], []) to []", loc.x, loc.y, At(newloc)));
        return await base.Move(newloc, direct, glide_size_override, update_dir);
    }

    public override async DmTask<object> doMove(atom destination = null)
    {
        movement_order.Add(Dm.text("Abstractly Moving from ([], []) to []", loc.x, loc.y, At(destination)));
        return await base.doMove(destination);
    }

    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        movement_order.Add(Dm.text("Moved from ([], []) to []", old_loc.x, old_loc.y, At(loc)));
        return await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
    }
}

/// <summary>
/// A signal listener: records "label source a b c" and returns <see cref="result"/>; <see cref="act"/>, when set, runs
/// after the record (a handler that moves or deletes something).
/// </summary>
[DmPath("/datum/test_movable_listener")]
public class datum_test_movable_listener : datum
{
    [NotHashed]
    public string label;
    [NotHashed]
    public object result = Defines.NONE;
    [NotHashed]
    public Action<object, object> act;

    [SignalHandler]
    public virtual object on_signal(datum source = null, object a = null, object b = null, object c = null)
    {
        EngineTrace.Add(label + " " + EngineTrace.D(source) + " " + EngineTrace.D(a) + " " + EngineTrace.D(b) + " " + EngineTrace.D(c));
        act?.Invoke(source, a);
        return result;
    }

    /// <summary>Creates a listener for <paramref name="signal"/> on <paramref name="target"/>.</summary>
    public static datum_test_movable_listener On(datum target, string signal, string label, object result = null, Action<object, object> act = null)
    {
        var l = Dm.@new<datum_test_movable_listener>();
        l.label = label;
        l.result = result ?? Defines.NONE;
        l.act = act;
        l.RegisterSignal(target, signal, nameof(on_signal));
        return l;
    }
}

// ---- fixtures of the independent test-writer (areas, floors with tg's own Enter, sleeping overrides) ----

/// <summary>
/// An area whose Entered and Exited record their call, as tg's /area/Entered and /area/Exited are called by Move and
/// doMove (areas.dm:581-606, package B): they never call ..(), so no COMSIG_ATOM_ENTERED is sent for an area. The
/// recorded line is "area_entered|area_exited &lt;area&gt; &lt;mover&gt; &lt;old area or direction&gt;".
/// </summary>
[DmPath("/area/misc/testroom/test_movable_room/recording")]
public class area_misc_testroom_test_movable_room_recording : area_misc_testroom_test_movable_room
{
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        EngineTrace.Add("area_entered " + EngineTrace.D(this) + " " + EngineTrace.D(arrived) + " " + EngineTrace.D(old_loc));
        return null;
    }

    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        EngineTrace.Add("area_exited " + EngineTrace.D(this) + " " + EngineTrace.D(gone) + " " + EngineTrace.D(direction));
        return null;
    }
}

/// <summary>A second recording area, so that a move changes area.</summary>
[DmPath("/area/misc/testroom/test_movable_room/recording/other")]
public class area_misc_testroom_test_movable_room_recording_other : area_misc_testroom_test_movable_room_recording
{
}

/// <summary>A third recording area.</summary>
[DmPath("/area/misc/testroom/test_movable_room/recording/third")]
public class area_misc_testroom_test_movable_room_recording_third : area_misc_testroom_test_movable_room_recording
{
}

/// <summary>
/// tg's /area/Entered declares <c>set waitfor = FALSE</c> (areas.dm:582): this recording room's Entered is that wrapper
/// (waiting-model.md 5.4, "in a slot that suspends its callers the wrapper is an async method that returns a completed
/// task") over a body that sleeps for <see cref="sleep_ds"/> after recording its start.
/// </summary>
[DmPath("/area/misc/testroom/test_movable_room/sleepy")]
public class area_misc_testroom_test_movable_room_sleepy : area_misc_testroom_test_movable_room
{
    [NotHashed]
    public float sleep_ds = 10;

    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(Entered__body(arrived, old_loc, old_locs, dot), dot);
    }

    private async DmTask<object> Entered__body(atom_movable arrived, atom old_loc, DmList old_locs, DotBox<object> dot)
    {
        dot.Value = null;
        EngineTrace.Add("sleepy_area_entered start " + EngineTrace.D(arrived));
        await Dm.sleep(sleep_ds);
        EngineTrace.Add("sleepy_area_entered woke " + EngineTrace.D(arrived));
        return dot.Value;
    }
}

/// <summary>
/// A floor with tg's own /turf/Enter (turf.dm:434-466: CanPass, Cross of every occupant, Bump of the first blocker) over
/// the fixture floor's recording: package B translates the real one; with this copy the Move chain is tested against the
/// Bump logic it was written for (Bump from inside Enter, before the point of no return). tg's code is copied
/// literally; W4.M reruns the same cases on the real type (Integration/MoveChainT4Tests.cs and MoveChainT4MoreTests.cs).
/// </summary>
[DmPath("/turf/test_movable_floor/tg_enter")]
public class turf_test_movable_floor_tg_enter : turf_test_movable_floor
{
    // tg: code/game/turfs/turf.dm:434 /turf/Enter
    public override async DmTask<object> Enter(atom_movable mover = null, atom oldloc = null)
    {
        EngineTrace.Add("Enter " + EngineTrace.D(this) + " " + EngineTrace.D(mover));
        atom first_bump = null;
        object can_pass_self = await CanPass(mover, Builtins.get_dir(this, mover));

        if (Dm.truthy(can_pass_self))
        {
            atom mover_loc = mover.loc;
            object mover_is_phasing = Dm.bitand(mover.movement_type, Defines.PHASING);
            foreach (atom_movable thing in Dm.each_cast<atom_movable>(contents))
            {
                if (thing == mover || thing == mover_loc) // Multi tile objects and moving out of other objects
                    continue;
                if (!Dm.truthy(await thing.Cross(mover)))
                {
                    if (GlobalProcs.QDELETED(mover)) //deleted from Cross()
                        return Dm.FALSE;
                    if (Dm.truthy(mover_is_phasing))
                    {
                        await mover.Bump(thing);
                        if (GlobalProcs.QDELETED(mover)) //deleted from Bump()
                            return Dm.FALSE;
                        continue;
                    }
                    else
                    {
                        if (first_bump == null || ((thing.layer > first_bump.layer || Dm.truthy(Dm.bitand(thing.flags_1, Defines.ON_BORDER_1))) && !Dm.truthy(Dm.bitand(first_bump.flags_1, Defines.ON_BORDER_1))))
                            first_bump = thing;
                    }
                }
            }
        }
        if (GlobalProcs.QDELETED(mover)) //Mover deleted from Cross/CanPass/Bump, do not proceed.
            return Dm.FALSE;
        if (!Dm.truthy(can_pass_self)) //Even if mover is unstoppable they need to bump us.
            first_bump = this;
        if (first_bump != null)
        {
            await mover.Bump(first_bump);
            return Dm.bitand(mover.movement_type, Defines.PHASING);
        }
        return Dm.TRUE;
    }
}

/// <summary>
/// A floor that stands in for the z-pass procs of package B (turf.dm:333-346, not merged when this fixture was written): both answer
/// <see cref="z_pass"/>. tg's own answer depends on obj_flags and the open space types.
/// </summary>
[DmPath("/turf/test_movable_floor/zpass")]
public class turf_test_movable_floor_zpass : turf_test_movable_floor
{
    [NotHashed]
    public object z_pass = Dm.TRUE;

    public override object zPassIn(object direction = null) => z_pass;

    public override object zPassOut(object direction = null) => z_pass;
}

/// <summary>A floor whose Exited sleeps (any override of a DmTask slot may sleep; the chain waits for it).</summary>
[DmPath("/turf/test_movable_floor/sleepy_exit")]
public class turf_test_movable_floor_sleepy_exit : turf_test_movable_floor
{
    [NotHashed]
    public float sleep_ds = 5;

    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        EngineTrace.Add("sleepy_exited start " + EngineTrace.D(gone));
        await Dm.sleep(sleep_ds);
        EngineTrace.Add("sleepy_exited woke " + EngineTrace.D(gone));
        return dot;
    }
}

/// <summary>A dense obj with a name, for Bump order tests: the layer decides which blocker is bumped.</summary>
[DmPath("/obj/test_movable_block/high")]
public class obj_test_movable_block_high : obj_test_movable_block
{
    public obj_test_movable_block_high()
    {
        layer = 5;
    }
}

/// <summary>A dense obj that stands on the border of its tile (ON_BORDER_1, like a directional window).</summary>
[DmPath("/obj/test_movable_block/border")]
public class obj_test_movable_block_border : obj_test_movable_block
{
    public obj_test_movable_block_border()
    {
        Dm.combine(ref flags_1, Defines.ON_BORDER_1);
    }
}

/// <summary>A movable that is not an obj or a mob (tg has many: screen objects, effects), for the base type's own behaviour.</summary>
[DmPath("/atom/movable/test_movable_effect")]
public class atom_movable_test_movable_effect : atom_movable
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>An obj that records every Moved it receives (call order, arguments), as a hook of the base chain.</summary>
[DmPath("/obj/test_movable_witness")]
public class obj_test_movable_witness : obj
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        EngineTrace.Add("witness_move " + EngineTrace.D(this) + " " + EngineTrace.D(newloc) + " " + EngineTrace.D(direct));
        return await base.Move(newloc, direct, glide_size_override, update_dir);
    }

    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= Dm.TRUE; // the root's default (atoms_movable.dm:896), which an override that redeclares nothing sees as null
        EngineTrace.Add("witness_moved " + EngineTrace.D(this) + " " + EngineTrace.D(old_loc) + " " + EngineTrace.D(movement_dir) + " " + EngineTrace.D(forced) + " " + EngineTrace.D(old_locs) + " " + EngineTrace.D(momentum_change));
        return await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
    }
}

/// <summary>A dense table: its pass_flags_self let thrown things and clicks pass (the LETPASSTHROW of ClickCross, adjacent.dm:109).</summary>
[DmPath("/obj/test_movable_table")]
public class obj_test_movable_table : obj
{
    public obj_test_movable_table()
    {
        density = Dm.TRUE;
        pass_flags_self = Defines.LETPASSTHROW;
    }

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>An item without tg's /atom/New, for /obj/item/Adjacent (adjacent.dm:81).</summary>
[DmPath("/obj/item/test_movable_item")]
public class obj_item_test_movable_item : obj_item
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>The target of a movement_detector callback: records what the detector passes (datums/movement_detector.dm:53).</summary>
[DmPath("/datum/test_movable_recorder")]
public class datum_test_movable_recorder : datum
{
    public virtual void on_tracked_moved(object tracked = null, object mover = null, object oldloc = null, object direction = null)
    {
        EngineTrace.Add("tracked_moved " + EngineTrace.D(tracked) + " " + EngineTrace.D(mover) + " " + EngineTrace.D(oldloc) + " " + EngineTrace.D(direction));
    }
}

/// <summary>A signal listener that sees all five arguments of COMSIG_MOVABLE_MOVED: "label source a b c d e".</summary>
[DmPath("/datum/test_movable_wide_listener")]
public class datum_test_movable_wide_listener : datum
{
    [NotHashed]
    public string label;

    [SignalHandler]
    public virtual object on_signal(datum source = null, object a = null, object b = null, object c = null, object d = null, object e = null)
    {
        EngineTrace.Add(label + " " + EngineTrace.D(source) + " " + EngineTrace.D(a) + " " + EngineTrace.D(b) + " " + EngineTrace.D(c) + " " + EngineTrace.D(d) + " " + EngineTrace.D(e));
        return Defines.NONE;
    }

    public static datum_test_movable_wide_listener On(datum target, string signal, string label)
    {
        var l = Dm.@new<datum_test_movable_wide_listener>();
        l.label = label;
        l.RegisterSignal(target, signal, nameof(on_signal));
        return l;
    }
}

/// <summary>A chair that records unbuckle_mob (buckling.dm:157 is M2's: the base procs are NotTranslated until then).</summary>
[DmPath("/obj/test_movable_chair")]
public class obj_test_movable_chair : obj
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public override async DmTask<object> unbuckle_mob(mob_living buckled_mob = null, float? force = null, float? can_fall = null)
    {
        EngineTrace.Add("unbuckle_mob " + EngineTrace.D(this) + " " + EngineTrace.D(buckled_mob) + " " + EngineTrace.D(force));
        return null;
    }
}

/// <summary>A drift handler without tg's New (zero-gravity drift is M4): only a holder for Destroy's QDEL_NULL.</summary>
[DmPath("/datum/drift_handler/test_movable")]
public class datum_drift_handler_test_movable : datum_drift_handler
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}
