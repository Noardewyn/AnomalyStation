// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the foamed metal wall's base since C6 (the hivebot conjures it), the foam fluids, do_foam, the foam
// factories, the resin wall and the foam starters since gaps F1.

/**
 * ## Foam
 *
 * Similar to smoke, but slower and mobs absorb its reagent through their exposed skin.
 */
// tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:13
public partial class obj_effect_particle_effect_fluid_foam : obj_effect_particle_effect_fluid
{
    /// The minimum foam range required to start diluting the reagents past the minimum dilution rate.
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:2  #define MINIMUM_FOAM_DILUTION_RANGE 3
    public const float MINIMUM_FOAM_DILUTION_RANGE = 3;
    /// The minumum foam-area based divisor used to decrease foam exposure volume.
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:4  #define MINIMUM_FOAM_DILUTION DIAMOND_AREA(MINIMUM_FOAM_DILUTION_RANGE)
    public static float MINIMUM_FOAM_DILUTION => DIAMOND_AREA(MINIMUM_FOAM_DILUTION_RANGE);

    /// The types of turfs that this foam cannot spread to.
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:23  var/static/list/blacklisted_turfs (rules 10.6)
    private static DmList blacklisted_turfs => TgState.Current.obj_effect_particle_effect_fluid_foam__blacklisted_turfs ??= typecacheof(Dm.list(
        typeof(turf_open_space_transit),
        typeof(turf_open_chasm),
        typeof(turf_open_lava))) as DmList;

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:40
    [DmParams("mapload", "fluid_group", "source", "lifetime", "slippery")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object lifetime = Dm.arg<object>(__extra, 2);
        object slippery = Dm.arg<object>(__extra, 3);
        object dot = base.Initialize(mapload, __extra);
        this.lifetime = Dm.truthy(lifetime) ? Dm.num(lifetime) : this.lifetime;
        this.original_lifetime = this.lifetime;
        this.slippery_foam = slippery != null ? Dm.num(slippery) : slippery_foam;
        if (Dm.truthy(slippery_foam))
            Waiting.NoSleep(AddComponent(typeof(datum_component_slippery), 100, Dm.named("can_slip_callback", Dm.@new<datum_callback>(this, nameof(try_slip)))));
        if (HAS_TRAIT(loc, TRAIT_ELEVATED_TURF))
            layer = WATER_LEVEL_LAYER;
        create_reagents(1000, REAGENT_HOLDER_INSTANT_REACT);
        playsound(this, Dm.resource("sound/effects/bubbles/bubbles2.ogg"), 80, TRUE, -3);
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload));
        SSfoam?.start_processing(this); // ?: a world whose Master has not made the subsystem (a test room), as tg always has
        return dot;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:54
    public override object Destroy(float? force = null)
    {
        SSfoam?.stop_processing(this);
        if (Dm.truthy(spread_bucket))
            SSfoam.cancel_spread(this);
        return base.Destroy(force);
    }

    /**
     * Makes the foam dissipate and create whatever remnants it must.
     */
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:63
    public virtual async DmTask kill_foam()
    {
        SSfoam?.stop_processing(this);
        if (Dm.truthy(spread_bucket))
            SSfoam.cancel_spread(this);
        await make_result();
        flick(Dm.text("[]-disolve", icon_state), this);
        // QDEL_IN(src, 0.5 SECONDS) (__DEFINES/qdel.dm:59), inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 0.5f * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 0.5f * SECONDS, file: "code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm", line: 69);
    }

    /**
     * Makes the foam leave behind something when it dissipates.
     *
     * Returns the thing the foam leaves behind for further modification by subtypes.
     */
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:76
    public virtual async DmTask<object> make_result()
    {
        if (result_type == null)
            return null;

        atom location = loc;
        atom_movable result = (!Dm.truthy(allow_duplicate_results) ? Dm.locate_type(result_type, location) as atom_movable : null) ?? Dm.@new(result_type, location) as atom_movable;
        transfer_fingerprints_to(result);
        return result;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:86
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        float ds_seconds_per_tick = Dm.num(seconds_per_tick) * SECONDS;
        lifetime -= ds_seconds_per_tick;
        if (lifetime <= 0)
        {
            await kill_foam();
            return null;
        }

        if (ismob(loc))
        {
            stack_trace(Dm.text("A foam effect ([]) was created within a mob! (Actual location: [] ([]))", type, loc, loc.type), "code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm", 94);
            qdel(this);
            return null;
        }

        float fraction = (ds_seconds_per_tick * MINIMUM_FOAM_DILUTION) / (Dm.initial(GetType(), (obj_effect_particle_effect_fluid_foam f) => f.lifetime) * Dm.max(MINIMUM_FOAM_DILUTION, group.total_size));

        if (isturf(loc))
        {
            turf turf_location = loc as turf;
            foreach (obj @object in Dm.each<obj>(turf_location))
            {
                if (@object == this)
                    continue;
                if (@object.invisibility >= INVISIBILITY_ABSTRACT) // Don't foam landmarks please
                    continue;
                if (HAS_TRAIT(@object, TRAIT_UNDERFLOOR))
                    continue;
                if (HAS_TRAIT(loc, TRAIT_ELEVATED_TURF) && !HAS_TRAIT(@object, TRAIT_ELEVATING_OBJECT))
                    continue; // Do expose tables, don't expose items on tables
                await (reagents as datum_reagents).expose(@object, VAPOR, fraction);
            }
        }

        float hit = 0;
        foreach (mob_living foamer in Dm.each<mob_living>(loc))
        {
            if (HAS_TRAIT(foamer, TRAIT_MOB_ELEVATED))
                continue;
            hit += Dm.num(await foam_mob(foamer, seconds_per_tick));
        }
        if (hit != 0)
            lifetime += ds_seconds_per_tick; //this is so the decrease from mobs hit and the natural decrease don't cumulate.

        await (reagents as datum_reagents).expose(loc, VAPOR, fraction);
        return null;
    }

    /**
     * Applies the effect of this foam to a mob.
     *
     * Arguments:
     * - [foaming][/mob/living]: The mob that this foam is acting on.
     * - seconds_per_tick: The amount of time that this foam is acting on them over.
     *
     * Returns:
     * - [TRUE]: If the foam was successfully applied to the mob. Used to scale how quickly foam dissipates according to the number of mobs it is applied to.
     * - [FALSE]: Otherwise.
     */
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:134
    public virtual async DmTask<object> foam_mob(mob_living foaming = null, object seconds_per_tick = null)
    {
        if (lifetime <= 0)
            return FALSE;
        if (!istype(foaming, typeof(mob_living)))
            return FALSE;

        float spt = Dm.min(Dm.num(seconds_per_tick) * SECONDS, lifetime);
        float fraction = (spt * MINIMUM_FOAM_DILUTION) / (Dm.initial(GetType(), (obj_effect_particle_effect_fluid_foam f) => f.lifetime) * Dm.max(MINIMUM_FOAM_DILUTION, group.total_size));
        await (reagents as datum_reagents).expose(foaming, VAPOR, fraction);
        lifetime -= spt;
        return TRUE;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:146
    public virtual object try_slip(mob_living slipper = null, mob_living slippee = null)
    {
        return !HAS_TRAIT(slippee, TRAIT_MOB_ELEVATED) ? TRUE : FALSE;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:149
    [DmParams("seconds_per_tick")]
    public override async DmTask<object> spread(params object[] __extra)
    {
        float seconds_per_tick = Dm.arg<float?>(__extra, 0) ?? 0.2f * SECONDS;
        if (group.total_size > Dm.num(group.target_size))
            return null;
        turf location = get_turf(this);
        if (!istype(location, typeof(turf)))
            return FALSE;

        datum_can_pass_info info = Dm.@new<datum_can_pass_info>(null, null, TRUE); // new(no_id = TRUE)
        info.pass_flags = Dm.bitor(Dm.bitor(PASSTABLE, PASSGRILLE), Dm.bitor(PASSMACHINE, PASSSTRUCTURE));
        foreach (object iter_dir in Dm.each(GLOB.cardinals))
        {
            turf spread_turf = get_step(this, Dm.num(iter_dir));
            if (Dm.truthy(spread_turf?.density) || Dm.truthy(location.LinkBlockedWithAccess(spread_turf, info)))
                continue;

            obj_effect_particle_effect_fluid_foam foundfoam = Dm.locate<obj_effect_particle_effect_fluid_foam>(spread_turf); //Don't spread foam where there's already foam!
            if (foundfoam != null)
                continue;
            if (Dm.truthy(is_type_in_typecache(spread_turf, blacklisted_turfs)))
                continue;

            foreach (mob_living foaming in Dm.each<mob_living>(spread_turf))
            {
                if (HAS_TRAIT(foaming, TRAIT_MOB_ELEVATED))
                    continue;
                await foam_mob(foaming, seconds_per_tick);
            }

            obj_effect_particle_effect_fluid_foam spread_foam = Dm.@new(type, spread_turf, group, this, this.original_lifetime, this.slippery_foam) as obj_effect_particle_effect_fluid_foam;
            await (reagents as datum_reagents).trans_to(spread_foam, (reagents as datum_reagents).total_volume, copy_only: TRUE);
            await spread_foam.add_atom_colour(color, FIXED_COLOUR_PRIORITY);
            spread_foam.result_type = result_type;
            SSfoam?.queue_spread(spread_foam); // ?: a world whose Master has not made the subsystem (a test room), as tg always has
        }
        return null;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:180
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(exposed_temperature) > 475 ? TRUE : FALSE;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:183
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        if (prob(Dm.max(0, Dm.num(exposed_temperature) - 475))) //foam dissolves when heated
            await kill_foam();
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:23  var/static/list/blacklisted_turfs = typecacheof(...)
    internal DmList obj_effect_particle_effect_fluid_foam__blacklisted_turfs;
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:428  var/static/list/ignored_gases = typecacheof(...)
    internal DmList obj_structure_foamedmetal_resin__ignored_gases;
}

public static partial class GlobalProcs
{
    ///	The effective scaling of the reagents in the foam. (Total delivered at or below [MINIMUM_FOAM_DILUTION])
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:6  #define FOAM_REAGENT_SCALE 3.2
    public const float FOAM_REAGENT_SCALE = 3.2f;

    /// Proc to quickly spawn foam
    /// reagent_type can accept a list of reagents, optionally as a key-value pair with values overriding reagent_volume if not null
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:188
    public static async DmTask do_foam(float? range = null, atom holder = null, turf location = null, object reagent_type = null, float? reagent_volume = null, datum_reagents carry = null, object amount = null, float? log = null, Type foam_type = null, Type result_type = null, float? stop_reactions = null, float? reagent_scale = null)
    {
        range ??= 1;
        reagent_volume ??= 10;
        log ??= FALSE;
        foam_type ??= typeof(datum_effect_system_fluid_spread_foam);
        stop_reactions ??= FALSE;
        reagent_scale ??= FOAM_REAGENT_SCALE;
        if (carry != null || reagent_type == null)
        {
            datum_effect_system_fluid_spread_foam foam = Dm.@new(foam_type, location, range, amount, holder ?? location, carry, result_type, stop_reactions, reagent_scale) as datum_effect_system_fluid_spread_foam;
            await foam.start(log); // start(log = log): positional, the variadic slot reads its arguments by position
            return;
        }

        if (ispath(reagent_type, typeof(datum_reagent)))
        {
            datum_reagents foam_reagents = Dm.@new<datum_reagents>(reagent_volume);
            await foam_reagents.add_reagent(reagent_type, reagent_volume);
            datum_effect_system_fluid_spread_foam foam = Dm.@new(foam_type, location, range, amount, holder ?? location, foam_reagents, result_type, stop_reactions, reagent_scale) as datum_effect_system_fluid_spread_foam;
            await foam.start(log); // start(log = log): positional, the variadic slot reads its arguments by position
            return;
        }

        if (!islist(reagent_type))
            throw Dm.CRASH(Dm.text("do_foam passed a non-reagent path, non-list reagent_type []!", reagent_type), "code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm", 203);

        DmList reagent_list = reagent_type as DmList;
        float chem_volume = 0;
        foreach (object chem_type in Dm.each(reagent_list))
            chem_volume += Dm.truthy(reagent_list[chem_type]) ? Dm.num(reagent_list[chem_type]) : Dm.num(reagent_volume);

        datum_reagents list_reagents = Dm.@new<datum_reagents>(chem_volume);
        foreach (object chem_type in Dm.each(reagent_list))
            await list_reagents.add_reagent(chem_type, Dm.truthy(reagent_list[chem_type]) ? reagent_list[chem_type] : reagent_volume);

        datum_effect_system_fluid_spread_foam list_foam = Dm.@new(foam_type, location, range, amount, holder ?? location, list_reagents, result_type, stop_reactions, reagent_scale) as datum_effect_system_fluid_spread_foam;
        await list_foam.start(log);
    }
}

/// A factory for foam fluid floods.
// tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:219
public partial class datum_effect_system_fluid_spread_foam : datum_effect_system_fluid_spread
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:227
    [DmParams("location", "range", "amount", "holder", "carry", "result_type", "stop_reactions", "reagent_scale")]
    public override async DmTask New(params object[] __extra)
    {
        datum_reagents carry = Dm.arg<datum_reagents>(__extra, 4);
        Type result_type = Dm.arg<Type>(__extra, 5);
        float? stop_reactions = Dm.arg<float?>(__extra, 6);
        float? reagent_scale = Dm.arg<float?>(__extra, 7);
        stop_reactions ??= FALSE;
        reagent_scale ??= FOAM_REAGENT_SCALE;
        await base.New(__extra);
        chemholder = Dm.@new<datum_reagents>(1000, NO_REACT);
        if (carry != null)
            await carry.trans_to(chemholder, carry.total_volume, no_react: stop_reactions, copy_only: TRUE);
        if (result_type != null)
            this.result_type = result_type;
        this.reagent_scale = Dm.num(reagent_scale);
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:235
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref chemholder);
        return base.Destroy(force);
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:239
    [DmParams("log", "lifetime", "slippery")]
    public override async DmTask<object> start(params object[] __extra)
    {
        object log = Dm.arg<object>(__extra, 0) ?? FALSE;
        object lifetime = Dm.arg<object>(__extra, 1);
        object slippery = Dm.arg<object>(__extra, 2);
        obj_effect_particle_effect_fluid_foam foam = Dm.@new(effect_type, location, Dm.@new<datum_fluid_group>(amount), null, lifetime, slippery) as obj_effect_particle_effect_fluid_foam;
        object foamcolor = mix_color_from_reagents(chemholder.reagent_list);
        if (reagent_scale > 1) // Make room in case we were created by a particularly stuffed payload.
            (foam.reagents as datum_reagents).maximum_volume *= reagent_scale;
        await chemholder.trans_to(foam, chemholder.total_volume, reagent_scale, copy_only: TRUE); // Foam has an amplifying effect on the reagents it is supplied with. This is balanced by the reagents being diluted as the area the foam covers increases.
        await foam.add_atom_colour(foamcolor, FIXED_COLOUR_PRIORITY);
        if (result_type != null)
            foam.result_type = result_type;
        if (Dm.truthy(log))
            await help_out_the_admins(foam, holder as atom, location as atom);
        SSfoam?.queue_spread(foam); // ?: a world whose Master has not made the subsystem (a test room), as tg always has
        return null;
    }
}

// Firefighting foam
/// A variant of foam which absorbs plasma in the air if there is a fire.
// tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:274
public partial class obj_effect_particle_effect_fluid_foam_firefighting : obj_effect_particle_effect_fluid_foam
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:282
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RemoveElement(typeof(datum_element_atmos_sensitive));
        return dot;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:286
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        // ..(): the parent's process runs to its first sleep, as the parent's wrapper returns then (waiting-model.md 5.4)
        base.process(seconds_per_tick);

        turf_open location = loc as turf_open;
        if (!istype(location, typeof(turf_open)))
            return null;

        obj_effect_hotspot hotspot = Dm.locate<obj_effect_hotspot>(location);
        if (!(hotspot != null && location.air != null))
            return null;

        QDEL_NULL(ref hotspot);
        datum_gas_mixture air = location.air;
        if (Dm.truthy(air.moles[typeof(datum_gas_plasma)]))
        {
            float scrub_amt = Dm.min(30, Dm.num(air.moles[typeof(datum_gas_plasma)])); //Absorb some plasma
            air.adjust_gas(typeof(datum_gas_plasma), -scrub_amt);
            absorbed_plasma += scrub_amt;
        }
        if (air.temperature > T20C)
            air.temperature = Dm.max(air.temperature / 2, T20C);
        air.garbage_collect();
        location.air_update_turf(FALSE, FALSE);
        return null;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:309
    public override async DmTask<object> make_result()
    {
        atom_movable deposit = await base.make_result() as atom_movable;
        if (istype(deposit, typeof(atom_movable)) && deposit.reagents != null && absorbed_plasma > 0)
        {
            await (deposit.reagents as datum_reagents).add_reagent(typeof(datum_reagent_stable_plasma), absorbed_plasma);
            absorbed_plasma = 0;
        }
        return deposit;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:316
    public override async DmTask<object> foam_mob(mob_living foaming = null, object seconds_per_tick = null)
    {
        if (!istype(foaming, typeof(mob_living)))
            return null;
        await foaming.adjust_wet_stacks(2);
        return null;
    }
}

// Smart metal foam
// tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:398
public partial class obj_effect_particle_effect_fluid_foam_metal_smart : obj_effect_particle_effect_fluid_foam_metal
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:406
    public override async DmTask<object> make_result() //Smart foam adheres to area borders for walls
    {
        turf_open location = loc as turf_open;
        if (isspaceturf(location) || isopenspaceturf(location))
            await location.place_on_top(typeof(turf_open_floor_plating_foam));

        foreach (object cardinal in Dm.each(GLOB.cardinals))
        {
            turf cardinal_turf = get_step(location, Dm.num(cardinal));
            if (get_area(cardinal_turf) != get_area(location))
                return await base.make_result();
        }
        return null;
    }
}

/// FOAM STRUCTURE. Formed by metal foams. Dense and opaque, but easy to break
// tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:338
public partial class obj_structure_foamedmetal : obj_structure
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:355
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        air_update_turf(TRUE, TRUE);
        Waiting.NoSleep(AddElement(typeof(datum_element_uses_girder_wall_recipes)));
        return dot;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:360
    public override object Destroy(float? force = null)
    {
        air_update_turf(TRUE, FALSE);
        return base.Destroy(force);
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:364
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        turf T = loc as turf;
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        move_update_air(T);
        return dot;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:369
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:372
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        playsound(loc, Dm.resource("sound/items/weapons/tap.ogg"), 100, TRUE);
        return null;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:375
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        user.changeNext_move(CLICK_CD_MELEE);
        await user.do_attack_animation(this, ATTACK_EFFECT_PUNCH);
        to_chat(user, span_warning(Dm.text("You hit [] but bounce off it!", this)));
        playsound(loc, Dm.resource("sound/items/weapons/tap.ogg"), 100, TRUE);
        return dot;
    }
}

/// Atmos Backpack Resin, transparent, prevents atmos and filters the air
// tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:426
public partial class obj_structure_foamedmetal_resin : obj_structure_foamedmetal
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:428  var/static/list/ignored_gases (rules 10.6)
    private static DmList ignored_gases => TgState.Current.obj_structure_foamedmetal_resin__ignored_gases ??= typecacheof(Dm.list(
        typeof(datum_gas_nitrogen),
        typeof(datum_gas_oxygen),
        typeof(datum_gas_pluoxium),
        typeof(datum_gas_halon))) as DmList;

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:435
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        turf_open location = loc as turf_open;
        if (!istype(location, typeof(turf_open)))
            return dot;

        location.ClearWet();
        location.temperature = T20C;
        if (location.air != null)
        {
            datum_gas_mixture air = location.air;
            air.temperature = T20C;
            foreach (obj_effect_hotspot fire in Dm.each<obj_effect_hotspot>(location))
                qdel(fire);

            DmList cached_moles = air.moles;
            foreach (object gas_id in Dm.each(cached_moles))
            {
                if (!Dm.truthy(ignored_gases[gas_id]))
                    cached_moles[gas_id] = 0f;
            }
            air.garbage_collect();
        }

        foreach (obj_machinery_atmospherics_components_unary comp in Dm.each<obj_machinery_atmospherics_components_unary>(location))
        {
            if (!Dm.truthy(comp.welded))
            {
                comp.welded = TRUE;
                comp.update_appearance();
                comp.visible_message(span_danger(Dm.text("[] sealed shut!", comp)));
            }
        }

        foreach (mob_living potential_tinder in Dm.each<mob_living>(location))
            potential_tinder.extinguish_mob();
        foreach (obj_item potential_tinder in Dm.each<obj_item>(location))
            Waiting.NoSleep(potential_tinder.extinguish());
        return dot;
    }
}

/// A variant of resin foam that is created from halon combustion. It does not dissolve in heat to allow the gas to spread before foaming.
// tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:477
public partial class obj_effect_particle_effect_fluid_foam_metal_resin_halon : obj_effect_particle_effect_fluid_foam_metal_resin
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:479
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RemoveElement(typeof(datum_element_atmos_sensitive)); // Doesn't dissolve in heat.
        return dot;
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:483
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return FALSE; // Doesn't dissolve in heat.
    }

    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:486
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return null; // Doesn't dissolve in heat.
    }
}

// tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:495
public partial class obj_effect_spawner_foam_starter : obj_effect_spawner
{
    // tg: code/game/objects/effects/effect_system/fluid_spread/effects_foam.dm:499
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_effect_system_fluid_spread_foam foam = Dm.@new(foam_type, loc, foam_size, null, this) as datum_effect_system_fluid_spread_foam; // new foam_type(loc, foam_size, holder = src)
        Waiting.NoSleep(foam.start());
        return dot;
    }
}
