// Translated from tgstation (snapshot 1d99bd7f): code/modules/lost_crew/lost_crew_manager.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: the recovered crew). The var blocks and the AAS entry are generated (D111). The damage classes leave
// out what is not ported yet (DV561, damages/_damages.cs).

// tg: code/modules/lost_crew/lost_crew_manager.dm:2 (interleaved types, rules 1.9)
public partial class datum_controller_global_vars
{
    /// Manager for the lost crew bodies, for spawning and granting rewards
    // tg: code/modules/lost_crew/lost_crew_manager.dm:2  GLOBAL_DATUM_INIT(lost_crew_manager, /datum/lost_crew_manager, new)
    public datum_lost_crew_manager lost_crew_manager;

    // tg: code/modules/lost_crew/lost_crew_manager.dm:2  (GLOBAL_MANAGED(lost_crew_manager, new), __DEFINES/__globals.dm:5)
    public virtual void InitGloballost_crew_manager()
    {
        lost_crew_manager = Dm.@new<datum_lost_crew_manager>();
        Dm.append(ref gvars_datum_init_order, "lost_crew_manager");
    }
}

/// Handles procs and timers for the lost crew bodies
// tg: code/modules/lost_crew/lost_crew_manager.dm:5
public partial class datum_lost_crew_manager : datum
{
    /**
     * Creates a body with random background and injuries
     *
     * Arguments:
     * * revivable - Whether or not we can be revived to grand a ghost controle
     * * container - Humans really dont like not having a loc, so please either give the container where you want to spawn it or a turf
     * * forced_class - To force a specific damage class for some specific lore reason
     * * recovered_items - Items recovered, such as some organs, dropped directly with the body
     * * protected_items - Items that can only be recovered by the revived player
     * * body_data - Debug data we can use to get a readout of what has been done
     */
    // tg: code/modules/lost_crew/lost_crew_manager.dm:24
    // forced_class: tg's only callers pass a typepath (mapping_helpers.dm:1013), so the parameter is object (rules 3.5, a "-" cell,
    // as Appendix A decides for log_combat's object): the slot's datum_corpse_damage_class could not take it.
    public virtual async DmTask<object> create_lost_crew(float? revivable = null, object forced_class = null, DmList recovered_items = null, DmList protected_items = null, DmList body_data = null)
    {
        revivable ??= TRUE;
        body_data ??= Dm.list();
        var new_body = Dm.@new<mob_living_carbon_human>();
        await new_body.death();

        DmList scenarios = TgState.Current.datum_lost_crew_manager__scenarios ??= Dm.list();
        if (!Dm.truthy(scenarios.len))
        {
            DmList types = subtypesof(typeof(datum_corpse_damage_class));
            foreach (Type scenario_type in Dm.each<Type>(types))
            {
                if (datum_corpse_damage_class.dv561_unported(scenario_type)) // DEVIATION(DV561): the exotic species class (the podpeople)
                    continue;
                scenarios[scenario_type] = Dm.initial(scenario_type, (datum_corpse_damage_class p) => p.weight);
            }
        }

        DmList on_revive_and_player_occupancy = Dm.list();

        object scenario_pick = Dm.truthy(forced_class) ? forced_class : pick_weight(scenarios);
        var scenario = Dm.@new((Type)scenario_pick) as datum_corpse_damage_class;

        new_body.living_flags = Dm.bitor(new_body.living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS);
        await scenario.apply_character(new_body, protected_items, recovered_items, on_revive_and_player_occupancy, body_data);
        await scenario.apply_injuries(new_body, recovered_items, on_revive_and_player_occupancy, body_data);
        Dm.append(ref scenario.death_lore, I18n.T("I should get a formalized assignment!"));
        new_body.living_flags = Dm.bitand(new_body.living_flags, ~STOP_OVERLAY_UPDATE_BODY_PARTS);
        await new_body.update_body_parts();

        object dot = new_body;
        // so bodies can also be used for runes, morgue, etc
        if (!Dm.truthy(revivable))
            return dot;

        //it's not necessary since we dont spawn the body until we open the bodybag, but why not be nice for once
        await ((datum_reagents)new_body.reagents).add_reagent(typeof(datum_reagent_toxin_formaldehyde), 5);

        if (recovered_items == null)
            return dot;

        var paper = Dm.@new<obj_item_paper>();
        Dm.append(ref recovered_items, paper);

        if (!HAS_TRAIT(new_body, TRAIT_HUSK))
        {
            paper.name = I18n.T("DO NOT REMOVE BRAIN");
            paper.add_raw_text(I18n.T("Body swapping is not covered by medical insurance for unhusked bodies. Chemical brain explosives have been administered to enforce stipend."));
            var boombrain = new_body.get_organ_by_type(typeof(obj_item_organ_brain)) as obj_item_organ_brain;
            //I swear to fuck I will explode you. you're not clever
            //everyone thought of this, but I am the fool for having any faith
            //in people actually wanting to play the job in an interesting manner
            //instead of just taking the easiest way out and learning nothing
            //(no one abused it yet but I am already getting pinged by people who think they've broken the system when really I just expected better of them)
            await boombrain.AddElement(typeof(datum_element_dangerous_organ_removal));
        }
        else
        {
            paper.name = I18n.T("BODYSWAPPING PERMITTED");
            paper.add_raw_text(I18n.T("Body swapping is covered by medical insurance in case of husking and a lack of skill in the practictioner."));
        }

        var hersens = new_body.get_organ_by_type(typeof(obj_item_organ_brain)) as obj_item_organ_brain;
        await hersens.AddComponent(
            typeof(datum_component_ghostrole_on_revive),
            /* refuse_revival_if_failed = */ TRUE,
            /*on_revival = */ Dm.@new<datum_callback>(this, nameof(on_successful_revive), hersens, scenario.death_lore, on_revive_and_player_occupancy)
        );
        return dot;
    }

    /// Set a timer for awarding succes and drop some awesome deathlore
    // tg: code/modules/lost_crew/lost_crew_manager.dm:82
    public virtual async DmTask on_successful_revive(obj_item_organ_brain brain = null, DmList death_lore = null, DmList on_revive_and_player_occupancy = null)
    {
        var owner = brain.owner as mob_living_carbon_human;

        await owner.mind.add_antag_datum(typeof(datum_antagonist_recovered_crew)); //for tracking mostly

        var bank_account = Dm.@new<datum_bank_account>(owner.real_name, owner.mind.assigned_role, ((datum_species)owner.dna.species).payday_modifier);
        bank_account.adjust_money(starting_funds, I18n.T("[][] given to [] as starting fund.", starting_funds, MONEY_SYMBOL, owner.name));
        owner.account_id = bank_account.account_id;
        bank_account.replaceable = FALSE;

        owner.add_mob_memory(typeof(datum_memory_key_account), Dm.named("remembered_id", owner.account_id));

        Dm.append(ref death_lore, I18n.T("My account number was [].", owner.account_id));

        brain.RemoveElement(typeof(datum_element_dangerous_organ_removal));

        // Drop the sick ass death lore and give them an indicator of who they were and what they can do
        for (int i = 1; i <= death_lore.len; i++)
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, nameof(to_chat), owner, span_boldnotice(Dm.str(death_lore[i]))), 10 * SECONDS + 2 * SECONDS * i, file: "code/modules/lost_crew/lost_crew_manager.dm", line: 100);

        _addtimer(Dm.@new<datum_callback>(this, nameof(award_succes), owner.mind, death_lore), succes_check_time, file: "code/modules/lost_crew/lost_crew_manager.dm", line: 102);

        // Run any callbacks our characters or damages may have placed for some effects for when the player is revived
        foreach (datum_callback callback in Dm.each<datum_callback>(on_revive_and_player_occupancy))
            await callback.Invoke();
    }

    /// Give medbay a happy announcement and put some money into their budget
    // tg: code/modules/lost_crew/lost_crew_manager.dm:109
    public virtual async DmTask award_succes(datum_mind revived_mind = null, DmList death_lore = null)
    {
        // I am incredibly disappointed in you
        if (revived_mind.current.stat == DEAD)
        {
            await aas_config_announce(typeof(datum_aas_config_entry_medical_lost_crew_reward), Dm.list(Dm.kv("PERSON", revived_mind.name), Dm.kv("AWARD", 0)), null, Dm.list(RADIO_CHANNEL_MEDICAL), "Deceased");
            return;
        }

        // You are a credit to society
        await aas_config_announce(typeof(datum_aas_config_entry_medical_lost_crew_reward), Dm.list(Dm.kv("PERSON", revived_mind.name), Dm.kv("AWARD", credits_on_succes)), null, Dm.list(RADIO_CHANNEL_MEDICAL), "Revived");

        datum_bank_account medical_budget = SSeconomy.get_dep_account(ACCOUNT_MED);
        medical_budget.adjust_money(credits_on_succes);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/lost_crew/lost_crew_manager.dm:28  var/static/list/scenarios
    internal DmList datum_lost_crew_manager__scenarios;
}

/// A box for recovered items that can only be opened by the new crewmember
// tg: code/modules/lost_crew/lost_crew_manager.dm:122
public partial class obj_item_storage_lockbox_mind : obj_item_storage_lockbox
{
    // tg: code/modules/lost_crew/lost_crew_manager.dm:129
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.@in(this, user.held_items))
            return await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(atom_storage.locked) && Dm.truthy(can_unlock(user as mob_living, silent: TRUE)))
        {
            toggle_locked(user as mob_living);
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/lost_crew/lost_crew_manager.dm:137
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(atom_storage.locked) && Dm.truthy(can_unlock(user as mob_living)))
        {
            toggle_locked(user as mob_living);
            return null;
        }
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/modules/lost_crew/lost_crew_manager.dm:143
    public override object can_unlock(mob_living user = null, obj_item_card_id id_card = null, float? silent = null)
    {
        silent ??= FALSE;
        if (user.mind == mind)
            return TRUE;
        if (!Dm.truthy(silent))
            balloon_alert(user, I18n.T("access denied!"));
        return FALSE;
    }

    // tg: code/modules/lost_crew/lost_crew_manager.dm:150
    public override void toggle_locked(mob_living user = null)
    {
        if (!Dm.truthy(atom_storage.locked))
            return;

        atom_storage.set_locked(STORAGE_NOT_LOCKED);
        balloon_alert(user, I18n.T("unlocked"));
    }

    // tg: code/modules/lost_crew/lost_crew_manager.dm:157
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (Dm.truthy(broken) || user.mind != mind)
            return NONE;
        context[SCREENTIP_CONTEXT_LMB] = I18n.T("Use in-hand to unlock");
        return CONTEXTUAL_SCREENTIP_SET;
    }
}
