// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/floor/misc_floor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps G2, D291): the circuit floors (they glow while their area has light power and draw 120 W of it, red while a
// nuke threatens the station), the high-traction, plastic, retro and engraved (cult) floors. Not here, so they stay inert
// with their own milestones: the tram's high-traction floor (holiday colours), the rusted platings (the rust element, heretics)
// and the living meat floor (custom materials).

// Usage for a bar light is 160, let's do a bit less then that since these tend to be used a lot in one place
//Circuit flooring, glows a little
// tg: code/game/turfs/open/floor/misc_floor.dm:4
public partial class turf_open_floor_circuit : turf_open_floor
{
    // tg: code/game/turfs/open/floor/misc_floor.dm:2  #define CIRCUIT_FLOOR_POWERUSE 120 (#undef at :72)
    private const int CIRCUIT_FLOOR_POWERUSE = 120;

    // tg: code/game/turfs/open/floor/misc_floor.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.append(ref SSmapping.nuke_tiles, this);
        RegisterSignal(loc as datum, COMSIG_AREA_POWER_CHANGE, nameof(handle_powerchange));
        area cur_area = get_area(this) as area;
        if (cur_area != null)
            handle_powerchange(cur_area, TRUE);
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/turfs/open/floor/misc_floor.dm:26
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref SSmapping.nuke_tiles, this);
        UnregisterSignal(loc as datum, COMSIG_AREA_POWER_CHANGE);
        area cur_area = get_area(this) as area;
        if (Dm.truthy(on) && cur_area != null)
            cur_area.removeStaticPower(CIRCUIT_FLOOR_POWERUSE, AREA_USAGE_STATIC_LIGHT);
        return base.Destroy(force);
    }

    // tg: code/game/turfs/open/floor/misc_floor.dm:34
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        if (!Dm.truthy(on))
        {
            Waiting.NoSleep(set_light(0));
            return dot;
        }

        set_light_color(Dm.truthy(LAZYLEN(SSmapping.nuke_threats)) ? LIGHT_COLOR_INTENSE_RED : Dm.initial(this, nameof(light_color)));
        Waiting.NoSleep(set_light(2, 1.5f));
        return dot;
    }

    // tg: code/game/turfs/open/floor/misc_floor.dm:43
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.truthy(on) ? (Dm.truthy(LAZYLEN(SSmapping.nuke_threats)) ? "rcircuitanim" : (string)Dm.initial(this, nameof(icon_state))) : Dm.text("[]off", base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/turfs/open/floor/misc_floor.dm:47
    public override void on_change_area(area old_area = null, area new_area = null)
    {
        base.on_change_area(old_area, new_area);
        UnregisterSignal(old_area, COMSIG_AREA_POWER_CHANGE);
        RegisterSignal(new_area, COMSIG_AREA_POWER_CHANGE, nameof(handle_powerchange));
        if (Dm.truthy(on))
            old_area.removeStaticPower(CIRCUIT_FLOOR_POWERUSE, AREA_USAGE_STATIC_LIGHT);
        handle_powerchange(new_area);
    }

    /// Enables/disables our lighting based off our source area
    // tg: code/game/turfs/open/floor/misc_floor.dm:56  (SIGNAL_HANDLER is dropped, rules 7.9)
    [SignalHandler]
    public virtual void handle_powerchange(area source = null, float? mapload = null)
    {
        mapload ??= FALSE;
        object old_on = on;
        if (Dm.truthy(always_off))
            on = FALSE;
        else
            on = Dm.truthy(source.powered(AREA_USAGE_LIGHT)) ? TRUE : FALSE;
        if (Dm.eq(on, old_on))
            return;

        if (Dm.truthy(on))
            source.addStaticPower(CIRCUIT_FLOOR_POWERUSE, AREA_USAGE_STATIC_LIGHT);
        else if (!Dm.truthy(mapload))
            source.removeStaticPower(CIRCUIT_FLOOR_POWERUSE, AREA_USAGE_STATIC_LIGHT);
        update_appearance();
    }
}

// tg: code/game/turfs/open/floor/misc_floor.dm:139
public partial class turf_open_floor_noslip : turf_open_floor
{
    // tg: code/game/turfs/open/floor/misc_floor.dm:145
    public override object broken_states()
    {
        return Dm.list("noslip-damaged1", "noslip-damaged2", "noslip-damaged3");
    }

    // tg: code/game/turfs/open/floor/misc_floor.dm:148
    public override object burnt_states()
    {
        return Dm.list("noslip-scorched1", "noslip-scorched2");
    }

    // tg: code/game/turfs/open/floor/misc_floor.dm:151
    public override async DmTask MakeSlippery(float? wet_setting = null, float? min_wet_time = null, float? wet_time_to_add = null, float? max_wet_time = null, float? permanent = null, float? should_display_overlay = null)
    {
        return;
    }
}

// tg: code/game/turfs/open/floor/misc_floor.dm:154
public partial class turf_open_floor_noslip_tram : turf_open_floor_noslip
{
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object current_holiday_color = request_decoration_colors(this, PATTERN_VERTICAL_STRIPE); // gaps R3
        if (Dm.truthy(current_holiday_color))
            color = current_holiday_color;
        else
            color = "#EFB341";
        return dot;
    }
}

// tg: code/game/turfs/open/floor/misc_floor.dm:216
public partial class turf_open_floor_plastic : turf_open_floor
{
    // tg: code/game/turfs/open/floor/misc_floor.dm:225
    public override object broken_states()
    {
        return Dm.list("plastic-damaged1", "plastic-damaged2");
    }
}

// tg: code/game/turfs/open/floor/misc_floor.dm:252
public partial class turf_open_floor_eighties : turf_open_floor
{
    // tg: code/game/turfs/open/floor/misc_floor.dm:259
    public override object broken_states()
    {
        return Dm.list("eighties_damaged");
    }
}

// tg: code/game/turfs/open/floor/misc_floor.dm:262
public partial class turf_open_floor_eighties_red : turf_open_floor_eighties
{
    // tg: code/game/turfs/open/floor/misc_floor.dm:268
    public override object broken_states()
    {
        return Dm.list("eightiesred_damaged");
    }
}

// tg: code/game/turfs/open/floor/misc_floor.dm:335
public partial class turf_open_floor_cult : turf_open_floor
{
    // tg: code/game/turfs/open/floor/misc_floor.dm:341
    public override object broken_states()
    {
        return Dm.list("cultdamage", "cultdamage2", "cultdamage3", "cultdamage4", "cultdamage5", "cultdamage6", "cultdamage7");
    }

    // tg: code/game/turfs/open/floor/misc_floor.dm:344
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        return null;
    }
}

// tg: code/game/turfs/open/floor/misc_floor.dm:270  (the shoe factory)
public partial class turf_open_floor_plating_rust : turf_open_floor_plating
{
    // tg: code/game/turfs/open/floor/misc_floor.dm:276
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_rust)));
        color = null;
        return dot;
    }
}
