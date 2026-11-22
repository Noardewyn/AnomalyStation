// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/weather_listener.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2a (D327): the whole file: every logged-in mob listens for its storm types (ash, rain, sand, snow) while on a level of their trait,
// through an area sound manager that changes its loop when a storm of that type telegraphs, starts, winds down or ends. The var block is
// generated (D111).

///This element just handles creating and destroying an area sound manager that's hooked into weather stuff
// tg: code/datums/elements/weather_listener.dm:2
public partial class datum_element_weather_listener : datum_element
{
    // tg: code/datums/elements/weather_listener.dm:14
    [DmParams("target", "w_type", "trait", "weather_playlist")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        var w_type = Dm.arg(__extra, 0) as Type;
        object trait = Dm.arg(__extra, 1);
        var weather_playlist = Dm.arg(__extra, 2) as DmList;
        object dot = await base.Attach(target, __extra);
        if (weather_type == null)
        {
            weather_type = w_type;
            sound_change_signals ??= Dm.list();
            foreach (Type type in Dm.each<Type>(typesof(weather_type)))
            {
                Dm.append(sound_change_signals, Dm.list(
                    COMSIG_WEATHER_TELEGRAPH(type),
                    COMSIG_WEATHER_START(type),
                    COMSIG_WEATHER_WINDDOWN(type),
                    COMSIG_WEATHER_END(type)));
            }
            weather_trait = trait;
            playlist = weather_playlist;
        }
        RegisterSignal(target, COMSIG_MOVABLE_Z_CHANGED, nameof(handle_z_level_change), @override: TRUE);
        RegisterSignal(target, COMSIG_MOB_LOGOUT, nameof(handle_logout), @override: TRUE);
        var target_mob = (mob)target;
        handle_z_level_change(target_mob, null, target_mob.loc as turf);
        return dot;
    }

    // tg: code/datums/elements/weather_listener.dm:32
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_MOVABLE_Z_CHANGED, COMSIG_MOB_LOGOUT));
        return dot;
    }

    // tg: code/datums/elements/weather_listener.dm:36
    [SignalHandler]
    public virtual void handle_z_level_change(datum source = null, turf old_loc = null, turf new_loc = null)
    {
        var fitting_z_levels = SSmapping.levels_by_trait(weather_trait) as DmList;
        if (new_loc == null || !Dm.@in(new_loc.z, fitting_z_levels))
            return;
        var our_comp = Waiting.NoSleep(source.AddComponent(
            typeof(datum_component_area_sound_manager),
            Dm.named("area_loop_pairs", playlist),
            Dm.named("remove_on", COMSIG_MOB_LOGOUT),
            Dm.named("acceptable_zs", fitting_z_levels))) as datum_component_area_sound_manager;
        our_comp?.RegisterSignals(SSdcs, sound_change_signals, nameof(datum_component_area_sound_manager.handle_change));
    }

    // tg: code/datums/elements/weather_listener.dm:48
    [SignalHandler]
    public virtual void handle_logout(datum source = null)
    {
        source.RemoveElement(typeof(datum_element_weather_listener), weather_type, weather_trait, playlist);
    }
}
