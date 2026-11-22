// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/airalarm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public partial class datum_wires_airalarm : datum_wires
{
    public override bool can_reveal_wires(mob user) => HAS_TRAIT(user, TRAIT_KNOW_ENGI_WIRES) || base.can_reveal_wires(user);

    public override DmList get_status()
    {
        var alarm = (obj_machinery_airalarm)holder;
        return Dm.list(Dm.truthy(alarm.locked) ? "The interface light is red." : "The interface light is green.",
            Dm.truthy(alarm.shorted) ? "The short indicator is lit." : "The short indicator is off.",
            Dm.truthy(alarm.aidisabled) ? "The AI connection light is off." : "The AI connection light is on.");
    }

    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        wires = Dm.list(WIRE_POWER, WIRE_IDSCAN, WIRE_AI, WIRE_PANIC, WIRE_ALARM, WIRE_SPEAKER);
        add_duds(3);
        await base.New(__extra);
    }

    public override bool interactable(mob user) => base.interactable(user)
        && holder is obj_machinery_airalarm alarm && Dm.truthy(alarm.panel_open) && Dm.eq(alarm.buildstage, AIR_ALARM_BUILD_COMPLETE);

    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        var alarm = (obj_machinery_airalarm)holder;
        if (Dm.eq(wire, WIRE_POWER))
        {
            if (!Dm.truthy(alarm.shorted))
            {
                alarm.shorted = TRUE;
                alarm.update_appearance();
            }
            _addtimer(Dm.@new<datum_callback>(alarm, nameof(obj_machinery_airalarm.reset), wire), 2 * MINUTES);
        }
        else if (Dm.eq(wire, WIRE_IDSCAN)) alarm.locked = Dm.num(!Dm.truthy(alarm.locked));
        else if (Dm.eq(wire, WIRE_AI))
        {
            alarm.aidisabled = TRUE;
            _addtimer(Dm.@new<datum_callback>(alarm, nameof(obj_machinery_airalarm.reset), wire), 10 * SECONDS);
        }
        else if (Dm.eq(wire, WIRE_PANIC) && !Dm.truthy(alarm.shorted))
            alarm.select_mode(user, alarm.selected_mode is datum_air_alarm_mode_filtering ? typeof(datum_air_alarm_mode_panic_siphon) : typeof(datum_air_alarm_mode_filtering));
        else if (Dm.eq(wire, WIRE_ALARM))
        {
            if (Dm.truthy(alarm.alarm_manager.clear_alarm(ALARM_ATMOS))) alarm.danger_level = AIR_ALARM_ALERT_NONE;
            alarm.update_appearance();
        }
    }

    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        var alarm = (obj_machinery_airalarm)holder;
        bool mending = Dm.truthy(mend);
        if (Dm.eq(wire, WIRE_POWER))
        {
            await alarm.shock__proc(source, 50);
            alarm.shorted = Dm.num(!mending);
            alarm.update_appearance();
        }
        else if (Dm.eq(wire, WIRE_IDSCAN) && !mending) alarm.locked = TRUE;
        else if (Dm.eq(wire, WIRE_AI)) alarm.aidisabled = Dm.num(mending);
        else if (Dm.eq(wire, WIRE_PANIC) && !mending && !Dm.truthy(alarm.shorted))
            alarm.select_mode(source, typeof(datum_air_alarm_mode_panic_siphon));
        else if (Dm.eq(wire, WIRE_ALARM))
        {
            if (Dm.truthy(alarm.alarm_manager.send_alarm(ALARM_ATMOS))) alarm.danger_level = AIR_ALARM_ALERT_HAZARD;
            alarm.update_appearance();
        }
        else if (Dm.eq(wire, WIRE_SPEAKER)) alarm.speaker_enabled = Dm.num(mending);
    }
}
