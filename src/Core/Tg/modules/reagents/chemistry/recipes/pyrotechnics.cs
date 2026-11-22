// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/recipes/pyrotechnics.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the smoke powder's reactions and the firefighting foam (gaps F1); the explosive, EMP, bee, fire, vortex, flash,
// sonic, phlogiston, cryostylane, pyrosium and teslium reactions (gaps P5). The var-only reactions need no part (they are live
// under a live parent). The admin messages and game logs are comments (logs, M10+); the cultists and heretics are not ported.

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:10
public partial class datum_chemical_reaction_reagent_explosion : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:1  #define PURGING_REAGENTS list(...) (file-local)
    private static DmList PURGING_REAGENTS => Dm.list(
        typeof(datum_reagent_medicine_c2_multiver),
        typeof(datum_reagent_medicine_pen_acid),
        typeof(datum_reagent_medicine_calomel),
        typeof(datum_reagent_medicine_ammoniated_mercury),
        typeof(datum_reagent_medicine_c2_syriniver),
        typeof(datum_reagent_medicine_c2_musiver));

    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:19
    // clear_mob_reagents is tg's fourth parameter, which hides the var of the same name: the reaction engine passes three
    // arguments, so it is null here and in every ..() of the subtypes (tg's own shadowing)
    [DmParams("holder", "reaction", "created_volume", "clear_mob_reagents")]
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        object clear_mob_reagents = Dm.arg(__extra, 0);
        if (!ismob(holder.my_atom))
        {
            await reagent_explode(holder, created_volume, modifier, strengthdiv, clear_mob_reagents);
            return null;
        }

        // If an explosive reaction clears mob reagents, it should always be a minimum power
        if (Dm.truthy(clear_mob_reagents))
        {
            if (round((Dm.num(created_volume) / strengthdiv) + modifier, 1) < 1)
                modifier += 1 - ((Dm.num(created_volume) / strengthdiv) + modifier);
            await reagent_explode(holder, created_volume, modifier, strengthdiv, clear_mob_reagents);
            return null;
        }

        // If this particular explosion doesn't automatically clear mob reagents as an inherent quality,
        // then we can still clear mob reagents with some mad science malpractice that shouldn't work but
        // does because omnizine is magic and also it's the future or whatever
        // The explosion needs to be a minimum power to clear reagents: see above
        float purge_power = round((Dm.num(created_volume) / strengthdiv) + modifier, 1);
        if (purge_power < 1)
        {
            await reagent_explode(holder, created_volume, modifier, strengthdiv, clear_mob_reagents);
            return null;
        }

        float has_purging_chemical = FALSE;
        // They need one of the purge reagents in them
        foreach (object purging_chem in Dm.each(PURGING_REAGENTS))
        {
            if (Dm.truthy(holder.has_reagent(purging_chem)))
            {
                // We have a purging chemical
                has_purging_chemical = TRUE;
                break;
            }
        }
        // Then we need omnizine! MAGIC!
        object has_omnizine = holder.has_reagent(typeof(datum_reagent_medicine_omnizine));
        if (Dm.truthy(has_purging_chemical) && Dm.truthy(has_omnizine))
        {
            // With all this medical "science" combined, we can clear mob reagents
            clear_mob_reagents = TRUE;
        }
        await reagent_explode(holder, created_volume, modifier, strengthdiv, clear_mob_reagents);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:55
public partial class datum_chemical_reaction_reagent_explosion_nitroglycerin : datum_chemical_reaction_reagent_explosion
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:60
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_exotic_stabilizer), Dm.num(created_volume) / 25)))
            return null;
        await holder.remove_reagent(typeof(datum_reagent_nitroglycerin), Dm.num(created_volume) * 2);
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:72
public partial class datum_chemical_reaction_reagent_explosion_rdx : datum_chemical_reaction_reagent_explosion
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:79
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_stabilizing_agent))))
            return null;
        await holder.remove_reagent(typeof(datum_reagent_rdx), Dm.num(created_volume) * 2);
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:91
public partial class datum_chemical_reaction_reagent_explosion_rdx_explosion2 : datum_chemical_reaction_reagent_explosion
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:96
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        float fire_range = round(Dm.num(created_volume) / 30);
        turf T = get_turf(holder.my_atom);
        foreach (turf target in Dm.each_cast<turf>(RANGE_TURFS(fire_range, T)))
            Dm.@new<obj_effect_hotspot>(target);
        holder.chem_temp = 500;
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:104
public partial class datum_chemical_reaction_reagent_explosion_rdx_explosion3 : datum_chemical_reaction_reagent_explosion
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:110
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        float fire_range = round(Dm.num(created_volume) / 20);
        turf T = get_turf(holder.my_atom);
        foreach (turf turf in Dm.each_cast<turf>(RANGE_TURFS(fire_range, T)))
            Dm.@new<obj_effect_hotspot>(turf);
        holder.chem_temp = 750;
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:118
public partial class datum_chemical_reaction_reagent_explosion_tatp : datum_chemical_reaction_reagent_explosion
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:124
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        required_temp = 450 + rand(-49, 49);
    }

    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:128
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_exotic_stabilizer), Dm.num(created_volume) / 50))) // we like exotic stabilizer
            return null;
        await holder.remove_reagent(typeof(datum_reagent_tatp), created_volume);
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:134
public partial class datum_chemical_reaction_reagent_explosion_tatp_explosion : datum_chemical_reaction_reagent_explosion
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:139
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        float initial_strengthdiv = Dm.initial(this, (datum_chemical_reaction_reagent_explosion p) => p.strengthdiv);
        float strengthdiv_adjust = Dm.num(created_volume) / (2100 / initial_strengthdiv);
        strengthdiv = Dm.max(initial_strengthdiv - strengthdiv_adjust + 1.5f, 1.5f); //Slightly better than nitroglycerin
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }

    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:144
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        required_temp = 550 + rand(-49, 49);
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:168
public partial class datum_chemical_reaction_reagent_explosion_holyboom : datum_chemical_reaction_reagent_explosion
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:172
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        if (Dm.num(created_volume) >= 150)
        {
            strengthdiv = 8;
            ///turf where to play sound
            turf T = get_turf(holder.my_atom);
            ///special size for anti cult effect
            float effective_size = round(Dm.num(created_volume) / 48);
            playsound(T, Dm.resource("sound/effects/pray.ogg"), 80, FALSE, effective_size);
            foreach (mob_living_basic_revenant ghostie in Dm.each<mob_living_basic_revenant>(get_hearers_in_view(7, T)))
            {
                object deity;
                if (Dm.truthy(GLOB.deity))
                    deity = GLOB.deity;
                else
                    deity = "Christ";
                to_chat(ghostie, span_userdanger(I18n.T("The power of [] compels you!", deity)));
                await ghostie.apply_status_effect(typeof(datum_status_effect_incapacitating_paralyzed_revenant), 2 * SECONDS);
                await ghostie.apply_status_effect(typeof(datum_status_effect_revenant_revealed), 10 * SECONDS);
                await ghostie.adjust_brute_loss(50);
            }
            foreach (mob_living_carbon evil_motherfucker in Dm.each<mob_living_carbon>(get_hearers_in_view(effective_size, T)))
            {
                // IS_CULTIST(evil_motherfucker) ||: the cult is an antagonist, not ported (nobody is a cultist)
                if (HAS_TRAIT(evil_motherfucker, TRAIT_EVIL))
                {
                    to_chat(evil_motherfucker, span_userdanger(I18n.T("The divine explosion sears you!")));
                    await evil_motherfucker.Paralyze(40);
                    await evil_motherfucker.adjust_fire_stacks(5);
                    evil_motherfucker.ignite_mob();
                }
            }
        }
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:203
public partial class datum_chemical_reaction_reagent_explosion_gunpowder_explosion : datum_chemical_reaction_reagent_explosion
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:210
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        do_sparks(2, TRUE, get_turf(holder.my_atom));
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(reagent_explode), holder, created_volume, modifier, strengthdiv), rand(5 * SECONDS, 10 * SECONDS), file: "code/modules/reagents/chemistry/recipes/pyrotechnics.dm", line: 212);
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:219
public partial class datum_chemical_reaction_emp_pulse : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:223
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        //pretending this reaction took two ingredients and not three for its effects
        float two_thirds = Dm.num(created_volume) / 1.5f;
        turf location = get_turf(holder.my_atom);
        object lastkey = holder.my_atom.fingerprintslast;
        // 100 created volume = 4 heavy range & 7 light range. A few tiles smaller than traitor EMP grandes.
        // 200 created volume = 8 heavy range & 14 light range. 4 tiles larger than traitor EMP grenades.
        await empulse(location, round(two_thirds / 12), round(two_thirds / 7), emp_source: this); // tg passes a 1 as the 4th (emp_source) and then emp_source = src: the source is src
        await holder.clear_reagents();
        if (Dm.truthy(lastkey))
        {
            mob toucher = get_mob_by_key(lastkey) as mob;
            // tg's AREACOORD(turf) names the /turf typepath here (an unset var/turf/turf shadows nothing): the port names the EMP's turf
            toucher.log_message(Dm.text("triggered EMP reaction at [].", AREACOORD(location)), LOG_GAME, log_globally: FALSE);
        }
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:239
public partial class datum_chemical_reaction_beesplosion : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:243
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        atom location = holder.my_atom.drop_location() as atom;
        if (Dm.num(created_volume) < 5)
            playsound(location, Dm.resource("sound/effects/sparks/sparks1.ogg"), 100, TRUE);
        else
        {
            playsound(location, Dm.resource("sound/mobs/non-humanoids/bee/bee.ogg"), 100, TRUE);
            DmList beeagents = Dm.list();
            foreach (object R in Dm.each(holder.reagent_list))
            {
                if (Dm.truthy(required_reagents[R])) // R is a reagent datum, the keys are types: never a match (tg's own check)
                    continue;
                Dm.append(beeagents, R);
            }
            float bee_amount = round(Dm.num(created_volume) * 0.2f);
            for (float i = 1; i <= bee_amount; i++)
            {
                mob_living_basic_bee_timed new_bee = Dm.@new<mob_living_basic_bee_timed>(location);
                if (Dm.truthy(LAZYLEN(beeagents)))
                    await new_bee.assign_reagent(pick(beeagents) as datum_reagent);
            }
        }
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:266
public partial class datum_chemical_reaction_clf3 : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:273
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf T = get_turf(holder.my_atom);
        foreach (turf target in Dm.each_cast<turf>(RANGE_TURFS(1, T)))
            Dm.@new<obj_effect_hotspot>(target);
        holder.chem_temp = 1000; // hot as shit
        await DmTask.CompletedTask;
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:279
public partial class datum_chemical_reaction_reagent_explosion_methsplosion : datum_chemical_reaction_reagent_explosion
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:286
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf T = get_turf(holder.my_atom);
        foreach (turf target in Dm.each<turf>(RANGE_TURFS(1, T)))
            Dm.@new<obj_effect_hotspot>(target);
        holder.chem_temp = 1000; // hot as shit
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:297
public partial class datum_chemical_reaction_sorium : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:302
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_stabilizing_agent))))
            return null;
        await holder.remove_reagent(typeof(datum_reagent_sorium), Dm.num(created_volume) * 4);
        turf T = get_turf(holder.my_atom);
        float range = Dm.clamp(sqrt(Dm.num(created_volume) * 4), 1, 6);
        await goonchem_vortex(T, 1, range);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:310
public partial class datum_chemical_reaction_sorium_vortex : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:315
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf T = get_turf(holder.my_atom);
        float range = Dm.clamp(sqrt(Dm.num(created_volume)), 1, 6);
        await goonchem_vortex(T, 1, range);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:320
public partial class datum_chemical_reaction_liquid_dark_matter : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:325
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_stabilizing_agent))))
            return null;
        await holder.remove_reagent(typeof(datum_reagent_liquid_dark_matter), Dm.num(created_volume) * 3);
        turf T = get_turf(holder.my_atom);
        float range = Dm.clamp(sqrt(Dm.num(created_volume) * 3), 1, 6);
        await goonchem_vortex(T, 0, range);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:333
public partial class datum_chemical_reaction_ldm_vortex : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:338
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf T = get_turf(holder.my_atom);
        float range = Dm.clamp(sqrt(Dm.num(created_volume) / 2), 1, 6);
        await goonchem_vortex(T, 0, range);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:343
public partial class datum_chemical_reaction_flash_powder : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:349
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_stabilizing_agent))))
            return null;
        turf location = get_turf(holder.my_atom);
        do_sparks(2, TRUE, location);
        float range = Dm.num(created_volume) / 3;
        if (isatom(holder.my_atom))
        {
            atom A = holder.my_atom;
            A.flash_lighting_fx(range: range + 2);
        }
        foreach (mob_living C in Dm.each<mob_living>(get_hearers_in_view(range, location)))
        {
            if (Dm.truthy(await C.flash_act(affect_silicon: TRUE)))
            {
                if (get_dist(C, location) < 4)
                    await C.Paralyze(60);
                else
                    await C.Stun(100);
            }
        }
        await holder.remove_reagent(typeof(datum_reagent_flash_powder), Dm.num(created_volume) * 3);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:366
public partial class datum_chemical_reaction_flash_powder_flash : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:371
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        do_sparks(2, TRUE, location);
        float range = Dm.num(created_volume) / 10;
        if (isatom(holder.my_atom))
        {
            atom A = holder.my_atom;
            A.flash_lighting_fx(range: range + 2);
        }
        foreach (mob_living C in Dm.each<mob_living>(get_hearers_in_view(range, location)))
        {
            if (Dm.truthy(await C.flash_act(affect_silicon: TRUE)))
            {
                if (get_dist(C, location) < 4)
                    await C.Paralyze(60);
                else
                    await C.Stun(100);
            }
        }
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:386
public partial class datum_chemical_reaction_smoke_powder : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:391
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_stabilizing_agent))))
            return null;
        await holder.remove_reagent(typeof(datum_reagent_smoke_powder), Dm.num(created_volume) * 3);
        turf location = get_turf(holder.my_atom);
        await do_chem_smoke(amount: Dm.num(created_volume) * 3, holder: holder.my_atom, location: location, carry: holder, silent: FALSE, log: TRUE);
        playsound(location, Dm.resource("sound/effects/smoke.ogg"), 50, TRUE, -3);
        if (holder?.my_atom != null)
            await holder.clear_reagents();
        if (!iscarbon(holder?.my_atom))
            return null;
        mob_living_carbon victim = holder.my_atom as mob_living_carbon;
        if (victim.stat != DEAD)
            victim.visible_message(span_warning(Dm.text("[] starts violently coughing up smoke!", victim)));
        await victim.adjust_organ_loss(ORGAN_SLOT_LUNGS, Dm.num(created_volume) / 5);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:408
public partial class datum_chemical_reaction_smoke_powder_smoke : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:414
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        await do_chem_smoke(amount: created_volume, holder: holder.my_atom, location: location, carry: holder, log: TRUE, silent: FALSE);
        playsound(location, Dm.resource("sound/effects/smoke.ogg"), 50, TRUE, -3);
        if (holder?.my_atom != null)
            await holder.clear_reagents();
        if (holder?.my_atom != null) // tg clears twice (pyrotechnics.dm:418-421)
            await holder.clear_reagents();
        if (!iscarbon(holder?.my_atom))
            return null;
        mob_living_carbon victim = holder.my_atom as mob_living_carbon;
        if (victim.stat != DEAD)
            victim.visible_message(span_warning(Dm.text("[] starts violently coughing up smoke!", victim)));
        await victim.adjust_organ_loss(ORGAN_SLOT_LUNGS, Dm.num(created_volume) / 10);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:429
public partial class datum_chemical_reaction_sonic_powder : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:434
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_stabilizing_agent))))
            return null;
        await holder.remove_reagent(typeof(datum_reagent_sonic_powder), Dm.num(created_volume) * 3);
        turf location = get_turf(holder.my_atom);
        playsound(location, Dm.resource("sound/effects/bang.ogg"), 25, TRUE);
        foreach (mob_living living in Dm.each<mob_living>(get_hearers_in_view(Dm.num(created_volume) / 3, location)))
            await living.soundbang_act(1, 10 * SECONDS, rand(0, 5));
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:443
public partial class datum_chemical_reaction_sonic_powder_deafen : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:448
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        turf location = get_turf(holder.my_atom);
        playsound(location, Dm.resource("sound/effects/bang.ogg"), 25, TRUE);
        foreach (mob_living living in Dm.each<mob_living>(get_hearers_in_view(Dm.num(created_volume) / 10, location)))
            await living.soundbang_act(1, 10 * SECONDS, rand(0, 5));
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:454
public partial class datum_chemical_reaction_phlogiston : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:459
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        if (Dm.truthy(holder.has_reagent(typeof(datum_reagent_stabilizing_agent))))
            return null;
        turf_open T = get_turf(holder.my_atom) as turf_open;
        if (istype(T, typeof(turf_open)))
            T.atmos_spawn_air(Dm.text("[]=[];[]", GAS_PLASMA, created_volume, TURF_TEMPERATURE(1000)));
        await holder.clear_reagents();
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:476
public partial class datum_chemical_reaction_cryostylane : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:472  #define CRYOSTYLANE_UNDERHEAT_TEMP 50, :473 CRYOSTYLANE_IMPURE_TEMPERATURE_RANGE 200 (file-local)
    private const float CRYOSTYLANE_UNDERHEAT_TEMP = 50;
    private const float CRYOSTYLANE_IMPURE_TEMPERATURE_RANGE = 200;

    //Halve beaker temp on reaction
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:495
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        datum_reagent oxygen = holder.has_reagent(typeof(datum_reagent_oxygen)) as datum_reagent; //If we have oxygen, bring in the old cooling effect
        if (oxygen != null)
        {
            holder.chem_temp = Dm.max(holder.chem_temp - (10 * Dm.num(oxygen.volume) * 2), 0);
            await holder.remove_reagent(typeof(datum_reagent_oxygen), oxygen.volume); // halves the temperature - tried to bring in some of the old effects at least!
        }
        return null;
    }

    //purity != temp (above 50) - the colder you are the more impure it becomes
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:503
    public override async DmTask<object> reaction_step(datum_reagents holder = null, datum_equilibrium reaction = null, object delta_t = null, object delta_ph = null, object step_reaction_vol = null)
    {
        object dot = await base.reaction_step(holder, reaction, delta_t, delta_ph, step_reaction_vol);
        if (holder.chem_temp < CRYOSTYLANE_UNDERHEAT_TEMP)
            await overheated(holder, reaction, step_reaction_vol);
        //Modify our purity by holder temperature
        float step_temp = (holder.chem_temp - CRYOSTYLANE_UNDERHEAT_TEMP) / CRYOSTYLANE_IMPURE_TEMPERATURE_RANGE;
        if (step_temp >= 1) //We're hotter than 300
            return dot;
        reaction.delta_ph = Dm.num(reaction.delta_ph) * step_temp;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:513
    public override async DmTask<object> reaction_finish(datum_reagents holder = null, datum_equilibrium reaction = null, object react_vol = null)
    {
        object dot = await base.reaction_finish(holder, reaction, react_vol);
        if (holder.chem_temp < CRYOSTYLANE_UNDERHEAT_TEMP)
            await overheated(holder, reaction, react_vol);
        return dot;
    }

    //Freezes the area around you!
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:519
    public override async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        datum_reagent_cryostylane cryostylane = holder.has_reagent(typeof(datum_reagent_cryostylane)) as datum_reagent_cryostylane;
        if (cryostylane == null)
            return await base.overheated(holder, equilibrium, step_volume_added, __extra);
        turf local_turf = get_turf(holder.my_atom);
        playsound(local_turf, Dm.resource("sound/effects/magic/ethereal_exit.ogg"), 50, 1);
        local_turf.visible_message("The reaction frosts over, releasing its chilly contents!");
        await freeze_radius(holder, null, holder.chem_temp * 2, Dm.clamp(Dm.num(cryostylane.volume) / 30, 2, 6), 120 * SECONDS, 2);
        await clear_reactants(holder, 15);
        holder.chem_temp += 100;
        return null;
    }

    //Makes a snowman if you're too impure!
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:531
    public override async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        datum_reagent_cryostylane cryostylane = holder.has_reagent(typeof(datum_reagent_cryostylane)) as datum_reagent_cryostylane;
        turf local_turf = get_turf(holder.my_atom);
        playsound(local_turf, Dm.resource("sound/effects/magic/ethereal_exit.ogg"), 50, 1);
        local_turf.visible_message(Dm.text("The reaction furiously freezes up as a snowman suddenly rises out of \\the []!", holder.my_atom));
        await freeze_radius(holder, equilibrium, holder.chem_temp, Dm.clamp(Dm.num(cryostylane.volume) / 15, 3, 10), 180 * SECONDS, 5);
        Dm.@new<obj_structure_statue_snow_snowman>(local_turf);
        await clear_reactants(holder);
        await clear_products(holder);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:564
public partial class datum_chemical_reaction_pyrosium_oxygen : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:571
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        await holder.expose_temperature(holder.chem_temp + (10 * Dm.num(created_volume)), 1);
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:574
public partial class datum_chemical_reaction_pyrosium : datum_chemical_reaction
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:584
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        await holder.expose_temperature(20, 1); // also cools the fuck down
        return null;
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:600
public partial class datum_chemical_reaction_reagent_explosion_teslium_lightning : datum_chemical_reaction_reagent_explosion
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:609
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        float T1 = Dm.num(created_volume) * 20; //100 units : Zap 3 times, with powers 8e5/2e6/4.8e6. Tesla revolvers have a power of 10000 for comparison.
        float T2 = Dm.num(created_volume) * 50;
        float T3 = Dm.num(created_volume) * 120;
        float added_delay = 0.5f * SECONDS;
        if (Dm.num(created_volume) >= 75)
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(zappy_zappy), holder, T1), added_delay, file: "code/modules/reagents/chemistry/recipes/pyrotechnics.dm", line: 615);
            added_delay += 1.5f * SECONDS;
        }
        if (Dm.num(created_volume) >= 40)
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(zappy_zappy), holder, T2), added_delay, file: "code/modules/reagents/chemistry/recipes/pyrotechnics.dm", line: 618);
            added_delay += 1.5f * SECONDS;
        }
        if (Dm.num(created_volume) >= 10) //10 units minimum for lightning, 40 units for secondary blast, 75 units for tertiary blast.
            _addtimer(Dm.@new<datum_callback>(this, nameof(zappy_zappy), holder, T3), added_delay, file: "code/modules/reagents/chemistry/recipes/pyrotechnics.dm", line: 621);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(reagent_explode), holder, created_volume, modifier, strengthdiv), added_delay, file: "code/modules/reagents/chemistry/recipes/pyrotechnics.dm", line: 622);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:624
    public virtual async DmTask zappy_zappy(datum_reagents holder = null, object power = null)
    {
        atom holder_atom = holder.my_atom;
        if (QDELETED(holder_atom))
            return;
        await tesla_zap(source: holder_atom, zap_range: 7, power: power, cutoff: 1 * KILO * JOULES, zap_flags: zap_flags);
        playsound(holder_atom, Dm.resource("sound/machines/defib/defib_zap.ogg"), 50, TRUE);
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:635
public partial class datum_chemical_reaction_reagent_explosion_nitrous_oxide : datum_chemical_reaction_reagent_explosion
{
    // tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:641
    public override async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        await holder.remove_reagent(typeof(datum_reagent_sorium), Dm.num(created_volume) * 2);
        turf turfie = get_turf(holder.my_atom);
        //generally half as strong as sorium.
        float range = Dm.clamp(sqrt(Dm.num(created_volume) * 2), 1, 6);
        //This first throws people away and then it explodes
        await goonchem_vortex(turfie, 1, range);
        turfie.atmos_spawn_air(Dm.text("[]=[];[]", GAS_O2, Dm.num(created_volume) / 2, TURF_TEMPERATURE(575)));
        turfie.atmos_spawn_air(Dm.text("[]=[];[]", GAS_N2, Dm.num(created_volume) / 2, TURF_TEMPERATURE(575)));
        return await base.on_reaction(holder, reaction, created_volume, __extra);
    }
}

// tg: code/modules/reagents/chemistry/recipes/pyrotechnics.dm:653
public partial class datum_chemical_reaction_firefighting_foam : datum_chemical_reaction
{
}
