// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/ai_listen_to_weather.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * given to a mob to set a key on or off when a storm is coming or ending
 */
// tg: code/datums/components/ai_listen_to_weather.dm:4
public partial class datum_component_ai_listen_to_weather : datum_component
{
    // tg: code/datums/components/ai_listen_to_weather.dm:10
    [DmParams("weather_type", "weather_key")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        Type weather_type = Dm.arg<Type>(__extra, 0) ?? typeof(datum_weather_particle_ash_storm);
        object weather_key = Dm.arg(__extra, 1) ?? BB_STORM_APPROACHING;
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        this.weather_type = weather_type;
        this.weather_key = weather_key;
        return null;
    }

    // tg: code/datums/components/ai_listen_to_weather.dm:16
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(SSdcs, COMSIG_WEATHER_START(weather_type), nameof(storm_start));
        RegisterSignal(SSdcs, COMSIG_WEATHER_END(weather_type), nameof(storm_end));
    }

    // tg: code/datums/components/ai_listen_to_weather.dm:20
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(SSdcs, Dm.list(COMSIG_WEATHER_START(weather_type), COMSIG_WEATHER_END(weather_type)));
    }

    // tg: code/datums/components/ai_listen_to_weather.dm:23
    [SignalHandler]
    public virtual void storm_start()
    {
        mob_living_basic source = parent as mob_living_basic;
        datum_ai_controller controller = source.ai_controller as datum_ai_controller;
        if (controller == null)
            return;
        Waiting.NoSleep(controller.cancel_current_plan());
        controller.set_blackboard_key(weather_key, TRUE);
    }

    // tg: code/datums/components/ai_listen_to_weather.dm:32
    [SignalHandler]
    public virtual void storm_end()
    {
        mob_living_basic source = parent as mob_living_basic;
        (source.ai_controller as datum_ai_controller)?.set_blackboard_key(weather_key, FALSE);
    }
}
