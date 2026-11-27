// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/mook/mook_village.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


///unique items that spawn at the mook village
// tg: code/modules/mob/living/basic/lavaland/mook/mook_village.dm:2
public partial class obj_structure_ore_container_material_stand : obj_structure_ore_container
{
    ///put ore icons on the counter!
    // tg: code/modules/mob/living/basic/lavaland/mook/mook_village.dm:14
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        foreach (obj_item_stack_ore ore_item in Dm.each<obj_item_stack_ore>(contents))
        {
            image ore_icon = image(icon: Dm.initial(ore_item, (atom a) => a.icon), icon_state: Dm.initial(ore_item, (atom a) => a.icon_state), layer: LOW_ITEM_LAYER);
            ore_icon.transform = (ore_icon.transform as matrix).Scale(0.6f, 0.6f);
            ore_icon.pixel_w = rand(9, 17);
            ore_icon.pixel_z = rand(2, 4);
            Dm.append(ref dot, ore_icon);
        }
        return dot;
    }
}
