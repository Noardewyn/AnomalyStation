// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/recipes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The reaction datum's procs (gaps F1: the reaction engine); gaps P5 (the chemistry machines and reactions): chemical_mob_spawn,
// the global goonchem_vortex and reagent_explode. The admin messages and game logs are comments (logs, M10+).

/**
 * #Chemical Reaction
 *
 * Datum that makes the magic between reagents happen.
 *
 * Chemical reactions is a class that is instantiated and stored in a global list 'chemical_reactions_list'
 */
// tg: code/modules/reagents/chemistry/recipes.dm:8
public partial class datum_chemical_reaction : datum
{
    ///REACTION PROCS

    /**
     * Checks if this reaction can occur.
     */
    // tg: code/modules/reagents/chemistry/recipes.dm:66
    public virtual async DmTask<object> pre_reaction_other_checks(datum_reagents holder = null)
    {
        return TRUE;
    }

    /**
     * Shit that happens on reaction
     * Only procs at the START of a reaction
     * use reaction_step() for each step of a reaction
     * or reaction_end() when the reaction stops
     * If reaction_flags & REACTION_INSTANT then this is the only proc that is called.
     *
     * Proc where the additional magic happens.
     * You dont want to handle mob spawning in this since there is a dedicated proc for that.client
     * Arguments:
     * * holder - the datum that holds this reagent, be it a beaker or anything else
     * * created_volume - volume created when this is mixed. look at 'var/list/results'.
     */
    // tg: code/modules/reagents/chemistry/recipes.dm:82
    public virtual async DmTask<object> on_reaction(datum_reagents holder = null, datum_equilibrium reaction = null, object created_volume = null, params object[] __extra)
    {
        return null;
        //I recommend you set the result amount to the total volume of all components.
    }

    /**
     * Stuff that occurs in the middle of a reaction
     * Only procs DURING a reaction
     * If reaction_flags & REACTION_INSTANT then this isn't called
     * returning END_REACTION will END the reaction
     *
     * Arguments:
     * * reaction - the equilibrium reaction holder that is reaction is processed within - use this to edit delta_t and delta
     * * holder - the datum that holds this reagent, be it a beaker or anything else
     * * created_volume - volume created per step
     * * added_purity - how pure the created volume is per step
     *
     * Outputs:
     * * returning END_REACTION will end the associated reaction - flagging it for deletion and preventing any reaction in that timestep from happening. Make sure to set the vars in the holder to one that can't start it from starting up again.
     */
    // tg: code/modules/reagents/chemistry/recipes.dm:101
    public virtual async DmTask<object> reaction_step(datum_reagents holder = null, datum_equilibrium reaction = null, object delta_t = null, object delta_ph = null, object step_reaction_vol = null)
    {
        return null;
    }

    /**
     * Stuff that should happen at the end of the reaction. Presently only REACTION_CLEAR_INVERSE is respected here
     * You should be calling ..() if you're writing a child function of this proc otherwise purity methods won't work correctly
     *
     * Proc where the additional magic happens.
     * You dont want to handle mob spawning in this since there is a dedicated proc for that.client
     * Arguments:
     * * holder - the datum that holds this reagent, be it a beaker or anything else
     * * react_volume - volume created across the whole reaction
     */
    // tg: code/modules/reagents/chemistry/recipes.dm:114
    public virtual async DmTask<object> reaction_finish(datum_reagents holder = null, datum_equilibrium reaction = null, object react_vol = null)
    {
        if (!Dm.truthy(Dm.bitand(reaction_flags, REACTION_CLEAR_INVERSE)))
            return null;

        DmList inverse_data = Dm.list();
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(holder.reagent_list))
        {
            if (!Dm.truthy(results[reagent.type]) || reagent.inverse_chem == null || reagent.purity == 1 || reagent.purity > reagent.inverse_chem_val)
                continue;

            Dm.append(ref inverse_data, reagent.inverse_chem);
            Dm.append(ref inverse_data, reagent.volume);
            Dm.append(ref inverse_data, reagent.get_inverse_purity(reagent.purity));
            reagent.volume = 0;
        }

        await holder.update_total();

        float cached_temp = holder.chem_temp;
        while (Dm.truthy(inverse_data.len))
            await holder.add_reagent(popleft(inverse_data), popleft(inverse_data), reagtemp: cached_temp, added_purity: popleft(inverse_data));
        return null;
    }

    /**
     * Occurs when a reation is overheated (i.e. past its overheatTemp)
     * Will be called every tick in the reaction that it is overheated
     * If you want this to be a once only proc (i.e. the reaction is stopped after) set reaction.toDelete = TRUE
     * The above is useful if you're writing an explosion
     * By default the parent proc will reduce the final yield slightly. If you don't want that don't add ..()
     *
     * Arguments:
     * * holder - the datum that holds this reagent, be it a beaker or anything else
     * * equilibrium - the equilibrium datum that contains the equilibrium reaction properties and methods
     * * step_volume_added - how much product (across all products) was added for this single step
     */
    // tg: code/modules/reagents/chemistry/recipes.dm:146
    public virtual async DmTask<object> overheated(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null, params object[] __extra)
    {
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(holder.reagent_list))
        {
            if (!Dm.truthy(results[reagent.type]))
                continue;
            reagent.volume *= 0.98f; //Slowly lower yield per tick
        }
        await holder.update_total();
        return null;
    }

    /**
     * Occurs when a reation is too impure (i.e. it's below purity_min)
     * Will be called every tick in the reaction that it is too impure
     * If you want this to be a once only proc (i.e. the reaction is stopped after) set reaction.toDelete = TRUE
     * The above is useful if you're writing an explosion
     * By default the parent proc will reduce the purity of all reagents involved in the reaction in the beaker slightly. If you don't want that don't add ..()
     *
     * Arguments:
     * * holder - the datum that holds this reagent, be it a beaker or anything else
     * * equilibrium - the equilibrium datum that contains the equilibrium reaction properties and methods
     * * step_volume_added - how much product (across all products) was added for this single step
     */
    // tg: code/modules/reagents/chemistry/recipes.dm:165
    public virtual async DmTask<object> overly_impure(datum_reagents holder = null, datum_equilibrium equilibrium = null, object step_volume_added = null)
    {
        DmList affected_list = Dm.add(results, required_reagents) as DmList;
        foreach (object _reagent in Dm.each(affected_list))
        {
            datum_reagent reagent = holder.has_reagent(_reagent) as datum_reagent;
            if (reagent == null)
                continue;
            reagent.purity = Dm.clamp(reagent.purity - 0.01f, 0, 1); //slowly reduce purity of reagents
        }
        return null;
    }

    /**
     * Spawns mobs from a chemical reaction.
     * Arguments:
     * * holder - the reagents holder
     * * amount_to_spawn - the amount of mobs to spawn
     * * reaction_name - what is the name of this reaction. be creative, the world is your oyster after all!
     * * mob_class - determines if the mob will be friendly, neutral or hostile
     * * mob_faction - used in determining targets, mobs from the same faction won't harm eachother.
     * * random - creates random mobs. self explanatory.
     */
    // tg: code/modules/reagents/chemistry/recipes.dm:185
    // mob_class is object (Appendix A, gaps P5): a spawn class number when random, a mob type when not (the slime reactions)
    public virtual async DmTask chemical_mob_spawn(datum_reagents holder = null, object amount_to_spawn = null, object reaction_name = null, object mob_class = null, string mob_faction = null, float? random = null)
    {
        mob_class ??= HOSTILE_SPAWN;
        mob_faction ??= FACTION_CHEMICAL_SUMMON;
        random ??= TRUE;
        if (holder?.my_atom != null)
        {
            atom A = holder.my_atom;
            turf T = get_turf(A);
            string message = Dm.text("Mobs have been spawned in [] by a [] reaction.", ADMIN_VERBOSEJMP(T), reaction_name);
            message += Dm.text(" (<A href='byond://?_src_=vars;Vars=[]'>VV</A>)", REF(A));

            mob M = get(A, typeof(mob)) as mob;
            if (M != null)
                message += Dm.text(" - Carried By: []", ADMIN_LOOKUPFLW(M));
            else
                message += Dm.text(" - Last Fingerprint: []", Dm.truthy(A.fingerprintslast) ? A.fingerprintslast : "N/A");

            message_admins(message);
            log_game(Dm.text("[] chemical mob spawn reaction occuring at [] carried by [] with last fingerprint []", reaction_name, AREACOORD(T), key_name(M), Dm.truthy(A.fingerprintslast) ? A.fingerprintslast : "N/A"));

            playsound(get_turf(holder.my_atom), Dm.resource("sound/effects/phasein.ogg"), 100, TRUE);

            foreach (mob_living_carbon C in Dm.each<mob_living_carbon>(viewers(get_turf(holder.my_atom), null)))
                await C.flash_act();

            for (float i = 1; i <= Dm.num(amount_to_spawn); i++)
            {
                mob_living spawned_mob;
                if (Dm.truthy(random))
                    spawned_mob = create_random_mob(get_turf(holder.my_atom), mob_class) as mob_living;
                else
                    spawned_mob = Dm.@new((Type)mob_class, get_turf(holder.my_atom)) as mob_living; //Spawn our specific mob_class
                spawned_mob.add_faction(mob_faction);
                ADD_TRAIT(spawned_mob, TRAIT_SPAWNED_MOB, INNATE_TRAIT);
                if (prob(50))
                {
                    for (float j = 1, j_end = rand(1, 3); j <= j_end; j++)
                        await step(spawned_mob, Dm.num(pick(NORTH, SOUTH, EAST, WEST)));
                }
            }
        }
    }

    //////////////////Generic explosions/failures////////////////////
    // It is HIGHLY, HIGHLY recomended that you consume all/a good volume of the reagents/products in an explosion - because it will just keep going forever until the reaction stops
    //If you have competitive reactions - it's a good idea to consume ALL reagents in a beaker (or product+reactant), otherwise it'll swing back with the deficit and blow up again

    /*
     *Creates a flash effect only - less expensive than explode()
     *
     * *Arguments
     * * range - the radius around the holder's atom that is flashed
     * * length - how long it lasts in ds
     */
    // tg: code/modules/reagents/chemistry/recipes.dm:329
    public virtual async DmTask explode_flash(datum_reagents holder = null, datum_equilibrium equilibrium = null, float? range = null, float? length = null)
    {
        range ??= 2;
        length ??= 25;
        turf location = get_turf(holder.my_atom);
        foreach (mob_living living_mob in Dm.each<mob_living>(viewers(range, location)))
            await living_mob.flash_act(length: length);
        holder.my_atom.visible_message(Dm.text("The [] suddenly lets out a bright flash!", holder.my_atom));
    }

    /*
     *Deafens those in range causing ear damage and muting sound
     *
     * Arguments
     * * power - How much damage is applied to the ear organ (I believe?)
     * * stun - How long the mob is stunned for
     * * range - the radius around the holder's atom that is banged
     */
    // tg: code/modules/reagents/chemistry/recipes.dm:343
    public virtual async DmTask explode_deafen(datum_reagents holder = null, datum_equilibrium equilibrium = null, float? power = null, float? stun = null, float? range = null)
    {
        power ??= 3;
        stun ??= 20;
        range ??= 2;
        turf location = get_turf(holder.my_atom);
        playsound(location, Dm.resource("sound/effects/bang.ogg"), 25, TRUE);
        foreach (mob_living living in Dm.each<mob_living>(get_hearers_in_view(range, location)))
            await living.soundbang_act(SOUNDBANG_NORMAL, stun, power);
    }

    //Spews out the inverse of the chems in the beaker of the products/reactants only
    // tg: code/modules/reagents/chemistry/recipes.dm:350
    public virtual async DmTask explode_invert_smoke(datum_reagents holder = null, datum_equilibrium equilibrium = null, float? force_range = null, float? clear_products = null, float? clear_reactants = null, float? accept_impure = null)
    {
        force_range ??= 0;
        clear_products ??= TRUE;
        clear_reactants ??= TRUE;
        accept_impure ??= TRUE;
        datum_reagents invert_reagents = Dm.@new<datum_reagents>(2100, NO_REACT); //I think the biggest size we can get is 2100?
        float sum_volume = 0;
        invert_reagents.my_atom = holder.my_atom; //Give the gas a fingerprint
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(holder.reagent_list)) //make gas for reagents, has to be done this way, otherwise it never stops Exploding
        {
            if (!Dm.truthy(Dm.@in(reagent.type, required_reagents)) || !Dm.truthy(Dm.@in(reagent.type, results)))
                continue;
            if (reagent.inverse_chem != null)
            {
                await invert_reagents.add_reagent(reagent.inverse_chem, reagent.volume, no_react: TRUE);
                await holder.remove_reagent(reagent.type, reagent.volume);
                continue;
            }
            await invert_reagents.add_reagent(reagent.type, reagent.volume, added_purity: reagent.purity, no_react: TRUE);
            sum_volume += Dm.num(reagent.volume);
            await holder.remove_reagent(reagent.type, reagent.volume);
        }
        if (force_range == 0)
            force_range = (sum_volume / 6) + 3;
        if (invert_reagents.reagent_list != null)
            await do_chem_smoke(force_range, holder.my_atom, holder.my_atom, carry: invert_reagents, log: TRUE);
        holder.my_atom.audible_message(Dm.text("The [] suddenly explodes, launching the aerosolized reagents into the air!", holder.my_atom));
        if (Dm.truthy(clear_reactants))
            await this.clear_reactants(holder);
        if (Dm.truthy(clear_products))
            await this.clear_products(holder);
    }

    //Spews out the corrisponding reactions reagents  (products/required) of the beaker in a smokecloud. Doesn't spew catalysts
    // tg: code/modules/reagents/chemistry/recipes.dm:375
    public virtual async DmTask explode_smoke(datum_reagents holder = null, datum_equilibrium equilibrium = null, float? force_range = null, float? clear_products = null, float? clear_reactants = null)
    {
        force_range ??= 0;
        clear_products ??= TRUE;
        clear_reactants ??= TRUE;
        datum_reagents reagents = Dm.@new<datum_reagents>(2100, NO_REACT); //Lets be safe first
        reagents.my_atom = holder.my_atom; //fingerprint
        float sum_volume = 0;
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(holder.reagent_list))
        {
            if (Dm.truthy(Dm.@in(reagent.type, required_reagents)) || Dm.truthy(Dm.@in(reagent.type, results)))
            {
                await reagents.add_reagent(reagent.type, reagent.volume, added_purity: reagent.purity, no_react: TRUE);
                await holder.remove_reagent(reagent.type, reagent.volume);
            }
        }
        if (force_range == 0)
            force_range = (sum_volume / 6) + 3;
        if (reagents.reagent_list != null)
            await do_chem_smoke(force_range, holder: holder.my_atom, location: holder.my_atom, carry: reagents, log: TRUE);
        holder.my_atom.audible_message(Dm.text("The [] suddenly explodes, launching the aerosolized reagents into the air!", holder.my_atom));
        if (Dm.truthy(clear_reactants))
            await this.clear_reactants(holder);
        if (Dm.truthy(clear_products))
            await this.clear_products(holder);
    }

    //Pushes everything out, and damages mobs with 10 brute damage.
    // tg: code/modules/reagents/chemistry/recipes.dm:394
    public virtual async DmTask explode_shockwave(datum_reagents holder = null, datum_equilibrium equilibrium = null, float? range = null, float? damage = null, float? sound_and_text = null, float? implosion = null)
    {
        range ??= 3;
        damage ??= 5;
        sound_and_text ??= TRUE;
        implosion ??= FALSE;
        turf this_turf = get_turf(holder.my_atom);
        if (Dm.truthy(sound_and_text))
        {
            holder.my_atom.audible_message(Dm.text("The [] suddenly explodes, sending a shockwave rippling through the air!", holder.my_atom));
            playsound(this_turf, Dm.resource("sound/effects/chemistry/shockwave_explosion.ogg"), 80, TRUE);
        }
        //Modified goonvortex
        foreach (atom_movable movey in Dm.each<atom_movable>(orange(range, this_turf)))
        {
            if (isliving(movey) && Dm.truthy(damage))
            {
                mob_living live = movey as mob_living;
                await live.apply_damage(damage); //Since this can be called multiple times
            }
            if (Dm.truthy(movey.anchored))
                continue;
            if (iseffect(movey) || iseyemob(movey) || isdead(movey))
                continue;
            if (Dm.truthy(implosion))
            {
                float distance = get_dist(movey, this_turf);
                float moving_power = Dm.max(4 - distance, 1);
                turf target = get_turf(holder.my_atom);
                await movey.throw_at(target, moving_power, 1);
            }
            else
            {
                float distance = get_dist(movey, this_turf);
                float moving_power = Dm.max(3 - distance, 1); //Make sure we're thrown out of range of the next one
                atom throw_target = get_edge_target_turf(movey, get_dir(movey, get_step_away(movey, this_turf))) as atom;
                await movey.throw_at(throw_target, moving_power, 1);
            }
        }
    }

    ////////BEGIN FIRE BASED EXPLOSIONS

    //Calls the default explosion subsystem handiler to explode with fire (random firespots and noise)
    // tg: code/modules/reagents/chemistry/recipes.dm:422
    public virtual async DmTask explode_fire(datum_reagents holder = null, datum_equilibrium equilibrium = null, float? range = null)
    {
        range ??= 3;
        // explosion_cause = src: the reaction datum names the cause in the explosion's log; the port's slot takes an atom (logs, M10+)
        await explosion(holder.my_atom, flame_range: range);
        holder.my_atom.audible_message(Dm.text("The [] suddenly errupts in flames!", holder.my_atom));
    }

    //Creates a ring of fire in a set range around the beaker location
    // tg: code/modules/reagents/chemistry/recipes.dm:427
    public virtual void explode_fire_vortex(datum_reagents holder = null, datum_equilibrium equilibrium = null, float? x_offset = null, float? y_offset = null, float? reverse = null, string id = null)
    {
        x_offset ??= 1;
        y_offset ??= 1;
        reverse ??= FALSE;
        id ??= "f_vortex";
        float increment = Dm.truthy(reverse) ? -1 : 1;
        string ix = Dm.text("[]_x", id), iy = Dm.text("[]_y", id), itar = Dm.text("[]_tar", id);
        if (equilibrium.data[itar] == null)
            equilibrium.data = Dm.list(Dm.kv(ix, x_offset), Dm.kv(iy, y_offset), Dm.kv(itar, iy)); //tar is the current movement direction the cyclone is moving in
        if (Dm.eq(equilibrium.data[itar], ix))
        {
            if (Dm.num(equilibrium.data[ix]) >= x_offset)
            {
                equilibrium.data[itar] = iy;
                equilibrium.data[iy] = Dm.num(equilibrium.data[iy]) + increment;
            }
            else if (Dm.num(equilibrium.data[ix]) <= -x_offset)
            {
                equilibrium.data[itar] = iy;
                equilibrium.data[iy] = Dm.num(equilibrium.data[iy]) - increment;
            }
            else
            {
                if (Dm.num(equilibrium.data[iy]) < 0)
                    equilibrium.data[ix] = Dm.num(equilibrium.data[ix]) + increment;
                else if (Dm.num(equilibrium.data[iy]) > 0)
                    equilibrium.data[ix] = Dm.num(equilibrium.data[ix]) - increment;
            }
        }
        else if (Dm.eq(equilibrium.data[itar], iy))
        {
            if (Dm.num(equilibrium.data[iy]) >= y_offset)
            {
                equilibrium.data[itar] = ix;
                equilibrium.data[ix] = Dm.num(equilibrium.data[ix]) - increment;
            }
            else if (Dm.num(equilibrium.data[iy]) <= -y_offset)
            {
                equilibrium.data[itar] = ix;
                equilibrium.data[ix] = Dm.num(equilibrium.data[ix]) + increment;
            }
            else
            {
                if (Dm.num(equilibrium.data[ix]) < 0)
                    equilibrium.data[iy] = Dm.num(equilibrium.data[iy]) - increment;
                else if (Dm.num(equilibrium.data[ix]) > 0)
                    equilibrium.data[iy] = Dm.num(equilibrium.data[iy]) + increment;
            }
        }
        turf holder_turf = get_turf(holder.my_atom);
        turf target = locate(holder_turf.x + Dm.num(equilibrium.data[ix]), holder_turf.y + Dm.num(equilibrium.data[iy]), holder_turf.z);
        Dm.@new<obj_effect_hotspot>(target);
        // debug_world("X: ..., Y: ..."): tg's debug line to the world log (debugging output, not a game effect)
    }

    /*
     * Creates a square of fire in a fire_range radius,
     * fire_range = 0 will be on the exact spot of the holder,
     * fire_range = 1 or more will be additional tiles around the holder. Every tile will be heated this way.
     * How clf3 works, you know!
     */
    // tg: code/modules/reagents/chemistry/recipes.dm:467
    public virtual void explode_fire_square(datum_reagents holder = null, datum_equilibrium equilibrium = null, float? fire_range = null)
    {
        fire_range ??= 1;
        turf location = get_turf(holder.my_atom);
        if (fire_range == 0)
        {
            Dm.@new<obj_effect_hotspot>(location);
            return;
        }
        foreach (turf turf in Dm.each_cast<turf>(RANGE_TURFS(fire_range, location)))
            Dm.@new<obj_effect_hotspot>(turf);
    }

    ///////////END FIRE BASED EXPLOSIONS

    /*
    * Freezes in a circle around the holder location
    * Arguments:
    * * temp - the temperature to set the air to
    * * radius - the range of the effect
    * * freeze_duration - how long the icey spots remain for
    * * snowball_chance - the chance to spawn a snowball on a turf
    */
    // tg: code/modules/reagents/chemistry/recipes.dm:485
    public virtual async DmTask freeze_radius(datum_reagents holder = null, datum_equilibrium equilibrium = null, object temp = null, float? radius = null, float? freeze_duration = null, float? snowball_chance = null)
    {
        radius ??= 2;
        freeze_duration ??= 50 * SECONDS;
        snowball_chance ??= 0;
        foreach (object any_turf in Dm.each(circle_range_turfs(center: get_turf(holder.my_atom), radius: radius)))
        {
            if (!isopenturf(any_turf))
                continue;
            turf_open open_turf = any_turf as turf_open;
            await open_turf.MakeSlippery(TURF_WET_PERMAFROST, freeze_duration, freeze_duration, freeze_duration);
            open_turf.temperature = Dm.num(temp);
            if (prob(Dm.num(snowball_chance)))
                Dm.@new<obj_item_toy_snowball>(open_turf);
        }
    }

    ///Clears the beaker of the reagents only
    ///if volume is not set, it will remove all of the reactant
    // tg: code/modules/reagents/chemistry/recipes.dm:497
    public virtual async DmTask<object> clear_reactants(datum_reagents holder = null, float? volume = null)
    {
        volume ??= 1000;
        if (holder == null)
            return FALSE;
        foreach (object reagent in Dm.each(required_reagents))
            await holder.remove_reagent(reagent, volume);
        return null;
    }

    ///Clears the beaker of the product only
    // tg: code/modules/reagents/chemistry/recipes.dm:504
    public virtual async DmTask<object> clear_products(datum_reagents holder = null, float? volume = null)
    {
        volume ??= 1000;
        if (holder == null)
            return FALSE;
        foreach (object reagent in Dm.each(results))
            await holder.remove_reagent(reagent, volume);
        return null;
    }

    ///Clears the beaker of ALL reagents inside
    // tg: code/modules/reagents/chemistry/recipes.dm:512
    public virtual async DmTask<object> clear_reagents(datum_reagents holder = null, float? volume = null)
    {
        volume ??= 1000;
        if (holder == null)
            return FALSE;
        if (volume == 0)
            volume = holder.total_volume;
        await holder.remove_all(volume);
        return null;
    }

    /*
    * "Attacks" all mobs within range with a specified reagent
    * Will be blocked if they're wearing proper protective equipment unless disabled
    * Arguments
    * * reagent - the reagent typepath that will be added
    * * vol - how much will be added
    * * range - the range that this will affect mobs for
    * * ignore_mask - if masks block the effect, making this true will affect someone regardless
    * * ignore_eyes - if glasses block the effect, making this true will affect someone regardless
    */
    // tg: code/modules/reagents/chemistry/recipes.dm:529
    public virtual async DmTask explode_attack_chem(datum_reagents holder = null, datum_equilibrium equilibrium = null, object reagent = null, object vol = null, float? range = null, float? ignore_mask = null, float? ignore_eyes = null)
    {
        range ??= 3;
        ignore_mask ??= FALSE;
        ignore_eyes ??= FALSE;
        if (istype(reagent, typeof(datum_reagent)))
        {
            datum_reagent temp_reagent = reagent as datum_reagent;
            reagent = temp_reagent.type;
        }
        foreach (mob_living_carbon target in Dm.each<mob_living_carbon>(orange(range, get_turf(holder.my_atom))))
        {
            if (Dm.truthy(target.has_smoke_protection()) && !Dm.truthy(ignore_mask))
                continue;
            if (Dm.truthy(target.get_eye_protection()) && !Dm.truthy(ignore_eyes))
                continue;
            to_chat(target, Dm.text("The [] launches some of [] contents at you!", holder.my_atom.name, holder.p_their()));
            await (target.reagents as datum_reagents).add_reagent(reagent, vol);
        }
    }

    /*
    * Applys a cooldown to the reaction
    * Returns false if time is below required, true if it's above required
    * Time is kept in eqilibrium data
    *
    * Arguments:
    * * seconds - the amount of time in server seconds to delay between true returns, will ceiling to the nearest 0.25
    * * id - a string phrase so that multiple cooldowns can be applied if needed
    * * initial_delay - The number of seconds of delay to add on creation
    */
    // tg: code/modules/reagents/chemistry/recipes.dm:552
    public virtual object off_cooldown(datum_reagents holder = null, datum_equilibrium equilibrium = null, float? seconds = null, object id = null, float? initial_delay = null)
    {
        seconds ??= 1;
        id ??= "default";
        initial_delay ??= 0;
        id = Dm.text("[]_cooldown", id);
        if (equilibrium.data[id] == null)
        {
            equilibrium.data[id] = 0f;
            if (Dm.truthy(initial_delay))
            {
                equilibrium.data[id] = Dm.num(equilibrium.data[id]) + Dm.num(initial_delay);
                return FALSE;
            }
            return TRUE; //first time we know we can go
        }
        equilibrium.data[id] = Dm.num(equilibrium.data[id]) + (Dm.truthy(equilibrium.time_deficit) ? 0.5f : 0.25f); //sync to lag compensator
        if (Dm.num(equilibrium.data[id]) >= seconds)
        {
            equilibrium.data[id] = 0f;
            return TRUE;
        }
        return FALSE;
    }
}

public static partial class GlobalProcs
{
    /**
     * Magical move-wooney that happens sometimes.
     *
     * Simulates a vortex that moves nearby movable atoms towards or away from the turf T.
     * Range also determines the strength of the effect. High values cause nearby objects to be thrown.
     * Arguments:
     * * T - turf where it happens
     * * setting_type - does it suck or does it blow?
     * * range - range.
     */
    // tg: code/modules/reagents/chemistry/recipes.dm:228
    public static async DmTask goonchem_vortex(turf T = null, object setting_type = null, object range = null)
    {
        foreach (atom_movable X in Dm.each<atom_movable>(orange(range, T)))
        {
            if (Dm.truthy(X.anchored))
                continue;
            if (iseffect(X) || iseyemob(X) || isdead(X))
                continue;
            float distance = Dm.num(get_dist(X, T));
            float moving_power = Dm.max(Dm.num(range) - distance, 1);
            if (moving_power > 2) //if the vortex is powerful and we're close, we get thrown
            {
                if (Dm.truthy(setting_type))
                {
                    atom throw_target = get_edge_target_turf(X, get_dir(X, get_step_away(X, T))) as atom;
                    await X.throw_at(throw_target, moving_power, 1);
                }
                else
                    await X.throw_at(T, moving_power, 1);
            }
            else
            {
                if (Dm.truthy(setting_type))
                {
                    if (Dm.truthy(await step_away(X, T)) && moving_power > 1) //Can happen twice at most. So this is fine.
                        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(_step_away), X, T), 0.2f * SECONDS, file: "code/modules/reagents/chemistry/recipes.dm", line: 247);
                }
                else
                {
                    if (Dm.truthy(await step_towards(X, T)) && moving_power > 1)
                        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(_step_towards), X, T), 0.2f * SECONDS, file: "code/modules/reagents/chemistry/recipes.dm", line: 250);
                }
            }
        }
    }

    /*
     * The same method that pyrotechnic reagents used before
     * Now instead of defining the var as part of the reaction - any recipe can call it and define their own method
     * WILL REMOVE ALL REAGENTS
     *
     * arguments:
     * * holder - the reagents datum that it is being used on
     * * volume - the volume of reacting elements
     * * modifier - a flat additive numeric to the size of the explosion - set this if you want a minimum range
     * * strengthdiv - the divisional factor of the explosion, a larger number means a smaller range - This is the part that modifies an explosion's range with volume (i.e. it divides it by this number)
     * * clear_mob_reagents - should our mob be purged of their reagents?
     * * clear_holder_reagents - should the reagent holder be fully purged? Heavily recommended to be TRUE, unless you have code handling that elsewhere down the line for whatever reason
     * * flame_factor - multiplier to flame range of the explosion
     * * flash_factor - multiplier to flash range of the explosion
     */
    // tg: code/modules/reagents/chemistry/recipes.dm:269
    public static async DmTask reagent_explode(datum_reagents holder = null, object volume = null, object modifier = null, object strengthdiv = null, object clear_mob_reagents = null, object clear_holder_reagents = null, object flash_factor = null, object flame_factor = null)
    {
        modifier ??= 0f;
        strengthdiv ??= 10f;
        clear_mob_reagents ??= FALSE;
        clear_holder_reagents ??= TRUE;
        if (QDELETED(holder))
            return;

        float power = Dm.num(modifier) + round(Dm.num(volume) / Dm.num(strengthdiv), 1);
        if (power > 0)
        {
            turf our_turf = get_turf(holder.my_atom);
            string inside_msg = null;
            if (ismob(holder.my_atom))
            {
                mob owner = (mob)holder.my_atom;
                inside_msg = Dm.text(" inside []", ADMIN_LOOKUPFLW(owner));
            }
            object lastkey = holder.my_atom.fingerprintslast; //This can runtime (null.fingerprintslast) - due to plumbing?
            string touch_msg = "N/A";
            if (Dm.truthy(lastkey))
            {
                mob toucher = get_mob_by_key(lastkey) as mob;
                touch_msg = Dm.text("[]", ADMIN_LOOKUPFLW(toucher));
            }
            if (!istype(holder.my_atom, typeof(obj_machinery_plumbing))) //excludes standard plumbing equipment from spamming admins with this shit
                message_admins(Dm.text("Reagent explosion reaction occurred at [][]. Last Fingerprint: [].", ADMIN_VERBOSEJMP(our_turf), inside_msg, touch_msg));
            log_game(Dm.text("Reagent explosion reaction occurred at []. Last Fingerprint: [].", AREACOORD(our_turf), Dm.truthy(lastkey) ? lastkey : "N/A"));
            datum_effect_system_reagents_explosion explosion_system = Dm.@new<datum_effect_system_reagents_explosion>(our_turf, power, flash_factor, flame_factor); // flash_fact = flash_factor, flame_fact = flame_factor
            await explosion_system.start(holder.my_atom);
        }

        if (istype(holder.my_atom, typeof(obj_item_organ_stomach)))
        {
            obj_item_organ_stomach indigestion = holder.my_atom as obj_item_organ_stomach;
            if (power < 1)
                return;
            if (Dm.truthy(clear_mob_reagents) && indigestion.owner != null)
                await indigestion.owner.vomit(Dm.bitor(MOB_VOMIT_MESSAGE, MOB_VOMIT_FORCE), lost_nutrition: 150, distance: 5, purge_ratio: 1);
            if (Dm.truthy(clear_holder_reagents))
                await holder.clear_reagents();
            return;
        }

        if (!ismob(holder.my_atom))
        {
            await holder.clear_reagents();
            return;
        }

        // Not quite the same if the reaction is in their stomach; they'll throw up
        // from any explosion, but it'll only make them puke up everything in their
        // stomach
        if (!Dm.truthy(clear_mob_reagents))
            return;

        // Only clear reagents if they use a special explosive reaction to do it; it shouldn't apply
        // to any explosion inside a person
        if (Dm.truthy(clear_holder_reagents))
            await holder.clear_reagents();

        if (iscarbon(holder.my_atom))
        {
            mob_living_carbon victim = holder.my_atom as mob_living_carbon;
            float vomit_flags = Dm.bitor(MOB_VOMIT_MESSAGE, MOB_VOMIT_FORCE);
            // The vomiting here is for effect, not meant to help with purging
            await victim.vomit(vomit_flags, distance: 5);
        }
    }
}
