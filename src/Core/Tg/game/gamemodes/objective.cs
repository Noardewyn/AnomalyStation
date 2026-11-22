// Translated from tgstation (snapshot 1d99bd7f): code/game/gamemodes/objective.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate package A1, D316): the objective base, considered_escaped and every objective whose pieces exist (assassinate,
// mutiny, maroon, debrain, protect, jailbreak and detain, hijack, elimination and highlander, block, purge, escape and escape with
// identity, survive, exile, martyr, nuclear, steal, capture, protect object, the changelings' absorb objectives, steal n of type with
// the guns and organs, custom, contract), generate_admin_objective_list. Inert with rows (not-translated.md section 130): the AI's
// objectives (robot army, survive as a malfunctioning AI, destroy the AI: the silicons), summon magic's (the wizard). Syndicate G4c: the
// special equipment's supply pod (special_equipment_fallback). BYOND's input() prompts of the admin tools are tgui's prompts
// (DV340). The var blocks are generated but steal_target (a typepath: hand-declared here) and team.

public partial class datum_controller_global_vars
{
    // tg: code/game/gamemodes/objective.dm:1  GLOBAL_LIST(admin_objective_list) //Prefilled admin assignable objective list
    public DmList admin_objective_list;

    // tg: code/game/gamemodes/objective.dm:1  (GLOBAL_UNMANAGED(admin_objective_list), __DEFINES/__globals.dm:11)
    public virtual void InitGlobaladmin_objective_list()
    {
        return;
    }

    // tg: code/game/gamemodes/objective.dm:620  GLOBAL_LIST_EMPTY(possible_items)
    public DmList possible_items;

    // tg: code/game/gamemodes/objective.dm:620  (GLOBAL_MANAGED(possible_items, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpossible_items()
    {
        possible_items = Dm.list();
        Dm.append(ref gvars_datum_init_order, "possible_items");
    }
}

// tg: code/game/gamemodes/objective.dm:3
public partial class datum_objective : datum
{
    // tg: code/game/gamemodes/objective.dm:5  var/datum/team/team (the types tool reads a typepath into it, as some tg var named team holds
    // one; an objective's team is a /datum/team)
    public datum_team team; //An alternative to 'owner': a team. Use this when writing new code.

    // tg: code/game/gamemodes/objective.dm:20
    [DmParams("text")]
    public override async DmTask New(params object[] __extra)
    {
        object text = Dm.arg(__extra, 0);
        if (Dm.truthy(text))
            explanation_text = Dm.str(text);
    }

    //Apparently objectives can be qdel'd. Learn a new thing every day
    // tg: code/game/gamemodes/objective.dm:25
    public override object Destroy(float? force = null)
    {
        return base.Destroy(force);
    }

    // tg: code/game/gamemodes/objective.dm:28
    public virtual object get_owners() // Combine owner and team into a single list.
    {
        DmList dot = Dm.truthy(team?.members) ? team.members.Copy() : Dm.list();
        if (owner != null)
            Dm.append(ref dot, owner);
        return dot;
    }

    // tg: code/game/gamemodes/objective.dm:33
    public virtual async DmTask admin_edit(mob admin = null)
    {
        return;
    }

    //Shared by few objective types
    // tg: code/game/gamemodes/objective.dm:37
    public virtual async DmTask admin_simple_target_pick(mob admin = null)
    {
        DmList possible_targets = Dm.list();
        object def_value = null;
        foreach (datum_mind possible_target in Dm.each<datum_mind>(SSticker.minds))
        {
            if (((object)possible_target != this) && ishuman(possible_target.current))
                Dm.append(ref possible_targets, possible_target.current);
        }

        possible_targets = Dm.add(Dm.list("Free objective", "Random"), sort_names(possible_targets)) as DmList;

        if (target?.current != null)
            def_value = target.current;

        // DEVIATION(DV340): BYOND's input() as null|anything in possible_targets is tgui's list prompt
        object new_target = await tgui_input_list(admin, "Select target:", "Objective target", possible_targets, def_value);
        if (!Dm.truthy(new_target))
            return;

        if (Dm.eq(new_target, "Free objective"))
            target = null;
        else if (Dm.eq(new_target, "Random"))
            await find_target();
        else
            target = ((mob)new_target).mind;

        update_explanation_text();
    }

    // tg: code/game/gamemodes/objective.dm:96
    public virtual object check_completion()
    {
        return completed;
    }

    /// Provides a string describing what a good job you did or did not do
    // tg: code/game/gamemodes/objective.dm:100
    public virtual object get_roundend_success_suffix()
    {
        if (Dm.truthy(no_failure))
            return ""; // Just print the objective with no success/fail evaluation, as it has no mechanical backing
        return Dm.truthy(check_completion()) ? span_greentext("Success!") : span_redtext("Fail.");
    }

    // tg: code/game/gamemodes/objective.dm:105
    public virtual object is_unique_objective(object possible_target = null, object dupe_search_range = null)
    {
        if (!islist(dupe_search_range))
        {
            stack_trace("Non-list passed as duplicate objective search range", "code/game/gamemodes/objective.dm", 107);
            dupe_search_range = Dm.list(dupe_search_range);
        }

        foreach (object A in Dm.each(dupe_search_range))
        {
            DmList objectives_to_compare = null;
            if (istype(A, typeof(datum_mind)))
            {
                datum_mind M = (datum_mind)A;
                objectives_to_compare = M.get_all_objectives() as DmList;
            }
            else if (istype(A, typeof(datum_antagonist)))
            {
                datum_antagonist G = (datum_antagonist)A;
                objectives_to_compare = G.objectives;
            }
            else if (istype(A, typeof(datum_team)))
            {
                datum_team T = (datum_team)A;
                objectives_to_compare = T.objectives;
            }
            foreach (datum_objective O in Dm.each<datum_objective>(objectives_to_compare))
            {
                if (istype(O, type) && Dm.eq(O.get_target(), possible_target))
                    return FALSE;
            }
        }
        return TRUE;
    }

    // tg: code/game/gamemodes/objective.dm:126
    public virtual object get_target()
    {
        return target;
    }

    // tg: code/game/gamemodes/objective.dm:129
    public virtual object is_valid_target(datum_mind possible_target = null)
    {
        if (!ishuman(possible_target.current))
            return FALSE;

        if (possible_target.current.stat == DEAD)
            return FALSE;

        area target_area = get_area(possible_target.current) as area;
        if (!HAS_TRAIT(SSstation, STATION_TRAIT_LATE_ARRIVALS) && istype(target_area, typeof(area_shuttle_arrival)))
            return FALSE;

        return TRUE;
    }

    //dupe_search_range is a list of antag datums / minds / teams
    // tg: code/game/gamemodes/objective.dm:143
    public virtual async DmTask<object> find_target(object dupe_search_range = null, DmList blacklist = null)
    {
        DmList owners = get_owners() as DmList;
        if (!Dm.truthy(dupe_search_range))
            dupe_search_range = get_owners();
        DmList possible_targets = Dm.list();
        bool try_target_late_joiners = false;
        foreach (object I in Dm.each(owners))
        {
            datum_mind O = (datum_mind)I;
            if (Dm.truthy(O.late_joiner))
                try_target_late_joiners = true;
        }
        foreach (datum_mind possible_target in Dm.each<datum_mind>(get_crewmember_minds()))
        {
            if (Dm.@in(possible_target, owners))
                continue;
            if (!Dm.truthy(is_unique_objective(possible_target, dupe_search_range)))
                continue;
            if (Dm.@in(possible_target, blacklist))
                continue;
            if (!Dm.truthy(is_valid_target(possible_target)))
                continue;
            Dm.append(ref possible_targets, possible_target);
        }
        if (try_target_late_joiners)
        {
            DmList all_possible_targets = possible_targets.Copy();
            foreach (object I in Dm.each(all_possible_targets))
            {
                datum_mind PT = (datum_mind)I;
                if (!Dm.truthy(PT.late_joiner))
                    Dm.remove(ref possible_targets, PT);
            }
            if (!Dm.truthy(possible_targets.len))
                possible_targets = all_possible_targets;
        }
        if (possible_targets.len > 0)
            target = pick(possible_targets) as datum_mind;
        update_explanation_text();
        return target;
    }

    // tg: code/game/gamemodes/objective.dm:176
    public virtual void update_explanation_text()
    {
        if (Dm.truthy(team_explanation_text) && LAZYLEN(get_owners()) > 1)
            explanation_text = Dm.str(team_explanation_text);
    }

    // tg: code/game/gamemodes/objective.dm:180
    public virtual async DmTask give_special_equipment(object special_equipment = null)
    {
        datum_mind receiver = pick(get_owners() as DmList) as datum_mind;
        if (!ishuman(receiver?.current))
            return;
        mob_living_carbon_human receiver_current = (mob_living_carbon_human)receiver.current;
        foreach (object equipment_path in Dm.each(special_equipment))
        {
            obj equipment_object = Dm.@new(equipment_path as Type) as obj;
            if (Dm.truthy(receiver_current.equip_to_storage(equipment_object as obj_item, ITEM_SLOT_BACK, indirect_action: TRUE)))
                continue;
            LAZYINITLIST(ref receiver.failed_special_equipment);
            Dm.append(ref receiver.failed_special_equipment, equipment_path);
            await receiver.try_give_equipment_fallback();
        }
    }
}

// tg: code/game/gamemodes/objective.dm:193
public partial class datum_action_special_equipment_fallback : datum_action
{
    // tg: code/game/gamemodes/objective.dm:199
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return FALSE;

        if (target is not datum_mind our_mind)
            throw Dm.CRASH(Dm.text("[] - [] has an incorrect target!", type, this), "code/game/gamemodes/objective.dm", 206);
        if (our_mind.current != owner)
            throw Dm.CRASH(Dm.text("[] - [] was owned by a mob which was not the current of the target mind!", type, this), "code/game/gamemodes/objective.dm", 208);

        if (Dm.truthy(LAZYLEN(our_mind.failed_special_equipment)))
        {
            await podspawn(Dm.list(
                Dm.kv("target", get_turf(owner)),
                Dm.kv("style", typeof(datum_pod_style_syndicate)),
                Dm.kv("spawn", our_mind.failed_special_equipment)));
            our_mind.failed_special_equipment = null;
        }
        qdel(this);
        return TRUE;
    }
}

public static partial class GlobalProcs
{
    /**
     * Checks if the passed mind is considered "escaped".
     *
     * Escaped mobs are used to check certain antag objectives / results.
     *
     * Escaped includes minds with alive, non-exiled mobs generally.
     *
     * Returns TRUE if they're a free person, or FALSE if they failed
     */
    // tg: code/game/gamemodes/objective.dm:72
    public static object considered_escaped(datum_mind escapee = null)
    {
        if (!Dm.truthy(considered_alive(escapee)))
            return FALSE;
        if (Dm.truthy(considered_exiled(escapee)))
            return FALSE;
        // "Into the sunset" force escaping for forced escape success
        if (Dm.truthy(escapee.force_escaped))
            return TRUE;
        // Station destroying events (blob, cult, nukies)? Just let them win, even if there was no hope of escape
        if (Dm.truthy(SSticker.force_ending) || Dm.truthy(GLOB.station_was_nuked))
            return TRUE;
        // Escape hasn't happened yet
        if (!Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ENDGAME))
            return FALSE;
        area current_area = get_area(escapee.current) as area;
        // In custody (shuttle brig) does not count as escaping
        if (current_area == null || istype(current_area, typeof(area_shuttle_escape_brig)))
            return FALSE;
        turf current_turf = get_turf(escapee.current);
        if (current_turf == null)
            return FALSE;
        // Finally, if we made it to centcom (or the syndie base - got hijacked), we're home free
        return Dm.truthy(current_turf.onCentCom()) ? current_turf.onCentCom() : current_turf.onSyndieBase();
    }

    //Ideally this would be all of them but laziness and unusual subtypes
    // tg: code/game/gamemodes/objective.dm:1001
    public static void generate_admin_objective_list()
    {
        GLOB.admin_objective_list = Dm.list();

        DmList allowed_types = sort_list(subtypesof(typeof(datum_objective)), "/proc/" + nameof(cmp_typepaths_asc)) as DmList;

        foreach (object goal in Dm.each(allowed_types))
        {
            if (!Dm.truthy(Dm.initial(goal as Type, (datum_objective p) => p.admin_grantable)))
                continue;
            // DEVIATION(DV340): an inert objective type (its pieces not ported) is not offered to the admins
            if (Attribute.IsDefined((Type)goal, typeof(DmInertAttribute), inherit: false))
                continue;
            GLOB.admin_objective_list[Dm.initial(goal as Type, (datum_objective p) => p.name)] = goal;
        }
    }
}

// tg: code/game/gamemodes/objective.dm:221
public partial class datum_objective_assassinate : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:227
    public override object check_completion()
    {
        return Dm.num(Dm.truthy(completed) || (!Dm.truthy(considered_alive(target)) || Dm.truthy(considered_afk(target)) || Dm.truthy(considered_exiled(target))));
    }

    // tg: code/game/gamemodes/objective.dm:230
    public override void update_explanation_text()
    {
        base.update_explanation_text();
        if (target?.current != null)
            explanation_text = Dm.text("Assassinate [], the [].", target.name, !Dm.truthy(target_role_type) ? (object)target.assigned_role.title : english_list(target.get_special_roles() as DmList));
        else
            explanation_text = "Free objective.";
    }

    // tg: code/game/gamemodes/objective.dm:237
    public override async DmTask admin_edit(mob admin = null)
    {
        await admin_simple_target_pick(admin);
    }
}

// tg: code/game/gamemodes/objective.dm:244
public partial class datum_objective_mutiny : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:240  #define DISCONNECT_GRACE_TIME (2 MINUTES), DISCONNECT_GRACE_WARNING_TIME (1 MINUTES) (file-local, #undef at :275)
    private const float DISCONNECT_GRACE_TIME = 2 * MINUTES;
    private const float DISCONNECT_GRACE_WARNING_TIME = 1 * MINUTES;

    // tg: code/game/gamemodes/objective.dm:252
    public virtual void warn_admins()
    {
        message_admins(Dm.text("[] has gone AFK with a mutiny objective that involves them. They only have [] seconds remaining before they are treated as if they were dead.", ADMIN_LOOKUPFLW(target.current), Math.Max(disconnect_timer - Dm.num(world.time), 0) / 10)  /* COOLDOWN_TIMELEFT, inline */);
    }

    // tg: code/game/gamemodes/objective.dm:255
    public override object check_completion()
    {
        if (target == null || !Dm.truthy(considered_alive(target)) || Dm.truthy(considered_exiled(target)))
            return TRUE;

        if (Dm.truthy(considered_afk(target)))
        {
            // COOLDOWN_STARTED, COOLDOWN_START, COOLDOWN_FINISHED, COOLDOWN_TIMELEFT, COOLDOWN_RESET (__DEFINES/cooldowns.dm), expanded inline
            if (disconnect_timer == 0)
            {
                disconnect_timer = Dm.num(world.time) + DISCONNECT_GRACE_TIME;
                warn_admins();
            }
            else if (disconnect_timer < Dm.num(world.time))
                return TRUE;
            else if (max(disconnect_timer - Dm.num(world.time), 0) <= DISCONNECT_GRACE_WARNING_TIME && !Dm.truthy(warned_admins))
            {
                warned_admins = TRUE;
                warn_admins();
            }
        }
        else
        {
            disconnect_timer = 0;
            warned_admins = FALSE;
        }

        turf T = get_turf(target.current);
        return Dm.num(T == null || !Dm.truthy(is_station_level(T.z)));
    }

    // tg: code/game/gamemodes/objective.dm:280
    public override void update_explanation_text()
    {
        base.update_explanation_text();
        if (target?.current != null)
            explanation_text = Dm.text("Assassinate or exile [], the [].", target.name, !Dm.truthy(target_role_type) ? (object)target.assigned_role.title : english_list(target.get_special_roles() as DmList));
        else
            explanation_text = "Free objective.";
    }
}

// tg: code/game/gamemodes/objective.dm:288
public partial class datum_objective_maroon : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:294
    public override object check_completion()
    {
        if (target == null)
            return TRUE;
        if (!Dm.truthy(considered_alive(target)))
            return TRUE;
        if (!Dm.truthy(target.current.onCentCom()) && !Dm.truthy(target.current.onSyndieBase()))
            return TRUE;
        return FALSE;
    }

    // tg: code/game/gamemodes/objective.dm:303
    public override void update_explanation_text()
    {
        if (target?.current != null)
            explanation_text = Dm.text("Prevent [], the [], from escaping alive.", target.name, !Dm.truthy(target_role_type) ? (object)target.assigned_role.title : english_list(target.get_special_roles() as DmList));
        else
            explanation_text = "Free objective.";
    }

    // tg: code/game/gamemodes/objective.dm:309
    public override async DmTask admin_edit(mob admin = null)
    {
        await admin_simple_target_pick(admin);
    }
}

// tg: code/game/gamemodes/objective.dm:313
public partial class datum_objective_debrain : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:318
    public override object check_completion()
    {
        if (target == null) //If it's a free objective.
            return TRUE;
        if (target.current == null || !isbrain(target.current))
            return FALSE;
        atom A = target.current;
        DmList owners = get_owners() as DmList;

        while (A.loc != null) // Check to see if the brainmob is on our person
        {
            A = A.loc as atom;
            foreach (datum_mind M in Dm.each<datum_mind>(owners))
            {
                if (M.current != null && M.current.stat != DEAD && A == M.current)
                    return TRUE;
            }
        }
        return FALSE;
    }

    // tg: code/game/gamemodes/objective.dm:333
    public override void update_explanation_text()
    {
        base.update_explanation_text();
        if (target?.current != null)
            explanation_text = Dm.text("Steal the brain of [], the [].", target.name, !Dm.truthy(target_role_type) ? (object)target.assigned_role.title : english_list(target.get_special_roles() as DmList));
        else
            explanation_text = "Free objective.";
    }

    // tg: code/game/gamemodes/objective.dm:340
    public override async DmTask admin_edit(mob admin = null)
    {
        await admin_simple_target_pick(admin);
    }
}

// tg: code/game/gamemodes/objective.dm:344
public partial class datum_objective_protect : datum_objective //The opposite of killing a dude.
{
    // tg: code/game/gamemodes/objective.dm:350
    public override object check_completion()
    {
        obj_item_organ_brain brain_target = null;
        if (target == null)
            return FALSE;
        if (Dm.truthy(human_check))
            brain_target = (target.current as mob_living_carbon)?.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        //Protect will always succeed when someone suicides
        return Dm.num(target == null || (target.current != null && HAS_TRAIT(target.current, TRAIT_SUICIDED)) || Dm.truthy(considered_alive(target, enforce_human: human_check)) || (brain_target != null && HAS_TRAIT(brain_target, TRAIT_SUICIDED)));
    }

    // tg: code/game/gamemodes/objective.dm:359
    public override void update_explanation_text()
    {
        base.update_explanation_text();
        if (target?.current != null)
            explanation_text = Dm.text("Protect [], the [].", target.name, !Dm.truthy(target_role_type) ? (object)target.assigned_role.title : english_list(target.get_special_roles() as DmList));
        else
            explanation_text = "Free objective.";
    }

    // tg: code/game/gamemodes/objective.dm:366
    public override async DmTask admin_edit(mob admin = null)
    {
        await admin_simple_target_pick(admin);
    }
}

// tg: code/game/gamemodes/objective.dm:375
public partial class datum_objective_jailbreak : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:381
    public override object check_completion()
    {
        return Dm.num(Dm.truthy(completed) || Dm.truthy(considered_escaped(target)));
    }

    // tg: code/game/gamemodes/objective.dm:384
    public override void update_explanation_text()
    {
        base.update_explanation_text();
        if (target?.current != null)
            explanation_text = Dm.text("Ensure that [], the [] escapes alive and out of custody.", target.name, !Dm.truthy(target_role_type) ? (object)target.assigned_role.title : english_list(target.get_special_roles() as DmList));
        else
            explanation_text = "Free objective.";
    }

    // tg: code/game/gamemodes/objective.dm:391
    public override async DmTask admin_edit(mob admin = null)
    {
        await admin_simple_target_pick(admin);
    }
}

// tg: code/game/gamemodes/objective.dm:395
public partial class datum_objective_jailbreak_detain : datum_objective_jailbreak
{
    // tg: code/game/gamemodes/objective.dm:397
    public override object check_completion()
    {
        return Dm.num(Dm.truthy(completed) || (!Dm.truthy(considered_escaped(target)) && (Dm.truthy(considered_alive(target)) && Dm.truthy(target.current.onCentCom()))));
    }

    // tg: code/game/gamemodes/objective.dm:400
    public override void update_explanation_text()
    {
        base.update_explanation_text();
        if (target?.current != null)
            explanation_text = Dm.text("Ensure that [], the [] is delivered to Nanotrasen alive and in custody.", target.name, !Dm.truthy(target_role_type) ? (object)target.assigned_role.title : english_list(target.get_special_roles() as DmList));
        else
            explanation_text = "Free objective.";
    }
}

// tg: code/game/gamemodes/objective.dm:408
public partial class datum_objective_hijack : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:416
    public override object check_completion() // Requires all owners to escape.
    {
        if (!Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ENDGAME))
            return FALSE;
        DmList owners = get_owners() as DmList;
        foreach (datum_mind M in Dm.each<datum_mind>(owners))
        {
            if (!Dm.truthy(considered_alive(M)) || !Dm.truthy(SSshuttle.emergency.shuttle_areas[get_area(M.current)]))
                return FALSE;
        }
        return SSshuttle.emergency.is_hijacked();
    }
}

// tg: code/game/gamemodes/objective.dm:426
public partial class datum_objective_elimination : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:431
    public override object check_completion()
    {
        if (!Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ENDGAME))
            return FALSE;
        DmList owners = get_owners() as DmList;
        foreach (datum_mind M in Dm.each<datum_mind>(owners))
        {
            if (!Dm.truthy(considered_alive(M, enforce_human: FALSE)) || !Dm.truthy(SSshuttle.emergency.shuttle_areas[get_area(M.current)]))
                return FALSE;
        }
        return SSshuttle.emergency.elimination_hijack();
    }
}

// tg: code/game/gamemodes/objective.dm:441
public partial class datum_objective_elimination_highlander : datum_objective_elimination
{
    // tg: code/game/gamemodes/objective.dm:444
    public override object check_completion()
    {
        if (!Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ENDGAME))
            return FALSE;
        DmList owners = get_owners() as DmList;
        foreach (datum_mind M in Dm.each<datum_mind>(owners))
        {
            if (!Dm.truthy(considered_alive(M, enforce_human: FALSE)) || !Dm.truthy(SSshuttle.emergency.shuttle_areas[get_area(M.current)]))
                return FALSE;
        }
        return SSshuttle.emergency.elimination_hijack(filter_by_human: FALSE, solo_hijack: TRUE);
    }
}

// tg: code/game/gamemodes/objective.dm:454
public partial class datum_objective_block : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:458
    public override object check_completion()
    {
        if (!Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ENDGAME))
            return TRUE;
        foreach (mob_living player in Dm.each<mob_living>(GLOB.player_list))
        {
            if (player.mind != null && player.stat != DEAD && Dm.truthy(Dm.bitand(player.mob_biotypes, MOB_ORGANIC)))
            {
                if (Dm.@in(get_area(player), SSshuttle.emergency.shuttle_areas))
                    return FALSE;
            }
        }
        return TRUE;
    }
}

// tg: code/game/gamemodes/objective.dm:468
public partial class datum_objective_purge : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:472
    public override object check_completion()
    {
        if (!Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ENDGAME))
            return TRUE;
        foreach (mob_living player in Dm.each<mob_living>(GLOB.player_list))
        {
            if (Dm.@in(get_area(player), SSshuttle.emergency.shuttle_areas) && player.mind != null && player.stat != DEAD && ishuman(player))
            {
                mob_living_carbon_human H = (mob_living_carbon_human)player;
                if (!Dm.eq(((datum_species)H.dna.species).id, SPECIES_HUMAN))
                    return FALSE;
            }
        }
        return TRUE;
    }
}

// tg: code/game/gamemodes/objective.dm:500
public partial class datum_objective_escape : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:505
    public override object check_completion()
    {
        // Require all owners escape safely.
        DmList owners = get_owners() as DmList;
        foreach (datum_mind M in Dm.each<datum_mind>(owners))
        {
            if (!Dm.truthy(considered_escaped(M)))
                return FALSE;
        }
        return TRUE;
    }
}

// tg: code/game/gamemodes/objective.dm:514
public partial class datum_objective_escape_escape_with_identity : datum_objective_escape
{
    // tg: code/game/gamemodes/objective.dm:518
    public override async DmTask<object> find_target(object dupe_search_range = null, DmList blacklist = null)
    {
        target = await base.find_target(dupe_search_range, blacklist) as datum_mind;
        update_explanation_text();
        return null;
    }

    // tg: code/game/gamemodes/objective.dm:522
    public override object is_valid_target(datum_mind possible_target = null)
    {
        if (HAS_TRAIT(possible_target.current, TRAIT_NO_DNA_COPY))
            return FALSE;
        return base.is_valid_target(possible_target);
    }

    // tg: code/game/gamemodes/objective.dm:527
    public override void update_explanation_text()
    {
        if (target?.current != null)
        {
            target_real_name = target.current.real_name;
            explanation_text = Dm.text("Escape on the shuttle or an escape pod with the identity of [], the []", target_real_name, target.assigned_role.title);
            mob_living_carbon_human H = null;
            if (ishuman(target.current))
                H = (mob_living_carbon_human)target.current;
            if (H != null && !Dm.eq(H.get_id_name(), target_real_name))
                target_missing_id = 1;
            else
                explanation_text += " while wearing their identification card";
            explanation_text += "."; //Proper punctuation is important!
        }
        else
            explanation_text = "Escape on the shuttle or an escape pod alive and without being in custody.";
    }

    // tg: code/game/gamemodes/objective.dm:543
    public override object check_completion()
    {
        DmList owners = get_owners() as DmList;
        if (target == null || !Dm.truthy(target_real_name))
            return base.check_completion();
        foreach (datum_mind M in Dm.each<datum_mind>(owners))
        {
            if (!ishuman(M.current) || !Dm.truthy(considered_escaped(M)))
                continue;
            mob_living_carbon_human H = (mob_living_carbon_human)M.current;
            if (Dm.eq(H.dna.real_name, target_real_name) && (Dm.eq(H.get_id_name(), target_real_name) || Dm.truthy(target_missing_id)))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/gamemodes/objective.dm:555
    public override async DmTask admin_edit(mob admin = null)
    {
        await admin_simple_target_pick(admin);
    }
}

// tg: code/game/gamemodes/objective.dm:559
public partial class datum_objective_survive : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:563
    public override object check_completion()
    {
        DmList owners = get_owners() as DmList;
        foreach (datum_mind M in Dm.each<datum_mind>(owners))
        {
            if (!Dm.truthy(considered_alive(M)))
                return FALSE;
        }
        return TRUE;
    }
}

// tg: code/game/gamemodes/objective.dm:583
public partial class datum_objective_exile : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:586
    public override object check_completion()
    {
        DmList owners = get_owners() as DmList;
        foreach (object m in Dm.each(owners))
        {
            datum_mind mind = (datum_mind)m;
            if (!Dm.truthy(considered_alive(mind)))
                return FALSE;
            if (Dm.truthy(SSmapping.level_has_any_trait(mind.current.z, Dm.list(ZTRAIT_STATION, ZTRAIT_CENTCOM)))) //went to centcom or ended round on station
                return FALSE;
        }
        return TRUE;
    }
}

// tg: code/game/gamemodes/objective.dm:596
public partial class datum_objective_martyr : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:600
    public override object check_completion()
    {
        DmList owners = get_owners() as DmList;
        foreach (datum_mind M in Dm.each<datum_mind>(owners))
        {
            if (Dm.truthy(considered_alive(M)))
                return FALSE;
            if (M.current != null && HAS_TRAIT(M.current, TRAIT_SUICIDED)) //killing yourself ISN'T glorious.
                return FALSE;
        }
        return TRUE;
    }
}

// tg: code/game/gamemodes/objective.dm:610
public partial class datum_objective_nuclear : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:615
    public override object check_completion()
    {
        if (Dm.truthy(GLOB.station_was_nuked))
            return TRUE;
        return FALSE;
    }
}

// tg: code/game/gamemodes/objective.dm:620
public partial class datum_objective_steal : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:626  var/obj/item/steal_target (rules 3.2, typed vars that hold wider values: tg stores the target's
    // typepath in it, targetinfo.targetitem or the admin's custom path, and tests items with istype against it)
    public Type steal_target = null; //Needed for custom objectives (they're just items, not datums).

    // tg: code/game/gamemodes/objective.dm:622  (the var overrides; the generated var block drops its constructor once a var is
    // hand-declared)
    public datum_objective_steal()
    {
        name = "steal";
        martyr_compatible = 0;
        admin_grantable = 1;
    }

    // tg: code/game/gamemodes/objective.dm:628
    public override object get_target()
    {
        return steal_target;
    }

    // tg: code/game/gamemodes/objective.dm:631
    public override async DmTask<object> find_target(object dupe_search_range = null, DmList blacklist = null)
    {
        DmList owners = get_owners() as DmList;
        if (!Dm.truthy(dupe_search_range))
            dupe_search_range = get_owners();
        DmList approved_targets = Dm.list();
        foreach (datum_objective_item possible_item in Dm.each<datum_objective_item>(GLOB.possible_items))
        {
            if (!Dm.truthy(possible_item.valid_objective_for(owners, require_owner: FALSE)))
                continue;
            if (!Dm.eq(possible_item.objective_type, OBJECTIVE_ITEM_TYPE_NORMAL))
                continue;
            if (!Dm.truthy(is_unique_objective(possible_item.targetitem, dupe_search_range)))
                continue;
            Dm.append(ref approved_targets, possible_item);
        }
        if (Dm.truthy(length(approved_targets)))
            return await set_target(pick(approved_targets) as datum_objective_item);
        return await set_target(null);
    }

    // tg: code/game/gamemodes/objective.dm:648
    public virtual async DmTask<object> set_target(datum_objective_item item = null)
    {
        if (item != null)
        {
            targetinfo = item;
            steal_target = targetinfo.targetitem;
            explanation_text = Dm.text("Steal [].", targetinfo.name);
            await give_special_equipment(targetinfo.special_equipment);
            return steal_target;
        }
        else
        {
            explanation_text = "Free objective.";
            return null;
        }
    }

    // tg: code/game/gamemodes/objective.dm:659
    public override async DmTask admin_edit(mob admin = null)
    {
        DmList possible_items_all = GLOB.possible_items;
        // DEVIATION(DV340): BYOND's input() as null|anything in ... is tgui's list prompt
        object new_target = await tgui_input_list(admin, "Select target:", "Objective target", Dm.add(sort_names(possible_items_all), "custom") as DmList, steal_target);
        if (!Dm.truthy(new_target))
            return;

        if (Dm.eq(new_target, "custom")) //Can set custom items.
        {
            // the admin's search for a custom item type (pick_closest_path and make_types_fancy, :665-673): the admin's type picker is not
            // ported (the spawn panel's helpers, D275)
            Dm.Deferred("code/game/gamemodes/objective.dm:665", "admin_edit: a custom steal target by type search (pick_closest_path, the admin type picker, D275)");
            return;
        }

        else
            await set_target(new_target as datum_objective_item);
    }

    // tg: code/game/gamemodes/objective.dm:680
    public override object check_completion()
    {
        DmList owners = get_owners() as DmList;
        if (steal_target == null)
            return TRUE;
        foreach (datum_mind M in Dm.each<datum_mind>(owners))
        {
            if (!isliving(M.current))
                continue;

            DmList all_items = M.current.get_all_contents() as DmList; //this should get things in cheesewheels, books, etc.

            foreach (obj I in Dm.each<obj>(all_items)) //Check for items
            {
                if (HAS_TRAIT(I, TRAIT_ITEM_OBJECTIVE_BLOCKED))
                    continue;

                if (istype(I, steal_target))
                {
                    if (targetinfo == null) //If there's no targetinfo, then that means it was a custom objective. At this point, we know you have the item, so return 1.
                        return TRUE;
                    else if (Dm.truthy(targetinfo.check_special_completion(I as obj_item))) //Returns true by default. Items with special checks will return true if the conditions are fulfilled.
                        return TRUE;
                }

                if (targetinfo != null && Dm.@in(I.type, targetinfo.altitems)) //Ok, so you don't have the item. Do you have an alternative, at least?
                {
                    if (Dm.truthy(targetinfo.check_special_completion(I as obj_item))) //Yeah, we do! Don't return 0 if we don't though - then you could fail if you had 1 item that didn't pass and got checked first!
                        return TRUE;
                }
            }
        }
        return FALSE;
    }
}

// tg: code/game/gamemodes/objective.dm:706
public partial class datum_objective_capture : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:709
    public virtual object gen_amount_goal()
    {
        target_amount = rand(5, 10);
        update_explanation_text();
        return target_amount;
    }

    // tg: code/game/gamemodes/objective.dm:714
    public override void update_explanation_text()
    {
        base.update_explanation_text();
        explanation_text = Dm.text("Capture [] lifeform\\s with an energy net. Live, rare specimens are worth more.", target_amount);
    }

    // tg: code/game/gamemodes/objective.dm:718
    public override object check_completion() //Basically runs through all the mobs in the area to determine how much they are worth.
    {
        float captured_amount = 0;
        area_centcom_central_command_areas_holding area = GLOB.areas_by_type[typeof(area_centcom_central_command_areas_holding)] as area_centcom_central_command_areas_holding;
        // for(var/mob/living/... in area): an area's contents are its turfs here (Builtin/area.builtin.cs), so the mobs are looked for on
        // the area's turfs, as BYOND lists them
        DmList in_area = Dm.list();
        foreach (DmList zlevel_turfs in Dm.each_cast<DmList>(area?.get_zlevel_turf_lists()))
        {
            foreach (turf area_turf in Dm.each_cast<turf>(zlevel_turfs))
                Dm.append(ref in_area, area_turf.contents);
        }
        foreach (mob_living_carbon_human mob in Dm.each<mob_living_carbon_human>(in_area)) //Humans.
        {
            if (HAS_TRAIT(mob, TRAIT_LESSER_HUMANOID))
            {
                captured_amount += 0.1f;
                continue;
            }
            if (mob.stat == DEAD) //Dead folks are worth less.
            {
                captured_amount += 0.5f;
                continue;
            }
            captured_amount += 1;
        }
        foreach (mob_living_carbon_alien_larva mob in Dm.each<mob_living_carbon_alien_larva>(in_area)) //Larva are important for research.
        {
            if (mob.stat == DEAD)
            {
                captured_amount += 0.5f;
                continue;
            }
            captured_amount += 1;
        }
        foreach (mob_living_carbon_alien_adult mob in Dm.each<mob_living_carbon_alien_adult>(in_area)) //Aliens are worth twice as much as humans.
        {
            if (istype(mob, typeof(mob_living_carbon_alien_adult_royal_queen))) //Queens (isalienqueen, __DEFINES/is_helpers.dm:169) are worth three times as much as humans.
            {
                if (mob.stat == DEAD)
                    captured_amount += 1.5f;
                else
                    captured_amount += 3;
                continue;
            }
            if (mob.stat == DEAD)
            {
                captured_amount += 1;
                continue;
            }
            captured_amount += 2;
        }
        return Dm.num(captured_amount >= target_amount);
    }

    // tg: code/game/gamemodes/objective.dm:747
    public override async DmTask admin_edit(mob admin = null)
    {
        // DEVIATION(DV340): BYOND's input() as num|null is tgui's number prompt
        object count = await tgui_input_number(admin, "How many mobs to capture ?", "capture", target_amount);
        if (Dm.truthy(count))
            target_amount = Dm.num(count);
        update_explanation_text();
    }
}

// tg: code/game/gamemodes/objective.dm:754
public partial class datum_objective_protect_object : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:757
    public virtual void set_target(obj O = null)
    {
        protect_target = O;
        RegisterSignal(protect_target, COMSIG_QDELETING, nameof(on_objective_qdel));
        update_explanation_text();
    }

    // tg: code/game/gamemodes/objective.dm:762
    public override void update_explanation_text()
    {
        base.update_explanation_text();
        if (protect_target != null)
            explanation_text = Dm.text("Protect \\the [] at all costs.", protect_target);
        else
            explanation_text = "Free objective.";
    }

    // tg: code/game/gamemodes/objective.dm:769
    public override object check_completion()
    {
        return Dm.num(protect_target != null);
    }

    // tg: code/game/gamemodes/objective.dm:772
    [SignalHandler]
    public virtual void on_objective_qdel()
    {
        protect_target = null;
    }
}

//Changeling Objectives

// tg: code/game/gamemodes/objective.dm:779
public partial class datum_objective_absorb : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:782
    public virtual object gen_amount_goal(float? lowbound = null, float? highbound = null)
    {
        lowbound ??= 4;
        highbound ??= 6;
        target_amount = rand(lowbound.Value, highbound.Value);
        float n_p = 1; //autowin
        DmList owners = get_owners() as DmList;
        if (Dm.eq(SSticker.current_state, GAME_STATE_SETTING_UP))
        {
            foreach (object i in Dm.each(GLOB.new_player_list))
            {
                mob_dead_new_player P = (mob_dead_new_player)i;
                if (Dm.eq(P.ready, PLAYER_READY_TO_PLAY) && !Dm.@in(P.mind, owners))
                    n_p++;
            }
        }
        else if (Dm.truthy(SSticker.IsRoundInProgress()))
        {
            foreach (mob_living_carbon_human P in Dm.each<mob_living_carbon_human>(GLOB.player_list))
            {
                if (!Dm.truthy(IS_CHANGELING(P)) && !Dm.@in(P.mind, owners))
                    n_p++;
            }
        }
        target_amount = Dm.num(min(target_amount, n_p));

        update_explanation_text();
        return target_amount;
    }

    // tg: code/game/gamemodes/objective.dm:800
    public override void update_explanation_text()
    {
        base.update_explanation_text();
        explanation_text = Dm.text("Extract [] compatible genome\\s.", target_amount);
    }

    // tg: code/game/gamemodes/objective.dm:804
    public override async DmTask admin_edit(mob admin = null)
    {
        // DEVIATION(DV340): BYOND's input() as num|null is tgui's number prompt
        object count = await tgui_input_number(admin, "How many people to absorb?", "absorb", target_amount);
        if (Dm.truthy(count))
            target_amount = Dm.num(count);
        update_explanation_text();
    }

    // tg: code/game/gamemodes/objective.dm:810
    public override object check_completion()
    {
        DmList owners = get_owners() as DmList;
        float absorbed_count = 0;
        foreach (datum_mind M in Dm.each<datum_mind>(owners))
        {
            if (M == null)
                continue;
            datum_antagonist_changeling changeling = M.has_antag_datum(typeof(datum_antagonist_changeling)) as datum_antagonist_changeling;
            if (changeling == null || !Dm.truthy(changeling.stored_profiles))
                continue;
            absorbed_count += changeling.absorbed_count;
        }
        return Dm.num(absorbed_count >= target_amount);
    }
}

// tg: code/game/gamemodes/objective.dm:823
public partial class datum_objective_absorb_most : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:826
    public override object check_completion()
    {
        DmList owners = get_owners() as DmList;
        float absorbed_count = 0;
        foreach (datum_mind M in Dm.each<datum_mind>(owners))
        {
            if (M == null)
                continue;
            datum_antagonist_changeling changeling = M.has_antag_datum(typeof(datum_antagonist_changeling)) as datum_antagonist_changeling;
            if (changeling == null || !Dm.truthy(changeling.stored_profiles))
                continue;
            absorbed_count += changeling.absorbed_count;
        }

        foreach (datum_antagonist_changeling changeling2 in Dm.each<datum_antagonist_changeling>(GLOB.antagonists))
        {
            if (changeling2.owner == null || changeling2.owner == owner || !Dm.truthy(changeling2.stored_profiles) || changeling2.absorbed_count < absorbed_count)
                continue;
            return FALSE;
        }
        return TRUE;
    }
}

// tg: code/game/gamemodes/objective.dm:844
public partial class datum_objective_absorb_changeling : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:847
    public override object check_completion()
    {
        DmList owners = get_owners() as DmList;
        foreach (object m in Dm.each(owners))
        {
            datum_mind ling_mind = (datum_mind)m;
            datum_antagonist_changeling changeling = ling_mind.has_antag_datum(typeof(datum_antagonist_changeling)) as datum_antagonist_changeling;
            if (changeling == null)
                continue;

            float total_genetic_points = changeling.genetic_points;
            foreach (object power_path in Dm.each(changeling.purchased_powers))
            {
                datum_action_changeling power = changeling.purchased_powers[power_path] as datum_action_changeling;
                total_genetic_points += Dm.num(power.dna_cost);
            }

            if (total_genetic_points > Dm.num(Dm.initial(changeling, (datum_antagonist_changeling p) => p.genetic_points)))
                return TRUE;
        }
        return completed;
    }
}

//End Changeling Objectives

// tg: code/game/gamemodes/objective.dm:899
public partial class datum_objective_steal_n_of_type : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:906
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        wanted_items = typecacheof(wanted_items) as DmList;
    }

    // tg: code/game/gamemodes/objective.dm:910
    public override object check_completion()
    {
        DmList owners = get_owners() as DmList;
        float stolen_count = 0;
        foreach (datum_mind M in Dm.each<datum_mind>(owners))
        {
            if (!isliving(M.current))
                continue;
            DmList all_items = M.current.get_all_contents() as DmList; //this should get things in cheesewheels, books, etc.
            foreach (obj current_item in Dm.each<obj>(all_items)) //Check for wanted items
            {
                if (Dm.truthy(is_type_in_typecache(current_item, wanted_items)))
                {
                    if (Dm.truthy(check_if_valid_item(current_item as obj_item)))
                        stolen_count++;
                }
            }
        }
        return Dm.num(stolen_count >= Dm.num(amount));
    }

    // tg: code/game/gamemodes/objective.dm:923
    public virtual object check_if_valid_item(obj_item current_item = null)
    {
        return TRUE;
    }
}

// tg: code/game/gamemodes/objective.dm:927
public partial class datum_objective_steal_n_of_type_summon_guns : datum_objective_steal_n_of_type
{
    // tg: code/game/gamemodes/objective.dm:932
    public override object check_if_valid_item(obj_item current_item = null)
    {
        obj_item_gun gun = (obj_item_gun)current_item;
        return Dm.num(!Dm.truthy(Dm.bitand(gun.gun_flags, NOT_A_REAL_GUN)));
    }
}

// tg: code/game/gamemodes/objective.dm:963
public partial class datum_objective_steal_n_of_type_organs : datum_objective_steal_n_of_type
{
    // tg: code/game/gamemodes/objective.dm:968
    public override object check_completion()
    {
        DmList owners = get_owners() as DmList;
        float stolen_count = 0;
        foreach (datum_mind mind in Dm.each<datum_mind>(owners))
        {
            if (!isliving(mind.current))
                continue;
            DmList all_items = mind.current.get_all_contents() as DmList; //this should get things in cheesewheels, books, etc.
            foreach (obj_item stolen in Dm.each<obj_item>(all_items)) //Check for wanted items
            {
                bool found = false;
                foreach (object wanted_type in Dm.each(wanted_items))
                {
                    if (istype(stolen, wanted_type as Type))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    continue;
                //this is an objective item
                obj_item_organ wanted = (obj_item_organ)stolen;
                if (!Dm.truthy(Dm.bitand(wanted.organ_flags, ORGAN_FAILING)) && !Dm.truthy(IS_ROBOTIC_ORGAN(wanted)))
                    stolen_count++;
            }
        }
        return Dm.num(stolen_count >= Dm.num(amount));
    }
}

//Created by admin tools
// tg: code/game/gamemodes/objective.dm:991
public partial class datum_objective_custom : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:995
    public override async DmTask admin_edit(mob admin = null)
    {
        object expl = await stripped_input(admin, "Custom objective:", "Objective", explanation_text);
        if (Dm.truthy(expl))
            explanation_text = Dm.str(expl);
    }
}

// tg: code/game/gamemodes/objective.dm:1012
public partial class datum_objective_contract : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:1016
    public override object is_valid_target(datum_mind possible_target = null)
    {
        if (HAS_TRAIT(possible_target, TRAIT_HAS_BEEN_KIDNAPPED))
            return FALSE;
        return base.is_valid_target(possible_target);
    }

    // Generate a random valid area on the station that the dropoff will happen.
    // tg: code/game/gamemodes/objective.dm:1022
    public virtual void generate_dropoff()
    {
        bool found = false;
        while (!found)
        {
            area dropoff_area = pick(GLOB.areas) as area;
            if (dropoff_area != null && Dm.@in(dropoff_area.type, GLOB.the_station_areas) && !Dm.truthy(dropoff_area.outdoors))
            {
                dropoff = dropoff_area;
                found = true;
            }
        }
    }

    // Check if both the contractor and contract target are at the dropoff point.
    // tg: code/game/gamemodes/objective.dm:1031
    public virtual object dropoff_check(mob user = null, mob target = null)
    {
        area user_area = get_area(user) as area;
        area target_area = get_area(target) as area;

        return Dm.num(istype(user_area, dropoff?.type) && istype(target_area, dropoff?.type));
    }
}

// tg: code/game/gamemodes/objective.dm:482 (silicons SI8, D326)
public partial class datum_objective_robot_army : datum_objective
{
    // tg: code/game/gamemodes/objective.dm:487
    public override object check_completion()
    {
        int counter = 0;
        DmList owners = get_owners() as DmList;
        foreach (datum_mind M in Dm.each<datum_mind>(owners))
        {
            if (M.current is not mob_living_silicon_ai A)
                continue;
            foreach (mob_living_silicon_robot R in Dm.each<mob_living_silicon_robot>(A.connected_robots))
            {
                if (R.stat != DEAD)
                    counter++;
            }
        }
        return counter >= 8 ? TRUE : FALSE;
    }
}

//Like survive, but for Malf AIs
// tg: code/game/gamemodes/objective.dm:570 (silicons SI8, D326)
public partial class datum_objective_survive_malf : datum_objective_survive
{
    // tg: code/game/gamemodes/objective.dm:575
    public override object check_completion()
    {
        DmList owners = get_owners() as DmList;
        foreach (datum_mind mindobj in Dm.each<datum_mind>(owners))
        {
            if (!iscyborg(mindobj) && !Dm.truthy(considered_alive(mindobj, FALSE))) //Shells (and normal borgs for that matter) are considered alive for Malf
                return FALSE;
            return TRUE;
        }
        return null;
    }
}
