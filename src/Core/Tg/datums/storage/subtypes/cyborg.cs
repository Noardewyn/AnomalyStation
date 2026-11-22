// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/cyborg.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the cyborg's internal module storage, whole. The var block is generated (D111).

// tg: code/datums/storage/subtypes/cyborg.dm:1
public partial class datum_storage_cyborg_internal_storage : datum_storage
{
    // tg: code/datums/storage/subtypes/cyborg.dm:11
    public override object can_insert(obj_item to_insert = null, mob user = null, float? messages = null, float? force = null)
    {
        mob_living_silicon_robot robot_user = user as mob_living_silicon_robot;
        return Dm.@in(to_insert, robot_user.model.modules) ? TRUE : FALSE;
    }

    // tg: code/datums/storage/subtypes/cyborg.dm:14
    public override object attempt_insert(obj_item to_insert = null, mob user = null, float? @override = null, float? force = null, float? messages = null)
    {
        mob_living_silicon_robot robot_user = user as mob_living_silicon_robot;
        Waiting.NoSleep(robot_user.deactivate_module(to_insert));
        return null;
    }

    /**
     * Cyborg internal storage orienting
     * We're using the model's total amount of modules as reference for how many slots we fill,
     * otherwise having enough items in hand and only 1 item in a row would mean, as we're a static inventory,
     * the rows won't fill for all items in the UI.
     * We also don't give an additional row if all slots are filled because as a static inventory borgs don't need extra space
     * to put items in, you can click on the slot you took it out from, or use the dedicated "store" button.
     */
    // tg: code/datums/storage/subtypes/cyborg.dm:25
    public override void orient_storage()
    {
        obj_item_robot_model model = real_location as obj_item_robot_model;

        float adjusted_contents = Dm.length(model.modules);
        DmList numbered_contents = null;
        if (Dm.truthy(numerical_stacking))
        {
            numbered_contents = process_numerical_display() as DmList;
            adjusted_contents = Dm.length(numbered_contents);
        }

        float columns = clamp(max_slots, 1, screen_max_columns);
        float rows = clamp(ceil(adjusted_contents / columns), 1, screen_max_rows);

        foreach (mob ui_user in Dm.each_cast<mob>(storage_interfaces))
        {
            if (storage_interfaces[ui_user] == null)
                continue;
            ((datum_storage_interface)storage_interfaces[ui_user]).update_position(
                screen_start_x,
                screen_pixel_x,
                screen_start_y,
                screen_pixel_y,
                columns,
                rows,
                ui_user,
                real_location,
                numbered_contents
            );
        }
    }
}
