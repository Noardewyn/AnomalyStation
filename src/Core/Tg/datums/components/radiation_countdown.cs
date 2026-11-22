// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/radiation_countdown.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 radiation slice: the whole file. The var block is generated (D111).

/// Begins the countdown before a target can be irradiated.
/// Added by the radiation subsystem when a pulse information has a minimum exposure time.
/// Will clear itself out after a while.
// tg: code/datums/components/radiation_countdown.dm:7
public partial class datum_component_radiation_countdown : datum_component
{
    // Should be more than any minimum exposure time coming in
    // tg: code/datums/components/radiation_countdown.dm:2
    private const float TIME_UNTIL_DELETION = 10 * SECONDS;

    // tg: code/datums/components/radiation_countdown.dm:15
    [DmParams("minimum_exposure_time")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object minimum_exposure_time = Dm.arg(__extra, 0);
        if (!CAN_IRRADIATE(parent))
            return COMPONENT_INCOMPATIBLE;

        this.minimum_exposure_time = minimum_exposure_time;

        time_added = world.time;

        to_chat(parent, span_userdanger(I18n.T("The air around you feels warm...perhaps you should go somewhere else.")));

        start_deletion_timer();
        return null;
    }

    // tg: code/datums/components/radiation_countdown.dm:27
    public virtual void start_deletion_timer()
    {
        _addtimer(Dm.@new<datum_callback>(this, nameof(remove_self)), TIME_UNTIL_DELETION, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/datums/components/radiation_countdown.dm", line: 28);
    }

    // tg: code/datums/components/radiation_countdown.dm:30
    public virtual void remove_self()
    {
        if (!HAS_TRAIT(parent, TRAIT_IRRADIATED))
            to_chat(parent, span_notice(I18n.T("The air here feels safer.")));

        qdel(this);
    }

    // tg: code/datums/components/radiation_countdown.dm:36
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_IN_THRESHOLD_OF_IRRADIATION, nameof(on_pre_potential_irradiation_within_range));
    }

    // tg: code/datums/components/radiation_countdown.dm:39
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_IN_THRESHOLD_OF_IRRADIATION);
    }

    // tg: code/datums/components/radiation_countdown.dm:42
    [SignalHandler]
    public virtual object on_pre_potential_irradiation_within_range(datum source = null, datum_radiation_pulse_information pulse_information = null)
    {
        // SIGNAL_HANDLER

        minimum_exposure_time = min(Dm.num(minimum_exposure_time), Dm.num(pulse_information.minimum_exposure_time));

        start_deletion_timer();

        // Played with fire, now you might be getting irradiated.
        if (world.time - Dm.num(time_added) >= Dm.num(minimum_exposure_time))
            return SKIP_MINIMUM_EXPOSURE_TIME_CHECK;

        return CANCEL_IRRADIATION;
    }
}
