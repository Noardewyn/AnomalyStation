using System.Globalization;
using System.Linq;
using Anomaly.Client.Shared.World;

namespace Anomaly.Client.Shared.Models;

/// <summary>One part a model draws for an entity, and its yaw (radians about +Y, counter-clockwise seen from above; 0 leaves the model as its file has it).</summary>
public readonly record struct ModelPlacement(string Part, float Yaw);

/// <summary>The rules that pick a model's parts for an entity (<see cref="ModelRule"/>).</summary>
public static class ModelRules
{
    /// <summary>The kit's neighbour bits (the modular table's README): north (-Z) 1, east (+X) 2, south (+Z) 4, west (-X) 8.</summary>
    public const int North = 1, East = 2, South = 4, West = 8;

    /// <summary>tg's junction bits (code/__DEFINES/icon_smoothing.dm): the cardinals are the BYOND dirs (NORTH 1, SOUTH 2, EAST 4, WEST 8), the diagonals 16 .. 128.</summary>
    public const int TgNorth = 1, TgSouth = 2, TgEast = 4, TgWest = 8, TgNorthEast = 16, TgSouthEast = 32, TgSouthWest = 64, TgNorthWest = 128;

    /// <summary>The smoothing modules: an isolated cell, an end, a straight run, a corner whose diagonal is joined too (a block), a corner whose diagonal is not (an L), a tee, the middle.</summary>
    /// <summary>A closet's parts: its body, its door's front and the door's back (the materials of one door mesh, each its own part with its own picture).</summary>
    public const string Body = "body", DoorFront = "doorFront", DoorInside = "doorInside";

    /// <summary>A <see cref="ModelRule.Dir8"/> entry's diagonal part (made for southeast).</summary>
    public const string Corner8 = "corner";

    /// <summary>A crate's inside (its floor is where things in an open crate rest).</summary>
    public const string Inside = "inside";

    /// <summary>A computer's overlays as parts: its screen and its keyboard.</summary>
    public const string Screen = "screen", Keyboard = "keyboard";

    public const string Single = "single", End = "end", Straight = "straight", Corner = "corner", CornerOpen = "cornerOpen", Tee = "tee", Center = "center";

    /// <summary>The parts a rule needs.</summary>
    public static string[] PartsOf(ModelRule rule) => rule switch
    {
        ModelRule.Airlock => Array.Empty<string>(), // any parts: leaves, sections, a frame
        ModelRule.Crate => new[] { Body },
        ModelRule.Console => new[] { Body, Screen, Keyboard },
        ModelRule.Smooth4 => new[] { Single, End, Straight, Corner, CornerOpen, Tee, Center },
        ModelRule.Closet => new[] { Body, DoorFront }, // more leaves: doorFront2, doorInside2 ...; a door's back is optional
        ModelRule.Dir8 => new[] { "model" }, // "corner" for the diagonals
        ModelRule.Wall => new[] { "model" },
        ModelRule.Sun => Array.Empty<string>(), // any parts: the pedestal's, and the head's (named head...)
        _ => new[] { "model" },
    };

    /// <summary>
    /// The parts an entry draws: <paramref name="iconState"/> is the appearance's icon state (a smoothing structure's junction), <paramref name="dir"/> the entity's
    /// dir, <paramref name="face"/> the face the planner gave a door (<see cref="ChunkPlanner"/>) or the way a closet looks (<see cref="FaceAwayFromWall"/>).
    /// </summary>
    public static ModelPlacement[] Choose(ModelEntry entry, string iconState, byte dir, byte face, float azimuth = 0f)
    {
        ModelPlacement[] placed = entry.Rule == ModelRule.Sun
            ? entry.Parts.Keys.Select(part => new ModelPlacement(part, part.StartsWith(SunHead, StringComparison.Ordinal) ? AzimuthYaw(azimuth) : 0f)).ToArray()
            : ChooseUnturned(entry, iconState, dir, face);
        return placed.Select(p =>
        {
            float degrees = entry.Turn + (entry.Parts.TryGetValue(p.Part, out ModelPart part) ? part.Turn : 0f);
            return degrees == 0f ? p : p with { Yaw = p.Yaw + degrees * MathF.PI / 180f };
        }).ToArray();
    }

    /// <summary>The part names of a <see cref="ModelRule.Sun"/> entry that turn.</summary>
    public const string SunHead = "head";

    /// <summary>
    /// The sun's height over the station's plane (degrees) in the client's sky (Starfield). tg's sun has an azimuth only (SSsun, 2D); the sky draws it this high, and a
    /// head turned to its azimuth tilts back by 90 degrees less this so its face looks at it (<see cref="SunHeadMotion.TiltFor"/>).
    /// </summary>
    public const float SunElevationDegrees = 35f;

    /// <summary>
    /// The yaw of a head that faces the compass bearing <paramref name="azimuthDegrees"/> (tg's azimuth: clockwise from north, 90 east), for a model whose front looks
    /// south (+Z) unturned: its front at 180 degrees (south) is not turned.
    /// </summary>
    public static float AzimuthYaw(float azimuthDegrees) => (180f - azimuthDegrees) * MathF.PI / 180f;

    /// <summary>
    /// The azimuth that the transform of tg's panel or dish overlay carries: <c>get_panel_transform</c> and <c>get_tracker_transform</c> turn by the angle first, then shear
    /// and squash the vertical axis only, so the matrix's first row stays (cos, sin) of it (BYOND's a and b).
    /// </summary>
    public static float AzimuthOf(float a, float b) => MathF.Atan2(b, a) * 180f / MathF.PI;

    private static ModelPlacement[] ChooseUnturned(ModelEntry entry, string iconState, byte dir, byte face)
    {
        switch (entry.Rule)
        {
            case ModelRule.Airlock:
                // a two-tile door turns its +x toward its filler: east (unturned) for NORTH or SOUTH, north (a quarter turn west) for EAST or WEST
                float yaw = entry.MultiTile ? (dir is ByondDirConst.East or ByondDirConst.West ? TileSpace.DirYaw(ByondDirConst.West) : 0f)
                    : TileSpace.DirYaw(face == 0 ? ByondDirConst.North : face);
                return entry.Parts.Keys.Select(part => new ModelPlacement(part, yaw)).ToArray();
            case ModelRule.Crate:
                float crateYaw = TileSpace.DirYaw(face == 0 ? ByondDirConst.South : face) - TileSpace.DirYaw(ByondDirConst.South);
                return entry.Parts.Keys.Select(part => new ModelPlacement(part, crateYaw)).ToArray();
            case ModelRule.Dir4:
                // The model looks south unturned; BYOND's dir is where the thing faces.
                return new[] { new ModelPlacement("model", TileSpace.DirYaw(dir == 0 ? ByondDirConst.South : dir) - TileSpace.DirYaw(ByondDirConst.South)) };
            case ModelRule.Console:
                float consoleYaw = TileSpace.DirYaw(dir == 0 ? ByondDirConst.South : dir) - TileSpace.DirYaw(ByondDirConst.South);
                return new[] { new ModelPlacement(Body, consoleYaw), new ModelPlacement(Screen, consoleYaw), new ModelPlacement(Keyboard, consoleYaw) };
            case ModelRule.Closet:
                float closetYaw = TileSpace.DirYaw(face == 0 ? ByondDirConst.South : face) - TileSpace.DirYaw(ByondDirConst.South);
                return new[] { new ModelPlacement(Body, closetYaw), new ModelPlacement(DoorFront, closetYaw), new ModelPlacement(DoorInside, closetYaw) };
            case ModelRule.Dir8:
            {
                byte d = dir == 0 ? ByondDirConst.South : dir;
                bool diagonal = (d & (ByondDirConst.North | ByondDirConst.South)) != 0 && (d & (ByondDirConst.East | ByondDirConst.West)) != 0;
                if (diagonal && entry.Parts.ContainsKey(Corner8))
                    return new[] { new ModelPlacement(Corner8, TileSpace.DirYaw(d) - TileSpace.DirYaw(ByondDirConst.South | ByondDirConst.East)) };
                return new[] { new ModelPlacement("model", TileSpace.DirYaw(d) - TileSpace.DirYaw(ByondDirConst.South)) };
            }
            case ModelRule.Wall:
                return entry.Parts.Keys.Select(part => new ModelPlacement(part, TileSpace.DirYaw(face == 0 ? ByondDirConst.North : face))).ToArray();
            case ModelRule.Smooth4:
                (string part, int turns) = Smooth4(Junction(iconState));
                return new[] { new ModelPlacement(part, turns * MathF.PI / 2f) };
            default:
                // A single model looks south unturned and faces its dir, as a dir4 one (DV56): the catalog's "dir" rule only sets that dir on the server (a
                // vending machine made facing away from the wall), and the dev rotate turns any of them (the owner, 2026-10-20: a microwave on a table).
                // Its other parts come with it, each drawn while the appearance has its overlays (ModelPart.Overlays: a canister's lights and held tank).
                float singleYaw = TileSpace.DirYaw(dir == 0 ? ByondDirConst.South : dir) - TileSpace.DirYaw(ByondDirConst.South);
                return entry.Parts.Keys.Where(p => p != "model").Prepend("model").Select(p => new ModelPlacement(p, singleYaw)).ToArray();
        }
    }

    /// <summary>
    /// The way a closet looks (a 3D deviation, docs/ai/specs/models.md: tg draws every closet from the front, so one by a west wall would show its side to the room): away
    /// from the wall behind it, north first (south: the way tg draws it), then west, east and south; south when no wall touches it.
    /// </summary>
    public static byte FaceAwayFromWall(Func<byte, bool> isWall)
    {
        if (isWall(ByondDirConst.North))
            return ByondDirConst.South;
        if (isWall(ByondDirConst.West))
            return ByondDirConst.East;
        if (isWall(ByondDirConst.East))
            return ByondDirConst.West;
        if (isWall(ByondDirConst.South))
            return ByondDirConst.North;
        return ByondDirConst.South;
    }

    /// <summary>
    /// Which way a door the door animation does not drive (a firelock) is going by its icon state (firedoor.dm: <c>door_opening</c> and <c>door_closing</c>
    /// while it moves, then <c>door_open</c> or <c>door_closed</c>): true toward open, false toward shut.
    /// </summary>
    public static bool SlidesOpen(string iconState)
        => iconState != null && (iconState.EndsWith("open", StringComparison.Ordinal) || iconState.EndsWith("opening", StringComparison.Ordinal));

    /// <summary>tg's junction an icon state ends with (<c>table-38</c>: 38); 0 for a state without one.</summary>
    public static int Junction(string iconState)
    {
        if (string.IsNullOrEmpty(iconState))
            return 0;
        int dash = iconState.LastIndexOf('-');
        return dash >= 0 && int.TryParse(iconState.AsSpan(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int junction) ? junction : 0;
    }

    /// <summary>The kit's neighbour mask of tg's junction (the four cardinals).</summary>
    public static int Mask(int junction)
        => ((junction & TgNorth) != 0 ? North : 0) | ((junction & TgEast) != 0 ? East : 0) | ((junction & TgSouth) != 0 ? South : 0) | ((junction & TgWest) != 0 ? West : 0);

    /// <summary>
    /// The sides a smoothing kit's module joins unturned (<see cref="Smooth4"/>): an end east, a straight run east and west, a corner north and east, a tee north,
    /// east and west, the centre all four, a single none.
    /// </summary>
    public static int JoinsOf(string part) => part switch
    {
        End => East,
        Straight => East | West,
        Corner or CornerOpen => North | East,
        Tee => North | East | West,
        Center => North | East | South | West,
        _ => 0,
    };

    /// <summary>
    /// tg's junction (its bits: <see cref="TgNorth"/> ...) a smoothing kit's module shows joined on the sides <paramref name="joins"/> (ours: <see cref="North"/> ...):
    /// the cardinals, and a diagonal between two joined cardinals where the module fills it (a closed corner, a tee, the centre); an open corner leaves it out.
    /// </summary>
    public static int TgJunction(string part, int joins)
    {
        int junction = ((joins & North) != 0 ? TgNorth : 0) | ((joins & South) != 0 ? TgSouth : 0) | ((joins & East) != 0 ? TgEast : 0) | ((joins & West) != 0 ? TgWest : 0);
        if (part is Corner or Tee or Center)
        {
            if ((joins & (North | East)) == (North | East)) junction |= TgNorthEast;
            if ((joins & (South | East)) == (South | East)) junction |= TgSouthEast;
            if ((joins & (South | West)) == (South | West)) junction |= TgSouthWest;
            if ((joins & (North | West)) == (North | West)) junction |= TgNorthWest;
        }
        return junction;
    }

    /// <summary>A mask turned a quarter turn counter-clockwise seen from above (a positive yaw): east goes north, north west, west south, south east.</summary>
    public static int TurnLeft(int mask)
        => ((mask & East) != 0 ? North : 0) | ((mask & North) != 0 ? West : 0) | ((mask & West) != 0 ? South : 0) | ((mask & South) != 0 ? East : 0);

    /// <summary>
    /// The module and its quarter turns (counter-clockwise) for tg's junction. Unturned the modules join: an end east, a straight run east and west, a corner north and
    /// east, a tee north, east and west. A corner is the closed one (one leg) when the diagonal between its two joins is joined too, as in a block of tables, and the open one
    /// (two legs, the second by the inner corner) when it is not, as in an L: tg sets a diagonal junction only when both its cardinals are joined.
    /// </summary>
    public static (string Part, int Turns) Smooth4(int junction)
    {
        int mask = Mask(junction);
        switch (mask)
        {
            case 0:
                return (Single, 0);
            case North | East | South | West:
                return (Center, 0);
        }
        int count = BitCount(mask);
        int baseMask = count switch
        {
            1 => East,
            2 => (mask & (North | South)) == (North | South) || (mask & (East | West)) == (East | West) ? East | West : North | East,
            _ => North | East | West,
        };
        int turned = baseMask;
        for (int turns = 0; turns < 4; turns++)
        {
            if (turned == mask)
            {
                if (count == 1)
                    return (End, turns);
                if (count == 3)
                    return (Tee, turns);
                if (baseMask == (East | West))
                    return (Straight, turns);
                // The corner's diagonal: north-east unturned, turned with it.
                int diagonal = DiagonalOf(mask);
                return ((junction & diagonal) != 0 ? Corner : CornerOpen, turns);
            }
            turned = TurnLeft(turned);
        }
        throw new InvalidOperationException("no turn of " + baseMask + " gives " + mask);
    }

    // tg's diagonal junction between a corner's two joins.
    private static int DiagonalOf(int mask) => mask switch
    {
        North | East => TgNorthEast,
        East | South => TgSouthEast,
        South | West => TgSouthWest,
        _ => TgNorthWest,
    };

    private static int BitCount(int mask) => ((mask >> 0) & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1) + ((mask >> 3) & 1);
}
