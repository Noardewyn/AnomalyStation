// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/shields.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// /obj/item/shield, the var-only shields, the riot and telescopic shields (gaps J4, D298); the energy shield (syndicate N2a, D316: the
// operatives' sword and board); the strobe shield (its flash bulb flashes whom it bashes without combat mode, the room when used in
// hand or when it blocks, drawn with tg's flash flick and burnt-out look, a new bulb fitted in two seconds; `embedded_flash` holds
// a path until made, so it is hand-declared with the generated constructor kept here, hand mode) and the ballistic shield's repair
// with titanium (gaps WP1, D333).

// tg: code/game/objects/items/weaponry/shields.dm:3
public partial class obj_item_shield : obj_item
{
    private const float WEAPON_BASH_COOLDOWN = 3 * SECONDS;

    // tg: code/game/objects/items/weaponry/shields.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_disarm_attack)));
        Dm.Deferred("code/game/objects/items/weaponry/shields.dm:47", "Initialize: AddElement(/datum/element/cuffable_item): cuffing the shield to the wrist (the element and the cuffed_item status effect)");
        return dot;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:49
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        float effective_block_chance = final_block_chance.Value;
        if (Dm.truthy(transparent) && Dm.truthy(Dm.bitand(hitby.pass_flags, PASSGLASS)))
            return FALSE;
        if (attack_type == THROWN_PROJECTILE_ATTACK)
            effective_block_chance += 30;
        if (attack_type == LEAP_ATTACK)
            effective_block_chance = 100;
        if (attack_type == OVERWHELMING_ATTACK)
            effective_block_chance = 0;
        final_block_chance = clamp(effective_block_chance, 0, 100);
        object dot = await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
        if (Dm.truthy(dot))
            await on_shield_block(owner, hitby, attack_text, damage, attack_type, damage_type);
        return dot;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:64
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        float healthpercent = round((Dm.num(atom_integrity) / Dm.num(max_integrity)) * 100, 1);
        if (healthpercent >= 50 && healthpercent <= 99)
            dot.Add(span_info(I18n.T("It looks slightly damaged.")));
        else if (healthpercent >= 25 && healthpercent <= 50)
            dot.Add(span_info(I18n.T("It appears heavily damaged.")));
        else if (healthpercent >= 0 && healthpercent <= 25)
            dot.Add(span_warning(I18n.T("It's falling apart!")));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:75
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (!istype(tool, typeof(obj_item_melee)) || !Dm.truthy(is_bashable))
            return dot;
        if (weapon_bash > Dm.num(world.time)) // COOLDOWN_FINISHED, inline
            return ITEM_INTERACT_BLOCKING;
        playsound(this, shield_bash_sound, 50, TRUE);
        user.manual_emote(I18n.T("bashes [] with []!", this, tool));
        weapon_bash = Dm.num(world.time) + WEAPON_BASH_COOLDOWN; // COOLDOWN_START, inline
        user.Shake(3, 3, 0.5f * SECONDS);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:89
    public virtual async DmTask<object> on_shield_block(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        damage ??= 0;
        damage_type ??= BRUTE;
        if (!Dm.truthy(breakable_by_damage) || (damage_type != BRUTE && damage_type != BURN))
            return TRUE;
        object penetration = 0;
        object armor_flag = MELEE;
        if (hitby is obj_projectile bang_bang)
        {
            armor_flag = bang_bang.armor_flag;
            penetration = bang_bang.armour_penetration;
        }
        else if (hitby is obj_item weapon)
            penetration = weapon.armour_penetration;
        else if (hitby is mob_living_simple_animal critter)
            penetration = critter.armour_penetration;
        else if (hitby is mob_living_basic basic_critter)
            penetration = basic_critter.armour_penetration;
        await take_damage(damage, damage_type, armor_flag, armour_penetration: Dm.num(penetration));
        return null;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:109
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        playsound(this, shield_break_sound, 50);
        Dm.@new((Type)shield_break_leftover, get_turf(this));
        if (isliving(loc))
            loc.balloon_alert(loc as mob, I18n.T("shield broken!"));
        await base.atom_destruction(damage_flag);
    }
}

// tg: code/game/objects/items/weaponry/shields.dm:167
public partial class obj_item_shield_riot : obj_item_shield
{
    // tg: code/game/objects/items/weaponry/shields.dm:181
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), Dm.named("slapcraft_recipes", Dm.list(typeof(datum_crafting_recipe_strobeshield)))));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:190
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_stack_sheet_mineral_titanium)))
            return NONE;

        if (Dm.num(atom_integrity) >= Dm.num(max_integrity))
        {
            to_chat(user, span_warning(I18n.T("[] is already in perfect condition.", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_item_stack_sheet_mineral_titanium titanium_sheet = tool as obj_item_stack_sheet_mineral_titanium;
        await titanium_sheet.use(1);
        atom_integrity = max_integrity;
        to_chat(user, span_notice(I18n.T("You repair [] with [].", this, titanium_sheet)));
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/items/weaponry/shields.dm:398
public partial class obj_item_shield_riot_tele : obj_item_shield_riot
{
    // tg: code/game/objects/items/weaponry/shields.dm:411
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_transforming),
            Dm.named("force_on", 8),
            Dm.named("throwforce_on", 5),
            Dm.named("throw_speed_on", 2),
            Dm.named("hitsound_on", hitsound),
            Dm.named("w_class_on", WEIGHT_CLASS_BULKY),
            Dm.named("attack_verb_continuous_on", Dm.list("smacks", "strikes", "cracks", "beats")),
            Dm.named("attack_verb_simple_on", Dm.list("smack", "strike", "crack", "beat"))));

        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        RegisterSignal(this, COMSIG_ITEM_CAN_DISARM_ATTACK, nameof(can_disarm_attack));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:427
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        if (HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
        return FALSE;
    }

    /**
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     *
     * Allows it to be placed on back slot when active.
     */
    // tg: code/game/objects/items/weaponry/shields.dm:437
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        slot_flags = Dm.truthy(active) ? ITEM_SLOT_BACK : 0;
        if (user != null)
            balloon_alert(user, Dm.truthy(active) ? I18n.T("extended") : I18n.T("collapsed"));
        playsound(this, Dm.resource("sound/items/weapons/batonextend.ogg"), 50, TRUE);
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:446
    [SignalHandler]
    public virtual object can_disarm_attack(datum source = null, mob_living victim = null, mob_living user = null, object send_message = null)
    {
        send_message ??= TRUE;
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
        {
            if (Dm.truthy(send_message))
                balloon_alert(user, I18n.T("extend it first!"));
            return COMPONENT_BLOCK_ITEM_DISARM_ATTACK;
        }
        return null;
    }
}

// tg: code/game/objects/items/weaponry/shields.dm:204
public partial class obj_item_shield_riot_flash : obj_item_shield_riot
{
    // tg: code/game/objects/items/weaponry/shields.dm:209  var/obj/item/assembly/flash/handheld/embedded_flash (a path until made)
    public object embedded_flash = typeof(obj_item_assembly_flash_handheld);

    // tg: code/game/objects/items/weaponry/shields.dm:205  (the generated constructor, kept here: hand mode, see the header)
    public obj_item_shield_riot_flash()
    {
        name = "strobe shield";
        desc = "A shield with a built in, high intensity light capable of blinding and disorienting suspects. Takes regular handheld flashes as bulbs.";
        icon_state = "flashshield";
        inhand_icon_state = "flashshield";
    }

    // tg: code/game/objects/items/weaponry/shields.dm:210  (the generated var initializer, kept here: hand mode)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_iron), 555), Dm.kv(typeof(datum_material_glass), 480.00003f)); // tg: code/game/objects/items/weaponry/shields.dm:210
    }

    private obj_item_assembly_flash_handheld flash_bulb => embedded_flash as obj_item_assembly_flash_handheld;

    // tg: code/game/objects/items/weaponry/shields.dm:212
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        if (embedded_flash != null)
        {
            embedded_flash = Dm.@new((Type)embedded_flash, this);
            flash_bulb.set_light_flags(Dm.bitor(flash_bulb.light_flags, LIGHT_ATTACHED));
            update_appearance(UPDATE_ICON);
        }
        return dot;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:220
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (arrived is obj_item_assembly_flash_handheld)
        {
            embedded_flash = arrived;
            flash_bulb.set_light_flags(Dm.bitor(flash_bulb.light_flags, LIGHT_ATTACHED));
            update_appearance(UPDATE_ICON);
        }
        return await base.Entered(arrived, old_loc, old_locs);
    }

    // tg: code/game/objects/items/weaponry/shields.dm:227
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == embedded_flash)
        {
            flash_bulb.set_light_flags(Dm.bitand(flash_bulb.light_flags, Dm.bitnot(LIGHT_ATTACHED)));
            embedded_flash = null;
            update_appearance(UPDATE_ICON);
        }
        return await base.Exited(gone, direction);
    }

    // tg: code/game/objects/items/weaponry/shields.dm:234
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        if (Dm.eq(var_name, nameof(embedded_flash)))
            update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:239
    public override object Destroy(float? force = null)
    {
        if (embedded_flash is datum bulb)
            qdel(bulb);
        embedded_flash = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/weaponry/shields.dm:243
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        await flash_away(user, target_mob);
        return null;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:248
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await flash_away(user);
        return null;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:251
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        object dot = await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
        if (Dm.truthy(dot))
            await flash_away(owner);
        return dot;
    }

    ///Handles calls for the actual flash object + plays the flashing animations.
    // tg: code/game/objects/items/weaponry/shields.dm:257
    public virtual async DmTask flash_away(mob owner = null, mob target = null, object animation_only = null)
    {
        if (QDELETED(flash_bulb) || (Dm.truthy(flash_bulb.burnt_out) && !Dm.truthy(animation_only)))
            return;
        object flick_it = Dm.truthy(animation_only) ? TRUE : (target != null ? await flash_bulb.attack__proc(target as mob_living, owner as mob_living) : await flash_bulb.AOE_flash(user: owner));
        if (!Dm.truthy(flick_it) && !Dm.truthy(flash_bulb.burnt_out))
            return;
        flick("flashshield_flash", this);
        inhand_icon_state = "flashshield_flash";
        if (owner != null)
            await owner.update_held_items();
        _addtimer(Dm.@new<datum_callback>(this, nameof(atom.update_appearance)), 0.5f * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/game/objects/items/weaponry/shields.dm", line: 266); //.5 second delay so the inhands sprite finishes its anim since inhands don't support flick().
    }

    // tg: code/game/objects/items/weaponry/shields.dm:268
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_assembly_flash_handheld flash)
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (Dm.truthy(flash.burnt_out))
        {
            to_chat(user, span_warning(I18n.T("No sense replacing it with a broken bulb!")));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You begin to replace the bulb...")));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: user)))
            return ITEM_INTERACT_BLOCKING;

        if (QDELETED(flash) || Dm.truthy(flash.burnt_out))
            return ITEM_INTERACT_BLOCKING;

        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        qdel(embedded_flash as datum);
        await flash.forceMove(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:289
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (QDELETED(flash_bulb) || Dm.truthy(flash_bulb.burnt_out))
            return dot;
        await flash_bulb.emp_act(severity);
        if (Dm.truthy(flash_bulb.burnt_out)) // a little hacky but no good way to check otherwise.
            await flash_away(ismob(loc) ? loc as mob : null, animation_only: TRUE);
        return dot;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:297
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (QDELETED(flash_bulb) || Dm.truthy(flash_bulb.burnt_out))
        {
            icon_state = "riot";
            inhand_icon_state = "riot";
        }
        else
        {
            icon_state = "flashshield";
            inhand_icon_state = "flashshield";
        }
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/weaponry/shields.dm:306
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();
        if (Dm.truthy(flash_bulb?.burnt_out))
            Dm.append(dot, span_info(I18n.T("The mounted bulb has burnt out. You can try replacing it with a new <b>flash</b>.")));
        return dot;
    }
}

// tg: code/game/objects/items/weaponry/shields.dm:311
public partial class obj_item_shield_energy : obj_item_shield
{
    // tg: code/game/objects/items/weaponry/shields.dm:338
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_transforming),
            Dm.named("force_on", active_force),
            Dm.named("throwforce_on", active_throwforce),
            Dm.named("throw_speed_on", active_throw_speed),
            Dm.named("hitsound_on", hitsound),
            Dm.named("clumsy_check", Dm.truthy(can_clumsy_use) ? FALSE : TRUE)));
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        RegisterSignal(this, COMSIG_ITEM_CAN_DISARM_ATTACK, nameof(can_disarm_attack));
        return dot;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:351
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return FALSE;

        float effective_block_chance = final_block_chance.Value;
        if (attack_type == OVERWHELMING_ATTACK)
            effective_block_chance = 0;

        if (attack_type == PROJECTILE_ATTACK)
        {
            var our_projectile = hitby as obj_projectile;

            if (Dm.truthy(our_projectile.reflectable)) //We handle this via IsReflect() instead.
                effective_block_chance = 0;
        }
        final_block_chance = clamp(effective_block_chance, 0, 100);
        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }

    // tg: code/game/objects/items/weaponry/shields.dm:367
    public override async DmTask<object> IsReflect(object def_zone = null)
    {
        return HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE) && prob(reflection_probability) ? TRUE : FALSE;
    }

    /*
     * Signal proc for [COMSIG_TRANSFORMING_ON_TRANSFORM].
     */
    // tg: code/game/objects/items/weaponry/shields.dm:373
    [SignalHandler]
    public virtual object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        if (user != null)
            balloon_alert(user, Dm.truthy(active) ? I18n.T("activated") : I18n.T("deactivated"));
        playsound(this, Dm.truthy(active) ? "sound/items/weapons/saberon.ogg" : "sound/items/weapons/saberoff.ogg", 35, TRUE);
        is_bashable = Dm.truthy(is_bashable) ? FALSE : TRUE;
        return COMPONENT_NO_DEFAULT_MESSAGE;
    }

    // tg: code/game/objects/items/weaponry/shields.dm:382
    [SignalHandler]
    public virtual object can_disarm_attack(datum source = null, mob_living victim = null, mob_living user = null, object send_message = null)
    {
        send_message ??= TRUE;
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
        {
            if (Dm.truthy(send_message))
                balloon_alert(user, I18n.T("activate it first!"));
            return COMPONENT_BLOCK_ITEM_DISARM_ATTACK;
        }
        return null;
    }
}

// tg: code/game/objects/items/weaponry/shields.dm:459
public partial class obj_item_shield_ballistic : obj_item_shield
{
    // tg: code/game/objects/items/weaponry/shields.dm:469
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_stack_sheet_mineral_titanium)))
            return NONE;

        if (Dm.num(atom_integrity) >= Dm.num(max_integrity))
        {
            to_chat(user, span_warning(I18n.T("[] is already in perfect condition.", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_item_stack_sheet_mineral_titanium titanium_sheet = tool as obj_item_stack_sheet_mineral_titanium;
        await titanium_sheet.use(1);
        atom_integrity = max_integrity;
        to_chat(user, span_notice(I18n.T("You repair [] with [].", this, titanium_sheet)));
        return ITEM_INTERACT_SUCCESS;
    }
}
