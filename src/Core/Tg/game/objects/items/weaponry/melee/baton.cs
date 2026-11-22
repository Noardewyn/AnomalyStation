// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/baton.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice E (batons): the police baton, the telescopic baton (and the gold, silver, bronze and contractor ones, which only
// change vars), the stun baton with its loaded and HoS variants, the stunprod and the OZtek boomerang, whole but for the
// stunsword's reskins (the reskinable item component; docs/ai/specs/not-translated.md section 46); gaps WP1 (D333): the fantasy bonuses,
// the stunsword, the teleprod (it teleports whom it stuns), the snatcherprod (it pulls the held item into its wielder's other hand)
// and the syndicate's nunchaku (a random pose after each swing and throw; in throw mode it blocks melee and thrown attacks for a
// little stamina and strikes back at a melee attacker). add_baton_notes is the weapon
// description element's readout (D291: the element, the baton's add_weapon_description and the security and contractor
// batons' lore are translated). The var blocks are generated (D111).

// tg: code/game/objects/items/weaponry/melee/baton.dm:1
public partial class obj_item_melee_baton : obj_item_melee
{
    // tg: code/game/objects/items/weaponry/melee/baton.dm:75
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        register_item_context();
        Waiting.NoSleep(add_deep_lore());
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:81
    public override async DmTask add_weapon_description()
    {
        await AddElement(typeof(datum_element_weapon_description), Dm.named("attached_proc", nameof(add_baton_notes)));
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:84
    public virtual object add_baton_notes()
    {
        DmList readout = Dm.list();

        if (Dm.truthy(affect_cyborg))
            Dm.append(ref readout, Dm.text("It can stun cyborgs for [] seconds.", round(stun_time_cyborg / 10, 1)));

        Dm.append(ref readout, Dm.text("\n[]", Dm.truthy(active) ? Dm.text("It is currently [], and capable of stunning.", span_warning(activated_word)) : Dm.text("It is [], and not capable of stunning.", span_warning(Dm.text("not []", activated_word)))));

        if (stamina_damage <= 0) // The advanced baton actually does have 0 stamina damage so...yeah.
        {
            Dm.append(ref readout, Dm.text("Either it is [], or it [].", span_warning("completely unable to perform a stunning strike"), span_warning("attacks via some unusual method")));
            return readout.Join("\n");
        }

        Dm.append(ref readout, Dm.text("It takes [] to stun an enemy.", span_warning(Dm.text("[] strike[]", HITS_TO_CRIT(stamina_damage), "s"))));

        Dm.append(ref readout, Dm.text("\nThe effects of each strike can be mitigated by utilizing [] armor.", span_warning(armour_type_against_stun)));

        Dm.append(ref readout, Dm.text("\nIt has a stun armor-piercing capability of [].", span_warning(Dm.text("[]%", stun_armour_penetration))));
        return readout.Join("\n");
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:103
    public virtual async DmTask add_deep_lore()
    {
        return;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:206
    public override async DmTask apply_fantasy_bonuses(object bonus = null)
    {
        await base.apply_fantasy_bonuses(bonus);
        stamina_damage = Dm.num(modify_fantasy_variable("stamina_damage", stamina_damage, Dm.num(bonus) * 4));
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:210
    public override async DmTask remove_fantasy_bonuses(object bonus = null)
    {
        stamina_damage = Dm.num(reset_fantasy_variable("stamina_damage", stamina_damage));
        await base.remove_fantasy_bonuses(bonus);
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:106  #define STUN_ATTACK "stun attack" (a file-local define)
    private const string STUN_ATTACK = "stun attack";

    /**
     * Checks if we can stun targets with the baton.
     *
     * Impure (has chat feedback and mutates state in some subtypes).
     * In other words don't call this unless a stun is being attempted.
     *
     * * target - The mob being hit with the baton
     * * user - The mob using the baton
     * * harmbatonning - Whether or not this is being called from a harmbaton attack
     *
     * Returns TRUE if we can stun the target, FALSE if we cannot.
     */
    // tg: code/game/objects/items/weaponry/melee/baton.dm:120
    public virtual object try_stun(mob_living target = null, mob_living user = null, object harmbatonning = null)
    {
        if (!Dm.truthy(active))
            return FALSE;
        if (!Dm.truthy(chunky_finger_usable) && ishuman(user))
        {
            mob_living_carbon_human human_user = user as mob_living_carbon_human;
            if (Dm.truthy(human_user.check_chunky_fingers()) && Dm.truthy(user.is_holding(this)) && !HAS_MIND_TRAIT(user, TRAIT_CHUNKYFINGERS_IGNORE_BATON))
            {
                if (!Dm.truthy(harmbatonning))
                    balloon_alert(human_user, I18n.T("fingers are too big!"));
                return FALSE;
            }
        }
        if (!(cooldown_check <= world.time))
        {
            if (Dm.truthy(wait_desc) && !Dm.truthy(harmbatonning))
                balloon_alert(user, I18n.T(wait_desc));
            return FALSE;
        }
        if (HAS_TRAIT_FROM(target, TRAIT_IWASBATONED, REF(user))) //no doublebaton abuse anon!
        {
            if (!Dm.truthy(harmbatonning))
                target.balloon_alert(user, I18n.T("can't stun yet!"));
            return FALSE;
        }
        return TRUE;
    }

    // Stun attack
    // tg: code/game/objects/items/weaponry/melee/baton.dm:141
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.pre_attack(target, user, modifiers, attack_modifiers);
        if (Dm.truthy(dot))
            return TRUE;

        if (!isliving(target))
            return FALSE; // bashing objects

        object harmbatonning = LAZYACCESS(modifiers, RIGHT_CLICK);
        if (Dm.truthy(harmbatonning) && !Dm.truthy(stun_on_harmbaton))
            return FALSE; // harmbatonning, no stun

        // see if this attack will result in a stun, or if we need to cancel it
        if (!Dm.truthy(try_stun(target as mob_living, user, harmbatonning)))
        {
            if (Dm.truthy(harmbatonning))
                return FALSE; // if harmbatonning, ALwAYS attack
            if (Dm.truthy(active))
                return TRUE;  // if active, but can't stun? no attack
            if (!Dm.truthy(user.combat_mode))
                return TRUE;  // if not in combat mode? no attack

            return FALSE; // otherwise, attack normally
        }

        // clumsy people redirect this attack - yes, this bypasses IWASBATONED and such
        if (HAS_TRAIT(user, TRAIT_CLUMSY) && prob(50))
        {
            user.visible_message(
                span_danger(I18n.T("[] accidentally hits []self over the head with []! What a doofus!", user, user.p_them(), this)),
                span_userdanger(I18n.T("You accidentally hit yourself over the head with []!", this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );

            await finalize_baton_attack(user, user, TRUE);
            await user.apply_damage(2 * force, BRUTE, BODY_ZONE_HEAD, attacking_item: this);
            log_combat(user, user, Dm.text("accidentally stun attacked []self due to their clumsiness", user.p_them()), this);
            await user.do_attack_animation(user);
            user.changeNext_move(attack_speed);
            return TRUE; // you hit yourself, nerd
        }

        // if not harm batonning, we override the default attack properties do do nothing
        if (!Dm.truthy(harmbatonning))
        {
            // SET_ATTACK_FORCE(attack_modifiers, 0), MUTE_ATTACK_HITSOUND(attack_modifiers), HIDE_ATTACK_MESSAGES(attack_modifiers)
            attack_modifiers ??= Dm.list();
            attack_modifiers[FORCE_OVERRIDE] = 0;
            attack_modifiers[SILENCE_HITSOUND] = TRUE;
            attack_modifiers[SILENCE_DEFAULT_MESSAGES] = TRUE;
        }
        // then denote the attack as "will stun" for afterattack
        attack_modifiers ??= Dm.list(); // LAZYSET
        attack_modifiers[STUN_ATTACK] = TRUE;
        return FALSE; // attack and stun
    }

    // This is where stun gets applied
    // tg: code/game/objects/items/weaponry/melee/baton.dm:189
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (QDELETED(target) || !Dm.truthy(LAZYACCESS(attack_modifiers, STUN_ATTACK)))
            return;

        await finalize_baton_attack(target as mob_living, user as mob_living);

        DmList desc;
        if (iscyborg(target))
        {
            desc = get_cyborg_stun_description(target as mob_living, user as mob_living) as DmList;
            if (!Dm.truthy(affect_cyborg))
                playsound(this, Dm.resource("sound/effects/bang.ogg"), 10, TRUE); //bonk
        }
        else
        {
            desc = get_stun_description(target as mob_living, user as mob_living) as DmList;
        }

        if (desc != null)
            target.visible_message(desc["visible"], desc["local"], visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE);
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:214
    public override object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        if (isturf(target))
            return NONE;

        if (isobj(target))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Attack";
        }
        else
        {
            if (Dm.truthy(active))
            {
                context[SCREENTIP_CONTEXT_RMB] = context_living_rmb_active;

                if (Dm.truthy(user.combat_mode))
                    context[SCREENTIP_CONTEXT_LMB] = context_living_target_active_combat_mode;
                else
                    context[SCREENTIP_CONTEXT_LMB] = context_living_target_active;
            }
            else
            {
                context[SCREENTIP_CONTEXT_RMB] = context_living_rmb_inactive;

                if (Dm.truthy(user.combat_mode))
                    context[SCREENTIP_CONTEXT_LMB] = context_living_target_inactive_combat_mode;
                else
                    context[SCREENTIP_CONTEXT_LMB] = context_living_target_inactive;
            }
        }

        return CONTEXTUAL_SCREENTIP_SET;
    }

    /// Wrapper for calling "stun()" and doing relevant vfx/sfx
    // tg: code/game/objects/items/weaponry/melee/baton.dm:239
    public virtual async DmTask finalize_baton_attack(mob_living target = null, mob_living user = null, float? clumsy = null)
    {
        clumsy ??= FALSE;
        cooldown_check = world.time + cooldown; // COOLDOWN_START(src, cooldown_check, cooldown)
        if (Dm.truthy(on_stun_sound))
            playsound(this, on_stun_sound, on_stun_volume, TRUE, -1);
        if (Dm.truthy(await baton_effect(target, user, null, clumsy)) && user != null)
        {
            set_batoned(target, user, cooldown);
            log_combat(user, target, "stunned", name);
        }
    }

    /// The actual "stun()" of the stun baton
    // tg: code/game/objects/items/weaponry/melee/baton.dm:248
    public virtual async DmTask<object> baton_effect(mob_living target = null, mob_living user = null, object stun_override = null, object clumsy = null, params object[] __extra)
    {
        bool trait_check = HAS_TRAIT(target, TRAIT_BATON_RESISTANCE);
        if (iscyborg(target))
        {
            if (!Dm.truthy(affect_cyborg))
                return FALSE;
            await target.flash_act(affect_silicon: TRUE);
            await target.Paralyze(Dm.num(stun_override == null ? stun_time_cyborg : stun_override) * (trait_check ? 0.1f : 1));
            additional_effects_cyborg(target, user);
        }
        else
        {
            if (ishuman(target))
            {
                mob_living_carbon_human human_target = target as mob_living_carbon_human;
                if (prob(force_say_chance))
                    human_target.force_say();
            }
            object armour_block = target.run_armor_check(BODY_ZONE_CHEST, armour_type_against_stun, null, null, stun_armour_penetration);
            await target.apply_damage(stamina_damage, STAMINA, blocked: Dm.num(armour_block));
            if (!trait_check)
                await target.Knockdown(stun_override == null ? knockdown_time : stun_override);
            await additional_effects_non_cyborg(target, user);
        }
        if (Dm.truthy(target._listen_lookup?[COMSIG_MOB_BATONED]))
            target._SendSignal(COMSIG_MOB_BATONED, Dm.list(target, user, this));
        return TRUE;
    }

    /// Default message for stunning a living, non-cyborg mob.
    // tg: code/game/objects/items/weaponry/melee/baton.dm:271
    public virtual object get_stun_description(mob_living target = null, mob_living user = null)
    {
        DmList dot = Dm.list();
        dot["visible"] = span_danger(I18n.T("[] knocks [] down with []!", user, target, this));
        dot["local"] = span_userdanger(I18n.T("[] knocks you down with []!", user, this));
        return dot;
    }

    /// Default message for stunning a cyborg.
    // tg: code/game/objects/items/weaponry/melee/baton.dm:278
    public virtual object get_cyborg_stun_description(mob_living target = null, mob_living user = null)
    {
        DmList dot = Dm.list();
        if (Dm.truthy(affect_cyborg))
        {
            dot["visible"] = span_danger(I18n.T("[] pulses []'s sensors with the baton!", user, target));
            dot["local"] = span_danger(I18n.T("You pulse []'s sensors with the baton!", target));
        }
        else
        {
            dot["visible"] = span_danger(I18n.T("[] tries to knock down [] with [], and predictably fails!", user, target, this)); //look at this duuuuuude
            dot["local"] = span_userdanger(I18n.T("[] tries to... knock you down with []?", user, this)); //look at the top of his head!
        }
        return dot;
    }

    /// Contains any special effects that we apply to living, non-cyborg mobs we stun. Does not include applying a knockdown, dealing stamina damage, etc.
    // tg: code/game/objects/items/weaponry/melee/baton.dm:289
    public virtual async DmTask additional_effects_non_cyborg(mob_living target = null, mob_living user = null)
    {
        return;
    }

    /// Contains any special effects that we apply to cyborgs we stun. Does not include flashing the cyborg's screen, hardstunning them, etc.
    // tg: code/game/objects/items/weaponry/melee/baton.dm:294
    public virtual void additional_effects_cyborg(mob_living target = null, mob_living user = null)
    {
        return;
    }

    /// Used in marking a target as being hit by a baton
    // tg: code/game/objects/items/weaponry/melee/baton.dm:299
    public virtual void set_batoned(mob_living target = null, mob_living user = null, object cooldown = null)
    {
        if (!Dm.truthy(cooldown))
            return;
        string user_ref = REF(user) as string; // avoids harddels.
        ADD_TRAIT(target, TRAIT_IWASBATONED, user_ref);
        _addtimer(TRAIT_CALLBACK_REMOVE(target, TRAIT_IWASBATONED, user_ref), Dm.num(cooldown), file: "code/game/objects/items/weaponry/melee/baton.dm", line: 323);
    }
}

// tg: code/game/objects/items/weaponry/melee/baton.dm:316
public partial class obj_item_melee_baton_telescopic : obj_item_melee_baton
{
    // tg: code/game/objects/items/weaponry/melee/baton.dm:350
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_transforming),
            Dm.named("force_on", active_force),
            Dm.named("hitsound_on", hitsound),
            Dm.named("w_class_on", WEIGHT_CLASS_NORMAL),
            Dm.named("clumsy_check", FALSE),
            Dm.named("attack_verb_continuous_on", Dm.list("smacks", "strikes", "cracks", "beats")),
            Dm.named("attack_verb_simple_on", Dm.list("smack", "strike", "crack", "beat"))
        ));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:363
    public override async DmTask additional_effects_non_cyborg(mob_living target = null, mob_living user = null)
    {
        await target.apply_status_effect(typeof(datum_status_effect_dazed));
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:366
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        mob_living_carbon_human human_user = user as mob_living_carbon_human;
        obj_item_organ_brain our_brain = human_user.get_organ_by_type(typeof(obj_item_organ_brain)) as obj_item_organ_brain;

        user.visible_message(span_suicide(I18n.T("[] stuffs [] up [] nose and presses the 'extend' button! It looks like [] trying to clear [] mind.", user, this, user.p_their(), user.p_theyre(), user.p_their())));
        if (Dm.truthy(active))
        {
            playsound(this, on_sound, 50, TRUE);
            add_fingerprint(user);
        }
        else
        {
            await attack_self(user);
        }

        await sleep(0.3f * SECONDS);
        if (QDELETED(human_user))
            return null;
        if (!QDELETED(our_brain))
        {
            Dm.remove(ref human_user.organs, our_brain);
            qdel(our_brain);
        }
        Dm.@new<obj_effect_gibspawner_generic>(human_user.drop_location(), human_user);
        return BRUTELOSS;
    }

    /*
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     *
     * Gives feedback to the user and makes it show up inhand.
     */
    // tg: code/game/objects/items/weaponry/melee/baton.dm:391
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        this.active = Dm.num(active);
        inhand_icon_state = Dm.truthy(active) ? on_inhand_icon_state as string : null; // When inactive, there is no inhand icon_state.
        if (user != null)
            balloon_alert(user, I18n.T(Dm.truthy(active) ? "extended" : "collapsed"));
        if (!Dm.truthy(active))
        {
            drop_sound = folded_drop_sound;
            pickup_sound = folded_pickup_sound;
        }
        else
        {
            drop_sound = unfolded_drop_sound;
            pickup_sound = unfolded_pickup_sound;
        }
        playsound(this, on_sound, 50, TRUE);
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }
}

// tg: code/game/objects/items/weaponry/melee/baton.dm:424
public partial class obj_item_melee_baton_telescopic_contractor_baton : obj_item_melee_baton_telescopic
{
    // tg: code/game/objects/items/weaponry/melee/baton.dm:1032
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore_hint", span_notice(I18n.T("You can [] to learn a little more about [].", EXAMINE_HINT(I18n.T("look closer")), this))),
            Dm.named("lore", I18n.T("The Contract Acquisition Device (sometimes referred to as the CAD in encrypted correspondence) is the most frequently encountered example of Cybersun Industries weaponry. Similar in purpose to Nanotrasen's own Secure Apprehension Device, the baton is capable of inducing rapid CNS disruption in a target to render them helpless. It also makes for an effective bludgeon, another quality it shares with the stun baton. To maximize ease of concealment, the baton is also able to be telescopically collapsed, to then be rapidly deployed at the pull of a trigger.<br><br>The contractor baton is famously associated with contractors, elite Cybersun field agents sent to kidnap and extract high value enemy personnel for interrogation. Anyone with the potential to possess classified or sensitive data about Nanotrasen could find themselves a target for Cybersun. The company relentlessly employs contractors to probe Nanotrasen for vulnerabilities, starting with their employees.")));
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:452
    public override async DmTask additional_effects_non_cyborg(mob_living target = null, mob_living user = null)
    {
        await base.additional_effects_non_cyborg(target, user);
        await target.set_jitter_if_lower(40 * SECONDS * (HAS_TRAIT(target, TRAIT_BATON_RESISTANCE) ? 0.5f : 1));
        await target.set_stutter_if_lower(40 * SECONDS * (HAS_TRAIT(target, TRAIT_BATON_RESISTANCE) ? 0.5f : 1));
    }
}

// tg: code/game/objects/items/weaponry/melee/baton.dm:457
public partial class obj_item_melee_baton_security : obj_item_melee_baton
{
    // tg: code/game/objects/items/weaponry/melee/baton.dm:515
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (preload_cell_type != null)
        {
            if (!ispath(preload_cell_type, typeof(obj_item_stock_parts_power_store_cell)))
                log_mapping(Dm.text("[] at [] had an invalid preload_cell_type: [].", this, AREACOORD(this), preload_cell_type));
            else
                cell = Dm.@new(preload_cell_type, this) as obj_item_stock_parts_power_store;
        }
        RegisterSignal(this, COMSIG_ATOM_ATTACKBY, nameof(convert));
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:1011
    public override async DmTask add_deep_lore()
    {
        await AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore_hint", span_notice(I18n.T("You can [] to learn a little more about [].", EXAMINE_HINT(I18n.T("look closer")), this))),
            Dm.named("lore", I18n.T("The Secure Apprehension Device (sometimes referred to as the SAD in the officer training manuals) is an unholy union of mace and cattleprod. Designed to stop criminals in their tracks, Nanotrasen security members are rarely without their trusty stun baton. Assuming they haven't lost it somewhere.<br><br>Trained to 'baton first, interrogate later', Nanotrasen security has long since earned itself a mixed reputation. The device is able to rapidly shut down the central nervous system of a criminal with only a few direct applications of the conductive striking head.<br><br>TerraGov law enforcement has avoided the adoption of stun batons due to various ethical dilemmas posed by their utilization. Studies of their usage have shown numerous longterm physical and mental ramifications caused by being struck by a human cattleprod. Citizens' rights advocacy groups protest against the proliferation of stun batons as a policing tool, arguing that they are 'inhumane' and 'authoritarian'. Nanotrasen, on the other hand, has had no such qualms when deploying stun batons as a compliance measure across all of their existing stations and facilities against their own unruly members of staff.")));
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:525
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:528
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (Dm.truthy(cell?.charge) && Dm.truthy(active))
        {
            user.visible_message(span_suicide(I18n.T("[] is putting the live [] in [] mouth! It looks like [] trying to commit suicide!", user, name, user.p_their(), user.p_theyre())));
            await finalize_baton_attack(user, user);
            return FIRELOSS;
        }
        else
        {
            user.visible_message(span_suicide(I18n.T("[] is shoving \\the [] down their throat! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
            return OXYLOSS;
        }
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:537
    public override object Destroy(float? force = null)
    {
        if (cell != null)
            QDEL_NULL(ref cell);
        UnregisterSignal(this, COMSIG_ATOM_ATTACKBY);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:543
    [SignalHandler]
    public virtual void convert(datum source = null, obj_item item = null, mob user = null)
    {
        if (!istype(item, typeof(obj_item_conversion_kit)) || !Dm.truthy(convertible))
            return;
        turf source_turf = get_turf(this);
        obj_item_melee_baton baton = Dm.@new<obj_item_melee_baton>(source_turf);
        baton.alpha = 20;
        playsound(source_turf, Dm.resource("sound/items/tools/drill_use.ogg"), 80, TRUE, -1);
        animate(this, Dm.named("alpha", 0), Dm.named("time", 1 * SECONDS));
        animate(baton, Dm.named("alpha", 255), Dm.named("time", 1 * SECONDS));
        qdel(item);
        qdel(this);
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:557
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        object dot = await base.on_saboteur(source, disrupt_duration);
        if (!Dm.truthy(active))
            return dot;
        turn_off();
        update_appearance();
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:565
    public override async DmTask<object> Exited(atom_movable mov_content = null, object direction = null)
    {
        object dot = await base.Exited(mov_content, direction);
        if (mov_content == cell)
        {
            cell = null;
            turn_off();
            update_appearance();
        }
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:572
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object base_inhand = base_inhand_state ?? base_icon_state;
        if (Dm.truthy(active))
        {
            icon_state = Dm.text("[]_active", base_icon_state);
            if (Dm.truthy(active_changes_inhand))
                inhand_icon_state = Dm.text("[]_active", base_inhand);
            return await base.update_icon_state(__extra);
        }
        if (cell == null)
        {
            icon_state = Dm.text("[]_nocell", base_icon_state);
            inhand_icon_state = base_inhand as string;
            return await base.update_icon_state(__extra);
        }
        icon_state = base_icon_state as string;
        inhand_icon_state = base_inhand as string;
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:587
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (cell != null)
            Dm.append(ref dot, span_notice(I18n.T("\\The [] is []% charged.", this, round(Dm.num(cell.percent())))));
        else
            Dm.append(ref dot, span_warning(I18n.T("\\The [] does not have a power source installed.", this)));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:594
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await tryremovecell(user)))
            tool.play_tool_sound(this);
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:599
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_stock_parts_power_store_cell)))
            return NONE;
        if (cell != null)
        {
            to_chat(user, span_warning(I18n.T("[] already has a cell!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if ((tool as obj_item_stock_parts_power_store_cell).maxcharge < cell_hit_cost)
        {
            to_chat(user, span_notice(I18n.T("[] requires a higher capacity cell.", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;

        cell = tool as obj_item_stock_parts_power_store;
        to_chat(user, span_notice(I18n.T("You install a cell in [].", this)));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:618
    public virtual async DmTask<object> tryremovecell(mob user = null)
    {
        if (cell != null && Dm.truthy(can_remove_cell))
        {
            await cell.forceMove(drop_location() as atom);
            to_chat(user, span_notice(I18n.T("You remove the cell from [].", this)));
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:625
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(cell?.charge >= cell_hit_cost) && !Dm.truthy(active))
        {
            turn_on(user);
            balloon_alert(user, I18n.T("turned on"));
        }
        else
        {
            turn_off();
            if (cell == null)
                balloon_alert(user, I18n.T("no power source!"));
            else if (cell.charge < cell_hit_cost)
                balloon_alert(user, I18n.T("out of charge!"));
            else
                balloon_alert(user, I18n.T("turned off"));
        }
        add_fingerprint(user);
        return null;
    }

    /// Toggles the stun baton's light
    // tg: code/game/objects/items/weaponry/melee/baton.dm:640
    public virtual void toggle_light()
    {
        Waiting.NoSleep(set_light_on(Dm.truthy(light_on) ? FALSE : TRUE));
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:643
    public virtual void turn_on(mob user = null)
    {
        active = TRUE;
        playsound(this, SFX_SPARKS, 75, TRUE, -1);
        update_appearance();
        toggle_light();
        do_sparks(1, TRUE, this);
        drop_sound = active_drop_sound;
        pickup_sound = active_pickup_sound;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:652
    public virtual void turn_off()
    {
        active = FALSE;
        Waiting.NoSleep(set_light_on(FALSE));
        update_appearance();
        playsound(this, SFX_SPARKS, 75, TRUE, -1);
        drop_sound = inactive_drop_sound;
        pickup_sound = inactive_pickup_sound;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:660
    public virtual async DmTask<object> deductcharge(object deducted_charge = null)
    {
        if (cell == null)
            return null;
        //Note this value returned is significant, as it will determine
        //if a stun is applied or not
        object dot = await cell.use(deducted_charge);
        if (Dm.truthy(active) && cell.charge < cell_hit_cost)
            //we're below minimum, turn off
            turn_off();
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:670
    public override object try_stun(mob_living target = null, mob_living user = null, object harmbatonning = null)
    {
        if (!Dm.truthy(active) && !Dm.truthy(harmbatonning) && !Dm.truthy(user.combat_mode))
        {
            target.visible_message(
                span_warning(I18n.T("[] prods [] with []. Luckily it was off.", user, target, this)),
                span_warning(I18n.T("[] prods you with []. Luckily it was off.", user, this)),
                visible_message_flags: ALWAYS_SHOW_SELF_MESSAGE
            );
            return FALSE;
        }

        return base.try_stun(target, user, harmbatonning);
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:681
    public override async DmTask<object> baton_effect(mob_living target = null, mob_living user = null, object stun_override = null, object clumsy = null, params object[] __extra)
    {
        if (iscyborg(loc))
        {
            mob_living_silicon_robot robot = loc as mob_living_silicon_robot;
            if (robot == null || robot.cell__obj == null || !Dm.truthy(await robot.cell__obj.use(cell_hit_cost)))
                return FALSE;
        }
        else if (!Dm.truthy(await deductcharge(cell_hit_cost)))
        {
            return FALSE;
        }
        stun_override = 0; //Avoids knocking people down prematurely.
        return await base.baton_effect(target, user, stun_override, clumsy, __extra);
    }

    /*
     * After a target is hit, we apply some status effects.
     * After a period of time, we then check to see what stun duration we give.
     */
    // tg: code/game/objects/items/weaponry/melee/baton.dm:695
    public override async DmTask additional_effects_non_cyborg(mob_living target = null, mob_living user = null)
    {
        await target.set_jitter_if_lower(40 * SECONDS * (HAS_TRAIT(target, TRAIT_BATON_RESISTANCE) ? 0.5f : 1));
        await target.set_confusion_if_lower(10 * SECONDS * (HAS_TRAIT(target, TRAIT_BATON_RESISTANCE) ? 0.5f : 1));
        await target.set_stutter_if_lower(16 * SECONDS * (HAS_TRAIT(target, TRAIT_BATON_RESISTANCE) ? 0.5f : 1));

        if (Dm.truthy(target._listen_lookup?[COMSIG_LIVING_MINOR_SHOCK]))
            target._SendSignal(COMSIG_LIVING_MINOR_SHOCK, Dm.list(target));
        _addtimer(Dm.@new<datum_callback>(this, nameof(apply_stun_effect_end), target), 2 * SECONDS, file: "code/game/objects/items/weaponry/melee/baton.dm", line: 690);
    }

    /// After the initial stun period, we check to see if the target needs to have the stun applied.
    // tg: code/game/objects/items/weaponry/melee/baton.dm:704
    public virtual async DmTask apply_stun_effect_end(mob_living target = null)
    {
        bool trait_check = HAS_TRAIT(target, TRAIT_BATON_RESISTANCE); //var since we check it in out to_chat as well as determine stun duration
        if (!Dm.truthy(target.IsKnockdown()))
            to_chat(target, span_warning(I18n.T("Your muscles seize, making you collapse[]", trait_check ? ", but your body quickly recovers..." : "!")));

        if (!trait_check)
            await target.Knockdown(knockdown_time);
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:712
    public override object get_stun_description(mob_living target = null, mob_living user = null)
    {
        DmList dot = Dm.list();

        dot["visible"] = span_danger(I18n.T("[] stuns [] with []!", user, target, this));
        dot["local"] = span_userdanger(I18n.T("[] stuns you with []!", user, this));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:718
    public override object get_cyborg_stun_description(mob_living target = null, mob_living user = null)
    {
        DmList dot = base.get_cyborg_stun_description(target, user) as DmList;
        if (!Dm.truthy(affect_cyborg))
        {
            dot["visible"] = span_danger(I18n.T("[] tries to stun [] with [], and predictably fails!", user, target, this));
            dot["local"] = span_userdanger(I18n.T("[] tries to... stun you with []?", user, this));
        }
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:724
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (!Dm.truthy(dot) && Dm.truthy(active) && prob(throw_stun_chance) && isliving(hit_atom))
            Waiting.NoSleep(finalize_baton_attack(hit_atom as mob_living, throwingdatum?.get_thrower() as mob_living));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:729
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (cell == null)
            return dot;
        if (!Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            await cell.emp_act(severity);

        if (cell.charge >= cell_hit_cost)
        {
            float scramble_time;
            scramble_mode();
            for (int loops = 1; loops <= rand(6, 12); loops++)
            {
                scramble_time = rand(5, 15) / (1 * SECONDS);
                _addtimer(Dm.@new<datum_callback>(this, nameof(scramble_mode)), scramble_time * loops * (1 * SECONDS), file: "code/game/objects/items/weaponry/melee/baton.dm", line: 729);
            }
        }
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:743
    public virtual void scramble_mode()
    {
        if (cell == null || cell.charge < cell_hit_cost)
            return;
        active = Dm.truthy(active) ? FALSE : TRUE;
        toggle_light();
        do_sparks(1, TRUE, this);
        playsound(this, SFX_SPARKS, 75, TRUE, -1);
        update_appearance();
    }
}

//Makeshift stun baton. Replacement for stun gloves.
// tg: code/game/objects/items/weaponry/melee/baton.dm:809
public partial class obj_item_melee_baton_security_cattleprod : obj_item_melee_baton_security
{
    // tg: code/game/objects/items/weaponry/melee/baton.dm:835
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        sparkler = Dm.@new<obj_item_assembly_igniter>(this);
        return dot;
    }

    // handles sticking a crystal onto a stunprod to make an improved cattleprod
    // tg: code/game/objects/items/weaponry/melee/baton.dm:839
    public override async DmTask add_deep_lore()
    {
        return;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:842
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_stack)))
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (!Dm.truthy(can_upgrade))
        {
            user.visible_message(span_warning(I18n.T("This prod is already improved!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (cell != null)
        {
            user.visible_message(span_warning(I18n.T("You can't put the crystal onto the stunprod while it has a power cell installed!")));
            return ITEM_INTERACT_BLOCKING;
        }

        Type our_prod;
        if (istype(tool, typeof(obj_item_stack_ore_bluespace_crystal)))
        {
            obj_item_stack_ore_bluespace_crystal our_crystal = tool as obj_item_stack_ore_bluespace_crystal;
            await our_crystal.use(1);
            our_prod = typeof(obj_item_melee_baton_security_cattleprod_teleprod);
        }
        else if (istype(tool, typeof(obj_item_stack_telecrystal)))
        {
            obj_item_stack_telecrystal our_crystal = tool as obj_item_stack_telecrystal;
            await our_crystal.use(1);
            our_prod = typeof(obj_item_melee_baton_security_cattleprod_telecrystalprod);
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You don't think \\the [] will do anything to improve \\the [].", tool, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You place \\the [] firmly into \\the [].", tool, sparkler)));
        await remove_item_from_storage(user);
        qdel(this);
        obj_item_melee_baton_security_cattleprod brand_new_prod = Dm.@new(our_prod, user.loc) as obj_item_melee_baton_security_cattleprod;
        await user.put_in_hands(brand_new_prod);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:875
    public override object try_stun(mob_living target = null, mob_living user = null, object harmbatonning = null)
    {
        return Dm.truthy(base.try_stun(target, user, harmbatonning)) && Dm.truthy(Waiting.NoSleep(sparkler.activate())) ? TRUE : FALSE;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:878
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref sparkler);
        return base.Destroy(force);
    }
}

// tg: code/game/objects/items/weaponry/melee/baton.dm:885
public partial class obj_item_melee_baton_security_boomerang : obj_item_melee_baton_security
{
    // tg: code/game/objects/items/weaponry/melee/baton.dm:906
    public override async DmTask add_deep_lore()
    {
        return;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:902
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_boomerang), throw_range + 2, TRUE));
        return dot;
    }
}

// tg: code/game/objects/items/weaponry/melee/baton.dm:774
public partial class obj_item_melee_baton_security_stunsword : obj_item_melee_baton_security
{
    // tg: code/game/objects/items/weaponry/melee/baton.dm:799
    public override async DmTask setup_reskins()
    {
        // AddComponent(/datum/component/reskinable_item, /datum/atom_skin/stunsword): the reskin menu (not-translated.md 114)
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:802
    public override async DmTask add_deep_lore()
    {
        return;
    }
}

// tg: code/game/objects/items/weaponry/melee/baton.dm:912
public partial class obj_item_melee_baton_security_cattleprod_teleprod : obj_item_melee_baton_security_cattleprod
{
    // tg: code/game/objects/items/weaponry/melee/baton.dm:923
    public override async DmTask<object> baton_effect(mob_living target = null, mob_living user = null, object stun_override = null, object clumsy = null, params object[] __extra)
    {
        object dot = await base.baton_effect(target, user, stun_override, clumsy, __extra);
        if (!Dm.truthy(dot) || target.move_resist >= MOVE_FORCE_OVERPOWERING)
            return dot;
        await do_teleport(target, get_turf(target), Dm.truthy(clumsy) ? 50 : 15, channel: TELEPORT_CHANNEL_BLUESPACE);
        return dot;
    }
}

// tg: code/game/objects/items/weaponry/melee/baton.dm:929
public partial class obj_item_melee_baton_security_cattleprod_telecrystalprod : obj_item_melee_baton_security_cattleprod
{
    // tg: code/game/objects/items/weaponry/melee/baton.dm:941
    public override async DmTask<object> baton_effect(mob_living target = null, mob_living user = null, object stun_override = null, object clumsy = null, params object[] __extra)
    {
        object dot = await base.baton_effect(target, user, stun_override, clumsy, __extra);
        if (!Dm.truthy(dot))
            return dot;
        obj_item stuff_in_hand = target.get_active_held_item() as obj_item;
        if (user == null || stuff_in_hand == null || !Dm.truthy(await target.temporarilyRemoveItemFromInventory(stuff_in_hand)))
            return dot;
        if (Dm.truthy(await user.put_in_inactive_hand(stuff_in_hand)))
            ((atom)stuff_in_hand.loc).visible_message(span_warning(I18n.T("[] suddenly appears in []'s hand!", stuff_in_hand, user)));
        else
        {
            await stuff_in_hand.forceMove(user.drop_location() as atom);
            ((atom)stuff_in_hand.loc).visible_message(span_warning(I18n.T("[] suddenly appears!", stuff_in_hand)));
        }

        if (Dm.truthy(clumsy) && Dm.truthy(await user.dropItemToGround(this, force: TRUE, silent: TRUE)))
            await do_teleport(this, get_turf(user), 50, channel: TELEPORT_CHANNEL_BLUESPACE); //Wait, where did it go?
        return dot;
    }
}

// tg: code/game/objects/items/weaponry/melee/baton.dm:957
public partial class obj_item_melee_baton_nunchaku : obj_item_melee_baton
{
    // tg: code/game/objects/items/weaponry/melee/baton.dm:979
    public virtual void randomize_state()
    {
        icon_state = pick(Dm.list("nunchaku", "nunchaku_x", "nunchaku_y")) as string;
        update_appearance();
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:983
    public override async DmTask<object> after_throw(datum_callback callback = null)
    {
        object dot = await base.after_throw(callback);
        randomize_state();
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:987
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        await base.afterattack(target, user, modifiers, attack_modifiers);
        randomize_state();
    }

    // tg: code/game/objects/items/weaponry/melee/baton.dm:991
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        if (attack_type == PROJECTILE_ATTACK || !Dm.truthy(owner.throw_mode))
            return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);

        randomize_state();

        // blocks any melee/throwable attacks
        await owner.adjust_stamina_loss(5);
        final_block_chance = 100;

        // counterattack at melee
        if (Dm.@in(attack_type, Dm.list(MELEE_ATTACK, UNARMED_ATTACK, LEAP_ATTACK)))
        {
            mob_living attacker = get(hitby, typeof(mob_living)) as mob_living; // GET_ASSAILANT(hitby)
            playsound(this, pick(Dm.list(Dm.resource("sound/items/weapons/cqchit2.ogg"), Dm.resource("sound/items/weapons/cqchit1.ogg"))), 70, FALSE);
            await melee_attack_chain(owner, attacker, Dm.list(LEFT_CLICK));
        }

        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }
}
