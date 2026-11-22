// Translated from tgstation (snapshot 1d99bd7f): code/datums/mind/antag.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate packages A1 and A2, D316): the whole file but make_wizard (the wizard, no package yet; give_uplink is A2's); the
// revolutionary's deconversion in remove_antags_for_borging and conversion in enslave_mind_to_creator came with gaps V1.

// Datum antag mind procs
// tg: code/datums/mind/antag.dm:2
public partial class datum_mind : datum
{
    // tg: code/datums/mind/antag.dm:2
    public virtual async DmTask<object> add_antag_datum(object datum_type_or_instance = null, object team = null)
    {
        if (!Dm.truthy(datum_type_or_instance))
            return null;
        datum_antagonist A;
        if (!ispath(datum_type_or_instance))
        {
            A = datum_type_or_instance as datum_antagonist;
            if (A == null)
                return null;
        }
        else
            A = Dm.@new(datum_type_or_instance as Type) as datum_antagonist;
        //Choose snowflake variation if antagonist handles it
        datum_antagonist S = A.specialization(this) as datum_antagonist;
        if (S != null && S != A)
        {
            qdel(A);
            A = S;
        }
        if (!Dm.truthy(A.can_be_owned(this)))
        {
            qdel(A);
            return null;
        }
        A.owner = this;
        LAZYADD(ref antag_datums, A);
        await A.create_team(team as datum_team);
        datum_team antag_team = A.get_team() as datum_team;
        if (antag_team != null)
            await antag_team.add_member(this);
        Waiting.Detach(A.on_gain()); // INVOKE_ASYNC(A, TYPE_PROC_REF(/datum/antagonist, on_gain))
        log_game(Dm.text("[] has gained antag datum []([]).", key_name(this), A.name, A.type));
        return A;
    }

    // tg: code/datums/mind/antag.dm:30
    public virtual async DmTask<object> remove_antag_datum(object datum_type = null)
    {
        if (!Dm.truthy(datum_type))
            return null;
        datum_antagonist antag = has_antag_datum(datum_type) as datum_antagonist;
        if (antag == null)
            return null;

        await antag.on_removal();
        qdel(antag);
        current?.log_message(Dm.text("has lost antag datum [] ([]).", antag, antag.type), LOG_GAME);
        return TRUE;
    }

    // tg: code/datums/mind/antag.dm:42
    public virtual async DmTask remove_all_antag_datums() //For the Lazy amongst us.
    {
        foreach (object a in Dm.each(antag_datums?.Copy()))
        {
            datum_antagonist antag = (datum_antagonist)a;
            await antag.on_removal();
            qdel(antag);
        }

        current?.log_message("has lost all antag datums.", LOG_GAME);
    }

    // tg: code/datums/mind/antag.dm:49
    public virtual object has_antag_datum(object datum_type = null, float? check_subtypes = null)
    {
        check_subtypes ??= TRUE;
        if (!Dm.truthy(datum_type))
            return null;
        foreach (object a in Dm.each(antag_datums))
        {
            datum_antagonist A = a as datum_antagonist;
            if (Dm.truthy(check_subtypes) && istype(A, datum_type as Type))
                return A;
            else if (Dm.eq(A.type, datum_type))
                return A;
        }
        return null;
    }

    /// Returns true if mind has any antag datum from a list of types
    // tg: code/datums/mind/antag.dm:60
    public virtual object has_antag_datum_in_list(DmList antag_types = null)
    {
        foreach (object antag_datum in Dm.each(antag_datums))
        {
            datum_antagonist check_datum = antag_datum as datum_antagonist;
            if (Dm.truthy(is_type_in_list(check_datum, antag_types)))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/datums/mind/antag.dm:67
    public virtual void remove_antag_equip()
    {
        if (current == null)
            return;
        DmList Mob_Contents = current.get_contents() as DmList;
        foreach (obj_item I in Dm.each<obj_item>(Mob_Contents))
        {
            datum_component_uplink O = I.GetComponent(typeof(datum_component_uplink)) as datum_component_uplink; //Todo make this reset signal
            if (O != null)
                O.unlock_code = null;
        }
    }

    /// Remove the antagonists that should not persist when being borged
    // tg: code/datums/mind/antag.dm:77
    public virtual async DmTask remove_antags_for_borging()
    {
        await remove_antag_datum(typeof(datum_antagonist_cult));

        datum_antagonist_rev revolutionary = has_antag_datum(typeof(datum_antagonist_rev)) as datum_antagonist_rev;
        if (revolutionary != null) // gaps V1
            await revolutionary.remove_revolutionary();
    }

    // tg: code/datums/mind/antag.dm:226
    public virtual object find_syndicate_uplink(object check_unlocked = null)
    {
        DmList L = current.get_all_contents() as DmList;
        foreach (object i in Dm.each(L))
        {
            atom_movable I = i as atom_movable;
            datum_component_uplink found_uplink = I.GetComponent(typeof(datum_component_uplink)) as datum_component_uplink;
            if (found_uplink == null || (Dm.truthy(check_unlocked) && Dm.truthy(found_uplink.locked)))
                continue;
            return found_uplink;
        }
        return null;
    }

    /**
    * Checks to see if the mind has an accessible uplink (their own, if they are a traitor; any unlocked uplink otherwise),
    * and gives them a fallback spell if no uplink was found
    */
    // tg: code/datums/mind/antag.dm:239
    public virtual async DmTask try_give_equipment_fallback()
    {
        object uplink_exists = null;
        datum_antagonist_traitor traitor_datum = has_antag_datum(typeof(datum_antagonist_traitor)) as datum_antagonist_traitor;
        if (traitor_datum != null)
            uplink_exists = traitor_datum.uplink_ref;
        if (!Dm.truthy(uplink_exists))
            uplink_exists = find_syndicate_uplink(check_unlocked: TRUE);
        if (!Dm.truthy(uplink_exists) && Dm.locate<datum_action_special_equipment_fallback>(current.actions) == null)
        {
            datum_action_special_equipment_fallback fallback = Dm.@new<datum_action_special_equipment_fallback>(this);
            await fallback.Grant(current);
        }
    }

    // tg: code/datums/mind/antag.dm:250
    public virtual void take_uplink()
    {
        qdel(find_syndicate_uplink());
    }

    /**
     * ## give_uplink
     *
     * A mind proc for giving anyone an uplink.
     * arguments:
     * * silent: if this should send a message to the mind getting the uplink. traitors do not use this silence, but the silence var on their antag datum.
     * * antag_datum: the antag datum of the uplink owner, for storing it in antag memory. optional!
     */
    // tg: code/datums/mind/antag.dm:117
    public virtual async DmTask<object> give_uplink(float? silent = null, datum_antagonist antag_datum = null)
    {
        silent ??= FALSE;
        if (!isliving(current))
            return null;
        mob_living_carbon_human traitor_mob = current as mob_living_carbon_human;
        if (traitor_mob == null)
        {
            datum_status_effect_shapechange_mob shapeshift = (current as mob_living).has_status_effect(typeof(datum_status_effect_shapechange_mob)) as datum_status_effect_shapechange_mob;
            if (!ishuman(shapeshift?.caster_mob))
                return null;
            traitor_mob = shapeshift.caster_mob as mob_living_carbon_human;
        }

        obj_item uplink_loc = null;
        object uplink_spawn_location = current.client?.prefs?.read_preference(typeof(datum_preference_choiced_uplink_location));
        bool cant_speak = HAS_TRAIT(traitor_mob, TRAIT_MUTE) || is_mime_job(assigned_role);
        if (Dm.eq(uplink_spawn_location, UPLINK_RADIO) && cant_speak)
        {
            if (!Dm.truthy(silent))
                to_chat(current, span_warning(I18n.T("You have been deemed ineligible for a radio uplink. Supplying standard uplink instead.")));
            uplink_spawn_location = UPLINK_PDA;
        }

        if (!Dm.eq(uplink_spawn_location, UPLINK_IMPLANT))
        {
            uplink_loc = traitor_mob.get_uplink_location(uplink_spawn_location as string) as obj_item;
            if (uplink_loc is obj_item_radio && cant_speak)
                uplink_loc = null;
        }

        if (uplink_loc == null)
        {
            obj_item_implant_uplink_starting new_implant = await Dm.new_async<obj_item_implant_uplink_starting>(traitor_mob);
            await new_implant.implant(traitor_mob, null, silent: TRUE);
            if (!Dm.truthy(silent))
                to_chat(current, span_boldnotice(I18n.T("Your Syndicate Uplink has been cunningly implanted in you, for a small TC fee. Simply trigger the uplink to access it.")));
            add_memory(typeof(datum_memory_key_traitor_uplink_implant), Dm.named("uplink_loc", "implant"));
            return new_implant;
        }

        object dot = uplink_loc;
        string unlock_text;
        datum_component_uplink new_uplink = await uplink_loc.AddComponent(typeof(datum_component_uplink), traitor_mob.key) as datum_component_uplink;
        if (new_uplink == null)
            throw Dm.CRASH("Uplink creation failed.", "code/datums/mind/antag.dm", 152);
        new_uplink.setup_unlock_code();
        new_uplink.uplink_handler.owner = this;
        new_uplink.uplink_handler.assigned_role = (traitor_mob.mind.assigned_role as datum_job)?.title;
        new_uplink.uplink_handler.assigned_species = (traitor_mob.dna.species as datum_species)?.id;

        unlock_text = I18n.T("Your Uplink is cunningly disguised as your []. ", uplink_loc.name);
        if (uplink_loc is obj_item_modular_computer_pda)
        {
            unlock_text += I18n.T("Simply enter the code \"[]\" into the ring tone selection to unlock its hidden features.", new_uplink.unlock_code);
            add_memory(typeof(datum_memory_key_traitor_uplink), Dm.named("uplink_loc", "PDA"), Dm.named("uplink_code", new_uplink.unlock_code));
        }

        else if (uplink_loc is obj_item_radio)
        {
            unlock_text += I18n.T("Simply speak \"[]\" into frequency [] to unlock its hidden features.", new_uplink.unlock_code, RADIO_TOKEN_UPLINK);
            add_memory(typeof(datum_memory_key_traitor_uplink), Dm.named("uplink_loc", uplink_loc.name), Dm.named("uplink_code", new_uplink.unlock_code));
        }

        else if (uplink_loc is obj_item_pen)
        {
            object instructions = english_list(new_uplink.unlock_code as DmList);
            unlock_text += I18n.T("Simply twist the top of the pen [] from its starting position to unlock its hidden features.", instructions);
            add_memory(typeof(datum_memory_key_traitor_uplink), Dm.named("uplink_loc", uplink_loc.name), Dm.named("uplink_code", instructions));
        }

        new_uplink.unlock_text = unlock_text;
        if (!Dm.truthy(silent))
            to_chat(current, span_boldnotice(unlock_text));
        if (antag_datum != null)
            antag_datum.antag_memory += Dm.str(new_uplink.unlock_note) + "<br>";
        return dot;
    }

    /// Link a new mobs mind to the creator of said mob. They will join any team they are currently on, and will only switch teams when their creator does.
    // tg: code/datums/mind/antag.dm:180
    public virtual async DmTask enslave_mind_to_creator(mob_living creator = null)
    {
        // IS_CULTIST(creator), IS_REVOLUTIONARY(creator), IS_NUKE_OP(creator) (__DEFINES/antagonists.dm:280, :323, :312), expanded inline
        if (Dm.truthy(creator?.mind?.has_antag_datum(typeof(datum_antagonist_cult))) || HAS_TRAIT(creator, TRAIT_ACT_AS_CULTIST))
            await add_antag_datum(typeof(datum_antagonist_cult));

        else if (Dm.truthy(creator?.mind?.has_antag_datum(typeof(datum_antagonist_rev))))
        {
            var converter = creator.mind.has_antag_datum(typeof(datum_antagonist_rev), TRUE) as datum_antagonist_rev; // gaps V1
            await converter.add_revolutionary(this, stun: FALSE, mute: FALSE);
        }

        else if (Dm.truthy(creator?.mind?.has_antag_datum(typeof(datum_antagonist_nukeop))))
        {
            datum_antagonist_nukeop converter = creator.mind.has_antag_datum(typeof(datum_antagonist_nukeop), TRUE) as datum_antagonist_nukeop;
            datum_antagonist_nukeop N = Dm.@new<datum_antagonist_nukeop>();
            N.send_to_spawnpoint = FALSE;
            N.give_bonus_tc = FALSE;
            N.nukeop_outfit = null;
            await add_antag_datum(N, converter.nuke_team);
        }

        enslaved_to = WEAKREF(creator) as datum_weakref;

        if (Dm.truthy(current._listen_lookup?[COMSIG_MOB_ENSLAVED_TO]))
            current._SendSignal(COMSIG_MOB_ENSLAVED_TO, Dm.list(current, creator));

        current.add_faction(creator.get_faction());
        current.add_ally(current);

        current.log_message(Dm.text("has been enslaved to [].", key_name(creator)), LOG_GAME);
        log_admin(Dm.text("[] has been enslaved to [].", key_name(current), key_name(creator)));
        log_admin(Dm.text("[] has been enslaved to [].", key_name(current), key_name(creator)));

        if (Dm.truthy(creator.is_antag()))
        {
            message_admins(Dm.text("[] has been created by [], an antagonist.", ADMIN_LOOKUPFLW(current), ADMIN_LOOKUPFLW(creator)));
            to_chat(current, span_userdanger(Dm.text("Despite your creator's current allegiances, your true master remains []. If their loyalties change, so do yours. This will never change unless your creator's body is destroyed.", creator.real_name)));
        }
    }

    // tg: code/datums/mind/antag.dm:210
    public virtual object get_all_objectives()
    {
        DmList all_objectives = Dm.list();
        foreach (datum_antagonist A in Dm.each<datum_antagonist>(antag_datums))
            Dm.combine(ref all_objectives, A.objectives);
        return all_objectives;
    }

    // tg: code/datums/mind/antag.dm:216
    public virtual async DmTask announce_objectives()
    {
        float obj_count = 1;
        to_chat(current, span_notice("Your current objectives:"));
        foreach (object o in Dm.each(get_all_objectives()))
        {
            datum_objective objective = (datum_objective)o;
            to_chat(current, Dm.text("<B>[] #[]</B>: []", objective.objective_name, obj_count, objective.explanation_text));
            obj_count++;
        }
        // Objectives are often stored in the static data of antag uis, so we should update those as well
        foreach (object antag in Dm.each(antag_datums))
            await ((datum_antagonist)antag).update_static_data(current);
    }

    /// Sets our can_hijack to the fastest speed our antag datums allow.
    // tg: code/datums/mind/antag.dm:260
    public virtual object get_hijack_speed()
    {
        object dot = 0;
        foreach (datum_antagonist A in Dm.each<datum_antagonist>(antag_datums))
            dot = max(Dm.num(dot), Dm.num(A.hijack_speed__proc()));
        return dot;
    }

    // tg: code/datums/mind/antag.dm:265
    public virtual object has_objective(object objective_type = null)
    {
        foreach (datum_antagonist A in Dm.each<datum_antagonist>(antag_datums))
        {
            foreach (object O in Dm.each(A.objectives))
            {
                if (istype(O, objective_type as Type))
                    return TRUE;
            }
        }
        return null;
    }
}

// tg: code/datums/mind/antag.dm:91
public partial class mob_living_carbon : mob_living
{
    /**
     * Gets an item that can be used as an uplink somewhere on the mob's person.
     *
     * * desired_location: the location to look for the uplink in. An UPLINK_ define.
     * If the desired location is not found, defaults to another location.
     *
     * Returns the item found, or null if no item was found.
     */
    // tg: code/datums/mind/antag.dm:91
    public virtual object get_uplink_location(string desired_location = null)
    {
        desired_location ??= UPLINK_PDA;
        DmList all_contents = get_all_contents() as DmList;
        obj_item_modular_computer_pda my_pda = Dm.locate<obj_item_modular_computer_pda>(all_contents);
        obj_item_radio my_radio = Dm.locate<obj_item_radio>(all_contents);
        obj_item_pen my_pen = Dm.locate<obj_item_pen>(my_pda) ?? Dm.locate<obj_item_pen>(all_contents);

        switch (desired_location)
        {
            case UPLINK_PDA:
                return (object)my_pda ?? (object)my_radio ?? my_pen;

            case UPLINK_RADIO:
                return (object)my_radio ?? (object)my_pda ?? my_pen;

            case UPLINK_PEN:
                return (object)my_pen ?? (object)my_pda ?? my_radio;
        }

        return null;
    }
}
