// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/reactive_armour.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the reactive armour shell, which an anomaly core turns into the armour of its kind (the stealth armour for a
// kind without its own), and the reactive armours: switched on with their action, half of the hits on the wearer trigger their
// reaction (blocking the hit, most of them) unless they are recharging; an EMP turns the next reaction against the wearer for
// thirty seconds. The teleport, incendiary, stealth (with its decoy), tesla, repulse, table, hallucinating, bioscrambling,
// barricade (with its dimensional themes), possession and weather armours. Their on and off states. The var blocks are generated
// (D111).

// tg: code/modules/clothing/suits/reactive_armour.dm:1
public partial class obj_item_reactive_armor_shell : obj_item
{
    // tg: code/modules/clothing/suits/reactive_armour.dm:9
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        DmList anomaly_armour_types = TgState.Current.obj_item_reactive_armor_shell__item_interaction__anomaly_armour_types ??= Dm.list(
            Dm.kv(typeof(obj_effect_anomaly_grav), typeof(obj_item_clothing_suit_armor_reactive_repulse)),
            Dm.kv(typeof(obj_effect_anomaly_flux), typeof(obj_item_clothing_suit_armor_reactive_tesla)),
            Dm.kv(typeof(obj_effect_anomaly_bluespace), typeof(obj_item_clothing_suit_armor_reactive_teleport)),
            Dm.kv(typeof(obj_effect_anomaly_bioscrambler), typeof(obj_item_clothing_suit_armor_reactive_bioscrambling)),
            Dm.kv(typeof(obj_effect_anomaly_hallucination), typeof(obj_item_clothing_suit_armor_reactive_hallucinating)),
            Dm.kv(typeof(obj_effect_anomaly_dimensional), typeof(obj_item_clothing_suit_armor_reactive_barricade)),
            Dm.kv(typeof(obj_effect_anomaly_ectoplasm), typeof(obj_item_clothing_suit_armor_reactive_ectoplasm)),
            Dm.kv(typeof(obj_effect_anomaly_weather), typeof(obj_item_clothing_suit_armor_reactive_weather))
        ); // var/static/list/anomaly_armour_types

        if (istype(tool, typeof(obj_item_assembly_signaler_anomaly)))
        {
            obj_item_assembly_signaler_anomaly anomaly = tool as obj_item_assembly_signaler_anomaly;
            object armour_path = is_path_in_list(anomaly.anomaly_type, anomaly_armour_types, TRUE);
            if (!Dm.truthy(armour_path))
                armour_path = typeof(obj_item_clothing_suit_armor_reactive_stealth); //Lets not cheat the player if an anomaly type doesnt have its own armour coded
            to_chat(user, span_notice(I18n.T("You insert [] into the chest plate, and the armour gently hums to life.", anomaly)));
            Dm.@new(armour_path as Type, get_turf(this));
            qdel(this);
            qdel(anomaly);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }
}

//Reactive armor
// tg: code/modules/clothing/suits/reactive_armour.dm:34
public partial class obj_item_clothing_suit_armor_reactive : obj_item_clothing_suit_armor
{
    // tg: code/modules/clothing/suits/reactive_armour.dm:61
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        return dot;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:65
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("reactive[]", Dm.truthy(active) ? null : "off");
        return dot;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:69
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        active = Dm.not(active);
        to_chat(user, span_notice(I18n.T("[] is now [].", this, Dm.truthy(active) ? "active" : "inactive")));
        await update_icon();
        add_fingerprint(user);
        return null;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:75
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        damage_type ??= BRUTE;
        if (!Dm.truthy(active) || !prob(hit_reaction_chance))
            return FALSE;
        if (world.time < reactivearmor_cooldown)
        {
            cooldown_activation(owner);
            return FALSE;
        }
        if (Dm.truthy(bad_effect))
            return await emp_activation(owner, hitby, attack_text, final_block_chance, damage, attack_type);
        else
            return await reactive_activation(owner, hitby, attack_text, final_block_chance, damage, attack_type);
    }

    /**
     * A proc for doing cooldown effects (like the sparks on the tesla armor, or the semi-stealth on stealth armor)
     * Called from the suit activating whilst on cooldown.
     * You should be calling ..()
     */
    // tg: code/modules/clothing/suits/reactive_armour.dm:91
    public virtual void cooldown_activation(mob_living_carbon_human owner = null)
    {
        owner.visible_message(cooldown_message);
    }

    /**
     * A proc for doing reactive armor effects.
     * Called from the suit activating while off cooldown, with no emp.
     * Returning TRUE will block the attack that triggered this
     */
    // tg: code/modules/clothing/suits/reactive_armour.dm:99
    public virtual async DmTask<object> reactive_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        await DmTask.CompletedTask;
        owner.visible_message(span_danger(I18n.T("The reactive armor doesn't do much! No surprises here.")));
        return TRUE;
    }

    /**
     * A proc for doing owner unfriendly reactive armor effects.
     * Called from the suit activating while off cooldown, while the armor is still suffering from the effect of an EMP.
     * Returning TRUE will block the attack that triggered this
     */
    // tg: code/modules/clothing/suits/reactive_armour.dm:108
    public virtual async DmTask<object> emp_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        await DmTask.CompletedTask;
        owner.visible_message(span_danger(I18n.T("The reactive armor doesn't do much, despite being emp'd! Besides giving off a special message, of course.")));
        return TRUE;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:112
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)) || Dm.truthy(bad_effect) || !Dm.truthy(active)) //didn't get hit or already emp'd, or off
            return dot;
        visible_message(emp_message);
        bad_effect = TRUE;
        // VARSET_CALLBACK(src, bad_effect, FALSE): ___callbackvarset is not declared (vars is not translated, rules 10.7), so the timer
        // calls bad_effect__varset (as doorcontrol.cs)
        _addtimer(Dm.@new<datum_callback>(this, nameof(bad_effect__varset), FALSE), 30 * SECONDS, file: "code/modules/clothing/suits/reactive_armour.dm", line: 118);
        return dot;
    }

    // tg: code/__HELPERS/varset_callback.dm:3  VARSET_CALLBACK(src, bad_effect, value)
    public virtual void bad_effect__varset(object var_value = null) => bad_effect = Dm.num(var_value);
}

//When the wearer gets hit, this armor will teleport the user a short distance away (to safety or to more danger, no one knows. That's the fun of it!)
// tg: code/modules/clothing/suits/reactive_armour.dm:121
public partial class obj_item_clothing_suit_armor_reactive_teleport : obj_item_clothing_suit_armor_reactive
{
    // tg: code/modules/clothing/suits/reactive_armour.dm:129
    public override async DmTask<object> reactive_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        owner.visible_message(span_danger(I18n.T("The reactive teleport system flings [] clear of []!", owner, attack_text)));
        playsound(get_turf(owner), "sound/effects/magic/blink.ogg", 100, TRUE);
        await do_teleport(owner, get_turf(owner), tele_range, no_effects: TRUE, channel: TELEPORT_CHANNEL_BLUESPACE);
        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return TRUE;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:136
    public override async DmTask<object> emp_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        owner.visible_message(span_danger(I18n.T("The reactive teleport system flings itself clear of [], leaving someone behind in the process!", attack_text)));
        await owner.dropItemToGround(this, TRUE, TRUE);
        playsound(get_turf(owner), "sound/machines/buzz/buzz-sigh.ogg", 50, TRUE);
        playsound(get_turf(owner), "sound/effects/magic/blink.ogg", 100, TRUE);
        await do_teleport(this, get_turf(owner), tele_range, no_effects: TRUE, channel: TELEPORT_CHANNEL_BLUESPACE);
        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return FALSE; //you didn't actually evade the attack now did you
    }
}

//Fire
// tg: code/modules/clothing/suits/reactive_armour.dm:147
public partial class obj_item_clothing_suit_armor_reactive_fire : obj_item_clothing_suit_armor_reactive
{
    // tg: code/modules/clothing/suits/reactive_armour.dm:153
    public override async DmTask<object> reactive_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        owner.visible_message(span_danger(I18n.T("[] blocks [], sending out jets of flame!", this, attack_text)));
        playsound(get_turf(owner), "sound/effects/magic/fireball.ogg", 100, TRUE);
        foreach (mob_living_carbon carbon_victim in Dm.each<mob_living_carbon>(range(6, get_turf(this))))
        {
            if (carbon_victim != owner)
            {
                await carbon_victim.adjust_fire_stacks(8);
                carbon_victim.ignite_mob();
            }
        }
        await owner.set_wet_stacks(20);
        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return TRUE;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:164
    public override async DmTask<object> emp_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        owner.visible_message(span_danger(I18n.T("[] just makes [] worse by spewing molten death on []!", this, attack_text, owner)));
        playsound(get_turf(owner), "sound/effects/magic/fireball.ogg", 100, TRUE);
        await owner.adjust_fire_stacks(12);
        owner.ignite_mob();
        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return FALSE;
    }
}

//Stealth
// tg: code/modules/clothing/suits/reactive_armour.dm:174
public partial class obj_item_clothing_suit_armor_reactive_stealth : obj_item_clothing_suit_armor_reactive
{
    // tg: code/modules/clothing/suits/reactive_armour.dm:189
    public override void cooldown_activation(mob_living_carbon_human owner = null)
    {
        if (Dm.truthy(in_stealth))
            return; //we don't want the cooldown message either)
        owner.alpha = max(0, Dm.num(owner.alpha) - cooldown_alpha_removal);
        animate(owner, Dm.named("alpha", Dm.initial(owner, nameof(owner.alpha))), Dm.named("time", cooldown_animation_time));
        base.cooldown_activation(owner);
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:196
    public override async DmTask<object> reactive_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        mob_living_basic_illusion_escape decoy = Dm.@new<mob_living_basic_illusion_escape>(owner.loc);
        await decoy.full_setup(
            owner,
            target_mob: owner,
            life: 5 * SECONDS,
            hp: Dm.num(owner.health) / 4,
            damage: 5,
            replicate: 0
        );
        owner.alpha = 0;
        in_stealth = TRUE;
        owner.visible_message(span_danger(I18n.T("[] is hit by [] in the chest!", owner, attack_text))); //We pretend to be hit, since blocking it would stop the message otherwise
        _addtimer(Dm.@new<datum_callback>(this, nameof(end_stealth), owner), stealth_time, file: "code/modules/clothing/suits/reactive_armour.dm", line: 209);
        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return TRUE;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:213
    public virtual void end_stealth(mob_living_carbon_human owner = null)
    {
        in_stealth = FALSE;
        animate(owner, Dm.named("alpha", Dm.initial(owner, nameof(owner.alpha))), Dm.named("time", animation_time));
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:217
    public override async DmTask<object> emp_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        await DmTask.CompletedTask;
        if (!isliving(hitby))
            return FALSE; //it just doesn't activate
        mob_living attacker = hitby as mob_living;
        owner.visible_message(span_danger(I18n.T("[] activates, cloaking the wrong person!", this)));
        attacker.alpha = 0;
        // VARSET_CALLBACK(attacker, alpha, initial(attacker.alpha)): ___callbackvarset is not declared (vars is not translated, rules
        // 10.7), so the timer calls alpha__varset (as morgue.cs' layer__varset)
        _addtimer(Dm.@new<datum_callback>(this, nameof(alpha__varset), attacker, Dm.initial(attacker, nameof(attacker.alpha))), 4 * SECONDS, file: "code/modules/clothing/suits/reactive_armour.dm", line: 223);
        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return FALSE;
    }

    /// VARSET_CALLBACK(attacker, alpha, initial(attacker.alpha))'s target: ___callbackvarset's datum branch (varset_callback.dm:8-23)
    /// for the alpha.
    public virtual void alpha__varset(atom_movable target, object var_value)
    {
        if (!QDELETED(target))
            target.alpha = Dm.num(var_value);
    }
}

//Tesla
// tg: code/modules/clothing/suits/reactive_armour.dm:229
public partial class obj_item_clothing_suit_armor_reactive_tesla : obj_item_clothing_suit_armor_reactive
{
    // tg: code/modules/clothing/suits/reactive_armour.dm:244
    public override void cooldown_activation(mob_living_carbon_human owner = null)
    {
        do_sparks(1, TRUE, this);
        base.cooldown_activation(owner);
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:248
    public override async DmTask<object> reactive_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        owner.visible_message(span_danger(I18n.T("[] blocks [], sending out arcs of lightning!", this, attack_text)));
        await tesla_zap(source: owner, zap_range: zap_range, power: zap_power, cutoff: 1e3f, zap_flags: zap_flags);
        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return TRUE;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:254
    public override async DmTask<object> emp_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        owner.visible_message(span_danger(I18n.T("[] blocks [], but pulls a massive charge of energy into [] from the surrounding environment!", this, attack_text, owner)));
        REMOVE_TRAIT(owner, TRAIT_TESLA_SHOCKIMMUNE, Dm.text("[]_[]", CLOTHING_TRAIT, REF(this))); //oops! can't shock without this! (REMOVE_CLOTHING_TRAIT, expanded inline)
        await electrocute_mob(owner, get_area(this), this, 1);
        ADD_TRAIT(owner, TRAIT_TESLA_SHOCKIMMUNE, Dm.text("[]_[]", CLOTHING_TRAIT, REF(this))); // ADD_CLOTHING_TRAIT, expanded inline (rules 11.1 (d))
        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return TRUE;
    }
}

//Repulse
// tg: code/modules/clothing/suits/reactive_armour.dm:264
public partial class obj_item_clothing_suit_armor_reactive_repulse : obj_item_clothing_suit_armor_reactive
{
    // tg: code/modules/clothing/suits/reactive_armour.dm:271
    public override async DmTask<object> reactive_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        playsound(get_turf(owner), "sound/effects/magic/repulse.ogg", 100, TRUE);
        owner.visible_message(span_danger(I18n.T("[] blocks [], converting the attack into a wave of force!", this, attack_text)));
        turf owner_turf = get_turf(owner);
        DmList thrown_items = Dm.list();
        foreach (atom_movable repulsed in Dm.each<atom_movable>(range(owner_turf, 7)))
        {
            if (repulsed == owner || Dm.truthy(repulsed.anchored) || Dm.truthy(thrown_items[repulsed]))
                continue;
            object throwtarget = get_edge_target_turf(owner_turf, get_dir(owner_turf, get_step_away(repulsed, owner_turf)));
            await repulsed.safe_throw_at(throwtarget as atom, 10, 1, force: repulse_force);
            thrown_items[repulsed] = repulsed;
        }

        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return TRUE;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:286
    public override async DmTask<object> emp_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        playsound(get_turf(owner), "sound/effects/magic/repulse.ogg", 100, TRUE);
        owner.visible_message(span_danger(I18n.T("[] does not block [], and instead generates an attracting force!", this, attack_text)));
        turf owner_turf = get_turf(owner);
        DmList thrown_items = Dm.list();
        foreach (atom_movable repulsed in Dm.each<atom_movable>(range(owner_turf, 7)))
        {
            if (repulsed == owner || Dm.truthy(repulsed.anchored) || Dm.truthy(thrown_items[repulsed]))
                continue;
            await repulsed.safe_throw_at(owner, 10, 1, force: repulse_force);
            thrown_items[repulsed] = repulsed;
        }

        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return FALSE;
    }
}

// tg: code/modules/clothing/suits/reactive_armour.dm:300
public partial class obj_item_clothing_suit_armor_reactive_table : obj_item_clothing_suit_armor_reactive
{
    // tg: code/modules/clothing/suits/reactive_armour.dm:307
    public override async DmTask<object> reactive_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        owner.visible_message(span_danger(I18n.T("The reactive teleport system flings [] clear of [] and slams [] into a fabricated table!", owner, attack_text, owner.p_them())));
        owner.visible_message(Dm.text("<font color='red' size='3'>[]</font>", I18n.T("[] GOES ON THE TABLE!!!", owner)));
        await owner.Knockdown(30);
        await owner.apply_damage(10, BRUTE);
        await owner.apply_damage(40, STAMINA);
        playsound(owner, "sound/effects/tableslam.ogg", 90, TRUE);
        await owner.add_mood_event("table", typeof(datum_mood_event_table));
        await do_teleport(owner, get_turf(owner), tele_range, no_effects: TRUE, channel: TELEPORT_CHANNEL_BLUESPACE);
        Dm.@new<obj_structure_table>(get_turf(owner));
        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return TRUE;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:320
    public override async DmTask<object> emp_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        owner.visible_message(span_danger(I18n.T("The reactive teleport system flings [] clear of [] and slams [] into a fabricated glass table!", owner, attack_text, owner.p_them())));
        owner.visible_message(Dm.text("<font color='red' size='3'>[]</font>", I18n.T("[] GOES ON THE GLASS TABLE!!!", owner)));
        await do_teleport(owner, get_turf(owner), tele_range, no_effects: TRUE, channel: TELEPORT_CHANNEL_BLUESPACE);
        obj_structure_table_glass shattering_table = Dm.@new<obj_structure_table_glass>(get_turf(owner));
        await shattering_table.table_shatter(owner);

        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return TRUE;
    }
}

//Hallucinating
// tg: code/modules/clothing/suits/reactive_armour.dm:332
public partial class obj_item_clothing_suit_armor_reactive_hallucinating : obj_item_clothing_suit_armor_reactive
{
    // tg: code/modules/clothing/suits/reactive_armour.dm:339
    public override void cooldown_activation(mob_living_carbon_human owner = null)
    {
        do_sparks(1, TRUE, this);
        base.cooldown_activation(owner);
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:343
    public override async DmTask<object> reactive_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        owner.visible_message(span_danger(I18n.T("[] blocks [], sending out mental pulses!", this, attack_text)));
        await visible_hallucination_pulse(
            center: get_turf(owner),
            radius: 3,
            hallucination_duration: 50 * SECONDS,
            hallucination_max_duration: 300 * SECONDS
        );
        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return TRUE;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:354
    public override async DmTask<object> emp_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        owner.visible_message(span_danger(I18n.T("[] blocks [], but pulls a massive charge of mental energy into [] from the surrounding environment!", this, attack_text, owner)));
        await owner.adjust_hallucinations_up_to(50 * SECONDS, 240 * SECONDS);
        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return TRUE;
    }
}

//Bioscrambling
// tg: code/modules/clothing/suits/reactive_armour.dm:361
public partial class obj_item_clothing_suit_armor_reactive_bioscrambling : obj_item_clothing_suit_armor_reactive
{
    ///Lists for zones and bodyparts to swap and randomize (var/static/list/zones, chests, heads, l_arms, r_arms, l_legs, r_legs)
    // tg: code/modules/clothing/suits/reactive_armour.dm:372
    public static DmList zones => TgState.Current.obj_item_clothing_suit_armor_reactive_bioscrambling__zones ??= Dm.list(BODY_ZONE_HEAD, BODY_ZONE_CHEST, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG);

    // tg: code/modules/clothing/suits/reactive_armour.dm:377
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        TgState state = TgState.Current;
        if (state.obj_item_clothing_suit_armor_reactive_bioscrambling__chests == null)
            state.obj_item_clothing_suit_armor_reactive_bioscrambling__chests = typesof(typeof(obj_item_bodypart_chest));
        if (state.obj_item_clothing_suit_armor_reactive_bioscrambling__heads == null)
            state.obj_item_clothing_suit_armor_reactive_bioscrambling__heads = typesof(typeof(obj_item_bodypart_head));
        if (state.obj_item_clothing_suit_armor_reactive_bioscrambling__l_arms == null)
            state.obj_item_clothing_suit_armor_reactive_bioscrambling__l_arms = typesof(typeof(obj_item_bodypart_arm_left));
        if (state.obj_item_clothing_suit_armor_reactive_bioscrambling__r_arms == null)
            state.obj_item_clothing_suit_armor_reactive_bioscrambling__r_arms = typesof(typeof(obj_item_bodypart_arm_right));
        if (state.obj_item_clothing_suit_armor_reactive_bioscrambling__l_legs == null)
            state.obj_item_clothing_suit_armor_reactive_bioscrambling__l_legs = typesof(typeof(obj_item_bodypart_leg_left));
        if (state.obj_item_clothing_suit_armor_reactive_bioscrambling__r_legs == null)
            state.obj_item_clothing_suit_armor_reactive_bioscrambling__r_legs = typesof(typeof(obj_item_bodypart_leg_right));
        return dot;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:392
    public override void cooldown_activation(mob_living_carbon_human owner = null)
    {
        do_sparks(1, TRUE, this);
        base.cooldown_activation(owner);
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:396
    public override async DmTask<object> reactive_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        owner.visible_message(span_danger(I18n.T("[] blocks [], biohazard body scramble released!", this, attack_text)));
        await bioscrambler_pulse(owner, FALSE);
        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return TRUE;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:402
    public override async DmTask<object> emp_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        owner.visible_message(span_danger(I18n.T("[] blocks [], but pulls a massive charge of biohazard material into [] from the surrounding environment!", this, attack_text, owner)));
        await bioscrambler_pulse(owner, TRUE);
        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return TRUE;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:408
    public virtual async DmTask bioscrambler_pulse(mob_living_carbon_human owner = null, float? can_hit_owner = null)
    {
        can_hit_owner ??= FALSE;
        foreach (mob_living_carbon nearby in Dm.each<mob_living_carbon>(Builtins.range(range, get_turf(this))))
        {
            if (!Dm.truthy(can_hit_owner) && nearby == owner)
                continue;
            await nearby.bioscramble(name);
        }
    }
}

// When the wearer gets hit, this armor will push people nearby and spawn some blocking objects.
// tg: code/modules/clothing/suits/reactive_armour.dm:415
public partial class obj_item_clothing_suit_armor_reactive_barricade : obj_item_clothing_suit_armor_reactive
{
    // tg: code/modules/clothing/suits/reactive_armour.dm:422
    public override async DmTask<object> reactive_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        playsound(get_turf(owner), "sound/effects/magic/repulse.ogg", 100, TRUE);
        owner.visible_message(span_danger(I18n.T("The reactive armor interposes matter from another world between [] and []!", this, attack_text)));
        foreach (atom_movable target in Dm.each<atom_movable>(repulse_targets(owner)))
            await repulse(target, owner);

        datum_armour_dimensional_theme theme = Dm.@new<datum_armour_dimensional_theme>();
        await theme.apply_random(get_turf(owner), dangerous: FALSE);
        qdel(theme);

        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return TRUE;
    }

    /**
     * Returns a list of all atoms around the source which can be moved away from it.
     *
     * Arguments
     * * source - Thing to try to move things away from.
     */
    // tg: code/modules/clothing/suits/reactive_armour.dm:441
    public virtual object repulse_targets(atom source = null)
    {
        DmList push_targets = Dm.list();
        foreach (atom_movable nearby_movable in Dm.each<atom_movable>(view(1, source)))
        {
            if (nearby_movable == source)
                continue;
            if (Dm.truthy(nearby_movable.anchored))
                continue;
            Dm.append(ref push_targets, nearby_movable);
        }
        return push_targets;
    }

    /**
     * Pushes something one tile away from the source.
     *
     * Arguments
     * * victim - Thing being moved.
     * * source - Thing to move it away from.
     */
    // tg: code/modules/clothing/suits/reactive_armour.dm:458
    public virtual async DmTask repulse(atom_movable victim = null, atom source = null)
    {
        float dist_from_caster = get_dist(victim, source);

        if (dist_from_caster == 0)
            return;

        if (isliving(victim))
            to_chat(victim, span_userdanger(I18n.T("You're thrown back by a wave of pressure!")));
        turf throwtarget = get_edge_target_turf(source, get_dir(source, get_step_away(victim, source, 1))) as turf;
        await victim.safe_throw_at(throwtarget, 1, 1, source, force: MOVE_FORCE_EXTREMELY_STRONG);
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:469
    public override async DmTask<object> emp_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        owner.visible_message(span_danger(I18n.T("The reactive armor shunts matter from an unstable dimension!")));
        datum_armour_dimensional_theme theme = Dm.@new<datum_armour_dimensional_theme>();
        await theme.apply_random(get_turf(owner), dangerous: TRUE);
        qdel(theme);
        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return FALSE;
    }
}

// tg: code/modules/clothing/suits/reactive_armour.dm:477
public partial class obj_item_clothing_suit_armor_reactive_ectoplasm : obj_item_clothing_suit_armor_reactive
{
    // tg: code/modules/clothing/suits/reactive_armour.dm:484
    public override async DmTask<object> reactive_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        await DmTask.CompletedTask;
        playsound(get_turf(owner), "sound/effects/hallucinations/veryfar_noise.ogg", 100, TRUE);
        owner.visible_message(span_danger(I18n.T("The [] lets loose a burst of otherworldly energy!", this)));

        haunt_outburst(epicenter: get_turf(owner), range: 5, haunt_chance: 85, duration: 30 * SECONDS);

        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return TRUE;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:493
    public override async DmTask<object> emp_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        if (owner.reagents != null)
            await (owner.reagents as datum_reagents).add_reagent(typeof(datum_reagent_inverse_helgrasp), 20);
        return null;
    }
}

// tg: code/modules/clothing/suits/reactive_armour.dm:496
public partial class obj_item_clothing_suit_armor_reactive_weather : obj_item_clothing_suit_armor_reactive
{
    // tg: code/modules/clothing/suits/reactive_armour.dm:503
    public override async DmTask<object> reactive_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        attack_text ??= "the attack";
        owner.visible_message(span_danger(I18n.T("The reactive armor alters the weather around [], shielding [] from []!", owner, owner.p_them(), attack_text)));
        playsound(this, "sound/effects/magic/lightningshock.ogg", 33, TRUE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);

        datum_effect_system_basic_steam_spread steam = Dm.@new<datum_effect_system_basic_steam_spread>(owner.loc, 10, FALSE);
        await steam.start();

        DmList affected_turfs = Dm.list();
        foreach (mob_living attacker in Dm.each<mob_living>(oview(2, owner)))
        {
            await attacker.adjust_wet_stacks(5);
            attacker.extinguish_mob();
            if (Dm.@in(attacker.loc, affected_turfs) || !isopenturf(attacker.loc))
                continue;

            foreach (turf_open to_affect in Dm.each<turf_open>(view(1, attacker.loc)))
                Dm.append(ref affected_turfs, to_affect);

            shock_turf_windup(attacker.loc as turf);
        }

        reactivearmor_cooldown = world.time + reactivearmor_cooldown_duration;
        return TRUE;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:525
    public override async DmTask<object> emp_activation(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null)
    {
        owner.visible_message(span_danger(I18n.T("The reactive armor malfunctions, calling down a storm upon []!", owner.p_them())));
        playsound(this, "sound/effects/magic/lightningshock.ogg", 33, TRUE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);

        datum_effect_system_basic_steam_spread steam = Dm.@new<datum_effect_system_basic_steam_spread>(owner.loc, 2, FALSE);
        await steam.start();

        await owner.adjust_wet_stacks(10);
        owner.extinguish_mob();
        if (!isopenturf(owner.loc))
            return null;
        shock_turf_windup(owner.loc as turf);
        return null;
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:538
    public virtual void shock_turf_windup(turf target = null)
    {
        Dm.@new<obj_effect_temp_visual_telegraphing_circle>(target);
        _addtimer(Dm.@new<datum_callback>(this, nameof(shock_turf), target), 1 * SECONDS, file: "code/modules/clothing/suits/reactive_armour.dm", line: 540);
    }

    // tg: code/modules/clothing/suits/reactive_armour.dm:542
    public virtual async DmTask shock_turf(turf target = null)
    {
        playsound(target, "sound/effects/magic/lightningbolt.ogg", 66, TRUE);
        Dm.@new<obj_effect_temp_visual_thunderbolt>(target);
        foreach (turf_open adjacent_turf in Dm.each<turf_open>(oview(1, target)))
            Dm.@new<obj_effect_temp_visual_electricity>(adjacent_turf);

        foreach (mob_living hit_mob in Dm.each<mob_living>(target))
        {
            if (hit_mob == loc) // avoid hitting the wearer
                continue;
            to_chat(hit_mob, span_userdanger(I18n.T("You've been struck by lightning!")));
            await hit_mob.electrocute_act(30, this, flags: Dm.bitor(SHOCK_TESLA, SHOCK_NOSTUN));
            await hit_mob.Knockdown(2.5f * SECONDS, 10 * SECONDS);
        }

        foreach (mob_living nearby_target in Dm.each<mob_living>(oview(1, target)))
        {
            if (nearby_target == loc) // avoid hitting the wearer
                continue;
            to_chat(nearby_target, span_userdanger(I18n.T("You've been struck by an arc of lightning!")));
            await nearby_target.electrocute_act(10, this, flags: Dm.bitor(SHOCK_TESLA, SHOCK_NOSTUN));
        }

        foreach (obj hit_thing in Dm.each<obj>(target))
            await hit_thing.take_damage(20, BURN, ENERGY, FALSE);

        foreach (obj nearby_thing in Dm.each<obj>(oview(1, target)))
            await nearby_thing.take_damage(5, BURN, ENERGY, FALSE);
    }
}

// The reactive armours' statics (rules 10.6).
public sealed partial class TgState
{
    // tg: code/modules/clothing/suits/reactive_armour.dm:10
    internal DmList obj_item_reactive_armor_shell__item_interaction__anomaly_armour_types;
    // tg: code/modules/clothing/suits/reactive_armour.dm:372-378
    internal DmList obj_item_clothing_suit_armor_reactive_bioscrambling__zones;
    internal DmList obj_item_clothing_suit_armor_reactive_bioscrambling__chests;
    internal DmList obj_item_clothing_suit_armor_reactive_bioscrambling__heads;
    internal DmList obj_item_clothing_suit_armor_reactive_bioscrambling__l_arms;
    internal DmList obj_item_clothing_suit_armor_reactive_bioscrambling__r_arms;
    internal DmList obj_item_clothing_suit_armor_reactive_bioscrambling__l_legs;
    internal DmList obj_item_clothing_suit_armor_reactive_bioscrambling__r_legs;
}
