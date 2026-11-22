// Translated from tgstation (snapshot 1d99bd7f): code/modules/manufactorio/machines/unloader.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the manufacturing crate unloader, whole. It takes a closed crate (or an ore box) from behind while its cable has the
// power, and each fast-processing tick sends its contents (mobs stay) forward and then the empty crate, opened, to its side (a
// multitool flips the side); its overlays are the in, out and crate-out arrows. The var blocks are generated.

// tg: code/modules/manufactorio/machines/unloader.dm:1
public partial class obj_machinery_power_manufacturing_unloader : obj_machinery_power_manufacturing
{
    // tg: code/modules/manufactorio/machines/unloader.dm:12
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, generate_io_overlays(dir, COLOR_ORANGE)); // OUT - stuff in it
        Dm.append(ref dot, generate_io_overlays(REVERSE_DIR(dir), COLOR_MODERATE_BLUE)); // IN - crate
        Dm.append(ref dot, generate_io_overlays(turn(dir, Dm.truthy(flip_side) ? 90 : -90), COLOR_ORANGE)); // OUT -- empty crate
        return dot;
    }

    // tg: code/modules/manufactorio/machines/unloader.dm:18
    public override object request_resource() //returns held crate if someone wants to do that for some reason
    {
        DmList real_contents = (DmList)Dm.sub(contents, circuit_value);
        if (!Dm.truthy(Dm.length(real_contents)))
            return null;
        return real_contents[1];
    }

    // tg: code/modules/manufactorio/machines/unloader.dm:24
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        balloon_alert(user, "flipped");
        flip_side = Dm.num(!Dm.truthy(flip_side));
        update_appearance();
        return dot;
    }

    // tg: code/modules/manufactorio/machines/unloader.dm:30
    public override async DmTask<object> receive_resource(atom_movable receiving = null, atom from = null, object receive_dir = null)
    {
        if (Dm.num(surplus()) < power_to_unload_crate || !Dm.eq(receive_dir, REVERSE_DIR(dir)))
            return MANUFACTURING_FAIL;
        DmList real_contents = (DmList)Dm.sub(contents, circuit_value);
        if (Dm.truthy(Dm.length(real_contents)))
            return MANUFACTURING_FAIL;

        if (receiving is obj_structure_closet as_closet)
        {
            if (!Dm.truthy(as_closet.can_open()))
                return MANUFACTURING_FAIL;
        }
        else if (receiving is not obj_structure_ore_box)
            return MANUFACTURING_FAIL;
        await receiving.Move(this, get_dir(receiving, this));
        START_PROCESSING(SSfastprocess, this);
        return MANUFACTURING_SUCCESS;
    }

    // tg: code/modules/manufactorio/machines/unloader.dm:48
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        DmList real_contents = (DmList)Dm.sub(contents, circuit_value);
        if (!Dm.truthy(Dm.length(real_contents)))
            return PROCESS_KILL;
        if (Dm.num(surplus()) < power_to_unload_crate)
            return null;
        add_load(power_to_unload_crate);
        if (real_contents[1] is obj_structure_closet closet)
            return Waiting.NoSleep(unload_crate(closet));
        else
            return Waiting.NoSleep(unload_orebox(real_contents[1] as obj_structure_ore_box));
    }

    // tg: code/modules/manufactorio/machines/unloader.dm:61
    public virtual async DmTask<object> unload_crate(obj_structure_closet closet = null)
    {
        if (!Dm.truthy(closet.contents_initialized))
        {
            closet.contents_initialized = TRUE;
            await closet.PopulateContents();
            if (Dm.truthy(closet._listen_lookup?[COMSIG_CLOSET_CONTENTS_INITIALIZED]))
                closet._SendSignal(COMSIG_CLOSET_CONTENTS_INITIALIZED, Dm.list(closet));
        }
        foreach (atom thing in Dm.each_cast<atom>(closet.contents.Copy()))
        {
            if (ismob(thing))
                continue;
            await send_resource(thing as atom_movable, dir);
        }
        if (!Dm.truthy(Dm.length(closet.contents)) && Dm.truthy(await send_resource(closet, turn(dir, Dm.truthy(flip_side) ? 90 : -90))))
        {
            await closet.open(force: TRUE);
            return PROCESS_KILL;
        }
        return null;
    }

    // tg: code/modules/manufactorio/machines/unloader.dm:74
    public virtual async DmTask<object> unload_orebox(obj_structure_ore_box box = null)
    {
        foreach (atom thing in Dm.each_cast<atom>(box.contents.Copy()))
            await send_resource(thing as atom_movable, dir);
        if (!Dm.truthy(Dm.length(box.contents)) && Dm.truthy(await send_resource(box, turn(dir, Dm.truthy(flip_side) ? 90 : -90))))
            return PROCESS_KILL;
        return null;
    }
}
