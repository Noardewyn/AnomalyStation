// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/objects/gimmick_disks/sports_disk.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file. The loadouts are var blocks (generated, D111). The gamer's laptop installs only its starting
// programs; it stays inert while its parent, the laptop (modular_computers/computers/item/laptop.dm), is not ported.

// tg: code/modules/bitrunning/objects/gimmick_disks/sports_disk.dm:84
public partial class obj_item_modular_computer_laptop_gamer : obj_item_modular_computer_laptop
{
    // tg: code/modules/bitrunning/objects/gimmick_disks/sports_disk.dm:95
    public override void install_default_programs()
    {
        // Only install starting programs, we don't want the software downloading program from default programs
        foreach (object programs in Dm.each(starting_programs))
        {
            datum_computer_file_program program_type = Dm.@new((Type)programs) as datum_computer_file_program;
            store_file(program_type);
        }
    }
}
