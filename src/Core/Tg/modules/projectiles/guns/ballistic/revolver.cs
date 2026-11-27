// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/guns/ballistic/revolver.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice C: the revolver (cylinder, spin, ignition), the .38 revolver and the detective's, and the Russian revolver (the spin before
// the trigger, the aim pause, the self-shot and its mood events). The detective's revolver's reskins are live; the memories are a row of
// docs/ai/specs/not-translated.md section 48; the traitor mail's Russian revolver and the reverse revolver are syndicate G4a's (D316);
// gaps GU1: the cursed Russian revolver of the Lavaland greed ruin (its cheater's dusting; the soul capture into a soulstone waits for
// the cult's soulstones, section 215). The var blocks are generated (D111).

// tg: code/modules/projectiles/guns/ballistic/revolver.dm:1
public partial class obj_item_gun_ballistic_revolver : obj_item_gun_ballistic
{
    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:19
    public override async DmTask<object> process_fire(atom target = null, mob_living user = null, float? message = null, object @params = null, string zone_override = null, float? bonus_spread = null)
    {
        object dot = await base.process_fire(target, user, message, @params, zone_override, bonus_spread);
        if (Dm.truthy(dot))
            last_fire = world.time;
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:24
    public override void chamber_round(float? spin_cylinder = null, float? replace_new_round = null)
    {
        spin_cylinder ??= TRUE;
        if (magazine == null) //if it mag was qdel'd somehow.
            throw Dm.CRASH("revolver tried to chamber a round without a magazine!", "code/modules/projectiles/guns/ballistic/revolver.dm", 29);
        if (chambered != null)
            UnregisterSignal(chambered, COMSIG_MOVABLE_MOVED);
        if (Dm.truthy(spin_cylinder))
        {
            chambered = magazine.get_round();
        }
        else
        {
            object first = magazine.stored_ammo[1f];
            if (ispath(first))
            {
                first = Dm.@new(first as Type, magazine);
                magazine.stored_ammo[1f] = first;
            }
            chambered = first as obj_item_ammo_casing;
        }
        if (chambered != null)
            RegisterSignal(chambered, COMSIG_MOVABLE_MOVED, nameof(clear_chambered));
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:39
    public override async DmTask shoot_with_empty_chamber(mob_living user = null)
    {
        await base.shoot_with_empty_chamber(user);
        chamber_round();
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:43
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await spin_chamber(user as mob_living);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:47
    public override void fire_sounds()
    {
        float frequency_to_use = sin((90 / (magazine?.max_ammo ?? 0)) * get_ammo(TRUE, FALSE)); // fucking REVOLVERS
        float click_frequency_to_use = 1 - frequency_to_use * 0.75f;
        bool play_click = sqrt(magazine?.max_ammo ?? 0) > get_ammo(TRUE, FALSE);
        if (Dm.truthy(suppressed))
        {
            playsound(this, suppressed_sound, suppressed_volume, vary_fire_sound, ignore_walls: FALSE, extrarange: SILENCED_SOUND_EXTRARANGE, falloff_distance: 0);
            if (play_click)
                playsound(this, Dm.resource("sound/items/weapons/gun/general/ballistic_click.ogg"), suppressed_volume, vary_fire_sound, ignore_walls: FALSE, extrarange: SILENCED_SOUND_EXTRARANGE, falloff_distance: 0, frequency: click_frequency_to_use);
        }
        else
        {
            playsound(this, fire_sound, fire_sound_volume, vary_fire_sound);
            if (play_click)
                playsound(this, Dm.resource("sound/items/weapons/gun/general/ballistic_click.ogg"), fire_sound_volume, vary_fire_sound, frequency: click_frequency_to_use);
        }
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:68  GAME_VERB(/obj/item/gun/ballistic/revolver, spin, "Spin Chamber", null)
    public virtual void spin(params object[] __extra)
    {
        // The verb queues its body (__gvb_spin) through SSverb_manager: the verbs reach the client with the command layer (rules 7.10, M9).
        Dm.NotTranslated("code/modules/projectiles/guns/ballistic/revolver.dm:68", "spin: the Spin Chamber verb (verbs, M10+)");
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:68  (the verb's body, __gvb_spin)
    public virtual async DmTask __gvb_spin(DmList structured_args = null)
    {
        await spin_chamber(usr as mob_living);
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:63
    public virtual async DmTask spin_chamber(mob_living user = null)
    {
        if (user == null || IS_UNCONSCIOUS_OR_CRIT(user) || !Dm.truthy(in_range(user, this)))
            return;
        if (recent_spin > world.time)
            return;
        recent_spin = world.time + spin_delay;

        if (Dm.truthy(do_spin()))
        {
            playsound(usr, SFX_REVOLVER_SPIN, 30, FALSE);
            visible_message(span_notice(I18n.T("[] spins []'s chamber.", user, this)), span_notice(I18n.T("You spin []'s chamber.", this)));
            balloon_alert(user, I18n.T("chamber spun"));
        }
        else
        {
            // verbs -= /obj/item/gun/ballistic/revolver/verb/spin: the verb list is not translated (verbs, M9)
        }
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:79
    public virtual object do_spin()
    {
        obj_item_ammo_box_magazine_internal_cylinder C = magazine as obj_item_ammo_box_magazine_internal_cylinder;
        object dot = C != null ? TRUE : FALSE;
        if (Dm.truthy(dot))
        {
            C.spin();
            chamber_round(spin_cylinder: FALSE);
        }
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:86
    public override float get_ammo(float? countchambered = null, float? countempties = null)
    {
        countchambered ??= FALSE;
        countempties ??= TRUE;
        float boolets = 0; //mature var names for mature people
        if (chambered != null && Dm.truthy(countchambered))
            boolets++;
        if (magazine != null)
            boolets += magazine.ammo_count(countempties);
        return boolets;
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:94
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        float live_ammo = get_ammo(FALSE, FALSE);
        Dm.append(ref dot, Dm.text("[] of those are live rounds.", live_ammo != 0 ? (object)live_ammo : "None"));
        Dm.append(ref dot, span_notice(Dm.text("It can be spun with [].", EXAMINE_HINT("alt-click"))));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:100
    public override async DmTask<object> ignition_effect(atom A = null, mob user = null)
    {
        if (Dm.truthy(last_fire) && last_fire + 15 * SECONDS > world.time)
            return span_rose(Dm.text("[] touches the end of [] to \\the [], using the residual heat to ignite it in a puff of smoke. What a badass.", user, this, A));
        return null;
    }
}

// tg: code/modules/projectiles/guns/ballistic/revolver.dm:104
public partial class obj_item_gun_ballistic_revolver_c38 : obj_item_gun_ballistic_revolver
{
}

// tg: code/modules/projectiles/guns/ballistic/revolver.dm:153
public partial class obj_item_gun_ballistic_revolver_c38_detective : obj_item_gun_ballistic_revolver_c38
{
    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:168
    public override async DmTask setup_reskins()
    {
        await AddComponent(typeof(datum_component_reskinable_item), typeof(datum_atom_skin_det_revolver));
    }
}

// A gun to play Russian Roulette!
// You can spin the chamber to randomize the position of the bullet.
// tg: code/modules/projectiles/guns/ballistic/revolver.dm:212
public partial class obj_item_gun_ballistic_revolver_russian : obj_item_gun_ballistic_revolver
{
    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:226
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(Dm.text("You can change length of your pause before pulling the trigger with [].", EXAMINE_HINT("alt-right-click"))));
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:230
    public override async DmTask<object> click_alt_secondary(mob user = null)
    {
        if (loc != user)
        {
            to_chat(user, span_warning(I18n.T("You need to be holding the gun to determine how long you are going to pause!")));
            return CLICK_ACTION_BLOCKING;
        }
        object new_aim_time = await tgui_input_number(user, I18n.T("How long will you pause before pulling the trigger (seconds)?"), I18n.T("Do you feel lucky?"), aim_time / (1 * SECONDS), 10, 0);
        if (loc != user || Dm.truthy(user.incapacitated))
            return CLICK_ACTION_BLOCKING;
        aim_time = Dm.num(new_aim_time) * (1 * SECONDS);
        to_chat(user, span_warning(I18n.T("You're going to pause [] second\\s before pulling the trigger[].", aim_time, aim_time == 0 ? "... Good luck" : "")));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:241
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        aim_time = Dm.num(Dm.initial(this, (obj_item_gun_ballistic_revolver_russian p) => p.aim_time)); // next person chooses their own time
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:245
    public override object do_spin()
    {
        object dot = base.do_spin();
        if (Dm.truthy(dot))
            spun = TRUE;
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:250
    public override object can_shoot()
    {
        return TRUE; // we ALWAYS want to shoot. even if we don't have a chambered round, even if our chambered round has no bullet
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:253
    public override async DmTask<object> load_gun(obj_item ammo = null, mob_living user = null)
    {
        object dot = await base.load_gun(ammo, user);
        if (!Dm.truthy(dot))
            return dot;
        do_spin();
        return dot;
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:259
    public override async DmTask<object> can_trigger_gun(mob_living user = null, object akimbo_usage = null)
    {
        if (Dm.truthy(akimbo_usage))
            return FALSE;
        return await base.can_trigger_gun(user, akimbo_usage);
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:370  (syndicate G4a, D316)
    public override object on_mail_unwrap(atom source = null, mob user = null, obj_item_mail_traitor letter = null)
    {
        if ((get_ammo(FALSE, FALSE) > 1) || (get_ammo(TRUE, TRUE) < 6))
            return NONE;
        return base.on_mail_unwrap(source, user, letter);
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:264
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(spun))
        {
            await spin_chamber(user as mob_living);
            return TRUE;
        }
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:270
    public override async DmTask handle_chamber(float? empty_chamber = null, float? from_firing = null, float? chamber_next_round = null)
    {
        empty_chamber ??= TRUE;
        from_firing = FALSE; // never eject casings from firing the gun
        await base.handle_chamber(empty_chamber, from_firing, chamber_next_round);
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:274
    public override async DmTask<object> try_fire_gun(atom target = null, mob_living user = null, object @params = null)
    {
        if (Dm.truthy(user.combat_mode))
            return FALSE; // melee attack
        if (target != user)
        {
            await shoot_with_empty_chamber(user);
            spun = FALSE;
            user.visible_message(
                span_danger(I18n.T("[] tries to fire \\the [] aimed at something else, but only succeeds at looking like an idiot.", user, this)),
                span_danger(I18n.T("\\The []'s anti-combat mechanism prevents you from firing it at anyone but yourself!", this)));
            return TRUE; // no melee attack
        }
        if (!Dm.truthy(spun))
        {
            to_chat(user, span_warning(I18n.T("You need to spin \\the []'s chamber first!", this)));
            return TRUE; // no melee attack
        }
        if (HAS_TRAIT(user, TRAIT_CURSED)) // I cannot live, I cannot die, trapped in myself, body my holding cell.
        {
            to_chat(user, span_warning(I18n.T("What a horrible night... To have a curse!")));
            return TRUE; // no melee attack
        }
        if (loc != user)
        {
            if (Dm.truthy(tk_firing(user)))
                to_chat(user, span_warning(I18n.T("Russian roulette is stressful enough without trying to focus on telekinesis!")));
            else
                to_chat(user, span_warning(I18n.T("You need to be holding the gun to fire it!")));
            return TRUE; // no melee attack
        }
        return await base.try_fire_gun(target, user, @params); // try to shoot the gun
    }

    // Replaces clumsy check with a do after
    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:301
    public override async DmTask<object> check_botched(mob_living user = null, atom target = null)
    {
        if (aim_time <= 0)
            return FALSE;
        user.visible_message(
            span_danger(I18n.T("[] aims \\the [] at [] []...", user, this, user.p_their(), parse_zone(user.zone_selected))),
            span_userdanger(I18n.T("You aim \\the [] at your []...", this, parse_zone(user.zone_selected))),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        if (prob(10) && !HAS_TRAIT(user, TRAIT_FEARLESS))
            await user.adjust_jitter(aim_time);
        if (!Dm.truthy(await do_after(user, aim_time, target)))
        {
            if (!Dm.truthy(user.incapacitated))
            {
                user.visible_message(
                    span_danger(I18n.T("[] loses [] nerve and puts \\the [] down.", user, user.p_their(), this)),
                    span_userdanger(I18n.T("You lose your nerve and put \\the [] down.", this)),
                    visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
            }
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:321
    public override void before_firing(atom target = null, mob user = null)
    {
        if (target != user)
            throw Dm.CRASH("Russian revolver somehow got to before_firing with a target that isn't the user!", "code/modules/projectiles/guns/ballistic/revolver.dm", 323);
        // we will definitely have a chambered round, but not always projectile
        if (Dm.eq(check_zone((user as mob_living).zone_selected), BODY_ZONE_HEAD))
        {
            if (chambered.loaded_projectile != null)
            {
                chambered.loaded_projectile.damage = 300;
                chambered.loaded_projectile.wound_bonus = 100;
            }
        }
        else
        {
            if (chambered.loaded_projectile != null)
            {
                chambered.loaded_projectile.damage = 80;
                chambered.loaded_projectile.wound_bonus = 10;
            }
        }
    }

    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:332
    public override async DmTask<object> fire_gun(atom target = null, object user = null, object flag = null, object @params = null)
    {
        mob_living shooter = user as mob_living;
        // . = false = no shot fired
        object dot = await base.fire_gun(target, user, flag, @params);
        spun = FALSE;
        bool is_target_face = Dm.eq(check_zone(shooter.zone_selected), BODY_ZONE_HEAD);
        object aimed_at_readable = parse_zone(shooter.zone_selected);
        float loaded_rounds = get_ammo(FALSE, FALSE); // check before it is fired
        if (loaded_rounds != 0 && is_target_face)
        {
            add_memory_in_range(shooter, 7, typeof(datum_memory_witnessed_russian_roulette),
                Dm.named("protagonist", shooter),
                Dm.named("antagonist", this),
                Dm.named("rounds_loaded", loaded_rounds),
                Dm.named("aimed_at", aimed_at_readable),
                Dm.named("result", Dm.truthy(dot) ? "lost" : "won")
            );
        }

        if (!Dm.truthy(dot))
        {
            if (loaded_rounds != 0 && is_target_face)
                await shooter.add_mood_event("russian_roulette_win", typeof(datum_mood_event_russian_roulette_win), loaded_rounds);
            shooter.visible_message(
                span_danger(I18n.T("[][] points \\the [] at [] [], pulls the trigger, and... nothing happens!", shooter, is_target_face ? "" : " cowardly", this, shooter.p_their(), aimed_at_readable)),
                span_danger(I18n.T("You[] point \\the [] at your [], pull the trigger, and... nothing happens!", is_target_face ? "" : " cowardly", this, aimed_at_readable)),
                span_hear(I18n.T("You hear a click!")),
                vision_distance: COMBAT_MESSAGE_RANGE,
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
            return TRUE; // so they don't hit themselves in the forehead. because returning FALSE translates to "do melee attack" for whatever reason
        }

        shooter.visible_message(
            span_danger(I18n.T("[][] aims \\the [] at [] [] as it goes off!", shooter, is_target_face ? "" : " cowardly", this, shooter.p_their(), aimed_at_readable)),
            span_danger(I18n.T("You[] aim \\the [] at your [] as it goes off![]", is_target_face ? "" : " cowardly", this, aimed_at_readable, shooter.stat >= HARD_CRIT ? " <b>Everything suddenly goes black.</b>" : "")),
            span_hear(I18n.T("You hear a grunt[]!", !IS_UNCONSCIOUS_OR_CRIT(shooter) ? "" : ", followed by a thud")),
            vision_distance: COMBAT_MESSAGE_RANGE,
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        await shoot_self(shooter as mob_living_carbon_human, check_zone(shooter.zone_selected));
        return dot;
    }

    /// Called after successfully(if you can call it that) shooting ourselves
    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:376
    public virtual async DmTask shoot_self(mob_living_carbon_human user = null, object affecting = null)
    {
        affecting ??= BODY_ZONE_HEAD;
        await user.add_mood_event(
            "russian_roulette_lose",
            Dm.eq(affecting, BODY_ZONE_HEAD) ? typeof(datum_mood_event_russian_roulette_lose) : typeof(datum_mood_event_russian_roulette_lose_cheater));
    }
}

// tg: code/modules/projectiles/guns/ballistic/revolver.dm:382  (gaps GU1)
public partial class obj_item_gun_ballistic_revolver_russian_soul : obj_item_gun_ballistic_revolver_russian
{
    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:386
    public override async DmTask shoot_self(mob_living_carbon_human user = null, object affecting = null)
    {
        affecting ??= BODY_ZONE_HEAD;
        await base.shoot_self(user, affecting);
        if (Dm.eq(affecting, BODY_ZONE_HEAD))
        {
            obj_item_soulstone_anybody_revolver stone = Dm.@new<obj_item_soulstone_anybody_revolver>(user.drop_location());
            if (!Dm.truthy(await stone.capture_soul(user, forced: TRUE))) //Something went wrong
            {
                qdel(stone);
                return;
            }
            user.visible_message(
                span_danger(I18n.T("[]'s soul is captured by \\the []!", user, this)),
                span_userdanger(I18n.T("You've lost the gamble! Your soul is forfeit!")),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
            return;
        }

        user.visible_message(
            span_danger(I18n.T("[] is punished for trying to cheat the game!", user)),
            span_userdanger(I18n.T("You've lost the gamble! Not only is your soul forfeit, but it is whisked away for attempting to cheat death!")),
            visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
        await user.dust(drop_items: TRUE);
    }
}

//Fires directly at its user... unless the user is a clown, of course.
// tg: code/modules/projectiles/guns/ballistic/revolver.dm:407  (syndicate G4a, D316)
public partial class obj_item_gun_ballistic_revolver_reverse : obj_item_gun_ballistic_revolver
{
    // tg: code/modules/projectiles/guns/ballistic/revolver.dm:410
    public override async DmTask<object> can_trigger_gun(mob_living user = null, object akimbo_usage = null)
    {
        if (Dm.truthy(akimbo_usage))
            return FALSE;
        if (HAS_TRAIT(user, TRAIT_CLUMSY) || is_clown_job(user.mind?.assigned_role))
            return await base.can_trigger_gun(user, akimbo_usage);
        if (Dm.truthy(await process_fire(user, user, FALSE, null, BODY_ZONE_HEAD)))
        {
            user.visible_message(span_warning(I18n.T("[] somehow manages to shoot []self in the face!", user, user.p_them())), span_userdanger(I18n.T("You somehow shoot yourself in the face! How the hell?!")));
            await user.emote("scream");
            await user.drop_all_held_items();
            await user.Paralyze(80);
        }
        return null;
    }
}
