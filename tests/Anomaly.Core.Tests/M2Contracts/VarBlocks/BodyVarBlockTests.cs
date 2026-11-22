using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.M2Contracts.VarBlocks;

/// <summary>
/// The var blocks of the body area of the M2 contracts step: /obj/item (game/objects/items.dm), /obj/item/bodypart
/// (surgery/bodyparts/_bodyparts.dm), /obj/item/bodypart/head (head.dm) and the default chest, arms and legs
/// (default_parts.dm). Each type has its tg path, and initial() reads tg's defaults and the subtypes' overrides from
/// the prototype (a constructor-only instance, docs/ai/specs/object-model.md section 5).
/// </summary>
public class BodyVarBlockTests
{
    [Theory]
    [InlineData(typeof(obj_item), "/obj/item")]
    [InlineData(typeof(obj_item_bodypart), "/obj/item/bodypart")]
    [InlineData(typeof(obj_item_bodypart_head), "/obj/item/bodypart/head")]
    [InlineData(typeof(obj_item_bodypart_chest), "/obj/item/bodypart/chest")]
    [InlineData(typeof(obj_item_bodypart_arm), "/obj/item/bodypart/arm")]
    [InlineData(typeof(obj_item_bodypart_arm_left), "/obj/item/bodypart/arm/left")]
    [InlineData(typeof(obj_item_bodypart_arm_right), "/obj/item/bodypart/arm/right")]
    [InlineData(typeof(obj_item_bodypart_leg), "/obj/item/bodypart/leg")]
    [InlineData(typeof(obj_item_bodypart_leg_left), "/obj/item/bodypart/leg/left")]
    [InlineData(typeof(obj_item_bodypart_leg_right), "/obj/item/bodypart/leg/right")]
    public void EachTypeHasItsTgPath(Type type, string path)
    {
        Assert.Equal(path, DmTypes.PathOf(type));
    }

    [Fact]
    public void ItemDefaults()
    {
        Type t = typeof(obj_item);
        Assert.Equal("item", Dm.initial(t, (obj_item p) => p.name));                                      // items.dm:3
        Assert.Equal(typeof(obj_item), Dm.initial(t, (obj_item p) => p.abstract_type));                   // items.dm:5
        Assert.Equal(typeof(particles_smoke_burning_small), Dm.initial(t, (obj_item p) => p.burning_particles)); // :7
        Assert.True(Dm.eq(Dm.initial(t, (obj_item p) => p.lefthand_file), Dm.resource("icons/mob/inhands/items_lefthand.dmi"))); // :23
        Assert.Equal(200f, Dm.initial(t, (obj_item p) => p.max_integrity));                               // items.dm:71
        Assert.Equal((float)WEIGHT_CLASS_NORMAL, Dm.initial(t, (obj_item p) => p.w_class));               // items.dm:101
        Assert.True(Dm.eq(Dm.initial(t, (obj_item p) => p.slot_flags), NONE));                            // :103, float?
        Assert.Equal((float)PASSTABLE, Dm.initial(t, (obj_item p) => p.pass_flags));                      // items.dm:104
        Assert.Equal(4f, Dm.initial(t, (obj_item p) => p.pressure_resistance));                           // items.dm:105
        Assert.Equal(20f, Dm.initial(t, (obj_item p) => p.equip_delay_other));                            // 2 SECONDS, :162
        Assert.Equal(40f, Dm.initial(t, (obj_item p) => p.strip_delay));                                  // 4 SECONDS, :164
        Assert.Equal(typeof(obj_effect_temp_visual_block), Dm.initial(t, (obj_item p) => p.block_effect)); // :204
        Assert.True(Dm.eq(Dm.initial(t, (obj_item p) => p.mouse_drag_pointer), MOUSE_ACTIVE_POINTER));    // :182
        Assert.Null(Dm.initial(t, (obj_item p) => p.dog_fashion));                                        // :213
        Assert.Equal((float)CLICK_CD_BREAKOUT, Dm.initial(t, (obj_item p) => p.resist_cooldown));         // :242
    }

    [Fact]
    public void BodypartDefaults()
    {
        Type t = typeof(obj_item_bodypart);
        Assert.Equal("limb", Dm.initial(t, (obj_item_bodypart p) => p.name));                             // _bodyparts.dm:2
        Assert.Equal(3f, Dm.initial(t, (obj_item_bodypart p) => p.force));                                // :5
        Assert.Equal((float)WEIGHT_CLASS_SMALL, Dm.initial(t, (obj_item_bodypart p) => p.w_class));       // :7, over items.dm
        Assert.Equal("", Dm.initial(t, (obj_item_bodypart p) => p.icon_state));                           // :9
        Assert.True(Dm.eq(Dm.initial(t, (obj_item_bodypart p) => p.icon_greyscale), Dm.resource("icons/mob/human/bodyparts_greyscale.dmi"))); // :12
        Assert.Equal(BELOW_MOB_LAYER, Dm.initial(t, (obj_item_bodypart p) => p.layer));                   // :26
        Assert.Equal(SPECIES_HUMAN, Dm.initial(t, (obj_item_bodypart p) => p.limb_id));                   // :58
        Assert.Equal((float)LIMB_NO_DISABLE, Dm.initial(t, (obj_item_bodypart p) => p.disabling_threshold_percentage)); // :89
        Assert.Equal(15f, Dm.initial(t, (obj_item_bodypart p) => p.disabled_wound_penalty));              // :154
        Assert.Equal(ATTACK_EFFECT_PUNCH, Dm.initial(t, (obj_item_bodypart p) => p.unarmed_attack_effect)); // :185
        Assert.Equal(0.8f, Dm.initial(t, (obj_item_bodypart p) => p.hp_percent_to_dismemberable));        // :217
        Assert.Null(Dm.initial(t, (obj_item_bodypart p) => p.damage_examines));                           // list(): dm_init_vars
        Assert.Null(Dm.initial(t, (obj_item_bodypart p) => p.stump_typepath));                            // :244
    }

    [Fact]
    public void HeadAndChestOverrides()
    {
        Type head = typeof(obj_item_bodypart_head);
        Assert.Equal(BODY_ZONE_HEAD, Dm.initial(head, (obj_item_bodypart p) => p.name));                  // head.dm:2
        Assert.Equal((float)LIMB_MAX_HP_CORE, Dm.initial(head, (obj_item_bodypart p) => p.max_damage));   // head.dm:6
        Assert.True(Dm.eq(Dm.initial(head, (obj_item_bodypart p) => p.body_part), HEAD));                 // head.dm:8
        Assert.Equal(-8f, Dm.initial(head, (obj_item_bodypart p) => p.px_y));                             // head.dm:14
        Assert.Equal(typeof(obj_item_bodypart_head_stump), Dm.initial(head, (obj_item_bodypart p) => p.stump_typepath)); // :30
        Assert.Equal("Bald", Dm.initial(head, (obj_item_bodypart_head p) => p.hairstyle));                // head.dm:42
        Assert.Equal(32f, Dm.initial(head, (obj_item_bodypart_head p) => p.teeth_count));                 // head.dm:73

        Type chest = typeof(obj_item_bodypart_chest);
        Assert.Equal(BODY_ZONE_CHEST, Dm.initial(chest, (obj_item_bodypart p) => p.body_zone));           // default_parts.dm:7
        Assert.Equal(10f, Dm.initial(chest, (obj_item_bodypart p) => p.wound_resistance));                // :13
        Assert.Equal(typeof(obj_item_bodypart_chest_skeleton_nonfunctional), Dm.initial(chest, (obj_item_bodypart p) => p.butcher_replacement)); // :16
        Assert.Equal((float)ALL, Dm.initial(chest, (obj_item_bodypart_chest p) => p.acceptable_bodytype)); // :20
    }

    [Fact]
    public void ArmAndLegOverrides()
    {
        Type arm = typeof(obj_item_bodypart_arm);
        Assert.Equal(typeof(obj_item_bodypart_arm), Dm.initial(arm, (obj_item_bodypart p) => p.abstract_type)); // :117
        Assert.Equal(1.5f, Dm.initial(arm, (obj_item_bodypart p) => p.unarmed_pummeling_bonus));          // :133
        Assert.Equal((float)BIO_STANDARD_JOINTED, Dm.initial(arm, (obj_item_bodypart p) => p.biological_state)); // :148
        Assert.Equal("hand", Dm.initial(arm, (obj_item_bodypart_arm p) => p.appendage_noun));             // :146

        Type left = typeof(obj_item_bodypart_arm_left);
        Assert.Equal(1f, Dm.initial(left, (obj_item_bodypart p) => p.held_index));                        // :282
        Assert.True(Dm.eq(Dm.initial(left, (obj_item_bodypart p) => p.aux_zone), BODY_ZONE_PRECISE_L_HAND)); // :281
        Assert.Equal(5f, Dm.initial(left, (obj_item_bodypart p) => p.unarmed_damage_low));                // inherited, :131

        Type right = typeof(obj_item_bodypart_arm_right);
        Assert.True(Dm.eq(Dm.initial(right, (obj_item_bodypart p) => p.body_zone), BODY_ZONE_R_ARM));     // :324 over :134
        Assert.Equal(2f, Dm.initial(right, (obj_item_bodypart p) => p.held_index));                       // :330

        Type leg = typeof(obj_item_bodypart_leg);
        Assert.Equal(ATTACK_EFFECT_KICK, Dm.initial(leg, (obj_item_bodypart p) => p.unarmed_attack_effect)); // :379
        Assert.Equal(15f, Dm.initial(leg, (obj_item_bodypart p) => p.unarmed_damage_high));               // :384
        Assert.Equal(FOOTSTEP_MOB_BAREFOOT, Dm.initial(leg, (obj_item_bodypart_leg p) => p.footstep_type)); // :394

        Type lleg = typeof(obj_item_bodypart_leg_left);
        Assert.Equal(12f, Dm.initial(lleg, (obj_item_bodypart p) => p.px_y));                             // :471
        Assert.Equal(typeof(obj_item_bodypart_leg_left_stump), Dm.initial(lleg, (obj_item_bodypart p) => p.stump_typepath)); // :475

        Type rleg = typeof(obj_item_bodypart_leg_right);
        Assert.Equal(
            "You put your right leg in, your right leg out. In, out, in, out, shake it all about. And apparently then it detaches.\nThe hokey pokey has certainly changed a lot since space colonisation.",
            Dm.initial(rleg, (obj_item_bodypart p) => p.desc));                                           // :510-512
        Assert.Equal(RIGHT_LEG_TRAIT, Dm.initial(rleg, (obj_item_bodypart p) => p.bodypart_trait_source)); // :520
    }

    [Fact]
    public void ButcherDropCacheIsAPerWorldStatic()
    {
        var a = new World();
        var b = new World();
        using (a.Enter())
            obj_item_bodypart.butcher_drop_cache["k"] = 1;
        using (b.Enter())
            Assert.Equal(0, obj_item_bodypart.butcher_drop_cache.Count);
        using (a.Enter())
            Assert.True(Dm.eq(obj_item_bodypart.butcher_drop_cache["k"], 1));
    }
}
