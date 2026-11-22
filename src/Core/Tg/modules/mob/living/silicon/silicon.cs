// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/silicon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI1, SI2 and SI4, D326; Topic came with the AI's tracking).

// tg: code/modules/mob/living/silicon/silicon.dm:1
public partial class mob_living_silicon : mob_living
{
    // tg: code/modules/mob/living/silicon/silicon.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        voice = SStts?.random_tts_voice() as string; // rules 10.6: a harness world may lack SStts (a booted world always has it)
        Dm.append(GLOB.silicon_mobs, this);
        add_faction(FACTION_SILICON);
        if (ispath(radio))
            radio = Dm.@new((Type)radio, this);
        datum_atom_hud_data_diagnostic diag_hud = GLOB.huds[DATA_HUD_DIAGNOSTIC] as datum_atom_hud_data_diagnostic;
        diag_hud.add_atom_to_hud(this);
        diag_hud_set_status();
        diag_hud_set_health();
        add_sensors();

        add_traits(Initialize__traits_to_apply, ROUNDSTART_TRAIT);
        ADD_TRAIT(this, TRAIT_SILICON_EMOTES_ALLOWED, INNATE_TRAIT);
        ADD_TRAIT(this, TRAIT_ANOSMIA, INNATE_TRAIT);
        RegisterSignal(this, COMSIG_LIVING_ELECTROCUTE_ACT, nameof(on_silicon_shocked));
        law_ui = Dm.@new<datum_state_laws_ui>(this);
        return dot;
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:62  var/static/list/traits_to_apply
    private static ref DmList Initialize__traits_to_apply => ref TgState.Current.mob_living_silicon__Initialize__traits_to_apply;

    // tg: code/modules/mob/living/silicon/silicon.dm:85
    public override object Destroy(float? force = null)
    {
        object radio__obj = radio;
        QDEL_NULL(ref radio__obj);
        radio = radio__obj;
        QDEL_NULL(ref aicamera);
        QDEL_NULL(ref builtInCamera);
        QDEL_NULL(ref laws);
        QDEL_NULL(ref modularInterface);
        Dm.remove(GLOB.silicon_mobs, this);
        QDEL_NULL(ref law_ui);
        return base.Destroy(force);
    }

    ///Sets cyborg gender from preferences. Expects a client.
    // tg: code/modules/mob/living/silicon/silicon.dm:96
    public virtual void set_gender(client player_client = null)
    {
        object silicon_pronouns = player_client.prefs.read_preference(typeof(datum_preference_choiced_silicon_gender));
        if (Dm.eq(silicon_pronouns, datum_preference_choiced_silicon_gender.use_character_gender))
        {
            gender = player_client.prefs.read_preference(typeof(datum_preference_choiced_gender)) as string;
            return;
        }
        object silicon_gender = datum_preference_choiced_silicon_gender.pronouns_to_genders[silicon_pronouns];
        if (!(silicon_gender == null))
            gender = silicon_gender as string;
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:105
    public virtual object on_silicon_shocked(datum source = null, object shock_damage = null, object shock_source = null, object siemens_coeff = null, object flags = null)
    {
        foreach (mob_living living_mob in Dm.each<mob_living>(buckled_mobs))
        {
            Waiting.NoSleep(unbuckle_mob(living_mob));
            Waiting.NoSleep(living_mob.electrocute_act(Dm.div(shock_damage, 100), shock_source, Dm.num_or_null(siemens_coeff), Dm.num_or_null(flags))); //Hard metal shell conducts!
        }

        return COMPONENT_LIVING_BLOCK_SHOCK; //So borgs don't die trying to fix wiring
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:113
    public virtual void create_modularInterface()
    {
        if (modularInterface == null)
            modularInterface = Dm.@new<obj_item_modular_computer_pda_silicon>(this);
        string job_name = "";
        if (isAI(this))
            job_name = "AI";
        if (ispAI(this))
            job_name = "pAI Messenger";

        modularInterface.layer = ABOVE_HUD_PLANE;
        SET_PLANE_EXPLICIT(modularInterface, ABOVE_HUD_PLANE, this);
        modularInterface.imprint_id(Dm.truthy(real_name) ? real_name : name, job_name);
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:132
    public override void med_hud_set_health()
    {
        return; //we use a different hud
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:135
    public override object med_hud_set_status()
    {
        return null; //we use a different hud
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:138
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        return;
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:141
    public virtual void queueAlarm(object message = null, object type = null, float? incoming = null)
    {
        incoming ??= FALSE;
        bool in_cooldown = (Dm.truthy(Dm.length(alarms_to_show)) || Dm.truthy(Dm.length(alarms_to_clear)));
        if (Dm.truthy(incoming))
        {
            Dm.append(alarms_to_show, message);
            alarm_types_show[type] = Dm.add(alarm_types_show[type], 1);
        }
        else
        {
            Dm.append(alarms_to_clear, message);
            alarm_types_clear[type] = Dm.add(alarm_types_clear[type], 1);
        }

        if (in_cooldown)
            return;

        _addtimer(Dm.@new<datum_callback>(this, nameof(show_alarms)), 3 * SECONDS, file: "code/modules/mob/living/silicon/silicon.dm", line: 153);
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:155
    public virtual void show_alarms()
    {
        if (Dm.length(alarms_to_show) < 5)
        {
            foreach (object msg in Dm.each(alarms_to_show))
                to_chat(this, msg);
        }
        else if (Dm.truthy(Dm.length(alarms_to_show)))
        {
            object msg = "--- ";
            foreach (object alarm_type in Dm.each(alarm_types_show))
                Dm.append(ref msg, I18n.T("[]: [] alarms detected. - ", uppertext(alarm_type), alarm_types_show[alarm_type]));

            Dm.append(ref msg, Dm.text("<A href=byond://?src=[];showalerts=1'>\\[Show Alerts\\]</a>", REF(this)));
            to_chat(this, msg);
        }

        if (Dm.length(alarms_to_clear) < 3)
        {
            foreach (object msg in Dm.each(alarms_to_clear))
                to_chat(this, msg);
        }
        else if (Dm.truthy(Dm.length(alarms_to_clear)))
        {
            object msg = "--- ";

            foreach (object alarm_type in Dm.each(alarm_types_clear))
                Dm.append(ref msg, I18n.T("[]: [] alarms cleared. - ", uppertext(alarm_type), alarm_types_clear[alarm_type]));

            Dm.append(ref msg, Dm.text("<A href=byond://?src=[];showalerts=1'>\\[Show Alerts\\]</a>", REF(this)));
            to_chat(this, msg);
        }


        alarms_to_show.Cut();
        alarms_to_clear.Cut();
        foreach (object key in Dm.each(alarm_types_show))
            alarm_types_show[key] = 0;
        foreach (object key in Dm.each(alarm_types_clear))
            alarm_types_clear[key] = 0;
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:189
    public override object can_inject(mob user = null, object target_zone = null, object injection_flags = null)
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:192
    public override object try_inject(mob user = null, object target_zone = null, object injection_flags = null)
    {
        object dot = base.try_inject(user, target_zone, injection_flags);
        if (!Dm.truthy(dot) && Dm.truthy(Dm.bitand(injection_flags, INJECT_TRY_SHOW_ERROR_MESSAGE)))
            to_chat(user, span_alert(I18n.T("[] outer shell is too tough.", p_Their())));
        return dot;
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:231
    public virtual void statelaws(float? force = null)
    {
        force ??= FALSE;
        law_ui.state_laws(force);
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:234
    public virtual async DmTask checklaws()
    {
        await law_ui.ui_interact(this);
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:237
    public virtual async DmTask ai_roster()
    {
        if (client == null)
            return;
        await GLOB.manifest.ui_interact(this);
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:242
    public virtual async DmTask set_autosay() //For allowing the AI and borgs to set the radio behavior of auto announcements (state laws, arrivals).
    {
        obj_item_radio_borg radio = this.radio as obj_item_radio_borg;
        if (radio == null)
        {
            to_chat(this, span_alert(I18n.T("Radio not detected.")));
            return;
        }

        //Ask the user to pick a channel from what it has available.
        object chosen_channel = await tgui_input_list(usr, "Select a channel", "Channel Selection", Dm.add(Dm.list("Default", "None"), radio.channels) as DmList);
        if (chosen_channel == null)
            return;
        if (Dm.eq(chosen_channel, "Default")) //Autospeak on whatever frequency to which the radio is set, usually Common.
        {
            radiomod = ";";
            Dm.append(ref chosen_channel, Dm.text(" ([])", radio.get_frequency()));
        }
        if (Dm.eq(chosen_channel, "None")) //Prevents use of the radio for automatic annoucements.
            radiomod = "";
        else //For department channels, if any, given by the internal radio.
        {
            foreach (object key in Dm.each(GLOB.department_radio_keys))
            {
                if (Dm.eq(GLOB.department_radio_keys[key], chosen_channel))
                {
                    radiomod = ":" + Dm.str(key);
                    break;
                }
            }
        }

        to_chat(this, span_notice(I18n.T("Automatic announcements []", Dm.eq(chosen_channel, "None") ? I18n.T("will not use the radio.") : I18n.T("set to [].", chosen_channel))));
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:264
    public override object put_in_hand_check(obj_item I = null) // This check is for borgs being able to receive items, not put them in others' hands.
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:267
    public override object assess_threat(object judgement_criteria = null, string lasercolor = null, datum_callback weaponcheck = null) //Secbots won't hunt silicon units
    {
        lasercolor ??= "";
        return -10;
    }

    /// Innate, toggleable silicon HUDs
    // tg: code/modules/mob/living/silicon/silicon.dm:271  #define SILICON_HUD_TRAIT (#undef at :280)
    private const string SILICON_HUD_TRAIT = "silicon_hud";

    // tg: code/modules/mob/living/silicon/silicon.dm:273
    public virtual void remove_sensors()
    {
        remove_traits(silicon_huds, SILICON_HUD_TRAIT);
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:276
    public virtual void add_sensors()
    {
        add_traits(silicon_huds, SILICON_HUD_TRAIT);
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:281
    public virtual void toggle_sensors()
    {
        if (Dm.truthy(incapacitated))
            return;
        sensors_on = Dm.truthy(sensors_on) ? FALSE : TRUE;
        if (!Dm.truthy(sensors_on))
        {
            to_chat(this, span_notice(I18n.T("Sensor overlay deactivated.")));
            remove_sensors();
            return;
        }
        add_sensors();
        to_chat(this, span_notice(I18n.T("Sensor overlay activated.")));
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:292
    public virtual async DmTask<object> GetPhoto(mob user = null)
    {
        if (aicamera != null)
            return await aicamera.selectpicture(user);
        return null;
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:296
    public override object get_inactive_held_item()
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:299
    public override async DmTask handle_high_gravity(object gravity = null, object seconds_per_tick = null)
    {
        return;
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:302
    public override async DmTask<object> rust_heretic_act(object rust_strength = null)
    {
        await adjust_brute_loss(500);
        return TRUE;
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:306
    public override async DmTask on_floored_start()
    {
        return; // Silicons are always standing by default.
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:309
    public override void on_floored_end()
    {
        return; // Silicons are always standing by default.
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:312
    public override async DmTask on_lying_down(object new_lying_angle = null)
    {
        return; // Silicons are always standing by default.
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:315
    public override async DmTask on_standing_up(params object[] __extra)
    {
        return; // Silicons are always standing by default.
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:318
    public override object get_butt_sprite()
    {
        return icon(Dm.resource("icons/mob/butts.dmi"), BUTT_SPRITE_QR_CODE);
    }

    /**
     * Records an IC event log entry in the cyborg's internal tablet.
     *
     * Creates an entry in the borglog list of the cyborg's internal tablet (if it's a borg), listing the current
     * in-game time followed by the message given. These logs can be seen by the cyborg in their
     * BorgUI tablet app. By design, logging fails if the cyborg is dead.
     *
     * (This used to be in robot.dm. It's in here now.)
     *
     * Arguments:
     * arg1: a string containing the message to log.
     */
    // tg: code/modules/mob/living/silicon/silicon.dm:333
    public virtual async DmTask logevent(string @string = null)
    {
        @string ??= "";
        if (!Dm.truthy(@string))
            return;
        if (stat == DEAD) //Dead silicons log no longer
            return;
        if (modularInterface == null)
        {
            stack_trace(Dm.text("Silicon [] ( [] ) was somehow missing their integrated tablet. Please make a bug report.", this, GetType()), "code/modules/mob/living/silicon/silicon.dm", 339);
            create_modularInterface();
        }
        if (modularInterface.silicon_owner is mob_living_silicon_robot)
            Dm.append(ref modularInterface.borglog, Dm.text("[] - []", round_timestamp(), @string));
        datum_computer_file_program_robotact program = modularInterface.get_robotact() as datum_computer_file_program_robotact;
        if (program != null)
        {
            datum_tgui active_ui = SStgui?.get_open_ui(this, program.computer) as datum_tgui; // rules 10.6: a test harness world may lack SStgui
            if (active_ui != null)
                await active_ui.send_full_update();
        }
    }

    /// Same as the normal character name replacement, but updates the contents of the modular interface.
    // tg: code/modules/mob/living/silicon/silicon.dm:351
    public override async DmTask<object> fully_replace_character_name(object oldname = null, object newname = null, float? log_new_name = null)
    {
        log_new_name ??= FALSE;
        object dot = await base.fully_replace_character_name(oldname, newname, log_new_name);
        if (!Dm.truthy(dot))
            return dot;
        if (modularInterface == null)
        {
            stack_trace(Dm.text("Silicon [] ( [] ) was somehow missing their integrated tablet. Please make a bug report.", this, GetType()), "code/modules/mob/living/silicon/silicon.dm", 356);
            create_modularInterface();
        }
        modularInterface.imprint_id(name: newname);
        return dot;
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:360
    public override object can_track(mob_living user = null)
    {
        //if their camera is online, it's safe to assume they are in cameranets
        //since it takes a while for camera vis to update, this lets us bypass that so AIs can always see their borgs,
        //without making cameras constantly update every time a borg moves.
        if (builtInCamera != null && Dm.truthy(builtInCamera.can_use()))
            return TRUE;
        return base.can_track(user);
    }

    ///Places laws on the status panel for silicons
    // tg: code/modules/mob/living/silicon/silicon.dm:369
    public override object get_status_tab_items()
    {
        object dot = base.get_status_tab_items();
        DmList law_list = Dm.list("Obey these laws:");
        Dm.append(law_list, laws.get_law_list(include_zeroth: TRUE, render_html: FALSE));
        foreach (object borg_laws in Dm.each(law_list))
            Dm.append(ref dot, borg_laws);
        return dot;
    }

    // tg: code/modules/mob/living/silicon/silicon.dm:376
    public override object get_access()
    {
        return SSid_access.accesses_by_region[REGION_ALL_STATION];
    }
}

// tg: code/modules/mob/living/silicon/silicon.dm:197
public static partial class GlobalProcs
{
    // tg: code/modules/mob/living/silicon/silicon.dm:197
    public static object islinked(mob_living_silicon_robot bot = null, mob_living_silicon_ai ai = null)
    {
        if (!(bot is mob_living_silicon_robot) || !(ai is mob_living_silicon_ai))
            return FALSE;
        if (bot.connected_ai == ai)
            return TRUE;
        return FALSE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/silicon/silicon.dm:62  var/static/list/traits_to_apply
    internal DmList mob_living_silicon__Initialize__traits_to_apply = Dm.list(
        TRAIT_ADVANCEDTOOLUSER,
        TRAIT_ASHSTORM_IMMUNE,
        TRAIT_BRAWLING_KNOCKDOWN_BLOCKED,
        TRAIT_FENCE_CLIMBER,
        TRAIT_LITERATE,
        TRAIT_MADNESS_IMMUNE,
        TRAIT_MARTIAL_ARTS_IMMUNE,
        TRAIT_NEVER_CONSIDERED_ALIVE,
        TRAIT_NOFIRE_SPREAD,
        TRAIT_NO_SLIP_ALL,
        TRAIT_REAGENT_SCANNER,
        TRAIT_SILICON_ACCESS,
        TRAIT_UNOBSERVANT);
}

// tg: code/modules/mob/living/silicon/silicon.dm:204  (silicons SI4: the laws' and the tracking links)
public partial class mob_living_silicon : mob_living
{
    // tg: code/modules/mob/living/silicon/silicon.dm:204
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        if (Dm.truthy(href_list["laws"]))
            statelaws();

        if (Dm.truthy(href_list["printlawtext"])) // this is kinda backwards
        {
            mob user = usr as mob;
            if (Dm.truthy(href_list["dead"]) && (!isdead(user) && user.client.holder == null)) // do not print deadchat law notice if the user is now alive
            {
                to_chat(user, span_warning(I18n.T("You cannot view law changes that were made while you were dead.")));
                return null;
            }
            to_chat(user, href_list["printlawtext"]);
        }

        if (Dm.truthy(href_list["track"]))
        {
            if (!Dm.truthy(can_track(href_list["track"] as mob_living)))
            {
                to_chat(this, span_info(I18n.T("This person is not currently on cameras.")));
                return null;
            }
            mob_living_silicon_ai AI;
            mob_living_silicon_robot shell;
            if (!isAI(this))
            {
                shell = this as mob_living_silicon_robot;
                AI = shell.mainframe;
                await AI.deployed_shell.undeploy();
            }
            else
                AI = this as mob_living_silicon_ai;

            AI.ai_tracking_tool.track_name(this, href_list["track"]);
        }

        return null;
    }
}

