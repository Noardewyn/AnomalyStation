// Translated from tgstation (snapshot 1d99bd7f): code/modules/uplink/uplink_items/job.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the entries' procs (the entries and their categories are the generated var blocks; the job-locked items
// are G4's). The organic capital disturbance virus's spawn_item (force_event of the round events, not ported) is not translated, so it
// stays hidden (DV343; not-translated.md section 131).

// tg: code/modules/uplink/uplink_items/job.dm:265
public partial class datum_uplink_item_role_restricted_clown_bomb : datum_uplink_item_role_restricted
{
    // tg: code/modules/uplink/uplink_items/job.dm:276
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        desc = replacetext(desc, "%MIN_BOMB_TIMER", SYNDIEBOMB_MIN_TIMER_SECONDS);
    }
}

// tg: code/modules/uplink/uplink_items/job.dm:281
public partial class datum_uplink_item_role_restricted_clowncar : datum_uplink_item_role_restricted
{
    // tg: code/modules/uplink/uplink_items/job.dm:293
    public override async DmTask<object> spawn_item_for_generic_use(mob user = null)
    {
        obj_vehicle_sealed_car_clowncar car = await base.spawn_item_for_generic_use(user) as obj_vehicle_sealed_car_clowncar;
        car.enforce_clown_role = FALSE;
        obj_item key = await Dm.new_async(car.key_type, user.loc) as obj_item;
        car.visible_message(span_notice(I18n.T("[] drops out of [] onto the floor.", key, car)));
        return car;
    }
}
