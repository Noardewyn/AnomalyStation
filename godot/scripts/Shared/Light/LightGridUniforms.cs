namespace Anomaly.Client.Shared.Light;

// Owner: the M3 contracts step (complete, frozen: docs/ai/specs/m3-ownership.md, docs/ai/specs/light-grid.md section 7). The
// interface M3 P6's world materials and M3 P7's light grid share. Engine-free: compiled into the client and, linked, into
// tests/Anomaly.Server.Tests (Client/).

/// <summary>
/// The names of the light grid's global shader uniforms (declared in <c>godot/project.godot</c>'s <c>[shader_globals]</c>,
/// set by P7 through <c>RenderingServer.GlobalShaderParameterSet</c>) and of the shader include every world material uses.
/// </summary>
public static class LightGridUniforms
{
    /// <summary>The include every world material (P6's turf, wall, prop, billboard and door shaders) adds: it declares the globals below and <c>vec3 anomaly_grid_light(vec3 world_pos)</c>.</summary>
    public const string IncludePath = "res://shaders/light_grid.gdshaderinc";

    /// <summary>The function of the include: the light the grid gives a surface point (world position in metres); a material adds <c>ALBEDO * anomaly_grid_light(world_pos)</c> to <c>EMISSION</c>.</summary>
    public const string LightFunction = "anomaly_grid_light";

    /// <summary><c>sampler2D</c>: the corner texture (RGBA16F, (maxx + 1) x (maxy + 1) texels) of the level the camera is on; texel (i, j) is the corner at the south-west of tile (i + 1, j + 1).</summary>
    public const string Grid = "anomaly_light_grid";

    /// <summary><c>vec2</c>: the texture's size in texels.</summary>
    public const string GridSize = "anomaly_light_grid_size";

    /// <summary><c>vec3</c>: the world position (metres) of corner (0, 0), the south-west corner of tile (1, 1) on the camera's level (<c>TileSpace</c>: X = 0, Z = 0, Y the level's floor).</summary>
    public const string GridOrigin = "anomaly_light_grid_origin";

    /// <summary><c>float</c>: metres per tile (<c>TileSpace.TileSize</c>, 2).</summary>
    public const string TileMetres = "anomaly_light_tile_metres";

    /// <summary><c>vec3</c>: the dim ambient every lit material gets (P7 tunes it).</summary>
    public const string Ambient = "anomaly_light_ambient";

    /// <summary><c>float</c>: the scale of the grid term (P7 tunes it; 0 turns the grid off).</summary>
    public const string Strength = "anomaly_light_strength";

    /// <summary><c>float</c>: the most light a surface gets after the scale (1: never brighter than its sprite, as tg); 0 for no cap.</summary>
    public const string Cap = "anomaly_light_cap";

    /// <summary>
    /// <c>vec3</c>: protocol version 31 (DV81), the floor of the lighting plane per channel (0 to 1), tg's lighting cutoff plus the colour cutoff
    /// of the own mob's glasses and eyes (<c>VisionState.LightFloor</c>): the grid's light is raised to it before it is scaled. (0, 0, 0) for none.
    /// </summary>
    public const string Floor = "anomaly_light_floor";

    /// <summary>
    /// <c>sampler2D</c>: one byte per tile of the camera's level (R8, the grid's size; texel (x, y) is tile (x, y)), <see cref="DoorCellLight"/>'s code times 127:
    /// in a closed door's cell a point reads the corners of its own side of the door.
    /// </summary>
    public const string Cells = "anomaly_light_cells";

    /// <summary><c>sampler2D</c>: D288, one byte per tile of the camera's level (R8, texel (x, y) is tile (x, y)), 255 where the cell is open to the sky (<see cref="SkyLight"/>).</summary>
    public const string Exterior = "anomaly_exterior";

    /// <summary><c>vec3</c>: D288, the light from space a surface open to the sky gets facing up (the stars), sideways and down (the lava planet): the ambient cube.</summary>
    public const string SkyUp = "anomaly_sky_up", SkySide = "anomaly_sky_side", SkyDown = "anomaly_sky_down";

    /// <summary><c>float</c>: D288, how far from the camera (metres) the sun's shadow alone decides where it lights; beyond, only where the sky is open. 0: everywhere only there.</summary>
    public const string SunReach = "anomaly_sun_reach";

    /// <summary>D288: a level's height in metres (<c>TileSpace.LevelHeight</c>, the walls' height): the sun stays out of the band under a covered cell's ceiling.</summary>
    public const string LevelHeight = "anomaly_level_height";

    /// <summary>D325: how many levels of a multi-z stack the grid, cells and exterior textures hold, the lowest first, one block of rows each
    /// (the shader picks a point's block by its height above <see cref="GridOrigin"/>); 1 off multi-z maps.</summary>
    public const string Levels = "anomaly_light_grid_levels";

    /// <summary>Every global uniform name, in the order above (a test checks that <c>project.godot</c> declares each).</summary>
    public static readonly string[] All = { Grid, GridSize, GridOrigin, TileMetres, Ambient, Strength, Cap, Floor, Cells, Exterior, SkyUp, SkySide, SkyDown, SunReach, LevelHeight, Levels };
}
