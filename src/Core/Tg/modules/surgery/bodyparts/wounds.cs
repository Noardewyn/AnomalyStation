// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/wounds.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 2, the wound engine: rolling, applying and promoting wounds, and the gauze helpers that the
// wounds read; applying the gauze itself is slice 6).

// tg: code/modules/surgery/bodyparts/wounds.dm:2
public partial class obj_item_bodypart : obj_item
{
    /// Allows us to roll for and apply a wound without actually dealing damage. Used for aggregate wounding power with pellet clouds
    // tg: code/modules/surgery/bodyparts/wounds.dm:2
    [ShouldCallParent]
    public virtual async DmTask<object> painless_wound_roll(object wounding_type = null, object wounding_dmg = null, object wound_bonus = null, object exposed_wound_bonus = null, float? sharpness = null, object wound_clothing = null)
    {
        sharpness ??= NONE;

        if (owner == null || Dm.num(wounding_dmg) <= WOUND_MINIMUM_DAMAGE || Dm.eq(wound_bonus, CANT_WOUND) || HAS_TRAIT(owner, TRAIT_GODMODE))
            return null;

        bool easy_dismember = HAS_TRAIT(owner, TRAIT_EASYDISMEMBER); // if we have easydismember, we don't reduce damage when redirecting damage to different types (slashing weapons on mangled/skinless limbs attack at 100% instead of 50%)

        float has_exterior = Dm.bitand(bio_status, ANATOMY_EXTERIOR);
        float has_interior = Dm.bitand(bio_status, ANATOMY_INTERIOR);

        bool exterior_ready_to_dismember = (!Dm.truthy(has_exterior) || Dm.truthy(Dm.bitand(mangled_state, BODYPART_MANGLED_EXTERIOR)));
        bool interior_ready_to_dismember = (!Dm.truthy(has_interior) || Dm.truthy(Dm.bitand(mangled_state, BODYPART_MANGLED_INTERIOR)));

        // if we're bone only, all cutting attacks go straight to the bone
        if (!Dm.truthy(has_exterior) && Dm.truthy(has_interior))
        {
            if (Dm.eq(wounding_type, WOUND_SLASH))
            {
                wounding_type = WOUND_BLUNT;
                wounding_dmg = Dm.num(wounding_dmg) * (easy_dismember ? 1 : 0.6f);
            }
            else if (Dm.eq(wounding_type, WOUND_PIERCE))
            {
                wounding_type = WOUND_BLUNT;
                wounding_dmg = Dm.num(wounding_dmg) * (easy_dismember ? 1 : 0.75f);
            }
        }
        else
        {
            // if we've already mangled the skin (critical slash or piercing wound), then the bone is exposed, and we can damage it with sharp weapons at a reduced rate
            // So a big sharp weapon is still all you need to destroy a limb
            if (Dm.truthy(has_interior) && exterior_ready_to_dismember && !Dm.truthy(Dm.bitand(mangled_state, BODYPART_MANGLED_INTERIOR)) && Dm.truthy(sharpness))
            {
                if (Dm.eq(wounding_type, WOUND_SLASH) && !easy_dismember)
                    wounding_dmg = Dm.num(wounding_dmg) * 0.6f; // edged weapons pass along 60% of their wounding damage to the bone since the power is spread out over a larger area
                if (Dm.eq(wounding_type, WOUND_PIERCE) && !easy_dismember)
                    wounding_dmg = Dm.num(wounding_dmg) * 0.75f; // piercing weapons pass along 75% of their wounding damage to the bone since it's more concentrated
                wounding_type = WOUND_BLUNT;
            }
            // tg passes mangled_state to dismemberable_by_total_damage(), which takes no arguments (_bodyparts.dm:839); BYOND ignores it
            if (((exterior_ready_to_dismember && interior_ready_to_dismember) || Dm.truthy(dismemberable_by_total_damage())) && Dm.truthy(await try_dismember(wounding_type, wounding_dmg, wound_bonus, exposed_wound_bonus)))
                return null;
        }
        return await check_wounding(wounding_type, wounding_dmg, wound_bonus, exposed_wound_bonus, wound_clothing);
    }

    /**
     * check_wounding() is where we handle rolling for, selecting, and applying a wound if we meet the criteria
     *
     * We generate a "score" for how woundable the attack was based on the damage and other factors discussed in [/obj/item/bodypart/proc/check_woundings_mods], then go down the list from most severe to least severe wounds in that category.
     * We can promote a wound from a lesser to a higher severity this way, but we give up if we have a wound of the given type and fail to roll a higher severity, so no sidegrades/downgrades
     *
     * Arguments:
     * * woundtype- Either WOUND_BLUNT, WOUND_SLASH, WOUND_PIERCE, or WOUND_BURN based on the attack type.
     * * damage- How much damage is tied to this attack, since wounding potential scales with damage in an attack (see: WOUND_DAMAGE_EXPONENT)
     * * wound_bonus- The wound_bonus of an attack
     * * exposed_wound_bonus- The exposed_wound_bonus of an attack
     * * wound_clothing- If this should damage clothing.
     */
    // tg: code/modules/surgery/bodyparts/wounds.dm:50
    [ShouldCallParent]
    public virtual async DmTask<object> check_wounding(object woundtype = null, object damage = null, object wound_bonus = null, object exposed_wound_bonus = null, object attack_direction = null, object damage_source = null, object wound_clothing = null)
    {
        if (!Dm.truthy(is_woundable()))
            return null;

        // note that these are fed into an exponent, so these are magnified
        bool easily_wounded = HAS_TRAIT(owner, TRAIT_EASILY_WOUNDED);
        bool hardly_wounded = HAS_TRAIT(owner, TRAIT_HARDLY_WOUNDED);
        float considered_damage_cap = WOUND_MAX_CONSIDERED_DAMAGE;
        if (easily_wounded && !hardly_wounded)
            considered_damage_cap *= 1.5f;
        else if (hardly_wounded && !easily_wounded)
            considered_damage_cap /= 2;

        if (HAS_TRAIT(owner, TRAIT_BLOODY_MESS))
        {
            if (easily_wounded && !hardly_wounded) //Super sucks to be you.
                damage = Dm.num(damage) * 1.5f;
            else if (hardly_wounded && !easily_wounded)
                damage = Dm.num(damage) * 0.5f;
        }
        else
            damage = min(Dm.num(damage), considered_damage_cap);

        if (HAS_TRAIT(owner, TRAIT_EASYDISMEMBER) && !HAS_TRAIT(owner, TRAIT_NODISMEMBER))
            damage = Dm.num(damage) * 1.1f;

        if (HAS_TRAIT(owner, TRAIT_EASYBLEED) && Dm.truthy(owner.can_bleed()) && ((Dm.eq(woundtype, WOUND_PIERCE)) || (Dm.eq(woundtype, WOUND_SLASH))))
            damage = Dm.num(damage) * 1.5f;

        float base_roll = rand(1, round(Dm.pow(Dm.num(damage), WOUND_DAMAGE_EXPONENT)));
        object injury_roll = base_roll;
        injury_roll = await check_woundings_mods(woundtype, injury_roll, damage, wound_bonus, exposed_wound_bonus, wound_clothing);
        DmList series_wounding_mods = check_series_wounding_mods() as DmList;

        if (Dm.num(injury_roll) > WOUND_DISMEMBER_OUTRIGHT_THRESH && prob(Dm.num(get_damage()) / max_damage * 100) && Dm.truthy(can_dismember__proc()))
        {
            datum_wound_loss dismembering = await Dm.new_async<datum_wound_loss>();
            await dismembering.apply_dismember(this, Dm.text("[]", woundtype), outright: TRUE, attack_direction: attack_direction);
            return null;
        }

        DmList possible_wounds = Dm.list();
        foreach (Type wound_type in Dm.each_cast<Type>(GLOB.all_wound_pregen_data))
        {
            datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[wound_type];
            if (!Dm.truthy(pregen_data.compete_for_wounding))
                continue;

            float specific_injury_roll = (Dm.num(injury_roll) + Dm.num(series_wounding_mods[pregen_data.wound_series]));
            if (Dm.num(pregen_data.get_threshold_for(this, attack_direction, damage_source)) > specific_injury_roll)
                continue;

            if (Dm.truthy(pregen_data.can_be_applied_to(this, woundtype, random_roll: TRUE)))
                possible_wounds[wound_type] = pregen_data.get_weight(this, woundtype, damage, attack_direction, damage_source);
        }

        foreach (Type wound_type in Dm.each_cast<Type>(possible_wounds))
        {
            datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[wound_type];
            foreach (Type other_path in Dm.each_cast<Type>(Dm.sub(possible_wounds, wound_type)))
            {
                if (Dm.eq(pregen_data.competition_mode, WOUND_COMPETITION_OVERPOWER_LESSERS))
                {
                    if (Dm.initial(wound_type, (datum_wound p) => p.severity) > Dm.initial(other_path, (datum_wound p) => p.severity))
                        Dm.remove(possible_wounds, other_path);
                }
                else if (Dm.eq(pregen_data.competition_mode, WOUND_COMPETITION_OVERPOWER_GREATERS))
                {
                    if (Dm.initial(wound_type, (datum_wound p) => p.severity) < Dm.initial(other_path, (datum_wound p) => p.severity))
                        Dm.remove(possible_wounds, other_path);
                }
            }
        }

        if (length(possible_wounds) == 0)
            return null;

        Type possible_wound = pick_weight(possible_wounds) as Type;
        // DEVIATION(DV186): every candidate weighs 0 (a cranial fissure alone, whose weight is 0 above hard crit): tg's pick_weight
        // gives null and `new null` runtimes, losing the attack's damage; no wound instead
        if (possible_wound == null)
            return null;
        datum_wound_pregen_data possible_pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[possible_wound];
        datum_wound replaced_wound = null;
        foreach (datum_wound existing_wound in Dm.each_cast<datum_wound>(wounds))
        {
            datum_wound_pregen_data existing_pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[existing_wound.type];
            if (Dm.eq(existing_pregen_data.wound_series, possible_pregen_data.wound_series))
            {
                if (existing_wound.severity >= Dm.initial(possible_wound, (datum_wound p) => p.severity))
                    continue;
                replaced_wound = existing_wound;
            }
        }

        datum_wound new_wound = (datum_wound)await Dm.new_async(possible_wound);
        if (replaced_wound != null)
            new_wound = (datum_wound)await replaced_wound.replace_wound(new_wound, attack_direction: attack_direction);
        else
            await new_wound.apply_wound(this, attack_direction: attack_direction, wound_source: damage_source);
        log_wound(owner, new_wound, damage, wound_bonus, exposed_wound_bonus, base_roll); // dismembering wounds are logged in the apply_wound() for loss wounds since they delete themselves immediately, these will be immediately returned
        return new_wound;
    }

    // try forcing a specific wound, but only if there isn't already a wound of that severity or greater for that type on this bodypart
    // tg: code/modules/surgery/bodyparts/wounds.dm:136
    public virtual async DmTask<object> force_wound_upwards(Type potential_wound = null, float? smited = null, object wound_source = null)
    {
        smited ??= FALSE;

        if (potential_wound == null)
            return null;

        datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[potential_wound];
        foreach (datum_wound existing_wound in Dm.each_cast<datum_wound>(wounds))
        {
            datum_wound_pregen_data existing_pregen_data = (datum_wound_pregen_data)existing_wound.get_pregen_data();
            if (Dm.eq(existing_pregen_data.wound_series, pregen_data.wound_series))
            {
                if (existing_wound.severity < Dm.initial(potential_wound, (datum_wound p) => p.severity)) // we only try if the existing one is inferior to the one we're trying to force
                    await existing_wound.replace_wound((datum_wound)await Dm.new_async(potential_wound), smited);
                return null;
            }
        }

        datum_wound new_wound = (datum_wound)await Dm.new_async(potential_wound);
        await new_wound.apply_wound(this, smited: smited, wound_source: wound_source);
        return new_wound;
    }

    /**
     * A simple proc that gets the best wound to fit the criteria laid out, then returns its wound threshold.
     *
     * Args:
     * * wounding_type: The wounding_type, e.g. WOUND_BLUNT, WOUND_SLASH to force onto the mob. Can be a list of wounding_types.
     * * severity: The severity that will be considered.
     * * return_value_if_no_wound: If no wound is found, we will return this instead. (It is reccomended to use named args for this one, as its unclear what it is without)
     * * wound_source: The theoretical source of the wound. Nullable.
     *
     * Returns:
     * return_value_if_no_wound if no wound is found - if one IS found, the wound threshold for that wound.
     */
    // tg: code/modules/surgery/bodyparts/wounds.dm:200
    public virtual object get_wound_threshold_of_wound_type(object wounding_type = null, object severity = null, object return_value_if_no_wound = null, object wound_source = null)
    {
        Type wound_path = get_corresponding_wound_type(wounding_type, this, severity, duplicates_allowed: TRUE, care_about_existing_wounds: FALSE);
        if (wound_path != null)
        {
            datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[wound_path];
            return pregen_data.get_threshold_for(this, damage_source: wound_source);
        }

        return return_value_if_no_wound;
    }

    /**
     * check_wounding_mods() is where we handle the various modifiers of a wound roll
     *
     * A short list of things we consider: any armor a human target may be wearing, and if they have no wound armor on the limb, and add the plain wound_bonus if there is any value to add
     * We also flick through all of the wounds we currently have on this limb and add their threshold penalties, so that having lots of bad wounds makes you more liable to get hurt worse
     * We add the inherent wound_resistance variable the bodypart has (heads and chests are slightly harder to wound) as an armor bonus unless the limb is mangled, and a small wound bonus if the limb is already disabled
     * Once we have everything, we then check if we have acquired any armor. If so, reduce our value by the percentage value of that armour. If not, we add our exposed_wound_bonus as a final bonus to our roll.
     *
     * Arguments:
     * * It's the same ones on [/obj/item/bodypart/proc/receive_damage] except injury_roll, which is fed to this proc.
     */
    // tg: code/modules/surgery/bodyparts/wounds.dm:219
    [ShouldCallParent]
    public virtual async DmTask<object> check_woundings_mods(object wounding_type = null, object injury_roll = null, object damage = null, object wound_bonus = null, object exposed_wound_bonus = null, object wound_clothing = null)
    {
        float armor_ablation = 0;
        float injury_mod = Dm.num(injury_roll);

        if (owner != null && ishuman(owner))
        {
            mob_living_carbon_human human_owner = (mob_living_carbon_human)owner;
            DmList clothing = human_owner.get_clothing_on_part(this) as DmList;
            foreach (obj_item_clothing clothes in Dm.each_cast<obj_item_clothing>(clothing))
            {
                // unlike normal armor checks, we tabluate these piece-by-piece manually so we can also pass on appropriate damage the clothing's limbs if necessary
                armor_ablation += Dm.num(clothes.get_armor_rating(WOUND));
                // Should attack also cause damage to the clothes?
                if (Dm.truthy(wound_clothing))
                {
                    if (Dm.eq(wounding_type, WOUND_SLASH))
                        await clothes.take_damage_zone(body_zone, damage, BRUTE);
                    else if (Dm.eq(wounding_type, WOUND_BURN))
                        await clothes.take_damage_zone(body_zone, damage, BURN);
                }
            }
        }

        injury_mod += Dm.num(wound_bonus);

        foreach (datum_wound wound in Dm.each_cast<datum_wound>(wounds))
            injury_mod += Dm.num(wound.threshold_penalty);

        if (!Dm.truthy(mangled_state))
            armor_ablation += wound_resistance;

        if (Dm.num(get_damage()) >= max_damage)
            injury_mod += disabled_wound_penalty;

        if (!Dm.truthy(armor_ablation))
            injury_mod += Dm.num(exposed_wound_bonus);
        else
            injury_mod *= ((100 - armor_ablation) / 100);

        return injury_mod;
    }

    /// Should return an assoc list of (wound_series -> penalty). Will be used in determining series-specific penalties for wounding.
    // tg: code/modules/surgery/bodyparts/wounds.dm:257
    public virtual object check_series_wounding_mods()
    {
        DmList series_mods = Dm.list();

        foreach (datum_wound iterated_wound in Dm.each_cast<datum_wound>(wounds))
        {
            datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)GLOB.all_wound_pregen_data[iterated_wound.type];

            series_mods[pregen_data.wound_series] = Dm.add(series_mods[pregen_data.wound_series], iterated_wound.series_threshold_penalty);
        }

        return series_mods;
    }

    /// Get whatever wound of the given type is currently attached to this limb, if any
    // tg: code/modules/surgery/bodyparts/wounds.dm:270
    public virtual object get_wound_type(object checking_type = null)
    {
        if (wounds == null)
            return null;

        foreach (object wound in Dm.each(wounds))
        {
            if (istype(wound, checking_type as Type))
                return wound;
        }
        return null;
    }

    /**
     * update_wounds() is called whenever a wound is gained or lost on this bodypart, as well as if there's a change of some kind on a bone wound possibly changing disabled status
     *
     * Covers tabulating the damage multipliers we have from wounds (burn specifically), as well as deleting our gauze wrapping if we don't have any wounds that can use bandaging
     *  Dismemberment for flesh and bone requires the victim to have the skin on their bodypart destroyed (either a critical cut or piercing wound), and at least a hairline fracture
     * (severe bone), at which point we can start rolling for dismembering. The attack must also deal at least 10 damage, and must be a brute attack of some kind (sorry for now, cakehat, maybe later)
     *
     * Arguments:
     * * replaced- If true, this is being called from the remove_wound() of a wound that's being replaced, so the bandage that already existed is still relevant, but the new wound hasn't been added yet
     */
    // tg: code/modules/surgery/bodyparts/wounds.dm:291
    [ShouldCallParent]
    public virtual void update_wounds(float? replaced = null)
    {
        replaced ??= FALSE;

        wound_damage_multiplier = 1;
        mangled_state = BODYPART_MANGLED_NONE;

        // we can (normally) only have one wound per type, but remember there's multiple types (smites like :B:loodless can generate multiple cuts on a limb)
        foreach (datum_wound iter_wound in Dm.each_cast<datum_wound>(wounds))
        {
            wound_damage_multiplier *= iter_wound.damage_multiplier_penalty;
            if (Dm.truthy(Dm.bitand(iter_wound.wound_flags, MANGLES_INTERIOR)))
                mangled_state = Dm.bitor(mangled_state, BODYPART_MANGLED_INTERIOR);
            if (Dm.truthy(Dm.bitand(iter_wound.wound_flags, MANGLES_EXTERIOR)))
                mangled_state = Dm.bitor(mangled_state, BODYPART_MANGLED_EXTERIOR);
        }

        refresh_bleed_rate();
    }

    /**
     * Get how splinted this bodypart is based on applied items
     *
     * Multiplier applied to maluses, so lower = better
     */
    // tg: code/modules/surgery/bodyparts/wounds.dm:312
    public virtual object get_splint_factor()
    {
        float factor = 1;
        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (current_gauze != null)
            factor *= Dm.num(current_gauze.splint_factor);
        return factor;
    }

    /// Returns TRUE if the limb is splinted with gauze or tape with an effective splint factor
    // tg: code/modules/surgery/bodyparts/wounds.dm:320
    public virtual object is_splinted()
    {
        return Dm.num(get_splint_factor()) < 1 ? TRUE : FALSE;
    }

    /**
     * seep_gauze() is for when a gauze wrapping absorbs blood or pus from wounds, lowering its absorption capacity.
     *
     * The passed amount of seepage is deducted from the bandage's absorption capacity, and if we reach a negative absorption capacity, the bandages falls off and we're left with nothing.
     *
     * Arguments:
     * * seep_amt - How much absorption capacity we're removing from our current bandages (think, how much blood or pus are we soaking up this tick?)
     */
    // tg: code/modules/surgery/bodyparts/wounds.dm:331
    public virtual async DmTask seep_gauze(float? seep_amt = null)
    {
        seep_amt ??= 0;

        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (current_gauze == null)
            return;
        current_gauze.absorption_capacity = Dm.num(current_gauze.absorption_capacity) - Dm.num(seep_amt);
        current_gauze.update_appearance(UPDATE_NAME);
        if (Dm.num(current_gauze.absorption_capacity) > 0)
            return;
        owner.visible_message(
            span_danger(I18n.T("[] on []'s [] falls away in rags.", current_gauze, owner, name)),
            span_warning(I18n.T("[] on your [] falls away in rags.", current_gauze, name)),
            vision_distance: COMBAT_MESSAGE_RANGE);
        await current_gauze.forceMove(drop_location() as atom);
        await owner.update_damage_overlays();
    }

    /**
     * Helper for someone helping to remove our gauze
     */
    // tg: code/modules/surgery/bodyparts/wounds.dm:350
    public virtual async DmTask help_remove_gauze(mob_living helper = null)
    {
        if (helper == null)
            return;
        if (!Dm.truthy(await helper.can_perform_action(owner, Dm.bitor(NEED_HANDS, FORBID_TELEKINESIS_REACH)))) // telekinetic removal can be added later
            return;

        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        // ASSERT(!isnull(current_gauze)) (__DEFINES/assert.dm:4, rules 9.5)
        if (current_gauze == null)
            throw Dm.CRASH("assertion failed: !isnull(current_gauze)", "code/modules/surgery/bodyparts/wounds.dm", 357);

        helper.visible_message(
            span_notice(I18n.T("[] starts carefully removing [] from [] [].", helper, current_gauze, helper == owner ? helper.p_their() : I18n.T("[]'s", owner), plaintext_zone)),
            span_notice(I18n.T("You start carefully removing [] from [] []...", current_gauze, helper == owner ? I18n.T("your") : I18n.T("[]'s", owner), plaintext_zone)),
            vision_distance: COMBAT_MESSAGE_RANGE);
        helper.balloon_alert(helper, I18n.T("removing gauze..."));
        if (helper != owner)
            helper.balloon_alert(owner, I18n.T("removing your gauze..."));

        if (!Dm.truthy(await do_after(helper, 3 * SECONDS, owner)))
            return;
        if (QDELETED(current_gauze) || current_gauze.loc != this)
            return;

        helper.visible_message(
            span_notice(I18n.T("[] finishes removing [] from [] [].", helper, current_gauze, helper == owner ? helper.p_their() : I18n.T("[]'s", owner), plaintext_zone)),
            span_notice(I18n.T("You finish removing [] from [] [].", current_gauze, helper == owner ? I18n.T("your") : I18n.T("[]'s", owner), plaintext_zone)),
            vision_distance: COMBAT_MESSAGE_RANGE);

        helper.balloon_alert(helper, I18n.T("gauze removed"));
        if (helper != owner)
            helper.balloon_alert(owner, I18n.T("gauze removed"));

        await helper.put_in_hands(current_gauze);
    }
}

// tg: code/modules/surgery/bodyparts/wounds.dm:170
public partial class mob_living_carbon : mob_living
{
    /**
     *  A simple proc to force a type of wound onto this mob. If you just want to force a specific mainline (fractures, bleeding, etc.) wound, you only need to care about the first 3 args.
     *
     * Args:
     * * wounding_type: The wounding_type, e.g. WOUND_BLUNT, WOUND_SLASH to force onto the mob. Can be a list.
     * * obj/item/bodypart/limb: The limb we wil be applying the wound to. If null, a random bodypart will be picked.
     * * min_severity: The minimum severity that will be considered.
     * * max_severity: The maximum severity that will be considered.
     * * severity_pick_mode: The "pick mode" to be used. See get_corresponding_wound_type's documentation
     * * wound_source: The source of the wound to be applied. Nullable.
     *
     * For the rest of the args, refer to get_corresponding_wound_type().
     *
     * Returns:
     * A new wound instance if the application was successful, null otherwise.
    */
    // tg: code/modules/surgery/bodyparts/wounds.dm:170
    public virtual async DmTask<object> cause_wound_of_type_and_severity(object wounding_type = null, obj_item_bodypart limb = null, object min_severity = null, object max_severity = null, float? severity_pick_mode = null, object wound_source = null)
    {
        max_severity ??= min_severity;
        severity_pick_mode ??= WOUND_PICK_HIGHEST_SEVERITY;

        if (limb == null)
            limb = pick(bodyparts) as obj_item_bodypart;

        Type corresponding_typepath = get_corresponding_wound_type(wounding_type, limb, min_severity, max_severity, severity_pick_mode);
        if (corresponding_typepath != null)
            return await limb.force_wound_upwards(corresponding_typepath, wound_source: wound_source);
        return null;
    }

    /// Limb is nullable, but picks a random one. Defers to limb.get_wound_threshold_of_wound_type, see it for documentation.
    // tg: code/modules/surgery/bodyparts/wounds.dm:179
    public virtual object get_wound_threshold_of_wound_type(object wounding_type = null, object severity = null, object @default = null, obj_item_bodypart limb = null, object wound_source = null)
    {
        if (limb == null)
            limb = pick(bodyparts) as obj_item_bodypart;

        if (limb == null)
            return @default;

        return limb.get_wound_threshold_of_wound_type(wounding_type, severity, @default, wound_source);
    }
}
