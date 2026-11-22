// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/default_parts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the M2 subset translated (M2 P2, scope.md 2e "bodyparts": the chest, arms and legs' ownership, Destroy,
// set_disabled, the owner-paralysis handlers, the leg's speed modifier and attachment check; M6 slice 4: the chest's
// forced_removal, can_dismember and drop_organs). Butchering, the arm speed
// penalty, the butt sprite and the animated limbs come with M4a, M8 and M9 (their roots stay Dm.NotTranslated).

// tg: code/modules/surgery/bodyparts/default_parts.dm:2
// The type block is translated in full by the M2 contracts step; the procs are still skeletons.
[DmPath("/obj/item/bodypart/chest")]
public partial class obj_item_bodypart_chest : obj_item_bodypart
{
    // The constant var overrides of the type block (rules 10.2), in DM declaration order.
    public obj_item_bodypart_chest()
    {
        // tg: code/modules/surgery/bodyparts/default_parts.dm:3
        name = BODY_ZONE_CHEST;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:4
        desc = "It's impolite to stare at a person's chest.";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:5
        icon_state = "default_human_chest";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:6
        max_damage = LIMB_MAX_HP_CORE;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:7
        body_zone = BODY_ZONE_CHEST;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:8
        body_part = CHEST;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:9
        plaintext_zone = "chest";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:10
        is_dimorphic = TRUE;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:11
        px_x = 0;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:12
        px_y = 0;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:13
        wound_resistance = 10;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:14
        bodypart_trait_source = CHEST_TRAIT;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:15
        base_meat_amount = 2;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:16
        butcher_replacement = typeof(obj_item_bodypart_chest_skeleton_nonfunctional);
    }

    // The object-creating defaults and overrides of the type block (rules 10.2), in DM declaration order.
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        // tg: code/modules/surgery/bodyparts/default_parts.dm:39
        wing_types = Dm.list(typeof(obj_item_organ_wings_angel));
    }

    ///The bodyshape(s) allowed to attach to this chest.
    // tg: code/modules/surgery/bodyparts/default_parts.dm:18
    public float acceptable_bodyshape = BODYSHAPE_HUMANOID;
    ///The bodytype(s) allowed to attach to this chest.
    // tg: code/modules/surgery/bodyparts/default_parts.dm:20
    public float acceptable_bodytype = ALL;

    // tg: code/modules/surgery/bodyparts/default_parts.dm:22
    public obj_item cavity_item;

    /// Offset to apply to equipment worn as a uniform
    // tg: code/modules/surgery/bodyparts/default_parts.dm:25
    public datum_worn_feature_offset worn_uniform_offset;
    /// Offset to apply to equipment worn on the id slot
    // tg: code/modules/surgery/bodyparts/default_parts.dm:27
    public datum_worn_feature_offset worn_id_offset;
    /// Offset to apply to equipment worn in the suit slot
    // tg: code/modules/surgery/bodyparts/default_parts.dm:29
    public datum_worn_feature_offset worn_suit_storage_offset;
    /// Offset to apply to equipment worn on the hips
    // tg: code/modules/surgery/bodyparts/default_parts.dm:31
    public datum_worn_feature_offset worn_belt_offset;
    /// Offset to apply to overlays placed on the back
    // tg: code/modules/surgery/bodyparts/default_parts.dm:33
    public datum_worn_feature_offset worn_back_offset;
    /// Offset to apply to equipment worn as a suit
    // tg: code/modules/surgery/bodyparts/default_parts.dm:35
    public datum_worn_feature_offset worn_suit_offset;
    /// Offset to apply to equipment worn on the neck
    // tg: code/modules/surgery/bodyparts/default_parts.dm:37
    public datum_worn_feature_offset worn_neck_offset;
    /// Which functional (i.e. flightpotion) wing types (if any) does this bodypart support? If count is >1 a radial menu is used to choose between all icons in list
    // tg: code/modules/surgery/bodyparts/default_parts.dm:39 (list(...): dm_init_vars above)
    public DmList wing_types;
}

// tg: code/modules/surgery/bodyparts/default_parts.dm:78
public partial class obj_item_bodypart_chest : obj_item_bodypart
{
    // tg: code/modules/surgery/bodyparts/default_parts.dm:41
    public override async DmTask apply_ownership(mob_living_carbon new_owner = null)
    {
        await base.apply_ownership(new_owner);
        if (ishuman(new_owner))
        {
            mob_living_carbon_human humie = new_owner as mob_living_carbon_human;
            await humie.update_mob_height();
        }
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:58
    public override async DmTask forced_removal(object special = null, object dismembered = null, object move_to_floor = null)
    {
        mob_living_carbon old_owner = owner;
        await base.forced_removal(special: TRUE); //special because we're self destructing

        //If someones chest is teleported away, they die pretty hard
        if (old_owner == null)
            return;
        message_admins(Dm.text("[] was gibbed after their chest teleported to [].", ADMIN_LOOKUPFLW(old_owner), ADMIN_VERBOSEJMP(loc as atom)));
        await old_owner.gib(DROP_ALL_REMAINS);
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:67
    public override object can_dismember__proc(obj_item item = null)
    {
        if ((!HAS_TRAIT(owner, TRAIT_CURSED) && Dm.num(owner.stat) < HARD_CRIT) || !Dm.truthy(contents.len))
            return FALSE;
        return base.can_dismember__proc(item);
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:87
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref cavity_item);
        QDEL_NULL(ref worn_uniform_offset);
        QDEL_NULL(ref worn_id_offset);
        QDEL_NULL(ref worn_suit_storage_offset);
        QDEL_NULL(ref worn_belt_offset);
        QDEL_NULL(ref worn_back_offset);
        QDEL_NULL(ref worn_suit_offset);
        QDEL_NULL(ref worn_neck_offset);
        return base.Destroy(force);
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:98
    public override async DmTask drop_organs(mob user = null, object violent_removal = null)
    {
        if (cavity_item != null)
        {
            await cavity_item.forceMove(drop_location() as atom);
            cavity_item = null;
        }
        await base.drop_organs(user, violent_removal);
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:78
    public virtual object update_mob_heights(mob_living_carbon holder = null)
    {
        if (HAS_TRAIT(holder, TRAIT_DWARF))
            return HUMAN_HEIGHT_DWARF;

        if (HAS_TRAIT(holder, TRAIT_TOO_TALL))
            return HUMAN_HEIGHT_TALLEST;

        return null;
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:105
    /// Sprite to show for photocopying mob butts (gaps S3: the photocopier)
    public virtual object get_butt_sprite()
    {
        if (!ishuman(owner))
            return null;
        mob_living_carbon_human human_owner = (mob_living_carbon_human)owner;
        obj_item_organ_tail tail = human_owner.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) as obj_item_organ_tail;
        if (tail != null)
            return tail.get_butt_sprite();

        return Dm.icon(Dm.resource("icons/mob/butts.dmi"), human_owner.physique == FEMALE ? BUTT_SPRITE_HUMAN_FEMALE : BUTT_SPRITE_HUMAN_MALE);
    }
}

/// Parent Type for arms, should not appear in game.
// tg: code/modules/surgery/bodyparts/default_parts.dm:116
// The type block is translated in full by the M2 contracts step; the procs are still skeletons.
[DmPath("/obj/item/bodypart/arm")]
public partial class obj_item_bodypart_arm : obj_item_bodypart
{
    // The constant var overrides of the type block (rules 10.2), in DM declaration order.
    public obj_item_bodypart_arm()
    {
        // tg: code/modules/surgery/bodyparts/default_parts.dm:117
        abstract_type = typeof(obj_item_bodypart_arm);
        // tg: code/modules/surgery/bodyparts/default_parts.dm:118
        name = "arm";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:119
        desc = "Hey buddy give me a HAND and report this to the github because you shouldn't be seeing this.";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:120
        abstract_type = typeof(obj_item_bodypart_arm); // (tg sets it twice, :117 and :120)
        // (attack_verb_continuous, :121, attack_verb_simple, :122, unarmed_attack_verbs, :127, and
        // unarmed_attack_verbs_continuous, :128, create lists: dm_init_vars below)
        // tg: code/modules/surgery/bodyparts/default_parts.dm:123
        max_damage = LIMB_MAX_HP_DEFAULT;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:124
        aux_layer = BODYPARTS_HIGH_LAYER;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:125
        body_damage_coeff = LIMB_BODY_DAMAGE_COEFFICIENT_DEFAULT;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:126
        can_be_disabled = TRUE;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:129
        grappled_attack_verb = "pummel";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:130
        grappled_attack_verb_continuous = "pummels";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:131
        unarmed_damage_low = 5;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:132
        unarmed_damage_high = 10;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:133
        unarmed_pummeling_bonus = 1.5f;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:134
        body_zone = BODY_ZONE_L_ARM;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:148
        biological_state = BIO_STANDARD_JOINTED;
    }

    // The object-creating defaults and overrides of the type block (rules 10.2), in DM declaration order.
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        // tg: code/modules/surgery/bodyparts/default_parts.dm:121
        attack_verb_continuous = Dm.list("slaps", "punches");
        // tg: code/modules/surgery/bodyparts/default_parts.dm:122
        attack_verb_simple = Dm.list("slap", "punch");
        // tg: code/modules/surgery/bodyparts/default_parts.dm:127
        unarmed_attack_verbs = Dm.list("punch"); /// The classic punch, wonderfully classic and completely random
        // tg: code/modules/surgery/bodyparts/default_parts.dm:128
        unarmed_attack_verbs_continuous = Dm.list("punches");
    }

    /// Actionspeed modifier applied to doafters being done while this arm is active
    // tg: code/modules/surgery/bodyparts/default_parts.dm:137
    public float interaction_modifier = 0;
    /// Modifier to all click cds while this arm is active
    // tg: code/modules/surgery/bodyparts/default_parts.dm:139
    public float click_cd_modifier = 1;

    /// Datum describing how to offset things worn on the hands of this arm, note that an x offset won't do anything here
    // tg: code/modules/surgery/bodyparts/default_parts.dm:142
    public datum_worn_feature_offset worn_glove_offset;
    /// Datum describing how to offset things held in the hands of this arm, the x offset IS functional here
    // tg: code/modules/surgery/bodyparts/default_parts.dm:144
    public datum_worn_feature_offset held_hand_offset;
    /// The noun to use when referring to this arm's appendage, e.g. "hand" or "paw"
    // tg: code/modules/surgery/bodyparts/default_parts.dm:146
    public string appendage_noun = "hand";

    // (biological_state = BIO_STANDARD_JOINTED, default_parts.dm:148: an override, in the constructor above)
}

// tg: code/modules/surgery/bodyparts/default_parts.dm:155
public partial class obj_item_bodypart_arm : obj_item_bodypart
{
    // tg: code/modules/surgery/bodyparts/default_parts.dm:150
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref worn_glove_offset);
        QDEL_NULL(ref held_hand_offset);
        return base.Destroy(force);
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:155
    public virtual async DmTask set_speed_modifiers(float? interaction = null, float? click = null)
    {
        interaction ??= 0;
        click ??= 1;
        if (interaction_modifier == interaction && click_cd_modifier == click)
            return;

        owner?.remove_status_effect(typeof(datum_status_effect_arm_speed_penalty), held_index);
        interaction_modifier = interaction.Value;
        click_cd_modifier = click.Value;
        if (interaction_modifier != 0 || click_cd_modifier != 1)
        {
            if (owner != null)
                await owner.apply_status_effect(typeof(datum_status_effect_arm_speed_penalty), held_index, interaction, click);
        }
    }
}

// tg: code/modules/surgery/bodyparts/default_parts.dm:166 (interleaved types, rules 1.9)
public partial class obj_item_bodypart_arm : obj_item_bodypart
{
    /// We need to clear out hand hud items and appearance, so do that here
    // tg: code/modules/surgery/bodyparts/default_parts.dm:166
    public override async DmTask clear_ownership(mob_living_carbon old_owner = null)
    {
        await base.clear_ownership(old_owner);
        await old_owner.update_worn_gloves();
        if (!Dm.truthy(held_index))
            return;

        old_owner.on_lost_hand(this);
        if (interaction_modifier != 0 || click_cd_modifier != 1)
            old_owner.remove_status_effect(typeof(datum_status_effect_arm_speed_penalty), held_index);
        old_owner.hud_used?.update_inventory_slot(ITEM_SLOT_HANDS, held_index);
    }

    /// We need to add hand hud items and appearance, so do that here
    // tg: code/modules/surgery/bodyparts/default_parts.dm:178
    public override async DmTask apply_ownership(mob_living_carbon new_owner = null)
    {
        await base.apply_ownership(new_owner);

        await new_owner.update_worn_gloves();

        if (!Dm.truthy(held_index))
            return;

        new_owner.on_added_hand(this, held_index);
        if (interaction_modifier != 0 || click_cd_modifier != 1)
            await new_owner.apply_status_effect(typeof(datum_status_effect_arm_speed_penalty), held_index, interaction_modifier, click_cd_modifier);
        new_owner.hud_used?.update_inventory_slot(ITEM_SLOT_HANDS, held_index);
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:191
    [DmParams("new_disabled")]
    public override async DmTask<object> set_disabled(object new_disabled = null, float? update_limbs = null)
    {
        object dot = null;
        dot = await base.set_disabled(new_disabled, update_limbs);
        if (dot == null || owner == null)
            return dot;

        if (!Dm.truthy(dot))
        {
            if (Dm.truthy(bodypart_disabled))
            {
                owner.set_usable_hands(owner.usable_hands - 1);
                if (!IS_UNCONSCIOUS(owner))
                    to_chat(owner, span_userdanger(I18n.T("You lose control of your []!", plaintext_zone)));
                if (Dm.truthy(held_index))
                {
                    // owner.dropItemToGround(owner.get_item_for_held_index(held_index)): GAP(P1): the inventory roots
                    // (inventory.dm) are not declared; items in hands are M4a and an M2 hand holds nothing.
                    Dm.Deferred("code/modules/surgery/bodyparts/default_parts.dm:202", "set_disabled: owner.dropItemToGround(owner.get_item_for_held_index(held_index)): dropping what the disabled hand holds (items in hands, M4a)");
                }
            }
        }
        else if (!Dm.truthy(bodypart_disabled))
            owner.set_usable_hands(owner.usable_hands + 1);
        owner.hud_used?.update_inventory_slot(ITEM_SLOT_HANDS, held_index);
        return dot;
    }
}

// tg: code/modules/surgery/bodyparts/default_parts.dm:271
// The type block is translated in full by the M2 contracts step; the procs are still skeletons.
[DmPath("/obj/item/bodypart/arm/left")]
public partial class obj_item_bodypart_arm_left : obj_item_bodypart_arm
{
    // The constant var overrides of the type block (rules 10.2), in DM declaration order.
    public obj_item_bodypart_arm_left()
    {
        // tg: code/modules/surgery/bodyparts/default_parts.dm:272
        name = "left arm";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:273
        desc = "Did you know that the word 'sinister' stems originally from the Latin 'sinestra' (left hand), because the left hand was supposed to be possessed by the devil? This arm appears to be possessed by no one though.";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:277
        icon_state = "default_human_l_arm";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:278
        body_zone = BODY_ZONE_L_ARM;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:279
        body_part = ARM_LEFT;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:280
        plaintext_zone = "left arm";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:281
        aux_zone = BODY_ZONE_PRECISE_L_HAND;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:282
        held_index = 1;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:283
        px_x = -6;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:284
        px_y = 0;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:285
        bodypart_trait_source = LEFT_ARM_TRAIT;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:286
        butcher_replacement = typeof(obj_item_bodypart_arm_left_skeleton_nonfunctional);
        // tg: code/modules/surgery/bodyparts/default_parts.dm:287
        stump_typepath = typeof(obj_item_bodypart_arm_left_stump);
    }
}

// tg: code/modules/surgery/bodyparts/default_parts.dm:307
public partial class obj_item_bodypart_arm_left : obj_item_bodypart_arm
{
    // tg: code/modules/surgery/bodyparts/default_parts.dm:289
    public override async DmTask apply_ownership(mob_living_carbon new_owner = null)
    {
        if (HAS_TRAIT(new_owner, TRAIT_PARALYSIS_L_ARM))
        {
            ADD_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_L_ARM);
            RegisterSignal(new_owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_L_ARM), nameof(on_owner_paralysis_loss));
        }
        else
        {
            REMOVE_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_L_ARM);
            RegisterSignal(new_owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_L_ARM), nameof(on_owner_paralysis_gain));
        }
        await base.apply_ownership(new_owner);
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:298
    public override async DmTask clear_ownership(mob_living_carbon old_owner = null)
    {
        await base.clear_ownership(old_owner);
        if (HAS_TRAIT(old_owner, TRAIT_PARALYSIS_L_ARM))
        {
            UnregisterSignal(old_owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_L_ARM));
            REMOVE_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_L_ARM);
        }
        else
            UnregisterSignal(old_owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_L_ARM));
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:307
    [SignalHandler]
    public virtual void on_owner_paralysis_gain(mob_living_carbon source = null)
    {
        ADD_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_L_ARM);
        UnregisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_L_ARM));
        RegisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_L_ARM), nameof(on_owner_paralysis_loss));
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:314
    [SignalHandler]
    public virtual void on_owner_paralysis_loss(mob_living_carbon source = null)
    {
        REMOVE_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_L_ARM);
        UnregisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_L_ARM));
        RegisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_L_ARM), nameof(on_owner_paralysis_gain));
    }
}

// tg: code/modules/surgery/bodyparts/default_parts.dm:320
// The type block is translated in full by the M2 contracts step; the procs are still skeletons.
[DmPath("/obj/item/bodypart/arm/right")]
public partial class obj_item_bodypart_arm_right : obj_item_bodypart_arm
{
    // The constant var overrides of the type block (rules 10.2), in DM declaration order.
    public obj_item_bodypart_arm_right()
    {
        // tg: code/modules/surgery/bodyparts/default_parts.dm:321
        name = "right arm";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:322
        desc = "Over 87% of humans are right handed. That figure is much lower among humans missing their right arm.";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:324
        body_zone = BODY_ZONE_R_ARM;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:325
        body_part = ARM_RIGHT;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:326
        icon_state = "default_human_r_arm";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:327
        plaintext_zone = "right arm";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:328
        aux_zone = BODY_ZONE_PRECISE_R_HAND;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:329
        aux_layer = BODYPARTS_HIGH_LAYER;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:330
        held_index = 2;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:331
        px_x = 6;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:332
        px_y = 0;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:333
        bodypart_trait_source = RIGHT_ARM_TRAIT;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:334
        butcher_replacement = typeof(obj_item_bodypart_arm_right_skeleton_nonfunctional);
        // tg: code/modules/surgery/bodyparts/default_parts.dm:335
        stump_typepath = typeof(obj_item_bodypart_arm_right_stump);
    }
}

// tg: code/modules/surgery/bodyparts/default_parts.dm:355
public partial class obj_item_bodypart_arm_right : obj_item_bodypart_arm
{
    // tg: code/modules/surgery/bodyparts/default_parts.dm:337
    public override async DmTask apply_ownership(mob_living_carbon new_owner = null)
    {
        if (HAS_TRAIT(new_owner, TRAIT_PARALYSIS_R_ARM))
        {
            ADD_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_R_ARM);
            RegisterSignal(new_owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_R_ARM), nameof(on_owner_paralysis_loss));
        }
        else
        {
            REMOVE_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_R_ARM);
            RegisterSignal(new_owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_R_ARM), nameof(on_owner_paralysis_gain));
        }
        await base.apply_ownership(new_owner);
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:346
    public override async DmTask clear_ownership(mob_living_carbon old_owner = null)
    {
        await base.clear_ownership(old_owner);
        if (HAS_TRAIT(old_owner, TRAIT_PARALYSIS_R_ARM))
        {
            UnregisterSignal(old_owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_R_ARM));
            REMOVE_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_R_ARM);
        }
        else
            UnregisterSignal(old_owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_R_ARM));
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:355
    [SignalHandler]
    public virtual void on_owner_paralysis_gain(mob_living_carbon source = null)
    {
        ADD_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_R_ARM);
        UnregisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_R_ARM));
        RegisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_R_ARM), nameof(on_owner_paralysis_loss));
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:362
    [SignalHandler]
    public virtual void on_owner_paralysis_loss(mob_living_carbon source = null)
    {
        REMOVE_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_R_ARM);
        UnregisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_R_ARM));
        RegisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_R_ARM), nameof(on_owner_paralysis_gain));
    }
}

/// Parent Type for legs, should not appear in game.
// tg: code/modules/surgery/bodyparts/default_parts.dm:369
// [DmPath("/obj/item/bodypart/leg")] is on the part in code/modules/mob/living/carbon/carbon_update_icons.dm, the
// earliest .dme file that declares /obj/item/bodypart/leg (rules 2.3).
// The type block is translated in full by the M2 contracts step; the procs are still skeletons.
public partial class obj_item_bodypart_leg : obj_item_bodypart
{
    // The constant var overrides of the type block (rules 10.2), in DM declaration order.
    public obj_item_bodypart_leg()
    {
        // tg: code/modules/surgery/bodyparts/default_parts.dm:370
        abstract_type = typeof(obj_item_bodypart_leg);
        // tg: code/modules/surgery/bodyparts/default_parts.dm:371
        name = "leg";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:372
        desc = "This item shouldn't exist. Talk about breaking a leg. Badum-Tss!";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:373
        abstract_type = typeof(obj_item_bodypart_leg); // (tg sets it twice, :370 and :373)
        // (attack_verb_continuous, :374, attack_verb_simple, :375, unarmed_attack_verbs, :381, and
        // unarmed_attack_verbs_continuous, :382, create lists: dm_init_vars below)
        // tg: code/modules/surgery/bodyparts/default_parts.dm:376
        max_damage = LIMB_MAX_HP_DEFAULT;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:377
        body_damage_coeff = LIMB_BODY_DAMAGE_COEFFICIENT_DEFAULT;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:378
        can_be_disabled = TRUE;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:379
        unarmed_attack_effect = ATTACK_EFFECT_KICK;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:380
        body_zone = BODY_ZONE_L_LEG;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:383
        unarmed_damage_low = 7;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:384
        unarmed_damage_high = 15;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:385
        unarmed_effectiveness = 15;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:386
        biological_state = BIO_STANDARD_JOINTED;
    }

    // The object-creating defaults and overrides of the type block (rules 10.2), in DM declaration order.
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        // tg: code/modules/surgery/bodyparts/default_parts.dm:374
        attack_verb_continuous = Dm.list("kicks", "stomps");
        // tg: code/modules/surgery/bodyparts/default_parts.dm:375
        attack_verb_simple = Dm.list("kick", "stomp");
        // tg: code/modules/surgery/bodyparts/default_parts.dm:381
        unarmed_attack_verbs = Dm.list("kick"); // The lovely kick, typically only accessable by attacking a grouded foe. 1.5 times better than the punch.
        // tg: code/modules/surgery/bodyparts/default_parts.dm:382
        unarmed_attack_verbs_continuous = Dm.list("kicks");
    }

    /// A speed modifier we apply to the owner when attached, if any. Positive numbers make it move slower, negative numbers make it move faster.
    // tg: code/modules/surgery/bodyparts/default_parts.dm:388
    public float? speed_modifier = 0;
    /// Datum describing how to offset things worn on the foot of this leg, note that an x offset won't do anything here
    // tg: code/modules/surgery/bodyparts/default_parts.dm:390
    public datum_worn_feature_offset worn_foot_offset;
    /// Used by the bloodysoles component to make footprints
    // tg: code/modules/surgery/bodyparts/default_parts.dm:392
    public string footprint_sprite = FOOTPRINT_SPRITE_SHOES;
    /// What does our footsteps (barefoot) sound like? Only BAREFOOT, CLAW, HEAVY, and SHOE (or null, I guess) are valid
    // tg: code/modules/surgery/bodyparts/default_parts.dm:394
    public string footstep_type = FOOTSTEP_MOB_BAREFOOT;
    /// You can set this to a list of sounds to pick from when a footstep is played rather than use the footstep types
    /// Requires special formatting: list(list(sounds, go, here), volume, range modifier)
    // tg: code/modules/surgery/bodyparts/default_parts.dm:397
    public DmList special_footstep_sounds;
}

// tg: code/modules/surgery/bodyparts/default_parts.dm:449
public partial class obj_item_bodypart_leg : obj_item_bodypart
{
    // tg: code/modules/surgery/bodyparts/default_parts.dm:399
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(FALSE)) // PERFORM_ALL_TESTS(focus_only/humanstep_validity): FALSE in production (rules 1.7)
        {
            // Update this list if more types are suported in the footstep element
            DmList supported_types = Dm.list(
                null,
                FOOTSTEP_MOB_BAREFOOT,
                FOOTSTEP_MOB_CLAW,
                FOOTSTEP_MOB_HEAVY,
                FOOTSTEP_MOB_SHOE,
                FOOTSTEP_MOB_SYNTHETIC);
            if (!Dm.@in(footstep_type, supported_types))
                stack_trace(Dm.text("Invalid footstep type set on leg: \\[[]\\] If you want to use this type, you will need to create a global footstep index for it.", footstep_type), "code/modules/surgery/bodyparts/default_parts.dm", 411);
        }
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:415
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref worn_foot_offset);
        return base.Destroy(force);
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:419
    public override async DmTask<object> set_disabled(object new_disabled = null, float? update_limbs = null)
    {
        update_limbs ??= TRUE;
        object dot = null;
        dot = await base.set_disabled(new_disabled, update_limbs);
        if (dot == null || owner == null || !Dm.truthy(update_limbs))
            return dot;

        if (!Dm.truthy(dot))
        {
            if (Dm.truthy(bodypart_disabled))
            {
                await owner.set_usable_legs(owner.usable_legs - 1);
                if (!IS_UNCONSCIOUS(owner))
                    to_chat(owner, span_userdanger(I18n.T("You lose control of your []!", plaintext_zone)));
            }
        }
        else if (!Dm.truthy(bodypart_disabled))
            await owner.set_usable_legs(owner.usable_legs + 1);
        return dot;
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:439
    public override async DmTask apply_ownership(mob_living_carbon new_owner = null)
    {
        await base.apply_ownership(new_owner);
        if (Dm.truthy(speed_modifier))
            await new_owner.update_bodypart_speed_modifier();
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:444
    public override async DmTask clear_ownership(mob_living_carbon old_owner = null)
    {
        await base.clear_ownership(old_owner);
        if (Dm.truthy(speed_modifier))
            await old_owner.update_bodypart_speed_modifier();
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:449
    public virtual async DmTask set_speed_modifier(object new_modifier = null)
    {
        if (Dm.eq(speed_modifier, new_modifier))
            return;

        speed_modifier = Dm.num_or_null(new_modifier);
        if (owner != null)
            await owner.update_bodypart_speed_modifier();
    }
}

// tg: code/modules/surgery/bodyparts/default_parts.dm:457 (interleaved types, rules 1.9)
public partial class obj_item_bodypart_leg : obj_item_bodypart
{
    /// block attachment of legs if the owner has the related trait
    // tg: code/modules/surgery/bodyparts/default_parts.dm:457
    [DmParams("owner", "special")]
    public override object can_attach_limb(mob_living_carbon new_limb_owner = null, object special = null)
    {
        mob_living_carbon owner = new_limb_owner;
        if (!Dm.truthy(special) && HAS_TRAIT(owner, TRAIT_BLOCK_ATTACHING_LEGS))
            return FALSE;
        return base.can_attach_limb(owner, special);
    }
}

// tg: code/modules/surgery/bodyparts/default_parts.dm:462
// The type block is translated in full by the M2 contracts step; the procs are still skeletons.
[DmPath("/obj/item/bodypart/leg/left")]
public partial class obj_item_bodypart_leg_left : obj_item_bodypart_leg
{
    // The constant var overrides of the type block (rules 10.2), in DM declaration order.
    public obj_item_bodypart_leg_left()
    {
        // tg: code/modules/surgery/bodyparts/default_parts.dm:463
        name = "left leg";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:464
        desc = "Some athletes prefer to tie their left shoelaces first for good luck. In this instance, it probably would not have helped.";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:466
        icon_state = "default_human_l_leg";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:467
        body_zone = BODY_ZONE_L_LEG;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:468
        body_part = LEG_LEFT;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:469
        plaintext_zone = "left leg";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:470
        px_x = -2;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:471
        px_y = 12;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:472
        can_be_disabled = TRUE;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:473
        bodypart_trait_source = LEFT_LEG_TRAIT;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:474
        butcher_replacement = typeof(obj_item_bodypart_leg_left_skeleton_nonfunctional);
        // tg: code/modules/surgery/bodyparts/default_parts.dm:475
        stump_typepath = typeof(obj_item_bodypart_leg_left_stump);
    }
}

// tg: code/modules/surgery/bodyparts/default_parts.dm:495
public partial class obj_item_bodypart_leg_left : obj_item_bodypart_leg
{
    // tg: code/modules/surgery/bodyparts/default_parts.dm:477
    public override async DmTask apply_ownership(mob_living_carbon new_owner = null)
    {
        if (HAS_TRAIT(new_owner, TRAIT_PARALYSIS_L_LEG))
        {
            ADD_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_L_LEG);
            RegisterSignal(new_owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_L_LEG), nameof(on_owner_paralysis_loss));
        }
        else
        {
            REMOVE_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_L_LEG);
            RegisterSignal(new_owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_L_LEG), nameof(on_owner_paralysis_gain));
        }
        await base.apply_ownership(new_owner);
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:486
    public override async DmTask clear_ownership(mob_living_carbon old_owner = null)
    {
        await base.clear_ownership(old_owner);
        if (HAS_TRAIT(old_owner, TRAIT_PARALYSIS_L_LEG))
        {
            UnregisterSignal(old_owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_L_LEG));
            REMOVE_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_L_LEG);
        }
        else
            UnregisterSignal(old_owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_L_LEG));
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:495
    [SignalHandler]
    public virtual void on_owner_paralysis_gain(mob_living_carbon source = null)
    {
        ADD_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_L_LEG);
        UnregisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_L_LEG));
        RegisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_L_LEG), nameof(on_owner_paralysis_loss));
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:502
    [SignalHandler]
    public virtual void on_owner_paralysis_loss(mob_living_carbon source = null)
    {
        REMOVE_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_L_LEG);
        UnregisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_L_LEG));
        RegisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_L_LEG), nameof(on_owner_paralysis_gain));
    }
}

// tg: code/modules/surgery/bodyparts/default_parts.dm:508
// The type block is translated in full by the M2 contracts step; the procs are still skeletons.
[DmPath("/obj/item/bodypart/leg/right")]
public partial class obj_item_bodypart_leg_right : obj_item_bodypart_leg
{
    // The constant var overrides of the type block (rules 10.2), in DM declaration order.
    public obj_item_bodypart_leg_right()
    {
        // tg: code/modules/surgery/bodyparts/default_parts.dm:509
        name = "right leg";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:510
        desc = "You put your right leg in, your right leg out. In, out, in, out, shake it all about. And apparently then it detaches.\nThe hokey pokey has certainly changed a lot since space colonisation.";
        // alternative spellings of 'pokey' are available
        // tg: code/modules/surgery/bodyparts/default_parts.dm:514
        icon_state = "default_human_r_leg";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:515
        body_zone = BODY_ZONE_R_LEG;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:516
        body_part = LEG_RIGHT;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:517
        plaintext_zone = "right leg";
        // tg: code/modules/surgery/bodyparts/default_parts.dm:518
        px_x = 2;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:519
        px_y = 12;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:520
        bodypart_trait_source = RIGHT_LEG_TRAIT;
        // tg: code/modules/surgery/bodyparts/default_parts.dm:521
        butcher_replacement = typeof(obj_item_bodypart_leg_right_skeleton_nonfunctional);
        // tg: code/modules/surgery/bodyparts/default_parts.dm:522
        stump_typepath = typeof(obj_item_bodypart_leg_right_stump);
    }
}

// tg: code/modules/surgery/bodyparts/default_parts.dm:542
public partial class obj_item_bodypart_leg_right : obj_item_bodypart_leg
{
    // tg: code/modules/surgery/bodyparts/default_parts.dm:524
    public override async DmTask apply_ownership(mob_living_carbon new_owner = null)
    {
        if (HAS_TRAIT(new_owner, TRAIT_PARALYSIS_R_LEG))
        {
            ADD_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_R_LEG);
            RegisterSignal(new_owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_R_LEG), nameof(on_owner_paralysis_loss));
        }
        else
        {
            REMOVE_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_R_LEG);
            RegisterSignal(new_owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_R_LEG), nameof(on_owner_paralysis_gain));
        }
        await base.apply_ownership(new_owner);
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:533
    public override async DmTask clear_ownership(mob_living_carbon old_owner = null)
    {
        await base.clear_ownership(old_owner);
        if (HAS_TRAIT(old_owner, TRAIT_PARALYSIS_R_LEG))
        {
            UnregisterSignal(old_owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_R_LEG));
            REMOVE_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_R_LEG);
        }
        else
            UnregisterSignal(old_owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_R_LEG));
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:542
    [SignalHandler]
    public virtual void on_owner_paralysis_gain(mob_living_carbon source = null)
    {
        ADD_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_R_LEG);
        UnregisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_R_LEG));
        RegisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_R_LEG), nameof(on_owner_paralysis_loss));
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:549
    [SignalHandler]
    public virtual void on_owner_paralysis_loss(mob_living_carbon source = null)
    {
        REMOVE_TRAIT(this, TRAIT_PARALYSIS, TRAIT_PARALYSIS_R_LEG);
        UnregisterSignal(owner, SIGNAL_REMOVETRAIT(TRAIT_PARALYSIS_R_LEG));
        RegisterSignal(owner, SIGNAL_ADDTRAIT(TRAIT_PARALYSIS_R_LEG), nameof(on_owner_paralysis_gain));
    }
}

// tg: code/modules/surgery/bodyparts/default_parts.dm:214
public partial class datum_status_effect_arm_speed_penalty : datum_status_effect
{
    // tg: code/modules/surgery/bodyparts/default_parts.dm:229
    [DmParams("new_owner", "hand_index", "new_actionspeed", "new_click_cd")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object hand_index = Dm.arg(__extra, 0);
        object new_actionspeed = Dm.arg(__extra, 1) ?? 0f;
        object new_click_cd = Dm.arg(__extra, 2) ?? 1f;
        this.hand_index = hand_index;
        actionspeed_mod = Dm.num(new_actionspeed);
        click_cd_mod = Dm.num(new_click_cd);

        if (Dm.eq(hand_index, 1))
            actionspeed_typepath = typeof(datum_actionspeed_modifier_arm_speed_penalty_left_hand);
        else if (Dm.eq(hand_index, 2))
            actionspeed_typepath = typeof(datum_actionspeed_modifier_arm_speed_penalty_right_hand);

        return await base.on_creation(new_owner, Dm.forward(__extra, hand_index, new_actionspeed, new_click_cd));
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:242
    [DmParams("hand_index")]
    public override object before_remove(params object[] __extra)
    {
        return Dm.num(Dm.eq(this.hand_index, Dm.arg(__extra, 0)));
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:245
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_MOB_SWAP_HANDS, nameof(on_handswap));
        on_handswap(owner as mob_living_carbon);
        return TRUE;
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:250
    public override async DmTask<object> on_remove()
    {
        owner.remove_actionspeed_modifier(actionspeed_typepath);
        UnregisterSignal(owner, COMSIG_MOB_SWAP_HANDS);
        return null;
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:254
    [SignalHandler]
    public virtual void on_handswap(mob_living_carbon source = null)
    {
        if (Dm.eq(owner.active_hand_index, hand_index))
            Waiting.NoSleep(owner.add_or_update_variable_actionspeed_modifier(actionspeed_typepath, update: TRUE, multiplicative_slowdown: actionspeed_mod));
        else
            owner.remove_actionspeed_modifier(actionspeed_typepath);
    }

    // tg: code/modules/surgery/bodyparts/default_parts.dm:261
    public override object nextmove_modifier()
    {
        return Dm.eq(owner.active_hand_index, hand_index) ? click_cd_mod : 1;
    }
}
