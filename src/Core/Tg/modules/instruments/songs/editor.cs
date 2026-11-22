// Translated from tgstation (snapshot 1d99bd7f): code/modules/instruments/songs/editor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6c (D326): the whole file (the InstrumentEditor window).

public partial class datum_song : datum
{
    // tg: code/modules/instruments/songs/editor.dm:1
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "InstrumentEditor", parent.name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/instruments/songs/editor.dm:7
    public override object ui_host(mob user = null) => parent;

    // tg: code/modules/instruments/songs/editor.dm:10
    public override object ui_state(mob user = null)
    {
        if (ispAI(user))
            return GLOB.always_state;
        return base.ui_state(user);
    }

    // tg: code/modules/instruments/songs/editor.dm:15
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = await base.ui_data(user) as DmList ?? Dm.list();
        data["id"] = id;
        data["using_instrument"] = Dm.truthy(using_instrument?.name) ? using_instrument.name : "No instrument loaded!";
        data["note_shift"] = note_shift;
        data["octaves"] = round(note_shift / 12, 0.01f);
        data["sustain_mode"] = sustain_mode;
        switch (sustain_mode)
        {
            case SUSTAIN_LINEAR:
                data["sustain_mode_button"] = "Linear Sustain Duration (in seconds)";
                data["sustain_mode_duration"] = sustain_linear_duration / 10;
                data["sustain_mode_min"] = INSTRUMENT_MIN_TOTAL_SUSTAIN;
                data["sustain_mode_max"] = INSTRUMENT_MAX_TOTAL_SUSTAIN;
                break;
            case SUSTAIN_EXPONENTIAL:
                data["sustain_mode_button"] = "Exponential Falloff Factor (% per decisecond)";
                data["sustain_mode_duration"] = sustain_exponential_dropoff;
                data["sustain_mode_min"] = INSTRUMENT_EXP_FALLOFF_MIN;
                data["sustain_mode_max"] = INSTRUMENT_EXP_FALLOFF_MAX;
                break;
        }
        data["instrument_ready"] = using_instrument?.ready();
        data["volume"] = volume;
        data["volume_dropoff_threshold"] = sustain_dropoff_volume;
        data["sustain_indefinitely"] = full_sustain_held_note;
        data["playing"] = playing;
        data["repeat"] = repeat;
        data["bpm"] = round(60 * SECONDS / tempo);
        DmList lineData = Dm.list();
        int linecount = 0;
        foreach (object line in Dm.each(lines))
        {
            linecount++;
            lineData.Add(Dm.list(Dm.list(Dm.kv("line_count", linecount), Dm.kv("line_text", line))));
        }
        data["lines"] = lineData;
        return data;
    }

    // tg: code/modules/instruments/songs/editor.dm:50
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = await base.ui_static_data(user) as DmList ?? Dm.list();
        data["can_switch_instrument"] = Dm.length(allowed_instrument_ids) > 1 ? TRUE : FALSE;
        DmList possible = Dm.list();
        foreach (object instrument in Dm.each(allowed_instrument_ids))
            possible.Add(Dm.list(Dm.list(Dm.kv("name", SSinstruments.instrument_data[instrument]), Dm.kv("id", instrument)))); // UNTYPED_LIST_ADD
        data["possible_instruments"] = possible;
        data["sustain_modes"] = SSinstruments.note_sustain_modes;
        data["max_repeats"] = max_repeats;
        data["min_volume"] = min_volume;
        data["max_volume"] = max_volume;
        data["note_shift_min"] = note_shift_min;
        data["note_shift_max"] = note_shift_max;
        data["max_line_chars"] = datum_controller_subsystem_processing_instruments.musician_maxlinechars;
        data["max_lines"] = datum_controller_subsystem_processing_instruments.musician_maxlines;
        return data;
    }

    // tg: code/modules/instruments/songs/editor.dm:66
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        mob user = ui.user;
        if (user == null)
            return FALSE;
        const float maxChars = datum_controller_subsystem_processing_instruments.musician_maxlines * datum_controller_subsystem_processing_instruments.musician_maxlinechars;

        switch (Dm.str(action))
        {
            //SETTINGS
            case "play_music":
                if (!Dm.truthy(playing))
                    start_playing(user); // INVOKE_ASYNC: start_playing does not sleep, so the asynchronous call is a call
                else
                    stop_playing();
                return TRUE;
            case "set_instrument_id":
            {
                object new_id = reject_bad_name(LOWER_TEXT(@params["id"]), max_length: 20, allow_numbers: TRUE, cap_after_symbols: FALSE);
                if (Dm.truthy(new_id))
                    id = Dm.str(new_id);
                return TRUE;
            }
            case "change_instrument":
            {
                object new_instrument = @params["new_instrument"];
                //only one instrument, so no need to bother changing it.
                if (Dm.length(allowed_instrument_ids) == 0)
                    return FALSE;
                if (!Dm.@in(new_instrument, allowed_instrument_ids))
                    return FALSE;
                set_instrument(new_instrument);
                return TRUE;
            }
            case "tempo":
            {
                object move_direction = @params["tempo_change"];
                float tempo_diff;
                if (Dm.eq(move_direction, "increase_speed"))
                    tempo_diff = world.tick_lag;
                else
                    tempo_diff = -world.tick_lag;
                tempo = sanitize_tempo(tempo + tempo_diff);
                return TRUE;
            }

            //SONG MAKING
            case "import_song":
            {
                object song_text = "";
                do
                {
                    song_text = await tgui_input_text(user, I18n.T("Please paste the entire song, formatted:"), name, max_length: maxChars, multiline: TRUE);
                    if (!in_range(parent, user))
                        return null;

                    if (Dm.length(Dm.str(song_text)) >= maxChars)
                    {
                        object should_continue = await tgui_alert(user, I18n.T("Your message is too long! Would you like to continue editing it?"), I18n.T("Warning"), Dm.list("Yes", "No"));
                        if (!Dm.eq(should_continue, "Yes"))
                            break;
                    }
                }
                while (Dm.length(Dm.str(song_text)) > maxChars);
                ParseSong(user, song_text);
                return TRUE;
            }
            case "start_new_song":
                name = "";
                lines = Dm.list();
                tempo = sanitize_tempo(5); // default 120 BPM
                return TRUE;
            case "add_new_line":
            {
                object newline = await tgui_input_text(user, I18n.T("Enter your line"), parent.name, max_length: datum_controller_subsystem_processing_instruments.musician_maxlinechars);
                if (!Dm.truthy(newline) || !in_range(parent, user))
                    return null;
                if (Dm.length(lines) > datum_controller_subsystem_processing_instruments.musician_maxlines)
                    return null;
                string line = Dm.str(newline);
                if (line.Length > datum_controller_subsystem_processing_instruments.musician_maxlinechars)
                    line = Dm.str(copytext(line, 1, datum_controller_subsystem_processing_instruments.musician_maxlinechars));
                lines.Add(line);
                return dot;
            }
            case "delete_line":
            {
                float line_to_delete = Dm.num(@params["line_deleted"]);
                if (line_to_delete > Dm.length(lines) || line_to_delete < 1)
                    return FALSE;
                lines.Cut((int)line_to_delete, (int)line_to_delete + 1);
                return TRUE;
            }
            case "modify_line":
            {
                float line_to_edit = Dm.num(@params["line_editing"]);
                if (line_to_edit > Dm.length(lines) || line_to_edit < 1)
                    return FALSE;
                object new_line_text = await tgui_input_text(user, I18n.T("Enter your line "), parent.name, lines[(int)line_to_edit], max_length: datum_controller_subsystem_processing_instruments.musician_maxlinechars);
                if (new_line_text == null || !in_range(parent, user))
                    return FALSE;
                lines[(int)line_to_edit] = new_line_text;
                return TRUE;
            }

            //MODE STUFF
            case "set_sustain_mode":
            {
                object new_mode = @params["new_mode"];
                if (new_mode == null || !Dm.@in(new_mode, SSinstruments.note_sustain_modes))
                    return FALSE;
                sustain_mode = Dm.str(new_mode);
                return TRUE;
            }
            case "set_note_shift":
            {
                object amount = @params["amount"];
                if (!isnum(amount))
                    return FALSE;
                note_shift = Math.Clamp(Dm.num(amount), note_shift_min, note_shift_max);
                return TRUE;
            }
            case "set_volume":
            {
                object new_volume = @params["amount"];
                if (!isnum(new_volume))
                    return FALSE;
                set_volume(new_volume);
                return TRUE;
            }
            case "set_dropoff_volume":
            {
                object dropoff_threshold = @params["amount"];
                if (!isnum(dropoff_threshold))
                    return FALSE;
                set_dropoff_volume(dropoff_threshold);
                return TRUE;
            }
            case "toggle_sustain_hold_indefinitely":
                full_sustain_held_note = !Dm.truthy(full_sustain_held_note) ? TRUE : FALSE;
                return TRUE;
            case "set_repeat_amount":
            {
                if (Dm.truthy(playing))
                    return null;
                object repeat_amount = @params["amount"];
                if (!isnum(repeat_amount))
                    return FALSE;
                set_repeats(repeat_amount);
                return TRUE;
            }
            case "edit_sustain_mode":
            {
                object sustain_amount = @params["amount"];
                if (sustain_amount == null || !isnum(sustain_amount))
                    return null;
                switch (sustain_mode)
                {
                    case SUSTAIN_LINEAR:
                        set_linear_falloff_duration(sustain_amount);
                        break;
                    case SUSTAIN_EXPONENTIAL:
                        set_exponential_drop_rate(sustain_amount);
                        break;
                }
                return dot;
            }
        }
        return dot;
    }

    /**
     * Parses a song the user has input into lines and stores them.
     */
    // tg: code/modules/instruments/songs/editor.dm:198  (set waitfor = FALSE: it does not sleep, so the call is a call)
    public virtual void ParseSong(mob user = null, object new_song = null)
    {
        //split into lines
        lines = new_song is DmList list ? list : splittext(new_song, "\n") as DmList;
        if (Dm.length(lines) > 0)
        {
            const string bpm_string = "BPM: ";
            if (Dm.truthy(findtext(lines[1], bpm_string, 1, bpm_string.Length + 1)))
            {
                float divisor = Dm.num(text2num(copytext(lines[1], bpm_string.Length + 1)));
                if (divisor == 0)
                    divisor = 120; // default
                tempo = sanitize_tempo(600 / round(divisor, 1)); // BPM_TO_TEMPO_SETTING(divisor) (__DEFINES/song.dm:1)
                lines.Cut(1, 2);
            }
            else
                tempo = sanitize_tempo(5); // default 120 BPM
            if (Dm.length(lines) > datum_controller_subsystem_processing_instruments.musician_maxlines)
            {
                if (user != null)
                    to_chat(user, I18n.T("Too many lines!"));
                lines.Cut((int)datum_controller_subsystem_processing_instruments.musician_maxlines + 1);
            }
            int linenum = 1;
            foreach (object l in Dm.each(lines.Copy()))
            {
                if (Dm.str(l).Length > datum_controller_subsystem_processing_instruments.musician_maxlinechars)
                {
                    if (user != null)
                        to_chat(user, I18n.T("Line [] too long!", linenum));
                    lines.Remove(l);
                }
                else
                    linenum++;
            }
        }
    }
}
