// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/transit_tubes/transit_tube.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Transit tubes (the owner, 2026-10-26): the whole file. The tube sections, their connections (tube_dirs), the pod's exit search and
// delays, detaching with a wrench and the decorative end overlays. The var blocks are generated.

// tg: code/game/objects/structures/transit_tubes/transit_tube.dm:3
public partial class obj_structure_transit_tube : obj_structure
{
    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:16
    [DmParams("mapload", "newdirection")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object newdirection = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(newdirection))
            Waiting.NoSleep(setDir(newdirection));
        init_tube_dirs();
        update_appearance();
        Waiting.NoSleep(AddElement(typeof(datum_element_climbable)));
        Waiting.NoSleep(AddElement(typeof(datum_element_elevation), Dm.named("pixel_shift", 12)));
        return dot;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:25
    public override object Destroy(float? force = null)
    {
        foreach (obj_structure_transit_tube_pod P in Dm.each<obj_structure_transit_tube_pod>(loc))
            Waiting.NoSleep(P.deconstruct(FALSE));
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:30
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await base.singularity_pull(singularity, current_size);
        if (Dm.num(current_size) >= STAGE_FIVE)
            await deconstruct(FALSE);
        return null;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:35
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (tube_construction == null)
            return NONE;

        foreach (obj_structure_transit_tube_pod pod in Dm.each<obj_structure_transit_tube_pod>(loc))
        {
            to_chat(user, span_warning(I18n.T("Remove the pod first!")));
            return ITEM_INTERACT_BLOCKING;
        }

        user.visible_message(span_notice(I18n.T("[] starts to detach \\the [].", user, this)),
            span_notice(I18n.T("You start to detach \\the []...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 2 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You detach \\the [].", this)));
        obj_structure_c_transit_tube husk = Dm.@new(tube_construction, loc) as obj_structure_c_transit_tube;
        await husk.setDir(dir);
        transfer_fingerprints_to(husk);
        husk.add_fingerprint(user);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:56
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        foreach (obj_structure_transit_tube_pod pod in Dm.each<obj_structure_transit_tube_pod>(loc))
            await pod.item_interaction(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // Called to check if a pod should stop upon entering this tube.
    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:62
    public virtual object should_stop_pod(object pod = null, object from_dir = null)
    {
        return FALSE;
    }

    // Called when a pod stops in this tube section.
    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:66
    public virtual void pod_stopped(object pod = null, object from_dir = null)
    {
        return;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:70
    public virtual object has_entrance(object from_dir = null)
    {
        from_dir = REVERSE_DIR(Dm.num(from_dir));

        foreach (object direction in Dm.each(tube_dirs))
        {
            if (Dm.eq(direction, from_dir))
                return TRUE;
        }

        return FALSE;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:81
    public virtual object has_exit(object in_dir = null)
    {
        foreach (object direction in Dm.each(tube_dirs))
        {
            if (Dm.eq(direction, in_dir))
                return TRUE;
        }

        return FALSE;
    }

    // Searches for an exit direction within 45 degrees of the
    //  specified dir. Returns that direction, or 0 if none match.
    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:92
    public virtual object get_exit(object in_dir = null)
    {
        object near_dir = 0;
        object in_dir_cw = turn(in_dir, -45);
        object in_dir_ccw = turn(in_dir, 45);

        foreach (object direction in Dm.each(tube_dirs))
        {
            if (Dm.eq(direction, in_dir))
                return direction;

            else if (Dm.eq(direction, in_dir_cw))
                near_dir = direction;

            else if (Dm.eq(direction, in_dir_ccw))
                near_dir = direction;
        }

        return near_dir;
    }

    // Return how many BYOND ticks to wait before entering/exiting
    //  the tube section. Default action is to return the value of
    //  a var, which wouldn't need a proc, but it makes it possible
    //  for later tube types to interact in more interesting ways
    //  such as being very fast in one direction, but slow in others
    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:115
    public virtual object exit_delay__proc(object pod = null, object to_dir = null)
    {
        return exit_delay;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:118
    public virtual object enter_delay__proc(object pod = null, object to_dir = null)
    {
        return enter_delay;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:122
    public virtual void init_tube_dirs()
    {
        switch (Dm.num(dir))
        {
            case NORTH:
                tube_dirs = Dm.list(NORTH, SOUTH);
                break;
            case SOUTH:
                tube_dirs = Dm.list(NORTH, SOUTH);
                break;
            case EAST:
                tube_dirs = Dm.list(EAST, WEST);
                break;
            case WEST:
                tube_dirs = Dm.list(EAST, WEST);
                break;
        }
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:133
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        foreach (object direction in Dm.each(tube_dirs))
        {
            if (!Dm.truthy(ISDIAGONALDIR(Dm.num(direction))))
            {
                Dm.append(ref dot, create_tube_overlay(direction));
                continue;
            }
            if (!Dm.truthy(Dm.bitand(direction, NORTH)))
                continue;

            Dm.append(ref dot, create_tube_overlay(Dm.bitxor(direction, 3), NORTH));
            if (Dm.truthy(Dm.bitand(direction, EAST)))
                Dm.append(ref dot, create_tube_overlay(Dm.bitxor(direction, 12), EAST));
            else
                Dm.append(ref dot, create_tube_overlay(Dm.bitxor(direction, 12), WEST));
        }
        return dot;
    }

    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:148
    public virtual object create_tube_overlay(object direction = null, object shift_dir = null)
    {
        // We use image() because a mutable appearance will have its dir mirror the parent which sort of fucks up what we're doing here
        image tube_overlay = image(icon, dir: direction);
        if (Dm.truthy(shift_dir))
        {
            tube_overlay.icon_state = "decorative_diag";
            switch (Dm.num(shift_dir))
            {
                case NORTH:
                    tube_overlay.pixel_z = 32;
                    break;
                case SOUTH:
                    tube_overlay.pixel_z = -32;
                    break;
                case EAST:
                    tube_overlay.pixel_w = 32;
                    break;
                case WEST:
                    tube_overlay.pixel_w = -32;
                    break;
            }
        }
        else
            tube_overlay.icon_state = "decorative";

        // DEVIATION(DV12): tube_overlay.overlays += emissive_blocker(icon, tube_overlay.icon_state, src): emissives are not ported (D24, lead decision D2).
        return tube_overlay;
    }
}

// tg: code/game/objects/structures/transit_tubes/transit_tube.dm:174
public partial class obj_structure_transit_tube_diagonal : obj_structure_transit_tube
{
    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:177
    public override void init_tube_dirs()
    {
        switch (Dm.num(dir))
        {
            case NORTH:
                tube_dirs = Dm.list(NORTHEAST, SOUTHWEST);
                break;
            case SOUTH:
                tube_dirs = Dm.list(NORTHEAST, SOUTHWEST);
                break;
            case EAST:
                tube_dirs = Dm.list(NORTHWEST, SOUTHEAST);
                break;
            case WEST:
                tube_dirs = Dm.list(NORTHWEST, SOUTHEAST);
                break;
        }
    }
}

// tg: code/game/objects/structures/transit_tubes/transit_tube.dm:203
public partial class obj_structure_transit_tube_curved : obj_structure_transit_tube
{
    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:206
    public override void init_tube_dirs()
    {
        switch (Dm.num(dir))
        {
            case NORTH:
                tube_dirs = Dm.list(NORTH, SOUTHWEST);
                break;
            case SOUTH:
                tube_dirs = Dm.list(SOUTH, NORTHEAST);
                break;
            case EAST:
                tube_dirs = Dm.list(EAST, NORTHWEST);
                break;
            case WEST:
                tube_dirs = Dm.list(SOUTHEAST, WEST);
                break;
        }
    }
}

// tg: code/game/objects/structures/transit_tubes/transit_tube.dm:218
public partial class obj_structure_transit_tube_curved_flipped : obj_structure_transit_tube_curved
{
    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:221
    public override void init_tube_dirs()
    {
        switch (Dm.num(dir))
        {
            case NORTH:
                tube_dirs = Dm.list(NORTH, SOUTHEAST);
                break;
            case SOUTH:
                tube_dirs = Dm.list(SOUTH, NORTHWEST);
                break;
            case EAST:
                tube_dirs = Dm.list(EAST, SOUTHWEST);
                break;
            case WEST:
                tube_dirs = Dm.list(NORTHEAST, WEST);
                break;
        }
    }
}

// tg: code/game/objects/structures/transit_tubes/transit_tube.dm:234
public partial class obj_structure_transit_tube_junction : obj_structure_transit_tube
{
    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:237
    public override void init_tube_dirs()
    {
        switch (Dm.num(dir))
        {
            case NORTH:
                tube_dirs = Dm.list(NORTH, SOUTHEAST, SOUTHWEST); //ending with the prefered direction
                break;
            case SOUTH:
                tube_dirs = Dm.list(SOUTH, NORTHWEST, NORTHEAST);
                break;
            case EAST:
                tube_dirs = Dm.list(EAST, SOUTHWEST, NORTHWEST);
                break;
            case WEST:
                tube_dirs = Dm.list(WEST, NORTHEAST, SOUTHEAST);
                break;
        }
    }
}

// tg: code/game/objects/structures/transit_tubes/transit_tube.dm:249
public partial class obj_structure_transit_tube_junction_flipped : obj_structure_transit_tube_junction
{
    // tg: code/game/objects/structures/transit_tubes/transit_tube.dm:252
    public override void init_tube_dirs()
    {
        switch (Dm.num(dir))
        {
            case NORTH:
                tube_dirs = Dm.list(NORTH, SOUTHWEST, SOUTHEAST); //ending with the prefered direction
                break;
            case SOUTH:
                tube_dirs = Dm.list(SOUTH, NORTHEAST, NORTHWEST);
                break;
            case EAST:
                tube_dirs = Dm.list(EAST, NORTHWEST, SOUTHWEST);
                break;
            case WEST:
                tube_dirs = Dm.list(WEST, SOUTHEAST, NORTHEAST);
                break;
        }
    }
}
