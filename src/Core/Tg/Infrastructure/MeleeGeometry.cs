using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public partial class mob
{
    internal DmList dm_attack_modifiers;
    internal atom dm_attack_target;
    internal long dm_attack_tick;
}

public partial class atom_movable_flick_visual : atom_movable
{
    // A melee effect uses the same replicated 3D line as a shot effect.
    public bool dm_melee;
    public float dm_origin_x, dm_origin_y, dm_origin_h, dm_slope, dm_angle;
}

public static class MeleeGeometry
{
    public static void Set(atom_movable_flick_visual visual, atom_movable attacker, atom target)
    {
        if (visual == null || attacker == null || target == null)
            return;
        double x = Dm.num(attacker.x) * 32, y = Dm.num(attacker.y) * 32;
        if (LocomotionSeam.TryGetState(World.Current, attacker.dm_id, out var position, out _))
            (x, y) = (position.X / 8.0, position.Y / 8.0);
        double dx = Dm.num(target.x) * 32 - x, dy = Dm.num(target.y) * 32 - y;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        double height = ShotGeometry.UnaimedHeightPixels;
        if (attacker is mob user && user.dm_attack_target == target && user.dm_attack_tick == World.Current.Tick
            && ClientSeam.TryReadAim(user.dm_attack_modifiers, out _, out _, out double up, out _, out double pitch))
            height = Math.Clamp(up + Math.Tan(pitch * Math.PI / 180) * distance, 0, ShotGeometry.CeilingPixels);
        visual.dm_melee = true;
        visual.dm_origin_x = (float)x;
        visual.dm_origin_y = (float)y;
        visual.dm_origin_h = ShotGeometry.UnaimedHeightPixels;
        visual.dm_slope = distance > 0.01 ? (float)((height - visual.dm_origin_h) / distance) : 0;
        visual.dm_angle = (float)(Math.Atan2(dx, dy) * 180 / Math.PI);
    }
}
