// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/space_vines/vine_structure.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4b (D327): the whole file, the space vine: its three growth stages (Light, Med and Hvy, opaque from the second), the
// entangling of the mobs on it, the spreading to a free floor beside it (sliding in from the vine it grew from), the cutting (four
// times the force of a sharp or a burning item), the eating, the explosions, the heat that burns it and the cold that stops it, the
// sunlight it blocks; every mutation's hooks. The var block is generated (D111).

// SPACE VINES (Note that this code is very similar to Biomass code)
// tg: code/modules/events/space_vines/vine_structure.dm:2
public partial class obj_structure_spacevine : obj_structure
{
    // tg: code/modules/events/space_vines/vine_structure.dm:35  var/static/list/loc_connections (rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_structure_spacevine__Initialize__loc_connections;

    // tg: code/modules/events/space_vines/vine_structure.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_CHASM_DESTROYED, INNATE_TRAIT);
        ADD_TRAIT(this, TRAIT_INVERTED_DEMOLITION, INNATE_TRAIT);
        Waiting.NoSleep(add_atom_colour("#ffffff", FIXED_COLOUR_PRIORITY));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        Waiting.NoSleep(AddComponent(typeof(datum_component_storm_hating)));
        Waiting.NoSleep(block_sunlight());
        return dot;
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:43
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(length(mutations)))
        {
            Dm.append(ref dot, I18n.T("This vine has no mutations."));
            return dot;
        }
        string text = I18n.T("This vine has the following mutations:\n");
        foreach (datum_spacevine_mutation mutation in Dm.each_cast<datum_spacevine_mutation>(mutations))
        {
            if (mutation.name == "transparent") /// Transparent has no hue
                text += I18n.T("<font color='#346751'>Transparent</font> ");
            else
                text += Dm.text("<font color='[]'>[]</font> ", mutation.hue, mutation.name);
        }
        Dm.append(ref dot, text);
        return dot;
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:56
    public override object Destroy(float? force = null)
    {
        foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(mutations))
            Waiting.NoSleep(mutation.on_death(this));
        if (master != null)
            master.VineDestroyed(this);
        mutations = Dm.list();
        Waiting.NoSleep(set_opacity(PASS_LIGHT));
        if (Dm.truthy(has_buckled_mobs()))
            unbuckle_all_mobs(force: 1);
        return base.Destroy(force);
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:67
    public virtual void on_chem_effect(datum_reagent chem = null)
    {
        object chem_flags = null;
        foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(mutations))
        {
            mutation.on_chem(this, chem);
            Dm.combine(ref chem_flags, null); // on_chem returns nothing (its slot is void): DM's null
        }
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:72
    public virtual async DmTask eat(mob eater = null)
    {
        object eat_flags = null;
        foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(mutations))
        {
            await mutation.on_eat(this, eater as mob_living);
            Dm.combine(ref eat_flags, null); // on_eat returns nothing (its slot is void): DM's null
        }

        if (Dm.truthy(Dm.bitand(eat_flags, BLOCK_EAT_ATTEMPT)))
            return;

        qdel(this);
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:82
    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        obj_item item = attacking_item;
        LAZYSET(ref attack_modifiers, SILENCE_DEFAULT_MESSAGES, TRUE);
        if (item.damtype == BURN)
        {
            // MODIFY_ATTACK_FORCE_MULTIPLIER(attack_modifiers, 4) (__DEFINES/combat.dm:427)
            if (!islist(attack_modifiers)) { attack_modifiers = Dm.list(); }
            if (!Dm.@in(FORCE_MULTIPLIER, attack_modifiers)) { attack_modifiers[FORCE_MULTIPLIER] = 1; }
            attack_modifiers[FORCE_MULTIPLIER] = Dm.mul(attack_modifiers[FORCE_MULTIPLIER], 4);
        }
        if (Dm.truthy(item.get_sharpness()))
        {
            // MODIFY_ATTACK_FORCE_MULTIPLIER(attack_modifiers, 4)
            if (!islist(attack_modifiers)) { attack_modifiers = Dm.list(); }
            if (!Dm.@in(FORCE_MULTIPLIER, attack_modifiers)) { attack_modifiers[FORCE_MULTIPLIER] = 1; }
            attack_modifiers[FORCE_MULTIPLIER] = Dm.mul(attack_modifiers[FORCE_MULTIPLIER], 4);
        }
        return await base.attacked_by(attacking_item, user, modifiers, attack_modifiers);
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:90
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        switch (damage_type)
        {
            case BRUTE:
                if (Dm.truthy(damage_amount))
                    playsound(this, Dm.resource("sound/items/weapons/slash.ogg"), 50, TRUE);
                else
                    playsound(this, Dm.resource("sound/items/weapons/tap.ogg"), 50, TRUE);
                break;
            case BURN:
                playsound(loc, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
                break;
        }
        return null;
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:100
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable movable = null)
    {
        if (!isliving(movable))
            return;
        foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(mutations))
            Waiting.NoSleep(mutation.on_cross(this, movable as mob));
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:107
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(mutations))
            await mutation.on_hit(this, attacking_item, user, modifiers, attack_modifiers);

        return await base.attackby(attacking_item, user, modifiers, attack_modifiers);
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/modules/events/space_vines/vine_structure.dm:114
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        await user_unbuckle_mob(user as mob_living, user);
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:118
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        // tg passes the paw's user as on_hit's item (a mob where the slot declares obj/item/attacking_item, and no hitter); the slot's
        // C# parameter is an obj_item, so the port passes no item (C# cannot pass a mob there, rules 7.2)
        foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(mutations))
            await mutation.on_hit(this, null);
        await user_unbuckle_mob(user as mob_living, user);
        return null;
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:123
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        await eat(user);
        return null;
    }

    /// Updates the icon as the space vine grows
    // tg: code/modules/events/space_vines/vine_structure.dm:127
    public virtual async DmTask grow()
    {
        if (!Dm.truthy(growth_stage))
        {
            this.icon_state = pick("Med1", "Med2", "Med3") as string;
            growth_stage = 1;
            await set_opacity(light_state);
        }
        else
        {
            this.icon_state = pick("Hvy1", "Hvy2", "Hvy3") as string;
            growth_stage = 2;
        }

        foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(mutations))
            await mutation.on_grow(this);
    }

    /// Buckles mobs trying to pass through it
    // tg: code/modules/events/space_vines/vine_structure.dm:140
    public virtual async DmTask entangle_mob()
    {
        if (Dm.truthy(has_buckled_mobs()) || prob(75))
            return;

        foreach (mob_living victim in Dm.each<mob_living>(this.loc))
        {
            await entangle(victim);
            if (Dm.truthy(has_buckled_mobs()))
                break; //only capture one mob at a time
        }
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:149
    public virtual async DmTask entangle(mob_living victim = null)
    {
        if (victim == null || Dm.truthy(isvineimmune(victim)))
            return;
        foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(mutations))
            await mutation.on_buckle(this, victim);
        if ((victim.stat != DEAD) && (victim.buckled != this) && Dm.truthy(can_tangle)) //not dead and not captured and can tangle
        {
            to_chat(victim, span_userdanger(I18n.T("The vines [] around you!", pick(I18n.T("wind"), I18n.T("tangle"), I18n.T("tighten")))));
            await buckle_mob(victim, force: TRUE);
        }
    }

    /// Finds a target tile to spread to. If checks pass it will spread to it and also proc on_spread on target.
    // tg: code/modules/events/space_vines/vine_structure.dm:159
    public virtual async DmTask spread()
    {
        if (master == null) //If we've lost our controller, something has gone terribly wrong.
            return;

        float direction = Dm.num(pick(GLOB.cardinals));
        turf stepturf = get_step(this, direction);
        if (stepturf == null)
            return;

        if (is_space_or_openspace(stepturf) || !Dm.truthy(await stepturf.Enter(this)))
            return;
        if (ischasm(stepturf) && !HAS_TRAIT(stepturf, TRAIT_CHASM_STOPPED))
            return;
        if (islava(stepturf) && !HAS_TRAIT(stepturf, TRAIT_LAVA_STOPPED))
            return;
        obj_structure_spacevine spot_taken = Dm.locate<obj_structure_spacevine>(stepturf);
        datum_spacevine_mutation_vine_eating eating = Dm.locate<datum_spacevine_mutation_vine_eating>(mutations);
        if (spot_taken != null) //Proceed if there isn't a vine on the target turf, OR we have vine eater AND target vine is from our seed and doesn't.
        {
            if (eating == null)
                return;
            if (Dm.truthy(spot_taken.mutations?.Find(eating)))
                return;
        }
        foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(mutations))
        {
            await mutation.on_spread(this, stepturf);
            stepturf = get_step(this, direction);
        }
        obj_structure_spacevine spawning_vine = await master.spawn_spacevine_piece(stepturf, this) as obj_structure_spacevine;
        if (Dm.truthy(NSCOMPONENT(direction)))
        {
            spawning_vine.pixel_y = direction == NORTH ? -32 : 32;
            Dm.animate(spawning_vine, Dm.named("pixel_y", 0), Dm.named("time", 1 * SECONDS));
        }
        else
        {
            spawning_vine.pixel_x = direction == EAST ? -32 : 32;
            Dm.animate(spawning_vine, Dm.named("pixel_x", 0), Dm.named("time", 1 * SECONDS));
        }
    }

    /// Destroying an explosive vine sets off a chain reaction
    // tg: code/modules/events/space_vines/vine_structure.dm:193
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        object index = null;
        foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(mutations))
            Dm.append(ref index, mutation.on_explosion(severity, target, this));
        if (!Dm.truthy(index) && prob(34 * Dm.num(severity)))
            qdel(this);

        return TRUE;
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:202
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return (Dm.truthy(always_atmos_process) || Dm.num(exposed_temperature) > FIRE_MINIMUM_TEMPERATURE_TO_SPREAD || Dm.num(exposed_temperature) < VINE_FREEZING_POINT || !Dm.truthy(can_spread)) ? TRUE : FALSE; //if you're room temperature you're safe
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:205
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        foreach (datum_spacevine_mutation mutation in Dm.each<datum_spacevine_mutation>(mutations))
            mutation.additional_atmos_processes(this, air);
        if (!Dm.truthy(can_spread) && (Dm.num(exposed_temperature) >= VINE_FREEZING_POINT || Dm.truthy(Dm.bitand(resistance_flags, FREEZE_PROOF))))
            can_spread = TRUE; // not returning here just in case its now a plasmafire and the kudzu should be deleted
        if (Dm.num(exposed_temperature) > FIRE_MINIMUM_TEMPERATURE_TO_SPREAD && !Dm.truthy(Dm.bitand(resistance_flags, FIRE_PROOF)))
            qdel(this);
        else if (Dm.num(exposed_temperature) < VINE_FREEZING_POINT && !Dm.truthy(Dm.bitand(resistance_flags, FREEZE_PROOF)))
            can_spread = FALSE;
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:215
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(isvineimmune(mover)))
            return TRUE;
        return dot;
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:220
    public virtual async DmTask block_sunlight()
    {
        await AddElement(typeof(datum_element_give_turf_traits), string_list(Dm.list(TRAIT_TURF_SUN_BLOCKED)));
    }

    // tg: code/modules/events/space_vines/vine_structure.dm:223
    public virtual void unblock_sunlight()
    {
        RemoveElement(typeof(datum_element_give_turf_traits), string_list(Dm.list(TRAIT_TURF_SUN_BLOCKED)));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/events/space_vines/vine_structure.dm:35  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_structure_spacevine__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_structure_spacevine.on_entered)));
}
