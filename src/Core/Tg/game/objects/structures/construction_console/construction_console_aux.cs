// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/construction_console/construction_console_aux.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///base consturctino console subtype for the mining aux base
// tg: code/game/objects/structures/construction_console/construction_console_aux.dm:3
public partial class obj_machinery_computer_camera_advanced_base_construction_aux : obj_machinery_computer_camera_advanced_base_construction
{
    // tg: code/game/objects/structures/construction_console/construction_console_aux.dm:9
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        internal_rcd = Dm.@new<obj_item_construction_rcd_internal>(this);
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/structures/construction_console/construction_console_aux.dm:13
    public override void restock_materials()
    {
        internal_rcd.matter = internal_rcd.max_matter;
        structures["fans"] = 4;
        structures["turrets"] = 4;
    }

    // tg: code/game/objects/structures/construction_console/construction_console_aux.dm:18
    public override void populate_actions_list()
    {
        Dm.append(ref actions, Dm.@new<datum_action_innate_construction_configure_mode>(this)); //Action for switching the RCD's build modes
        Dm.append(ref actions, Dm.@new<datum_action_innate_construction_build>(this)); //Action for using the RCD
        Dm.append(ref actions, Dm.@new<datum_action_innate_construction_place_structure_fan>(this)); //Action for spawning fans
        Dm.append(ref actions, Dm.@new<datum_action_innate_construction_place_structure_turret>(this)); //Action for spawning turrets
    }

    // tg: code/game/objects/structures/construction_console/construction_console_aux.dm:24
    public override async DmTask<object> find_spawn_spot()
    {
        //Aux base controller. Where the eyeobj will spawn.
        obj_machinery_computer_auxiliary_base aux_controller = null;
        foreach (obj_machinery_computer_auxiliary_base potential_aux_console in Dm.each<obj_machinery_computer_auxiliary_base>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_computer_auxiliary_base))))
        {
            if (istype(get_area(potential_aux_console), allowed_area))
            {
                aux_controller = potential_aux_console;
                break;
            }
        }
        if (aux_controller == null)
        {
            await say(I18n.T("ERROR: Unable to locate auxiliary base controller!"));
            return null;
        }
        return aux_controller;
    }
}
