// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/machinery/protolathe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package RD1, D333: the fabricators): the protolathe, which logs its construction and deconstruction, and the
// offstation one. The departmental protolathes (departmental_protolathe.dm) and the techfabs (techfab.dm, departmental_techfab.dm)
// only set vars: their classes are generated (D111).

// tg: code/modules/research/machinery/protolathe.dm:1
public partial class obj_machinery_rnd_production_protolathe : obj_machinery_rnd_production
{
    // tg: code/modules/research/machinery/protolathe.dm:10
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        log_game(Dm.text("Protolathe of type [] [] by [] at []", type, Dm.truthy(disassembled) ? "disassembled" : "deconstructed", key_name(usr), get_area_name(this, TRUE)));

        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/research/machinery/protolathe.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (!Dm.truthy(mapload))
            log_game(Dm.text("Protolathe of type [] constructed by [] at []", type, key_name(usr), get_area_name(this, TRUE)));

        return base.Initialize(mapload, __extra);
    }
}
