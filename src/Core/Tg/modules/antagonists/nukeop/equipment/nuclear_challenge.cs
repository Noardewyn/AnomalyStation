using System.Linq;
// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1c, D316: the leader's war declaration; silicons SI3, D326: the station's research bonus, the science
// techweb's points and the research channel's announcement). The var blocks are generated (D111).

public partial class datum_controller_global_vars
{
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm:5  GLOBAL_LIST_EMPTY(jam_on_wardec)
    public DmList jam_on_wardec;

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm:5  (GLOBAL_MANAGED(jam_on_wardec, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaljam_on_wardec()
    {
        jam_on_wardec = Dm.list();
        Dm.append(ref gvars_datum_init_order, "jam_on_wardec");
    }
}

// tg: code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm:7
public partial class obj_item_nuclear_challenge : obj_item
{
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm:1  #define CHALLENGE_TELECRYSTALS 280
    private const int CHALLENGE_TELECRYSTALS = 280;
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm:2  #define CHALLENGE_TIME_LIMIT (5 MINUTES)
    private const float CHALLENGE_TIME_LIMIT = 5 * MINUTES;
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm:3  #define CHALLENGE_SHUTTLE_DELAY (25 MINUTES)
    private const float CHALLENGE_SHUTTLE_DELAY = 25 * MINUTES; // 25 minutes, so the ops have at least 5 minutes before the shuttle is callable.

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm:21
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        var living = user as mob_living;
        if (!Dm.truthy(check_allowed(living)))
            return null;

        declaring_war = TRUE;
        object are_you_sure = await tgui_alert(user, Dm.text("Consult your team carefully before you declare war on []. Are you sure you want to alert the enemy crew? You have [] to decide.",
            station_name(), DisplayTimeText(CHALLENGE_TIME_LIMIT - world.time - Dm.num(SSticker.round_start_time))), "Declare war?", Dm.list("Yes", "No"));
        declaring_war = FALSE;

        if (!Dm.truthy(check_allowed(living)))
            return null;

        if (!Dm.eq(are_you_sure, "Yes"))
        {
            to_chat(user, span_notice(I18n.T("On second thought, the element of surprise isn't so bad after all.")));
            return null;
        }

        object war_declaration = Dm.text("A syndicate fringe group has declared their intent to utterly destroy [] with a nuclear device, and dares the crew to try and stop them.", station_name());

        declaring_war = TRUE;
        object custom_threat = await tgui_alert(user, "Do you want to customize your declaration?", "Customize?", Dm.list("Yes", "No"));
        declaring_war = FALSE;

        if (!Dm.truthy(check_allowed(living)))
            return null;

        if (Dm.eq(custom_threat, "Yes"))
        {
            declaring_war = TRUE;
            war_declaration = await tgui_input_text(user, "Insert your custom declaration", "Declaration", max_length: MAX_MESSAGE_LEN, multiline: TRUE, encode: FALSE);
            declaring_war = FALSE;
        }

        if (!Dm.truthy(check_allowed(living)) || !Dm.truthy(war_declaration))
            return null;

        await war_was_declared(living, memo: war_declaration);
        return null;
    }

    ///Admin only proc to bypass checks and force a war declaration. Button on antag panel.
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm:55
    public virtual async DmTask force_war()
    {
        object are_you_sure = await tgui_alert(usr, Dm.text("Are you sure you wish to force a war declaration?[]",
            GLOB.player_list.len < CHALLENGE_MIN_PLAYERS ? " Note, the player count is under the required limit." : ""), "Declare war?", Dm.list("Yes", "No"));

        if (!Dm.eq(are_you_sure, "Yes"))
            return;

        object war_declaration = Dm.text("A syndicate fringe group has declared their intent to utterly destroy [] with a nuclear device, and dares the crew to try and stop them.", station_name());

        object custom_threat = await tgui_alert(usr, "Do you want to customize the declaration?", "Customize?", Dm.list("Yes", "No"));

        if (Dm.eq(custom_threat, "Yes"))
            war_declaration = await tgui_input_text(usr, "Insert your custom declaration", "Declaration", max_length: MAX_MESSAGE_LEN, multiline: TRUE, encode: FALSE);

        if (!Dm.truthy(war_declaration))
        {
            await tgui_alert(usr, "Invalid war declaration.", "Poor Choice of Words");
            return;
        }

        foreach (obj_item_circuitboard_computer_syndicate_shuttle board in Dm.each<obj_item_circuitboard_computer_syndicate_shuttle>(GLOB.syndicate_shuttle_boards))
        {
            if (Dm.truthy(board.challenge_start_time))
            {
                await tgui_alert(usr, "War has already been declared!", "War Was Declared");
                return;
            }
        }

        await war_was_declared(memo: war_declaration);
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm:83
    public virtual async DmTask war_was_declared(mob_living user = null, object memo = null)
    {
        priority_announce(
            text: memo,
            title: "Declaration of War",
            sound: announcement_sound,
            has_important_message: TRUE,
            sender_override: "Nuclear Operative Outpost",
            color_override: "red");
        if (user != null)
            to_chat(user, I18n.T("You've attracted the attention of powerful forces within the syndicate. A bonus bundle of telecrystals has been granted to your team. Great things await you if you complete the mission."));

        await distribute_tc();
        // CONFIG_SET(number/shuttle_refuel_delay, max(CONFIG_GET(number/shuttle_refuel_delay), CHALLENGE_SHUTTLE_DELAY))
        await config.Set(typeof(datum_config_entry_number_shuttle_refuel_delay),
            max(Dm.num(config.Get(typeof(datum_config_entry_number_shuttle_refuel_delay))), CHALLENGE_SHUTTLE_DELAY));
        SSblackbox.record_feedback("amount", "nuclear_challenge_mode", 1);

        foreach (obj_item_circuitboard_computer_syndicate_shuttle board in Dm.each<obj_item_circuitboard_computer_syndicate_shuttle>(GLOB.syndicate_shuttle_boards))
            board.challenge_start_time = world.time;

        foreach (obj_machinery_computer_camera_advanced_shuttle_docker dock in Dm.each<obj_machinery_computer_camera_advanced_shuttle_docker>(GLOB.jam_on_wardec))
            dock.jammed = TRUE;

        datum_techweb station_techweb = Dm.each<datum_techweb>(SSresearch?.techwebs).OfType<datum_techweb_science>().FirstOrDefault(); // locate(/datum/techweb/science) in SSresearch.techwebs
        if (station_techweb != null)
        {
            obj_machinery_announcement_system announcement_system = get_announcement_system(null, null, Dm.list(RADIO_CHANNEL_SCIENCE));
            if (announcement_system != null)
                await announcement_system.broadcast("Additional research data received from Nanotrasen R&D Division following the emergency protocol.", Dm.list(RADIO_CHANNEL_SCIENCE), TRUE);
            station_techweb.adjust_points(TECHWEB_POINT_TYPE_GENERIC, TECHWEB_TIER_5_POINTS * 3);
        }

        qdel(this);
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm:110
    public virtual async DmTask distribute_tc()
    {
        DmList orphans = Dm.list();
        DmList uplinks = Dm.list();

        foreach (datum_mind M in Dm.each<datum_mind>(get_antag_minds(typeof(datum_antagonist_nukeop))))
        {
            if (iscyborg(M.current))
                continue;
            var uplink = M.find_syndicate_uplink() as datum_component_uplink;
            if (uplink == null)
            {
                Dm.append(ref orphans, M.current);
                continue;
            }
            Dm.append(ref uplinks, uplink);
        }

        float tc_to_distribute = CHALLENGE_TELECRYSTALS;
        float tc_per_nukie = round(tc_to_distribute / (length(orphans) + length(uplinks)));

        foreach (datum_component_uplink uplink in Dm.each<datum_component_uplink>(uplinks))
        {
            uplink.uplink_handler.add_telecrystals(tc_per_nukie);
            tc_to_distribute -= tc_per_nukie;
        }

        foreach (mob_living L in Dm.each<mob_living>(orphans))
        {
            var TC = Dm.@new<obj_item_stack_telecrystal>(L.drop_location(), tc_per_nukie);
            to_chat(L, span_warning(I18n.T("Your uplink could not be found so your share of the team's bonus telecrystals has been bluespaced to your [].",
                Dm.truthy(await L.put_in_hands(TC)) ? "hands" : "feet")));
            tc_to_distribute -= tc_per_nukie;
        }

        if (tc_to_distribute > 0) // What shall we do with the remainder...
        {
            foreach (mob_living_basic_carp_pet_cayenne C in Dm.each<mob_living_basic_carp_pet_cayenne>(GLOB.mob_living_list))
            {
                if (C.stat != DEAD)
                {
                    var TC = Dm.@new<obj_item_stack_telecrystal>(C.drop_location(), tc_to_distribute);
                    Waiting.Detach(TC.throw_at(get_step(C, C.dir) as atom, 3, 3));
                    C.visible_message(span_notice(I18n.T("[] coughs up a half-digested telecrystal", C)), span_notice(I18n.T("You cough up a half-digested telecrystal!")));
                    break;
                }
            }
        }
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm:146
    public virtual object check_allowed(mob_living user = null)
    {
        if (Dm.truthy(declaring_war))
        {
            to_chat(user, span_boldwarning(I18n.T("You are already in the process of declaring war! Make your mind up.")));
            return FALSE;
        }
        if (GLOB.player_list.len < CHALLENGE_MIN_PLAYERS)
        {
            to_chat(user, span_boldwarning(I18n.T("The enemy crew is too small to be worth declaring war on.")));
            return FALSE;
        }
        if (!Dm.truthy(user.onSyndieBase()))
        {
            to_chat(user, span_boldwarning(I18n.T("You have to be at your base to use this.")));
            return FALSE;
        }
        if (world.time - Dm.num(SSticker.round_start_time) > CHALLENGE_TIME_LIMIT)
        {
            to_chat(user, span_boldwarning(I18n.T("It's too late to declare hostilities. Your benefactors are already busy with other schemes. You'll have to make do with what you have on hand.")));
            return FALSE;
        }
        foreach (obj_item_circuitboard_computer_syndicate_shuttle board in Dm.each<obj_item_circuitboard_computer_syndicate_shuttle>(GLOB.syndicate_shuttle_boards))
        {
            if (Dm.truthy(board.moved))
            {
                to_chat(user, span_boldwarning(I18n.T("The shuttle has already been moved! You have forfeit the right to declare war.")));
                return FALSE;
            }
            if (Dm.truthy(board.challenge_start_time))
            {
                to_chat(user, span_boldwarning(I18n.T("War has already been declared!")));
                return FALSE;
            }
        }
        return TRUE;
    }
}

/// Subtype that does nothing but plays the war op message. Intended for debugging
// tg: code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm:177
public partial class obj_item_nuclear_challenge_literally_just_does_the_message : obj_item_nuclear_challenge
{
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm:183
    public override object check_allowed(mob_living user = null)
    {
        if (Dm.truthy(admin_only) && !Dm.truthy(check_rights_for(user.client, R_SPAWN | R_FUN | R_DEBUG)))
        {
            to_chat(user, span_hypnophrase(I18n.T("You shouldn't have this!")));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm:190
    public override async DmTask war_was_declared(mob_living user = null, object memo = null)
    {
        // #ifndef TESTING: production builds ask the admin once more
        object are_you_sure = await tgui_alert(user, "Last second reminder that fake war declarations is a horrible idea and yes, this does the whole shebang, so be careful what you're doing.",
            "Don't do it", Dm.list("I'm sure", "You're right"));
        if (!Dm.eq(are_you_sure, "I'm sure"))
            return;

        priority_announce(
            text: memo,
            title: "Declaration of War",
            sound: announcement_sound,
            has_important_message: TRUE,
            sender_override: "Nuclear Operative Outpost",
            color_override: "red");
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_challenge.dm:206
    public override async DmTask distribute_tc()
    {
        return;
    }
}
