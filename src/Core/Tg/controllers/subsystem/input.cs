// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/input.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M1 P1, the client layer), but stat_entry (:96: stat panels are not translated, translation-rules.md 13,
// so the override is not declared). /atom/_Click came with M5 slice 9 (the click client/Click queues to SSinput when the tick
// is over VERB_HIGH_PRIORITY_QUEUE_THRESHOLD: the M5 demo's clicks during SSair's charged work reached it). The skin macros it sets (set_macros) are deferred there.

// tg: code/controllers/subsystem/input.dm:1  VERB_MANAGER_SUBSYSTEM_DEF(input)
[DmPath("/datum/controller/subsystem/verb_manager/input")]
public partial class datum_controller_subsystem_verb_manager_input : datum_controller_subsystem_verb_manager
{
    // tg: code/controllers/subsystem/input.dm:2
    public datum_controller_subsystem_verb_manager_input()
    {
        name = "Input";
        // tg: code/controllers/subsystem/input.dm:3
        init_stage = INITSTAGE_EARLY;
        // tg: code/controllers/subsystem/input.dm:4
        ss_flags = SS_TICKER;
        // tg: code/controllers/subsystem/input.dm:5
        priority = FIRE_PRIORITY_INPUT;
        // tg: code/controllers/subsystem/input.dm:6
        runlevels = Dm.bitor(RUNLEVELS_DEFAULT, RUNLEVEL_LOBBY);

        // tg: code/controllers/subsystem/input.dm:8
        use_default_stats = FALSE;
    }

    // The New() that VERB_MANAGER_SUBSYSTEM_DEF(input) defines (__DEFINES/MC.dm:131-135), with NEW_SS_GLOBAL(SSinput)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src). Its fire() {..()} (MC.dm:136) is fire__d0 below.
    // tg: code/controllers/subsystem/input.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSinput != this)
        {
            if (SSinput is datum_controller_subsystem_verb_manager_input)
            {
                Recover();
                qdel(SSinput);
            }
            SSinput = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/input.dm:10
    public DmList macro_set;

    ///running average of how many clicks inputted by a player the server processes every second. used for the subsystem stat entry
    // tg: code/controllers/subsystem/input.dm:13
    public float clicks_per_second = 0;
    ///count of how many clicks onto atoms have elapsed before being cleared by fire(). used to average with clicks_per_second.
    // tg: code/controllers/subsystem/input.dm:15
    public float current_clicks = 0;
    ///acts like clicks_per_second but only counts the clicks actually processed by SSinput itself while clicks_per_second counts all clicks
    // tg: code/controllers/subsystem/input.dm:17
    public float delayed_clicks_per_second = 0;
    ///running average of how many movement iterations from player input the server processes every second. used for the subsystem stat entry
    // tg: code/controllers/subsystem/input.dm:19
    public float movements_per_second = 0;
    ///running average of the amount of real time clicks take to truly execute after the command is originally sent to the server.
    ///if a click isn't delayed at all then it counts as 0 deciseconds.
    // tg: code/controllers/subsystem/input.dm:22
    public float average_click_delay = 0;

    // tg: code/controllers/subsystem/input.dm:24
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        setup_default_macro_sets();

        initialized = TRUE;

        refresh_client_macro_sets();

        return SS_INIT_SUCCESS;
    }

    // This is for when macro sets are eventually datumized
    // tg: code/controllers/subsystem/input.dm:34
    public virtual void setup_default_macro_sets()
    {
        macro_set = Dm.list(
            Dm.kv("Any", "\"KeyDown [[*]] [[map.mouse-pos]] [[map.size]]\""),
            Dm.kv("Any+UP", "\"KeyUp [[*]] [[map.mouse-pos]] [[map.size]]\""),
            Dm.kv("Back", Dm.text("\\\".output output_browser.browseroutput:update []\\\\n.winset \\\\\\\"input.text=\\\\\\\"\\\\\\\"\\\\\\\"\\\"", TGUI_PANEL_CLEAR_MESSAGE)),
            Dm.kv("Tab", Dm.text("\\\".output output_browser.browseroutput:update []\\\\n.winset \\\\\\\"output_browser.browseroutput.focus=true?map.focus=true:output_browser.browseroutput.focus=true\\\\\\\"\\\"", TGUI_PANEL_FOCUS_MESSAGE)),
            Dm.kv("Escape", Dm.text("\\\".winset \\\\\\\"[].is-visible=true?[].is-visible=false:[].is-visible=true\\\\\\\"\\\\n.output []:update []\\\\nReset-Held-Keys\\\"", SKIN_ESCAPE_MENU, SKIN_ESCAPE_MENU, SKIN_ESCAPE_MENU, SKIN_ESCAPE_MENU, ESCAPE_MENU_TOGGLE_MESSAGE)));
    }

    // Badmins just wanna have fun ♪
    // tg: code/controllers/subsystem/input.dm:44
    public virtual void refresh_client_macro_sets()
    {
        DmList clients = GLOB.clients;
        for (float i = 1, i__end = clients.len; i <= i__end; i++)
        {
            client user = clients[i] as client;
            user.set_macros();
        }
    }

    // tg: code/controllers/subsystem/input.dm:50
    [DmParams("incoming_callback", "control")]
    public override object can_queue_verb(datum_callback_verb_callback incoming_callback = null, params object[] __extra)
    {
        object control = Dm.arg(__extra, 0);
        //make sure the incoming verb is actually something we specifically want to handle
        if (!Dm.eq(control, SKIN_MAPWINDOW_MAP))
            return FALSE;

        if (average_click_delay > MAXIMUM_CLICK_LATENCY || !Dm.truthy(base.can_queue_verb(incoming_callback, Dm.forward(__extra, control))))
        {
            current_clicks++;
            average_click_delay = MC_AVG_FAST_UP_SLOW_DOWN(average_click_delay, 0);
            return FALSE;
        }

        return TRUE;
    }
}

// tg: code/controllers/subsystem/input.dm:63 (interleaved types, rules 1.9)
public partial class atom : datum
{
    ///stupid workaround for byond not recognizing the /atom/Click typepath for the queued click callbacks
    // tg: code/controllers/subsystem/input.dm:63
    public virtual async DmTask _Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(usr))
            await Click(location, control, @params);
    }
}

// tg: code/controllers/subsystem/input.dm:67 (interleaved types, rules 1.9)
public partial class datum_controller_subsystem_verb_manager_input : datum_controller_subsystem_verb_manager
{
    // The fire() {..() /*just so it shows up on the profiler*/} that VERB_MANAGER_SUBSYSTEM_DEF(input) defines
    // (__DEFINES/MC.dm:136): the earlier of two definitions of fire on this type (rules 7.5).
    // tg: code/controllers/subsystem/input.dm:1
    public async DmTask fire__d0(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra);
    }

    // tg: code/controllers/subsystem/input.dm:67
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await fire__d0(resumed, __extra);

        float moves_this_run = 0;
        foreach (mob user in Dm.each<mob>(GLOB.keyloop_list))
            moves_this_run += Dm.num(user.focus?.keyLoop(user.client));//only increments if a player moves due to their own input

        movements_per_second = MC_AVG_SECONDS(movements_per_second, moves_this_run, wait * world.tick_lag);
    }

    // tg: code/controllers/subsystem/input.dm:76
    public override void run_verb_queue()
    {
        float deferred_clicks_this_run = 0; //acts like current_clicks but doesn't count clicks that don't get processed by SSinput

        // Rules 6.5: a typed `as anything` loop var that the body type-tests is object.
        foreach (object queued_click in Dm.each(verb_queue))
        {
            if (!(queued_click is datum_callback_verb_callback))
            {
                stack_trace("non /datum/callback/verb_callback instance inside SSinput's verb_queue!", "code/controllers/subsystem/input.dm", 81);
                continue;
            }

            average_click_delay = MC_AVG_FAST_UP_SLOW_DOWN(average_click_delay, TICKS2DS(DS2TICKS(world.time) - ((datum_callback_verb_callback)queued_click).creation_time));
            ((datum_callback_verb_callback)queued_click).InvokeAsync();

            current_clicks++;
            deferred_clicks_this_run++;
        }

        verb_queue.Cut(); //is ran all the way through every run, no exceptions

        clicks_per_second = MC_AVG_SECONDS(clicks_per_second, current_clicks, wait * SECONDS);
        delayed_clicks_per_second = MC_AVG_SECONDS(delayed_clicks_per_second, deferred_clicks_this_run, wait * SECONDS);
        current_clicks = 0;
    }
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_verb_manager_input SSinput => ref TgState.Current.SSinput;
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/input.dm:1  VERB_MANAGER_SUBSYSTEM_DEF(input): GLOBAL_REAL(SSinput, /datum/controller/subsystem/verb_manager/input)
    internal datum_controller_subsystem_verb_manager_input SSinput;
}
