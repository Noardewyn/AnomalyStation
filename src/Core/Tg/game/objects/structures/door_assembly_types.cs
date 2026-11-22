// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/door_assembly_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 6 (airlocks): the assembly types (department, mineral, material, multi-tile) are var blocks (generated); the
// material assembly's deconstruction by its materials and its finished airlock's materials are translated here.

// tg: code/game/objects/structures/door_assembly_types.dm:265
public partial class obj_structure_door_assembly_door_assembly_material : obj_structure_door_assembly
{
    // tg: code/game/objects/structures/door_assembly_types.dm:290
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        turf target_turf = get_turf(this);
        foreach (datum_material material_datum in Dm.each<datum_material>(custom_materials))
        {
            float material_count = FLOOR(Dm.num(custom_materials[material_datum]) / SHEET_MATERIAL_AMOUNT, 1);
            if (!Dm.truthy(disassembled))
                material_count = Dm.num(rand(FLOOR(material_count / 2, 1), material_count));
            Dm.@new(material_datum.sheet_type, target_turf, material_count);
        }
        if (Dm.truthy(glass))
        {
            if (Dm.truthy(disassembled))
            {
                if (Dm.truthy(heat_proof_finished))
                    Dm.@new<obj_item_stack_sheet_rglass>(target_turf);
                else
                    Dm.@new<obj_item_stack_sheet_glass>(target_turf);
            }
            else
                Dm.@new<obj_item_shard>(target_turf);
        }
    }

    // tg: code/game/objects/structures/door_assembly_types.dm:306
    public override async DmTask<obj_machinery_door_airlock> finish_door()
    {
        obj_machinery_door_airlock door = await base.finish_door();
        await door.set_custom_materials(custom_materials);
        door.update_appearance();
        return door;
    }
}
