using System;

namespace Anomaly.Client.Shared.Play;

// The owner (2026-10-29): a hit's particles by what was hit, not blood on a wall. Engine-free.

/// <summary>What a hit thing is made of, as its hit particles show it.</summary>
public enum HitMaterial
{
    Metal,
    Glass,
    Wood,
    Stone,
    Flesh,
    AlienFlesh,
    Electronics,
}

/// <summary>
/// The look of a material's hit burst: its debris colour and count, how fast and how far apart the pieces fly, how long they live, how hard
/// they fall, and the bright sparks thrown with them (none for a soft thing).
/// </summary>
public readonly record struct HitDebris(
    float R, float G, float B, int Amount, float SpeedMin, float SpeedMax, float Spread, float Lifetime, float Gravity, int Sparks);

/// <summary>Which material a hit entity or turf is (by its tg type path) and how its burst looks.</summary>
public static class HitMaterials
{
    // shortcut: tg type path prefixes, not tg's custom_materials; replicate the materials if a thing is drawn with the wrong debris.
    private static readonly (string Prefix, HitMaterial Material)[] Things =
    {
        ("/mob/living/silicon", HitMaterial.Electronics),
        ("/mob/living/basic/bot", HitMaterial.Electronics),
        ("/mob/living/basic/drone", HitMaterial.Electronics),
        ("/mob/living/carbon/alien", HitMaterial.AlienFlesh),
        ("/mob", HitMaterial.Flesh),
        ("/obj/structure/window", HitMaterial.Glass),
        ("/obj/structure/table/glass", HitMaterial.Glass),
        ("/obj/structure/table/reinforced/rglass", HitMaterial.Glass),
        ("/obj/machinery/door/window", HitMaterial.Glass),
        ("/obj/item/shard", HitMaterial.Glass),
        ("/obj/structure/table/wood", HitMaterial.Wood),
        ("/obj/structure/chair/wood", HitMaterial.Wood),
        ("/obj/structure/closet/crate/wooden", HitMaterial.Wood),
        ("/obj/structure/closet/cabinet", HitMaterial.Wood),
        ("/obj/structure/barricade/wooden", HitMaterial.Wood),
        ("/obj/structure/bookcase", HitMaterial.Wood),
        ("/obj/structure/dresser", HitMaterial.Wood),
        ("/obj/structure/mineral_door/wood", HitMaterial.Wood),
        ("/obj/structure/flora/tree", HitMaterial.Wood),
        ("/obj/item/stack/sheet/mineral/wood", HitMaterial.Wood),
        ("/obj/structure/flora/rock", HitMaterial.Stone),
        ("/obj/structure/statue", HitMaterial.Stone),
        ("/obj/machinery/computer", HitMaterial.Electronics),
        ("/obj/machinery/modular_computer", HitMaterial.Electronics),
        ("/obj/machinery/vending", HitMaterial.Electronics),
        ("/obj/machinery/power/apc", HitMaterial.Electronics),
        ("/obj/machinery/camera", HitMaterial.Electronics),
    };

    private static readonly (string Prefix, HitMaterial Material)[] Turfs =
    {
        ("/turf/closed/wall/mineral/wood", HitMaterial.Wood),
        ("/turf/closed/wall/mineral/bamboo", HitMaterial.Wood),
        ("/turf/closed/wall/mineral/sandstone", HitMaterial.Stone),
        ("/turf/closed/wall/rock", HitMaterial.Stone),
        ("/turf/closed/mineral", HitMaterial.Stone),
        ("/turf/closed/indestructible/rock", HitMaterial.Stone),
        ("/turf/open/floor/wood", HitMaterial.Wood),
        ("/turf/open/floor/bamboo", HitMaterial.Wood),
        ("/turf/open/floor/glass", HitMaterial.Glass),
        ("/turf/open/misc", HitMaterial.Stone),
    };

    /// <summary>An entity's material by its type path (a thing of no listed kind is metal).</summary>
    public static HitMaterial OfThing(string typePath) => Find(Things, typePath);

    /// <summary>A turf's material by its type path (a wall or floor of no listed kind is metal).</summary>
    public static HitMaterial OfTurf(string typePath) => Find(Turfs, typePath);

    private static HitMaterial Find((string Prefix, HitMaterial Material)[] rules, string typePath)
    {
        if (typePath != null)
            foreach ((string prefix, HitMaterial material) in rules)
                if (typePath.StartsWith(prefix, StringComparison.Ordinal)
                    && (typePath.Length == prefix.Length || typePath[prefix.Length] == '/'))
                    return material;
        return HitMaterial.Metal;
    }

    /// <summary>A material's burst (the first two are the blood red and the light grey chips the burst had before materials).</summary>
    public static HitDebris Look(HitMaterial material) => material switch
    {
        HitMaterial.Flesh => new(0.62f, 0.04f, 0.04f, 16, 1.2f, 2.6f, 75f, 0.45f, 9.8f, 0),
        HitMaterial.AlienFlesh => new(0.45f, 0.62f, 0.05f, 16, 1.2f, 2.6f, 75f, 0.45f, 9.8f, 0),
        HitMaterial.Glass => new(0.78f, 0.9f, 0.96f, 14, 1.8f, 3.4f, 60f, 0.5f, 12f, 0),
        HitMaterial.Wood => new(0.46f, 0.3f, 0.15f, 12, 1.0f, 2.2f, 70f, 0.5f, 9.8f, 0),
        HitMaterial.Stone => new(0.52f, 0.47f, 0.4f, 18, 0.6f, 1.6f, 85f, 0.6f, 6f, 0),
        HitMaterial.Electronics => new(0.3f, 0.32f, 0.34f, 8, 1.2f, 2.4f, 70f, 0.4f, 9.8f, 10),
        _ => new(0.55f, 0.57f, 0.6f, 10, 1.2f, 2.6f, 70f, 0.4f, 9.8f, 4),
    };

    /// <summary>The bright sparks thrown with a hard thing's debris (and, at a shot's impact, in the shot's colour).</summary>
    public static readonly (float R, float G, float B) Spark = (1f, 0.88f, 0.4f);
}
