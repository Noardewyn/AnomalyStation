// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/environmental/LINDA_fire.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 1 (the air that moves): the root /atom/proc/temperature_expose, which the turf sharing calls. M4b slice 3 (fire
// and plasma): the whole file. M4b audio slice 5: the hot group's looping fire sound (update_sound); M4b audio slice 6: the
// fire puff (:144).

public static partial class GlobalProcs
{
    /// Returns reactions which will contribute to a hotspot's size.
    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:2
    public static object init_hotspot_reactions()
    {
        DmList fire_reactions = Dm.list();
        foreach (Type reaction in Dm.each_cast<Type>(subtypesof(typeof(datum_gas_reaction_standard))))
        {
            if (Dm.truthy(Dm.initial(reaction, (datum_gas_reaction_standard p) => p.expands_hotspot)))
                Dm.append(fire_reactions, reaction);
        }

        return fire_reactions;
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_fire.dm:10
public partial class atom : datum
{
    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:10
    public virtual async DmTask<object> temperature_expose(datum_gas_mixture air = null, object exposed_temperature = null, object exposed_volume = null)
    {
        return null;
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_fire.dm:15
public partial class turf : atom
{
    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:15
    public virtual void hotspot_expose(object exposed_temperature = null, object exposed_volume = null, float? soh = null)
    {
        soh ??= 0;
        return;
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_fire.dm:23
public partial class turf_open : turf
{
    /**
     * Handles the creation of hotspots and initial activation of turfs.
     * Setting the conditions for the reaction to actually happen for gasmixtures
     * is handled by the hotspot itself, specifically perform_exposure().
     */
    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:23
    public override void hotspot_expose(object exposed_temperature = null, object exposed_volume = null, float? soh = null)
    {
        if (Dm.num(exposed_temperature) < TCMB)
        {
            exposed_temperature = TCMB;
            throw Dm.CRASH(Dm.text("[].hotspot_expose() called with exposed_temperature < []", this, TCMB), "code/modules/atmospherics/environmental/LINDA_fire.dm", 26);
        }
        //If the air doesn't exist we just return false
        DmList cached_moles = air?.moles;
        if (!Dm.truthy(cached_moles))
            return;

        if (Dm.num(cached_moles[typeof(datum_gas_oxygen)]) < 0.5f)
            return;

        bool plas_trit_h2_threshold = (
               Dm.num(cached_moles[typeof(datum_gas_plasma)]) > 0.5f
            || Dm.num(cached_moles[typeof(datum_gas_tritium)]) > 0.5f
            || Dm.num(cached_moles[typeof(datum_gas_hydrogen)]) > 0.5f);
        bool freon_threshold = (Dm.num(cached_moles[typeof(datum_gas_freon)]) > 0.5f);
        if (Dm.truthy(active_hotspot))
        {
            if (Dm.truthy(soh))
            {
                if (plas_trit_h2_threshold)
                {
                    if (active_hotspot.temperature < Dm.num(exposed_temperature))
                        active_hotspot.temperature = Dm.num(exposed_temperature);
                    if (Dm.num(active_hotspot.volume) < Dm.num(exposed_volume))
                        active_hotspot.volume = Dm.num(exposed_volume);
                }
                else if (freon_threshold)
                {
                    if (active_hotspot.temperature > Dm.num(exposed_temperature))
                        active_hotspot.temperature = Dm.num(exposed_temperature);
                    if (Dm.num(active_hotspot.volume) < Dm.num(exposed_volume))
                        active_hotspot.volume = Dm.num(exposed_volume);
                }
            }
            return;
        }

        if (((Dm.num(exposed_temperature) > PLASMA_MINIMUM_BURN_TEMPERATURE) && plas_trit_h2_threshold) ||
            ((Dm.num(exposed_temperature) < FREON_MAXIMUM_BURN_TEMPERATURE) && freon_threshold))
        {
            Dm.@new<obj_effect_hotspot>(this, Dm.num(exposed_volume) * 25, exposed_temperature);
            SSair.add_to_active(this);
        }
    }
}

/**
 * Hotspot objects interfaces with the temperature of turf gasmixtures while also providing visual effects.
 * One important thing to note about hotspots are that they can roughly be divided into two categories based on the bypassing variable.
 */
// tg: code/modules/atmospherics/environmental/LINDA_fire.dm:64
[DmPath("/obj/effect/hotspot")]
public partial class obj_effect_hotspot : obj_effect
{
    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:137  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_effect_hotspot__Initialize__loc_connections;

    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:98
    [DmParams("mapload", "starting_volume", "starting_temperature")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object starting_volume = Dm.arg(__extra, 0);
        object starting_temperature = Dm.arg(__extra, 1);
        object dot = base.Initialize(mapload, Dm.forward(__extra, starting_volume, starting_temperature));
        Dm.append(SSair.hotspots, this);
        if (starting_volume != null)
            volume = Dm.num(starting_volume);
        if (starting_temperature != null)
        {
            temperature = Dm.num(starting_temperature);
            if (temperature <= FREON_MAXIMUM_BURN_TEMPERATURE)
                cold_fire = TRUE;
        }

        turf our_turf = loc as turf; // tg types it open; a hotspot made on a wall (a drake's meteor) reads the wall's empty list
        //on creation we check adjacent turfs for hot spot to start grouping, if surrounding do not have hot spots we create our own
        foreach (turf_open to_check in Dm.each_cast<turf_open>(our_turf.atmos_adjacent_turfs))
        {
            if (!Dm.truthy(to_check.active_hotspot))
                continue;
            obj_effect_hotspot enemy_spot = to_check.active_hotspot;
            // Safeguard to prevent infectious init runtimes for hotspots if we somehow end up with a hotspot without a group
            if (!Dm.truthy(enemy_spot.our_hot_group))
                continue;
            if (!Dm.truthy(our_hot_group))
                enemy_spot.our_hot_group.add_to_group(this);
            else if (our_hot_group != enemy_spot.our_hot_group) //if we belongs to a hot group from prior loop and we encounter another hot spot with a group then we merge
                our_hot_group.merge_hot_groups(enemy_spot.our_hot_group);
        }

        if (QDELETED(our_hot_group))//if after loop through all the adjacents turfs and we havent belong to a group yet, make our own
        {
            our_hot_group = Dm.@new<datum_hot_group>();
            our_hot_group.add_to_group(this);
        }

        // If our hotspot gets created on a turf with existing hotspots on it that just got spawned, abort
        if (!Dm.truthy(Waiting.NoSleep(perform_exposure())))
        {
            if (QDELETED(this))
                return dot;
            return INITIALIZE_HINT_QDEL;
        }

        if (QDELETED(this)) // It is actually possible for this hotspot to become qdeleted in perform_exposure() if another hotspot gets created (for example in fire_act() of fuel pools)
            return dot; // In this case, we want to just leave and let the new hotspot take over.

        Waiting.NoSleep(setDir(pick(GLOB.cardinals)));
        air_update_turf(FALSE, FALSE);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));

        turf_open open_turf = (turf_open)our_turf; // the exposure above only goes on over an open turf's air
        if (open_turf.fire_puff_cooldown <= world.time) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        {
            playsound(open_turf, Dm.resource("sound/effects/fire_puff.ogg"), 30); // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:144
            open_turf.fire_puff_cooldown = world.time + 5 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
        }

        // Remove just_spawned protection if no longer processing the parent cell
        just_spawned = (our_turf.current_cycle < SSair.times_fired) ? TRUE : FALSE;
        update_color();
        return dot;
    }

    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:151
    public override object set_smoothed_icon_state(object new_junction = null)
    {
        smoothing_junction = Dm.num(new_junction);

        update_color();
        return null;
    }

    /**
     * Perform interactions between the hotspot and the gasmixture.
     *
     * For the first tick, hotspots will take a sample of the air in the turf,
     * set the temperature equal to a certain amount, and then reacts it.
     * In some implementations the ratio comes out to around 1, so all of the air in the turf.
     *
     * Afterwards if the reaction is big enough it mostly just tags along the fire,
     * copying the temperature and handling the colouring.
     * If the reaction is too small it will perform like the first tick.
     *
     * Also calls fire_act() which handles burning.
     * Returns TRUE if exposed successfully, and FALSE if the hotspot should delete itself
     */
    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:171
    public virtual async DmTask<object> perform_exposure()
    {
        turf_open location = loc as turf_open;
        datum_gas_mixture reference = null;
        if (location == null || !Dm.truthy(location.air))
            return FALSE;

        if (Dm.truthy(location.active_hotspot) && location.active_hotspot != this)
        {
            // If we're attempting to spawn on a turf which *just* had a hotspot spawned on it, abort and kill ourselves
            if (Dm.truthy(location.active_hotspot.just_spawned))
                return FALSE;
            // When we are spawned from a deletion signal from our previous hotspot, this can happen
            if (!QDELETED(location.active_hotspot))
                qdel(location.active_hotspot);
        }
        location.active_hotspot = this;

        bypassing = (!Dm.truthy(just_spawned) && (Dm.num(volume) > CELL_VOLUME * 0.95f)) ? TRUE : FALSE;

        //Passive mode
        if (Dm.truthy(bypassing) || Dm.truthy(cold_fire))
            reference = location.air; // Our color and volume will depend on the turf's gasmix
        //Active mode
        else
        {
            datum_gas_mixture affected = location.air.remove_ratio(Dm.num(volume) / Dm.num(location.air.volume)) as datum_gas_mixture;
            if (Dm.truthy(affected)) //in case volume is 0
            {
                reference = affected; // Our color and volume will depend on this small sparked gasmix
                affected.temperature = temperature;
                await affected.react(this);
                await location.assume_air(affected);
            }
        }

        if (Dm.truthy(reference))
        {
            volume = 0;
            DmList cached_results = reference.reaction_results;
            foreach (object reaction in Dm.each(SSair.hotspot_reactions))
                volume += Dm.num(cached_results[reaction]) * FIRE_GROWTH_RATE;
            temperature = reference.temperature;
        }

        // Handles the burning of atoms.
        if (Dm.truthy(cold_fire))
            return TRUE;

        foreach (object A in Dm.each(location.contents))
        {
            atom AT = A as atom;
            if (!QDELETED(AT) && AT != this)
            {
                // DEVIATION(DV27): the interaction guard (D132) on fire reaching an atom: a type whose own fire_act is not
                // translated does nothing (its parent's fire_act does not run in its place).
                if (InertSeam.MissingOverride(AT, "fire_act"))
                    continue;
                await AT.fire_act(temperature, volume);
            }
        }
        return TRUE;
    }

    /// Mathematics to be used for color calculation.
    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:218
    public virtual object gauss_lerp(object x = null, object x1 = null, object x2 = null)
    {
        float b = (Dm.num(x1) + Dm.num(x2)) * 0.5f;
        float c = (Dm.num(x2) - Dm.num(x1)) / 6;
        return Dm.pow(NUM_E, -(Dm.pow(Dm.num(x) - b, 2) / Dm.pow(2 * c, 2)));
    }

    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:223
    public virtual void update_color()
    {
        cut_overlays();

        float heat_r = Dm.num(heat2colour_r(temperature));
        float heat_g = Dm.num(heat2colour_g(temperature));
        float heat_b = Dm.num(heat2colour_b(temperature));
        float heat_a = 255;
        float greyscale_fire = 1; //This determines how greyscaled the fire is.

        if (Dm.truthy(cold_fire))
        {
            heat_r = 0;
            heat_g = LERP(255, temperature, 1.2f);
            heat_b = LERP(255, temperature, 0.9f);
            heat_a = 100;
        }
        else if (temperature < 5000) //This is where fire is very orange, we turn it into the normal fire texture here.
        {
            float normal_amt = Dm.num(gauss_lerp(temperature, 1000, 3000));
            heat_r = LERP(heat_r, 255, normal_amt);
            heat_g = LERP(heat_g, 255, normal_amt);
            heat_b = LERP(heat_b, 255, normal_amt);
            heat_a -= Dm.num(gauss_lerp(temperature, -5000, 5000)) * 128;
            greyscale_fire -= normal_amt;
        }
        if (temperature > 40000) //Past this temperature the fire will gradually turn a bright purple
        {
            float purple_amt = temperature < LERP(40000, 200000, 0.5f) ? Dm.num(gauss_lerp(temperature, 40000, 200000)) : 1;
            heat_r = LERP(heat_r, 255, purple_amt);
        }
        if (temperature > 200000 && temperature < 500000) //Somewhere at this temperature nitryl happens.
        {
            float sparkle_amt = Dm.num(gauss_lerp(temperature, 200000, 500000));
            mutable_appearance sparkle_overlay = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "shieldsparkles") as mutable_appearance;
            sparkle_overlay.blend_mode = BLEND_ADD;
            sparkle_overlay.alpha = sparkle_amt * 255;
            add_overlay(sparkle_overlay);
        }
        if (temperature > 400000 && temperature < 1500000) //Lightning because very anime.
        {
            mutable_appearance lightning_overlay = mutable_appearance(Dm.resource("icons/effects/fire.dmi"), "overcharged") as mutable_appearance;
            lightning_overlay.blend_mode = BLEND_ADD;
            add_overlay(lightning_overlay);
        }
        if (temperature > 4500000) //This is where noblium happens. Some fusion-y effects.
        {
            float fusion_amt = temperature < LERP(4500000, 12000000, 0.5f) ? Dm.num(gauss_lerp(temperature, 4500000, 12000000)) : 1;
            mutable_appearance fusion_overlay = mutable_appearance(Dm.resource("icons/effects/atmospherics.dmi"), "fusion_gas") as mutable_appearance;
            fusion_overlay.blend_mode = BLEND_ADD;
            fusion_overlay.alpha = fusion_amt * 255;
            mutable_appearance rainbow_overlay = mutable_appearance(Dm.resource("icons/hud/screen_gen.dmi"), "druggy") as mutable_appearance;
            rainbow_overlay.blend_mode = BLEND_ADD;
            rainbow_overlay.alpha = fusion_amt * 255;
            rainbow_overlay.appearance_flags = RESET_COLOR;
            heat_r = LERP(heat_r, 150, fusion_amt);
            heat_g = LERP(heat_g, 150, fusion_amt);
            heat_b = LERP(heat_b, 150, fusion_amt);
            add_overlay(fusion_overlay);
            add_overlay(rainbow_overlay);
        }

        set_light_color(rgb(LERP(250, heat_r, greyscale_fire), LERP(160, heat_g, greyscale_fire), LERP(25, heat_b, greyscale_fire)));

        heat_r /= 255;
        heat_g /= 255;
        heat_b /= 255;

        color = Dm.list(LERP(0.3f, 1, 1 - greyscale_fire) * heat_r, 0.3f * heat_g * greyscale_fire, 0.3f * heat_b * greyscale_fire, 0.59f * heat_r * greyscale_fire, LERP(0.59f, 1, 1 - greyscale_fire) * heat_g, 0.59f * heat_b * greyscale_fire, 0.11f * heat_r * greyscale_fire, 0.11f * heat_g * greyscale_fire, LERP(0.11f, 1, 1 - greyscale_fire) * heat_b, 0, 0, 0);
        alpha = heat_a;
    }

    // #define INSUFFICIENT(path) (!location.air.moles[path] || location.air.moles[path] < 0.5)  (expanded inline, rules 11.1)
    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:281
    private static bool INSUFFICIENT(turf_open location, Type path) => (!Dm.truthy(location.air.moles[path]) || Dm.num(location.air.moles[path]) < 0.5f);

    /**
     * Regular process proc for hotspots governed by the controller.
     * Handles the calling of perform_exposure() which handles the bulk of temperature processing.
     * Burning or fire_act() are also called by perform_exposure().
     * Also handles the dying and qdeletion of the hotspot and hotspot creations on adjacent cardinal turfs.
     * And some visual stuffs too! Colors and fainter icons for specific conditions.
     */
    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:290
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(just_spawned))
        {
            just_spawned = FALSE;
            return null;
        }

        turf_open location = loc as turf_open;
        if (location == null)
        {
            qdel(this);
            return null;
        }

        if (Dm.truthy(location.excited_group))
            location.excited_group.reset_cooldowns();

        cold_fire = FALSE;
        if (temperature <= FREON_MAXIMUM_BURN_TEMPERATURE)
            cold_fire = TRUE;

        if ((temperature < FIRE_MINIMUM_TEMPERATURE_TO_EXIST && !Dm.truthy(cold_fire)) || (Dm.num(volume) <= 1))
        {
            qdel(this);
            return null;
        }

        //Not enough / nothing to burn
        if (!Dm.truthy(location.air) || (INSUFFICIENT(location, typeof(datum_gas_plasma)) && INSUFFICIENT(location, typeof(datum_gas_tritium)) && INSUFFICIENT(location, typeof(datum_gas_hydrogen)) && INSUFFICIENT(location, typeof(datum_gas_freon))) || INSUFFICIENT(location, typeof(datum_gas_oxygen)))
        {
            qdel(this);
            return null;
        }

        Waiting.NoSleep(perform_exposure());

        if (Dm.truthy(bypassing))
        {
            set_fire_stage("heavy");
            if (!Dm.truthy(cold_fire))
                location.burn_tile();

            //Possible spread due to radiated heat.
            if (location.air.temperature > FIRE_MINIMUM_TEMPERATURE_TO_SPREAD || Dm.truthy(cold_fire))
            {
                float radiated_temperature = location.air.temperature * FIRE_SPREAD_RADIOSITY_SCALE;
                if (Dm.truthy(cold_fire))
                    radiated_temperature = location.air.temperature * COLD_FIRE_SPREAD_RADIOSITY_SCALE;
                foreach (object t in Dm.each(location.atmos_adjacent_turfs))
                {
                    turf_open T = (turf_open)t;
                    if (!Dm.truthy(T.active_hotspot))
                        T.hotspot_expose(radiated_temperature, CELL_VOLUME / 4);
                }
            }
        }
        else
        {
            if (Dm.num(volume) > CELL_VOLUME * 0.4f)
                set_fire_stage("medium");
            else
                set_fire_stage("light");
        }

        if ((Dm.mod(visual_update_tick++, 7)) == 0)
            update_color();

        return TRUE;
    }

    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:344
    public virtual void set_fire_stage(object stage = null)
    {
        if (Dm.eq(fire_stage, stage))
            return;
        fire_stage = stage as string;
        icon_state = stage as string;
        dir = Dm.num(pick(GLOB.cardinals));
        update_color();
    }

    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:352
    public override object Destroy(float? force = null)
    {
        Dm.remove(SSair.hotspots, this);
        turf_open cur_turf = loc as turf_open;
        if (Dm.truthy(our_hot_group))
        {
            our_hot_group.remove_from_group(this);
            our_hot_group = null;
        }
        if (cur_turf != null && cur_turf.active_hotspot == this)
            cur_turf.active_hotspot = null;
        return base.Destroy(force);
    }

    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:362
    public virtual void on_entered(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        // SIGNAL_HANDLER
        if (isliving(arrived) && !Dm.truthy(cold_fire))
        {
            mob_living immolated = (mob_living)arrived;
            Waiting.NoSleep(immolated.fire_act(temperature, volume));
        }
    }

    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:368  /obj/effect/hotspot/singularity_pull: return (a hotspot is not
    // pulled). GAP(lead): the root /atom/proc/singularity_pull (the singularity and the tesla, a later milestone) is not
    // declared, so there is nothing to override and nothing calls it.
}

// tg: code/modules/atmospherics/environmental/LINDA_fire.dm:371  /datum/looping_sound/fire: a var block only (generated); its procs
// are /datum/looping_sound's (datums/looping_sounds/_looping_sound.cs).

///handle the grouping of hotspot and then determining an average center to play sound in
// tg: code/modules/atmospherics/environmental/LINDA_fire.dm:380
public partial class datum_hot_group : datum
{
    // #define MIN_SIZE_SOUND 2
    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:378
    private const int MIN_SIZE_SOUND = 2;

    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:398
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        current_sound_loc = null;
        spot_list = null;
        qdel(sound);
        return dot;
    }

    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:404
    public virtual void remove_from_group(obj_effect_hotspot target = null)
    {
        Dm.remove(ref spot_list, target);
        turf target_turf = target.loc as turf; // as add_to_group reads it
        if (Dm.truthy(target_turf))
        {
            Dm.remove(ref x_coord, target_turf.x);
            Dm.remove(ref y_coord, target_turf.y);
        }
        if (!Dm.truthy(length(spot_list)))
        {
            qdel(this);
            return;
        }
    }

    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:414
    public virtual void add_to_group(obj_effect_hotspot target = null)
    {
        if (QDELETED(target))
            return;
        Dm.append(ref spot_list, target);
        target.our_hot_group = this;
        turf target_turf = target.loc as turf; // tg types it open; a hotspot made on a wall joins a group before it is deleted
        Dm.append(ref x_coord, target_turf.x);
        Dm.append(ref y_coord, target_turf.y);
        Dm.append(ref z_coord, target_turf.z);
        if (update_sound_center <= world.time && Dm.num(length(spot_list)) > MIN_SIZE_SOUND)//arbitrary size to start playing the sound; COOLDOWN_FINISHED, inline
        {
            update_sound();
            update_sound_center = world.time + 5 * SECONDS; // COOLDOWN_START, inline
        }
    }

    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:427
    public virtual void merge_hot_groups(datum_hot_group enemy_group = null)
    {
        if (Dm.num(length(spot_list)) >= tiles_limit || Dm.num(length(enemy_group.spot_list)) >= tiles_limit)
            return;
        datum_hot_group saving_group;
        datum_hot_group sacrificial_group;
        if (Dm.num(length(spot_list)) > Dm.num(length(enemy_group.spot_list)) || (Dm.num(length(spot_list)) == Dm.num(length(enemy_group.spot_list)) && prob(50)))//we're bigger take all of their territory!
        {
            saving_group = this;
            sacrificial_group = enemy_group;
        }
        else
        {
            saving_group = enemy_group;
            sacrificial_group = this;
        }
        foreach (obj_effect_hotspot reference in Dm.each_cast<obj_effect_hotspot>(sacrificial_group.spot_list))
            reference.our_hot_group = saving_group;
        Dm.append(ref saving_group.spot_list, sacrificial_group.spot_list);
        Dm.append(ref saving_group.x_coord, sacrificial_group.x_coord);
        Dm.append(ref saving_group.y_coord, sacrificial_group.y_coord);
        qdel(sacrificial_group);
        if (update_sound_center <= world.time && Dm.num(length(spot_list)) > MIN_SIZE_SOUND)//arbitrary size to start playing the sound; COOLDOWN_FINISHED, inline
        {
            update_sound();
            update_sound_center = world.time + 5 * SECONDS; // COOLDOWN_START, inline
        }
    }

    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:448
    public virtual void update_sound()
    {
        //we can draw a cross around the average middle of any globs of group, curves or hollow groups may cause issues with this
        average_x = round((Dm.num(max(x_coord)) + Dm.num(min(x_coord))) / 2);
        average_y = round((Dm.num(max(y_coord)) + Dm.num(min(y_coord))) / 2);
        average_Z = round((Dm.num(min(z_coord)) + Dm.num(max(z_coord))) / 2);
        drop_off_dist = max((Dm.num(max(y_coord)) - Dm.num(min(y_coord))), (Dm.num(max(x_coord)) - Dm.num(min(x_coord))), 1);// pick the largest value between the width and length of the group to determine sound drop off
        turf_open sound_turf = locate(Dm.num(average_x), Dm.num(average_y), Dm.num(average_Z)) as turf_open;
        if (Dm.truthy(sound))
        {
            sound.falloff_distance = drop_off_dist;
            sound.extra_range = Dm.num(drop_off_dist);
            if (sound_turf != current_sound_loc)
                sound.parent = sound_turf;
            return;
        }
        sound = Dm.@new<datum_looping_sound_fire>(sound_turf, TRUE);
        sound.falloff_distance = drop_off_dist;
        sound.extra_range = Dm.num(drop_off_dist);
        current_sound_loc = sound_turf;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:137  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered), COMSIG_ATOM_ABSTRACT_ENTERED = PROC_REF(on_entered))
    internal DmList obj_effect_hotspot__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_effect_hotspot.on_entered)),
        Dm.kv(COMSIG_ATOM_ABSTRACT_ENTERED, nameof(obj_effect_hotspot.on_entered)));
}

// tg: code/modules/atmospherics/environmental/LINDA_fire.dm:368 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj_effect_hotspot : obj_effect
{
    // tg: code/modules/atmospherics/environmental/LINDA_fire.dm:368
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }
}
