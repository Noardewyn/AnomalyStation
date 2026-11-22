// Translated from tgstation (snapshot 1d99bd7f): code/modules/assembly/timer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the suicide (gaps AS1, D333): the timer assembly counting down on SSobj, its pulse with the triple beep at
// its end, the repeat, its lights (one to three by the time left) and its holder overlay, its window ("Timer"). The suicide
// (suicide_act, manual_suicide) is a pair of docs/ai/specs/live-partial.txt rows (the suicide verb, as the signaler's). The beep
// line's chat icon (icon2html) is left out, as at the other icon2html sites (the chat icons, not-translated.md section 76).
// The var block is generated (D111).

// tg: code/modules/assembly/timer.dm:1
public partial class obj_item_assembly_timer : obj_item_assembly
{
    // tg: code/modules/assembly/timer.dm:16  suicide_act and :22 manual_suicide: live-partial rows (the suicide verb)

    // tg: code/modules/assembly/timer.dm:27
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/assembly/timer.dm:31
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        object dot = base.Destroy(force);
        return dot;
    }

    // tg: code/modules/assembly/timer.dm:35
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("The timer is [].", Dm.truthy(timing) ? I18n.T("counting down from []", time) : I18n.T("set for [] seconds", time))));
        return dot;
    }

    // tg: code/modules/assembly/timer.dm:39
    public override async DmTask<object> activate(mob activator = null)
    {
        if (!Dm.truthy(await base.activate(activator)))
            return FALSE; //Cooldown check
        timing = Dm.num(!Dm.truthy(timing));
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/assembly/timer.dm:46
    public override async DmTask<object> toggle_secure()
    {
        secured = Dm.num(!Dm.truthy(secured));
        if (Dm.truthy(secured))
            START_PROCESSING(SSobj, this);
        else
        {
            timing = FALSE;
            STOP_PROCESSING(SSobj, this);
        }
        update_appearance();
        return secured;
    }

    // tg: code/modules/assembly/timer.dm:56
    public virtual async DmTask timer_end()
    {
        if (Dm.truthy(secured) && next_activate <= world.time)
        {
            await pulse();
            // [icon2html(src, hearers(src))]: the chat icons (section 76)
            audible_message(span_infoplain(I18n.T("*beep* *beep* *beep*")), null, hearing_range);
            foreach (mob hearing_mob in Dm.each<mob>(get_hearers_in_view(hearing_range, this)))
                hearing_mob.playsound_local(get_turf(this), Dm.resource("sound/machines/beep/triple_beep.ogg"), ASSEMBLY_BEEP_VOLUME, TRUE);
        }
        if (Dm.truthy(loop))
            timing = TRUE;
        update_appearance();
    }

    // tg: code/modules/assembly/timer.dm:66
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(seconds_per_tick), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body(object seconds_per_tick)
    {
        if (!Dm.truthy(timing))
            return null;
        time -= Dm.num(seconds_per_tick);
        if (time == 9 || time == 19 || time == 29)
            update_appearance();

        if (time <= 0)
        {
            timing = FALSE;
            await timer_end();
            time = saved_time;
        }
        return null;
    }

    // tg: code/modules/assembly/timer.dm:78
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        holder?.update_appearance();
        return dot;
    }

    // tg: code/modules/assembly/timer.dm:82
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        attached_overlays = Dm.list();
        if (!Dm.truthy(timing))
            return dot;

        Dm.append(ref attached_overlays, "timer_timing");
        float lights = clamp(ceil(Dm.num(time) / 10), 1, 3);
        for (float i = 1; i <= lights; i++)
        {
            mutable_appearance timer_light = mutable_appearance(icon, "timer_light", layer, this) as mutable_appearance;
            timer_light.pixel_w = (i - 1) * 2;
            Dm.append(ref dot, timer_light);
        }
        return dot;
    }

    // tg: code/modules/assembly/timer.dm:94
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (Dm.truthy(is_secured(user)))
            return await base.ui_status(user, state);
        return UI_CLOSE;
    }

    // tg: code/modules/assembly/timer.dm:99
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Timer", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/assembly/timer.dm:105
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["seconds"] = round(Dm.num(time) % 60);
        data["minutes"] = round((Dm.num(time) - Dm.num(data["seconds"])) / 60);
        data["timing"] = timing;
        data["loop"] = loop;
        return data;
    }

    // tg: code/modules/assembly/timer.dm:113
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        object sw__1 = action;
        if (Dm.eq(sw__1, "time"))
        {
            timing = Dm.num(!Dm.truthy(timing));
            if (Dm.truthy(timing) && istype(holder, typeof(obj_item_transfer_valve)))
                log_bomber(usr, "activated a", this, Dm.text("attachment on []", holder));
            update_appearance();
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "repeat"))
        {
            loop = Dm.num(!Dm.truthy(loop));
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "input"))
        {
            object value = text2num(@params["adjust"]);
            if (Dm.truthy(value))
            {
                value = round(Dm.num(time) + Dm.num(value));
                time = clamp(Dm.num(value), 1, 600);
                saved_time = Dm.num(time);
                dot = TRUE;
            }
        }
        return dot;
    }
}
