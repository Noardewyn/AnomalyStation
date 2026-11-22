// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/bounties/security.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file, the patrols and the contraband bounty. The patrol declares demanded_area as a type (tg's
// `area/demanded_area` holds an area's typepath, rules 3.2), so its var block is hand-written; the others are generated.

///Bounties that require you to perform documentation and inspection of your department to send to centcom.
// tg: code/modules/cargo/bounties/security.dm:2
public partial class datum_bounty_patrol : datum_bounty
{
    /// Ref to the component applied to the ID card to track movement.
    // tg: code/modules/cargo/bounties/security.dm:12
    public datum tracker;
    /// Typepath of what area we want patrolled.
    // tg: code/modules/cargo/bounties/security.dm:15  a typepath (rules 3.2)
    public Type demanded_area;
    /// How much area coverage is needed to complete the patrol.
    // tg: code/modules/cargo/bounties/security.dm:17
    public float needed_coverage = -1;
    /// List of turf IDs that have been walked on during the patrol.
    // tg: code/modules/cargo/bounties/security.dm:19
    public DmList walked_turfs;
    /// Number 0-5 tracking which progress alerts have been sent.
    // tg: code/modules/cargo/bounties/security.dm:21
    public float alerted = 0;

    // tg: code/modules/cargo/bounties/security.dm:2
    public datum_bounty_patrol()
    {
        name = "Patrol Station";
        description = "Perform a routine patrol of %AREA_NAME%. You must travel at least %AREA_COVERAGE% meters within the area. Your ID card will update you as you progress.";
        reward = CARGO_CRATE_VALUE * 5;
        allow_duplicate = TRUE;
        global_exempt = TRUE;
    }

    // tg: code/modules/cargo/bounties/security.dm:23
    public override async DmTask New(params object[] __extra)
    {
        demanded_area = pick(Dm.bitand(get_patrol_area_types(), GLOB.areas_by_type) as DmList) as Type;

        float total_coverage = 0;
        foreach (turf_open_floor walkable in Dm.each<turf_open_floor>(((area)GLOB.areas_by_type[demanded_area]).get_turfs_from_all_zlevels()))
            total_coverage += 1;

        needed_coverage = round(total_coverage * rand(4, 8) * 0.1f, 1);

        object area_name = Dm.initial(demanded_area, (area p) => p.name);
        name = Dm.text("[]: []", name, area_name);
        description = replacetext(description, "%AREA_NAME%", area_name);
        description = replacetext(description, "%AREA_COVERAGE%", needed_coverage);

        // scale the reward based on how big the area is, so you don't feel like you're wasting time
        // central primary hallway can have somewhere in the ballpark of 500 turfs
        // but something like the bar only sits around 100-200
        reward = Dm.num(reward) * (needed_coverage / 100);
    }

    // tg: code/modules/cargo/bounties/security.dm:41
    public override object can_get()
    {
        // only give out bounties worth completing.
        return needed_coverage >= 20 ? TRUE : FALSE;
    }

    // tg: code/modules/cargo/bounties/security.dm:45
    public virtual DmList get_patrol_area_types()
    {
        return typecacheof(Dm.list(
            typeof(area_station_commons),
            typeof(area_station_hallway),
            typeof(area_station_maintenance),
            typeof(area_station_security_checkpoint),
            typeof(area_station_security_prison),
            typeof(area_station_service))) as DmList;
    }

    // tg: code/modules/cargo/bounties/security.dm:55
    public virtual float get_progress()
    {
        float progress = 0;
        foreach (object turf_id in Dm.each(walked_turfs))
            progress += Dm.num(walked_turfs[turf_id]);
        return progress;
    }

    // tg: code/modules/cargo/bounties/security.dm:61
    public override object print_required() => I18n.T("[]/[] meters", get_progress(), needed_coverage);

    // tg: code/modules/cargo/bounties/security.dm:64
    public override object can_claim() => get_progress() >= needed_coverage ? TRUE : FALSE;

    // tg: code/modules/cargo/bounties/security.dm:67
    public override void on_selected(obj_item_card_id id_card = null)
    {
        start_tracking(id_card);
    }

    // tg: code/modules/cargo/bounties/security.dm:70
    public virtual void start_tracking(obj_item_card_id id_card = null)
    {
        Dm.combine(ref contribution, id_card.registered_account);
        tracker = Waiting.NoSleep(AddComponent(typeof(datum_component_connect_containers), id_card, Dm.list(Dm.kv(COMSIG_MOVABLE_MOVED, nameof(on_card_moved))))) as datum;
        RegisterSignal(id_card, COMSIG_MOVABLE_MOVED, nameof(on_card_moved));
    }

    // tg: code/modules/cargo/bounties/security.dm:75
    public override void on_reset(obj_item_card_id id_card = null)
    {
        stop_tracking(id_card);
    }

    // tg: code/modules/cargo/bounties/security.dm:78
    public virtual void stop_tracking(obj_item_card_id id_card = null)
    {
        QDEL_NULL(ref tracker);
        UnregisterSignal(id_card, COMSIG_MOVABLE_MOVED);
    }

    // tg: code/modules/cargo/bounties/security.dm:82
    public virtual void on_card_moved(atom_movable moving = null, atom old_loc = null, params object[] __extra)
    {
        turf new_turf = get_turf(moving);
        if (!isfloorturf(new_turf) || new_turf == get_turf(old_loc) || new_turf.loc.GetType() != demanded_area)
            return;

        string turf_id = Dm.text("[],[],[]", new_turf.x, new_turf.y, new_turf.z);
        // you can retread the same tiles, but only up to three times.
        // prevents cheese (walking between the same two tiles over and over),
        // but still allows smaller rooms to be completable without being frustrating.
        if (Dm.num(LAZYACCESS(walked_turfs, turf_id)) >= 3)
            return;

        obj_item_card_id id_card = null;
        if (moving is mob_living living_mover)
            id_card = living_mover.get_idcard() as obj_item_card_id;
        else if (isidcard(moving))
            id_card = (obj_item_card_id)moving;
        if (id_card == null)
            id_card = Dm.locate<obj_item_card_id>(moving.get_all_contents());

        if (id_card?.registered_account?.civilian_bounty != this)
            return;

        LAZYADDASSOC(ref walked_turfs, turf_id, 1f);
        float progress = get_progress();
        datum_bank_account account = id_card.registered_account;
        if (alerted == 0)
        {
            alerted = 1;
            Waiting.NoSleep(account.bank_card_talk(I18n.T("Patrol started. Travel [] meters in the area to complete your patrol.", needed_coverage), force: TRUE));
            return;
        }

        float progress_percent = progress / needed_coverage;
        if (progress_percent >= 0.25f && alerted < 2)
        {
            alerted = 2;
            Waiting.NoSleep(account.bank_card_talk(I18n.T("Patrol 25% complete."), force: TRUE));
            return;
        }

        if (progress_percent >= 0.5f && alerted < 3)
        {
            alerted = 3;
            Waiting.NoSleep(account.bank_card_talk(I18n.T("Patrol 50% complete."), force: TRUE));
            return;
        }

        if (progress_percent >= 0.75f && alerted < 4)
        {
            alerted = 4;
            Waiting.NoSleep(account.bank_card_talk(I18n.T("Patrol 75% complete."), force: TRUE));
            return;
        }

        if (progress >= needed_coverage && alerted < 5)
        {
            alerted = 5;
            Waiting.NoSleep(account.bank_card_talk(I18n.T("Patrol complete. Return to the bounty terminal to claim your reward."), force: TRUE));
            stop_tracking(id_card); // don't need this anymore
            return;
        }
    }
}

// tg: code/modules/cargo/bounties/security.dm:139
public partial class datum_bounty_patrol_supply : datum_bounty_patrol
{
    // tg: code/modules/cargo/bounties/security.dm:143
    public override DmList get_patrol_area_types()
    {
        return Dm.add(typecacheof(Dm.list(
            typeof(area_station_cargo_breakroom),
            typeof(area_station_cargo_lobby),
            typeof(area_station_cargo_lower),
            typeof(area_station_cargo_mining_breakroom),
            typeof(area_station_cargo_miningdock),
            typeof(area_station_cargo_miningfoundry),
            typeof(area_station_cargo_miningoffice),
            typeof(area_station_cargo_office),
            typeof(area_station_cargo_sorting),
            typeof(area_station_cargo_storage),
            typeof(area_station_cargo_warehouse),
            typeof(area_station_maintenance_department_cargo))), Dm.list(
            typeof(area_station_cargo), // base cargo type as well
            typeof(area_mine_lounge))) as DmList; // and public mining on lavaland
    }
}

// tg: code/modules/cargo/bounties/security.dm:162
public partial class datum_bounty_patrol_medical : datum_bounty_patrol
{
    // tg: code/modules/cargo/bounties/security.dm:166
    public override DmList get_patrol_area_types()
    {
        return Dm.add(typecacheof(Dm.list(
            typeof(area_station_maintenance_department_medical),
            typeof(area_station_medical_abandoned),
            typeof(area_station_medical_break_room),
            typeof(area_station_medical_cryo),
            typeof(area_station_medical_exam_room),
            typeof(area_station_medical_lower),
            typeof(area_station_medical_medbay),
            typeof(area_station_medical_morgue),
            typeof(area_station_medical_office),
            typeof(area_station_medical_storage),
            typeof(area_station_medical_surgery),
            typeof(area_station_medical_treatment_center))), Dm.list(
            typeof(area_station_medical))) as DmList; // base medical type as well
    }
}

// tg: code/modules/cargo/bounties/security.dm:184
public partial class datum_bounty_patrol_science : datum_bounty_patrol
{
    // tg: code/modules/cargo/bounties/security.dm:188
    public override DmList get_patrol_area_types()
    {
        return Dm.add(typecacheof(Dm.list(
            typeof(area_station_science_auxlab),
            typeof(area_station_science_breakroom),
            typeof(area_station_science_explab),
            typeof(area_station_science_lab),
            typeof(area_station_science_lobby),
            typeof(area_station_science_lower),
            typeof(area_station_science_research),
            typeof(area_station_science_research_abandoned))), Dm.list(
            typeof(area_station_science), // base science type as well
            typeof(area_station_maintenance_department_science))) as DmList; // and only base sci maint - not xenobio maint
    }
}

// tg: code/modules/cargo/bounties/security.dm:203
public partial class datum_bounty_patrol_engineering : datum_bounty_patrol
{
    // tg: code/modules/cargo/bounties/security.dm:207
    public override DmList get_patrol_area_types()
    {
        return Dm.add(typecacheof(Dm.list(
            typeof(area_station_engineering_break_room),
            typeof(area_station_engineering_lobby),
            typeof(area_station_engineering_main),
            typeof(area_station_engineering_storage),
            typeof(area_station_engineering_storage_shared),
            typeof(area_station_maintenance_department_engine),
            typeof(area_station_maintenance_department_electrical))), Dm.list(
            typeof(area_station_engineering), // plus base engineering
            typeof(area_station_engineering_atmos))) as DmList; // and base atmospherics
    }
}

// tg: code/modules/cargo/bounties/security.dm:221
public partial class datum_bounty_item_contraband : datum_bounty_item
{
    // tg: code/modules/cargo/bounties/security.dm:228
    public override object applies_to(obj O = null) => HAS_TRAIT(O, TRAIT_CONTRABAND) ? TRUE : FALSE;
}
