// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/bsa_blocker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R1 (D327): the whole file: what stops the bluespace artillery's beam (the supermatter crystal, a singularity). The element
// flags are generated (D111).

//blocks bluespace artillery beams that try to fly through
//look not all elements need to be fancy
// tg: code/datums/elements/bsa_blocker.dm:3
public partial class datum_element_bsa_blocker : datum_element
{
    // tg: code/datums/elements/bsa_blocker.dm:3
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_ATOM_BSA_BEAM, nameof(block_bsa));
        return await base.Attach(target, __extra);
    }

    // tg: code/datums/elements/bsa_blocker.dm:9
    [SignalHandler]
    public virtual object block_bsa()
    {
        return COMSIG_ATOM_BLOCKS_BSA_BEAM;
    }
}
