// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/stray_cargo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E3 (D327): the whole file but its admin options (the pod's aim and the crate an admin picks, the syndicate crate's value and
// uplink: /datum/event_admin_setup/*, the admins' Trigger Event panel, D275): the stray cargo pod (a random supply crate, unlocked, in a
// supply pod that lands in a station area that is safe to blow up) and its syndicate variant (an emagged syndicate crate of uplink
// items in a syndicate pod). The var blocks are generated (D111).

///Spawns a cargo pod containing a random cargo supply pack on a random area of the station
// tg: code/modules/events/stray_cargo.dm:32
public partial class datum_round_event_stray_cargo : datum_round_event
{
    // tg: code/modules/events/stray_cargo.dm:36  var/static/list/stray_spawnable_supply_packs = list() (a type-level static, rules 10.6)
    public static ref DmList stray_spawnable_supply_packs => ref TgState.Current.datum_round_event_stray_cargo__stray_spawnable_supply_packs;

    // tg: code/modules/events/stray_cargo.dm:146  var/static/mutable_appearance/target_appearance (a proc-level static, rules 10.6)
    private static ref mutable_appearance create_ghost_notification__target_appearance => ref TgState.Current.datum_round_event_stray_cargo__create_ghost_notification__target_appearance;

    // tg: code/modules/events/stray_cargo.dm:151  var/static/list/allowed_areas (a proc-level static, rules 10.6)
    private static ref DmList find_event_area__allowed_areas => ref TgState.Current.datum_round_event_stray_cargo__find_event_area__allowed_areas;

    // tg: code/modules/events/stray_cargo.dm:154  var/static/list/safe_area_types (a proc-level static, rules 10.6)
    private static ref DmList find_event_area__safe_area_types => ref TgState.Current.datum_round_event_stray_cargo__find_event_area__safe_area_types;

    // tg: code/modules/events/stray_cargo.dm:162  var/static/list/unsafe_area_subtypes (a proc-level static, rules 10.6)
    private static ref DmList find_event_area__unsafe_area_subtypes => ref TgState.Current.datum_round_event_stray_cargo__find_event_area__unsafe_area_subtypes;

    // tg: code/modules/events/stray_cargo.dm:50
    public override async DmTask announce__proc(object fake = null)
    {
        if (Dm.truthy(fake))
            impact_area = find_event_area() as area;
        priority_announce(Dm.text("Stray cargo pod detected on long-range scanners. Expected location of impact: [].", impact_area.name), "Collision Alert");
    }

    /**
    * Tries to find a valid area, throws an error if none are found
    * Also randomizes the start timer
    */
    // tg: code/modules/events/stray_cargo.dm:59
    public override async DmTask<object> setup()
    {
        start_when = rand(lower_bound_wait_time, upper_bound_wait_time);
        impact_area = get_impact_area() as area;
        DmList turf_test = get_area_turfs(impact_area) as DmList;
        if (!Dm.truthy(turf_test.len))
            throw Dm.CRASH(Dm.text("Stray Cargo Pod : No valid turfs found for [] - []", impact_area, impact_area.type), "code/modules/events/stray_cargo.dm", 64);

        if (!Dm.truthy(stray_spawnable_supply_packs.len))
        {
            stray_spawnable_supply_packs = SSshuttle.supply_packs.Copy();
            foreach (object pack in Dm.each(stray_spawnable_supply_packs))
            {
                // initial(pack_type.order_flags) of a pack id: a typepath reads its initial flags, a text id (the generated packs) reads null
                // in DM (initial() of a primitive), which the port's Dm.initial refuses
                object pack_flags = pack is Type pack_type ? (object)Dm.initial(pack_type, (datum_supply_pack p) => p.order_flags) : null;
                if (Dm.truthy(Dm.bitand(pack_flags, ORDER_SPECIAL)))
                    Dm.remove(stray_spawnable_supply_packs, pack);
            }
        }
        return null;
    }

    // tg: code/modules/events/stray_cargo.dm:73
    public virtual object get_impact_area()
    {
        object dot;
        if (admin_override_turf != null)
            dot = get_area(admin_override_turf);
        else
            dot = find_event_area();
        if (dot == null)
            throw Dm.CRASH("No valid areas for cargo pod found.", "code/modules/events/stray_cargo.dm", 79);
        return dot;
    }

    ///Spawns a random supply pack, puts it in a pod, and spawns it on a random tile of the selected area
    // tg: code/modules/events/stray_cargo.dm:82
    public override async DmTask<object> start()
    {
        turf landing_zone = get_landing_zone() as turf;
        obj_structure_closet_supplypod pod = make_pod() as obj_structure_closet_supplypod;
        obj container = await get_contents() as obj;
        send_pod(landing_zone, pod, container);
        return null;
    }

    ///Selects a place for the pod to land in
    // tg: code/modules/events/stray_cargo.dm:89
    public virtual object get_landing_zone()
    {
        DmList valid_turfs = get_area_turfs(impact_area) as DmList;
        //Only target non-dense turfs to prevent wall-embedded pods
        foreach (object i in Dm.each(valid_turfs))
        {
            turf T = (turf)i;
            if (Dm.truthy(T.density))
                Dm.remove(valid_turfs, T);
        }
        object landing_zone;
        if (admin_override_turf != null)
            landing_zone = admin_override_turf;
        else
            landing_zone = pick(valid_turfs);
        return landing_zone;
    }

    ///Handles the creation of the pod, in case it needs to be modified beforehand
    // tg: code/modules/events/stray_cargo.dm:104
    public virtual object make_pod()
    {
        return Dm.@new<obj_structure_closet_supplypod>();
    }

    ///Generates the contents of the pod
    // tg: code/modules/events/stray_cargo.dm:108
    public virtual async DmTask<object> get_contents()
    {
        object pack_type;
        if (Dm.truthy(admin_override_contents))
        {
            pack_type = admin_override_contents;
        }
        else
        {
            if (Dm.truthy(possible_pack_types.len))
                pack_type = pick(possible_pack_types);
            else
                pack_type = pick(stray_spawnable_supply_packs);
        }
        datum_supply_pack supply_pack;
        if (ispath(pack_type, typeof(datum_supply_pack)))
        {
            supply_pack = (datum_supply_pack)await Dm.new_async((Type)pack_type);
        }
        else  // treat this as a supply pack id and resolving it with SSshuttle
        {
            if (Dm.truthy(admin_override_contents))
                supply_pack = admin_override_contents as datum_supply_pack; //Syndicate crates create a new datum while being customized which will result in this being triggered. Outside of this situation this should never trigger
            else
                supply_pack = SSshuttle.supply_packs[pack_type] as datum_supply_pack;
        }

        Type storage_override = null;
        if (Dm.truthy(Dm.bitand(Dm.initial(supply_pack, (datum_supply_pack p) => p.order_flags), ORDER_GOODY))) // We offer goody items inside of briefcases, but regular crates still default to their standard crates.
            storage_override = typeof(obj_item_storage_briefcase_empty);

        obj container = await supply_pack.generate(null, crate_override: crate_type ?? storage_override) as obj;
        if (container != null && container is obj_structure_closet_crate crate) //empty supply packs are a thing! get memed on.
        {
            if (Dm.truthy(emag_crate))
            {
                await crate.emag_act(); // `emag_act()` already calls `update_appeance()`
            }
            else
            {
                crate.locked = FALSE; //Unlock secure crates
                crate.update_appearance();
            }
        }
        return container;
    }

    ///Sends the pod to the given location, containing the container
    // tg: code/modules/events/stray_cargo.dm:141
    public virtual void send_pod(object landing_zone = null, object pod = null, object container = null)
    {
        obj_effect_pod_landingzone landing_marker = Dm.@new<obj_effect_pod_landingzone>(landing_zone, pod, container);
        create_ghost_notification(landing_marker);
    }

    // tg: code/modules/events/stray_cargo.dm:145
    public virtual void create_ghost_notification(object landing_marker = null)
    {
        create_ghost_notification__target_appearance ??= (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/supplypods_32x32.dmi"), "LZ");
        notify_ghosts(Dm.text("[] has summoned a supply crate!", control.name), source: get_turf(landing_marker), header: "Cargo Inbound", alert_overlay: create_ghost_notification__target_appearance);
    }

    ///Picks an area that wouldn't risk critical damage if hit by a pod explosion
    // tg: code/modules/events/stray_cargo.dm:150
    public virtual object find_event_area()
    {
        if (!Dm.truthy(find_event_area__allowed_areas))
        {
            ///Places that shouldn't explode
            find_event_area__safe_area_types ??= typecacheof(Dm.list(
                typeof(area_station_ai_satellite_chamber),
                typeof(area_station_ai_upload_chamber),
                typeof(area_station_engineering),
                typeof(area_shuttle)
            )) as DmList;

            ///Subtypes from the above that actually should explode.
            find_event_area__unsafe_area_subtypes ??= typecacheof(Dm.list(typeof(area_station_engineering_break_room))) as DmList;
            find_event_area__allowed_areas = Dm.add(Dm.sub(make_associative(GLOB.the_station_areas), find_event_area__safe_area_types), find_event_area__unsafe_area_subtypes) as DmList;
        }
        DmList possible_areas = typecache_filter_list(GLOB.areas, find_event_area__allowed_areas) as DmList;
        if (Dm.truthy(length(possible_areas)))
            return pick(possible_areas);
        return null;
    }
}

///A rare variant that drops a crate containing syndicate uplink items
// tg: code/modules/events/stray_cargo.dm:222
public partial class datum_round_event_stray_cargo_syndicate : datum_round_event_stray_cargo
{
    ///Apply the syndicate pod skin
    // tg: code/modules/events/stray_cargo.dm:228
    public override object make_pod()
    {
        obj_structure_closet_supplypod S = Dm.@new<obj_structure_closet_supplypod>();
        S.set_style(typeof(datum_pod_style_syndicate));
        return S;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/events/stray_cargo.dm:36  var/static/list/stray_spawnable_supply_packs = list()
    internal DmList datum_round_event_stray_cargo__stray_spawnable_supply_packs = Dm.list();

    // tg: code/modules/events/stray_cargo.dm:146  var/static/mutable_appearance/target_appearance
    internal mutable_appearance datum_round_event_stray_cargo__create_ghost_notification__target_appearance;

    // tg: code/modules/events/stray_cargo.dm:151  var/static/list/allowed_areas
    internal DmList datum_round_event_stray_cargo__find_event_area__allowed_areas;

    // tg: code/modules/events/stray_cargo.dm:154  var/static/list/safe_area_types
    internal DmList datum_round_event_stray_cargo__find_event_area__safe_area_types;

    // tg: code/modules/events/stray_cargo.dm:162  var/static/list/unsafe_area_subtypes
    internal DmList datum_round_event_stray_cargo__find_event_area__unsafe_area_subtypes;
}
