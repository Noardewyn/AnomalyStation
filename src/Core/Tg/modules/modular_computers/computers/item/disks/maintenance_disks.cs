// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/computers/item/disks/maintenance_disks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file's one proc, the theme disk's Initialize (one random maintenance theme program); the other maintenance
// disks are var-only (their programs set). The var blocks are generated (D111).

// tg: code/modules/modular_computers/computers/item/disks/maintenance_disks.dm:25
public partial class obj_item_disk_computer_maintenance_theme : obj_item_disk_computer_maintenance
{
    // tg: code/modules/modular_computers/computers/item/disks/maintenance_disks.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        starting_programs = Dm.list(pick(subtypesof(typeof(datum_computer_file_program_maintenance_theme)) as DmList));
        return base.Initialize(mapload, __extra);
    }
}
