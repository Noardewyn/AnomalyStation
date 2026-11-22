// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/space_vines/vine_mutations.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4b (D327): the whole file, every space vine mutation (light, toxic, explosive, fire proof, cold proof, temperature
// stabilisation, vine eating, aggressive spreading, transparent, the four gas eaters, thorny, hardened, timid, flowering,
// slippery, conductive, radioactive) with its hooks on the vine, the mobs it touches, the turf's air and the venus human trap it
// equips. The mutations are singletons (GLOB.vine_mutations_list) shared by every vine. The var blocks are generated (D111).

// tg: code/modules/events/space_vines/vine_mutations.dm:1
public partial class datum_spacevine_mutation : datum
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:15
    public virtual async DmTask add_mutation_to_vinepiece(obj_structure_spacevine vine = null)
    {
        Dm.combine(ref vine.mutations, this);
        await vine.add_atom_colour(hue, FIXED_COLOUR_PRIORITY);
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:19
    [ShouldCallParent]
    public virtual async DmTask on_buckle(obj_structure_spacevine vine = null, mob_living buckled = null)
    {
        buckled.layer = SPACEVINE_MOB_LAYER;
        RegisterSignal(buckled, COMSIG_MOB_UNBUCKLED, nameof(on_unbuckle), @override: TRUE);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:24
    [ShouldCallParent, SignalHandler]
    public virtual void on_unbuckle(datum source = null)
    {
        if (!isliving(source))
            return;
        mob_living buckled = source as mob_living;
        buckled.layer = Dm.initial(buckled, (mob_living p) => p.layer);
        UnregisterSignal(buckled, COMSIG_MOB_UNBUCKLED);
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:33
    public virtual async DmTask equip_venus_trap(mob_living_basic_venus_human_trap venus_trap = null)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:36
    public virtual void process_mutation(obj_structure_spacevine vine = null)
    {
        return;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:39
    public virtual async DmTask on_birth(obj_structure_spacevine vine = null)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:42
    public virtual async DmTask on_grow(obj_structure_spacevine vine = null)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:45
    public virtual async DmTask on_death(obj_structure_spacevine vine = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:48
    public virtual async DmTask on_hit(obj_structure_spacevine vine = null, obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:51
    public virtual async DmTask on_cross(obj_structure_spacevine vine = null, mob crosser = null)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:54
    public virtual void on_chem(obj_structure_spacevine vine = null, datum_reagent chem = null)
    {
        return;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:57
    public virtual async DmTask on_eat(obj_structure_spacevine vine = null, mob_living eater = null)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:60
    public virtual async DmTask on_spread(obj_structure_spacevine vine = null, turf target = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:63
    public virtual object on_explosion(object severity = null, object target = null, obj_structure_spacevine vine = null)
    {
        return FALSE;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:66
    public virtual void additional_atmos_processes(obj_structure_spacevine vine = null, datum_gas_mixture air = null)
    {
        return;
    }
}

// tg: code/modules/events/space_vines/vine_mutations.dm:72
public partial class datum_spacevine_mutation_light : datum_spacevine_mutation
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:80
    public override async DmTask on_grow(obj_structure_spacevine vine = null)
    {
        if (Dm.truthy(vine.growth_stage))
            await vine.set_light(LIGHT_MUTATION_BRIGHTNESS, 0.3f);
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:84
    public override async DmTask equip_venus_trap(mob_living_basic_venus_human_trap venus_trap = null)
    {
        venus_trap.glow = venus_trap.mob_light(LIGHT_MUTATION_BRIGHTNESS, 0.3f) as obj_effect_dummy_lighting_obj_moblight;
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/events/space_vines/vine_mutations.dm:87
public partial class datum_spacevine_mutation_toxicity : datum_spacevine_mutation
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:96
    public override async DmTask on_cross(obj_structure_spacevine vine = null, mob crosser = null)
    {
        if (Dm.truthy(isvineimmune(crosser)) || issilicon(crosser))
            return;
        if (!prob(TOXICITY_MUTATION_PROB))
            return;

        datum_spacevine_mutation_thorns thorns = Dm.locate<datum_spacevine_mutation_thorns>(vine.mutations);

        if (thorns != null)
        {
            to_chat(crosser, span_alert(I18n.T("You are pricked by thorns and feel a strange sensation.")));
            await ((mob_living)crosser).apply_damage(20, TOX);
            return;
        }

        float body_parts_covered = 0;
        foreach (obj_item_clothing worn_item in Dm.each<obj_item_clothing>(crosser.get_equipped_items()))
        {
            if (!Dm.truthy(Dm.bitand(worn_item.clothing_flags, THICKMATERIAL)))
                continue;
            body_parts_covered = Dm.bitor(body_parts_covered, worn_item.body_parts_covered);

            if (Dm.bitand(body_parts_covered, required_coverage) == required_coverage)
                return;
        }

        to_chat(crosser, span_alert(I18n.T("You accidentally touch the vine and feel a strange sensation.")));
        await ((mob_living)crosser).apply_damage(20, TOX);
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:121
    public override async DmTask on_eat(obj_structure_spacevine vine = null, mob_living eater = null)
    {
        if (!Dm.truthy(isvineimmune(eater)))
            await eater.apply_damage(20, TOX);
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:125
    public override async DmTask equip_venus_trap(mob_living_basic_venus_human_trap venus_trap = null)
    {
        ADD_TRAIT(venus_trap, TRAIT_TOXIMMUNE, INNATE_TRAIT);
        await DmTask.CompletedTask;
    }
}

// JC IT'S A BOMB
// tg: code/modules/events/space_vines/vine_mutations.dm:128
public partial class datum_spacevine_mutation_explosive : datum_spacevine_mutation
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:136
    [DmParams("explosion_severity", "target", "vine")]
    public override object on_explosion(object explosion_severity = null, object target = null, obj_structure_spacevine vine = null)
    {
        if (Dm.num(explosion_severity) >= EXPLODE_DEVASTATE)
        {
            // QDEL_IN(vine, 0.5 SECONDS) (__DEFINES/qdel.dm:59, inline: rules 11.1 (f))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 0.5f * SECONDS > GC_FILTER_QUEUE ? WEAKREF(vine) : (object)vine), 0.5f * SECONDS, file: "code/modules/events/space_vines/vine_mutations.dm", line: 138);
            return TRUE;
        }

        qdel(vine);
        return FALSE;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:144
    [DmParams("vine", "hitter", "item")]
    public override async DmTask on_death(obj_structure_spacevine vine = null, params object[] __extra)
    {
        await explosion(vine, light_impact_range: EXPLOSION_MUTATION_IMPACT_RADIUS, adminlog: FALSE);
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:147
    public override async DmTask equip_venus_trap(mob_living_basic_venus_human_trap venus_trap = null)
    {
        RegisterSignal(venus_trap, COMSIG_LIVING_DEATH, nameof(on_venus_trap_death), @override: TRUE);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:150
    [SignalHandler]
    public virtual void on_venus_trap_death(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        Waiting.NoSleep(explosion(spawned_mob, light_impact_range: EXPLOSION_MUTATION_IMPACT_RADIUS, adminlog: FALSE));
    }
}

// tg: code/modules/events/space_vines/vine_mutations.dm:155
public partial class datum_spacevine_mutation_fire_proof : datum_spacevine_mutation
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:163
    [DmParams("vine", "item", "hitter", "modifiers", "attack_modifiers")]
    public override async DmTask on_hit(obj_structure_spacevine vine = null, obj_item item = null, mob hitter = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (Dm.eq(item?.damtype, BURN))
        {
            // MODIFY_ATTACK_FORCE_MULTIPLIER(attack_modifiers, 0) (__DEFINES/combat.dm:427)
            if (!islist(attack_modifiers)) { attack_modifiers = Dm.list(); }
            if (!Dm.@in(FORCE_MULTIPLIER, attack_modifiers)) { attack_modifiers[FORCE_MULTIPLIER] = 1; }
            attack_modifiers[FORCE_MULTIPLIER] = Dm.mul(attack_modifiers[FORCE_MULTIPLIER], 0);
        }
        await base.on_hit(vine, item, hitter, modifiers, attack_modifiers);
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:168
    public override async DmTask on_grow(obj_structure_spacevine vine = null)
    {
        vine.resistance_flags = Dm.bitor(vine.resistance_flags, FIRE_PROOF);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:171
    public override async DmTask equip_venus_trap(mob_living_basic_venus_human_trap venus_trap = null)
    {
        venus_trap.unsuitable_heat_damage = 0;
        venus_trap.resistance_flags = Dm.bitor(venus_trap.resistance_flags, FIRE_PROOF);
        ADD_TRAIT(venus_trap, TRAIT_RESISTHEAT, INNATE_TRAIT);
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/events/space_vines/vine_mutations.dm:176
public partial class datum_spacevine_mutation_cold_proof : datum_spacevine_mutation
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:184
    public override async DmTask on_grow(obj_structure_spacevine vine = null)
    {
        vine.resistance_flags = Dm.bitor(vine.resistance_flags, FREEZE_PROOF);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:187
    public override async DmTask equip_venus_trap(mob_living_basic_venus_human_trap venus_trap = null)
    {
        venus_trap.unsuitable_cold_damage = 0;
        venus_trap.resistance_flags = Dm.bitor(venus_trap.resistance_flags, FREEZE_PROOF);
        ADD_TRAIT(venus_trap, TRAIT_RESISTCOLD, INNATE_TRAIT);
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/events/space_vines/vine_mutations.dm:192
public partial class datum_spacevine_mutation_temp_stabilisation : datum_spacevine_mutation
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:199
    public override async DmTask add_mutation_to_vinepiece(obj_structure_spacevine vine = null)
    {
        await base.add_mutation_to_vinepiece(vine);
        vine.always_atmos_process = TRUE;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:203
    public override void additional_atmos_processes(obj_structure_spacevine vine = null, datum_gas_mixture air = null)
    {
        float heat_capacity = Dm.num(air.heat_capacity());
        if (!Dm.truthy(heat_capacity)) // No heating up space or vacuums
            return;
        float energy_used = min(abs(air.temperature - T20C) * heat_capacity, TEMP_STABILISATION_MUTATION_MAXIMUM_ENERGY);
        float delta_temperature = Dm.div(energy_used, heat_capacity);
        if (delta_temperature < 0.1f)
            return;
        if (air.temperature > T20C)
            delta_temperature *= -1;
        air.temperature += delta_temperature;
        vine.air_update_turf(FALSE, FALSE);
    }
}

// tg: code/modules/events/space_vines/vine_mutations.dm:216
public partial class datum_spacevine_mutation_vine_eating : datum_spacevine_mutation
{
    /// Destroys any vine on spread-target's tile. The checks for if this should be done are in the spread() proc.
    // tg: code/modules/events/space_vines/vine_mutations.dm:225
    public override async DmTask on_spread(obj_structure_spacevine vine = null, turf target = null, params object[] __extra)
    {
        foreach (obj_structure_spacevine prey in Dm.each<obj_structure_spacevine>(target))
            qdel(prey);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:229
    public override async DmTask equip_venus_trap(mob_living_basic_venus_human_trap venus_trap = null)
    {
        venus_trap.weed_heal = Dm.initial(venus_trap, (mob_living_basic_venus_human_trap p) => p.weed_heal) * 2;
        await DmTask.CompletedTask;
    }
}

//very OP, but im out of other ideas currently
// tg: code/modules/events/space_vines/vine_mutations.dm:232
public partial class datum_spacevine_mutation_aggressive_spread : datum_spacevine_mutation
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:69  (the proc's first definition, replaced at :251 on the same type;
    // nothing calls it: rules 2.5)
    public async DmTask aggrospread_act__d0(obj_structure_spacevine vine = null, mob_living M = null)
    {
        await DmTask.CompletedTask;
    }

    /// Checks mobs on spread-target's turf to see if they should be hit by a damaging proc or not.
    // tg: code/modules/events/space_vines/vine_mutations.dm:241
    [DmParams("vine", "turf", "living")]
    public override async DmTask on_spread(obj_structure_spacevine vine = null, turf turf = null, params object[] __extra)
    {
        foreach (mob_living victim in Dm.each<mob_living>(turf))
            await aggrospread_act(vine, victim);
    }

    /// What happens if an aggr spreading vine buckles a mob.
    // tg: code/modules/events/space_vines/vine_mutations.dm:246
    public override async DmTask on_buckle(obj_structure_spacevine vine = null, mob_living buckled = null)
    {
        await base.on_buckle(vine, buckled);
        await aggrospread_act(vine, buckled);
    }

    /// Hurts mobs. To be used when a vine with aggressive spread mutation spreads into the mob's tile or buckles them.
    // tg: code/modules/events/space_vines/vine_mutations.dm:251
    [DmParams("vine", "living_mob")]
    public virtual async DmTask aggrospread_act(obj_structure_spacevine vine = null, mob_living living_mob = null)
    {
        if (Dm.truthy(isvineimmune(living_mob)) || living_mob.stat == DEAD)
            return;

        if (!iscarbon(living_mob))
        {
            await living_mob.apply_damage(75, BRUTE, blocked: Dm.num(living_mob.run_armor_check(attack_flag: MELEE, silent: TRUE)));
            playsound(living_mob, Dm.resource("sound/items/weapons/whip.ogg"), 50, TRUE, -1);
            living_mob.visible_message(span_danger(I18n.T("[] is brutally threshed by []!", living_mob, vine)),
                span_userdanger(I18n.T("You are brutally threshed by []!", vine)));
            log_combat(vine, living_mob, "aggressively spread into"); //You aren't being attacked by the vines. You just happen to stand in their way.
            return;
        }

        mob_living_carbon victim = living_mob as mob_living_carbon;
        obj_item_bodypart limb = victim.get_bodypart(victim.get_random_valid_zone(even_weights: TRUE) as string) as obj_item_bodypart;
        object armor = victim.run_armor_check(def_zone: limb, attack_flag: MELEE);

        if (!HAS_TRAIT(victim, TRAIT_PIERCEIMMUNE))
        {
            datum_spacevine_mutation_thorns thorn = Dm.locate<datum_spacevine_mutation_thorns>(vine.mutations);

            if (thorn != null && prob(40)) //If we found the thorns mutation there is now a chance to get stung instead of lashed or smashed.
            {
                await victim.apply_damage(50, BRUTE, def_zone: limb, wound_bonus: rand(-20, 10), sharpness: SHARP_POINTY); //This one gets a bit lower damage because it ignores armor.
                await victim.Stun(1 * SECONDS); //Stopped in place for a moment.
                playsound(living_mob, Dm.resource("sound/items/weapons/pierce.ogg"), 50, TRUE, -1);
                living_mob.visible_message(span_danger(I18n.T("[] is nailed by a sharp thorn!", living_mob)),
                    span_userdanger(I18n.T("You are nailed by a sharp thorn!")));
                log_combat(vine, living_mob, "aggressively pierced"); //"Aggressively" for easy ctrl+F'ing in the attack logs.
                return;
            }

            if (prob(80))
            {
                await victim.apply_damage(60, BRUTE, def_zone: limb, blocked: Dm.num(armor), wound_bonus: rand(-20, 10), sharpness: SHARP_EDGED);
                await victim.Knockdown(2 * SECONDS);
                playsound(victim, Dm.resource("sound/items/weapons/whip.ogg"), 50, TRUE, -1);
                living_mob.visible_message(span_danger(I18n.T("[] is lacerated by an outburst of vines!", living_mob)),
                    span_userdanger(I18n.T("You are lacerated by an outburst of vines!")));
                log_combat(vine, living_mob, "aggressively lacerated");
                return;
            }
        }

        await victim.apply_damage(60, BRUTE, def_zone: limb, blocked: Dm.num(armor), wound_bonus: rand(-20, 10), sharpness: NONE);
        await victim.Knockdown(3 * SECONDS);
        atom throw_target = get_edge_target_turf(living_mob, get_dir(vine, get_step_away(living_mob, vine))) as atom;
        await victim.throw_at(throw_target, 3, 6);
        playsound(victim, Dm.resource("sound/effects/hit_kick.ogg"), 50, TRUE, -1);
        living_mob.visible_message(span_danger(I18n.T("[] is smashed by a large vine!", living_mob)),
            span_userdanger(I18n.T("You are smashed by a large vine!")));
        log_combat(vine, living_mob, "aggressively smashed");
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:297
    public override async DmTask equip_venus_trap(mob_living_basic_venus_human_trap venus_trap = null)
    {
        venus_trap.kudzu_off_distance_range = 2;
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/events/space_vines/vine_mutations.dm:300
public partial class datum_spacevine_mutation_transparency : datum_spacevine_mutation
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:308
    public override async DmTask on_birth(obj_structure_spacevine vine = null)
    {
        vine.light_state = PASS_LIGHT;
        vine.alpha = 125;
        vine.unblock_sunlight();
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:313
    public override async DmTask equip_venus_trap(mob_living_basic_venus_human_trap venus_trap = null)
    {
        venus_trap.alpha = Dm.initial(venus_trap, (mob_living_basic_venus_human_trap p) => p.alpha) * 0.5f;
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/events/space_vines/vine_mutations.dm:316
public partial class datum_spacevine_mutation_gas_eater : datum_spacevine_mutation
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:321
    public override void process_mutation(obj_structure_spacevine vine = null)
    {
        if (gas_type == null)
        {
            stack_trace(Dm.text("gas_type not set for gas_eater mutation []", type), "code/modules/events/space_vines/vine_mutations.dm", 323);
            return;
        }

        turf_open_floor turf = vine.loc as turf_open_floor;
        if (turf == null)
            return;

        datum_gas_mixture gas_mix = turf.air;
        if (!Dm.truthy(gas_mix.moles[gas_type]))
            return;

        gas_mix.set_gas(gas_type, max(Dm.num(gas_mix.moles[gas_type]) - GAS_MUTATION_REMOVAL_MULTIPLIER * vine.growth_stage, 0));
        gas_mix.garbage_collect();
    }
}

// tg: code/modules/events/space_vines/vine_mutations.dm:369
public partial class datum_spacevine_mutation_thorns : datum_spacevine_mutation
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:377
    public override async DmTask on_cross(obj_structure_spacevine vine = null, mob crosser = null)
    {
        if (Dm.truthy(isvineimmune(crosser)) || HAS_TRAIT(crosser, TRAIT_PIERCEIMMUNE))
            return;
        if (prob(THORN_MUTATION_CUT_PROB))
        {
            mob_living victim = crosser as mob_living;
            if (Dm.truthy(await victim.apply_damage(15, BRUTE, blocked: Dm.num(victim.run_armor_check(attack_flag: MELEE, silent: TRUE)), spread_damage: TRUE)))
                to_chat(victim, span_danger(I18n.T("You cut yourself on the thorny vines.")));
        }
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:385
    [DmParams("vine", "item", "hitter", "modifiers", "attack_modifiers")]
    public override async DmTask on_hit(obj_structure_spacevine vine = null, obj_item item = null, mob hitter = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (Dm.truthy(isvineimmune(hitter)) || HAS_TRAIT(hitter, TRAIT_PIERCEIMMUNE) || HAS_TRAIT(hitter, TRAIT_PLANT_SAFE))
            return;

        if (iscarbon(hitter))
        {
            mob_living_carbon carbon_victim = hitter as mob_living_carbon;
            foreach (obj_item_clothing worn_item in Dm.each<obj_item_clothing>(carbon_victim.get_equipped_items()))
            {
                if (Dm.truthy(Dm.bitand(worn_item.body_parts_covered, HANDS)) && Dm.truthy(Dm.bitand(worn_item.clothing_flags, THICKMATERIAL)))
                    return;
            }
        }

        if (prob(THORN_MUTATION_CUT_PROB))
        {
            mob_living victim = hitter as mob_living;
            if (Dm.truthy(await victim.apply_damage(15, BRUTE, blocked: Dm.num(victim.run_armor_check(attack_flag: MELEE, silent: TRUE)), spread_damage: TRUE)))
                to_chat(victim, span_danger(I18n.T("You cut yourself on the thorny vines.")));
        }
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:400
    public override async DmTask equip_venus_trap(mob_living_basic_venus_human_trap venus_trap = null)
    {
        venus_trap.melee_damage_lower = Dm.initial(venus_trap, (mob_living_basic_venus_human_trap p) => p.melee_damage_lower) * 0.5f;
        venus_trap.melee_damage_upper = Dm.initial(venus_trap, (mob_living_basic_venus_human_trap p) => p.melee_damage_upper) * 0.5f;
        venus_trap.obj_damage = Dm.initial(venus_trap, (mob_living_basic_venus_human_trap p) => p.obj_damage) * 0.5f;
        venus_trap.wound_bonus = 5;
        venus_trap.exposed_wound_bonus = 15;
        venus_trap.sharpness = SHARP_POINTY;
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/events/space_vines/vine_mutations.dm:408
public partial class datum_spacevine_mutation_hardened : datum_spacevine_mutation
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:416
    public override async DmTask on_grow(obj_structure_spacevine vine = null)
    {
        if (Dm.truthy(vine.growth_stage))
            vine.set_density(TRUE);
        await vine.modify_max_integrity(Dm.initial(vine, (obj_structure_spacevine p) => p.max_integrity) * 2);
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:421
    [DmParams("vine", "item", "hitter", "modifiers", "attack_modifiers")]
    public override async DmTask on_hit(obj_structure_spacevine vine = null, obj_item item = null, mob hitter = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (Dm.truthy(item?.get_sharpness()))
        {
            // MODIFY_ATTACK_FORCE_MULTIPLIER(attack_modifiers, 0.5) (__DEFINES/combat.dm:427)
            if (!islist(attack_modifiers)) { attack_modifiers = Dm.list(); }
            if (!Dm.@in(FORCE_MULTIPLIER, attack_modifiers)) { attack_modifiers[FORCE_MULTIPLIER] = 1; }
            attack_modifiers[FORCE_MULTIPLIER] = Dm.mul(attack_modifiers[FORCE_MULTIPLIER], 0.5f);
        }
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:425
    public override async DmTask equip_venus_trap(mob_living_basic_venus_human_trap venus_trap = null)
    {
        venus_trap.health = Dm.initial(venus_trap, (mob_living_basic_venus_human_trap p) => p.health) * 2;
        venus_trap.maxHealth = Dm.initial(venus_trap, (mob_living_basic_venus_human_trap p) => p.maxHealth) * 2;
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/events/space_vines/vine_mutations.dm:429
public partial class datum_spacevine_mutation_timid : datum_spacevine_mutation
{
    //This specific mutation only covers floors instead of structures, items, mobs and cant tangle mobs
    // tg: code/modules/events/space_vines/vine_mutations.dm:438
    public override async DmTask on_birth(obj_structure_spacevine vine = null)
    {
        SET_PLANE_IMPLICIT(vine, FLOOR_PLANE);
        vine.layer = ABOVE_OPEN_TURF_LAYER;
        vine.light_state = PASS_LIGHT;
        vine.can_tangle = FALSE;
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:444
    public override async DmTask equip_venus_trap(mob_living_basic_venus_human_trap venus_trap = null)
    {
        datum_action_cooldown_mob_cooldown_projectile_attack_vine_tangle tangle_action = null;
        tangle_action = Dm.locate<datum_action_cooldown_mob_cooldown_projectile_attack_vine_tangle>(venus_trap.actions);
        await tangle_action.Remove(venus_trap);
    }
}

// tg: code/modules/events/space_vines/vine_mutations.dm:449
public partial class datum_spacevine_mutation_flowering : datum_spacevine_mutation
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:456
    public override async DmTask on_grow(obj_structure_spacevine vine = null)
    {
        if (Dm.locate<obj_structure_alien_resin_flower_bud>(Builtins.range(5, vine)) != null)
            return;
        if (vine.growth_stage == 2 && prob(FLOWERING_MUTATION_SPAWN_PROB))
        {
            obj_structure_alien_resin_flower_bud spawned_flower_bud = Dm.@new<obj_structure_alien_resin_flower_bud>(vine.loc);
            spawned_flower_bud.mutations = vine.mutations;
        }
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:463
    public override async DmTask on_cross(obj_structure_spacevine vine = null, mob crosser = null)
    {
        if (prob(25))
            await vine.entangle(crosser as mob_living);
    }
}

// tg: code/modules/events/space_vines/vine_mutations.dm:467
public partial class datum_spacevine_mutation_slippery : datum_spacevine_mutation
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:474
    public override async DmTask on_grow(obj_structure_spacevine vine = null)
    {
        await vine.AddComponent(typeof(datum_component_slippery), 5 * SECONDS, Dm.bitor(NO_SLIP_WHEN_WALKING, SLIDE));
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:477
    [DmParams("vine", "item", "hitter", "modifiers", "attack_modifiers")]
    public override async DmTask on_hit(obj_structure_spacevine vine = null, obj_item item = null, mob hitter = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (Dm.truthy(isvineimmune(hitter)) || HAS_TRAIT(hitter, TRAIT_PLANT_SAFE))
            return;

        if (prob(20) && item != null)
            await item.AddComponent(typeof(datum_component_slippery_item), Dm.named("fall_chance", 25), Dm.named("fall_catch_chance", 25), Dm.named("duration", 5 * SECONDS));
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:484
    public override async DmTask equip_venus_trap(mob_living_basic_venus_human_trap venus_trap = null)
    {
        RegisterSignal(venus_trap, COMSIG_ATOM_ATTACKBY, nameof(on_venus_hit), @override: TRUE);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:487
    [SignalHandler]
    public virtual void on_venus_hit(mob_living_basic_venus_human_trap venus_trap = null, obj_item attacking_item = null, mob_living attacker = null, object @params = null)
    {
        if (Dm.truthy(isvineimmune(attacker)) || HAS_TRAIT(attacker, TRAIT_PLANT_SAFE))
            return;

        if (prob(20) && attacking_item != null)
            Waiting.NoSleep(attacking_item.AddComponent(typeof(datum_component_slippery_item), Dm.named("fall_chance", 25), Dm.named("fall_catch_chance", 25), Dm.named("duration", 5 * SECONDS)));
    }
}

// tg: code/modules/events/space_vines/vine_mutations.dm:496
public partial class datum_spacevine_mutation_conductive : datum_spacevine_mutation
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:503
    public virtual async DmTask attempt_shock(obj_structure_spacevine vine = null, mob_living victim = null, obj_item attacking_item = null)
    {
        if (Dm.truthy(isvineimmune(victim)) || HAS_TRAIT(victim, TRAIT_PLANT_SAFE))
            return;
        if (attacking_item != null && !Dm.truthy(Dm.bitand(attacking_item.obj_flags, CONDUCTS_ELECTRICITY)))
            return;

        turf vine_loc = get_turf(vine);
        if (Dm.truthy(vine_loc.overfloor_placed))
            return;
        obj_structure_cable cable = vine_loc.get_cable_node() as obj_structure_cable;
        if (cable == null)
            return;

        await vine.shock__proc(victim, 100, cable);
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:518
    [DmParams("vine", "item", "hitter", "modifiers", "attack_modifiers")]
    public override async DmTask on_hit(obj_structure_spacevine vine = null, obj_item item = null, mob hitter = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        await attempt_shock(vine, hitter as mob_living, item);
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:521
    public override async DmTask on_buckle(obj_structure_spacevine vine = null, mob_living buckled = null)
    {
        await base.on_buckle(vine, buckled);
        await attempt_shock(vine, buckled);
    }
}

// tg: code/modules/events/space_vines/vine_mutations.dm:525
public partial class datum_spacevine_mutation_radioactive : datum_spacevine_mutation
{
    // tg: code/modules/events/space_vines/vine_mutations.dm:532
    public override async DmTask on_birth(obj_structure_spacevine vine = null)
    {
        await vine.AddElement(typeof(datum_element_radioactive), Dm.named("chance", (float)DEFAULT_RADIATION_CHANCE / 3));
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:535
    public override async DmTask equip_venus_trap(mob_living_basic_venus_human_trap venus_trap = null)
    {
        RegisterSignal(venus_trap, COMSIG_ATOM_ATTACKBY, nameof(on_venus_hit), @override: TRUE);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/space_vines/vine_mutations.dm:538
    [SignalHandler]
    public virtual void on_venus_hit(mob_living_basic_venus_human_trap venus_trap = null, obj_item attacking_item = null, mob_living attacker = null, object @params = null)
    {
        if (Dm.truthy(isvineimmune(attacker)) || HAS_TRAIT(attacker, TRAIT_PLANT_SAFE))
            return;
        if (!Dm.truthy(SSradiation.can_irradiate_basic(attacker)))
            return;
        if (ishuman(attacker) && Dm.truthy(SSradiation.wearing_rad_protected_clothing(attacker as mob_living_carbon_human)))
            return;

        radiation_pulse(
            venus_trap,
            max_range: 1,
            threshold: RAD_VERY_LIGHT_INSULATION,
            chance: (float)DEFAULT_RADIATION_CHANCE / 3,
            minimum_exposure_time: 3 * SECONDS
        );
    }
}
