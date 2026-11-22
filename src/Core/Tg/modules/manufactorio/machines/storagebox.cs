// Translated from tgstation (snapshot 1d99bd7f): code/modules/manufactorio/machines/storagebox.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the manufacturing storage unit, whole (its file's base request_resource is in _manufacturing.cs). It keeps up to
// sixteen things (stacks merge), never a full closet; a hand dumps it after a second and a quarter, a screwdriver takes it apart in
// five seconds (ten iron sheets, its contents dumped), and someone shut inside climbs out. The var blocks are generated.

// tg: code/modules/manufactorio/machines/storagebox.dm:1
public partial class obj_machinery_power_manufacturing_storagebox : obj_machinery_power_manufacturing
{
    // tg: code/modules/manufactorio/machines/storagebox.dm:15
    public override async DmTask<object> receive_resource(atom_movable receiving = null, atom from = null, object receive_dir = null)
    {
        if (iscloset(receiving) && Dm.truthy(Dm.length(receiving.contents)))
            return MANUFACTURING_FAIL;
        if (Dm.length(Dm.sub(contents, circuit_value)) >= max_stuff && !Dm.truthy(await may_merge_in_contents_and_do_so(receiving as obj_item_stack)))
            return MANUFACTURING_FAIL;
        await receiving.Move(this, receive_dir);
        return MANUFACTURING_SUCCESS;
    }

    // tg: code/modules/manufactorio/machines/storagebox.dm:23
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        object dot = await base.container_resist_act(user, __extra);
        await user.Move((atom)drop_location());
        return dot;
    }

    // tg: code/modules/manufactorio/machines/storagebox.dm:27
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        balloon_alert(user, "disassembling...");
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, this)))
            return ITEM_INTERACT_FAILURE;
        await atom_destruction();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/manufactorio/machines/storagebox.dm:34
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        Dm.@new<obj_item_stack_sheet_iron>(drop_location(), 10);
        await dump_inventory_contents();
        await base.atom_destruction(damage_flag);
    }

    // tg: code/modules/manufactorio/machines/storagebox.dm:39
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(((mob_living)user).combat_mode))
            return dot;
        balloon_alert(user, "dumping..");
        if (!Dm.truthy(await do_after(user, 1.25f * SECONDS, this)))
            return dot;
        await dump_inventory_contents();
        return dot;
    }
}
