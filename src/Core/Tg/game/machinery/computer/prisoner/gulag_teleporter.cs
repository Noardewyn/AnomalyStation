// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/prisoner/gulag_teleporter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Gaps LC1 (lavaland.md L3's labour camp): the whole file. The labour camp teleporter console: it finds the teleporter beside it
// and the camp's beacon, reads the occupant's record, sets the prisoner ID's goal, opens, closes and locks the teleporter, and
// sends the prisoner (stripped and dressed by the teleporter) to the beacon with the ID. The var block is generated but for
// `prisoner` (see below).

using System.Linq;
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//computer that handle the points and teleports the prisoner
// tg: code/game/machinery/computer/prisoner/gulag_teleporter.dm:2
public partial class obj_machinery_computer_prisoner_gulag_teleporter_computer : obj_machinery_computer_prisoner
{
    // tg: code/game/machinery/computer/prisoner/gulag_teleporter.dm:14  var/mob/living/carbon/human/prisoner = null (hand-written:
    // the generator types it a Type from a typepath assigned elsewhere to a var of that name)
    public mob_living_carbon_human prisoner;

    // The type's defaults (tg: code/game/machinery/computer/prisoner/gulag_teleporter.dm:3): with prisoner declared above, the var
    // block is hand-written (types-converter.md 3.2, mode Hand), so the constructor and dm_init_vars the generator wrote are here.
    public obj_machinery_computer_prisoner_gulag_teleporter_computer()
    {
        name = "labor camp teleporter console";
        desc = "Used to send criminals to the Labor Camp.";
        icon_screen = "explosive";
        icon_keyboard = "security_key";
        circuit = typeof(obj_item_circuitboard_computer_gulag_teleporter_console);
        light_color = "#FA8282";
    }

    // tg: code/game/machinery/computer/prisoner/gulag_teleporter.dm:7
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        req_access = Dm.list("brig"); // tg: code/game/machinery/computer/prisoner/gulag_teleporter.dm:7
    }

    // tg: code/game/machinery/computer/prisoner/gulag_teleporter.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        scan_machinery();
        return dot;
    }

    // tg: code/game/machinery/computer/prisoner/gulag_teleporter.dm:22
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "GulagTeleporterConsole", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/prisoner/gulag_teleporter.dm:29
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList prisoner_list = Dm.list();
        bool can_teleport = false;

        if (teleporter != null && teleporter.occupant is mob_living_carbon_human occupant_human)
        {
            prisoner = occupant_human;
            prisoner_list["name"] = prisoner.real_name;
            if (contained_id != null)
                can_teleport = true;
            if (GLOB.manifest.general != null)
            {
                foreach (datum_record_crew record in Dm.each<datum_record_crew>(GLOB.manifest.general))
                {
                    if (Dm.eq(record.name, prisoner_list["name"]))
                    {
                        temporary_record = record;
                        prisoner_list["crimstat"] = temporary_record.wanted_status;
                    }
                }
            }
        }

        data["prisoner"] = prisoner_list;

        if (teleporter != null)
        {
            data["teleporter"] = teleporter;
            data["teleporter_location"] = Dm.text("([], [], [])", teleporter.x, teleporter.y, teleporter.z);
            data["teleporter_lock"] = teleporter.locked;
            data["teleporter_state_open"] = teleporter.state_open;
        }
        else
            data["teleporter"] = null;
        if (beacon != null)
        {
            data["beacon"] = beacon;
            data["beacon_location"] = Dm.text("([], [], [])", beacon.x, beacon.y, beacon.z);
        }
        else
            data["beacon"] = null;
        if (contained_id != null)
        {
            data["id"] = contained_id;
            data["id_name"] = contained_id.registered_name;
            data["goal"] = contained_id.goal;
        }
        else
            data["id"] = null;
        data["can_teleport"] = can_teleport ? TRUE : FALSE;

        return data;
    }

    // tg: code/game/machinery/computer/prisoner/gulag_teleporter.dm:70
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (isliving(usr))
            playsound(this, Dm.resource("sound/machines/terminal/terminal_prompt_confirm.ogg"), 50, FALSE);
        if (!Dm.truthy(await allowed__proc(usr)))
        {
            to_chat(usr, span_warning(I18n.T("Access denied.")));
            return dot;
        }
        switch (Dm.str(action))
        {
            case "scan_teleporter":
                teleporter = findteleporter();
                return TRUE;
            case "scan_beacon":
                beacon = findbeacon();
                return TRUE;
            case "handle_id":
                if (contained_id != null)
                    await id_eject(usr);
                else
                    await id_insert(usr);
                return TRUE;
            case "set_goal":
            {
                if (contained_id == null)
                    return dot;
                object new_goal = text2num(@params["value"]);
                if (!isnum(new_goal))
                    return dot;
                if (!Dm.truthy(new_goal))
                    new_goal = default_goal;
                contained_id.goal = clamp(Dm.num(new_goal), 0, 1000); //maximum 1000 points
                return TRUE;
            }
            case "toggle_open":
                if (Dm.truthy(teleporter.locked))
                {
                    to_chat(usr, span_alert(I18n.T("The teleporter must be unlocked first.")));
                    return dot;
                }
                await teleporter.toggle_open();
                return TRUE;
            case "teleporter_lock":
                if (Dm.truthy(teleporter.state_open))
                {
                    to_chat(usr, span_alert(I18n.T("The teleporter must be closed first.")));
                    return dot;
                }
                teleporter.locked = Dm.num(!Dm.truthy(teleporter.locked));
                return TRUE;
            case "teleport":
                if (teleporter == null || beacon == null)
                    return dot;
                _addtimer(Dm.@new<datum_callback>(this, nameof(teleport), usr), 0.5f * SECONDS, file: "code/game/machinery/computer/prisoner/gulag_teleporter.dm", line: 120);
                return TRUE;
        }
        return dot;
    }

    // tg: code/game/machinery/computer/prisoner/gulag_teleporter.dm:123
    public virtual void scan_machinery()
    {
        teleporter = findteleporter();
        beacon = findbeacon();
    }

    // tg: code/game/machinery/computer/prisoner/gulag_teleporter.dm:127
    public virtual obj_machinery_gulag_teleporter findteleporter()
    {
        obj_machinery_gulag_teleporter teleporterf = null;

        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            teleporterf = Dm.locate<obj_machinery_gulag_teleporter>(get_step(this, Dm.num(direction)));
            if (teleporterf != null && Dm.truthy(teleporterf.is_operational))
                return teleporterf;
        }
        return null;
    }

    // tg: code/game/machinery/computer/prisoner/gulag_teleporter.dm:135
    public virtual obj_structure_gulag_beacon findbeacon() => Dm.each<obj_structure_gulag_beacon>().FirstOrDefault(); // locate(/obj/structure/gulag_beacon)

    // tg: code/game/machinery/computer/prisoner/gulag_teleporter.dm:138
    public virtual async DmTask teleport(mob user)
    {
        if (contained_id == null) //incase the ID was removed after the transfer timer was set.
        {
            await say(I18n.T("Warning: Unable to transfer prisoner without a valid Prisoner ID inserted!"));
            return;
        }
        bool id_goal_not_set = false;
        if (!Dm.truthy(contained_id.goal))
        {
            id_goal_not_set = true;
            contained_id.goal = default_goal;
            await say(I18n.T("[]'s ID card goal defaulting to [] points.", contained_id, contained_id.goal));
        }
        user.log_message(Dm.text("teleported [] to the Labor Camp [] for [][] points.", key_name(prisoner), COORD(beacon), id_goal_not_set ? "default goal of " : "", contained_id.goal), LOG_GAME);
        prisoner.log_message(Dm.text("teleported to Labor Camp [] by [] for [][] points.", COORD(beacon), key_name(user), id_goal_not_set ? "default goal of " : "", contained_id.goal), LOG_GAME, log_globally: FALSE);
        await teleporter.handle_prisoner(contained_id, temporary_record);
        playsound(this, Dm.resource("sound/items/weapons/emitter.ogg"), 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        await prisoner.forceMove(get_turf(beacon));
        await prisoner.Paralyze(40); // small travel dizziness
        to_chat(prisoner, span_warning(I18n.T("The teleportation makes you a little dizzy.")));
        Dm.@new<obj_effect_particle_effect_sparks>(get_turf(prisoner));
        playsound(this, SFX_PORTAL_ENTER, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        if (Dm.truthy(teleporter.locked))
            teleporter.locked = FALSE;
        await teleporter.toggle_open();
        contained_id = null;
        temporary_record = null;
    }
}
