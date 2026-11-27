// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmo_wires.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the gizmo's eight cryptic wires, which send each pulse to the gizmo's puzzle. The var block is generated (D111).
//
// GAP(types): tg's `var/datum/gizmo_puzzle/puzzle` (:21) is generated as a Type (rules 3.2 V3: the name is typepath-assigned
// elsewhere); the puzzle the wires pulse is puzzle_value.

/// Wires that send pulses to a gizmo puzzle datum
// tg: code/modules/research/gizmo/gizmo_wires.dm:2
public partial class datum_wires_gizmo : datum_wires
{
    /// GAP(types): tg's `puzzle` (:21)
    public datum_gizmo_puzzle puzzle_value;

    // tg: code/modules/research/gizmo/gizmo_wires.dm:23
    [DmParams("holder", "puzzle")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        datum_gizmo_puzzle puzzle = Dm.arg<datum_gizmo_puzzle>(__extra, 1);
        wires = cryptic_wires;
        puzzle_value = puzzle;

        await base.New(Dm.forward(__extra, holder, puzzle));
    }

    // tg: code/modules/research/gizmo/gizmo_wires.dm:29
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        await puzzle_value.on_pulse(cryptic_wires.Find(wire), user, holder as atom_movable);
    }
}
