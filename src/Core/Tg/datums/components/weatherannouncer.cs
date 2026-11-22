// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/weatherannouncer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16 (the barometric monitors): the whole file. The component polls the weather of the mining levels every second, shows
// the clear, incoming or danger overlay and light colour, says the change (through the AAS on the supply channel when one is set up),
// stops while its machine has no power and adds the forecast to the examine. It reads SSweather (gaps E2a); on a station without
// mining levels (MetaStation alone, as the port loads it) tg's own early return reports all clear. The var blocks are generated.

/// Component which makes you yell about what the weather is
// tg: code/datums/components/weatherannouncer.dm:9
public partial class datum_component_weather_announcer : datum_component
{
    // tg: code/datums/components/weatherannouncer.dm:1-6 (file-local, #undef at :257-262)
    private const float WEATHER_ALERT_CLEAR = 0;
    private const float WEATHER_ALERT_INCOMING = 1;
    private const float WEATHER_ALERT_IMMINENT_OR_ACTIVE = 2;
    private const float WEATHER_CLEAR_DELAY = 2 * MINUTES;
    private const float WEATHER_INCOMING_DELAY = 30 * SECONDS;

    // tg: code/datums/components/weatherannouncer.dm:32
    [DmParams("state_normal", "state_warning", "state_danger", "radar_z_trait")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object state_normal = Dm.arg(__extra, 0);
        object state_warning = Dm.arg(__extra, 1);
        object state_danger = Dm.arg(__extra, 2);
        object radar_z_trait = Dm.arg(__extra, 3);
        object dot = await base.Initialize(__extra);
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        START_PROCESSING(SSprocessing, this);
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_update_overlays));
        RegisterSignal(parent, COMSIG_MACHINERY_POWER_RESTORED, nameof(on_powered));
        RegisterSignal(parent, COMSIG_MACHINERY_POWER_LOST, nameof(on_power_lost));

        this.state_normal = state_normal;
        this.state_warning = state_warning;
        this.state_danger = state_danger;
        this.radar_z_trait = radar_z_trait;
        atom speaker = (atom)parent;
        speaker.update_appearance(UPDATE_ICON);
        update_light_color();
        return dot;
    }

    // tg: code/datums/components/weatherannouncer.dm:55
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSprocessing, this);
        return base.Destroy(force);
    }

    /// Add appropriate overlays
    // tg: code/datums/components/weatherannouncer.dm:60
    public virtual void on_update_overlays(atom parent_atom = null, DmList overlays = null)
    {
        if (!Dm.truthy(enabled) || !Dm.truthy(state_normal) || !Dm.truthy(state_warning) || !Dm.truthy(state_danger))
            return;

        if (warning_level == WEATHER_ALERT_CLEAR)
            Dm.append(overlays, state_normal);
        else if (warning_level == WEATHER_ALERT_INCOMING)
            Dm.append(overlays, state_warning);
        else if (warning_level == WEATHER_ALERT_IMMINENT_OR_ACTIVE)
            Dm.append(overlays, Dm.truthy(is_weather_dangerous) ? state_danger : state_warning);
    }

    /// If powered, receive updates
    // tg: code/datums/components/weatherannouncer.dm:74
    public virtual void on_powered()
    {
        enabled = TRUE;
        atom speaker = (atom)parent;
        speaker.update_appearance(UPDATE_ICON);
    }

    /// If no power, don't receive updates
    // tg: code/datums/components/weatherannouncer.dm:81
    public virtual void on_power_lost()
    {
        enabled = FALSE;
        atom speaker = (atom)parent;
        speaker.update_appearance(UPDATE_ICON);
    }

    // tg: code/datums/components/weatherannouncer.dm:87
    public virtual object check_accuracy()
    {
        foreach (object z_level in Dm.each(SSmapping.levels_by_trait(radar_z_trait)))
        {
            foreach (obj_machinery_power_weather_tower radar in Dm.each_cast<obj_machinery_power_weather_tower>(GLOB.weather_towers[Dm.text("[]", z_level)]))
            {
                if (Dm.truthy(radar.active))
                    return TRUE;
            }
        }

        return FALSE;
    }

    // tg: code/datums/components/weatherannouncer.dm:95
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(enabled))
            return null;

        float previous_level = warning_level;
        float previous_danger = is_weather_dangerous;
        set_current_alert_level();
        if (previous_level == warning_level && previous_danger == is_weather_dangerous)
            return null; // No change
        atom_movable speaker = (atom_movable)parent;
        object msg = get_warning_message();
        obj_machinery_announcement_system aas = get_announcement_system(typeof(datum_aas_config_entry_weather), speaker, Dm.list(RADIO_CHANNEL_SUPPLY));
        // Active AAS will override default announcement lines
        if (aas != null)
        {
            msg = aas.compile_config_message(typeof(datum_aas_config_entry_weather), Dm.list(), Dm.truthy(is_weather_dangerous) ? warning_level + 1 : 4);
            // Stop toggling on radios for it, please!
            Waiting.NoSleep(aas.broadcast(msg, Dm.list(RADIO_CHANNEL_SUPPLY)));
        }
        // Still say it, because you can be not on our level
        Waiting.NoSleep(speaker.say(msg));
        speaker.update_appearance(UPDATE_ICON);
        update_light_color();
        return null;
    }

    // tg: code/datums/components/weatherannouncer.dm:117
    public virtual void update_light_color()
    {
        atom_movable light = (atom_movable)parent;
        if (warning_level == WEATHER_ALERT_CLEAR)
            light.set_light_color(LIGHT_COLOR_GREEN);
        else if (warning_level == WEATHER_ALERT_INCOMING)
            light.set_light_color(LIGHT_COLOR_DIM_YELLOW);
        else if (warning_level == WEATHER_ALERT_IMMINENT_OR_ACTIVE)
        {
            if (Dm.truthy(is_weather_dangerous))
                light.set_light_color(LIGHT_COLOR_INTENSE_RED);
            else
                light.set_light_color(LIGHT_COLOR_DIM_YELLOW);
        }
        light.update_light();
    }

    /// Returns a string we should display to communicate what you should be doing
    // tg: code/datums/components/weatherannouncer.dm:132
    public virtual object get_warning_message()
    {
        if (warning_level == WEATHER_ALERT_CLEAR)
            return I18n.T("All clear, no weather alerts to report.");
        else if (warning_level == WEATHER_ALERT_INCOMING)
            return I18n.T("Weather front incoming, begin to seek shelter.");
        else if (warning_level == WEATHER_ALERT_IMMINENT_OR_ACTIVE)
        {
            if (!Dm.truthy(is_weather_dangerous))
                return I18n.T("No risk expected from incoming weather front.");
            return I18n.T("Weather front imminent, find shelter immediately.");
        }
        return I18n.T("Error in meteorological calculation. Please report this deviation to a trained programmer.");
    }

    // tg: code/datums/components/weatherannouncer.dm:144
    public virtual object time_till_storm()
    {
        DmList mining_z_levels = SSmapping.levels_by_trait(radar_z_trait) as DmList;
        if (!Dm.truthy(Dm.length(mining_z_levels)))
            return null; // No problems if there are no mining z levels

        foreach (datum_weather check_weather in Dm.each_cast<datum_weather>(SSweather.processing))
        {
            if (!Dm.truthy(Dm.bitand(check_weather.weather_flags, WEATHER_BAROMETER)) || check_weather.stage == WIND_DOWN_STAGE || check_weather.stage == END_STAGE)
                continue;

            foreach (object mining_level in Dm.each(mining_z_levels))
            {
                if (Dm.@in(mining_level, check_weather.impacted_z_levels))
                {
                    warning_level = WEATHER_ALERT_IMMINENT_OR_ACTIVE;
                    accuracy_mod = 0; // reset for next storm
                    return 0;
                }
            }
        }

        float time_until_next = INFINITY;
        foreach (object mining_level in Dm.each(mining_z_levels))
        {
            // About to start!
            if (Dm.truthy(SSweather.eligible_zlevels[Dm.text("[]", mining_level)]))
            {
                time_until_next = 0;
                break;
            }

            object next_time = timeleft(SSweather.next_hit_by_zlevel[Dm.text("[]", mining_level)]);
            if (next_time != null && Dm.num(next_time) < time_until_next)
                time_until_next = Dm.num(next_time);
        }

        if (Dm.truthy(check_accuracy()))
            return time_until_next;

        if (!Dm.truthy(accuracy_mod) && Dm.truthy(radar_z_trait))
            accuracy_mod = rand(-9, 9) * 5 * SECONDS;

        float maximum_value = INFINITY;
        // Don't backtrack on weather warnings every second of process() if we're poorly calibrated
        if (warning_level == WEATHER_ALERT_INCOMING)
            maximum_value = WEATHER_CLEAR_DELAY;
        else if (warning_level == WEATHER_ALERT_IMMINENT_OR_ACTIVE)
            maximum_value = WEATHER_INCOMING_DELAY;

        time_until_next = clamp(time_until_next + accuracy_mod, min(time_until_next, 10 * SECONDS), max(time_until_next, maximum_value));
        return time_until_next;
    }

    /// Polls existing weather for what kind of warnings we should be displaying.
    // tg: code/datums/components/weatherannouncer.dm:187
    public virtual void set_current_alert_level()
    {
        object time_until_next = time_till_storm();
        is_weather_dangerous = FALSE;

        if (time_until_next == null)
        {
            warning_level = WEATHER_ALERT_CLEAR;
            return; // No problems if there are no mining z levels
        }

        if (Dm.num(time_until_next) > WEATHER_CLEAR_DELAY)
        {
            warning_level = WEATHER_ALERT_CLEAR;
            return;
        }

        if (Dm.num(time_until_next) > WEATHER_INCOMING_DELAY)
        {
            warning_level = WEATHER_ALERT_INCOMING;
            return;
        }

        // Weather is here, now we need to figure out if it is dangerous
        warning_level = WEATHER_ALERT_IMMINENT_OR_ACTIVE;

        foreach (datum_weather check_weather in Dm.each_cast<datum_weather>(SSweather.processing))
        {
            if (!Dm.truthy(Dm.bitand(check_weather.weather_flags, WEATHER_BAROMETER)) || check_weather.stage == WIND_DOWN_STAGE || check_weather.stage == END_STAGE)
                continue;

            DmList mining_z_levels = SSmapping.levels_by_trait(radar_z_trait) as DmList;
            foreach (object mining_level in Dm.each(mining_z_levels))
            {
                if (!Dm.@in(mining_level, check_weather.impacted_z_levels))
                    continue;

                if (Dm.truthy(Dm.bitand(check_weather.weather_flags, FUNCTIONAL_WEATHER)))
                {
                    is_weather_dangerous = TRUE;
                    return;
                }
            }
        }
    }

    // tg: code/datums/components/weatherannouncer.dm:219
    public virtual void on_examine(atom radio = null, mob examiner = null, DmList examine_texts = null)
    {
        object time_until_next = time_till_storm();
        if (time_until_next == null)
            return;
        if (Dm.num(time_until_next) == 0)
            Dm.append(examine_texts, span_warning(I18n.T("A storm is currently active, please seek shelter.")));
        else
            Dm.append(examine_texts, span_notice(I18n.T("The next storm is inbound in [].", DisplayTimeText(time_until_next))));

        if (!Dm.truthy(check_accuracy()))
            Dm.append(examine_texts, span_smallnoticeital(I18n.T("Due to insufficient radar coverage, the timing of this forecast may be inaccurate.")));
    }

    // tg: code/datums/components/weatherannouncer.dm:231
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
    }

    // tg: code/datums/components/weatherannouncer.dm:234
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, COMSIG_ATOM_EXAMINE);
    }
}

// tg: code/datums/components/weatherannouncer.dm:238
public partial class datum_aas_config_entry_weather : datum_aas_config_entry
{
    // tg: code/datums/components/weatherannouncer.dm:248
    public override bool act_up()
    {
        bool dot = base.act_up();
        if (dot)
            return dot;

        // 10% chance to turn off weather broadcast entirely
        if (prob(10))
            enabled = FALSE;
        return dot;
    }
}
