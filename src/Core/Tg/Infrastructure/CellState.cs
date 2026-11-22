using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

// Owner: the contract (these signatures and the meaning in the comments) is the M3 contracts step's and frozen
// (docs/ai/specs/m3-ownership.md); M3 P5 implements it for MapGrid version 3 (docs/ai/specs/light-grid.md section 1). A host seam,
// not tg code: it reads tg's state raw, like LocomotionSeam.IsSolid, inside the world or outside it after World.Step.

/// <summary>The per-cell state the replicator sends: (protocol version 3) opacity, starlight, the area's base light; (version 5) the gas a cell shows.</summary>
public static class CellState
{
    /// <summary>
    /// tg's <c>IS_OPAQUE_TURF(T)</c> (<c>__DEFINES/lighting.dm</c>: <c>T.directional_opacity == ALL_CARDINALS</c>): the turf is
    /// opaque, or an opaque movable stands on it (tg keeps <c>directional_opacity</c> through <c>recalculate_directional_opacity</c>;
    /// the port reads the turf's and its movables' <c>opacity</c> directly, as the light_blocking element is deferred).
    /// Outside the map: true.
    /// </summary>
    public static bool IsOpaque(World world, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(world);
        turf cell = MapView.TurfAt(world, x, y, z);
        if (cell == null)
            return true; // outside the map
        if (Dm.truthy(cell.opacity))
            return true;
        foreach (atom thing in cell.dm_held)
        {
            if (Dm.truthy(thing.opacity))
                return true; // BYOND's rule: any atom on the tile with opacity blocks sight
        }
        return false;
    }

    /// <summary>Whether the cell is a <c>/turf/open/space</c> whose starlight is on (tg's <c>enable_starlight</c>, <c>space.dm:117</c>: its <c>light_on</c>).</summary>
    public static bool IsStarlit(World world, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(world);
        // tg: code/game/turfs/open/space/space.dm:117 enable_starlight(): set_light(l_on = TRUE, ...) on a space turf next to non-space
        // (update_starlight); the port reads the result, the turf's light_on.
        return MapView.TurfAt(world, x, y, z) is turf_open_space space && Dm.truthy(space.light_on);
    }

    /// <summary>
    /// The base light of the cell's area as <c>0xRRGGBBAA</c>: tg's <c>base_lighting_color</c> (null is white) and
    /// <c>base_lighting_alpha</c> (<c>areas.dm</c>); 0 when the alpha is 0 or the cell has no area.
    /// </summary>
    public static uint BaseLightOf(World world, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(world);
        turf cell = MapView.TurfAt(world, x, y, z);
        if (cell == null || cell.dm_loc_raw is not area a)
            return 0;
        float alpha = a.base_lighting_alpha;
        if (!(alpha > 0f))
            return 0;
        // tg: lighting_area.dm update_base_lighting: the overlay takes base_lighting_color and base_lighting_alpha
        return (ParseColor(a.base_lighting_color) & 0xFFFFFF00u) | (uint)Math.Clamp((int)MathF.Round(alpha), 0, 255);
    }

    /// <summary>tg's <c>PARSE_LIGHT_COLOR</c> for a light's colour: <c>#rrggbb</c> or <c>#rgb</c> as <c>0xRRGGBBFF</c>; null and anything unparsable are white.</summary>
    public static uint ParseColor(string color)
    {
        const uint White = 0xFFFFFFFFu;
        if (string.IsNullOrEmpty(color) || color[0] != '#')
            return White;
        string hex = color.Substring(1);
        if (hex.Length == 3)
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
        if (hex.Length == 8)
            hex = hex.Substring(0, 6);
        if (hex.Length != 6 || !uint.TryParse(hex, System.Globalization.NumberStyles.AllowHexSpecifier, null, out uint rgb))
            return White;
        return (rgb << 8) | 0xFFu;
    }

    /// <summary>tg's starlight values: <c>GLOB.starlight_range</c>, <c>GLOB.starlight_power</c> and <c>GLOB.starlight_color</c> as <c>0xRRGGBBAA</c> (<c>space.dm:1-20</c>).</summary>
    public static (float Range, float Power, uint Rgba) Starlight(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        // tg: code/game/turfs/open/space/space.dm:1-20: GLOB.starlight_range, _power and _color start as initial() of /turf/open/space's
        // light_range (2), light_power (1) and light_color (COLOR_STARLIGHT). The globals and set_starlight are ported (M7 lumcount,
        // space.dm); nothing in a round calls set_starlight yet, so the defaults are the values and this method does not read the
        // globals (Delta.StarlightChanged would carry a change once something does).
        return (2f, 1f, ParseColor(COLOR_STARLIGHT));
    }

    private static readonly Lazy<string[]> GasKindTable = new(ReadGasKinds); // the types never change: one table per process

    /// <summary>
    /// Protocol version 5: the icon states of tg's gas overlays (<c>/datum/gas</c>'s <c>gas_overlay</c>, gas_types.dm), each
    /// once, in type order: the kinds of the gas a cell shows.
    /// </summary>
    public static IReadOnlyList<string> GasKinds() => GasKindTable.Value;

    private static string[] ReadGasKinds()
    {
        var kinds = new List<string>();
        foreach (Type gas in DmTypes.Subtypes(typeof(datum_gas), includeRoot: false))
        {
            string state = Dm.initial(gas, (datum_gas p) => p.gas_overlay);
            if (!string.IsNullOrEmpty(state) && !kinds.Contains(state))
                kinds.Add(state);
        }
        return kinds.ToArray();
    }

    /// <summary>
    /// Protocol version 5: the gas the cell shows, the overlays tg's <c>update_visuals</c> put into the open turf's
    /// <c>atmos_overlay_types</c> (GAS_OVERLAYS), in its order, as (icon state, level): the level is the overlay's visible state
    /// 1 to <c>TOTAL_VISIBLE_STATES</c>, read back from its alpha (generate_gas_overlays: <c>log(4, (j + 0.4 * 80) / (0.35 * 80)) * 255</c>).
    /// Nothing for a closed turf or outside the map.
    /// </summary>
    public static void GasOf(World world, int x, int y, int z, List<(string State, int Level)> into)
    {
        ArgumentNullException.ThrowIfNull(world);
        into.Clear();
        if (MapView.TurfAt(world, x, y, z) is not turf_open open || open.atmos_overlay_types == null)
            return;
        DmList overlays = open.atmos_overlay_types;
        for (int i = 1; i <= overlays.len; i++)
        {
            if (overlays[i] is not obj_effect_overlay_gas overlay || overlay.icon_state == null)
                continue;
            double level = 0.35 * TOTAL_VISIBLE_STATES * Math.Pow(4, overlay.alpha / 255.0) - 0.4 * TOTAL_VISIBLE_STATES;
            into.Add((overlay.icon_state, Math.Clamp((int)Math.Round(level), 1, TOTAL_VISIBLE_STATES)));
        }
    }
}
