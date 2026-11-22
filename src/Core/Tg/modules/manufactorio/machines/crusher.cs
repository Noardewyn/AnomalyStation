// Translated from tgstation (snapshot 1d99bd7f): code/modules/manufactorio/machines/crusher.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the manufacturing crusher, whole. It takes items (not ore, not indestructible ones) from behind while its cable has
// the power, holds up to five, and each manufacturing cycle crushes the last one into its materials' sheets (a boulder's into ores),
// keeping the fractions below a sheet until they add up; an item with no materials is destroyed, a living thing is hurt and passed on.
// The var blocks are generated.

// tg: code/modules/manufactorio/machines/crusher.dm:1
public partial class obj_machinery_power_manufacturing_crusher : obj_machinery_power_manufacturing //todo make it work for other stuff
{
    // tg: code/modules/manufactorio/machines/crusher.dm:15
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, generate_io_overlays(dir, COLOR_ORANGE)); // OUT - stuff in it
        Dm.append(ref dot, generate_io_overlays(REVERSE_DIR(dir), COLOR_MODERATE_BLUE)); // IN - to crush
        return dot;
    }

    // tg: code/modules/manufactorio/machines/crusher.dm:20
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_NULL(ref withholding);
        return dot;
    }

    // tg: code/modules/manufactorio/machines/crusher.dm:24
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        if (withholding != null)
            await withholding.Move((atom)drop_location());
        await base.atom_destruction(damage_flag);
    }

    // tg: code/modules/manufactorio/machines/crusher.dm:28
    public override async DmTask<object> receive_resource(atom_movable receiving = null, atom from = null, object receive_dir = null)
    {
        if (Dm.istype(receiving, typeof(obj_item_stack_ore)) || Dm.truthy(Dm.bitand(((obj)receiving).resistance_flags, INDESTRUCTIBLE)) || !isitem(receiving) || Dm.num(surplus()) < crush_cost || !Dm.eq(receive_dir, REVERSE_DIR(dir)))
            return MANUFACTURING_FAIL;
        if (Dm.length(Dm.sub(contents, circuit_value)) >= capacity && Dm.truthy(await may_merge_in_contents_and_do_so(receiving as obj_item_stack)))
            return MANUFACTURING_FAIL;
        await receiving.Move(this, get_dir(receiving, this));
        START_PROCESSING(SSmanufacturing, this);
        return MANUFACTURING_SUCCESS;
    }

    // tg: code/modules/manufactorio/machines/crusher.dm:37
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == withholding)
            withholding = null;
        return dot;
    }

    // tg: code/modules/manufactorio/machines/crusher.dm:42
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (withholding != null && !Dm.truthy(Waiting.NoSleep(send_resource(withholding, dir))))
            return null;
        foreach (object material in Dm.each(held_mats.Copy()))
        {
            if (Dm.num(held_mats[material]) >= 1)
            {
                float new_amount = floor(Dm.num(held_mats[material]));
                held_mats[material] = Dm.num(held_mats[material]) - new_amount;
                if (Dm.num(held_mats[material]) <= 0)
                    Dm.remove(held_mats, material);
                withholding = Dm.@new((Type)material, null, new_amount) as atom_movable;
                return null;
            }
        }
        DmList poor_saps = (DmList)Dm.sub(contents, circuit_value);
        if (!Dm.truthy(Dm.length(poor_saps)))
            return PROCESS_KILL;
        if (Dm.num(surplus()) < crush_cost)
            return null;
        object victim_thing = poor_saps[Dm.length(poor_saps)];
        if (victim_thing is obj victim) //todo handling for other things
        {
            if (!Dm.truthy(Dm.length(victim.custom_materials)))
            {
                add_load(crush_cost);
                Waiting.NoSleep(victim.atom_destruction());
            }
            DmList objects = (DmList)Dm.add(victim.contents, victim);
            foreach (obj @object in Dm.each<obj>(objects))
            {
                foreach (datum_material possible_mat in Dm.each_cast<datum_material>(@object.custom_materials?.Copy()))
                {
                    object quantity = @object.custom_materials[possible_mat];
                    Waiting.NoSleep(@object.set_custom_materials((DmList)Dm.sub(@object.custom_materials.Copy(), possible_mat), 1));
                    Type type_to_use = Dm.istype(victim, typeof(obj_item_boulder)) ? possible_mat.ore_type : possible_mat.sheet_type;
                    if (Dm.num(quantity) < SHEET_MATERIAL_AMOUNT)
                    {
                        if (!Dm.@in(type_to_use, held_mats))
                        {
                            held_mats[type_to_use] = Dm.num(quantity) / SHEET_MATERIAL_AMOUNT;
                            continue;
                        }
                        held_mats[type_to_use] = Dm.num(held_mats[type_to_use]) + Dm.num(quantity) / SHEET_MATERIAL_AMOUNT;
                        continue;
                    }
                    obj_item_stack_sheet new_item = Dm.@new(type_to_use, this, Dm.num(quantity) / SHEET_MATERIAL_AMOUNT) as obj_item_stack_sheet;
                    if (!Dm.truthy(Waiting.NoSleep(send_resource(new_item, dir))))
                    {
                        withholding = new_item;
                        return null;
                    }
                }
            }
        }
        else if (isliving(victim_thing))
        {
            mob_living poor_sap = (mob_living)victim_thing;
            Waiting.NoSleep(poor_sap.adjust_brute_loss(95, TRUE));
            if (!Dm.truthy(Waiting.NoSleep(send_resource(poor_sap, dir))))
            {
                withholding = poor_sap;
                return null;
            }
        }
        return null;
    }
}
