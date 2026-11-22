// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/designs/autolathe/multi-department_designs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): this file's design procs (SSresearch makes every design); the designs are var blocks (generated, D111).

// tg: code/modules/research/designs/autolathe/multi-department_designs.dm:176
public partial class datum_design_toolbox : datum_design
{
    // tg: code/modules/research/designs/autolathe/multi-department_designs.dm:186
    public override async DmTask<object> create_result(atom drop_loc = null, DmList custom_materials = null, object amount = null)
    {
        obj_item_storage_toolbox toolbox = await base.create_result(drop_loc, custom_materials, amount) as obj_item_storage_toolbox;
        if (Dm.truthy(length(custom_materials)) && custom_materials[1] is not datum_material_iron)
            return toolbox;

        // Default and custom material iron toolboxes get a random color assigned rather than being greyscale'd
        string toolbox_color = Dm.str(pick("blue", "yellow", "red"));
        toolbox.icon_state = toolbox_color;
        toolbox.inhand_icon_state = Dm.text("toolbox_[]", toolbox_color);
        toolbox.material_flags = Dm.bitand(toolbox.material_flags, Dm.bitnot(MATERIAL_COLOR));
        await toolbox.remove_atom_colour(FIXED_COLOUR_PRIORITY);
        return toolbox;
    }
}
