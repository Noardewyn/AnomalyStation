using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

// D320 (DV352): the build preview. tg shows no ghost of what a construction tool will build: its 2D player sees the tile and the tool's
// window. The 3D client draws the next build as a translucent, pulsing picture on the aimed tile, so the player sees what goes where and
// which way it faces. The item decides the tile from what its user hovers (tg's MouseEntered, client.dm_mouse_over): none where a click
// would not build. An item in the active hand that builds says what it would build; the server sends it to its holder's client
// (protocol version 70) when it changes. The RPD is the first; the other construction and crafting tools override build_preview later.

/// <summary>
/// What a held tool would build on a click at what its user aims at: tg's picture (<paramref name="Icon"/> file, <paramref name="State"/>,
/// BYOND <paramref name="Dir"/>), its <paramref name="Color"/> (tg's <c>"#rrggbb"</c>, null for none) and pixel shift, on <paramref name="Place"/>.
/// </summary>
public readonly record struct BuildPreview(string Icon, string State, int Dir, string Color, int PixelX, int PixelY, turf Place);

public partial class obj_item
{
    /// <summary>DEVIATION(DV352): what this item in <paramref name="user"/>'s active hand would build on a click at what the user's client hovers now, null for nothing.</summary>
    public virtual BuildPreview? build_preview(mob user) => null;
}
