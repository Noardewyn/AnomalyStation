// Translated from tgstation (snapshot 1d99bd7f): code/modules/awaymissions/mission_code/Cabin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the firepit (lit by a flame, put out by hand or by water, its light and its two states). The cabin's lumbermill, its
// teleportation rune ladder and its snowy map generator wait for the away mission. The var blocks are generated.

// tg: code/modules/awaymissions/mission_code/Cabin.dm:36
public partial class obj_structure_firepit : obj_structure
{
    // tg: code/modules/awaymissions/mission_code/Cabin.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(toggleFirepit());
        return dot;
    }

    // tg: code/modules/awaymissions/mission_code/Cabin.dm:48
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(active))
        {
            active = FALSE;
            await toggleFirepit();
        }
        return null;
    }

    // tg: code/modules/awaymissions/mission_code/Cabin.dm:53
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(active))
        {
            await tool.fire_act();
            return ITEM_INTERACT_SUCCESS;
        }

        object msg = await tool.ignition_effect(this, user);
        if (!Dm.truthy(msg))
            return NONE;

        visible_message(msg);
        await toggleFirepit();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/awaymissions/mission_code/Cabin.dm:67
    public virtual async DmTask toggleFirepit()
    {
        active = Dm.truthy(active) ? FALSE : TRUE;
        if (Dm.truthy(active))
        {
            await set_light(8);
            icon_state = "firepit-active";
        }
        else
        {
            await set_light(0);
            icon_state = "firepit";
        }
    }

    // tg: code/modules/awaymissions/mission_code/Cabin.dm:76
    public override async DmTask<object> extinguish()
    {
        object dot = await base.extinguish();
        if (Dm.truthy(active))
        {
            active = FALSE;
            await toggleFirepit();
        }
        return dot;
    }

    // tg: code/modules/awaymissions/mission_code/Cabin.dm:82
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        if (!Dm.truthy(active))
        {
            active = TRUE;
            await toggleFirepit();
        }
        return null;
    }
}
