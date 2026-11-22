// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/recipes/cat2_medicines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5): the category two medicines' own procs (the helbital fires, the aiuri bang, the hercuri freeze, the
// convermol, tirimol and multiver failures, the syriniver and penthrite steps); the var-only ones are live under a live parent.
// The container's chat icon is a comment (the chat icons, section 76).

// tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:5
public partial class datum_chemical_reaction_medicine_helbital : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:25
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        explode_fire_vortex(holder, equilibrium, 1, 1, TRUE); // reverse = "impure", a truthy text (tg passes the id in the reverse slot)
        holder.chem_temp += 2.5f;
        datum_reagent helbital = holder.has_reagent(typeof(datum_reagent_medicine_c2_helbital)) as datum_reagent;
        if (helbital == null)
            return null;
        if (helbital.purity <= 0.25f)
        {
            if (prob(25))
            {
                Dm.@new<obj_effect_hotspot>(holder.my_atom.loc);
                await holder.remove_reagent(typeof(datum_reagent_medicine_c2_helbital), 2);
                holder.chem_temp += 5;
                // [icon2html(holder.my_atom, viewers(DEFAULT_MESSAGE_RANGE, src))]: the container's picture in the chat line (the chat icons)
                holder.my_atom.audible_message(span_notice(Dm.text(" The impurity of the reacting helbital is too great causing [] to let out a hearty burst of flame, evaporating part of the product!", holder.my_atom)));
            }
        }
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:38
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        object dot = await base.overheated(holder, equilibrium, step_volume_added, __extra); //drains product
        explode_fire_vortex(holder, equilibrium, 2, 2, TRUE, "1"); // reverse = "overheat" (truthy text), id = TRUE (DM writes it "1")
        return dot;
    }

    // tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:42
    public override async DmTask<object> reaction_finish(datum_reagents holder = null, datum_equilibrium reaction = null, object react_vol = null)
    {
        object dot = await base.reaction_finish(holder, reaction, react_vol);
        datum_reagent helbital = holder.has_reagent(typeof(datum_reagent_medicine_c2_helbital)) as datum_reagent;
        if (helbital == null)
            return dot;
        if (helbital.purity <= 0.1f) //So people don't ezmode this by keeping it at min
        {
            await explode_fire(holder, null, 3);
            await clear_products(holder);
        }
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:110
public partial class datum_chemical_reaction_medicine_aiuri : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:128
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        object dot = await base.overheated(holder, equilibrium, step_volume_added, __extra);
        foreach (mob_living living_mob in Dm.each<mob_living>(orange(3, get_turf(holder.my_atom))))
        {
            if (Dm.truthy(await living_mob.flash_act(1, length: 5)))
                await living_mob.set_eye_blur(20 * SECONDS);
        }
        // [icon2html(holder.my_atom, viewers(DEFAULT_MESSAGE_RANGE, src))]: the container's picture in the chat line (the chat icons)
        holder.my_atom.audible_message(span_notice(Dm.text(" The [] lets out a loud bang!", holder.my_atom)));
        playsound(holder.my_atom, Dm.resource("sound/effects/explosion/explosion1.ogg"), 50, 1);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:136
public partial class datum_chemical_reaction_medicine_hercuri : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:154
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        if (Dm.truthy(off_cooldown(holder, equilibrium, 2, "hercuri_freeze")))
            return null;
        playsound(holder.my_atom, Dm.resource("sound/effects/magic/ethereal_exit.ogg"), 50, 1);
        holder.my_atom.visible_message("The reaction frosts over, releasing its chilly contents!");
        float radius = Dm.max(equilibrium.step_target_vol / 50, 1);
        await freeze_radius(holder, equilibrium, 200, radius, 60 * SECONDS); //drying agent exists
        await explode_shockwave(holder, equilibrium, sound_and_text: FALSE);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:166
public partial class datum_chemical_reaction_medicine_convermol : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:185
    public override async DmTask<object> reaction_step(datum_reagents holder = null, datum_equilibrium reaction = null, object delta_t = null, object delta_ph = null, object step_reaction_vol = null)
    {
        object dot = await base.reaction_step(holder, reaction, delta_t, delta_ph, step_reaction_vol);
        object oxy = holder.has_reagent(typeof(datum_reagent_oxygen));
        if (Dm.truthy(oxy))
            await holder.remove_reagent(typeof(datum_reagent_oxygen), 0.25f);
        else
            reaction.delta_t = Dm.num(delta_t) / 10; //slow without oxygen
        return dot;
    }

    // tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:193
    [DmParams("holder", "equilibrium", "step_volume_added", "impure")]
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        object impure = Dm.arg(__extra, 0) ?? FALSE;
        float range = Dm.truthy(impure) ? 4 : 3;
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_oxygen))))
            await explode_shockwave(holder, equilibrium, range); //damage 5
        else
            await explode_shockwave(holder, equilibrium, range, damage: 2);
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:200
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        object dot = await base.overly_impure(holder, equilibrium, step_volume_added);
        await overheated(holder, equilibrium, step_volume_added, TRUE); // impure = TRUE
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_oxygen))))
            await clear_reactants(holder, Dm.num(step_volume_added) * 2);
        else
            await clear_reactants(holder);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:209
public partial class datum_chemical_reaction_medicine_tirimol : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:229
    [DmParams("holder", "equilibrium", "step_volume_added", "impure")]
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        object impure = Dm.arg(__extra, 0) ?? FALSE;
        float bonus = Dm.truthy(impure) ? 2 : 1;
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_oxygen))))
        {
            await explode_attack_chem(holder, equilibrium, typeof(datum_reagent_inverse_healing_tirimol), 7.5f * bonus, 2, ignore_eyes: TRUE); //since we're smoke/air based
            await clear_products(holder, 5); //since we attacked
            await explode_invert_smoke(holder, equilibrium, 3);
        }
        else
            await explode_invert_smoke(holder, equilibrium, 3);
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:238
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        object dot = await base.overly_impure(holder, equilibrium, step_volume_added);
        await overheated(holder, equilibrium, step_volume_added, TRUE); // impure = TRUE
        await clear_reactants(holder, 2);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:266
public partial class datum_chemical_reaction_medicine_multiver : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:285
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        object dot = await base.overheated(holder, equilibrium, step_volume_added, __extra);
        datum_reagent monover = holder.has_reagent(typeof(datum_reagent_inverse_healing_monover)) as datum_reagent;
        if (monover != null)
        {
            await holder.remove_reagent(typeof(datum_reagent_inverse_healing_monover), monover.volume);
            // [icon2html(holder.my_atom, viewers(DEFAULT_MESSAGE_RANGE, src))]: the container's picture in the chat line (the chat icons)
            holder.my_atom.audible_message(span_notice(" The Monover bursts into flames from the heat!"));
            explode_fire_square(holder, equilibrium, 1);
            await holder.my_atom.fire_act(holder.chem_temp, monover.volume); //I'm kinda banking on this setting the thing on fire. If you see this, then it didn't!
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:294
    public override async DmTask<object> reaction_step(datum_reagents holder = null, datum_equilibrium reaction = null, object delta_t = null, object delta_ph = null, object step_reaction_vol = null)
    {
        object dot = await base.reaction_step(holder, reaction, delta_t, delta_ph, step_reaction_vol);
        if (Dm.num(delta_ph) < 0.35f)
        {
            //normalise delta_ph
            float norm_d_ph = 1 - (Dm.num(delta_ph) / 0.35f);
            holder.chem_temp += norm_d_ph * 12; //0 - 48 per second)
        }
        if (Dm.num(delta_ph) < 0.1f)
            holder.my_atom.visible_message(span_notice(" The Monover begins to glow!"));
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:303
public partial class datum_chemical_reaction_medicine_syriniver : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:321
    public override async DmTask<object> reaction_step(datum_reagents holder = null, datum_equilibrium reaction = null, object delta_t = null, object delta_ph = null, object step_reaction_vol = null)
    {
        object dot = await base.reaction_step(holder, reaction, delta_t, delta_ph, step_reaction_vol);
        reaction.delta_t = Dm.num(delta_t) * Dm.num(delta_ph);
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:325
public partial class datum_chemical_reaction_medicine_penthrite : datum_chemical_reaction_medicine
{
    // tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:344
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        object dot = await base.overheated(holder, equilibrium, step_volume_added, __extra);
        if (Dm.truthy(off_cooldown(holder, equilibrium, 1, "lub")))
        {
            await explode_shockwave(holder, equilibrium, 3, 2);
            playsound(holder.my_atom, Dm.resource("sound/effects/health/slowbeat.ogg"), 50, 1); // this is 2 mintues long (!) cut it up!
        }
        if (Dm.truthy(off_cooldown(holder, equilibrium, 1, "dub", 0.5f)))
        {
            await explode_shockwave(holder, equilibrium, 3, 2, implosion: TRUE);
            playsound(holder.my_atom, Dm.resource("sound/effects/health/slowbeat.ogg"), 50, 1);
        }
        explode_fire_vortex(holder, equilibrium, 1, 1);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/recipes/cat2_medicines.dm:355
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        holder.chem_temp += 15;
        await DmTask.CompletedTask;
        return null;
    }
}
