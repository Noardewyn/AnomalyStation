// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapping/mapping_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the mapping helper base and its airlock helpers (M3 P3: the airlocks' cycle links, bolts, unrestricted sides,
// welds, access and the abandoned airlocks of the map), broken_floor and burnt_floor; M4b slice 2: the air alarm helpers
// (the locks, accesses, thresholds, sensor links and the surgery scrubbers); gaps G3 (D291): the APC helpers, the lights-off
// helper, the dead body placer (its bodies deferred: the morgue is inert), Ian's birthday and new year helpers and the request
// console helpers. The injectors and the file's other helpers stay inert (D112) until their milestones.
// Gaps BR1 (D339): the baseturf helper (:6-46). The var blocks are generated (D111).

// Gaps BR1 (D339): the baseturf helper, the virtual domains lay their binary floor under every tile of their area
// tg: code/modules/mapping/mapping_helpers.dm:6
public partial class obj_effect_baseturf_helper : obj_effect
{
    // tg: code/modules/mapping/mapping_helpers.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/mapping/mapping_helpers.dm:20
    // set waitfor = FALSE (inherited from /atom/LateInitialize): replace_baseturf may sleep (the turf changes), so this is the
    // wrapper of waiting-model.md 5.4.
    public override void LateInitialize(params object[] __extra)
    {
        Waiting.WaitforFalse(LateInitialize__body());
    }

    private async DmTask LateInitialize__body()
    {
        if (!Dm.truthy(baseturf_to_replace))
            baseturf_to_replace = typecacheof(Dm.list(typeof(turf_open_space), typeof(turf_baseturf_bottom))) as DmList;
        else if (length(baseturf_to_replace) == 0)
            // tg's named list key: the string "baseturf_to_replace", not the var (kept as tg has it)
            baseturf_to_replace = Dm.list(Dm.kv("baseturf_to_replace", TRUE));
        else if (!Dm.eq(baseturf_to_replace[baseturf_to_replace[1]], TRUE)) // It's not associative
        {
            DmList formatted = Dm.list();
            foreach (object i in Dm.each(baseturf_to_replace))
                formatted[i] = TRUE;
            baseturf_to_replace = formatted;
        }

        area our_area = get_area(this) as area;
        foreach (object i in Dm.each(get_area_turfs(our_area, z)))
            await replace_baseturf(i as turf);

        qdel(this);
    }

    /// Replaces all the requested baseturfs (usually space/baseturfbottom) with the desired baseturf. Skips if its already there
    // tg: code/modules/mapping/mapping_helpers.dm:38
    public virtual async DmTask replace_baseturf(turf thing = null)
    {
        await thing.remove_baseturfs_from_typecache(baseturf_to_replace);

        if (length(thing.baseturfs) != 0)
        {
            object tile = (thing.baseturfs as DmList)?[1];
            if (Dm.eq(tile, baseturf))
                return;
        }

        await thing.place_on_bottom(baseturf);
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:117
public partial class obj_effect_mapping_helpers : obj_effect
{
    // tg: code/modules/mapping/mapping_helpers.dm:125
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return Dm.truthy(late) ? INITIALIZE_HINT_LATELOAD : INITIALIZE_HINT_QDEL;
    }
}

//airlock helpers
// tg: code/modules/mapping/mapping_helpers.dm:130
public partial class obj_effect_mapping_helpers_airlock : obj_effect_mapping_helpers
{
    // tg: code/modules/mapping/mapping_helpers.dm:136
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload))
        {
            log_mapping(Dm.text("[] spawned outside of mapload!", this));
            return dot;
        }

        obj_machinery_door_airlock airlock = Dm.locate<obj_machinery_door_airlock>(loc);
        if (!Dm.truthy(airlock))
        {
            if (Dm.truthy(apply_to_windoors))
            {
                float any_found = FALSE;
                foreach (obj_machinery_door_window windoor in Dm.each<obj_machinery_door_window>(loc))
                {
                    payload(windoor);
                    any_found = TRUE;
                }
                if (!Dm.truthy(any_found))
                    log_mapping(Dm.text("[] failed to find an airlock at [], AND no windoors were found.", this, AREACOORD__gap()));
                return dot;
            }

            log_mapping(Dm.text("[] failed to find an airlock at []", this, AREACOORD__gap()));
            return dot;
        }

        payload(airlock);
        return dot;
    }

    // GAP(lead): AREACOORD(src) (__DEFINES/admin.dm:67) needs /atom/proc/Admin_Coordinates_Readable (__DEFINES/admin.dm:83)
    // and /proc/get_area_name (__HELPERS/game.dm:5), not declared: the mapping log lines name the coordinates only. Not a tg
    // proc: the stand-in of the macro's text for this file.
    private protected string AREACOORD__gap() => Dm.text("([],[],[])", x, y, z);

    // tg: code/modules/mapping/mapping_helpers.dm:158
    // set waitfor = FALSE (inherited from /atom/LateInitialize): this body may sleep through late_payload, so it is the
    // wrapper of waiting-model.md 5.4.
    public override void LateInitialize(params object[] __extra)
    {
        Waiting.WaitforFalse(LateInitialize__body(__extra));
    }

    private async DmTask LateInitialize__body(object[] __extra)
    {
        obj_machinery_door_airlock airlock = Dm.locate<obj_machinery_door_airlock>(loc);
        if (!Dm.truthy(airlock))
        {
            qdel(this);
            return;
        }
        await late_payload(airlock);
        if (!QDELETED(airlock))
        {
            if (Dm.truthy(airlock.cyclelinkeddir))
                airlock.cyclelinkairlock();
            if (Dm.truthy(airlock.closeOtherId))
                airlock.update_other_id();
            if (Dm.truthy(airlock.cutAiWire))
                await airlock.wires.cut(WIRE_AI);
            if (Dm.truthy(airlock.autoname))
                airlock.name = Dm.str(get_area_name(this, TRUE));
            airlock.update_appearance();
        }
        qdel(this);
    }

    // tg: code/modules/mapping/mapping_helpers.dm:176
    // Appendix A (M3 P3, decided): payload is object, not slots.tsv's /obj/machinery/door/airlock: Initialize passes it a
    // windoor when apply_to_windoors (:144), a "-" cell of rules 3.5.
    public virtual void payload(object payload = null)
    {
        return;
    }

    // tg: code/modules/mapping/mapping_helpers.dm:179
    public virtual async DmTask late_payload(obj_machinery_door_airlock payload = null)
    {
        return;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:182
public partial class obj_effect_mapping_helpers_airlock_cyclelink_helper : obj_effect_mapping_helpers_airlock
{
    // tg: code/modules/mapping/mapping_helpers.dm:186
    public override void payload(object airlock = null)
    {
        if (Dm.truthy(((obj_machinery_door_airlock)airlock).cyclelinkeddir))
            log_mapping(Dm.text("[] at [] tried to set [] cyclelinkeddir, but it's already set!", this, AREACOORD__gap(), airlock));
        else
            ((obj_machinery_door_airlock)airlock).cyclelinkeddir = dir;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:192
public partial class obj_effect_mapping_helpers_airlock_cyclelink_helper_multi : obj_effect_mapping_helpers_airlock
{
    // tg: code/modules/mapping/mapping_helpers.dm:197
    public override void payload(object airlock = null)
    {
        if (Dm.truthy(((obj_machinery_door_airlock)airlock).closeOtherId))
            log_mapping(Dm.text("[] at [] tried to set [] closeOtherId, but it's already set!", this, AREACOORD__gap(), airlock));
        else if (!Dm.truthy(cycle_id))
            log_mapping(Dm.text("[] at [] doesn't have a cycle_id to assign to []!", this, AREACOORD__gap(), airlock));
        else
            ((obj_machinery_door_airlock)airlock).closeOtherId = cycle_id;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:205
public partial class obj_effect_mapping_helpers_airlock_locked : obj_effect_mapping_helpers_airlock
{
    // tg: code/modules/mapping/mapping_helpers.dm:209
    public override void payload(object airlock = null)
    {
        if (Dm.truthy(((obj_machinery_door_airlock)airlock).locked))
            log_mapping(Dm.text("[] at [] tried to bolt [] but it's already locked!", this, AREACOORD__gap(), airlock));
        else
        {
            // Used instead of bolt so that we can pretend it was always locked, i.e. no sound effects on init.
            ((obj_machinery_door_airlock)airlock).locked = TRUE;
        }
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:216
public partial class obj_effect_mapping_helpers_airlock_unres : obj_effect_mapping_helpers_airlock
{
    // tg: code/modules/mapping/mapping_helpers.dm:220
    public override void payload(object airlock = null)
    {
        Dm.bitxor_assign(ref ((obj_machinery_door_airlock)airlock).unres_sides, dir);
        ((obj_machinery_door_airlock)airlock).unres_latch = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:224
public partial class obj_effect_mapping_helpers_airlock_unres_delayed : obj_effect_mapping_helpers_airlock_unres
{
    // tg: code/modules/mapping/mapping_helpers.dm:228
    public override void payload(object airlock = null)
    {
        base.payload(airlock);
        ((obj_machinery_door_airlock)airlock).delayed_unres_open = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:240
public partial class obj_effect_mapping_helpers_airlock_abandoned : obj_effect_mapping_helpers_airlock
{
    // tg: code/modules/mapping/mapping_helpers.dm:232-238  the file-local #defines (#undef at :307-313); internal: the
    // nowall helper uses two of them (rules 11.4)
    internal const string ABANDON_WALL = "wall";
    internal const string ABANDON_FALSEWALL = "falsewall";
    internal const string ABANDON_LOCKED_NOLIGHT = "locked_no_lights";
    internal const string ABANDON_LOCKED = "locked";
    internal const string ABANDON_WELDED = "welded";
    internal const string ABANDON_PANEL_OPEN = "panel_open";
    internal const string ABANDON_NONE = "none";

    // tg: code/modules/mapping/mapping_helpers.dm:252
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        modify_weights();
        float max_weight = values_sum(weights);
        weights[ABANDON_NONE] = 100 - max_weight;
        return dot;
    }

    // tg: code/modules/mapping/mapping_helpers.dm:258
    public virtual void modify_weights()
    {
        return;
    }

    // tg: code/modules/mapping/mapping_helpers.dm:261
    public override async DmTask late_payload(obj_machinery_door_airlock airlock = null)
    {
        object sw__1 = pick_weight(weights);
        if (Dm.eq(sw__1, ABANDON_WALL))
        {
            turf here = get_turf(this);
            await here.place_on_top(get_wall_type(airlock) as Type);
            qdel(airlock);
        }

        else if (Dm.eq(sw__1, ABANDON_FALSEWALL))
        {
            object walltype = get_wall_type(airlock);
            Type falsewalltype = typeof(obj_structure_falsewall);
            foreach (Type false_wall in Dm.each_cast<Type>(typesof(typeof(obj_structure_falsewall))))
            {
                if (Dm.eq(Dm.initial(false_wall, (obj_structure_falsewall p) => p.walltype), walltype))
                {
                    falsewalltype = false_wall;
                    break;
                }
            }

            turf here = get_turf(this);
            Dm.@new(falsewalltype, here);
            qdel(airlock);
        }

        else if (Dm.eq(sw__1, ABANDON_LOCKED_NOLIGHT))
        {
            airlock.feedback = FALSE;
            // These do not use airlock.bolt() because we want to pretend it was always locked. That means no sound effects.
            airlock.locked = TRUE;
        }

        else if (Dm.eq(sw__1, ABANDON_LOCKED))
            airlock.locked = TRUE;

        else if (Dm.eq(sw__1, ABANDON_WELDED))
            airlock.welded = TRUE;

        else if (Dm.eq(sw__1, ABANDON_PANEL_OPEN))
            airlock.set_panel_open(TRUE);
    }

    // tg: code/modules/mapping/mapping_helpers.dm:294
    public virtual object get_wall_type(obj_machinery_door_airlock airlock = null)
    {
        foreach (turf_closed wall in Dm.each<turf_closed>(range(2, airlock))) // uses range to prioritize closest
            return wall.type;

        return typeof(turf_closed_wall);
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:300
public partial class obj_effect_mapping_helpers_airlock_abandoned_nowall : obj_effect_mapping_helpers_airlock_abandoned
{
    // tg: code/modules/mapping/mapping_helpers.dm:303
    public override void modify_weights()
    {
        weights[ABANDON_WALL] = 0;
        weights[ABANDON_FALSEWALL] = 0;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:315
public partial class obj_effect_mapping_helpers_airlock_welded : obj_effect_mapping_helpers_airlock
{
    // tg: code/modules/mapping/mapping_helpers.dm:319
    public override void payload(object airlock = null)
    {
        if (Dm.truthy(((obj_machinery_door_airlock)airlock).welded))
            log_mapping(Dm.text("[] at [] tried to make [] welded but it's already welded closed!", this, AREACOORD__gap(), airlock));
        ((obj_machinery_door_airlock)airlock).welded = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:324
public partial class obj_effect_mapping_helpers_airlock_cutaiwire : obj_effect_mapping_helpers_airlock
{
    // tg: code/modules/mapping/mapping_helpers.dm:328
    public override void payload(object airlock = null)
    {
        if (Dm.truthy(((obj_machinery_door_airlock)airlock).cutAiWire))
            log_mapping(Dm.text("[] at [] tried to cut the ai wire on [] but it's already cut!", this, AREACOORD__gap(), airlock));
        else
            ((obj_machinery_door_airlock)airlock).cutAiWire = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:334
public partial class obj_effect_mapping_helpers_airlock_autoname : obj_effect_mapping_helpers_airlock
{
    // tg: code/modules/mapping/mapping_helpers.dm:338
    public override void payload(object airlock = null)
    {
        if (Dm.truthy(((obj_machinery_door_airlock)airlock).autoname))
            log_mapping(Dm.text("[] at [] tried to autoname the [] but it's already autonamed!", this, AREACOORD__gap(), airlock));
        else
            ((obj_machinery_door_airlock)airlock).autoname = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:344
public partial class obj_effect_mapping_helpers_airlock_inaccessible : obj_effect_mapping_helpers_airlock
{
    // tg: code/modules/mapping/mapping_helpers.dm:348
    public override void payload(object airlock = null)
    {
        if (((obj_machinery_door_airlock)airlock).req_one_access != null)
            log_mapping(Dm.text("[] at [] tried to set req_access, but req__one_access was already set!", this, AREACOORD__gap()));
        else
            Dm.append(ref ((obj_machinery_door_airlock)airlock).req_access, Dm.list(ACCESS_INACCESSIBLE));
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:354
public partial class obj_effect_mapping_helpers_airlock_red_alert_access : obj_effect_mapping_helpers_airlock
{
    // tg: code/modules/mapping/mapping_helpers.dm:359
    public override void payload(object airlock = null)
    {
        ((obj_machinery_door)airlock).red_alert_access = TRUE;
    }
}

//air alarm helpers
// tg: code/modules/mapping/mapping_helpers.dm:363
public partial class obj_effect_mapping_helpers_airalarm : obj_effect_mapping_helpers
{
    // tg: code/modules/mapping/mapping_helpers.dm:367
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload))
        {
            log_mapping(Dm.text("[] spawned outside of mapload!", this));
            return INITIALIZE_HINT_QDEL;
        }

        obj_machinery_airalarm target = Dm.locate<obj_machinery_airalarm>(loc);
        if (target == null)
        {
            area target_area = get_area(this) as area;
            log_mapping(Dm.text("[] failed to find an air alarm at [] ([]).", this, AREACOORD__gap(), target_area.type));
        }
        else
            payload(target);

        return INITIALIZE_HINT_LATELOAD;
    }

    // GAP(lead): AREACOORD(src) (__DEFINES/admin.dm:67) needs /atom/proc/Admin_Coordinates_Readable (M10): the mapping log
    // lines name the coordinates only (as the airlock helpers' stand-in above).
    private protected string AREACOORD__gap() => Dm.text("([],[],[])", x, y, z);

    // tg: code/modules/mapping/mapping_helpers.dm:382
    public override void LateInitialize(params object[] __extra)
    {
        obj_machinery_airalarm target = Dm.locate<obj_machinery_airalarm>(loc);

        if (target == null)
        {
            qdel(this);
            return;
        }
        if (Dm.truthy(target.unlocked))
            target.unlock();

        if (Dm.truthy(target.tlv_cold_room))
            target.set_tlv_cold_room();
        if (Dm.truthy(target.tlv_kitchen))
            target.set_tlv_kitchen();
        if (Dm.truthy(target.tlv_no_checks))
            target.set_tlv_no_checks();
        if (target.tlv_no_checks + target.tlv_cold_room + target.tlv_kitchen > 1)
            throw Dm.CRASH("Tried to apply incompatible air alarm threshold helpers!", "code/modules/mapping/mapping_helpers.dm", 397);

        if (Dm.truthy(target.syndicate_access))
            target.give_syndicate_access();
        if (Dm.truthy(target.away_general_access))
            target.give_away_general_access();
        if (Dm.truthy(target.engine_access))
            target.give_engine_access();
        if (Dm.truthy(target.mixingchamber_access))
            target.give_mixingchamber_access();
        if (Dm.truthy(target.all_access))
            target.give_all_access();
        if (target.syndicate_access + target.away_general_access + target.engine_access + target.mixingchamber_access + target.all_access > 1)
            throw Dm.CRASH("Tried to combine incompatible air alarm access helpers!", "code/modules/mapping/mapping_helpers.dm", 410);

        target.update_appearance();
        qdel(this);
    }

    // tg: code/modules/mapping/mapping_helpers.dm:416
    public virtual void payload(obj_machinery_airalarm target = null)
    {
        return;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:419
public partial class obj_effect_mapping_helpers_airalarm_unlocked : obj_effect_mapping_helpers_airalarm
{
    // tg: code/modules/mapping/mapping_helpers.dm:423
    public override void payload(obj_machinery_airalarm target = null)
    {
        if (Dm.truthy(target.unlocked))
        {
            area area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to unlock the [] but it's already unlocked!", this, AREACOORD__gap(), area.type, target));
        }
        target.unlocked = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:429
public partial class obj_effect_mapping_helpers_airalarm_syndicate_access : obj_effect_mapping_helpers_airalarm
{
    // tg: code/modules/mapping/mapping_helpers.dm:433
    public override void payload(obj_machinery_airalarm target = null)
    {
        if (Dm.truthy(target.syndicate_access))
        {
            area area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to adjust []'s access to syndicate but it's already changed!", this, AREACOORD__gap(), area.type, target));
        }
        target.syndicate_access = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:439
public partial class obj_effect_mapping_helpers_airalarm_away_general_access : obj_effect_mapping_helpers_airalarm
{
    // tg: code/modules/mapping/mapping_helpers.dm:443
    public override void payload(obj_machinery_airalarm target = null)
    {
        if (Dm.truthy(target.away_general_access))
        {
            area area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to adjust []'s access to away_general but it's already changed!", this, AREACOORD__gap(), area.type, target));
        }
        target.away_general_access = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:449
public partial class obj_effect_mapping_helpers_airalarm_engine_access : obj_effect_mapping_helpers_airalarm
{
    // tg: code/modules/mapping/mapping_helpers.dm:453
    public override void payload(obj_machinery_airalarm target = null)
    {
        if (Dm.truthy(target.engine_access))
        {
            area area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to adjust []'s access to engine_access but it's already changed!", this, AREACOORD__gap(), area.type, target));
        }
        target.engine_access = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:459
public partial class obj_effect_mapping_helpers_airalarm_mixingchamber_access : obj_effect_mapping_helpers_airalarm
{
    // tg: code/modules/mapping/mapping_helpers.dm:463
    public override void payload(obj_machinery_airalarm target = null)
    {
        if (Dm.truthy(target.mixingchamber_access))
        {
            area area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to adjust []'s access to mixingchamber_access but it's already changed!", this, AREACOORD__gap(), area.type, target));
        }
        target.mixingchamber_access = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:469
public partial class obj_effect_mapping_helpers_airalarm_all_access : obj_effect_mapping_helpers_airalarm
{
    // tg: code/modules/mapping/mapping_helpers.dm:473
    public override void payload(obj_machinery_airalarm target = null)
    {
        if (Dm.truthy(target.all_access))
        {
            area area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to adjust []'s access to all_access but it's already changed!", this, AREACOORD__gap(), area.type, target));
        }
        target.all_access = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:479
public partial class obj_effect_mapping_helpers_airalarm_tlv_cold_room : obj_effect_mapping_helpers_airalarm
{
    // tg: code/modules/mapping/mapping_helpers.dm:483
    public override void payload(obj_machinery_airalarm target = null)
    {
        if (Dm.truthy(target.tlv_cold_room))
        {
            area area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to adjust []'s tlv to cold_room but it's already changed!", this, AREACOORD__gap(), area.type, target));
        }
        target.tlv_cold_room = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:489
public partial class obj_effect_mapping_helpers_airalarm_tlv_kitchen : obj_effect_mapping_helpers_airalarm
{
    // tg: code/modules/mapping/mapping_helpers.dm:493
    public override void payload(obj_machinery_airalarm target = null)
    {
        if (Dm.truthy(target.tlv_kitchen))
        {
            area area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to adjust []'s tlv to kitchen but it's already changed!", this, AREACOORD__gap(), area.type, target));
        }
        target.tlv_kitchen = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:499
public partial class obj_effect_mapping_helpers_airalarm_tlv_no_checks : obj_effect_mapping_helpers_airalarm
{
    // tg: code/modules/mapping/mapping_helpers.dm:503
    public override void payload(obj_machinery_airalarm target = null)
    {
        if (Dm.truthy(target.tlv_no_checks))
        {
            area area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to adjust []'s tlv to no_checks but it's already changed!", this, AREACOORD__gap(), area.type, target));
        }
        target.tlv_no_checks = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:509
public partial class obj_effect_mapping_helpers_airalarm_link : obj_effect_mapping_helpers_airalarm
{
    // tg: code/modules/mapping/mapping_helpers.dm:516
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload))
        {
            log_mapping(Dm.text("[] spawned outside of mapload!", this));
            return INITIALIZE_HINT_QDEL;
        }
        return dot;
    }

    // tg: code/modules/mapping/mapping_helpers.dm:522
    public override void LateInitialize(params object[] __extra)
    {
        obj_machinery_airalarm alarm = Dm.locate<obj_machinery_airalarm>(loc);
        if (alarm != null)
        {
            alarm.air_sensor_chamber_id = chamber_id;
            alarm.allow_link_change = allow_link_change;
            alarm.setup_chamber_link();
        }
        else
            log_mapping(Dm.text("[] failed to find air alarm at [].", this, AREACOORD__gap()));
        qdel(this);
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:532
public partial class obj_effect_mapping_helpers_airalarm_surgery : obj_effect_mapping_helpers_airalarm
{
    // tg: code/modules/mapping/mapping_helpers.dm:536
    public override void LateInitialize(params object[] __extra)
    {
        obj_machinery_airalarm target = Dm.locate<obj_machinery_airalarm>(loc);
        foreach (obj_machinery_atmospherics_components_unary_vent_scrubber scrubber in Dm.each_cast<obj_machinery_atmospherics_components_unary_vent_scrubber>(target?.my_area?.air_scrubbers))
        {
            Dm.combine(ref scrubber.filter_types, typeof(datum_gas_nitrous_oxide));
            scrubber.set_widenet(TRUE);
        }
        base.LateInitialize(__extra);
    }
}

//apc helpers
// tg: code/modules/mapping/mapping_helpers.dm:544
public partial class obj_effect_mapping_helpers_apc : obj_effect_mapping_helpers
{
    // tg: code/modules/mapping/mapping_helpers.dm:548
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload))
        {
            log_mapping(Dm.text("[] spawned outside of mapload!", this));
            return INITIALIZE_HINT_QDEL;
        }

        obj_machinery_power_apc target = Dm.locate<obj_machinery_power_apc>(loc);
        if (target == null)
        {
            area target_area = get_area(this) as area;
            log_mapping(Dm.text("[] failed to find an apc at [] ([]).", this, AREACOORD(this), target_area.type));
        }
        else
            payload(target);

        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/mapping/mapping_helpers.dm:563
    public override void LateInitialize(params object[] __extra)
    {
        obj_machinery_power_apc target = Dm.locate<obj_machinery_power_apc>(loc);

        if (target == null)
        {
            qdel(this);
            return;
        }
        if (Dm.truthy(target.cut_AI_wire))
            Waiting.NoSleep(target.wires.cut(WIRE_AI));
        if (Dm.truthy(target.cell_5k))
            target.install_cell_5k();
        if (Dm.truthy(target.cell_10k))
            target.install_cell_10k();
        if (Dm.truthy(target.unlocked))
            target.unlock();
        if (Dm.truthy(target.syndicate_access))
            target.give_syndicate_access();
        if (Dm.truthy(target.away_general_access))
            target.give_away_general_access();
        if (Dm.truthy(target.no_charge))
            target.set_no_charge();
        if (Dm.truthy(target.full_charge))
            target.set_full_charge();
        if (Dm.truthy(target.cell_5k) && Dm.truthy(target.cell_10k))
            throw Dm.CRASH("Tried to combine non-combinable cell_5k and cell_10k APC helpers!", "code/modules/mapping/mapping_helpers.dm", 586);
        if (Dm.truthy(target.syndicate_access) && Dm.truthy(target.away_general_access))
            throw Dm.CRASH("Tried to combine non-combinable syndicate_access and away_general_access APC helpers!", "code/modules/mapping/mapping_helpers.dm", 588);
        if (Dm.truthy(target.no_charge) && Dm.truthy(target.full_charge))
            throw Dm.CRASH("Tried to combine non-combinable no_charge and full_charge APC helpers!", "code/modules/mapping/mapping_helpers.dm", 590);
        target.update_appearance();
        qdel(this);
    }

    // tg: code/modules/mapping/mapping_helpers.dm:594
    public virtual void payload(obj_machinery_power_apc target = null)
    {
        return;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:597
public partial class obj_effect_mapping_helpers_apc_cut_AI_wire : obj_effect_mapping_helpers_apc
{
    // tg: code/modules/mapping/mapping_helpers.dm:601
    public override void payload(obj_machinery_power_apc target = null)
    {
        if (Dm.truthy(target.cut_AI_wire))
        {
            area apc_area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to mend the AI wire on the [] but it's already cut!", this, AREACOORD(this), apc_area.type, target));
        }
        target.cut_AI_wire = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:607
public partial class obj_effect_mapping_helpers_apc_cell_5k : obj_effect_mapping_helpers_apc
{
    // tg: code/modules/mapping/mapping_helpers.dm:611
    public override void payload(obj_machinery_power_apc target = null)
    {
        if (Dm.truthy(target.cell_5k))
        {
            area apc_area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to change []'s cell to cell_5k but it's already changed!", this, AREACOORD(this), apc_area.type, target));
        }
        target.cell_5k = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:617
public partial class obj_effect_mapping_helpers_apc_cell_10k : obj_effect_mapping_helpers_apc
{
    // tg: code/modules/mapping/mapping_helpers.dm:621
    public override void payload(obj_machinery_power_apc target = null)
    {
        if (Dm.truthy(target.cell_10k))
        {
            area apc_area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to change []'s cell to cell_10k but it's already changed!", this, AREACOORD(this), apc_area.type, target));
        }
        target.cell_10k = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:627
public partial class obj_effect_mapping_helpers_apc_syndicate_access : obj_effect_mapping_helpers_apc
{
    // tg: code/modules/mapping/mapping_helpers.dm:631
    public override void payload(obj_machinery_power_apc target = null)
    {
        if (Dm.truthy(target.syndicate_access))
        {
            area apc_area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to adjust []'s access to syndicate but it's already changed!", this, AREACOORD(this), apc_area.type, target));
        }
        target.syndicate_access = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:637
public partial class obj_effect_mapping_helpers_apc_away_general_access : obj_effect_mapping_helpers_apc
{
    // tg: code/modules/mapping/mapping_helpers.dm:641
    public override void payload(obj_machinery_power_apc target = null)
    {
        if (Dm.truthy(target.away_general_access))
        {
            area apc_area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to adjust []'s access to away_general but it's already changed!", this, AREACOORD(this), apc_area.type, target));
        }
        target.away_general_access = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:647
public partial class obj_effect_mapping_helpers_apc_unlocked : obj_effect_mapping_helpers_apc
{
    // tg: code/modules/mapping/mapping_helpers.dm:651
    public override void payload(obj_machinery_power_apc target = null)
    {
        if (Dm.truthy(target.unlocked))
        {
            area apc_area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to unlock the [] but it's already unlocked!", this, AREACOORD(this), apc_area.type, target));
        }
        target.unlocked = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:657
public partial class obj_effect_mapping_helpers_apc_no_charge : obj_effect_mapping_helpers_apc
{
    // tg: code/modules/mapping/mapping_helpers.dm:661
    public override void payload(obj_machinery_power_apc target = null)
    {
        if (Dm.truthy(target.no_charge))
        {
            area apc_area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to set []'s charge to 0 but it's already at 0!", this, AREACOORD(this), apc_area.type, target));
        }
        target.no_charge = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:667
public partial class obj_effect_mapping_helpers_apc_full_charge : obj_effect_mapping_helpers_apc
{
    // tg: code/modules/mapping/mapping_helpers.dm:671
    public override void payload(obj_machinery_power_apc target = null)
    {
        if (Dm.truthy(target.full_charge))
        {
            area apc_area = get_area(target) as area;
            log_mapping(Dm.text("[] at [] [] tried to set []'s charge to 100 but it's already at 100!", this, AREACOORD(this), apc_area.type, target));
        }
        target.full_charge = TRUE;
    }
}

//Used to turn off lights with lightswitch in areas.
// tg: code/modules/mapping/mapping_helpers.dm:678
public partial class obj_effect_mapping_helpers_turn_off_lights_with_lightswitch : obj_effect_mapping_helpers
{
    // tg: code/modules/mapping/mapping_helpers.dm:682
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload))
        {
            log_mapping(Dm.text("[] spawned outside of mapload!", this));
            return INITIALIZE_HINT_QDEL;
        }
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/mapping/mapping_helpers.dm:689
    public override void LateInitialize(params object[] __extra)
    {
        area needed_area = get_area(this) as area;
        if (!Dm.truthy(needed_area.lightswitch))
            stack_trace(Dm.text("[] at [] [] tried to turn lights off but they are already off!", this, AREACOORD(this), needed_area.type), "code/modules/mapping/mapping_helpers.dm", 692);
        // RULE-GAP: tg's `locate(/obj/machinery/light_switch) in needed_area`: the port's area.contents lists only its turfs
        // (Builtin/area.builtin.cs), so the switch is looked for on the area's turfs, the first one found as BYOND's would be
        obj_machinery_light_switch light_switch = null;
        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(needed_area.get_zlevel_turf_lists()))
        {
            foreach (turf area_turf in Dm.each_cast<turf>(zlevel_turfs))
            {
                light_switch = Dm.locate<obj_machinery_light_switch>(area_turf);
                if (light_switch != null)
                    break;
            }
            if (light_switch != null)
                break;
        }
        if (light_switch == null)
            throw Dm.CRASH(Dm.text("Trying to turn off lights with lightswitch in area without lightswitches. In [] to be precise.", needed_area.type), "code/modules/mapping/mapping_helpers.dm", 695);
        light_switch.set_lights(FALSE);
        qdel(this);
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:974
public partial class obj_effect_mapping_helpers_dead_body_placer : obj_effect_mapping_helpers
{
    // tg: code/modules/mapping/mapping_helpers.dm:985
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
            return dot;
        admin_spawned = TRUE;
        return dot;
    }

    // tg: code/modules/mapping/mapping_helpers.dm:991
    public override void LateInitialize(params object[] __extra)
    {
        area morgue_area = get_area(this) as area;
        DmList trays = Dm.list();
        foreach (DmList zlevel_turfs in Dm.each<DmList>(morgue_area.get_zlevel_turf_lists()))
        {
            foreach (turf area_turf in Dm.each<turf>(zlevel_turfs))
            {
                var morgue_tray = Dm.locate<obj_structure_bodycontainer_morgue>(area_turf);
                // connected: the tray instance is connected_value (morgue.cs, GAP(types))
                if (morgue_tray == null || !Dm.truthy(morgue_tray.beeper) || morgue_tray.connected_value?.loc != morgue_tray)
                    continue;
                Dm.append(ref trays, morgue_tray);
            }
        }

        float numtrays = length(trays);
        if (numtrays == 0)
        {
            if (Dm.truthy(admin_spawned))
                message_admins(I18n.T("[] spawned at [] failed to find a closed morgue to spawn a body!", this, ADMIN_VERBOSEJMP(this)));
            else
                log_mapping(Dm.text("[] at [],[] could not find any morgues.", this, x, y));
            return;
        }

        bool reuse_trays = (numtrays < bodycount); //are we going to spawn more trays than bodies?

        for (int i = 1; i <= bodycount; i++)
        {
            var morgue_tray = (reuse_trays ? pick(trays) : pick_n_take(trays)) as obj_structure_bodycontainer_morgue;
            var body_bag = Dm.@new<obj_structure_closet_body_bag>(morgue_tray.loc);
            var new_human = Waiting.NoSleep(GLOB.lost_crew_manager.create_lost_crew(revivable: FALSE, forced_class: morgue_body_class)) as mob_living_carbon_human;

            Waiting.NoSleep(body_bag.insert(new_human, TRUE));
            Waiting.NoSleep(body_bag.close());
            body_bag.tag_name = Dm.text("[][]", new_human.real_name, new_human.dna?.species != null ? Dm.text(" - []", ((datum_species)new_human.dna.species).name) : " - Human");
            Waiting.NoSleep(body_bag.AddComponent(typeof(datum_component_rename), Dm.text("[][]", Dm.initial(body_bag, (atom p) => p.name), Dm.truthy(body_bag.tag_name) ? Dm.text(" - []", body_bag.tag_name) : null), body_bag.desc));
            Waiting.NoSleep(body_bag.update_icon());
            Waiting.NoSleep(body_bag.forceMove(morgue_tray));

            morgue_tray.update_appearance();
        }

        qdel(this);
    }
}

//On Ian's birthday, the hop's office is decorated.
// tg: code/modules/mapping/mapping_helpers.dm:1028
public partial class obj_effect_mapping_helpers_ianbirthday : obj_effect_mapping_helpers
{
    // tg: code/modules/mapping/mapping_helpers.dm:1037
    public override void LateInitialize(params object[] __extra)
    {
        if (Dm.truthy(check_holidays(IAN_HOLIDAY)))
            birthday();
        qdel(this);
    }

    // tg: code/modules/mapping/mapping_helpers.dm:1042
    public virtual void birthday()
    {
        area celebration_area = get_area(this) as area;
        DmList table_turfs = Dm.list();
        DmList open_turfs = Dm.list();
        turf dogbed_turf = null;
        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(celebration_area.get_zlevel_turf_lists()))
        {
            foreach (turf area_turf in Dm.each_cast<turf>(zlevel_turfs))
            {
                if (Dm.locate<obj_structure_table_reinforced>(area_turf) != null)
                    Dm.append(ref table_turfs, area_turf);
                if (Dm.locate<obj_structure_bed_dogbed_ian>(area_turf) != null)
                    dogbed_turf = area_turf;
                if (isopenturf(area_turf))
                {
                    Dm.@new<obj_effect_decal_cleanable_confetti>(area_turf);
                    Dm.append(ref open_turfs, area_turf);
                }
            }
        }

        if (dogbed_turf == null && Dm.truthy(map_warning))
            log_mapping(Dm.text("[] in [] could not find Ian's dogbed.", this, celebration_area));

        else
        {
            Dm.@new<obj_item_toy_balloon_corgi>(dogbed_turf);
            turf food_turf = Dm.truthy(length(table_turfs)) ? pick(table_turfs) as turf : dogbed_turf;
            Dm.@new<obj_item_knife_kitchen>(food_turf);
            obj_item_food_cake_birthday iancake = Dm.@new<obj_item_food_cake_birthday>(food_turf);
            iancake.desc = I18n.T("Happy birthday, Ian!");
        }

        if (!Dm.truthy(length(open_turfs)))
            return;

        //some balloons! this picks an open turf and pops a few balloons in and around that turf, yay.
        for (float i = 1; i <= balloon_clusters; i++)
        {
            turf clusterspot = pick_n_take(open_turfs) as turf;
            Dm.@new<obj_item_toy_balloon>(clusterspot);
            float balloons_left_to_give = 3; //the amount of balloons around the cluster
            DmList dirs_to_balloon = GLOB.cardinals.Copy();
            while (balloons_left_to_give > 0)
            {
                balloons_left_to_give--;
                object chosen_dir = pick_n_take(dirs_to_balloon);
                turf balloonstep = get_step(clusterspot, Dm.num(chosen_dir));
                float placed = FALSE;
                if (isopenturf(balloonstep))
                {
                    obj_item_toy_balloon B = Dm.@new<obj_item_toy_balloon>(balloonstep); //this clumps the cluster together
                    placed = TRUE;
                    if (Dm.eq(chosen_dir, NORTH))
                        B.pixel_y -= 10;
                    if (Dm.eq(chosen_dir, SOUTH))
                        B.pixel_y += 10;
                    if (Dm.eq(chosen_dir, EAST))
                        B.pixel_x -= 10;
                    if (Dm.eq(chosen_dir, WEST))
                        B.pixel_x += 10;
                }
                if (!Dm.truthy(placed))
                    Dm.@new<obj_item_toy_balloon>(clusterspot);
            }
        }
        //remind me to add wall decor!
    }
}

//so admins may birthday any room
// tg: code/modules/mapping/mapping_helpers.dm:1096
public partial class obj_effect_mapping_helpers_ianbirthday_admin : obj_effect_mapping_helpers_ianbirthday
{
    // tg: code/modules/mapping/mapping_helpers.dm:1101
    public override void LateInitialize(params object[] __extra)
    {
        birthday();
        qdel(this);
    }
}

//Ian, like most dogs, loves a good new years eve party.
// tg: code/modules/mapping/mapping_helpers.dm:1106
public partial class obj_effect_mapping_helpers_iannewyear : obj_effect_mapping_helpers
{
    // tg: code/modules/mapping/mapping_helpers.dm:1111
    public override void LateInitialize(params object[] __extra)
    {
        if (Dm.truthy(check_holidays(NEW_YEAR)))
            fireworks();
        qdel(this);
    }

    // tg: code/modules/mapping/mapping_helpers.dm:1116
    public virtual void fireworks()
    {
        area celebration_area = get_area(this) as area;
        DmList table_turfs = Dm.list();
        turf dogbed_turf = null;
        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(celebration_area.get_zlevel_turf_lists()))
        {
            foreach (turf area_turf in Dm.each_cast<turf>(zlevel_turfs))
            {
                if (Dm.locate<obj_structure_table_reinforced>(area_turf) != null)
                    Dm.append(ref table_turfs, area_turf);
                if (Dm.locate<obj_structure_bed_dogbed_ian>(area_turf) != null)
                    dogbed_turf = area_turf;
            }
        }

        if (dogbed_turf == null)
        {
            log_mapping(Dm.text("[] in [] could not find Ian's dogbed.", this, celebration_area));
            return;
        }

        Dm.@new<obj_item_clothing_head_costume_festive>(dogbed_turf);
        obj_item_reagent_containers_cup_glass_bottle_champagne iandrink = Dm.@new<obj_item_reagent_containers_cup_glass_bottle_champagne>(dogbed_turf);
        iandrink.name = I18n.T("dog champagne");
        iandrink.pixel_y += 8;
        iandrink.pixel_x += 8;

        turf fireworks_turf = Dm.truthy(length(table_turfs)) ? pick(table_turfs) as turf : dogbed_turf;
        obj_item_storage_box_matches matchbox = Dm.@new<obj_item_storage_box_matches>(fireworks_turf);
        matchbox.pixel_y += 8;
        matchbox.pixel_x -= 3;
        Dm.@new<obj_item_storage_box_fireworks_dangerous>(fireworks_turf); //dangerous version for extra holiday memes.
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:1272
public partial class obj_effect_mapping_helpers_broken_floor : obj_effect_mapping_helpers
{
    // tg: code/modules/mapping/mapping_helpers.dm:1279
    public override void LateInitialize(params object[] __extra)
    {
        turf_open_floor floor = get_turf(this) as turf_open_floor;
        floor.break_tile();
        qdel(this);
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:1284
public partial class obj_effect_mapping_helpers_burnt_floor : obj_effect_mapping_helpers
{
    // tg: code/modules/mapping/mapping_helpers.dm:1291
    public override void LateInitialize(params object[] __extra)
    {
        turf_open_floor floor = get_turf(this) as turf_open_floor;
        floor.burn_tile();
        qdel(this);
    }
}

//requests console helpers
// tg: code/modules/mapping/mapping_helpers.dm:1371
public partial class obj_effect_mapping_helpers_requests_console : obj_effect_mapping_helpers
{
    // tg: code/modules/mapping/mapping_helpers.dm:1375
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload))
        {
            log_mapping(Dm.text("[] spawned outside of mapload!", this));
            return INITIALIZE_HINT_QDEL;
        }

        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/modules/mapping/mapping_helpers.dm:1383  (tg types the local /obj/machinery/airalarm; it holds the console)
    public override void LateInitialize(params object[] __extra)
    {
        obj_machinery_requests_console target = Dm.locate<obj_machinery_requests_console>(loc);
        if (target == null)
        {
            area target_area = get_area(this) as area;
            log_mapping(Dm.text("[] failed to find a requests console at [] ([]).", this, AREACOORD(this), target_area.type));
        }
        else
            payload(target);

        qdel(this);
    }

    /// Fills out the request console's variables
    // tg: code/modules/mapping/mapping_helpers.dm:1394
    public virtual void payload(obj_machinery_requests_console console = null)
    {
        return;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:1397
public partial class obj_effect_mapping_helpers_requests_console_announcement : obj_effect_mapping_helpers_requests_console
{
    // tg: code/modules/mapping/mapping_helpers.dm:1401
    public override void payload(obj_machinery_requests_console console = null)
    {
        console.can_send_announcements = TRUE;
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:1404
public partial class obj_effect_mapping_helpers_requests_console_assistance : obj_effect_mapping_helpers_requests_console
{
    // tg: code/modules/mapping/mapping_helpers.dm:1408
    public override void payload(obj_machinery_requests_console console = null)
    {
        Dm.combine(ref GLOB.req_console_assistance, console.department);
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:1411
public partial class obj_effect_mapping_helpers_requests_console_supplies : obj_effect_mapping_helpers_requests_console
{
    // tg: code/modules/mapping/mapping_helpers.dm:1415
    public override void payload(obj_machinery_requests_console console = null)
    {
        Dm.combine(ref GLOB.req_console_supplies, console.department);
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:1418
public partial class obj_effect_mapping_helpers_requests_console_information : obj_effect_mapping_helpers_requests_console
{
    // tg: code/modules/mapping/mapping_helpers.dm:1422
    public override void payload(obj_machinery_requests_console console = null)
    {
        Dm.combine(ref GLOB.req_console_information, console.department);
    }
}

// tg: code/modules/mapping/mapping_helpers.dm:1425
public partial class obj_effect_mapping_helpers_requests_console_ore_update : obj_effect_mapping_helpers_requests_console
{
    // tg: code/modules/mapping/mapping_helpers.dm:1429
    public override void payload(obj_machinery_requests_console console = null)
    {
        console.receive_ore_updates = TRUE;
    }
}
