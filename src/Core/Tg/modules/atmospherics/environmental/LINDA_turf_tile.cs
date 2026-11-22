// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/environmental/LINDA_turf_tile.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated: the var blocks (W4.0 contracts), /turf/open/Initialize and Destroy (package B, wave 4), the gas mixture procs
// :81-125 (M2 P2), and (M4b slice 1, D134: the air that moves) the rest of LINDA: the heat exposure of turfs, the archive,
// the turf sharing (process_cell), the space wind, the excited groups and the superconduction; (M4b slice 3, the gas
// visuals) update_visuals. One leaf stays: the excited groups' debug display (the
// admin atmos control panel, M10).

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:1
public partial class turf : atom
{
    ///used for temperature calculations in superconduction
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:3
    public float thermal_conductivity = 0.05f;
    ///Amount of heat necessary to activate some atmos processes (there is a weird usage of this var because is compared directly to the temperature instead of heat energy)
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:5
    public float heat_capacity = INFINITY; //This should be opt in rather then opt out
    ///Archived version of the temperature on a turf
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:7
    public object temperature_archived;
    ///All currently stored conductivities changes
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:9
    public DmList thermal_conductivities;

    ///list of turfs adjacent to us that air can flow onto
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:12
    public DmList atmos_adjacent_turfs;
    ///bitfield of dirs in which we are superconducitng
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:14
    public float atmos_supeconductivity = NONE;

    ///used to determine whether we should archive
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:17
    public float archived_cycle = 0;
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:18
    public float current_cycle = 0;

    /**
    * used for mapping and for breathing while in walls (because that's a thing that needs to be accounted for...)
    * string parsed by /datum/gas/proc/copy_from_turf
    * approximation of MOLES_O2STANDARD and MOLES_N2STANDARD pending byond allowing constant expressions to be embedded in constant strings
    * If someone will place 0 of some gas there, SHIT WILL BREAK. Do not do that.
    **/
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:26
    public string initial_gas_mix = OPENTURF_DEFAULT_ATMOS;
}

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:28
public partial class turf_open : turf
{
    //used for spacewind
    ///Pressure difference between two turfs
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:31
    public float pressure_difference = 0;
    ///Where the difference come from (from higher pressure to lower pressure)
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:33
    public float pressure_direction = 0;
    /// <summary>DEVIATION(DV34): UP or DOWN when the share that set <see cref="pressure_direction"/> was vertical (tg's get_dir ignores z and
    /// leaves it 0, so nothing is pushed); only the airflow streams read it, to draw the air going through open space.</summary>
    public int airflow_vertical;

    ///Excited group we are part of
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:36
    public datum_excited_group excited_group;
    ///Are we active?
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:38
    public float excited = FALSE;
    ///Our gas mix
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:40
    public datum_gas_mixture air;

    ///If there is an active hotspot on us store a reference to it here
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:43
    public obj_effect_hotspot active_hotspot;
    /// air will slowly revert to initial_gas_mix
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:45
    public float planetary_atmos = FALSE;
    /// once our paired turfs are finished with all other shares, do one 100% share
    /// exists so things like space can ask to take 100% of a tile's gas
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:48
    public float run_later = FALSE;

    ///gas IDs of current active gas overlays
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:51
    public DmList atmos_overlay_types;
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:52
    public float significant_share_ticker = 0;
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:54  COOLDOWN_DECLARE(fire_puff_cooldown)
    // (__DEFINES/cooldowns.dm: var/fire_puff_cooldown = 0)
    public float fire_puff_cooldown = 0;
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:55-57  #ifdef TRACK_MAX_SHARE ... #endif not
    // translated (inactive in production)
}

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:59  (interleaved types, rules 1.9)
public partial class turf_open : turf
{
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:59
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!Dm.truthy(blocks_air))
        {
            // DEVIATION(DV505): a map sets the air its station's levels start with (the map json's anomaly.air, MapSettings): a floor that has tg's default
            // mix has the map's "inside" mix, and a turf open to a planet's sky (planetary_atmos) the map's "outside" one
            MapSettings map_settings = MapSettingsSeam.Current;
            if ((map_settings.AirInside != null || map_settings.AirOutside != null) && Dm.truthy(SSmapping?.level_trait(z, ZTRAIT_STATION)))
            {
                if (Dm.truthy(planetary_atmos))
                    initial_gas_mix = map_settings.AirOutside ?? initial_gas_mix;
                else if (Dm.eq(initial_gas_mix, OPENTURF_DEFAULT_ATMOS))
                    initial_gas_mix = map_settings.AirInside ?? initial_gas_mix;
            }
            air = create_gas_mixture() as datum_gas_mixture;
            if (Dm.truthy(planetary_atmos))
            {
                if (!Dm.truthy(SSair.planetary[initial_gas_mix]))
                {
                    datum_gas_mixture_immutable_planetary mix = Dm.@new<datum_gas_mixture_immutable_planetary>();
                    mix.parse_string_immutable(initial_gas_mix);
                    SSair.planetary[initial_gas_mix] = mix;
                }
            }
        }
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:68
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(active_hotspot))
            QDEL_NULL(ref active_hotspot);
        // Adds the adjacent turfs to the current atmos processing
        foreach (object near_turf in Dm.each(atmos_adjacent_turfs))
            SSair.add_to_active(near_turf as turf); // gaps BR1: a closed neighbour too, as tg passes it (the typed arg is no cast in DM)
        return base.Destroy(force);
    }
}

/////////////////GAS MIXTURE PROCS///////////////////

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:81  (interleaved types, rules 1.9)
public partial class turf : atom
{
    ///Copies all gas info from the turf into a new gas_mixture, along with our temperature
    ///Returns the created gas_mixture
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:81
    public virtual object create_gas_mixture()
    {
        datum_gas_mixture mix = SSair.parse_gas_string(initial_gas_mix, typeof(datum_gas_mixture_turf)) as datum_gas_mixture;

        //acounts for changes in temperature
        Type parent = Dm.parent_type(type);
        if (temperature != Dm.initial(this, (turf p) => p.temperature) || temperature != Dm.initial(parent, (turf p) => p.temperature))
            mix.temperature = temperature;

        return mix;
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:91  (interleaved types, rules 1.9)
public partial class turf_open : turf
{
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:91
    public override async DmTask<object> assume_air(datum_gas_mixture giver = null) //use this for machines to adjust air
    {
        if (giver == null)
            return FALSE;
        air.merge(giver);
        update_visuals();
        air_update_turf(FALSE, FALSE);
        return TRUE;
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:99
    public override object remove_air(object amount = null)
    {
        datum_gas_mixture ours = Waiting.NoSleep(return_air()) as datum_gas_mixture;
        datum_gas_mixture removed = ours.remove(amount) as datum_gas_mixture;
        update_visuals();
        air_update_turf(FALSE, FALSE);
        return removed;
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:106
    public virtual void copy_air_with_tile(turf_open target_turf = null)
    {
        if (target_turf is turf_open)
            air.copy_from(target_turf.air);
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:110
    public virtual void copy_air(datum_gas_mixture copy = null)
    {
        if (copy != null)
            air.copy_from(copy);
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:114  (interleaved types, rules 1.9)
public partial class turf : atom
{
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:114
    public override async DmTask<object> return_air()
    {
        datum_gas_mixture copied_mixture = create_gas_mixture() as datum_gas_mixture;
        return copied_mixture;
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:119  (interleaved types, rules 1.9)
public partial class turf_open : turf
{
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:119
    public override async DmTask<object> return_air()
    {
        return air;
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:123
    public override object return_analyzable_air()
    {
        return Waiting.NoSleep(return_air());
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:166  (interleaved types, rules 1.9)
public partial class turf_open : turf
{
    /////////////////////////GAS OVERLAYS//////////////////////////////

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:166
    public virtual void update_visuals()
    {
        DmList atmos_overlay_types = this.atmos_overlay_types; // Cache for free performance

        if (!Dm.truthy(air)) // 2019-05-14: was not able to get this path to fire in testing. Consider removing/looking at callers -Naksu
        {
            if (Dm.truthy(atmos_overlay_types))
            {
                foreach (object overlay in Dm.each(atmos_overlay_types))
                    Dm.remove(vis_contents, overlay);
                this.atmos_overlay_types = null;
            }
            return;
        }

        DmList moles = air.moles;
        DmList new_overlay_types = GAS_OVERLAYS(moles, this);

        if (Dm.truthy(atmos_overlay_types))
        {
            foreach (object overlay in Dm.each(Dm.sub(atmos_overlay_types, new_overlay_types))) //doesn't remove overlays that would only be added
                Dm.remove(vis_contents, overlay);
        }

        if (Dm.truthy(length(new_overlay_types)))
        {
            if (Dm.truthy(atmos_overlay_types))
                Dm.append(vis_contents, Dm.sub(new_overlay_types, atmos_overlay_types)); //don't add overlays that already exist
            else
                Dm.append(vis_contents, new_overlay_types);
        }

        if (!Dm.truthy(length(new_overlay_types))) new_overlay_types = null; // UNSETEMPTY(new_overlay_types)
        this.atmos_overlay_types = new_overlay_types;
    }
}



// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:125  (interleaved types, rules 1.9)
public partial class turf : atom
{
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:125
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(Dm.num(exposed_temperature) >= heat_capacity || Dm.truthy(to_be_destroyed));
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:128
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        if (Dm.num(exposed_temperature) >= heat_capacity)
            to_be_destroyed = TRUE;
        if (Dm.truthy(to_be_destroyed) && Dm.num(exposed_temperature) >= max_fire_temperature_sustained)
            max_fire_temperature_sustained = min(Dm.num(exposed_temperature), max_fire_temperature_sustained + heat_capacity / 4); //Ramp up to 100% yeah?
        if (Dm.truthy(to_be_destroyed) && !Dm.truthy(changing_turf))
            await burn_turf();
        return null;
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:137
    public virtual async DmTask burn_turf()
    {
        burn_tile();
        float chance_of_deletion;
        if (Dm.truthy(heat_capacity)) //beware of division by zero
            chance_of_deletion = max_fire_temperature_sustained / heat_capacity * 8; //there is no problem with prob(23456), min() was redundant --rastaf0
        else
            chance_of_deletion = 100;
        if (prob(chance_of_deletion))
        {
            await Melt();
            max_fire_temperature_sustained = 0;
        }
        else
            to_be_destroyed = FALSE;
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:149
    public override async DmTask<object> temperature_expose(datum_gas_mixture air = null, object exposed_temperature = null, object exposed_volume = null)
    {
        await atmos_expose(air, exposed_temperature);
        return null;
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:152  (interleaved types, rules 1.9)
public partial class turf_open : turf
{
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:152
    public override async DmTask<object> temperature_expose(datum_gas_mixture air = null, object exposed_temperature = null, object exposed_volume = null)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_TURF_EXPOSE]))
            _SendSignal(COMSIG_TURF_EXPOSE, Dm.list(this, air, exposed_temperature));
        check_atmos_process(this, air, exposed_temperature); //Manually do this to avoid needing to use elements, don't want 200 second atom init times
        return null;
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:157  (interleaved types, rules 1.9)
public partial class turf : atom
{
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:157
    public virtual void archive()
    {
        temperature_archived = temperature;
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:160  (interleaved types, rules 1.9)
public partial class turf_open : turf
{
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:160
    public override void archive()
    {
        LINDA_CYCLE_ARCHIVE(this);
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:193 (global procs)
public static partial class GlobalProcs
{
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:193
    public static object typecache_of_gases_with_no_overlays()
    {
        DmList dot = Dm.list();
        foreach (object gastype in Dm.each(subtypesof(typeof(datum_gas))))
        {
            if (!Dm.truthy(Dm.initial(gastype, (datum_gas p) => p.gas_overlay)))
                dot[gastype] = TRUE;
        }
        return dot;
    }
}

/////////////////////////////SIMULATION///////////////////////////////////

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:250  (interleaved types, rules 1.9)
public partial class turf : atom
{
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:250
    public virtual async DmTask process_cell(object fire_count = null)
    {
        SSair.remove_from_active(this);
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:253  (interleaved types, rules 1.9)
public partial class turf_open : turf
{
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:209-246  #define LAST_SHARE_CHECK and PLANET_SHARE_CHECK
    // (statements reading and writing the caller's locals, rules 11.1: expanded inline at each use); the TRACK_MAX_SHARE
    // forms are inactive in production (rules 1.7).

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:253
    public override async DmTask process_cell(object fire_count = null)
    {
        if (archived_cycle < Dm.num(fire_count)) //archive self if not already done
            LINDA_CYCLE_ARCHIVE(this);

        current_cycle = Dm.num(fire_count);
        float cached_ticker = significant_share_ticker;
        cached_ticker += 1;

        //cache for sanic speed
        DmList adjacent_turfs = atmos_adjacent_turfs;
        datum_excited_group our_excited_group = excited_group;
        float our_share_coeff = 1 / (LAZYLEN(adjacent_turfs) + 1);

        datum_gas_mixture our_air = air;

        DmList share_end = null;

        foreach (turf_open enemy_tile in Dm.each<turf_open>(adjacent_turfs))
        {
            // :274-278  #ifdef UNIT_TESTS (a closed turf inside of adjacent turfs) #endif: inactive in production (rules 1.7)

            // This var is only rarely set, exists so turfs can request to share at the end of our sharing
            // We need this so we can assume share is communative, which we need to do to avoid a hellish amount of garbage_collect()s
            if (Dm.truthy(enemy_tile.run_later))
                LAZYADD(ref share_end, enemy_tile);

            if (Dm.num(fire_count) <= enemy_tile.current_cycle)
                continue;
            LINDA_CYCLE_ARCHIVE(enemy_tile);

            /******************* GROUP HANDLING START *****************************************************************/

            bool should_share_air = false;
            datum_gas_mixture enemy_air = enemy_tile.air;

            //cache for sanic speed
            datum_excited_group enemy_excited_group = enemy_tile.excited_group;
            //If we are both in an excited group, and they aren't the same, merge.
            //If we are both in an excited group, and you're active, share
            //If we pass compare, and if we're not already both in a group, lets join up
            //If we both pass compare, add to active and share
            if (our_excited_group != null && enemy_excited_group != null)
            {
                if (our_excited_group != enemy_excited_group)
                {
                    //combine groups (this also handles updating the excited_group var of all involved turfs)
                    our_excited_group.merge_groups(enemy_excited_group);
                    our_excited_group = excited_group; //update our cache
                }
            }
            if (our_excited_group != null && enemy_excited_group != null && Dm.truthy(enemy_tile.excited)) //If you're both excited, no need to compare right?
                should_share_air = true;
            else if (Dm.truthy(our_air.compare(enemy_air, /*cmp_archive = */ TRUE))) //Lets see if you're up for it
            {
                SSair.add_to_active(enemy_tile); //Add yourself young man
                datum_excited_group existing_group = our_excited_group ?? enemy_excited_group ?? Dm.@new<datum_excited_group>();
                if (our_excited_group == null)
                    existing_group.add_turf(this);
                if (enemy_excited_group == null)
                    existing_group.add_turf(enemy_tile);
                our_excited_group = excited_group;
                should_share_air = true;
            }

            //air sharing
            if (should_share_air)
            {
                float difference = Dm.num(our_air.share(enemy_air, our_share_coeff, 1 / (LAZYLEN(enemy_tile.atmos_adjacent_turfs) + 1)));
                if (difference != 0)
                {
                    if (difference > 0)
                        consider_pressure_difference(enemy_tile, difference);
                    else
                        enemy_tile.consider_pressure_difference(this, -difference);
                }
                //This acts effectivly as a very slow timer, the max deltas of the group will slowly lower until it breaksdown, they then pop up a bit, and fall back down until irrelevant
                // LAST_SHARE_CHECK (:226)
                float last_share = our_air.last_share;
                if (last_share > MINIMUM_AIR_TO_SUSPEND)
                {
                    our_excited_group.reset_cooldowns();
                    cached_ticker = 0;
                    enemy_tile.significant_share_ticker = 0;
                }
                else if (last_share > MINIMUM_MOLES_DELTA_TO_MOVE)
                {
                    our_excited_group.dismantle_cooldown = 0;
                    cached_ticker = 0;
                    enemy_tile.significant_share_ticker = 0;
                }
            }

            /******************* GROUP HANDLING FINISH *********************************************************************/
        }

        if (Dm.truthy(planetary_atmos)) //share our air with the "atmosphere" "above" the turf
        {
            datum_gas_mixture planetary_mix = SSair.planetary[initial_gas_mix] as datum_gas_mixture;
            // archive ourself again so we don't accidentally share more gas than we currently have
            LINDA_CYCLE_ARCHIVE(this);
            if (Dm.truthy(our_air.compare(planetary_mix, /*cmp_archive = */ TRUE)))
            {
                if (our_excited_group == null)
                {
                    datum_excited_group new_group = Dm.@new<datum_excited_group>();
                    new_group.add_turf(this);
                    our_excited_group = excited_group;
                }
                // shares 4/5 of our difference in moles with the atmosphere
                our_air.share(planetary_mix, 0.8f, 0.8f);
                // temperature share with the atmosphere with an inflated heat capacity to simulate faster sharing with a large atmosphere
                our_air.temperature_share(planetary_mix, OPEN_HEAT_TRANSFER_COEFFICIENT, planetary_mix.temperature_archived, Dm.num(planetary_mix.heat_capacity()) * 5);
                planetary_mix.garbage_collect();
                // PLANET_SHARE_CHECK (:245)
                float last_share = our_air.last_share;
                if (last_share > MINIMUM_AIR_TO_SUSPEND)
                {
                    our_excited_group.reset_cooldowns();
                    cached_ticker = 0;
                }
                else if (last_share > MINIMUM_MOLES_DELTA_TO_MOVE)
                {
                    our_excited_group.dismantle_cooldown = 0;
                    cached_ticker = 0;
                }
            }
        }

        foreach (turf_open enemy_tile in Dm.each<turf_open>(share_end))
        {
            datum_gas_mixture enemy_mix = enemy_tile.air;
            archive();
            // We share 100% of our mix in this step. Let's jive
            float difference = Dm.num(our_air.share(enemy_mix, 1, 1));
            // LAST_SHARE_CHECK (:226)
            // DEVIATION(DV84): the turf can reach this loop with no excited group: its neighbours all processed earlier in this cycle
            // (the main loop skips them, :301) or their processing killed its group (remove_from_active, air.dm:475), and the space turf
            // it shares with here is one of them, so no compare ever made a group. tg's macro then calls reset_cooldowns() on null: a
            // BYOND runtime that ends the proc before react(), update_visuals() and the turf's sleep (:358-368). The port keeps going.
            float last_share = our_air.last_share;
            if (last_share > MINIMUM_AIR_TO_SUSPEND)
            {
                our_excited_group?.reset_cooldowns();
                cached_ticker = 0;
                enemy_tile.significant_share_ticker = 0;
            }
            else if (last_share > MINIMUM_MOLES_DELTA_TO_MOVE)
            {
                if (our_excited_group != null)
                    our_excited_group.dismantle_cooldown = 0;
                cached_ticker = 0;
                enemy_tile.significant_share_ticker = 0;
            }
            if (difference == 0)
                continue;
            if (difference > 0)
                consider_pressure_difference(enemy_tile, difference);
            else
                enemy_tile.consider_pressure_difference(this, difference);
        }

        object reacting = await our_air.react(this);
        if (our_excited_group != null)
            Dm.combine(ref our_excited_group.turf_reactions, reacting); //Adds the flag to turf_reactions so excited groups can check for them before dismantling.

        update_visuals();
        if (!Dm.truthy(consider_superconductivity(starting: TRUE)) && !Dm.truthy(active_hotspot) && !Dm.truthy(Dm.bitand(reacting, Dm.bitor(REACTING, STOP_REACTIONS))))
        {
            if (our_excited_group == null) //If nothing of interest is happening, kill the active turf
                SSair.remove_from_active(this); //This will kill any connected excited group, be careful (This broke atmos for 4 years)
            if (cached_ticker > EXCITED_GROUP_DISMANTLE_CYCLES) //If you're stalling out, take a rest
                SSair.sleep_active_turf(this);
        }

        significant_share_ticker = cached_ticker; //Save our changes
        await temperature_expose(our_air, our_air.temperature);
    }

    //////////////////////////SPACEWIND/////////////////////////////

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:377
    public virtual void consider_pressure_difference(turf target_turf = null, object difference = null)
    {
        Dm.combine(ref SSair.high_pressure_delta, this);
        if (Dm.num(difference) > pressure_difference)
        {
            pressure_direction = get_dir(this, target_turf);
            airflow_vertical = target_turf != null && !Dm.eq(target_turf.z, z) ? (int)Dm.num(get_dir_multiz(this, target_turf)) & (int)Dm.bitor(UP, DOWN) : 0; // DEVIATION(DV34)
            pressure_difference = Dm.num(difference);
        }
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:383
    public virtual void high_pressure_movements()
    {
        // DEVIATION(DV179): an exposed ceiling's outlet blows UP (CeilingSystem.Atmos); its wind is only drawn: a step UP would
        // carry the mobs under it onto the next z-level (the owner, 2026-10-26: flung into the space of z 2 patching a hole)
        if (Dm.truthy(Dm.bitand(pressure_direction, Dm.bitor(UP, DOWN))))
            return;
        atom_movable moving_atom;
        foreach (object thing in Dm.each(this))
        {
            moving_atom = thing as atom_movable;
            if (moving_atom.last_high_pressure_movement_air_cycle < SSair.times_fired)
            {
                if (!Dm.truthy(moving_atom.anchored) && !Dm.truthy(moving_atom.pulledby))
                    moving_atom.experience_pressure_difference(pressure_difference, pressure_direction);
                else if (Dm.truthy(moving_atom._listen_lookup?[COMSIG_MOVABLE_RESISTED_SPACEWIND]))
                    moving_atom._SendSignal(COMSIG_MOVABLE_RESISTED_SPACEWIND, Dm.list(moving_atom, pressure_difference, pressure_direction));
            }
        }
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:394
public partial class atom_movable : atom
{
    ///How much delta pressure is needed for us to move
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:395
    public float pressure_resistance = 10;
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:396
    public float last_high_pressure_movement_air_cycle = 0;

    /// <summary>DEVIATION(DV412): the world.time of the wind's last push; a drift that starts within <see cref="BlownWindow"/> of it is blown (datum_drift_handler.dm_blown).</summary>
    public float dm_blown_at = -1000;

    /// <summary>DV416: the tile and way (<c>FlightSeam.StopKey</c>) where its slide last met something it could not enter; -1: none.</summary>
    public long dm_slide_stop = -1;

    /// <summary>DV412: how long after a push (deciseconds) a drift counts as the wind's.</summary>
    public const float BlownWindow = 20;

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:398  (set waitfor = FALSE: nothing below sleeps)
    public virtual void experience_pressure_difference(object pressure_difference = null, object direction = null, float? pressure_resistance_prob_delta = null)
    {
        pressure_resistance_prob_delta ??= 0;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_PRE_PRESSURE_PUSH]) ? NONE : this._SendSignal(COMSIG_ATOM_PRE_PRESSURE_PUSH, Dm.list(this))), COMSIG_ATOM_BLOCKS_PRESSURE)))
            return;
        const float PROBABILITY_OFFSET = 25;
        const float PROBABILITY_BASE_PRECENT = 75;
        float max_force = sqrt(Dm.num(pressure_difference)) * (MOVE_FORCE_DEFAULT / 5);
        float move_prob = 100;
        if (pressure_resistance > 0)
            move_prob = (Dm.num(pressure_difference) / pressure_resistance * PROBABILITY_BASE_PRECENT) - PROBABILITY_OFFSET;
        move_prob += Dm.num(pressure_resistance_prob_delta);
        // DEVIATION(DV357): a walking capsule mob is carried by the wind at tg's mean pace (no roll) instead of a step
        bool carried = this is mob_living && move_prob > PROBABILITY_OFFSET && (move_resist != INFINITY) && !Dm.truthy(anchored) && (max_force >= (move_resist * MOVE_FORCE_PUSH_RATIO))
            && LocomotionSeam.Blow(this, Dm.num(direction), Dm.num(pressure_difference), move_prob);
        // DEVIATION(DV416): a thing that is no mob slides along the floor at the step's mean pace (no roll); friction stops it
        if (!carried && this is not mob && move_prob > PROBABILITY_OFFSET && (move_resist != INFINITY) && !Dm.truthy(anchored) && (max_force >= (move_resist * MOVE_FORCE_PUSH_RATIO)))
            carried = FlightSeam.Push(this, Dm.num(direction), move_prob);
        if (carried)
        {
            last_high_pressure_movement_air_cycle = SSair.times_fired;
            dm_blown_at = Dm.num(world.time);
        }
        else if (move_prob > PROBABILITY_OFFSET && prob(move_prob) && (move_resist != INFINITY) && (!Dm.truthy(anchored) && (max_force >= (move_resist * MOVE_FORCE_PUSH_RATIO))) || (Dm.truthy(anchored) && (max_force >= (move_resist * MOVE_FORCE_FORCEPUSH_RATIO))))
        {
            dm_blown_at = Dm.num(world.time); // DV412: before the step, whose Moved may start the drift
            Waiting.NoSleep(step(this, Dm.num(direction)));
            last_high_pressure_movement_air_cycle = SSair.times_fired;
        }
    }
}

///////////////////////////EXCITED GROUPS/////////////////////////////

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:415  (the var block is generated, D111)
[DmPath("/datum/excited_group")]
public partial class datum_excited_group : datum
{
    ///Wrapping loop of the index colors
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:427  var/static/wrapping_id = 0 (rules 10.6: a TgState field)
    public static ref float wrapping_id => ref TgState.Current.datum_excited_group__wrapping_id;

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:431
    public override async DmTask New(params object[] __extra)
    {
        Dm.append(ref SSair.excited_groups, this);
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:434
    public virtual void add_turf(turf_open target_turf = null)
    {
        Dm.append(ref turf_list, target_turf);
        target_turf.excited_group = this;
        dismantle_cooldown = 0;
        if (Dm.truthy(should_display) || Dm.truthy(SSair.display_all_groups))
            display_turf(target_turf);
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:441
    public virtual void merge_groups(datum_excited_group target_group = null)
    {
        if (turf_list.len > target_group.turf_list.len)
        {
            Dm.remove(ref SSair.excited_groups, target_group);
            foreach (turf_open group_member in Dm.each<turf_open>(target_group.turf_list))
            {
                group_member.excited_group = this;
                Dm.append(ref turf_list, group_member);
            }
            should_display = Dm.bitor(target_group.should_display, should_display);
            if (Dm.truthy(should_display) || Dm.truthy(SSair.display_all_groups))
            {
                target_group.hide_turfs();
                display_turfs();
            }
            breakdown_cooldown = min(breakdown_cooldown, target_group.breakdown_cooldown); //Take the smaller of the two options
            dismantle_cooldown = 0;
        }
        else
        {
            Dm.remove(ref SSair.excited_groups, this);
            foreach (turf_open group_member in Dm.each<turf_open>(turf_list))
            {
                group_member.excited_group = target_group;
                Dm.append(ref target_group.turf_list, group_member);
            }
            target_group.should_display = Dm.bitor(target_group.should_display, should_display);
            if (Dm.truthy(target_group.should_display) || Dm.truthy(SSair.display_all_groups))
            {
                hide_turfs();
                target_group.display_turfs();
            }
            target_group.breakdown_cooldown = min(breakdown_cooldown, target_group.breakdown_cooldown);
            target_group.dismantle_cooldown = 0;
        }
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:467
    public virtual void reset_cooldowns()
    {
        breakdown_cooldown = 0;
        dismantle_cooldown = 0;
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:471
    public virtual void self_breakdown(float? roundstart = null, float? poke_turfs = null)
    {
        roundstart ??= FALSE;
        poke_turfs ??= FALSE;
        datum_gas_mixture shared_mix = Dm.@new<datum_gas_mixture>();

        //make local for sanic speed
        DmList shared_cached_moles = shared_mix.moles;
        DmList turf_list = this.turf_list;
        float turflen = turf_list.len;
        bool imumutable_in_group = false;
        float energy = 0;
        float heat_cap = 0;

        foreach (turf_open group_member in Dm.each<turf_open>(turf_list))
        {
            //Cache?
            datum_gas_mixture mix = group_member.air;
            if (Dm.truthy(roundstart))
            {
                if (group_member.air is datum_gas_mixture_immutable)
                {
                    imumutable_in_group = true;
                    shared_mix.copy_from(group_member.air); //This had better be immutable young man
                    shared_cached_moles = shared_mix.moles; //update the cache
                    break;
                }
                // If we're planetary use THAT mix, and stop here
                if (Dm.truthy(group_member.planetary_atmos))
                {
                    imumutable_in_group = true;
                    datum_gas_mixture planetary_mix = SSair.planetary[group_member.initial_gas_mix] as datum_gas_mixture;
                    shared_mix.copy_from(planetary_mix);
                    shared_cached_moles = shared_mix.moles; // Cache update
                    break;
                }
            }
            //"borrowing" this code from merge(), I need to play with the temp portion. Lets expand it out
            //temperature = (giver.temperature * giver_heat_capacity + temperature * self_heat_capacity) / combined_heat_capacity
            float capacity = Dm.num(mix.heat_capacity());
            energy += mix.temperature * capacity;
            heat_cap += capacity;

            foreach ((object gas_id, object amount) in Dm.pairs(mix.moles))
                shared_cached_moles[gas_id] = Dm.num(shared_cached_moles[gas_id]) + Dm.num(amount);
        }

        if (!imumutable_in_group)
        {
            shared_mix.temperature = energy / heat_cap;
            foreach (object gas_id in Dm.each(shared_cached_moles))
                shared_cached_moles[gas_id] = Dm.num(shared_cached_moles[gas_id]) / turflen;
            shared_mix.garbage_collect();
        }

        foreach (turf_open group_member in Dm.each<turf_open>(turf_list))
        {
            if (Dm.truthy(group_member.planetary_atmos)) //We do this as a hack to try and minimize unneeded excited group spread over planetary turfs
                group_member.air.copy_from(SSair.planetary[group_member.initial_gas_mix] as datum_gas_mixture); //Comes with a cost of "slower" drains, but it's worth it
            else
                group_member.air.copy_from(shared_mix); //Otherwise just set the mix to a copy of our equalized mix
            group_member.update_visuals();
            if (Dm.truthy(poke_turfs)) //Because we only activate all these once every breakdown, in event of lag due to this code and slow space + vent things, increase the wait time for breakdowns
            {
                SSair.add_to_active(group_member);
                group_member.significant_share_ticker = EXCITED_GROUP_DISMANTLE_CYCLES; //Max out the ticker, if they don't share next tick, nuke em
            }
        }

        breakdown_cooldown = 0;
    }

    ///Dismantles the excited group, puts allll the turfs to sleep
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:524
    public virtual void dismantle()
    {
        foreach (turf_open current_turf in Dm.each<turf_open>(turf_list))
        {
            current_turf.excited = FALSE;
            current_turf.significant_share_ticker = 0;
            Dm.remove(ref SSair.active_turfs, current_turf);
            // :529-531  #ifdef VISUALIZE_ACTIVE_TURFS #endif: inactive in production (rules 1.7)
        }
        garbage_collect();
    }

    //Breaks down the excited group, this doesn't sleep the turfs mind, just removes them from the group
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:535
    public virtual void garbage_collect()
    {
        if (Dm.truthy(display_id)) //If we ever did make those changes
            hide_turfs();
        foreach (turf_open current_turf in Dm.each<turf_open>(turf_list))
            current_turf.excited_group = null;
        turf_list.Cut();
        Dm.remove(ref SSair.excited_groups, this);
        if (SSair.currentpart == SSAIR_EXCITEDGROUPS)
            Dm.remove(ref SSair.currentrun, this);
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:545
    public virtual void display_turfs()
    {
        // The colored overlays of GLOB.colored_turfs (setup_turf_visuals) are the admin atmos control panel's debug display;
        // should_display and SSair.display_all_groups are set only from that panel (ui_act, M10), so no group shows.
        Dm.Deferred("code/modules/atmospherics/environmental/LINDA_turf_tile.dm:545", "display_turfs: the excited groups' debug overlays (the admin atmos control panel, M10+)");
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:555
    public virtual void hide_turfs()
    {
        Dm.Deferred("code/modules/atmospherics/environmental/LINDA_turf_tile.dm:555", "hide_turfs: the excited groups' debug overlays (the admin atmos control panel, M10+)");
        display_id = 0;
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:562
    public virtual void display_turf(turf thing = null)
    {
        Dm.Deferred("code/modules/atmospherics/environmental/LINDA_turf_tile.dm:562", "display_turf: the excited groups' debug overlays (the admin atmos control panel, M10+)");
    }
}

public sealed partial class TgState
{
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:427  /datum/excited_group/var/static/wrapping_id
    internal float datum_excited_group__wrapping_id;
}

////////////////////////SUPERCONDUCTIVITY/////////////////////////////

/**
ALLLLLLLLLLLLLLLLLLLLRIGHT HERE WE GOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOO

Read the code for more details, but first, a brief concept discussion/area

Our goal here is to "model" heat moving through solid objects, so walls, windows, and sometimes doors.
We do this by heating up the floor itself with the heat of the gasmix ontop of it, this is what the coeffs are for here, they slow that movement
Then we go through the process below.

If an active turf is fitting, we add it to processing, conduct with any covered tiles, (read windows and sometimes walls)
Then we space some of our heat, and think about if we should stop conducting.
**/

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:585  (interleaved types, rules 1.9)
public partial class turf : atom
{
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:585
    public virtual object conductivity_directions()
    {
        if (archived_cycle < SSair.times_fired)
            archive();
        return ALL_CARDINALS;
    }

    ///These two procs are a bit of a web, I belive in you
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:600
    public virtual async DmTask neighbor_conduct_with_src(turf other = null)
    {
        turf_open open_other = other as turf_open;
        if (!Dm.truthy(other.blocks_air)) //Solid but neighbor is open
            open_other.temperature_share_open_to_solid(this);
        else //Both tiles are solid
            other.share_temperature_mutual_solid(this, thermal_conductivity);
        await temperature_expose(null, temperature);
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:618
    public virtual async DmTask super_conduct()
    {
        object conductivity_directions = this.conductivity_directions();

        if (Dm.truthy(conductivity_directions))
        {
            //Conduct with tiles around me
            foreach (object direction in Dm.each(GLOB.cardinals))
            {
                if (!Dm.truthy(Dm.bitand(conductivity_directions, direction)))
                    continue;
                turf neighbor = get_step(this, Dm.num(direction)) as turf;

                if (!Dm.truthy(neighbor.thermal_conductivity))
                    continue;

                if (neighbor.archived_cycle < SSair.times_fired)
                    neighbor.archive();

                await neighbor.neighbor_conduct_with_src(this);

                neighbor.consider_superconductivity();
            }
        }

        radiate_to_spess();

        finish_superconduction();
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:642
    public virtual object finish_superconduction(object temp = null)
    {
        temp ??= temperature;
        //Make sure still hot enough to continue conducting heat
        if (Dm.num(temp) < MINIMUM_TEMPERATURE_FOR_SUPERCONDUCTION)
        {
            Dm.remove(ref SSair.active_super_conductivity, this);
            return FALSE;
        }
        return null;
    }

    ///Should we attempt to superconduct?
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:654
    public virtual object consider_superconductivity(object starting = null)
    {
        if (!Dm.truthy(thermal_conductivity))
            return FALSE;

        Dm.combine(ref SSair.active_super_conductivity, this);
        return TRUE;
    }

    /// Radiate excess tile heat to space.
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:674
    public virtual void radiate_to_spess()
    {
        if (temperature <= T0C) // Considering 0 degC as the break even point for radiation in and out.
            return;
        // Because we keep losing energy, makes more sense for us to be the T2 here.
        float delta_temperature = Dm.num(temperature_archived) - TCMB; //hardcoded space temperature
        if (heat_capacity <= 0 || abs(delta_temperature) <= MINIMUM_TEMPERATURE_DELTA_TO_CONSIDER)
            return;
        // Heat should be positive in most cases
        // coefficient applied first because some turfs have very big heat caps.
        float heat = CALCULATE_CONDUCTION_ENERGY(thermal_conductivity * delta_temperature, HEAT_CAPACITY_VACUUM, heat_capacity);
        temperature -= heat / heat_capacity;
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:689
    public virtual void share_temperature_mutual_solid(turf sharer = null, object conduction_coefficient = null) //This is all just heat sharing, don't get freaked out
    {
        float delta_temperature = Dm.num(sharer.temperature_archived) - Dm.num(temperature_archived);
        if (abs(delta_temperature) <= MINIMUM_TEMPERATURE_DELTA_TO_CONSIDER || !Dm.truthy(heat_capacity) || !Dm.truthy(sharer.heat_capacity))
            return;
        float heat = Dm.num(conduction_coefficient) * CALCULATE_CONDUCTION_ENERGY(delta_temperature, heat_capacity, sharer.heat_capacity);
        temperature += heat / heat_capacity; //The higher your own heat cap the less heat you get from this arrangement
        sharer.temperature -= heat / sharer.heat_capacity;
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:590  (interleaved types, rules 1.9)
public partial class turf_open : turf
{
    ///Returns a set of directions that we should be conducting in, NOTE, atmos_supeconductivity is ACTUALLY inversed, don't worrry about it
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:591
    public override object conductivity_directions()
    {
        if (Dm.truthy(blocks_air))
            return base.conductivity_directions();
        object dot = null;
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            turf checked_turf = get_step(this, Dm.num(direction)) as turf;
            if (!Dm.@in(checked_turf, atmos_adjacent_turfs) && !Dm.truthy(Dm.bitand(atmos_supeconductivity, direction)))
                dot = Dm.bitor(dot, direction);
        }
        return dot;
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:607
    public override async DmTask neighbor_conduct_with_src(turf other = null)
    {
        if (Dm.truthy(blocks_air))
        {
            await base.neighbor_conduct_with_src(other);
            return;
        }

        if (!Dm.truthy(other.blocks_air)) //Both tiles are open
        {
            turf_open open_other = other as turf_open;
            open_other.air.temperature_share(air, WINDOW_HEAT_TRANSFER_COEFFICIENT);
        }
        else //Open but neighbor is solid
            temperature_share_open_to_solid(other);
        SSair.add_to_active(this);
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:648
    public override object finish_superconduction(object temp = null)
    {
        //Conduct with air on my tile if I have it
        if (!Dm.eq(base.finish_superconduction(Dm.truthy(blocks_air) ? temperature : air.temperature), FALSE) && !Dm.truthy(blocks_air))
            temperature = Dm.num(air.temperature_share(null, thermal_conductivity, temperature, heat_capacity));
        return null;
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:662
    public override object consider_superconductivity(object starting = null)
    {
        if (air.temperature < (Dm.truthy(starting) ? MINIMUM_TEMPERATURE_START_SUPERCONDUCTION : MINIMUM_TEMPERATURE_FOR_SUPERCONDUCTION))
            return FALSE;
        if (Dm.num(air.heat_capacity()) < M_CELL_WITH_RATIO) // Was: MOLES_CELLSTANDARD*0.1*0.05 Since there are no variables here we can make this a constant.
            return FALSE;
        return base.consider_superconductivity(starting);
    }

    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:686
    public virtual void temperature_share_open_to_solid(turf sharer = null)
    {
        sharer.temperature = Dm.num(air.temperature_share(null, sharer.thermal_conductivity, sharer.temperature, sharer.heat_capacity));
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:669  (interleaved types, rules 1.9)
public partial class turf_closed : turf
{
    // tg: code/modules/atmospherics/environmental/LINDA_turf_tile.dm:669
    public override object consider_superconductivity(object starting = null)
    {
        if (temperature < (Dm.truthy(starting) ? MINIMUM_TEMPERATURE_START_SUPERCONDUCTION : MINIMUM_TEMPERATURE_FOR_SUPERCONDUCTION))
            return FALSE;
        return base.consider_superconductivity(starting);
    }
}
