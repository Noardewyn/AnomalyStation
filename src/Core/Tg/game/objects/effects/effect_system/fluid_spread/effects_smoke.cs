// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * A fluid which spreads through the air affecting every mob it engulfs.
 */
// tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:4
public partial class obj_effect_particle_effect_fluid_smoke : obj_effect_particle_effect_fluid
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:19  var/static/loc_connections (rules 10.6)
    private static ref DmList loc_connections => ref TgState.Current.obj_effect_particle_effect_fluid_smoke__loc_connections;

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:23
    [DmParams("mapload", "group")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        create_reagents(1000, REAGENT_HOLDER_INSTANT_REACT);
        Waiting.NoSleep(setDir(pick(GLOB.cardinals)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        SSsmoke?.start_processing(this); // ?: a world whose Master has not made the subsystem (a test room), as tg always has
        return dot;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:30
    public override object Destroy(float? force = null)
    {
        SSsmoke?.stop_processing(this);
        if (Dm.truthy(spread_bucket))
            SSsmoke.cancel_spread(this);
        return base.Destroy(force);
    }

    /**
     * Makes the smoke fade out and then deletes it.
     */
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:40
    public virtual void kill_smoke()
    {
        SSsmoke.stop_processing(this);
        if (Dm.truthy(spread_bucket))
            SSsmoke.cancel_spread(this);
        Waiting.Detach(fade_out());
        // QDEL_IN(src, 1 SECONDS) (__DEFINES/qdel.dm:59), inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 1 * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 1 * SECONDS, file: "code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm", line: 45);
    }

    /**
     * Animates the smoke gradually fading out of visibility.
     * Also makes the smoke turf transparent as it passes 160 alpha.
     *
     * Arguments:
     * - frames = 0.8 [SECONDS]: The amount of time the smoke should fade out over.
     */
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:54
    public virtual async DmTask fade_out(float? frames = null)
    {
        frames ??= 0.8f * SECONDS;
        if (alpha == 0) //Handle already transparent case
        {
            if (Dm.truthy(opacity))
                await set_opacity(FALSE);
            return;
        }

        if (frames == 0)
        {
            await set_opacity(FALSE);
            alpha = 0;
            return;
        }

        float time_to_transparency = round(((alpha - 160f) / alpha) * Dm.num(frames));
        if (time_to_transparency >= 1)
            _addtimer(Dm.@new<datum_callback>(this, nameof(atom.set_opacity), FALSE), time_to_transparency, file: "code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm", line: 67);
        else
            await set_opacity(FALSE);
        animate(this, Dm.named("time", frames), Dm.named("alpha", 0));
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:73
    [DmParams("seconds_per_tick")]
    public override async DmTask<object> spread(params object[] __extra)
    {
        if (group.total_size > Dm.num(group.target_size))
            return null;
        turf t_loc = get_turf(this);
        if (t_loc == null)
            return null;

        foreach (turf spread_turf in Dm.each<turf>(t_loc.get_atmos_adjacent_turfs()))
        {
            if (group.total_size > Dm.num(group.target_size))
                break;
            if (Dm.truthy(Dm.locate_type(type, spread_turf)))
                continue; // Don't spread smoke where there's already smoke!

            obj_effect_particle_effect_fluid_smoke spread_smoke = Dm.@new(type, spread_turf, group, this) as obj_effect_particle_effect_fluid_smoke;
            await (reagents as datum_reagents).trans_to(spread_smoke, (reagents as datum_reagents).total_volume, copy_only: TRUE);
            await spread_smoke.add_atom_colour(color, FIXED_COLOUR_PRIORITY);
            spread_smoke.lifetime = lifetime;

            // the smoke spreads rapidly, but not instantly
            SSsmoke.queue_spread(spread_smoke);
        }
        return null;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:95
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        lifetime -= Dm.num(seconds_per_tick) * SECONDS;
        if (lifetime <= 0)
        {
            kill_smoke();
            return FALSE;
        }
        foreach (mob_living smoker in Dm.each<mob_living>(loc)) // In case smoke somehow winds up in a locker or something this should still behave sanely.
            Waiting.NoSleep(smoke_mob(smoker as mob_living_carbon, seconds_per_tick));
        return TRUE;
    }

    /**
     * Handles the effects of this smoke on any mobs it comes into contact with.
     *
     * Arguments:
     * - [smoker][/mob/living/carbon]: The mob that is being exposed to this smoke.
     * - seconds_per_tick: A scaling factor for the effects this has. Primarily based off of tick rate to normalize effects to units of rate/sec.
     *
     * Returns whether the smoke effect was applied to the mob.
     */
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:113
    public virtual async DmTask<object> smoke_mob(mob_living_carbon smoker = null, object seconds_per_tick = null)
    {
        if (!istype(smoker, typeof(mob_living_carbon)))
            return FALSE;
        if (lifetime < 1)
            return FALSE;
        if (smoker.@internal != null || Dm.truthy(smoker.has_smoke_protection()))
            return FALSE;
        if (Dm.truthy(smoker.smoke_delay))
            return FALSE;

        smoker.smoke_delay = TRUE;
        _addtimer(Dm.@new<datum_callback>(smoker, nameof(mob_living.smoke_delay__varset), FALSE), 1 * SECONDS, file: "code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm", line: 124);
        if (Dm.truthy(smoker._listen_lookup?[COMSIG_CARBON_EXPOSED_TO_SMOKE]))
            smoker._SendSignal(COMSIG_CARBON_EXPOSED_TO_SMOKE, Dm.list(smoker, seconds_per_tick));
        return TRUE;
    }

    /**
     * Makes the smoke react to nearby opening/closing airlocks and the like.
     * Makes it possible for smoke to spread through airlocks that open after the edge of the smoke cloud has already spread past them.
     *
     * Arguments:
     * - [source][/turf]: The turf that has been touched by an atmos adjacency change.
     */
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:135
    [SignalHandler]
    public virtual object react_to_atmos_adjacency_changes(turf source = null)
    {
        if (group == null)
            return NONE;
        if (Dm.truthy(spread_bucket))
            return NONE;
        SSsmoke.queue_spread(this);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:19  var/static/loc_connections = list(COMSIG_TURF_CALCULATED_ADJACENT_ATMOS = PROC_REF(react_to_atmos_adjacency_changes))
    internal DmList obj_effect_particle_effect_fluid_smoke__loc_connections = Dm.list(
        Dm.kv(COMSIG_TURF_CALCULATED_ADJACENT_ATMOS, nameof(obj_effect_particle_effect_fluid_smoke.react_to_atmos_adjacency_changes)));
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:198  var/static/list/loc_connections = list(COMSIG_ATOM_ENTERED = PROC_REF(on_entered))
    internal DmList obj_effect_particle_effect_fluid_smoke_bad__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_effect_particle_effect_fluid_smoke_bad.on_entered)));
}

public partial class mob_living
{
    /// VARSET_CALLBACK(smoker, smoke_delay, FALSE)'s target (the GAP(lead) of modules/assembly/doorcontrol.cs): ___callbackvarset's datum branch for this var.
    public virtual void smoke_delay__varset(object var_value = null)
    {
        smoke_delay = Dm.num(var_value);
    }
}

public static partial class GlobalProcs
{
    /**
     * A helper proc used to spawn small puffs of smoke.
     *
     * Arguments:
     * - range: The amount of smoke to produce as number of steps from origin covered.
     * - amount: The amount of smoke to produce as the total desired coverage area. Autofilled from the range arg if not set.
     * - location: Where to produce the smoke cloud.
     * - smoke_type - Typepath for the effect system to use
     * - effect_type: The smoke typepath to spawn.
     * - log: Should the system log the smoke spawned?
     */
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:170
    public static async DmTask do_smoke(float? range = null, atom holder = null, object location = null, object amount = null, Type smoke_type = null, Type effect_type = null, float? log = null)
    {
        range ??= 0;
        smoke_type ??= typeof(datum_effect_system_fluid_spread_smoke);
        effect_type ??= typeof(obj_effect_particle_effect_fluid_smoke);
        log ??= FALSE;
        datum_effect_system_fluid_spread_smoke smoke = Dm.@new(smoke_type, location, range, amount, holder) as datum_effect_system_fluid_spread_smoke;
        smoke.effect_type = effect_type;
        await smoke.start(log);
    }

    /// Helper to quickly create a cloud of reagent smoke
    /// reagent_type can accept a list of reagents, optionally as a key-value pair with values overriding reagent_volume if not null
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:400
    public static async DmTask do_chem_smoke(float? range = null, atom holder = null, object location = null, object reagent_type = null, float? reagent_volume = null, datum_reagents carry = null, object carry_limit = null, float? log = null, object amount = null, Type smoke_type = null, float? silent = null)
    {
        range ??= 0;
        reagent_type ??= typeof(datum_reagent_water);
        reagent_volume ??= 10;
        log ??= FALSE;
        smoke_type ??= typeof(datum_effect_system_fluid_spread_smoke_chem);
        silent ??= TRUE;
        if (carry != null)
        {
            datum_effect_system_fluid_spread_smoke_chem smoke = Dm.@new(smoke_type, location, range, amount, holder ?? location, carry, carry_limit, silent) as datum_effect_system_fluid_spread_smoke_chem;
            await smoke.start(log);
            return;
        }

        if (ispath(reagent_type, typeof(datum_reagent)))
        {
            datum_reagents smoke_reagents = Dm.@new<datum_reagents>(reagent_volume);
            await smoke_reagents.add_reagent(reagent_type, reagent_volume);
            datum_effect_system_fluid_spread_smoke_chem smoke = Dm.@new(smoke_type, location, range, amount, holder ?? location, smoke_reagents, carry_limit, silent) as datum_effect_system_fluid_spread_smoke_chem;
            await smoke.start(log);
            return;
        }

        if (!islist(reagent_type))
            throw Dm.CRASH(Dm.text("do_chem_smoke passed a non-reagent path, non-list reagent_type []!", reagent_type));

        DmList reagent_list = reagent_type as DmList;
        float chem_volume = 0;
        foreach (object chem_type in Dm.each(reagent_list))
            chem_volume += Dm.num(reagent_list[chem_type] ?? reagent_volume);

        datum_reagents list_reagents = Dm.@new<datum_reagents>(chem_volume);
        foreach (object chem_type in Dm.each(reagent_list))
            await list_reagents.add_reagent(chem_type, reagent_list[chem_type] ?? reagent_volume);

        datum_effect_system_fluid_spread_smoke_chem list_smoke = Dm.@new(smoke_type, location, range, amount, holder ?? location, list_reagents, carry_limit, silent) as datum_effect_system_fluid_spread_smoke_chem;
        await list_smoke.start(log);
    }
}

/////////////////////////////////////////////
// Bad smoke
/////////////////////////////////////////////

/// Smoke that makes you cough and reduces the power of lasers.
// tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:193
public partial class obj_effect_particle_effect_fluid_smoke_bad : obj_effect_particle_effect_fluid_smoke
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:198  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_effect_particle_effect_fluid_smoke_bad__Initialize__loc_connections;

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:196
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return dot;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:203
    public override async DmTask<object> smoke_mob(mob_living_carbon smoker = null, object seconds_per_tick = null)
    {
        object dot = await base.smoke_mob(smoker, seconds_per_tick);
        if (!Dm.truthy(dot))
            return dot;
        await smoker.drop_all_held_items();
        await smoker.adjust_oxy_loss(1);
        await smoker.emote("cough");
        return dot;
    }

    /**
     * Reduces the power of any beam projectile that passes through the smoke.
     *
     * Arguments:
     * - [source][/datum]: The location that has just been entered. If [/datum/element/connect_loc] is working this is [src.loc][/atom/var/loc].
     * - [arrived][/atom/movable]: The atom that has just entered the source location.
     * - [old_loc][/atom]: The location the entering atom just was in.
     * - [old_locs][/list/atom]: The set of locations the entering atom was just in.
     */
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:220
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (istype(arrived, typeof(obj_projectile_beam)))
        {
            obj_projectile_beam beam = arrived as obj_projectile_beam;
            beam.damage = Dm.num(beam.damage) * 0.5f;
        }
    }
}

/////////////////////////////////////////////
// Nanofrost smoke
/////////////////////////////////////////////

/// A factory which produces light blue, transparent smoke and a blast that chills every turf in the area.
// tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:265
public partial class datum_effect_system_fluid_spread_smoke_freezing : datum_effect_system_fluid_spread_smoke
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:276
    [DmParams("location", "range", "amount", "holder", "blast_radius")]
    public override async DmTask New(params object[] __extra)
    {
        float? blast_radius = Dm.arg<float?>(__extra, 4);
        blast_radius ??= 0;
        await base.New(__extra);
        blast = Dm.num(blast_radius);
    }

    /**
     * Chills an open turf.
     *
     * Forces the air temperature to a specific value.
     * Transmutes all of the plasma in the air into nitrogen.
     * Extinguishes all fires and burning objects/mobs in the turf.
     * May freeze all vents and vent scrubbers shut.
     *
     * Arguments:
     * - [chilly][/turf/open]: The open turf to chill
     */
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:291
    public virtual async DmTask chill_turf(turf_open chilly = null)
    {
        if (!istype(chilly, typeof(turf_open)))
            return;

        if (chilly.air != null)
        {
            datum_gas_mixture air = chilly.air;
            if (!Dm.truthy(distcheck) || get_dist(location, chilly) < blast) // Otherwise we'll get silliness like people using Nanofrost to kill people through walls with cold air
                air.temperature = temperature;

            if (Dm.truthy(air.moles[typeof(datum_gas_plasma)]))
            {
                object mole_count = air.moles[typeof(datum_gas_plasma)];
                air.adjust_gas(typeof(datum_gas_nitrogen), mole_count);
                air.adjust_gas(typeof(datum_gas_plasma), -Dm.num(mole_count));
                air.garbage_collect();
            }

            foreach (obj_effect_hotspot fire in Dm.each<obj_effect_hotspot>(chilly))
                qdel(fire);
            chilly.air_update_turf(FALSE, FALSE);
        }

        if (Dm.truthy(weldvents))
        {
            foreach (obj_machinery_atmospherics_components_unary comp in Dm.each<obj_machinery_atmospherics_components_unary>(chilly))
            {
                if (comp.welded != null && !Dm.truthy(comp.welded)) //must be an unwelded vent pump or vent scrubber.
                {
                    comp.welded = TRUE;
                    comp.update_appearance();
                    comp.visible_message(span_danger(I18n.T("[] is frozen shut!", comp)));
                }
            }
        }

        // Extinguishes everything in the turf
        foreach (mob_living potential_tinder in Dm.each<mob_living>(chilly))
            potential_tinder.extinguish_mob();
        foreach (obj_item potential_tinder in Dm.each<obj_item>(chilly))
            await potential_tinder.extinguish();
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:323
    [DmParams("log")]
    public override async DmTask<object> start(params object[] __extra)
    {
        if (Dm.truthy(blast))
        {
            foreach (turf T in Dm.each_cast<turf>(RANGE_TURFS(blast, location)))
                await chill_turf(T as turf_open);
        }
        return await base.start(__extra);
    }
}

/////////////////////////////////////////////
// Sleep smoke
/////////////////////////////////////////////

/// Smoke which knocks you out if you breathe it in.
// tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:341
public partial class obj_effect_particle_effect_fluid_smoke_sleeping : obj_effect_particle_effect_fluid_smoke
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:345
    public override async DmTask<object> smoke_mob(mob_living_carbon smoker = null, object seconds_per_tick = null)
    {
        if (Dm.truthy(await base.smoke_mob(smoker, seconds_per_tick)))
        {
            await smoker.Sleeping(20 * SECONDS);
            await smoker.emote("cough");
            return TRUE;
        }
        return null;
    }
}

/////////////////////////////////////////////
// Chem smoke
/////////////////////////////////////////////

/**
 * Smoke which contains reagents which it applies to everything it comes into contact with.
 */
// tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:362
public partial class obj_effect_particle_effect_fluid_smoke_chem : obj_effect_particle_effect_fluid_smoke
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:365
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.process(seconds_per_tick, __extra);
        if (!Dm.truthy(dot))
            return dot;

        turf location = get_turf(this);
        float fraction = (Dm.num(seconds_per_tick) * SECONDS) / Dm.num(Dm.initial(this, nameof(lifetime)));
        foreach (atom_movable thing in Dm.each_cast<atom_movable>(location))
        {
            if (thing == this)
                continue;
            if (thing.invisibility >= INVISIBILITY_ABSTRACT) // Don't smoke landmarks please
                continue;
            if (HAS_TRAIT(thing, TRAIT_UNDERFLOOR))
                continue;
            if (isliving(thing))
                continue;
            Waiting.NoSleep((reagents as datum_reagents).expose(thing, SMOKE_MACHINE, fraction));
        }

        Waiting.NoSleep((reagents as datum_reagents).expose(location, SMOKE_MACHINE, fraction));
        return TRUE;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:386
    public override async DmTask<object> smoke_mob(mob_living_carbon smoker = null, object seconds_per_tick = null)
    {
        if (lifetime < 1)
            return FALSE;
        if (!istype(smoker, typeof(mob_living_carbon)))
            return FALSE;
        if (smoker.@internal != null || Dm.truthy(smoker.has_smoke_protection()))
            return FALSE;

        float fraction = (Dm.num(seconds_per_tick) * SECONDS) / Dm.num(Dm.initial(this, nameof(lifetime)));
        await (reagents as datum_reagents).trans_to(smoker, (reagents as datum_reagents).total_volume, fraction, methods: SMOKE_MACHINE, copy_only: TRUE);
        return TRUE;
    }
}

/// A factory which produces clouds of chemical bearing smoke.
// tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:429
public partial class datum_effect_system_fluid_spread_smoke_chem : datum_effect_system_fluid_spread_smoke
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:434
    [DmParams("location", "range", "amount", "holder", "carry", "carry_limit", "silent")]
    public override async DmTask New(params object[] __extra)
    {
        datum_reagents carry = Dm.arg<datum_reagents>(__extra, 4);
        object carry_limit = Dm.arg<object>(__extra, 5);
        object silent = Dm.arg<object>(__extra, 6) ?? FALSE;
        await base.New(__extra);
        chemholder = Dm.@new<datum_reagents>(1000, NO_REACT);
        if (carry != null)
            await carry.trans_to(chemholder, carry_limit == null ? carry.total_volume : Dm.num(carry_limit), copy_only: TRUE);

        if (Dm.truthy(silent))
            return;

        var location = Dm.arg<atom>(__extra, 0);
        DmList contained_reagents = Dm.list();
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(chemholder.reagent_list))
            Dm.append(ref contained_reagents, Dm.text("[]u []", reagent.volume, reagent));

        string where = Dm.text("[]", AREACOORD(location));
        object contained = Dm.truthy(length(contained_reagents)) ? Dm.text("\\[[]\\] @ []K", contained_reagents.Join(", "), chemholder.chem_temp) : null;
        var fluid_area = get_area(location) as area;
        // Some reagents don't have a my_atom in some cases
        if (!Dm.truthy((carry?.my_atom as atom)?.fingerprintslast))
        {
            // Deathmatch has way too much smoke to log
            if (!(carry?.my_atom is obj_machinery_plumbing) && !Dm.truthy(Dm.bitand(fluid_area?.area_flags, QUIET_LOGS)))
                message_admins(Dm.text("Smoke: ([])[]. No associated key.", ADMIN_VERBOSEJMP(location), contained));
            log_game(Dm.text("A chemical smoke reaction has taken place in ([])[]. No associated key.", where, contained));
            return;
        }

        var bomber = get_mob_by_key(((atom)carry.my_atom).fingerprintslast) as mob;
        if (!(carry.my_atom is obj_machinery_plumbing) && !Dm.truthy(Dm.bitand(fluid_area?.area_flags, QUIET_LOGS))) // I like to be able to see my logs thank you
            message_admins(Dm.text("Smoke: ([])[]. Key: [].", ADMIN_VERBOSEJMP(location), contained, bomber != null ? Dm.text("[] ", ADMIN_LOOKUPFLW(bomber)) : ((atom)carry.my_atom).fingerprintslast));
        log_game(Dm.text("A chemical smoke reaction has taken place in ([])[]. Last touched by [].", where, contained, ((atom)carry.my_atom).fingerprintslast));
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:462
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref chemholder);
        return base.Destroy(force);
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_smoke.dm:466
    [DmParams("log")]
    public override async DmTask<object> start(params object[] __extra)
    {
        object log = Dm.arg<object>(__extra, 0) ?? FALSE;
        object start_loc = holder != null ? get_turf(holder as atom) : this.location;
        object mixcolor = mix_color_from_reagents(chemholder.reagent_list);
        obj_effect_particle_effect_fluid_smoke_chem smoke = Dm.@new(effect_type, start_loc, Dm.@new<datum_fluid_group>(amount)) as obj_effect_particle_effect_fluid_smoke_chem;
        await chemholder.trans_to(smoke, chemholder.total_volume, copy_only: TRUE);

        if (Dm.truthy(mixcolor))
            await smoke.add_atom_colour(mixcolor, FIXED_COLOUR_PRIORITY); // give the smoke color, if it has any to begin with
        if (Dm.truthy(log))
            await help_out_the_admins(smoke, holder as atom, location);
        await smoke.spread(); // Making the smoke spread immediately.
        return null;
    }
}
