// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/atmos_reaction_recorder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4c slice 1: every gas tank records its reactions). The var block is generated.

/**
 * Component for atmos-related atoms to be able to record and transmit their reaction_results data
 * Use this ONLY for atoms, gasmixes will be accessed by calling return air on the object.
 * Add this component after gasmixes has been initialized.
 */
// tg: code/datums/components/atmos_reaction_recorder.dm:7
public partial class datum_component_atmos_reaction_recorder : datum_component
{
    /**
     * Verify that parent is indeed an atom, and then register signals.
     * Args:
     * - target_list (list): The list we are writing the captured reaction_results to.
     * - reset_criteria (list): Assoc signal-source list containing signals to be registered to. We will reset if any of them are sent.
     */
    // tg: code/datums/components/atmos_reaction_recorder.dm:20
    [DmParams("target_list", "reset_criteria")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object target_list = Dm.arg(__extra, 0) ?? Dm.list();
        DmList reset_criteria = Dm.arg(__extra, 1) as DmList ?? Dm.list();
        object dot = null;
        dot = await base.Initialize(Dm.forward(__extra, target_list, reset_criteria));

        atom parent_atom = parent as atom;
        datum_gas_mixture parent_air = (parent_atom == null ? null : await parent_atom.return_air()) as datum_gas_mixture;
        if (!(parent_atom is atom) || !(parent_air is datum_gas_mixture))
            return COMPONENT_INCOMPATIBLE;

        if (islist(target_list))
            copied_reaction_results = target_list as DmList;
        else
        {
            // Still can record reactions without a list, but it most likely wont be accesible outside of the component.
            stack_trace("Atmos reaction recorder component initialized without a list.", "code/datums/components/atmos_reaction_recorder.dm", 32);
            copied_reaction_results = Dm.list();
        }

        registered_signals = Dm.list();
        RegisterSignal(parent_air, COMSIG_GASMIX_REACTED, nameof(update_data));
        Dm.append(ref registered_signals, Dm.list(Dm.kv(COMSIG_GASMIX_REACTED, parent_air)));

        foreach (object signal in Dm.each(reset_criteria))
        {
            // We currently dont implement the same signal registered twice even from different sources. This allows this component to be simpler.
            if (Dm.@in(signal, registered_signals))
                return COMPONENT_INCOMPATIBLE;
            RegisterSignal(reset_criteria[signal] as datum, signal, nameof(reset_data));
            registered_signals[signal] = reset_criteria[signal];
        }
        return dot;
    }

    /// Fetches reaction_results and updates the list.
    // tg: code/datums/components/atmos_reaction_recorder.dm:47
    public virtual void update_data(datum_gas_mixture recorded_gasmix = null)
    {
        foreach (object reaction in Dm.each(recorded_gasmix.reaction_results))
            copied_reaction_results[reaction] = Dm.add(copied_reaction_results[reaction], recorded_gasmix.reaction_results[reaction]);
    }

    // tg: code/datums/components/atmos_reaction_recorder.dm:52
    public virtual void reset_data()
    {
        copied_reaction_results.Cut();
    }

    // tg: code/datums/components/atmos_reaction_recorder.dm:56
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        foreach (object signal in Dm.each(registered_signals))
            UnregisterSignal(registered_signals[signal] as datum, signal);
    }
}
