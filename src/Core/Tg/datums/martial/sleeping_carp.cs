// Translated from tgstation (snapshot 1d99bd7f): code/datums/martial/sleeping_carp.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3a (D316): the Sleeping Carp (its four combos, the nerve pinch and the neck snap, the projectile deflection,
// the dodge of a carp-dressed user, its traits, faction and language), the bo staff and the carp gloves, whole. The var blocks are
// generated (D111).

// tg: code/datums/martial/sleeping_carp.dm:6
public partial class datum_martial_art_the_sleeping_carp : datum_martial_art
{
    // tg: code/datums/martial/sleeping_carp.dm:1-4  #define WRIST_WRENCH_COMBO "HG" ... (file-local, #undef at :489-492)
    private const string WRIST_WRENCH_COMBO = "HG";
    private const string LAUNCH_KICK_COMBO = "HD";
    private const string DROP_KICK_COMBO = "DD";
    private const string KNEE_STOMACH_COMBO = "GH";

    // tg: code/datums/martial/sleeping_carp.dm:35
    public override async DmTask activate_style(mob_living new_holder = null)
    {
        await base.activate_style(new_holder);
        new_holder.add_traits(scarp_traits, SLEEPING_CARP_TRAIT);
        RegisterSignal(new_holder, COMSIG_ATOM_ATTACKBY, nameof(on_attackby));
        RegisterSignal(new_holder, COMSIG_ATOM_PRE_BULLET_ACT, nameof(hit_by_projectile));
        RegisterSignal(new_holder, COMSIG_LIVING_CHECK_BLOCK, nameof(check_dodge));
        new_holder.add_faction(FACTION_CARP); //:D
        new_holder.grant_language(typeof(datum_language_carptongue), ALL, Dm.str(GetType()));
    }

    // tg: code/datums/martial/sleeping_carp.dm:44
    public override async DmTask deactivate_style(mob_living remove_from = null)
    {
        remove_from.remove_traits(scarp_traits, SLEEPING_CARP_TRAIT);
        UnregisterSignal(remove_from, Dm.list(COMSIG_ATOM_ATTACKBY, COMSIG_ATOM_PRE_BULLET_ACT, COMSIG_LIVING_CHECK_BLOCK));
        remove_from.remove_faction(FACTION_CARP); //:(
        if (!QDELING(remove_from))
            remove_from.remove_language(typeof(datum_language_carptongue), ALL, Dm.str(GetType()));
        await base.deactivate_style(remove_from);
    }

    // tg: code/datums/martial/sleeping_carp.dm:52
    public virtual async DmTask<object> check_streak(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.truthy(findtext(streak, WRIST_WRENCH_COMBO)))
        {
            await reset_streak();
            return await wrist_wrench(attacker, defender);
        }

        if (Dm.truthy(findtext(streak, LAUNCH_KICK_COMBO)))
        {
            await reset_streak();
            return await launch_kick(attacker, defender);
        }

        if (Dm.truthy(findtext(streak, DROP_KICK_COMBO)))
        {
            await reset_streak();
            return await drop_kick(attacker, defender);
        }

        if (Dm.truthy(findtext(streak, KNEE_STOMACH_COMBO)))
        {
            await reset_streak();
            return await knee_stomach(attacker, defender);
        }

        return FALSE;
    }

    /// Gnashing Teeth: Harm Grab, twists and possibly breaks the target's arm, disarming them.
    // tg: code/datums/martial/sleeping_carp.dm:72
    public virtual async DmTask<object> wrist_wrench(mob_living attacker = null, mob_living defender = null)
    {
        // Determine if our defender is a carbon. If not, we don't wrist wrench
        if (!iscarbon(defender))
            return FALSE;

        // Determine if our target has a functioning active arm. If not, return.
        var affecting = defender.get_active_hand() as obj_item_bodypart;

        if (affecting == null)
            return FALSE;

        await attacker.do_attack_animation(defender, ATTACK_EFFECT_PUNCH);
        defender.visible_message(
            span_danger(I18n.T("[] violently twists []'s []!", attacker, defender, affecting)),
            span_userdanger(I18n.T("[] violently twists your []!", attacker, affecting)),
            span_hear(I18n.T("You hear a sickening sound of bone snapping!")),
            null,
            attacker);
        to_chat(attacker, span_danger(I18n.T("You violently twist []'s []!", defender, affecting)));
        playsound(defender, "sound/items/weapons/punch1.ogg", 25, TRUE, -1);
        log_combat(attacker, defender, "wrist wrenched (Sleeping Carp)");
        await defender.apply_damage(20, BRUTE, affecting, wound_bonus: 30);
        await defender.drop_all_held_items();
        return TRUE;
    }

    /// Crashing Wave Kick: Harm Disarm combo, throws people seven tiles backwards
    // tg: code/datums/martial/sleeping_carp.dm:98
    public virtual async DmTask<object> launch_kick(mob_living attacker = null, mob_living defender = null)
    {
        await attacker.do_attack_animation(defender, ATTACK_EFFECT_KICK);
        defender.visible_message(
            span_warning(I18n.T("[] kicks [] square in the chest, sending them flying!", attacker, defender)),
            span_userdanger(I18n.T("You are kicked square in the chest by [], sending you flying!", attacker)),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
            COMBAT_MESSAGE_RANGE,
            attacker);
        playsound(attacker, "sound/effects/hit_kick.ogg", 50, TRUE, -1);
        var throw_target = get_edge_target_turf(defender, attacker.dir) as atom;
        await defender.throw_at(throw_target, 7, 4, attacker);
        await defender.apply_damage(15, Dm.str(attacker.get_attack_type()), BODY_ZONE_CHEST, wound_bonus: CANT_WOUND);
        log_combat(attacker, defender, "launchkicked (Sleeping Carp)");
        return TRUE;
    }

    /// Keelhaul: Disarm Disarm combo, knocks people down and deals substantial stamina damage, and also discombobulates them. Knocks objects out of their hands if they're already on the ground.
    // tg: code/datums/martial/sleeping_carp.dm:115
    public virtual async DmTask<object> drop_kick(mob_living attacker = null, mob_living defender = null)
    {
        await attacker.do_attack_animation(defender, ATTACK_EFFECT_KICK);
        playsound(attacker, "sound/effects/hit_kick.ogg", 50, TRUE, -1);
        if (Dm.eq(defender.body_position, STANDING_UP))
        {
            await defender.Knockdown(4 * SECONDS);
            defender.visible_message(span_warning(I18n.T("[] kicks [] in the head, sending them face first into the floor!", attacker, defender)),
                        span_userdanger(I18n.T("You are kicked in the head by [], sending you crashing to the floor!", attacker)), span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")), COMBAT_MESSAGE_RANGE, attacker);
        }
        else
        {
            await defender.drop_all_held_items();
            defender.visible_message(span_warning(I18n.T("[] kicks [] in the head!", attacker, defender)),
                        span_userdanger(I18n.T("You are kicked in the head by []!", attacker)), span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")), COMBAT_MESSAGE_RANGE, attacker);
        }
        await defender.apply_damage(40, STAMINA);
        await defender.adjust_dizzy_up_to(10 * SECONDS, 10 * SECONDS);
        await defender.adjust_temp_blindness_up_to(2 * SECONDS, 10 * SECONDS);
        log_combat(attacker, defender, "dropkicked (Sleeping Carp)");
        return TRUE;
    }

    /// Kraken Wrack: Grab Harm combo, causes them to be silenced and briefly stunned, as well as doing a moderate amount of stamina damage.
    // tg: code/datums/martial/sleeping_carp.dm:134
    public virtual async DmTask<object> knee_stomach(mob_living attacker = null, mob_living defender = null)
    {
        await attacker.do_attack_animation(defender, ATTACK_EFFECT_KICK);
        playsound(attacker, "sound/effects/hit_kick.ogg", 50, TRUE, -1);
        defender.visible_message(
            span_warning(I18n.T("[] violently slams [] knee into []!", attacker, attacker.p_their(), defender)),
            span_userdanger(I18n.T("You slam your knee straight into []!", defender)),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
            COMBAT_MESSAGE_RANGE,
            attacker);
        await defender.apply_damage(20, STAMINA);
        await defender.Paralyze(1 * SECONDS);
        await defender.adjust_silence_up_to(5 * SECONDS, 5 * SECONDS);
        if (Dm.num(defender.losebreath) <= 10)
            defender.losebreath = clamp(Dm.num(defender.losebreath) + 5, 0, 10);
        log_combat(attacker, defender, "kneed in the stomach (Sleeping Carp)");
        return TRUE;
    }

    // tg: code/datums/martial/sleeping_carp.dm:152
    public override async DmTask<object> grab_act(mob_living attacker = null, mob_living defender = null)
    {
        if (!Dm.truthy(can_deflect(attacker))) //allows for deniability
            return MARTIAL_ATTACK_INVALID;

        if (Dm.truthy(await defender.check_block(attacker, 0, Dm.text("[]'s grab", attacker), UNARMED_ATTACK)))
            return MARTIAL_ATTACK_FAIL;

        await add_to_streak("G", defender);
        if (Dm.truthy(await check_streak(attacker, defender)))
            return MARTIAL_ATTACK_SUCCESS;

        string grab_log_description = "grabbed";
        await attacker.do_attack_animation(defender, ATTACK_EFFECT_PUNCH);
        playsound(defender, "sound/items/weapons/punch1.ogg", 25, TRUE, -1);
        if (!IS_UNCONSCIOUS(defender) && Dm.num(defender.get_stamina_loss()) >= 80) //We put our target to sleep.
        {
            defender.visible_message(
                span_danger(I18n.T("[] carefully pinch a nerve in []'s neck, knocking them out cold!", attacker, defender)),
                span_userdanger(I18n.T("[] pinches something in your neck, and you fall unconscious!", attacker)));
            grab_log_description = "grabbed and nerve pinched";
            await defender.Unconscious(10 * SECONDS);
        }
        await defender.apply_damage(20, STAMINA);
        log_combat(attacker, defender, Dm.text("[] (Sleeping Carp)", grab_log_description));
        return MARTIAL_ATTACK_INVALID; // normal grab
    }

    // tg: code/datums/martial/sleeping_carp.dm:177
    public override async DmTask<object> harm_act(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.eq(attacker.grab_state, GRAB_KILL)
            && Dm.eq(attacker.zone_selected, BODY_ZONE_HEAD)
            && attacker.pulling == defender
            && !Dm.eq(defender.stat, DEAD))
        {
            var head = defender.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
            if (head != null)
            {
                playsound(defender, "sound/effects/wounds/crack1.ogg", 100);
                defender.visible_message(
                    span_danger(I18n.T("[] snaps the neck of []!", attacker, defender)),
                    span_userdanger(I18n.T("Your neck is snapped by []!", attacker)),
                    span_hear(I18n.T("You hear a sickening snap!")),
                    ignored_mobs: attacker);
                to_chat(attacker, span_danger(I18n.T("In a swift motion, you snap the neck of []!", defender)));
                log_combat(attacker, defender, "snapped neck");
                await defender.apply_damage(100, BRUTE, BODY_ZONE_HEAD, wound_bonus: CANT_WOUND);
                if (!HAS_TRAIT(defender, TRAIT_NODEATH))
                {
                    await defender.death();
                    defender.investigate_log(Dm.text("has had [] neck snapped by [].", defender.p_their(), attacker), INVESTIGATE_DEATHS);
                }
                return MARTIAL_ATTACK_SUCCESS;
            }
        }

        if (Dm.truthy(await defender.check_block(attacker, 10, attacker.name, UNARMED_ATTACK)))
            return MARTIAL_ATTACK_FAIL;

        await add_to_streak("H", defender);
        if (Dm.truthy(await check_streak(attacker, defender)))
            return MARTIAL_ATTACK_SUCCESS;

        return MARTIAL_ATTACK_INVALID; // normal punch
    }

    // tg: code/datums/martial/sleeping_carp.dm:208
    public override async DmTask<object> disarm_act(mob_living attacker = null, mob_living defender = null)
    {
        if (!Dm.truthy(can_deflect(attacker))) //allows for deniability
            return MARTIAL_ATTACK_INVALID;
        if (Dm.truthy(await defender.check_block(attacker, 0, attacker.name, UNARMED_ATTACK)))
            return MARTIAL_ATTACK_FAIL;

        await add_to_streak("D", defender);
        if (Dm.truthy(await check_streak(attacker, defender)))
            return MARTIAL_ATTACK_SUCCESS;

        await attacker.do_attack_animation(defender, ATTACK_EFFECT_PUNCH);
        playsound(defender, "sound/items/weapons/punch1.ogg", 25, TRUE, -1);
        await defender.apply_damage(20, STAMINA);
        log_combat(attacker, defender, "disarmed (Sleeping Carp)");
        return MARTIAL_ATTACK_INVALID; // normal disarm
    }

    // tg: code/datums/martial/sleeping_carp.dm:224
    public virtual object can_deflect(mob_living carp_user = null)
    {
        if (!Dm.truthy(can_use(carp_user)) || !Dm.truthy(carp_user.combat_mode))
            return FALSE;
        if (Dm.truthy(INCAPACITATED_IGNORING(carp_user, INCAPABLE_GRAB))) //NO STUN
            return FALSE;
        if (!Dm.truthy(Dm.bitand(carp_user.mobility_flags, MOBILITY_USE))) //NO UNABLE TO USE
            return FALSE;
        if (HAS_TRAIT(carp_user, TRAIT_HULK)) //NO HULK
            return FALSE;
        if (!isturf(carp_user.loc)) //NO MOTHERFLIPPIN MECHS!
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/martial/sleeping_carp.dm:237
    [SignalHandler]
    public virtual object hit_by_projectile(mob_living carp_user = null, obj_projectile hitting_projectile = null, object def_zone = null)
    {
        float determine_avoidance = 100;
        float additional_adjustments = 0;

        if (hitting_projectile is obj_projectile_bullet_c38_match_true) // 75% chance to ignore evasion
            additional_adjustments -= 75;

        determine_avoidance = clamp(determine_avoidance + carp_style_check(carp_user) + additional_adjustments, 0, 100);

        if (hitting_projectile is obj_projectile_bullet_harpoon) // WHITE WHALE HOLY GRAIL
            return NONE;

        if (!Dm.truthy(can_deflect(carp_user)))
            return NONE;

        if (!prob(determine_avoidance))
            return NONE;

        carp_user.visible_message(
            span_danger(I18n.T("[] effortlessly swats [] aside! [] can block bullets with [] bare hands!", carp_user, hitting_projectile, carp_user.p_They(), carp_user.p_their())),
            span_userdanger(I18n.T("You deflect []!", hitting_projectile)));
        playsound(carp_user, SFX_BULLET_MISS, 75, TRUE);
        hitting_projectile.firer = carp_user;
        hitting_projectile.set_angle(rand(0, 360)); //SHING
        return COMPONENT_BULLET_PIERCED;
    }

    /// Signal from getting attacked with an item, for a special interaction with touch spells
    // tg: code/datums/martial/sleeping_carp.dm:268
    [SignalHandler]
    public virtual object on_attackby(mob_living carp_user = null, obj_item attack_weapon = null, mob attacker = null, DmList modifiers = null)
    {
        if (attack_weapon is not obj_item_melee_touch_attack touch_weapon)
            return null;
        if (!Dm.truthy(can_deflect(carp_user)))
            return null;
        carp_user.visible_message(
            span_danger(I18n.T("[] carefully dodges []'s []!", carp_user, attacker, touch_weapon)),
            span_userdanger(I18n.T("You take great care to remain untouched by []'s []!", attacker, touch_weapon)));
        return COMPONENT_NO_AFTERATTACK;
    }

    /// If our user has committed to being as martial arty as they can be, they may be able to avoid incoming attacks.
    // tg: code/datums/martial/sleeping_carp.dm:283
    [SignalHandler]
    public virtual object check_dodge(mob_living carp_user = null, atom_movable hitby = null, object damage = null, object attack_text = null, object attack_type = null, params object[] __extra)
    {
        float determine_avoidance = clamp(round(carp_style_check(carp_user) / (Dm.eq(attack_type, OVERWHELMING_ATTACK) ? 2 : 1), 1), 0, 75);

        if (!Dm.truthy(can_deflect(carp_user)))
            return null;

        if (Dm.eq(attack_type, PROJECTILE_ATTACK) || Dm.eq(attack_type, THROWN_PROJECTILE_ATTACK))
            return NONE;

        if (!prob(determine_avoidance))
            return NONE;

        carp_user.visible_message(
            span_danger(I18n.T("[] cleanly avoids [] with incredible speed!", carp_user, attack_text)),
            span_userdanger(I18n.T("You dodge []", attack_text)));
        playsound(carp_user.loc, "sound/items/weapons/punchmiss.ogg", 25, TRUE, -1);
        return SUCCESSFUL_BLOCK;
    }

    /* Determines how 'carp-y' or how 'martial arts-y' we are, granting us the ability to avoid attacks.
    * At a baseline, we will always avoid projectile attacks, but we may not necessarily avoid other attacks.
    * If we wear carp based clothing, or martial arts based clothing, we improve our style factor.
    * If we are a carp mutant, we improve our style factor.
    * If we literally are a carp, we just assume we're very carpy and return our max value.
    * If we wear a lot of armor, we reduce our style factor. Some martial arts or carp items may result in a net netural bonus.
    * If there is anything in our hands, we're also less likely to avoid attacks.
    */
    // tg: code/datums/martial/sleeping_carp.dm:314
    public virtual float carp_style_check(mob_living carp_user = null)
    {
        // An evaluation of how 'carp' we are.
        float style_factor_points = 0;

        if (carp_user is mob_living_basic_space_dragon || carp_user is mob_living_basic_carp)
            return 100;

        if (carp_user is not mob_living_carbon_human human_carp_user) // We're not concerned about nonhumans here, we can assume we've covered any relevant mobs by checking for carp.
            return 0;

        var potential_head = human_carp_user.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
        var potential_chest = human_carp_user.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart;
        var is_it_the_shoes = human_carp_user.get_item_by_slot(ITEM_SLOT_FEET) as obj_item_clothing;

        // The presence of armor and heavy objects is a style malus
        float style_factor_malus = 0;

        // Lets look to see if any relevant headwear is armored or on theme
        if (potential_head != null)
        {
            foreach (obj_item_clothing possible_headbands in Dm.each<obj_item_clothing>(human_carp_user.get_clothing_on_part(potential_head)))
            {
                if (Dm.truthy(Dm.bitand(possible_headbands.clothing_flags, CARP_STYLE_FACTOR)))
                    style_factor_points += 20; // Basically, you only need one chest level item to contribute
            }
            style_factor_malus += Dm.num(human_carp_user.run_armor_check(potential_head, MELEE));
        }

        // Then let's look for any chest clothing that is either armored or on theme
        if (potential_chest != null)
        {
            foreach (obj_item_clothing possible_gi in Dm.each<obj_item_clothing>(human_carp_user.get_clothing_on_part(potential_chest)))
            {
                if (Dm.truthy(Dm.bitand(possible_gi.clothing_flags, CARP_STYLE_FACTOR)))
                    style_factor_points += 20; // Only need one head level item to contribute
            }
            style_factor_malus += Dm.num(human_carp_user.run_armor_check(potential_chest, MELEE));
        }

        // We also consider whether our footwear is appropriate
        if (is_it_the_shoes != null && Dm.truthy(Dm.bitand(is_it_the_shoes.clothing_flags, CARP_STYLE_FACTOR)))
            style_factor_points += 20;

        // Achieved a carp state of mind.
        if (Dm.truthy(human_carp_user.has_status_effect(typeof(datum_status_effect_organ_set_bonus_carp))))
            style_factor_points += 20;

        // We check for wielded objects. If they're not abstract items or exempt items, we add their weight as a penalty. And their block chance.
        foreach (obj_item possibly_a_held_object in Dm.each<obj_item>(human_carp_user.held_items))
        {
            if (Dm.truthy(Dm.bitand(possibly_a_held_object.item_flags, Dm.bitor(ABSTRACT, HAND_ITEM))) && !Dm.truthy(possibly_a_held_object.block_chance))
                continue;

            if (Dm.@in(possibly_a_held_object, exempt_objects))
                continue;

            if (Dm.num(possibly_a_held_object.w_class) <= WEIGHT_CLASS_SMALL && !Dm.truthy(possibly_a_held_object.block_chance))
                continue;

            style_factor_malus += Dm.num(possibly_a_held_object.block_chance);
            style_factor_malus += Dm.num(possibly_a_held_object.w_class) * 10 * (HAS_TRAIT(possibly_a_held_object, TRAIT_WIELDED) ? 2 : 1);
        }

        if (!Dm.eq(human_carp_user.body_position, STANDING_UP)) // this ain't monkey style
            style_factor_points -= 30;

        style_factor_points -= style_factor_malus;

        return style_factor_points;
    }

    /// Verb added to humans who learn the art of the sleeping carp.
    // tg: code/datums/martial/sleeping_carp.dm:378
    public override object get_style_help()
    {
        DmList dot = Dm.list();

        Dm.append(dot, span_info(Dm.text("<b><i>You retreat inward and recall the teachings of the Sleeping Carp...</i></b>\n[]: Punch Grab. Violently twists your opponent's arm, dislocating or even shattering bone and forcing them to drop their held items.\n[]: Punch Shove. Launch your opponent away from you with incredible force!\n[]: Shove Shove. Nonlethally kick an opponent to the floor, knocking them down, discombobulating them and dealing substantial stamina damage. If they're already prone, disarm them as well.\n[]: Grab Punch. Deliver a knee jab into the opponent, dealing high stamina damage, as well as briefly stunning them, winding them and making it difficult for them to speak.\n[]: While in combat mode, your typical grab and shove do decent stamina damage, and your grabs harder to break. If you grab someone who has substantial amounts of stamina damage, you knock them out!\n<span class='notice'>While in combat mode (and not stunned, not a hulk, and not in a mech), you can reflect all projectiles that come your way, sending them back at the people who fired them! \nHowever, your ability to avoid projectiles is negatively affected when your are burdened by armor, or whenever you are carrying normal-sized or heavier objects in your hands. \nBut if you commmit fully to the martial arts lifestyle by wearing martial arts or carp-related regalia, you will feel empowered enough to potentially avoid attacks even from melee weapons or other unarmed combatants. \nSome melee weapons, such as bo starves, spears, short blades, knives, toolboxes, baseball bats and non-blocking small objects are safe to carry without affecting your ability to defend yourself. Exploit this for a tactical advantage. \nAlso, you are more resilient against suffering wounds in combat, and your limbs cannot be dismembered. This grants you extra staying power during extended combat, especially against slashing and other bleeding weapons. \nYou are not invincible, however- while you may not suffer debilitating wounds often, you must still watch your health and should have appropriate medical supplies for use during downtime. \nIn addition, your training has imbued you with a loathing of guns, and you can no longer use them.</span>",
            span_notice("Gnashing Teeth"), span_notice("Crashing Wave Kick"), span_notice("Keelhaul"), span_notice("Kraken Wrack"), span_notice("Grabs and Shoves"))));
        return dot;
    }
}

// tg: code/datums/martial/sleeping_carp.dm:396
public partial class obj_item_staff_bostaff : obj_item_staff
{
    // tg: code/datums/martial/sleeping_carp.dm:414
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed),
            Dm.named("force_unwielded", 10),
            Dm.named("force_wielded", 24)));
        return dot;
    }

    // tg: code/datums/martial/sleeping_carp.dm:420
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        inhand_icon_state = Dm.text("[][]", base_icon_state, HAS_TRAIT(this, TRAIT_WIELDED) ? 1 : 0);
        icon_state = Dm.str(inhand_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/datums/martial/sleeping_carp.dm:424
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);
        if (HAS_TRAIT(user, TRAIT_CLUMSY) && prob(50))
        {
            to_chat(user, span_warning(I18n.T("You club yourself over the head with [].", this)));
            await user.Paralyze(6 * SECONDS);
            if (user is mob_living_carbon_human H)
                await H.apply_damage(2 * force, BRUTE, BODY_ZONE_HEAD);
            else
                await user.take_bodypart_damage(2 * force);
            return null;
        }
        if (iscyborg(target_mob))
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (!isliving(target_mob))
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        var C = target_mob as mob_living_carbon;
        if (IS_UNCONSCIOUS_OR_CRIT(C))
        {
            to_chat(user, span_warning(I18n.T("It would be dishonorable to attack a foe while they cannot retaliate.")));
            return null;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
        {
            if (!HAS_TRAIT(this, TRAIT_WIELDED))
                return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
            if (target_mob is not mob_living_carbon_human H)
                return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
            DmList fluffmessages = Dm.list("club", "smack", "broadside", "beat", "slam");
            H.visible_message(span_warning(I18n.T("[] []s [] with []!", user, pick(fluffmessages), H, this)),
                            span_userdanger(I18n.T("[] []s you with []!", user, pick(fluffmessages), this)), span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")), null, user);
            to_chat(user, span_danger(I18n.T("You [] [] with []!", pick(fluffmessages), H, this)));
            playsound(get_turf(user), "sound/effects/woodhit.ogg", 75, TRUE, -1);
            await H.adjust_stamina_loss(rand(13, 20));
            if (prob(10))
            {
                H.visible_message(span_warning(I18n.T("[] collapses!", H)),
                                span_userdanger(I18n.T("Your legs give out!")));
                await H.Paralyze(8 * SECONDS);
            }
            if (Dm.truthy(H.staminaloss) && !Dm.truthy(H.IsSleeping()))
            {
                float total_health = Dm.num(H.health) - Dm.num(H.staminaloss);
                if (total_health <= Dm.num(H.crit_threshold) && !IS_UNCONSCIOUS_OR_CRIT(H))
                {
                    H.visible_message(span_warning(I18n.T("[] delivers a heavy hit to []'s head, knocking [] out cold!", user, H, H.p_them())),
                                    span_userdanger(I18n.T("You're knocked unconscious by []!", user)), span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")), null, user);
                    to_chat(user, span_danger(I18n.T("You deliver a heavy hit to []'s head, knocking [] out cold!", H, H.p_them())));
                    await H.SetSleeping(60 * SECONDS);
                    await H.adjust_organ_loss(ORGAN_SLOT_BRAIN, 15, 150);
                }
            }
        }
        else
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        return null;
    }

    // tg: code/datums/martial/sleeping_carp.dm:470
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        if (!HAS_TRAIT(this, TRAIT_WIELDED))
            return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
        return FALSE;
    }
}

// tg: code/datums/martial/sleeping_carp.dm:475
public partial class obj_item_clothing_gloves_the_sleeping_carp : obj_item_clothing_gloves
{
    // tg: code/datums/martial/sleeping_carp.dm:485
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_martial_art_giver), typeof(datum_martial_art_the_sleeping_carp)));
        return dot;
    }
}
