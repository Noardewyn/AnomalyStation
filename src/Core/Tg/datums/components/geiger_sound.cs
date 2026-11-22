// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/geiger_sound.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 radiation slice: the whole file. The var blocks of the component and of /datum/looping_sound/geiger are generated (D111).

/// Atoms with this component will play sounds depending on nearby radiation
// tg: code/datums/components/geiger_sound.dm:2
public partial class datum_component_geiger_sound : datum_component
{
    // tg: code/datums/components/geiger_sound.dm:7
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;
        return null;
    }

    // tg: code/datums/components/geiger_sound.dm:11
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref sound);

        if (last_parent != null)
            UnregisterSignal((datum)last_parent, COMSIG_IN_RANGE_OF_IRRADIATION);

        last_parent = null;

        return base.Destroy(force);
    }

    // tg: code/datums/components/geiger_sound.dm:21
    public override async DmTask RegisterWithParent()
    {
        sound = (datum_looping_sound_geiger)await Dm.new_async(typeof(datum_looping_sound_geiger), parent);

        RegisterSignal(parent, COMSIG_IN_RANGE_OF_IRRADIATION, nameof(on_pre_potential_irradiation));

        ADD_TRAIT(parent, TRAIT_BYPASS_EARLY_IRRADIATED_CHECK, REF(this));

        if (isitem(parent))
        {
            atom atom_parent = (atom)parent;
            RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_moved));
            register_to_loc(atom_parent.loc);
        }
    }

    // tg: code/datums/components/geiger_sound.dm:33
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_MOVABLE_MOVED,
            COMSIG_IN_RANGE_OF_IRRADIATION
        ));

        REMOVE_TRAIT(parent, TRAIT_BYPASS_EARLY_IRRADIATED_CHECK, REF(this));
    }

    // tg: code/datums/components/geiger_sound.dm:41
    [SignalHandler]
    public virtual void on_pre_potential_irradiation(datum source = null, datum_radiation_pulse_information pulse_information = null, object insulation_to_target = null)
    {
        // SIGNAL_HANDLER

        sound.last_insulation_to_target = insulation_to_target;
        sound.last_radiation_pulse = pulse_information;
        sound.start();

        _addtimer(Dm.@new<datum_callback>(sound, nameof(datum_looping_sound.stop)), TIME_WITHOUT_RADIATION_BEFORE_RESET, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/datums/components/geiger_sound.dm", line: 48);
    }

    // tg: code/datums/components/geiger_sound.dm:50
    [SignalHandler]
    public virtual void on_moved(atom source = null)
    {
        // SIGNAL_HANDLER
        register_to_loc(source.loc);
    }

    // tg: code/datums/components/geiger_sound.dm:54
    public virtual void register_to_loc(object new_loc = null)
    {
        if (Dm.eq(last_parent, new_loc))
            return;

        if (last_parent != null)
            UnregisterSignal((datum)last_parent, COMSIG_IN_RANGE_OF_IRRADIATION);

        last_parent = new_loc;

        if (new_loc != null)
            RegisterSignal((datum)new_loc, COMSIG_IN_RANGE_OF_IRRADIATION, nameof(on_pre_potential_irradiation));
    }
}

// tg: code/datums/components/geiger_sound.dm:66
public partial class datum_looping_sound_geiger : datum_looping_sound
{
    // tg: code/datums/components/geiger_sound.dm:79
    public override object Destroy(float? force = null)
    {
        last_radiation_pulse = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/geiger_sound.dm:83
    public override object get_sound(object _mid_sounds = null)
    {
        if (last_radiation_pulse == null)
            return null;

        return base.get_sound(((DmList)mid_sounds)[get_perceived_radiation_danger(last_radiation_pulse, last_insulation_to_target)]);
    }

    // tg: code/datums/components/geiger_sound.dm:89
    public override void stop(float? null_parent = null)
    {
        null_parent ??= FALSE;
        base.stop(null_parent);

        last_radiation_pulse = null;
    }
}
