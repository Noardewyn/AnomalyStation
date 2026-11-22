// Translated from tgstation (snapshot 1d99bd7f): code/datums/mind/_mind.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only what a player's mind and its ghost need (the ghosts, 2026-10-16, D151): New, Destroy, set_current,
// clear_current, set_original_character, set_death_time, get_ghost, grab_ghost, set_assigned_role and sync_mind (with its
// new player and observer overrides); M6 slice 5 (a gibbed player's brain): transfer_to. The rest of the file (serialize_list, vv_edit_var,
// set_holy_role, the greeting, get_work_areas) waits for minds' later users (docs/ai/specs/not-translated.md section 16); gaps RG2
// (D339): the addiction points (add_addiction_points, remove_addiction_points). Gaps package G4: witnessed_death and experienced_death. Syndicate A1 (D316): the traitor panel's Topic and the antagonist
// HUD and body transfer of transfer_to. Syndicate A2: the traitor panel's uplink and telecrystal links.

// tg: code/datums/mind/_mind.dm:32
public partial class datum_mind : datum
{
    /// Key of the mob
    // tg: code/datums/mind/_mind.dm:34  var/key (types-converter.md 3.5: rule V3 makes the untyped var Type, since tg assigns
    // a typepath to some var named key; a mind's key is a player's key, text)
    public object key;

    /// Current mob this mind datum is attached to
    // tg: code/datums/mind/_mind.dm:40  var/mob/living/current (rules 3.2, typed vars that hold wider values: set_current and
    // transfer_to take any /mob, an AI eye or a test mob among them, so the field is /mob)
    public mob current;

    // tg: code/datums/mind/_mind.dm:113
    [DmParams("_key")]
    public override async DmTask New(params object[] __extra)
    {
        object _key = Dm.arg(__extra, 0);
        key = _key;
        init_known_skills();
        set_assigned_role(SSjob.get_job_type(typeof(datum_job_unassigned)) as datum_job); // Unassigned by default.
    }

    // tg: code/datums/mind/_mind.dm:118
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref SSticker.minds, this);
        QDEL_NULL(ref antag_hud);
        QDEL_LIST_ASSOC_VAL(memories);
        QDEL_NULL(ref memory_panel);
        QDEL_LIST(antag_datums);
        set_current(null);
        return base.Destroy(force);
    }

    // tg: code/datums/mind/_mind.dm:160
    public virtual void set_current(mob new_current = null)
    {
        if (Dm.truthy(new_current) && QDELETED(new_current))
            throw Dm.CRASH("Tried to set a mind's current var to a qdeleted mob, what the fuck", "code/datums/mind/_mind.dm", 162);
        if (Dm.truthy(current))
            UnregisterSignal(this, COMSIG_QDELETING);
        current = new_current;
        if (Dm.truthy(current))
            RegisterSignal(this, COMSIG_QDELETING, nameof(clear_current));
    }

    // tg: code/datums/mind/_mind.dm:169
    [SignalHandler]
    public virtual void clear_current(datum source = null)
    {
        set_current(null);
    }

    // tg: code/datums/mind/_mind.dm:173  (M6 slice 5: a gibbed player's brain takes the mind, transfer_identity)
    public virtual async DmTask transfer_to(mob new_character = null, float? force_key_move = null)
    {
        force_key_move ??= 0;
        set_original_character(null);
        if (Dm.truthy(current)) // remove ourself from our old body's mind variable
        {
            current.mind = null;
            UnregisterSignal(current, COMSIG_LIVING_DEATH);
            SStgui.on_transfer(current, new_character);
        }

        if (Dm.truthy(key))
        {
            if (!Dm.eq(new_character.key, key)) //if we're transferring into a body with a key associated which is not ours
                new_character.ghostize(TRUE); //we'll need to ghostize so that key isn't mobless.
        }
        else
            key = new_character.key;

        if (new_character.mind != null) //disassociate any mind currently in our new body's mind variable
            new_character.mind.set_current(null);

        mob_living old_current = current as mob_living;
        if (old_current != null)
        {
            //transfer anyone observing the old character to the new one
            // old_current.transfer_observers_to(new_character) (datums/components/orbiter.dm:209): the orbiters (the ghosts'
            // Orbit, not in D151's scope); no atom has one, so tg's proc returns at once
            Dm.Deferred("code/datums/mind/_mind.dm:192", "transfer_to: transfer_observers_to, the orbiting ghosts follow the mind (orbit, after D151)");

            // Offload all mind languages from the old holder to a temp one
            datum_language_holder_empty temp_holder = Dm.@new<datum_language_holder_empty>();
            datum_language_holder old_holder = old_current.get_language_holder();
            datum_language_holder new_holder = new_character.get_language_holder();
            // Off load mind languages to the temp holder momentarily
            new_holder.transfer_mind_languages(temp_holder);
            // Transfer the old holder's mind languages to the new holder
            old_holder.transfer_mind_languages(new_holder);
            // And finally transfer the temp holder's mind languages back to the old holder
            temp_holder.transfer_mind_languages(old_holder);
        }

        set_current(new_character); //associate ourself with our new body
        QDEL_NULL(ref antag_hud);
        new_character.mind = this; //and associate our new body with ourself
        antag_hud = new_character.add_alt_appearance(typeof(datum_atom_hud_alternate_appearance_basic_antagonist_hud), "combo_hud", this) as datum_atom_hud_alternate_appearance_basic_antagonist_hud;
        foreach (datum_antagonist antag_datum in Dm.each_cast<datum_antagonist>(antag_datums)) //Makes sure all antag datums effects are applied in the new body
            await antag_datum.on_body_transfer(old_current, current as mob_living);
        if (iscarbon(new_character))
        {
            mob_living_carbon carbon_character = (mob_living_carbon)new_character;
            carbon_character.last_mind = this;
        }

        RegisterSignal(new_character, COMSIG_LIVING_DEATH, nameof(set_death_time));
        if (Dm.truthy(active) || Dm.truthy(force_key_move))
            new_character.PossessByPlayer(key); //now transfer the key to link the client to our new body
        if (Dm.truthy(new_character.client))
        {
            new_character.client.recent_examines = null; // LAZYCLEARLIST
            // new_character.client.init_verbs(): the client's verbs of the character (the verb layer, M9)
            Dm.Deferred("code/datums/mind/_mind.dm:220", "transfer_to: client.init_verbs(), the character's verbs (verbs, M10+)");
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_MIND_TRANSFERRED]))
            this._SendSignal(COMSIG_MIND_TRANSFERRED, Dm.list(this, old_current));
        if (Dm.truthy(current._listen_lookup?[COMSIG_MOB_MIND_TRANSFERRED_INTO]))
            current._SendSignal(COMSIG_MOB_MIND_TRANSFERRED_INTO, Dm.list(current, old_current, this));
        if (old_current != null && Dm.truthy(old_current._listen_lookup?[COMSIG_MOB_MIND_TRANSFERRED_OUT_OF]))
            old_current._SendSignal(COMSIG_MOB_MIND_TRANSFERRED_OUT_OF, Dm.list(old_current, current, this));
    }

    //I cannot trust you fucks to do this properly
    // tg: code/datums/mind/_mind.dm:233
    public virtual void set_original_character(object new_original_character = null)
    {
        original_character = WEAKREF(new_original_character as datum) as datum_weakref;
    }

    // tg: code/datums/mind/_mind.dm:236
    [SignalHandler]
    public virtual void set_death_time()
    {
        last_death = world.time;
    }

    // tg: code/datums/mind/_mind.dm:460
    public virtual object get_ghost(object even_if_they_cant_reenter = null, object ghosts_with_clients = null)
    {
        foreach (mob_dead_observer G in Dm.each<mob_dead_observer>(Dm.truthy(ghosts_with_clients) ? GLOB.player_list : GLOB.dead_mob_list))
        {
            if (G.mind == this)
            {
                if (Dm.truthy(G.can_reenter_corpse) || Dm.truthy(even_if_they_cant_reenter))
                    return G;
                break;
            }
        }
        return null;
    }

    // tg: code/datums/mind/_mind.dm:468
    public virtual object grab_ghost(object force = null)
    {
        object dot = null;
        mob_dead_observer G = get_ghost(even_if_they_cant_reenter: force) as mob_dead_observer;
        dot = G;
        if (Dm.truthy(G))
            G.reenter_corpse();
        return dot;
    }

    /// Setter for the assigned_role job datum.
    // tg: code/datums/mind/_mind.dm:491
    public virtual object set_assigned_role(datum_job new_role = null)
    {
        object dot = null;
        if (assigned_role == new_role)
            return dot;
        if (!is_job(new_role))
            throw Dm.CRASH(Dm.text("set_assigned_role called with invalid role: []", new_role == null ? "null" : new_role), "code/datums/mind/_mind.dm", 495);
        dot = assigned_role;
        assigned_role = new_role;
        if (current != null)
        {
            if (Dm.truthy(current._listen_lookup?[COMSIG_MOB_MIND_SET_ROLE]))
                current._SendSignal(COMSIG_MOB_MIND_SET_ROLE, Dm.list(current, new_role));
            // tg: :500  current.client?.tgui_panel?.send_player_info()
            Dm.Deferred("code/datums/mind/_mind.dm:500", "set_assigned_role: current.client?.tgui_panel?.send_player_info(), the chat panel's job info (the tgui chat panel, M10+)");
        }
        return dot;
    }
}

// tg: code/datums/mind/_mind.dm:530 (interleaved types, rules 1.9)
public partial class mob : atom_movable
{
    // tg: code/datums/mind/_mind.dm:530
    public virtual async DmTask sync_mind()
    {
        await mind_initialize(); //updates the mind (or creates and initializes one if one doesn't exist)
        mind.active = TRUE; //indicates that the mind is currently synced with a client
    }
}

// tg: code/datums/mind/_mind.dm:534
public partial class mob_dead_new_player : mob_dead
{
    // tg: code/datums/mind/_mind.dm:534
    public override async DmTask sync_mind()
    {
        return;
    }
}

// tg: code/datums/mind/_mind.dm:537
public partial class mob_dead_observer : mob_dead
{
    // tg: code/datums/mind/_mind.dm:537
    public override async DmTask sync_mind()
    {
        return;
    }
}

// tg: code/datums/mind/_mind.dm:521 (interleaved types, rules 1.9; M10 slice F: the job's spawn message)
public partial class datum_mind : datum
{
    // tg: code/datums/mind/_mind.dm:521
    public virtual object set_assigned_role_with_greeting(datum_job new_role = null, client incoming_client = null)
    {
        object dot = set_assigned_role(new_role);
        if (assigned_role != new_role)
            return dot;

        object intro_message = new_role.get_spawn_message();
        if (incoming_client != null && Dm.truthy(intro_message))
            to_chat(incoming_client, intro_message);
        return dot;
    }
}

// tg: code/datums/mind/_mind.dm:551 (interleaved types, rules 1.9; gaps package G4: the deaths a mind lives through)
public partial class datum_mind : datum
{
    /// Called when we witness the death of a humanoid mob.
    // tg: code/datums/mind/_mind.dm:551
    public virtual void witnessed_death(mob_living dead_mob = null)
    {
        if (HAS_TRAIT(dead_mob, TRAIT_SPAWNED_MOB) || !ishuman(dead_mob) || Dm.truthy(Dm.bitand(dead_mob.flags_1, ADMIN_SPAWNED_1)))
            return;

        // every humanoid death gives us % resistance to the next one
        desensitized_level = max(desensitized_level - DESENSITIZED_REDUCTION_PER_DEATH, DESENSITIZED_MINIMUM);
        deaths_witnessed += 1;

        // if you manage to gain 90% resistance to death moodlets in one shift, you get an "achievement"
        if (deaths_witnessed * DESENSITIZED_REDUCTION_PER_DEATH >= (1.0f - DESENSITIZED_MINIMUM))
            // tg: :561  current.client?.give_award(/datum/award/achievement/misc/desensitized, current)
            Dm.Deferred("code/datums/mind/_mind.dm:561", "witnessed_death: the desensitized achievement (achievements, M10+)");
    }

    /// Called when this mob is killed, but not gibbed or dusted
    // tg: code/datums/mind/_mind.dm:564
    public virtual void experienced_death()
    {
        witnessed_death(current as mob_living); // you get desensitized for your own death!
    }

    // tg: code/datums/mind/_mind.dm:326
    public virtual void set_holy_role(float? new_holy_role = null)
    {
        if (Dm.eq(holy_role, new_holy_role))
            return;
        bool was_holy = Dm.truthy(holy_role);
        holy_role = new_holy_role;
        if (Dm.truthy(holy_role))
            ADD_TRAIT(this, TRAIT_SEE_BLESSED_TILES, HOLY_TRAIT);
        else
            REMOVE_TRAIT(this, TRAIT_SEE_BLESSED_TILES, HOLY_TRAIT);
        // (!Dm.truthy(current._listen_lookup?[COMSIG_MOB_MIND_SET_HOLY_ROLE]) ? (object)NONE : current._SendSignal(COMSIG_MOB_MIND_SET_HOLY_ROLE, Dm.list(current, new_holy_role))) (rules 11.1 (e), a statement)
        if (Dm.truthy(current?._listen_lookup?[COMSIG_MOB_MIND_SET_HOLY_ROLE]))
            current._SendSignal(COMSIG_MOB_MIND_SET_HOLY_ROLE, Dm.list(current, new_holy_role));
        //the signal stops tracking when losing holy roles, but since we're gaining it, give us our HUDs if we're becoming holy.
        if (!was_holy && Dm.truthy(holy_role))
            Dm.Deferred("code/datums/mind/_mind.dm:338", "set_holy_role: the blessed_aware HUD's check_hud over GLOB.active_alternate_appearances (the blessed tiles' overlay, modules/religion)");
    }
}

// tg: code/datums/mind/_mind.dm:236 (interleaved types, rules 1.9; syndicate package A1, D316: the traitor panel's links)
public partial class datum_mind : datum
{
    // tg: code/datums/mind/_mind.dm:236
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        if (!Dm.truthy(check_rights(R_ADMIN)))
            return null;

        bool self_antagging = usr == current;

        if (Dm.truthy(href_list["add_antag"]))
        {
            Type antag_type = Dm.text2path(Dm.str(href_list["add_antag"])) as Type;
            // DEVIATION(DV340): an inert antagonist type (its package not merged yet) is not added (the panel greys it out)
            if (antag_type != null && !Attribute.IsDefined(antag_type, typeof(DmInertAttribute), inherit: false))
                await add_antag_wrapper(antag_type, usr);
        }

        if (Dm.truthy(href_list["remove_antag"]))
        {
            object found = Dm.locate_ref(href_list["remove_antag"]);
            datum_antagonist A = Dm.@in(found, antag_datums) ? found as datum_antagonist : null;
            if (A == null)
            {
                to_chat(usr, span_warning("Invalid antagonist ref to be removed."));
                return null;
            }
            await A.admin_remove(usr);
        }

        if (Dm.truthy(href_list["open_antag_vv"]))
        {
            object found = Dm.locate_ref(href_list["open_antag_vv"]);
            datum_antagonist to_vv = Dm.@in(found, antag_datums) ? found as datum_antagonist : null;
            if (to_vv == null)
            {
                to_chat(usr, span_warning("Invalid antagonist ref to be vv'd."));
                return null;
            }
            // usr.client?.debug_variables(to_vv): the variable editor is not ported (the admin tools, D275)
            Dm.Deferred("code/datums/mind/_mind.dm:256", "Topic: open_antag_vv, the variable editor of the antagonist datum (the admin tools, D275)");
        }

        if (Dm.truthy(href_list["role_edit"]))
        {
            // DEVIATION(DV340): BYOND's input() as null|anything in ... is tgui's list prompt
            object new_role = await tgui_input_list(usr, "Select new role", "Assigned role", sort_list(SSjob.name_occupations) as DmList, assigned_role.title);
            if (new_role == null)
                return null;
            datum_job new_job = SSjob.get_job(new_role) as datum_job;
            if (new_job == null)
            {
                to_chat(usr, span_warning("Job not found."));
                return null;
            }
            set_assigned_role(new_job);
        }

        else if (Dm.truthy(href_list["obj_edit"]) || Dm.truthy(href_list["obj_add"]))
        {
            float objective_pos = 0; //Edited objectives need to keep same order in antag objective list
            object def_value = null;
            datum_antagonist target_antag = null;
            datum_objective old_objective = null; //The old objective we're replacing/editing
            datum_objective new_objective = null; //New objective we're be adding

            if (Dm.truthy(href_list["obj_edit"]))
            {
                object found = Dm.locate_ref(href_list["obj_edit"]);
                foreach (datum_antagonist A in Dm.each<datum_antagonist>(antag_datums))
                {
                    old_objective = Dm.@in(found, A.objectives) ? found as datum_objective : null;
                    if (old_objective != null)
                    {
                        target_antag = A;
                        objective_pos = A.objectives.Find(old_objective);
                        break;
                    }
                }
                if (old_objective == null)
                {
                    to_chat(usr, "Invalid objective.");
                    return null;
                }
            }
            else
            {
                if (Dm.truthy(href_list["target_antag"]))
                {
                    object found = Dm.locate_ref(href_list["target_antag"]);
                    datum_antagonist X = Dm.@in(found, antag_datums) ? found as datum_antagonist : null;
                    if (X != null)
                        target_antag = X;
                }
                if (target_antag == null)
                {
                    switch ((int)Dm.num(antag_datums?.len ?? 0))
                    {
                        case 0:
                            target_antag = await add_antag_datum(typeof(datum_antagonist_custom)) as datum_antagonist;
                            break;
                        case 1:
                            target_antag = antag_datums[1] as datum_antagonist;
                            break;
                        default:
                        {
                            // DEVIATION(DV340): BYOND's input() as null|anything in ... is tgui's list prompt
                            object target = await tgui_input_list(usr, "Which antagonist gets the objective:", "Antagonist", Dm.add(sort_list(antag_datums), "(new custom antag)") as DmList, "(new custom antag)");
                            if (target == null || (target is datum target_datum && QDELETED(target_datum)))
                                return null;
                            else if (Dm.eq(target, "(new custom antag)"))
                                target_antag = await add_antag_datum(typeof(datum_antagonist_custom)) as datum_antagonist;
                            else
                                target_antag = target as datum_antagonist;
                            break;
                        }
                    }
                }
            }

            if (!Dm.truthy(GLOB.admin_objective_list))
                generate_admin_objective_list();

            if (old_objective != null)
            {
                if (Dm.@in(old_objective.name, GLOB.admin_objective_list))
                    def_value = old_objective.name;
            }

            // DEVIATION(DV340): BYOND's input() as null|anything in ... is tgui's list prompt
            object selected_type = await tgui_input_list(usr, "Select objective type:", "Objective type", GLOB.admin_objective_list, def_value);
            selected_type = GLOB.admin_objective_list[selected_type];
            if (!Dm.truthy(selected_type))
                return null;

            if (old_objective == null)
            {
                //Add new one
                new_objective = Dm.@new(selected_type as Type) as datum_objective;
                new_objective.owner = this;
                await new_objective.admin_edit(usr);
                Dm.append(ref target_antag.objectives, new_objective);
                message_admins(Dm.text("[] added a new objective for []: []", key_name_admin(usr), current, new_objective.explanation_text));
                log_admin(Dm.text("[] added a new objective for []: []", key_name(usr), current, new_objective.explanation_text));
            }
            else
            {
                if (Dm.eq(old_objective.type, selected_type))
                {
                    //Edit the old
                    await old_objective.admin_edit(usr);
                    new_objective = old_objective;
                }
                else
                {
                    //Replace the old
                    new_objective = Dm.@new(selected_type as Type) as datum_objective;
                    new_objective.owner = this;
                    await new_objective.admin_edit(usr);
                    Dm.remove(ref target_antag.objectives, old_objective);
                    target_antag.objectives.Insert(objective_pos, new_objective);
                }
                message_admins(Dm.text("[] edited []'s objective to []", key_name_admin(usr), current, new_objective.explanation_text));
                log_admin(Dm.text("[] edited []'s objective to []", key_name(usr), current, new_objective.explanation_text));
            }
        }

        else if (Dm.truthy(href_list["obj_delete"]))
        {
            datum_objective objective = null;
            object found = Dm.locate_ref(href_list["obj_delete"]);
            foreach (datum_antagonist A in Dm.each<datum_antagonist>(antag_datums))
            {
                objective = Dm.@in(found, A.objectives) ? found as datum_objective : null;
                if (objective != null)
                {
                    Dm.remove(ref A.objectives, objective);
                    break;
                }
            }
            if (objective == null)
            {
                to_chat(usr, "Invalid objective.");
                return null;
            }
            //qdel(objective) Needs cleaning objective destroys
            message_admins(Dm.text("[] removed an objective for []: []", key_name_admin(usr), current, objective.explanation_text));
            log_admin(Dm.text("[] removed an objective for []: []", key_name(usr), current, objective.explanation_text));
        }

        else if (Dm.truthy(href_list["obj_completed"]))
        {
            datum_objective objective = null;
            object found = Dm.locate_ref(href_list["obj_completed"]);
            foreach (datum_antagonist A in Dm.each<datum_antagonist>(antag_datums))
            {
                objective = Dm.@in(found, A.objectives) ? found as datum_objective : null;
                if (objective != null)
                    break;
            }
            if (objective == null)
            {
                to_chat(usr, "Invalid objective.");
                return null;
            }
            objective.completed = Dm.num(!Dm.truthy(objective.completed));
            log_admin(Dm.text("[] toggled the win state for []'s objective: []", key_name(usr), current, objective.explanation_text));
        }

        else if (Dm.truthy(href_list["obj_prompt_custom"]))
        {
            datum_antagonist target_antag = null;
            if (Dm.truthy(href_list["target_antag"]))
            {
                object found = Dm.locate_ref(href_list["target_antag"]);
                datum_antagonist found_datum = Dm.@in(found, antag_datums) ? found as datum_antagonist : null;
                if (found_datum != null)
                    target_antag = found_datum;
            }
            if (target_antag == null)
            {
                switch ((int)Dm.num(length(antag_datums)))
                {
                    case 0:
                        target_antag = await add_antag_datum(typeof(datum_antagonist_custom)) as datum_antagonist;
                        break;
                    case 1:
                        target_antag = antag_datums[1] as datum_antagonist;
                        break;
                    default:
                    {
                        // DEVIATION(DV340): BYOND's input() as null|anything in ... is tgui's list prompt
                        object target = await tgui_input_list(usr, "Which antagonist gets the objective:", "Antagonist", Dm.add(sort_list(antag_datums), "(new custom antag)") as DmList, "(new custom antag)");
                        if (target == null || (target is datum target_datum && QDELETED(target_datum)))
                            return null;
                        else if (Dm.eq(target, "(new custom antag)"))
                            target_antag = await add_antag_datum(typeof(datum_antagonist_custom)) as datum_antagonist;
                        else
                            target_antag = target as datum_antagonist;
                        break;
                    }
                }
            }
            // DEVIATION(DV340): BYOND's input() in list("Yes", "No") is tgui's list prompt
            object replace_existing = await tgui_input_list(usr, "Replace existing objectives?", "Replace objectives?", Dm.list("Yes", "No"));
            if (replace_existing == null)
                return null;
            bool replace_existing__1 = Dm.eq(replace_existing, "Yes");
            bool replace_escape;
            if (!replace_existing__1)
                replace_escape = false;
            else
            {
                object answer = await tgui_input_list(usr, "Replace survive/escape/martyr objectives?", "Replace objectives?", Dm.list("Yes", "No"));
                if (answer == null)
                    return null;
                replace_escape = Dm.eq(answer, "Yes");
            }
            await target_antag.submit_player_objective(retain_existing: Dm.num(!replace_existing__1), retain_escape: Dm.num(!replace_escape), force: TRUE);
            log_admin(Dm.text("[] prompted [] to enter their own objectives for [].", key_name(usr), current, target_antag));
        }

        // tg: code/datums/mind/_mind.dm:403  (silicons SI2)
        else if (Dm.truthy(href_list["silicon"]))
        {
            object sw__silicon = href_list["silicon"];
            if (Dm.eq(sw__silicon, "unemag"))
            {
                if (current is mob_living_silicon_robot R)
                {
                    await R.SetEmagged(0);
                    message_admins(Dm.text("[] has unemag'ed [].", key_name_admin(usr), R));
                    log_admin(Dm.text("[] has unemag'ed [].", key_name(usr), R));
                }
            }
            else if (Dm.eq(sw__silicon, "unemagcyborgs"))
            {
                if (isAI(current))
                {
                    mob_living_silicon_ai ai = (mob_living_silicon_ai)current;
                    foreach (mob_living_silicon_robot R in Dm.each<mob_living_silicon_robot>(ai.connected_robots))
                        await R.SetEmagged(0);
                    message_admins(Dm.text("[] has unemag'ed []'s Cyborgs.", key_name_admin(usr), ai));
                    log_admin(Dm.text("[] has unemag'ed []'s Cyborgs.", key_name(usr), ai));
                }
            }
        }

        else if (Dm.truthy(href_list["common"]))
        {
            switch (Dm.str(href_list["common"]))
            {
                case "undress":
                    foreach (obj_item W in Dm.each<obj_item>(current))
                        await current.dropItemToGround(W, TRUE); //The TRUE forces all items to drop, since this is an admin undress.
                    break;
                case "takeuplink":
                    take_uplink();
                    wipe_memory_type(typeof(datum_memory_key_traitor_uplink_implant));
                    log_admin(Dm.text("[] removed []'s uplink.", key_name(usr), current));
                    break;
                case "crystals":
                    if (Dm.truthy(check_rights(R_FUN)))
                    {
                        datum_component_uplink U = find_syndicate_uplink() as datum_component_uplink;
                        if (U != null)
                        {
                            object crystals = await tgui_input_number(
                                user: usr,
                                message: I18n.T("Amount of telecrystals for []", key),
                                title: I18n.T("Syndicate uplink"),
                                @default: U.uplink_handler.telecrystals);
                            if (isnum(crystals))
                            {
                                U.uplink_handler.set_telecrystals(crystals);
                                message_admins(Dm.text("[] changed []'s telecrystal count to [].", key_name_admin(usr), current, crystals));
                                log_admin(Dm.text("[] changed []'s telecrystal count to [].", key_name(usr), current, crystals));
                            }
                        }
                    }
                    break;
                case "uplink":
                {
                    datum_antagonist_traitor traitor_datum = has_antag_datum(typeof(datum_antagonist_traitor)) as datum_antagonist_traitor;
                    if (!Dm.truthy(await give_uplink(antag_datum: traitor_datum)))
                    {
                        to_chat(usr, span_danger(I18n.T("Equipping a syndicate failed!")));
                        log_admin(Dm.text("[] tried and failed to give [] an uplink.", key_name(usr), current));
                    }
                    else
                        log_admin(Dm.text("[] gave [] an uplink.", key_name(usr), current));
                    break;
                }
            }
        }

        else if (Dm.truthy(href_list["obj_announce"]))
            await announce_objectives();

        //Something in here might have changed your mind
        if (self_antagging && (usr == null || usr.client == null) && current.client != null)
            usr = current;
        await traitor_panel();
        return null;
    }
}

// Gaps RG2 (D339): the mind's addiction points.
// tg: code/datums/mind/_mind.dm:34
public partial class datum_mind : datum
{
    ///Adds addiction points to the specified addiction
    // tg: code/datums/mind/_mind.dm:475
    public virtual object add_addiction_points(object type = null, object amount = null)
    {
        object last_amount = LAZYACCESS(addiction_points, type);
        if (!Dm.truthy(last_amount))
            last_amount = 0;
        LAZYSET(ref addiction_points, type, min(Dm.num(LAZYACCESS(addiction_points, type)) + Dm.num(amount), MAX_ADDICTION_POINTS));
        object new_amount = LAZYACCESS(addiction_points, type);
        ((datum_addiction)GLOB.addictions[type]).on_gain_addiction_points(this, Dm.num_or_null(new_amount), Dm.num_or_null(last_amount));
        return null;
    }

    ///Adds addiction points to the specified addiction
    // tg: code/datums/mind/_mind.dm:482
    public virtual object remove_addiction_points(object type = null, object amount = null)
    {
        object last_amount = LAZYACCESS(addiction_points, type);
        if (!Dm.truthy(last_amount))
            last_amount = 0;
        LAZYSET(ref addiction_points, type, max(Dm.num(LAZYACCESS(addiction_points, type)) - Dm.num(amount), 0));
        object new_amount = LAZYACCESS(addiction_points, type);
        if (Dm.num(new_amount) <= 0)
            LAZYREMOVE(ref addiction_points, type);
        return ((datum_addiction)GLOB.addictions[type]).on_lose_addiction_points(this, Dm.num_or_null(new_amount), Dm.num_or_null(last_amount));
    }
}
