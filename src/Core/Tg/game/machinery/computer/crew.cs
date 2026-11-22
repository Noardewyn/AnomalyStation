// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/crew.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J5 (D298): the crew monitor behind the console and the handheld monitor. The console's circuit component stays inert with the circuits;
// the AI's "select_person" tracking came with silicons SI4 (D326).

// tg: code/game/machinery/computer/crew.dm:7
public partial class obj_machinery_computer_crew : obj_machinery_computer
{
    // tg: code/game/machinery/computer/crew.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // AddComponent(/datum/component/usb_port, ...): the circuits (not-translated.md 114)
        return dot;
    }

    // tg: code/game/machinery/computer/crew.dm:81
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        await base.ui_interact(user, ui, __extra);
        await GLOB.crewmonitor.show(user, this);
        return null;
    }
}

// tg: code/game/machinery/computer/crew.dm:87
public partial class datum_crewmonitor : datum
{
    /// How often the sensor data is updated
    private const float SENSORS_UPDATE_PERIOD = 10 * SECONDS; //How often the sensor data updates.
    /// The job sorting ID associated with otherwise unknown jobs
    private const int UNKNOWN_JOB_ID = 998;

    // tg: code/game/machinery/computer/crew.dm:164
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "CrewConsole");
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/computer/crew.dm:170
    public virtual async DmTask show(mob M = null, object source = null)
    {
        ui_sources[WEAKREF(M)] = WEAKREF(source as datum);
        await ui_interact(M);
    }

    // tg: code/game/machinery/computer/crew.dm:174
    public override object ui_host(mob user = null)
    {
        datum_weakref host_ref = ui_sources[WEAKREF(user)] as datum_weakref;
        return host_ref?.resolve();
    }

    // tg: code/game/machinery/computer/crew.dm:178
    public override async DmTask<object> ui_data(mob user = null)
    {
        object z = user.z;
        if (!Dm.truthy(z))
        {
            turf T = get_turf(user);
            z = T.z;
        }
        return Dm.list(
            Dm.kv("sensors", update_data(z)),
            Dm.kv("link_allowed", HAS_AI_ACCESS(user) ? TRUE : FALSE));
    }

    // tg: code/game/machinery/computer/crew.dm:188
    public virtual object update_data(object z = null)
    {
        string zkey = Dm.text("[]", z);
        if (Dm.truthy(data_by_z[zkey]) && Dm.truthy(last_update[zkey]) && world.time <= Dm.num(last_update[zkey]) + SENSORS_UPDATE_PERIOD)
            return data_by_z[zkey];

        DmList results = Dm.list();
        foreach (object tracked_mob in Dm.each(GLOB.suit_sensors_list))
        {
            if (tracked_mob == null)
            {
                stack_trace("Null entry in suit sensors list.", "code/game/machinery/computer/crew.dm", 188);
                continue;
            }

            mob_living tracked_living_mob = tracked_mob as mob_living;

            // Check if z-level is correct
            turf pos = get_turf(tracked_living_mob);

            // Is our target in nullspace for some reason?
            if (pos == null)
            {
                stack_trace(Dm.text("Tracked mob has no loc and is likely in nullspace: [] ([])", tracked_living_mob, tracked_living_mob.type), "code/game/machinery/computer/crew.dm", 188);
                continue;
            }

            // Machinery and the target should be on the same level or different levels of the same station
            if (!Dm.eq(pos.z, z) && (!Dm.truthy(is_station_level(pos.z)) || !Dm.truthy(is_station_level(z))) && !HAS_TRAIT(tracked_living_mob, TRAIT_MULTIZ_SUIT_SENSORS))
                continue;

            mob_living_carbon_human tracked_human = tracked_living_mob as mob_living_carbon_human;

            // Check their humanity.
            if (!ishuman(tracked_human))
            {
                stack_trace(Dm.text("Non-human mob is in suit_sensors_list: [] ([])", tracked_living_mob, tracked_living_mob.type), "code/game/machinery/computer/crew.dm", 188);
                continue;
            }

            // Check they have a uniform
            obj_item_clothing_under uniform = tracked_human.w_uniform as obj_item_clothing_under;
            if (uniform == null)
            {
                stack_trace(Dm.text("Human without a suit sensors compatible uniform is in suit_sensors_list: [] ([]) ([])", tracked_human, tracked_human.type, tracked_human.w_uniform?.type), "code/game/machinery/computer/crew.dm", 188);
                continue;
            }

            // Check if their uniform is in a compatible mode.
            if (Dm.eq(uniform.has_sensor, NO_SENSORS) || !Dm.truthy(uniform.sensor_mode))
            {
                stack_trace(Dm.text("Human without active suit sensors is in suit_sensors_list: [] ([]) ([])", tracked_human, tracked_human.type, uniform.type), "code/game/machinery/computer/crew.dm", 188);
                continue;
            }

            float sensor_mode = Dm.num(uniform.sensor_mode);

            // The entry for this human
            DmList entry = Dm.list(
                Dm.kv("ref", REF(tracked_living_mob)),
                Dm.kv("name", "Unknown"),
                Dm.kv("ijob", UNKNOWN_JOB_ID));

            // ID and id-related data
            obj_item_card_id id_card = tracked_living_mob.get_idcard(hand_first: FALSE) as obj_item_card_id;
            if (id_card != null)
            {
                entry["name"] = id_card.registered_name;
                entry["assignment"] = id_card.assignment;
                object trim_assignment = id_card.get_trim_assignment();
                if (trim_assignment != null && jobs[trim_assignment] != null)
                    entry["ijob"] = jobs[trim_assignment];
            }

            // Broken sensors show garbage data
            if (Dm.eq(uniform.has_sensor, BROKEN_SENSORS))
            {
                entry["life_status"] = rand(0, 1);
                entry["area"] = pick_list(ION_FILE, "ionarea");
                entry["oxydam"] = rand(0, 175);
                entry["toxdam"] = rand(0, 175);
                entry["burndam"] = rand(0, 175);
                entry["brutedam"] = rand(0, 175);
                entry["health"] = -50;
                results.Add(Dm.list(entry)); // results[++results.len] = entry
                continue;
            }

            // Current status
            if (sensor_mode >= SENSOR_VITALS)
                entry["life_status"] = tracked_living_mob.stat;
            else if (sensor_mode == SENSOR_LIVING)
                // binary sensors should only report alive or dead
                entry["life_status"] = Dm.eq(tracked_living_mob.stat, DEAD) ? DEAD : STABLE;

            // Damage
            if (sensor_mode >= SENSOR_VITALS)
            {
                entry["oxydam"] = round(Dm.num(tracked_living_mob.get_oxy_loss()), 1);
                entry["toxdam"] = round(Dm.num(tracked_living_mob.get_tox_loss()), 1);
                entry["burndam"] = round(Dm.num(tracked_living_mob.get_fire_loss()), 1);
                entry["brutedam"] = round(Dm.num(tracked_living_mob.get_brute_loss()), 1);
                entry["health"] = round(Dm.num(tracked_living_mob.health), 1);
            }

            // Location
            if (sensor_mode >= SENSOR_COORDS)
                entry["area"] = get_area_name(tracked_living_mob, format_text: TRUE);

            results.Add(Dm.list(entry)); // results[++results.len] = entry
        }

        // Cache result
        data_by_z[zkey] = results;
        last_update[zkey] = world.time;

        return results;
    }

    // tg: code/game/machinery/computer/crew.dm:289
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "select_person":
                var AI = usr as mob_living_silicon_ai;
                if (AI == null)
                    return null;
                // We need to do this because the ID might add an honorific and otherwise break tracking
                var target = Dm.locate_ref(@params["ref"]) as mob_living; // locate(params["ref"]) in GLOB.mob_living_list
                if (target == null || !Dm.@in(target, GLOB.mob_living_list))
                    return null;
                AI.ai_tracking_tool.track_mob(AI, target);
                break;
        }
        return null;
    }
}

// tg: code/game/machinery/computer/crew.dm:85  GLOBAL_DATUM_INIT(crewmonitor, /datum/crewmonitor, new)
public partial class datum_controller_global_vars
{
    public datum_crewmonitor crewmonitor;

    public virtual void InitGlobalcrewmonitor()
    {
        crewmonitor = Dm.@new<datum_crewmonitor>();
        Dm.append(ref gvars_datum_init_order, "crewmonitor");
    }
}
