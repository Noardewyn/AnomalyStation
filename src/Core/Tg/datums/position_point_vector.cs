// Translated from tgstation (snapshot 1d99bd7f): code/datums/position_point_vector.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 projectiles slice (D165): the whole file, the points and vectors a projectile moves with. The var blocks are generated (D111).

public static partial class GlobalProcs
{
    // tg: code/datums/position_point_vector.dm:1
    public static object point_midpoint_points(datum_point a = null, datum_point b = null) //Obviously will not support multiZ calculations! Same for the two below.
    {
        return Dm.@new<datum_point>(Dm.named("_z", a.z), Dm.named("_pixel_x", Dm.mul(Dm.add(a.return_px(), b.return_px()), 0.5f)), Dm.named("_pixel_y", Dm.mul(Dm.add(a.return_py(), b.return_py()), 0.5f)));
    }

    // tg: code/datums/position_point_vector.dm:4
    public static object pixel_length_between_points(datum_point a = null, datum_point b = null)
    {
        return sqrt(Dm.pow(Dm.num(b.return_px()) - Dm.num(a.return_px()), 2) + Dm.pow(Dm.num(b.return_py()) - Dm.num(a.return_py()), 2));
    }

    // tg: code/datums/position_point_vector.dm:7
    public static object angle_between_points(datum_point a = null, datum_point b = null)
    {
        return ATAN2(Dm.num(b.return_py()) - Dm.num(a.return_py()), Dm.num(b.return_px()) - Dm.num(a.return_px()));
    }
}

/// For positions with map x/y/z and pixel x/y so you don't have to return lists. Could use addition/subtraction in the future I guess.
// tg: code/datums/position_point_vector.dm:11
public partial class datum_position : datum
{
    // tg: code/datums/position_point_vector.dm:18
    public virtual object valid()
    {
        return ((((x is var and__1 && !Dm.truthy(and__1) ? and__1 : y) is var and__2 && !Dm.truthy(and__2) ? and__2 : z) is var and__3 && !Dm.truthy(and__3) ? and__3 : (object)Dm.num(pixel_x != null && pixel_y != null)));
    }

    // tg: code/datums/position_point_vector.dm:21
    [DmParams("_x", "_y", "_z", "_pixel_x", "_pixel_y")]
    public override async DmTask New(params object[] __extra) //first argument can also be a /datum/point.
    {
        object _x = Dm.arg(__extra, 0);
        object _y = Dm.arg(__extra, 1);
        object _z = Dm.arg(__extra, 2);
        object _pixel_x = Dm.arg(__extra, 3);
        object _pixel_y = Dm.arg(__extra, 4);
        _x ??= 0;
        _y ??= 0;
        _z ??= 0;
        _pixel_x ??= 0;
        _pixel_y ??= 0;
        if (istype(_x, typeof(datum_point)))
        {
            datum_point P = _x as datum_point;
            turf T = P.return_turf() as turf;
            _x = T.x;
            _y = T.y;
            _z = T.z;
            _pixel_x = P.pixel_x;
            _pixel_y = P.pixel_y;
        }
        else if (isatom(_x))
        {
            atom A = _x as atom;
            _x = A.x;
            _y = A.y;
            _z = A.z;
            _pixel_x = A.pixel_x;
            _pixel_y = A.pixel_y;
        }
        x = Dm.num_or_null(_x);
        y = Dm.num_or_null(_y);
        z = Dm.num_or_null(_z);
        pixel_x = Dm.num_or_null(_pixel_x);
        pixel_y = Dm.num_or_null(_pixel_y);
    }

    // tg: code/datums/position_point_vector.dm:43
    public virtual object return_turf()
    {
        return locate(Dm.num(x), Dm.num(y), Dm.num(z));
    }

    // tg: code/datums/position_point_vector.dm:46
    public virtual object return_px()
    {
        return pixel_x;
    }

    // tg: code/datums/position_point_vector.dm:49
    public virtual object return_py()
    {
        return pixel_y;
    }

    // tg: code/datums/position_point_vector.dm:52
    public virtual object return_point()
    {
        return Dm.@new<datum_point>(this);
    }
}

/// A precise point on the map in absolute pixel locations based on world.icon_size. Pixels are FROM THE EDGE OF THE MAP!
// tg: code/datums/position_point_vector.dm:56
public partial class datum_point : datum
{
    // tg: code/datums/position_point_vector.dm:63
    public virtual object valid()
    {
        return ((x is var and__1 && !Dm.truthy(and__1) ? and__1 : y) is var and__2 && !Dm.truthy(and__2) ? and__2 : z);
    }

    // tg: code/datums/position_point_vector.dm:66
    public virtual object copy_to(datum_point p = null)
    {
        p ??= Dm.@new<datum_point>();
        p.x = x;
        p.y = y;
        p.z = z;
        return p;
    }

    /// First argument can also be a /datum/position or /atom.
    // tg: code/datums/position_point_vector.dm:73
    [DmParams("_x", "_y", "_z", "_pixel_x", "_pixel_y")]
    public override async DmTask New(params object[] __extra)
    {
        object _x = Dm.arg(__extra, 0);
        object _y = Dm.arg(__extra, 1);
        object _z = Dm.arg(__extra, 2);
        object _pixel_x = Dm.arg(__extra, 3);
        object _pixel_y = Dm.arg(__extra, 4);
        _pixel_x ??= 0;
        _pixel_y ??= 0;
        if (istype(_x, typeof(datum_position)))
        {
            datum_position P = _x as datum_position;
            _x = P.x;
            _y = P.y;
            _z = P.z;
            _pixel_x = P.pixel_x;
            _pixel_y = P.pixel_y;
        }
        else if (istype(_x, typeof(atom)))
        {
            atom A = _x as atom;
            _x = A.x;
            _y = A.y;
            _z = A.z;
            _pixel_x = A.pixel_x;
            _pixel_y = A.pixel_y;
        }
        initialize_location(_x, _y, _z, _pixel_x, _pixel_y);
    }

    // tg: code/datums/position_point_vector.dm:90
    public virtual void initialize_location(object tile_x = null, object tile_y = null, object tile_z = null, object p_x = null, object p_y = null)
    {
        if (tile_x != null)
            x = Dm.num_or_null(tile_x);
        if (tile_y != null)
            y = Dm.num_or_null(tile_y);
        if (tile_z != null)
            z = Dm.num_or_null(tile_z);
        if (p_x != null)
        {
            float x_offset = SIGNED_FLOOR_DIVISION(Dm.num(p_x), ICON_SIZE_X);
            Dm.append(ref x, x_offset);
            pixel_x = Dm.num(p_x) - x_offset * ICON_SIZE_X;
        }
        if (p_y != null)
        {
            float y_offset = SIGNED_FLOOR_DIVISION(Dm.num(p_y), ICON_SIZE_Y);
            Dm.append(ref y, y_offset);
            pixel_y = Dm.num(p_y) - y_offset * ICON_SIZE_Y;
        }
    }

    // tg: code/datums/position_point_vector.dm:106
    public virtual void increment(object p_x = null, object p_y = null)
    {
        float x_offset = SIGNED_FLOOR_DIVISION(Dm.num(p_x), ICON_SIZE_X);
        Dm.append(ref x, x_offset);
        Dm.append(ref pixel_x, Dm.num(p_x) - x_offset * ICON_SIZE_X);
        float y_offset = SIGNED_FLOOR_DIVISION(Dm.num(p_y), ICON_SIZE_Y);
        Dm.append(ref y, y_offset);
        Dm.append(ref pixel_y, Dm.num(p_y) - y_offset * ICON_SIZE_Y);
    }

    // tg: code/datums/position_point_vector.dm:114
    public virtual object debug_out()
    {
        turf T = return_turf() as turf;
        return Dm.text("[] aX [] aY [] aZ [] pX [] pY [] mX [] mY [] mZ []", text_ref(this), x, y, z, pixel_x, pixel_y, T.x, T.y, T.z);
    }

    // tg: code/datums/position_point_vector.dm:118
    public virtual async DmTask move_atom_to_src(atom_movable AM = null)
    {
        await AM.forceMove(return_turf() as atom);
        AM.pixel_x = Dm.num(pixel_x);
        AM.pixel_y = Dm.num(pixel_y);
    }

    // tg: code/datums/position_point_vector.dm:123
    public virtual object return_turf()
    {
        return locate(Dm.num(x) + SIGNED_FLOOR_DIVISION(Dm.num(pixel_x), ICON_SIZE_X), Dm.num(y) + SIGNED_FLOOR_DIVISION(Dm.num(pixel_y), ICON_SIZE_Y), Dm.num(z));
    }

    // tg: code/datums/position_point_vector.dm:126
    public virtual object return_coordinates() //[turf_x, turf_y, z]
    {
        return Dm.list(Dm.num(x) + SIGNED_FLOOR_DIVISION(Dm.num(pixel_x), ICON_SIZE_X), Dm.num(y) + SIGNED_FLOOR_DIVISION(Dm.num(pixel_y), ICON_SIZE_Y), z);
    }

    // tg: code/datums/position_point_vector.dm:129
    public virtual object return_position()
    {
        return Dm.@new<datum_position>(this);
    }

    // tg: code/datums/position_point_vector.dm:132
    public virtual object return_px()
    {
        return Dm.num(x) * ICON_SIZE_X + Dm.num(pixel_x);
    }

    // tg: code/datums/position_point_vector.dm:135
    public virtual object return_py()
    {
        return Dm.num(y) * ICON_SIZE_Y + Dm.num(pixel_y);
    }
}

// tg: code/datums/position_point_vector.dm:138
public partial class datum_vector : datum
{
    // tg: code/datums/position_point_vector.dm:147
    [DmParams("new_magnitude", "new_angle")]
    public override async DmTask New(params object[] __extra)
    {
        object new_magnitude = Dm.arg(__extra, 0);
        object new_angle = Dm.arg(__extra, 1);
        await base.New(__extra);
        initialize_trajectory(new_magnitude, new_angle);
    }

    // tg: code/datums/position_point_vector.dm:151
    public virtual void initialize_trajectory(object new_magnitude = null, object new_angle = null)
    {
        if (new_magnitude != null)
            magnitude = Dm.num(new_magnitude);
        set_angle(new_angle);
    }

    /// Calculations use "byond angle" where north is 0 instead of 90, and south is 180 instead of 270.
    // tg: code/datums/position_point_vector.dm:157
    public virtual void set_angle(object new_angle = null)
    {
        if (angle == null)
            return;
        angle = Dm.num_or_null(new_angle);
        update_offsets();
    }

    // tg: code/datums/position_point_vector.dm:163
    public virtual void update_offsets()
    {
        pixel_x = sin(Dm.num(angle));
        pixel_y = cos(Dm.num(angle));
        total_x = Dm.num(pixel_x) * magnitude;
        total_y = Dm.num(pixel_y) * magnitude;
    }

    // tg: code/datums/position_point_vector.dm:169
    public virtual void set_speed(object new_magnitude = null)
    {
        if (new_magnitude == null || Dm.eq(magnitude, new_magnitude))
            return;
        magnitude = Dm.num(new_magnitude);
        total_x = Dm.num(pixel_x) * magnitude;
        total_y = Dm.num(pixel_y) * magnitude;
    }
}
