using System.Linq;

namespace Anomaly.Client.Shared.Editor;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The map editor's arrows (docs/ai/specs/map-editor.md 5.19): which things get an arrow that shows the way they face, and how a way is turned in
/// the view. A thing still drawn as a flat picture shows one side of itself to every viewer, so the way it faces cannot be read off it.
/// </summary>
public static class FacingMarks
{
    // what stands on a tile and can face a way: furniture, machines, mobs, vehicles
    private static readonly string[] Families = { "/obj/structure", "/obj/machinery", "/mob", "/obj/vehicle" };

    // the markers whose way goes to what they make: tg's spawner gives its dir to its pick, a landmark to who starts on it
    private static readonly string[] Markers = { "/obj/effect/spawner", "/obj/effect/landmark" };

    // What has a dir that says nothing a mapper turns: runs that join their neighbours (their dir is their shape), what fills its tile, and what
    // hangs on a wall (its dir is the wall's side, seen at once).
    private static readonly string[] Never =
    {
        "/obj/structure/cable", "/obj/structure/disposalpipe", "/obj/structure/lattice", "/obj/structure/table", "/obj/structure/window", "/obj/structure/grille",
        "/obj/structure/girder", "/obj/structure/falsewall", "/obj/structure/sign", "/obj/structure/plaque", "/obj/machinery/atmospherics/pipe", "/obj/machinery/door/airlock",
        "/obj/machinery/door/firedoor", "/obj/machinery/door/poddoor", "/obj/machinery/light", "/obj/machinery/power/apc", "/obj/machinery/camera",
    };

    private static bool Under(string path, string root)
        => path.StartsWith(root, StringComparison.Ordinal) && (path.Length == root.Length || path[root.Length] == '/');

    /// <summary>True for a marker whose arrow is shown whatever its picture: a random spawner, a landmark.</summary>
    public static bool IsMarker(string path) => path != null && Markers.Any(root => Under(path, root));

    /// <summary>
    /// True for a type that can get an arrow. Of these the view shows one over a marker, and over a thing whose picture has a side for each way
    /// or that a 3D model draws: a picture with one side faces no way.
    /// </summary>
    public static bool Wanted(string path)
        => path != null && (IsMarker(path) || Families.Any(root => Under(path, root)) && !Never.Any(root => Under(path, root)));

    /// <summary>
    /// The turn, in degrees about the vertical axis, counter-clockwise seen from above, of an arrow that points north when it is not turned, for
    /// BYOND's dir (1 north, 2 south, 4 east, 8 west, and their sums); a dir that is none faces south, as tg's atoms do.
    /// </summary>
    public static float Degrees(int dir) => dir switch
    {
        1 => 0f,
        4 => -90f,
        8 => 90f,
        5 => -45f,
        9 => 45f,
        6 => -135f,
        10 => 135f,
        _ => 180f,
    };
}
