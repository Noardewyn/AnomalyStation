// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/camera/presets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the exosuit camera's procs with the exosuits; the presets and the upgrade procs silicons SI4, D326); the upgrade checks
// are in camera.cs. The var blocks are generated (D111).

/**
 * EMP Proof
 * Starts off with the EMP protection upgrade, and can't start unactivated.
 */
// tg: code/game/machinery/camera/presets.dm:14
public partial class obj_machinery_camera_emp_proof : obj_machinery_camera
{
    // tg: code/game/machinery/camera/presets.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        upgradeEmpProof();
        return dot;
    }
}

/**
 * Motion EMP proof
 * Same as EMP but also starts with motion upgrade.
 */
// tg: code/game/machinery/camera/presets.dm:25
public partial class obj_machinery_camera_emp_proof_motion : obj_machinery_camera_emp_proof
{
    // tg: code/game/machinery/camera/presets.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        upgradeMotion();
        return dot;
    }
}

/**
 * X-Ray Cameras
 * Starts off with x-ray, and can't start deactivated.
 */
// tg: code/game/machinery/camera/presets.dm:33
public partial class obj_machinery_camera_xray : obj_machinery_camera
{
    // tg: code/game/machinery/camera/presets.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        upgradeXRay();
        return dot;
    }
}

/**
 * Motion camera
 * Starts off with the motion detector and can't be disablede on roundstart.
 */
// tg: code/game/machinery/camera/presets.dm:45
public partial class obj_machinery_camera_motion : obj_machinery_camera
{
    // tg: code/game/machinery/camera/presets.dm:49
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        upgradeMotion();
        return dot;
    }
}

/**
 * All camera
 * Has all upgrades by default, can't be disabled roundstart.
 */
// tg: code/game/machinery/camera/presets.dm:57
public partial class obj_machinery_camera_all : obj_machinery_camera
{
    // tg: code/game/machinery/camera/presets.dm:61
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        upgradeEmpProof();
        upgradeXRay();
        upgradeMotion();
        return dot;
    }
}

/**
 * Autonaming camera
 * Automatically names itself after the area it's in during post_machine_initialize,
 * good for mappers who don't want to manually name them all.
 */
// tg: code/game/machinery/camera/presets.dm:72
public partial class obj_machinery_camera_autoname : obj_machinery_camera
{
    // tg: code/game/machinery/camera/presets.dm:75
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/machinery/camera/presets.dm:79
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        DmList autonames_in_areas = TgState.Current.obj_machinery_camera_autoname__autonames_in_areas ??= Dm.list(); // var/static/list/autonames_in_areas
        area camera_area = get_area(this) as area;
        number = Dm.num(autonames_in_areas[camera_area]) + 1;
        autonames_in_areas[camera_area] = number;
        c_tag = Dm.text("[] #[]", format_text(camera_area.name), number);
    }
}

// tg: code/game/machinery/camera/presets.dm:87
public partial class obj_machinery_camera_autoname_motion : obj_machinery_camera_autoname
{
    // tg: code/game/machinery/camera/presets.dm:91
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        upgradeMotion();
        return dot;
    }
}

// tg: code/game/machinery/camera/presets.dm:110
public partial class obj_machinery_camera_preset_siren_pirates : obj_machinery_camera_preset
{
    // tg: code/game/machinery/camera/presets.dm:116
    public override async DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        await DmTask.CompletedTask;
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/camera/presets.dm:81  var/static/list/autonames_in_areas
    internal DmList obj_machinery_camera_autoname__autonames_in_areas;
}

// tg: code/game/machinery/camera/presets.dm:119
public partial class obj_machinery_camera_exosuit : obj_machinery_camera
{
    ///Restore the camera's view default view range after an EMP
    // tg: code/game/machinery/camera/presets.dm:133
    public virtual void emp_refocus(obj_vehicle_sealed_mecha our_chassis = null)
    {
        is_emp_scrambled = FALSE;
        setViewRange(Dm.num(Dm.initial(this, nameof(view_range))));
        our_chassis.diag_hud_set_camera();
    }

    ///Updates the c_tag of the mech camera while preventing duplicate c_tag usage due to having mechs with the same name
    // tg: code/game/machinery/camera/presets.dm:139
    public virtual void update_c_tag(obj_vehicle_sealed_mecha mech = null)
    {
        //List of all used mech names
        DmList existing_mech_names = GLOB.exosuit_camera_names ??= Dm.list();
        //Name of the mech passed with this proc. We use format_text to wipe away stuff like `\initial` to prevent c_tag from erroring out
        object mech_name = format_text(mech.name);

        if (Dm.truthy(current_name) && !Dm.eq(current_name, mech_name)) //decrease by 1 to preserve correct naming numeration
            existing_mech_names[current_name] = Dm.num(existing_mech_names[current_name]) - 1;
        number = Dm.num(existing_mech_names[mech_name]) + 1;
        existing_mech_names[mech_name] = number;

        c_tag = Dm.text("Exosuit: [] #[]", mech_name, number);
    }
}

// UPGRADE PROCS (silicons SI4, D326)
public partial class obj_machinery_camera : obj_machinery
{
    // tg: code/game/machinery/camera/presets.dm:158
    public virtual void upgradeEmpProof(object malf_upgrade = null, object ignore_malf_upgrades = null)
    {
        if (Dm.truthy(isEmpProof(ignore_malf_upgrades))) //pass a malf upgrade to ignore_malf_upgrades so we can replace the malf module with the normal one
            return; //that way if someone tries to upgrade an already malf-upgraded camera, it'll just upgrade it to a normal version.
        if (Dm.truthy(malf_upgrade))
        {
            malf_emp_firmware_active = TRUE; //don't add parts to drop, update icon, ect. reconstructing it will also retain the upgrade.
            malf_emp_firmware_present = TRUE; //so the upgrade is retained after incompatible parts are removed.
            Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), Dm.bitor(EMP_PROTECT_ALL, EMP_NO_EXAMINE)));
        }
        else if (emp_module == null) //only happens via upgrading in camera/attackby()
        {
            emp_module = Dm.@new<obj_item_stack_sheet_mineral_plasma>(this);
            malf_emp_firmware_active = FALSE; //make it appear like it's just normally upgraded so the icons and examine texts are restored.
            Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL));
        }

        camera_upgrade_bitflags = Dm.bitor(camera_upgrade_bitflags, CAMERA_UPGRADE_EMP_PROOF);
        calculate_active_power();
    }

    // tg: code/game/machinery/camera/presets.dm:174
    public virtual void removeEmpProof(object ignore_malf_upgrades = null)
    {
        if (Dm.truthy(ignore_malf_upgrades)) //don't downgrade it if malf software is forced onto it.
            return;
        RemoveElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL);
        RemoveElement(typeof(datum_element_empprotection), Dm.bitor(EMP_PROTECT_ALL, EMP_NO_EXAMINE));
        camera_upgrade_bitflags = Dm.bitand(camera_upgrade_bitflags, Dm.bitnot(CAMERA_UPGRADE_EMP_PROOF));
        calculate_active_power();
    }

    // tg: code/game/machinery/camera/presets.dm:185
    public virtual void upgradeXRay(object malf_upgrade = null, object ignore_malf_upgrades = null)
    {
        if (Dm.truthy(isXRay(ignore_malf_upgrades))) //pass a malf upgrade to ignore_malf_upgrades so we can replace the malf upgrade with the normal one
            return; //that way if someone tries to upgrade an already malf-upgraded camera, it'll just upgrade it to a normal version.
        if (Dm.truthy(malf_upgrade))
        {
            malf_xray_firmware_active = TRUE; //don't add parts to drop, update icon, ect. reconstructing it will also retain the upgrade.
            malf_xray_firmware_present = TRUE; //so the upgrade is retained after incompatible parts are removed.
        }
        else if (xray_module == null) //only happens via upgrading in camera/attackby()
        {
            xray_module = Dm.@new<obj_item_analyzer>(this);
            if (Dm.truthy(malf_xray_firmware_active))
                malf_xray_firmware_active = FALSE; //make it appear like it's just normally upgraded so the icons and examine texts are restored.
        }

        camera_upgrade_bitflags = Dm.bitor(camera_upgrade_bitflags, CAMERA_UPGRADE_XRAY);
        update_appearance();
        SScameras.update_visibility(this);
        calculate_active_power();
    }

    // tg: code/game/machinery/camera/presets.dm:202
    public virtual void removeXRay(object ignore_malf_upgrades = null)
    {
        if (!Dm.truthy(ignore_malf_upgrades)) //don't downgrade it if malf software is forced onto it.
            camera_upgrade_bitflags = Dm.bitand(camera_upgrade_bitflags, Dm.bitnot(CAMERA_UPGRADE_XRAY));
        update_appearance();
        calculate_active_power();
    }

    // tg: code/game/machinery/camera/presets.dm:211
    public virtual void upgradeMotion()
    {
        if (Dm.truthy(isMotion()))
            return;
        if (Dm.eq(name, Dm.initial(GetType(), (atom a) => a.name)))
            name = "motion-sensitive security camera";
        if (proximity_monitor == null)
            proximity_monitor = Dm.@new<datum_proximity_monitor>(this);
        camera_upgrade_bitflags = Dm.bitor(camera_upgrade_bitflags, CAMERA_UPGRADE_MOTION);
        create_prox_monitor();
        calculate_active_power();
    }

    // tg: code/game/machinery/camera/presets.dm:223
    public virtual void removeMotion()
    {
        if (Dm.eq(name, "motion-sensitive security camera"))
            name = "security camera";
        camera_upgrade_bitflags = Dm.bitand(camera_upgrade_bitflags, Dm.bitnot(CAMERA_UPGRADE_MOTION));
        if (area_motion == null)
            QDEL_NULL(ref proximity_monitor);
        calculate_active_power();
    }
}
