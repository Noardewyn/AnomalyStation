// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/boulder_processing/boulder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2c (D304): the whole file. The var block is generated (D111). The Sisyphus achievement is not ported (owner-deferred,
// D333); the mining skill came with gaps SL1.

/**
 * The objects that ore vents produce, which is refined into minerals.
 */
// tg: code/modules/mining/boulder_processing/boulder.dm:6
public partial class obj_item_boulder : obj_item
{
    // tg: code/modules/mining/boulder_processing/boulder.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("require_twohands", TRUE), Dm.named("force_unwielded", 0), Dm.named("force_wielded", 5))); //Heavy as all hell, it's a boulder, dude.
        // AddComponent(/datum/component/sisyphus_awarder): the achievements are not ported
        Dm.Deferred("code/modules/mining/boulder_processing/boulder.dm:36", "Initialize: the sisyphus_awarder component (the achievements are not ported)");
        Waiting.NoSleep(AddComponent(typeof(datum_component_bane), Dm.named("affected_biotypes", MOB_SPECIAL), Dm.named("added_damage", 20)));
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/boulder.dm:39
    public override object Destroy(float? force = null)
    {
        SSore_generation?.available_boulders.Remove(this); // a host-built room without the Master has no SSore_generation
        processed_by = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mining/boulder_processing/boulder.dm:44
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item != null && (Dm.eq(held_item.tool_behaviour, TOOL_MINING) || HAS_TRAIT(held_item, TRAIT_BOULDER_BREAKER)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Crush boulder into ore";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (HAS_TRAIT(user, TRAIT_BOULDER_BREAKER))
        {
            if (isbasicmob(user))
                context[SCREENTIP_CONTEXT_LMB] = "Crush boulder into ore";
            else
                context[SCREENTIP_CONTEXT_RMB] = "Crush boulder into ore";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return null;
    }

    // tg: code/modules/mining/boulder_processing/boulder.dm:55
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("This boulder would take [] more steps to refine or break.", durability)));
        if (HAS_TRAIT(user, TRAIT_BOULDER_BREAKER))
            Dm.append(ref dot, span_notice(I18n.T("You can crush this boulder with your bare hands.")));
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/boulder.dm:61
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("[] can either be cracked open by [], or processed into sheets with []. Undisturbed boulders can be collected by the [].",
            span_bold(I18n.T("Boulders")), span_bold(I18n.T("mining tools")), span_bold(I18n.T("refineries or smelters")), span_bold(I18n.T("BRM")))));
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/boulder.dm:65
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        switch (boulder_size)
        {
            case BOULDER_SIZE_SMALL:
                icon_state = Dm.text("[]_small", boulder_string);
                break;
            case BOULDER_SIZE_MEDIUM:
                icon_state = Dm.text("[]_medium", boulder_string);
                break;
            case BOULDER_SIZE_LARGE:
                icon_state = Dm.text("[]_large", boulder_string);
                break;
            default:
                icon_state = Dm.text("[]_small", boulder_string);
                break;
        }
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/boulder.dm:77
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        if (istype(mover, typeof(obj_item_boulder))) //This way, boulders can only go one at a time on conveyor belts, but everyone else can go through.
            return FALSE;
        return await base.CanAllowThrough(mover, border_dir);
    }

    // tg: code/modules/mining/boulder_processing/boulder.dm:82
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (HAS_TRAIT(user, TRAIT_BOULDER_BREAKER))
        {
            await manual_process(null, (mob_living)user, INNATE_BOULDER_SPEED_MULTIPLIER);
            return dot;
        }
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/boulder.dm:90
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.truthy(dot))
            return dot;
        if (HAS_TRAIT(user, TRAIT_BOULDER_BREAKER))
        {
            await manual_process(null, (mob_living)user, INNATE_BOULDER_SPEED_MULTIPLIER);
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/boulder.dm:98
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (HAS_TRAIT(tool, TRAIT_BOULDER_BREAKER))
        {
            await manual_process(tool, user, INNATE_BOULDER_SPEED_MULTIPLIER);
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.eq(tool.tool_behaviour, TOOL_MINING))
        {
            await manual_process(tool, user);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/modules/mining/boulder_processing/boulder.dm:107
    public override async DmTask<object> attack_basic_mob(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_basic_mob(user, modifiers);
        if (Dm.truthy(dot))
            return dot;
        if (HAS_TRAIT(user, TRAIT_BOULDER_BREAKER))
        {
            await manual_process(null, (mob_living)user, INNATE_BOULDER_SPEED_MULTIPLIER); //A little hacky but it works around the speed of the blackboard task selection process for now.
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/boulder.dm:115
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (istype(interacting_with, typeof(turf_open_lava)))
        {
            if (!Dm.truthy(create_platform(interacting_with, user, null)))
                return ITEM_INTERACT_BLOCKING;
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/boulder.dm:122
    public override async DmTask<object> throw_at(atom target = null, object range = null, object speed = null, atom thrower = null, float? spin = null, float? diagonals_first = null, datum_callback callback = null, float? force = null, float? gentle = null, object quickstart = null, Type throw_datum_typepath = null, params object[] __extra)
    {
        object dot = await base.throw_at(target, range, speed, thrower, spin, diagonals_first, callback, force, gentle, quickstart, throw_datum_typepath, __extra);
        if (istype(target, typeof(turf_open_lava)))
        {
            if (!Dm.truthy(create_platform(target, thrower as mob_living)))
                return FALSE;
        }
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/boulder.dm:128
    public virtual object create_platform(atom interacting_with = null, mob_living user = null, object timer_override = null)
    {
        if (Dm.locate<obj_structure_lattice_catwalk_boulder>(interacting_with) != null)
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("There is already a boulder platform here!")));
            return null;
        }

        float active_platform_lifespan = platform_lifespan; //Default to the assigned value.
        if (Dm.truthy(timer_override))
            active_platform_lifespan = Dm.num(timer_override);

        obj_structure_lattice_catwalk_boulder platform = Dm.@new<obj_structure_lattice_catwalk_boulder>(interacting_with);
        _addtimer(Dm.@new<datum_callback>(platform, nameof(obj_structure_lattice_catwalk_boulder.pre_self_destruct)), active_platform_lifespan, file: "code/modules/mining/boulder_processing/boulder.dm", line: 139);
        // See Lattice.dm for more info
        visible_message(span_notice(I18n.T("\\The [] floats on \\the [], forming a temporary platform!", this, interacting_with)));
        qdel(this);
        return platform;
    }

    /**
     * This is called when a boulder is processed by a mob or tool, and reduces the durability of the boulder.
     * @param obj/item/weapon The weapon that is being used to process the boulder, that we pull toolspeed from. If null, we use the override_speed_multiplier instead.
     * @param mob/living/user The mob that is processing the boulder.
     * @param override_speed_multiplier The speed multiplier to use if weapon is null. The do_after will take 2 * this value seconds to complete.
     * @param continued Whether or not this is a continued process, or the first one. If true, we don't play the "You swing at the boulder" message.
     */
    // tg: code/modules/mining/boulder_processing/boulder.dm:152
    public virtual async DmTask manual_process(obj_item weapon = null, mob_living user = null, object override_speed_multiplier = null, float? continued = null)
    {
        continued ??= FALSE;
        float process_speed = 0;
        //Handle weapon conditions.
        float skill_modifier = Dm.num((user.mind?.get_skill_modifier(typeof(datum_skill_mining), SKILL_SPEED_MODIFIER) is var or__1 && Dm.truthy(or__1) ? or__1 : 1));
        if (weapon != null)
        {
            if (HAS_TRAIT(weapon, TRAIT_INSTANTLY_PROCESSES_BOULDERS))
                durability = 0;
            process_speed = weapon.toolspeed;
            weapon.play_tool_sound(this, 50);
            if (!Dm.truthy(continued))
                to_chat(user, span_notice(I18n.T("You swing at \\the []...", this)));
        }

        // Handle user conditions/override conditions.
        else if (Dm.truthy(override_speed_multiplier) || HAS_TRAIT(user, TRAIT_BOULDER_BREAKER))
        {
            if (HAS_TRAIT(user, TRAIT_INSTANTLY_PROCESSES_BOULDERS))
                durability = 0;
            else if (Dm.truthy(override_speed_multiplier))
                process_speed = Dm.num(override_speed_multiplier);
            else
                process_speed = INNATE_BOULDER_SPEED_MULTIPLIER;
            playsound(this, Dm.resource("sound/effects/rock/rocktap1.ogg"), 50);
            if (!Dm.truthy(continued))
                to_chat(user, span_notice(I18n.T("You scrape away at \\the []...", this)));
        }
        else
            throw Dm.CRASH("No weapon, acceptable user, or override speed multiplier passed to manual_process()", "code/modules/mining/boulder_processing/boulder.dm", 177);
        if (durability > 0)
        {
            if (!Dm.truthy(await do_after(user, 2 * process_speed * skill_modifier * SECONDS, target: this)))
                return;
            if (!Dm.truthy(await user.Adjacent(this)))
                return;
            durability--;
            await user.apply_damage(4 * skill_modifier, STAMINA);
        }
        if (durability <= 0)
        {
            await convert_to_ore();
            to_chat(user, span_notice(I18n.T("You finish working on \\the [], and it crumbles into ore.", this)));
            playsound(this, Dm.resource("sound/effects/rock/rock_break.ogg"), 50);
            user.mind?.adjust_experience(typeof(datum_skill_mining), MINING_SKILL_BOULDER_SIZE_XP * 0.2f);
            user.mind?.adjust_experience(typeof(datum_skill_athletics), MINING_SKILL_BOULDER_SIZE_XP * 0.2f);
            qdel(this);
            return;
        }
        string msg = durability == 1 ? I18n.T("is crumbling!") : I18n.T("looks weaker!");
        to_chat(user, span_notice(I18n.T("\\The [] []", this, msg)));
        await manual_process(weapon, user, override_speed_multiplier, continued: TRUE);
    }

    /**
     * This function is called while breaking boulders manually, and drops ore based on the boulder's mineral content.
     * Quantity of ore spawned here is 1 less than if the boulder was processed by a machine, but clamped at 10 maximum, 1 minimum.
     *
     * target_destination: Optional - Sets the location directly instead of dropping it
     */
    // tg: code/modules/mining/boulder_processing/boulder.dm:202
    public virtual async DmTask convert_to_ore(atom target_destination = null)
    {
        foreach (datum_material picked in Dm.each<datum_material>(custom_materials))
        {
            // Take the associated value and convert it into ore stacks...
            float quantity = clamp(round((Dm.num(custom_materials[picked]) - SHEET_MATERIAL_AMOUNT) / SHEET_MATERIAL_AMOUNT), 1, 10); //but less resources than if they processed it by hand.

            object cracked_ore_type = picked.ore_type;
            if (cracked_ore_type == null)
            {
                stack_trace(Dm.text("boulder found containing material type [] with no set ore_type", picked.GetType()), "code/modules/mining/boulder_processing/boulder.dm", 209);
                continue;
            }
            object ore_destination = drop_location();
            if (target_destination != null)
                ore_destination = target_destination;
            obj_item_stack_ore cracked_ore = Dm.@new((System.Type)cracked_ore_type, ore_destination, quantity) as obj_item_stack_ore;
            SSblackbox.record_feedback("tally", "ore_mined", quantity, cracked_ore.GetType());
        }
        await DmTask.CompletedTask;
    }

    ///Moves boulder contents to the drop location, and then deletes the boulder.
    // tg: code/modules/mining/boulder_processing/boulder.dm:218
    public virtual async DmTask break_apart()
    {
        if (Dm.truthy(length(contents)))
        {
            DmList quips = Dm.list("Clang!", "Crack!", "Bang!", "Clunk!", "Clank!");
            visible_message(span_notice(I18n.T("[] Something falls out of \\the []!", pick(quips), this)));
            playsound(loc, Dm.resource("sound/effects/pickaxe/picaxe1.ogg"), 60, FALSE);
            foreach (obj_item content in Dm.each<obj_item>(contents))
                await content.forceMove(get_turf(this));
        }
        qdel(this);
    }
}
