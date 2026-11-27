// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G3 (D272): the whole file, the rotation of what a shuttle carries when it docks turned (Meta's mining docks); the alien
// weeds' since gaps X5, and the shuttle bar's.

/*
All shuttleRotate procs go here

If ever any of these procs are useful for non-shuttles, rename it to proc/rotate and move it to be a generic atom proc
*/

/************************************Base proc************************************/

// tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:9
public partial class atom : datum
{
    // tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:9
    public virtual async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        @params ??= Dm.bitor(Dm.bitor(ROTATE_DIR, ROTATE_SMOOTH), ROTATE_OFFSET);
        float turned = Dm.num(rotation);
        if (Dm.truthy(Dm.bitand(@params, ROTATE_DIR)))
        {
            //rotate our direction
            Waiting.NoSleep(setDir(Dm.num(angle2dir(turned + Dm.num(dir2angle(dir))))));
        }

        //resmooth if need be.
        if (Dm.truthy(Dm.bitand(@params, ROTATE_SMOOTH)) && Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
            QUEUE_SMOOTH(this);

        //rotate the pixel offsets too.
        if ((Dm.truthy(pixel_x) || Dm.truthy(pixel_y)) && Dm.truthy(Dm.bitand(@params, ROTATE_OFFSET)))
        {
            if (turned < 0)
                turned += 360;
            for (float turntimes = turned / 90; turntimes > 0; turntimes--)
            {
                float oldPX = pixel_x;
                float oldPY = pixel_y;
                pixel_x = oldPY;
                pixel_y = (oldPX * (-1));
            }
        }
        await DmTask.CompletedTask;
    }
}

/************************************Base /atom/movable proc************************************/

// tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:30
public partial class atom_movable : atom
{
    // tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:30
    public override async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        await base.shuttleRotate(rotation, @params);
        //rotate the physical bounds and offsets for multitile atoms too. Owerride base "rotate the pixel offsets" for multitile atoms.
        //Owerride non zero bound_x, bound_y, pixel_x, pixel_y to zero.
        //Dont take in account starting bound_x, bound_y, pixel_x, pixel_y.
        //So it can unintentionally shift physical bounds of things that starts with non zero bound_x, bound_y.
        if (((bound_height != ICON_SIZE_Y) || (bound_width != ICON_SIZE_X)) && (bound_x == 0) && (bound_y == 0)) //Dont shift things that have non zero bound_x and bound_y, or it move somewhere. Now it BSA and Gateway.
        {
            pixel_x = Dm.truthy(Dm.bitand(dir, Dm.bitor(NORTH, EAST))) ? -bound_width + ICON_SIZE_X : 0;
            pixel_y = Dm.truthy(Dm.bitand(dir, Dm.bitor(NORTH, WEST))) ? -bound_width + ICON_SIZE_X : 0; //?
            bound_x = pixel_x;
            bound_y = pixel_y;
        }
    }
}

/************************************Turf rotate procs************************************/

// tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:44
public partial class turf_closed_mineral : turf_closed
{
    // tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:44
    public override async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        @params = Dm.bitand(@params ?? 0, Dm.bitnot(ROTATE_OFFSET));
        await base.shuttleRotate(rotation, @params);
    }
}

/************************************Mob rotate procs************************************/

//override to avoid rotating pixel_xy on mobs
// tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:51
public partial class mob : atom_movable
{
    // tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:51
    public override async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        @params = NONE;
        await base.shuttleRotate(rotation, @params);
        if (buckled == null)
            Waiting.NoSleep(setDir(Dm.num(angle2dir(Dm.num(rotation) + Dm.num(dir2angle(dir))))));
    }
}

// tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:57
public partial class mob_dead_observer : mob_dead
{
    // tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:57
    public override async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        await base.shuttleRotate(rotation, @params);
        update_appearance();
    }
}

/************************************Structure rotate procs************************************/

//Fixes dpdir on shuttle rotation
// tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:64
public partial class obj_structure_disposalpipe : obj_structure
{
    // tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:64
    public override async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        await base.shuttleRotate(rotation, @params);
        float new_dpdir = 0;
        foreach (object D in Dm.each(GLOB.cardinals))
        {
            if (Dm.truthy(Dm.bitand(dpdir, D)))
                new_dpdir = Dm.bitor(new_dpdir, Dm.num(angle2dir(Dm.num(rotation) + Dm.num(dir2angle(D)))));
        }
        dpdir = new_dpdir;
    }
}

// tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:72 (the shuttle bar's kick, a Lavaland ruin's table)
public partial class obj_structure_table_wood_shuttle_bar : obj_structure_table_wood
{
    // tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:72
    public override async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        await base.shuttleRotate(rotation, @params);
        boot_dir = Dm.num(angle2dir(Dm.num(rotation) + Dm.num(dir2angle(boot_dir))));
    }
}

// tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:76 (gaps X5: the alien weeds)
public partial class obj_structure_alien_weeds : obj_structure_alien
{
    // tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:76
    public override async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        @params = Dm.num(Dm.bitand(@params, Dm.bitnot(ROTATE_OFFSET)));
        await base.shuttleRotate(rotation, @params);
    }
}

/************************************Machine rotate procs************************************/

// tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:82
public partial class obj_machinery_atmospherics : obj_machinery
{
    // tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:82
    public override async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        DmList real_node_connect = get_node_connects() as DmList;
        for (float i = 1; i <= device_type; i++)
        {
            object node_dir = real_node_connect[i];
            if (node_dir == null)
                continue;

            real_node_connect[i] = Dm.turn(Dm.num(node_dir), -Dm.num(rotation));
        }

        await base.shuttleRotate(rotation, @params);
        set_init_directions();
        DmList supposed_node_connect = get_node_connects() as DmList;
        DmList nodes_copy = nodes.Copy();

        for (float i = 1; i <= device_type; i++)
        {
            object node_dir = real_node_connect[i];
            if (node_dir == null)
                continue;

            float new_pos = supposed_node_connect.Find(node_dir);
            nodes[new_pos] = nodes_copy[i];
        }
    }
}

// tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:105
public partial class obj_machinery_gateway : obj_machinery
{
    // tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:105
    public override async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        @params = NONE;
        await base.shuttleRotate(rotation, @params);
    }
}

// tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:109
public partial class obj_machinery_door_airlock : obj_machinery_door
{
    // tg: code/modules/shuttle/mobile_port/shuttle_rotate_callbacks.dm:109
    public override async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        await base.shuttleRotate(rotation, @params);
        if (Dm.truthy(cyclelinkeddir) && Dm.truthy(Dm.bitand(@params, ROTATE_DIR)))
        {
            cyclelinkeddir = Dm.num(angle2dir(Dm.num(rotation) + Dm.num(dir2angle(cyclelinkeddir))));
            // If we update the linked airlock here, the partner airlock might
            // not be present yet, so don't do that. Just assume we're still
            // partnered with the same airlock as before.
        }
    }
}
