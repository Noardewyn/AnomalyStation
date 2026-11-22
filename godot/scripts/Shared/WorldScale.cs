using System.Globalization;

namespace Anomaly.Client.Shared;

// Owner: M3 R1 (D119). Engine-free: no Godot API here, so these files are compiled into the client and, linked, into tests/Anomaly.Server.Tests (Client/).

/// <summary>
/// The ONE setting for how high the world is (D119, amending D33): walls, doors, windows, grilles, pod doors, windoors and the ceiling are
/// <see cref="WallHeight"/> metres tall, 2 m by default, as high as a tile is wide, so tg's square 32 x 32 sprites sit on square 2 x 2 m faces
/// and are neither cut nor stretched. Everything that depends on the height derives from it: the meshes (<c>WorldGeometry</c>), the level
/// spacing (<see cref="TileSpace.LevelHeight"/>), the wall-mount heights (<see cref="MountHeight"/>, as shares of the wall), the shadowed lights
/// (<see cref="LampHeight"/>), the occluders, the chunk bounds and the pod doors' drop. The standing eye is the other setting here,
/// <see cref="EyeHeight"/> (D157: 1.3 m, at the eyes of the mob-scaled human sprite, 0.4 m lying; 1.5 m under D137, 1.2 m under D120), so the ceiling is 0.7 m above it.
/// <para>
/// The wall height is the server's (the owner, 2026-11-20, revising D119: its <c>wall_height</c> setting, sent in MapGrid, protocol 91), read once
/// when the world is built: GameScene sets it before <c>MapRenderer.Build</c>; changing it later needs a rebuilt world. The eye height is read by the camera layer every
/// frame, but is set the same way, once at the start (<c>--eye-height=M</c>, the file's <c>eye_height</c>). The third setting is the item scale (<see cref="ItemScale"/>,
/// D121: 0.5), read when the world is built. Tests give the geometry functions their height explicitly and never set either height.
/// </para>
/// </summary>
public static class WorldScale
{
    /// <summary>D119: as high as a tile is wide.</summary>
    public const float DefaultWallHeight = TileSpace.TileSize;

    /// <summary>The lowest height accepted: the wall must clear the highest eye (<see cref="MaxEyeHeight"/>) and a standing mob (1.81 m) with a margin.</summary>
    public const float MinWallHeight = 2f;

    /// <summary>
    /// D157 (the owner, 2026-10-18, after the mobs were drawn smaller): the standing eye is 1.3 m above the floor, at the eyes of tg's human drawn at
    /// <see cref="MobScale"/>: the eyes (<c>human_eyes.dmi</c>) are the 26th pixel row above the feet, their middle 25.5 pixels up, which is 1.275 m at
    /// 0.8 of the tile scale (2 m / 32 * 0.8 = 0.05 m a pixel; the top of the head, row 29, is 1.45 m), rounded to 1.3 m. It was 1.5 m under D137, 1.2 m
    /// under D120 and 1.6 m under D119.
    /// </summary>
    public const float DefaultEyeHeight = 1.3f;

    /// <summary>D137's default (1.5 m): an options file older than <see cref="PlayOptions.FileVersion"/> 3 that still has it gets the new one.</summary>
    public const float D137EyeHeight = 1.5f;

    /// <summary>D120's default (1.2 m): an options file older than <see cref="PlayOptions.FileVersion"/> 2 that still has it gets the new one.</summary>
    public const float OldDefaultEyeHeight = 1.2f;

    /// <summary>The lowest standing eye accepted: well above the lying eye (<see cref="CameraLayer.LyingEye"/>, 0.4 m).</summary>
    public const float MinEyeHeight = 0.8f;

    /// <summary>The highest standing eye accepted: under the lowest ceiling (<see cref="MinWallHeight"/>) with a margin.</summary>
    public const float MaxEyeHeight = 1.9f;

    /// <summary>The highest height accepted (D33's old ceiling was about 3 m).</summary>
    public const float MaxWallHeight = 4f;

    // Where on the wall each kind of mount sits, as a share of the wall height (so a taller wall keeps the lamps near the top): at 2 m
    // these are 1.85 m (lamps), 1.5 m (signs), 1.1 m (buttons, APCs and the like), 1.7 m (cameras) and 1.2 m (everything else).
    public const float LampShare = 0.925f, SignShare = 0.75f, PanelShare = 0.55f, CameraShare = 0.85f, OtherShare = 0.6f;

    // tg maps an elevator's call button and its lift indicator on one tile at one offset (32 pixels): the indicator's pixels are the top of the frame
    // (rows 1-13), the button's the bottom (rows 16-28). A 2 m wall is that frame, so the two keep tg's places: the indicator at 1.4 m (under the lamp's
    // box), the button at 0.6 m; both 0.75 m tall, they no longer cover each other (the owner's report, 2026-11-09: the indicator took the call clicks).
    public const float LiftIndicatorShare = 0.7f, ElevatorButtonShare = 0.3f;

    /// <summary>
    /// D157 (the owner, 2026-10-18): a table's top is 0.75 m above the floor whatever the eye. M3 R5 (the owner, the third walk: the tables were "almost at eye level")
    /// made it half the standing eye (0.6 m at D120's 1.2 m, 0.75 m at D137's 1.5 m; it was 0.9 m before); D157 lowered the eye to 1.3 m and kept the tables where they were.
    /// Items and machines on a table follow the top.
    /// </summary>
    public const float TableTopHeight = 0.75f;

    /// <summary>D121 (the owner, the second walk): things a hand can hold (<c>/obj/item</c>) are drawn at half the tile scale, so a 32 pixel icon is 1 m and not 2 m.</summary>
    public const float DefaultItemScale = 0.5f;

    /// <summary>
    /// The mobs' scale (the owner, 2026-10-17): a mob's picture is drawn at this share of the tile scale, its drawn pixels standing on the floor, so tg's human
    /// (1.81 m at the tile scale) is about 1.45 m. The first-person eye is set at that human's eyes (D157, <see cref="DefaultEyeHeight"/>); it is its own setting.
    /// </summary>
    public const float MobScale = 0.8f;

    /// <summary>The smallest item scale accepted (a tenth of the tile scale).</summary>
    public const float MinItemScale = 0.1f;

    /// <summary>The largest item scale accepted: the tile scale itself, which turns the D121 halving off.</summary>
    public const float MaxItemScale = 1f;

    private static float _wallHeight = DefaultWallHeight;
    private static float _eyeHeight = DefaultEyeHeight;
    private static float _itemScale = DefaultItemScale;

    /// <summary>The standing eye's height above the floor, metres (D157: 1.3 by default; lying it is <see cref="CameraLayer.LyingEye"/>). The one setting that <see cref="TileSpace.EyeHeight"/> and <see cref="CameraLayer.StandingEye"/> report.</summary>
    public static float EyeHeight => _eyeHeight;

    /// <summary>True when <paramref name="metres"/> is an eye height <see cref="SetEyeHeight"/> takes.</summary>
    public static bool IsValidEyeHeight(float metres) => !float.IsNaN(metres) && metres >= MinEyeHeight && metres <= MaxEyeHeight;

    /// <summary>Sets the standing eye height; false (and nothing changed) when it is outside <see cref="MinEyeHeight"/> to <see cref="MaxEyeHeight"/>.</summary>
    public static bool SetEyeHeight(float metres)
    {
        if (!IsValidEyeHeight(metres))
            return false;
        _eyeHeight = metres;
        return true;
    }

    /// <summary>Back to <see cref="DefaultEyeHeight"/>.</summary>
    public static void ResetEyeHeight() => _eyeHeight = DefaultEyeHeight;

    /// <summary>Reads an eye height in metres (invariant culture, <c>1.2</c>) that <see cref="SetEyeHeight"/> would take.</summary>
    public static bool TryParseEyeHeight(string text, out float metres)
    {
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out metres) && IsValidEyeHeight(metres))
            return true;
        metres = DefaultEyeHeight;
        return false;
    }

    /// <summary>
    /// The scale an item (<c>/obj/item</c>) is drawn at, as a share of the tile scale (D121: 0.5 by default; 1 is the tile scale). One client setting next to the wall
    /// and eye heights: <c>--item-scale=S</c>, the options file's <c>item_scale</c>, <c>--option=item_scale=S</c>. The planner and the shader read it when the world is built;
    /// an item is scaled about the bottom centre of its picture, which rests on its support (the floor, or a table top).
    /// </summary>
    public static float ItemScale => _itemScale;

    /// <summary>True when <paramref name="scale"/> is an item scale <see cref="SetItemScale"/> takes.</summary>
    public static bool IsValidItemScale(float scale) => !float.IsNaN(scale) && scale >= MinItemScale && scale <= MaxItemScale;

    /// <summary>Sets the item scale; false (and nothing changed) when it is outside <see cref="MinItemScale"/> to <see cref="MaxItemScale"/>.</summary>
    public static bool SetItemScale(float scale)
    {
        if (!IsValidItemScale(scale))
            return false;
        _itemScale = scale;
        return true;
    }

    /// <summary>Back to <see cref="DefaultItemScale"/>.</summary>
    public static void ResetItemScale() => _itemScale = DefaultItemScale;

    /// <summary>Reads an item scale (invariant culture, <c>0.5</c>) that <see cref="SetItemScale"/> would take.</summary>
    public static bool TryParseItemScale(string text, out float scale)
    {
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) && IsValidItemScale(scale))
            return true;
        scale = DefaultItemScale;
        return false;
    }

    /// <summary>
    /// D152 (block windows) (<c>BlockFaces</c>): full-tile windows, grilles and girders are drawn as tile-sized blocks showing tg's smoothing kit on each face,
    /// instead of D121's joined slabs. Off unless the client sets it (the options file's <c>block_windows</c>, on by default), read when the world is built.
    /// </summary>
    public static bool BlockPieces { get; private set; }

    /// <summary>Turns the block pieces on or off (before the world is built).</summary>
    public static void SetBlockPieces(bool on) => BlockPieces = on;

    /// <summary>The height of a table's top above the floor, metres (<see cref="TableTopHeight"/>, D157).</summary>
    public static float TableTop => TableTopHeight;

    /// <summary>Metres from a level's floor to its ceiling (and to the floor of the level above).</summary>
    public static float WallHeight => _wallHeight;

    /// <summary>The height (metres above the floor) of a light fixture's centre, and of the shadowed lights the light layer places.</summary>
    public static float LampHeight => _wallHeight * LampShare;

    /// <summary>The height of the centre of a sign or a notice board.</summary>
    public static float SignHeight => _wallHeight * SignShare;

    /// <summary>The height of the centre of a button, an APC, an alarm or a similar panel.</summary>
    public static float PanelHeight => _wallHeight * PanelShare;

    /// <summary>The height of the centre of an elevator's lift indicator (<see cref="LiftIndicatorShare"/>).</summary>
    public static float LiftIndicatorHeight => _wallHeight * LiftIndicatorShare;

    /// <summary>The height of the centre of an elevator's call button (<see cref="ElevatorButtonShare"/>).</summary>
    public static float ElevatorButtonHeight => _wallHeight * ElevatorButtonShare;

    /// <summary>The height of the centre of a wall camera.</summary>
    public static float CameraHeight => _wallHeight * CameraShare;

    /// <summary>The height of the centre of any other wall-mounted picture.</summary>
    public static float OtherMountHeight => _wallHeight * OtherShare;

    /// <summary>True when <paramref name="metres"/> is a height <see cref="SetWallHeight"/> takes.</summary>
    public static bool IsValidWallHeight(float metres) => !float.IsNaN(metres) && metres >= MinWallHeight && metres <= MaxWallHeight;

    /// <summary>Sets the height; false (and nothing changed) when it is outside <see cref="MinWallHeight"/> to <see cref="MaxWallHeight"/>.</summary>
    public static bool SetWallHeight(float metres)
    {
        if (!IsValidWallHeight(metres))
            return false;
        _wallHeight = metres;
        return true;
    }

    /// <summary>Back to <see cref="DefaultWallHeight"/>.</summary>
    public static void ResetWallHeight() => _wallHeight = DefaultWallHeight;

    /// <summary>Reads a height in metres (invariant culture, <c>2</c> or <c>2.4</c>) that <see cref="SetWallHeight"/> would take.</summary>
    public static bool TryParseWallHeight(string text, out float metres)
    {
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out metres) && IsValidWallHeight(metres))
            return true;
        metres = DefaultWallHeight;
        return false;
    }

    /// <summary>
    /// The centre height at which a flat picture <paramref name="pictureHeight"/> metres tall, wanted with its centre at <paramref name="centre"/>,
    /// sits wholly on a wall <paramref name="wallHeight"/> tall: at least <paramref name="margin"/> above the floor and below the ceiling.
    /// A picture taller than the wall allows is centred on the wall.
    /// </summary>
    public static float FitMount(float centre, float pictureHeight, float wallHeight, float margin = 0.04f)
    {
        float half = pictureHeight / 2f;
        float low = half + margin, high = wallHeight - half - margin;
        if (low > high)
            return wallHeight / 2f;
        return Math.Clamp(centre, low, high);
    }

    /// <summary>
    /// DV122 (M8x): the scale of a standing picture whose drawn pixels reach <paramref name="drawnTop"/> metres above the floor at the tile
    /// scale, so that they end under a ceiling <paramref name="wallHeight"/> high (tg's 32 x 64 cryo cell is two walls high); 1 for one that fits.
    /// </summary>
    public static float FitUnderCeiling(float drawnTop, float wallHeight) => drawnTop > wallHeight ? wallHeight / drawnTop : 1f;
}
