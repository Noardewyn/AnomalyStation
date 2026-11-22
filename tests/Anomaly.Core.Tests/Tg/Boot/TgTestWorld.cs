using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Boot;

/// <summary>
/// Booted tg worlds for tests (wave 3, boot package): the only supported way to start the Master Controller in a test
/// (scope risk R13: the MC reads the configuration every iteration, so it must start the way tg starts it).
/// <see cref="Start"/> runs the bootstrap (<see cref="TgWorld.Boot"/>) and steps nothing; <see cref="Boot"/> also steps the
/// world until the Master has initialized every subsystem stage. <see cref="RunLevel"/> stands in for SSticker, which is an
/// inert stub until M10: tg's ticker moves the MC from the lobby to the game runlevel (plan.md, scope risk R6).
/// </summary>
public static class TgTestWorld
{
    /// <summary>Lines the world wrote to <c>world.log</c>, per world (DmWorldState.WorldLog).</summary>
    public sealed class Log
    {
        public readonly List<string> Lines = new();
        public readonly List<Exception> Runtimes = new();
    }

    /// <summary>
    /// A fresh world (seed <paramref name="seed"/>) with recording seams, booted (<see cref="TgWorld.Boot"/>) but not stepped:
    /// world/New has run up to the first sleep of Master.Initialize. <paramref name="configure"/> runs before the boot (file
    /// seam contents, the subsystem-type filter, NotTranslatedThrows); <paramref name="compiledMap"/> is the boot's map step.
    /// </summary>
    public static World Start(ulong seed = 1, Action<World> configure = null, Action<World> compiledMap = null)
        => Start(out _, seed, configure, compiledMap);

    /// <summary><see cref="Start(ulong, Action{World}, Action{World})"/>, also returning the world log and the runtimes.</summary>
    public static World Start(out Log log, ulong seed = 1, Action<World> configure = null, Action<World> compiledMap = null)
    {
        var record = new Log();
        var world = new World(seed) { RuntimeErrorSink = record.Runtimes.Add };
        DmWorldState.Of(world).WorldLog = record.Lines.Add;
        M4bAudio.TestSoundLengths.Install(world); // the host's sound length table (M4b audio slice 5); a configure may replace it
        // tg's strings/ files, as the host mounts them (M10 slice F: the ticker's Initialize and the station name read them)
        if (Tools.TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(world).Files, Tools.TgClone.Root);
        configure?.Invoke(world);
        TgWorld.Boot(world, compiledMap);
        log = record;
        return world;
    }

    /// <summary>
    /// <see cref="Start(ulong, Action{World}, Action{World})"/>, then steps the world until the Master has initialized every
    /// stage (<c>init_stage_completed == INITSTAGE_MAX</c>, controllers/master.dm:446), at most <paramref name="maxTicks"/>
    /// ticks; throws when it does not get there.
    /// </summary>
    public static World Boot(ulong seed = 1, Action<World> configure = null, Action<World> compiledMap = null, long maxTicks = 200)
        => Boot(out _, seed, configure, compiledMap, maxTicks);

    /// <summary><see cref="Boot(ulong, Action{World}, Action{World}, long)"/>, also returning the world log and the runtimes.</summary>
    public static World Boot(out Log log, ulong seed = 1, Action<World> configure = null, Action<World> compiledMap = null, long maxTicks = 200)
    {
        World world = Start(out log, seed, configure, compiledMap);
        while (!Initialized(world) && world.Tick < maxTicks)
            world.Step();
        if (!Initialized(world))
            throw new InvalidOperationException($"the Master did not finish initializing within {maxTicks} ticks");
        return world;
    }

    /// <summary>Whether the Master of <paramref name="world"/> has initialized every subsystem stage.</summary>
    public static bool Initialized(World world)
    {
        using (world.Enter())
            return Master != null && Dm.eq(datum_controller_master.init_stage_completed, INITSTAGE_MAX);
    }

    /// <summary>
    /// The SSticker stand-in: <c>Master.SetRunLevel(<paramref name="runlevel"/>)</c> (controllers/master.dm:554), as tg's
    /// ticker does when the round moves on (RUNLEVEL_SETUP, RUNLEVEL_GAME, ...). M10 slice F translated the ticker: the stand-in
    /// pauses it (tg's <c>can_fire</c>, master.dm:893), so its pregame countdown does not set a round up in the middle of a test.
    /// </summary>
    public static void RunLevel(World world, float runlevel)
    {
        using (world.Enter())
        {
            if (SSticker != null)
                SSticker.can_fire = Dm.FALSE;
            Master.SetRunLevel(runlevel);
        }
    }
}
