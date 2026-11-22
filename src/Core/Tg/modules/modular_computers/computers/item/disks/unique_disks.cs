// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/computers/item/disks/unique_disks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate data disk (syndicate G3d, D316: the uplink's SyndEye disk); the black market disk with two to four random programs
// that the verified downloader does not offer (gaps DV2); the contractor's disk holds the contractor uplink (the contractors, after
// the core version).

// tg: code/modules/modular_computers/computers/item/disks/unique_disks.dm:1
public partial class obj_item_disk_computer_syndicate : obj_item_disk_computer
{
    // tg: code/modules/modular_computers/computers/item/disks/unique_disks.dm:7
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_overlay("o_syndicate");
        return dot;
    }
}

// tg: code/modules/modular_computers/computers/item/disks/unique_disks.dm:17
public partial class obj_item_disk_computer_black_market : obj_item_disk_computer
{
    // tg: code/modules/modular_computers/computers/item/disks/unique_disks.dm:20
    public object Initialize__d0(object mapload = null, params object[] __extra) // the earlier definition of two (rules 7.5); the later one is :24
    {
        object dot = base.Initialize(mapload, __extra);
        add_overlay("o_code");
        return dot;
    }

    // tg: code/modules/modular_computers/computers/item/disks/unique_disks.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        icon_state = Dm.text("datadisk[]", rand(0, 10));
        //Populated with programs not found in the verified downloader app or that require access to download (but not to run).
        DmList potential_programs = Dm.list(
            typeof(datum_computer_file_program_arcade_eazy),
            typeof(datum_computer_file_program_radar_lifeline),
            typeof(datum_computer_file_program_radar_custodial_locator),
            typeof(datum_computer_file_program_supermatter_monitor),
            typeof(datum_computer_file_program_newscaster),
            typeof(datum_computer_file_program_secureye),
            typeof(datum_computer_file_program_status));
        Dm.append(ref potential_programs, Dm.sub(subtypesof(typeof(datum_computer_file_program_maintenance)), typeof(datum_computer_file_program_maintenance_theme)));

        float total_programs_size = 0;
        for (float i = 1, i__end = rand(2, 4); i <= i__end; i++)
        {
            Type to_add = pick_n_take(potential_programs) as Type;
            total_programs_size += Dm.num(Dm.initial(to_add, "size"));
            Dm.append(ref starting_programs, to_add);
        }
        ///Make sure the disk has enough space for all the programs
        max_capacity = max(total_programs_size, max_capacity);
        return Initialize__d0(mapload, __extra);
    }
}
