// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/atom/remotecam.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the abstract remote camera and its drone, airlock and polaroid kinds translated; /obj/item/circuit_component/remotecam/bci,
// the brain-computer interface's, comes with the BCI (not-translated.md section 232). The camera a circuit adds is an ordinary
// station camera on the camera net, watched from the camera consoles as any other. The var blocks are generated.

/**
 * # Remote Camera Component
 *
 * Attaches a camera for surveillance-on-the-go.
 */
// tg: code/modules/wiremod/components/atom/remotecam.dm:12
public partial class obj_item_circuit_component_remotecam : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/atom/remotecam.dm:1
    protected const float REMOTECAM_RANGE_FAR = 7;
    // tg: code/modules/wiremod/components/atom/remotecam.dm:2
    protected const float REMOTECAM_RANGE_NEAR = 2;
    // tg: code/modules/wiremod/components/atom/remotecam.dm:3
    protected const float REMOTECAM_ENERGY_USAGE_NEAR = 0.003f * STANDARD_CELL_CHARGE; //Normal components have 0.001 * STANDARD_CELL_CHARGE, this is expensive to livestream footage
    // tg: code/modules/wiremod/components/atom/remotecam.dm:4
    protected const float REMOTECAM_ENERGY_USAGE_FAR = 0.008f * STANDARD_CELL_CHARGE; //Far range vision should be expensive, crank this up 8 times
    // tg: code/modules/wiremod/components/atom/remotecam.dm:5
    protected const float REMOTECAM_EMP_RESET = 90 * SECONDS;

    // tg: code/modules/wiremod/components/atom/remotecam.dm:56
    public override object get_ui_notices()
    {
        DmList dot = base.get_ui_notices() as DmList;
        if (Dm.truthy(camera_range_settable))
        {
            Dm.append(dot, create_ui_notice(Dm.text("Energy Usage For Near (0) Range: [] Per []", display_energy(REMOTECAM_ENERGY_USAGE_NEAR), DisplayTimeText(COMP_CLOCK_DELAY)), "orange", "clock"));
            Dm.append(dot, create_ui_notice(Dm.text("Energy Usage For Far (1) Range: [] Per []", display_energy(REMOTECAM_ENERGY_USAGE_FAR), DisplayTimeText(COMP_CLOCK_DELAY)), "orange", "clock"));
        }
        else
            Dm.append(dot, create_ui_notice(Dm.text("Energy Usage While Active: [] Per []", display_energy(current_camera_range > 0 ? REMOTECAM_ENERGY_USAGE_FAR : REMOTECAM_ENERGY_USAGE_NEAR), DisplayTimeText(COMP_CLOCK_DELAY)), "orange", "clock"));
        return dot;
    }

    // tg: code/modules/wiremod/components/atom/remotecam.dm:64
    public override async DmTask populate_ports()
    {
        start = add_input_port("Start", PORT_TYPE_SIGNAL) as datum_port_input;
        stop = add_input_port("Stop", PORT_TYPE_SIGNAL) as datum_port_input;
        if (Dm.truthy(camera_range_settable))
            camera_range = add_input_port("Far Range", PORT_TYPE_BOOLEAN, @default: FALSE) as datum_port_input;
        network = add_input_port("Network", PORT_TYPE_STRING, @default: "ss13") as datum_port_input;

        if (Dm.truthy(camera_range_settable))
            current_camera_range = Dm.num(camera_range.value);
        c_tag_random = rand(1, 999);
    }

    // tg: code/modules/wiremod/components/atom/remotecam.dm:75
    public override async DmTask register_shell(atom_movable shell = null)
    {
        shell_parent = shell;
        stop_process();
    }

    // tg: code/modules/wiremod/components/atom/remotecam.dm:79
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        stop_process();
        remove_camera();
        shell_parent = null;
    }

    // tg: code/modules/wiremod/components/atom/remotecam.dm:84
    public override object Destroy(float? force = null)
    {
        stop_process();
        remove_camera();
        shell_parent = null;
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/components/atom/remotecam.dm:90
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (shell_parent == null || shell_camera == null)
            return null;
        update_camera_name_network();
        if (COMPONENT_TRIGGERED_BY(start, port))
        {
            start_process();
            cameranet_add();
            current_camera_state = TRUE;
        }
        else if (COMPONENT_TRIGGERED_BY(stop, port))
        {
            stop_process();
            close_camera(); //Instantly turn off the camera
            current_camera_state = FALSE;
        }
        return null;
    }

    /**
     * Initializes the camera
     */
    // tg: code/modules/wiremod/components/atom/remotecam.dm:106
    public virtual void init_camera()
    {
        shell_camera.desc = "This camera belongs in a circuit. If you see this, tell a coder!";
        Waiting.NoSleep(shell_camera.AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL));
        shell_camera.use_power = NO_POWER_USE;
        shell_camera.start_active = TRUE;
        shell_camera.internal_light = FALSE;
        current_camera_name = "";
        if (Dm.truthy(camera_range_settable))
            current_camera_range = Dm.num(camera_range.value);
        current_cameranet_state = TRUE;
        current_camera_emp = FALSE;
        current_camera_network = "";
        close_camera();
        update_camera_range();
        update_camera_name_network();
        if (Dm.truthy(current_camera_state))
        {
            start_process();
            update_camera_location();
        }
        else
            cameranet_remove(); //Remove camera from global cameranet until user activates the camera first
        if (!Dm.truthy(camera_signal_move_override))
            RegisterSignal(shell_parent, COMSIG_MOVABLE_MOVED, nameof(update_camera_location));
        RegisterSignal(shell_parent, COMSIG_ATOM_EMP_ACT, nameof(set_camera_emp));
    }

    /**
     * Remove the camera
     */
    // tg: code/modules/wiremod/components/atom/remotecam.dm:132
    public virtual void remove_camera()
    {
        if (shell_camera == null)
            return;
        if (!Dm.truthy(camera_signal_move_override))
            UnregisterSignal(shell_parent, COMSIG_MOVABLE_MOVED);
        UnregisterSignal(shell_parent, COMSIG_ATOM_EMP_ACT);
        if (Dm.truthy(current_camera_emp))
        {
            deltimer(current_camera_emp_timer_id);
            current_camera_emp = FALSE;
        }
        cameranet_add(); //Readd camera to cameranet before deleting camera
        QDEL_NULL(ref shell_camera);
    }

    /**
     * Close the camera state (only if it's already active)
     */
    // tg: code/modules/wiremod/components/atom/remotecam.dm:148
    public virtual void close_camera()
    {
        if (Dm.truthy(shell_camera?.camera_enabled))
            shell_camera.toggle_cam(null, 0);
    }

    /**
     * Set the camera range
     */
    // tg: code/modules/wiremod/components/atom/remotecam.dm:155
    public virtual void update_camera_range()
    {
        shell_camera.setViewRange(current_camera_range > 0 ? REMOTECAM_RANGE_FAR : REMOTECAM_RANGE_NEAR);
    }

    /**
     * Updates the camera name and network
     */
    // tg: code/modules/wiremod/components/atom/remotecam.dm:161
    public virtual void update_camera_name_network()
    {
        if (parent == null || !Dm.truthy(parent.display_name) || parent.display_name == "")
        {
            shell_camera.c_tag = Dm.text("[]: unspecified #[]", camera_prefix, c_tag_random);
            current_camera_name = "";
        }
        else if (current_camera_name != parent.display_name)
        {
            current_camera_name = parent.display_name;
            object new_cam_name = reject_bad_name(current_camera_name, allow_numbers: TRUE, ascii_only: FALSE, strict: TRUE, cap_after_symbols: FALSE);
            //Set camera name using parent circuit name
            if (Dm.truthy(new_cam_name))
                shell_camera.c_tag = Dm.text("[]: [] #[]", camera_prefix, new_cam_name, c_tag_random);
            else
                shell_camera.c_tag = Dm.text("[]: unspecified #[]", camera_prefix, c_tag_random);
        }

        if (!Dm.truthy(network.value) || Dm.eq(network.value, ""))
        {
            shell_camera.network = Dm.list("ss13");
            current_camera_network = "";
        }
        else if (!Dm.eq(current_camera_network, network.value))
        {
            current_camera_network = (string)network.value;
            string new_net_name = LOWER_TEXT(sanitize(current_camera_network));
            //Set camera network string
            if (Dm.truthy(new_net_name))
                shell_camera.network = Dm.list(Dm.text("[]", new_net_name));
            else
                shell_camera.network = Dm.list("ss13");
        }
    }

    /**
     * Update the chunk for the camera (if enabled)
     */
    // tg: code/modules/wiremod/components/atom/remotecam.dm:191
    [SignalHandler]
    public virtual void update_camera_location(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, object momentum_change = null)
    {
        if (Dm.truthy(current_camera_state) && Dm.truthy(current_cameranet_state))
        {
            if (!Dm.truthy(shell_camera?.can_use()))
                return;
            SScameras.camera_moved(shell_camera, get_turf(old_loc), get_turf(shell_camera), 0.5f * SECONDS);
        }
    }

    /**
     * Add camera from global cameranet
     */
    // tg: code/modules/wiremod/components/atom/remotecam.dm:201
    public virtual void cameranet_add()
    {
        if (Dm.truthy(current_cameranet_state))
            return;
        Dm.append(SScameras.cameras, shell_camera);
        SScameras.add_camera_to_chunk(shell_camera);
        current_cameranet_state = TRUE;
    }

    /**
     * Remove camera from global cameranet
     */
    // tg: code/modules/wiremod/components/atom/remotecam.dm:211
    public virtual void cameranet_remove()
    {
        if (!Dm.truthy(current_cameranet_state))
            return;
        SScameras.remove_camera_from_chunk(shell_camera);
        Dm.remove(SScameras.cameras, shell_camera);
        current_cameranet_state = FALSE;
    }

    /**
     * Set the camera as emp'd
     */
    // tg: code/modules/wiremod/components/atom/remotecam.dm:221
    [SignalHandler]
    public virtual void set_camera_emp(datum source = null, object severity = null, object protection = null)
    {
        if (Dm.truthy(current_camera_emp))
            return;
        if (!prob(150 / Dm.num(severity)))
            return;
        current_camera_emp = TRUE;
        close_camera();
        current_camera_emp_timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(remove_camera_emp)), REMOTECAM_EMP_RESET, TIMER_STOPPABLE, file: "code/modules/wiremod/components/atom/remotecam.dm", line: 229);
        foreach (object M__it in Dm.each(GLOB.player_list))
        {
            mob M = (mob)M__it;
            if (M.client?.eye == shell_camera)
            {
                Waiting.NoSleep(M.reset_perspective(null));
                to_chat(M, span_warning(I18n.T("The screen bursts into static!")));
            }
        }
    }

    /**
     * Restore emp'd camera
     */
    // tg: code/modules/wiremod/components/atom/remotecam.dm:238
    public virtual void remove_camera_emp()
    {
        current_camera_emp = FALSE;
    }

    /**
     * Adds the component to the SSclock_component process list
     *
     * Starts draining cell per second while camera is active
     */
    // tg: code/modules/wiremod/components/atom/remotecam.dm:246
    public virtual void start_process()
    {
        if (SSclock_component != null) // rules 10.6: a test harness world may lack the subsystem
            START_PROCESSING(SSclock_component, this);
    }

    /**
     * Removes the component to the SSclock_component process list
     *
     * Stops draining cell per second
     */
    // tg: code/modules/wiremod/components/atom/remotecam.dm:254
    public virtual void stop_process()
    {
        if (SSclock_component != null) // rules 10.6: a test harness world may lack the subsystem
            STOP_PROCESSING(SSclock_component, this);
    }

    /**
     * Handle power usage and camera state updating
     *
     * This is the generic abstract proc - subtypes with specialized logic should use their own copy of process()
     */
    // tg: code/modules/wiremod/components/atom/remotecam.dm:262
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (shell_parent == null || shell_camera == null)
            return PROCESS_KILL;
        //Camera is currently emp'd
        if (Dm.truthy(current_camera_emp))
        {
            close_camera();
            return null;
        }
        obj_item_stock_parts_power_store cell = parent.get_cell() as obj_item_stock_parts_power_store;
        //If cell doesn't exist, or we ran out of power
        if (!Dm.truthy(cell == null ? null : Waiting.NoSleep(cell.use(current_camera_range > 0 ? REMOTECAM_ENERGY_USAGE_FAR : REMOTECAM_ENERGY_USAGE_NEAR))))
        {
            close_camera();
            return null;
        }
        if (Dm.truthy(camera_range_settable))
        {
            //If the camera range has changed, update camera range
            if (!Dm.truthy(camera_range.value) != !Dm.truthy(current_camera_range))
            {
                current_camera_range = Dm.num(camera_range.value);
                update_camera_range();
            }
        }
        //Set the camera state (if state has been changed)
        if (Dm.truthy(Dm.bitxor(current_camera_state, shell_camera.camera_enabled)))
            shell_camera.toggle_cam(null, 0);
        return null;
    }
}

// tg: code/modules/wiremod/components/atom/remotecam.dm:295
public partial class obj_item_circuit_component_remotecam_drone : obj_item_circuit_component_remotecam
{
    // tg: code/modules/wiremod/components/atom/remotecam.dm:355
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (!istype(shell_parent, typeof(mob_living_circuit_drone)))
            return;
        current_camera_state = FALSE; //Always reset camera state for built-in shell components
        shell_camera = Dm.@new<obj_machinery_camera>(shell_parent);
        init_camera();
    }
}

// tg: code/modules/wiremod/components/atom/remotecam.dm:300
public partial class obj_item_circuit_component_remotecam_airlock : obj_item_circuit_component_remotecam
{
    // tg: code/modules/wiremod/components/atom/remotecam.dm:363
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (!istype(shell_parent, typeof(obj_machinery_door_airlock)))
            return;
        current_camera_state = FALSE; //Always reset camera state for built-in shell components
        shell_camera = Dm.@new<obj_machinery_camera>(shell_parent);
        init_camera();
    }
}

// tg: code/modules/wiremod/components/atom/remotecam.dm:309
public partial class obj_item_circuit_component_remotecam_polaroid : obj_item_circuit_component_remotecam
{
    // tg: code/modules/wiremod/components/atom/remotecam.dm:371
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (!istype(shell_parent, typeof(obj_item_camera)))
            return;
        current_camera_state = FALSE; //Always reset camera state for built-in shell components
        shell_camera = Dm.@new<obj_machinery_camera>(shell_parent);
        init_camera();
    }

    // tg: code/modules/wiremod/components/atom/remotecam.dm:412
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (shell_parent == null || shell_camera == null)
            return PROCESS_KILL;
        //Camera is currently emp'd
        if (Dm.truthy(current_camera_emp))
        {
            close_camera();
            return null;
        }
        //If camera is stored inside of bag or something, turn it off
        if (shell_parent.loc.atom_storage != null)
        {
            close_camera();
            return null;
        }
        obj_item_stock_parts_power_store cell = parent.get_cell() as obj_item_stock_parts_power_store;
        //If cell doesn't exist, or we ran out of power
        if (!Dm.truthy(cell == null ? null : Waiting.NoSleep(cell.use(REMOTECAM_ENERGY_USAGE_NEAR))))
        {
            close_camera();
            return null;
        }
        //Set the camera state (if state has been changed)
        if (Dm.truthy(Dm.bitxor(current_camera_state, shell_camera.camera_enabled)))
            shell_camera.toggle_cam(null, 0);
        return null;
    }
}
