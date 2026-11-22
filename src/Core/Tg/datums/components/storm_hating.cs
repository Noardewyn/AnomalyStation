// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/storm_hating.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4b (D327): the whole file (the space vines fade away when an ash, snow or void storm begins or ends in their area). The var
// block is generated (D111).

/**
 * The parent of this component will be destroyed if it's on the ground during a storm
 */
// tg: code/datums/components/storm_hating.dm:4
public partial class datum_component_storm_hating : datum_component
{
    /// Types of weather which trigger the effect (var/static/list, rules 10.6)
    // tg: code/datums/components/storm_hating.dm:6
    public static ref DmList stormy_weather => ref TgState.Current.datum_component_storm_hating__stormy_weather;

    // tg: code/datums/components/storm_hating.dm:12
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object dot = await base.Initialize(__extra);
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;
        on_area_entered(parent as atom, get_area(parent) as area);
        return dot;
    }

    // tg: code/datums/components/storm_hating.dm:18
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_ENTER_AREA, nameof(on_area_entered));
        RegisterSignal(parent, COMSIG_EXIT_AREA, nameof(on_area_exited));
        RegisterSignal(parent, COMSIG_MOB_LOGIN, nameof(on_login));
    }

    // tg: code/datums/components/storm_hating.dm:24
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, Dm.list(COMSIG_ENTER_AREA, COMSIG_EXIT_AREA));
        area old_area = get_area(parent) as area;
        if (old_area != null)
            on_area_exited(parent as atom, old_area);
    }

    // tg: code/datums/components/storm_hating.dm:31
    [SignalHandler]
    public virtual void on_area_entered(atom source = null, area new_area = null)
    {
        foreach (object weather in Dm.each(stormy_weather))
        {
            RegisterSignal(new_area, COMSIG_WEATHER_BEGAN_IN_AREA(weather), nameof(on_storm_event));
            RegisterSignal(new_area, COMSIG_WEATHER_ENDED_IN_AREA(weather), nameof(on_storm_event));
        }
    }

    // tg: code/datums/components/storm_hating.dm:37
    [SignalHandler]
    public virtual void on_area_exited(atom source = null, area old_area = null)
    {
        foreach (object weather in Dm.each(stormy_weather))
        {
            UnregisterSignal(old_area, COMSIG_WEATHER_BEGAN_IN_AREA(weather));
            UnregisterSignal(old_area, COMSIG_WEATHER_ENDED_IN_AREA(weather));
        }
    }

    // tg: code/datums/components/storm_hating.dm:43
    [SignalHandler]
    public virtual void on_storm_event()
    {
        atom parent_atom = parent as atom;
        if (!isturf(parent_atom.loc))
            return;
        parent_atom.fade_into_nothing(life_time: 3 * SECONDS, fade_time: 2 * SECONDS);
        qdel(this);
    }

    // tg: code/datums/components/storm_hating.dm:51
    [SignalHandler]
    public virtual void on_login(datum source = null)
    {
        qdel(this);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/storm_hating.dm:6  var/static/list/stormy_weather
    internal DmList datum_component_storm_hating__stormy_weather = Dm.list(
        typeof(datum_weather_particle_ash_storm),
        typeof(datum_weather_snow_storm),
        typeof(datum_weather_void_storm));
}
