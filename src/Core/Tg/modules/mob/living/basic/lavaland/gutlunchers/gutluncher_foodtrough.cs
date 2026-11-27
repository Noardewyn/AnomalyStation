// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/gutlunchers/gutluncher_foodtrough.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutluncher_foodtrough.dm:1
public partial class obj_structure_ore_container_food_trough : obj_structure_ore_container
{
    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutluncher_foodtrough.dm:11
    public override async DmTask<object> Entered(atom_movable mover = null, atom old_loc = null, DmList old_locs = null)
    {
        if (!(mover is obj_item_stack_ore))
            return await base.Entered(mover, old_loc, old_locs);
        if (Dm.truthy(list_of_materials[mover.type]))
            return await base.Entered(mover, old_loc, old_locs);
        list_of_materials[mover.type] = Dm.list(
            Dm.kv("pixel_x", rand(Dm.num(x_offsets[1f]), Dm.num(x_offsets[2f]))),
            Dm.kv("pixel_y", rand(Dm.num(y_offsets[1f]), Dm.num(y_offsets[2f]))));
        return await base.Entered(mover, old_loc, old_locs);
    }

    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutluncher_foodtrough.dm:19
    public override async DmTask<object> Exited(atom_movable mover = null, object direction = null)
    {
        if (!(mover is obj_item_stack_ore) || Dm.locate_type(mover.type, contents) != null)
            return await base.Exited(mover, direction);
        Dm.remove(ref list_of_materials, mover.type);
        return await base.Exited(mover, direction);
    }

    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutluncher_foodtrough.dm:25
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_mineral_wood>(drop_location(), 5);
    }

    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutluncher_foodtrough.dm:28
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        foreach (object ore_entry in Dm.each(list_of_materials))
        {
            Type ore_item = ore_entry as Type;
            image ore_icon = image(icon: Dm.initial(ore_item, (atom a) => a.icon), icon_state: Dm.initial(ore_item, (atom a) => a.icon_state), layer: LOW_ITEM_LAYER);
            DmList pixel_positions = list_of_materials[ore_entry] as DmList;
            ore_icon.transform = (ore_icon.transform as matrix).Scale(0.4f, 0.4f);
            ore_icon.pixel_w = Dm.num(pixel_positions["pixel_x"]);
            ore_icon.pixel_z = Dm.num(pixel_positions["pixel_y"]);
            Dm.append(ref dot, ore_icon);
        }
        return dot;
    }
}
