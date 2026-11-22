// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 supermatter slice (G5, D163) and the delamination slice (G6): the whole file; the radio announcements came with the
// radio (M9 slice D). The round persistence, the logs and admin messages are left to M10. The var block is generated (D111).

/// Priority is top to bottom.
// tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:2  GLOBAL_LIST_INIT(sm_delam_list, list(...))
public partial class datum_controller_global_vars
{
    // tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:2  GLOBAL_LIST_INIT(sm_delam_list, list(...))
    public DmList sm_delam_list;

    // tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:2  (GLOBAL_MANAGED(sm_delam_list, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsm_delam_list()
    {
        sm_delam_list = Dm.list(
            Dm.kv(typeof(datum_sm_delam_cascade), Dm.@new<datum_sm_delam_cascade>()),
            Dm.kv(typeof(datum_sm_delam_singularity), Dm.@new<datum_sm_delam_singularity>()),
            Dm.kv(typeof(datum_sm_delam_tesla), Dm.@new<datum_sm_delam_tesla>()),
            Dm.kv(typeof(datum_sm_delam_explosive), Dm.@new<datum_sm_delam_explosive>()));
        Dm.append(ref gvars_datum_init_order, "sm_delam_list");
    }
}

/// Logic holder for supermatter delaminations, goes off the strategy design pattern.
/// Selected by [/obj/machinery/power/supermatter_crystal/proc/set_delam]
// tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:11
public partial class datum_sm_delam : datum
{
    /// Whether we are eligible for this delamination or not. TRUE if valid, FALSE if not.
    /// [/obj/machinery/power/supermatter_crystal/proc/set_delam]
    // tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:19
    public virtual object can_select(obj_machinery_power_supermatter_crystal sm = null)
    {
        return FALSE;
    }

    // tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:22  #define ROUNDCOUNT_ENGINE_JUST_EXPLODED -1 (file-local, rules 11.4)
    private const int ROUNDCOUNT_ENGINE_JUST_EXPLODED = -1;

    /// Called when the count down has been finished, do the nasty work.
    /// [/obj/machinery/power/supermatter_crystal/proc/count_down]
    // tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:26
    public virtual async DmTask delaminate(obj_machinery_power_supermatter_crystal sm = null)
    {
        if (Dm.truthy(sm.is_main_engine))
        {
            SSpersistence.delam_highscore = SSpersistence.rounds_since_engine_exploded;
            SSpersistence.rounds_since_engine_exploded = ROUNDCOUNT_ENGINE_JUST_EXPLODED;
            foreach (obj_machinery_incident_display sign in Dm.each_cast<obj_machinery_incident_display>(GLOB.map_incident_displays))
                sign.update_delam_count(ROUNDCOUNT_ENGINE_JUST_EXPLODED);
        }
        qdel(sm);
    }

    /// Whatever we're supposed to do when a delam is currently in progress.
    /// Mostly just to tell people how useless engi is, and play some alarm sounds.
    /// Returns TRUE if we just told people a delam is going on. FALSE if its healing or we didnt say anything.
    /// [/obj/machinery/power/supermatter_crystal/proc/process_atmos]
    // tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:40
    public virtual async DmTask<object> delam_progress(obj_machinery_power_supermatter_crystal sm = null)
    {
        if (Dm.num(sm.damage) <= sm.warning_point) // Damage is too low, lets not
            return FALSE;

        if (Dm.num(sm.damage) >= sm.emergency_point && sm.damage_archived < sm.emergency_point)
        {
            sm.investigate_log(Dm.text("has entered the emergency point: Pending [].", sm.delamination_strategy.name), INVESTIGATE_ENGINE);
            message_admins(Dm.text("[] has entered the emergency point: Pending [].", ADMIN_VERBOSEJMP(sm), sm.delamination_strategy.name));
        }

        if ((REALTIMEOFDAY() - sm.lastwarning) < SUPERMATTER_WARNING_DELAY)
            return FALSE;
        sm.lastwarning = REALTIMEOFDAY();

        if (sm.damage_archived - Dm.num(sm.damage) > SUPERMATTER_FAST_HEALING_RATE && sm.damage_archived >= sm.emergency_point) // Fast healing, engineers probably have it all sorted
        {
            if (Dm.truthy(sm.should_alert_common())) // We alert common once per cooldown period, otherwise alert engineering
            {
                sm.radio.talk_into(sm, I18n.T("Crystalline hyperstructure returning to safe operating parameters. Integrity: []%", round(Dm.num(sm.get_integrity_percent()), 0.01f)), sm.emergency_channel);
            }
            else
            {
                sm.radio.talk_into(sm, I18n.T("Crystalline hyperstructure returning to safe operating parameters. Integrity: []%", round(Dm.num(sm.get_integrity_percent()), 0.01f)), sm.warning_channel);
            }
            playsound(sm, Dm.resource("sound/machines/terminal/terminal_alert.ogg"), 75);
            return FALSE;
        }

        switch (Dm.num(sm.get_status()))
        {
            case SUPERMATTER_DELAMINATING:
                playsound(sm, Dm.resource("sound/announcer/alarm/bloblarm.ogg"), 100, FALSE, 40, 30, falloff_distance: 10);
                break;
            case SUPERMATTER_EMERGENCY:
                playsound(sm, Dm.resource("sound/machines/engine_alert/engine_alert1.ogg"), 100, FALSE, 30, 30, falloff_distance: 10);
                break;
            case SUPERMATTER_DANGER:
                playsound(sm, Dm.resource("sound/machines/engine_alert/engine_alert2.ogg"), 100, FALSE, 30, 30, falloff_distance: 10);
                break;
            case SUPERMATTER_WARNING:
                playsound(sm, Dm.resource("sound/machines/terminal/terminal_alert.ogg"), 75);
                break;
        }

        if (Dm.num(sm.damage) >= sm.emergency_point) // In emergency
        {
            sm.radio.talk_into(sm, I18n.T("CRYSTAL DELAMINATION IMMINENT! Integrity: []%", round(Dm.num(sm.get_integrity_percent()), 0.01f)), sm.emergency_channel);
            sm.lastwarning = REALTIMEOFDAY() - (SUPERMATTER_WARNING_DELAY / 2); // Cut the time to next announcement in half.
        }
        else if (sm.damage_archived > Dm.num(sm.damage)) // Healing, in warning
        {
            sm.radio.talk_into(sm, I18n.T("Crystalline hyperstructure returning to safe operating parameters. Integrity: []%", round(Dm.num(sm.get_integrity_percent()), 0.01f)), sm.warning_channel);
            return FALSE;
        }
        else // Taking damage, in warning
        {
            sm.radio.talk_into(sm, I18n.T("Danger! Crystal hyperstructure integrity faltering! Integrity: []%", round(Dm.num(sm.get_integrity_percent()), 0.01f)), sm.warning_channel);
        }

        if (Dm.truthy(sm._listen_lookup?[COMSIG_SUPERMATTER_DELAM_ALARM]))
            sm._SendSignal(COMSIG_SUPERMATTER_DELAM_ALARM, Dm.list(sm));
        return TRUE;
    }

    /// Generates an admin message and creates an investigate log.
    // tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:83
    public virtual void log_delamination(obj_machinery_power_supermatter_crystal sm = null)
    {
        message_admins(Dm.text("[] triggered a [].", AREACOORD(sm), name)); // ADMIN_VERBOSEJMP without its jump link (the admin panels, D275)
        sm.investigate_log(Dm.text("triggered a [].", name), INVESTIGATE_ENGINE);
    }

    /// Called when a supermatter switches its strategy from another one to us.
    /// [/obj/machinery/power/supermatter_crystal/proc/set_delam]
    // tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:89
    [ShouldCallParent]
    public virtual void on_select(obj_machinery_power_supermatter_crystal sm = null)
    {
        sm.investigate_log(Dm.text("functional strategy is now [].", name), INVESTIGATE_ENGINE);
        if (Dm.truthy(SSticker.HasRoundStarted()))
            message_admins(Dm.text("[] functional strategy is now [].", AREACOORD(sm), name));
    }

    /// Called when a supermatter switches its strategy from us to something else.
    /// [/obj/machinery/power/supermatter_crystal/proc/set_delam]
    // tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:97
    [ShouldCallParent]
    public virtual void on_deselect(obj_machinery_power_supermatter_crystal sm = null)
    {
        sm.investigate_log(Dm.text("previous strategy was [].", name), INVESTIGATE_ENGINE);
    }

    /// Added to an examine return value.
    /// [/obj/machinery/power/supermatter_crystal/examine]
    // tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:103
    public virtual object examine(obj_machinery_power_supermatter_crystal sm = null)
    {
        return Dm.list();
    }

    /// Add whatever overlay to the sm.
    /// [/obj/machinery/power/supermatter_crystal/update_overlays]
    // tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:108
    public virtual object overlays(obj_machinery_power_supermatter_crystal sm = null)
    {
        if (Dm.truthy(sm.final_countdown))
            return Dm.list(mutable_appearance(icon: sm.icon, icon_state: "causality_field", layer: FLOAT_LAYER));
        return Dm.list();
    }

    /// Applies filters to the SM.
    /// [/obj/machinery/power/supermatter_crystal/process_atmos]
    // tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:115
    public virtual void filters(obj_machinery_power_supermatter_crystal sm = null)
    {
        float new_filter = Dm.num(sm.get_filter("ray") == null);

        sm.add_filter(name: "ray", priority: 1, @params: Dm.list(
            Dm.kv("type", "rays"),
            Dm.kv("size", clamp(sm.internal_energy / 50, 1, 100)),
            Dm.kv("color", (sm.gas_heat_power_generation > 0.8f ? SUPERMATTER_RED : SUPERMATTER_COLOUR)),
            Dm.kv("factor", clamp(Dm.num(sm.damage) / 10, 1, 10)),
            Dm.kv("density", clamp(Dm.num(sm.damage), 12, 100))
        ));

        // Filter animation persists even if the filter itself is changed externally.
        // Probably prone to breaking. Treat with suspicion.
        if (Dm.truthy(new_filter))
        {
            Dm.animate(sm.get_filter("ray"), Dm.named("offset", 10), Dm.named("time", 10 * SECONDS), Dm.named("loop", -1));
            Dm.animate(Dm.named("offset", 0), Dm.named("time", 10 * SECONDS));
        }
    }

    // Change how bright the rock is.
    /// [/obj/machinery/power/supermatter_crystal/process_atmos]
    // tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:134
    public virtual async DmTask lights(obj_machinery_power_supermatter_crystal sm = null)
    {
        if (Dm.truthy(sm.internal_energy))
        {
            await sm.set_light(
                l_range: ROUND_UP(clamp(sm.internal_energy / 500, 4, 10)),
                l_power: ROUND_UP(clamp(sm.internal_energy / 1000, 1, 5)),
                l_color: sm.gas_heat_power_generation > 0.8f ? SUPERMATTER_RED : SUPERMATTER_COLOUR,
                l_on: TRUE
            );
        }
    }

    /// Returns a set of messages to be spouted during delams
    /// First message is start of count down, second message is quitting of count down (if sm healed), third is 5 second intervals
    // tg: code/modules/power/supermatter/supermatter_delamination/_sm_delam.dm:145
    public virtual object count_down_messages(obj_machinery_power_supermatter_crystal sm = null)
    {
        DmList messages = Dm.list();
        Dm.append(messages, I18n.T("CRYSTAL DELAMINATION IMMINENT. The supermatter has reached critical integrity failure. Emergency causality destabilization field has been activated."));
        Dm.append(messages, I18n.T("Crystalline hyperstructure returning to safe operating parameters. Failsafe has been disengaged."));
        Dm.append(messages, I18n.T("remain before causality stabilization."));
        return messages;
    }
}
