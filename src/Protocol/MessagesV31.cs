namespace Anomaly.Protocol;

// Version 31 (M7 slice T3, D164, DV81, protocol.md section 40): the own mob's vision. tg's /mob/living/update_sight sets the mob's
// `sight` flags (SEE_TURFS from TRAIT_MESON_VISION, SEE_MOBS, SEE_OBJS, BLIND), its `lighting_cutoff` and its `lighting_color_cutoffs`
// (a percentage of the lighting plane's brightness the dark is lifted to, in all and by colour channel: the eyes, night vision
// glasses, the mesons); tg's lighting plane master turns the cutoffs into colour matrix filters and the client's BYOND shows the
// turfs of SEE_TURFS through the dark and through walls. The port has no plane masters, so the private block names the three values
// when they change and the 3D client does the same with its light grid and its turf geometry.
//
// Version 31 was taken by another branch (the projectile base) and is not used here.

/// <summary>The BYOND sight flags the client reads (BYOND's own values: <c>SEE_MOBS</c> 4, <c>SEE_OBJS</c> 8, <c>SEE_TURFS</c> 16).</summary>
public static class SightFlags
{
    public const int Blind = 1;
    public const int SeeMobs = 4;
    public const int SeeObjs = 8;
    public const int SeeTurfs = 16;
}

/// <summary>Version 31: the own mob's sight flags and lighting cutoffs (tg's <c>sight</c>, <c>lighting_cutoff</c>, <c>lighting_color_cutoffs</c>).</summary>
/// <param name="Sight">BYOND's <c>sight</c> bit field (0 to 2^24 - 1).</param>
/// <param name="LightingCutoff">tg's <c>lighting_cutoff</c> in percent (0 to 100): the floor of the lighting plane, every channel.</param>
/// <param name="CutoffRed">tg's <c>lighting_color_cutoffs[1]</c> in percent (0 to 100): added to the floor of the red channel.</param>
/// <param name="CutoffGreen">tg's <c>lighting_color_cutoffs[2]</c> in percent.</param>
/// <param name="CutoffBlue">tg's <c>lighting_color_cutoffs[3]</c> in percent.</param>
public readonly record struct VisionState(int Sight, float LightingCutoff, float CutoffRed, float CutoffGreen, float CutoffBlue)
{
    /// <summary>A mob that sees as a person does: no flags, no cutoff.</summary>
    public static readonly VisionState Normal = new(0, 0f, 0f, 0f, 0f);

    /// <summary>The floor of the lighting plane per channel (0 to 1), as tg's <c>set_light_cutoff</c> builds it: the cutoff plus the channel's cutoff over 100.</summary>
    public (float R, float G, float B) LightFloor => (Clamp01((LightingCutoff + CutoffRed) / 100f), Clamp01((LightingCutoff + CutoffGreen) / 100f), Clamp01((LightingCutoff + CutoffBlue) / 100f));

    /// <summary>Whether the mob sees the turfs (tg's <c>SEE_TURFS</c>: the mesons' sight).</summary>
    public bool SeesTurfs => (Sight & SightFlags.SeeTurfs) != 0;

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}

public sealed partial class OwnBlock
{
    /// <summary>Version 31: the block carries <see cref="Vision"/> (it changed since the last block, or this is a Snapshot's whose mob does not see as a person does).</summary>
    public bool VisionChanged { get; set; }

    /// <summary>Version 31: the own mob's vision.</summary>
    public VisionState Vision { get; set; }
}

public sealed partial class ReplicaWorld
{
    /// <summary>Version 31: the own mob's sight flags and lighting cutoffs; <see cref="VisionState.Normal"/> when the server named none.</summary>
    public VisionState OwnVision { get; private set; }
}
