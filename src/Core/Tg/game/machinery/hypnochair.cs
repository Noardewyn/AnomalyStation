// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/hypnochair.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Gaps SG1: the whole file. The enhanced interrogation chamber: a mob dragged in is closed inside; a trigger phrase set in its
// HypnoChair window and 45 seconds of strobing lights (blind and deaf while it runs) implant a hypnotic trigger (or, one time
// in ten, a hypnotic stupor) unless the occupant's eyes are protected; an interrupted session leaves confusion, dizziness and
// blur by how long it ran, maybe a trance. The var block is generated.

using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/machinery/hypnochair.dm:1
public partial class obj_machinery_hypnochair : obj_machinery
{
    ///Timer ID for interrogations
    // tg: code/game/machinery/hypnochair.dm:21  var/timerid = 0 (hand-written: the generator types it a number from its 0, but addtimer
    // returns the stoppable timer's id as text, as tg's does)
    public object timerid = 0;

    // The type's defaults (tg: code/game/machinery/hypnochair.dm:2): with timerid declared above, the var block is hand-written
    // (types-converter.md 3.2, mode Hand), so the constructor the generator wrote is here.
    public obj_machinery_hypnochair()
    {
        name = "enhanced interrogation chamber";
        desc = "A device used to perform \"enhanced interrogation\" through invasive mental conditioning.";
        icon = Dm.resource("icons/obj/machines/implant_chair.dmi");
        icon_state = "hypnochair";
        base_icon_state = "hypnochair";
        circuit = typeof(obj_item_circuitboard_machine_hypnochair);
        density = 1;
        opacity = 0;
        interaction_flags_mouse_drop = 8;
    }

    // tg: code/game/machinery/hypnochair.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(open_machine());
        update_appearance();
        return dot;
    }

    // tg: code/game/machinery/hypnochair.dm:30
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return occupant == null ? default_deconstruction_screwdriver(user, tool) : NONE;
    }

    // tg: code/game/machinery/hypnochair.dm:33
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_pry_open(user, tool, deconstruct_on_fail: TRUE);
    }

    // tg: code/game/machinery/hypnochair.dm:36
    public override object ui_state(mob user = null) => GLOB.notcontained_state;

    // tg: code/game/machinery/hypnochair.dm:39
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "HypnoChair", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/hypnochair.dm:45
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        var mob_occupant = occupant as mob_living;

        data["occupied"] = mob_occupant != null ? 1 : 0;
        data["open"] = state_open;
        data["interrogating"] = interrogating;

        DmList occupant_data = Dm.list();
        data["occupant"] = occupant_data;
        if (mob_occupant != null)
        {
            occupant_data["name"] = mob_occupant.name;
            occupant_data["stat"] = mob_occupant.stat;
        }

        data["trigger"] = trigger_phrase;

        return data;
    }

    // tg: code/game/machinery/hypnochair.dm:62
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (Dm.str(action))
        {
            case "door":
                if (Dm.truthy(state_open))
                    await close_machine();
                else
                {
                    if (!Dm.truthy(interrogating))
                        await open_machine();
                }
                dot = TRUE;
                break;
            case "set_phrase":
                set_phrase(@params["phrase"]);
                dot = TRUE;
                break;
            case "interrogate":
                if (!Dm.truthy(interrogating))
                    await interrogate();
                else
                    await interrupt_interrogation();
                dot = TRUE;
                break;
        }
        return dot;
    }

    // tg: code/game/machinery/hypnochair.dm:85
    public virtual void set_phrase(object phrase)
    {
        trigger_phrase = phrase as string;
    }

    // tg: code/game/machinery/hypnochair.dm:88
    public virtual async DmTask interrogate()
    {
        if (!Dm.truthy(trigger_phrase))
        {
            playsound(get_turf(this), Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 25, TRUE);
            return;
        }
        if (occupant is not mob_living_carbon C)
        {
            playsound(get_turf(this), Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 25, TRUE);
            return;
        }
        victim = C;
        if (Dm.num(C.get_eye_protection()) <= 0)
        {
            to_chat(C, span_warning(I18n.T("Strobing coloured lights assault you relentlessly! You're losing your ability to think straight!")));
            await C.become_blind(HYPNOCHAIR_TRAIT);
            ADD_TRAIT(C, TRAIT_DEAF, HYPNOCHAIR_TRAIT);
        }
        interrogating = TRUE;
        START_PROCESSING(SSobj, this);
        start_time = world.time;
        update_appearance();
        timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(finish_interrogation)), 450, TIMER_STOPPABLE, file: "code/game/machinery/hypnochair.dm", line: 105);
    }

    // tg: code/game/machinery/hypnochair.dm:107
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (occupant is not mob_living_carbon C || !ReferenceEquals(C, victim))
        {
            Waiting.NoSleep(interrupt_interrogation());
            return null;
        }
        if (SPT_PROB(5, seconds_per_tick) && !(Dm.num(C.get_eye_protection()) > 0))
        {
            to_chat(C, span_hypnophrase(Dm.str(pick(
                I18n.T("...blue... red... green... blue, red, green, blueredgreen[]", span_small(I18n.T("blueredgreen"))),
                I18n.T("...pretty colors..."),
                I18n.T("...you keep hearing words, but you can't seem to understand them..."),
                I18n.T("...so peaceful..."),
                I18n.T("...an annoying buzz in your ears...")))));
        }

        Waiting.NoSleep(use_energy(active_power_usage * Dm.num(seconds_per_tick)));
        return null;
    }

    // tg: code/game/machinery/hypnochair.dm:123
    public virtual async DmTask finish_interrogation()
    {
        interrogating = FALSE;
        STOP_PROCESSING(SSobj, this);
        update_appearance();
        string temp_trigger = trigger_phrase;
        trigger_phrase = ""; //Erase evidence, in case the subject is able to look at the panel afterwards
        audible_message(span_notice(I18n.T("[] pings!", this)));
        playsound(this, Dm.resource("sound/machines/ping.ogg"), 30, TRUE);

        if (QDELETED(victim) || !ReferenceEquals(victim, occupant))
        {
            victim = null;
            return;
        }
        victim.cure_blind(HYPNOCHAIR_TRAIT);
        REMOVE_TRAIT(victim, TRAIT_DEAF, HYPNOCHAIR_TRAIT);
        if (!(Dm.num(victim.get_eye_protection()) > 0))
        {
            victim.cure_trauma_type(typeof(datum_brain_trauma_severe_hypnotic_trigger), TRAUMA_RESILIENCE_SURGERY);
            if (prob(90))
                await victim.gain_trauma(Dm.@new<datum_brain_trauma_severe_hypnotic_trigger>(temp_trigger), TRAUMA_RESILIENCE_SURGERY);
            else
                await victim.gain_trauma(Dm.@new<datum_brain_trauma_severe_hypnotic_stupor>(), TRAUMA_RESILIENCE_SURGERY);
        }
        victim = null;
    }

    // tg: code/game/machinery/hypnochair.dm:145
    public virtual async DmTask interrupt_interrogation()
    {
        deltimer(timerid);
        interrogating = FALSE;
        STOP_PROCESSING(SSobj, this);
        update_appearance();

        if (QDELETED(victim))
        {
            victim = null;
            return;
        }
        victim.cure_blind(HYPNOCHAIR_TRAIT);
        REMOVE_TRAIT(victim, TRAIT_DEAF, HYPNOCHAIR_TRAIT);
        if (!(Dm.num(victim.get_eye_protection()) > 0))
        {
            float time_diff = world.time - Dm.num(start_time);
            if (time_diff >= 0 && time_diff <= 100)
            {
                await victim.adjust_confusion(10 * SECONDS);
                await victim.set_dizzy_if_lower(200 * SECONDS);
                await victim.set_eye_blur_if_lower(10 * SECONDS);
            }
            else if (time_diff >= 101 && time_diff <= 200)
            {
                await victim.adjust_confusion(15 * SECONDS);
                await victim.set_dizzy_if_lower(400 * SECONDS);
                await victim.set_eye_blur_if_lower(20 * SECONDS);
                if (prob(25))
                    await victim.apply_status_effect(typeof(datum_status_effect_trance), rand(50, 150), FALSE);
            }
            else if (time_diff >= 201)
            {
                await victim.adjust_confusion(20 * SECONDS);
                await victim.set_dizzy_if_lower(600 * SECONDS);
                await victim.set_eye_blur_if_lower(30 * SECONDS);
                if (prob(65))
                    await victim.apply_status_effect(typeof(datum_status_effect_trance), rand(50, 150), FALSE);
            }
        }
        victim = null;
    }

    // tg: code/game/machinery/hypnochair.dm:177
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][][]", base_icon_state, Dm.truthy(state_open) ? "_open" : null,
            occupant != null ? Dm.text("_[]", Dm.truthy(interrogating) ? "active" : "occupied") : null);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/hypnochair.dm:181
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        user.change_next_special_move(CLICK_CD_BREAKOUT);
        user.visible_message(span_notice(I18n.T("You see [] kicking against the door of []!", user, this)),
            span_notice(I18n.T("You lean on the back of [] and start pushing the door open... (this will take about [].)", this, DisplayTimeText(600))),
            span_hear(I18n.T("You hear a metallic creaking from [].", this)));
        if (Dm.truthy(await do_after(user, 600, target: this)))
        {
            if (user == null || IS_UNCONSCIOUS_OR_CRIT(user) || !ReferenceEquals(user.loc, this) || Dm.truthy(state_open))
                return null;
            user.visible_message(span_warning(I18n.T("[] successfully broke out of []!", user, this)),
                span_notice(I18n.T("You successfully break out of []!", this)));
            await open_machine();
        }
        return null;
    }

    // tg: code/game/machinery/hypnochair.dm:193
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (message_cooldown <= world.time)
        {
            message_cooldown = world.time + 50;
            to_chat(user, span_warning(I18n.T("[]'s door won't budge!", this)));
        }
        return null;
    }

    // tg: code/game/machinery/hypnochair.dm:199
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        if (!isliving(dropped))
            return null;
        await close_machine(dropped as atom_movable);
        return null;
    }
}
