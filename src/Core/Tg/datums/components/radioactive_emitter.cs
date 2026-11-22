// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/radioactive_emitter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 radiation slice: the whole file. The var block is generated (D111).

/**
 * # Radioactive Emitter
 *
 * Simple component that you can attach to something to make it emit radiation pulses over time.
 */
// tg: code/datums/components/radioactive_emitter.dm:10
public partial class datum_component_radioactive_emitter : datum_component
{
    /// Minimum duration between pulses for the radioactive emitter component.
    /// This is chosen arbitrarily. It can theoretically go down to 0.1 SECONDS but god please don't
    // tg: code/datums/components/radioactive_emitter.dm:3
    private const float MIN_PULSE_COOLDOWN = 0.5f * SECONDS;

    // tg: code/datums/components/radioactive_emitter.dm:25
    [DmParams("cooldown_time", "range", "threshold", "examine_text")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object cooldown_time = Dm.arg(__extra, 0) ?? 5 * SECONDS;
        object range = Dm.arg(__extra, 1) ?? 1;
        object threshold = Dm.arg(__extra, 2) ?? RAD_MEDIUM_INSULATION;
        object examine_text = Dm.arg(__extra, 3);

        if (!isturf(parent) && !ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        this.cooldown_time = max(Dm.num(cooldown_time), MIN_PULSE_COOLDOWN);
        this.range = Dm.num_or_null(range);
        this.threshold = Dm.num_or_null(threshold);
        this.examine_text = examine_text;

        // We process on fastprocess even though we're on a cooldown based system.
        // Easier to handle edits to the cooldown duration, prevents timer spam for short cooldown emitters
        START_PROCESSING(SSfastprocess, this);
        return null;
    }

    // tg: code/datums/components/radioactive_emitter.dm:44
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }

    // tg: code/datums/components/radioactive_emitter.dm:48
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
    }

    // tg: code/datums/components/radioactive_emitter.dm:51
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_ATOM_EXAMINE);
    }

    // tg: code/datums/components/radioactive_emitter.dm:54
    [DmParams("new_comp", "i_am_original", "cooldown_time", "range", "threshold", "examine_text")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        object cooldown_time = Dm.arg(__extra, 0) ?? 5 * SECONDS;
        object range = Dm.arg(__extra, 1) ?? 1;
        object threshold = Dm.arg(__extra, 2) ?? RAD_NO_INSULATION;

        if (!Dm.truthy(i_am_original))
            return;

        // Only care about modifying our rad wave argument.
        this.cooldown_time = Dm.num_or_null(cooldown_time);
        this.range = Dm.num_or_null(range);
        this.threshold = Dm.num_or_null(threshold);
        // Don't touch examine text or whatever else.
    }

    // tg: code/datums/components/radioactive_emitter.dm:72
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!(rad_pulse_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return null;

        rad_pulse_cooldown = world.time + Dm.num(cooldown_time); // COOLDOWN_START (__DEFINES/cooldowns.dm:110), inline
        radiation_pulse(parent as atom, range, threshold);
        return null;
    }

    // tg: code/datums/components/radioactive_emitter.dm:79
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        // SIGNAL_HANDLER

        if (!Dm.truthy(examine_text))
            return;

        Dm.append(ref examine_list, examine_text);
    }
}
