// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/wet_floor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 3 (wet floors and slipping: the condensation of a fire's water vapour wets the floor): the whole file. The var
// block is generated; the four static overlays are TgState fields made on first use (rules 10.6).

// tg: code/datums/components/wet_floor.dm:1
public partial class datum_component_wet_floor : datum_component
{
    // tg: code/datums/components/wet_floor.dm:7  var/static/mutable_appearance/permafrost_overlay = mutable_appearance('icons/effects/water.dmi', "ice_floor")
    public static mutable_appearance permafrost_overlay => TgState.Current.datum_component_wet_floor__permafrost_overlay ??= (mutable_appearance)mutable_appearance(Dm.resource("icons/effects/water.dmi"), "ice_floor");
    // tg: code/datums/components/wet_floor.dm:8  var/static/mutable_appearance/ice_overlay = mutable_appearance('icons/turf/overlays.dmi', "snowfloor")
    public static mutable_appearance ice_overlay => TgState.Current.datum_component_wet_floor__ice_overlay ??= (mutable_appearance)mutable_appearance(Dm.resource("icons/turf/overlays.dmi"), "snowfloor");
    // tg: code/datums/components/wet_floor.dm:9  var/static/mutable_appearance/water_overlay = mutable_appearance('icons/effects/water.dmi', "wet_floor_static")
    public static mutable_appearance water_overlay => TgState.Current.datum_component_wet_floor__water_overlay ??= (mutable_appearance)mutable_appearance(Dm.resource("icons/effects/water.dmi"), "wet_floor_static");
    // tg: code/datums/components/wet_floor.dm:10  var/static/mutable_appearance/generic_turf_overlay = mutable_appearance('icons/effects/water.dmi', "wet_static")
    public static mutable_appearance generic_turf_overlay => TgState.Current.datum_component_wet_floor__generic_turf_overlay ??= (mutable_appearance)mutable_appearance(Dm.resource("icons/effects/water.dmi"), "wet_static");

    // tg: code/datums/components/wet_floor.dm:18
    [DmParams("newcomp", "orig", "strength", "duration_minimum", "duration_add", "duration_maximum", "_permanent", "_should_display_overlay")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        datum_component newcomp = C;
        if (!Dm.truthy(newcomp)) //We are getting passed the arguments of a would-be new component, but not a new component
        {
            // add_wet(arglist(args.Copy(3))): strength, duration_minimum, duration_add, duration_maximum, _permanent, _should_display_overlay
            await add_wet(Dm.arg(__extra, 0), Dm.arg<float?>(__extra, 1), Dm.arg<float?>(__extra, 2), Dm.arg<float?>(__extra, 3), Dm.arg<float?>(__extra, 4), Dm.arg<float?>(__extra, 5));
        }
        else //We are being passed in a full blown component
        {
            datum_component_wet_floor WF = (datum_component_wet_floor)newcomp; //Lets make an assumption
            if (Dm.truthy(WF.gc())) //See if it's even valid, still. Also does LAZYLEN and stuff for us.
                throw Dm.CRASH("Wet floor component tried to inherit another, but the other was able to garbage collect while being inherited! What a waste of time!", "code/datums/components/wet_floor.dm", 24);
            foreach (object i in Dm.each(WF.time_left_list))
                await add_wet(text2num(i), Dm.num(WF.time_left_list[i]));
        }
    }

    // tg: code/datums/components/wet_floor.dm:28
    [DmParams("strength", "duration_minimum", "duration_add", "duration_maximum", "_permanent", "_should_display_overlay")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object strength = Dm.arg(__extra, 0);
        float? duration_minimum = Dm.arg<float?>(__extra, 1);
        float? duration_add = Dm.arg<float?>(__extra, 2);
        float? duration_maximum = Dm.arg<float?>(__extra, 3);
        float _permanent = Dm.arg<float?>(__extra, 4) ?? FALSE;
        float _should_display_overlay = Dm.arg<float?>(__extra, 5) ?? TRUE;
        if (!isopenturf(parent))
            return COMPONENT_INCOMPATIBLE;
        should_display_overlay = _should_display_overlay;
        await add_wet(strength, duration_minimum, duration_add, duration_maximum, _permanent, _should_display_overlay);
        permanent = _permanent;
        if (!Dm.truthy(permanent))
            START_PROCESSING(SSwet_floors, this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(gc), TRUE), 1, file: "code/datums/components/wet_floor.dm", line: 36); //GC after initialization.
        last_process = world.time;
        return null;
    }

    // tg: code/datums/components/wet_floor.dm:39
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_TURF_IS_WET, nameof(is_wet));
        RegisterSignal(parent, COMSIG_TURF_MAKE_DRY, nameof(dry));
    }

    // tg: code/datums/components/wet_floor.dm:43
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_TURF_IS_WET, COMSIG_TURF_MAKE_DRY));
    }

    // tg: code/datums/components/wet_floor.dm:46
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSwet_floors, this);
        turf T = parent as turf;
        qdel(T?.GetComponent(typeof(datum_component_slippery)));
        if (T is turf) //If this is false there is so many things wrong with it.
            T.cut_overlay(current_overlay);
        else
            stack_trace("Warning: Wet floor component wasn't on a turf when being destroyed! This is really bad!", "code/datums/components/wet_floor.dm", 53);
        return base.Destroy(force);
    }

    // tg: code/datums/components/wet_floor.dm:56
    public virtual void update_overlay()
    {
        turf parent_turf = (turf)parent;
        if (!Dm.truthy(should_display_overlay))
        {
            if (!Dm.truthy(current_overlay))
                return;

            parent_turf.cut_overlay(current_overlay);
            current_overlay = null;
            return;
        }

        object intended;
        float sw = highest_strength;
        if (sw == TURF_WET_PERMAFROST)
            intended = permafrost_overlay;
        else if (sw == TURF_WET_ICE)
        {
            // Should really get someone to make a generic_ice_overlay.
            intended = Dm.truthy(parent_turf.tiled_turf) ? ice_overlay : generic_turf_overlay;
        }
        else
            intended = Dm.truthy(parent_turf.tiled_turf) ? water_overlay : generic_turf_overlay;
        if (current_overlay != intended)
        {
            parent_turf.cut_overlay(current_overlay);
            parent_turf.add_overlay(intended);
            current_overlay = intended;
        }
    }

    // tg: code/datums/components/wet_floor.dm:79
    public virtual async DmTask AfterSlip(mob_living slipped = null)
    {
        if (highest_strength != TURF_WET_LUBE)
            return;

        await slipped.set_timed_status_effect(8 * SECONDS, typeof(datum_status_effect_confusion), TRUE); // set_confusion_if_lower (__DEFINES/status_effects.dm:169), inline
    }

    // tg: code/datums/components/wet_floor.dm:85
    public virtual async DmTask update_flags()
    {
        float intensity;
        lube_flags = SLIPPERY_TURF;
        float sw = highest_strength;
        if (sw == TURF_WET_WATER)
        {
            intensity = 60;
            lube_flags = Dm.bitor(lube_flags, NO_SLIP_WHEN_WALKING);
        }
        else if (sw == TURF_WET_LUBE)
        {
            intensity = 80;
            lube_flags = Dm.bitor(lube_flags, Dm.bitor(SLIDE, GALOSHES_DONT_HELP));
        }
        else if (sw == TURF_WET_ICE)
        {
            intensity = 120;
            lube_flags = Dm.bitor(lube_flags, Dm.bitor(SLIDE, GALOSHES_DONT_HELP));
        }
        else if (sw == TURF_WET_PERMAFROST)
        {
            intensity = 120;
            lube_flags = Dm.bitor(lube_flags, Dm.bitor(SLIDE_ICE, GALOSHES_DONT_HELP));
        }
        else if (sw == TURF_WET_SUPERLUBE)
        {
            intensity = 120;
            lube_flags = Dm.bitor(lube_flags, Dm.bitor(SLIDE, Dm.bitor(GALOSHES_DONT_HELP, SLIP_WHEN_CRAWLING)));
        }
        else
        {
            qdel(parent.GetComponent(typeof(datum_component_slippery)));
            return;
        }

        await parent.LoadComponent(typeof(datum_component_slippery), intensity, lube_flags, Dm.@new<datum_callback>(this, nameof(AfterSlip)));
    }

    // tg: code/datums/components/wet_floor.dm:111
    [SignalHandler]
    public virtual void dry(datum source = null, float? strength = null, float? immediate = null, float? duration_decrease = null)
    {
        // SIGNAL_HANDLER
        strength ??= TURF_WET_WATER;
        immediate ??= FALSE;
        duration_decrease ??= INFINITY;

        foreach (object i in Dm.each(time_left_list))
        {
            if (Dm.num(text2num(i)) <= strength)
                time_left_list[i] = max(0, Dm.num(time_left_list[i]) - duration_decrease.Value);
        }
        if (Dm.truthy(immediate))
            Waiting.NoSleep(check());
    }

    // tg: code/datums/components/wet_floor.dm:120
    public virtual object max_time_left()
    {
        float dot = 0;
        foreach (object i in Dm.each(time_left_list))
            dot = max(dot, Dm.num(time_left_list[i]));
        return dot;
    }

    // tg: code/datums/components/wet_floor.dm:125
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        turf_open T = (turf_open)parent;
        float diff = world.time - last_process;
        float decrease = 0;
        float t = Dm.num(T.GetTemperature());
        if (t <= T0C) // -INFINITY to T0C
            Waiting.NoSleep(add_wet(TURF_WET_ICE, Dm.num(max_time_left()))); //Water freezes into ice!
        else if (t >= T0C && t <= T0C + 100) // T0C to T0C + 100
            decrease = ((T.air.temperature - T0C) / SSwet_floors.temperature_coeff) * (diff / SSwet_floors.time_ratio);
        else if (t >= T0C + 100) // T0C + 100 to INFINITY
            decrease = INFINITY;
        decrease = max(0, decrease);
        if (Dm.truthy(Dm.bitand(is_wet(), TURF_WET_ICE)) && t > T0C) //Ice melts into water!
        {
            foreach (obj O in Dm.each<obj>(T.contents))
                O.unfreeze();
            Waiting.NoSleep(add_wet(TURF_WET_WATER, Dm.num(max_time_left())));
            dry(null, TURF_WET_ICE);
        }
        dry(null, ALL, FALSE, decrease);
        Waiting.NoSleep(check());
        last_process = world.time;
        return null;
    }

    // tg: code/datums/components/wet_floor.dm:145
    public virtual void update_strength()
    {
        highest_strength = 0; //Not bitflag.
        foreach (object i in Dm.each(time_left_list))
            highest_strength = max(highest_strength, Dm.num(text2num(i)));
    }

    // tg: code/datums/components/wet_floor.dm:150
    [SignalHandler]
    public virtual object is_wet()
    {
        // SIGNAL_HANDLER

        float dot = 0;
        foreach (object i in Dm.each(time_left_list))
            dot = Dm.bitor(dot, Dm.num(text2num(i)));
        return dot;
    }

    // tg: code/datums/components/wet_floor.dm:157
    public override async DmTask PreTransfer(datum new_parent = null)
    {
        turf O = (turf)parent;
        O.cut_overlay(current_overlay);
        //That turf is no longer slippery, we're out of here
        //Slippery components don't transfer due to callbacks
        qdel(O.GetComponent(typeof(datum_component_slippery)));
    }

    // tg: code/datums/components/wet_floor.dm:164
    public override async DmTask<object> PostTransfer(datum new_parent = null)
    {
        if (!isopenturf(parent))
            return COMPONENT_INCOMPATIBLE;
        turf T = (turf)parent;
        T.add_overlay(current_overlay);
        //Make sure to add/update any slippery component on the new turf (update_flags calls LoadComponent)
        await update_flags();

        //NB it's possible we get deleted after this, due to inherit
        return null;
    }

    // tg: code/datums/components/wet_floor.dm:174
    public virtual async DmTask<object> add_wet(object type = null, float? duration_minimum = null, float? duration_add = null, float? duration_maximum = null, float? _permanent = null, float? _should_display_overlay = null)
    {
        duration_minimum ??= 0;
        duration_add ??= 0;
        duration_maximum ??= MAXIMUM_WET_TIME;
        _permanent ??= FALSE;
        _should_display_overlay ??= TRUE;
        DmList allowed_types = add_wet__allowed_types;
        object dot = null;
        if (duration_minimum <= 0 || !Dm.truthy(type))
            return FALSE;
        if (Dm.@in(type, allowed_types))
            return await _do_add_wet(type, duration_minimum, duration_add, duration_maximum);
        else
        {
            dot = NONE;
            foreach (object i in Dm.each(allowed_types))
            {
                if (!Dm.truthy(Dm.bitand(type, i)))
                    continue;
                dot = Dm.bitor(dot, await _do_add_wet(i, duration_minimum, duration_add, duration_maximum));
            }
        }
        if (Dm.truthy(_permanent))
        {
            permanent = TRUE;
            STOP_PROCESSING(SSwet_floors, this);
        }

        should_display_overlay = _should_display_overlay.Value;
        return dot;
    }

    // tg: code/datums/components/wet_floor.dm:175  var/static/list/allowed_types = list(TURF_WET_WATER, TURF_WET_LUBE, TURF_WET_ICE, TURF_WET_PERMAFROST, TURF_WET_SUPERLUBE)
    private static DmList add_wet__allowed_types => TgState.Current.datum_component_wet_floor__add_wet__allowed_types;

    // tg: code/datums/components/wet_floor.dm:192
    public virtual async DmTask<object> _do_add_wet(object type = null, object duration_minimum = null, object duration_add = null, object duration_maximum = null)
    {
        float time = 0;
        string key = Dm.text("[]", type);
        if (Dm.truthy(LAZYACCESS(time_left_list, key)))
            time = clamp(Dm.num(LAZYACCESS(time_left_list, key)) + Dm.num(duration_add), Dm.num(duration_minimum), Dm.num(duration_maximum));
        else
            time = min(Dm.num(duration_minimum), Dm.num(duration_maximum));
        LAZYSET(ref time_left_list, key, time);
        await check(TRUE);
        return TRUE;
    }

    // tg: code/datums/components/wet_floor.dm:202
    public virtual object gc(float? on_init = null)
    {
        on_init ??= FALSE;
        if (!Dm.truthy(LAZYLEN(time_left_list)))
        {
            if (Dm.truthy(on_init))
            {
                turf T = parent as turf;
                // GAP(lead): AREACOORD(T) (__DEFINES/admin.dm:67, Admin_Coordinates_Readable) is not declared: the turf's coordinates
                // stand in for it in this warning.
                stack_trace(Dm.text("Warning: Wet floor component gc'd right after initialization! What a waste of time and CPU! Type = [], Location = [].", T != null ? T.type : "ERROR - NO PARENT", T is turf ? Dm.text("([],[],[])", T.x, T.y, T.z) : "ERROR - INVALID PARENT"), "code/datums/components/wet_floor.dm", 208);
            }
            qdel(this);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/datums/components/wet_floor.dm:212
    public virtual async DmTask check(float? force_update = null)
    {
        force_update ??= FALSE;
        bool changed = false;
        foreach (object i in Dm.each(time_left_list))
        {
            if (Dm.num(time_left_list[i]) <= 0)
            {
                Dm.remove(ref time_left_list, i);
                changed = true;
            }
        }
        if (changed || Dm.truthy(force_update))
        {
            update_strength();
            update_overlay();
            await update_flags();
            gc();
        }
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/wet_floor.dm:7-10  the static overlays (made on first use: datum_component_wet_floor)
    internal mutable_appearance datum_component_wet_floor__permafrost_overlay;
    internal mutable_appearance datum_component_wet_floor__ice_overlay;
    internal mutable_appearance datum_component_wet_floor__water_overlay;
    internal mutable_appearance datum_component_wet_floor__generic_turf_overlay;

    // tg: code/datums/components/wet_floor.dm:175  var/static/list/allowed_types
    internal DmList datum_component_wet_floor__add_wet__allowed_types = Dm.list(TURF_WET_WATER, TURF_WET_LUBE, TURF_WET_ICE, TURF_WET_PERMAFROST, TURF_WET_SUPERLUBE);
}
