using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Appearance.P3;

/// <summary>
/// A human whose body is wired by the test (M2 P3's fixture, rules 15 "Mock and fixture types"): in P3's worktree P2's
/// bodypart procs (<c>get_bodyparts</c>, <c>get_bodyparts_by_zones</c>, <c>get_bodypart</c> of <c>surgery/bodyparts/helpers.dm</c>)
/// and P1's blood procs are skeletons, so this subtype answers them from the parts the test made, as tg's do for a human
/// whose <c>real_bodypart_cache</c> holds them (helpers.dm:14-16, :39-44, :54-58; the zones in the human species' order,
/// <c>_species.dm:64-71</c>, which <c>get_all_limbs</c> returns). No blood type (<c>get_bloodtype</c> null): the damage
/// overlay falls back to tg's <c>BLOOD_COLOR_RED</c> (<c>_bodyparts.dm:1162</c>) and no bleeding overlay is drawn.
/// </summary>
[DmPath("/mob/living/carbon/human/test_p3_body")]
public class mob_living_carbon_human_test_p3_body : mob_living_carbon_human
{
    public DmList test_parts = Dm.list();

    public override object get_bodypart(string zone = null, float? include_stumps = null)
    {
        // M2 P2 (integration): the human's own Initialize now makes its real bodyparts and organs (P2); until the fixture
        // adds its test parts after Initialize, the real lookups answer.
        if (test_parts.len == 0)
            return base.get_bodypart(zone, include_stumps);
        zone ??= BODY_ZONE_CHEST;
        foreach (obj_item_bodypart part in Dm.each<obj_item_bodypart>(test_parts))
        {
            if (Dm.eq(part.body_zone, zone))
                return part;
        }
        return null;
    }

    public override object get_bodyparts(float? include_stumps = null) => test_parts.len == 0 ? base.get_bodyparts(include_stumps) : test_parts.Copy();

    public override object get_bodyparts_by_zones()
    {
        if (test_parts.len == 0)
            return base.get_bodyparts_by_zones();
        DmList parts = Dm.list();
        foreach (string zone in new[] { BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_HEAD, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG, BODY_ZONE_CHEST })
            parts[zone] = get_bodypart(zone);
        return parts;
    }

    public override object get_bloodtype() => null;

    // tg's /mob/living/carbon/get_all_limbs (helpers.dm:166): the human species' bodypart_overrides keys (_species.dm:64-71)
    public override object get_all_limbs()
        => Dm.list(BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_HEAD, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG, BODY_ZONE_CHEST);

    // The carbon's brute and burn totals are P2's (carbon/damage_procs.dm); the test sets them.
    public float test_brute;
    public float test_fire;
    public override object get_brute_loss() => test_brute;
    public override object get_fire_loss() => test_fire;

    // tg's /mob/sync_lighting_plane_cutoff (mob.dm:1235-1239), which P1's mob.cs leaves NotTranslated: a loop over the HUD's
    // lighting plane masters, of which the port has none (hud.cs get_true_plane_masters).
    public override void sync_lighting_plane_cutoff()
    {
        if (!Dm.truthy(hud_used))
            return;
        foreach (object light in Dm.each(hud_used.get_true_plane_masters(RENDER_PLANE_LIGHTING)))
            throw new InvalidOperationException("the port makes no plane masters");
    }
}

// The bodypart damage total of tg (_bodyparts.dm:930, `return brute_dam + burn_dam`), which is P2's skeleton in P3's branch,
// on the six default limbs (fixture types, rules 15).
[DmPath("/obj/item/bodypart/chest/test_p3")]
public class obj_item_bodypart_chest_test_p3 : obj_item_bodypart_chest { public override object get_damage() => brute_dam + burn_dam; }
[DmPath("/obj/item/bodypart/head/test_p3")]
public class obj_item_bodypart_head_test_p3 : obj_item_bodypart_head { public override object get_damage() => brute_dam + burn_dam; }
[DmPath("/obj/item/bodypart/arm/left/test_p3")]
public class obj_item_bodypart_arm_left_test_p3 : obj_item_bodypart_arm_left { public override object get_damage() => brute_dam + burn_dam; }
[DmPath("/obj/item/bodypart/arm/right/test_p3")]
public class obj_item_bodypart_arm_right_test_p3 : obj_item_bodypart_arm_right { public override object get_damage() => brute_dam + burn_dam; }
[DmPath("/obj/item/bodypart/leg/left/test_p3")]
public class obj_item_bodypart_leg_left_test_p3 : obj_item_bodypart_leg_left { public override object get_damage() => brute_dam + burn_dam; }
[DmPath("/obj/item/bodypart/leg/right/test_p3")]
public class obj_item_bodypart_leg_right_test_p3 : obj_item_bodypart_leg_right { public override object get_damage() => brute_dam + burn_dam; }

internal static class AppearanceFixtures
{
    public const string Skin = "#ffcc99";

    /// <summary>A fixture human at (4, 4) with the six default limbs (male, skin colour <see cref="Skin"/>).</summary>
    public static mob_living_carbon_human_test_p3_body Human(int x = 4, int y = 4, bool damageParts = false)
    {
        var human = Dm.@new<mob_living_carbon_human_test_p3_body>(TgToyRoom.T(x, y));
        obj_item_bodypart[] parts = damageParts
            ? new obj_item_bodypart[]
            {
                Dm.@new<obj_item_bodypart_chest_test_p3>(), Dm.@new<obj_item_bodypart_head_test_p3>(), Dm.@new<obj_item_bodypart_arm_left_test_p3>(),
                Dm.@new<obj_item_bodypart_arm_right_test_p3>(), Dm.@new<obj_item_bodypart_leg_right_test_p3>(), Dm.@new<obj_item_bodypart_leg_left_test_p3>(),
            }
            : new obj_item_bodypart[]
            {
                Dm.@new<obj_item_bodypart_chest>(), Dm.@new<obj_item_bodypart_head>(), Dm.@new<obj_item_bodypart_arm_left>(),
                Dm.@new<obj_item_bodypart_arm_right>(), Dm.@new<obj_item_bodypart_leg_right>(), Dm.@new<obj_item_bodypart_leg_left>(),
            };
        // carbon/Initialize removes STOP_OVERLAY_UPDATE_BODY_PARTS after its update_body (carbon.dm:4-5, P2's): as after init
        human.living_flags = Dm.bitand(human.living_flags, Dm.bitnot(STOP_OVERLAY_UPDATE_BODY_PARTS));
        foreach (obj_item_bodypart part in parts)
        {
            part.owner = human;
            part.species_color = Skin;
            Dm.append(ref human.test_parts, part);
        }
        return human;
    }

    public static obj_item_bodypart Part(mob_living_carbon_human_test_p3_body human, string zone)
        => (obj_item_bodypart)human.get_bodypart(zone);

    /// <summary>The top-level overlay snapshots of an atom, read only through IDmAppearance.</summary>
    public static List<IDmAppearance> Overlays(atom thing)
        => Dm.each(thing.overlays).Cast<object>().Select(o => (IDmAppearance)o).ToList();

    public static string IconPath(IDmAppearance a) => a.icon switch
    {
        DmResource r => r.Path,
        // M6 slice 7b: an /icon made with Blend (tg's masked legs) as its file and each blended icon's state ("file*mask_state")
        icon made => (made.dm_file is DmResource f ? f.Path : made.dm_file as string)
            + string.Concat(made.dm_ops.Select(op => "*" + (op.With as icon)?.dm_state)),
        _ => a.icon as string,
    };

    public static string Describe(IDmAppearance a)
        => $"{IconPath(a)}:{a.icon_state}@{a.layer}#{a.color}";
}
