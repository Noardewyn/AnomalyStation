using System;
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

/// <summary>
/// DEVIATION(DV96, the owner 2026-11-21: most guns kicked like nothing, the anti-tank rifle snapped): the kick a gun's shot gives its shooter's first-person
/// view, on top of tg's own <c>recoil</c>. tg gives almost every gun none (a ballistic gun's 0.1-tile <c>min_recoil</c>, an energy gun's 0), which in 2D
/// is fine and in first person feels like a toy. The kick is the gun's kind's (tg's type tree: a pistol, a revolver, a rifle ...) scaled by the square
/// root of its round's damage against that kind's usual round, harder fired one-handed (a gun of tg's WEAPON_MEDIUM or heavier with the other hand full).
/// Degrees of view turn; <see cref="TilesOf"/> gives tg's tiles of recoil offset (D179: 3 degrees a tile).
/// </summary>
public static class FeltRecoil
{
    /// <summary>D179: degrees of first-person view kick per tile of tg's recoil offset (the client's <c>CameraLayer.RecoilDegreesPerTile</c>).</summary>
    public const float DegreesPerTile = 3f;

    /// <summary>The kick of a gun tg's type tree does not sort (a syringe gun, a crossbow, a magic wand ...), degrees.</summary>
    public const float OtherDegrees = 0.5f;

    /// <summary>A gun of WEAPON_MEDIUM or heavier fired with the other hand full kicks this much harder.</summary>
    public const float OneHandFactor = 1.5f;

    /// <summary>The kick of a kind of gun, degrees, and the damage of its usual round (0: the round does not scale it).</summary>
    public static (float Degrees, float Damage) KindOf(obj_item_gun gun) => gun switch
    {
        obj_item_gun_energy => (0.4f, 0f),
        obj_item_gun_ballistic_shotgun => (6f, 0f), // the owner 2026-11-21: 3 felt weak; a pellet's damage says little of a shell's kick
        obj_item_gun_ballistic_revolver => (2f, 60f),
        obj_item_gun_ballistic_automatic_pistol => (1.2f, 30f),
        obj_item_gun_ballistic_rifle => (3f, 60f),
        obj_item_gun_ballistic_automatic => (0.8f, 30f),
        obj_item_gun_ballistic => (1f, 30f),
        _ => (OtherDegrees, 0f),
    };

    /// <summary>The first-person kick of <paramref name="gun"/>'s shot fired by <paramref name="user"/>, degrees.</summary>
    public static float DegreesOf(obj_item_gun gun, mob_living user)
    {
        if (gun == null)
            return 0f;
        (float degrees, float usual) = KindOf(gun);
        if (usual > 0f && RoundDamage(gun.chambered) is float damage and > 0f)
            degrees *= Math.Clamp(MathF.Sqrt(damage / usual), 0.6f, 1.6f);
        if (gun.weapon_weight >= WEAPON_MEDIUM && Dm.truthy(user?.get_inactive_held_item()))
            degrees *= OneHandFactor;
        return degrees;
    }

    /// <summary><see cref="DegreesOf"/> in tg's tiles of recoil offset.</summary>
    public static float TilesOf(obj_item_gun gun, mob_living user) => DegreesOf(gun, user) / DegreesPerTile;

    // a round's damage: its projectile's (stamina counts half), times its pellets
    private static float? RoundDamage(obj_item_ammo_casing casing)
    {
        if (casing?.projectile_type == null)
            return null;
        float damage = Dm.initial(casing.projectile_type, (obj_projectile p) => p.damage) ?? 0f;
        float stamina = Dm.initial(casing.projectile_type, (obj_projectile p) => p.stamina);
        return (damage + 0.5f * stamina) * Math.Max(1f, casing.pellets ?? 1f);
    }
}
