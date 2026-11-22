// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/status_display.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps W1, 2026-11-07): the status displays (the base display, evac, supply, shuttle, AI and random message), their shared
// text overlays (status_display_text, drawn by the clients as world maptext, protocol version 74) and the global alert procs.
// The greenscreen (the location indicator, the appearance holder, the greenscreen display and the captain-caster camera) is
// translated in the syndicate campaign (D316). Not translated: the circuit
// component (/obj/item/circuit_component/status_display, wiremod), the evac display's
// circuit USB port (Deferred). DEVIATION(DV12): the displays' emissive "outline" overlay is not created.

// tg: code/game/machinery/status_display.dm:11  GLOBAL_DATUM_INIT(status_font, /datum/font, new /datum/font/tiny_unicode/size_12pt())
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/game/machinery/status_display.dm:11
    public datum_font status_font;

    // tg: code/game/machinery/status_display.dm:11  (GLOBAL_MANAGED(status_font, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstatus_font()
    {
        status_font = Dm.@new<datum_font_tiny_unicode_size_12pt>();
        Dm.append(ref gvars_datum_init_order, "status_font");
    }

    // List in the form key -> status display that shows said key
    // tg: code/game/machinery/status_display.dm:143
    public DmList key_to_status_display;

    // tg: code/game/machinery/status_display.dm:143  (GLOBAL_LIST_EMPTY, GLOBAL_MANAGED(key_to_status_display, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalkey_to_status_display()
    {
        key_to_status_display = Dm.list();
        Dm.append(ref gvars_datum_init_order, "key_to_status_display");
    }
}

// tg: code/game/machinery/status_display.dm:312  var/static/regex/header_regex
public sealed partial class TgState
{
    internal regex status_display_text_header_regex;
}

/// Status display which can show images and scrolling text.
// tg: code/game/machinery/status_display.dm:14
public partial class obj_machinery_status_display : obj_machinery
{
    // tg: code/game/machinery/status_display.dm:7-10  #define LINE1_X 1, LINE1_Y -4, LINE2_X 1, LINE2_Y -11
    private const float LINE1_X = 1;
    private const float LINE1_Y = -4;
    private const float LINE2_X = 1;
    private const float LINE2_Y = -11;

    //makes it go on the wall when built
    // tg: code/game/machinery/status_display.dm:54
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }

    // tg: code/game/machinery/status_display.dm:58
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        object dot = null;
        dot = await base.wrench_act_secondary(user, tool);
        balloon_alert(user, I18n.T("[]securing...", Dm.truthy(anchored) ? "un" : ""));
        tool.play_tool_sound(this);
        if (Dm.truthy(await tool.use_tool(this, user, 6 * SECONDS)))
        {
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            balloon_alert(user, I18n.T("[]secured", Dm.truthy(anchored) ? "un" : ""));
            await deconstruct();
            return TRUE;
        }
        return dot;
    }

    // tg: code/game/machinery/status_display.dm:68
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return null;
        if (Dm.num(atom_integrity) >= max_integrity)
        {
            balloon_alert(user, I18n.T("it doesn't need repairs!"));
            return TRUE;
        }
        user.balloon_alert_to_viewers(I18n.T("repairing display..."), I18n.T("repairing..."));
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, amount: 0, volume: 50)))
            return TRUE;
        balloon_alert(user, I18n.T("repaired"));
        atom_integrity = max_integrity;
        await set_machine_stat(Dm.bitand(machine_stat, ~BROKEN));
        update_appearance();
        return TRUE;
    }

    // tg: code/game/machinery/status_display.dm:83
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (!Dm.truthy(disassembled))
        {
            Dm.@new<obj_item_stack_sheet_iron>(drop_location(), 2);
            Dm.@new<obj_item_shard>(drop_location());
            Dm.@new<obj_item_shard>(drop_location());
        }
        else
            Dm.@new<obj_item_wallframe_status_display>(drop_location());
        return null;
    }

    /// Immediately change the display to the given picture.
    // tg: code/game/machinery/status_display.dm:92
    public virtual void set_picture(object state = null)
    {
        if (!Dm.eq(state, current_picture))
            current_picture = state as string;

        update_appearance();
    }

    /// Immediately change the display to the given two lines.
    // tg: code/game/machinery/status_display.dm:99
    public virtual void set_messages(object line1 = null, object line2 = null)
    {
        line1 = uppertext(line1);
        line2 = uppertext(line2);

        float message_changed = FALSE;
        if (!Dm.eq(line1, message1))
        {
            message1 = line1 as string;
            message_changed = TRUE;
        }

        if (!Dm.eq(line2, message2))
        {
            message2 = line2 as string;
            message_changed = TRUE;
        }

        if (Dm.truthy(message_changed))
            update_appearance();
    }

    /**
     * Remove both message objs and null the fields.
     * Don't call this in subclasses.
     */
    // tg: code/game/machinery/status_display.dm:119
    public virtual async DmTask clear_display()
    {
        clear_text();
        await clear_green_screen();
    }

    /// Clears text off the display.
    // tg: code/game/machinery/status_display.dm:125
    public virtual void clear_text()
    {
        obj_effect_overlay_status_display_text overlay_1 = get_status_text(message_key_1) as obj_effect_overlay_status_display_text;
        message_key_1 = null;
        overlay_1?.disown(this);
        obj_effect_overlay_status_display_text overlay_2 = get_status_text(message_key_2) as obj_effect_overlay_status_display_text;
        message_key_2 = null;
        overlay_2?.disown(this);
    }

    /// Clears the green screen display.
    // tg: code/game/machinery/status_display.dm:135
    public virtual async DmTask clear_green_screen()
    {
        speakers?.set_listening(FALSE);
        if (Dm.truthy(LAZYLEN(active_displays)))
        {
            Dm.remove(vis_contents, active_displays);
            active_displays = null; // LAZYNULL (__DEFINES/_lists.dm), inline (rules 11.1 (a))
        }
    }

    /**
     * Create/update message overlay.
     * They must be handled as real objects for the animation to run.
     * Don't call this in subclasses.
     * Arguments:
     * * overlay - the current /obj/effect/overlay/status_display_text instance
     * * line_y - The Y offset to render the text.
     * * x_offset - Used to offset the text on the X coordinates, not usually needed.
     * * message - the new message text.
     * Returns new /obj/effect/overlay/status_display_text or null if unchanged.
     */
    // tg: code/game/machinery/status_display.dm:167
    public virtual object update_message(object current_key = null, object line_y = null, object message = null, object x_offset = null, object line_pair = null)
    {
        obj_effect_overlay_status_display_text current_overlay = get_status_text(current_key) as obj_effect_overlay_status_display_text;
        obj_effect_overlay_status_display_text new_overlay = generate_status_text(line_y, message, x_offset, text_color, header_text_color, line_pair) as obj_effect_overlay_status_display_text;

        if (current_overlay == new_overlay)
            return current_key;

        current_overlay?.disown(this);
        new_overlay.own(this);
        return new_overlay.status_key;
    }

    // tg: code/game/machinery/status_display.dm:178
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = null;
        dot = base.update_appearance(updates);
        if (
            Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) ||
            (current_mode == SD_BLANK) ||
            (current_mode != SD_PICTURE && Dm.eq(message1, "") && Dm.eq(message2, ""))
        )
        {
            Waiting.NoSleep(set_light(0));
            return dot;
        }
        Waiting.NoSleep(set_light(1.5f, 0.7f, LIGHT_COLOR_FAINT_CYAN)); // blue light
        return dot;
    }

    // tg: code/game/machinery/status_display.dm:189
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);

        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
        {
            await clear_display();
            return dot;
        }

        switch (current_mode)
        {
            case SD_BLANK:
                await clear_display();
                // Turn off backlight.
                return dot;
            case SD_PICTURE:
                await clear_display();
                Dm.append(ref dot, mutable_appearance(icon, current_picture));
                if (Dm.eq(current_picture, AI_DISPLAY_DONT_GLOW)) // If the thing's off, don't display the emissive yeah?
                    return dot;
                break;
            case SD_GREENSCREEN:
                clear_text();
                if (Dm.truthy(LAZYLEN(active_displays)))
                {
                    Dm.combine(vis_contents, active_displays);
                    speakers?.set_listening(TRUE);
                }
                break;
            default:
                await clear_green_screen();
                float line1_metric;
                float line2_metric;
                float line_pair;
                line1_metric = Dm.num(GLOB.status_font.get_metrics(message1));
                line2_metric = Dm.num(GLOB.status_font.get_metrics(message2));
                line_pair = (line1_metric > line2_metric ? line1_metric : line2_metric);

                message_key_1 = update_message(message_key_1, LINE1_Y, message1, LINE1_X, line_pair);
                message_key_2 = update_message(message_key_2, LINE2_Y, message2, LINE2_X, line_pair);

                // Turn off backlight if message is blank
                if (Dm.eq(message1, "") && Dm.eq(message2, ""))
                    return dot;
                break;
        }

        // DEVIATION(DV12): . += emissive_appearance(icon, "outline", src, alpha = src.alpha), the screen's glow on the emissive plane, is not
        // created (lighting planes and emissives are not ported); the clients light the screen from the display's light (update_appearance).
        return dot;
    }

    // Timed process - performs nothing in the base class
    // tg: code/game/machinery/status_display.dm:230
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            // No power, no processing.
            update_appearance();
        }

        return PROCESS_KILL;
    }

    /// Updates the display and starts it processing again if needed
    // tg: code/game/machinery/status_display.dm:238
    public virtual void update()
    {
        if (!Dm.eq(process(Dm.num(SSmachines.wait) / 10), PROCESS_KILL)) // SSMACHINES_DT (__DEFINES/subsystems.dm:276)
            START_PROCESSING(SSmachines, this);
    }

    // tg: code/game/machinery/status_display.dm:242
    public override object power_change()
    {
        object dot = null;
        dot = base.power_change();
        update();
        return dot;
    }

    // tg: code/game/machinery/status_display.dm:246
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        current_mode = SD_PICTURE;
        set_picture("ai_bsod");
        return dot;
    }

    // tg: code/game/machinery/status_display.dm:253
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (Dm.truthy(LAZYLEN(active_displays)))
        {
            Dm.append(ref dot, span_notice(I18n.T("<hr>It's currently broadcasting. You can see...")));
            float has_any = FALSE;
            foreach (obj_effect_abstract_greenscreen_display display in Dm.each<obj_effect_abstract_greenscreen_display>(active_displays))
            {
                foreach (atom_movable thing in Dm.each<atom_movable>(display.displaying))
                {
                    Dm.append(ref dot, span_notice(I18n.T("&bull; []", Dm.text("\\A []", thing.name))));
                    has_any = TRUE;
                }
            }
            if (!Dm.truthy(has_any))
                Dm.append(ref dot, span_notice(I18n.T("&bull; Nothing.")));
        }

        obj_effect_overlay_status_display_text message1_overlay = get_status_text(message_key_1) as obj_effect_overlay_status_display_text;
        obj_effect_overlay_status_display_text message2_overlay = get_status_text(message_key_2) as obj_effect_overlay_status_display_text;
        if (message1_overlay != null || message2_overlay != null)
        {
            Dm.append(ref dot, I18n.T("<hr>The display says:"));
            // tg reads message1_overlay.message unchecked (a runtime when only the second line has a text object); the port reads it null-safe
            if (Dm.truthy(message1_overlay?.message))
                Dm.append(ref dot, I18n.T("\t<tt>[]</tt>", html_encode(message1_overlay.message)));
            if (Dm.truthy(message2_overlay?.message))
                Dm.append(ref dot, I18n.T("\t<tt>[]</tt>", html_encode(message2_overlay.message)));
        }
        return dot;
    }

    // Helper procs for child display types.
    // tg: code/game/machinery/status_display.dm:275
    public virtual object display_shuttle_status(obj_docking_port_mobile shuttle = null)
    {
        if (shuttle == null)
        {
            // the shuttle is missing - no processing
            set_messages("shutl", "not in service");
            return PROCESS_KILL;
        }
        else if (Dm.truthy(shuttle.timer))
        {
            object line1 = shuttle.getModeStr();
            object line2 = shuttle.getTimerStr();

            set_messages(line1, line2);
        }
        else
        {
            // don't kill processing, the timer might turn back on
            set_messages("", "");
        }
        return null;
    }

    // tg: code/game/machinery/status_display.dm:289
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(clear_display());
        // QDEL_NULL(speakers) (__DEFINES/qdel.dm), inline (rules 11.1 (a))
        qdel(speakers);
        speakers = null;
        return base.Destroy(force);
    }
}

public static partial class GlobalProcs
{
    // tg: code/game/machinery/status_display.dm:145
    public static object generate_status_text(object line_y = null, object message = null, object x_offset = null, object text_color = null, object header_text_color = null, object line_pair = null)
    {
        object key = Dm.text("[]-[]-[]-[]-[]-[]", line_y, message, x_offset, text_color, header_text_color, line_pair);
        obj_effect_overlay_status_display_text new_overlay = GLOB.key_to_status_display[key] as obj_effect_overlay_status_display_text;
        if (new_overlay == null)
        {
            new_overlay = Dm.@new<obj_effect_overlay_status_display_text>(null, line_y, message, text_color, header_text_color, x_offset, line_pair, key);
            GLOB.key_to_status_display[key] = new_overlay;
        }
        return new_overlay;
    }

    // tg: code/game/machinery/status_display.dm:153
    public static object get_status_text(object key = null)
    {
        return GLOB.key_to_status_display[key];
    }

    /// Send an emergency alert signal to all status displays
    /// alert_type: The picture state to display ("biohazard", "lockdown", "radiation", etc.)
    // tg: code/game/machinery/status_display.dm:1204
    public static void send_status_display_alert(object alert_type = null)
    {
        datum_radio_frequency frequency = SSradio.return_frequency(FREQ_STATUS_DISPLAYS);
        if (frequency != null)
        {
            datum_signal alert_signal = Dm.@new<datum_signal>();
            alert_signal.data["command"] = "alert";
            alert_signal.data["picture_state"] = alert_type;
            alert_signal.data["emergency_override"] = TRUE;
            alert_signal.data["emergency_type"] = alert_type; // Track specific emergency type
            // GAP(types): tg posts from a new /atom/movable/virtualspeaker (var/atom/movable/virtualspeaker/virtual_speaker = new(null)); the port's
            // post_signal takes an obj source, so the signal has none (the displays never read it).
            frequency.post_signal(null, alert_signal);
        }
    }

    /// Clear a specific emergency type from status displays
    /// emergency_type: The specific type to clear ("biohazard", "lockdown", "radiation", etc.)
    /// If emergency_type is null, clears ALL emergencies
    // tg: code/game/machinery/status_display.dm:1218
    public static void clear_status_display_emergency(object emergency_type = null)
    {
        datum_radio_frequency frequency = SSradio.return_frequency(FREQ_STATUS_DISPLAYS);
        if (frequency != null)
        {
            datum_signal clear_signal = Dm.@new<datum_signal>();
            clear_signal.data["command"] = "clear_emergency";
            clear_signal.data["emergency_override"] = FALSE;
            if (Dm.truthy(emergency_type))
                clear_signal.data["emergency_type"] = emergency_type;
            // GAP(types): as send_status_display_alert, the signal has no source (tg's virtualspeaker)
            frequency.post_signal(null, clear_signal);
        }
    }

    /// Send a biohazard alert signal to all status displays
    // tg: code/game/machinery/status_display.dm:1230
    public static void send_status_display_biohazard_alert() => send_status_display_alert("biohazard");

    /// Send a lockdown alert signal to all status displays
    // tg: code/game/machinery/status_display.dm:1234
    public static void send_status_display_lockdown_alert() => send_status_display_alert("lockdown");

    /// Send a radiation alert signal to all status displays
    // tg: code/game/machinery/status_display.dm:1238
    public static void send_status_display_radiation_alert() => send_status_display_alert("radiation");

    /// Clear specific emergency types
    // tg: code/game/machinery/status_display.dm:1242
    public static void clear_status_display_biohazard() => clear_status_display_emergency("biohazard");

    // tg: code/game/machinery/status_display.dm:1245
    public static void clear_status_display_lockdown() => clear_status_display_emergency("lockdown");

    // tg: code/game/machinery/status_display.dm:1248
    public static void clear_status_display_radiation() => clear_status_display_emergency("radiation");
}

/**
 * Nice overlay to make text smoothly scroll with no client updates after setup.
 */
// tg: code/game/machinery/status_display.dm:297
public partial class obj_effect_overlay_status_display_text : obj_effect_overlay
{
    // tg: code/game/machinery/status_display.dm:3-6  #define MAX_STATIC_WIDTH 22, FONT_STYLE "12pt 'TinyUnicode'",
    // SCROLL_RATE (0.04 SECONDS) // time per pixel, SCROLL_PADDING 2 // how many pixels we chop to make a smooth loop
    private const float MAX_STATIC_WIDTH = 22;
    private const string FONT_STYLE = "12pt 'TinyUnicode'";
    private const float SCROLL_RATE = 0.04f * SECONDS;
    private const float SCROLL_PADDING = 2;

    // If the line is short enough to not marquee, and it matches this, it's a header.
    // tg: code/game/machinery/status_display.dm:312  var/static/regex/header_regex = regex("^-.*-$")
    private static regex header_regex => TgState.Current.status_display_text_header_regex ??= regex("^-.*-$");

    // tg: code/game/machinery/status_display.dm:314
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object maptext_y = Dm.arg(__extra, 0);
        object message = Dm.arg(__extra, 1);
        object text_color = Dm.arg(__extra, 2);
        object header_text_color = Dm.arg(__extra, 3);
        object xoffset = Dm.arg(__extra, 4) ?? 0;
        object line_pair = Dm.arg(__extra, 5);
        object status_key = Dm.arg(__extra, 6);
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        this.maptext_y = Dm.num(maptext_y);
        this.message = message;
        this.status_key = status_key;

        float line_width = Dm.num(GLOB.status_font.get_metrics(message));

        if (line_width > MAX_STATIC_WIDTH)
        {
            // Marquee text
            object marquee_message = Dm.text("[]    []    []", message, message, message);

            // Width of full content. Must of these is never revealed unless the user inputted a single character.
            float full_marquee_width = Dm.num(GLOB.status_font.get_metrics(Dm.text("[]    ", marquee_message)));
            // We loop after only this much has passed.
            float looping_marquee_width = (Dm.num(GLOB.status_font.get_metrics(Dm.text("[]    ]", message))) - SCROLL_PADDING);

            maptext = generate_text(marquee_message, center: FALSE, text_color: text_color) as string;
            maptext_width = full_marquee_width;
            maptext_x = 0;

            // Mask off to fit in screen.
            add_filter("mask", 1, alpha_mask_filter(icon: icon(icon, "outline")) as DmList);

            // Scroll.
            float time = Dm.num(line_pair) * SCROLL_RATE;
            animate(this, Dm.named("maptext_x", (-looping_marquee_width) + MAX_STATIC_WIDTH), Dm.named("time", time), Dm.named("loop", -1));
            animate(Dm.named("maptext_x", MAX_STATIC_WIDTH), Dm.named("time", 0));
        }
        else
        {
            // Centered text
            object color = Dm.truthy(header_regex.Find(message)) ? header_text_color : text_color;
            maptext = generate_text(message, center: TRUE, text_color: color) as string;
            maptext_x = Dm.num(xoffset); //Defaults to 0, this would be centered unless overided
        }
        return dot;
    }

    // tg: code/game/machinery/status_display.dm:349
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.key_to_status_display, status_key);
        return base.Destroy(force);
    }

    /// Status displays are static, shared by everyone who needs them
    /// This marks us as being used by one more guy
    // tg: code/game/machinery/status_display.dm:355
    public virtual void own(atom_movable owned_by = null)
    {
        Dm.append(owned_by.vis_contents, this);
        use_count += 1;
    }

    /// Status displays are static, shared by everyone who needs them
    /// This marks us as no longer being used by a guy
    // tg: code/game/machinery/status_display.dm:361
    public virtual void disown(atom_movable disowned_by = null)
    {
        Dm.remove(disowned_by.vis_contents, this);
        use_count -= 1;
        if (use_count <= 0)
            qdel(this);
    }

    /**
     * Generate the actual maptext.
     * Arguments:
     * * text - the text to display
     * * center - center the text if TRUE, otherwise right-align (the direction the text is coming from)
     * * text_color - the text color
     */
    // tg: code/game/machinery/status_display.dm:374
    public virtual object generate_text(object text = null, object center = null, object text_color = null)
    {
        return Dm.text("<div style=\"color:[];font:[][]\" valign=\"top\">[]</div>", text_color, FONT_STYLE, Dm.truthy(center) ? ";text-align:center" : "text-align:right", text);
    }
}

/// Evac display which shows shuttle timer or message set by Command.
// tg: code/game/machinery/status_display.dm:378
public partial class obj_machinery_status_display_evac : obj_machinery_status_display
{
    // tg: code/game/machinery/status_display.dm:401
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        // register for radio system
        SSradio.add_object(this, frequency);
        // Circuit USB
        Dm.Deferred("code/game/machinery/status_display.dm:406", "Initialize: AddComponent(/datum/component/usb_port, ...) for the status display circuit component (wiremod is not ported)");
        // the port's test worlds may boot without the subsystem (tg always has it)
        if (SSsecurity_level != null)
            RegisterSignal(SSsecurity_level, COMSIG_SECURITY_LEVEL_CHANGED, nameof(on_sec_level_change));
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        return dot;
    }

    // tg: code/game/machinery/status_display.dm:411
    public override object Destroy(float? force = null)
    {
        SSradio.remove_object(this, frequency);
        if (SSsecurity_level != null)
            UnregisterSignal(SSsecurity_level, COMSIG_SECURITY_LEVEL_CHANGED);
        // Clean up our timer so we don't have ghost timers floating around
        if (Dm.truthy(alert_display_timer))
        {
            deltimer(alert_display_timer);
            alert_display_timer = null;
        }
        return base.Destroy(force);
    }

    // tg: code/game/machinery/status_display.dm:420
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            // No power, no processing.
            update_appearance();
            return PROCESS_KILL;
        }

        if (Dm.truthy(friendc)) //Makes all status displays except supply shuttle timer display the eye -- Urist
        {
            current_mode = SD_PICTURE;
            set_picture("ai_friend");
            return PROCESS_KILL;
        }

        // Figure out what we should be showing right now
        DmList target_display = get_highest_priority_display() as DmList;
        object target_priority = target_display["priority"];

        // Don't mess with more important stuff that's already showing
        if (Dm.num(target_priority) <= Dm.num(current_priority))
        {
            // Keep the shuttle timer updating if that's what we're showing
            if (current_mode == SD_EMERGENCY)
                return display_shuttle_status(SSshuttle?.emergency);
            return PROCESS_KILL;
        }

        // Switch to whatever should have priority right now
        object sw__1 = target_display["mode"];
        if (Dm.eq(sw__1, SD_EMERGENCY))
        {
            set_display_with_priority(SD_EMERGENCY, target_priority);
            return display_shuttle_status(SSshuttle?.emergency);
        }
        else if (Dm.eq(sw__1, SD_PICTURE))
        {
            set_display_with_priority(SD_PICTURE, target_priority, picture_state: target_display["picture"]);
            return PROCESS_KILL;
        }
        else
        {
            return PROCESS_KILL;
        }
    }

    /// Figures out what we should be showing based on what's happening on the station
    /// Returns a list with the mode, priority, and other details
    // tg: code/game/machinery/status_display.dm:455
    public virtual object get_highest_priority_display()
    {
        // Shuttle stuff gets top priority - but only when it's actually doing something
        if (SSshuttle?.emergency != null)
        {
            object sw__1 = SSshuttle.emergency.mode;
            if (Dm.eq(sw__1, SHUTTLE_CALL) || Dm.eq(sw__1, SHUTTLE_DOCKED) || Dm.eq(sw__1, SHUTTLE_ESCAPE))
                return Dm.list(Dm.kv("mode", SD_EMERGENCY), Dm.kv("priority", DISPLAY_PRIORITY_SHUTTLE));
            // Don't bother showing shuttle status when it's just sitting there
        }

        // Check if someone manually set something from the bridge
        if (manual_display_state != null && Dm.num(manual_display_state["priority"]) >= DISPLAY_PRIORITY_MESSAGE)
            return manual_display_state.Copy();

        // Nothing special happening, just show the logo
        return Dm.list(Dm.kv("mode", SD_PICTURE), Dm.kv("priority", DISPLAY_PRIORITY_LOGO), Dm.kv("picture", "default"));
    }

    /// Tries to change what the display is showing, but respects priority levels
    /// Returns TRUE if we actually changed it, FALSE if something more important was already showing
    // tg: code/game/machinery/status_display.dm:472
    public virtual object set_display_with_priority(object new_mode = null, object priority = null, object picture_state = null, object message1 = null, object message2 = null, float? force_override = null)
    {
        force_override ??= FALSE;
        // Allow manual overrides to always work (except when temporary alerts are showing)
        if (!Dm.truthy(force_override) && Dm.num(priority) < Dm.num(current_priority) && !Dm.eq(current_priority, DISPLAY_PRIORITY_ALERT_TEMP))
            return FALSE;

        // Cancel alert timer if we're changing to a different display type
        if (Dm.truthy(alert_display_timer) && !Dm.eq(priority, DISPLAY_PRIORITY_ALERT_TEMP))
        {
            deltimer(alert_display_timer);
            alert_display_timer = null;
        }

        current_mode = Dm.num(new_mode);
        current_priority = Dm.num_or_null(priority);

        // Store manually set displays (not temporary alerts, automated shuttle, or logo defaults)
        // Logo selections shouldn't be treated as manual overrides since logo is the default anyway
        bool is_logo_default = (Dm.eq(new_mode, SD_PICTURE) && (Dm.eq(picture_state, "default") || picture_state == null));
        if (Dm.num(priority) >= DISPLAY_PRIORITY_MESSAGE && !Dm.eq(priority, DISPLAY_PRIORITY_ALERT_TEMP) && !Dm.eq(priority, DISPLAY_PRIORITY_SHUTTLE) && !is_logo_default)
        {
            manual_display_state = Dm.list(
                Dm.kv("mode", new_mode),
                Dm.kv("priority", priority),
                Dm.kv("picture", picture_state),
                Dm.kv("message1", message1),
                Dm.kv("message2", message2)
            );
        }
        else if (is_logo_default)
        {
            // Clear manual display state when logo is selected
            manual_display_state = null;
        }

        object sw__1 = new_mode;
        if (Dm.eq(sw__1, SD_BLANK))
            update_appearance();
        else if (Dm.eq(sw__1, SD_EMERGENCY))
            set_messages("", "");
        else if (Dm.eq(sw__1, SD_MESSAGE))
            set_messages(Dm.truthy(message1) ? message1 : "", Dm.truthy(message2) ? message2 : "");
        else if (Dm.eq(sw__1, SD_PICTURE))
        {
            last_picture = picture_state;
            set_picture(picture_state);
        }
        else if (Dm.eq(sw__1, SD_GREENSCREEN))
            update_appearance();

        // Start processing to watch for higher priority displays (like shuttle calls)
        update();

        return TRUE;
    }

    // tg: code/game/machinery/status_display.dm:518
    public override object receive_signal(datum_signal signal = null)
    {
        object sw__1 = signal.data["command"];
        if (Dm.eq(sw__1, "blank"))
        {
            set_display_with_priority(SD_BLANK, DISPLAY_PRIORITY_MESSAGE, force_override: TRUE);
        }
        else if (Dm.eq(sw__1, "shuttle"))
        {
            set_display_with_priority(SD_EMERGENCY, DISPLAY_PRIORITY_SHUTTLE, force_override: TRUE);
            // Start processing to keep shuttle timer updated and handle priority changes
            update();
        }
        else if (Dm.eq(sw__1, "message"))
        {
            set_display_with_priority(SD_MESSAGE, DISPLAY_PRIORITY_MESSAGE, message1: signal.data["top_text"], message2: signal.data["bottom_text"], force_override: TRUE);
        }
        else if (Dm.eq(sw__1, "alert"))
        {
            object picture_state = signal.data["picture_state"];
            object priority = DISPLAY_PRIORITY_MESSAGE;
            object is_emergency = signal.data["emergency_override"];
            object emergency_type = signal.data["emergency_type"];

            // Check if this is just setting the logo back to default
            if (Dm.eq(picture_state, "default"))
            {
                // Clear any manual display state and reset to logo priority
                manual_display_state = null;
                set_display_with_priority(SD_PICTURE, DISPLAY_PRIORITY_LOGO, picture_state: picture_state, force_override: TRUE);
            }
            // Automated emergency alerts use temporary high priority to interrupt even shuttle displays
            else if (Dm.truthy(is_emergency))
            {
                // Track this emergency type
                if (Dm.truthy(emergency_type) && !Dm.@in(emergency_type, active_emergencies))
                    Dm.combine(ref active_emergencies, emergency_type);
                show_temporary_display(SD_PICTURE, DISPLAY_PRIORITY_EMERGENCY_TEMP, picture_state: picture_state);
            }
            else
            {
                // Manual alerts can override existing displays
                set_display_with_priority(SD_PICTURE, priority, picture_state: picture_state, force_override: TRUE);
            }
        }
        else if (Dm.eq(sw__1, "clear_emergency"))
        {
            object emergency_type = signal.data["emergency_type"];

            // Clear specific emergency type or all if no type specified
            if (Dm.truthy(emergency_type))
                Dm.remove(ref active_emergencies, emergency_type);
            else
                active_emergencies.Cut(); // Clear all emergencies

            // Only revert display if no more emergencies are active
            if (!Dm.truthy(length(active_emergencies)))
            {
                // Clear emergency displays - if we're showing a temporary emergency, just cancel the timer
                if (Dm.eq(current_priority, DISPLAY_PRIORITY_EMERGENCY_TEMP) && Dm.truthy(alert_display_timer))
                {
                    deltimer(alert_display_timer);
                    alert_display_timer = null;
                    revert_from_alert_display();
                }
                // Otherwise clear persistent emergency displays by reverting to highest priority
                else if (Dm.num(current_priority) >= DISPLAY_PRIORITY_EMERGENCY)
                {
                    DmList target_display = get_highest_priority_display() as DmList;
                    current_mode = Dm.num(target_display["mode"]);
                    current_priority = Dm.num_or_null(target_display["priority"]);

                    float sw__2 = current_mode;
                    if (sw__2 == SD_EMERGENCY)
                        set_messages("", "");
                    else if (sw__2 == SD_PICTURE)
                        set_picture(target_display["picture"]);
                    else if (sw__2 == SD_MESSAGE)
                        set_messages(Dm.truthy(target_display["message1"]) ? target_display["message1"] : "", Dm.truthy(target_display["message2"]) ? target_display["message2"] : "");
                    else
                        update_appearance();
                }
            }
        }
        else if (Dm.eq(sw__1, "greenscreen"))
        {
            datum_weakref display_ref = signal.data["display"] as datum_weakref;
            obj_effect_abstract_greenscreen_display new_display = display_ref?.resolve() as obj_effect_abstract_greenscreen_display;
            if (new_display != null)
            {
                if (speakers == null)
                {
                    speakers = Dm.@new<obj_item_radio_entertainment_speakers>(this);
                    // not seen, primarily for VV
                    speakers.name = Dm.text("[] speakers", name);
                    // to allow centcom cameras to reach the station
                    speakers.special_channels = Dm.bitor(speakers.special_channels, RADIO_SPECIAL_CENTCOM);
                    speakers.set_frequency(FREQ_STATUS_DISPLAYS);
                }
                LAZYOR(ref active_displays, new_display);
            }
            set_display_with_priority(SD_GREENSCREEN, DISPLAY_PRIORITY_MESSAGE, force_override: TRUE);
        }
        else if (Dm.eq(sw__1, "friendcomputer"))
        {
            // Friend Computer should only affect AI displays, not evac displays
            return null;
        }
        update();
        return null;
    }

    // tg: code/game/machinery/status_display.dm:597
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = null;
        dot = await base.vv_edit_var(var_name, var_value);
        if (Dm.eq(var_name, nameof(current_mode)))
        {
            update_appearance();
            update();
        }
        return dot;
    }

    /// Gets called when security changes the alert level - shows it for 30 seconds then goes back to normal
    // tg: code/game/machinery/status_display.dm:604
    public virtual void on_sec_level_change(datum source = null, object new_level = null)
    {
        // SIGNAL_HANDLER

        // Get the alert level icon
        datum_security_level alert_level = SSsecurity_level.available_levels[SSsecurity_level.number_level_to_text(new_level)] as datum_security_level;

        // Show as temporary alert (can be overridden by emergency temps)
        show_temporary_display(SD_PICTURE, DISPLAY_PRIORITY_ALERT_TEMP, picture_state: alert_level.status_display_icon_state);
    }

    /// Shows a temporary display for 30 seconds then reverts to previous display
    /// Can interrupt lower priority displays but respects higher priority temporary displays
    // tg: code/game/machinery/status_display.dm:615
    public virtual void show_temporary_display(object temp_mode = null, object temp_priority = null, object picture_state = null, object message1 = null, object message2 = null)
    {
        // Don't interrupt higher priority temporary displays
        if (Dm.truthy(alert_display_timer) && Dm.num(current_priority) >= Dm.num(temp_priority))
            return;

        // Remember what we were showing before (unless we're already doing a lower priority alert)
        if (!Dm.truthy(alert_display_timer) || Dm.num(current_priority) < Dm.num(temp_priority))
        {
            revert_mode = current_mode;
            revert_picture = current_picture;
            revert_priority = Dm.num(current_priority);
        }

        // Cancel any existing timer
        if (Dm.truthy(alert_display_timer))
        {
            deltimer(alert_display_timer);
            alert_display_timer = null;
        }

        // Show the temporary display
        current_mode = Dm.num(temp_mode);
        current_priority = Dm.num_or_null(temp_priority);

        float sw__1 = current_mode;
        if (sw__1 == SD_PICTURE)
            set_picture(picture_state);
        else if (sw__1 == SD_MESSAGE)
            set_messages(Dm.truthy(message1) ? message1 : "", Dm.truthy(message2) ? message2 : "");
        else
            update_appearance();

        // Go back to what we were showing after 30 seconds
        alert_display_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(revert_from_alert_display)), 30 * SECONDS, TIMER_STOPPABLE, file: "code/game/machinery/status_display.dm", line: 644);
    }

    /// Goes back to showing whatever we were displaying before the alert level interrupt
    // tg: code/game/machinery/status_display.dm:647
    public virtual void revert_from_alert_display()
    {
        alert_display_timer = null;

        // Figure out what we should be showing now
        DmList target_display = get_highest_priority_display() as DmList;
        current_mode = Dm.num(target_display["mode"]);
        current_priority = Dm.num_or_null(target_display["priority"]);

        float sw__1 = current_mode;
        if (sw__1 == SD_EMERGENCY)
            set_messages("", "");
        else if (sw__1 == SD_PICTURE)
            set_picture(target_display["picture"]);
        else if (sw__1 == SD_MESSAGE)
            set_messages(Dm.truthy(target_display["message1"]) ? target_display["message1"] : "", Dm.truthy(target_display["message2"]) ? target_display["message2"] : "");
        else
            update_appearance();
    }
}

/// Supply display which shows the status of the supply shuttle.
// tg: code/game/machinery/status_display.dm:666
public partial class obj_machinery_status_display_supply : obj_machinery_status_display
{
    // tg: code/game/machinery/status_display.dm:672
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            // No power, no processing.
            update_appearance();
            return PROCESS_KILL;
        }

        object line1;
        object line2;
        if (SSshuttle?.supply == null) // ?.: the port's test worlds may boot without SSshuttle
        {
            // Might be missing in our first update on initialize before shuttles
            // have loaded. Cross our fingers that it will soon return.
            line1 = "shutl";
            line2 = "not in service";
        }
        else if (Dm.eq(SSshuttle.supply.mode, SHUTTLE_IDLE))
        {
            if (Dm.truthy(is_station_level(SSshuttle.supply.z)))
            {
                line1 = "CARGO";
                line2 = "Docked";
            }
            else
            {
                line1 = "";
                line2 = "";
            }
        }
        else
        {
            line1 = SSshuttle.supply.getModeStr();
            line2 = SSshuttle.supply.getTimerStr();
        }
        set_messages(line1, line2);
        return null;
    }
}

/// General-purpose shuttle status display.
// tg: code/game/machinery/status_display.dm:699
public partial class obj_machinery_status_display_shuttle : obj_machinery_status_display
{
    // tg: code/game/machinery/status_display.dm:707
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(shuttle_id) || Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            // No power, no processing.
            update_appearance();
            return PROCESS_KILL;
        }

        return display_shuttle_status(SSshuttle?.getShuttle(shuttle_id) as obj_docking_port_mobile);
    }

    // tg: code/game/machinery/status_display.dm:715
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = null;
        dot = await base.vv_edit_var(var_name, var_value);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.eq(var_name, nameof(shuttle_id)))
            update();
        return dot;
    }

    // tg: code/game/machinery/status_display.dm:723
    public override async DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        if (port != null)
            shuttle_id = port.shuttle_id;
        update();
        return null;
    }
}

/// Pictograph display which the AI can use to emote.
// tg: code/game/machinery/status_display.dm:730
public partial class obj_machinery_status_display_ai : obj_machinery_status_display
{
    // tg: code/game/machinery/status_display.dm:741
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        // register for radio system to receive AI emote signals
        SSradio.add_object(this, frequency);
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        return dot;
    }

    // tg: code/game/machinery/status_display.dm:748
    public override object Destroy(float? force = null)
    {
        SSradio.remove_object(this, frequency);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/status_display.dm:752
    public override async DmTask<object> attack_ai(mob user = null)
    {
        if (!isAI(user))
            return null;

        // Open the TGUI interface for selecting status display options
        mob_living_silicon_ai ai_user = user as mob_living_silicon_ai;
        ai_user.status_display_picker ??= Dm.@new<datum_ai_status_display_picker>(ai_user, this);
        await ai_user.status_display_picker.ui_interact(ai_user);
        return null;
    }

    // tg: code/game/machinery/status_display.dm:761
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            update_appearance();
            return PROCESS_KILL;
        }

        if (!Dm.truthy(length(GLOB.ai_status_display_all_options)))
            init_ai_status_display_options();

        object icon_state;
        // First try the combined list
        if (Dm.@in(emotion, GLOB.ai_status_display_all_options))
            icon_state = GLOB.ai_status_display_all_options[emotion];
        // Then try the original emotes list
        else if (Dm.@in(emotion, GLOB.ai_status_display_emotes))
            icon_state = GLOB.ai_status_display_emotes[emotion];
        // Default fallback
        else
            icon_state = "ai_download";

        set_picture(icon_state);
        return PROCESS_KILL;
    }

    // tg: code/game/machinery/status_display.dm:783
    public override object receive_signal(datum_signal signal = null)
    {
        object sw__1 = signal.data["command"];
        if (Dm.eq(sw__1, "friendcomputer"))
        {
            friendc = Dm.num(!Dm.truthy(friendc));
            if (Dm.truthy(friendc))
            {
                last_picture = emotion; // Save current display
                emotion = AI_EMOTION_FRIEND_COMPUTER;
            }
            else
            {
                if (Dm.truthy(last_picture))
                    emotion = last_picture as string; // Restore previous display
                else
                    emotion = AI_DISPLAY_DONT_GLOW;
            }
            update_appearance();
        }
        return null;
    }
}

// tg: code/game/machinery/status_display.dm:876
public partial class obj_machinery_status_display_random_message : obj_machinery_status_display
{
    // tg: code/game/machinery/status_display.dm:881
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(length(firstline_to_secondline)))
        {
            message1 = pick(firstline_to_secondline) as string;
            message2 = firstline_to_secondline[message1] as string;
        }
        return base.Initialize(mapload, __extra); // status displays call update appearance on init so i suppose we should set the messages before calling parent as to not call it twice
    }
}

/// Tracks all active greenscreen displays
// tg: code/game/machinery/status_display.dm:899  GLOBAL_LIST_EMPTY_TYPED(greenscreen_displays, /obj/effect/abstract/greenscreen_display)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/game/machinery/status_display.dm:899
    public DmList greenscreen_displays;

    // tg: code/game/machinery/status_display.dm:899  (GLOBAL_MANAGED(greenscreen_displays, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgreenscreen_displays()
    {
        greenscreen_displays = Dm.list();
        Dm.append(ref gvars_datum_init_order, "greenscreen_displays");
    }
}

/// Used to indicate where the greenscreen is recording
// tg: code/game/machinery/status_display.dm:902
public partial class obj_effect_abstract_greenscreen_location_indicator : obj_effect_abstract
{
    // tg: code/game/machinery/status_display.dm:908
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_filter("greenscreen_indicator", 1, outline_filter(1.5f, COLOR_YELLOW) as DmList);
        Dm.animate(get_filter("greenscreen_indicator"), Dm.named("alpha", 0), Dm.named("time", 2.5f * SECONDS), Dm.named("loop", -1));
        Dm.animate(Dm.named("alpha", 200), Dm.named("time", 1 * SECONDS));
        return dot;
    }
}

/// Basically exists to hold an appearance that we can slot into vis_contents
// tg: code/game/machinery/status_display.dm:915
public partial class obj_effect_abstract_greenscreen_appearance_holder : obj_effect_abstract
{
    // tg: code/game/machinery/status_display.dm:918
    [DmParams("mapload", "to_copy")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object to_copy = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (to_copy is atom_movable movable)
            copy_appearance(movable);
        return dot;
    }

    // tg: code/game/machinery/status_display.dm:923
    public virtual void copy_appearance(atom_movable from = null)
    {
        mutable_appearance copy = copy_appearance_filter_overlays(from.appearance);
        atom display = loc as atom;
        // if they have keep apart overlays we NEED to manually propogate the mask
        // (list() + copy.underlays + copy.overlays: the underlays, then the overlays; the port's copy holds them as mutable appearances)
        foreach (DmList layer_list in new[] { copy.underlays, copy.overlays })
        {
            foreach (image subcopy in Dm.each<image>(layer_list))
            {
                if (Dm.truthy(Dm.bitand(subcopy.appearance_flags, KEEP_APART)))
                {
                    object merged = subcopy.filters;
                    Dm.combine(ref merged, display.filters); // where loc = the display
                    subcopy.filters = merged;
                }
            }
        }
        appearance = copy;
        float display_dir = Dm.truthy(ISDIAGONALDIR(display.dir)) ? EWCOMPONENT(display.dir) : display.dir;
        float from_dir = Dm.truthy(ISDIAGONALDIR(from.dir)) ? EWCOMPONENT(from.dir) : from.dir;
        if (Dm.eq(display_dir, NORTH))
        {
            // it just works
        }
        else if (Dm.eq(display_dir, EAST))
            dir = turn(from_dir, 90);
        else if (Dm.eq(display_dir, SOUTH))
            dir = turn(from_dir, 180);
        else if (Dm.eq(display_dir, WEST))
            dir = turn(from_dir, 270);
        // appearance copies these so we need to manually set them
        vis_flags = Dm.bitor(vis_flags, Dm.bitor(VIS_INHERIT_PLANE, Dm.bitor(VIS_INHERIT_LAYER, VIS_INHERIT_ID))); // is vis_inherit recursive?
    }
}

/// Basically exists to compile a list of appearances to display to all status displays
// tg: code/game/machinery/status_display.dm:945
public partial class obj_effect_abstract_greenscreen_display : obj_effect_abstract
{
    // tg: code/game/machinery/status_display.dm:953
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(GLOB.greenscreen_displays, this);
        // crops out the bits that don't fit the screen
        add_filter("display_mask", 1, alpha_mask_filter(x: -1 * pixel_x, y: -1 * pixel_y, icon: Dm.icon(Dm.resource("icons/obj/machines/status_display.dmi"), "outline")) as DmList);
        // adds some pizzazz (copied from records)
        Dm.append(underlays, mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "static_base", alpha: 20));
        add_overlay(mutable_appearance(generate_icon_alpha_mask(Dm.resource("icons/effects/effects.dmi"), "scanline"), alpha: 20));
        return dot;
    }

    // tg: code/game/machinery/status_display.dm:962
    public override object Destroy(float? force = null)
    {
        foreach (atom_movable thing in Dm.each<atom_movable>(displaying))
            remove_from_display(thing);
        Dm.remove(GLOB.greenscreen_displays, this);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/status_display.dm:968
    public virtual void add_to_display(atom_movable thing = null)
    {
        if (Dm.truthy(displaying[thing]))
            return;

        displaying[thing] = Dm.@new<obj_effect_abstract_greenscreen_appearance_holder>(this, thing);
        RegisterSignals(thing, Dm.list(
            COMSIG_ATOM_POST_DIR_CHANGE,
            COMSIG_ATOM_UPDATED_ICON,
            COMSIG_CARBON_APPLY_OVERLAY,
            COMSIG_CARBON_REMOVE_OVERLAY,
            COMSIG_LIVING_POST_UPDATE_TRANSFORM
        ), nameof(thing_changed));
        RegisterSignal(thing, COMSIG_QDELETING, nameof(remove_from_display));
        Dm.append(vis_contents, displaying[thing]);
    }

    // tg: code/game/machinery/status_display.dm:983
    [SignalHandler]
    public virtual void remove_from_display(atom_movable thing = null)
    {
        if (!Dm.truthy(displaying[thing]))
            return;

        UnregisterSignal(thing, Dm.list(
            COMSIG_ATOM_POST_DIR_CHANGE,
            COMSIG_ATOM_UPDATED_ICON,
            COMSIG_CARBON_APPLY_OVERLAY,
            COMSIG_CARBON_REMOVE_OVERLAY,
            COMSIG_LIVING_POST_UPDATE_TRANSFORM,
            COMSIG_QDELETING
        ));
        Dm.remove(vis_contents, displaying[thing]);
        qdel(displaying[thing]);
        Dm.remove(displaying, thing);
    }

    // tg: code/game/machinery/status_display.dm:1001
    [SignalHandler]
    public virtual void thing_changed(atom_movable source = null)
    {
        obj_effect_abstract_greenscreen_appearance_holder holder = displaying[source] as obj_effect_abstract_greenscreen_appearance_holder;
        holder.copy_appearance(source);
    }
}

/// A stationary object which "records" anyone who is in front of it and broadcasts them to all status displays
// tg: code/game/machinery/status_display.dm:1008
public partial class obj_machinery_greenscreen_camera : obj_machinery
{
    // tg: code/game/machinery/status_display.dm:1030
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation), ROTATION_IGNORE_ANCHORED));
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/machinery/status_display.dm:1035
    public override object Destroy(float? force = null)
    {
        deactivate_feed();
        return base.Destroy(force);
    }

    // tg: code/game/machinery/status_display.dm:1039
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It's currently []broadcasting. <i>Click it to change that.</i>", display == null ? I18n.T("not ") : "")));
        return dot;
    }

    // tg: code/game/machinery/status_display.dm:1043
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (Dm.truthy(dot))
            return TRUE;

        toggle_feed();
        return TRUE;
    }

    // tg: code/game/machinery/status_display.dm:1051
    public virtual void toggle_feed()
    {
        if (display == null)
        {
            if (!Dm.truthy(is_operational))
                return;
            activate_feed();
            if (display == null)
            {
                playsound(this, Dm.resource("sound/machines/terminal/terminal_on.ogg"), 33, TRUE, frequency: 0.5f);
                balloon_alert_to_viewers(I18n.T("no backdrop, can't broadcast!"));
                return;
            }
            playsound(this, Dm.resource("sound/machines/terminal/terminal_on.ogg"), 33, FALSE);
            use_power = ACTIVE_POWER_USE;
        }
        else
        {
            deactivate_feed();
            playsound(this, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 33, FALSE);
            use_power = IDLE_POWER_USE;
        }
        balloon_alert_to_viewers(display == null ? I18n.T("feed deactivated") : I18n.T("feed activated"));
    }

    // tg: code/game/machinery/status_display.dm:1068
    public virtual void activate_feed()
    {
        greenscreen_turf = find_displayed_turf();
        if (greenscreen_turf == null)
            return;

        Dm.@new<obj_effect_abstract_greenscreen_location_indicator>(greenscreen_turf);
        mic = Dm.@new<obj_item_radio_entertainment_microphone>(this);
        // not seen, primarily for VV
        mic.name = Dm.text("[] microphone", name);
        // allows centcom cameras to reach the station + keeps transmission active even if comms are out
        mic.special_channels = Dm.bitor(mic.special_channels, RADIO_SPECIAL_CENTCOM);
        mic.set_frequency(FREQ_STATUS_DISPLAYS);
        display = Dm.@new<obj_effect_abstract_greenscreen_display>();
        display.dir = dir;
        RegisterSignal(greenscreen_turf, COMSIG_ATOM_ENTERED, nameof(turf_entered));
        RegisterSignal(greenscreen_turf, COMSIG_ATOM_EXITED, nameof(turf_exited));

        foreach (mob_living existing in Dm.each<mob_living>(greenscreen_turf.contents))
            turf_entered(greenscreen_turf, existing);

        become_hearing_sensitive("active");

        obj_effect_abstract_greenscreen_display shown = display;
        Waiting.DetachSync(() => update_status_displays(Dm.list(Dm.kv("command", "greenscreen"), Dm.kv("display", WEAKREF(shown)))));
        update_appearance(UPDATE_OVERLAYS);
    }

    /// Sends a signal to all relevant status displays to update their greenscreen
    // tg: code/game/machinery/status_display.dm:1094
    public virtual void update_status_displays(DmList signal_args = null)
    {
        // update the display on all status displays
        datum_radio_frequency frequency = SSradio.return_frequency(FREQ_STATUS_DISPLAYS);
        frequency?.post_signal(this, Dm.@new<datum_signal>(signal_args));
    }

    // tg: code/game/machinery/status_display.dm:1099
    public virtual void deactivate_feed()
    {
        if (greenscreen_turf != null)
        {
            foreach (obj_effect_abstract_greenscreen_location_indicator indicator in Dm.each<obj_effect_abstract_greenscreen_location_indicator>(greenscreen_turf.contents))
                qdel(indicator);
            UnregisterSignal(greenscreen_turf, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_EXITED));
            greenscreen_turf = null;
        }
        QDEL_NULL(ref mic);
        QDEL_NULL(ref display);

        lose_hearing_sensitivity("active");

        // there's another green screen to broadcast from, switch to it - otherwise, blank
        string command = Dm.truthy(length(GLOB.greenscreen_displays)) ? "greenscreen" : "shuttle";
        Waiting.DetachSync(() => update_status_displays(Dm.list(Dm.kv("command", command))));
        update_appearance(UPDATE_OVERLAYS);
    }

    /// Check if the passed atom can be shown on the display
    // tg: code/game/machinery/status_display.dm:1115
    public virtual float can_broadcast(atom_movable thing = null)
    {
        // blacklist underfloor, just in case
        // i would also blacklist invisible, but i figure it might be funny to see a ghost
        if (HAS_TRAIT(thing, TRAIT_UNDERFLOOR))
            return FALSE;
        // always show any mobs
        if (isliving(thing))
            return TRUE;
        // show things like wheelchairs, but not beds which are low
        if (Dm.truthy(thing.can_buckle) && (Dm.eq(thing.buckle_lying, NO_BUCKLE_LYING) || Dm.eq(thing.buckle_lying, 0)))
            return TRUE;
        return FALSE;
    }

    // tg: code/game/machinery/status_display.dm:1128
    [SignalHandler]
    public virtual void turf_entered(datum source = null, atom_movable entered = null)
    {
        if (Dm.truthy(can_broadcast(entered)))
            display.add_to_display(entered);
    }

    // tg: code/game/machinery/status_display.dm:1133
    [SignalHandler]
    public virtual void turf_exited(datum source = null, atom_movable exited = null)
    {
        display.remove_from_display(exited);
    }

    // tg: code/game/machinery/status_display.dm:1137
    public virtual turf find_displayed_turf()
    {
        DmList line = get_line(this, get_ranged_target_turf(this, dir, 5) as atom) as DmList;
        for (int i = 1; i <= length(line); i++)
        {
            turf current = line[(float)i] as turf;
            // found a greenscreen turf
            if (current is turf_open_floor_greenscreen)
                return current;
            // found any wall, or something we can't see through
            if (i != 1)
            {
                if (Dm.truthy(current.opacity))
                    return line[(float)(i - 1)] as turf;
                foreach (atom_movable thing in Dm.each<atom_movable>(current.contents))
                {
                    if (Dm.truthy(thing.opacity))
                        return line[(float)(i - 1)] as turf;
                }
            }
            // found a greenscreen poster
            obj_structure_sign_poster greenscreen = Dm.locate<obj_structure_sign_poster>(current);
            if (greenscreen == null)
                continue;
            // the poster is wallmounted, so return this turf
            // isProbablyWallMounted (__DEFINES/is_helpers.dm:332), inline (rules 11.1 (a))
            if (greenscreen.pixel_x > 20 || greenscreen.pixel_x < -20 || greenscreen.pixel_y > 20 || greenscreen.pixel_y < -20)
                return current;
            // the poster is clipped into a wall, so return the last turf
            if (i != 1)
                return line[(float)(i - 1)] as turf;
        }

        return null;
    }

    // tg: code/game/machinery/status_display.dm:1164
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (display == null)
            return dot;
        // stop the feed if we're moved
        deactivate_feed();
        return dot;
    }

    // tg: code/game/machinery/status_display.dm:1171
    public override async DmTask setDir(object newdir = null)
    {
        object old_dir = null; // tg never sets it: any turn of a broadcasting camera stops the feed
        await base.setDir(newdir);
        if (Dm.eq(dir, old_dir) || display == null)
            return;
        // stop the feed if we're rotated
        deactivate_feed();
    }

    // tg: code/game/machinery/status_display.dm:1179
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = await base.set_anchored(anchorvalue);
        if (Dm.truthy(anchored) || display == null)
            return dot;
        // stop the feed if we're unanchored
        deactivate_feed();
        return dot;
    }

    // tg: code/game/machinery/status_display.dm:1186
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        await base.on_set_is_operational(old_value);
        if (Dm.truthy(is_operational) || display == null)
            return;
        // no more power, no more feed
        // (uses toggle for feedback)
        toggle_feed();
    }

    // tg: code/game/machinery/status_display.dm:1194
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (display == null)
            Dm.append(ref dot, "camera_off");
        else
        {
            Dm.append(ref dot, "camera_on");
            // DEVIATION(DV12): . += emissive_appearance(icon, "camera_emissive", src, alpha = src.alpha), the glow on the emissive plane, is not created
        }
        return dot;
    }
}
