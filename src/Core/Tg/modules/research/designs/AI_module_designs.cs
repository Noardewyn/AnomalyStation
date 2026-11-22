// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/designs/AI_module_designs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): this file's design procs (SSresearch makes every design); the designs are var blocks (generated, D111).

// tg: code/modules/research/designs/AI_module_designs.dm:17
public partial class datum_design_board_ai_law_rack : datum_design_board
{
    // tg: code/modules/research/designs/AI_module_designs.dm:28
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Type board = build_path;
        Type rack = Dm.initial(board, (obj_item_circuitboard b) => b.build_path);
        float law_slots = Dm.num(Dm.initial(rack, (obj_machinery_ai_law_rack r) => r.law_slots));
        desc += Dm.text(" Can hold [] module[][].", law_slots, law_slots == 1 ? "" : "s", Dm.truthy(Dm.initial(rack, (obj_machinery_ai_law_rack r) => r.has_core_slot)) ? ", including a core module slot" : "");
    }
}
