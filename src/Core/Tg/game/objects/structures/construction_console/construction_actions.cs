// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/construction_console/construction_actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///Generic construction action for base [construction consoles][/obj/machinery/computer/camera_advanced/base_construction].
// tg: code/game/objects/structures/construction_console/construction_actions.dm:6
public partial class datum_action_innate_construction : datum_action_innate
{
    // tg: code/game/objects/structures/construction_console/construction_actions.dm:14
    public override async DmTask<object> Activate()
    {
        if (target == null)
            return TRUE;
        remote_eye = owner.remote_control as mob_eye_camera_remote_base_construction;
        base_console = target as obj_machinery_computer_camera_advanced_base_construction;
        return null;
    }

    ///Sanity check for any construction action that relies on an RCD being in the base console
    // tg: code/game/objects/structures/construction_console/construction_actions.dm:21
    public virtual void check_rcd()
    {
        //The console must always have an RCD.
        if (base_console.internal_rcd == null)
            throw Dm.CRASH("Base console is somehow missing an internal RCD!", "code/game/objects/structures/construction_console/construction_actions.dm", 24);
    }

    ///Check a loction to see if it is inside the aux base at the station. Camera visbility checks omitted so as to not hinder construction.
    // tg: code/game/objects/structures/construction_console/construction_actions.dm:27
    public virtual bool check_spot()
    {
        turf build_target = get_turf(remote_eye);
        area build_area = get_area(build_target) as area;
        Type area_constraint = base_console.allowed_area;
        //If the console has no allowed_area var, then we're free to build wherever
        if (area_constraint == null)
            return true;
        string constraint_name = Dm.initial(area_constraint, (area a) => a.name);
        if (!istype(build_area, area_constraint))
        {
            to_chat(owner, span_warning(I18n.T("You can only build within []!", constraint_name)));
            return false;
        }
        if (Dm.truthy(only_station_z) && !Dm.truthy(is_station_level(build_target.z)))
        {
            to_chat(owner, span_warning(I18n.T("[] has launched and can no longer be modified.", constraint_name)));
            return false;
        }
        return true;
    }
}

// tg: code/game/objects/structures/construction_console/construction_actions.dm:43
public partial class datum_action_innate_construction_build : datum_action_innate_construction
{
    // tg: code/game/objects/structures/construction_console/construction_actions.dm:47
    public override async DmTask<object> Activate()
    {
        if (Dm.truthy(await base.Activate()))
            return null;
        if (!check_spot())
            return null;
        turf target_turf = get_turf(remote_eye);
        atom rcd_target = target_turf;
        //Find airlocks and other shite
        foreach (obj S in Dm.each<obj>(target_turf.contents))
        {
            if (Dm.length(S.rcd_vals(owner, base_console.internal_rcd)) > 0)
                rcd_target = S; //If we don't break out of this loop we'll get the last placed thing
        }
        owner.changeNext_move(CLICK_CD_RANGE);
        check_rcd();
        await base_console.internal_rcd.rcd_create(rcd_target, owner); //Activate the RCD and force it to work remotely!
        playsound(target_turf, "sound/items/deconstruct.ogg", 60, TRUE);
        return null;
    }
}

// tg: code/game/objects/structures/construction_console/construction_actions.dm:63
public partial class datum_action_innate_construction_configure_mode : datum_action_innate_construction
{
    // tg: code/game/objects/structures/construction_console/construction_actions.dm:68
    public override async DmTask<object> Activate()
    {
        if (Dm.truthy(await base.Activate()))
            return null;
        check_rcd();
        base_console.internal_rcd.owner = base_console;
        await base_console.internal_rcd.ui_interact(owner);
        return null;
    }
}

///Generic action used with base construction consoles to build anything that can't be built with an RCD
// tg: code/game/objects/structures/construction_console/construction_actions.dm:76
public partial class datum_action_innate_construction_place_structure : datum_action_innate_construction
{
    // tg: code/game/objects/structures/construction_console/construction_actions.dm:81
    public override async DmTask<object> Activate()
    {
        if (Dm.truthy(await base.Activate()))
            return null;
        turf place_turf = get_turf(remote_eye);
        if (!Dm.truthy(base_console.structures[structure_name]))
        {
            to_chat(owner, span_warning(I18n.T("[] is out of []!", base_console, structure_name)));
            return null;
        }
        if (!check_spot())
            return null;
        //Can't place inside a closed turf
        if (Dm.truthy(place_turf.density))
        {
            to_chat(owner, span_warning(I18n.T("[] may only be placed on a floor.", structure_name)));
            return null;
        }
        //Can't place two dense objects inside each other
        if (Dm.truthy(Dm.initial(structure_path, (atom a) => a.density)) && Dm.truthy(await place_turf.is_blocked_turf()))
        {
            to_chat(owner, span_warning(I18n.T("Location is obstructed by something. Please clear the location and try again.")));
            return null;
        }
        var placed_structure = Dm.@new(structure_path, place_turf) as obj;
        base_console.structures[structure_name] = Dm.num(base_console.structures[structure_name]) - 1;
        float remaining = Dm.num(base_console.structures[structure_name]);
        playsound(place_turf, place_sound, 50, TRUE);
        after_place(placed_structure, remaining);
        return null;
    }

    ///Proc to handle additional behavior after placing an object
    // tg: code/game/objects/structures/construction_console/construction_actions.dm:106
    public virtual void after_place(obj placed_structure = null, float? remaining = null)
    {
    }
}

// tg: code/game/objects/structures/construction_console/construction_actions.dm:109
public partial class datum_action_innate_construction_place_structure_fan : datum_action_innate_construction_place_structure
{
    // tg: code/game/objects/structures/construction_console/construction_actions.dm:116
    public override void after_place(obj placed_structure = null, float? remaining = null)
    {
        to_chat(owner, span_notice(I18n.T("Tiny fan placed. [] fans remaining.", remaining)));
    }
}

// tg: code/game/objects/structures/construction_console/construction_actions.dm:119
public partial class datum_action_innate_construction_place_structure_turret : datum_action_innate_construction_place_structure
{
    // tg: code/game/objects/structures/construction_console/construction_actions.dm:126
    public override void after_place(obj placed_structure = null, float? remaining = null)
    {
        obj_machinery_computer_auxiliary_base turret_controller = null;
        foreach (obj_machinery_computer_auxiliary_base found in Dm.each<obj_machinery_computer_auxiliary_base>((get_area(placed_structure) as area)?.contents))
        {
            turret_controller = found;
            break;
        }
        if (turret_controller == null)
        {
            to_chat(owner, span_notice(I18n.T("<b>Warning:</b> Aux base controller not found. Turrets might not work properly.")));
            return;
        }

        LAZYADD(ref turret_controller.turrets, WEAKREF(placed_structure));
        to_chat(owner, span_notice(I18n.T("You've constructed an additional turret. [] turrets remaining.", remaining)));
    }
}
