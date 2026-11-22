// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/clothing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the type's vars and var overrides, Initialize, item_interaction, dropped, equipped, update_greyscale,
// update_clothes_damaged_state, get_blood_overlay, examine, examine_tags, examine_descriptor, Topic (the armour readout, X3) and the visor (adjust_visor, visor_toggling, can_use: M4c slice 1, the breath mask's internals) and the clothing trait helpers (attach_clothing_traits,
// detach_clothing_traits: M4c slice 2, the magboots) translated (M2 F1: a player's jumpsuit is made, equipped and worn; M4a
// clothing: every piece of clothing is put on, taken off and dropped); M6 slice 2: the zone damage that wounds deal
// (take_damage_zone, disable_zone, bristle), repair with cloth, breaking and shredding (atom_break, atom_destruction,
// spawn_shreds); M6 slice 7: the damaged item's own picture (update_overlays: an /icon Blend, drawn by the client). Gaps CL1
// (D333): the voice box toggle action, the moth snack (attack, create_moth_snack and /obj/item/food/clothing), Destroy,
// machine_wash (the fresh laundry) and the fantasy bonuses. generate_female_clothing (:545) waits for the female cut of
// uniforms (wear_female_version is deferred, DV-F1a): not-translated.md 209. [DmPath("/obj/item/clothing")] is on the
// skeleton part in __HELPERS/pronouns.cs, the earliest .dme file that declares the type (rules 2.3).

// tg: code/modules/clothing/clothing.dm:1
public partial class obj_item_clothing : obj_item
{
    // tg: code/modules/clothing/clothing.dm:2-6
    public obj_item_clothing()
    {
        name = "clothing";
        abstract_type = typeof(obj_item_clothing);
        resistance_flags = FLAMMABLE;
        max_integrity = 200;
        integrity_failure = 0.4f;
    }

    // tg: code/modules/clothing/clothing.dm:7
    public float damaged_clothes = CLOTHING_PRISTINE; //similar to machine's BROKEN stat and structure's broken var

    ///What level of bright light protection item has.
    // tg: code/modules/clothing/clothing.dm:10
    public float flash_protect = FLASH_PROTECTION_NONE;
    // tg: code/modules/clothing/clothing.dm:11
    public float tint = 0; //Sets the item's level of visual impairment tint, normally set to the same as flash_protect
    // tg: code/modules/clothing/clothing.dm:12
    public float up = FALSE; //but separated to allow items to protect but not impair vision, like space helmets
    // tg: code/modules/clothing/clothing.dm:13
    public float visor_flags = NONE; //flags that are added/removed when an item is adjusted up/down
    // tg: code/modules/clothing/clothing.dm:14
    public float visor_flags_inv = NONE; //same as visor_flags, but for flags_inv
    // tg: code/modules/clothing/clothing.dm:15
    public float visor_flags_cover = NONE; //same as above, but for flags_cover
    ///What to toggle when toggled with adjust_visor()
    // tg: code/modules/clothing/clothing.dm:17
    public float visor_vars_to_toggle = Dm.bitor(Dm.bitor(VISOR_FLASHPROTECT, VISOR_TINT), VISOR_INVISVIEW);
    ///Sound this item makes when its visor is flipped down
    // tg: code/modules/clothing/clothing.dm:19
    public object visor_toggle_down_sound = null;
    ///Sound this item makes when its visor is flipped up
    // tg: code/modules/clothing/clothing.dm:21
    public object visor_toggle_up_sound = null;
    ///chat message when the visor is toggled down.
    // tg: code/modules/clothing/clothing.dm:23
    public object toggle_message;
    ///chat message when the visor is toggled up.
    // tg: code/modules/clothing/clothing.dm:25
    public object alt_toggle_message;
    /// What level of emp protection item has
    // tg: code/modules/clothing/clothing.dm:27
    public float emp_protection = EMP_PROTECTION_NONE;

    // tg: code/modules/clothing/clothing.dm:29
    public float clothing_flags = NONE;
    ///List of items that can be equipped in the suit storage slot while we're worn.
    // tg: code/modules/clothing/clothing.dm:31
    public DmList allowed;

    // tg: code/modules/clothing/clothing.dm:33
    public float can_be_bloody = TRUE;

    ///Prevents the article of clothing from gaining the mood boost from washing. Used for the tacticool turtleneck.
    // tg: code/modules/clothing/clothing.dm:36
    public float stubborn_stains = FALSE;

    /// What items can be consumed to repair this clothing (must by an /obj/item/stack)
    // tg: code/modules/clothing/clothing.dm:39
    public Type repairable_by = typeof(obj_item_stack_sheet_cloth);

    //Var modification - PLEASE be careful with this I know who you are and where you live
    // tg: code/modules/clothing/clothing.dm:42
    public DmList user_vars_to_edit; //VARNAME = VARVALUE eg: "name" = "butts"
    // tg: code/modules/clothing/clothing.dm:43
    public DmList user_vars_remembered; //Auto built by the above + dropped() + equipped()

    /// Trait modification, lazylist of traits to add/take away, on equipment/drop in the correct slot
    // tg: code/modules/clothing/clothing.dm:46
    public DmList clothing_traits;

    /// How much clothing damage has been dealt to each of the limbs of the clothing, assuming it covers more than one limb
    // tg: code/modules/clothing/clothing.dm:49
    public DmList damage_by_parts;
    /// How much integrity is in a specific limb before that limb is disabled (for use in [/obj/item/clothing/proc/take_damage_zone], and only if we cover multiple zones.) Set to 0 to disable shredding.
    // tg: code/modules/clothing/clothing.dm:51
    public float limb_integrity = 0;
    /// How many zones (body parts, not precise) we have disabled so far, for naming purposes
    // tg: code/modules/clothing/clothing.dm:53
    public object zones_disabled;

    /// A lazily initiated "food" version of the clothing for moths.
    // tg: code/modules/clothing/clothing.dm:63  var/obj/item/food/clothing/moth_snack
    // (typed /obj/item: the snack is an /obj/item/food/clothing, made only by a moth's bite)
    public obj_item moth_snack;

    // Is it freshly laundered
    // tg: code/modules/clothing/clothing.dm:66
    public float is_laundered = FALSE;

    // tg: code/modules/clothing/clothing.dm:68
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        if (Dm.truthy(Dm.bitand(clothing_flags, VOICEBOX_TOGGLABLE)))
            Dm.append(ref actions_types, Dm.list(typeof(datum_action_item_action_toggle_voice_box)));
        if (Dm.truthy(LAZYLEN(clothing_traits)))
            clothing_traits = string_list(clothing_traits) as DmList;
        dot = base.Initialize(mapload, __extra);
        // AddElement(/datum/element/venue_price, FOOD_PRICE_CHEAP): the element is not declared (venues and the economy)
        Dm.Deferred("code/modules/clothing/clothing.dm:74", "Initialize: AddElement(/datum/element/venue_price, FOOD_PRICE_CHEAP): the venue price of clothing (the restaurant venues' robot customers, not ported)");
        if (Dm.truthy(can_be_bloody) && (Dm.truthy(Dm.bitand(body_parts_covered, FEET)) || Dm.truthy(Dm.bitand(flags_inv, HIDESHOES))))
            Waiting.NoSleep(LoadComponent(typeof(datum_component_bloodysoles)));
        // AddElement(/datum/element/attack_equip): the element is not declared (equipping onto another mob by attacking, M4a)
        Dm.Deferred("code/modules/clothing/clothing.dm:77", "Initialize: AddElement(/datum/element/attack_equip): equipping clothing on others by attacking (inventory, M4a)");
        if (!Dm.truthy(icon_state))
            Dm.combine(ref item_flags, ABSTRACT);
        return dot;
    }
}

// tg: code/modules/clothing/clothing.dm:82
public partial class obj_item_food_clothing : obj_item_food
{
    // tg: code/modules/clothing/clothing.dm:97
    public override async DmTask make_edible()
    {
        await base.make_edible();
        await AddComponentFrom(SOURCE_EDIBLE_INNATE, typeof(datum_component_edible), Dm.named("after_eat", Dm.@new<datum_callback>(this, nameof(after_eat))));
    }

    // tg: code/modules/clothing/clothing.dm:101
    public virtual async DmTask after_eat(mob eater = null)
    {
        obj_item_clothing resolved_clothing = clothing.resolve() as obj_item_clothing;
        if (resolved_clothing != null)
            await resolved_clothing.take_damage(MOTH_EATING_CLOTHING_DAMAGE, sound_effect: FALSE, damage_flag: CONSUME);
        else
            qdel(this);
    }
}

// tg: code/modules/clothing/clothing.dm:108
public partial class obj_item_clothing : obj_item
{
    // tg: code/modules/clothing/clothing.dm:108
    [DmParams("target", "user", "modifiers", "attack_modifiers")]
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode) || !HAS_TRAIT(user, TRAIT_CLOTH_EATER) || Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (!Dm.truthy(moth_snack))
            moth_snack = create_moth_snack() as obj_item;
        if (moth_snack == null)
        {
            to_chat(user, span_warning(I18n.T("You can't eat []!", this)));
            return null;
        }
        await moth_snack.attack__proc(target_mob, user, modifiers);
        return null;
    }

    /// Creates a food object in null space which we can eat and imagine we're eating this pair of shoes
    // tg: code/modules/clothing/clothing.dm:118
    public virtual object create_moth_snack()
    {
        obj_item_food_clothing new_moth_snack = Dm.@new<obj_item_food_clothing>();
        new_moth_snack.name = name;
        new_moth_snack.clothing = WEAKREF(this) as datum_weakref;
        return new_moth_snack;
    }

    // tg: code/modules/clothing/clothing.dm:124
    [DmParams("user", "weapon", "modifiers")]
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item weapon = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = null;
        dot = NONE;
        if (!istype(weapon, repairable_by))
            return dot;

        switch (damaged_clothes)
        {
            case CLOTHING_PRISTINE:
                return dot;

            case CLOTHING_DAMAGED:
            {
                obj_item_stack cloth_repair = weapon as obj_item_stack;
                await cloth_repair.use(1);
                await repair(user);
                return ITEM_INTERACT_SUCCESS;
            }

            case CLOTHING_SHREDDED:
            {
                obj_item_stack cloth_repair = weapon as obj_item_stack;
                if (Dm.num(cloth_repair.amount) < 3)
                {
                    to_chat(user, span_warning(I18n.T("You require 3 [] to repair [].", cloth_repair.name, this)));
                    return ITEM_INTERACT_BLOCKING;
                }
                to_chat(user, span_notice(I18n.T("You begin fixing the damage to [] with []...", this, cloth_repair)));
                if (!Dm.truthy(await do_after(user, 6 * SECONDS, this)) || !Dm.truthy(await cloth_repair.use(3)))
                    return ITEM_INTERACT_BLOCKING;
                await repair(user);
                return ITEM_INTERACT_SUCCESS;
            }
        }
        return dot;
    }

    /// Set the clothing's integrity back to 100%, remove all damage to bodyparts, and generally fix it up
    // tg: code/modules/clothing/clothing.dm:151
    public virtual async DmTask repair(mob user = null)
    {
        await update_clothes_damaged_state(CLOTHING_PRISTINE);
        atom_integrity = max_integrity;
        name = Dm.initial(this, (atom p) => p.name); // remove "tattered" or "shredded" if there's a prefix
        body_parts_covered = Dm.initial(this, (obj_item p) => p.body_parts_covered);
        slot_flags = Dm.initial(this, (obj_item p) => p.slot_flags);
        damage_by_parts = null;
        if (user != null)
        {
            UnregisterSignal(user, COMSIG_MOVABLE_MOVED);
            to_chat(user, span_notice(I18n.T("You fix the damage on [].", this)));
        }
        update_appearance();
    }

    /**
     * take_damage_zone() is used for dealing damage to specific bodyparts on a worn piece of clothing, meant to be called from [/obj/item/bodypart/proc/check_woundings_mods]
     *
     * This proc only matters when a bodypart that this clothing is covering is harmed by a direct attack (being on fire or in space need not apply), and only if this clothing covers
     * more than one bodypart to begin with. No point in tracking damage by zone for a hat, and I'm not cruel enough to let you fully break them in a few shots.
     * Also if limb_integrity is 0, then this clothing doesn't have bodypart damage enabled so skip it.
     *
     * Arguments:
     * * def_zone: The bodypart zone in question
     * * damage_amount: Incoming damage
     * * damage_type: BRUTE or BURN
     * * armour_penetration: If the attack had armour_penetration
     */
    // tg: code/modules/clothing/clothing.dm:176
    public virtual async DmTask take_damage_zone(object def_zone = null, object damage_amount = null, object damage_type = null, object armour_penetration = null)
    {
        if (!Dm.truthy(def_zone) || !Dm.truthy(limb_integrity) || Dm.@in(Dm.initial(this, (obj_item p) => p.body_parts_covered), GLOB.bitflags)) // the second check sees if we only cover one bodypart anyway and don't need to bother with this
            return;
        DmList covered_limbs = cover_flags2body_zones(body_parts_covered) as DmList; // what do we actually cover?
        if (!Dm.@in(def_zone, covered_limbs))
            return;

        float damage_dealt = Dm.num(await take_damage(Dm.num(damage_amount) * 0.1f, damage_type as string, armour_penetration, FALSE)) * 10; // only deal 10% of the damage to the general integrity damage, then multiply it by 10 so we know how much to deal to limb
        LAZYINITLIST(ref damage_by_parts);
        damage_by_parts[def_zone] = Dm.num(damage_by_parts[def_zone]) + damage_dealt;
        if (Dm.num(damage_by_parts[def_zone]) > limb_integrity)
            await disable_zone(def_zone, damage_type);
    }

    /**
     * disable_zone() is used to disable a given bodypart's protection on our clothing item, mainly from [/obj/item/clothing/proc/take_damage_zone]
     *
     * This proc disables all protection on the specified bodypart for this piece of clothing: it'll be as if it doesn't cover it at all anymore (because it won't!)
     * If every possible bodypart has been disabled on the clothing, we put it out of commission entirely and mark it as shredded, whereby it will have to be repaired in
     * order to equip it again. Also note we only consider it damaged if there's more than one bodypart disabled.
     *
     * Arguments:
     * * def_zone: The bodypart zone we're disabling
     * * damage_type: Only really relevant for the verb for describing the breaking, and maybe atom_destruction()
     */
    // tg: code/modules/clothing/clothing.dm:200
    public virtual async DmTask disable_zone(object def_zone = null, object damage_type = null)
    {
        DmList covered_limbs = cover_flags2body_zones(body_parts_covered) as DmList;
        if (!Dm.@in(def_zone, covered_limbs))
            return;

        object zone_name;
        string break_verb = (Dm.eq(damage_type, BRUTE) ? I18n.T("torn") : I18n.T("burned"));

        if (iscarbon(loc))
        {
            mob_living_carbon carbon_loc = (mob_living_carbon)loc;
            zone_name = carbon_loc.parse_zone_with_bodypart(def_zone);
            carbon_loc.visible_message(span_danger(I18n.T("The [] on []'s [] is [] away!", zone_name, carbon_loc, name, break_verb)), span_userdanger(I18n.T("The [] on your [] is [] away!", zone_name, name, break_verb)), vision_distance: COMBAT_MESSAGE_RANGE);
            RegisterSignal(carbon_loc, COMSIG_MOVABLE_MOVED, nameof(bristle), @override: TRUE);
        }
        else
            zone_name = parse_zone(def_zone);

        zones_disabled = Dm.num(zones_disabled) + 1;
        if (Dm.truthy(Dm.bitand(clothing_flags, NO_ZONE_DISABLING)))
            body_parts_covered = Dm.num(Dm.bitand(body_parts_covered, Dm.bitnot(body_zone2cover_flags(def_zone))));

        if (body_parts_covered == NONE) // if there are no more parts to break then the whole thing is kaput
        {
            await atom_destruction((Dm.eq(damage_type, BRUTE) ? MELEE : LASER)); // melee/laser is good enough since this only procs from direct attacks anyway and not from fire/bombs
            return;
        }

        float sw__1 = Dm.num(zones_disabled);
        if (sw__1 == 1)
            name = I18n.T("damaged []", Dm.initial(this, (atom p) => p.name));
        else if (sw__1 == 2)
            name = I18n.T("mangy []", Dm.initial(this, (atom p) => p.name));
        else if (3 <= sw__1) // take better care of your shit, dude
            name = I18n.T("tattered []", Dm.initial(this, (atom p) => p.name));

        await update_clothes_damaged_state(CLOTHING_DAMAGED);
        update_appearance();
    }

    // tg: code/modules/clothing/clothing.dm:235
    public override object Destroy(float? force = null)
    {
        user_vars_remembered = null; //Oh god somebody put REFERENCES in here? not to worry, we'll clean it up
        QDEL_NULL(ref moth_snack);
        return base.Destroy(force);
    }

    // tg: code/modules/clothing/clothing.dm:240
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        await base.dropped(user, silent, __extra);
        if (!(user is mob_living))
            return null;
        UnregisterSignal(user, COMSIG_MOVABLE_MOVED);
        foreach (object trait in Dm.each(clothing_traits))
        {
            // REMOVE_CLOTHING_TRAIT(user, trait) (__DEFINES/clothing.dm:41), expanded inline (rules 11.1 (d): it reads src)
            REMOVE_TRAIT(user, trait, Dm.text("[]_[]", CLOTHING_TRAIT, REF(this)));
        }
        if (iscarbon(user) && Dm.truthy(tint))
        {
            mob_living_carbon carbon_user = user as mob_living_carbon;
            await carbon_user.update_tint();
        }
        if (Dm.truthy(LAZYLEN(user_vars_remembered)))
        {
            // for(var/variable in user_vars_remembered) ... user.vars[variable] = ...; user_vars_remembered =
            // initial(user_vars_remembered): vars reflection is not translated (rules 10.7, 13), and equipped never fills
            // the list (its user_vars_to_edit branch is not translated either).
            Dm.NotTranslated("code/modules/clothing/clothing.dm:250", "dropped: user_vars_remembered (vars reflection, rules 13)");
        }
        return null;
    }

    // tg: code/modules/clothing/clothing.dm:257
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!(user is mob_living))
            return;
        if (Dm.truthy(Dm.bitand(slot_flags, slot))) //Was equipped to a valid slot for this item?
        {
            if (iscarbon(user) && Dm.truthy(LAZYLEN(zones_disabled)))
                RegisterSignal(user, COMSIG_MOVABLE_MOVED, nameof(bristle), @override: TRUE);
            foreach (object trait in Dm.each(clothing_traits))
            {
                // ADD_CLOTHING_TRAIT(user, trait) (__DEFINES/traits/_traits.dm), expanded inline (rules 11.1 (d): it reads src)
                ADD_TRAIT(user, trait, Dm.text("[]_[]", CLOTHING_TRAIT, REF(this)));
            }
            if (iscarbon(user) && Dm.truthy(tint))
            {
                mob_living_carbon carbon_user = user as mob_living_carbon;
                await carbon_user.update_tint();
            }
            if (Dm.truthy(LAZYLEN(user_vars_to_edit)))
            {
                // for(var/variable in user_vars_to_edit) ... user.vars[...]: vars reflection is not translated (rules 10.7,
                // 13); no M2 clothing edits its wearer's vars.
                Dm.NotTranslated("code/modules/clothing/clothing.dm:270", "equipped: user_vars_to_edit (vars reflection, rules 13)");
            }
        }
    }

    // If the item is a piece of clothing and is being worn, make sure it updates on the player
    // tg: code/modules/clothing/clothing.dm:276
    public override async DmTask update_greyscale()
    {
        await base.update_greyscale();

        mob_living_carbon_human wearer = loc as mob_living_carbon_human;

        if (!(wearer is mob_living_carbon_human))
            return;

        await wearer.update_clothing(slot_flags);
    }

    /**
     * Inserts a trait (or multiple traits) into the clothing traits list
     *
     * If worn, then we will also give the wearer the trait as if equipped
     *
     * This is so you can add clothing traits without worrying about needing to equip or unequip them to gain effects
     */
    // tg: code/modules/clothing/clothing.dm:293
    public virtual void attach_clothing_traits(object trait_or_traits = null)
    {
        if (!islist(trait_or_traits))
            trait_or_traits = Dm.list(trait_or_traits);

        // Use a temporary list so we don't mutate the cached version
        clothing_traits = LAZYLISTDUPLICATE(clothing_traits);
        LAZYOR(ref clothing_traits, trait_or_traits);
        if (Dm.truthy(clothing_traits)) // because we might be null
            clothing_traits = string_list(clothing_traits) as DmList;
        mob wearer = loc as mob;
        if (wearer is mob && Dm.truthy(Dm.bitand(wearer.get_slot_by_item(this), slot_flags)))
        {
            foreach (object new_trait in Dm.each(trait_or_traits))
            {
                // ADD_CLOTHING_TRAIT(wearer, new_trait) (__DEFINES/traits/_traits.dm), expanded inline (rules 11.1 (d): it reads src)
                ADD_TRAIT(wearer, new_trait, Dm.text("[]_[]", CLOTHING_TRAIT, REF(this)));
            }
        }
    }

    /**
     * Removes a trait (or multiple traits) from the clothing traits list
     *
     * If worn, then we will also remove the trait from the wearer as if unequipped
     *
     * This is so you can add clothing traits without worrying about needing to equip or unequip them to gain effects
     */
    // tg: code/modules/clothing/clothing.dm:314
    public virtual void detach_clothing_traits(object trait_or_traits = null)
    {
        if (!islist(trait_or_traits))
            trait_or_traits = Dm.list(trait_or_traits);

        // Use a temporary list so we don't mutate the cached version
        clothing_traits = LAZYLISTDUPLICATE(clothing_traits);
        LAZYREMOVE(ref clothing_traits, trait_or_traits);
        if (Dm.truthy(clothing_traits)) // because we might be null
            clothing_traits = string_list(clothing_traits) as DmList;
        mob wearer = loc as mob;
        if (wearer is mob)
        {
            foreach (object new_trait in Dm.each(trait_or_traits))
            {
                // REMOVE_CLOTHING_TRAIT(wearer, new_trait) (__DEFINES/clothing.dm:41), expanded inline (rules 11.1 (d): it reads src)
                REMOVE_TRAIT(wearer, new_trait, Dm.text("[]_[]", CLOTHING_TRAIT, REF(this)));
            }
        }
    }

    // tg: code/modules/clothing/clothing.dm:328
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (damaged_clothes == CLOTHING_SHREDDED)
        {
            Dm.append(ref dot, span_warning(I18n.T("<b>[] completely shredded and require[] mending before [] can be worn again!</b>", p_Theyre(), p_s(), p_they())));
            return dot;
        }

        foreach (object zone in Dm.each(damage_by_parts))
        {
            float pct_damage_part = Dm.num(damage_by_parts[zone]) / limb_integrity * 100;
            object zone_name = parse_zone(zone);
            if (pct_damage_part >= 100)
                Dm.append(ref dot, span_warning(I18n.T("<b>The [] is useless and requires mending!</b>", zone_name)));
            else if (pct_damage_part >= 60 && pct_damage_part <= 99)
                Dm.append(ref dot, span_warning(I18n.T("The [] is heavily shredded!", zone_name)));
            else if (pct_damage_part >= 30 && pct_damage_part <= 59)
                Dm.append(ref dot, span_danger(I18n.T("The [] is partially shredded.", zone_name)));
        }

        if (atom_storage != null)
        {
            DmList how_cool_are_your_threads = Dm.list("<span class='notice'>");
            if (Dm.truthy(atom_storage.attack_hand_interact))
                Dm.append(ref how_cool_are_your_threads, I18n.T("[]'s storage opens when clicked.\n", this));
            else
                Dm.append(ref how_cool_are_your_threads, I18n.T("[]'s storage opens when dragged to yourself.\n", this));
            if (Dm.truthy(Dm.length(atom_storage.can_hold))) // If pocket type can hold anything, vs only specific items
                Dm.append(ref how_cool_are_your_threads, I18n.T("[] can store [] <a href='byond://?src=[];show_valid_pocket_items=1'>item\\s</a>.\n", this, atom_storage.max_slots, REF(this)));
            else
                Dm.append(ref how_cool_are_your_threads, I18n.T("[] can store [] item\\s that are [] or smaller.\n", this, atom_storage.max_slots, weight_class_to_text(atom_storage.max_specific_storage)));
            if (Dm.truthy(atom_storage.quickdraw))
                Dm.append(ref how_cool_are_your_threads, I18n.T("You can quickly remove an item from [] using Right-Click.\n", this));
            if (Dm.truthy(atom_storage.silent))
                Dm.append(ref how_cool_are_your_threads, I18n.T("Adding or removing items from [] makes no noise.\n", this));
            Dm.append(ref how_cool_are_your_threads, "</span>");
            Dm.append(ref dot, jointext(how_cool_are_your_threads, ""));
        }

        if (Dm.truthy(get_armor().has_any_armor()) || Dm.truthy(Dm.bitand(flags_cover, HEADCOVERSMOUTH | PEPPERPROOF)) || Dm.truthy(Dm.bitand(clothing_flags, STOPSPRESSUREDAMAGE)) || Dm.truthy(Dm.bitand(visor_flags, STOPSPRESSUREDAMAGE)))
            Dm.append(ref dot, span_notice(I18n.T("It has a <a href='byond://?src=[];list_armor=1'>tag</a> listing its protection classes.", REF(this))));

        if (Dm.truthy(is_laundered))
            Dm.append(ref dot, I18n.T("[] looks crisp and pristine.", this));
        return dot;
    }

    // tg: code/modules/clothing/clothing.dm:368
    public override object examine_tags(mob user = null)
    {
        DmList dot = base.examine_tags(user) as DmList;
        if (Dm.truthy(Dm.bitand(clothing_flags, THICKMATERIAL)))
            dot[I18n.T("thick")] = I18n.T("Protects from most injections and sprays.");
        if (HAS_TRAIT(this, TRAIT_CASTING_CLOTHING))
            dot[I18n.T("magical")] = I18n.T("Allows magical beings to cast spells when wearing [].", this);
        if (Dm.truthy(Dm.bitand(clothing_flags, STOPSPRESSUREDAMAGE)) || Dm.truthy(Dm.bitand(visor_flags, STOPSPRESSUREDAMAGE)))
            dot[I18n.T("pressure-proof")] = I18n.T("Protects the wearer from extremely low or high pressure, such as vacuum of space.");
        if (Dm.truthy(Dm.bitand(flags_cover, PEPPERPROOF)))
            dot[I18n.T("pepper-proof")] = I18n.T("Protects the wearer from the effects of pepperspray.");
        if (Dm.truthy(heat_protection) || Dm.truthy(cold_protection))
        {
            object heat_desc = null;
            object cold_desc = null;
            float max_heat = Dm.num(max_heat_protection_temperature);
            if (max_heat >= 400 && max_heat <= 1000)
                heat_desc = I18n.T("high");
            else if (max_heat >= 1001 && max_heat <= 1600)
                heat_desc = I18n.T("very high");
            else if (max_heat >= 1601 && max_heat <= 35000)
                heat_desc = I18n.T("extremely high");
            float min_cold = Dm.num(min_cold_protection_temperature);
            if (min_cold >= 160 && min_cold <= 272)
                cold_desc = I18n.T("low");
            else if (min_cold >= 72 && min_cold <= 159)
                cold_desc = I18n.T("very low");
            else if (min_cold >= 0 && min_cold <= 71)
                cold_desc = I18n.T("extremely low");
            dot[I18n.T("thermally insulated")] = I18n.T("Protects the wearer from [] temperatures.", jointext(Dm.sub(Dm.list(heat_desc, cold_desc), null), I18n.T(" and ")));
        }
        if (Dm.@in(TRAIT_QUICK_CARRY, clothing_traits) || Dm.@in(TRAIT_QUICKER_CARRY, clothing_traits))
            dot[I18n.T("tactile")] = I18n.T("Decreases the time it takes to pick up creatures by [].", Dm.@in(TRAIT_QUICKER_CARRY, clothing_traits) ? I18n.T("2 seconds") : I18n.T("1 second"));
        if (Dm.@in(TRAIT_FASTMED, clothing_traits))
            dot[I18n.T("sterile")] = I18n.T("Increases the speed at which reagents are administered to others by []%.", round((1 / NITRILE_GLOVES_MULTIPLIER - 1) * 100, 1));
        if (Dm.@in(TRAIT_FAST_CUFFING, clothing_traits))
            dot[I18n.T("secure")] = I18n.T("Increases the speed at which you apply restraints.");
        if (emp_protection > EMP_PROTECTION_NONE)
            dot[I18n.T("emp resistant")] = I18n.T("Reduces the effects of incoming electromagnetic pulses on the wearer.");
        return dot;
    }

    // tg: code/modules/clothing/clothing.dm:405
    public override object examine_descriptor(mob user = null)
    {
        return I18n.T("clothing");
    }

    // tg: code/modules/clothing/clothing.dm:408  (the readout behind the examine's "tag" link)
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        object dot = await base.Topic(href, href_list);

        if (Dm.truthy(href_list["list_armor"]))
        {
            DmList readout = Dm.list();

            datum_armor armor = get_armor();
            bool added_damage_header = false;
            foreach (object damage_key in Dm.each(Dm.list(BIO, BOMB, BULLET, ENERGY, LASER, MELEE, WOUND))) // ARMOR_LIST_DAMAGE (__DEFINES/combat.dm:45)
            {
                object rating = armor.get_rating(damage_key);
                if (!Dm.truthy(rating))
                    continue;
                if (!added_damage_header)
                {
                    Dm.append(ref readout, I18n.T("<b><u>ARMOR (I-X)</u></b>"));
                    added_damage_header = true;
                }
                Dm.append(ref readout, Dm.text("[] []", armor_to_protection_name(damage_key), armor_to_protection_class(rating)));
            }

            bool added_durability_header = false;
            foreach (object durability_key in Dm.each(Dm.list(ACID, FIRE))) // ARMOR_LIST_DURABILITY (__DEFINES/combat.dm:48)
            {
                object rating = armor.get_rating(durability_key);
                if (!Dm.truthy(rating))
                    continue;
                if (!added_durability_header)
                {
                    Dm.append(ref readout, I18n.T("<b><u>DURABILITY (I-X)</u></b>"));
                    added_durability_header = true;
                }
                Dm.append(ref readout, Dm.text("[] []", armor_to_protection_name(durability_key), armor_to_protection_class(rating)));
            }

            if (Dm.truthy(Dm.bitand(flags_cover, HEADCOVERSMOUTH)) || Dm.truthy(Dm.bitand(flags_cover, PEPPERPROOF)))
            {
                DmList things_blocked = Dm.list();
                if (Dm.truthy(Dm.bitand(flags_cover, HEADCOVERSMOUTH)))
                    Dm.append(ref things_blocked, span_tooltip(I18n.T("Because this item is worn on the head and is covering the mouth, it will block facehugger proboscides, killing facehuggers."), I18n.T("facehuggers")));
                if (Dm.truthy(Dm.bitand(flags_cover, PEPPERPROOF)))
                    Dm.append(ref things_blocked, I18n.T("pepperspray"));
                if (Dm.truthy(Dm.length(things_blocked)))
                {
                    Dm.append(ref readout, I18n.T("<b><u>COVERAGE</u></b>"));
                    Dm.append(ref readout, I18n.T("It will block [].", english_list(things_blocked)));
                }
            }

            DmList parts_covered = Dm.list();
            if (Dm.truthy(Dm.bitand(body_parts_covered, HEAD)))
                Dm.append(ref parts_covered, I18n.T("head"));
            if (Dm.truthy(Dm.bitand(body_parts_covered, CHEST)))
                Dm.append(ref parts_covered, I18n.T("torso"));
            if (Dm.truthy(Dm.bitand(body_parts_covered, ARMS | HANDS)))
                Dm.append(ref parts_covered, I18n.T("arms"));
            if (Dm.truthy(Dm.bitand(body_parts_covered, LEGS | FEET)))
                Dm.append(ref parts_covered, I18n.T("legs"));
            if (Dm.truthy(Dm.length(parts_covered)))
                Dm.append(ref readout, I18n.T("It covers the wearer's [].", english_list(parts_covered)));

            if (Dm.truthy(Dm.bitand(clothing_flags, STOPSPRESSUREDAMAGE)) || Dm.truthy(Dm.bitand(visor_flags, STOPSPRESSUREDAMAGE)))
            {
                string output_string = I18n.T("It");
                if (!Dm.truthy(Dm.bitand(clothing_flags, STOPSPRESSUREDAMAGE)))
                    output_string = I18n.T("When sealed, it");
                Dm.append(ref readout, I18n.T("[] will protect the wearer's bodyparts that it covers from [].", output_string, span_tooltip(I18n.T("The extremely low pressure is the biggest danger posed by the vacuum of space."), I18n.T("low pressure"))));
            }

            object heat_prot = null;
            float max_heat = Dm.num(max_heat_protection_temperature);
            if (max_heat >= 400 && max_heat <= 1000)
                heat_prot = I18n.T("minor");
            else if (max_heat >= 1001 && max_heat <= 1600)
                heat_prot = I18n.T("some");
            else if (max_heat >= 1601 && max_heat <= 35000)
                heat_prot = I18n.T("extreme");
            // tg appends this line to the proc's return value, not to the readout, so it never shows (kept as tg has it)
            if (Dm.truthy(heat_prot))
                Dm.append(ref dot, I18n.T("[] offers the wearer [] protection from heat, up to [] kelvin.", this, heat_protection, max_heat_protection_temperature));

            if (Dm.truthy(min_cold_protection_temperature))
                Dm.append(ref readout, I18n.T("It will insulate the wearer from [].", Dm.num(min_cold_protection_temperature) <= SPACE_SUIT_MIN_TEMP_PROTECT ? span_tooltip(I18n.T("While not as dangerous as the lack of pressure, the extremely low temperature of space is also a hazard."), I18n.T("the cold of space, down to [] kelvin", min_cold_protection_temperature)) : I18n.T("cold, down to [] kelvin", min_cold_protection_temperature)));

            if (!Dm.truthy(Dm.length(readout)))
                Dm.append(ref readout, I18n.T("No armor or durability information available."));

            string formatted_readout = span_notice(I18n.T("<b>PROTECTION CLASSES</b><hr>[]", jointext(readout, "\n")));
            to_chat(usr, boxed_message(formatted_readout));
        }
        return dot;
    }

    /**
     * Rounds armor_value down to the nearest 10, divides it by 10 and then converts it to Roman numerals.
     *
     * Arguments:
     * * armor_value - Number we're converting
     */
    // tg: code/modules/clothing/clothing.dm:489
    public virtual object armor_to_protection_class(object armor_value = null)
    {
        object dot = null;
        if (Dm.num(armor_value) < 0)
            dot = "-";
        dot = Dm.add(dot, Dm.text("\\Roman[]", round(abs(Dm.num(armor_value)), 10) / 10));
        return dot;
    }

    // tg: code/modules/clothing/clothing.dm:495
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = null;
        dot = await base.atom_break(damage_flag);
        await update_clothes_damaged_state(CLOTHING_DAMAGED);

        if (isliving(loc)) //It's not important enough to warrant a message if it's not on someone
        {
            mob_living M = (mob_living)loc;
            if (Dm.@in(this, M.get_equipped_items()))
                to_chat(M, span_warning(I18n.T("Your [] start[] to fall apart!", name, p_s())));
            else
                to_chat(M, span_warning(I18n.T("[] start[] to fall apart!", this, p_s())));
        }
        return dot;
    }

    // you just dont get the same feeling with handwashed clothes
    // tg: code/modules/clothing/clothing.dm:507
    [DmParams()]
    public override async DmTask machine_wash(obj_machinery_washing_machine washer = null, params object[] __extra)
    {
        await base.machine_wash(washer, __extra);
        if (Dm.truthy(stubborn_stains)) //Just can't make it feel right
            return;

        is_laundered = TRUE;
        // VARSET_CALLBACK(src, is_laundered, FALSE): ___callbackvarset's datum branch for this var (is_laundered__varset)
        _addtimer(Dm.@new<datum_callback>(this, nameof(is_laundered__varset), FALSE), 2 * MINUTES, file: "code/modules/clothing/clothing.dm", line: 513);
    }

    /// VARSET_CALLBACK(src, is_laundered, FALSE)'s target: ___callbackvarset's datum branch (varset_callback.dm:8-23) for the var.
    public virtual void is_laundered__varset(object var_value)
    {
        is_laundered = Dm.num(var_value);
    }

    //This mostly exists so subtypes can call appriopriate update icon calls on the wearer.
    // tg: code/modules/clothing/clothing.dm:516
    public virtual async DmTask update_clothes_damaged_state(float? damaged_state = null)
    {
        damaged_state ??= CLOTHING_DAMAGED;
        damaged_clothes = Dm.num(damaged_state);
    }

    // tg: code/modules/clothing/clothing.dm:519  (M6 slice 7, D155: the /icon's Blend is drawn by the client, icon.cs)
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (!Dm.truthy(damaged_clothes))
            return dot;

        // DEVIATION(DV19): a GAGS icon is the client-composed name (text), which has no BYOND ref: the name keys the cache, as the
        // airlock overlay cache does (damaged GAGS shoes and clothes, the M6 demo's bomb).
        string index = Dm.text("[]-[]", istext(icon) ? icon : REF(icon), icon_state);
        DmList damaged_clothes_icons = update_overlays__damaged_clothes_icons ??= Dm.list();
        var damaged_clothes_icon = damaged_clothes_icons[index] as icon;
        if (damaged_clothes_icon == null)
        {
            damaged_clothes_icon = Dm.icon(icon, icon_state, null, 1);
            damaged_clothes_icon.Blend("#fff", ICON_ADD); //fills the icon_state with white (except where it's transparent)
            damaged_clothes_icon.Blend(Dm.icon(Dm.resource("icons/effects/item_damage.dmi"), "itemdamaged"), ICON_MULTIPLY); //adds damage effect and the remaining white areas become transparant
            damaged_clothes_icon = Dm.fcopy_rsc(damaged_clothes_icon) as icon;
            damaged_clothes_icons[index] = damaged_clothes_icon;
        }
        Dm.append(ref dot, damaged_clothes_icon);
        return dot;
    }

    // tg: code/modules/clothing/clothing.dm:523  var/static/list/damaged_clothes_icons (rules 10.6: a TgState field)
    private static ref DmList update_overlays__damaged_clothes_icons => ref TgState.Current.obj_item_clothing__update_overlays__damaged_clothes_icons;

    // tg: code/modules/clothing/clothing.dm:606
    public virtual void spawn_shreds()
    {
        Dm.@new<obj_effect_decal_cleanable_shreds>(get_turf(this), name);
    }

    // tg: code/modules/clothing/clothing.dm:609
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        if (Dm.@in(damage_flag, Dm.list(ACID, FIRE)))
        {
            await base.atom_destruction(damage_flag);
            return;
        }
        if (Dm.eq(damage_flag, BOMB))
        {
            //so the shred survives potential turf change from the explosion.
            _addtimer(Dm.@new<datum_callback>(this, nameof(spawn_shreds)), 0.1f * SECONDS, file: "code/modules/clothing/clothing.dm", line: 614);
            await deconstruct(FALSE);
        }
        if (Dm.eq(damage_flag, CONSUME)) //This allows for moths to fully consume clothing, rather than damaging it like other sources like brute
        {
            turf current_position = get_turf(this);
            Dm.@new<obj_effect_decal_cleanable_shreds>(current_position, name);
            if (isliving(loc))
            {
                mob_living possessing_mob = (mob_living)loc;
                possessing_mob.visible_message(span_danger(I18n.T("[] is consumed until naught but shreds remains!", this)), span_boldwarning(I18n.T("[] falls apart into little bits!", this)));
            }
            await deconstruct(FALSE);
        }
        else
        {
            body_parts_covered = NONE;
            slot_flags = NONE;
            await update_clothes_damaged_state(CLOTHING_SHREDDED);
            if (isliving(loc))
            {
                mob_living M = (mob_living)loc;
                if (Dm.@in(this, M.get_equipped_items())) //make sure they were wearing it and not attacking the item in their hands
                {
                    M.visible_message(span_danger(I18n.T("[]'s [] fall[] off, [] completely shredded!", M, name, p_s(), p_theyre())), span_warning(I18n.T("<b>Your [] fall[] off, [] completely shredded!</b>", name, p_s(), p_theyre())), vision_distance: COMBAT_MESSAGE_RANGE);
                    await M.dropItemToGround(this);
                }
                else
                    M.visible_message(span_danger(I18n.T("[] fall[] apart, completely shredded!", this, p_s())), vision_distance: COMBAT_MESSAGE_RANGE);
            }
            name = I18n.T("shredded []", Dm.initial(this, (atom p) => p.name)); // change the name -after- the message, not before.
            update_appearance();
        }
        // SEND_SIGNAL(src, COMSIG_ATOM_DESTRUCTION, damage_flag) (rules 11.1 (e), a statement)
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_DESTRUCTION]))
            this._SendSignal(COMSIG_ATOM_DESTRUCTION, Dm.list(this, damage_flag));
    }

    /// If we're a clothing with at least 1 shredded/disabled zone, give the wearer a periodic heads up letting them know their clothes are damaged
    // tg: code/modules/clothing/clothing.dm:639
    [SignalHandler]
    public virtual void bristle(mob_living L = null)
    {
        if (L == null)
            return;
        if (prob(0.2f))
            to_chat(L, span_warning(I18n.T("The damaged threads on your [] chafe!", name)));
    }

    // tg: code/modules/clothing/clothing.dm:647
    public override async DmTask apply_fantasy_bonuses(object bonus = null)
    {
        await base.apply_fantasy_bonuses(bonus);
        set_armor(get_armor().generate_new_with_modifiers(Dm.list(Dm.kv(ARMOR_ALL, bonus))));
    }

    // tg: code/modules/clothing/clothing.dm:651
    public override async DmTask remove_fantasy_bonuses(object bonus = null)
    {
        set_armor(get_armor().generate_new_with_modifiers(Dm.list(Dm.kv(ARMOR_ALL, Dm.neg(bonus)))));
        await base.remove_fantasy_bonuses(bonus);
    }

    /// Returns a list of overlays with our blood, if we're bloodied
    // tg: code/modules/clothing/clothing.dm:656
    public virtual async DmTask<object> get_blood_overlay(object blood_state = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        if (!Dm.truthy(GET_ATOM_BLOOD_DECAL_LENGTH(this)))
            return null;

        mutable_appearance blood_overlay = null;
        if (Dm.truthy(Dm.bitand(clothing_flags, LARGE_WORN_ICON)))
            blood_overlay = (mutable_appearance)mutable_appearance(Dm.resource("icons/effects/64x64.dmi"), Dm.text("[]blood_large", blood_state));
        else
            blood_overlay = (mutable_appearance)mutable_appearance(Dm.resource("icons/effects/blood.dmi"), Dm.text("[]blood", blood_state));

        blood_overlay.color = get_blood_dna_color();

        object emissive_alpha = get_blood_emissive_alpha(is_worn: TRUE);
        if (Dm.truthy(emissive_alpha))
        {
            // emissive_overlay = emissive_appearance(blood_overlay.icon, blood_overlay.icon_state, src, alpha = emissive_alpha,
            // effect_type = EMISSIVE_NO_BLOOM); blood_overlay.overlays += emissive_overlay: glowing blood (lighting, D24)
            Dm.Deferred("code/modules/clothing/clothing.dm:669", "get_blood_overlay: the emissive overlay of glowing blood (emissives, D24)");
        }

        return blood_overlay;
    }

    /// Proc that adjusts the clothing item, used by things like breathing masks, welding helmets, welding goggles etc.
    // tg: code/modules/clothing/clothing.dm:554
    public virtual async DmTask<object> adjust_visor(mob_living user = null)
    {
        if (!Dm.truthy(await can_use(user)))
            return FALSE;

        visor_toggling();

        object message = null;
        if (Dm.truthy(up))
            message = this.alt_toggle_message is var or__1 && Dm.truthy(or__1) ? or__1 : I18n.T("You push [] out of the way.", this);
        else
            message = this.toggle_message is var or__2 && Dm.truthy(or__2) ? or__2 : I18n.T("You push [] back into place.", this);

        to_chat(user, span_notice(Dm.text("[]", message)));

        //play sounds when toggling the visor up or down (if there is any)
        if (Dm.truthy(visor_toggle_up_sound) && Dm.truthy(up))
            playsound(this, visor_toggle_up_sound, 20, TRUE, -1);
        if (Dm.truthy(visor_toggle_down_sound) && !Dm.truthy(up))
            playsound(this, visor_toggle_down_sound, 20, TRUE, -1);

        await update_item_action_buttons();

        if (Dm.truthy(user.is_holding(this)))
        {
            await user.update_held_items();
            return TRUE;
        }
        await user.update_clothing(slot_flags);
        if (!iscarbon(user))
            return TRUE;
        mob_living_carbon carbon_user = user as mob_living_carbon;
        if (Dm.truthy(visor_flags_inv))
            carbon_user.refresh_obscured();
        if (Dm.truthy(Dm.bitand(visor_vars_to_toggle, VISOR_TINT)))
            await carbon_user.update_tint();
        if (Dm.truthy(Dm.bitand(visor_flags, Dm.bitor(MASKINTERNALS, HEADINTERNALS))) && Dm.truthy(carbon_user.invalid_internals()))
            await carbon_user.cutoff_internals();
        return TRUE;
    }

    // tg: code/modules/clothing/clothing.dm:591
    public virtual void visor_toggling(params object[] __extra) //handles all the actual toggling of flags
    {
        up = Dm.num(!Dm.truthy(up));
        clothing_flags = Dm.bitxor(clothing_flags, visor_flags);
        flags_inv = Dm.bitxor(flags_inv, visor_flags_inv);
        flags_cover = Dm.bitxor(flags_cover, visor_flags_cover);
        if (Dm.truthy(Dm.bitand(visor_vars_to_toggle, VISOR_FLASHPROTECT)))
            flash_protect = Dm.bitxor(flash_protect, Dm.initial(this, (obj_item_clothing p) => p.flash_protect));
        if (Dm.truthy(Dm.bitand(visor_vars_to_toggle, VISOR_TINT)))
            tint = Dm.bitxor(tint, Dm.initial(this, (obj_item_clothing p) => p.tint));
        if (Dm.truthy(_listen_lookup?[COMSIG_CLOTHING_VISOR_TOGGLE]))
            _SendSignal(COMSIG_CLOTHING_VISOR_TOGGLE, Dm.list(this, up));
        update_appearance(); //most of the time the sprite changes
    }

    // tg: code/modules/clothing/clothing.dm:603
    public virtual async DmTask<object> can_use(mob user = null)
    {
        return Dm.num(user is mob && !Dm.truthy(user.incapacitated));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/clothing/clothing.dm:523  /obj/item/clothing/update_overlays var/static/list/damaged_clothes_icons
    internal DmList obj_item_clothing__update_overlays__damaged_clothes_icons;
}
