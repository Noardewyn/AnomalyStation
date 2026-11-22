using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// <summary>
/// The bootstrap of a tg world (wave 3, boot package): what BYOND does from world start until <c>world/New</c> has handed
/// the Master Controller its first sleep (tg's init order is documented in <c>code/game/world.dm</c>:11-56). Hosts call
/// <see cref="Boot"/> once on a fresh <see cref="World"/> after installing their seams (sinks, <c>DmWorldState.WorldLog</c>,
/// the file seam <c>DmWorldState.Files</c>, the runtime sink) and before the first <see cref="World.Step()"/>; then they step
/// the world. tg's timeline from there (tick 0 = boot): <c>Master.Initialize</c> sleeps 10 ds and initializes the
/// subsystems at tick 20, where the MC also fires for the first time (SSgarbage in the lobby runlevel), and schedules every
/// subsystem from tick 21. The round never starts by itself (SSticker is an inert stub until M10): a host or test that
/// wants the game runlevel calls <c>Master.SetRunLevel(RUNLEVEL_GAME)</c>, the ticker's job in tg.
/// </summary>
public static partial class TgWorld
{
    /// <summary>
    /// Boots <paramref name="world"/>: in its scope, (1) the <c>/world</c> var defaults of <c>code/world.dm</c>; (2) static
    /// initialization (<see cref="TgState.InitStatics"/>), which runs <c>world.Genesis()</c> first (<c>code/genesis_call.dme</c>):
    /// the logger, the Master with every subsystem and its <c>PreInit</c>, the configuration (not loaded yet) and <c>GLOB</c>
    /// with its <c>InitGlobal*</c> procs; (3) <paramref name="compiledMap"/>, the stand-in for the atoms of tg's compiled-in map
    /// (<c>_maps/_basemap.dm</c>; they are created before <c>world/New</c>, while SSatoms is not initialized, so their
    /// <c>Initialize</c> waits for it); (4) <c>world/New()</c> detached, as BYOND starts it: it logs, loads the configuration
    /// from the file seam, runs <c>ConfigLoaded</c> and calls <c>Master.Initialize(10, FALSE, TRUE)</c>, which sleeps.
    /// A runtime error in (4) is reported by the detach boundary; one in (1)-(3) propagates to the caller.
    /// </summary>
    public static void Boot(World world, Action<World> compiledMap = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        using (world.Enter())
        {
            // The engine's map grid hooks world.maxx/maxy/maxz before anything sets them, so every turf is created at the
            // assignment that makes its cell, as in BYOND (Builtin/MapGrid.cs).
            MapGrid.Attach(world);
            // Gaps R2: a world booted without tg's configuration file (the toy and test worlds; the server host always writes
            // config/config.txt) boots as tg's unit test build does at station.dm:24, without station traits, through tg's own
            // FORBID_STATION_TRAITS flag. A host or test that wants them writes its own config/config.txt.
            DmWorldState files = DmWorldState.Of(world);
            if (!files.Files.Exists("config/config.txt"))
                files.Files.WriteText("config/config.txt", "FORBID_STATION_TRAITS\n");
            DmWorld facade = Dm.world;
            dm_init_vars(facade);
            TgState.Current.InitStatics();
            compiledMap?.Invoke(world);
            Waiting.Detach(facade.New());
        }
        // BYOND calls world/Tick every tick after the sleepers (the MC) and before SendMaps and the client verbs (TICK_ORDER.md), so
        // its phase comes before the host's command phase; tg's NEXT_VISUAL_TICK, which starts every move loop a delay after the last
        // maptick, reads the GLOB.last_maptick_time it records (world.dm:274, visual_helpers.dm:30, movement_types.dm:71).
        world.AddPhase("world/Tick", static _ => Dm.world.Tick());
    }

    /// <summary>
    /// M3 P2: the compiled-in map of a world with a selected map (<see cref="MapSeam.Select"/>), as the
    /// <c>compiledMap</c> of <see cref="Boot"/>: BYOND sizes the world by its compiled-in maps before world/New
    /// (<c>world.maxx</c> and <c>maxy</c>; tg's compiled-in map is the CentCom level of <c>_maps/_basemap.dm</c>). D108 loads
    /// no compiled-in level, so this sets the size of the selected map (<see cref="MapSeam.CompiledMapSize"/>) and leaves
    /// <c>world.maxz</c> at 0: SSmapping's translated <c>loadWorld</c> adds the station level (<c>add_new_zlevel</c> grows
    /// maxz, which fills it with <c>world.turf</c> in <c>world.area</c>) and loads the map into it, centred as tg centres
    /// a map in the world (<c>LoadGroup</c>'s offsets).
    /// </summary>
    public static void CompiledMap(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        (int width, int height) = MapSeam.CompiledMapSize(world);
        if (width == 0 || height == 0)
            throw new InvalidOperationException("TgWorld.CompiledMap needs a selected map (MapSeam.Select before TgWorld.Boot)");
        Dm.world.maxx = width;
        Dm.world.maxy = height;
    }
}
