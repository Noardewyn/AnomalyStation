using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Boot;

/// <summary>
/// A world with the boot package's own pieces and nothing of the Master Controller (the mc package): GLOB with its
/// translated InitGlobal procs and the configuration, built as Genesis and world/New build them. Until every wave-3 package
/// is merged the full bootstrap reaches the other packages' skeletons, so the boot package's behaviour tests use this world
/// and assert only on what the boot package owns (docs/ai/specs/wave3-ownership.md). With <c>notTranslatedThrows</c> false a
/// path into another package's skeleton (log_config, _addtimer, ...) is reported instead of thrown.
/// </summary>
internal sealed class BootPieces : IDisposable
{
    private readonly World.Scope _scope;

    public BootPieces(ulong seed = 1, bool notTranslatedThrows = true, bool createGlob = true)
    {
        World = new World(seed) { RuntimeErrorSink = Runtimes.Add };
        DmWorldState state = DmWorldState.Of(World);
        state.NotTranslatedThrows = notTranslatedThrows;
        state.WorldLog = Log.Add;
        _scope = World.Enter();
        logger = Dm.@new<datum_log_holder>(); // Genesis creates it before the Master (game/world.dm:97)
        // The subsystems Master/New creates before GLOB that GLOB's New needs: qdel (garbage.dm:346) and QDEL_IN's timer
        // (globals.dm:24).
        Dm.@new<datum_controller_subsystem_garbage>();
        Dm.@new<datum_controller_subsystem_timer>();
        Dm.@new<datum_controller_subsystem_materials>(); // GLOB's make_datum_reference_lists reads SSmaterials' recipes (global_lists.dm:93)
        Dm.@new<datum_controller_subsystem_accessories>(); // M10x slice D2: tg makes it at PreInit, before GLOB (sprite_accessories.dm:47); bodies read its lists
        if (createGlob)
            Dm.@new<datum_controller_global_vars>(); // as Master/New creates it (master.dm:128)
    }

    public World World { get; }

    public List<Exception> Runtimes { get; } = new();

    public List<string> Log { get; } = new();

    public DmMemoryFiles Files => (DmMemoryFiles)DmWorldState.Of(World).Files;

    /// <summary>Creates the configuration (Master/New: <c>if(!config) config = new</c>) and loads it (world/New).</summary>
    public datum_controller_configuration LoadConfig(string directory = null)
    {
        config = Dm.@new<datum_controller_configuration>();
        Waiting.NoSleep(config.Load(directory));
        return config;
    }

    public void Dispose() => _scope.Dispose();
}
