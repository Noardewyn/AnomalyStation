// Translated from tgstation (snapshot 1d99bd7f): code/modules/assembly/flash.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice E (flashes): the flash and the handheld flash (lines 1-308 and the handheld's materials); the arm implant's photon
// projector (:339-372) came with gaps package P6, the hypnotic flash with the syndicate campaign G2 (D316) and the cyborg flash
// with silicons SI2; the memorizer stays generated only (docs/ai/specs/not-translated.md section 46). The
// flash's picture is its "flash-f" overlay for half a second (update_overlays with flashing set), the victim's is the
// fullscreen flash of flash_act. The var block is generated (D111).

// tg: code/modules/assembly/flash.dm:3
public partial class obj_item_assembly_flash : obj_item_assembly
{
    // tg: code/modules/assembly/flash.dm:1
    private const float CONFUSION_STACK_MAX_MULTIPLIER = 2;

    // tg: code/modules/assembly/flash.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_IN_UNWRAPPED_TRAITOR_MAIL, nameof(on_mail_unwrap));
        return dot;
    }

    // tg: code/modules/assembly/flash.dm:36
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (Dm.truthy(burnt_out))
        {
            user.visible_message(span_suicide(I18n.T("[] raises \\the [] up to [] eyes and activates it ... but it's burnt out!", user, this, user.p_their())));
            return SHAME;
        }
        else if (Dm.truthy(user.is_blind()))
        {
            user.visible_message(span_suicide(I18n.T("[] raises \\the [] up to [] eyes and activates it ... but [] blind!", user, this, user.p_their(), user.p_theyre())));
            return SHAME;
        }
        user.visible_message(span_suicide(I18n.T("[] raises \\the [] up to [] eyes and activates it! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        await attack__proc(user, user);
        return FIRELOSS;
    }

    // tg: code/modules/assembly/flash.dm:47
    [DmParams("updates", "flash")]
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        updates ??= ALL;
        float flash = Dm.num(Dm.arg(__extra, 0));
        inhand_icon_state = Dm.truthy(burnt_out) ? "flashtool_burnt" : Dm.text("[]", Dm.initial(this, (obj_item_assembly_flash p) => p.inhand_icon_state));
        flashing = flash;
        object dot = await base.update_icon(updates, Dm.forward(__extra, flash));
        if (Dm.truthy(flash))
            _addtimer(Dm.@new<datum_callback>(this, nameof(update_icon)), 0.5f * SECONDS, file: "code/modules/assembly/flash.dm", line: 51);
        if (holder != null)
            await holder.update_icon(updates);
        return dot;
    }

    // tg: code/modules/assembly/flash.dm:55
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        attached_overlays = Dm.list();
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(burnt_out))
        {
            Dm.append(ref dot, "flashburnt");
            Dm.append(ref attached_overlays, "flashburnt");
        }
        if (Dm.truthy(flashing))
        {
            Dm.append(ref dot, flashing_overlay);
            Dm.append(ref attached_overlays, flashing_overlay);
        }
        return dot;
    }

    // tg: code/modules/assembly/flash.dm:65
    public override object update_name(float? updates = null)
    {
        updates ??= ALL;
        name = Dm.text("[]", Dm.truthy(burnt_out) ? Dm.text("burnt-out []", Dm.initial(this, (obj_item_assembly_flash p) => p.name)) : Dm.text("[]", Dm.initial(this, (obj_item_assembly_flash p) => p.name)));
        return base.update_name(updates);
    }

    // tg: code/modules/assembly/flash.dm:69
    public virtual async DmTask<object> clown_check(mob_living_carbon_human user = null)
    {
        if (HAS_TRAIT(user, TRAIT_CLUMSY) && prob(50))
        {
            await flash_mob(user, user, confusion_duration: 15 * SECONDS, targeted: FALSE);
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/assembly/flash.dm:75
    public virtual void burn_out() //Made so you can override it if you want to have an invincible flash from R&D or something.
    {
        if (!Dm.truthy(burnt_out))
        {
            burnt_out = TRUE;
            loc?.visible_message(span_danger(I18n.T("[] burns out!", this)), span_userdanger(I18n.T("[] burns out!", this)));
            update_appearance();
        }
    }

    // tg: code/modules/assembly/flash.dm:81
    public virtual object flash_recharge(float? interval = null)
    {
        interval ??= 10;
        float deciseconds_passed = world.time - last_used;
        for (float seconds = deciseconds_passed / 10; seconds >= interval; seconds -= interval.Value) //get 1 charge every interval
            times_used--;
        last_used = world.time;
        times_used = max(0, times_used); //sanity
        if (Dm.truthy(max(0, (prob(times_used * 3) ? 1 : 0) - burnout_resistance))) //The more often it's used in a short span of time the more likely it will burn out
        {
            burn_out();
            return FALSE;
        }
        return TRUE;
    }

    //BYPASS CHECKS ALSO PREVENTS BURNOUT!
    // tg: code/modules/assembly/flash.dm:93
    public virtual async DmTask<object> AOE_flash(float? bypass_checks = null, float? range = null, float? confusion_duration = null, mob user = null)
    {
        bypass_checks ??= FALSE;
        range ??= 3;
        confusion_duration ??= 5 * SECONDS;
        if (!Dm.truthy(bypass_checks) && !Dm.truthy(await try_use_flash()))
            return FALSE;
        DmList targets = get_flash_targets(get_turf(this), range, FALSE) as DmList;
        if (user != null)
        {
            Dm.remove(ref targets, user);
            to_chat(user, span_danger(I18n.T("[] emits a blinding light!", this)));
        }
        foreach (mob_living nearby_living in Dm.each_cast<mob_living>(targets))
            await flash_mob(nearby_living, user, confusion_duration, targeted: FALSE, generic_message: TRUE);
        return TRUE;
    }

    // tg: code/modules/assembly/flash.dm:104
    public virtual object get_flash_targets(atom target_loc = null, float? range = null, float? override_vision_checks = null)
    {
        range ??= 3;
        override_vision_checks ??= FALSE;
        if (target_loc == null)
            target_loc = loc;
        if (Dm.truthy(override_vision_checks))
            return get_hearers_in_view(range, get_turf(target_loc));
        if (isturf(target_loc) || (ismob(target_loc) && isturf(target_loc.loc)))
            return viewers(range, get_turf(target_loc));
        else
            return typecache_filter_list(target_loc.get_all_contents() as DmList, GLOB.typecache_living);
    }

    // tg: code/modules/assembly/flash.dm:114
    public virtual async DmTask<object> try_use_flash(mob user = null)
    {
        if (Dm.truthy(burnt_out) || (world.time < last_trigger + cooldown))
            return FALSE;
        last_trigger = world.time;
        playsound(this, Dm.resource("sound/items/weapons/flash.ogg"), 100, TRUE);
        await set_light_on(TRUE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(flash_end)), FLASH_LIGHT_DURATION, TIMER_OVERRIDE | TIMER_UNIQUE, file: "code/modules/assembly/flash.dm", line: 121);
        times_used++;
        if (!Dm.truthy(flash_recharge()))
            return FALSE;
        await update_icon(ALL, TRUE);
        update_name(ALL); //so if burnt_out was somehow reverted to 0 the name changes back to flash
        if (user != null && !Dm.truthy(await clown_check(user as mob_living_carbon_human)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/assembly/flash.dm:131
    public virtual async DmTask flash_end()
    {
        await set_light_on(FALSE);
    }

    /**
     * Handles actual flashing part of the attack
     *
     * This proc is awful in every sense of the way, someone should definitely refactor this whole code.
     * Arguments:
     * * M - Victim
     * * user - Attacker
     * * confusion_duration - handles the amount of confusion it gives you
     * * targeted - determines if it was aoe or targeted
     * * generic_message - checks if it should display default message.
     */
    // tg: code/modules/assembly/flash.dm:145
    public virtual async DmTask<object> flash_mob(mob_living flashed = null, mob user = null, float? confusion_duration = null, float? targeted = null, float? generic_message = null, object extra_log = null)
    {
        confusion_duration ??= 15 * SECONDS;
        targeted ??= TRUE;
        generic_message ??= FALSE;
        if (flashed == null)
            return FALSE;
        if (user != null)
            log_combat(user, flashed, Dm.text("[] []", Dm.truthy(targeted) ? "flashed(targeted)" : "flashed(AOE)", extra_log), this);
        else //caused by emp/remote signal
            log_combat(user, flashed, Dm.text("[] []", Dm.truthy(targeted) ? "flashed(targeted)" : "flashed(AOE)", extra_log), this);

        if (Dm.truthy(generic_message) && flashed != user)
            to_chat(flashed, span_danger(I18n.T("[] emits a blinding light!", this)));

        float deviation = calculate_deviation(flashed, (atom)user ?? this);

        if (user != null)
        {
            object sigreturn = !Dm.truthy(user._listen_lookup?[COMSIG_MOB_PRE_FLASHED_MOB]) ? NONE : user._SendSignal(COMSIG_MOB_PRE_FLASHED_MOB, Dm.list(user, flashed, this, deviation));
            if (Dm.truthy(Dm.bitand(sigreturn, STOP_FLASH)))
                return FALSE;

            if (Dm.truthy(Dm.bitand(sigreturn, DEVIATION_OVERRIDE_FULL)))
                deviation = DEVIATION_FULL;
            else if (Dm.truthy(Dm.bitand(sigreturn, DEVIATION_OVERRIDE_PARTIAL)))
                deviation = DEVIATION_PARTIAL;
            else if (Dm.truthy(Dm.bitand(sigreturn, DEVIATION_OVERRIDE_NONE)))
                deviation = DEVIATION_NONE;
        }

        //If you face away from someone they shouldn't notice any effects.
        if (deviation == DEVIATION_FULL)
            return FALSE;

        object flash_result = await flashed.flash_act(1, override_blindness_check: targeted, affect_silicon: TRUE);
        if (!Dm.truthy(flash_result))
        {
            if (Dm.truthy(targeted))
            {
                if (user != null)
                    visible_message(span_warning(I18n.T("[] fails to blind [] with the flash!", user, flashed)), span_danger(I18n.T("[] fails to blind you with the flash!", user)));
                else
                    to_chat(flashed, span_danger(I18n.T("[] fails to blind you!", this)));
            }
            return FALSE;
        }

        await flashed.adjust_confusion_up_to(confusion_duration, confusion_duration.Value * CONFUSION_STACK_MAX_MULTIPLIER);

        if (!Dm.truthy(targeted))
            return TRUE;

        if (!Dm.eq(flash_result, FLASH_COMPLETED)) //If the behavior was overwritten, we just skip the flashy stunny part and go with the override behavior instead
        {
            if (issilicon(flashed))
            {
                if (Dm.truthy(flashed.is_blind()))
                {
                    float flash_duration = rand(8, 12) * SECONDS;
                    await flashed.Paralyze(flash_duration);
                    await flashed.set_temp_blindness_if_lower(flash_duration);
                    if (user != null)
                        user.visible_message(span_warning(I18n.T("[] overloads []'s sensors and computing with the flash!", user, flashed)), span_danger(I18n.T("You overload []'s sensors and computing with the flash!", flashed)));
                    else
                        to_chat(flashed, I18n.T("[] overloads your sensors and computing!", this));
                }
                else
                {
                    await flashed.set_temp_blindness_if_lower(rand(5, 15) * SECONDS);
                    if (user != null)
                        user.visible_message(span_warning(I18n.T("[] blinds [] with the flash!", user, flashed)), span_danger(I18n.T("You blind [] with the flash!", flashed)));
                    else
                        to_chat(flashed, I18n.T("You're blinded by []!", this));
                }
            }
            else
            {
                //easy way to make sure that you can only long stun someone who is facing in your direction
                await flashed.adjust_stamina_loss(rand(80, 120) * (1 - (deviation * 0.5f)));
                await flashed.Knockdown(rand(25, 50) * (1 - (deviation * 0.5f)));
                if (user != null)
                    visible_message(span_danger(I18n.T("[] blinds [] with the flash!", user, flashed)), span_userdanger(I18n.T("[] blinds you with the flash!", user)));
                else
                    to_chat(flashed, I18n.T("You're blinded by []!", this));
            }
        }

        if (user != null && Dm.truthy(user._listen_lookup?[COMSIG_MOB_SUCCESSFUL_FLASHED_MOB]))
            user._SendSignal(COMSIG_MOB_SUCCESSFUL_FLASHED_MOB, Dm.list(user, flashed, this, deviation));
        return TRUE;
    }

    /**
     * Handles the directionality of the attack
     *
     * Returns the amount of 'deviation', 0 being facing each other, 1 being sideways, 2 being facing away from each other.
     * Arguments:
     * * victim - Victim
     * * attacker - Attacker
     */
    // tg: code/modules/assembly/flash.dm:225
    public virtual float calculate_deviation(mob victim = null, atom attacker = null)
    {
        // Tactical combat emote-spinning should not counter intended gameplay mechanics.
        // This trumps same-loc checks to discourage floor spinning in general to counter flashes.
        // In short, combat spinning is silly and you should feel silly for doing it.
        if (HAS_TRAIT(victim, TRAIT_SPINNING))
            return DEVIATION_NONE;

        // I don't want to mess with cyborgs' deep-rooted weakness to flashes
        if (issilicon(victim))
            return DEVIATION_NONE;

        if (iscarbon(victim))
        {
            mob_living_carbon carbon_victim = victim as mob_living_carbon;
            if (Dm.num(carbon_victim.get_eye_protection()) < FLASH_PROTECTION_SENSITIVE) // If we have really bad flash sensitivity, usually due to really sensitive eyes, we get flashed from all directions
                return DEVIATION_NONE;
        }

        turf attacker_turf = get_turf(attacker);

        // Are they on the same tile? We'll return partial deviation. This may be someone flashing while lying down
        // or flashing someone they're stood on the same turf as, or a borg flashing someone buckled to them.
        if (victim.loc == attacker_turf)
            return DEVIATION_PARTIAL;

        // If the victim was looking at the attacker, this is the direction they'd have to be facing.
        float victim_to_attacker = get_dir(victim, attacker_turf);
        // The victim's dir is necessarily a cardinal value.
        float victim_dir = victim.dir;

        // - - -
        // - V - Victim facing south
        // # # #
        // Attacker within 45 degrees of where the victim is facing.
        if (Dm.truthy(Dm.bitand(victim_dir, victim_to_attacker)))
            return DEVIATION_NONE;

        // # # #
        // - V - Victim facing south
        // - - -
        // Attacker at 135 or more degrees of where the victim is facing.
        if (Dm.truthy(Dm.bitand(victim_dir, REVERSE_DIR(victim_to_attacker))))
            return DEVIATION_FULL;

        // - - -
        // # V # Victim facing south
        // - - -
        // Attacker lateral to the victim.
        return DEVIATION_PARTIAL;
    }

    // tg: code/modules/assembly/flash.dm:273
    [SignalHandler]
    public virtual object on_mail_unwrap(atom source = null, mob user = null, obj letter = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(try_use_flash())))
            return NONE;
        to_chat(user, span_danger(I18n.T("As you open [], a very bright light shoots out from inside!", letter)));
        Waiting.NoSleep(flash_mob(user as mob_living));
        Waiting.NoSleep(forceMove(user.loc as atom));
        return COMPONENT_TRAITOR_MAIL_HANDLED;
    }

    // tg: code/modules/assembly/flash.dm:282
    [DmParams("target", "user")]
    public override async DmTask<object> attack__proc(mob_living target = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await try_use_flash(user)))
            return FALSE;

        await flash_mob(target, user, confusion_duration: 5 * SECONDS, targeted: TRUE);
        return TRUE;
    }

    // tg: code/modules/assembly/flash.dm:289
    [DmParams("user", "flag", "emp")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(holder))
            return FALSE;
        if (!Dm.truthy(await AOE_flash(user: user)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/assembly/flash.dm:296
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (!Dm.truthy(await AOE_flash()))
            return dot;
        burn_out();
        return dot;
    }

    // tg: code/modules/assembly/flash.dm:304
    public override async DmTask<object> activate(mob activator = null)//AOE flash on signal received
    {
        if (!Dm.truthy(await base.activate(activator)))
            return null;
        await AOE_flash();
        return null;
    }
}

// tg: code/modules/assembly/flash.dm:309
public partial class obj_item_assembly_flash_cyborg : obj_item_assembly_flash
{
    // tg: code/modules/assembly/flash.dm:311
    public override async DmTask<object> attack__proc(mob_living target = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target, user, modifiers, attack_modifiers, __extra);
        if (Dm.truthy(dot))
            Dm.@new<obj_effect_temp_visual_borgflash>(get_turf(this));
        return dot;
    }

    // tg: code/modules/assembly/flash.dm:316
    [DmParams("user", "flag", "emp")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            Dm.@new<obj_effect_temp_visual_borgflash>(get_turf(this));
        return dot;
    }

    // tg: code/modules/assembly/flash.dm:321
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/assembly/flash.dm:324
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item I = null)
    {
        return ITEM_INTERACT_BLOCKING;
    }
}

// tg: code/modules/assembly/flash.dm:339
public partial class obj_item_assembly_flash_armimplant : obj_item_assembly_flash
{
    // tg: code/modules/assembly/flash.dm:346
    public override void burn_out()
    {
        obj_item_organ_cyberimp_arm_toolkit_flash real_arm = arm?.resolve() as obj_item_organ_cyberimp_arm_toolkit_flash;
        if (real_arm?.owner != null)
        {
            to_chat(real_arm.owner, span_warning(I18n.T("Your photon projector implant overheats and deactivates!")));
            Waiting.NoSleep(real_arm.Retract());
        }
        overheat = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(cooldown__proc)), flashcd * 2, file: "code/modules/assembly/flash.dm", line: 352);
    }

    // tg: code/modules/assembly/flash.dm:354
    public override async DmTask<object> try_use_flash(mob user = null)
    {
        if (Dm.truthy(overheat))
        {
            obj_item_organ_cyberimp_arm_toolkit_flash real_arm = arm?.resolve() as obj_item_organ_cyberimp_arm_toolkit_flash;
            if (real_arm?.owner != null)
                to_chat(real_arm.owner, span_warning(I18n.T("Your photon projector is running too hot to be used again so quickly!")));
            return FALSE;
        }
        overheat = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(cooldown__proc)), flashcd, file: "code/modules/assembly/flash.dm", line: 363);
        playsound(this, Dm.resource("sound/items/weapons/flash.ogg"), 100, TRUE);
        await update_icon(ALL, TRUE);
        return TRUE;
    }

    // tg: code/modules/assembly/flash.dm:367  (the proc cooldown, renamed: the var cooldown, rules 2.4)
    public virtual void cooldown__proc()
    {
        overheat = FALSE;
    }

    // tg: code/modules/assembly/flash.dm:370
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item I = null)
    {
        to_chat(user, span_notice(I18n.T("\\The [] is an implant! It cannot be unsecured!", this)));
        add_fingerprint(user);
        return null;
    }
}

// The syndicate campaign G2 (D316): the hypnotic flash, which never burns out and sends a vulnerable mind into a trance.
// tg: code/modules/assembly/flash.dm:373
public partial class obj_item_assembly_flash_hypnotic : obj_item_assembly_flash
{
    // tg: code/modules/assembly/flash.dm:1  #define CONFUSION_STACK_MAX_MULTIPLIER 2 (the file's define, private to each class of the mirror)
    private const float CONFUSION_STACK_MAX_MULTIPLIER = 2;

    // tg: code/modules/assembly/flash.dm:380
    public override void burn_out()
    {
        return;
    }

    // tg: code/modules/assembly/flash.dm:383
    public override async DmTask<object> flash_mob(mob_living flashed = null, mob user = null, float? confusion_duration = null, float? targeted = null, float? generic_message = null, object extra_log = null)
    {
        confusion_duration ??= 5 * SECONDS;
        targeted ??= TRUE;
        generic_message ??= FALSE;
        if (user != null)
            log_combat(user, flashed, Dm.text("[] []", Dm.truthy(targeted) ? "hypno-flashed(targeted)" : "hypno-flashed(AOE)", extra_log), this);
        else //caused by emp/remote signal
            log_combat(user, flashed, Dm.text("[] []", Dm.truthy(targeted) ? "hypno-flashed(targeted)" : "hypno-flashed(AOE)", extra_log), this);

        if (Dm.truthy(generic_message) && flashed != user)
            to_chat(flashed, span_notice(I18n.T("[] emits a soothing light...", this)));

        if (!Dm.truthy(await flashed.flash_act(1, override_blindness_check: targeted, affect_silicon: TRUE)))
        {
            if (Dm.truthy(targeted))
            {
                if (user != null)
                    user.visible_message(span_warning(I18n.T("[] fails to blind [] with the flash!", user, flashed)), span_warning(I18n.T("You fail to hypno-flash []!", flashed)));
                else
                    to_chat(flashed, span_danger(I18n.T("[] fails to blind you!", this)));
            }
            return FALSE;
        }

        if (!Dm.truthy(targeted))
        {
            to_chat(flashed, span_notice(I18n.T("Such a pretty light...")));
            await flashed.adjust_confusion_up_to(confusion_duration, confusion_duration.Value * 2 * CONFUSION_STACK_MAX_MULTIPLIER);
            await flashed.adjust_dizzy_up_to(8 * SECONDS, 40 * SECONDS);
            await flashed.adjust_drowsiness_up_to(8 * SECONDS, 40 * SECONDS);
            await flashed.adjust_timed_status_effect(4 * SECONDS, typeof(datum_status_effect_pacify)); // adjust_pacifism(4 SECONDS)
            return TRUE;
        }

        if (user != null)
            user.visible_message(span_danger(I18n.T("[] blinds [] with the flash!", user, flashed)), span_danger(I18n.T("You hypno-flash []!", flashed)));
        else
            to_chat(flashed, I18n.T("You're blinded by []!", this));

        if (!Dm.truthy(flashed.hypnosis_vulnerable()))
        {
            to_chat(flashed, span_hypnophrase(I18n.T("The light makes you feel oddly relaxed...")));
            await flashed.adjust_confusion_up_to(confusion_duration.Value * 2, confusion_duration.Value * 2 * CONFUSION_STACK_MAX_MULTIPLIER);
            await flashed.adjust_dizzy_up_to(20 * SECONDS, 40 * SECONDS);
            await flashed.adjust_drowsiness_up_to(20 * SECONDS, 40 * SECONDS);
            await flashed.adjust_timed_status_effect(10 * SECONDS, typeof(datum_status_effect_pacify)); // adjust_pacifism(10 SECONDS)
        }
        else
        {
            await flashed.apply_status_effect(typeof(datum_status_effect_trance), 200, TRUE);
        }
        return null;
    }
}
