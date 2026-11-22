// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/countdown.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the M7 supermatter slice (G5, D163): the countdown base and the supermatter's and the anomalies' countdowns (the
// ghosts' readout of a crystal's integrity and an anomaly's time left; the ghosts see it, D151). The syndicate bomb's, the nuke's,
// the transformer's and the doomsday device's stay inert with their users; the kudzu's flower bud's came with the space vines
// (gaps E4b), the hourglass's with the hourglass (gaps TY1). The var blocks are generated (D111).

// tg: code/game/objects/effects/countdown.dm:1
public partial class obj_effect_countdown : obj_effect
{
    // tg: code/game/objects/effects/countdown.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(attach(loc));
        return dot;
    }

    // tg: code/game/objects/effects/countdown.dm:21
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("This countdown is displaying: [].", displayed_text));
        return dot;
    }

    // tg: code/game/objects/effects/countdown.dm:25
    public virtual async DmTask attach(atom A = null)
    {
        attached_to = A;
        turf loc_turf = get_turf(A);
        if (!Dm.truthy(loc_turf))
            RegisterSignal(attached_to, COMSIG_MOVABLE_MOVED, nameof(retry_attach), TRUE);
        else
            await forceMove(loc_turf);
    }

    // tg: code/game/objects/effects/countdown.dm:33
    [SignalHandler]
    public virtual void retry_attach()
    {
        turf loc_turf = get_turf(attached_to);
        if (!Dm.truthy(loc_turf))
            return;
        Waiting.NoSleep(forceMove(loc_turf));
        UnregisterSignal(attached_to, COMSIG_MOVABLE_MOVED);
    }

    // tg: code/game/objects/effects/countdown.dm:42
    public virtual void start()
    {
        if (!Dm.truthy(started))
        {
            START_PROCESSING(SSfastprocess, this);
            started = TRUE;
        }
    }

    // tg: code/game/objects/effects/countdown.dm:47
    public virtual void stop()
    {
        if (Dm.truthy(started))
        {
            maptext = null;
            STOP_PROCESSING(SSfastprocess, this);
            started = FALSE;
        }
    }

    // tg: code/game/objects/effects/countdown.dm:53
    public virtual object get_value()
    {
        // Get the value from our atom
        return null;
    }

    // tg: code/game/objects/effects/countdown.dm:57  (set waitfor = FALSE, inherited from /datum/process: the wrapper of
    // waiting-model.md 5.4)
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(), null);
    }

    private async DmTask<object> process__body()
    {
        if (!Dm.truthy(attached_to) || QDELETED(attached_to))
            qdel(this);
        await forceMove(get_turf(attached_to));
        object new_val = get_value();
        if (Dm.eq(new_val, displayed_text))
            return null;
        displayed_text = new_val;

        if (Dm.truthy(displayed_text))
            maptext = MAPTEXT(Dm.text("[]", displayed_text));
        else
            maptext = null;
        return null;
    }

    // tg: code/game/objects/effects/countdown.dm:71
    public override object Destroy(float? force = null)
    {
        object dot = null;
        attached_to = null;
        STOP_PROCESSING(SSfastprocess, this);
        dot = base.Destroy(force);
        return dot;
    }

    // tg: code/game/objects/effects/countdown.dm:76
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }

    // tg: code/game/objects/effects/countdown.dm:79
    public override async DmTask<object> singularity_act()
    {
        return null;
    }
}

// tg: code/game/objects/effects/countdown.dm:82  (the syndicate campaign G2, D316)
public partial class obj_effect_countdown_syndicatebomb : obj_effect_countdown
{
    // tg: code/game/objects/effects/countdown.dm:85
    public override object get_value()
    {
        obj_machinery_syndicatebomb S = attached_to as obj_machinery_syndicatebomb;
        if (S == null)
            return null;
        else if (Dm.truthy(S.active))
            return S.seconds_remaining();
        return null;
    }
}

// tg: code/game/objects/effects/countdown.dm:92  (syndicate N1a, D316)
public partial class obj_effect_countdown_nuclearbomb : obj_effect_countdown
{
    // tg: code/game/objects/effects/countdown.dm:96
    public override object get_value()
    {
        var N = attached_to as obj_machinery_nuclearbomb;
        if (N == null)
            return null;
        else if (Dm.truthy(N.timing))
            return round(Dm.num(N.get_time_left()), 1);
        return null;
    }
}

// tg: code/game/objects/effects/countdown.dm:104
public partial class obj_effect_countdown_supermatter : obj_effect_countdown
{
    // tg: code/game/objects/effects/countdown.dm:108
    public override async DmTask attach(atom A = null)
    {
        await base.attach(A);
        if (A is obj_machinery_power_supermatter_crystal_shard)
            pixel_y = -12;
    }

    // tg: code/game/objects/effects/countdown.dm:113
    public override object get_value()
    {
        obj_machinery_power_supermatter_crystal S = attached_to as obj_machinery_power_supermatter_crystal;
        if (!(S is obj_machinery_power_supermatter_crystal))
            return null;
        return Dm.text("<div align='center' valign='bottom' style='position:relative; top:0px; left:0px'>[]%</div>", round(Dm.num(S.get_integrity_percent())));
    }
}

// tg: code/game/objects/effects/countdown.dm:140
public partial class obj_effect_countdown_anomaly : obj_effect_countdown
{
    // tg: code/game/objects/effects/countdown.dm:143
    public override object get_value()
    {
        obj_effect_anomaly A = attached_to as obj_effect_anomaly;
        if (!(A is obj_effect_anomaly))
            return null;
        else if (Dm.truthy(A.immortal)) //we can't die, why are we still here? just to suffer?
            stop();
        else
        {
            float time_left = max(0, (Dm.num(A.death_time) - world.time) / 10);
            return round(time_left);
        }
        return null;
    }
}

// tg: code/game/objects/effects/countdown.dm:119 (silicons SI8, D326)
public partial class obj_effect_countdown_transformer : obj_effect_countdown
{
    // tg: code/game/objects/effects/countdown.dm:123
    public override object get_value()
    {
        obj_machinery_transformer T = attached_to as obj_machinery_transformer;
        if (T == null)
            return null;
        else if (Dm.truthy(T.cooldown))
        {
            float seconds_left = max(0, (Dm.num(T.cooldown_timer) - world.time) / 10);
            return Dm.text("[]", round(seconds_left));
        }
        return null;
    }
}

// tg: code/game/objects/effects/countdown.dm:131 (silicons SI8, D326)
public partial class obj_effect_countdown_doomsday : obj_effect_countdown
{
    // tg: code/game/objects/effects/countdown.dm:134
    public override object get_value()
    {
        obj_machinery_doomsday_device DD = attached_to as obj_machinery_doomsday_device;
        if (DD == null)
            return null;
        else if (Dm.truthy(DD.timing))
            return Dm.text("<div align='center' valign='middle' style='position:relative; top:0px; left:0px'>[]</div>", DD.seconds_remaining());
        return null;
    }
}

// tg: code/game/objects/effects/countdown.dm:165 (gaps E4b, D327)
public partial class obj_effect_countdown_flower_bud : obj_effect_countdown
{
    // tg: code/game/objects/effects/countdown.dm:168
    public override object get_value()
    {
        obj_structure_alien_resin_flower_bud bud = attached_to as obj_structure_alien_resin_flower_bud;
        if (bud == null)
            return null;
        if (!Dm.truthy(bud.finish_time))
            return -1;
        float time_left = max(0, (Dm.num(bud.finish_time) - world.time) / 10);
        return time_left;
    }
}

// tg: code/game/objects/effects/countdown.dm:154 (gaps TY1, D333)
public partial class obj_effect_countdown_hourglass : obj_effect_countdown
{
    // tg: code/game/objects/effects/countdown.dm:157
    public override object get_value()
    {
        obj_item_hourglass H = attached_to as obj_item_hourglass;
        if (!(H is obj_item_hourglass))
            return null;
        else
        {
            float time_left = max(0, (Dm.num(H.finish_time) - world.time) / 10);
            return round(time_left);
        }
    }
}
