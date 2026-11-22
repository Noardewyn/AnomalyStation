using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// <summary>
/// The map editor's seam (docs/ai/specs/map-editor.md 3, D336): a world that stands as its round would start, so the editor shows a map
/// with the game's own init (the models, the joints of pipes, cables and walls, the decals), and the tiles of its loaded map changed the
/// way tg loads a map template into a running world (map_template.dm <c>load</c>: the reader's ChangeTurf per tile, then
/// <c>initTemplateBounds</c>).
/// </summary>
public static class MapEditSeam
{
    // An edit is loaded per square of this many tiles (the format's chunk), so one far tile does not make a template of the whole map.
    private const int Square = MapText.ChunkSize;

    private const string Noop = "/turf/template_noop | /area/template_noop"; // tg's members of a template tile that leaves the map's as it is

    private sealed class SeamState
    {
        internal bool On;
        internal bool UnderFloor; // what the floor tiles cover is shown
        internal bool NoMarkers; // the landmarks, the spawners and the mapping helpers are hidden
    }

    private static SeamState State(World world) => world.GetExtension(static _ => new SeamState());

    /// <summary>Makes <paramref name="world"/> a map editor's, before <c>TgWorld.Boot</c>.</summary>
    public static void Select(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        State(world).On = true;
    }

    /// <summary>Whether <paramref name="world"/> is a map editor's (<see cref="Select"/>).</summary>
    public static bool Selected(World world) => State(world).On;

    /// <summary>
    /// True in a map editor's world. DEVIATION(DV504): there a random spawner spawns nothing and stays (<c>/obj/effect/spawner/random</c>'s
    /// Initialize), and a landmark is visible (<see cref="Listen"/>): both stand where the map has them, as the markers the mapper places.
    /// </summary>
    public static bool On => World.CurrentOrNull is World world && State(world).On;

    /// <summary>
    /// True in a map editor's world that shows what its floor tiles cover (<see cref="ShowUnderFloor"/>). DEVIATION(DV504): tg's undertile element then
    /// leaves a covered cable, pipe or disposal pipe visible, half clear over its tile, as a T-ray scanner shows it.
    /// </summary>
    public static bool UnderFloorShown => World.CurrentOrNull is World world && State(world) is { On: true, UnderFloor: true };

    /// <summary>
    /// Shows or hides what the floor tiles cover; call in the world. Every turf tells its things again, as tg does when a tile is laid or lifted
    /// (<c>levelupdate</c>), and the undertile element hides them or not by <see cref="UnderFloorShown"/>.
    /// </summary>
    public static void ShowUnderFloor(bool show)
    {
        SeamState state = State(World.Current);
        if (state.UnderFloor == show)
            return;
        state.UnderFloor = show;
        foreach (turf T in Dm.each<turf>(ALL_TURFS()))
            T.levelupdate();
    }

    /// <summary>Shows or hides the markers a mapper places (DV504: the landmarks, the spawners and the mapping helpers); call in the world.</summary>
    public static void ShowMarkers(bool show)
    {
        SeamState state = State(World.Current);
        if (state.NoMarkers == !show)
            return;
        state.NoMarkers = !show;
        foreach (obj_effect marker in Dm.each<obj_effect>(Dm.world))
        {
            if (IsMarker(marker) && !QDELETED(marker))
                marker.invisibility = MarkerInvisibility(marker);
        }
    }

    private static bool IsMarker(obj_effect effect) => effect is obj_effect_landmark or obj_effect_spawner or obj_effect_mapping_helpers;

    // A marker's invisibility in an editor's world: a landmark is seen (tg hides it from everyone, INVISIBILITY_ABSTRACT), a spawner and a helper keep
    // their type's; none is seen while the mapper has the markers hidden.
    internal static float MarkerInvisibility(obj_effect marker)
        => State(World.Current).NoMarkers ? INVISIBILITY_ABSTRACT : marker is obj_effect_landmark ? 0 : Dm.num(Dm.initial(marker, (obj_effect p) => p.invisibility));

    /// <summary>Starts showing the landmarks tg initializes from now on (tg's <c>COMSIG_GLOB_ATOM_AFTER_POST_INIT</c> on SSdcs); call in the world, after the boot.</summary>
    public static void Listen()
    {
        var listener = Dm.@new<MapEditListener>();
        listener.RegisterSignal(SSdcs, COMSIG_GLOB_ATOM_AFTER_POST_INIT, nameof(MapEditListener.on_atom_initialized));
    }

    /// <summary>
    /// Gives tiles of the loaded map their new models; call in the world. Per square of the map: what stands on the changed tiles is deleted
    /// (ghosts stay), then tg loads a template that holds the changed tiles and leaves the ring around them as it is. The ring is inside the
    /// template's bounds, so <c>initTemplateBounds</c> links the cables and the pipes of the neighbours again, as it does for the new ones.
    /// </summary>
    public static async DmTask SetTiles(IReadOnlyList<(int X, int Y, int Z, MapModel Model)> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        // tg's space turf joins /area/space/nearstation when something is built on it (space.dm ChangeTurf): an area every station map of tg has; a map
        // without one (a new map) gets the area here, or the first floor laid on space would find none to join
        if (GLOB.areas_by_type[typeof(area_space_nearstation)] == null)
            GLOB.areas_by_type[typeof(area_space_nearstation)] ??= await Dm.new_async(typeof(area_space_nearstation));
        foreach (var square in tiles.GroupBy(t => (t.Z, (t.X - 1) / Square, (t.Y - 1) / Square)))
        {
            int z = square.Key.Z;
            int x0 = Math.Max(1, square.Min(t => t.X) - 1), x1 = Math.Min((int)Dm.world.maxx, square.Max(t => t.X) + 1);
            int y0 = Math.Max(1, square.Min(t => t.Y) - 1), y1 = Math.Min((int)Dm.world.maxy, square.Max(t => t.Y) + 1);
            MapDocument cut = MapDocument.Create(x1 - x0 + 1, y1 - y0 + 1, 1, Noop);
            cut.Edit("cut", square.Select(t => (t.X - x0 + 1, t.Y - y0 + 1, 1, cut.Model(t.Model.Text))));

            foreach ((int x, int y, _, _) in square)
            {
                if (locate(x, y, z) is not turf at)
                    continue;
                foreach (atom_movable thing in Dm.each<atom_movable>(at.contents).ToList())
                {
                    if (thing is not mob_dead)
                        qdel(thing, TRUE);
                }
            }

            var template = (datum_map_template)await Dm.new_async(typeof(datum_map_template));
            template.cached_map = (datum_parsed_map)await Dm.new_async(typeof(datum_parsed_map), cut.ToText());
            template.keep_cached_map = TRUE;
            // not on top of the turf that is there, as tg lays a shuttle or a ruin: every load would put the old turf under the new one, and a tile edited or
            // previewed a few times had more base turfs than tg allows (baseturfs_string_list: over ten), which stopped its load
            template.should_place_on_top = FALSE;
            template.width = cut.Width;
            template.height = cut.Height;
            await template.load(locate(x0, y0, z));

            // The area of a tile is the map's: a floor laid on space stays in the area the map gives it, as when the map is loaded from its file, whatever
            // area tg's ChangeTurf moved it to.
            foreach ((int x, int y, _, MapModel model) in square)
            {
                if (locate(x, y, z) is not turf at || model.Members.Count == 0 || DmTypes.FromPath(model.Members[^1].Path) is not Type wanted)
                    continue;
                if (at.loc is area now && now.GetType() != wanted && GLOB.areas_by_type[wanted] is area mapped)
                    set_turf_to_area(at, mapped);
            }
        }

        // The neighbours of a changed turf join it anew, or stop joining what stood there. tg queues them when a turf changes in a round (ChangeTurf,
        // change_turf.dm:202), but a template's load changes its turfs before they are initialized, and a turf's Initialize queues the turf alone.
        foreach ((int x, int y, int z, _) in tiles)
        {
            if (locate(x, y, z) is not turf at)
                continue;
            QUEUE_SMOOTH(at);
            QUEUE_SMOOTH_NEIGHBORS(at);
        }

        // tg works its smoothing queue off in a round only (SSicon_smooth fires at the game's runlevels), and the editor's world stands before the round:
        // what the edit queued (the new walls, tables and windows, and their neighbours) is smoothed here, as the subsystem's fire does (icon_smooth.dm:30).
        DmList queue = SSicon_smooth.smooth_queue;
        while (length(queue) > 0)
        {
            var smoothing_atom = queue[length(queue)] as atom;
            queue.len--;
            if (QDELETED(smoothing_atom) || !Dm.truthy(Dm.bitand(smoothing_atom.smoothing_flags, SMOOTH_QUEUED)))
                continue;
            if (Dm.truthy(Dm.bitand(smoothing_atom.flags_1, INITIALIZED_1)))
                smoothing_atom.smooth_icon__proc();
        }
    }
}

/// <summary>The listener of tg's global atom init signal for <see cref="MapEditSeam"/>: a plain datum with no DM path of its own (as ToyListener).</summary>
internal class MapEditListener : datum
{
    [SignalHandler]
    public virtual object on_atom_initialized(datum source = null, object created = null)
    {
        // DEVIATION(DV504): tg hides a landmark from everyone (INVISIBILITY_ABSTRACT); the map editor shows it where the map has it, and hides
        // every marker while the mapper has that layer off
        if (created is obj_effect marker && marker is obj_effect_landmark or obj_effect_spawner or obj_effect_mapping_helpers && !QDELETED(marker))
        {
            float invisibility = MapEditSeam.MarkerInvisibility(marker);
            if (marker.invisibility != invisibility)
                marker.invisibility = invisibility;
        }
        return NONE;
    }
}
