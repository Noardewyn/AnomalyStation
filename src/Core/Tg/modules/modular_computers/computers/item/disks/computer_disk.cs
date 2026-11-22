// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/computers/item/disks/computer_disk.dm, role_disks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/modular_computers/computers/item/disks/computer_disk.dm:2  (gaps J5, D298: the role disks; running their programs is S4's)
public partial class obj_item_disk_computer : obj_item_disk
{
    // tg: code/modules/modular_computers/computers/item/disks/computer_disk.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (object programs in Dm.each(starting_programs))
            add_file(Dm.@new(programs as Type) as datum_computer_file);
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/disks/computer_disk.dm:23
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        foreach (object file in Dm.each(stored_files.Copy()))
            qdel(file as datum);
        stored_files.Cut();
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/disks/computer_disk.dm:27
    public virtual object add_file(datum_computer_file file = null)
    {
        if (Dm.num(file.size) + Dm.num(used_capacity) > Dm.num(max_capacity))
            return FALSE;
        stored_files.Add(file);
        file.disk_host = this;
        used_capacity = Dm.num(used_capacity) + Dm.num(file.size);
        return TRUE;
    }

    // tg: code/modules/modular_computers/computers/item/disks/computer_disk.dm:35
    public virtual object remove_file(datum_computer_file file = null)
    {
        if (!Dm.@in(file, stored_files))
            return FALSE;
        stored_files.Remove(file);
        used_capacity = Dm.num(used_capacity) - Dm.num(file.size);
        qdel(file);
        return TRUE;
    }
}

// tg: code/modules/modular_computers/computers/item/disks/role_disks.dm:5
public partial class obj_item_disk_computer_command : obj_item_disk_computer
{
    // tg: code/modules/modular_computers/computers/item/disks/role_disks.dm:8  var/static/list/datum/computer_file/command_programs
    private static DmList command_programs => Dm.list(
        typeof(datum_computer_file_program_science),
        typeof(datum_computer_file_program_status));

    // tg: code/modules/modular_computers/computers/item/disks/role_disks.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (object programs in Dm.each(command_programs))
            add_file(Dm.@new(programs as Type) as datum_computer_file);
        return dot;
    }
}
