// Translated from tgstation (snapshot 1d99bd7f): code/modules/manufactorio/machines/router.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the manufacturing router (a splitter), whole. It passes what it receives to its other three sides in turn, one of
// a stack at a time, never from another router; a multitool toggles the output on the side its user stands at; its overlays show
// each side's in, out or blocked arrow. The var blocks are generated.

// tg: code/modules/manufactorio/machines/router.dm:1
public partial class obj_machinery_power_manufacturing_router : obj_machinery_power_manufacturing // Basically a splitter
{
    // tg: code/modules/manufactorio/machines/router.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        directions = GLOB.cardinals.Copy();
        return dot;
    }

    // tg: code/modules/manufactorio/machines/router.dm:16
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        float to_toggle = get_dir(this, user);
        if (!Dm.@in(to_toggle, GLOB.cardinals))
        {
            balloon_alert(user, "stand inline!");
            return ITEM_INTERACT_FAILURE;
        }
        if (Dm.@in(to_toggle, disabled_dirs))
            Dm.remove(disabled_dirs, to_toggle);
        else
            Dm.append(disabled_dirs, to_toggle);
        update_appearance(UPDATE_OVERLAYS);
        balloon_alert(user, "toggled output");
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/manufactorio/machines/router.dm:29
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            string variant;
            if (Dm.truthy(disabled_dirs.Find(direction)))
                variant = "bl";
            else
                variant = Dm.eq(direction, dir) ? "in" : "out";
            image new_overlay = image(icon: icon, icon_state: Dm.text("splitter_[]", variant), layer: layer + 0.001f, dir: direction);
            Dm.append(ref dot, new_overlay);
        }
        return dot;
    }

    // tg: code/modules/manufactorio/machines/router.dm:40
    public override async DmTask<object> receive_resource(atom_movable receiving = null, atom from = null, object receive_dir = null)
    {
        if (Dm.istype(from, typeof(obj_machinery_power_manufacturing_router)))
            return MANUFACTURING_FAIL;
        DmList filtered = (DmList)Dm.sub(Dm.sub(directions, receive_dir), disabled_dirs);
        if (!Dm.truthy(Dm.length(filtered)))
            directions = GLOB.cardinals.Copy();
        foreach (object target in Dm.each(filtered))
        {
            Dm.remove(directions, target);
            if (isstack(receiving))
                receiving = (atom_movable)await handle_stack((obj_item_stack)receiving, receive_dir);
            if (Dm.truthy(await send_resource(receiving, target)))
            {
                dir = Dm.num(receive_dir);
                update_appearance(UPDATE_OVERLAYS); // im sorry
                return MANUFACTURING_SUCCESS;
            }
        }
        return MANUFACTURING_FAIL;
    }

    // tg: code/modules/manufactorio/machines/router.dm:56
    public virtual async DmTask<object> handle_stack(obj_item_stack stack = null, object direction = null)
    {
        if (Dm.num(stack.amount) <= 1) // last implementation was just not good so lets cheap out
            return stack;
        return await stack.split_stack(1);
    }
}
