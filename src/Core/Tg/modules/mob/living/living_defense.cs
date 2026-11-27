// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/living_defense.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed in M2 (living mobs); M7 EMP: get_emp_protection and emp_act; the M7 shocks slice: electrocute_act. The W4.0 contracts declared the /mob/living proc that the pulling code of
// /atom/movable calls (game/atoms_movable.dm:541, :642); no wave-4 package edits this file. Pulling (2026-10-15): grab and
// grabbedby are translated; grippedby (the aggressive grabs) since M9 slice F.

// tg: code/modules/mob/living/living_defense.dm:372
public partial class mob_living : mob
{
    /**
     * Called when this mob is grabbed by another mob.
     */
    // tg: code/modules/mob/living/living_defense.dm:372  (pulling, 2026-10-15: the passive pull; the upgrade is grippedby, M9)
    public virtual async DmTask<object> grabbedby(mob_living user = null, float? supress_message = null)
    {
        supress_message ??= FALSE;
        if (user == this || Dm.truthy(anchored) || !isturf(user.loc))
            return FALSE;

        if (!Dm.truthy(user.pulling) || user.pulling != this)
        {
            await user.start_pulling(this, supress_message: supress_message);
            return null;
        }

        if (!Dm.truthy(Dm.bitand(status_flags, CANPUSH)) || HAS_TRAIT(this, TRAIT_PUSHIMMUNE))
        {
            to_chat(user, span_warning(I18n.T("[] can't be grabbed more aggressively!", this)));
            return FALSE;
        }

        if (user.grab_state >= GRAB_AGGRESSIVE && HAS_TRAIT(user, TRAIT_PACIFISM))
        {
            to_chat(user, span_warning(I18n.T("You don't want to risk hurting []!", this)));
            return FALSE;
        }
        await grippedby(user);
        update_incapacitated();
        return null;
    }
}

// tg: code/modules/mob/living/living_defense.dm:2
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living_defense.dm:2
    public virtual object run_armor_check(object def_zone = null, string attack_flag = null, object absorb_text = null, object soften_text = null, object armour_penetration = null, object penetrated_text = null, float? silent = null, float? weak_against_armour = null)
    {
        attack_flag ??= MELEE;
        silent ??= FALSE;
        weak_against_armour ??= FALSE;
        float our_armor = Dm.num(getarmor(def_zone, attack_flag));

        if (our_armor <= 0)
            return our_armor;
        if (Dm.truthy(weak_against_armour) && our_armor >= 0)
            our_armor *= ARMOR_WEAKENED_MULTIPLIER;
        if (Dm.truthy(silent))
            return max(0, PENETRATE_ARMOUR__local(our_armor, Dm.num(armour_penetration)));

        //the if "armor" check is because this is used for everything on /living, including humans
        if (Dm.truthy(armour_penetration))
        {
            our_armor = max(PENETRATE_ARMOUR__local(our_armor, Dm.num(armour_penetration)), 0);
            if (Dm.truthy(penetrated_text))
                to_chat(this, span_userdanger(Dm.text("[]", penetrated_text)));
            else
                to_chat(this, span_userdanger(I18n.T("Your armor was penetrated!")));
        }
        else if (our_armor >= 100)
        {
            if (Dm.truthy(absorb_text))
                to_chat(this, span_notice(Dm.text("[]", absorb_text)));
            else
                to_chat(this, span_notice(I18n.T("Your armor absorbs the blow!")));
        }
        else
        {
            if (Dm.truthy(soften_text))
                to_chat(this, span_warning(Dm.text("[]", soften_text)));
            else
                to_chat(this, span_warning(I18n.T("Your armor softens the blow!")));
        }
        return our_armor;
    }

    // tg: code/__DEFINES/combat.dm:362  #define PENETRATE_ARMOUR(armour, penetration) (penetration >= 100 ? 0 : 100 * (armour -
    // penetration) / (100 - penetration)). GAP(lead): no Defines member yet: a local copy (I1 cleanup).
    private static float PENETRATE_ARMOUR__local(float armour, float penetration) => penetration >= 100 ? 0 : 100 * (armour - penetration) / (100 - penetration);

    // tg: code/modules/mob/living/living_defense.dm:31
    [ShouldCallParent]
    public virtual object getarmor(object def_zone = null, object type = null)
    {
        float worn_armor = Dm.num(get_worn_armor_value(def_zone, type));
        float inner_armor_percent = min(Dm.num(inner_armor?.get_rating(type)) * 0.01f, 1);
        float not_blocked = (100 - worn_armor) * (1 - inner_armor_percent);
        return 100 - not_blocked;
    }

    // tg: code/modules/mob/living/living_defense.dm:38
    public virtual object get_worn_armor_value(object def_zone = null, object damage_type = null)
    {
        return 0;
    }

    // tg: code/modules/mob/living/living_defense.dm:42
    // Gaps P3b: the roach's infused armour (organ_sets/roach_organs.dm:25). armor_to_add: object (rules 3.2, a typed parameter that
    // takes wider values: the roach passes its armour's typepath, which add_other_armor resolves)
    public virtual void add_inner_armor(object armor_to_add = null)
    {
        if (inner_armor == null)
            inner_armor = Dm.@new<datum_armor>();
        inner_armor = inner_armor.add_other_armor(armor_to_add) as datum_armor;
    }

    // tg: code/modules/mob/living/living_defense.dm:48
    // armor_to_remove: object, as add_inner_armor's (rules 3.2)
    public virtual void remove_inner_armor(object armor_to_remove = null)
    {
        if (inner_armor == null)
            return;
        inner_armor = inner_armor.subtract_other_armor(armor_to_remove) as datum_armor;
        DmList ratings_list = inner_armor.get_rating_list() as DmList;
        foreach (object rating in Dm.each(ratings_list))
        {
            if (!Dm.eq(ratings_list[rating], 0))
                return;
        }
        QDEL_NULL(ref inner_armor); //all ratings are 0, meaning we no longer have an intrinsic armor of some kind.
    }

    // tg: code/modules/mob/living/living_defense.dm:59
    /// This returns the mob's protection against eye damage (number between -1 and 2) from bright lights
    public virtual object get_eye_protection()
    {
        return 0;
    }

    /// This returns a number is subtracted from the severity of incoming emps against this mob.
    // tg: code/modules/mob/living/living_defense.dm:63  (M7 EMP)
    public virtual object get_emp_protection()
    {
        return emp_protection;
    }

    // tg: code/modules/mob/living/living_defense.dm:67
    /// An easy to use proc to apply both organ damage and temporary deafness at once, so you don't have to get the ears everytime.
    public virtual async DmTask sound_damage(object damage = null, object deafen = null)
    {
        return;
    }

    // tg: code/modules/mob/living/living_defense.dm:71
    /// This returns the mob's protection against ear damage (0:no protection; 1: some ear protection; 2: has no ears)
    public virtual object get_ear_protection(float? ignore_deafness = null)
    {
        ignore_deafness ??= FALSE;
        if (!Dm.truthy(ignore_deafness) && HAS_TRAIT(this, TRAIT_DEAF))
            return INFINITY; //For all my homies that can not hear in the world
        DmList sig_protection = Dm.list(0);
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_GET_EAR_PROTECTION]))
            _SendSignal(COMSIG_LIVING_GET_EAR_PROTECTION, Dm.list(this, sig_protection));
        float protection = Dm.num(sig_protection[EAR_PROTECTION_ARG]);
        turf current_turf = get_turf(this);
        datum_gas_mixture environment = current_turf == null ? null : Waiting.NoSleep(current_turf.return_air()) as datum_gas_mixture;
        object pressure = environment?.return_pressure();
        if (Dm.lt(pressure, SOUND_MINIMUM_PRESSURE)) //space is empty
            protection += EAR_PROTECTION_VACUUM;
        return protection;
    }

    // tg: code/modules/mob/living/living_defense.dm:95
    public virtual object is_mouth_covered(float? check_flags = null)
    {
        return null;
    }

    // tg: code/modules/mob/living/living_defense.dm:109
    public virtual object is_eyes_covered(float? check_flags = null)
    {
        return null;
    }

    // tg: code/modules/mob/living/living_defense.dm:123
    public virtual object is_pepper_proof(float? check_flags = null)
    {
        return null;
    }

    // tg: code/modules/mob/living/living_defense.dm:128
    /// Checks if the mob's ears (BOTH EARS, BOWMANS NEED NOT APPLY) are covered by something.
    /// Returns the atom covering the mob's ears, or null if their ears are uncovered.
    public virtual object is_ears_covered()
    {
        return null;
    }

    // tg: code/modules/mob/living/living_defense.dm:142
    public virtual object is_location_accessible(object location = null, float? exluded_equipment_slots = null)
    {
        exluded_equipment_slots ??= NONE;
        return TRUE;
    }

    // tg: code/modules/mob/living/living_defense.dm:145  (the M7 projectiles slice)
    public override async DmTask<object> bullet_act(obj_projectile proj = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        piercing_hit ??= FALSE;
        blocked ??= 0;
        object dot = null;
        dot = await base.bullet_act(proj, def_zone, piercing_hit, blocked);
        if (!Dm.eq(dot, BULLET_ACT_HIT))
            return dot;

        if (Dm.num(blocked) >= 100)
        {
            if (Dm.truthy(proj.is_hostile_projectile()))
                await apply_projectile_effects(proj, def_zone, blocked);
            return dot;
        }

        object hit_limb_zone = check_hit_limb_zone_name(def_zone);
        string organ_hit_text = "";
        if (Dm.truthy(hit_limb_zone))
            organ_hit_text = I18n.T(" in \\the []", parse_zone_with_bodypart(hit_limb_zone));

        switch (proj.suppressed)
        {
            case SUPPRESSED_QUIET:
                to_chat(this, span_userdanger(I18n.T("You're shot by \\a [][]!", proj, organ_hit_text)));
                break;
            case SUPPRESSED_NONE:
                visible_message(span_danger(I18n.T("[] is hit by \\a [][]!", this, proj, organ_hit_text)),
                        span_userdanger(I18n.T("You're hit by \\a [][]!", proj, organ_hit_text)), null, COMBAT_MESSAGE_RANGE);
                if (Dm.truthy(is_blind()))
                    to_chat(this, span_userdanger(I18n.T("You feel something hit you[]!", organ_hit_text)));
                break;
        }

        if (Dm.truthy(proj.is_hostile_projectile()))
            await apply_projectile_effects(proj, def_zone, blocked);
        return dot;
    }

    // tg: code/modules/mob/living/living_defense.dm:172  (the M7 projectiles slice)
    public virtual async DmTask apply_projectile_effects(obj_projectile proj = null, object def_zone = null, object armor_check = null)
    {
        float damage_dealt = Dm.num(await apply_damage(
            damage: proj.damage,
            damagetype: proj.damage_type,
            def_zone: def_zone,
            blocked: Dm.num_or_null(min(ARMOR_MAX_BLOCK, armor_check)),  //cap damage reduction at 90%
            wound_bonus: proj.wound_bonus,
            exposed_wound_bonus: proj.exposed_wound_bonus,
            sharpness: proj.sharpness,
            attack_direction: get_dir(proj.starting, this),
            attacking_item: proj
        ));

        if (proj.damage_type == BRUTE && damage_dealt >= 10 && proj.speed >= 1 && prob(0.1f))
        {
            obj_item_organ_brain a_brain = Dm.locate<obj_item_organ_brain>(get_bodypart((string)def_zone));
            a_brain?.cure_trauma_type(resilience: TRAUMA_RESILIENCE_LOBOTOMY);
        }

        await apply_effects(
            stun: proj.stun,
            knockdown: proj.knockdown,
            unconscious: proj.unconscious,
            slur: (Dm.truthy(Dm.bitand(mob_biotypes, MOB_ROBOTIC)) ? 0 * SECONDS : proj.slur), // Don't want your cyborgs to slur from being ebow'd
            stutter: (Dm.truthy(Dm.bitand(mob_biotypes, MOB_ROBOTIC)) ? 0 * SECONDS : proj.stutter), // Don't want your cyborgs to stutter from being tazed
            eyeblur: proj.eyeblur,
            drowsy: proj.drowsy,
            blocked: Dm.num_or_null(armor_check),
            stamina: proj.stamina,
            jitter: (Dm.truthy(Dm.bitand(mob_biotypes, MOB_ROBOTIC)) ? 0 * SECONDS : proj.jitter), // Cyborgs can jitter but not from being shot
            paralyze: proj.paralyze,
            immobilize: proj.immobilize
        );

        if (Dm.truthy(proj.dismemberment))
            await check_projectile_dismemberment(proj, def_zone);

        if (Dm.truthy(proj.damage) && Dm.lt(armor_check, 100))
            await create_projectile_hit_effects(proj, def_zone, armor_check);

        if (Dm.truthy(proj.fired_from))
        {
            if (Dm.truthy(proj.fired_from._listen_lookup?[COMSIG_PROJECTILE_POST_HIT_LIVING]))
                proj.fired_from._SendSignal(COMSIG_PROJECTILE_POST_HIT_LIVING, Dm.list(proj.fired_from, this, def_zone, armor_check));
        }
        if (Dm.truthy(proj._listen_lookup?[COMSIG_PROJECTILE_SELF_POST_HIT_LIVING]))
            proj._SendSignal(COMSIG_PROJECTILE_SELF_POST_HIT_LIVING, Dm.list(proj, this, def_zone, armor_check));
    }

    // tg: code/modules/mob/living/living_defense.dm:214  (the M7 projectiles slice)
    public virtual async DmTask create_projectile_hit_effects(obj_projectile proj = null, object def_zone = null, object blocked = null)
    {
        if (proj.damage_type != BRUTE)
            return;

        obj_item_bodypart hit_bodypart = get_bodypart((string)check_hit_limb_zone_name(def_zone)) as obj_item_bodypart;
        if (Dm.truthy(get_blood_volume()) && (hit_bodypart == null || Dm.truthy(hit_bodypart.can_bleed())))
        {
            await create_splatter(angle2dir(Dm.num(proj.angle)));
            if (prob(33))
                await add_splatter_floor(get_turf(this));
            return;
        }

        if (Dm.truthy(Dm.bitand(hit_bodypart?.biological_state, Dm.bitor(BIO_METAL, BIO_WIRED))))
        {
            float random_damage_mult = RANDOM_DECIMAL(0.85f, 1.15f); // SOMETIMES you can get more or less sparks
            float damage_dealt = (Dm.div(Dm.num(proj.damage), (1 - (Dm.num(blocked) / 100))) * random_damage_mult);

            float spark_amount = round((damage_dealt / PROJECTILE_DAMAGE_PER_ROBOTIC_SPARK));
            if (spark_amount > 0)
                do_sparks(spark_amount, FALSE, this);
        }
    }

    // tg: code/modules/mob/living/living_defense.dm:233  (the M7 projectiles slice)
    public override object check_projectile_armor(object def_zone = null, obj_projectile impacting_projectile = null, object is_silent = null)
    {
        return run_armor_check(def_zone, impacting_projectile.armor_flag as string, "", "", impacting_projectile.armour_penetration, "", Dm.num_or_null(is_silent), impacting_projectile.weak_against_armour);
    }

    // tg: code/modules/mob/living/living_defense.dm:236  (the M7 projectiles slice)
    public virtual async DmTask check_projectile_dismemberment(obj_projectile proj = null, object def_zone = null)
    {
        return;
    }

    // tg: code/modules/mob/living/living_defense.dm:246
    public virtual object set_combat_mode(object new_mode = null, float? silent = null, float? force = null)
    {
        silent ??= TRUE;
        force ??= FALSE;
        object dot = null;

        if (HAS_TRAIT(this, TRAIT_COMBAT_MODE_LOCK) && !Dm.truthy(force))
            return null;

        if (Dm.eq(combat_mode, new_mode))
            return null;
        dot = combat_mode;
        combat_mode = Dm.num_or_null(new_mode);
        if (Dm.truthy(_listen_lookup?[COMSIG_COMBAT_MODE_TOGGLED]))
            _SendSignal(COMSIG_COMBAT_MODE_TOGGLED, Dm.list(this));
        (hud_used?.screen_objects[HUD_MOB_INTENTS] as atom)?.update_appearance();
        if (Dm.truthy(silent) || !Dm.truthy(client?.prefs?.read_preference(typeof(datum_preference_toggle_sound_combatmode))))
            return dot;
        if (Dm.truthy(combat_mode))
            Dm.output_to(this, sound(Dm.resource("sound/misc/ui_togglecombat.ogg"), volume: 25)); // SEND_SOUND (DIRECT_OUTPUT, rules 12): Sound from interbay!
        else
            Dm.output_to(this, sound(Dm.resource("sound/misc/ui_toggleoffcombat.ogg"), volume: 25)); // SEND_SOUND: Slightly modified version of the above
        return dot;
    }

    // tg: code/modules/mob/living/living_defense.dm:326
    public virtual object log_hit_combat(mob thrown_by = null, obj_item thrown_item = null)
    {
        if (Dm.truthy(thrown_by))
        {
            log_combat(thrown_by, this, "threw and hit", thrown_item);
            return null;
        }
        log_combat(thrown_item, this, "hit ");
        return null;
    }

    ///The core of catching thrown items, which non-carbons cannot without the help of items or abilities yet, as they've no throw mode.
    // tg: code/modules/mob/living/living_defense.dm:332
    public virtual object try_catch_item(obj_item item = null, float? skip_throw_mode_check = null, float? try_offhand = null)
    {
        skip_throw_mode_check ??= FALSE;
        try_offhand ??= FALSE;
        if (!Dm.truthy(can_catch_item(skip_throw_mode_check, try_offhand)) || !isitem(item) || HAS_TRAIT(item, TRAIT_UNCATCHABLE) || !isturf(item.loc))
            return FALSE;
        if (!Dm.truthy(can_hold_items(item)))
            return FALSE;
        Waiting.Detach(item.attempt_pickup(this, TRUE));
        if (get_active_held_item() == item) //if our attack_hand() picks up the item...
        {
            visible_message(span_warning(I18n.T("[] catches []!", this, item)),
                            span_userdanger(I18n.T("You catch [] in mid-air!", item)));
            return TRUE;
        }
        return null;
    }

    ///Checks the requites for catching a throw item.

    // tg: code/modules/mob/living/living_defense.dm:351  (M4b slice 3)
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        object dot = await base.fire_act(exposed_temperature, exposed_volume);
        await adjust_fire_stacks(3);
        ignite_mob();
        return dot;
    }
    // tg: code/modules/mob/living/living_defense.dm:344
    public virtual object can_catch_item(float? skip_throw_mode_check = null, float? try_offhand = null)
    {
        skip_throw_mode_check ??= FALSE;
        try_offhand ??= FALSE;
        if (HAS_TRAIT(this, TRAIT_HANDS_BLOCKED))
            return FALSE;
        if (Dm.truthy(get_active_held_item()) && (!Dm.truthy(try_offhand) || Dm.truthy(get_inactive_held_item()) || !Dm.truthy(swap_hand())))
            return FALSE;
        return TRUE;
    }

    /**
     * Called when a mob is grabbing another mob.
     */
    // tg: code/modules/mob/living/living_defense.dm:359  (pulling, 2026-10-15)
    public virtual async DmTask<object> grab(mob_living target = null)
    {
        if (!(target is mob_living))
            return GRAB_SKIP;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_LIVING_GRAB]) ? (object)NONE : this._SendSignal(COMSIG_LIVING_GRAB, Dm.list(this, target))), Dm.bitor(COMPONENT_CANCEL_ATTACK_CHAIN, COMPONENT_SKIP_ATTACK))))
            return GRAB_FAILURE;
        if (Dm.truthy(await target.check_block(this, 0, Dm.text("[]'s grab", this), UNARMED_ATTACK)))
            return GRAB_FAILURE;
        await target.grabbedby(this);
        return GRAB_SUCCESS;
    }

    //proc to upgrade a simple pull into a more aggressive grab.
    // tg: code/modules/mob/living/living_defense.dm:392  (M9 slice F)
    public virtual async DmTask<object> grippedby(mob_living user = null, float? instant = null)
    {
        instant ??= FALSE;
        if (user.grab_state >= user.max_grab)
            return null;
        user.changeNext_move(CLICK_CD_GRABBING);
        object sound_to_play = Dm.resource("sound/items/weapons/thudswoosh.ogg");
        if (ishuman(user))
        {
            mob_living_carbon_human H = (mob_living_carbon_human)user;
            if (Dm.truthy((H.dna.species as datum_species)?.grab_sound))
                sound_to_play = (H.dna.species as datum_species).grab_sound;
        }
        playsound(this.loc, sound_to_play, 50, TRUE, -1);

        if (user.grab_state != 0) //only the first upgrade is instantaneous
        {
            float old_grab_state = user.grab_state;
            float grab_upgrade_time = Dm.truthy(instant) ? 0 : 30;
            visible_message(span_danger(I18n.T("[] starts to tighten [] grip on []!", user, user.p_their(), this)),
                            span_userdanger(I18n.T("[] starts to tighten [] grip on you!", user, user.p_their())), span_hear(I18n.T("You hear aggressive shuffling!")), null, user);
            to_chat(user, span_danger(I18n.T("You start to tighten your grip on []!", this)));
            switch (user.grab_state)
            {
                case GRAB_AGGRESSIVE:
                    log_combat(user, this, "attempted to neck grab", addition: "neck grab");
                    break;
                case GRAB_NECK:
                    log_combat(user, this, "attempted to strangle", addition: "kill grab");
                    break;
            }
            if (!Dm.truthy(await do_after(user, grab_upgrade_time, this)))
                return FALSE;
            if (!Dm.truthy(user.pulling) || user.pulling != this || user.grab_state != old_grab_state)
                return FALSE;
        }
        await user.setGrabState(user.grab_state + 1);
        switch (user.grab_state)
        {
            case GRAB_AGGRESSIVE:
            {
                string add_log = "";
                if (HAS_TRAIT(user, TRAIT_PACIFISM))
                {
                    visible_message(span_danger(I18n.T("[] firmly grips []!", user, this)),
                                    span_danger(I18n.T("[] firmly grips you!", user)), span_hear(I18n.T("You hear aggressive shuffling!")), null, user);
                    to_chat(user, span_danger(I18n.T("You firmly grip []!", this)));
                    add_log = " (pacifist)";
                }
                else
                {
                    visible_message(span_danger(I18n.T("[] grabs [] aggressively!", user, this)),
                                    span_userdanger(I18n.T("[] grabs you aggressively!", user)), span_hear(I18n.T("You hear aggressive shuffling!")), null, user);
                    to_chat(user, span_danger(I18n.T("You grab [] aggressively!", this)));
                }
                await stop_pulling();
                log_combat(user, this, "grabbed", addition: Dm.text("aggressive grab[]", add_log));
                break;
            }
            case GRAB_NECK:
                log_combat(user, this, "grabbed", addition: "neck grab");
                visible_message(span_danger(I18n.T("[] grabs [] by the neck!", user, this)),
                                span_userdanger(I18n.T("[] grabs you by the neck!", user)), span_hear(I18n.T("You hear aggressive shuffling!")), null, user);
                to_chat(user, span_danger(I18n.T("You grab [] by the neck!", this)));
                if (!Dm.truthy(buckled) && !Dm.truthy(density))
                    await Move(user.loc);
                break;
            case GRAB_KILL:
                log_combat(user, this, "strangled", addition: "kill grab");
                visible_message(span_danger(I18n.T("[] is strangling []!", user, this)),
                                span_userdanger(I18n.T("[] is strangling you!", user)), span_hear(I18n.T("You hear aggressive shuffling!")), null, user);
                to_chat(user, span_danger(I18n.T("You're strangling []!", this)));
                if (!Dm.truthy(buckled) && !Dm.truthy(density))
                    await Move(user.loc);
                break;
        }
        await user.set_pull_offsets(this, user.grab_state);
        return TRUE;
    }

    // tg: code/modules/mob/living/living_defense.dm:607  (M5x slice 3; set waitfor = FALSE, inherited from /atom/ex_act)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        object origin = Dm.arg<object>(__extra, 0);
        if (Dm.truthy(origin) && istype(origin, typeof(datum_spacevine_mutation)) && Dm.truthy(isvineimmune(this)))
            return FALSE;
        return base.ex_act(severity, target, __extra);
    }

    // tg: code/modules/mob/living/living_defense.dm:612
    public override async DmTask<object> acid_act(object acidpwr = null, object acid_volume = null, params object[] __extra)
    {
        await take_bodypart_damage(Dm.num(acidpwr) * min(1, Dm.num(acid_volume) * 0.1f));
        return TRUE;
    }

    ///As the name suggests, this should be called to apply electric shocks.
    // tg: code/modules/mob/living/living_defense.dm:617
    public virtual async DmTask<object> electrocute_act(object shock_damage = null, object source = null, float? siemens_coeff = null, float? flags = null, params object[] __extra)
    {
        siemens_coeff ??= 1;
        flags ??= NONE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_LIVING_ELECTROCUTE_ACT]) ? (object)NONE : this._SendSignal(COMSIG_LIVING_ELECTROCUTE_ACT, Dm.list(this, shock_damage, source, siemens_coeff, flags))), COMPONENT_LIVING_BLOCK_SHOCK)))
            return FALSE;
        siemens_coeff = Dm.num(siemens_coeff) * Dm.num(GET_PHYSIOLOGY(this, PHYS_COEFF_ELEC_CONDUCTIVITY));
        shock_damage = Dm.mul(shock_damage, siemens_coeff);
        if (Dm.truthy(Dm.bitand(flags, SHOCK_TESLA)) && HAS_TRAIT(this, TRAIT_TESLA_SHOCKIMMUNE))
            return FALSE;
        if (!Dm.truthy(Dm.bitand(flags, SHOCK_IGNORE_IMMUNITY)) && HAS_TRAIT(this, TRAIT_SHOCKIMMUNE))
            return FALSE;
        if (Dm.lt(shock_damage, 1))
            return FALSE;
        if (!Dm.truthy(Dm.bitand(flags, SHOCK_ILLUSION)))
        {
            await adjust_fire_loss(shock_damage);
            if (Dm.gt(get_fire_loss(), 100))
            {
                // add_shared_particles(/particles/smoke/burning) and its removal in 10 seconds (shared_particle_holder.dm:47):
                // particles are a client visual of a later milestone (M10+)
                Dm.Deferred("code/modules/mob/living/living_defense.dm:631", "electrocute_act: the burning smoke particles of a badly burnt mob (particles, M10+)");
            }
        }
        else
            await adjust_stamina_loss(shock_damage);
        if (!Dm.truthy(Dm.bitand(flags, SHOCK_SUPPRESS_MESSAGE)))
        {
            visible_message(
                span_danger(I18n.T("[] was shocked by \\the []!", this, source)),
                span_userdanger(I18n.T("You feel a powerful shock coursing through your body!")),
                span_hear(I18n.T("You hear a heavy electrical crack."))
            );
        }
        return shock_damage;
    }

    // tg: code/modules/mob/living/living_defense.dm:688
    //called when the mob receives a bright flash
    public virtual async DmTask<object> flash_act(float? intensity = null, float? override_blindness_check = null, float? affect_silicon = null, float? visual = null, Type type = null, float? length = null)
    {
        intensity ??= 1;
        override_blindness_check ??= 0;
        affect_silicon ??= 0;
        visual ??= 0;
        type ??= typeof(atom_movable_screen_fullscreen_flash);
        length ??= 2.5f * SECONDS;
        if (HAS_TRAIT(this, TRAIT_NOFLASH))
            return FALSE;
        if (Dm.num(get_eye_protection()) >= intensity)
            return FALSE;
        if (Dm.truthy(is_blind()) && !(Dm.truthy(override_blindness_check) || Dm.truthy(affect_silicon)))
            return FALSE;

        // this forces any kind of flash (namely normal and static) to use a black screen for photosensitive players
        // it absolutely isn't an ideal solution since sudden flashes to black can apparently still trigger epilepsy, but byond apparently doesn't let you freeze screens
        // and this is apparently at least less likely to trigger issues than a full white/static flash
        // DEVIATION(DV41): client?.prefs?.read_preference(/datum/preference/toggle/darkened_flash) is read as the preference's
        // default, FALSE (modules/client/preferences/accessibility.dm:4): the preferences are not translated (M9).
        object darkened_flash = FALSE;
        if (Dm.truthy(client) && Dm.truthy(darkened_flash))
            type = typeof(atom_movable_screen_fullscreen_flash_black);

        overlay_fullscreen("flash", type);
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_fullscreen), "flash", length), length, file: "code/modules/mob/living/living_defense.dm", line: 704);
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_FLASHED]))
            _SendSignal(COMSIG_MOB_FLASHED, Dm.list(this, intensity, override_blindness_check, affect_silicon, visual, type, length));
        return TRUE;
    }

    // tg: code/modules/mob/living/living_defense.dm:708
    //called when the mob receives a loud bang
    public virtual async DmTask<object> soundbang_act(float? intensity = null, float? stun_pwr = null, float? damage_pwr = null, float? deafen_pwr = null, float? ignore_deafness = null, float? send_sound = null)
    {
        intensity ??= SOUNDBANG_NORMAL;
        stun_pwr ??= 2 * SECONDS;
        damage_pwr ??= 5;
        deafen_pwr ??= 1.5f * SECONDS;
        ignore_deafness ??= FALSE;
        send_sound ??= TRUE;
        float protection = Dm.num(get_ear_protection(ignore_deafness));
        if (protection >= intensity)
            return FALSE;

        ///The amplitude of the effect is reduced by sound protection, while weakness only makes it worse.
        float effect_amount = protection > 0 ? 1 - (protection / intensity.Value) : 1 - protection;
        if (Dm.truthy(stun_pwr))
        {
            await Paralyze(stun_pwr * effect_amount * 0.1f);
            await Knockdown(stun_pwr * effect_amount);
        }

        obj_item_organ_ears ears = get_organ_slot(ORGAN_SLOT_EARS) as obj_item_organ_ears;

        object dot = effect_amount; //how soundbanged we are
        if (ears == null || !(Dm.truthy(deafen_pwr) || Dm.truthy(damage_pwr)))
            return dot;

        float ear_damage = damage_pwr.Value * effect_amount;
        float deaf = deafen_pwr.Value * effect_amount;
        await sound_damage(ear_damage, deaf);

        if (Dm.truthy(send_sound))
            Dm.output_to(this, sound(Dm.resource("sound/items/weapons/flash_ring.ogg"), 0, 1, 0, 250)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)

        if (Dm.num(ears.damage) >= 15 && prob(Dm.num(ears.damage) - 5))
        {
            to_chat(this, span_userdanger(I18n.T("You can't hear anything!")));
            // Makes you deaf, enough that you need a proper source of healing, it won't self heal
            // you need earmuffs, inacusiate, or replacement
            await ears.set_organ_damage(ears.maxHealth);
        }
        else if (Dm.num(ears.damage) >= 5)
            to_chat(this, span_warning(Dm.num(ears.damage) >= 15 ? I18n.T("Your ears start to ring badly!") : I18n.T("Your ears start to ring!")));
        return dot;
    }

    // tg: code/modules/mob/living/living_defense.dm:742
    //to damage the clothes worn by a mob
    public virtual async DmTask damage_clothes(object damage_amount = null, string damage_type = null, object damage_flag = null, object def_zone = null)
    {
        return;
    }

    // tg: code/modules/mob/living/living_defense.dm:746
    public override async DmTask do_attack_animation(atom attacked_atom = null, object visual_effect_icon = null, obj_item used_item = null, object no_effect = null, float? fov_effect = null, object item_animation_override = null)
    {
        if (used_item == null)
            used_item = get_active_held_item() as obj_item;
        await base.do_attack_animation(attacked_atom, visual_effect_icon, used_item, no_effect, fov_effect, item_animation_override);
    }

    // tg: code/modules/mob/living/living_defense.dm:759
    public virtual async DmTask do_slap_animation(atom slapped = null)
    {
        Dm.NotTranslated("code/modules/mob/living/living_defense.dm:759", "do_slap_animation: not in M2 (M4a attacks, M5 ex_act, M10+ grabs)");
    }

    /** Handles exposing a mob to reagents.
     *
     * If the methods include INGEST or INHALE, the mob tastes the reagents.
     * If the methods include VAPOR or TOUCH it incorporates permiability protection.
     */
    // tg: code/modules/mob/living/living_defense.dm:776  (M4b: the fire extinguisher's water)
    [DmParams("reagents", "source", "methods", "volume_modifier", "show_message")]
    public override async DmTask<object> expose_reagents(DmList reagents = null, datum_reagents source = null, float? methods = null, float? show_message = null, params object[] __extra)
    {
        // DM binds the arguments by position: the fourth is volume_modifier here (the root's show_message), the fifth
        // show_message (tg's holder.expose passes four, so volume_modifier gets its show_message and show_message its default).
        float? volume_modifier = show_message;
        float? own_show_message = Dm.arg<float?>(__extra, 0);
        methods ??= TOUCH;
        volume_modifier ??= 1;
        own_show_message ??= TRUE;
        object dot = await base.expose_reagents(reagents, source, methods, volume_modifier, Dm.forward(__extra, own_show_message));
        if (Dm.truthy(Dm.bitand(dot, COMPONENT_NO_EXPOSE_REAGENTS)))
            return dot;

        if (Dm.truthy(own_show_message) && Dm.truthy(Dm.bitand(methods, Dm.bitor(INGEST, INHALE))))
            taste_list(reagents);

        float touch_protection = Dm.truthy(Dm.bitand(methods, Dm.bitor(VAPOR, TOUCH))) ? Dm.num(getarmor(null, BIO)) * 0.01f : 0;
        if (Dm.truthy(source._listen_lookup?[COMSIG_REAGENTS_EXPOSE_MOB]))
            source._SendSignal(COMSIG_REAGENTS_EXPOSE_MOB, Dm.list(source, this, reagents, methods, volume_modifier, own_show_message, touch_protection));
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(reagents))
        {
            object reac_volume = reagents[reagent];
            await reagent.expose_mob(this, methods, reac_volume, own_show_message, touch_protection); // . |= : expose_mob returns null (rules 7.4)
        }
        return dot;
    }

    /**
     * Attempt to disarm the target mob. Some items might let you do it, also carbon can do it with right click.
     * Will shove the target mob back, and drop them if they're in front of something dense
     * or another carbon.
    */
    // tg: code/modules/mob/living/living_defense.dm:802
    public virtual async DmTask disarm(mob_living target = null, obj_item weapon = null)
    {
        if (!Dm.truthy(can_disarm(target)))
            return;
        object shove_flags = target.get_shove_flags(this, weapon);
        if (Dm.truthy(weapon))
        {
            await do_attack_animation(target, used_item: weapon);
            playsound(target, Dm.resource("sound/effects/glass/glassbash.ogg"), 50, TRUE, -1); // tg: code/modules/mob/living/living_defense.dm:808
        }
        else
        {
            await do_attack_animation(target, ATTACK_EFFECT_DISARM);
            playsound(target, Dm.resource("sound/items/weapons/shove.ogg"), 50, TRUE, -1); // tg: code/modules/mob/living/living_defense.dm:811
        }
        if (ishuman(target) && weapon == null)
        {
            mob_living_carbon_human human_target = target as mob_living_carbon_human;
            if (human_target.w_uniform != null)
            {
                // tg: code/modules/mob/living/living_defense.dm:813
                ((atom)human_target.w_uniform).add_fingerprint(this);
            }
        }

        if (Dm.truthy(target._listen_lookup?[COMSIG_LIVING_DISARM_HIT]))
            target._SendSignal(COMSIG_LIVING_DISARM_HIT, Dm.list(target, this, zone_selected, weapon));
        float shove_dir = get_dir(loc, target.loc);
        // DEVIATION(DV17): when both mobs are capsule mobs (D105, LocomotionSeam), the shove direction is get_dir of the two
        // capsule centres, not of the two turfs (they often share one 2 m tile); the destination stays the target's turf plus
        // one step in that direction, and the target moves tg's full tile below.
        if (dv17_capsule_pair(target))
            shove_dir = dv17_capsule_dir(target);
        turf target_shove_turf = get_step(target.loc, shove_dir);
        atom target_old_turf = target.loc;

        //Are we hitting anything? or
        if (Dm.truthy(Dm.bitand(shove_flags, SHOVE_CAN_MOVE)))
        {
            if (Dm.truthy(Dm.bitand((!Dm.truthy(target_shove_turf._listen_lookup?[COMSIG_LIVING_DISARM_PRESHOVE]) ? NONE : target_shove_turf._SendSignal(COMSIG_LIVING_DISARM_PRESHOVE, Dm.list(target_shove_turf, this, target, weapon))), COMSIG_LIVING_ACT_SOLID)))
                Dm.combine(ref shove_flags, SHOVE_BLOCKED);
            else
            {
                await target.Move(target_shove_turf, shove_dir);
                if (get_turf(target) == target_old_turf)
                    Dm.combine(ref shove_flags, SHOVE_BLOCKED);
            }
        }

        if (!Dm.truthy(Dm.bitand(shove_flags, SHOVE_BLOCKED)))
            await target.setGrabState(GRAB_PASSIVE);

        //Directional checks to make sure that we're not shoving through a windoor or something like that
        if (Dm.truthy(Dm.bitand(shove_flags, SHOVE_BLOCKED)) && (Dm.@in(shove_dir, GLOB.cardinals)))
        {
            turf target_turf = get_turf(target);
            foreach (obj obj_content in Dm.each<obj>(target_turf))
            {
                if (Dm.truthy(Dm.bitand(obj_content.flags_1, ON_BORDER_1)) && obj_content.dir == shove_dir && Dm.truthy(obj_content.density))
                {
                    Dm.combine(ref shove_flags, SHOVE_DIRECTIONAL_BLOCKED);
                    break;
                }
            }
            if (target_turf != target_shove_turf && !Dm.truthy(Dm.bitand(shove_flags, SHOVE_DIRECTIONAL_BLOCKED))) //Make sure that we don't run the exact same check twice on the same tile
            {
                foreach (obj obj_content in Dm.each<obj>(target_shove_turf))
                {
                    if (Dm.truthy(Dm.bitand(obj_content.flags_1, ON_BORDER_1)) && obj_content.dir == REVERSE_DIR(shove_dir) && Dm.truthy(obj_content.density))
                    {
                        Dm.combine(ref shove_flags, SHOVE_DIRECTIONAL_BLOCKED);
                        break;
                    }
                }
            }
        }

        if (Dm.truthy(Dm.bitand(shove_flags, SHOVE_CAN_HIT_SOMETHING)))
        {
            //Don't hit people through windows, ok?
            if (!Dm.truthy(Dm.bitand(shove_flags, SHOVE_DIRECTIONAL_BLOCKED)) && (Dm.truthy(Dm.bitand((!Dm.truthy(target_shove_turf._listen_lookup?[COMSIG_LIVING_DISARM_COLLIDE]) ? NONE : target_shove_turf._SendSignal(COMSIG_LIVING_DISARM_COLLIDE, Dm.list(target_shove_turf, this, target, shove_flags, weapon))), COMSIG_LIVING_SHOVE_HANDLED))))
                return;
            if ((Dm.truthy(Dm.bitand(shove_flags, SHOVE_BLOCKED))) && !Dm.truthy(Dm.bitand(shove_flags, Dm.bitor(SHOVE_KNOCKDOWN_BLOCKED, SHOVE_CAN_KICK_SIDE))))
            {
                object knocked_down = await target.Knockdown(SHOVE_KNOCKDOWN_SOLID, daze_amount: 3 * SECONDS);
                target.visible_message(span_danger(I18n.T("[] shoves [][]!", name, target.name, (Dm.truthy(knocked_down) ? I18n.T(", knocking [] down", target.p_them()) : ""))),
                    span_userdanger(I18n.T("You[] from a shove by []!", (Dm.truthy(knocked_down) ? I18n.T("'re knocked down") : I18n.T(" resist falling down")), name)), span_hear(I18n.T("You hear aggressive shuffling []", (Dm.truthy(knocked_down) ? I18n.T("followed by a loud thud!") : ""))), COMBAT_MESSAGE_RANGE, this);
                to_chat(this, span_danger(I18n.T("You shove [][]!", target.name, (Dm.truthy(knocked_down) ? I18n.T(", knocking [] down", target.p_them()) : ""))));
                log_combat(this, target, "shoved", Dm.text("[]", (Dm.truthy(knocked_down) ? Dm.text("knocking them down[]", (Dm.truthy(weapon) ? Dm.text(" with []", weapon) : "")) : "")));
                if (ishuman(target))
                {
                    mob_living_carbon_human human_target = target as mob_living_carbon_human;
                    human_target.force_say();
                }
                return;
            }
        }

        if (Dm.truthy(Dm.bitand(shove_flags, SHOVE_CAN_KICK_SIDE))) //KICK HIM IN THE NUTS
        {
            if (Dm.truthy(await target.Paralyze(SHOVE_CHAIN_PARALYZE)))
            {
                await target.apply_status_effect(typeof(datum_status_effect_no_side_kick));
                target.visible_message(span_danger(I18n.T("[] kicks [] onto [] side!", name, target.name, target.p_their())),
                    span_userdanger(I18n.T("You're kicked onto your side by []!", name)), span_hear(I18n.T("You hear aggressive shuffling followed by a loud thud!")), COMBAT_MESSAGE_RANGE, this);
                to_chat(this, span_danger(I18n.T("You kick [] onto [] side!", target.name, target.p_their())));
                _addtimer(Dm.@new<datum_callback>(target, nameof(mob_living.SetKnockdown), 0), SHOVE_CHAIN_PARALYZE, file: "code/modules/mob/living/living_defense.dm", line: 867);
                log_combat(this, target, "kicks", "onto their side (paralyzing)");
                return;
            }
        }

        target.get_shoving_message(this, weapon, shove_flags);

        //Take their lunch money
        object target_held_item = target.get_active_held_item();
        object append_message = (Dm.truthy(weapon) ? Dm.text(" with []", weapon) : "");
        // If it's in our typecache, they're staggered and it exists, disarm. If they're knocked down, disarm too.
        if (Dm.truthy(target_held_item) && Dm.truthy(target.get_timed_status_effect_duration(typeof(datum_status_effect_staggered))) && Dm.truthy(is_type_in_typecache(target_held_item, GLOB.shove_disarming_types)) || Dm.truthy(target_held_item) && target.body_position == LYING_DOWN)
        {
            await target.dropItemToGround(target_held_item as obj_item);
            append_message = I18n.T("causing [] to drop []", target.p_them(), target_held_item);
            target.visible_message(span_danger(I18n.T("[] drops \\the []!", target.name, target_held_item)),
                span_warning(I18n.T("You drop \\the []!", target_held_item)), null, COMBAT_MESSAGE_RANGE);
        }

        if (Dm.truthy(Dm.bitand(shove_flags, SHOVE_CAN_STAGGER)))
            await target.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH, 10 * SECONDS);

        log_combat(this, target, "shoved", append_message);
    }

    /// DEVIATION(DV17): whether the shover and the target are both capsule mobs (D105: living mobs that the locomotion seam
    /// drives, LocomotionSeam.IsRegistered).
    private bool dv17_capsule_pair(mob_living target)
    {
        return LocomotionSeam.IsRegistered(this) && LocomotionSeam.IsRegistered(target);
    }

    /// DEVIATION(DV17): the direction between the two capsule centres (the seam's MotionState after the last locomotion
    /// step, in 1/256 tiles) by the bridge's 8-sector rule (LocomotionSeam.SectorDir: a cardinal unless the offset is more
    /// than 22.5 degrees off its axis, the rule of the wish direction and the facing; lead decision, M2 integration), 0 on
    /// equal centres. BYOND's get_dir of the centres would shove diagonally at any off-axis offset.
    private float dv17_capsule_dir(mob_living target)
    {
        World w = World.Current;
        if (!LocomotionSeam.TryGetState(w, dm_id, out Anomaly.Core.Motion.MotionState own, out _) || !LocomotionSeam.TryGetState(w, target.dm_id, out Anomaly.Core.Motion.MotionState theirs, out _))
            return get_dir(loc, target.loc);
        return LocomotionSeam.SectorDir((long)theirs.X - own.X, (long)theirs.Y - own.Y);
    }

    ///Check if the universal conditions for disarming/shoving are met.
    // tg: code/modules/mob/living/living_defense.dm:889
    public virtual object can_disarm(mob_living target = null)
    {
        // DEVIATION(DV17): two capsule mobs may share a turf (D105, DV16): the same-turf refusal (loc == target.loc) does not
        // apply to them.
        if (body_position != STANDING_UP || this == target || (loc == target.loc && !dv17_capsule_pair(target)))
            return FALSE;
        return TRUE;
    }

    ///Check if there's anything that could stop the knockdown from being shoved into something or someone.
    // tg: code/modules/mob/living/living_defense.dm:895
    public virtual object get_shove_flags(mob_living shover = null, obj_item weapon = null)
    {
        object dot = null;
        if (Dm.ge(shover.move_force, move_resist))
        {
            Dm.combine(ref dot, SHOVE_CAN_MOVE);
            if (!Dm.truthy(buckled))
                Dm.combine(ref dot, SHOVE_CAN_HIT_SOMETHING);
        }
        if (HAS_TRAIT(this, TRAIT_BRAWLING_KNOCKDOWN_BLOCKED))
            Dm.combine(ref dot, SHOVE_KNOCKDOWN_BLOCKED);
        return dot;
    }

    ///Send the chat feedback message for shoving
    // tg: code/modules/mob/living/living_defense.dm:904
    public virtual void get_shoving_message(mob_living shover = null, obj_item weapon = null, object shove_flags = null)
    {
        visible_message(span_danger(I18n.T("[] shoves [][]!", shover, name, (Dm.truthy(weapon) ? I18n.T(" with []", weapon) : ""))),
            span_userdanger(I18n.T("You're shoved by [][]!", shover, (Dm.truthy(weapon) ? I18n.T(" with []", weapon) : ""))), span_hear(I18n.T("You hear aggressive shuffling!")), COMBAT_MESSAGE_RANGE, shover);
        to_chat(shover, span_danger(I18n.T("You shove [][]!", name, (Dm.truthy(weapon) ? I18n.T(" with []", weapon) : ""))));
    }

    // tg: code/modules/mob/living/living_defense.dm:909
    public virtual async DmTask<object> check_block(atom hit_by = null, object damage = null, string attack_text = null, float? attack_type = null, float? armour_penetration = null, string damage_type = null, params object[] __extra)
    {
        attack_text ??= "the attack";
        attack_type ??= MELEE_ATTACK;
        armour_penetration ??= 0;
        damage_type ??= BRUTE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_LIVING_CHECK_BLOCK]) ? NONE : _SendSignal(COMSIG_LIVING_CHECK_BLOCK, Dm.list(this, hit_by, damage, attack_text, attack_type, armour_penetration, damage_type))), SUCCESSFUL_BLOCK)))
            return SUCCESSFUL_BLOCK;

        return FAILED_BLOCK;
    }

    // tg: code/modules/mob/living/living_defense.dm:915
    public virtual object hypnosis_vulnerable()
    {
        if (HAS_MIND_TRAIT(this, TRAIT_UNCONVERTABLE))
            return FALSE;
        if (Dm.truthy(has_status_effect(typeof(datum_status_effect_hallucination))) || Dm.truthy(has_status_effect(typeof(datum_status_effect_drugginess))))
            return TRUE;
        if (IS_UNCONSCIOUS(this))
            return TRUE;
        if (HAS_TRAIT(this, TRAIT_DUMB))
            return TRUE;
        if ((mob_mood != null ? mob_mood.sanity : 0) < SANITY_UNSTABLE) // null < SANITY_UNSTABLE is true in DM: a mob with no mood is vulnerable
            return TRUE;
        return FALSE;
    }
}

// tg: code/modules/mob/living/living_defense.dm:506
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living_defense.dm:506
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return TRUE;

        mob_living attacker = user as mob_living; // tg types the argument /mob/living/carbon/human
        if (attacker != null && !Dm.truthy(attacker.combat_mode) && HAS_TRAIT(this, TRAIT_READY_TO_OPERATE) && Dm.truthy(await attacker.perform_surgery(this)))
            return TRUE;

        return FALSE;
    }
}

// tg: code/modules/mob/living/living_defense.dm:239
public partial class obj_item : obj
{
    // tg: code/modules/mob/living/living_defense.dm:239
    public virtual object get_volume_by_throwforce_and_or_w_class()
    {
        if (Dm.truthy(throwforce) && Dm.truthy(w_class))
            return clamp((throwforce + w_class) * 5, 30, 100);// Add the item's throwforce to its weight class and multiply by 5, then clamp the value between 30 and 100
        if (Dm.truthy(w_class))
            return clamp(w_class * 8, 20, 100); // Multiply the item's weight class by 8, then clamp the value between 20 and 100
        return 0; // plays no sound
    }
}

// tg: code/modules/mob/living/living_defense.dm:264
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living_defense.dm:264
    public override async DmTask<object> hitby(atom_movable hitting_atom = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        hitpush ??= TRUE;
        blocked ??= FALSE;
        atom_movable AM = hitting_atom;
        if (!isitem(AM))
        {
            // Filled with made up numbers for non-items.
            if (Dm.truthy(Dm.bitand(await check_block(AM, 30, Dm.text("\\the []", AM.name), THROWN_PROJECTILE_ATTACK, 0, BRUTE), SUCCESSFUL_BLOCK)))
            {
                hitpush = FALSE;
                skipcatch = TRUE;
                blocked = TRUE;
                return SUCCESSFUL_BLOCK;
            }
            else
            {
                playsound(loc, Dm.resource("sound/items/weapons/genhit.ogg"), 50, TRUE, -1); // tg: code/modules/mob/living/living_defense.dm:273 //Item sounds are handled in the item itself
                // GAP(lead): isvendor (__DEFINES/is_helpers.dm:279) has no GlobalProcs member yet: expanded inline.
                if (!istype(AM, typeof(obj_machinery_vending)) && !iscarbon(AM)) //Vendors have special interactions, while carbon mobs already generate visible messages!
                    visible_message(span_danger(I18n.T("[] is hit by []!", this, AM)),
                                span_userdanger(I18n.T("You're hit by []!", AM)));
            }
            log_combat(AM, this, "hit ");
            return await base.hitby(hitting_atom, skipcatch, hitpush, blocked, throwingdatum);
        }

        obj_item thrown_item = AM as obj_item;
        if (throwingdatum?.get_thrower() != this) //No throwing stuff at yourself to trigger hit reactions
        {
            if (Dm.truthy(await check_block(AM, thrown_item.throwforce, Dm.text("\\the []", thrown_item.name), THROWN_PROJECTILE_ATTACK, 0, thrown_item.damtype)))
            {
                hitpush = FALSE;
                skipcatch = TRUE;
                blocked = TRUE;
            }
        }

        object zone = get_random_valid_zone(BODY_ZONE_CHEST, 65);//Hits a random part of the body, geared towards the chest
        object nosell_hit = Dm.bitand((!Dm.truthy(thrown_item._listen_lookup?[COMSIG_MOVABLE_IMPACT_ZONE]) ? NONE : thrown_item._SendSignal(COMSIG_MOVABLE_IMPACT_ZONE, Dm.list(thrown_item, this, zone, blocked, throwingdatum))), MOVABLE_IMPACT_ZONE_OVERRIDE); // TODO: find a better way to handle hitpush and skipcatch for humans
        if (Dm.truthy(nosell_hit))
        {
            skipcatch = TRUE;
            hitpush = FALSE;
        }

        if (Dm.truthy(blocked))
            return SUCCESSFUL_BLOCK;

        if (Dm.truthy(nosell_hit))
        {
            log_hit_combat(throwingdatum?.get_thrower() as mob, thrown_item);
            return await base.hitby(hitting_atom, skipcatch, hitpush, blocked, throwingdatum);
        }

        visible_message(span_danger(I18n.T("[] is hit by []!", this, thrown_item)),
            span_userdanger(I18n.T("You're hit by []!", thrown_item)));
        if (!Dm.truthy(thrown_item.throwforce))
        {
            log_hit_combat(throwingdatum?.get_thrower() as mob, thrown_item);
            return null;
        }

        object armor = run_armor_check(
            zone,
            MELEE,
            I18n.T("Your armor has protected your [].", parse_zone_with_bodypart__local(zone)),
            I18n.T("Your armor has softened hit to your [].", parse_zone_with_bodypart__local(zone)),
            thrown_item.armour_penetration,
            "",
            FALSE,
            thrown_item.weak_against_armour
        );
        await apply_damage(thrown_item.throwforce, thrown_item.damtype, zone, Dm.num_or_null(armor), sharpness: Dm.num_or_null(thrown_item.get_sharpness()), wound_bonus: Dm.num(nosell_hit) * CANT_WOUND, attacking_item: thrown_item);
        log_hit_combat(throwingdatum?.get_thrower() as mob, thrown_item);

        if (QDELETED(this)) //Damage can delete the mob.
            return null;
        if (body_position == LYING_DOWN) // physics says it's significantly harder to push someone by constantly chucking random furniture at them if they are down on the floor.
            hitpush = FALSE;

        return await base.hitby(hitting_atom, skipcatch, hitpush, blocked, throwingdatum);
    }

    // GAP(lead): /mob/living/proc/parse_zone_with_bodypart (__HELPERS/mobs.dm:495) is not declared; its body, a local copy
    // for hitby (the I1 GlobalProcs cleanup moves it).
    // tg: code/__HELPERS/mobs.dm:495
    private object parse_zone_with_bodypart__local(object zone)
    {
        obj_item_bodypart part = get_bodypart(zone as string) as obj_item_bodypart;

        object plaintext = part?.plaintext_zone;
        return Dm.truthy(plaintext) ? plaintext : parse_zone(zone);
    }
}

// tg: code/modules/mob/living/living_defense.dm:652 (interleaved types, rules 1.9; M5x slice 5)
public partial class mob_living : mob
{
    ///Logs, gibs and returns point values of whatever mob is unfortunate enough to get eaten.
    // tg: code/modules/mob/living/living_defense.dm:652  (D150: gib kills the mob, and its body stays until M6's gibbing)
    public override async DmTask<object> singularity_act()
    {
        investigate_log("has been consumed by the singularity.", INVESTIGATE_ENGINE); //Oh that's where the clown ended up!
        investigate_log("has been gibbed by the singularity.", INVESTIGATE_DEATHS);
        await gib();
        return 20;
    }
}

// tg: code/modules/mob/living/living_defense.dm:643 (interleaved types, rules 1.9; M7 EMP)
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living_defense.dm:643
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
            return dot;
        severity = Dm.num(severity) + Dm.num(get_emp_protection());
        foreach (obj inside in Dm.each<obj>(contents))
            await inside.emp_act(severity);
        return dot;
    }
}

// Gaps package C1: a basic mob's or simple animal's attack on a living mob.
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living_defense.dm:450  (DM's user is a simple animal by its declaration and a basic mob as often: their same-named vars read by name)
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_animal(user, modifiers);
        if (Dm.truthy(dot))
            return FALSE; // looks wrong, but if the attack chain was cancelled we don't propogate it up to children calls. Yeah it's cringe.

        mob_living animal = (mob_living)user;
        if (animal.melee_damage_upper == 0)
        {
            if (animal != this)
            {
                visible_message(
                    span_notice(I18n.T("[] [] []!", animal, Dm.get_var(animal, "friendly_verb_continuous"), this)),
                    span_notice(I18n.T("[] [] you!", animal, Dm.get_var(animal, "friendly_verb_continuous"))),
                    vision_distance: COMBAT_MESSAGE_RANGE,
                    ignored_mobs: animal
                );
                to_chat(animal, span_notice(I18n.T("You [] []!", Dm.get_var(animal, "friendly_verb_simple"), this)));
            }
            return FALSE;
        }

        if (HAS_TRAIT(animal, TRAIT_PACIFISM))
        {
            to_chat(animal, span_warning(I18n.T("You don't want to hurt anyone!")));
            return FALSE;
        }

        float damage = rand(Dm.num(animal.melee_damage_lower), Dm.num(animal.melee_damage_upper));
        string damage_type = Dm.get_var(animal, "melee_damage_type") as string;
        if (Dm.truthy(await check_block(animal, damage, I18n.T("[]'s []", animal, Dm.get_var(animal, "attack_verb_simple")), UNARMED_ATTACK, Dm.num(Dm.get_var(animal, "armour_penetration")), damage_type)))
            return FALSE;

        if (Dm.truthy(Dm.get_var(animal, "attack_sound")))
            playsound(this, Dm.get_var(animal, "attack_sound"), 50, TRUE, TRUE);

        await animal.do_attack_animation(this);
        visible_message(
            span_danger(I18n.T("[] [] []!", animal, Dm.get_var(animal, "attack_verb_continuous"), this)),
            span_userdanger(I18n.T("[] [] you!", animal, Dm.get_var(animal, "attack_verb_continuous"))),
            null,
            COMBAT_MESSAGE_RANGE,
            animal
        );

        object dam_zone = await dismembering_strike(animal, pick(BODY_ZONE_CHEST, BODY_ZONE_PRECISE_L_HAND, BODY_ZONE_PRECISE_R_HAND, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG));
        if (!Dm.truthy(dam_zone)) //Dismemberment successful
            return FALSE;

        object armor_block = run_armor_check(animal.zone_selected, MELEE, armour_penetration: Dm.get_var(animal, "armour_penetration"));

        to_chat(animal, span_danger(I18n.T("You [] []!", Dm.get_var(animal, "attack_verb_simple"), this)));
        object damage_done = await apply_damage(
            damage: damage,
            damagetype: damage_type,
            def_zone: animal.zone_selected,
            blocked: Dm.num(armor_block),
            wound_bonus: Dm.num(Dm.get_var(animal, "wound_bonus")),
            exposed_wound_bonus: Dm.num(Dm.get_var(animal, "exposed_wound_bonus")),
            sharpness: Dm.num(Dm.get_var(animal, "sharpness")),
            attack_direction: get_dir(animal, this)
        );
        log_combat(animal, this, "attacked");
        return damage_done;
    }
}

// tg: code/modules/mob/living/living_defense.dm:516 (interleaved types, rules 1.9; gaps package C5: a monkey's bite)
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living_defense.dm:516
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        mob_living_carbon_human biter = user as mob_living_carbon_human;
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
        {
            await biter.disarm(this);
            return TRUE;
        }
        if (!Dm.truthy(biter.combat_mode))
            return FALSE;
        if (HAS_TRAIT(biter, TRAIT_PACIFISM))
        {
            to_chat(biter, span_warning("You don't want to hurt anyone!"));
            return FALSE;
        }

        if (!Dm.truthy(biter.get_bodypart(BODY_ZONE_HEAD)))
            return FALSE;
        if (Dm.truthy(biter.is_mouth_covered(ITEM_SLOT_MASK)))
        {
            to_chat(biter, span_warning("You can't bite with your mouth covered!"));
            return FALSE;
        }

        if (Dm.truthy(await check_block(biter, 1, I18n.T("[]'s bite", biter), UNARMED_ATTACK, 0, BRUTE)))
            return FALSE;

        await biter.do_attack_animation(this, ATTACK_EFFECT_BITE);
        if (HAS_TRAIT(biter, TRAIT_PERFECT_ATTACKER) || prob(75))
        {
            log_combat(biter, this, "attacked");
            playsound(loc, Dm.resource("sound/items/weapons/bite.ogg"), 50, TRUE, -1);
            visible_message(span_danger(I18n.T("[] bites []!", biter.name, this)),
                            span_userdanger(I18n.T("[] bites you!", biter.name)), span_hear("You hear a chomp!"), COMBAT_MESSAGE_RANGE, biter);
            to_chat(biter, span_danger(I18n.T("You bite []!", this)));
            return TRUE;
        }
        else
        {
            visible_message(span_danger(I18n.T("[]'s bite misses []!", biter.name, this)),
                            span_danger(I18n.T("You avoid []'s bite!", biter.name)), span_hear("You hear the sound of jaws snapping shut!"), COMBAT_MESSAGE_RANGE, biter);
            to_chat(biter, span_warning(I18n.T("Your bite misses []!", this)));
        }

        return FALSE;
    }
}

// tg: code/modules/mob/living/living_defense.dm:550 (interleaved types, rules 1.9; gaps X5: the larva's bite, the alien's slash)
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living_defense.dm:550
    [DmParams("L", "modifiers")]
    public override async DmTask<object> attack_larva(mob user = null, DmList modifiers = null)
    {
        mob_living_carbon_alien_larva L = user as mob_living_carbon_alien_larva;
        if (Dm.truthy(L.combat_mode))
        {
            if (HAS_TRAIT(L, TRAIT_PACIFISM))
            {
                to_chat(L, span_warning(I18n.T("You don't want to hurt anyone!")));
                return FALSE;
            }

            if (Dm.truthy(await check_block(L, 1, I18n.T("[]'s bite", L), UNARMED_ATTACK, 0, BRUTE)))
                return FALSE;

            await L.do_attack_animation(this);
            if (prob(90))
            {
                log_combat(L, this, "attacked");
                visible_message(span_danger(I18n.T("[] bites []!", L.name, this)),
                                span_userdanger(I18n.T("[] bites you!", L.name)), span_hear(I18n.T("You hear a chomp!")), COMBAT_MESSAGE_RANGE, L);
                to_chat(L, span_danger(I18n.T("You bite []!", this)));
                playsound(loc, Dm.resource("sound/items/weapons/bite.ogg"), 50, TRUE, -1);
                return TRUE;
            }
            else
            {
                visible_message(span_danger(I18n.T("[]'s bite misses []!", L.name, this)),
                                span_danger(I18n.T("You avoid []'s bite!", L.name)), span_hear(I18n.T("You hear the sound of jaws snapping shut!")), COMBAT_MESSAGE_RANGE, L);
                to_chat(L, span_warning(I18n.T("Your bite misses []!", this)));
                return FALSE;
            }
        }

        visible_message(span_notice(I18n.T("[] rubs its head against [].", L.name, this)),
                        span_notice(I18n.T("[] rubs its head against you.", L.name)), null, null, L);
        to_chat(L, span_notice(I18n.T("You rub your head against [].", this)));
        return FALSE;
    }

    // tg: code/modules/mob/living/living_defense.dm:578
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOB_ATTACK_ALIEN]))
            this._SendSignal(COMSIG_MOB_ATTACK_ALIEN, Dm.list(this, user, modifiers));
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
        {
            if (Dm.truthy(await check_block(user, 0, I18n.T("[]'s tackle", user), UNARMED_ATTACK, 0, BRUTE)))
                return FALSE;
            await user.do_attack_animation(this, ATTACK_EFFECT_DISARM);
            return TRUE;
        }

        if (Dm.truthy(user.combat_mode))
        {
            if (HAS_TRAIT(user, TRAIT_PACIFISM))
            {
                to_chat(user, span_warning(I18n.T("You don't want to hurt anyone!")));
                return FALSE;
            }
            if (Dm.truthy(await check_block(user, user.melee_damage_upper, I18n.T("[]'s slash", user), UNARMED_ATTACK, 0, BRUTE)))
                return FALSE;
            await user.do_attack_animation(this);
            return TRUE;
        }

        visible_message(span_notice(I18n.T("[] caresses [] with its scythe-like arm.", user, this)),
                        span_notice(I18n.T("[] caresses you with its scythe-like arm.", user)), null, null, user);
        to_chat(user, span_notice(I18n.T("You caress [] with your scythe-like arm.", this)));
        return FALSE;
    }
}

// tg: code/modules/mob/living/living_defense.dm:600 (interleaved types, rules 1.9; the MetaStation leftovers: a hulk's punch)
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living_defense.dm:600
    public override async DmTask<object> attack_hulk(mob_living_carbon_human user = null, params object[] __extra)
    {
        await base.attack_hulk(user, __extra);
        if (HAS_TRAIT(user, TRAIT_PACIFISM))
        {
            to_chat(user, span_warning(I18n.T("You don't want to hurt []!", this)));
            return FALSE;
        }
        return TRUE;
    }
}
