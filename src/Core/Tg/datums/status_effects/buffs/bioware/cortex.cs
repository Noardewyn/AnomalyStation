// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/buffs/bioware/cortex.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file (the bioware operations grant these). The var blocks are generated.

// Bioware that affects the brain
// tg: code/datums/status_effects/buffs/bioware/cortex.dm:2
public partial class datum_status_effect_bioware_cortex : datum_status_effect_bioware
{
    // All cortex bioware removes the trauma boost as it is not granted via a status
    // tg: code/datums/status_effects/buffs/bioware/cortex.dm:6
    public override void bioware_gained()
    {
        REMOVE_TRAIT(owner, TRAIT_SPECIAL_TRAUMA_BOOST, BIOWARE_TRAIT);
    }
}

// Imprinted brain - Cures basic traumas continuously
// tg: code/datums/status_effects/buffs/bioware/cortex.dm:10
public partial class datum_status_effect_bioware_cortex_imprinted : datum_status_effect_bioware_cortex
{
    // tg: code/datums/status_effects/buffs/bioware/cortex.dm:12
    public override void bioware_gained()
    {
        base.bioware_gained();
        mob_living_carbon_human human_owner = (mob_living_carbon_human)owner;
        human_owner.cure_all_traumas(resilience: TRAUMA_RESILIENCE_BASIC);
        RegisterSignal(human_owner, COMSIG_CARBON_GAIN_TRAUMA, nameof(on_gain_trauma));
    }

    // tg: code/datums/status_effects/buffs/bioware/cortex.dm:18
    public override void bioware_lost()
    {
        UnregisterSignal(owner, COMSIG_CARBON_GAIN_TRAUMA);
    }

    // tg: code/datums/status_effects/buffs/bioware/cortex.dm:21
    [SignalHandler]
    public virtual object on_gain_trauma(datum source = null, object trauma = null, object resilience = null)
    {
        // the brain sends the trauma it was given, an instance or a typepath (brain_item.dm:688); a typepath's var is its initial value
        if (resilience == null)
            resilience = trauma is datum_brain_trauma trauma_datum ? trauma_datum.resilience : Dm.initial((Type)trauma, (datum_brain_trauma o) => o.resilience);
        if (Dm.num(resilience) <= TRAUMA_RESILIENCE_BASIC) // there SHOULD be nothing lower than TRAUMA_RESILIENCE_BASIC, but I'd prefer to not make assumptions in case this ever gets some sort of refactor
            return COMSIG_CARBON_BLOCK_TRAUMA;
        return null;
    }
}
