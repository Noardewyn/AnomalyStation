// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/mutations.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: the organic weapon and suit bases, the arm blade, the tentacle (its gun, casing and projectile:
// the pull of an item, the drag, the disarm and the grab-and-impale), the organic shield, the chitinous armor, the hive head with
// its bees and the legion hive head. The var blocks are generated (D111). The legion hive head's brood is not ported: its
// assign_creator waits (Dm.Deferred). The arm blade's smash of a computer is /obj/attack_alien (its slot's user widened to the carbon, gaps K1b).

/*
	Changeling Mutations! ~By Miauw (ALL OF IT :V)
	Contains:
		Arm Blade
		Space Suit
		Shield
		Armor
		Tentacles
*/


//Parent to shields and blades because muh copypasted code.
// tg: code/modules/antagonists/changeling/powers/mutations.dm:13
public partial class datum_action_changeling_weapon : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:25
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.Grant(granted_to, __extra);
        if (owner == null || !Dm.truthy(req_human))
            return dot;
        RegisterSignal(granted_to, COMSIG_HUMAN_MONKEYIZE, nameof(became_monkey));
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:31
    public override async DmTask Remove(mob remove_from = null)
    {
        UnregisterSignal(remove_from, COMSIG_HUMAN_MONKEYIZE);
        await unequip_held(remove_from);
        await base.Remove(remove_from);
    }

    /// Remove weapons if we become a monkey
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:37
    [SignalHandler]
    public virtual void became_monkey(mob source = null)
    {
        Waiting.NoSleep(unequip_held(source));
    }

    /// Removes weapon if it exists, returns true if we removed something
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:42
    public virtual async DmTask<object> unequip_held(mob user = null)
    {
        object found_weapon = FALSE;
        foreach (obj_item held in Dm.each<obj_item>(user.held_items))
            found_weapon = (await check_weapon(user, held) is var or__1 && Dm.truthy(or__1) ? or__1 : found_weapon);
        return found_weapon;
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:48
    public override async DmTask<object> try_to_sting(mob_living user = null, mob_living target = null)
    {
        if (Dm.truthy(await unequip_held(user)))
            return null;
        await base.try_to_sting(user, target);
        return null;
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:53
    public virtual async DmTask<object> check_weapon(mob user = null, obj_item hand_item = null)
    {
        if (istype(hand_item, weapon_type))
        {
            await user.temporarilyRemoveItemFromInventory(hand_item, TRUE); //DROPDEL will delete the item
            if (!Dm.truthy(silent))
            {
                playsound(user, Dm.resource("sound/effects/blob/blobattack.ogg"), 30, TRUE);
                user.visible_message(span_warning(I18n.T("With a sickening crunch, [] reforms [] [] into an arm!", user, user.p_their(), weapon_name_simple)), span_notice(I18n.T("We assimilate the [] back into our body.", weapon_name_simple)), span_italics(I18n.T("You hear organic matter ripping and tearing!")));
            }
            await user.update_held_items();
            return TRUE;
        }
        return null;
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:62
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        obj_item held = user.get_active_held_item() as obj_item;
        if (held != null && !Dm.truthy(await user.dropItemToGround(held)))
        {
            user.balloon_alert(user, I18n.T("hand occupied!"));
            return null;
        }
        if (!(user is mob_living_carbon))
        {
            user.balloon_alert(user, I18n.T("wrong shape!"));
            return null;
        }
        await base.sting_action(user, target);
        object limb_regen = 0;
        if (HAS_TRAIT_FROM_ONLY(user, TRAIT_PARALYSIS_L_ARM, CHANGELING_TRAIT) || HAS_TRAIT_FROM_ONLY(user, TRAIT_PARALYSIS_R_ARM, CHANGELING_TRAIT))
        {
            user.balloon_alert(user, I18n.T("not enough muscle!")); // no cheesing repuprosed glands
            return null;
        }
        // tg passes 1 as the dismembered_by_copy list: a non-list that LAZYLEN reads as empty, so no phantom scar (an empty list here)
        if (IS_RIGHT_INDEX(user.active_hand_index)) //we regen the arm before changing it into the weapon
            limb_regen = await ((mob_living_carbon)user).regenerate_limb(BODY_ZONE_R_ARM, Dm.list());
        else
            limb_regen = await ((mob_living_carbon)user).regenerate_limb(BODY_ZONE_L_ARM, Dm.list());
        if (Dm.truthy(limb_regen))
        {
            user.visible_message(span_warning(I18n.T("[]'s missing arm reforms, making a loud, grotesque sound!", user)), span_userdanger(I18n.T("Your arm regrows, making a loud, crunchy sound and giving you great pain!")), span_hear(I18n.T("You hear organic matter ripping and tearing!")));
            await user.emote("scream");
        }
        obj_item W = await Dm.new_async(weapon_type, user, silent) as obj_item;
        await user.put_in_hands(W);
        if (!Dm.truthy(silent))
            playsound(user, Dm.resource("sound/effects/blob/blobattack.ogg"), 30, TRUE);
        return W;
    }
}


//Parent to space suits and armor.
// tg: code/modules/antagonists/changeling/powers/mutations.dm:90
public partial class datum_action_changeling_suit : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:105
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.Grant(granted_to, __extra);
        if (owner == null || !Dm.truthy(req_human))
            return dot;
        RegisterSignal(granted_to, COMSIG_HUMAN_MONKEYIZE, nameof(became_monkey));
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:111
    public override async DmTask Remove(mob remove_from = null)
    {
        UnregisterSignal(remove_from, COMSIG_HUMAN_MONKEYIZE);
        await check_suit(remove_from);
        await base.Remove(remove_from);
    }

    /// Remove suit if we become a monkey
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:117
    [SignalHandler]
    public virtual void became_monkey()
    {
        Waiting.NoSleep(check_suit(owner));
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:121
    public override async DmTask<object> try_to_sting(mob_living user = null, mob_living target = null)
    {
        if (Dm.truthy(await check_suit(user)))
            return null;
        mob_living_carbon_human H = user as mob_living_carbon_human;
        await base.try_to_sting(H, target);
        return null;
    }

    //checks if we already have an organic suit and casts it off.
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:128
    public virtual async DmTask<object> check_suit(mob user = null)
    {
        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
        if (!ishuman(user) || changeling == null)
            return 1;
        mob_living_carbon_human H = user as mob_living_carbon_human;

        if (istype(H.wear_suit, suit_type) || istype(H.head, helmet_type))
        {
            string name_to_use = (suit_type == null ? helmet_name_simple : suit_name_simple);
            H.visible_message(span_warning(I18n.T("[] casts off [] []!", H, H.p_their(), name_to_use)), span_warning(I18n.T("We cast off our [].", name_to_use)), span_hear(I18n.T("You hear the organic matter ripping and tearing!")));
            if (helmet_type != null)
                await H.temporarilyRemoveItemFromInventory(H.head, TRUE); //The qdel on dropped() takes care of it
            if (suit_type != null)
                await H.temporarilyRemoveItemFromInventory(H.wear_suit, TRUE);
            await H.update_worn_oversuit();
            await H.update_worn_head();
            await H.update_body_parts();

            if (Dm.truthy(blood_on_castoff))
            {
                await H.add_splatter_floor();
                playsound(H.loc, Dm.resource("sound/effects/splat.ogg"), 50, TRUE); //So real sounds
            }

            changeling.chem_recharge_slowdown -= recharge_slowdown;
            return 1;
        }
        return null;
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:152
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        if (!Dm.truthy(user.canUnEquip(((mob_living_carbon_human)user).wear_suit)) && suit_type != null)
        {
            user.balloon_alert(user, I18n.T("body occupied!"));
            return null;
        }
        if (!Dm.truthy(user.canUnEquip(((mob_living_carbon_human)user).head)) && helmet_type != null)
        {
            user.balloon_alert(user, I18n.T("head occupied!"));
            return null;
        }
        await base.sting_action(user, target);
        if (suit_type != null)
        {
            await user.dropItemToGround(((mob_living_carbon_human)user).wear_suit);
            await user.equip_to_slot_if_possible(await Dm.new_async(suit_type, user) as obj_item, ITEM_SLOT_OCLOTHING, 1, 1, 1);
        }
        if (helmet_type != null)
        {
            await user.dropItemToGround(((mob_living_carbon_human)user).head);
            await user.equip_to_slot_if_possible(await Dm.new_async(helmet_type, user) as obj_item, ITEM_SLOT_HEAD, 1, 1, 1);
        }

        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling;
        changeling.chem_recharge_slowdown += recharge_slowdown;
        return TRUE;
    }
}


//fancy headers yo
/***************************************\
|***************ARM BLADE***************|
\***************************************/
// tg: code/modules/antagonists/changeling/powers/mutations.dm:188
public partial class obj_item_melee_arm_blade : obj_item_melee
{
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:215
    [DmParams("mapload", "silent", "synthetic")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object silent = Dm.arg(__extra, 0);
        object synthetic = Dm.arg(__extra, 1);
        object dot = null;
        dot = base.Initialize(mapload, Dm.forward(__extra, silent, synthetic));
        ADD_TRAIT(this, TRAIT_NODROP, CHANGELING_TRAIT);
        if (ismob(loc) && !Dm.truthy(silent))
            loc.visible_message(span_warning(I18n.T("A grotesque blade forms around []'s arm!", loc.name)), span_warning(I18n.T("Our arm twists and mutates, transforming it into a deadly blade.")), span_hear(I18n.T("You hear organic matter ripping and tearing!")));
        if (Dm.truthy(synthetic))
            can_drop = TRUE;
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple, -5));
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering), Dm.named("speed", 6 * SECONDS), Dm.named("effectiveness", 80)));
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:230
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (QDELETED(target))
            return;
        if (target is obj_structure_table)
        {
            obj smash = target as obj;
            await smash.deconstruct(FALSE);
        }

        else if (target is obj_machinery_computer)
        {
            await target.attack_alien(user as mob_living_carbon); //muh copypasta (the slot's user is a carbon since gaps K1b: tg passes the changeling)
        }

        else if (target is obj_machinery_door_airlock)
        {
            obj_machinery_door_airlock opening = target as obj_machinery_door_airlock;

            if ((!Dm.truthy(opening.requiresID()) || Dm.truthy(await opening.allowed__proc(user))) && Dm.truthy(opening.hasPower())) //This is to prevent stupid shit like hitting a door with an arm blade, the door opening because you have acces and still getting a "the airlocks motors resist our efforts to force it" message, power requirement is so this doesn't stop unpowered doors from being pried open if you have access
                return;
            if (Dm.truthy(opening.locked))
            {
                opening.balloon_alert(user, I18n.T("bolted!"));
                return;
            }

            if (Dm.truthy(opening.hasPower()))
            {
                user.visible_message(span_warning(I18n.T("[] jams [] into the airlock and starts prying it open!", user, this)), span_warning(I18n.T("We start forcing the [] open.", opening)),
                    span_hear(I18n.T("You hear a metal screeching sound.")));
                playsound(opening, Dm.resource("sound/machines/airlock/airlock_alien_prying.ogg"), 100, TRUE);
                if (!Dm.truthy(await do_after(user, 10 * SECONDS, target: opening)))
                    return;
            }
            //user.say("Heeeeeeeeeerrre's Johnny!")
            user.visible_message(span_warning(I18n.T("[] forces the airlock to open with [] [].", user, user.p_their(), this)), span_warning(I18n.T("We force the [] to open.", opening)),
                span_hear(I18n.T("You hear a metal screeching sound.")));
            await opening.open(BYPASS_DOOR_CHECKS);
        }
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:260
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        await base.dropped(user, silent, __extra);
        if (Dm.truthy(can_drop))
            Dm.@new<obj_item_melee_synthetic_arm_blade>(get_turf(user));
        return null;
    }
}

/***************************************\
|***********COMBAT TENTACLES*************|
\***************************************/

// tg: code/modules/antagonists/changeling/powers/mutations.dm:285
public partial class obj_item_gun_magic_tentacle : obj_item_gun_magic
{
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:310
    [DmParams("mapload", "silent")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object silent = Dm.arg(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, Dm.forward(__extra, silent));
        ADD_TRAIT(this, TRAIT_NODROP, CHANGELING_TRAIT);
        if (ismob(loc))
        {
            if (!Dm.truthy(silent))
                loc.visible_message(span_warning(I18n.T("[]'s arm starts stretching inhumanly!", loc.name)), span_warning(I18n.T("Our arm twists and mutates, transforming it into a tentacle.")), span_hear(I18n.T("You hear organic matter ripping and tearing!")));
            else
                to_chat(loc, span_notice(I18n.T("You prepare to extend a tentacle.")));
        }
        return dot;
    }


    // tg: code/modules/antagonists/changeling/powers/mutations.dm:320
    public override async DmTask shoot_with_empty_chamber(mob_living user = null)
    {
        user.balloon_alert(user, I18n.T("not ready!"));
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:323
    public override async DmTask<object> process_fire(atom target = null, mob_living user = null, float? message = null, object @params = null, string zone_override = null, float? bonus_spread = null)
    {
        object dot = null;
        obj_projectile_tentacle tentacle_shot = chambered.loaded_projectile as obj_projectile_tentacle; //Gets the actual projectile we will fire
        tentacle_shot.fire_modifiers = params2list(@params);
        dot = await base.process_fire(target, user, message, @params, zone_override, bonus_spread);
        if (charges == 0)
            qdel(this);
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:330
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] coils [] tightly around [] neck! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        return OXYLOSS;
    }
}

// tg: code/modules/antagonists/changeling/powers/mutations.dm:334
public partial class obj_item_ammo_casing_magic_tentacle : obj_item_ammo_casing_magic
{
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:342
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        gun = loc as obj_item_gun_magic_tentacle;
        dot = base.Initialize(mapload, __extra);
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:346
    public override object Destroy(float? force = null)
    {
        gun = null;
        return base.Destroy(force);
    }
}

// tg: code/modules/antagonists/changeling/powers/mutations.dm:350
public partial class obj_projectile_tentacle : obj_projectile
{
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:363
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        source = loc as obj_item_ammo_casing_magic_tentacle;
        dot = base.Initialize(mapload, __extra);
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:367
    [DmParams("setAngle")]
    public override async DmTask fire(object setAngle = null, atom direct_target = null)
    {
        if (firer != null)
            chain = firer.Beam(this, icon_state: "tentacle", emissive: NONE);
        await base.fire(setAngle, direct_target);
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:372
    public virtual void reset_throw(mob_living_carbon_human user = null)
    {
        if (Dm.truthy(user.throw_mode))
            user.throw_mode_off(THROW_MODE_TOGGLE); //Don't annoy the changeling if he doesn't catch the item
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:376
    public virtual async DmTask tentacle_grab(mob_living_carbon_human user = null, mob_living_carbon victim = null)
    {
        if (!Dm.truthy(await user.Adjacent(victim)))
            return;

        if (Dm.truthy(user.get_active_held_item()) && !Dm.truthy(user.get_inactive_held_item()))
            user.swap_hand();

        if (Dm.truthy(user.get_active_held_item()))
            return;

        await victim.grabbedby(user);
        await victim.grippedby(user, instant: TRUE); //instant aggro grab

        foreach (obj_item weapon in Dm.each<obj_item>(user.held_items))
        {
            if (Dm.truthy(weapon.get_sharpness()))
            {
                victim.visible_message(span_danger(I18n.T("[] impales [] with [] []!", user, victim, user.p_their(), weapon.name)), span_userdanger(I18n.T("[] impales you with [] []!", user, user.p_their(), weapon.name)));
                await victim.apply_damage(weapon.force, BRUTE, BODY_ZONE_CHEST, attacking_item: weapon);
                user.do_item_attack_animation(victim, used_item: weapon, animation_type: ATTACK_ANIMATION_PIERCE);
                await user.add_blood_DNA_to_items(victim.get_blood_dna_list() as DmList, Dm.bitor(ITEM_SLOT_ICLOTHING, ITEM_SLOT_OCLOTHING));
                playsound(get_turf(user), weapon.hitsound, 75, TRUE);
                return;
            }
        }
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:398
    public override async DmTask<object> on_hit(atom target = null, float? blocked = null, object pierce_hit = null)
    {
        blocked ??= 0;
        object dot = null;
        if (!isliving(firer) || !ismovable(target))
            return await base.on_hit(target, blocked, pierce_hit);

        if (blocked >= 100)
            return BULLET_ACT_BLOCK;

        mob_living ling = firer as mob_living;
        if (isitem(target) && iscarbon(ling))
        {
            obj_item catching = target as obj_item;
            if (Dm.truthy(catching.anchored))
                return BULLET_ACT_BLOCK;

            mob_living_carbon carbon_ling = ling as mob_living_carbon;
            to_chat(carbon_ling, span_notice(I18n.T("You pull [] towards yourself.", catching)));
            carbon_ling.throw_mode_on(THROW_MODE_TOGGLE);
            await catching.throw_at(
                target: carbon_ling,
                range: 10,
                speed: 2,
                thrower: carbon_ling,
                diagonals_first: TRUE,
                callback: Dm.@new<datum_callback>(this, nameof(reset_throw), carbon_ling),
                gentle: TRUE
            );
            return BULLET_ACT_HIT;
        }

        dot = await base.on_hit(target, blocked, pierce_hit);
        if (!Dm.eq(dot, BULLET_ACT_HIT))
            return dot;
        mob_living victim = target as mob_living;
        if (!isliving(victim) || Dm.truthy(((atom_movable)target).anchored) || victim.throwing != null)
            return BULLET_ACT_BLOCK;

        if (!iscarbon(victim) || !ishuman(ling) || !Dm.truthy(ling.combat_mode))
        {
            victim.visible_message(
                span_danger(I18n.T("[] is grabbed by []'s []]!", victim, ling, this)),
                span_userdanger(I18n.T("\\A [] grabs you and pulls you towards []!", this, ling))
            );
            await victim.throw_at(
                target: get_step_towards(ling, victim),
                range: 8,
                speed: 2,
                thrower: ling,
                diagonals_first: TRUE,
                gentle: TRUE
            );
            return BULLET_ACT_HIT;
        }

        if (Dm.truthy(LAZYACCESS(fire_modifiers, RIGHT_CLICK)))
        {
            obj_item stealing = victim.get_active_held_item() as obj_item;
            if (stealing != null)
            {
                if (Dm.truthy(await victim.dropItemToGround(stealing)))
                {
                    victim.visible_message(
                        span_danger(I18n.T("[] is yanked off []'s hand by []!", stealing, victim, this)),
                        span_userdanger(I18n.T("\\A [] pulls [] away from you!", this, stealing))
                    );
                    return await on_hit(stealing); //grab the item as if you had hit it directly with the tentacle
                }

                to_chat(ling, span_warning(I18n.T("You can't seem to pry [] off []'s hands!", stealing, victim)));
                return BULLET_ACT_BLOCK;
            }

            to_chat(ling, span_danger(I18n.T("[] has nothing in hand to disarm!", victim)));
            return BULLET_ACT_HIT;
        }

        if (Dm.truthy(ling.combat_mode))
        {
            victim.visible_message(
                span_danger(I18n.T("[] is thrown towards [] by \\a []!", victim, ling, this)),
                span_userdanger(I18n.T("\\A [] grabs you and throws you towards []!", this, ling))
            );
            await victim.throw_at(
                target: get_step_towards(ling, victim),
                range: 8,
                speed: 2,
                thrower: ling,
                diagonals_first: TRUE,
                callback: Dm.@new<datum_callback>(this, nameof(tentacle_grab), ling, victim),
                gentle: TRUE
            );
        }

        return BULLET_ACT_HIT;
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:480
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref chain);
        source = null;
        return base.Destroy(force);
    }
}


/***************************************\
|****************SHIELD*****************|
\***************************************/
// tg: code/modules/antagonists/changeling/powers/mutations.dm:489
public partial class datum_action_changeling_weapon_shield : datum_action_changeling_weapon
{
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:502
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        datum_antagonist_changeling changeling = IS_CHANGELING(user) as datum_antagonist_changeling; //So we can read the absorbed_count.
        if (changeling == null)
            return null;

        obj_item_shield_changeling S = await base.sting_action(user) as obj_item_shield_changeling;
        S.remaining_uses = round(changeling.absorbed_count * 3);
        return TRUE;
    }
}

// tg: code/modules/antagonists/changeling/powers/mutations.dm:511
public partial class obj_item_shield_changeling : obj_item_shield
{
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:524
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, CHANGELING_TRAIT);
        if (ismob(loc))
            loc.visible_message(span_warning(I18n.T("The end of []'s hand inflates rapidly, forming a huge shield-like mass!", loc.name)), span_warning(I18n.T("We inflate our hand into a strong shield.")), span_hear(I18n.T("You hear organic matter ripping and tearing!")));
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:530
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (attack_type == OVERWHELMING_ATTACK)
            return FALSE;

        if (Dm.lt(remaining_uses, 1))
        {
            if (ishuman(loc))
            {
                mob_living_carbon_human H = loc as mob_living_carbon_human;
                H.visible_message(span_warning(I18n.T("With a sickening crunch, [] reforms [] shield into an arm!", H, H.p_their())), span_notice(I18n.T("We assimilate our shield into our body")), span_italics(I18n.T("You hear organic matter ripping and tearing!")));
            }
            qdel(this);
            return 0;
        }
        else
        {
            Dm.dec(ref remaining_uses);
            return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
        }
    }
}

/***************************************\
|*****************ARMOR*****************|
\***************************************/
// tg: code/modules/antagonists/changeling/powers/mutations.dm:563
public partial class obj_item_clothing_suit_armor_changeling : obj_item_clothing_suit_armor
{
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:586
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, CHANGELING_TRAIT);
        if (ismob(loc))
            loc.visible_message(span_warning(I18n.T("[]'s flesh turns black, quickly transforming into a hard, chitinous mass!", loc.name)), span_warning(I18n.T("We harden our flesh, creating a suit of armor!")), span_hear(I18n.T("You hear organic matter ripping and tearing!")));
        return dot;
    }
}

// tg: code/modules/antagonists/changeling/powers/mutations.dm:592
public partial class obj_item_clothing_head_helmet_changeling : obj_item_clothing_head_helmet
{
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:611
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, CHANGELING_TRAIT);
        return dot;
    }
}

// tg: code/modules/antagonists/changeling/powers/mutations.dm:629
public partial class obj_item_clothing_head_helmet_changeling_hivehead : obj_item_clothing_head_helmet
{
    // The first definition of Initialize (tg defines it twice on this type; the second's ..() calls this one, rules 2.5 and 7.5).
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:642
    public object Initialize__d0(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(holds_reagents))
            create_reagents(50, REFILLABLE);
        return dot;
    }

    // The second definition of Initialize (rules 2.5: its ..() is the first definition, Initialize__d0).
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:654
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = Initialize__d0(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, CHANGELING_TRAIT);
        return dot;
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:658
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_organ_monster_core_regenerative_core_legion) || !Dm.truthy(holds_reagents))
            return NONE;
        visible_message(span_boldwarning(I18n.T("As [] shoves [] into [], [] begins to mutate.", user, tool, this, this)));
        mob_living_carbon_human wearer = loc as mob_living_carbon_human;
        playsound(wearer, Dm.resource("sound/effects/blob/attackblob.ogg"), 60, TRUE);
        await wearer.temporarilyRemoveItemFromInventory(this, TRUE);
        await wearer.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_head_helmet_changeling_hivehead_legion>(wearer), ITEM_SLOT_HEAD, 1, 1, 1);
        qdel(tool);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/antagonists/changeling/powers/mutations.dm:669
public partial class datum_action_cooldown_hivehead_spawn_minions : datum_action_cooldown
{
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:682
    public override async DmTask<object> PreActivate(atom target = null)
    {
        if (Dm.truthy(Dm.bitand(owner.movement_type, VENTCRAWLING)))
        {
            owner.balloon_alert(owner, I18n.T("unavailable here"));
            return FALSE;
        }
        return await base.PreActivate(target);
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:688
    public override async DmTask<object> Activate(atom target = null)
    {
        object dot = null;
        dot = await base.Activate(target);
        do_tell();
        float spawns = spawn_count;
        if (owner.stat >= HARD_CRIT)
            spawns = 1;
        for (float i = 1, i__end = spawns; i <= i__end; i++)
        {
            mob_living_basic summoned_minion = await Dm.new_async(spawn_type, owner.drop_location()) as mob_living_basic;
            summoned_minion.set_allies(Dm.list(Dm.text("[]", REF(owner))));
            summoned_minion.set_faction(null);
            await minion_additional_changes(summoned_minion);
        }
        return dot;
    }

    ///Our tell that we're using this ability. Usually a sound and a visible message.area
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:701
    public virtual void do_tell()
    {
        owner.visible_message(span_warning(I18n.T("[]'s head begins to buzz as bees begin to pour out!", owner)), span_warning(I18n.T("We release the bees.")), span_hear(I18n.T("You hear a loud buzzing sound!")));
        playsound(owner, Dm.resource("sound/mobs/non-humanoids/bee/bee_swarm.ogg"), 60, TRUE);
    }

    ///Stuff we want to do to our minions. This is in its own proc so subtypes can override this behaviour.
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:706
    public virtual async DmTask minion_additional_changes(mob_living_basic minion = null)
    {
        mob_living_basic_bee summoned_bee = minion as mob_living_basic_bee;
        obj_item_clothing_head_helmet_changeling_hivehead hivehead = owner.get_item_by_slot(ITEM_SLOT_HEAD) as obj_item_clothing_head_helmet_changeling_hivehead;
        if (summoned_bee is mob_living_basic_bee && hivehead is obj_item_clothing_head_helmet_changeling_hivehead && Dm.truthy(length(((datum_reagents)hivehead.reagents).reagent_list)))
            await summoned_bee.assign_reagent(pick(((datum_reagents)hivehead.reagents).reagent_list) as datum_reagent);
    }
}

// tg: code/modules/antagonists/changeling/powers/mutations.dm:719
public partial class datum_action_cooldown_hivehead_spawn_minions_legion : datum_action_cooldown_hivehead_spawn_minions
{
    // tg: code/modules/antagonists/changeling/powers/mutations.dm:728
    public override void do_tell()
    {
        owner.visible_message(span_warning(I18n.T("[]'s head begins to shake as legion begin to pour out!", owner)), span_warning(I18n.T("We release the legion.")), span_hear(I18n.T("You hear a loud squishing sound!")));
        playsound(owner, Dm.resource("sound/effects/blob/attackblob.ogg"), 60, TRUE);
    }

    // tg: code/modules/antagonists/changeling/powers/mutations.dm:732
    public override async DmTask minion_additional_changes(mob_living_basic minion = null)
    {
        mob_living_basic_mining_legion_brood brood = minion as mob_living_basic_mining_legion_brood;
        if (brood is mob_living_basic_mining_legion_brood)
            Dm.Deferred("code/modules/antagonists/changeling/powers/mutations.dm:735", "minion_additional_changes: brood.assign_creator(owner, FALSE), the legion brood's creator (the lavaland legion is not ported: mining fauna)");
    }
}
