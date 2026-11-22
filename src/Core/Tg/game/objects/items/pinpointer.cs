// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/pinpointer.dm, code/modules/antagonists/nukeop/equipment/pinpointer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The base, the crew and the nuclear pinpointer (gaps J4, D298). The pair, shuttle, material and cyborg pinpointers and the syndicate one's mode stay as tg's subtypes where they define no procs; those that do stay inert.

public partial class datum_controller_global_vars
{
    // tg: code/game/objects/items/pinpointer.dm:235  GLOBAL_LIST_EMPTY(sniffable_sheets)
    public DmList sniffable_sheets;

    // tg: code/game/objects/items/pinpointer.dm:235  (GLOBAL_MANAGED(sniffable_sheets, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsniffable_sheets()
    {
        sniffable_sheets = Dm.list();
        Dm.append(ref gvars_datum_init_order, "sniffable_sheets");
    }

    // tg: code/_globalvars/lists/objects.dm  GLOBAL_LIST_EMPTY(pinpointer_list)
    public DmList pinpointer_list;

    // tg: (GLOBAL_MANAGED(pinpointer_list, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpinpointer_list()
    {
        pinpointer_list = Dm.list();
        Dm.append(ref gvars_datum_init_order, "pinpointer_list");
    }
}

// tg: code/game/objects/items/pinpointer.dm:2
public partial class obj_item_pinpointer : obj_item
{
    // tg: code/game/objects/items/pinpointer.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.append(ref GLOB.pinpointer_list, this);
        return dot;
    }

    // tg: code/game/objects/items/pinpointer.dm:42
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        GLOB.pinpointer_list.Remove(this);
        target = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/pinpointer.dm:48
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(process_scan)) //since it's not scanning on process, it scans here.
            scan_for_target();
        toggle_on();
        user.visible_message(span_notice(I18n.T("[] []activates [] pinpointer.", user, Dm.truthy(active) ? "" : "de", user.p_their())), span_notice(I18n.T("You []activate your pinpointer.", Dm.truthy(active) ? "" : "de")));
        return null;
    }

    // tg: code/game/objects/items/pinpointer.dm:54
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (target != null)
            dot.Add(I18n.T("It is currently tracking [].", target));
        return dot;
    }

    // tg: code/game/objects/items/pinpointer.dm:59
    public virtual void toggle_on()
    {
        active = Dm.truthy(active) ? FALSE : TRUE;
        playsound(this, Dm.resource("sound/items/tools/screwdriver2.ogg"), 50, TRUE);
        if (Dm.truthy(active))
        {
            START_PROCESSING(SSfastprocess, this);
        }
        else
        {
            target = null;
            STOP_PROCESSING(SSfastprocess, this);
        }
        update_appearance();
    }

    // tg: code/game/objects/items/pinpointer.dm:69
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(active))
            return PROCESS_KILL;
        if (Dm.truthy(process_scan))
            scan_for_target();
        update_appearance();
        return null;
    }

    // tg: code/game/objects/items/pinpointer.dm:76
    public virtual void scan_for_target()
    {
    }

    // tg: code/game/objects/items/pinpointer.dm:79
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy(active))
            return dot;
        if (target == null)
        {
            dot.Add(Dm.text("pinon[]null[]", Dm.truthy(alert) ? "alert" : "", icon_suffix));
            return dot;
        }
        turf here = get_turf(this) as turf;
        turf there = get_turf(target) as turf;
        if (here == null || there == null || here.z != there.z)
        {
            dot.Add(Dm.text("pinon[]null[]", Dm.truthy(alert) ? "alert" : "", icon_suffix));
            return dot;
        }
        dot.Add(get_direction_icon(here, there));
        return dot;
    }

    ///Called by update_icon after sanity. There is a target
    // tg: code/game/objects/items/pinpointer.dm:94
    public virtual object get_direction_icon(turf here = null, turf there = null)
    {
        if (Dm.num(get_dist_euclidean(here, there)) <= minimum_range)
            return Dm.text("pinon[]direct[]", Dm.truthy(alert) ? "alert" : "", icon_suffix);
        Waiting.NoSleep(setDir(get_dir(here, there)));
        float current_distance = Dm.num(get_dist(here, there));
        if (current_distance >= 1 && current_distance <= close_range)
            return Dm.text("pinon[][]", Dm.truthy(alert) ? "alert" : "close", icon_suffix);
        else if (current_distance > (close_range + 1) && current_distance <= medium_range)
            return Dm.text("pinon[][]", Dm.truthy(alert) ? "alert" : "medium", icon_suffix);
        else if (current_distance > medium_range)
            return Dm.text("pinon[][]", Dm.truthy(alert) ? "alert" : "far", icon_suffix);
        return null;
    }
}

// tg: code/game/objects/items/pinpointer.dm:108
public partial class obj_item_pinpointer_crew : obj_item_pinpointer
{
    // tg: code/game/objects/items/pinpointer.dm:120
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        pinpointer_owner = null;
        return dot;
    }

    // tg: code/game/objects/items/pinpointer.dm:124
    public virtual bool trackable(mob_living_carbon_human H = null)
    {
        turf here = get_turf(this) as turf;
        turf there = get_turf(H) as turf;
        if (here != null && there != null && (there.z == here.z || (Dm.truthy(is_station_level(here.z)) && Dm.truthy(is_station_level(there.z))))) // Device and target should be on the same level or different levels of the same station
        {
            if (H.w_uniform is obj_item_clothing_under U)
            {
                if (Dm.truthy(U.has_sensor) && (Dm.num(U.sensor_mode) >= SENSOR_COORDS || Dm.truthy(ignore_suit_sensor_level))) // Suit sensors must be on maximum or a contractor pinpointer
                    return true;
            }
        }
        return false;
    }

    // tg: code/game/objects/items/pinpointer.dm:134
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(active))
        {
            toggle_on();
            user.visible_message(span_notice(I18n.T("[] deactivates [] pinpointer.", user, user.p_their())), span_notice(I18n.T("You deactivate your pinpointer.")));
            return null;
        }

        if (pinpointer_owner == null)
            pinpointer_owner = user;

        if (pinpointer_owner != null && pinpointer_owner != user)
        {
            to_chat(user, span_notice(I18n.T("The pinpointer doesn't respond. It seems to only recognise its owner.")));
            return null;
        }

        DmList name_counts = Dm.list();
        DmList names = Dm.list();

        foreach (object i in Dm.each(GLOB.human_list))
        {
            mob_living_carbon_human H = i as mob_living_carbon_human;
            if (!trackable(H))
                continue;

            object crewmember_name = I18n.T("Unknown");
            if (H.wear_id != null)
            {
                obj_item_card_id I = H.wear_id.GetID() as obj_item_card_id;
                if (Dm.truthy(I?.registered_name))
                    crewmember_name = I.registered_name;
            }

            while (Dm.@in(crewmember_name, name_counts))
            {
                name_counts[crewmember_name] = Dm.num(name_counts[crewmember_name]) + 1;
                crewmember_name = Dm.text("[] ([])", crewmember_name, name_counts[crewmember_name]);
            }
            names[crewmember_name] = H;
            name_counts[crewmember_name] = 1;
        }

        if (length(names) == 0)
        {
            user.visible_message(span_notice(I18n.T("[]'s pinpointer fails to detect a signal.", user)), span_notice(I18n.T("Your pinpointer fails to detect a signal.")));
            return null;
        }
        object pinpoint_target = await tgui_input_list(user, I18n.T("Person to track"), I18n.T("Pinpoint"), sort_list(names) as DmList);
        if (pinpoint_target == null)
            return null;
        if (names[pinpoint_target] == null)
            return null;
        if (QDELETED(this) || user == null || !Dm.truthy(user.is_holding(this)) || Dm.truthy(user.incapacitated))
            return null;
        target = names[pinpoint_target] as atom_movable;
        toggle_on();
        user.visible_message(span_notice(I18n.T("[] activates [] pinpointer.", user, user.p_their())), span_notice(I18n.T("You activate your pinpointer.")));
        return null;
    }

    // tg: code/game/objects/items/pinpointer.dm:181
    public override void scan_for_target()
    {
        if (target != null)
        {
            if (target is mob_living_carbon_human H)
            {
                if (!trackable(H))
                    target = null;
            }
        }
        if (target == null) //target can be set to null from above code, or elsewhere
            active = FALSE;
    }
}

// tg: code/modules/antagonists/nukeop/equipment/pinpointer.dm:2
public partial class obj_item_pinpointer_nuke : obj_item_pinpointer
{
    // tg: code/modules/antagonists/nukeop/equipment/pinpointer.dm:5
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        string msg = I18n.T("Its tracking indicator reads ");
        switch ((int)(mode ?? 0))
        {
            case TRACK_NUKE_DISK:
                msg += "\"nuclear_disk\".";
                break;
            case TRACK_MALF_AI:
                msg += "\"01000001 01001001\".";
                break;
            case TRACK_INFILTRATOR:
                msg += "\"vasvygengbefuvc\".";
                break;
            default:
                msg = I18n.T("Its tracking indicator is blank.");
                break;
        }
        dot.Add(msg);
        foreach (obj_machinery_nuclearbomb bomb in Dm.each<obj_machinery_nuclearbomb>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_nuclearbomb))))
        {
            if (Dm.truthy(bomb.timing))
                dot.Add(I18n.T("Extreme danger. Arming signal detected. Time remaining: [].", bomb.get_time_left()));
        }
        return dot;
    }

    // tg: code/modules/antagonists/nukeop/equipment/pinpointer.dm:22
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        base.process(seconds_per_tick, __extra);
        if (!Dm.truthy(active) || Dm.truthy(alert))
            return null;
        foreach (obj_machinery_nuclearbomb bomb in Dm.each<obj_machinery_nuclearbomb>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_nuclearbomb))))
        {
            if (!Dm.truthy(bomb.timing))
                continue;
            alert = TRUE;
            playsound(this, Dm.resource("sound/items/nuke_toy_lowpower.ogg"), 50, FALSE);
            if (isliving(loc))
                to_chat(loc, span_userdanger(I18n.T("Your [] vibrates and lets out an ominous alarm. Uh oh.", name)));
            return null;
        }
        return null;
    }

    // tg: code/modules/antagonists/nukeop/equipment/pinpointer.dm:36
    public override void scan_for_target()
    {
        target = null;
        switch ((int)(mode ?? 0))
        {
            case TRACK_NUKE_DISK:
                foreach (obj_item_disk_nuclear N in Dm.each<obj_item_disk_nuclear>(SSpoints_of_interest.real_nuclear_disks))
                {
                    target = N;
                    break;
                }
                break;
            case TRACK_MALF_AI:
                foreach (object V in Dm.each(GLOB.ai_list))
                {
                    mob_living_silicon_ai A = V as mob_living_silicon_ai;
                    if (Dm.truthy(A.nuking))
                        target = (A.linked_core as atom_movable) ?? A;
                }
                foreach (obj_machinery_power_apc apc in Dm.each<obj_machinery_power_apc>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_apc))))
                {
                    if (Dm.truthy(apc.malfhack) && apc.occupier != null)
                        target = apc;
                }
                break;
            case TRACK_INFILTRATOR:
                target = SSshuttle.getShuttle("syndicate") as atom_movable;
                break;
        }
        base.scan_for_target();
    }

    // tg: code/modules/antagonists/nukeop/equipment/pinpointer.dm:57
    public virtual void switch_mode_to(object new_mode = null)
    {
        if (isliving(loc))
        {
            mob_living L = loc as mob_living;
            to_chat(L, span_userdanger(I18n.T("Your [] beeps as it reconfigures its tracking algorithms.", name)));
            playsound(L, Dm.resource("sound/machines/beep/triple_beep.ogg"), 50, TRUE);
        }
        mode = Dm.num(new_mode);
        scan_for_target();
    }
}

// tg: code/game/objects/items/pinpointer.dm:190
public partial class obj_item_pinpointer_pair : obj_item_pinpointer
{
    // tg: code/game/objects/items/pinpointer.dm:196
    public override object Destroy(float? force = null)
    {
        other_pair = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/pinpointer.dm:200
    public override void scan_for_target()
    {
        target = other_pair as atom_movable;
    }

    // tg: code/game/objects/items/pinpointer.dm:203
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(active) || target == null)
            return dot;
        // get(target, /mob)
        atom holder_walk = target;
        while (holder_walk != null && holder_walk is not mob)
            holder_walk = holder_walk.loc;
        mob mob_holder = holder_walk as mob;
        if (mob_holder != null)
            dot.Add(I18n.T("Its pair is being held by [].", mob_holder));
        return dot;
    }
}

// tg: code/game/objects/items/pinpointer.dm:212
public partial class obj_item_pinpointer_shuttle : obj_item_pinpointer
{
    // tg: code/game/objects/items/pinpointer.dm:221
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        shuttleport = SSshuttle?.getShuttle("huntership") as obj_docking_port_mobile; // a host-built room without the Master has no SSshuttle
        return dot;
    }

    // tg: code/game/objects/items/pinpointer.dm:225
    public override void scan_for_target()
    {
        if (shuttleport == null)
            shuttleport = SSshuttle?.getShuttle("huntership") as obj_docking_port_mobile; // a host-built room without the Master has no SSshuttle
        target = shuttleport;
    }

    // tg: code/game/objects/items/pinpointer.dm:230
    public override object Destroy(float? force = null)
    {
        shuttleport = null;
        return base.Destroy(force);
    }
}

// tg: code/game/objects/items/pinpointer.dm:237
public partial class obj_item_pinpointer_material_sniffer : obj_item_pinpointer
{
    // tg: code/game/objects/items/pinpointer.dm:244
    public override void scan_for_target()
    {
        if (target != null || !Dm.truthy(length(GLOB.sniffable_sheets)))
            return;
        obj_item_stack_sheet new_sheet_target = null;
        float closest_distance = INFINITY;
        foreach (obj_item_stack_sheet potential_sheet in Dm.each<obj_item_stack_sheet>(GLOB.sniffable_sheets.Copy()))
        {
            // not enough for lag reasons, and shouldn't even be on this
            if (Dm.num(potential_sheet.amount) < 10)
            {
                GLOB.sniffable_sheets.Remove(potential_sheet);
                continue;
            }
            //held by someone
            if (isliving(potential_sheet.loc))
                continue;
            //not on scanner's z
            if (!Dm.eq(potential_sheet.z, z))
                continue;
            float distance_from_sniffer = Dm.num(get_dist(this, potential_sheet));
            if (distance_from_sniffer < closest_distance)
            {
                closest_distance = distance_from_sniffer;
                new_sheet_target = potential_sheet;
            }
        }
        if (new_sheet_target == null)
        {
            target = null;
            return;
        }
        Waiting.Detach(say(I18n.T("Located [] []s!", new_sheet_target.amount, new_sheet_target.singular_name)));
        target = new_sheet_target;
    }
}

// tg: code/modules/antagonists/nukeop/equipment/pinpointer.dm:71  (silicons SI9, D326: the syndicate cyborgs' pinpointer)
public partial class obj_item_pinpointer_syndicate_cyborg : obj_item_pinpointer // Cyborg pinpointers just look for a random operative.
{
    // tg: code/modules/antagonists/nukeop/equipment/pinpointer.dm:77
    public override async DmTask cyborg_unequip(mob user = null)
    {
        await DmTask.CompletedTask;
        if (!Dm.truthy(active))
            return;
        toggle_on();
    }

    // tg: code/modules/antagonists/nukeop/equipment/pinpointer.dm:82
    public override void scan_for_target()
    {
        target = null;
        DmList possible_targets = Dm.list();
        turf here = get_turf(this);
        foreach (datum_mind M in Dm.each<datum_mind>(get_antag_minds(typeof(datum_antagonist_nukeop))))
        {
            if (ishuman(M.current) && M.current.stat != DEAD && !Dm.@in(M.current, possible_targets))
                possible_targets.Add(M.current);
        }
        var closest_operative = get_closest_atom(typeof(mob_living_carbon_human), possible_targets, here) as mob_living;
        if (closest_operative != null)
            target = closest_operative;
        base.scan_for_target();
    }
}
