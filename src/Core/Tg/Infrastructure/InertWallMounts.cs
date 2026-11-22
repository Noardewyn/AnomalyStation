namespace Anomaly.Core.Tg;

// The mount-only subset of the listed tg Initialize procs. Their device logic remains inert (D112).
// Only inert families stay listed: a live family's own Initialize mounts it (find_and_mount_on_atom), as tg's does.
internal static class InertWallMounts
{
    internal static bool Applies(obj value) => value is obj_machinery_wall_healer or obj_machinery_computer_security_telescreen
        or obj_machinery_digital_clock;
}
