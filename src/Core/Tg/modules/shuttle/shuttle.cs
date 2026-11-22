// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/shuttle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5x slice 5: the docking port's singularity procs. M10 slice G3 (D272): the whole file but highlight (#ifdef TESTING).

//NORTH default dir
// tg: code/modules/shuttle/shuttle.dm:2
public partial class obj_docking_port : obj
{
    ///register to SSshuttles
    // tg: code/modules/shuttle/shuttle.dm:38
    public virtual object register(object replace = null, object custom = null)
    {
        if (Dm.truthy(registered))
        {
            warning("docking_port registered multiple times");
            unregister();
        }
        registered = TRUE;
        return null;
    }

    ///unregister from SSshuttles
    // tg: code/modules/shuttle/shuttle.dm:46
    public virtual object unregister()
    {
        if (!Dm.truthy(registered))
            warning("docking_port unregistered multiple times");
        registered = FALSE;
        return null;
    }

    // tg: code/modules/shuttle/shuttle.dm:52
    public virtual object Check_id(object id = null)
    {
        return null;
    }

    //these objects are indestructible
    // tg: code/modules/shuttle/shuttle.dm:56
    public override object Destroy(float? force = null)
    {
        // unless you assert that you know what you're doing. Horrible things
        // may result.
        if (Dm.truthy(force))
        {
            base.Destroy(force);
            return QDEL_HINT_QUEUE;
        }
        else
        {
            return QDEL_HINT_LETMELIVE;
        }
    }

    // tg: code/modules/shuttle/shuttle.dm:65
    public override object has_gravity(turf gravity_turf = null)
    {
        return TRUE;
    }

    // tg: code/modules/shuttle/shuttle.dm:68
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        return null;
    }

    // tg: code/modules/shuttle/shuttle.dm:71
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }

    // tg: code/modules/shuttle/shuttle.dm:74
    public override async DmTask<object> singularity_act()
    {
        return FALSE;
    }

    // tg: code/modules/shuttle/shuttle.dm:77
    public override async DmTask shuttleRotate(object rotation = null, float? @params = null)
    {
        //we don't rotate with shuttles via this code.
    }

    ///returns a list(x0,y0, x1,y1) where points 0 and 1 are bounding corners of the projected rectangle
    // tg: code/modules/shuttle/shuttle.dm:81
    public virtual DmList return_coords(object _x = null, object _y = null, object _dir = null)
    {
        _dir ??= dir;
        _x ??= x;
        _y ??= y;

        //byond's sin and cos functions are inaccurate. This is faster and perfectly accurate
        float cos = 1;
        float sin = 0;
        float d = Dm.num(_dir);
        if (d == WEST)
        {
            cos = 0;
            sin = 1;
        }
        else if (d == SOUTH)
        {
            cos = -1;
            sin = 0;
        }
        else if (d == EAST)
        {
            cos = 0;
            sin = -1;
        }

        float px = Dm.num(_x), py = Dm.num(_y);
        return Dm.list(
            px + (-dwidth * cos) - (-dheight * sin),
            py + (-dwidth * sin) + (-dheight * cos),
            px + (-dwidth + width - 1) * cos - (-dheight + height - 1) * sin,
            py + (-dwidth + width - 1) * sin + (-dheight + height - 1) * cos);
    }

    ///returns turfs within our projected rectangle in no particular order
    // tg: code/modules/shuttle/shuttle.dm:111
    public virtual DmList return_turfs()
    {
        DmList coords = return_coords();
        return block(
            Dm.num(coords[1]), Dm.num(coords[2]), z,
            Dm.num(coords[3]), Dm.num(coords[4]), z);
    }

    ///returns turfs within our projected rectangle in a specific order.this ensures that turfs are copied over in the same order, regardless of any rotation
    // tg: code/modules/shuttle/shuttle.dm:119
    public virtual DmList return_ordered_turfs(object _x = null, object _y = null, object _z = null, object _dir = null)
    {
        float cos = 1;
        float sin = 0;
        float d = Dm.num(_dir);
        if (d == WEST)
        {
            cos = 0;
            sin = 1;
        }
        else if (d == SOUTH)
        {
            cos = -1;
            sin = 0;
        }
        else if (d == EAST)
        {
            cos = 0;
            sin = -1;
        }

        DmList dot = Dm.list();
        float px = Dm.num(_x), py = Dm.num(_y), pz = Dm.num(_z);

        for (float dx = 0; dx <= width - 1; dx++)
        {
            float compX = dx - dwidth;
            for (float dy = 0; dy <= height - 1; dy++)
            {
                float compY = dy - dheight;
                // realX = _x + compX*cos - compY*sin
                // realY = _y + compY*cos - compX*sin
                // locate(realX, realY, _z)
                turf T = locate(px + compX * cos - compY * sin, py + compY * cos + compX * sin, pz);
                dot[T] = NONE;
            }
        }
        return dot;
    }

    //return first-found touching dockingport
    // tg: code/modules/shuttle/shuttle.dm:165
    public virtual object get_docked()
    {
        return locate<obj_docking_port_stationary>(loc);
    }

    // Return id of the docked docking_port
    // tg: code/modules/shuttle/shuttle.dm:169
    public virtual object getDockedId()
    {
        var P = get_docked() as obj_docking_port;
        if (P != null)
            return P.shuttle_id;
        return null;
    }

    // Say that A in the absolute (rectangular) bounds of this shuttle or no.
    // tg: code/modules/shuttle/shuttle.dm:175
    public virtual object is_in_shuttle_bounds(atom A = null)
    {
        turf T = get_turf(A);
        if (T.z != z)
            return FALSE;
        DmList bounds = return_coords();
        float x0 = Dm.num(bounds[1]);
        float y0 = Dm.num(bounds[2]);
        float x1 = Dm.num(bounds[3]);
        float y1 = Dm.num(bounds[4]);
        // ISINRANGE (__DEFINES/maths.dm), expanded inline
        if (!(T.x >= Math.Min(x0, x1) && T.x <= Math.Max(x0, x1)))
            return FALSE;
        if (!(T.y >= Math.Min(y0, y1) && T.y <= Math.Max(y0, y1)))
            return FALSE;
        return TRUE;
    }
}
