using Anomaly.Core.Motion;

namespace Anomaly.Client.Shared.World;

// Owner: M3 R5 (D122). Engine-free. tg's planes and layers (code/__DEFINES/layers.dm) decide which things lie flat on the tile and in which order they are stacked.

/// <summary>
/// The under-floor layers of D122. In tg's top-down view the lattice, the disposal pipes, the wires and the catwalks are on the FLOOR plane (the <c>TOPDOWN_LAYER</c> block, layers
/// 10001 to 10018 and the decals among them), and the pipes, wire terminals, vents, scrubbers, filters, pumps and ducts are on the game plane at layers 2.35 to 2.5 (below
/// <c>LOW_OBJ_LAYER</c>), under every object of the room. In first person they are the layers below the floor tile: a thing of these layers lies flat on its tile, turned by
/// its dir, and the things on one tile are stacked by their layer, a few millimetres apart, so that none fights with another or with the floor.
/// <para>
/// The decision is made from the replicated appearance (<c>plane</c> and <c>layer</c>: the real server sends the atom's own, with the undertile element's changes) or, in the fake
/// feed, from the type's class; a type's path is consulted only for the floor hatches (<see cref="PropKinds.IsFloorHatch"/>) when no plane or layer is known.
/// </para>
/// </summary>
public static class FloorLayers
{
    /// <summary>tg's <c>FLOOR_PLANE</c>: everything on it is part of the floor.</summary>
    public const int FloorPlane = -6;

    /// <summary>tg's <c>GAME_PLANE</c>.</summary>
    public const int GamePlane = -4;

    /// <summary>tg's <c>ABOVE_GAME_PLANE</c>: a navigation computer's landing zone is drawn on it (DV382).</summary>
    public const int AboveGamePlane = -3;

    /// <summary>tg's <c>ABOVE_NORMAL_TURF_LAYER</c> and <c>NAVIGATION_EYE_LAYER</c>: a navigation computer's landing zone, its tiles and the designated spot (DV382).</summary>
    public const float AboveNormalTurf = 2.08f, NavigationEye = 4.9f;

    /// <summary>BYOND's <c>FLOAT_PLANE</c>: the plane of an image made with <c>image()</c>, drawn on the plane of the atom it is on.</summary>
    public const int FloatPlane = -32767;

    /// <summary>tg's <c>LOWEST_EVER_PLANE</c> and <c>PLANE_RANGE</c> (<c>HIGHEST_EVER_PLANE - LOWEST_EVER_PLANE</c>, 57 - -50): a plane below the lowest is offset by a multiple of the range for a level of a multi-z stack.</summary>
    public const int LowestEverPlane = -50, PlaneRange = 107;

    /// <summary>tg's <c>TOPDOWN_LAYER</c>: the layers of the floor plane are this plus a small number.</summary>
    public const float TopdownLayer = 10000f;

    /// <summary>tg's <c>TOPDOWN_LAYER_COUNT</c>: the highest topdown layer's offset (<c>CLEANABLE_FLOOR_OBJECT_LAYER</c>).</summary>
    public const int TopdownLayerCount = 18;

    /// <summary>tg's floor-plane layers that things lie on (the offsets above <see cref="TopdownLayer"/>).</summary>
    public const int TurfDecalOffset = 4, HighTurfOffset = 7, LatticeOffset = 8, DisposalPipeOffset = 9, WireOffset = 10, BelowCatwalkOffset = 11, GlassFloorOffset = 12, CatwalkOffset = 13;

    /// <summary>tg's game-plane layers of the pipes and what is on them (<c>GAS_PIPE_HIDDEN_LAYER</c> to <c>LOW_OBJ_LAYER</c>).</summary>
    public const float GasPipeHidden = 2.35f, WireBridge = 2.44f, WireTerminal = 2.45f, GasScrubber = 2.46f, GasPipeVisible = 2.47f, GasFilter = 2.48f, GasPump = 2.49f, PlumbingPipeVisible = 2.495f,
        LowObj = 2.5f, HighPipe = 2.54f;

    /// <summary>tg's <c>TRAM_FLOOR_LAYER</c>: an elevator's or a tram's platform, a floor that moves (D325); it lies flat over everything of its tile's floor.</summary>
    public const float TramFloor = 2.58f;

    /// <summary>tg's <c>CLEANABLE_OBJECT_LAYER</c> and <c>GIB_LAYER</c>: the game-plane layers of some cleanables (ash, garbage; gibs, rubble).</summary>
    public const float CleanableObject = 2.55f, Gib = 2.85f;

    /// <summary>tg's dirt on the floor: blood, ash, ants, gibs and the like.</summary>
    public const string CleanableType = "/obj/effect/decal/cleanable";

    /// <summary>
    /// The lowest game-plane layer that counts as a pipe layer: a pipe's layer is <see cref="GasPipeHidden"/> moved by its piping layer (0.001 each way) and its colour (0.0001 each), so a
    /// hair under it is still a pipe.
    /// </summary>
    public const float PipeBandStart = 2.34f;

    /// <summary>Metres a thing of the lowest rank floats above the floor tile, and the step between ranks: 1.5 mm, so the ranks 0 to 28 are a little under 5 cm at most.</summary>
    public const float BaseLift = 0.004f, RankStep = 0.0015f;

    /// <summary>tg's plane with the offset of a multi-z level taken off (<c>PLANE_TO_TRUE</c>: a level's planes are the true plane minus <see cref="PlaneRange"/> per level).</summary>
    public static int TruePlane(float plane)
    {
        int p = (int)MathF.Round(plane);
        if (p >= LowestEverPlane)
            return p;
        int levels = (LowestEverPlane - p + PlaneRange - 1) / PlaneRange;
        return p + levels * PlaneRange;
    }

    /// <summary>
    /// The stacking rank of a thing that lies on the floor: higher draws above lower, in tg's layer order (the lowest decals, the lattice, the disposal pipes, the wires, below the catwalk, the
    /// glass floor and catwalk, what lies on the open turf, the cleanables; then on the game plane the hidden pipes by piping layer, the wire bridge and terminal, the vents and
    /// scrubbers, the visible pipes, the filters, the pumps and the ducts). Fractions order the variants of one layer (a cable's three layers, a pipe's five). -1: it does not lie flat.
    /// </summary>
    /// <param name="plane">The appearance's plane (0 when unknown: a type of the game plane, or of the floor plane when the layer is in the topdown block).</param>
    /// <param name="layer">The appearance's layer (0 when unknown).</param>
    /// <param name="typePath">The DM type, consulted for the hatches only when the layer is unknown.</param>
    /// <param name="iconState">The icon state, when known: a ladder linked to nothing lies on its tile.</param>
    public static float Rank(float plane, float layer, string typePath = null, string iconState = null)
    {
        // plane 0 (none known) or FLOAT_PLANE (an image made with image() takes its atom's plane: tg's engineering goggles' thermal overlay and pipe connection arrows):
        // the topdown block of layers is the floor plane's, anything else the game plane's
        int truePlane = plane == 0f || plane == FloatPlane ? (layer >= TopdownLayer ? FloorPlane : GamePlane) : TruePlane(plane);
        if (truePlane == FloorPlane)
        {
            float t = layer - TopdownLayer;
            if (t >= 0f && t <= TopdownLayerCount + 0.5f)
            {
                int tier = (int)MathF.Round(t);
                // The layers below the lattice are the turf's own and its decals: the lowest rank; from the lattice up one rank each.
                return Math.Max(0f, Math.Max(0, tier - HighTurfOffset) + Sub(t - tier));
            }
            return -1f; // a floor-plane thing with a layer outside the topdown block (not made by tg): leave it standing
        }
        const int gameBase = TopdownLayerCount - HighTurfOffset + 1; // the game plane's flat things come after the floor plane's
        if (truePlane == GamePlane)
        {
            if (layer >= PipeBandStart && layer < LowObj)
            {
                // The pipe layers: hidden pipes first, the pumps and ducts last.
                float k = (layer - GasPipeHidden) / 0.01f; // 0 for the hidden pipes, 12 for the visible, 14 for the pumps
                int g = (int)MathF.Floor(k + 0.5f);
                return gameBase + Math.Max(0, g) + Sub((k - g) * 0.01f);
            }
            if (layer >= HighPipe - 0.005f && layer < HighPipe + 0.005f)
                return gameBase + 16 + Sub(layer - HighPipe);
            if (layer >= TramFloor - 0.005f && layer < TramFloor + 0.005f)
                return gameBase + 17;
            // The cleanables tg moves to the game plane (the ants at LOW_OBJ_LAYER; ash, garbage, a chemical pile, wrapping shreds at CLEANABLE_OBJECT_LAYER; gibs and rubble
            // at GIB_LAYER) are still dirt on the floor: they lie on it, in that order, over a tram's platform. A cobweb (WALL_OBJ_LAYER) and the splatters on windows
            // (ABOVE_WINDOW_LAYER) stay upright.
            if (typePath != null && PropKinds.Is(typePath, CleanableType))
            {
                if (layer >= LowObj - 0.005f && layer < LowObj + 0.005f)
                    return gameBase + 17.25f;
                if (layer >= CleanableObject - 0.005f && layer < CleanableObject + 0.005f)
                    return gameBase + 17.5f;
                if (layer >= Gib - 0.005f && layer < Gib + 0.005f)
                    return gameBase + 17.75f;
            }
        }
        // DEVIATION(DV382): a navigation computer's landing zone (images on ABOVE_GAME_PLANE: its tiles and the airlocks, consoles and turrets on
        // them at ABOVE_NORMAL_TURF_LAYER, the designated spot at NAVIGATION_EYE_LAYER) is a map of tiles seen from above: it lies on its tiles,
        // over everything else on the floor
        if (truePlane == AboveGamePlane)
        {
            if (layer >= AboveNormalTurf - 0.005f && layer < AboveNormalTurf + 0.005f)
                return gameBase + 18;
            if (layer >= NavigationEye - 0.005f && layer < NavigationEye + 0.005f)
                return gameBase + 19;
        }
        // A hatch whose layer says nothing (a type source with no layer): by its type, at the scrubbers' layer.
        if (typePath != null && PropKinds.IsFloorHatch(typePath))
            return gameBase + 11;
        // DV383: a tile mark (the greenscreen camera's) lies over everything else on its tile's floor, as the navigation computer's zone does;
        // a ladder linked to nothing (every ladder of a single-level station) is tg's picture of a ladder lying down (the owner, 2026-11-14)
        if (typePath != null && (PropKinds.IsFloorMark(typePath) || iconState == LadderBody.LyingState && PropKinds.Is(typePath, "/obj/structure/ladder")))
            return gameBase + 18;
        return -1f;
    }

    // The order of the variants of one layer, as a share of a rank (under 1, so no variant reaches the next layer): a pipe's piping layer moves its layer by 0.001 each and its
    // colour by 0.0001, a cable's layer by 0.01 (and a multilayer hub by 0.02).
    private static float Sub(float frac)
    {
        float a = Math.Abs(frac);
        float share = a <= 0.006f ? Math.Min(a * 150f, 0.75f) : 0.5f + Math.Min(a - 0.006f, 0.02f) * 20f;
        return frac < 0f ? -share : share;
    }

    /// <summary>The height above the floor (metres) of a thing tuned to lie on it (<see cref="WorldLooks"/>' floor mode): the gibs' rank, over the floor's other things.</summary>
    public static float TunedLift => LiftOf(TopdownLayerCount - HighTurfOffset + 1 + 17.75f);

    /// <summary>The height above the floor (metres) of a thing of this rank: <see cref="BaseLift"/> plus <see cref="RankStep"/> per rank.</summary>
    public static float LiftOf(float rank) => BaseLift + Math.Max(0f, rank) * RankStep;

    /// <summary>
    /// Whether a thing of this plane and layer lies flat on its tile, and how high above the floor (metres). A thing that is not on the floor plane or in the pipe band stays
    /// as it was (an upright billboard, an item, a piece, a wall mount).
    /// </summary>
    public static bool TryLift(float plane, float layer, string typePath, out float lift, string iconState = null)
    {
        float rank = Rank(plane, layer, typePath, iconState);
        lift = rank < 0f ? 0f : LiftOf(rank);
        return rank >= 0f;
    }
}
