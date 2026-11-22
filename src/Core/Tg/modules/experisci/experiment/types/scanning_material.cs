// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/types/scanning_material.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the material scans, which ask for chairs, toilets, tables, walls or floors made of
// a drawn material. The var block is generated (D111).

// tg: code/modules/experisci/experiment/types/scanning_material.dm:1
public partial class datum_experiment_scanning_random_material : datum_experiment_scanning_random
{
    // tg: code/modules/experisci/experiment/types/scanning_material.dm:12
    [DmParams("techweb")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        foreach (object req_atom in Dm.each(required_atoms))
        {
            object chosen_material = pick(possible_material_types);
            required_materials[req_atom] = chosen_material;
        }
    }

    // tg: code/modules/experisci/experiment/types/scanning_material.dm:18
    public override async DmTask<object> final_contributing_index_checks(datum_component_experiment_handler experiment_handler = null, atom target = null, object typepath = null)
    {
        return (await base.final_contributing_index_checks(experiment_handler, target, typepath) is var and__1 && !Dm.truthy(and__1) ? and__1
            : (target.custom_materials is var and__2 && !Dm.truthy(and__2) ? and__2 : await target.has_material_type(required_materials[typepath])));
    }

    // tg: code/modules/experisci/experiment/types/scanning_material.dm:21
    public override async DmTask<object> serialize_progress_stage(Type target = null, DmList seen_instances = null)
    {
        datum_material required_material = await SSmaterials.get_material(required_materials[target]) as datum_material;
        return EXPERIMENT_PROG_INT(I18n.T("Scan samples of \\a [] []", required_material.name, Dm.initial(target, (atom p) => p.name)),
            Dm.truthy(Dm.bitand(traits, EXPERIMENT_TRAIT_DESTRUCTIVE)) ? scanned[target] : seen_instances.len, required_atoms[target]);
    }
}
