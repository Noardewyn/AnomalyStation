// Translated from tgstation (snapshot 1d99bd7f): code/game/data_huds.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M8 slice F, D185): the medical HUD (health bar, status), the basic health HUD, the security HUD's job and implant icons and its
// wanted status, the fan and blood HUDs, and the image helpers. Not translated, with their rows (not-translated.md section 55): the
// malfunctioning AI's hacked-APC HUD (a live-partial row, with the malfunction, silicons SI8); silicons SI1, SI2 and SI4 (D326) added the
// silicons', the cyborgs', the AI's and the airlocks' diagnostic HUD (the mechs' came with the mechs),
// and get_cached_width and get_cached_height of an image. The megafauna added get_visual_width and get_visual_height (the cleaving saw's
// bleed scales to its target).

/* DATA HUD DATUMS */

public static partial class GlobalProcs
{
    //helper for getting the appropriate health status
    // tg: code/game/data_huds.dm:107
    public static string RoundDiagBar(float value)
    {
        float percent = value * 100;
        if (percent >= 95)
            return "max";
        if (percent >= 80)
            return "good";
        if (percent >= 60)
            return "high";
        if (percent >= 40)
            return "med";
        if (percent >= 20)
            return "low";
        return "min";
    }

    public static object RoundHealth(mob_living M = null)
    {
        if (IS_DEAD_OR_FAKING(M))
            return "health-100"; //what's our health? it doesn't matter, we're dead, or faking
        float maxi_health = M.maxHealth;
        if (iscarbon(M) && M.health < 0)
            maxi_health = 100; //so crit shows up right for aliens and other high-health carbon mobs; noncarbons don't have crit.
        float resulthealth = (M.health / maxi_health) * 100;
        if (Dm.in_to(resulthealth, 100, INFINITY))
            return "health100";
        if (Dm.in_to(resulthealth, 90.625f, 100))
            return "health93.75";
        if (Dm.in_to(resulthealth, 84.375f, 90.625f))
            return "health87.5";
        if (Dm.in_to(resulthealth, 78.125f, 84.375f))
            return "health81.25";
        if (Dm.in_to(resulthealth, 71.875f, 78.125f))
            return "health75";
        if (Dm.in_to(resulthealth, 65.625f, 71.875f))
            return "health68.75";
        if (Dm.in_to(resulthealth, 59.375f, 65.625f))
            return "health62.5";
        if (Dm.in_to(resulthealth, 53.125f, 59.375f))
            return "health56.25";
        if (Dm.in_to(resulthealth, 46.875f, 53.125f))
            return "health50";
        if (Dm.in_to(resulthealth, 40.625f, 46.875f))
            return "health43.75";
        if (Dm.in_to(resulthealth, 34.375f, 40.625f))
            return "health37.5";
        if (Dm.in_to(resulthealth, 28.125f, 34.375f))
            return "health31.25";
        if (Dm.in_to(resulthealth, 21.875f, 28.125f))
            return "health25";
        if (Dm.in_to(resulthealth, 15.625f, 21.875f))
            return "health18.75";
        if (Dm.in_to(resulthealth, 9.375f, 15.625f))
            return "health12.5";
        if (Dm.in_to(resulthealth, 1, 9.375f))
            return "health6.25";
        if (Dm.in_to(resulthealth, -50, 1))
            return "health0";
        if (Dm.in_to(resulthealth, -85, -50))
            return "health-50";
        if (Dm.in_to(resulthealth, -99, -85))
            return "health-85";
        return "health-100";
    }

    /// A helper for getting the appropriate icon state for the blood hud.
    // tg: code/game/data_huds.dm:157
    public static object round_blood_for_hud(mob_living bloodbag = null)
    {
        float blood_level = (Dm.num(bloodbag.get_blood_volume(apply_modifiers: TRUE)) / BLOOD_VOLUME_NORMAL) * 100;
        if (Dm.in_to(blood_level, 87.5f, INFINITY))
            return "hudblood100";
        if (Dm.in_to(blood_level, 62.5f, 87.5f))
            return "hudblood75";
        if (Dm.in_to(blood_level, 37.5f, 62.5f))
            return "hudblood50";
        if (Dm.in_to(blood_level, 12.5f, 37.5f))
            return "hudblood25";
        if (Dm.in_to(blood_level, -INFINITY, 12.5f))
            return "hudblood0";
        return null;
    }

    /**
     * Updates the visual security huds on all mobs in GLOB.human_list that match the name passed to it.
     */
    // tg: code/game/data_huds.dm:338
    public static void update_matching_security_huds(object perp_name = null)
    {
        foreach (mob_living_carbon_human h in Dm.each_cast<mob_living_carbon_human>(GLOB.human_list))
        {
            if (Dm.eq(h.get_face_name((string)h.get_id_name("")), perp_name))
                h.sec_hud_set_security_status();
        }
    }

    /**
     * Updates the visual security huds on all mobs in GLOB.human_list
     */
    // tg: code/game/data_huds.dm:346
    public static void update_all_security_huds()
    {
        foreach (mob_living_carbon_human h in Dm.each_cast<mob_living_carbon_human>(GLOB.human_list))
            h.sec_hud_set_security_status();
    }
}

public partial class atom : datum
{
    // tg: code/game/data_huds.dm:10
    public virtual void add_to_all_human_data_huds()
    {
        foreach ((object hud_key, object hud_type) in Dm.pairs(GLOB.huds))
            (hud_type as datum_atom_hud_data_human)?.add_atom_to_hud(this);
    }

    // tg: code/game/data_huds.dm:14
    public virtual void remove_from_all_data_huds()
    {
        foreach ((object hud_key, object hud_type) in Dm.pairs(GLOB.huds))
            (hud_type as datum_atom_hud_data)?.remove_atom_from_hud(this);
    }

    // tg: code/game/data_huds.dm:490
    // DEVIATION(DV108): the core knows no .dmi metadata (D104), so an icon's size is 32 by 32 here: get_icon_dimensions is not translated.
    public virtual object get_cached_width()
    {
        if (icon == null)
            return 0;
        return ICON_SIZE_X;
    }

    // tg: code/game/data_huds.dm:496  (see get_cached_width, DV108)
    public virtual object get_cached_height()
    {
        if (icon == null)
            return 0;
        return ICON_SIZE_Y;
    }

    // tg: code/game/data_huds.dm:517
    public virtual object get_visual_width()
    {
        float width = Dm.num(get_cached_width());
        float height = Dm.num(get_cached_height());
        matrix t = transform as matrix ?? Dm.matrix();
        DmList scale_list = Dm.list(
            width * t.a + height * t.b + t.c,
            width * t.a + t.c,
            height * t.b + t.c,
            t.c);
        return Dm.num(max(scale_list)) - Dm.num(min(scale_list));
    }

    // tg: code/game/data_huds.dm:528
    public virtual object get_visual_height()
    {
        float width = Dm.num(get_cached_width());
        float height = Dm.num(get_cached_height());
        matrix t = transform as matrix ?? Dm.matrix();
        DmList scale_list = Dm.list(
            width * t.d + height * t.e + t.f,
            width * t.d + t.f,
            height * t.e + t.f,
            t.f);
        return Dm.num(max(scale_list)) - Dm.num(min(scale_list));
    }

    // tg: code/game/data_huds.dm:539
    public virtual object get_hud_x_offset()
    {
        return -(Dm.num(get_cached_width()) - ICON_SIZE_X) / 2;
    }

    // tg: code/game/data_huds.dm:542
    public virtual object get_hud_y_offset()
    {
        return Dm.num(get_cached_height()) - ICON_SIZE_Y;
    }

    // tg: code/game/data_huds.dm:545
    public virtual void adjust_hud_position(image holder = null, object animate_time = null)
    {
        if (Dm.truthy(animate_time))
        {
            animate(holder, Dm.named("pixel_w", get_hud_x_offset()), Dm.named("pixel_z", get_hud_y_offset()), Dm.named("time", animate_time));
            return;
        }
        holder.pixel_w = Dm.num(get_hud_x_offset());
        holder.pixel_z = Dm.num(get_hud_y_offset());
    }

    // tg: code/game/data_huds.dm:552
    public virtual void set_hud_image_state(object hud_type = null, object hud_icon = null, object hud_state = null, float? x_offset = null, float? y_offset = null)
    {
        hud_icon ??= Dm.resource("icons/mob/huds/hud.dmi"); // DEFAULT_HUDS_DMI (__DEFINES/atom_hud.dm:75), expanded inline (rules 3.7)
        x_offset ??= 0;
        y_offset ??= 0;
        if (!Dm.truthy(hud_list)) // Still initializing
            return;
        object holder_value = hud_list[hud_type];
        if (!Dm.truthy(holder_value))
            return;
        if (!(holder_value is image holder)) // Can contain lists for HUD_LIST_LIST hinted HUDs, if someone fucks up and passes this here we wanna know about it
            throw Dm.CRASH(Dm.text("[] ([]) had a HUD_LIST_LIST hud_type [] passed into set_hud_image_state!", this, type, hud_type), "code/game/data_huds.dm", 561);
        holder.icon = hud_icon;
        holder.icon_state = hud_state as string;
        adjust_hud_position(holder);
        if (Dm.truthy(x_offset) || Dm.truthy(y_offset))
        {
            holder.pixel_w += x_offset.Value;
            holder.pixel_z += y_offset.Value;
        }
    }
}

// tg: code/game/data_huds.dm:26
public partial class datum_atom_hud_data_human_medical_basic : datum_atom_hud_data_human_medical
{
    // tg: code/game/data_huds.dm:26
    public override void add_atom_to_single_mob_hud(mob requesting_mob = null, atom hud_atom = null)
    {
        if (HAS_TRAIT(hud_atom, TRAIT_BASIC_HEALTH_HUD_VISIBLE))
            base.add_atom_to_single_mob_hud(requesting_mob, hud_atom);
    }
}

// tg: code/game/data_huds.dm:59
public partial class datum_atom_hud_ai_detector : datum_atom_hud
{
    // tg: code/game/data_huds.dm:61
    public override void show_to(mob new_viewer = null)
    {
        base.show_to(new_viewer);
        if (!Dm.truthy(new_viewer) || Dm.length(hud_users_all_z_levels) != 1)
            return;
        foreach (mob_eye_camera_ai eye in Dm.each<mob_eye_camera_ai>(GLOB.camera_eyes))
            eye.update_ai_detect_hud();
    }
}

/***********************************************
Medical HUD! Basic mode needs suit sensors on.
************************************************/

//HELPERS

public partial class mob_living_carbon : mob_living
{
    //called when a carbon changes virus
    // tg: code/game/data_huds.dm:87
    public virtual object check_virus()
    {
        object threat = null;
        object severity = null;
        if (HAS_TRAIT(this, TRAIT_DISEASELIKE_SEVERITY_MEDIUM))
        {
            severity = DISEASE_SEVERITY_MEDIUM;
            threat = get_disease_severity_value(severity);
        }

        if (HAS_TRAIT(this, TRAIT_DISEASELIKE_SEVERITY_HIGH))
        {
            severity = DISEASE_SEVERITY_DANGEROUS;
            threat = get_disease_severity_value(severity);
        }

        foreach (datum_disease D in Dm.each_cast<datum_disease>(diseases))
        {
            if (!Dm.truthy(Dm.bitand(D.visibility_flags, HIDDEN_SCANNER)))
            {
                if (!Dm.truthy(threat) || Dm.num(get_disease_severity_value(D.severity)) > Dm.num(threat)) //a buffing virus gets an icon
                {
                    threat = get_disease_severity_value(D.severity);
                    severity = D.severity;
                }
            }
        }
        return severity;
    }
}

//HOOKS

public partial class mob_living : mob
{
    //called when a living mob changes health
    // tg: code/game/data_huds.dm:174
    public virtual void med_hud_set_health()
    {
        set_hud_image_state(HEALTH_HUD, hud_state: Dm.text("hud[]", RoundHealth(this)));
    }

    // Called when a carbon changes stat, virus or XENO_HOST
    // Returns TRUE if the mob is considered "perfectly healthy", FALSE otherwise
    // tg: code/game/data_huds.dm:179
    public virtual object med_hud_set_status()
    {
        if (IS_DEAD_OR_FAKING(this))
        {
            set_hud_image_state(STATUS_HUD, hud_state: "huddead");
            return FALSE;
        }

        set_hud_image_state(STATUS_HUD, hud_state: "hudhealthy");
        return TRUE;
    }

    // tg: code/game/data_huds.dm:277
    public virtual void sec_hud_set_implants()
    {
        foreach (object hud_type in Dm.each(Dm.bitand(Dm.list(IMPSEC_FIRST_HUD, IMPLOYAL_HUD, IMPSEC_SECOND_HUD), hud_list)))
            set_hud_image_inactive(hud_type);

        float security_slot = 1; //Which of the two security hud slots are we putting found security implants in?
        foreach (obj_item_implant current_implant in Dm.each<obj_item_implant>(implants))
        {
            if (Dm.truthy(Dm.bitand(current_implant.implant_flags, IMPLANT_TYPE_SECURITY)))
            {
                if (security_slot == 1)
                {
                    set_hud_image_state(IMPSEC_FIRST_HUD, hud_state: current_implant.hud_icon_state);
                    set_hud_image_active(IMPSEC_FIRST_HUD);
                    security_slot++;
                }

                else if (security_slot == 2) //Theoretically if we somehow get multiple sec implants, whatever the most recently implanted implant is will take over the 2nd position
                {
                    set_hud_image_state(IMPSEC_SECOND_HUD, hud_state: current_implant.hud_icon_state, x_offset: (ICON_SIZE_X / 4 - 1)); //Adds an offset that mirrors the hud blip to the other side of the mob
                    set_hud_image_active(IMPSEC_SECOND_HUD);
                }
            }
        }

        if (HAS_TRAIT(this, TRAIT_MINDSHIELD))
        {
            set_hud_image_state(IMPLOYAL_HUD, hud_state: "hud_imp_loyal");
            set_hud_image_active(IMPLOYAL_HUD);
        }
    }

    // tg: code/game/data_huds.dm:483
    public virtual void blood_hud_set_status()
    {
        if (Dm.truthy(CAN_HAVE_BLOOD(this)))
            set_hud_image_state(BLOOD_HUD, hud_state: round_blood_for_hud(this));
    }
}

public partial class mob_living_carbon : mob_living
{
    // tg: code/game/data_huds.dm:187
    public override object med_hud_set_status()
    {
        if (HAS_TRAIT(this, TRAIT_XENO_HOST))
        {
            set_hud_image_state(STATUS_HUD, hud_state: "hudxeno");
            return FALSE;
        }

        if (IS_DEAD_OR_FAKING(this))
        {
            if (Dm.truthy(can_defib_client()))
                set_hud_image_state(STATUS_HUD, hud_state: "huddefib");
            else if (HAS_TRAIT(this, TRAIT_GHOSTROLE_ON_REVIVE))
                set_hud_image_state(STATUS_HUD, hud_state: "hudghost");
            else
                set_hud_image_state(STATUS_HUD, hud_state: "huddead");
            return FALSE;
        }

        object virus_threat = check_virus();
        if (!Dm.truthy(virus_threat))
        {
            set_hud_image_state(STATUS_HUD, hud_state: "hudhealthy");
            return TRUE;
        }

        if (Dm.eq(virus_threat, DISEASE_SEVERITY_UNCURABLE))
            set_hud_image_state(STATUS_HUD, hud_state: "hudill6");
        else if (Dm.eq(virus_threat, DISEASE_SEVERITY_BIOHAZARD))
            set_hud_image_state(STATUS_HUD, hud_state: "hudill5");
        else if (Dm.eq(virus_threat, DISEASE_SEVERITY_DANGEROUS))
            set_hud_image_state(STATUS_HUD, hud_state: "hudill4");
        else if (Dm.eq(virus_threat, DISEASE_SEVERITY_HARMFUL))
            set_hud_image_state(STATUS_HUD, hud_state: "hudill3");
        else if (Dm.eq(virus_threat, DISEASE_SEVERITY_MEDIUM))
            set_hud_image_state(STATUS_HUD, hud_state: "hudill2");
        else if (Dm.eq(virus_threat, DISEASE_SEVERITY_MINOR))
            set_hud_image_state(STATUS_HUD, hud_state: "hudill1");
        else if (Dm.eq(virus_threat, DISEASE_SEVERITY_NONTHREAT))
            set_hud_image_state(STATUS_HUD, hud_state: "hudill0");
        else if (Dm.eq(virus_threat, DISEASE_SEVERITY_POSITIVE))
            set_hud_image_state(STATUS_HUD, hud_state: "hudbuff");
        return FALSE;
    }
}

public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/game/data_huds.dm:225
    public override object med_hud_set_status()
    {
        object dot = base.med_hud_set_status();
        if (!Dm.truthy(dot))
            return dot;
        obj_item_clothing_under uniform = w_uniform as obj_item_clothing_under;
        if (uniform != null && Dm.eq(uniform.has_sensor, BROKEN_SENSORS))
        {
            set_hud_image_state(STATUS_HUD, hud_state: "hudnosensor");
            return FALSE;
        }
        return dot;
    }

    /***********************************************
    FAN HUDs! For identifying other fans on-sight.
    ************************************************/

    //HOOKS

    // tg: code/game/data_huds.dm:241
    public virtual void fan_hud_set_fandom()
    {
        obj_item_clothing_under undershirt = w_uniform as obj_item_clothing_under;
        if (undershirt == null)
        {
            set_hud_image_inactive(FAN_HUD);
            return;
        }

        set_hud_image_active(FAN_HUD);
        foreach (object accessory in Dm.each(undershirt.attached_accessories))
        {
            if (accessory is obj_item_clothing_accessory_mime_fan_pin)
            {
                set_hud_image_state(FAN_HUD, hud_state: "mime_fan_pin");
                return;
            }

            if (accessory is obj_item_clothing_accessory_clown_enjoyer_pin)
            {
                set_hud_image_state(FAN_HUD, hud_state: "clown_enjoyer_pin");
                return;
            }
        }

        set_hud_image_state(FAN_HUD, hud_state: "hudfan_no");
    }

    /***********************************************
    Security HUDs! Basic mode shows only the job.
    ************************************************/

    //HOOKS

    // tg: code/game/data_huds.dm:265
    [SignalHandler]
    public virtual void update_ID_card()
    {
        object sechud_icon = wear_id?.get_sechud_job_icon();
        object sechud_icon_state = wear_id?.get_sechud_job_icon_state();
        if (!Dm.truthy(sechud_icon_state) || HAS_TRAIT(this, TRAIT_UNKNOWN_APPEARANCE))
        {
            sechud_icon = Dm.resource("icons/mob/huds/hud.dmi"); // DEFAULT_HUDS_DMI
            sechud_icon_state = "hudno_id";
        }
        set_hud_image_state(ID_HUD, sechud_icon, sechud_icon_state);
        sec_hud_set_security_status();
        update_visible_name();
    }

    // tg: code/game/data_huds.dm:298
    public virtual void sec_hud_set_security_status()
    {
        if (!Dm.truthy(hud_list))
        {
            // We haven't finished initializing yet, huds will be updated once we are
            return;
        }

        if (HAS_TRAIT(this, TRAIT_ALWAYS_WANTED))
        {
            set_hud_image_state(WANTED_HUD, hud_state: "hudwanted");
            set_hud_image_active(WANTED_HUD);
            return;
        }

        object perp_name = get_face_name((string)get_id_name(""));

        if (!Dm.truthy(perp_name) || !Dm.truthy(GLOB.manifest))
        {
            set_hud_image_inactive(WANTED_HUD);
            return;
        }

        datum_record_crew target = find_record(perp_name) as datum_record_crew;
        if (target == null || Dm.eq(target.wanted_status, WANTED_NONE))
        {
            set_hud_image_inactive(WANTED_HUD);
            return;
        }

        if (Dm.eq(target.wanted_status, WANTED_ARREST))
            set_hud_image_state(WANTED_HUD, hud_state: "hudwanted");
        else if (Dm.eq(target.wanted_status, WANTED_PRISONER))
            set_hud_image_state(WANTED_HUD, hud_state: "hudincarcerated");
        else if (Dm.eq(target.wanted_status, WANTED_SUSPECT))
            set_hud_image_state(WANTED_HUD, hud_state: "hudsuspected");
        else if (Dm.eq(target.wanted_status, WANTED_PAROLE))
            set_hud_image_state(WANTED_HUD, hud_state: "hudparolled");
        else if (Dm.eq(target.wanted_status, WANTED_DISCHARGED))
            set_hud_image_state(WANTED_HUD, hud_state: "huddischarged");
        set_hud_image_active(WANTED_HUD);
    }
}

/*~~~~~~~~~~~~~~~~~~~~
    BIG STOMPY MECHS
~~~~~~~~~~~~~~~~~~~~~*/
// tg: code/game/data_huds.dm:419
public partial class obj_vehicle_sealed_mecha : obj_vehicle_sealed
{
    // tg: code/game/data_huds.dm:419
    public virtual void diag_hud_set_mechhealth() => set_hud_image_state(DIAG_MECH_HUD, hud_state: Dm.text("huddiag[]", RoundDiagBar(Dm.num(atom_integrity) / Dm.num(max_integrity))));

    // tg: code/game/data_huds.dm:422
    public virtual void diag_hud_set_mechcell()
    {
        if (QDELETED(cell) || Dm.eq(cell.maxcharge, 0))
            set_hud_image_state(DIAG_BATT_HUD, hud_state: "hudnobatt");
        else
        {
            float chargelvl = Dm.num(cell.charge) / Dm.num(cell.maxcharge);
            set_hud_image_state(DIAG_BATT_HUD, hud_state: Dm.text("hudbatt[]", RoundDiagBar(chargelvl)));
        }
    }

    // tg: code/game/data_huds.dm:429
    public virtual void diag_hud_set_mechstat()
    {
        if (!Dm.truthy(internal_damage))
        {
            set_hud_image_inactive(DIAG_STAT_HUD);
            return;
        }

        set_hud_image_state(DIAG_STAT_HUD, hud_state: "hudwarn");
        set_hud_image_active(DIAG_STAT_HUD);
    }

    ///Shows tracking beacons on the mech
    // tg: code/game/data_huds.dm:438
    public virtual void diag_hud_set_mechtracking()
    {
        object new_icon_state = null; //This var exists so that the holder's icon state is set only once in the event of multiple mech beacons.
        foreach (obj_item_mecha_parts_mecha_tracking tracker in Dm.each<obj_item_mecha_parts_mecha_tracking>(trackers))
        {
            if (Dm.truthy(tracker.ai_beacon)) //Beacon with AI uplink
            {
                new_icon_state = "hudtrackingai";
                break; //Immediately terminate upon finding an AI beacon to ensure it is always shown over the normal one, as mechs can have several trackers.
            }
            else
                new_icon_state = "hudtracking";
        }
        set_hud_image_state(DIAG_TRACK_HUD, hud_state: new_icon_state);
    }

    ///Shows inbuilt camera on the mech; if the camera's view range was affected by an EMP, shows a red blip while it's affected
    // tg: code/game/data_huds.dm:449
    public virtual void diag_hud_set_camera()
    {
        if (chassis_camera == null)
        {
            set_hud_image_inactive(DIAG_CAMERA_HUD);
            return;
        }

        set_hud_image_active(DIAG_CAMERA_HUD);
        if (Dm.truthy(chassis_camera?.is_emp_scrambled))
            set_hud_image_state(DIAG_CAMERA_HUD, hud_state: "hudcamera_empd");
        else
            set_hud_image_state(DIAG_CAMERA_HUD, hud_state: "hudcamera");
    }
}

// tg: code/game/data_huds.dm:389
public partial class mob_living_silicon_robot
{
    //Borgie battery tracking!
    // tg: code/game/data_huds.dm:389
    public virtual void diag_hud_set_borgcell()
    {
        obj_item_stock_parts_power_store cell = this.cell as obj_item_stock_parts_power_store;
        if (QDELETED(cell) || Dm.num(cell.maxcharge) == 0)
            set_hud_image_state(DIAG_BATT_HUD, hud_state: "hudnobatt");
        else
        {
            float chargelvl = Dm.num(cell.charge) / Dm.num(cell.maxcharge);
            set_hud_image_state(DIAG_BATT_HUD, hud_state: Dm.text("hudbatt[]", RoundDiagBar(chargelvl)));
        }
    }

    //borg-AI shell tracking
    // tg: code/game/data_huds.dm:397
    public virtual void diag_hud_set_aishell() //Shows if AI is controlling a cyborg via a BORIS module
    {
        if (!Dm.truthy(shell)) //Not an AI shell
        {
            set_hud_image_inactive(DIAG_TRACK_HUD);
            return;
        }
        if (Dm.truthy(deployed)) //AI shell in use by an AI
            set_hud_image_state(DIAG_TRACK_HUD, hud_state: "hudtrackingai");
        else //Empty AI shell
            set_hud_image_state(DIAG_TRACK_HUD, hud_state: "hudtracking");
        set_hud_image_active(DIAG_TRACK_HUD);
    }
}

// tg: code/game/data_huds.dm:408
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/game/data_huds.dm:408
    public virtual void diag_hud_set_deployed() //Shows if AI is currently shunted into a BORIS borg
    {
        if (deployed_shell == null)
        {
            set_hud_image_inactive(DIAG_TRACK_HUD);
            return;
        }
        //AI is currently controlling a shell
        set_hud_image_state(DIAG_TRACK_HUD, hud_state: "hudtrackingai");
        set_hud_image_active(DIAG_TRACK_HUD);
    }
}

/*~~~~~~~~~~~~
    Airlocks!
~~~~~~~~~~~~~*/
// tg: code/game/data_huds.dm:463
public partial class obj_machinery_door_airlock
{
    // tg: code/game/data_huds.dm:463
    public virtual void diag_hud_set_electrified()
    {
        if (secondsElectrified == MACHINE_NOT_ELECTRIFIED)
        {
            set_hud_image_inactive(DIAG_AIRLOCK_HUD);
            return;
        }

        image holder = hud_list[DIAG_AIRLOCK_HUD] as image;
        holder.icon_state = "electrified";
        set_hud_image_active(DIAG_AIRLOCK_HUD);
    }
}

//Sillycone hooks
// tg: code/game/data_huds.dm:373
public partial class mob_living_silicon
{
    // tg: code/game/data_huds.dm:373
    public virtual void diag_hud_set_health()
    {
        if (stat == DEAD)
            set_hud_image_state(DIAG_HUD, hud_state: "huddiagdead");
        else
            set_hud_image_state(DIAG_HUD, hud_state: Dm.text("huddiag[]", RoundDiagBar(Dm.num(health) / Dm.num(maxHealth))));
    }

    // tg: code/game/data_huds.dm:379
    public virtual void diag_hud_set_status()
    {
        if (IS_UNCONSCIOUS(this))
        {
            set_hud_image_state(DIAG_STAT_HUD, hud_state: "hudoffline");
            return;
        }
        if (stat == DEAD)
        {
            set_hud_image_state(DIAG_STAT_HUD, hud_state: "huddead2");
            return;
        }
        set_hud_image_state(DIAG_STAT_HUD, hud_state: "hudstat");
    }
}

// tg: code/game/data_huds.dm:473 (silicons SI8, D326: the malfunctioning AI's hacked-APC HUD)
public partial class obj_machinery_power_apc
{
    // tg: code/game/data_huds.dm:473
    public virtual void set_hacked_hud()
    {
        image holder = hud_list[MALF_APC_HUD] as image;
        holder.loc = get_turf(this);
        SET_PLANE(holder, ABOVE_LIGHTING_PLANE, this);
        set_hud_image_active(MALF_APC_HUD);
    }
}
