// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/blood.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M2 P1 (the living core): the blood system's M2 subset is translated (volume, status, loss and regeneration, bleeding,
// the blood type lookup, the trail's blood cost); transfusions, blood data, the decals and the reagent and wound parts stay
// Dm.NotTranslated or deferred with their milestones. M8 slice C (D192): blood transfer, blood data and compatibility.

/****************************************************
				BLOOD SYSTEM
****************************************************/

// tg: code/modules/mob/living/blood.dm:11
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/blood.dm:1  #define BLOOD_DRIP_RATE_MOD 90 //Greater number means creating blood drips more often while bleeding (file-local, #undef at :832)
    private const int BLOOD_DRIP_RATE_MOD = 90;
    // Conversion between internal drunk power and common blood alcohol content
    // tg: code/modules/mob/living/blood.dm:3  #define DRUNK_POWER_TO_BLOOD_ALCOHOL 0.003 (file-local, #undef at :833)
    private const float DRUNK_POWER_TO_BLOOD_ALCOHOL = 0.003f;

    /// Returns whether this mob can have blood.
    /// Use the CAN_HAVE_BLOOD(mob) macro instead, this is used to update the cached value.
    // tg: code/modules/mob/living/blood.dm:11
    public virtual object can_have_blood()
    {
        return Dm.num(default_blood_volume > 0);
    }

    /// Returns the blood volume of the mob.
    /// Apply modifiers when reading blood volume for oxyloss damage, HUDs and analyzers.
    /// Don't apply modifiers when using blood itself, like in spells and reagent transfers.
    // tg: code/modules/mob/living/blood.dm:20
    public virtual object get_blood_volume(float? apply_modifiers = null)
    {
        apply_modifiers ??= FALSE;
        return (Dm.truthy(CAN_HAVE_BLOOD(this)) ? blood_volume : 0); // Overriding blood setting code can cause blood_volume to be non-zero even when a mob shouldn't have blood.
    }

    /// Sets the base blood volume of the mob, returns the blood volume of the mob after.
    // tg: code/modules/mob/living/blood.dm:49
    public virtual object set_blood_volume(object amount = null, float? minimum = null, float? maximum = null, object cached_blood_volume = null)
    {
        minimum ??= 0;
        maximum ??= BLOOD_VOLUME_MAXIMUM;
        if (!isnum(cached_blood_volume))
            cached_blood_volume = get_blood_volume();

        if (!Dm.truthy(CAN_HAVE_BLOOD(this)) && !Dm.eq(amount, 0))
            return cached_blood_volume;

        // Don't return early even if "amount == cached_blood_volume", because we don't know if minimum or maximum would change it anyway.
        // Putting this here because I made that mistake and it led to a bug.

        blood_volume = Dm.num(clamp(amount, minimum, maximum));

        object updated_blood_volume = get_blood_volume();

        if (!Dm.eq(cached_blood_volume, updated_blood_volume))
            QUEUE_BLOOD_UPDATE(this);

        return updated_blood_volume;
    }

    /// Adjusts the base blood volume of the mob and returns the change.
    /// Increases in blood volume give a positive return value and vice versa.
    /// Maximum only applies on positive amounts and vice versa.
    // tg: code/modules/mob/living/blood.dm:71
    public virtual object adjust_blood_volume(object amount = null, float? minimum = null, float? maximum = null)
    {
        minimum ??= 0;
        maximum ??= BLOOD_VOLUME_MAXIMUM;
        if (!Dm.truthy(CAN_HAVE_BLOOD(this)) || Dm.eq(amount, 0))
            return 0;

        object cached_blood_volume = get_blood_volume();

        if (Dm.lt(amount, 0))
        {
            if (Dm.le(cached_blood_volume, minimum))
            {
                // Already at or below the minimum, don't decrease further.
                return 0;
            }
            // Decreases shouldn't jump the pre-existing value to the maximum.
            maximum = INFINITY;
        }
        else
        {
            if (Dm.ge(cached_blood_volume, maximum))
            {
                // Already at or above the maximum, don't increase further.
                return 0;
            }
            // Increases shouldn't jump the pre-existing value to the minimum.
            minimum = -INFINITY;
        }

        object updated_blood_volume = set_blood_volume(Dm.add(cached_blood_volume, amount), minimum: minimum, maximum: maximum, cached_blood_volume: cached_blood_volume);
        return Dm.sub(updated_blood_volume, cached_blood_volume);
    }

    /// Updates effects that rely on blood volume or status, like blood HUDs.
    // tg: code/modules/mob/living/blood.dm:94
    public virtual void update_blood_effects()
    {
        blood_hud_set_status();
    }

    /// Updates effects that rely on whether the mob can have blood.
    // tg: code/modules/mob/living/blood.dm:98
    public virtual void update_blood_status()
    {
        object had_blood = CAN_HAVE_BLOOD(this);
        object has_blood = can_have_blood();

        // Must not return early on first init for mobs that can have blood. (otherwise they will miss being added to the blood hud)
        if (Dm.eq(had_blood, has_blood))
            return;

        object old_blood_volume = get_blood_volume();

        // Must be sent before living flags are updated.
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_PRE_UPDATE_BLOOD_STATUS]))
            _SendSignal(COMSIG_LIVING_PRE_UPDATE_BLOOD_STATUS, Dm.list(this, had_blood, has_blood, old_blood_volume));

        living_flags = Dm.num(Dm.truthy(has_blood) ? Dm.bitor(living_flags, LIVING_CAN_HAVE_BLOOD) : Dm.bitand(living_flags, Dm.bitnot(LIVING_CAN_HAVE_BLOOD)));

        set_blood_volume(Dm.truthy(has_blood) ? default_blood_volume : 0);

        object new_blood_volume = get_blood_volume();

        // Must be sent after living flags are updated.
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_UPDATE_BLOOD_STATUS]))
            _SendSignal(COMSIG_LIVING_UPDATE_BLOOD_STATUS, Dm.list(this, had_blood, has_blood, old_blood_volume, new_blood_volume));

        datum_atom_hud_data_human_blood blood_hud = GLOB.huds[DATA_HUD_BLOOD] as datum_atom_hud_data_human_blood;

        if (Dm.truthy(has_blood))
            blood_hud.add_atom_to_hud(this);
        else
            blood_hud.remove_atom_from_hud(this);

        update_blood_effects();
    }

    /// Sets the blood volume multiplier for the given source to the given multiplier value.
    // tg: code/modules/mob/living/blood.dm:130
    public virtual void set_blood_volume_modifier(object source = null, object multiplier = null)
    {
        LAZYSET(ref blood_volume_modifiers, source, multiplier);
        QUEUE_BLOOD_UPDATE(this);
    }

    /// Removes the blood volume multiplier for the given source.
    // tg: code/modules/mob/living/blood.dm:135
    public virtual void remove_blood_volume_modifier(object source = null)
    {
        LAZYREMOVE(ref blood_volume_modifiers, source);
        QUEUE_BLOOD_UPDATE(this);
    }

    /// A one-time coagulating effect of the mob's bloodiest cut/stab
    /// Returns TRUE if a wound was affected, FALSE if no wound was found
    // tg: code/modules/mob/living/blood.dm:141
    public virtual async DmTask<object> coagulant_effect(float? amount_to_heal = null)
    {
        amount_to_heal ??= 1;
        return FALSE;
    }

    /// Bleeds amount units of blood from the mob, sometimes creating a blood splatter on the floor.
    // tg: code/modules/mob/living/blood.dm:270
    public virtual async DmTask bleed(object amount = null)
    {
        if (HAS_TRAIT(this, TRAIT_GODMODE) || !Dm.truthy(can_bleed()))
            return;

        amount = Dm.mul(amount, GET_PHYSIOLOGY(this, PHYS_COEFF_BLEED));
        object amount_bled = Dm.neg(adjust_blood_volume(Dm.neg(amount)));

        // Blood loss still happens in locker, floor stays clean
        if (isturf(loc) && prob(sqrt(Dm.num(amount_bled)) * BLOOD_DRIP_RATE_MOD))
            await add_splatter_floor(loc as turf, Dm.num(Dm.le(amount_bled, 10)));
    }

    /// A helper to see how much blood we're losing per tick
    // tg: code/modules/mob/living/blood.dm:285
    public virtual object get_bleed_rate()
    {
        return 0;
    }

    // tg: code/modules/mob/living/blood.dm:356
    public virtual void restore_blood()
    {
        set_blood_volume(default_blood_volume);
    }

    // tg: code/modules/mob/living/blood.dm:370
    // Transfers blood from mob to a container or another mob, preserving all data in it.
    // Returns how much blood was able to be transferred.
    public virtual async DmTask<object> transfer_blood_to(atom_movable receiver = null, object amount = null, float? ignore_low_blood = null, float? ignore_incompatibility = null, float? transfer_viruses = null)
    {
        ignore_low_blood ??= FALSE;
        ignore_incompatibility ??= FALSE;
        transfer_viruses ??= TRUE;
        float cached_blood_volume = Dm.num(get_blood_volume());

        if (!Dm.truthy(cached_blood_volume) || receiver.reagents == null || Dm.num(amount) <= 0)
            return 0;

        if (cached_blood_volume < BLOOD_VOLUME_BAD && !Dm.truthy(ignore_low_blood))
            return 0;

        datum_blood_type blood_type = get_bloodtype() as datum_blood_type;
        if (blood_type == null)
            return 0;

        object blood_reagent = get_blood_reagent();
        DmList blood_data = await get_blood_data() as DmList;

        // Caps the amount to how much blood we have.
        amount = min(Dm.num(amount), Dm.num(get_blood_volume()));

        if (!Dm.truthy(ignore_low_blood))
        {
            // Caps the amount to how much we can transfer before reaching low blood.
            amount = min(Dm.num(amount), Dm.num(get_blood_volume()) - BLOOD_VOLUME_BAD);
        }

        mob_living target = receiver as mob_living;
        if (!isliving(receiver) || !Dm.eq(target.get_blood_reagent(), blood_reagent))
        {
            // Further caps the amount to how much blood we were able to add to the target.
            amount = await ((datum_reagents)receiver.reagents).add_reagent(blood_reagent, amount, blood_data, bodytemperature, creation_callback: Dm.@new<datum_callback>(this, nameof(on_blood_created), blood_type));
            adjust_blood_volume(-Dm.num(amount));
            return amount;
        }

        if (Dm.truthy(blood_data?["viruses"]) && Dm.truthy(transfer_viruses))
        {
            foreach (datum_disease blood_disease in Dm.each_cast<datum_disease>(blood_data["viruses"]))
            {
                if (Dm.truthy(Dm.bitand(blood_disease.spread_flags, Dm.bitor(DISEASE_SPREAD_SPECIAL, DISEASE_SPREAD_NON_CONTAGIOUS))))
                    continue;
                await target.ForceContractDisease(blood_disease);
            }
        }

        if (!Dm.truthy(ignore_incompatibility) && !Dm.@in(blood_type.type_key(), ((datum_blood_type)target.get_bloodtype()).compatible_types))
        {
            // Yes, we cap it to the amount of toxin. This is ridiculously niche, but we do it anyway.
            amount = Dm.num(await ((datum_reagents)target.reagents).add_reagent(typeof(datum_reagent_toxin), Dm.num(amount) * 0.5f)) * 2;
            adjust_blood_volume(-Dm.num(amount));
            return amount;
        }

        float cached_target_blood_volume = Dm.num(target.get_blood_volume());

        // And, obviously, cap it to how much blood the target can take if they're living.
        amount = target.adjust_blood_volume(amount, maximum: BLOOD_VOLUME_MAX_LETHAL);

        // Synthetic blood handling, used for capping blood worm growth from monkeys, blood bags and basic mobs. This forces blood worms to kill people to reach adulthood.
        if (!IS_BLOOD_ALWAYS_SYNTHETIC(target))
        {
            float added_synth_volume = Dm.num(amount) * Dm.num(get_blood_synth_content());
            float existing_synth_volume = cached_target_blood_volume * Dm.num(target.get_blood_synth_content());

            if (added_synth_volume != 0 || existing_synth_volume != 0)
            {
                // A simple weighted average that simplifies down to "total synth volume / total blood volume" i.e. "how much of their blood is synthetic"
                await target.AddComponent(typeof(datum_component_synth_blood), (added_synth_volume + existing_synth_volume) / (Dm.num(amount) + cached_target_blood_volume));
            }
        }

        adjust_blood_volume(-Dm.num(amount));
        return amount;
    }

    /// Callback that adds blood_reagent to any blood extracted from ourselves
    // tg: code/modules/mob/living/blood.dm:430
    public virtual async DmTask on_blood_created(datum_blood_type blood_type = null, datum_reagent new_blood = null)
    {
        await new_blood.AddElement(typeof(datum_element_blood_reagent), this, blood_type);
    }

    // tg: code/modules/mob/living/blood.dm:433
    public virtual async DmTask<object> get_blood_data()
    {
        datum_blood_type blood_type = get_bloodtype() as datum_blood_type;
        if (blood_type == null || !Dm.truthy(can_bleed()))
            return null;

        DmList blood_data = blood_type.get_default_blood_data() as DmList;

        if (reagents != null)
        {
            DmList temp_chem = Dm.list();
            foreach (datum_reagent blood_reagent in Dm.each<datum_reagent>(((datum_reagents)reagents).reagent_list))
                temp_chem[blood_reagent.GetType()] = blood_reagent.volume;
            blood_data["trace_chem"] = list2params(temp_chem);
        }

        if (Dm.truthy(Dm.bitand(blood_type.blood_flags, BLOOD_TRANSFER_VIRAL_DATA)))
        {
            // Viruses we possess
            DmList viruses = Dm.list();
            blood_data["viruses"] = viruses;
            foreach (datum_disease disease in Dm.each_cast<datum_disease>(diseases))
                Dm.append(viruses, await disease.Copy()); // gaps VI1

            if (Dm.truthy(LAZYLEN(disease_resistances)))
                blood_data["resistances"] = disease_resistances.Copy();
        }

        float synth_content = Dm.num(get_blood_synth_content());
        if (synth_content > 0)
            blood_data[BLOOD_DATA_SYNTH_CONTENT] = synth_content;

        // DNA, mind, facitons, etc don't get stored in stuff like oil
        if (!Dm.truthy(Dm.bitand(blood_type.blood_flags, BLOOD_ADD_DNA)))
            return blood_data;

        if (mind != null)
            blood_data["mind"] = mind;

        if (Dm.truthy(ckey))
            blood_data["ckey"] = ckey;
        else if (Dm.truthy(persistent_client?.mob?.ckey))
            blood_data["ckey"] = persistent_client.mob.ckey;

        blood_data["factions"] = faction;
        return blood_data;
    }

    // tg: code/modules/mob/living/blood.dm:509
    public virtual object get_bloodtype()
    {
        if (!Dm.truthy(CAN_HAVE_BLOOD(this)))
            return null;

        if (!Dm.truthy(Dm.bitand(mob_biotypes, MOB_ORGANIC)))
        {
            if (Dm.truthy(Dm.bitand(mob_biotypes, MOB_ROBOTIC)))
            {
                // tg: :516  return get_blood_type(/datum/blood_type/oil)
                // GAP(lead): /datum/blood_type/oil is not declared (its reagent type is not; synthetic blood, M8).
                Dm.NotTranslated("code/modules/mob/living/blood.dm:516", "get_bloodtype: the oil blood of robotic mobs (M8)");
                return null;
            }
            return null;
        }

        if (Dm.truthy(Dm.bitand(mob_biotypes, Dm.bitor(Dm.bitor(MOB_SLIME, MOB_PLANT), MOB_REPTILE))))
        {
            // tg: :519-524  slime, water (plants) and lizard blood: get_blood_type(/datum/blood_type/slime | water | lizard)
            // GAP(lead): those blood types are not declared (their reagent types are not; other species, M8-M9).
            Dm.NotTranslated("code/modules/mob/living/blood.dm:519", "get_bloodtype: slime, plant and lizard blood (other species, M10+)");
            return null;
        }
        if (Dm.truthy(Dm.bitand(mob_biotypes, MOB_HUMANOID)))
        {
            // O+ as to avoid mobs bleeding all human bloodtypes under the sun, and its statistically the most common one
            return get_blood_type(typeof(datum_blood_type_human_o_plus));
        }

        return get_blood_type(typeof(datum_blood_type_animal));
    }

    /// Returns the reagent type this mob has for blood
    // tg: code/modules/mob/living/blood.dm:532
    public virtual object get_blood_reagent()
    {
        if (!Dm.truthy(can_bleed()))
            return null;

        datum_blood_type blood_type = get_bloodtype() as datum_blood_type;
        return blood_type?.reagent_type;
    }

    /// Check if a mob can bleed, and possibly if they're capable of leaving decals on turfs/mobs/items
    // tg: code/modules/mob/living/blood.dm:540
    public virtual object can_bleed(float? bleed_flag = null)
    {
        bleed_flag ??= NONE;
        if (!Dm.truthy(CAN_HAVE_BLOOD(this)))
            return BLEED_NONE;

        if (!Dm.truthy(bleed_flag))
            return BLEED_SPLATTER;

        datum_blood_type blood_type = get_bloodtype() as datum_blood_type;
        if (!Dm.truthy(blood_type))
            return BLEED_NONE;

        if (Dm.truthy(Dm.bitand(blood_type.blood_flags, bleed_flag)))
            return BLEED_SPLATTER;

        if (Dm.truthy(Dm.bitand(blood_type.blood_flags, BLOOD_ADD_DNA)))
            return BLEED_ADD_DNA;

        return BLEED_NONE;
    }

    // tg: code/modules/mob/living/blood.dm:619
    public virtual object get_blood_compatibility(mob_living donor = null)
    {
        if (!Dm.eq(get_blood_reagent(), donor.get_blood_reagent()))
            return FALSE;

        datum_blood_type patient_blood_data = get_bloodtype() as datum_blood_type;
        datum_blood_type donor_blood_data = donor.get_bloodtype() as datum_blood_type;
        return Dm.@in(donor_blood_data?.type_key(), patient_blood_data?.compatible_types) ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/blood.dm:628
    public virtual async DmTask create_splatter(object splatter_dir = null)
    {
        splatter_dir ??= pick(GLOB.cardinals);
        // Check for TRAIT_NOBLOOD
        if (!Dm.truthy(can_bleed())) // Even if we can't cover turfs, we still can add DNA to everything our blood hits
            return;
        datum_blood_type blood_type = get_bloodtype() as datum_blood_type;
        obj_effect_temp_visual_dir_setting_bloodsplatter splatter = Dm.@new<obj_effect_temp_visual_dir_setting_bloodsplatter>(get_turf(this), splatter_dir, blood_type == null ? null : await blood_type.get_color());
        splatter.color = blood_type?.color;
    }

    /*
     * Create a splatter or drip of this mob's blood type
     * Arguments:
     * * splatter_turf - If the splatter should be made on a different turf than the mob
     * * small_drip
     */
    // tg: code/modules/mob/living/blood.dm:641
    public virtual async DmTask<object> add_splatter_floor(turf splatter_turf = null, float? small_drip = null)
    {
        small_drip ??= FALSE;
        if (!Dm.truthy(splatter_turf))
            splatter_turf = get_turf(this);

        if (!Dm.truthy(splatter_turf))
            return null;

        // Check for TRAIT_NOBLOOD
        object sw__1 = can_bleed(BLOOD_COVER_TURFS);
        if (Dm.eq(sw__1, BLEED_NONE))
            return null;
        else if (Dm.eq(sw__1, BLEED_ADD_DNA))
        {
            return await splatter_turf.add_mob_blood(this);
        }

        datum_blood_type t__1 = get_bloodtype() as datum_blood_type;
        return (t__1 == null ? null : await t__1.make_blood_splatter(this, splatter_turf, small_drip));
    }

    // tg: code/modules/mob/living/blood.dm:666
    public virtual async DmTask spray_blood(object splatter_direction = null, float? splatter_strength = null)
    {
        splatter_strength ??= 3;
        // Check if we can bleed and if our splatter can even go anywhere
        if (!isturf(loc) || !Dm.eq(can_bleed(BLOOD_COVER_TURFS), BLEED_SPLATTER))
            return;
        obj_effect_decal_cleanable_blood_hitsplatter our_splatter = Dm.@new<obj_effect_decal_cleanable_blood_hitsplatter>(loc, await get_static_viruses(), get_blood_dna_list(), splatter_strength);
        turf targ = get_ranged_target_turf(this, splatter_direction, splatter_strength) as turf;
        our_splatter.fly_towards(targ, splatter_strength.Value);
    }

    // FIXME: This duplicates blood like crazy. The amount you bleed is way less than the splatter.
    // To summarize, you can literally dupe blood and bypass bloodloss with a syringe and a beaker.
    // I'm writing this here because it's out of scope for my PR, but was discovered because of it.
    // tg: code/modules/mob/living/blood.dm:677
    public virtual async DmTask<object> make_blood_trail(turf target_turf = null, turf start = null, object was_facing = null, object movement_direction = null)
    {
        if (!Dm.truthy(has_gravity()) || !isturf(start) || !Dm.truthy(can_bleed()))
            return null;

        object cached_blood_volume = get_blood_volume();

        if (!Dm.truthy(cached_blood_volume))
            return null;

        object base_bleed_rate = get_bleed_rate();
        object base_brute = get_brute_loss();

        float brute_ratio = round(Dm.num(base_brute) / (maxHealth * 4), 0.1f);
        float bleeding_rate = round(Dm.num(base_bleed_rate) / 4, 0.1f);

        // We only leave a trail if we're below a certain blood threshold
        // The more brute damage we have, or the more we're bleeding, the less blood we need to leave a trail
        if (Dm.lt(cached_blood_volume, max(BLOOD_VOLUME_NORMAL * (1 - max(bleeding_rate, brute_ratio)), 0)))
            return null;

        object blood_to_add = BLOOD_AMOUNT_PER_DECAL * 0.1f;

        if (body_position == LYING_DOWN)
        {
            blood_to_add = Dm.add(blood_to_add, await bleed_drag_amount());
            adjust_blood_volume(Dm.neg(blood_to_add));
        }
        else
            blood_to_add = Dm.add(blood_to_add, base_bleed_rate);

        // If we're very damaged or bleeding a lot, add even more blood to the trail
        if (Dm.ge(base_brute, 300) || Dm.ge(base_bleed_rate, 7))
            blood_to_add = Dm.mul(blood_to_add, 2);

        // Checks if we can add visual blood effects on turfs.
        object sw__1 = can_bleed(BLOOD_COVER_TURFS);
        if (Dm.eq(sw__1, BLEED_NONE))
            return null;
        else if (Dm.eq(sw__1, BLEED_ADD_DNA))
        {
            return await start.add_mob_blood(this);
        }

        float trail_dir = REVERSE_DIR(Dm.num(movement_direction));
        // The mob is performing a diagonal movement so we need to make a diagonal trail
        // This is not the same as a diagonal dir. Sorry.
        // This is insteas denoted by a negative direction (so we don't conflict with real dirs)
        if (Dm.@in(movement_direction, GLOB.diagonals))
        {
            trail_dir = -1 * Dm.num(movement_direction);
            // Create a full trail on the tile we came from, and a start of a trail at the one we arrived to
            await create_blood_trail_component(start, trail_dir, Dm.mul(blood_to_add, 0.67f), FALSE);
            await create_blood_trail_component(target_turf, get_dir(start, target_turf), Dm.mul(blood_to_add, 0.33f), TRUE);
            return null;
        }

        bool continuing_trail = false;
        // The mob is going a direction they were not previously facing
        // We now factor in their facing direction to make a trail that looks like they're turning
        // This is done by creating a diagonal dir
        float facing = Dm.num(was_facing);
        if (trail_dir != facing && trail_dir != REVERSE_DIR(facing))
        {
            // Look a step back to see if we should be constructing a diagonal dir
            // If there's no existing trail making a curve would look weird
            foreach (obj_effect_decal_cleanable_blood_trail_holder past_trail in Dm.each<obj_effect_decal_cleanable_blood_trail_holder>(get_step(start, REVERSE_DIR(facing))))
            {
                if (past_trail.get_trail_component(facing, check_reverse: TRUE, check_diagonals: TRUE, check_reverse_diagonals: TRUE) != null)
                {
                    trail_dir = Dm.bitor(trail_dir, facing);
                    continuing_trail = true;
                    // In case we produced an invalid dir: go back on relevant axis
                    if (Dm.eq(Dm.bitand(trail_dir, Dm.bitor(NORTH, SOUTH)), Dm.bitor(NORTH, SOUTH)))
                        trail_dir = Dm.bitand(trail_dir, Dm.bitnot(Dm.bitand(facing, Dm.bitor(NORTH, SOUTH))));
                    if (Dm.eq(Dm.bitand(trail_dir, Dm.bitor(EAST, WEST)), Dm.bitor(EAST, WEST)))
                        trail_dir = Dm.bitand(trail_dir, Dm.bitnot(Dm.bitand(facing, Dm.bitor(EAST, WEST))));
                    break;
                }
            }
        }

        if (continuing_trail || Dm.@in(trail_dir, GLOB.diagonals))
        {
            await create_blood_trail_component(start, trail_dir, Dm.mul(blood_to_add, 0.67f), FALSE);
            await create_blood_trail_component(target_turf, get_dir(start, target_turf), Dm.mul(blood_to_add, 0.33f), TRUE);
            return null;
        }

        // If we're still moving cardinally and didn't change our dir, there's a chance that there's a half-trail on our turf
        // in which case we want to continue it instead of doing a full trail
        // Only scenario in which we don't have one is if we just started the trail from our tile
        foreach (obj_effect_decal_cleanable_blood_trail_holder trail_holder in Dm.each<obj_effect_decal_cleanable_blood_trail_holder>(start))
        {
            obj_effect_decal_cleanable_blood_trail trail = trail_holder.get_trail_component(trail_dir);
            if (Dm.truthy(trail?.half_piece)) // We're moving straight, so just continue the path
            {
                continuing_trail = true;
                break;
            }
        }

        // If we've just started moving, put a half-trail on our previous turf instead of a full one
        await create_blood_trail_component(start, trail_dir, Dm.mul(blood_to_add, 0.67f), !continuing_trail ? TRUE : FALSE);
        await create_blood_trail_component(target_turf, get_dir(start, target_turf), Dm.mul(blood_to_add, 0.33f), TRUE);
        return null;
    }

    // tg: code/modules/mob/living/blood.dm:770
    public virtual async DmTask<object> create_blood_trail_component(turf trail_turf = null, object trail_dir = null, object blood_to_add = null, object half_piece = null)
    {
        obj_effect_decal_cleanable_blood_trail_holder trail = null;
        bool check_reverse = true;
        // Do not check the reverse dir if we're a diagonal corner or a half piece
        if (Dm.num(trail_dir) > 0 && !Dm.@in(trail_dir, GLOB.cardinals) || Dm.truthy(half_piece))
            check_reverse = false;
        // Pick any trail in the turf to add onto
        foreach (obj_effect_decal_cleanable_blood_trail_holder any_trail in Dm.each<obj_effect_decal_cleanable_blood_trail_holder>(trail_turf))
        {
            // If there exists a trail already, we will add onto the trial
            // UNLESS that trail has the same direction component and it is already dried
            //
            // If that is the case we will look for another trail (or create a new one)
            // (this will let fresh blood be laid over very dried blood)
            obj_effect_decal_cleanable_blood_trail any_trail_component = any_trail.get_trail_component(Dm.num(trail_dir), check_reverse: check_reverse ? TRUE : FALSE);
            if (any_trail_component == null || !Dm.truthy(any_trail_component.dried))
            {
                trail = any_trail;
                break;
            }
        }

        if (trail != null)
        {
            await trail.adjust_bloodiness(Dm.num(blood_to_add));
            return await trail.add_dir_to_trail(Dm.num(trail_dir), this, Dm.num(blood_to_add), half_piece);
        }

        trail = Dm.@new<obj_effect_decal_cleanable_blood_trail_holder>(trail_turf, await get_static_viruses(), get_blood_dna_list());
        if (QDELETED(trail))
            return null;
        trail.bloodiness = Dm.num(blood_to_add);
        return await trail.add_dir_to_trail(Dm.num(trail_dir), this, Dm.num(blood_to_add), half_piece);
    }

    /// Returns how much blood we're losing from being dragged a tile, from [/mob/living/proc/make_blood_trail]
    // tg: code/modules/mob/living/blood.dm:804
    public virtual async DmTask<object> bleed_drag_amount()
    {
        float brute_ratio = round(Dm.num(get_brute_loss()) / maxHealth, 0.1f);
        return max(1, brute_ratio * 2);
    }

    // tg: code/modules/mob/living/blood.dm:815
    public virtual object get_blood_alcohol_content()
    {
        float blood_alcohol_content = 0;
        datum_status_effect_inebriated inebriation = has_status_effect(typeof(datum_status_effect_inebriated)) as datum_status_effect_inebriated;
        if (inebriation != null)
            blood_alcohol_content = round(inebriation.drunk_value * DRUNK_POWER_TO_BLOOD_ALCOHOL, 0.01f);

        return blood_alcohol_content;
    }

    // tg: code/modules/mob/living/blood.dm:824
    public virtual object get_blood_synth_content()
    {
        if (IS_BLOOD_ALWAYS_SYNTHETIC(this))
            return 1; // Basic mobs, simple mobs, born monkeys and spawned mobs have fully synthetic blood.

        // Handles accounting for synthetic blood injected into mobs that don't normally have it.
        datum_component_synth_blood synth_comp = GetComponent(typeof(datum_component_synth_blood)) as datum_component_synth_blood;
        return synth_comp?.synth_content;
    }
}

// tg: code/modules/mob/living/blood.dm:14
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/blood.dm:14
    public override object can_have_blood()
    {
        return Dm.num(!HAS_TRAIT(this, TRAIT_NOBLOOD));
    }

    // tg: code/modules/mob/living/blood.dm:23
    public override object get_blood_volume(float? apply_modifiers = null)
    {
        apply_modifiers ??= FALSE;
        if (!Dm.truthy(CAN_HAVE_BLOOD(this)))
            return 0; // Overriding blood setting code can cause blood_volume to be non-zero even when a mob shouldn't have blood.
        if (!Dm.truthy(apply_modifiers))
            return blood_volume; // Default behavior, returns the real blood volume.
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return default_blood_volume; // Makes TRAIT_GODMODE grant immunity to the effects of bleeding. (oxyloss, passing out, etc.)

        object amount = blood_volume;

        // For simple multipliers, like a blood worm in a mob.
        foreach (object source in Dm.each(blood_volume_modifiers))
            amount = Dm.mul(amount, blood_volume_modifiers[source]);

        // Handled here instead of in the saline reagent datum, because this way the modification order is consistent.
        // E.g. if you have an effect that modifies blood volume over the dilution cap, then saline should do nothing.
        var saline = (reagents as datum_reagents)?.has_reagent(typeof(datum_reagent_medicine_salglu_solution)) as datum_reagent_medicine_salglu_solution;
        if (saline != null && Dm.num(amount) < SALINE_DILUTION_CAP)
        {
            var blood_type = get_bloodtype() as datum_blood_type;
            var dilution = has_status_effect(typeof(datum_status_effect_stacking_saline_glucose_dilution)) as datum_status_effect_stacking_saline_glucose_dilution;
            if (blood_type?.restoration_chem == saline.required_restoration_chem)
                amount = min(Dm.num(amount) + Dm.num(saline.volume) * dilution.get_blood_multiplier(), BLOOD_VOLUME_NORMAL);
        }

        return amount;
    }

    // tg: code/modules/mob/living/blood.dm:144
    public override async DmTask<object> coagulant_effect(float? amount_to_heal = null)
    {
        amount_to_heal ??= 1;
        datum_wound bloodiest_wound = null;
        foreach (datum_wound iter_wound in Dm.each_cast<datum_wound>(all_wounds))
        {
            if (Dm.truthy(iter_wound.blood_flow) && Dm.gt(iter_wound.blood_flow, bloodiest_wound?.blood_flow))
                bloodiest_wound = iter_wound;
        }

        if (bloodiest_wound != null)
            await bloodiest_wound.adjust_blood_flow(-1 * abs(Dm.num(amount_to_heal)));
        return Dm.num(bloodiest_wound != null);
    }

    /// Has each bodypart update its bleed/wound overlay icon states
    // tg: code/modules/mob/living/blood.dm:265
    public virtual async DmTask update_bodypart_bleed_overlays()
    {
        foreach (obj_item_bodypart iter_part in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
            await iter_part.update_part_wound_overlay();
    }

    // tg: code/modules/mob/living/blood.dm:288
    public override object get_bleed_rate()
    {
        if (HAS_TRAIT(this, TRAIT_GODMODE) || !Dm.truthy(can_bleed()))
            return 0;

        object dot = null;
        dot = 0;
        foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
            dot = Dm.add(dot, bodypart.cached_bleed_rate);

        dot = Dm.mul(dot, GET_PHYSIOLOGY(this, PHYS_COEFF_BLEED));
        return dot;
    }

    /**
     * bleed_warn() is used to for carbons with an active client to occasionally receive messages warning them about their bleeding status (if applicable)
     *
     * Arguments:
     * * bleed_rate - When we run this from [/mob/living/carbon/human/proc/handle_blood] we already know how much blood we're losing per second, so we can skip tallying it again with this.
     * * skip_cooldown - Skips caring about the bleed message cooldown.
     */
    // tg: code/modules/mob/living/blood.dm:305
    public virtual void bleed_warn(object bleed_rate = null, float? skip_cooldown = null)
    {
        skip_cooldown ??= FALSE;
        if (!Dm.truthy(CAN_HAVE_BLOOD(this)) || !Dm.truthy(client))
            return;
        // COOLDOWN_FINISHED(src, bleeding_message_cd) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(this.bleeding_message_cd <= world.time) && !Dm.truthy(skip_cooldown))
            return;

        if (!isnum(bleed_rate))
            bleed_rate = get_bleed_rate();

        string bleeding_severity = "";
        float next_cooldown = BLEEDING_MESSAGE_BASE_CD;

        object sw__1 = bleed_rate;
        if (Dm.in_to(sw__1, -INFINITY, 0))
            return;
        else if (Dm.in_to(sw__1, 0, 1))
        {
            bleeding_severity = I18n.T("You feel light trickles of blood across your skin");
            next_cooldown *= 2.5f;
        }
        else if (Dm.in_to(sw__1, 1, 3))
        {
            bleeding_severity = I18n.T("You feel a small stream of blood running across your body");
            next_cooldown *= 2;
        }
        else if (Dm.in_to(sw__1, 3, 5))
        {
            bleeding_severity = I18n.T("You skin feels clammy from the flow of blood leaving your body");
            next_cooldown *= 1.7f;
        }
        else if (Dm.in_to(sw__1, 5, 7))
        {
            bleeding_severity = I18n.T("Your body grows more and more numb as blood streams out");
            next_cooldown *= 1.5f;
        }
        else if (Dm.in_to(sw__1, 7, INFINITY))
            bleeding_severity = I18n.T("Your heartbeat thrashes wildly trying to keep up with your bloodloss");

        string rate_of_change = I18n.T(", but it's getting better."); // if there's no wounds actively getting bloodier or maintaining the same flow, we must be getting better!
        if (HAS_TRAIT(this, TRAIT_COAGULATING)) // if we have coagulant, we're getting better quick
            rate_of_change = I18n.T(", but it's clotting up quickly!");
        else
        {
            // flick through our wounds to see if there are any bleeding ones getting worse or holding flow (maybe move this to handle_blood and cache it so we don't need to cycle through the wounds so much)
            foreach (datum_wound iter_wound in Dm.each_cast<datum_wound>(all_wounds))
            {
                if (!Dm.truthy(iter_wound.blood_flow))
                    continue;
                object iter_wound_roc = iter_wound.get_bleed_rate_of_change();
                if (Dm.eq(iter_wound_roc, BLOOD_FLOW_INCREASING)) // assume the worst, if one wound is getting bloodier, we focus on that
                {
                    rate_of_change = I18n.T(", <b>and it's getting worse!</b>");
                    break;
                }
                else if (Dm.eq(iter_wound_roc, BLOOD_FLOW_STEADY)) // our best case now is that our bleeding isn't getting worse
                    rate_of_change = I18n.T(", and it's holding steady.");
                else if (Dm.eq(iter_wound_roc, BLOOD_FLOW_DECREASING)) // this only matters if none of the wounds fit the above two cases, included here for completeness
                    continue;
            }
        }

        to_chat(this, span_warning(I18n.T("[][]", bleeding_severity, rate_of_change)));
        // COOLDOWN_START(src, bleeding_message_cd, next_cooldown) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        this.bleeding_message_cd = world.time + (next_cooldown);
    }

    // tg: code/modules/mob/living/blood.dm:359
    public override void restore_blood()
    {
        base.restore_blood();
        foreach (obj_item_bodypart bodypart_to_restore in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
            bodypart_to_restore.setBleedStacks(0);
    }

    // tg: code/modules/mob/living/blood.dm:477 (M8 slice C)
    public override async DmTask<object> get_blood_data()
    {
        DmList blood_data = await base.get_blood_data() as DmList;
        if (blood_data == null)
            return null;

        datum_blood_type blood_type = get_bloodtype() as datum_blood_type;
        if (!Dm.truthy(Dm.bitand(blood_type.blood_flags, BLOOD_ADD_DNA)))
            return blood_data;

        // If we haven't suicided but the ghost cannot reenter, i.e. we ghosted, don't set ourselves as cloneable
        mob_dead_observer ghost = get_ghost(TRUE, TRUE) as mob_dead_observer;
        if (!HAS_TRAIT(this, TRAIT_SUICIDED) && (ghost == null || Dm.truthy(ghost.can_reenter_corpse)))
            blood_data["cloneable"] = TRUE;

        if (!Dm.truthy(blood_data["mind"]) && last_mind != null)
            blood_data["mind"] = last_mind;

        if (!Dm.truthy(blood_data["ckey"]) && last_mind != null)
            blood_data["ckey"] = ckey(last_mind.key);

        blood_data["gender"] = gender;
        blood_data["real_name"] = real_name;
        if (dna != null)
        {
            blood_data[BLOOD_DATA_DNA] = dna.unique_enzymes;
            blood_data["features"] = dna.features;
        }

        DmList quirk_types = Dm.list();
        blood_data["quirks"] = quirk_types;
        foreach (datum_quirk quirk in Dm.each_cast<datum_quirk>(quirks))
            quirk_types.Add(quirk.GetType());

        return blood_data;
    }

    // tg: code/modules/mob/living/blood.dm:808
    public override async DmTask<object> bleed_drag_amount()
    {
        float bleed_amount = 0;
        foreach (object i in Dm.each(all_wounds))
        {
            datum_wound iter_wound = (datum_wound)i;
            bleed_amount += Dm.num(await iter_wound.drag_bleed_amount());
        }
        return bleed_amount;
    }
}

// tg: code/modules/mob/living/blood.dm:154
public partial class mob_living_carbon_human : mob_living_carbon
{
    // Takes care blood loss and regeneration
    // tg: code/modules/mob/living/blood.dm:154
    public override async DmTask handle_blood(object seconds_per_tick = null)
    {
        // Under these circumstances blood handling is not necessary
        if (bodytemperature < BLOOD_STOP_TEMP || HAS_TRAIT_NOT_FROM(this, TRAIT_FAKEDEATH, QUIRK_TRAIT))
            return;

        // Run the signal, still allowing mobs with noblood to "handle blood" in their own way
        object sigreturn = (!Dm.truthy(_listen_lookup?[COMSIG_HUMAN_ON_HANDLE_BLOOD]) ? NONE : _SendSignal(COMSIG_HUMAN_ON_HANDLE_BLOOD, Dm.list(this, seconds_per_tick)));
        if ((Dm.truthy(Dm.bitand(sigreturn, HANDLE_BLOOD_HANDLED))) || !Dm.truthy(CAN_HAVE_BLOOD(this)))
            return;

        object heart_blood_multiplier = get_heart_blood_regeneration_multiplier();
        //Blood regeneration if there is some space
        if (Dm.truthy(heart_blood_multiplier) && !Dm.truthy(Dm.bitand(sigreturn, HANDLE_BLOOD_NO_NUTRITION_DRAIN)) && Dm.lt(get_blood_volume(), BLOOD_VOLUME_NORMAL) && !HAS_TRAIT(this, TRAIT_NOHUNGER))
        {
            float nutrition_ratio = round(nutrition / NUTRITION_LEVEL_WELL_FED, 0.2f);

            if (satiety > 80)
                nutrition_ratio *= 1.25f;

            object blood_to_restore = Dm.mul(Dm.mul(Dm.mul(Dm.mul(BLOOD_REGEN_FACTOR, GET_PHYSIOLOGY(this, PHYS_COEFF_BLOOD_REGEN)), heart_blood_multiplier), nutrition_ratio), seconds_per_tick);
            object blood_restored = adjust_blood_volume(blood_to_restore, maximum: BLOOD_VOLUME_NORMAL);
            if (Dm.gt(blood_restored, 0))
                await adjust_nutrition(Dm.mul(Dm.mul(-nutrition_ratio * HUNGER_FACTOR, seconds_per_tick), Dm.div(blood_restored, blood_to_restore)));
        }

        object bleed_rate = get_bleed_rate();

        if (Dm.truthy(bleed_rate))
        {
            await bleed(Dm.mul(bleed_rate, seconds_per_tick));
            bleed_warn(bleed_rate);
        }

        foreach (obj_item_bodypart bodypart in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            if (Dm.truthy(bodypart.generic_bleedstacks))
                bodypart.adjustBleedStacks(-1, 0);
        }

        //Effects of bloodloss
        if (Dm.truthy(Dm.bitand(sigreturn, HANDLE_BLOOD_NO_OXYLOSS)))
            return;

        // Takes into account modifiers like saline-glucose solution in the blood
        object modified_blood_volume = get_blood_volume(apply_modifiers: TRUE);

        // Some effects are halved mid-combat.
        float determined_mod = Dm.truthy(has_status_effect(typeof(datum_status_effect_determined))) ? 0.5f : 1;

        object word = pick("dizzy", "woozy", "faint");
        object sw__1 = modified_blood_volume;
        // Way too much blood!
        if (Dm.in_to(sw__1, BLOOD_VOLUME_EXCESS, BLOOD_VOLUME_MAX_LETHAL))
        {
            if (SPT_PROB(7.5f, seconds_per_tick))
            {
                to_chat(this, span_userdanger(I18n.T("Blood starts to tear your skin apart. You're going to burst!")));
                investigate_log("has been gibbed by having too much blood.", INVESTIGATE_DEATHS);
                inflate_gib();
            }
        }
        // Too much blood
        else if (Dm.in_to(sw__1, BLOOD_VOLUME_MAXIMUM, BLOOD_VOLUME_EXCESS))
        {
            if (SPT_PROB(5, seconds_per_tick))
                to_chat(this, span_warning(I18n.T("You feel terribly bloated.")));
        }
        // Low blood but not a big deal in the immediate
        else if (Dm.in_to(sw__1, BLOOD_VOLUME_OKAY, BLOOD_VOLUME_SAFE))
        {
            if (SPT_PROB(2.5f, seconds_per_tick))
            {
                await set_eye_blur_if_lower(2 * SECONDS * determined_mod);
                if (prob(50))
                {
                    to_chat(this, span_danger(I18n.T("You feel []. It's getting a bit hard to breathe.", word)));
                    losebreath += 0.5f * determined_mod * Dm.num(seconds_per_tick);
                }
                else if (Dm.lt(get_stamina_loss(), 25 * determined_mod))
                {
                    to_chat(this, span_danger(I18n.T("You feel []. It's getting a bit hard to focus.", word)));
                    await adjust_stamina_loss(Dm.mul(5 * determined_mod, seconds_per_tick));
                }
            }
        }
        // Pretty low blood, getting dangerous!
        else if (Dm.in_to(sw__1, BLOOD_VOLUME_RISKY, BLOOD_VOLUME_OKAY))
        {
            if (SPT_PROB(5, seconds_per_tick))
            {
                await set_eye_blur_if_lower(2 * SECONDS * determined_mod);
                await set_dizzy_if_lower(2 * SECONDS * determined_mod);
                if (prob(50))
                {
                    to_chat(this, span_bolddanger(I18n.T("You feel very []. It's getting hard to breathe!", word)));
                    losebreath += 1 * determined_mod;
                }
                else if (Dm.lt(get_stamina_loss(), 40 * determined_mod))
                {
                    to_chat(this, span_bolddanger(I18n.T("You feel very []. It's getting hard to stay awake!", word)));
                    await adjust_stamina_loss(7.5f * determined_mod);
                }
            }
        }
        // Very low blood, danger!!
        else if (Dm.in_to(sw__1, BLOOD_VOLUME_BAD, BLOOD_VOLUME_RISKY))
        {
            if (SPT_PROB(5, seconds_per_tick))
            {
                await set_eye_blur_if_lower(4 * SECONDS * determined_mod);
                await set_dizzy_if_lower(4 * SECONDS * determined_mod);
                if (prob(50))
                {
                    to_chat(this, span_userdanger(I18n.T("You feel extremely []! It's getting very hard to breathe!", word)));
                    losebreath += 1.5f * determined_mod;
                }
                else if (Dm.lt(get_stamina_loss(), 80 * determined_mod))
                {
                    to_chat(this, span_userdanger(I18n.T("You feel extremely []! It's getting very hard to stay awake!", word)));
                    await adjust_stamina_loss(10 * determined_mod);
                }
            }
        }
        // Critically low blood, death is near! Adrenaline won't help you here.
        else if (Dm.in_to(sw__1, BLOOD_VOLUME_SURVIVE, BLOOD_VOLUME_BAD))
        {
            if (SPT_PROB(7.5f, seconds_per_tick))
            {
                await Unconscious(rand(1 * SECONDS, 2 * SECONDS));
                to_chat(this, span_userdanger(I18n.T("You black out for a moment!")));
            }
        }
        // Instantly die upon this threshold
        else if (Dm.in_to(sw__1, -INFINITY, BLOOD_VOLUME_SURVIVE))
        {
            if (!HAS_TRAIT(this, TRAIT_NODEATH))
            {
                investigate_log("has died of bloodloss.", INVESTIGATE_DEATHS);
                await death();
            }
        }

        // Blood ratio! if you have 280 blood, this equals 0.5 as that's half of the current value, 560.
        object effective_blood_ratio = Dm.div(modified_blood_volume, BLOOD_VOLUME_NORMAL);
        object target_oxyloss = max(Dm.mul(Dm.sub(1, effective_blood_ratio), 100), 0);

        // If your ratio is less than one (you're missing any blood) and your oxyloss is under missing blood %, start getting oxy damage.
        // This damage accrues faster the less blood you have.
        // If the damage surpasses the KO threshold for oxyloss, then we'll always tick up so you die eventually
        if (Dm.gt(target_oxyloss, 0) && (Dm.lt(get_oxy_loss(), target_oxyloss) || (Dm.ge(target_oxyloss, OXYLOSS_PASSOUT_THRESHOLD) && IS_UNCONSCIOUS(this))))
        {
            // At roughly half blood this equals to 3 oxyloss per tick. At 90% blood it's close to 0.5
            object rounded_oxyloss = Dm.mul(round(0.01f * Dm.num(Dm.sub(BLOOD_VOLUME_NORMAL, modified_blood_volume)), 0.25f), seconds_per_tick);
            await adjust_oxy_loss(rounded_oxyloss, updating_health: TRUE);
        }
    }

    // tg: code/modules/mob/living/blood.dm:281
    public override async DmTask bleed(object amount = null)
    {
        await base.bleed(amount);
    }

    // tg: code/modules/mob/living/blood.dm:798
    [DmParams("target_turf", "start", "direction")]
    public override async DmTask<object> make_blood_trail(turf target_turf = null, turf start = null, object was_facing = null, object movement_direction = null)
    {
        if (!Dm.truthy(is_bleeding()))
            return null;
        return await base.make_blood_trail(target_turf, start, was_facing, movement_direction);
    }
}

// tg: code/modules/mob/living/blood.dm:560 (global procs)
public static partial class GlobalProcs
{
    /// Returns the blood_type datum that corresponds to the string id key in GLOB.blood_types
    // tg: code/modules/mob/living/blood.dm:560
    public static object get_blood_type(object id = null)
    {
        if (ispath(id, typeof(datum_blood_type)))
        {
            Type bloodtype_type = id as Type;
            return GLOB.blood_types[Dm.initial(bloodtype_type, (datum_blood_type p) => p.name)];
        }

        return GLOB.blood_types[id];
    }

    // tg: code/modules/mob/living/blood.dm:569
    public static async DmTask<object> get_color_from_blood_list(DmList blood_DNA = null)
    {
        // M2 integration: a bodypart's brute damage overlay takes its colour from it (_bodyparts.dm:1352), every damaged human.
        datum_blood_type blood_type = null;
        if (!Dm.truthy(length(blood_DNA)))
            return await ((datum_blood_type)get_blood_type(typeof(datum_blood_type_human_o_plus))).get_color();
        if (length(blood_DNA) == 1) // Microop for when we don't need to do color mixing
        {
            blood_type = blood_DNA[blood_DNA[length(blood_DNA)]] as datum_blood_type;
            return await blood_type.get_color();
        }

        float r_color = 0;
        float g_color = 0;
        float b_color = 0;
        float valid_colors = 0;
        foreach (object blood_key in Dm.each(blood_DNA))
        {
            blood_type = blood_DNA[blood_key] as datum_blood_type;
            if (!(blood_type is datum_blood_type))
                continue;

            DmList rgb_blood = rgb2num((string)await blood_type.get_color());
            r_color += Dm.num(rgb_blood[1]);
            g_color += Dm.num(rgb_blood[2]);
            b_color += Dm.num(rgb_blood[3]);
            valid_colors += 1;
        }

        if (valid_colors == 0)
            return await ((datum_blood_type)get_blood_type(typeof(datum_blood_type_human_o_plus))).get_color();

        r_color = Dm.div(r_color, valid_colors);
        g_color = Dm.div(g_color, valid_colors);
        b_color = Dm.div(b_color, valid_colors);
        return rgb(r_color, g_color, b_color);
    }

    // tg: code/modules/mob/living/blood.dm:601
    public static object has_blood_flag(object blood_DNA = null, object blood_flags = null)
    {
        if ((blood_DNA == null))
            return FALSE;

        if (!islist(blood_DNA))
        {
            datum_blood_type single_type = blood_DNA as datum_blood_type;
            blood_DNA = Dm.list(Dm.kv(single_type.dna_string, single_type));
        }

        object matches = NONE;
        DmList blood_list = (DmList)blood_DNA;
        foreach (object blood_key in Dm.each(blood_list))
        {
            datum_blood_type blood_type = blood_list[blood_key] as datum_blood_type;
            matches = Dm.bitor(matches, Dm.bitand(blood_type.blood_flags, blood_flags));
        }
        return matches;
    }
}
