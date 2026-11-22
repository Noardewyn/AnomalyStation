using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tests.Tg.Wave4.Integration;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M0Acceptance;

/// <summary>
/// The world every M0 acceptance test runs on (ROADMAP M0, "Done when"): a BOOTED world, not a fixture one. The boot is the
/// real one: <see cref="TgWorld.Boot"/> runs Genesis, <c>world/New</c> and the Master's initialization on a fresh
/// <see cref="World"/>, the toy room (<see cref="ToyRoom"/>) is its compiled-map step, the Master initializes every
/// subsystem at tick 20, and the clock stands at tick 30 with the run level RUNLEVEL_GAME (the SSticker stand-in), so that the
/// ticker subsystems (SStimed_actions, SStimer, SSgarbage, SSobj, ...) fire every tick from here on. All of that is
/// <see cref="TgToyRoom.Boot"/>'s; this class adds what a scenario needs on top: a player command phase that runs scripted
/// commands at chosen ticks (BYOND's step 6, after the sleepers and the Master woke, waiting-model.md 7.1) and the short forms
/// the three clauses share. Nothing here moves an atom by any route but tg's own procs (<c>step</c>, <c>forceMove</c>,
/// <c>do_after</c>, <c>qdel</c>).
/// <para>
/// Ticks are counted from <see cref="T"/>, the tick of the first command phase after the boot: <c>At(0, ...)</c> runs in tick
/// T, after the Master fired in it. One second is 10 ds, which is 20 ticks of 0.5 ds.
/// </para>
/// </summary>
internal sealed class BootedToyWorld
{
    public readonly TgToyRoom Room;

    /// <summary>The tick of the first command phase after the boot.</summary>
    public readonly long T;

    private readonly SortedDictionary<long, List<Action>> _script = new();

    /// <summary>
    /// Boots a world of seed <paramref name="seed"/>. <paramref name="probe"/>, when given, is a phase that runs in every tick of
    /// the world, boot ticks included, after the player commands of that tick: the determinism tests hash the state there.
    /// </summary>
    public BootedToyWorld(ulong seed = 7, Action<World> probe = null)
    {
        // The phases are registered as part of the boot (the toy room's map step runs inside it), so that they exist from tick 1.
        Room = TgToyRoom.Boot(seed: seed, mapExtras: w =>
        {
            w.AddPhase("player commands", RunCommands);
            if (probe != null)
                w.AddPhase("state probe", probe);
        });   // Genesis, world/New, the real Master, the toy room, tick 30, RUNLEVEL_GAME
        T = World.Tick + 1;
    }

    private void RunCommands(World w)
    {
        if (_script.TryGetValue(w.Tick, out List<Action> commands))
        {
            foreach (Action command in commands)
                command();
        }
    }

    public World World => Room.World;

    /// <summary>The runtimes the world reported (its runtime error sink): every acceptance scenario ends with none.</summary>
    public List<Exception> Runtimes => Room.Runtimes;

    /// <summary>Runs <paramref name="command"/> in the command phase of tick T + <paramref name="offset"/>.</summary>
    public void At(long offset, Action command)
    {
        if (!_script.TryGetValue(T + offset, out List<Action> commands))
            _script[T + offset] = commands = new List<Action>();
        commands.Add(command);
    }

    /// <summary>Steps the world until tick T + <paramref name="offset"/> has run (a no-op when it already has).</summary>
    public void RunTo(long offset)
    {
        while (World.Tick < T + offset)
            World.Step();
    }

    /// <summary>The tick of T + <paramref name="offset"/>, for the traces that stamp their lines with it.</summary>
    public long Tick(long offset) => T + offset;

    public void In(Action body) => Room.In(body);

    public TResult In<TResult>(Func<TResult> body) => Room.In(body);

    // ---- the scene ----

    /// <summary>
    /// A real <c>/mob</c> named <paramref name="name"/> at (x, y) of level 1 (a <c>new</c> after SSatoms initialized runs its
    /// Initialize at once). Call inside the world's scope, or from a command.
    /// </summary>
    public static mob Mob(string name, int x, int y)
    {
        var m = Dm.@new<mob>(locate(x, y, 1));
        m.name = name;
        return m;
    }

    /// <summary>A real <c>/obj</c> named <paramref name="name"/> at (x, y), dense when asked.</summary>
    public static obj Obj(string name, int x, int y, bool dense = false) => Toy.Thing(name, locate(x, y, 1), dense);

    /// <summary>
    /// Splits the room in two areas, so that a step along x crosses an area border: the columns from <paramref name="fromX"/>
    /// on move into a second <c>/area/misc/testroom</c>, named "annex", with tg's own <c>turf/change_area</c> (the map loader's
    /// call, reader.dm:929-937); the room keeps the name "room". Returns the annex. Call before the atoms are made.
    /// </summary>
    public area Annex(int fromX = 4)
    {
        var annex = Dm.@new<area_misc_testroom>();
        annex.name = "annex";
        Room.Room.name = "room";
        foreach (turf t in Dm.each<turf>(block(fromX, 1, 1, Room.Width, Room.Height, 1)))
            t.change_area((area)t.loc, annex);
        return annex;
    }

    // ---- what a booted world is ----

    /// <summary>
    /// Whether the Master is the real one and running: Genesis made it, every subsystem stage is initialized, and a ticker
    /// subsystem has fired in the ticks since the run level was set. Reads state only.
    /// </summary>
    public bool MasterIsRunning()
    {
        return In(() => Master != null
            && Dm.eq(datum_controller_master.init_stage_completed, INITSTAGE_MAX)
            && SStimed_actions != null
            && SStimed_actions.times_fired > 0);
    }
}
