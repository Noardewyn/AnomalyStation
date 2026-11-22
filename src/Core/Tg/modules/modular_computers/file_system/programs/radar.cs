// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/radar.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the radar's circuit components (radar.dm:410-535, /obj/item/circuit_component/mod_program/radar and its kinds: the
// circuits, wiremod, are not ported; no shell ever registers to the computer's radar signals). Syndicate N1b (D316): the nuclear
// operatives' Fission360 on their PDA, with the crew's Lifeline and Custodial Locator. Its NtosRadar windows are the client's, its
// pictures tg's radar_assets (the asset transport is the client's, DV44). The var blocks are generated (D111).

// tg: code/modules/modular_computers/file_system/programs/radar.dm:11
public partial class datum_computer_file_program_radar : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/radar.dm:2  #define RADAR_NOT_TRACKABLE 0
    internal const int RADAR_NOT_TRACKABLE = 0;
    // tg: code/modules/modular_computers/file_system/programs/radar.dm:4  #define RADAR_TRACKABLE 1
    internal const int RADAR_TRACKABLE = 1;
    // tg: code/modules/modular_computers/file_system/programs/radar.dm:6  #define RADAR_TRACKABLE_ANYWAY 2
    internal const int RADAR_TRACKABLE_ANYWAY = 2;

    // tg: code/modules/modular_computers/file_system/programs/radar.dm:34
    public override async DmTask<object> on_start(mob_living user = null)
    {
        object dot = await base.on_start(user);
        if (!Dm.truthy(dot))
            return dot;
        if (next_scan <= world.time) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        {
            // start with a scan without a cooldown, but don't scan if we *are* on cooldown already.
            scan();
        }
        START_PROCESSING(SSfastprocess, this);
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/radar.dm:42
    public override object kill_program(mob user = null)
    {
        objects = Dm.list();
        selected = null;
        STOP_PROCESSING(SSfastprocess, this);
        return base.kill_program(user);
    }

    // tg: code/modules/modular_computers/file_system/programs/radar.dm:48
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/file_system/programs/radar.dm:52
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/simple/radar_assets)): the asset transport is the client's (DV44); the client draws the radar's
        // background and pointers from its own copy of tg's pictures (icons/ui/tgui/ntosradar_*.png)
        return Dm.list();
    }

    // tg: code/modules/modular_computers/file_system/programs/radar.dm:57
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["selected"] = selected;
        data["scanning"] = !(next_scan <= world.time) ? TRUE : FALSE; // !COOLDOWN_FINISHED(src, next_scan)
        data["object"] = objects;

        data["target"] = Dm.list();
        DmList trackinfo = track() as DmList;
        if (trackinfo != null)
            data["target"] = trackinfo;
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/radar.dm:69
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        switch (Dm.str(action))
        {
            case "selecttarget":
            {
                object selected_new_ref = @params["ref"];
                if (Dm.@in(selected_new_ref, trackable_object_refs()))
                {
                    selected = selected_new_ref;
                    computer.SEND_SIGNAL(COMSIG_MODULAR_COMPUTER_RADAR_SELECTED, Dm.list(computer, selected));
                }
                return TRUE;
            }
            case "scan":
                if (!(next_scan <= world.time))
                    return TRUE; // update anyways

                next_scan = world.time + 2 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))
                scan();
                return TRUE;
        }
        return dot;
    }

    /// Returns all ref()s that are being tracked currently
    // tg: code/modules/modular_computers/file_system/programs/radar.dm:88
    public virtual DmList trackable_object_refs()
    {
        DmList all_refs = Dm.list();
        foreach (DmList object_list in Dm.each<DmList>(objects))
            Dm.append(ref all_refs, object_list["ref"]);
        return all_refs;
    }

    /**
     *Updates tracking information of the selected target.
     *
     *The track() proc updates the entire set of information about the location
     *of the target, including whether the Ntos window should use a pinpointer
     *crosshair over the up/down arrows, or none in favor of a rotating arrow
     *for far away targets. This information is returned in the form of a list.
     *
    */
    // tg: code/modules/modular_computers/file_system/programs/radar.dm:104
    public virtual object track()
    {
        var signal = find_atom() as atom_movable;
        if (!Dm.truthy(trackable(signal)))
            return null;

        var here_turf = get_turf(computer) as turf;
        var target_turf = get_turf(signal) as turf;
        bool userot = false;
        float rot = 0;
        string pointer = "crosshairs";
        float locx = (target_turf.x - here_turf.x) + 24;
        float locy = (here_turf.y - target_turf.y) + 24;

        if (Dm.num(get_dist_euclidean(here_turf, target_turf)) > 24)
        {
            userot = true;
            rot = round(Dm.num(get_angle(here_turf, target_turf)));
        }
        else
        {
            if (target_turf.z > here_turf.z)
                pointer = "caret-up";
            else if (target_turf.z < here_turf.z)
                pointer = "caret-down";
        }

        DmList trackinfo = Dm.list(
            Dm.kv("locx", locx),
            Dm.kv("locy", locy),
            Dm.kv("userot", userot ? TRUE : FALSE),
            Dm.kv("rot", rot),
            Dm.kv("arrowstyle", arrowstyle),
            Dm.kv("color", pointercolor),
            Dm.kv("pointer", pointer));
        return trackinfo;
    }

    /**
     *
     *Checks the trackability of the selected target.
     *
     *If the target is on the computer's Z level, or both are on station Z
     *levels, and the target isn't untrackable, return TRUE.
     *Arguments:
     **arg1 is the atom being evaluated.
    */
    // tg: code/modules/modular_computers/file_system/programs/radar.dm:149
    public virtual object trackable(atom_movable signal = null)
    {
        // SHOULD_CALL_PARENT(TRUE)
        if (signal == null || computer == null)
            return RADAR_NOT_TRACKABLE;
        var here = get_turf(computer) as turf;
        var there = get_turf(signal) as turf;
        if (here == null || there == null || !Dm.truthy(is_valid_z_level(here, there)))
            return RADAR_NOT_TRACKABLE;
        object trackable_signal = computer.SEND_SIGNAL(COMSIG_MODULAR_COMPUTER_RADAR_TRACKABLE, Dm.list(computer, signal, here, there));
        if (Dm.truthy(Dm.bitand(trackable_signal, COMPONENT_RADAR_TRACK_ANYWAY)))
            return RADAR_TRACKABLE_ANYWAY;
        if (Dm.truthy(Dm.bitand(trackable_signal, COMPONENT_RADAR_DONT_TRACK)))
            return RADAR_NOT_TRACKABLE;
        return RADAR_TRACKABLE;
    }

    /**
     *
     *Runs a scan of all the trackable atoms.
     *
     *Checks each entry in the GLOB of the specific trackable atoms against
     *the track() proc, and fill the objects list with lists containing the
     *atoms' names and REFs. The objects list is handed to the tgui screen
     *for displaying to, and being selected by, the user. A two second
     *sleep is used to delay the scan, both for thematical reasons as well
     *as to limit the load players may place on the server using these
     *somewhat costly loops.
    */
    // tg: code/modules/modular_computers/file_system/programs/radar.dm:176
    public virtual void scan()
    {
        return;
    }

    /**
     *
     *Finds the atom in the appropriate list that the `selected` var indicates
     *
     *The `selected` var holds a REF, which is a string. A mob REF may be
     *something like "mob_209". In order to find the actual atom, we need
     *to search the appropriate list for the REF string. This is dependant
     *on the program (Lifeline uses GLOB.human_list, while Fission360 uses
     *GLOB.poi_list), but the result will be the same; evaluate the string and
     *return an atom reference.
    */
    // tg: code/modules/modular_computers/file_system/programs/radar.dm:191
    public virtual object find_atom()
    {
        // SHOULD_CALL_PARENT(TRUE)
        DmList atom_container = Dm.list((object)null);
        computer.SEND_SIGNAL(COMSIG_MODULAR_COMPUTER_RADAR_FIND_ATOM, Dm.list(computer, atom_container));
        return atom_container[1];
    }

    //We use SSfastprocess for the program icon state because it runs faster than process_tick() does.
    // tg: code/modules/modular_computers/file_system/programs/radar.dm:198
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (computer.active_program != this)
        {
            //We're not the active program, it's time to stop.
            return PROCESS_KILL;
        }
        if (!Dm.truthy(selected))
            return null;

        var signal = find_atom() as atom_movable;
        string initial_overlay = Dm.str(Dm.initial(this, (datum_computer_file_program_radar p) => p.program_open_overlay));
        if (!Dm.truthy(trackable(signal)))
        {
            program_open_overlay = Dm.text("[]lost", initial_overlay);
            if (!Dm.eq(last_icon_state, program_open_overlay))
            {
                computer.update_appearance();
                last_icon_state = Dm.str(program_open_overlay);
            }
            return null;
        }

        var here_turf = get_turf(computer) as turf;
        var target_turf = get_turf(signal) as turf;
        float trackdistance = Dm.num(get_dist_euclidean(here_turf, target_turf));
        if (trackdistance == 0)
            program_open_overlay = Dm.text("[]direct", initial_overlay);
        else if (trackdistance >= 1 && trackdistance <= 12)
            program_open_overlay = Dm.text("[]close", initial_overlay);
        else if (trackdistance >= 13 && trackdistance <= 24)
            program_open_overlay = Dm.text("[]medium", initial_overlay);
        else if (trackdistance >= 25)
            program_open_overlay = Dm.text("[]far", initial_overlay);

        if (!Dm.eq(last_icon_state, program_open_overlay))
        {
            computer.update_appearance();
            last_icon_state = Dm.str(program_open_overlay);
        }
        Waiting.NoSleep(computer.setDir(get_dir(here_turf, target_turf)));
        return null;
    }

    //We can use process_tick to restart fast processing, since the computer will be running this constantly either way.
    // tg: code/modules/modular_computers/file_system/programs/radar.dm:233
    public override object process_tick(object seconds_per_tick = null)
    {
        if (computer.active_program == this)
            START_PROCESSING(SSfastprocess, this);
        return null;
    }
}

///A program that tracks crew members via suit sensors
// tg: code/modules/modular_computers/file_system/programs/radar.dm:243
public partial class datum_computer_file_program_radar_lifeline : datum_computer_file_program_radar
{
    // tg: code/modules/modular_computers/file_system/programs/radar.dm:253
    public override object find_atom()
    {
        return base.find_atom() ?? locate_ref_in(selected, GLOB.human_list);
    }

    // tg: code/modules/modular_computers/file_system/programs/radar.dm:256
    public override void scan()
    {
        objects = Dm.list();
        foreach (mob_living_carbon_human humanoid in Dm.each<mob_living_carbon_human>(GLOB.human_list))
        {
            if (!Dm.truthy(trackable(humanoid)))
                continue;
            object crewmember_name = "Unknown";
            if (humanoid.wear_id != null)
            {
                var ID = humanoid.wear_id.GetID() as obj_item_card_id;
                if (Dm.truthy(ID?.registered_name))
                    crewmember_name = ID.registered_name;
            }
            DmList crewinfo = Dm.list(
                Dm.kv("ref", REF(humanoid)),
                Dm.kv("name", crewmember_name));
            Dm.append(ref objects, Dm.list(crewinfo));
        }
    }

    // tg: code/modules/modular_computers/file_system/programs/radar.dm:272
    public override object trackable(atom_movable signal = null)
    {
        object dot = base.trackable(signal);
        if (!Dm.eq(dot, RADAR_TRACKABLE))
            return dot;
        if (signal is not mob_living_carbon_human humanoid)
            return RADAR_NOT_TRACKABLE;
        if (humanoid.w_uniform is not obj_item_clothing_under uniform)
            return RADAR_NOT_TRACKABLE;
        if (!Dm.truthy(uniform.has_sensor) || Dm.num(uniform.sensor_mode) < SENSOR_COORDS) // Suit sensors must be on maximum
            return RADAR_NOT_TRACKABLE;
        return dot;
    }
}

///Tracks all janitor equipment
// tg: code/modules/modular_computers/file_system/programs/radar.dm:285
public partial class datum_computer_file_program_radar_custodial_locator : datum_computer_file_program_radar
{
    // tg: code/modules/modular_computers/file_system/programs/radar.dm:296
    public override object find_atom()
    {
        return base.find_atom() ?? locate_ref_in(selected, GLOB.janitor_devices);
    }

    // tg: code/modules/modular_computers/file_system/programs/radar.dm:299
    public override void scan()
    {
        objects = Dm.list();
        foreach (atom custodial_tools in Dm.each<atom>(GLOB.janitor_devices))
        {
            if (!Dm.truthy(trackable(custodial_tools as atom_movable)))
                continue;
            object tool_name = custodial_tools.name;

            if (custodial_tools is obj_item_mop wet_mop)
                tool_name = Dm.text("[] []", Dm.truthy((wet_mop.reagents as datum_reagents)?.total_volume) ? "Wet" : "Dry", wet_mop.name);

            if (custodial_tools is obj_structure_mop_bucket_janitorialcart janicart)
                tool_name = Dm.text("[] - Water level: [] / []", janicart.name, (janicart.reagents as datum_reagents)?.total_volume, (janicart.reagents as datum_reagents)?.maximum_volume);

            if (custodial_tools is mob_living_basic_bot_cleanbot cleanbots)
                tool_name = Dm.text("[] - []", cleanbots.name, Dm.truthy(Dm.bitand(cleanbots.bot_mode_flags, BOT_MODE_ON)) ? "Online" : "Offline");

            DmList tool_information = Dm.list(
                Dm.kv("ref", REF(custodial_tools)),
                Dm.kv("name", tool_name));
            Dm.append(ref objects, Dm.list(tool_information));
        }
    }
}

///A program that tracks nukes and nuclear accessories
// tg: code/modules/modular_computers/file_system/programs/radar.dm:324
public partial class datum_computer_file_program_radar_fission360 : datum_computer_file_program_radar
{
    // tg: code/modules/modular_computers/file_system/programs/radar.dm:336
    public override async DmTask<object> on_start(mob_living user = null)
    {
        object dot = await base.on_start(user);
        if (!Dm.truthy(dot))
            return dot;

        RegisterSignal(SSdcs, COMSIG_GLOB_NUKE_DEVICE_ARMED, nameof(on_nuke_armed));
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/radar.dm:343
    public override object kill_program(mob user = null)
    {
        UnregisterSignal(SSdcs, COMSIG_GLOB_NUKE_DEVICE_ARMED);
        return base.kill_program(user);
    }

    // tg: code/modules/modular_computers/file_system/programs/radar.dm:347
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(SSdcs, COMSIG_GLOB_NUKE_DEVICE_ARMED);
        return base.Destroy(force);
    }

    // tg: code/modules/modular_computers/file_system/programs/radar.dm:351
    public override object find_atom()
    {
        return base.find_atom() ?? SSpoints_of_interest.get_poi_atom_by_ref(selected);
    }

    // tg: code/modules/modular_computers/file_system/programs/radar.dm:354
    public override void scan()
    {
        objects = Dm.list();

        // All the nukes
        foreach (obj_machinery_nuclearbomb nuke in Dm.each<obj_machinery_nuclearbomb>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_nuclearbomb))))
        {
            DmList nuke_info = Dm.list(
                Dm.kv("ref", REF(nuke)),
                Dm.kv("name", nuke.name));
            Dm.append(ref objects, Dm.list(nuke_info));
        }

        // Dat fukken disk
        var disk = Dm.locate<obj_item_disk_nuclear>(SSpoints_of_interest.real_nuclear_disks);
        DmList disk_info = Dm.list(
            Dm.kv("ref", REF(disk)),
            Dm.kv("name", "Nuke Auth. Disk"));
        Dm.append(ref objects, Dm.list(disk_info));

        // The infiltrator
        var infiltrator = SSshuttle.getShuttle("syndicate") as obj_docking_port_mobile;
        DmList ship_info = Dm.list(
            Dm.kv("ref", REF(infiltrator)),
            Dm.kv("name", "Infiltrator"));
        Dm.append(ref objects, Dm.list(ship_info));
    }

    ///Shows how long until the nuke detonates, if one is active.
    // tg: code/modules/modular_computers/file_system/programs/radar.dm:382
    public override object on_examine(obj_item_modular_computer source = null, mob user = null)
    {
        DmList examine_list = Dm.list();

        foreach (obj_machinery_nuclearbomb bomb in Dm.each<obj_machinery_nuclearbomb>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_nuclearbomb))))
        {
            if (Dm.truthy(bomb.timing))
                Dm.append(ref examine_list, span_danger(I18n.T("Extreme danger. Arming signal detected. Time remaining: [].", bomb.get_time_left())));
        }
        return examine_list;
    }

    /*
     * Signal proc for [COMSIG_GLOB_NUKE_DEVICE_ARMED].
     * Warns anyone nearby or holding the computer that a nuke was armed.
     */
    // tg: code/modules/modular_computers/file_system/programs/radar.dm:394
    [SignalHandler]
    public virtual void on_nuke_armed(datum source = null, obj_machinery_nuclearbomb bomb = null)
    {
        if (computer == null)
            return;

        playsound(computer, "sound/items/nuke_toy_lowpower.ogg", 50, FALSE);
        if (isliving(computer.loc))
            to_chat(computer.loc, span_userdanger(I18n.T("Your [] vibrates and lets out an ominous alarm. Uh oh.", computer.name)));
        else
        {
            computer.audible_message(
                span_danger(I18n.T("[] vibrates and lets out an ominous alarm. Uh oh.", computer)),
                span_notice(I18n.T("[] begins to vibrate rapidly. Wonder what that means...", computer)));
        }
    }
}
