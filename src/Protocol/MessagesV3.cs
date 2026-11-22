using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

// Owner: the types and the meaning of every field are the M3 contracts step's (frozen, docs/ai/specs/m3-ownership.md); their
// encoding is M3 P5's (Codec, docs/ai/specs/protocol.md section for version 3). Design basis: M3 scope 2f, 2h, 2j; D24, D114,
// D115, D116.
//
// Version 3 adds to version 2 (MessagesV2.cs):
// - per cell of MapGrid: the flags byte (the 4 blocked faces of ON_BORDER_1 objects, the opacity bit, the starlight bit),
//   the base light (the area's base_lighting_* as an index into a small table), the starlight values;
// - the cell-change records of a Delta for each of them, and turf type changes (ChangeTurf after the join);
// - the entity field Light (NetField.Light): the state of an atom's tg light source.

/// <summary>
/// The bits of a cell's flags byte (<see cref="MapGrid.FlagRuns"/>, <see cref="CellFlagsChange"/>). The four face bits have
/// the values of <see cref="TileEdges"/> (and of BYOND's NORTH, SOUTH, EAST, WEST), so <c>(TileEdges)(flags &amp; Faces)</c>
/// is the cell's blocked faces.
/// </summary>
public static partial class CellFlags
{
    /// <summary>The north face is blocked (a dense <c>ON_BORDER_1</c> object facing north stands on the cell).</summary>
    public const byte FaceNorth = 1;

    /// <summary>The south face is blocked.</summary>
    public const byte FaceSouth = 2;

    /// <summary>The east face is blocked.</summary>
    public const byte FaceEast = 4;

    /// <summary>The west face is blocked.</summary>
    public const byte FaceWest = 8;

    /// <summary>The four face bits.</summary>
    public const byte Faces = FaceNorth | FaceSouth | FaceEast | FaceWest;

    /// <summary>The cell blocks light: tg's <c>IS_OPAQUE_TURF</c> (an opaque turf, or an opaque movable on it: walls, closed airlocks).</summary>
    public const byte Opaque = 16;

    /// <summary>A space cell whose starlight is on (tg's <c>enable_starlight</c>): a light source with <see cref="MapGrid.Starlight"/>'s values.</summary>
    public const byte Starlight = 32;

    /// <summary>Every valid bit; a decoder refuses any other (version 37: <see cref="ClimbWalkable"/>; version 73: <see cref="Flaps"/>).</summary>
    public const byte All = Faces | Opaque | Starlight | ClimbWalkable | Flaps;

    /// <summary>The blocked faces of a flags byte.</summary>
    public static TileEdges FacesOf(byte flags) => (TileEdges)(flags & Faces);
}

/// <summary>
/// One entry of the base-light table (<see cref="MapGrid.BaseLights"/>): an area's <c>base_lighting_color</c> (RGB, white
/// when tg's is null) and <c>base_lighting_alpha</c> (A), as <c>0xRRGGBBAA</c>. An area whose alpha is 0 has no entry (index 0).
/// </summary>
public readonly record struct BaseLight(uint Rgba);

/// <summary>
/// The state of an atom's tg light source (docs/ai/specs/light-grid.md section 1): what the client's light grid needs to
/// compute tg's corner light. <see cref="None"/> (range 0) when the atom has no light.
/// </summary>
/// <param name="Range">tg's <c>light_range</c> in tiles, 0 to <see cref="Limits.MaxLightRange"/>.</param>
/// <param name="Power">tg's <c>light_power</c> (negative darkens), at most <see cref="Limits.MaxLightPower"/> in size.</param>
/// <param name="Rgba">tg's <c>light_color</c> as <c>0xRRGGBBAA</c> (<c>PARSE_LIGHT_COLOR</c>: null is white, alpha 255).</param>
/// <param name="On">tg's <c>light_on</c>: whether tg has a light source for the atom now.</param>
/// <param name="Angle">tg's <c>light_angle</c> in degrees (360: all around; 0 to 360).</param>
/// <param name="Dir">tg's <c>light_dir</c>: a BYOND direction, the centre of the cone.</param>
/// <param name="Height">tg's <c>light_height</c> (<c>LIGHTING_HEIGHT</c> 1; space turfs <c>LIGHTING_HEIGHT_SPACE</c> -0.5).</param>
/// <param name="OffsetX">tg's visual offset of the source in tiles, x (<c>calculate_light_offset</c>, <c>lighting_atom.dm:218</c>), at most <see cref="Limits.MaxLightOffset"/> in size.</param>
/// <param name="OffsetY">The same, y.</param>
/// <param name="System">tg's <c>light_system</c>: <c>COMPLEX_LIGHT</c> 1 (the corner light), <c>OVERLAY_LIGHT</c> 2, <c>OVERLAY_LIGHT_DIRECTIONAL</c> 3, <c>OVERLAY_LIGHT_BEAM</c> 4.</param>
public readonly record struct LightState(float Range, float Power, uint Rgba, bool On, float Angle, byte Dir, float Height, float OffsetX, float OffsetY, byte System)
{
    /// <summary>No light.</summary>
    public static LightState None => default;

    /// <summary>True when there is no light (<see cref="Range"/> 0).</summary>
    public bool IsNone => Range == 0f;
}

/// <summary>A cell's flags changed (a door's opacity and faces, a window built: M3 doors, M5 construction).</summary>
public readonly record struct CellFlagsChange(int X, int Y, int Z, byte Flags);

/// <summary>A cell's base light changed (its area changed: M5's area editing; never in M3 after the join).</summary>
public readonly record struct CellBaseLightChange(int X, int Y, int Z, ushort BaseLight);

/// <summary>A cell's turf type changed (tg's <c>ChangeTurf</c> after the join; M5's construction): an index into the MapGrid's turf type table as extended by the Deltas.</summary>
public readonly record struct CellTurfTypeChange(int X, int Y, int Z, int TypeIndex);

/// <summary>Version 3 of <see cref="MapGrid"/>: the cell flags, the base lights and the starlight values.</summary>
public sealed partial class MapGrid
{
    /// <summary>
    /// Version 3: every cell's flags byte (<see cref="CellFlags"/>), as runs over the cells in the order of
    /// <see cref="Runs"/> (a run's <see cref="TurfRun.TypeIndex"/> is the flags byte here). The server computes the faces with
    /// <c>Anomaly.Core.Tg.LocomotionSeam.FacesOf</c>, the predicate its controller uses (so the client predicts against exactly
    /// the server's faces, D103), and opacity and starlight with <c>Anomaly.Core.Tg.CellState</c>. Later changes come as
    /// <see cref="Delta.CellFlagsChanged"/>.
    /// </summary>
    public List<TurfRun> FlagRuns { get; } = new();

    /// <summary>Version 3: the base-light table (index 1 is <c>BaseLights[0]</c>; 0 is none), append-only for the run (<see cref="Delta.NewBaseLights"/>).</summary>
    public List<BaseLight> BaseLights { get; } = new();

    /// <summary>Version 3: every cell's base-light index, as runs over the cells in the order of <see cref="Runs"/>.</summary>
    public List<TurfRun> BaseLightRuns { get; } = new();

    /// <summary>Version 3: tg's <c>GLOB.starlight_range</c>, <c>starlight_power</c> and <c>starlight_color</c> with <c>LIGHTING_HEIGHT_SPACE</c>, the light of every <see cref="CellFlags.Starlight"/> cell (<see cref="Delta.StarlightChanged"/> later).</summary>
    public LightState Starlight { get; set; }

    /// <summary>The flags runs expanded, one byte per cell in the order of <see cref="Runs"/> (an empty list of runs is all zero). Throws <see cref="InvalidOperationException"/> when the runs do not fit.</summary>
    public byte[] ExpandFlags()
    {
        var cells = new byte[CellCount];
        Expand(FlagRuns, cells.Length, byte.MaxValue, (start, count, value) => Array.Fill(cells, (byte)value, start, count), "flags");
        return cells;
    }

    /// <summary>The base-light runs expanded, one index per cell in the order of <see cref="Runs"/> (an empty list of runs is all zero). Throws <see cref="InvalidOperationException"/> when the runs do not fit.</summary>
    public ushort[] ExpandBaseLights()
    {
        var cells = new ushort[CellCount];
        Expand(BaseLightRuns, cells.Length, ushort.MaxValue, (start, count, value) => Array.Fill(cells, (ushort)value, start, count), "base-light");
        return cells;
    }

    private static void Expand(List<TurfRun> runs, int length, int maxValue, Action<int, int, int> fill, string what)
    {
        if (runs.Count == 0) return;
        int next = 0;
        foreach (TurfRun run in runs)
        {
            if (run.Count < 0 || run.Count > length - next || run.TypeIndex < 0 || run.TypeIndex > maxValue)
                throw new InvalidOperationException("the MapGrid " + what + " runs do not fit the map");
            fill(next, run.Count, run.TypeIndex);
            next += run.Count;
        }
        if (next != length)
            throw new InvalidOperationException("the MapGrid " + what + " runs cover " + next + " of " + length + " cells");
    }
}

/// <summary>Version 3 of <see cref="Delta"/>: the cell flags, base lights, turf types and starlight of the tick.</summary>
public sealed partial class Delta
{
    /// <summary>Body: cells whose flags (faces, opacity, starlight) changed this tick (M3 doors open and close).</summary>
    public List<CellFlagsChange> CellFlagsChanged { get; } = new();

    /// <summary>Body: the base-light table index of <c>NewBaseLights[0]</c> (the table's length before this Delta, plus one).</summary>
    public uint FirstNewBaseLight { get; set; }

    /// <summary>Body: base-light table entries this tick added, in index order.</summary>
    public List<BaseLight> NewBaseLights { get; } = new();

    /// <summary>Body: cells whose base light changed this tick.</summary>
    public List<CellBaseLightChange> BaseLightChanged { get; } = new();

    /// <summary>Body: the turf type table index of <c>NewTurfTypes[0]</c> (the MapGrid's <see cref="MapGrid.TurfTypes"/> as extended so far: its length before this Delta).</summary>
    public int FirstNewTurfType { get; set; }

    /// <summary>Body: turf type paths this tick added to the map's turf type table, in index order.</summary>
    public List<string> NewTurfTypes { get; } = new();

    /// <summary>Body: cells whose turf type changed this tick (tg's <c>ChangeTurf</c>; in M3 only after a join, which a static map never does).</summary>
    public List<CellTurfTypeChange> TurfTypeChanged { get; } = new();

    /// <summary>Body: the new starlight values when tg changed them this tick (<c>set_starlight</c>), else null.</summary>
    public LightState? StarlightChanged { get; set; }
}
