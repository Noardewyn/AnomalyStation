// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/energy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): the energy weapons whole: the energy axe, the energy sword and its coloured sabers (blocking
// while on, the multitool's rainbow), the cyborg sword and saw, the cutlass, the energy blade, the surplus Iaito and the null
// rod swords. The surplus sword's screentip is a live-partial row.

// tg: code/game/objects/items/weaponry/melee/energy.dm:1
public partial class obj_item_melee_energy : obj_item_melee
{
    // tg: code/game/objects/items/weaponry/melee/energy.dm:41
    public override object get_all_tool_behaviours()
    {
        return Dm.list(TOOL_SAW);
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(make_transformable());
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_butchering),
            Dm.named("speed", 5 * SECONDS),
            Dm.named("butcher_sound", active_hitsound)));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:54
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    /*
     * Gives our item the transforming component, passing in our various vars.
     */
    // tg: code/game/objects/items/weaponry/melee/energy.dm:61
    public virtual async DmTask<object> make_transformable()
    {
        await AddComponent(typeof(datum_component_transforming),
            Dm.named("force_on", active_force),
            Dm.named("throwforce_on", active_throwforce),
            Dm.named("throw_speed_on", active_throw_speed),
            Dm.named("sharpness_on", active_sharpness),
            Dm.named("hitsound_on", active_hitsound),
            Dm.named("w_class_on", active_w_class),
            Dm.named("attack_verb_continuous_on", Dm.list("attacks", "slashes", "slices", "tears", "lacerates", "rips", "dices", "cuts")),
            Dm.named("attack_verb_simple_on", Dm.list("attack", "slash", "slice", "tear", "lacerate", "rip", "dice", "cut")));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:75
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            await attack_self(user);
        user.visible_message(span_suicide(I18n.T("[] is [] []! It looks like [] trying to commit seppuku!", user, pick(I18n.T("slitting [] stomach open with", user.p_their()), I18n.T("falling on")), this, user.p_theyre())));
        return Dm.bitor(BRUTELOSS, FIRELOSS);
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:81
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(heat))
            open_flame();
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:85
    public override async DmTask<object> ignition_effect(atom A = null, mob user = null)
    {
        atom atom = A;
        if (!Dm.truthy(heat) && !HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return "";

        string in_mouth = "";
        if (ishuman(user) && user.get_item_by_slot(ITEM_SLOT_MASK) != null)
            in_mouth = I18n.T(", barely missing [] nose", user.p_their());
        object dot = span_rose(I18n.T("[] swings [] [][]. [] light[] [] [] in the process.", user, user.p_their(), name, in_mouth, user.p_They(), user.p_s(), user.p_their(), atom.name));
        playsound(loc, hitsound, get_clamped_volume(), TRUE, -1);
        add_fingerprint(user);
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:96
    public override object get_demolition_modifier(obj target = null)
    {
        return HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE) ? demolition_mod : 1;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:99
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (!Dm.truthy(sword_color_icon))
            return dot;
        if (HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
        {
            icon_state = Dm.text("[]_on_[]", base_icon_state, sword_color_icon); // "esword_on_red"
            inhand_icon_state = icon_state;
        }
        else
        {
            icon_state = Dm.str(base_icon_state);
            inhand_icon_state = base_icon_state;
        }
        return dot;
    }

    /**
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     *
     * Updates some of the stuff the transforming comp doesn't, such as heat and embedding.
     *
     * Also gives feedback to the user and activates or deactives the glow.
     */
    // tg: code/game/objects/items/weaponry/melee/energy.dm:117
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        if (Dm.truthy(active))
        {
            heat = active_heat;
            START_PROCESSING(SSobj, this);
        }
        else
        {
            heat = Dm.num(Dm.initial(this, nameof(heat)));
            STOP_PROCESSING(SSobj, this);
        }
        tool_behaviour = Dm.truthy(active) ? TOOL_SAW : NONE; //Lets energy weapons cut trees. Also lets them do bonecutting surgery, which is kinda metal!
        if (user != null)
            balloon_alert(user, I18n.T("[] []", name, Dm.truthy(active) ? I18n.T("enabled") : I18n.T("disabled")));
        playsound(this, Dm.truthy(active) ? Dm.resource("sound/items/weapons/saberon.ogg") : Dm.resource("sound/items/weapons/saberoff.ogg"), 35, TRUE);
        Waiting.NoSleep(set_light_on(active));
        update_appearance(UPDATE_ICON_STATE);
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }
}

/// Energy axe - extremely strong.
// tg: code/game/objects/items/weaponry/melee/energy.dm:136
public partial class obj_item_melee_energy_axe : obj_item_melee_energy
{
    // tg: code/game/objects/items/weaponry/melee/energy.dm:161
    public override async DmTask<object> make_transformable()
    {
        await AddComponent(typeof(datum_component_transforming),
            Dm.named("force_on", active_force),
            Dm.named("throwforce_on", active_throwforce),
            Dm.named("throw_speed_on", active_throw_speed),
            Dm.named("sharpness_on", sharpness),
            Dm.named("w_class_on", active_w_class));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:172
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] swings [] towards [] head! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        return Dm.bitor(BRUTELOSS, FIRELOSS);
    }
}

// tg: code/game/objects/items/weaponry/melee/energy.dm:182
public partial class obj_item_melee_energy_sword : obj_item_melee_energy
{
    // tg: code/game/objects/items/weaponry/melee/energy.dm:204
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), alt_sharpness, alt_continuous, alt_simple, alt_force_mod, TRAIT_TRANSFORM_ACTIVE, alt_hitsound));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:210
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return FALSE;

        if (attack_type == OVERWHELMING_ATTACK)
            return FALSE;

        if (attack_type == LEAP_ATTACK)
            final_block_chance -= 25; //OH GOD GET IT OFF ME

        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }
}

// The colored energy swords we all know and love.
// tg: code/game/objects/items/weaponry/melee/energy.dm:223
public partial class obj_item_melee_energy_sword_saber : obj_item_melee_energy_sword
{
    // tg: code/game/objects/items/weaponry/melee/energy.dm:235
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_jousting), Dm.named("damage_boost_per_tile", 1), Dm.named("knockdown_chance_per_tile", 10)));
        if (!Dm.truthy(sword_color_icon) && Dm.truthy(LAZYLEN(possible_sword_colors)))
            sword_color_icon = pick(possible_sword_colors);

        if (Dm.truthy(sword_color_icon))
            set_light_color(possible_sword_colors[sword_color_icon]);
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:244
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.process(seconds_per_tick, __extra);
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE) || !Dm.truthy(hacked))
            return dot;

        if (!Dm.truthy(LAZYLEN(possible_sword_colors)))
        {
            possible_sword_colors = Dm.list(
                Dm.kv("red", COLOR_SOFT_RED),
                Dm.kv("blue", LIGHT_COLOR_LIGHT_CYAN),
                Dm.kv("green", LIGHT_COLOR_GREEN),
                Dm.kv("purple", LIGHT_COLOR_LAVENDER));
            possible_sword_colors.Remove(hacked_color);
        }

        hacked_color = pick(possible_sword_colors);
        set_light_color(possible_sword_colors[hacked_color]);
        possible_sword_colors.Remove(hacked_color);
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:274
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(hacked))
        {
            to_chat(user, span_warning(I18n.T("It's already fabulous!")));
            return null;
        }
        hacked = TRUE;
        sword_color_icon = "rainbow";
        to_chat(user, span_warning(I18n.T("RNBW_ENGAGE")));
        update_appearance(UPDATE_ICON_STATE);
        return null;
    }
}

// tg: code/game/objects/items/weaponry/melee/energy.dm:283
public partial class obj_item_melee_energy_sword_saber_cyborg : obj_item_melee_energy_sword_saber
{
    // tg: code/game/objects/items/weaponry/melee/energy.dm:290
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_light_range(5); //Cyborgs don't have inhand sprites, so we compensate by making it glow brightly.
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:294
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob_living_silicon_robot robot_user = user as mob_living_silicon_robot;
        if (robot_user?.cell__obj == null)
            return null;

        obj_item_stock_parts_power_store our_cell = robot_user.cell__obj;
        if (HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE) && !Dm.truthy(await our_cell.use(hitcost)))
        {
            await attack_self(user);
            to_chat(user, span_notice(I18n.T("It's out of charge!")));
            return null;
        }
        return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:305
    public override async DmTask cyborg_unequip(mob user = null)
    {
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return;
        await attack_self(user);
    }
}

// tg: code/game/objects/items/weaponry/melee/energy.dm:329
public partial class obj_item_melee_energy_sword_pirate : obj_item_melee_energy_sword
{
    // tg: code/game/objects/items/weaponry/melee/energy.dm:338
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_cuffable_item))); //closed sword guard
        return dot;
    }
}

/// Energy blades, which are effectively perma-extended energy swords
// tg: code/game/objects/items/weaponry/melee/energy.dm:343
public partial class obj_item_melee_energy_blade : obj_item_melee_energy
{
    //Most of the other special functions are handled in their own files. aka special snowflake code so kewl
    // tg: code/game/objects/items/weaponry/melee/energy.dm:367
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Waiting.NoSleep(AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple, -10));
        spark_system = Dm.@new<datum_effect_system_basic_spark_spread>(5, FALSE, TRUE); // tg passes src as New's cardinals_only (truthy); the port's numeric var takes TRUE
        spark_system.attach(this);
        START_PROCESSING(SSobj, this);
        ADD_TRAIT(this, TRAIT_TRANSFORM_ACTIVE, INNATE_TRAIT); // Functions as an extended esword
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:377
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref spark_system);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:381
    public override async DmTask<object> make_transformable()
    {
        return FALSE;
    }
}

// tg: code/game/objects/items/weaponry/melee/energy.dm:392
public partial class obj_item_melee_energy_sword_surplus : obj_item_melee_energy_sword
{
    // tg: code/game/objects/items/weaponry/melee/energy.dm:423
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_TRANSFORMING_PRE_TRANSFORM, nameof(check_power));
        Waiting.NoSleep(AddElement(typeof(datum_element_examine_lore),
            Dm.named("lore", I18n.T("This early iteration of the now infamous energy sword was, supposedly, a Waffle Corp prototype first trialed in a variety of armed conflicts around the interstellar frontier.<br><br>Unfortunately, the success rate of the platform, along with the survival rate of its users, was abysmally low. To make matters worse, initial overestimation of its effectiveness meant that by the time its myriad flaws reared their heads, production had already reached such a level that the company behind its manufacture would have to pay more to properly disassemble and dispose of the swords, than if they started offloading them onto markets of various legitimacy to try and recoup costs. Thus, the Iaito was 'born'.<br><br>As a consequence of its haphazard proliferation and its low market price compared to later, improved energy sword models, examples of the Iaito are typically found in the hands of various grunts, mooks, goons, criminals, wannabe assassins, lunatics, or those otherwise embroiled in a desperate struggle. If you're actually trying to kill someone with this sword, you may or may not fit into one or more of those categories."))));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:439
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(charge))
            dot.Add(span_notice(I18n.T("[] has [] hits left before it must be recharged.", this, charge)));
        else
            dot.Add(span_warning(I18n.T("[] needs to be recharged.", this)));

        dot.Add(span_info(I18n.T("You get the sense that this weapon isn't very effective unless you hit someone while they are exposed in some way, like attacking from behind or while they're staggered.")));
        return dot;
    }

    // A weapon best employed by someone in a desperate struggle
    // tg: code/game/objects/items/weaponry/melee/energy.dm:458
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!isliving(target))
            return await base.pre_attack(target, user, modifiers, attack_modifiers);

        if (Dm.eq(sharpness, NONE))
            return await base.pre_attack(target, user, modifiers, attack_modifiers);

        mob_living living_target = (mob_living)target;
        bool vulnerable_target = false;

        if (living_target.stat == DEAD) // I know it doesn't make a lot of sense but it makes it a bit too good for dismemberment otherwise
            return await base.pre_attack(target, user, modifiers, attack_modifiers);

        if (Dm.truthy(living_target.get_timed_status_effect_duration(typeof(datum_status_effect_staggered))))
            vulnerable_target = true;

        if (HAS_TRAIT(living_target, TRAIT_INCAPACITATED))
            vulnerable_target = true;

        if (Dm.truthy(check_behind(user, living_target)))
            vulnerable_target = true;

        if (vulnerable_target)
        {
            // MODIFY_ATTACK_FORCE_MULTIPLIER(attack_modifiers, 2) (__DEFINES/combat.dm:427)
            if (!islist(attack_modifiers)) { attack_modifiers = Dm.list(); }
            if (!Dm.@in(FORCE_MULTIPLIER, attack_modifiers)) { attack_modifiers[FORCE_MULTIPLIER] = 1; }
            attack_modifiers[FORCE_MULTIPLIER] = Dm.num(attack_modifiers[FORCE_MULTIPLIER]) * 2;
        }

        return await base.pre_attack(target, user, modifiers, attack_modifiers);
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:485
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        if (Dm.truthy(dot))
            return dot;

        if (charge == max_charge)
            return SECONDARY_ATTACK_CALL_NORMAL;

        if (Dm.truthy(DOING_INTERACTION(user, DOAFTER_SOURCE_CHARGING_ESWORD)))
        {
            user.balloon_alert(user, I18n.T("busy!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }

        if (charge <= max_charge)
        {
            user.balloon_alert(user, I18n.T("attempting recharge..."));
            if (!Dm.truthy(await do_after(user, charge_time, target: this, extra_checks: Dm.@new<datum_callback>(this, nameof(do_jiggle), user), interaction_key: DOAFTER_SOURCE_CHARGING_ESWORD, cog_iconstate: "beat_the_heat")))
            {
                user.balloon_alert(user, I18n.T("interrupted!"));
                return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
            }
        }
        charge = max_charge;
        user.balloon_alert(user, I18n.T("recharge successful"));
        playsound(this, Dm.resource("sound/machines/ping.ogg"), 40, TRUE);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:507
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE) || charge <= 0)
            return;

        await expend_charge(user);
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:513
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return FALSE;

        if (attack_type == OVERWHELMING_ATTACK)
            return FALSE;

        if (attack_type == LEAP_ATTACK)
            final_block_chance -= 25;

        if (prob(final_block_chance.Value) && Dm.truthy(charge))
        {
            await expend_charge(owner);
            return TRUE;
        }
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:527
    public virtual async DmTask expend_charge(mob user = null)
    {
        if (!Dm.truthy(charge)) // not that this will ever get here without charge, but...
            return;

        charge--;
        if (charge <= 0)
        {
            user.balloon_alert(user, I18n.T("out of charge!"));
            await attack_self(user);
        }
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:536
    [SignalHandler]
    public virtual object check_power(obj_item source = null, mob user = null, object active = null)
    {
        if (charge <= 0 && !HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
        {
            balloon_alert(user, I18n.T("no charge!"));
            return COMPONENT_BLOCK_TRANSFORM;
        }
        return null;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:543
    public virtual object do_jiggle(mob user = null)
    {
        if (!(jiggle_cooldown <= world.time)) // COOLDOWN_FINISHED(src, jiggle_cooldown)
            return TRUE;

        user.Shake(2, 1, 0.3f * SECONDS, shake_interval: 0.1f * SECONDS);
        playsound(this, Dm.resource("sound/items/baton/telescopic_baton_folded_pickup.ogg"), 40, TRUE);
        jiggle_cooldown = world.time + 1 * SECONDS; // COOLDOWN_START(src, jiggle_cooldown, 1 SECONDS)
        return TRUE;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:552
    public override object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        object dot = base.on_transform(source, user, active);
        if (Dm.truthy(active))
            slot_flags = Dm.num(Dm.bitand(slot_flags, Dm.bitnot(ITEM_SLOT_BELT)));
        else
            slot_flags = Dm.num(Dm.bitor(slot_flags, ITEM_SLOT_BELT));
        return dot;
    }
}

// Null rod variants
// tg: code/game/objects/items/weaponry/melee/energy.dm:561
public partial class obj_item_melee_energy_sword_nullrod : obj_item_melee_energy_sword
{
    // tg: code/game/objects/items/weaponry/melee/energy.dm:578
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_nullrod_core)));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/energy.dm:582
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return FALSE;

        if (attack_type == PROJECTILE_ATTACK || attack_type == LEAP_ATTACK || attack_type == OVERWHELMING_ATTACK)
            final_block_chance = 0; //Don't bring a sword to a gunfight, and also you aren't going to really block someone full body tackling you with a sword. Or a road roller, if one happened to hit you.

        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }
}
