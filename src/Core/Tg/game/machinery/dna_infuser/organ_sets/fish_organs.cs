// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_infuser/organ_sets/fish_organs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327): the fish's infused organs (the swimming tail that waddles, flops and speeds up in water, the gills
// that breathe water vapour or wetness with their overlay, the amphibious lungs, the fish-eating stomach, the blubbing tongue and the
// ink-spitting one, the tetrodotoxin liver) and their set bonus: sturdier and healing while wet, weaker when dry, the fish colour at
// all five organs, the fishing skill's experience (gaps SL1). One leaf: the tail's filleting with a knife (the processable element,
// with the food); not-translated section 166. The colour defines are file-local; the var blocks are
// generated (D111).

// tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:9
public partial class datum_status_effect_organ_set_bonus_fish : datum_status_effect_organ_set_bonus
{
    /// How many fishy organs can you have at once, requirement to get the tail color
    /// Currently liver, stomach, lungs and tail plus tongue
    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:7  #define FISH_INFUSION_ALL_ORGANS 5 (file-local)
    private const float FISH_INFUSION_ALL_ORGANS = 5;

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:33
    public override async DmTask<object> enable_bonus(obj_item_organ inserted_organ = null, object visuals_only = null)
    {
        object dot = await base.enable_bonus(inserted_organ, visuals_only);
        if (!Dm.truthy(dot) || Dm.truthy(visuals_only))
            return dot;
        RegisterSignals(owner, Dm.list(COMSIG_CARBON_GAIN_ORGAN, COMSIG_CARBON_LOSE_ORGAN), nameof(check_tail));
        RegisterSignals(owner, Dm.list(SIGNAL_ADDTRAIT(TRAIT_IS_WET), SIGNAL_REMOVETRAIT(TRAIT_IS_WET)), nameof(update_wetness));
        RegisterSignal(owner, COMSIG_LIVING_GET_PERCEIVED_FOOD_QUALITY, nameof(get_perceived_food_quality)); // RegisterSignals with the one signal

        //Fish is slightly weaker to being cooked. oh oh.
        MODIFY_PHYSIOLOGY(owner, BURN, 1.15f);
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_HEAT, 1.15f);
        owner.damage_resistance += 8; //base 8% damage resistance, much wow.
        if (!HAS_TRAIT(owner, TRAIT_IS_WET))
            await apply_debuff();
        else
        {
            ADD_TRAIT(owner, TRAIT_GRABRESISTANCE, TRAIT_STATUS_EFFECT(id));
            await owner.add_mood_event("fish_organs_bonus", typeof(datum_mood_event_fish_water));
        }
        if (HAS_TRAIT(owner, TRAIT_IS_WET) && owner.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) is obj_item_organ_tail_fish)
            add_speed_buff();
        owner.mind?.adjust_experience(typeof(datum_skill_fishing), SKILL_EXP_JOURNEYMAN, silent: TRUE);
        owner.grant_language(typeof(datum_language_carptongue), ALL, Dm.str(type));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:56
    public override async DmTask disable_bonus(obj_item_organ removed_organ = null, object visuals_only = null)
    {
        await base.disable_bonus(removed_organ, visuals_only);
        if (Dm.truthy(visuals_only))
            return;
        UnregisterSignal(owner, Dm.list(
            COMSIG_CARBON_GAIN_ORGAN,
            COMSIG_CARBON_LOSE_ORGAN,
            SIGNAL_ADDTRAIT(TRAIT_IS_WET),
            SIGNAL_REMOVETRAIT(TRAIT_IS_WET),
            COMSIG_LIVING_TREAT_MESSAGE,
            COMSIG_LIVING_GET_PERCEIVED_FOOD_QUALITY));
        if (!HAS_TRAIT(owner, TRAIT_IS_WET))
            await remove_debuff();
        else
            REMOVE_TRAIT(owner, TRAIT_GRABRESISTANCE, TRAIT_STATUS_EFFECT(id));
        owner.clear_mood_event("fish_organs_bonus");
        MODIFY_PHYSIOLOGY(owner, BURN, 1 / 1.15f);
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_HEAT, 1 / 1.15f);
        owner.damage_resistance -= 8;
        if (HAS_TRAIT(owner, TRAIT_IS_WET) && owner.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) is obj_item_organ_tail_fish)
            remove_speed_buff();
        owner.mind?.adjust_experience(typeof(datum_skill_fishing), -SKILL_EXP_JOURNEYMAN, silent: TRUE);
        owner.remove_language(typeof(datum_language_carptongue), ALL, Dm.str(type));
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:80
    public override void set_organs(object new_value = null, obj_item_organ organ = null, object visuals_only = null)
    {
        base.set_organs(new_value, organ, visuals_only);
        if (!iscarbon(owner))
            return;
        mob_living_carbon carbon_owner = owner as mob_living_carbon;
        if (Dm.num(new_value) >= FISH_INFUSION_ALL_ORGANS)
        {
            if (!Dm.truthy(color_active))
            {
                foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(carbon_owner.get_bodyparts() as DmList))
                {
                    if (Dm.truthy(Dm.bitand(limb.bodytype, BODYTYPE_ROBOTIC)))
                        continue;
                    limb.add_color_override(carbon_owner.dna.features[FEATURE_TAIL_FISH_COLOR], LIMB_COLOR_FISH_INFUSION);
                }
                color_active = TRUE;
            }
            return;
        }

        if (!Dm.truthy(color_active))
            return;

        foreach (obj_item_bodypart limb in Dm.each_cast<obj_item_bodypart>(carbon_owner.get_bodyparts() as DmList))
            limb.remove_color_override(LIMB_COLOR_FISH_INFUSION);
        color_active = FALSE;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:100
    public override void texture_limb(atom source = null, obj_item_bodypart limb = null)
    {
        base.texture_limb(source, limb);
        if (!Dm.truthy(color_active) || !iscarbon(owner) || Dm.truthy(Dm.bitand(limb.bodytype, BODYTYPE_ROBOTIC)))
            return;
        mob_living_carbon carbon_owner = owner as mob_living_carbon;
        limb.add_color_override(carbon_owner.dna.features[FEATURE_TAIL_FISH_COLOR], LIMB_COLOR_FISH_INFUSION);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:107
    public override void untexture_limb(atom source = null, obj_item_bodypart limb = null)
    {
        base.untexture_limb(source, limb);
        if (Dm.truthy(color_active))
            limb.remove_color_override(LIMB_COLOR_FISH_INFUSION);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:112
    [SignalHandler]
    public virtual void get_perceived_food_quality(datum source = null, datum_component_edible edible = null, DmList extra_quality = null)
    {
        if (HAS_TRAIT(edible.parent, TRAIT_GREAT_QUALITY_BAIT))
            Dm.append(ref extra_quality, LIKED_FOOD_QUALITY_CHANGE * 3);
        else if (HAS_TRAIT(edible.parent, TRAIT_GOOD_QUALITY_BAIT))
            Dm.append(ref extra_quality, LIKED_FOOD_QUALITY_CHANGE * 2);
        else if (HAS_TRAIT(edible.parent, TRAIT_BASIC_QUALITY_BAIT))
            Dm.append(ref extra_quality, LIKED_FOOD_QUALITY_CHANGE);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:121
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        object dot = await base.tick(seconds_between_ticks);
        if (!Dm.truthy(bonus_active) || !HAS_TRAIT(owner, TRAIT_IS_WET))
            return dot;
        owner.adjust_bodytemperature(-2 * Dm.num(seconds_between_ticks), min_temp: Dm.num(owner.get_body_temp_normal()));
        await owner.adjust_stamina_loss(-1.5f * Dm.num(seconds_between_ticks));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:128
    [SignalHandler]
    public virtual void update_wetness(datum source = null, params object[] __extra)
    {
        if (HAS_TRAIT(owner, TRAIT_IS_WET)) //remove the debuffs from being dry
        {
            Waiting.NoSleep(remove_debuff());
            if (owner.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) is obj_item_organ_tail_fish)
                add_speed_buff();
            return;
        }
        Waiting.NoSleep(apply_debuff());
        if (owner.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) is obj_item_organ_tail_fish)
            remove_speed_buff();
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:140
    public virtual async DmTask apply_debuff()
    {
        REMOVE_TRAIT(owner, TRAIT_GRABRESISTANCE, REF(this));
        await owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_fish_waterless));
        await owner.add_mood_event("fish_organs_bonus", typeof(datum_mood_event_fish_waterless));
        MODIFY_PHYSIOLOGY(owner, BURN, 1.5f);
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_HEAT, 1.2f);
        MODIFY_PHYSIOLOGY(owner, BRUTE, 1.1f);
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_STUN, 1.1f);
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_KNOCKDOWN, 1.1f);
        MODIFY_PHYSIOLOGY(owner, STAMINA, 1.1f);
        owner.damage_resistance -= 16; // This should bring the resistance from +8% to -8%
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:152
    public virtual async DmTask remove_debuff()
    {
        ADD_TRAIT(owner, TRAIT_GRABRESISTANCE, TRAIT_STATUS_EFFECT(id)); //harder to grab when wet.
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_fish_waterless));
        await owner.add_mood_event("fish_organs_bonus", typeof(datum_mood_event_fish_water));
        MODIFY_PHYSIOLOGY(owner, BURN, 1 / 1.5f);
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_HEAT, 1 / 1.2f);
        MODIFY_PHYSIOLOGY(owner, BRUTE, 1 / 1.1f);
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_STUN, 1 / 1.1f);
        MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_KNOCKDOWN, 1 / 1.1f);
        MODIFY_PHYSIOLOGY(owner, STAMINA, 1 / 1.1f);
        owner.damage_resistance += 16;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:164
    [SignalHandler]
    public virtual void check_tail(mob_living_carbon source = null, obj_item_organ organ = null, object special = null)
    {
        if (!HAS_TRAIT(owner, TRAIT_IS_WET) || organ is not obj_item_organ_tail_fish)
            return;
        obj_item_organ tail = owner.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) as obj_item_organ;
        if (tail != organ)
        {
            remove_speed_buff();
            return;
        }
        add_speed_buff();
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:174
    [SignalHandler]
    public virtual void add_speed_buff(datum source = null)
    {
        RegisterSignal(owner, COMSIG_LIVING_SET_BODY_POSITION, nameof(check_body_position));
        check_body_position();
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:179
    [SignalHandler]
    public virtual void remove_speed_buff(datum source = null)
    {
        UnregisterSignal(owner, COMSIG_LIVING_SET_BODY_POSITION);
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_fish_flopping));
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:184
    [SignalHandler]
    public virtual void check_body_position(datum source = null, params object[] __extra)
    {
        if (owner.body_position == LYING_DOWN)
            Waiting.NoSleep(owner.add_movespeed_modifier(typeof(datum_movespeed_modifier_fish_flopping)));
        else
            owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_fish_flopping));
    }
}

///Tail for fish DNA-infused spacemen. It provides a speed buff while in water. It's also needed for the crawl speed bonus once the threshold is reached.
// tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:193
public partial class obj_item_organ_tail_fish : obj_item_organ_tail
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:223
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_fish)));
        float time_to_fillet = fillet_amount * 0.5f * SECONDS;
        Waiting.NoSleep(AddElement(typeof(datum_element_processable), TOOL_KNIFE, fillet_type, fillet_amount, time_to_fillet, Dm.named("screentip_verb", "Cut"))); // gaps H2
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:229
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        await organ_owner.AddElementTrait(TRAIT_WADDLING, type, typeof(datum_element_waddling));
        RegisterSignal(organ_owner, COMSIG_MOVABLE_MOVED, nameof(check_location));
        RegisterSignal(organ_owner, COMSIG_LIVING_GIBBER_ACT, nameof(on_gibber_processed));
        check_location(organ_owner, null);
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:236
    public override async DmTask on_mob_remove(object organ_owner__arg = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon organ_owner = organ_owner__arg as mob_living_carbon;
        await base.on_mob_remove(organ_owner__arg, special, movement_flags);
        organ_owner.remove_traits(Dm.list(TRAIT_WADDLING, TRAIT_NO_STAGGER), type);
        organ_owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_fish_on_water));
        organ_owner.remove_actionspeed_modifier(typeof(datum_actionspeed_modifier_fish_on_water));
        UnregisterSignal(organ_owner, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_LIVING_GIBBER_ACT));
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:243
    [SignalHandler]
    public virtual void on_gibber_processed(mob_living_carbon gibbed = null, mob_living user = null, obj_machinery gibber = null, DmList results = null)
    {
        for (float iteration = 1, iteration__end = fillet_amount * 0.5f; iteration <= iteration__end; iteration++)
            Dm.append(ref results, Dm.@new(fillet_type));
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:248
    public override async DmTask get_greyscale_color_from_draw_color()
    {
        await set_greyscale((bodypart_overlay as datum_bodypart_overlay_mutant).draw_color);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:251
    [SignalHandler]
    public virtual void check_location(mob_living_carbon source = null, atom old_loc = null, object dir = null, object forced = null)
    {
        bool was_water = old_loc is turf_open_water;
        bool is_water = source.loc is turf_open_water && !HAS_TRAIT(source.loc, TRAIT_TURF_IGNORE_SLOWDOWN);
        if (was_water && !is_water)
        {
            source.remove_movespeed_modifier(typeof(datum_movespeed_modifier_fish_on_water));
            source.remove_actionspeed_modifier(typeof(datum_actionspeed_modifier_fish_on_water));
            source.add_traits(Dm.list(TRAIT_OFF_BALANCE_TACKLER, TRAIT_NO_STAGGER, TRAIT_NO_THROW_HITPUSH), type);
        }
        else if (!was_water && is_water)
        {
            Waiting.NoSleep(source.add_movespeed_modifier(typeof(datum_movespeed_modifier_fish_on_water)));
            Waiting.NoSleep(source.add_actionspeed_modifier(typeof(datum_actionspeed_modifier_fish_on_water)));
            source.add_traits(Dm.list(TRAIT_OFF_BALANCE_TACKLER, TRAIT_NO_STAGGER, TRAIT_NO_THROW_HITPUSH), type);
        }
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:265
public partial class datum_bodypart_overlay_mutant_tail_fish : datum_bodypart_overlay_mutant_tail
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:271
    public override void on_mob_insert(obj_item_organ parent = null, mob_living_carbon receiver = null)
    {
        if (Dm.truthy(imprint_on_next_insertion) || !Dm.@in(receiver.dna.features[feature_key], get_global_feature_list()))
        {
            receiver.dna.features[feature_key] = (get_random_appearance() as datum_sprite_accessory).name;
            receiver.dna.update_uf_block(typeof(datum_dna_block_feature_accessory_tail_fish));
        }
        base.on_mob_insert(parent, receiver);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:277
    public override object override_color(obj_item_bodypart limb = null)
    {
        if (limb.owner == null || HAS_TRAIT(limb.owner, TRAIT_MUTANT_COLORS))
            return limb.draw_color;
        return limb.owner.dna.features[FEATURE_TAIL_FISH_COLOR];
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:282
    public override void randomize_appearance()
    {
        return; //we already do this above, we are bound to dna!
    }
}

///Lungs that replace the need of oxygen with water vapor or being wet
// tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:286
public partial class obj_item_organ_lungs_fish : obj_item_organ_lungs
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:304
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_gas_reaction(typeof(datum_gas_water_vapor), always: nameof(breathe_water));
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_fish)));
        if (Dm.truthy(has_gills))
        {
            gills = Dm.@new<datum_bodypart_overlay_simple_gills>();
            Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_Theyve a set of gills on %PRONOUN_their neck."), BODY_ZONE_PRECISE_MOUTH));
        }
        Waiting.NoSleep(AddComponent(typeof(datum_component_bubble_icon_override), "fish", BUBBLE_ICON_PRIORITY_ORGAN));
        Waiting.NoSleep(AddComponent(typeof(datum_component_speechmod), Dm.named("replacements", strings("crustacean_replacement.json", "crustacean"))));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:315
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref gills);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:319
    public override async DmTask on_bodypart_insert(obj_item_bodypart limb = null)
    {
        await base.on_bodypart_insert(limb);
        if (gills != null)
            await limb.add_bodypart_overlay(gills);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:324
    public override async DmTask on_bodypart_remove(obj_item_bodypart limb = null, object movement_flags = null)
    {
        await base.on_bodypart_remove(limb, movement_flags);
        if (gills != null)
            await limb.remove_bodypart_overlay(gills);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:329
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        ((mob_living_carbon)organ_owner).clear_alert(ALERT_NOT_ENOUGH_WATER);
    }

    /// Requires the spaceman to have either water vapor or be wet.
    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:334
    public virtual async DmTask breathe_water(mob_living_carbon breather = null, datum_gas_mixture breath = null, object water_pp = null, object old_water_pp = null)
    {
        bool need_to_breathe = !HAS_TRAIT(breather, TRAIT_NO_BREATHLESS_DAMAGE) && !HAS_TRAIT(breather, TRAIT_IS_WET);
        if (Dm.num(water_pp) < safe_water_level && need_to_breathe)
        {
            await on_low_water(breather, breath, water_pp);
            return;
        }

        if (Dm.num(old_water_pp) < safe_water_level || Dm.truthy(breather.failed_last_breath))
        {
            breather.failed_last_breath = FALSE;
            breather.clear_alert(ALERT_NOT_ENOUGH_WATER);
        }

        if (need_to_breathe)
            breathe_gas_volume(breath, typeof(datum_gas_water_vapor), typeof(datum_gas_carbon_dioxide));
        // Heal mob if not in crit.
        if (breather.health >= breather.crit_threshold && breather.oxyloss != 0)
            await breather.adjust_oxy_loss(-5);
    }

    /// Called when there isn't enough water to breath
    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:351
    public virtual async DmTask on_low_water(mob_living_carbon breather = null, datum_gas_mixture breath = null, object water_pp = null)
    {
        breather.throw_alert(ALERT_NOT_ENOUGH_WATER, typeof(atom_movable_screen_alert_not_enough_water));
        object gas_breathed = await handle_suffocation(breather as mob_living_carbon_human, Dm.num(water_pp), safe_water_level, Dm.num(breath.moles[typeof(datum_gas_water_vapor)]));
        if (Dm.truthy(water_pp))
            breathe_gas_volume(breath, typeof(datum_gas_water_vapor), typeof(datum_gas_carbon_dioxide), volume: Dm.num(gas_breathed));
    }
}

// Simple overlay so we can add gills to those with fish lungs
// tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:358
public partial class datum_bodypart_overlay_simple_gills : datum_bodypart_overlay_simple
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:364
    public override object get_image(obj_item_bodypart limb = null, object layer_index = null, object layer_real = null)
    {
        return image(icon: icon, icon_state: Dm.text("[]_[]", icon_state, layer_index), layer: layer_real);
    }
}

/// Subtype of gills that allow the mob to optionally breathe water.
// tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:372
public partial class obj_item_organ_lungs_fish_amphibious : obj_item_organ_lungs_fish
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:384
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        /**
         * We're setting the gas reaction for breathing oxygen here,
         * since gas reation procs are run in the order they're added,
         * and we want breathe_water() to run before breathe_oxygen,
         * so that if we're breathing water vapor (or are wet), we won't have to breathe oxygen.
         */
        safe_oxygen_min = Dm.initial(typeof(obj_item_organ_lungs), (obj_item_organ_lungs l) => l.safe_oxygen_min);
        add_gas_reaction(typeof(datum_gas_oxygen), always: nameof(breathe_oxygen));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:395
    public override async DmTask<object> check_breath(datum_gas_mixture breath = null, mob_living_carbon_human breather = null)
    {
        should_breathe_oxygen = FALSE; //assume we don't have to breathe oxygen until we fail to breathe water
        return await base.check_breath(breath, breather);
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:399
    public override async DmTask on_low_water(mob_living_carbon breather = null, datum_gas_mixture breath = null, object water_pp = null)
    {
        await DmTask.CompletedTask;
        should_breathe_oxygen = TRUE;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:403
    public override async DmTask breathe_oxygen(mob_living_carbon breather = null, datum_gas_mixture breath = null, object o2_pp = null, object old_o2_pp = null)
    {
        if (!Dm.truthy(should_breathe_oxygen))
        {
            if (Dm.truthy(breather.failed_last_breath)) //in case we had neither oxygen nor water last tick.
                breather.clear_alert(ALERT_NOT_ENOUGH_OXYGEN);
            return;
        }
        await base.breathe_oxygen(breather, breath, o2_pp, old_o2_pp);
    }
}

///Fish infuser organ, allows mobs to safely eat raw fish.
// tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:411
public partial class obj_item_organ_stomach_fish : obj_item_organ_stomach
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:431
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_fish)));
        return dot;
    }
}

// tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:435
public partial class obj_item_organ_tongue_fish : obj_item_organ_tongue
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:444
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_fish)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:448
    public override object get_possible_languages()
    {
        object dot = base.get_possible_languages();
        Dm.append(ref dot, typeof(datum_language_carptongue));
        return dot;
    }
}

///Organ from fish with the ink production trait.
// tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:453
public partial class obj_item_organ_tongue_fish_inky : obj_item_organ_tongue_fish
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:495
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("Slick black ink seldom rivulets from %PRONOUN_their mouth."), BODY_ZONE_PRECISE_MOUTH));
        return dot;
    }
}

///Organ from fish with the toxic trait. Allows the user to use tetrodotoxin as a healing chem instead of a toxin.
// tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:500
public partial class obj_item_organ_liver_fish : obj_item_organ_liver
{
    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:519
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_organ_set_bonus), typeof(datum_status_effect_organ_set_bonus_fish)));
        return dot;
    }

    // tg: code/game/machinery/dna_infuser/organ_sets/fish_organs.dm:523
    public override async DmTask<object> grind_results()
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv(typeof(datum_reagent_consumable_nutriment_peptides), 5), Dm.kv(typeof(datum_reagent_toxin_tetrodotoxin), 5));
    }
}
