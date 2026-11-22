// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/prisoner/management.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice F (D186): the global list of the implants the console tracks (the implants add and remove themselves). Gaps LC1 (D333):
// the console, the rest of the file: the brig officer's login, the prisoner ID's points and goal, and every tracked implant
// shown with its data and buttons (the PrisonerManagement window).

public partial class datum_controller_global_vars
{
    // tg: code/game/machinery/computer/prisoner/management.dm:2  GLOBAL_LIST_EMPTY(tracked_implants)
    public DmList tracked_implants;

    // tg: code/game/machinery/computer/prisoner/management.dm:2  (GLOBAL_MANAGED(tracked_implants, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltracked_implants()
    {
        tracked_implants = Dm.list();
        Dm.append(ref gvars_datum_init_order, "tracked_implants");
    }
}

// tg: code/game/machinery/computer/prisoner/management.dm:4
public partial class obj_machinery_computer_prisoner_management : obj_machinery_computer_prisoner
{
    // tg: code/game/machinery/computer/prisoner/management.dm:14
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "PrisonerManagement");
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/prisoner/management.dm:21
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["authorized"] = (Dm.truthy(authenticated) && isliving(user)) || HAS_SILICON_ACCESS(user) ? TRUE : FALSE;
        data["inserted_id"] = null;
        if (contained_id != null)
        {
            data["inserted_id"] = Dm.list(
                Dm.kv("name", contained_id.name),
                Dm.kv("points", contained_id.points),
                Dm.kv("goal", contained_id.goal));
        }

        DmList implants = Dm.list();
        foreach (obj_item_implant implant in Dm.each<obj_item_implant>(GLOB.tracked_implants))
        {
            if (!Dm.truthy(implant.is_shown_on_console(this)))
                continue;
            DmList implant_data = Dm.list();
            implant_data["info"] = implant.get_management_console_data();
            implant_data["buttons"] = implant.get_management_console_buttons();
            implant_data["category"] = Dm.initial(implant, (obj_item_implant i) => i.name);
            implant_data["ref"] = REF(implant);
            Dm.append(ref implants, Dm.list(implant_data)); // UNTYPED_LIST_ADD
        }
        data["implants"] = implants;

        return data;
    }

    // tg: code/game/machinery/computer/prisoner/management.dm:47
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(authenticated) && !Dm.eq(action, "login"))
            throw Dm.CRASH(Dm.text("[] potentially spoofed ui action [] on prisoner console without the console being logged in.", usr, action), "code/game/machinery/computer/prisoner/management.dm", 53);

        if (isliving(usr))
            playsound(this, Dm.resource("sound/machines/terminal/terminal_prompt_confirm.ogg"), 50, FALSE);

        switch (Dm.str(action))
        {
            case "login":
                if (Dm.truthy(await allowed__proc(usr)))
                {
                    authenticated = TRUE;
                    playsound(this, Dm.resource("sound/machines/terminal/terminal_on.ogg"), 50, FALSE);
                }
                else
                    playsound(this, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 50, FALSE);
                return TRUE;

            case "logout":
                authenticated = FALSE;
                playsound(this, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 50, FALSE);
                return TRUE;

            case "insert_id":
                await id_insert(usr, usr.get_active_held_item());
                return TRUE;

            case "eject_id":
                await id_eject(usr);
                return TRUE;

            case "set_id_goal":
            {
                mob user = usr;
                object num = await tgui_input_number(user, I18n.T("Enter the prisoner's goal"), I18n.T("Prisoner Management"), 100, 1000, 1);
                if (!isnum(num) || QDELETED(this) || QDELETED(contained_id) || QDELETED(user))
                    return TRUE;
                if (!Dm.truthy(is_operational) || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY | ALLOW_SILICON_REACH)))
                    return TRUE;

                contained_id.goal = Dm.num(num);
                return TRUE;
            }

            case "reset_id":
                contained_id.points = 0;
                return TRUE;

            case "handle_implant":
            {
                obj_item_implant affected_implant = Dm.locate_ref(@params["implant_ref"]) is obj_item_implant found && Dm.@in(found, GLOB.tracked_implants) ? found : null; // locate(params["implant_ref"]) in GLOB.tracked_implants
                if (affected_implant != null && Dm.truthy(affected_implant.is_shown_on_console(this)))
                    await affected_implant.handle_management_console_action(usr, @params, this);
                return TRUE;
            }
        }
        return dot;
    }
}
