// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmo_interface.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the gizmo interfaces, which pick a gizmo's modes (the guaranteed ones and some of the possible ones), let each
// mode add its callbacks and give them to a new puzzle. The var blocks are generated (D111).
//
// GAP(types): tg's `var/datum/gizmo_controller/controller` (:4) and `var/datum/gizmo_puzzle/puzzle = /datum/gizmo_puzzle` (:6) are
// generated as Types (rules 3.2 V3: the names are typepath-assigned elsewhere, and puzzle's default is a path that tg replaces by the
// puzzle in generate_interface); the controller and the puzzle the interface holds are controller_value and puzzle_value (the same
// split as /obj/machinery/var/circuit).

/// Essentially a small wrapper that holds the puzzle datum and connects it with the operating modes of the machine
// tg: code/modules/research/gizmo/gizmo_interface.dm:2
public partial class datum_gizmo_interface : datum
{
    /// GAP(types): tg's `controller` (:4): the gizmo master ultra mega controller
    public datum_gizmo_controller controller_value;
    /// GAP(types): tg's `puzzle` (:6) once generate_interface made it: the puzzle that connects the interface to the activation
    /// callbacks and checks if you pulsed the right sequences
    public datum_gizmo_puzzle puzzle_value;

    // tg: code/modules/research/gizmo/gizmo_interface.dm:20
    [DmParams("controller")]
    public override async DmTask New(params object[] __extra)
    {
        datum_gizmo_controller controller = Dm.arg<datum_gizmo_controller>(__extra, 0);
        await base.New();

        controller_value = controller;
    }

    /// Instantiate the active modes, tell them to instantiate and pass their callbacks to the puzzle maker
    // tg: code/modules/research/gizmo/gizmo_interface.dm:26
    public virtual async DmTask generate_interface(atom_movable holder = null, datum_callback pulse_callback = null)
    {
        DmList trigger_callbacks = Dm.list();
        DmList modes_to_spawn = Dm.add(Dm.list(), guaranteed_active_gizmodes) as DmList;

        for (float i = 1, i__end = rand(min_modes, max_modes); i <= i__end; i++)
        {
            object path = pick_weight_take(possible_active_modes);
            if (!Dm.truthy(path))
                break;
            Dm.append(ref modes_to_spawn, path);
        }

        foreach (object path in Dm.each(modes_to_spawn))
        {
            datum_gizmodes mode = Dm.@new(path as Type) as datum_gizmodes;
            await mode.generate_modes(trigger_callbacks, this);
            Dm.append(ref active_gizmodes, mode);
        }

        puzzle_value = Dm.@new(puzzle) as datum_gizmo_puzzle;
        puzzle_value.generate_code_sequences(trigger_callbacks);
    }
}
