// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/traitor/datum_traitor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the whole file (the traitor: the uplink and its sales, the objectives with the job's and the ending one,
// the employer and its flavor, the code words heard highlighted, the AntagInfoTraitor window, the round-end report with the
// telecrystals spent and the purchases, the preview outfit). The report's badass icon (icon2html) is a deferred leaf with the chat
// icons; the contractor's report reads X1's hub (null until the contract kit). The var blocks are generated.

// tg: code/modules/antagonists/traitor/datum_traitor.dm:2-4  #define FLAVOR_FACTION_SYNDICATE "syndicate", FLAVOR_FACTION_NANOTRASEN
// "nanotrasen" (file-local, #undef at :347)

// tg: code/modules/antagonists/traitor/datum_traitor.dm:6
public partial class datum_antagonist_traitor : datum_antagonist
{
    private const string FLAVOR_FACTION_SYNDICATE = "syndicate";
    private const string FLAVOR_FACTION_NANOTRASEN = "nanotrasen";

    // tg: code/modules/antagonists/traitor/datum_traitor.dm:50
    [DmParams("give_objectives")]
    public override async DmTask New(params object[] __extra)
    {
        object give_objectives = Dm.arg(__extra, 0) ?? TRUE;
        await base.New(__extra);
        this.give_objectives = Dm.num(give_objectives);
    }

    // tg: code/modules/antagonists/traitor/datum_traitor.dm:54
    public override async DmTask on_gain()
    {
        if (Dm.truthy(give_uplink))
            await owner.give_uplink(silent: TRUE, antag_datum: this);

        datum_component_uplink uplink = owner.find_syndicate_uplink() as datum_component_uplink;
        uplink_ref = WEAKREF(uplink) as datum_weakref;
        if (uplink != null)
        {
            if (uplink_handler != null)
                uplink.uplink_handler = uplink_handler;
            else
                uplink_handler = uplink.uplink_handler;
            uplink_handler.uplink_flag = uplink_flag_given;
            uplink_handler.primary_objectives = objectives;

            uplink_handler.can_replace_objectives = Dm.@new<datum_callback>(this, nameof(can_change_objectives));
            uplink_handler.replace_objectives = Dm.@new<datum_callback>(this, nameof(submit_player_objective));

            DmList uplink_items = Dm.list();
            foreach (datum_uplink_item item in Dm.each<datum_uplink_item>(SStraitor.uplink_items))
            {
                // DEVIATION(DV343): no sale of an entry the uplink hides (its item, procs or system are not live)
                if (uplink_item_hidden(item))
                    continue;
                if (item.item != null && !Dm.truthy(item.cant_discount) && Dm.truthy(Dm.bitand(item.purchasable_from, uplink_handler.uplink_flag)) && item.cost >= TRAITOR_DISCOUNT_MIN_PRICE)
                {
                    if (!Dm.truthy(length(item.restricted_roles)) && !Dm.truthy(length(item.restricted_species)))
                    {
                        Dm.append(uplink_items, item);
                        continue;
                    }
                    if (Dm.@in(uplink_handler.assigned_role, item.restricted_roles) || Dm.@in(uplink_handler.assigned_species, item.restricted_species))
                    {
                        Dm.append(uplink_items, item);
                        continue;
                    }
                }
            }
            Dm.append(uplink_handler.extra_purchasable, create_uplink_sales(rand(uplink_sales_min, uplink_sales_max), typeof(datum_uplink_category_discounts), -1, uplink_items));
        }

        if (Dm.truthy(give_objectives))
        {
            await forge_traitor_objectives();
            forge_ending_objective();
        }

        pick_employer();

        await base.on_gain();
    }

    // tg: code/modules/antagonists/traitor/datum_traitor.dm:90
    public override async DmTask on_removal()
    {
        if (uplink_handler != null)
        {
            uplink_handler.can_replace_objectives = null;
            uplink_handler.replace_objectives = null;
        }
        owner.take_uplink();
        await base.on_removal();
    }

    /// Returns true if we're allowed to assign ourselves a new objective
    // tg: code/modules/antagonists/traitor/datum_traitor.dm:98
    public virtual object can_change_objectives()
    {
        return can_assign_self_objectives;
    }

    // tg: code/modules/antagonists/traitor/datum_traitor.dm:101
    public virtual void pick_employer()
    {
        if (!Dm.truthy(employer))
        {
            string faction = prob(75) ? FLAVOR_FACTION_SYNDICATE : FLAVOR_FACTION_NANOTRASEN;
            DmList possible_employers = Dm.list();

            possible_employers.Add(GLOB.syndicate_employers, GLOB.nanotrasen_employers);

            if (ending_objective is datum_objective_hijack)
                Dm.remove(possible_employers, GLOB.normal_employers);
            else //escape or martyrdom
                Dm.remove(possible_employers, GLOB.hijack_employers);

            switch (faction)
            {
                case FLAVOR_FACTION_SYNDICATE:
                    Dm.remove(possible_employers, GLOB.nanotrasen_employers);
                    break;
                case FLAVOR_FACTION_NANOTRASEN:
                    Dm.remove(possible_employers, GLOB.syndicate_employers);
                    break;
            }
            employer = pick(possible_employers);
        }
        traitor_flavor = strings(TRAITOR_FLAVOR_FILE, employer) as DmList;
    }

    /// Generates a complete set of traitor objectives up to the traitor objective limit, including non-generic objectives such as martyr and hijack.
    // tg: code/modules/antagonists/traitor/datum_traitor.dm:124
    public virtual async DmTask forge_traitor_objectives()
    {
        float objective_count = 0;

        if (Dm.num(GLOB.joined_player_list.len) >= HIJACK_MIN_PLAYERS && prob(HIJACK_PROB))
        {
            is_hijacker = TRUE;
            objective_count++;
        }

        float objective_limit = Dm.num(config.Get(typeof(datum_config_entry_number_traitor_objectives_amount)));
        datum_objective job_objective = forge_job_objective() as datum_objective;
        // for(in...to) loops iterate inclusively, so to reach objective_limit we need to loop to objective_limit - 1
        // This does not give them 1 fewer objectives than intended.
        for (float i = objective_count, i__end = objective_limit - 1; i <= i__end; i++)
        {
            object generated = await forge_single_generic_objective(job_objective);
            if (generated == job_objective)
                job_objective = null;
            Dm.append(objectives, generated);
        }
    }

    /**
     * ## forge_ending_objective
     *
     * Forges the endgame objective and adds it to this datum's objective list.
     */
    // tg: code/modules/antagonists/traitor/datum_traitor.dm:146
    public virtual void forge_ending_objective()
    {
        if (Dm.truthy(is_hijacker))
        {
            ending_objective = Dm.@new<datum_objective_hijack>();
            ending_objective.owner = owner;
            return;
        }

        bool martyr_compatibility = true;

        foreach (datum_objective traitor_objective in Dm.each<datum_objective>(objectives))
        {
            if (!Dm.truthy(traitor_objective.martyr_compatible))
            {
                martyr_compatibility = false;
                break;
            }
        }

        if (martyr_compatibility && prob(MARTYR_PROB))
        {
            ending_objective = Dm.@new<datum_objective_martyr>();
            ending_objective.owner = owner;
            Dm.append(objectives, ending_objective);
            return;
        }

        ending_objective = Dm.@new<datum_objective_escape>();
        ending_objective.owner = owner;
        Dm.append(objectives, ending_objective);
    }

    // tg: code/modules/antagonists/traitor/datum_traitor.dm:170
    public virtual async DmTask<object> forge_single_generic_objective(object job_objective = null)
    {
        if (prob(JOB_PROB) && Dm.truthy(job_objective))
            return job_objective;

        if (prob(KILL_PROB))
        {
            DmList active_ais = GlobalProcs.active_ais(skip_syndicate: TRUE) as DmList;
            if (Dm.truthy(active_ais.len) && prob(DESTROY_AI_PROB(GLOB.joined_player_list.len)))
            {
                datum_objective_destroy destroy_objective = Dm.@new<datum_objective_destroy>();
                destroy_objective.owner = owner;
                await destroy_objective.find_target();
                return destroy_objective;
            }

            if (prob(MAROON_PROB))
            {
                datum_objective_maroon maroon_objective = Dm.@new<datum_objective_maroon>();
                maroon_objective.owner = owner;
                await maroon_objective.find_target();
                return maroon_objective;
            }

            datum_objective_assassinate kill_objective = Dm.@new<datum_objective_assassinate>();
            kill_objective.owner = owner;
            await kill_objective.find_target();
            return kill_objective;
        }

        datum_objective_steal steal_objective = Dm.@new<datum_objective_steal>();
        steal_objective.owner = owner;
        await steal_objective.find_target();
        return steal_objective;
    }

    // tg: code/modules/antagonists/traitor/datum_traitor.dm:195
    public virtual object forge_job_objective()
    {
        datum_objective job_objective = (owner.assigned_role as datum_job)?.generate_traitor_objective() as datum_objective; // can return null
        if (job_objective != null)
            job_objective.owner = owner;
        return job_objective;
    }

    // tg: code/modules/antagonists/traitor/datum_traitor.dm:200
    public override async DmTask<object> apply_innate_effects(mob_living mob_override = null)
    {
        object dot = await base.apply_innate_effects(mob_override);
        mob_living datum_owner = mob_override ?? owner.current as mob_living;

        await handle_clown_mutation(datum_owner, mob_override != null ? null : I18n.T("Your training has allowed you to overcome your clownish nature, allowing you to wield weapons without harming yourself."));
        if (Dm.truthy(should_give_codewords))
        {
            await datum_owner.AddComponent(typeof(datum_component_codeword_hearing), SStraitor.syndicate_code_phrase_regex, "blue", this);
            await datum_owner.AddComponent(typeof(datum_component_codeword_hearing), SStraitor.syndicate_code_response_regex, "red", this);
        }
        return dot;
    }

    // tg: code/modules/antagonists/traitor/datum_traitor.dm:209
    public override async DmTask remove_innate_effects(mob_living mob_override = null)
    {
        mob_living datum_owner = mob_override ?? owner.current as mob_living;
        await handle_clown_mutation(datum_owner, removing: FALSE);

        foreach (datum_component_codeword_hearing component in Dm.each_cast<datum_component_codeword_hearing>(datum_owner.GetComponents(typeof(datum_component_codeword_hearing))))
            component.delete_if_from_source(this);
    }

    // tg: code/modules/antagonists/traitor/datum_traitor.dm:217
    public override async DmTask<object> submit_player_objective(float? retain_existing = null, float? retain_escape = null, float? force = null)
    {
        object dot = await base.submit_player_objective(retain_existing, retain_escape, force);
        if (!Dm.truthy(dot))
            return dot;
        owner.current.playsound_local(get_turf(owner.current), Dm.resource("sound/music/antag/traitor/final_objective.ogg"), 100, FALSE, pressure_affected: FALSE, use_reverb: FALSE);
        return dot;
    }

    // tg: code/modules/antagonists/traitor/datum_traitor.dm:223
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        datum_component_uplink uplink = uplink_ref?.resolve() as datum_component_uplink;
        DmList data = Dm.list();
        data["has_codewords"] = should_give_codewords;
        if (Dm.truthy(should_give_codewords))
        {
            data["phrases"] = jointext(SStraitor.syndicate_code_phrase, ", ");
            data["responses"] = jointext(SStraitor.syndicate_code_response, ", ");
        }
        data["theme"] = traitor_flavor?["ui_theme"];
        data["code"] = uplink?.unlock_code;
        data["failsafe_code"] = uplink?.failsafe_code;
        data["intro"] = traitor_flavor?["introduction"];
        data["allies"] = traitor_flavor?["allies"];
        data["goal"] = traitor_flavor?["goal"];
        data["has_uplink"] = uplink != null ? TRUE : FALSE;
        data["given_uplink"] = give_uplink;
        if (uplink != null)
        {
            data["uplink_intro"] = traitor_flavor?["uplink"];
            data["uplink_unlock_info"] = uplink.unlock_text;
        }
        data["objectives"] = get_objectives();
        return data;
    }

    // tg: code/modules/antagonists/traitor/datum_traitor.dm:246
    public override object roundend_report()
    {
        DmList result = Dm.list();

        bool traitor_won = true;

        Dm.append(result, printplayer(owner));

        object used_telecrystals = 0;
        bool uplink_owned = false;
        string purchases = "";

        LAZYINITLIST(ref GLOB.uplink_purchase_logs_by_key);
        // Uplinks add an entry to uplink_purchase_logs_by_key on init.
        datum_uplink_purchase_log purchase_log = GLOB.uplink_purchase_logs_by_key[owner.key] as datum_uplink_purchase_log;
        if (purchase_log != null)
        {
            used_telecrystals = purchase_log.total_spent;
            uplink_owned = true;
            purchases += Dm.str(purchase_log.generate_render(FALSE));
        }

        string objectives_text = "";
        if (Dm.truthy(objectives.len)) //If the traitor had no objectives, don't need to process this.
        {
            int count = 1;
            foreach (datum_objective objective in Dm.each<datum_objective>(objectives))
            {
                if (!Dm.truthy(objective.check_completion()))
                    traitor_won = false;
                objectives_text += Dm.text("<br><B>Objective #[]</B>: [] []", count, objective.explanation_text, objective.get_roundend_success_suffix());
                count++;
            }
        }

        Dm.append(result, Dm.text("<br>[] <B>[]</B>", owner.name, traitor_flavor?["roundend_report"]));

        if (uplink_owned)
        {
            string uplink_text = Dm.text("(used [] TC) []", used_telecrystals, purchases);
            if (Dm.eq(used_telecrystals, 0) && traitor_won)
            {
                // var/static/icon/badass = icon('icons/ui/antags/badass.dmi', "badass"); uplink_text += "<BIG>[icon2html(badass, world)]</BIG>":
                // the badass picture in the report (icon2html, the chat icons)
                Dm.Deferred("code/modules/antagonists/traitor/datum_traitor.dm:277", "roundend_report: icon2html of the badass icon, a traitor who won with no TC spent (the chat icons)");
            }
            Dm.append(result, uplink_text);
        }

        Dm.append(result, objectives_text);

        if (uplink_handler != null && uplink_handler.contractor_hub != null)
            Dm.append(result, contractor_round_end());

        object special_role_text = LOWER_TEXT(name);

        if (traitor_won)
            Dm.append(result, span_greentext(Dm.text("The [] was successful!", special_role_text)));
        else
        {
            Dm.append(result, span_redtext(Dm.text("The [] has failed!", special_role_text)));
            Dm.output_to(owner.current, Dm.resource("sound/ambience/misc/ambifailure.ogg")); // SEND_SOUND (__DEFINES/sound.dm: DIRECT_OUTPUT, rules 12)
        }

        return result.Join("<br>");
    }

    ///Tells how many contracts have been completed.
    // tg: code/modules/antagonists/traitor/datum_traitor.dm:296
    public virtual object contractor_round_end()
    {
        float completed_contracts = uplink_handler.contractor_hub.contracts_completed;
        float tc_total = uplink_handler.contractor_hub.contract_TC_payed_out + uplink_handler.contractor_hub.contract_TC_to_redeem;

        datum_antagonist_traitor_contractor_support contractor_support_unit = uplink_handler.contractor_hub.contractor_teammate as datum_antagonist_traitor_contractor_support;

        if (completed_contracts <= 0)
            return null;
        string plural_check = "contract";
        if (completed_contracts > 1)
            plural_check = "contracts";
        string sent_data = Dm.text("Completed [] [] for a total of []!<br>", span_greentext(Dm.text("[]", completed_contracts)), plural_check, span_greentext(Dm.text("[] TC", tc_total)));
        if (contractor_support_unit != null)
            sent_data += Dm.text("<b>[]</b> played <b>[]</b>, their contractor support unit.<br>", contractor_support_unit.owner.key, contractor_support_unit.owner.current.name);
        return sent_data;
    }

    // tg: code/modules/antagonists/traitor/datum_traitor.dm:313
    public override object roundend_report_footer()
    {
        string phrases = jointext(SStraitor.syndicate_code_phrase, ", ");
        string responses = jointext(SStraitor.syndicate_code_response, ", ");

        string message = Dm.text("<br><b>The code phrases were:</b> <span class='bluetext'>[]</span><br><b>The code responses were:</b> []<br>",
            phrases, span_redtext(Dm.text("[]", responses)));

        return message;
    }

    // tg: code/modules/antagonists/traitor/datum_traitor.dm:348
    public override async DmTask<object> on_respawn(mob new_character = null)
    {
        await SSjob.equip_rank(new_character as mob_living, new_character.mind.assigned_role as datum_job, new_character.client);
        await new_character.mind.give_uplink(silent: TRUE, antag_datum: this);
        return TRUE;
    }
}

// tg: code/modules/antagonists/traitor/datum_traitor.dm:322
public partial class datum_outfit_traitor : datum_outfit
{
    // tg: code/modules/antagonists/traitor/datum_traitor.dm:333
    [DmParams("H", "visuals_only")]
    public override async DmTask post_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        mob_living_carbon_human H = user;
        obj_item_melee_energy_sword sword = Dm.locate<obj_item_melee_energy_sword>(H.held_items);
        if (Dm.truthy(Dm.bitand(sword.flags_1, INITIALIZED_1)))
            await sword.attack_self();
        else //Atoms aren't initialized during the screenshots unit test, so we can't call attack_self for it as the sword doesn't have the transforming weapon component to handle the icon changes. The below part is ONLY for the antag screenshots unit test.
        {
            sword.icon_state = "e_sword_on_red";
            sword.inhand_icon_state = "e_sword_on_red";
            sword.worn_icon_state = "e_sword_on_red";

            await H.update_held_items();
        }
    }
}
