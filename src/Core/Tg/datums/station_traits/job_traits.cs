// Translated from tgstation (snapshot 1d99bd7f): code/datums/station_traits/job_traits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R2 (D327): the whole file: the job station traits and their lobby sign-up. The cargo gorilla's, the bridge assistant's and
// Pun Pun's jobs came with them (jobs/job_types/station_trait/); the veteran advisor's and the human AI's are not ported yet (inert),
// so SSstation keeps those two traits out of the pool until their job lands (DV432); an admin may still force one. The var blocks are
// generated (D111).

/**
 * A station trait which enables a temporary job
 * Generally speaking these should always all be mutually exclusive, don't have too many at once
 */
// tg: code/datums/station_traits/job_traits.dm:5
public partial class datum_station_trait_job : datum_station_trait
{
    // tg: code/datums/station_traits/job_traits.dm:17
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.append(ref blacklist, Dm.sub(subtypesof(typeof(datum_station_trait_job)), type)); // All but ourselves
        RegisterSignal(SSdcs, COMSIG_GLOB_PRE_JOBS_ASSIGNED, nameof(pre_jobs_assigned));
    }

    // tg: code/datums/station_traits/job_traits.dm:22
    public override object get_lobby_description()
    {
        return button_desc;
    }

    // tg: code/datums/station_traits/job_traits.dm:25
    public override async DmTask<object> on_lobby_button_click(mob_dead_new_player player = null)
    {
        await DmTask.CompletedTask;
        if (Dm.truthy(LAZYFIND(lobby_candidates, player)))
            LAZYREMOVE(ref lobby_candidates, player);
        else
            LAZYADD(ref lobby_candidates, player);
        return null;
    }

    // tg: code/datums/station_traits/job_traits.dm:31
    public override object get_lobby_icon_state(mob_dead_new_player player = null)
    {
        return Dm.truthy(LAZYFIND(lobby_candidates, player)) ? "signup_on" : "signup";
    }

    // tg: code/datums/station_traits/job_traits.dm:34
    public override object get_lobby_overlay_states(mob_dead_new_player player = null)
    {
        return Dm.list(Dm.truthy(LAZYFIND(lobby_candidates, player)) ? "tick" : "cross");
    }

    /// Called before we start assigning roles, assign ours first
    // tg: code/datums/station_traits/job_traits.dm:38  SIGNAL_HANDLER
    public virtual void pre_jobs_assigned()
    {
        sign_up_button = FALSE;
        foreach (mob_dead_new_player signee in Dm.each_cast<mob_dead_new_player>(lobby_candidates))
        {
            if (signee == null || signee.client == null || signee.mind == null || !Dm.eq(signee.ready, PLAYER_READY_TO_PLAY))
                LAZYREMOVE(ref lobby_candidates, signee);
        }

        var our_job = SSjob.get_job_type(job_to_add) as datum_job;
        our_job.total_positions = position_amount;
        our_job.spawn_positions = position_amount;
        while (Dm.truthy(length(lobby_candidates)) && position_amount > 0)
        {
            var picked_player = pick_n_take(lobby_candidates) as mob_dead_new_player;
            picked_player.mind.set_assigned_role(our_job);
            our_job.current_positions++;
            position_amount--;
        }

        lobby_candidates = null;
    }

    // tg: code/datums/station_traits/job_traits.dm:56
    public override object can_display_lobby_button(client player = null)
    {
        var our_job = SSjob.get_job_type(job_to_add) as datum_job;
        return Dm.truthy(our_job.player_old_enough(player)) && Dm.truthy(base.can_display_lobby_button(player)) ? TRUE : FALSE;
    }
}

/// Adds a gorilla to the cargo department, replacing the sloth and the mech
// tg: code/datums/station_traits/job_traits.dm:61
public partial class datum_station_trait_job_cargorilla : datum_station_trait_job
{
    // tg: code/datums/station_traits/job_traits.dm:68
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(SSatoms, COMSIG_SUBSYSTEM_POST_INITIALIZE, nameof(replace_cargo));
    }

    // tg: code/datums/station_traits/job_traits.dm:72
    public override object get_lobby_overlay_states(mob_dead_new_player player = null)
    {
        return Dm.list(Dm.truthy(LAZYFIND(lobby_candidates, player)) ? "gorilla_on" : "gorilla_off");
    }

    /// Remove the cargo equipment and personnel that are being replaced by a gorilla.
    // tg: code/datums/station_traits/job_traits.dm:76  SIGNAL_HANDLER
    public virtual void replace_cargo(datum source = null)
    {
        mob_living_basic_sloth cargo_sloth = GLOB.cargo_sloth;
        if (cargo_sloth == null)
        {
            lobby_candidates = Dm.list();
            sign_up_button = FALSE;
            return;
        }

        // hmm our sloth looks funny today
        qdel(cargo_sloth);
        // monkey carries the crates, the age of robot is over
        if (GLOB.cargo_ripley != null)
            qdel(GLOB.cargo_ripley);
    }
}

// tg: code/datums/station_traits/job_traits.dm:90
public partial class datum_station_trait_job_bridge_assistant : datum_station_trait_job
{
    // tg: code/datums/station_traits/job_traits.dm:98
    public override object get_lobby_overlay_states(mob_dead_new_player player = null)
    {
        return Dm.list("bridge_assistant");
    }

    // tg: code/datums/station_traits/job_traits.dm:101
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(SSatoms, COMSIG_SUBSYSTEM_POST_INITIALIZE, nameof(add_coffeemaker));
    }

    /// Creates a coffeemaker in the bridge, if we don't have one yet.
    // tg: code/datums/station_traits/job_traits.dm:106  SIGNAL_HANDLER
    public virtual void add_coffeemaker(datum source = null)
    {
        var bridge = GLOB.areas_by_type[typeof(area_station_command_bridge)] as area;
        if (bridge == null) //no bridge, what will he assist?
            return;
        DmList possible_coffeemaker_positions = Dm.list(typeof(area_station_command_bridge), typeof(area_station_command_meeting_room));
        var coffeemakers = SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_coffeemaker)) as DmList;
        foreach (obj_machinery coffeemaker in Dm.each_cast<obj_machinery>(coffeemakers)) //don't spawn a coffeemaker if there is already one on the bridge
        {
            if (Dm.truthy(is_type_in_list(get_area(coffeemaker) as datum, possible_coffeemaker_positions)))
                return;
        }
        DmList tables = Dm.list();
        foreach (turf area_turf in Dm.each_cast<turf>(bridge.get_turfs_from_all_zlevels()))
        {
            var table = Dm.locate<obj_structure_table>(area_turf);
            if (table == null)
                continue;
            if (Dm.truthy(Waiting.NoSleep(area_turf.is_blocked_turf(ignore_atoms: Dm.list(table))))) //don't spawn a coffeemaker on a fax machine or smth
                continue;
            Dm.append(ref tables, table);
        }
        if (!Dm.truthy(length(tables)))
            return;
        var picked_table = pick_n_take(tables) as atom;
        var picked_turf = get_turf(picked_table);
        if (Dm.truthy(length(tables)))
        {
            var another_table = pick(tables) as atom;
            foreach (obj thing_on_table in Dm.each<obj>(picked_turf)) //if there's paper bins or other shit on the table, get that off
            {
                if (thing_on_table == picked_table)
                    continue;
                if (HAS_TRAIT(thing_on_table, TRAIT_WALLMOUNTED) || Dm.truthy(Dm.bitand(thing_on_table.flags_1, ON_BORDER_1)) || thing_on_table.layer < TABLE_LAYER)
                    continue;
                if (Dm.truthy(thing_on_table.invisibility) || !Dm.truthy(thing_on_table.alpha) || !Dm.truthy(thing_on_table.mouse_opacity))
                    continue;
                Waiting.NoSleep(thing_on_table.forceMove(get_turf(another_table)));
            }
        }
        Dm.@new<obj_machinery_coffeemaker_impressa>(picked_turf);
        Dm.@new<obj_item_reagent_containers_cup_coffeepot>(picked_turf);
        Dm.@new<obj_item_storage_box_coffeepack>(picked_turf);
    }
}

// tg: code/datums/station_traits/job_traits.dm:142
public partial class datum_station_trait_job_veteran_advisor : datum_station_trait_job
{
    // tg: code/datums/station_traits/job_traits.dm:150
    public override object get_lobby_overlay_states(mob_dead_new_player player = null)
    {
        return Dm.list("veteran_advisor");
    }
}

// tg: code/datums/station_traits/job_traits.dm:153
public partial class datum_station_trait_job_human_ai : datum_station_trait_job
{
    // tg: code/datums/station_traits/job_traits.dm:163
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(SSjob, COMSIG_OCCUPATIONS_SETUP, nameof(remove_ai_job));
        RegisterSignal(SSatoms, COMSIG_SUBSYSTEM_POST_INITIALIZE, nameof(give_fax_machine));
    }

    // tg: code/datums/station_traits/job_traits.dm:168
    public override object get_lobby_overlay_states(mob_dead_new_player player = null)
    {
        return Dm.list(Dm.truthy(LAZYFIND(lobby_candidates, player)) ? "human_ai_on" : "human_ai_off");
    }

    // tg: code/datums/station_traits/job_traits.dm:171
    public override void revert()
    {
        UnregisterSignal(SSjob, COMSIG_OCCUPATIONS_SETUP);
        UnregisterSignal(SSatoms, COMSIG_SUBSYSTEM_POST_INITIALIZE);
        base.revert();
    }

    // tg: code/datums/station_traits/job_traits.dm:176  SIGNAL_HANDLER
    public virtual void remove_ai_job(datum source = null)
    {
        var department = SSjob.joinable_departments_by_type[typeof(datum_job_department_silicon)] as datum_job_department;
        department.remove_job(typeof(datum_job_ai));
        // if(GLOB.triple_ai_controller) position_amount = 3: the AI triumvirate's admin verb (admin/verbs/ai_triumvirate.dm) is not
        // ported, so its controller is never made
    }

    /// Gives the AI SAT a fax machine if it doesn't have one. This is copy pasted from Bridge Assistant's coffee maker.
    // tg: code/datums/station_traits/job_traits.dm:184  SIGNAL_HANDLER
    public virtual void give_fax_machine(datum source = null)
    {
        var sat_area = GLOB.areas_by_type[typeof(area_station_ai_satellite_chamber)] as area;
        if (sat_area == null)
            return;
        var fax_machines = SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_fax)) as DmList;
        foreach (obj_machinery fax_machine in Dm.each_cast<obj_machinery>(fax_machines)) //don't spawn a fax machine if one exists already.
        {
            // is_type_in_list(get_area(fax_machine), sat_area): tg passes the area itself, not a list, so the loop over its turfs never
            // finds an area and the check is always false (as here, with no list)
            if (Dm.truthy(is_type_in_list(get_area(fax_machine) as datum, null)))
                return;
        }
        DmList tables = Dm.list();
        foreach (turf area_turf in Dm.each_cast<turf>(sat_area.get_turfs_from_all_zlevels()))
        {
            var table = Dm.locate<obj_structure_table>(area_turf);
            if (table == null)
                continue;
            if (Dm.truthy(Waiting.NoSleep(area_turf.is_blocked_turf(ignore_atoms: Dm.list(table)))))
                continue;
            Dm.append(ref tables, table);
        }
        if (!Dm.truthy(length(tables)))
            return;
        var picked_table = pick_n_take(tables) as atom;
        var picked_turf = get_turf(picked_table);
        foreach (obj thing_on_table in Dm.each<obj>(picked_turf)) //if there's paper bins or other shit on the table, get that off
        {
            if (thing_on_table == picked_table)
                continue;
            if (HAS_TRAIT(thing_on_table, TRAIT_WALLMOUNTED) || Dm.truthy(Dm.bitand(thing_on_table.flags_1, ON_BORDER_1)) || thing_on_table.layer < TABLE_LAYER)
                continue;
            if (Dm.truthy(thing_on_table.invisibility) || !Dm.truthy(thing_on_table.alpha) || !Dm.truthy(thing_on_table.mouse_opacity))
                continue;
            if (Dm.truthy(length(tables)))
                Waiting.NoSleep(thing_on_table.forceMove(get_turf(pick(tables) as atom)));
            else
                qdel(thing_on_table);
        }
        Dm.@new<obj_machinery_fax_auto_name>(picked_turf);
    }
}

// tg: code/datums/station_traits/job_traits.dm:218
public partial class datum_station_trait_job_pun_pun : datum_station_trait_job
{
    // tg: code/datums/station_traits/job_traits.dm:226
    public override object get_lobby_overlay_states(mob_dead_new_player player = null)
    {
        return Dm.list(Dm.truthy(LAZYFIND(lobby_candidates, player)) ? "pun_pun_on" : "pun_pun_off");
    }

    // tg: code/datums/station_traits/job_traits.dm:229
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        //Make sure we don't have two Pun Puns if loaded before the start of the round.
        if (Dm.truthy(SSticker.HasRoundStarted()) || GLOB.the_one_and_only_punpun == null)
            return;
        Dm.@new<obj_effect_landmark_start_pun_pun>(GLOB.the_one_and_only_punpun.loc);
        qdel(GLOB.the_one_and_only_punpun);
    }
}
