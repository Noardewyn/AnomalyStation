using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server;

// The map editor's part of the host (docs/ai/specs/map-editor.md 3, D336; ServerConfig.MapEditor).
public sealed partial class ServerHost
{
    /// <summary>
    /// Gives tiles of the loaded map their new models, the way tg loads a map template (<see cref="MapEditSeam.SetTiles"/>). Call between ticks, on the
    /// thread that runs them. The task is done when tg's loader is: a loader that waits goes on in the next ticks.
    /// </summary>
    public DmTask SetMapTiles(IReadOnlyList<(int X, int Y, int Z, MapModel Model)> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        using (World.Enter())
        {
            DmTask task = MapEditSeam.SetTiles(tiles);
            Waiting.Detach(task);
            return task;
        }
    }

    /// <summary>
    /// How many subsystems the Master initializes at a start (the ones without SS_NO_INIT), which <see cref="StepProfiler.InitsDone"/> counts as they
    /// finish: how far a start is. Call between ticks, on the thread that runs them; 0 before the Master is made.
    /// </summary>
    public int InitsExpected()
    {
        using (World.Enter())
            return Master == null ? 0 : Dm.each<datum_controller_subsystem>(Master.subsystems).Count(subsystem => !Dm.truthy(Dm.bitand(subsystem.ss_flags, SS_NO_INIT)));
    }

    /// <summary>
    /// Shows or hides a layer of the editor's world (docs/ai/specs/map-editor.md 5.5): what the floor tiles cover, and the markers a mapper places.
    /// Call between ticks, on the thread that runs them; the next ticks send the change.
    /// </summary>
    public void SetMapLayers(bool underFloor, bool markers)
    {
        using (World.Enter())
        {
            MapEditSeam.ShowUnderFloor(underFloor);
            MapEditSeam.ShowMarkers(markers);
        }
    }

}
