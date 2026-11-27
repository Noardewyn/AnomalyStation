// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the firefighting foam's exposure (M4b, the advanced fire extinguisher; its foam with gaps F1), phlogiston's exposure
// and life (M8 slice C), the smoke powder (gaps F1), the tray effects (gaps P2) and gaps RG3 (D339) the rest: thermite on a wall
// (datums/components/thermite.dm), the explosives' sparks (nitroglycerin, gunpowder, RDX, TaTP), clf3 burning through floors, the
// vortexes of sorium and liquid dark matter, the flash and sonic powders, napalm and phlogiston lit, cryostylane and pyrosium
// heating and cooling a container and a body, teslium's shocks and its zap, and teslium fed to a fish (it turns
// electrogenic for a while). The var blocks are generated (D111).

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:2
public partial class datum_reagent_thermite : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:10
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (Dm.num(reac_volume) >= 1)
            await exposed_turf.AddComponent(typeof(datum_component_thermite), reac_volume);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:15
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.adjust_fire_loss(0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:20
public partial class datum_reagent_nitroglycerin : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:29
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        if (Dm.truthy(await mob.adjust_organ_loss(ORGAN_SLOT_HEART, -0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick) * Dm.num(normalise_creation_purity()), required_organ_flag: affected_organ_flags)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:34
    public override async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        await reagent_explode(holder, volume, strengthdiv: 2, clear_holder_reagents: FALSE, flame_factor: 1);
        return Dm.bitor(SPARK_ACT_DESTRUCTIVE, SPARK_ACT_CLEAR_ALL);
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:38
public partial class datum_reagent_stabilizing_agent : datum_reagent
{
    //It has stable IN THE NAME. IT WAS MADE FOR THIS MOMENT.
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:47  (gaps P2, D298)
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        mytray.myseed?.adjust_instability(-Dm.round(Dm.num(volume)));
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:50
public partial class datum_reagent_clf3 : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:60
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.adjust_fire_stacks(0.1f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        if (Dm.truthy(await mob.adjust_fire_loss(0.015f * max(Dm.num(mob.fire_stacks), 1) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:66
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (isplatingturf(exposed_turf))
        {
            var target_plating = (turf_open_floor_plating)exposed_turf;
            if (prob(10 + Dm.num(target_plating.burnt) + 5 * Dm.num(target_plating.broken))) //broken or burnt plating is more susceptible to being destroyed
                await target_plating.ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
        }
        if (isfloorturf(exposed_turf) && prob(Dm.num(reac_volume)))
        {
            var target_floor = (turf_open_floor)exposed_turf;
            await target_floor.make_plating();
        }
        else if (prob(Dm.num(reac_volume)))
            exposed_turf.burn_tile();
        if (isfloorturf(exposed_turf))
        {
            foreach (turf nearby_turf in Dm.each_cast<turf>(RANGE_TURFS(1, exposed_turf)))
            {
                if (Dm.locate<obj_effect_hotspot>(nearby_turf) == null)
                    Dm.@new<obj_effect_hotspot>(nearby_turf);
            }
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:82
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        await exposed_mob.adjust_fire_stacks(min(Dm.num(reac_volume) / 5, 10));
        exposed_mob.ignite_mob();
        if (Dm.locate<obj_effect_hotspot>(exposed_mob.loc) == null)
            Dm.@new<obj_effect_hotspot>(exposed_mob.loc);
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:89
public partial class datum_reagent_sorium : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:97
    public override async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        float range = clamp(sqrt(Dm.num(volume)), 1, 6);
        await goonchem_vortex(get_turf(holder.my_atom), 1, range);
        return SPARK_ACT_NON_DESTRUCTIVE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:102
public partial class datum_reagent_liquid_dark_matter : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:110
    public override async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        float range = clamp(sqrt(Dm.num(volume) / 2), 1, 6);
        await goonchem_vortex(get_turf(holder.my_atom), 0, range);
        return SPARK_ACT_NON_DESTRUCTIVE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:115
public partial class datum_reagent_gunpowder : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:124
    public override async DmTask on_new(object data = null)
    {
        await base.on_new(data);
        if (holder?.my_atom != null)
            RegisterSignal(holder.my_atom, COMSIG_ATOM_EX_ACT, nameof(on_ex_act));
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:129
    public override object Destroy(float? force = null)
    {
        if (holder?.my_atom != null)
            UnregisterSignal(holder.my_atom, COMSIG_ATOM_EX_ACT);
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:134
    [SignalHandler]
    public virtual void on_ex_act(atom source = null, object severity = null, object target = null)
    {
        if (Dm.truthy(Dm.bitand(source.flags_1, PREVENT_CONTENTS_EXPLOSION_1)))
            return;
        turf location = get_turf(holder.my_atom);
        var expl = Dm.@new<datum_effect_system_reagents_explosion>(location, 1 + round(Dm.num(volume) / 6, 1), null, null, FALSE);
        Waiting.NoSleep(expl.start(holder.my_atom));
        Waiting.NoSleep(holder.clear_reagents());
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:143
    public override async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        power_charge ??= 0;
        spark_flags ??= NONE;
        // Gunpowder doesn't blow in presence of stabilizing agent but instead consumes it every time it'd get triggered
        float agent_volume = Dm.num(holder.get_reagent_amount(typeof(datum_reagent_stabilizing_agent)));
        if (Dm.truthy(agent_volume))
        {
            float sapped_amt = 0.1f + round(power_charge.Value / (0.1f * STANDARD_CELL_CHARGE), 0.01f);
            await holder.remove_reagent(typeof(datum_reagent_stabilizing_agent), sapped_amt);
            if (agent_volume > sapped_amt)
                return null;
        }

        if (power_charge >= STANDARD_CELL_CHARGE || holder.chem_temp >= 474)
        {
            await reagent_explode(holder, volume, modifier: 5, strengthdiv: 10, clear_holder_reagents: FALSE, flame_factor: 1);
            return Dm.bitor(SPARK_ACT_DESTRUCTIVE, SPARK_ACT_CLEAR_ALL);
        }

        holder.my_atom.visible_message(span_boldnotice(I18n.T("Sparks start flying around the gunpowder!")));
        if (!Dm.truthy(Dm.bitand(spark_flags, SPARK_ACT_ENCLOSED)))
            do_sparks(2, TRUE, get_turf(holder.my_atom));
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.reagent_explode), holder, volume, 5, 10), rand(5 * SECONDS, 10 * SECONDS), file: "code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm", line: 161);
        return SPARK_ACT_NON_DESTRUCTIVE; // just wait a bit...
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:162
public partial class datum_reagent_rdx : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:170
    public override async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        power_charge ??= 0;
        if (Dm.truthy(power_charge))
        {
            // Okay but what if we made a REALLY big boom?
            float power_coeff = Dm.log(2, power_charge.Value / STANDARD_CELL_CHARGE);
            await reagent_explode(holder, volume, modifier: min(2 * power_coeff, 8), strengthdiv: 7 / min(power_coeff, 4), clear_holder_reagents: FALSE, flame_factor: 1);
        }
        else if (holder.chem_temp >= 474)
            await reagent_explode(holder, volume, modifier: 2, strengthdiv: 7, clear_holder_reagents: FALSE, flame_factor: 1);
        else
            await reagent_explode(holder, volume, strengthdiv: 8, clear_holder_reagents: FALSE, flame_factor: 1);
        return Dm.bitor(SPARK_ACT_DESTRUCTIVE, SPARK_ACT_CLEAR_ALL);
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:181
public partial class datum_reagent_tatp : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:189
    public override async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        await reagent_explode(holder, volume, strengthdiv: 1.5f + Dm.rand() * 1.5f, clear_holder_reagents: FALSE, flame_factor: 1);
        return Dm.bitor(SPARK_ACT_DESTRUCTIVE, SPARK_ACT_CLEAR_ALL);
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:193
public partial class datum_reagent_flash_powder : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:201
    public override async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        spark_flags ??= NONE;
        // Even weaker version of the normal flash effect
        turf location = get_turf(holder.my_atom);
        if (!Dm.truthy(Dm.bitand(spark_flags, SPARK_ACT_ENCLOSED)))
            do_sparks(2, TRUE, location);
        else if (!Dm.truthy(Dm.bitand(holder.flags, TRANSPARENT)) || (!isturf(holder.my_atom.loc) && !ismob(holder.my_atom.loc))) // nope
            return SPARK_ACT_NON_DESTRUCTIVE;

        float range = round(Dm.num(volume) / 15, 1);
        holder.my_atom.flash_lighting_fx(range: (range + 2));
        foreach (mob_living victim in Dm.each<mob_living>(get_hearers_in_view(range, location)))
        {
            if (!Dm.truthy(await victim.flash_act(affect_silicon: TRUE)))
                continue;
            float distance = get_dist(location, victim);
            if (distance <= 4 || issilicon(victim))
            {
                await victim.Paralyze(max(2 * SECONDS / max(1, distance), 0.5f * SECONDS));
                await victim.Knockdown(max(20 * SECONDS / max(1, distance), 6 * SECONDS));
            }
            else
                await victim.adjust_dizzy_up_to(max(20 * SECONDS / max(1, distance), 0.5f * SECONDS), 20 * SECONDS);
            await victim.dropItemToGround(victim.get_active_held_item() as obj_item);
            await victim.dropItemToGround(victim.get_inactive_held_item() as obj_item);
        }
        return SPARK_ACT_NON_DESTRUCTIVE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:224  (gaps F1)
public partial class datum_reagent_smoke_powder : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:232
    public override async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        // Can't really make a cloud of smoke if we're inside of an enclosed container
        // ...unless we're a mob, in which case this is pretty cursed
        if (Dm.truthy(Dm.bitand(spark_flags, SPARK_ACT_ENCLOSED)) && !ismob(holder.my_atom))
            return null;
        turf location = get_turf(holder.my_atom);
        playsound(location, Dm.resource("sound/effects/smoke.ogg"), 50, TRUE, -3);
        if (iscarbon(holder.my_atom))
        {
            mob_living_carbon victim = holder.my_atom as mob_living_carbon;
            if (victim.stat != DEAD)
                victim.visible_message(span_warning(Dm.text("[] starts violently coughing up smoke!", victim)));
            await victim.adjust_organ_loss(ORGAN_SLOT_LUNGS, volume / 15);
        }
        await do_chem_smoke(amount: volume / 1.5f, holder: holder.my_atom, location: location, carry: holder, silent: FALSE, log: TRUE);
        return Dm.bitor(SPARK_ACT_NON_DESTRUCTIVE, SPARK_ACT_CLEAR_ALL);
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:247
public partial class datum_reagent_sonic_powder : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:255
    public override async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        // Even weaker version of the normal flash effect
        turf location = get_turf(holder.my_atom);
        float range = round(Dm.num(volume) / 15, 1);
        foreach (mob_living victim in Dm.each<mob_living>(get_hearers_in_view(range, location)))
            await victim.soundbang_act(1, 10 * SECONDS, rand(0, 0.5f * SECONDS));
        return SPARK_ACT_NON_DESTRUCTIVE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:263  (M8 slice C: tg's reagent_mob_expose test feeds it)
[DmPath("/datum/reagent/phlogiston")]
public partial class datum_reagent_phlogiston : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:272
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        touch_protection ??= 0;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        await exposed_mob.adjust_fire_stacks(1);
        float burndmg = max(0.3f * Dm.num(exposed_mob.fire_stacks) * (1 - touch_protection.Value), 0.3f);
        if (Dm.truthy(burndmg))
            await exposed_mob.adjust_fire_loss(burndmg, 0);
        exposed_mob.ignite_mob();
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:280
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var metabolizer = (mob_living_carbon)affected_mob;
        await metabolizer.adjust_fire_stacks(0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        if (Dm.truthy(await metabolizer.adjust_fire_loss(0.15f * max(Dm.num(metabolizer.fire_stacks), 0.15f) * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick), updating_health: FALSE)))
            return UPDATE_MOB_HEALTH;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:286
    public override async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        spark_flags ??= NONE;
        if (Dm.truthy(Dm.bitand(spark_flags, SPARK_ACT_ENCLOSED)) && !ismob(holder.my_atom))
        {
            if (!Dm.truthy(holder.my_atom.uses_integrity))
                return null;
            // Spicy!
            await holder.my_atom.take_damage(Dm.num(volume) / 3, BURN, FIRE, FALSE);
            return Dm.bitor(SPARK_ACT_NON_DESTRUCTIVE, SPARK_ACT_KEEP_REAGENT);
        }

        turf our_turf = get_turf(holder.my_atom);
        our_turf?.hotspot_expose(holder.chem_temp * 50, volume);
        return SPARK_ACT_NON_DESTRUCTIVE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:298
public partial class datum_reagent_napalm : datum_reagent
{
    // why, just why
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:309  (gaps P2, D298)
    public override async DmTask on_hydroponics_apply(obj_machinery_hydroponics mytray = null, mob user = null)
    {
        if (!Dm.truthy(Dm.bitand(mytray.myseed?.resistance_flags ?? 0, FIRE_PROOF)))
        {
            mytray.adjust_plant_health(-Dm.round(Dm.num(volume) * 6));
            mytray.adjust_toxic(Dm.round(Dm.num(volume) * 7));
        }
        mytray.adjust_weedlevel(-rand(5, 9)); //At least give them a small reward if they bother.
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:316
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        await mob.adjust_fire_stacks(0.5f * Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:320
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (istype(exposed_mob, typeof(mob_living)) && Dm.truthy(Dm.bitand(methods, Dm.bitor(Dm.bitor(TOUCH, VAPOR), PATCH))))
            await exposed_mob.adjust_fire_stacks(min(Dm.num(reac_volume) / 4, 20));
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:325
    public override async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        power_charge ??= 0;
        spark_flags ??= NONE;
        if (Dm.truthy(Dm.bitand(spark_flags, SPARK_ACT_ENCLOSED)) && !ismob(holder.my_atom))
        {
            if (!Dm.truthy(holder.my_atom.uses_integrity))
                return null;
            // Spicy!
            await holder.my_atom.take_damage(Dm.num(volume) / 5, BURN, FIRE, FALSE);
            return Dm.bitor(SPARK_ACT_NON_DESTRUCTIVE, SPARK_ACT_KEEP_REAGENT);
        }

        turf our_turf = get_turf(holder.my_atom);
        our_turf?.hotspot_expose(round(holder.chem_temp * 30 * (1 + sqrt(power_charge.Value / STANDARD_CELL_CHARGE)), 1), volume);
        return SPARK_ACT_NON_DESTRUCTIVE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:341
[DmPath("/datum/reagent/cryostylane")]
public partial class datum_reagent_cryostylane : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:338  #define CRYO_SPEED_PREFACTOR 0.4 (file-local, #undef at :406)
    private const float CRYO_SPEED_PREFACTOR = 0.4f;
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:339  #define CRYO_SPEED_CONSTANT 0.1 (file-local, #undef at :407)
    private const float CRYO_SPEED_CONSTANT = 0.1f;

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:355
    public override async DmTask burn(datum_reagents holder = null)
    {
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_oxygen))))
        {
            burning_temperature = 0; //king chilly
            return;
        }
        burning_temperature = null;
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:361
    public override async DmTask on_mob_add(mob_living affected_mob = null, object amount = null)
    {
        await base.on_mob_add(affected_mob, amount);
        // Between a 1.1x and a 1.5x to surgery time depending on purity
        affected_mob.add_surgery_speed_mod(type, 1 + ((CRYO_SPEED_PREFACTOR * (1 - creation_purity)) + CRYO_SPEED_CONSTANT), min(Dm.num(amount) * 1 * MINUTES, 5 * MINUTES));
        affected_mob.color = COLOR_CYAN;
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:367
    public override async DmTask on_mob_delete(mob_living affected_mob = null)
    {
        await base.on_mob_delete(affected_mob);
        affected_mob.remove_surgery_speed_mod(type);
        affected_mob.color = COLOR_WHITE;
    }

    //Pauses decay! Does do something, I promise.
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:373
    public override async DmTask<object> on_mob_dead(mob_living_carbon affected_mob = null, object seconds_per_tick = null, params object[] __extra)
    {
        object dot = await base.on_mob_dead(affected_mob, seconds_per_tick, __extra);
        metabolization_rate = 0.125f * REAGENTS_METABOLISM; //slower consumption when dead
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:377
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        metabolization_rate = 0.625f * REAGENTS_METABOLISM; //faster consumption when alive
        datum_reagents mob_reagents = (datum_reagents)mob.reagents;
        if (Dm.truthy(mob_reagents.has_reagent(typeof(datum_reagent_oxygen))))
        {
            await mob_reagents.remove_reagent(typeof(datum_reagent_oxygen), 1 * k);
            mob.adjust_bodytemperature(-30 * k);
            if (ishuman(mob))
            {
                var humi = (mob_living_carbon_human)mob;
                humi.adjust_coretemperature(-30 * k);
            }
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:387
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (Dm.num(reac_volume) < 5)
            return dot;
        foreach (mob_living_basic_slime exposed_slime in Dm.each<mob_living_basic_slime>(exposed_turf))
            await exposed_slime.adjust_tox_loss(rand(15, 30));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:409
public partial class datum_reagent_pyrosium : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:409
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_oxygen))))
        {
            await holder.remove_reagent(typeof(datum_reagent_oxygen), 0.5f * k);
            mob.adjust_bodytemperature(15 * k);
            if (ishuman(mob))
            {
                var affected_human = (mob_living_carbon_human)mob;
                affected_human.adjust_coretemperature(15 * k);
            }
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:418
    public override async DmTask burn(datum_reagents holder = null)
    {
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_oxygen))))
        {
            burning_temperature = 3500;
            return;
        }
        burning_temperature = null;
    }
}

//Teslium. Causes periodic shocks, and makes shocks against the target much more effective.
// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:424
public partial class datum_reagent_teslium : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:435
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        object dot = await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        var mob = (mob_living_carbon)affected_mob;
        shock_timer++;
        if (shock_timer >= rand(5, 30)) //Random shocks are wildly unpredictable
        {
            shock_timer = 0;
            await mob.electrocute_act(rand(5, 20), "Teslium in their body", 1, SHOCK_NOGLOVES); //SHOCK_NOGLOVES because it's caused from INSIDE of you
            playsound(mob, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:451
    public override async DmTask on_mob_metabolize(mob_living affected_mob = null, params object[] __extra)
    {
        await base.on_mob_metabolize(affected_mob, __extra);
        MODIFY_PHYSIOLOGY(affected_mob, PHYS_COEFF_ELEC_CONDUCTIVITY, 2);
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:455
    public override async DmTask on_mob_end_metabolize(mob_living affected_mob = null, object metabolization_ratio = null)
    {
        await base.on_mob_end_metabolize(affected_mob, metabolization_ratio);
        MODIFY_PHYSIOLOGY(affected_mob, PHYS_COEFF_ELEC_CONDUCTIVITY, 0.5f);
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:459
    public override async DmTask<object> on_spark_act(float? power_charge = null, float? spark_flags = null)
    {
        power_charge ??= 0;
        await tesla_zap(source: holder.my_atom, zap_range: round(Dm.num(volume) / 5, 1), power: Dm.num(volume) * 20 + power_charge.Value, cutoff: 1 * KILO * JOULES, zap_flags: Dm.bitor(Dm.bitor(Dm.bitor(ZAP_MOB_DAMAGE, ZAP_OBJ_DAMAGE), ZAP_MOB_STUN), ZAP_LOW_POWER_GEN));
        playsound(holder.my_atom, Dm.resource("sound/machines/defib/defib_zap.ogg"), 50, TRUE);
        return SPARK_ACT_NON_DESTRUCTIVE;
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:443
    public override async DmTask<object> used_on_fish(obj_item_fish fish = null)
    {
        await DmTask.CompletedTask;
        if (HAS_TRAIT_FROM(fish, TRAIT_FISH_ELECTROGENESIS, FISH_TRAIT_DATUM))
            return FALSE;
        fish.add_traits(Dm.list(TRAIT_FISH_ON_TESLIUM, TRAIT_FISH_ELECTROGENESIS), type);
        _addtimer(TRAIT_CALLBACK_REMOVE(fish, TRAIT_FISH_ON_TESLIUM, type), fish.feeding_frequency * 0.75f, TIMER_UNIQUE | TIMER_OVERRIDE, file: "code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm", line: 447);
        _addtimer(TRAIT_CALLBACK_REMOVE(fish, TRAIT_FISH_ELECTROGENESIS, type), fish.feeding_frequency * 0.75f, TIMER_UNIQUE | TIMER_OVERRIDE, file: "code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm", line: 448);
        return TRUE;
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:464
public partial class datum_reagent_teslium_energized_jelly : datum_reagent_teslium
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:472
    public override async DmTask<object> on_mob_life(object affected_mob = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        var mob = (mob_living_carbon)affected_mob;
        if (!Dm.truthy(Dm.bitand(mob.mob_biotypes, MOB_SLIME))) //everyone but jellypeople get shocked as normal.
            return await base.on_mob_life(affected_mob, seconds_per_tick, metabolization_ratio);
        object dot = null;
        float k = Dm.num(metabolization_ratio) * Dm.num(seconds_per_tick);
        await mob.AdjustAllImmobility(-20 * k);
        if (Dm.truthy(await mob.adjust_stamina_loss(-5 * k, updating_stamina: FALSE)))
            dot = UPDATE_MOB_HEALTH;
        if (Dm.truthy(is_species(mob, typeof(datum_species_jelly_luminescent))))
        {
            // GAP(XB1-2): the luminescent jellyperson's extract cooldown (slime_people.dm, xenobiology): the species is inert, so
            // is_species finds none and this branch does not run.
            Dm.Deferred("code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:492", "energized_jelly on_mob_life: slime_species.extract_cooldown (the luminescent jellypeople, XB1-2)");
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:483  (the vars are generated, D111; M4b: the
// advanced extinguisher)
[DmPath("/datum/reagent/firefighting_foam")]
public partial class datum_reagent_firefighting_foam : datum_reagent
{
    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:491
    public override async DmTask<object> expose_turf(turf exposed_turf = null, object reac_volume = null)
    {
        object dot = await base.expose_turf(exposed_turf, reac_volume);
        if (exposed_turf is not turf_open open_turf)
            return dot;

        if (Dm.num(reac_volume) >= 1)
        {
            // gaps F1: the foam fluid is live
            obj_effect_particle_effect_fluid_foam found = Dm.locate<obj_effect_particle_effect_fluid_foam>(open_turf);
            if (found == null)
                Dm.@new<obj_effect_particle_effect_fluid_foam_firefighting>(open_turf);
            else if (found is obj_effect_particle_effect_fluid_foam_firefighting foam)
                foam.lifetime = Dm.initial(foam.GetType(), (obj_effect_particle_effect_fluid_foam_firefighting f) => f.lifetime); //reduce object churn a little bit when using smoke by keeping existing foam alive a bit longer
        }

        obj_effect_hotspot hotspot = Dm.locate<obj_effect_hotspot>(open_turf);
        if (hotspot != null && !isspaceturf(open_turf) && open_turf.air != null)
        {
            datum_gas_mixture air = open_turf.air;
            if (air.temperature > T20C)
                air.temperature = max(air.temperature / 2, T20C);
            await air.react(this);
            qdel(hotspot);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:511
    public override async DmTask<object> expose_obj(obj exposed_obj = null, object reac_volume = null, float? methods = null, float? show_message = null)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        object dot = await base.expose_obj(exposed_obj, reac_volume, methods, show_message);
        // DEVIATION(DV27): the interaction guard (D132) on a type whose own extinguish is not translated.
        if (!InertSeam.MissingOverride(exposed_obj, "extinguish"))
            await exposed_obj.extinguish();
        return dot;
    }

    // tg: code/modules/reagents/chemistry/reagents/pyrotechnic_reagents.dm:515
    public override async DmTask expose_mob(mob_living exposed_mob = null, float? methods = null, object reac_volume = null, float? show_message = null, float? touch_protection = null, params object[] __extra)
    {
        methods ??= TOUCH;
        await base.expose_mob(exposed_mob, methods, reac_volume, show_message, touch_protection, __extra);
        if (Dm.truthy(Dm.bitand(methods, Dm.bitor(TOUCH, VAPOR))))
            exposed_mob.extinguish_mob(); //All stacks are removed
    }
}
