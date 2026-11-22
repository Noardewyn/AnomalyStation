// Translated from tgstation (snapshot 1d99bd7f): code/modules/manufactorio/machines/smelter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the manufacturing smelter, whole. It takes items from behind while its cable has the power (up to five, merging
// stacks), and each manufacturing cycle refines the last one: ore turns into up to five of its refined sheets, anything else is
// burnt (fire_act at 1400 K) and passed on; it shows smelter_on while it works. The var blocks are generated.

// tg: code/modules/manufactorio/machines/smelter.dm:1
public partial class obj_machinery_power_manufacturing_smelter : obj_machinery_power_manufacturing
{
    // tg: code/modules/manufactorio/machines/smelter.dm:11
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, generate_io_overlays(dir, COLOR_ORANGE)); // OUT - stuff in it
        Dm.append(ref dot, generate_io_overlays(REVERSE_DIR(dir), COLOR_MODERATE_BLUE)); // IN - to crush
        return dot;
    }

    // tg: code/modules/manufactorio/machines/smelter.dm:16
    public override async DmTask<object> receive_resource(atom_movable receiving = null, atom from = null, object receive_dir = null)
    {
        if (!isitem(receiving) || Dm.num(surplus()) < power_cost || !Dm.eq(receive_dir, REVERSE_DIR(dir)))
            return MANUFACTURING_FAIL;
        DmList stacks = (DmList)Dm.sub(contents, circuit_value);
        if (Dm.length(stacks) >= 5 && !Dm.truthy(await may_merge_in_contents_and_do_so(receiving as obj_item_stack)))
            return MANUFACTURING_FAIL;
        await receiving.Move(this, get_dir(receiving, this));
        START_PROCESSING(SSmanufacturing, this);
        return MANUFACTURING_SUCCESS;
    }

    // tg: code/modules/manufactorio/machines/smelter.dm:26
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_NULL(ref withheld);
        return dot;
    }

    // tg: code/modules/manufactorio/machines/smelter.dm:30
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        if (withheld != null)
            await withheld.Move((atom)drop_location());
        await base.atom_destruction(damage_flag);
    }

    // tg: code/modules/manufactorio/machines/smelter.dm:34
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        DmList stacks = (DmList)Dm.sub(contents, circuit_value);
        if (!Dm.truthy(Dm.length(stacks)))
            return null;

        DmList stacks_preprocess = (DmList)Dm.sub(contents, circuit_value);
        obj_item_stack_ore ore = stacks_preprocess[Dm.length(stacks_preprocess)] as obj_item_stack_ore;
        object ore_thing = stacks_preprocess[Dm.length(stacks_preprocess)]; // tg's typed local holds whatever was put in last
        if (ore_thing == null)
            return null;
        if (withheld == null && Dm.num(surplus()) >= power_cost)
        {
            icon_state = "smelter_on";
            add_load(power_cost);
            if (ore != null)
            {
                obj_item_stack new_stack = Dm.@new(ore.refined_type as Type, null, min(5, Dm.num(ore.amount)), FALSE) as obj_item_stack;
                Waiting.NoSleep(new_stack.moveToNullspace());
                Waiting.NoSleep(ore.use(min(5, Dm.num(ore.amount))));
                ore_thing = new_stack;
            }
            else
                Waiting.NoSleep(((atom)ore_thing).fire_act(1400));
            withheld = ore_thing as atom_movable;
        }
        else if (Dm.num(surplus()) < power_cost)
            icon_state = "smelter";
        if (Dm.truthy(Waiting.NoSleep(send_resource(withheld, dir))))
            withheld = null; // nullspace thumbs down
        if (!Dm.truthy(Dm.length(Dm.sub(contents, circuit_value))))
            return PROCESS_KILL; //we finished
        return null;
    }
}
