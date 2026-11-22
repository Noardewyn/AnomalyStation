// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/supermatter/supermatter_delamination/cascade_delam.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 supermatter slice (G5, D163) and the delamination slice (G6): the whole file; the radio and station announcements came
// with M9 slice D, the one that names the station with slice D2 (station_name). The admin messages wait for M10. The var block is generated (D111).

// tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam.dm:1
public partial class datum_sm_delam_cascade : datum_sm_delam
{
    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam.dm:4
    public override object can_select(obj_machinery_power_supermatter_crystal sm = null)
    {
        if (!Dm.truthy(sm.is_main_engine))
            return FALSE;
        float total_moles = Dm.num(sm.absorbed_gasmix.total_moles());
        if (total_moles < MOLE_PENALTY_THRESHOLD * sm.absorption_ratio)
            return FALSE;
        foreach (object gas_path in Dm.each(Dm.list(typeof(datum_gas_antinoblium), typeof(datum_gas_hypernoblium))))
        {
            object percent = sm.gas_percentage[gas_path];
            if (!Dm.truthy(percent) || Dm.num(percent) < 0.4f)
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam.dm:16
    public override async DmTask<object> delam_progress(obj_machinery_power_supermatter_crystal sm = null)
    {
        if (!Dm.truthy(await base.delam_progress(sm)))
            return FALSE;

        if (!Dm.truthy(announcement_triggered))
            await announce_cascade(sm);

        sm.radio.talk_into(
            sm,
            I18n.T("DANGER: HYPERSTRUCTURE OSCILLATION FREQUENCY OUT OF BOUNDS."),
            Dm.ge(sm.damage, sm.emergency_point) ? sm.emergency_channel : sm.warning_channel
        );
        DmList messages = Dm.list(
            I18n.T("Space seems to be shifting around you..."),
            I18n.T("You hear a high-pitched ringing sound."),
            I18n.T("You feel tingling going down your back."),
            I18n.T("Something feels very off."),
            I18n.T("A drowning sense of dread washes over you.")
        );
        dispatch_announcement_to_players(span_danger(Dm.str(Dm.pick(messages))), should_play_sound: FALSE);

        return TRUE;
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam.dm:39
    public override void on_select(obj_machinery_power_supermatter_crystal sm = null)
    {
        base.on_select(sm);
        sm.warp = Dm.@new<atom_movable_warp_effect>(sm);
        Dm.append(sm.vis_contents, sm.warp);
        Dm.animate(sm.warp, Dm.named("time", 1), Dm.named("transform", Dm.@new<matrix>().Scale(0.5f, 0.5f)));
        Dm.animate(Dm.named("time", 9), Dm.named("transform", Dm.@new<matrix>()));
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam.dm:46
    public override void on_deselect(obj_machinery_power_supermatter_crystal sm = null)
    {
        base.on_deselect(sm);
        message_admins(Dm.text("[] will no longer cascade.", ADMIN_VERBOSEJMP(sm)));
        Dm.remove(sm.vis_contents, sm.warp);
        QDEL_NULL(ref sm.warp);
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam.dm:52
    public override async DmTask delaminate(obj_machinery_power_supermatter_crystal sm = null)
    {
        log_delamination(sm);
        await effect_explosion(sm);
        await effect_emergency_state();
        await effect_cascade_demoralize();
        priority_announce(I18n.T("A Type-C resonance shift event has occurred in your sector. Scans indicate local oscillation flux affecting spatial and gravitational substructure. Multiple resonance hotspots have formed. Please standby."), I18n.T("Nanotrasen Star Observation Association"), ANNOUNCER_SPANOMALIES);
        await sleep(2 * SECONDS);
        Waiting.Detach(effect_strand_shuttle());
        await sleep(5 * SECONDS);
        obj_cascade_portal rift = await effect_evac_rift_start() as obj_cascade_portal;
        RegisterSignal(rift, COMSIG_QDELETING, nameof(end_round_holder));
        SSsupermatter_cascade.can_fire = TRUE;
        SSsupermatter_cascade.cascade_initiated = TRUE;
        effect_crystal_mass(sm, rift);
        await base.delaminate(sm);
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam.dm:69
    public override object examine(obj_machinery_power_supermatter_crystal sm = null)
    {
        return Dm.list(span_bolddanger(I18n.T("The crystal is vibrating at immense speeds, warping space around it!")));
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam.dm:72
    public override object overlays(obj_machinery_power_supermatter_crystal sm = null)
    {
        return Dm.list();
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam.dm:75
    public override object count_down_messages(obj_machinery_power_supermatter_crystal sm = null)
    {
        DmList messages = Dm.list();
        Dm.append(messages, I18n.T("CRYSTAL DELAMINATION IMMINENT. The supermatter has reached critical integrity failure. Harmonic frequency limits exceeded. Causality destabilization field could not be engaged."));
        Dm.append(messages, I18n.T("Crystalline hyperstructure returning to safe operating parameters. Harmonic frequency restored within emergency bounds. Anti-resonance filter initiated."));
        Dm.append(messages, I18n.T("remain before resonance-induced stabilization."));
        return messages;
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam.dm:82
    public virtual async DmTask<object> announce_cascade(obj_machinery_power_supermatter_crystal sm = null)
    {
        if (QDELETED(sm))
            return FALSE;
        if (!Dm.truthy(can_select(sm)))
            return FALSE;
        if (!Dm.truthy(sm.should_alert_common()))
            return FALSE;

        priority_announce(I18n.T("Attention: Long range anomaly scans indicate abnormal quantities of harmonic flux originating from a subject within [], a resonance collapse may occur.", station_name()),
            I18n.T("Nanotrasen Star Observation Association"), Dm.resource("sound/announcer/alarm/airraid.ogg"));
        announcement_triggered = TRUE;
        return TRUE;
    }

    /// Signal calls cant sleep, we gotta do this.
    // tg: code/modules/power/supermatter/supermatter_delamination/cascade_delam.dm:97
    [SignalHandler]
    public virtual void end_round_holder()
    {
        Waiting.Detach(effect_evac_rift_end());
    }
}
