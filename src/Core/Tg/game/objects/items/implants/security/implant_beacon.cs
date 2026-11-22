// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/security/implant_beacon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The beacon implant: its holder is a teleporter beacon (the teleporter's and the prisoner
// management console's), shown on the console until ten minutes after their death, with whether their tile is safe to land on.
// The var block is generated (D111).

///Essentially, just turns the implantee into a teleport beacon.
// tg: code/game/objects/items/implants/security/implant_beacon.dm:2
public partial class obj_item_implant_beacon : obj_item_implant
{
    // tg: code/game/objects/items/implants/security/implant_beacon.dm:20
    public override object is_shown_on_console(obj_machinery_computer_prisoner_management console = null)
    {
        if (imp_in.stat == DEAD && imp_in.timeofdeath + lifespan_postmortem < world.time)
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items/implants/security/implant_beacon.dm:25
    public override object get_management_console_data()
    {
        DmList info_shown = base.get_management_console_data() as DmList;

        area destination_area = get_area(imp_in) as area;
        if (destination_area == null || !Dm.truthy(check_teleport_valid(imp_in, usr)))
            info_shown["Status"] = I18n.T("Implant carrier teleport signal cannot be reached!");
        else
        {
            turf turf_to_check = get_turf(imp_in);
            info_shown["Status"] = I18n.T("Implant carrier is in []", Dm.truthy(is_safe_turf(turf_to_check, dense_atoms: TRUE)) ? I18n.T("a safe environment.") : I18n.T("a hazardous environment!"));
        }

        return info_shown;
    }
}
