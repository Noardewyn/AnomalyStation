namespace Anomaly.Protocol;

// Protocol version 78 (silicons SI4, D326): the AI camera net's static (DV386), the popup windows (DV387), the mob's command verbs and
// the actions that run a verb and close a popup.

/// <summary>
/// Version 78 (DV386): one camera chunk the client's eye looks into (tg's <c>/datum/camerachunk</c>, <c>CHUNK_SIZE</c> 8 tiles a side) and
/// which of its tiles no camera sees: bit <c>dy * 8 + dx</c> of <see cref="Obscured"/> for the tile (<see cref="X"/> + dx, <see cref="Y"/> + dy).
/// tg draws a static image on each such tile in the viewer's client.images; the client draws the static from the mask.
/// </summary>
/// <param name="X">The chunk's first tile x (tg's <c>GET_CHUNK_COORD</c>: 1, 8, 16, ...).</param>
/// <param name="Y">The chunk's first tile y.</param>
/// <param name="Z">The chunk's level.</param>
/// <param name="Obscured">The obscured tiles' bits.</param>
public readonly record struct StaticChunk(int X, int Y, int Z, ulong Obscured);

/// <summary>
/// Version 78 (DV387): a popup window open on the client (tg's <c>create_popup</c>, a clone of BYOND's popupwindow skin element with a
/// map in it): its name, title and size in pixels. The client draws the window with a second 3D view of the map <c>name_map</c> (the
/// screen objects whose screen_loc names it and the turfs in their vis_contents); closing it sends <see cref="ActionKind.PopupClose"/>.
/// </summary>
public sealed record PopupWindow(string Name, string Title, int Width, int Height);

/// <summary>
/// Version 78: one command verb of the client's mob (tg's <c>GAME_VERB</c> with a category, the stat panel's tabs, e.g. "AI Commands"): its
/// category, its name as shown and its path, which <see cref="ActionKind.Verb"/> sends back to run it.
/// </summary>
public sealed record VerbEntry(string Category, string Name, string Path);

/// <summary>
/// Version 78 (DV387): the tiles a second view shows: the turfs in the vis_contents of a popup map's screen objects (<see cref="Map"/>, tg's
/// <c>name_map</c>) or of a picture-in-picture screen object (<see cref="ScreenId"/>: the AI's multicamera windows), as their bounding
/// rectangle on one level. The client draws the rectangle from above.
/// </summary>
public readonly record struct ViewArea(long ScreenId, string Map, int X1, int Y1, int X2, int Y2, int Z);

public sealed partial class OwnBlock
{
    /// <summary>Version 78 (DV387): the second views' areas, when they changed; null when unchanged.</summary>
    public List<ViewArea> ViewAreas { get; set; }

    /// <summary>Version 78 (DV386): the camera chunks the client's eye sees, when they or their static changed since the last block; null when unchanged (an empty list: none).</summary>
    public List<StaticChunk> StaticChunks { get; set; }

    /// <summary>Version 78 (DV387): the popup windows open on the client, when the set changed; null when unchanged.</summary>
    public List<PopupWindow> Popups { get; set; }

    /// <summary>Version 78: the command verbs of the client's mob, when they changed; null when unchanged.</summary>
    public List<VerbEntry> Verbs { get; set; }
}
