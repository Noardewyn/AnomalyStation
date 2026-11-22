using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Appearance.T2;

/// <summary>
/// M2 T2's rig for the appearance goldens: a booted toy room (T2Room, so a body can also walk) with a real
/// <c>/mob/living/carbon/human/consistent</c> (P2's bodyparts and organs, not a fixture), damaged through tg's own
/// <c>apply_damage</c>, read through <c>IDmAppearance</c> (what the replicator reads). Every expected value in the tests is
/// written from the DM named at the test.
/// </summary>
internal sealed class AppearanceRig
{
    public const string Greyscale = "icons/mob/human/bodyparts_greyscale.dmi";
    public const string DamMob = "icons/mob/effects/dam_mob.dmi";

    /// <summary>The chest and the head hold 200 points, an arm or a leg 50 (default_parts.dm: max_damage).</summary>
    public static readonly string[] Zones = { BODY_ZONE_CHEST, BODY_ZONE_HEAD, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM };

    public T2Room Room { get; }

    public mob_living_carbon_human Human { get; }

    public AppearanceRig(ulong seed = 1801, bool hud = false)
    {
        Room = new T2Room(seed);
        Human = Room.In(() => Dm.@new<mob_living_carbon_human_consistent>(locate(5, 5, 1)));
        Room.Step(2);
        if (hud)
        {
            Room.In(() =>
            {
                Human.client = Dm.@new<client_test_m1_client>();
                Human.client.view = "15x15"; // tg's world.view, what a connected client has
                Assert.True(Dm.eq(TRUE, Waiting.NoSleep(Human.create_mob_hud())));
            });
        }
    }

    public float MaxDamage(string zone) => Room.In(() => ((obj_item_bodypart)Human.get_bodypart(zone)).max_damage);

    public obj_item_bodypart Part(string zone) => Room.In(() => (obj_item_bodypart)Human.get_bodypart(zone));

    /// <summary>tg's own damage path: apply_damage(amount, type, zone) in a command phase, then a tick.</summary>
    public void Damage(string type, float amount, string zone)
    {
        Room.Command(() => Waiting.NoSleep(Human.apply_damage(amount, type, zone)));
        Room.Step();
    }

    public List<IDmAppearance> Overlays() => Room.In(() => Dm.each(Human.overlays).Cast<object>().Select(o => (IDmAppearance)o).ToList());

    public static string Icon(IDmAppearance a) => a.icon switch
    {
        DmResource r => r.Path,
        // M6 slice 7b: an /icon made with Blend (tg's masked legs, handle_masking) as its file and each blended icon's state
        icon made => (made.dm_file is DmResource f ? f.Path : made.dm_file as string)
            + string.Concat(made.dm_ops.Select(op => "*" + (op.With as icon)?.dm_state)),
        _ => a.icon as string,
    };

    /// <summary>icon file, state, layer, colour, as the goldens write an entry: <c>file:state@layer#colour</c>.</summary>
    public static string Entry(IDmAppearance a) => $"{Icon(a)}:{a.icon_state}@{a.layer}#{a.color}";

    public static string[] Nested(IDmAppearance a) => Dm.each(a.overlays).Cast<object>().Select(o => Entry((IDmAppearance)o)).ToArray();

    /// <summary>The overlays that are not limb images: the damage appearance (no icon, layer -DAMAGE_LAYER) and its nested entries.</summary>
    public (IDmAppearance Holder, string[] Nested)? DamageOverlay()
    {
        List<IDmAppearance> holders = Room.In(() => Overlays().Where(a => Icon(a) == null && a.layer == -DAMAGE_LAYER).ToList());
        Assert.True(holders.Count <= 1, "more than one damage appearance");
        return holders.Count == 0 ? null : (holders[0], Room.In(() => Nested(holders[0])));
    }

    /// <summary>The eight images of a healthy human, in tg's order (chest, head, legs, arms with their hands), at the skin tone <paramref name="colour"/>.</summary>
    public static string[] HealthyStack(string colour) => new[]
    {
        // carbon_update_icons.dm:372 update_body_parts walks get_bodyparts_by_zones: the species' bodypart_overrides sorted by
        // cmp_bodypart_by_body_part_asc (_species.dm:282): chest, head, legs, arms. _bodyparts.dm:1344 get_limb_icon:
        // "[limb_id]_[body_zone][is_dimorphic ? "_[limb_gender]" : ""]" from the greyscale file at -BODYPARTS_LAYER, the draw
        // colour on it; an arm's aux zone (the hand) is "[limb_id]_[aux_zone]" at -aux_layer (BODYPARTS_HIGH_LAYER)
        $"{Greyscale}:human_chest_m@{-BODYPARTS_LAYER}#{colour}",
        $"{Greyscale}:human_head_m@{-BODYPARTS_LAYER}#{colour}",
        // carbon_update_icons.dm:512 handle_masking (M6 slice 7b): a leg is its image cut by leg_masks.dmi at -BODYPARTS_LAYER and the
        // lower one at -BODYPARTS_LOW_LAYER, in the leg's place
        $"{Greyscale}*left_leg:human_l_leg@{-BODYPARTS_LAYER}#{colour}",
        $"{Greyscale}*left_leg_lower:human_l_leg@{-BODYPARTS_LOW_LAYER}#{colour}",
        $"{Greyscale}*right_leg:human_r_leg@{-BODYPARTS_LAYER}#{colour}",
        $"{Greyscale}*right_leg_lower:human_r_leg@{-BODYPARTS_LOW_LAYER}#{colour}",
        $"{Greyscale}:human_l_arm@{-BODYPARTS_LAYER}#{colour}",
        $"{Greyscale}:human_l_hand@{-BODYPARTS_HIGH_LAYER}#{colour}",
        $"{Greyscale}:human_r_arm@{-BODYPARTS_LAYER}#{colour}",
        $"{Greyscale}:human_r_hand@{-BODYPARTS_HIGH_LAYER}#{colour}",
        // M10x slice D2: human_update_icons.dm:704 update_eyes, _eyes.dm:306 generate_body_overlay: the eyes at -EYES_LAYER in the
        // human's eye colour (human_defines.dm: #000000 for a consistent human)
        $"icons/mob/human/human_eyes.dmi:eyes_l@{-EYES_LAYER}##000000",
        $"icons/mob/human/human_eyes.dmi:eyes_r@{-EYES_LAYER}##000000",
        // _eyes.dm:491-495 the open eyes' two eyelid overlays: no icon, the eyelid effects' render_source
        $":@{-EYES_LAYER}#",
        $":@{-EYES_LAYER}#",
    };
}
