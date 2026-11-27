// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/mobs/adjust_vision.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/datums/actions/mobs/adjust_vision.dm:7
public partial class datum_action_adjust_vision : datum_action
{
    // tg: code/datums/actions/mobs/adjust_vision.dm:2  #define VISION_ACTION_LIGHT_OFF 0 (file-local, #undef at :71)
    private const int VISION_ACTION_LIGHT_OFF = 0;
    // tg: code/datums/actions/mobs/adjust_vision.dm:3  #define VISION_ACTION_LIGHT_LOW 1 (file-local, #undef at :72)
    private const int VISION_ACTION_LIGHT_LOW = 1;
    // tg: code/datums/actions/mobs/adjust_vision.dm:4  #define VISION_ACTION_LIGHT_MID 2 (file-local, #undef at :73)
    private const int VISION_ACTION_LIGHT_MID = 2;
    // tg: code/datums/actions/mobs/adjust_vision.dm:5  #define VISION_ACTION_LIGHT_HIG 3 (file-local, #undef at :74)
    private const int VISION_ACTION_LIGHT_HIG = 3;

    // tg: code/datums/actions/mobs/adjust_vision.dm:23
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        set_light_level(VISION_ACTION_LIGHT_LOW);
        RegisterSignal(grant_to, COMSIG_MOB_UPDATE_SIGHT, nameof(on_update_sight));
        return dot;
    }

    // tg: code/datums/actions/mobs/adjust_vision.dm:28
    public override async DmTask Remove(mob remove_from = null)
    {
        set_light_level(VISION_ACTION_LIGHT_OFF);
        UnregisterSignal(remove_from, COMSIG_MOB_UPDATE_SIGHT);
        await base.Remove(remove_from);
    }

    // tg: code/datums/actions/mobs/adjust_vision.dm:33
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;

        object sw__1 = light_level;
        if (Dm.eq(sw__1, VISION_ACTION_LIGHT_OFF))
            set_light_level(VISION_ACTION_LIGHT_LOW);
        else if (Dm.eq(sw__1, VISION_ACTION_LIGHT_LOW))
            set_light_level(VISION_ACTION_LIGHT_MID);
        else if (Dm.eq(sw__1, VISION_ACTION_LIGHT_MID))
            set_light_level(VISION_ACTION_LIGHT_HIG);
        else
            set_light_level(VISION_ACTION_LIGHT_OFF);
        return dot;
    }

    // tg: code/datums/actions/mobs/adjust_vision.dm:48
    public virtual void set_light_level(object new_level = null)
    {
        light_level = Dm.num(new_level);
        owner.update_sight();
    }

    // tg: code/datums/actions/mobs/adjust_vision.dm:52
    [SignalHandler]
    public virtual void on_update_sight(datum source = null)
    {
        DmList color_from = null;
        object sw__1 = light_level;
        if (Dm.eq(sw__1, VISION_ACTION_LIGHT_LOW))
            color_from = low_light_cutoff;
        else if (Dm.eq(sw__1, VISION_ACTION_LIGHT_MID))
            color_from = medium_light_cutoff;
        else if (Dm.eq(sw__1, VISION_ACTION_LIGHT_HIG))
            color_from = high_light_cutoff;
        else // just in case
            color_from = Dm.list(0, 0, 0);
        owner.lighting_color_cutoffs = blend_cutoff_colors(owner.lighting_color_cutoffs, color_from.Copy()) as DmList;
    }
}
