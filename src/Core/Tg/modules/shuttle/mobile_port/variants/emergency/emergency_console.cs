// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// D299: the console's early launch authorization, its window and its emag. Syndicate A2: the hijack's stages (an antagonist with a hijack
// speed reprograms the console stage by stage, the scrambled announcements, the delayed flight); N1b: the syndicate base's lazy load that
// a hijack starts early.

// tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:6
public partial class obj_machinery_computer_emergency_shuttle : obj_machinery_computer
{
    private const float SHUTTLE_CONSOLE_ACTION_DELAY = 5 * SECONDS;

    private static bool ENGINES_STARTED => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_IGNITING);
    private static bool IS_DOCKED => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_DOCKED) || ENGINES_STARTED;
    private static object TIME_LEFT => SSshuttle.emergency.timeLeft();

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:25
    public override object Destroy(float? force = null)
    {
        // Our fake IDs that the emag generated are just there for colour
        // They're not supposed to be accessible
        foreach (obj_item_card_id ID in Dm.each<obj_item_card_id>(this))
            qdel(ID);
        if (Dm.truthy(authorized?.len))
            authorized.Cut();
        authorized = null;

        return base.Destroy(force);
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:37
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        var dot = (DmList)await base.examine(user, __extra);
        if (Dm.truthy(hijack_announce))
            Dm.append(ref dot, span_danger("Security systems present on console. Any unauthorized tampering will result in an emergency announcement."));
        if (Dm.truthy(user?.mind?.get_hijack_speed()))
        {
            Dm.append(ref dot, span_danger(I18n.T("Alt click on this to attempt to hijack the shuttle. This will take multiple tries (current: stage []/[]).", (SSshuttle.emergency as obj_docking_port_mobile_emergency).hijack_status, HIJACK_COMPLETED)));
            Dm.append(ref dot, span_notice(I18n.T("It will take you [] seconds to reprogram a stage of the shuttle's navigational firmware, and the console will undergo automated timed lockout for [] seconds after each stage.", hijack_stage_time * Dm.num(user.mind.get_hijack_speed()) / 10, hijack_stage_cooldown / 10)));
            if (Dm.truthy(hijack_announce))
                Dm.append(ref dot, span_warning(I18n.T("It is probably best to fortify your position as to be uninterrupted during the attempt, given the automatic announcements..")));
        }
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:46
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!isidcard(tool))
            return NONE;
        await say("Please equip your ID card into your ID slot to authenticate.");
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:52
    public override datum_ui_state ui_state(mob user = null) => GLOB.human_adjacent_state;

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:55
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "EmergencyShuttleConsole", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:62
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["timer_str"] = SSshuttle.emergency.getTimerStr();
        data["engines_started"] = ENGINES_STARTED ? TRUE : FALSE;
        data["authorizations_remaining"] = Math.Max(auth_need - authorized.len, 0);
        DmList A = Dm.list();
        bool emagged = Dm.truthy(Dm.bitand(obj_flags, EMAGGED));
        foreach (obj_item_card_id ID in Dm.each<obj_item_card_id>(authorized))
        {
            object id_name = ID.registered_name;
            object job = ID.assignment;

            if (emagged)
            {
                id_name = Gibberish(id_name);
                job = Gibberish(job);
            }
            Dm.append(ref A, Dm.list(Dm.list(Dm.kv("name", id_name), Dm.kv("job", job))));
        }
        data["authorizations"] = A;

        data["enabled"] = IS_DOCKED && !ENGINES_STARTED && !Dm.@in(user, acted_recently) ? TRUE : FALSE;
        data["emagged"] = emagged ? 1 : 0;
        return data;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:84
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (ENGINES_STARTED) // past the point of no return
            return null;
        if (!IS_DOCKED) // shuttle computer only has uses when onstation
            return null;
        if (Dm.eq(SSshuttle.emergency.mode, SHUTTLE_DISABLED)) // admins have disabled the shuttle.
            return null;
        if (usr is not mob_living user)
            return null;

        if (get_area(this) is not area_shuttle_escape)
        {
            await say("Error - Network connectivity: Console has lost connection to the shuttle.");
            return null;
        }

        dot = FALSE;

        var ID = user.get_idcard(TRUE) as obj_item_card_id;

        if (ID == null)
        {
            to_chat(user, span_warning("You don't have an ID."));
            return dot;
        }

        if (!Dm.@in(ACCESS_COMMAND, ID.access))
        {
            to_chat(user, span_warning("The access level of your card is not high enough."));
            return dot;
        }

        if (Dm.@in(user, acted_recently))
            return dot;

        float old_len = length(authorized);
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_recent_action), user), SHUTTLE_CONSOLE_ACTION_DELAY);

        switch (Dm.str(action))
        {
            case "authorize":
                dot = authorize(user);
                break;
            case "repeal":
                Dm.remove(authorized, ID);
                break;
            case "abort":
                if (Dm.truthy(length(authorized)))
                {
                    // Abort. The action for when heads are fighting over whether to launch early.
                    authorized.Cut();
                    dot = TRUE;
                }
                break;
        }

        float new_len = length(authorized);
        if (old_len != new_len && !ENGINES_STARTED)
        {
            bool repeal = new_len < old_len;
            float remaining = Math.Max(0, auth_need - new_len);
            if (new_len > 0 && remaining > 0)
            {
                priority_announce(
                    Dm.text("[] authorization[] needed until shuttle is launched early.", remaining, remaining == 1 ? "" : "s"),
                    "Emergency Shuttle Status",
                    sound: Dm.resource("sound/announcer/notice/notice1.ogg"),
                    type: ANNOUNCEMENT_TYPE_PRIORITY,
                    has_important_message: TRUE,
                    color_override: "red");
            }
            if (repeal)
            {
                priority_announce(
                    Dm.text("Early launch authorization revoked, [] authorization[] needed.", remaining, remaining == 1 ? "" : "s"),
                    "Emergency Shuttle Status",
                    sound: Dm.resource("sound/announcer/notice/notice2.ogg"),
                    type: ANNOUNCEMENT_TYPE_PRIORITY,
                    color_override: "blue");
            }
        }

        Dm.append(ref acted_recently, user);
        SStgui.update_user_uis(user, this);
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:160
    public virtual object authorize(mob_living user = null, object source = null)
    {
        var ID = (obj_item_card_id)user.get_idcard(TRUE);

        if (Dm.@in(ID, authorized))
            return FALSE;
        foreach (obj_item_card_id other in Dm.each<obj_item_card_id>(authorized))
        {
            if (Dm.eq(other.registered_name, ID.registered_name))
                return FALSE; // No using IDs with the same name
        }

        Dm.append(ref authorized, ID);

        message_admins(Dm.text("[] has authorized early shuttle launch", key_name(user)));
        log_shuttle(Dm.text("[] has authorized early shuttle launch in [],[],[]", key_name(user), x, y, z));
        // Now check if we're on our way
        process(SSmachines.wait * 0.1f);
        return TRUE;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:178
    public virtual void clear_recent_action(mob user = null)
    {
        Dm.remove(acted_recently, user);
        if (!QDELETED(user))
            SStgui.update_user_uis(user, this);
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:183
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        // Launch check is in process in case auth_need changes for some reason
        // probably external.
        if (SSshuttle.emergency == null)
            return FALSE;

        if (Dm.eq(SSshuttle.emergency.mode, SHUTTLE_STRANDED))
        {
            authorized.Cut();
            obj_flags = Dm.bitand(obj_flags, Dm.bitnot(EMAGGED));
        }

        if (ENGINES_STARTED || !IS_DOCKED)
            return FALSE;

        // Check to see if we've reached criteria for early launch
        bool emagged = Dm.truthy(Dm.bitand(obj_flags, EMAGGED));
        if (authorized.len >= auth_need || emagged)
        {
            // shuttle timers use 1/10th seconds internally
            SSshuttle.emergency.setTimer(ENGINE_START_TIME);
            minor_announce(Dm.text("The emergency shuttle will launch in [] seconds", TIME_LEFT), emagged ? "SYSTEM ERROR:" : null, alert: TRUE);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:206
    public virtual async DmTask<object> increase_hijack_stage()
    {
        obj_docking_port_mobile_emergency shuttle = SSshuttle.emergency as obj_docking_port_mobile_emergency;
        // Begin loading this early, prevents a delay when the shuttle goes to land
        Waiting.Detach(SSmapping.lazy_load_template(LAZY_TEMPLATE_KEY_NUKIEBASE)); // INVOKE_ASYNC

        shuttle.hijack_status++;
        if (Dm.truthy(hijack_announce))
            await announce_hijack_stage();
        hijack_last_stage_increase = world.time;
        await say("Navigational protocol error! Rebooting systems.");
        if (Dm.eq(shuttle.mode, SHUTTLE_ESCAPE))
        {
            if (shuttle.hijack_status == HIJACK_COMPLETED)
                shuttle.setTimer(hijack_completion_flight_time_set);
            else
                shuttle.setTimer(Dm.num(shuttle.timeLeft(1)) + hijack_flight_time_increase); //give the guy more time to hijack if it's already in flight.
        }
        return shuttle.hijack_status;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:223
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (user is not mob_living living)
            return NONE;
        await attempt_hijack_stage(living);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:229
    public virtual async DmTask<object> attempt_hijack_stage(mob_living user = null)
    {
        if (!Dm.truthy(await IsReachableBy(user)))
            return null;
        if (HAS_TRAIT(user, TRAIT_HANDS_BLOCKED))
        {
            to_chat(user, span_warning(I18n.T("You need your hands free before you can manipulate [].", this)));
            return null;
        }
        area my_area = get_area(this) as area;
        if (my_area is not area_shuttle_escape)
        {
            await say("Error - Network connectivity: Console has lost connection to the shuttle.");
            return null;
        }
        if (!Dm.truthy(user?.mind?.get_hijack_speed()))
        {
            to_chat(user, span_warning(I18n.T("You manage to open a user-mode shell on [], and hundreds of lines of debugging output fly through your vision. It is probably best to leave this alone.", this)));
            return null;
        }
        if (!datum_controller_subsystem_shuttle.EMERGENCY_AT_LEAST_DOCKED()) // prevent advancing hijack stages on BYOS shuttles until the shuttle has "docked"
        {
            to_chat(user, span_warning(I18n.T("The flight plans for the shuttle haven't been loaded yet, you can't hack this right now.")));
            return null;
        }
        if (hijack_hacking == TRUE)
            return null;
        obj_docking_port_mobile_emergency emergency = SSshuttle.emergency as obj_docking_port_mobile_emergency;
        if (emergency.hijack_status >= HIJACK_COMPLETED)
        {
            to_chat(user, span_warning(I18n.T("The emergency shuttle is already loaded with a corrupt navigational payload. What more do you want from it?")));
            return null;
        }
        if (hijack_last_stage_increase >= Dm.num(world.time) - hijack_stage_cooldown)
        {
            await say("Error - Catastrophic software error detected. Input is currently on timeout.");
            return null;
        }
        hijack_hacking = TRUE;
        to_chat(user, span_boldwarning(I18n.T("You [] to override []'s navigational protocols.", emergency.hijack_status == HIJACK_NOT_BEGUN ? I18n.T("begin") : I18n.T("continue"), this)));
        await say("Software override initiated.");
        turf console_hijack_turf = get_turf(this);

        message_admins(Dm.text("[] is being overriden for hijack by [] in []", this, ADMIN_LOOKUPFLW(user), ADMIN_VERBOSEJMP(console_hijack_turf)));
        user.log_message(Dm.text("is hijacking [].", this), LOG_GAME);
        object dot = FALSE;
        if (Dm.truthy(await do_after(user, hijack_stage_time * (1 / Dm.num(user.mind.get_hijack_speed())), target: this)))
        {
            await increase_hijack_stage();
            console_hijack_turf = get_turf(this);
            message_admins(Dm.text("[] has hijacked [] in [].  Hijack stage increased to stage [] out of [].", ADMIN_LOOKUPFLW(user), this, ADMIN_VERBOSEJMP(console_hijack_turf), (SSshuttle.emergency as obj_docking_port_mobile_emergency)?.hijack_status, HIJACK_COMPLETED));
            user.log_message(Dm.text("has hijacked []. Hijack stage increased to stage [] out of [].", this, (SSshuttle.emergency as obj_docking_port_mobile_emergency)?.hijack_status, HIJACK_COMPLETED), LOG_GAME);
            dot = TRUE;
            to_chat(user, span_notice(I18n.T("You reprogram some of []'s programming, putting it on timeout for [] seconds.", this, hijack_stage_cooldown / 10)));
            visible_message(
                span_warning(I18n.T("[] appears to be tampering with [].", user.name, this)),
                blind_message: span_hear(I18n.T("You hear someone tapping computer keys.")),
                vision_distance: COMBAT_MESSAGE_RANGE,
                ignored_mobs: user);
        }
        hijack_hacking = FALSE;
        return dot;
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:275
    public virtual async DmTask announce_hijack_stage()
    {
        object msg = null;
        obj_docking_port_mobile_emergency emergency = SSshuttle.emergency as obj_docking_port_mobile_emergency;
        switch (emergency.hijack_status)
        {
            case HIJACK_NOT_BEGUN:
                return;
            case HIJACK_STAGE_1:
                msg = "AUTHENTICATING - FAIL. AUTHENTICATING - FAIL. AUTHENTICATING - FAI###### Welcome, technician JOHN DOE.";
                break;
            case HIJACK_STAGE_2:
                msg = Dm.text("Warning: Navigational route fails \"IS_AUTHORIZED\". Please try againNN[].", scramble_message_replace_chars("againagainagainagainagain", 70));
                break;
            case HIJACK_STAGE_3:
                msg = "CRC mismatch at ~h~ in calculated route buffer. Full reset initiated of FTL_NAVIGATION_SERVICES. Memory decrypted for automatic repair.";
                break;
            case HIJACK_STAGE_4:
                msg = "~ACS_directive module_load(cyberdyne.exploit.nanotrasen.shuttlenav)... NT key mismatch. Confirm load? Y...###Reboot complete. $SET transponder_state = 0; System link initiated with connected engines...";
                break;
            case HIJACK_COMPLETED:
                msg = Dm.text("SYSTEM OVERRIDE - Resetting course to \\[[]\\] ([]/[]/[]) {AUTH - ROOT (uid: 0)}.</font>[]",
                    scramble_message_replace_chars("###########", 100),
                    scramble_message_replace_chars("#######", 100),
                    scramble_message_replace_chars("#######", 100),
                    scramble_message_replace_chars("#######", 100),
                    Dm.eq(emergency.mode, SHUTTLE_ESCAPE)
                        ? Dm.text("Diverting from existing route - Bluespace exit in [] seconds.", hijack_completion_flight_time_set >= INFINITY ? Dm.text("[]", scramble_message_replace_chars("[ERROR]")) : (object)(hijack_completion_flight_time_set / 10))
                        : "");
                break;
        }
        minor_announce(scramble_message_replace_chars(msg, replaceprob: 10), "Emergency Shuttle", TRUE);
    }

    // tg: code/modules/shuttle/mobile_port/variants/emergency/emergency_console.dm:296
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        // How did you even get on the shuttle before it go to the station?
        if (!IS_DOCKED)
            return FALSE;

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) || ENGINES_STARTED) //SYSTEM ERROR: THE SHUTTLE WILL LA-SYSTEM ERROR: THE SHUTTLE WILL LA-SYSTEM ERROR: THE SHUTTLE WILL LAUNCH IN 10 SECONDS
        {
            balloon_alert(user, "shuttle already about to launch!");
            return FALSE;
        }

        object time = TIME_LEFT;
        if (user != null)
        {
            message_admins(Dm.text("[] has emagged the emergency shuttle [] seconds before launch.", key_name(user), time));
            log_shuttle(Dm.text("[] has emagged the emergency shuttle in [],[],[] [] seconds before launch.", key_name(user), x, y, z, time));
        }
        else
        {
            message_admins(Dm.text("The emergency shuttle was emagged [] seconds before launch, with no emagger.", time));
            log_shuttle(Dm.text("The emergency shuttle was emagged in [],[],[] [] seconds before launch, with no emagger.", x, y, z, time));
        }

        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        SSshuttle.emergency.movement_force = Dm.list(Dm.kv("KNOCKDOWN", 60f), Dm.kv("THROW", 20f)); //YOUR PUNY SEATBELTS can SAVE YOU NOW, MORTAL
        for (int i = 1; i <= 10; i++)
        {
            // the shuttle system doesn't know who these people are, but they
            // must be important, surely
            var ID = Dm.@new<obj_item_card_id>(this);
            var J = (datum_job)pick(SSjob.joinable_occupations);
            ID.registered_name = generate_random_name_species_based(species_type: typeof(datum_species_human));
            ID.assignment = J.title;

            Dm.append(ref authorized, ID);
        }

        process(SSmachines.wait * 0.1f);
        return TRUE;
    }
}
