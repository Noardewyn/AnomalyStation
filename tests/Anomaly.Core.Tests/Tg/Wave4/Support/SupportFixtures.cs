using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg.Boot;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Support;

// Fixtures of the support package's behaviour tests (wave 4, package D: connect_loc, movement_detector, the movespeed and
// actionspeed modifiers, mob_lists, string_list, get_nested_locs, the multiz and direction helpers), written from tg's DM.
// Fixture paths are /<kind>/test_support_<name> (translation-rules.md 15), which no tg path starts with. The atom fixtures'
// New does not call ..(), so these tests run without tg's /atom/New (SSatoms) and without the other wave-4 packages'
// Initialize and Destroy. What they record goes to a per-world trace (SupportTrace), never to a static.

/// <summary>The per-world recording harness of the support tests (translation-rules.md 15): lines in call order.</summary>
internal sealed class SupportTrace
{
    public readonly List<string> Lines = new();

    public static SupportTrace Of(World world) => world.GetExtension(static _ => new SupportTrace());

    /// <summary>Appends to the trace of the ambient world.</summary>
    public static void Add(string line) => Of(World.Current).Lines.Add(line);

    /// <summary>A value as the trace writes it: C# type and world id (RecordingSinks' form), null as "null".</summary>
    public static string D(object value) => RecordingSinks.Describe(value);
}

/// <summary>
/// A world as tg's world start leaves it for these procs: the logger, SSgarbage, SStimer and GLOB with its InitGlobal
/// procs run (BootPieces), SSdcs (elements, global signals), SSmapping (the multiz helpers) and the SSatoms stand-in, plus
/// an optional map of <c>/turf/test_support_floor</c> in one <c>/area/test_support_room</c>. The scope stays entered
/// until <see cref="Dispose"/>.
/// </summary>
internal sealed class SupportWorld : IDisposable
{
    private readonly BootPieces _boot;

    public SupportWorld(int maxx = 3, int maxy = 3, int maxz = 1, ulong seed = 1)
    {
        _boot = new BootPieces(seed);
        Dm.@new<datum_controller_subsystem_processing_dcs>();
        Dm.@new<datum_controller_subsystem_mapping>();
        AtomsStandIn.Install();
        MapGrid.Attach(World);
        SSmapping.gravity_by_z_level.len = maxz;   // as manage_z_level grows it (mapping.dm:756): has_gravity reads it on every Move
        ToyRoom.LinkLevels(maxz);
        if (maxx > 0)
        {
            Dm.world.turf = typeof(turf_test_support_floor);
            Dm.world.area = typeof(area_test_support_room);
            Dm.world.maxx = maxx;
            Dm.world.maxy = maxy;
            Dm.world.maxz = maxz;
        }
    }

    public World World => _boot.World;

    public List<Exception> Runtimes => _boot.Runtimes;

    public List<string> Trace => SupportTrace.Of(World).Lines;

    public DmMemoryFiles Files => _boot.Files;

    public void Dispose() => _boot.Dispose();
}

/// <summary>
/// Movement through package A's real chain (W4.M; wave 4 merged D first and these tests moved things by hand until A landed):
/// <c>Move</c> for a step to a neighbouring turf or container (tg's own <c>/turf/Enter</c>, <c>Exit</c>, <c>Exited</c>,
/// <c>Entered</c> and <c>Moved</c>, which sends <c>COMSIG_MOVABLE_MOVED</c>) and <c>forceMove</c> for a teleport
/// (<c>moveToNullspace</c> for a null destination, which tg's <c>forceMove</c> refuses with a CRASH).
/// </summary>
internal static class SupportMoves
{
    /// <summary>tg's forceMove (atoms_movable.dm:1159): Moved(oldloc, NONE, TRUE, null); a null destination is moveToNullspace.</summary>
    public static void ForceMove(atom_movable mover, atom destination)
    {
        if (destination == null)
            Waiting.NoSleep(mover.moveToNullspace());
        else
            Waiting.NoSleep(mover.forceMove(destination));
    }

    /// <summary>A real <c>Move(destination, direction)</c>: Moved(oldloc, direction, FALSE, null, TRUE).</summary>
    public static void Move(atom_movable mover, atom destination, object direction)
        => Waiting.NoSleep(mover.Move(destination, direction));
}

[DmPath("/turf/test_support_floor")]
public class turf_test_support_floor : turf
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

[DmPath("/area/test_support_room")]
public class area_test_support_room : area
{
    // a room with gravity (like /area/misc/testroom): the real Move reaches has_gravity, and a bare /area has none
    public area_test_support_room()
    {
        default_gravity = STANDARD_GRAVITY;
    }

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>A container: an obj other movables are put into.</summary>
[DmPath("/obj/test_support_box")]
public class obj_test_support_box : obj
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>An obj whose signal handler counts its calls and records the source (connect_loc's listener).</summary>
[DmPath("/obj/test_support_listener")]
public class obj_test_support_listener : obj
{
    public object times_called;

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    [SignalHandler]
    public virtual void on_mock(datum source = null)
    {
        Dm.append(ref times_called, 1);
        SupportTrace.Add("mock " + SupportTrace.D(this) + " from " + SupportTrace.D(source));
    }
}

/// <summary>A plain mob without tg's /mob/New and /mob/Initialize (package C).</summary>
[DmPath("/mob/test_support_mob")]
public class mob_test_support_mob : mob
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;
}

/// <summary>The other mob type, for the per-type config speed lookup.</summary>
[DmPath("/mob/test_support_mob/other")]
public class mob_test_support_mob_other : mob_test_support_mob
{
}

/// <summary>The target of a movement_detector's callback: records what the detector passes.</summary>
[DmPath("/datum/test_support_recorder")]
public class datum_test_support_recorder : datum
{
    public virtual void on_tracked_moved(object tracked = null, object mover = null, object oldloc = null, object direction = null)
    {
        SupportTrace.Add("moved tracked=" + SupportTrace.D(tracked) + " mover=" + SupportTrace.D(mover) + " oldloc=" + SupportTrace.D(oldloc) + " dir=" + SupportTrace.D(direction));
    }
}

// The movespeed modifier fixtures (tg's own modifiers set the same vars in their type blocks, movespeed/modifiers/*.dm).

[DmPath("/datum/movespeed_modifier/test_support_slow")]
public class datum_movespeed_modifier_test_support_slow : datum_movespeed_modifier
{
    public datum_movespeed_modifier_test_support_slow()
    {
        multiplicative_slowdown = 2;
    }
}

[DmPath("/datum/movespeed_modifier/test_support_fast")]
public class datum_movespeed_modifier_test_support_fast : datum_movespeed_modifier
{
    public datum_movespeed_modifier_test_support_fast()
    {
        multiplicative_slowdown = -0.5f;
    }
}

[DmPath("/datum/movespeed_modifier/test_support_flying")]
public class datum_movespeed_modifier_test_support_flying : datum_movespeed_modifier
{
    public datum_movespeed_modifier_test_support_flying()
    {
        multiplicative_slowdown = 4;
        movetypes = FLYING;
    }
}

[DmPath("/datum/movespeed_modifier/test_support_not_on_ground")]
public class datum_movespeed_modifier_test_support_not_on_ground : datum_movespeed_modifier
{
    public datum_movespeed_modifier_test_support_not_on_ground()
    {
        multiplicative_slowdown = 8;
        blacklisted_movetypes = GROUND;
    }
}

[DmPath("/datum/movespeed_modifier/test_support_named")]
public class datum_movespeed_modifier_test_support_named : datum_movespeed_modifier
{
    public datum_movespeed_modifier_test_support_named()
    {
        id = "named_mod";
        multiplicative_slowdown = 1;
    }
}

[DmPath("/datum/movespeed_modifier/test_support_variable")]
public class datum_movespeed_modifier_test_support_variable : datum_movespeed_modifier
{
    public datum_movespeed_modifier_test_support_variable()
    {
        variable = Dm.TRUE;
    }
}

[DmPath("/datum/actionspeed_modifier/test_support_slow")]
public class datum_actionspeed_modifier_test_support_slow : datum_actionspeed_modifier
{
    public datum_actionspeed_modifier_test_support_slow()
    {
        multiplicative_slowdown = 0.5f;
    }
}

[DmPath("/datum/actionspeed_modifier/test_support_variable")]
public class datum_actionspeed_modifier_test_support_variable : datum_actionspeed_modifier
{
    public datum_actionspeed_modifier_test_support_variable()
    {
        variable = Dm.TRUE;
    }
}
