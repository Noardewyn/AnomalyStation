// Translated from tgstation (snapshot 1d99bd7f): code/modules/assembly/signaler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M9 slice D: SSradio's device signals): the remote signaling device, its window ("Signaler"), its frequency and
// code, its sync with another signaler and its cyborg, internal and low-range variants. Left out with a comment: the
// beep line's chat icon (icon2html: the chat icons, as at the other icon2html sites) and the traitor mail's unwrap press (the
// traitor mail, an antagonist's item: its signal never comes); the suicide (suicide_act, manual_suicide) is a pair of
// docs/ai/specs/live-partial.txt rows (the suicide verb, M10). The var blocks are generated (D111).

// tg: code/modules/assembly/signaler.dm:1
public partial class obj_item_assembly_signaler : obj_item_assembly
{
    // tg: code/modules/assembly/signaler.dm:32  suicide_act and :40 manual_suicide: live-partial rows (the suicide verb, M10); suicider
    // stays null until then

    // tg: code/modules/assembly/signaler.dm:53
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_frequency(frequency);
        RegisterSignal(this, COMSIG_ITEM_IN_UNWRAPPED_TRAITOR_MAIL, nameof(on_mail_unwrap));
        return dot;
    }

    // tg: code/modules/assembly/signaler.dm:58
    public override object Destroy(float? force = null)
    {
        SSradio.remove_object(this, frequency);
        suicider = null;
        return base.Destroy(force);
    }

    // tg: code/modules/assembly/signaler.dm:63
    public override async DmTask<object> activate(mob activator = null)
    {
        if (!Dm.truthy(await base.activate(activator)))//cooldown processing
            return FALSE;
        signal();
        return TRUE;
    }

    // tg: code/modules/assembly/signaler.dm:69
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        (holder as atom)?.update_appearance();
        return dot;
    }

    // tg: code/modules/assembly/signaler.dm:73
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (Dm.truthy(is_secured(user)))
            return await base.ui_status(user, state);
        return UI_CLOSE;
    }

    // tg: code/modules/assembly/signaler.dm:78
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Signaler", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/assembly/signaler.dm:84
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["frequency"] = frequency;
        data["cooldown"] = cooldown_length;
        data["code"] = code;
        data["minFrequency"] = MIN_FREE_FREQ;
        data["maxFrequency"] = MAX_FREE_FREQ;
        return data;
    }

    // tg: code/modules/assembly/signaler.dm:93
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        object sw__1 = action;
        if (Dm.eq(sw__1, "signal"))
        {
            if (cooldown_length > 0)
            {
                if (Dm.truthy(TIMER_COOLDOWN_RUNNING(this, COOLDOWN_SIGNALLER_SEND)))
                {
                    balloon_alert(ui.user, I18n.T("recharging!"));
                    return dot;
                }
                // TIMER_COOLDOWN_START(src, COOLDOWN_SIGNALLER_SEND, cooldown_length) (__DEFINES/cooldowns.dm:77, it expands addtimer: inline, rules 11.1 (f))
                LAZYSET(ref cooldowns, COOLDOWN_SIGNALLER_SEND, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), this, COOLDOWN_SIGNALLER_SEND), cooldown_length, file: "code/modules/assembly/signaler.dm", line: 104));
            }
            signal(); // INVOKE_ASYNC: signal() cannot sleep
            balloon_alert(ui.user, I18n.T("signaled"));
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "freq"))
        {
            float new_frequency = sanitize_frequency(unformat_frequency(@params["freq"]), TRUE);
            set_frequency(new_frequency);
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "code"))
        {
            code = text2num(@params["code"]);
            code = Dm.round(Dm.num(code));
            dot = TRUE;
        }
        else if (Dm.eq(sw__1, "reset"))
        {
            if (Dm.eq(@params["reset"], "freq"))
                frequency = Dm.initial(this, (obj_item_assembly_signaler p) => p.frequency);
            else
                code = Dm.initial(this, (obj_item_assembly_signaler p) => p.code);
            dot = TRUE;
        }

        update_appearance();
        return dot;
    }

    // tg: code/modules/assembly/signaler.dm:125
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_assembly_signaler sister_signaler)) // !issignaler(tool)
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (!Dm.truthy(secured) || !Dm.truthy(sister_signaler.secured))
            return await base.item_interaction(user, tool, modifiers, __extra);

        code = sister_signaler.code;
        set_frequency(sister_signaler.frequency);
        to_chat(user, I18n.T("You transfer the frequency and code of \\the [] to \\the []", sister_signaler.name, name));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/assembly/signaler.dm:138
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        if (!Dm.truthy(await can_interact(user)))
            return dot;
        if (!ishuman(user))
            return dot;
        if (Dm.truthy(TIMER_COOLDOWN_RUNNING(this, COOLDOWN_SIGNALLER_SEND)))
        {
            balloon_alert(user, I18n.T("still recharging..."));
            return dot;
        }
        // TIMER_COOLDOWN_START(src, COOLDOWN_SIGNALLER_SEND, 1 SECONDS) (inline, rules 11.1 (f))
        LAZYSET(ref cooldowns, COOLDOWN_SIGNALLER_SEND, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), this, COOLDOWN_SIGNALLER_SEND), 1 * SECONDS, file: "code/modules/assembly/signaler.dm", line: 147));
        signal(); // INVOKE_ASYNC: signal() cannot sleep
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/assembly/signaler.dm:151
    public virtual void signal()
    {
        if (radio_connection == null)
            return;

        string time = time2text(world.realtime, "hh:mm:ss", TIMEZONE_UTC);
        turf T = get_turf(this);

        string logging_data = Dm.text("[] <B>:</B> [] used [] @ location ([],[],[]) <B>:</B> []/[]", time, key_name(usr), this, T.x, T.y, T.z, format_frequency(frequency), code);
        add_to_signaler_investigate_log(logging_data);

        datum_signal signal = Dm.@new<datum_signal>(Dm.list(Dm.kv("code", code)), null, logging_data);
        radio_connection.post_signal(this, signal, range: range);
    }

    // tg: code/modules/assembly/signaler.dm:164  (receive_signal cannot sleep, waiting-model.md 5.4: the suicide and the pulse may, so they run on their own)
    public override object receive_signal(datum_signal signal = null)
    {
        if (signal == null)
            return FALSE;
        if (!Dm.eq(signal.data["code"], code))
            return FALSE;
        Waiting.Detach(receive_signal__body(signal));
        return TRUE;
    }

    private async DmTask receive_signal__body(datum_signal signal)
    {
        if (suicider != null)
        {
            await manual_suicide();
            return;
        }

        // If the holder is a TTV, we want to store the last received signal to incorporate it into TTV logging, else wipe it.
        last_receive_signal_log = (object)holder is obj_item_transfer_valve ? signal.logging_data : null;

        await pulse();
        // [icon2html(src, hearers(src))]: the chat icons (as at the other icon2html sites, atom_examine.dm:202)
        audible_message(span_infoplain(I18n.T("*beep* *beep* *beep*")), null, hearing_range);
        foreach (mob hearing_mob in Dm.each<mob>(get_hearers_in_view(hearing_range, this)))
            hearing_mob.playsound_local(get_turf(this), Dm.resource("sound/machines/beep/triple_beep.ogg"), ASSEMBLY_BEEP_VOLUME, TRUE);
    }

    // tg: code/modules/assembly/signaler.dm:183
    public virtual void set_frequency(object new_frequency = null)
    {
        SSradio.remove_object(this, frequency);
        frequency = Dm.num(new_frequency);
        radio_connection = SSradio.add_object(this, frequency, RADIO_SIGNALER);
    }

    // tg: code/modules/assembly/signaler.dm:189  (syndicate G4a, D316)
    [SignalHandler]
    public virtual object on_mail_unwrap(atom source = null, mob user = null, obj_item_mail_traitor letter = null)
    {
        to_chat(user, span_danger(I18n.T("As you open [], you accidentally press a button on []!", letter, this)));
        signal(); // INVOKE_ASYNC of a proc that never sleeps. No need to check for cooldown, the cooldown is shorter than the do_after for opening mail
        return NONE; //don't return handled, we want in hands and open ui
    }
}

// tg: code/modules/assembly/signaler.dm:195
public partial class obj_item_assembly_signaler_cyborg : obj_item_assembly_signaler
{
    // tg: code/modules/assembly/signaler.dm:197
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/assembly/signaler.dm:200
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return ITEM_INTERACT_BLOCKING;
    }
}

// tg: code/modules/assembly/signaler.dm:203
public partial class obj_item_assembly_signaler_internal : obj_item_assembly_signaler
{
    // tg: code/modules/assembly/signaler.dm:206
    public override object ui_state(mob user = null)
    {
        return GLOB.inventory_state;
    }

    // tg: code/modules/assembly/signaler.dm:209
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/assembly/signaler.dm:212
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/assembly/signaler.dm:215
    public override async DmTask<object> can_interact(mob user = null, float? require_adjacent_turf = null)
    {
        if (ispAI(user))
            return TRUE;
        return await base.can_interact(user, require_adjacent_turf);
    }
}
