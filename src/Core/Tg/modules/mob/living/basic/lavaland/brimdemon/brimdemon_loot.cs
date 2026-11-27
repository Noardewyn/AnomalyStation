// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/brimdemon/brimdemon_loot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Ashwalker ore sensor crafted from brimdemon ash
// tg: code/modules/mob/living/basic/lavaland/brimdemon/brimdemon_loot.dm:14
public partial class obj_item_ore_sensor : obj_item
{
    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimdemon_loot.dm:25
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_EARS)))
            START_PROCESSING(SSobj, this);
        else
            STOP_PROCESSING(SSobj, this);
    }

    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimdemon_loot.dm:32
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        STOP_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/brimdemon/brimdemon_loot.dm:36
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!(ore_sensing_cooldown <= world.time)) // COOLDOWN_FINISHED(src, ore_sensing_cooldown)
            return null;
        ore_sensing_cooldown = world.time + cooldown; // COOLDOWN_START(src, ore_sensing_cooldown, cooldown)
        mineral_scan_pulse(get_turf(this) as turf, range, this);
        return null;
    }
}
