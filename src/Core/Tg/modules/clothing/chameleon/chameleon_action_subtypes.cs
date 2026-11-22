// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/chameleon/chameleon_action_subtypes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G1b (D316): the chameleon change actions of the clothing, the PDA, the stamp and the agent card (its
// card look and its trim); the chameleon gun's and the chameleon scanner's actions are with their items (chameleon_gun.cs,
// chameleon_scanner.cs). The var blocks are generated (D111).

public partial class datum_action_item_action_chameleon_change_neck : datum_action_item_action_chameleon_change
{
    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:6
    public override async DmTask initialize_blacklist()
    {
        await base.initialize_blacklist();
        Dm.combine(ref chameleon_blacklist, typecacheof(typeof(obj_item_clothing_neck_cloak_skill_reward)));
    }
}

public partial class datum_action_item_action_chameleon_change_tablet : datum_action_item_action_chameleon_change
{
    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:18
    public override async DmTask initialize_blacklist()
    {
        await base.initialize_blacklist();
        Dm.combine(ref chameleon_blacklist, typecacheof(Dm.list(typeof(obj_item_modular_computer_pda_crew_heads)), only_root_path: TRUE));
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:22
    protected internal override async DmTask update_item(Type picked_item = null)
    {
        await base.update_item(picked_item);
        if (target is obj_item_modular_computer_pda agent_pda)
            agent_pda.update_appearance();
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:28
    public override async DmTask apply_job_data(datum_job job_datum = null)
    {
        await base.apply_job_data(job_datum);
        if (target is obj_item_modular_computer_pda agent_pda && job_datum != null)
            agent_pda.imprint_id(job_name: job_datum.title);
    }
}

public partial class datum_action_item_action_chameleon_change_shoes : datum_action_item_action_chameleon_change
{
    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:54
    public override async DmTask initialize_blacklist()
    {
        await base.initialize_blacklist();
        Dm.combine(ref chameleon_blacklist, typecacheof(typeof(obj_item_clothing_shoes_changeling), only_root_path: TRUE));
    }
}

public partial class datum_action_item_action_chameleon_change_mask : datum_action_item_action_chameleon_change
{
    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:63
    public override async DmTask initialize_blacklist()
    {
        await base.initialize_blacklist();
        Dm.combine(ref chameleon_blacklist, typecacheof(typeof(obj_item_clothing_mask_changeling), only_root_path: TRUE));
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:67
    public override async DmTask initialize_disguises()
    {
        await base.initialize_disguises();
        add_chameleon_items(typeof(obj_item_cigarette));
        add_chameleon_items(typeof(obj_item_vape));
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:72
    protected internal override async DmTask update_item(Type picked_item = null)
    {
        await base.update_item(picked_item);
        obj_item_clothing_mask mask_used = target as obj_item_clothing_mask;
        if (Dm.ispath(picked_item, typeof(obj_item_clothing_mask)))
        {
            mask_used.voice_filter = Dm.initial(picked_item, (obj_item_clothing_mask p) => p.voice_filter);
            mask_used.use_radio_beeps_tts = Dm.initial(picked_item, (obj_item_clothing_mask p) => p.use_radio_beeps_tts);
        }
    }
}

public partial class datum_action_item_action_chameleon_change_hat : datum_action_item_action_chameleon_change
{
    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:85
    public override async DmTask initialize_blacklist()
    {
        await base.initialize_blacklist();
        Dm.combine(ref chameleon_blacklist, typecacheof(typeof(obj_item_clothing_head_changeling), only_root_path: TRUE));
    }
}

public partial class datum_action_item_action_chameleon_change_gloves : datum_action_item_action_chameleon_change
{
    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:94
    public override async DmTask initialize_blacklist()
    {
        await base.initialize_blacklist();
        Dm.combine(ref chameleon_blacklist, typecacheof(Dm.list(typeof(obj_item_clothing_gloves), typeof(obj_item_clothing_gloves_color), typeof(obj_item_clothing_gloves_changeling)), only_root_path: TRUE));
    }
}

public partial class datum_action_item_action_chameleon_change_glasses : datum_action_item_action_chameleon_change
{
    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:103
    public override async DmTask initialize_blacklist()
    {
        await base.initialize_blacklist();
        Dm.combine(ref chameleon_blacklist, typecacheof(Dm.list(typeof(obj_item_clothing_glasses_changeling), typeof(obj_item_clothing_glasses_hud_security_chameleon), typeof(obj_item_clothing_glasses_thermal_syndi)), only_root_path: TRUE));
    }
}

public partial class datum_action_item_action_chameleon_change_suit : datum_action_item_action_chameleon_change
{
    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:115
    public override async DmTask initialize_blacklist()
    {
        await base.initialize_blacklist();
        Dm.combine(ref chameleon_blacklist, typecacheof(Dm.list(typeof(obj_item_clothing_suit_armor_abductor), typeof(obj_item_clothing_suit_changeling)), only_root_path: TRUE));
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:119
    public override async DmTask<object> apply_outfit(datum_outfit applying_from = null, DmList all_items_to_apply = null)
    {
        object dot = await base.apply_outfit(applying_from, all_items_to_apply);
        if (!Dm.truthy(dot) || !Dm.ispath(applying_from.suit, typeof(obj_item_clothing_suit_hooded)))
            return dot;
        // If we're appling a hooded suit, and wearing a cham hat, make it a hood
        Type hooded = applying_from.suit as Type;
        var hood_action = owner == null ? null : Dm.locate<datum_action_item_action_chameleon_change_hat>(owner.actions) as datum_action_item_action_chameleon_change_hat;
        if (hood_action != null)
            await hood_action.update_look(Dm.initial(hooded, (obj_item_clothing_suit_hooded p) => p.hoodtype));
        return dot;
    }
}

public partial class datum_action_item_action_chameleon_change_jumpsuit : datum_action_item_action_chameleon_change
{
    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:133
    public override async DmTask initialize_blacklist()
    {
        await base.initialize_blacklist();
        Dm.combine(ref chameleon_blacklist, typecacheof(Dm.list(typeof(obj_item_clothing_under), typeof(obj_item_clothing_under_color), typeof(obj_item_clothing_under_rank), typeof(obj_item_clothing_under_changeling)), only_root_path: TRUE));
    }
}

public partial class datum_action_item_action_chameleon_change_id : datum_action_item_action_chameleon_change
{
    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:141
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (target is not obj_item_card_id_advanced_chameleon)
        {
            stack_trace(Dm.text("Adding chameleon ID action to non-chameleon id ([])", target), "code/modules/clothing/chameleon/chameleon_action_subtypes.dm", 146);
            qdel(this);
        }
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:147
    public override object format_readable_name(object format_type = null)
    {
        if (format_type is Type format_card && Dm.ispath(format_card, typeof(obj_item_card_id_advanced)))
        {
            object state = Dm.initial(format_card, (atom p) => p.post_init_icon_state);
            if (!Dm.truthy(state))
                state = Dm.initial(format_card, (atom p) => p.icon_state);
            DmList basesplit_state = splittext(state, "_") as DmList;
            if (Dm.eq(basesplit_state[1], "card")) // kind of evil, but so is making icon state user visible
                basesplit_state.Cut(1, 2);

            DmList assignedsplit_state = splittext(Dm.initial(format_card, (obj_item_card_id_advanced p) => p.assigned_icon_state), "_") as DmList;
            if (Dm.eq(assignedsplit_state[1], "assigned")) // kind of evil, but so is making icon state user visible
                assignedsplit_state.Cut(1, 2);

            object assigned = jointext(assignedsplit_state, " ");
            string final_state_string = Dm.text("[], []", jointext(basesplit_state, " "), Dm.truthy(assigned) ? assigned : "unassigned");

            return Dm.text("[] ([])", Dm.initial(format_card, (atom p) => p.name), final_state_string);
        }

        return base.format_readable_name(format_type);
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:164
    public override async DmTask initialize_disguises()
    {
        // basic id types
        add_chameleon_items(Dm.list(
            typeof(obj_item_card_id_advanced),
            typeof(obj_item_card_id_advanced_black),
            typeof(obj_item_card_id_advanced_bountyhunter),
            typeof(obj_item_card_id_advanced_centcom),
            typeof(obj_item_card_id_advanced_gold),
            typeof(obj_item_card_id_advanced_platinum),
            typeof(obj_item_card_id_advanced_prisoner),
            typeof(obj_item_card_id_advanced_rainbow),
            typeof(obj_item_card_id_advanced_robotic),
            typeof(obj_item_card_id_advanced_silver)), only_root: TRUE);

        // all black cards (they're all syndie cards, which are IC anyways)
        add_chameleon_items(typeof(obj_item_card_id_advanced_black));
        // all centcom cards (they should all be IC anyways)
        add_chameleon_items(typeof(obj_item_card_id_advanced_centcom));
        // explicitly include the captain's spare id, even though it could be manually constructed from the gold id, for ease
        add_chameleon_items(typeof(obj_item_card_id_advanced_gold_captains_spare), only_root: TRUE);
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:186
    protected internal override async DmTask update_item(Type picked_item = null)
    {
        await base.update_item(picked_item);
        obj_item_card_id_advanced_chameleon agent_card = target as obj_item_card_id_advanced_chameleon;
        Type copied_card = picked_item;

        // If the outfit comes with a special trim override, we'll steal some stuff from that.
        object new_trim = Dm.initial(copied_card, (obj_item_card_id p) => p.trim);

        if (Dm.truthy(new_trim))
            SSid_access.apply_trim_override(agent_card, new_trim, TRUE);

        // If the ID card hasn't been forged, we'll check if there has been an assignment set already by any new trim.
        // If there has not, we set the assignment to the copied card's default as well as copying over the the
        // default registered name from the copied card.
        if (!Dm.truthy(agent_card.forged))
        {
            if (!Dm.truthy(agent_card.assignment))
                agent_card.assignment = Dm.initial(copied_card, (obj_item_card_id p) => p.assignment);
            agent_card.registered_name = Dm.initial(copied_card, (obj_item_card_id p) => p.registered_name);
        }

        agent_card.icon_state = Dm.initial(copied_card, (atom p) => p.icon_state);
        if (Dm.ispath(copied_card, typeof(obj_item_card_id_advanced)))
        {
            agent_card.assigned_icon_state = Dm.initial(copied_card, (obj_item_card_id_advanced p) => p.assigned_icon_state);
            agent_card.inherent_assigned_name = Dm.initial(copied_card, (obj_item_card_id_advanced p) => p.inherent_assigned_name);
        }

        agent_card.update_label();
        agent_card.update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:213
    public override async DmTask apply_job_data(datum_job job_datum = null)
    {
        obj_item_card_id_advanced_chameleon agent_card = target as obj_item_card_id_advanced_chameleon;
        agent_card.forged = TRUE;

        // job_outfit is going to be a path.
        Type job_outfit = job_datum.outfit;
        if (job_outfit == null)
            return;

        // copied_card is also going to be a path.
        Type copied_card = Dm.initial(job_outfit, (datum_outfit p) => p.id) as Type;
        if (copied_card == null)
            return;

        // If the outfit comes with a special trim override, we'll use that. Otherwise, use the card's default trim. Failing that, no trim at all.
        object outfit_trim = Dm.initial(job_outfit, (datum_outfit_job p) => p.id_trim);
        object new_trim = Dm.truthy(outfit_trim) ? outfit_trim : Dm.initial(copied_card, (obj_item_card_id p) => p.trim);

        if (Dm.truthy(new_trim))
            SSid_access.apply_trim_override(agent_card, new_trim, FALSE);
        else
            agent_card.assignment = job_datum.title;

        agent_card.icon_state = Dm.initial(copied_card, (atom p) => p.icon_state);
        if (Dm.ispath(copied_card, typeof(obj_item_card_id_advanced)))
            agent_card.assigned_icon_state = Dm.initial(copied_card, (obj_item_card_id_advanced p) => p.assigned_icon_state);

        agent_card.update_label();
        agent_card.update_appearance(UPDATE_ICON);
    }
}

public partial class datum_action_item_action_chameleon_change_id_trim : datum_action_item_action_chameleon_change
{
    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:247
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (target is not obj_item_card_id_advanced_chameleon)
        {
            stack_trace(Dm.text("Adding chameleon ID trim action to non-chameleon id ([])", target), "code/modules/clothing/chameleon/chameleon_action_subtypes.dm", 254);
            qdel(this);
        }
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:253
    public override object format_readable_name(object format_type = null)
    {
        if (format_type is datum_id_trim trim)
        {
            DmList trimstate_split = splittext(trim.trim_state, "_") as DmList;
            if (Dm.eq(trimstate_split[1], "trim")) // kind of evil, but so is making icon state user visible
                trimstate_split.Cut(1, 2);

            return Dm.text("[] ([])", trim.assignment, jointext(trimstate_split, " "));
        }

        return base.format_readable_name(format_type);
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:264
    public override async DmTask initialize_blacklist()
    {
        Dm.combine(ref chameleon_blacklist, typecacheof(typeof(datum_id_trim_centcom_corpse)));
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:267
    public override async DmTask initialize_disguises()
    {
        DmList chameleon_whitelist = typecacheof(Dm.list(
            typeof(datum_id_trim_battlecruiser),
            typeof(datum_id_trim_bit_avatar),
            typeof(datum_id_trim_bounty_hunter),
            typeof(datum_id_trim_centcom),
            typeof(datum_id_trim_job),
            typeof(datum_id_trim_pirate),
            typeof(datum_id_trim_syndicom),
            typeof(datum_id_trim_technician_id))) as DmList;

        foreach (Type trim_path in Dm.each<Type>(chameleon_whitelist))
        {
            if (Dm.truthy(is_type_in_typecache(trim_path, chameleon_blacklist)))
                continue;

            // SSid_access?: a test harness world that creates items without Master has no SSid_access (rules 10.6), as the ID card's Initialize
            datum_id_trim trim = SSid_access?.trim_singletons_by_path[trim_path] as datum_id_trim;

            if (trim != null && Dm.truthy(trim.trim_state) && Dm.truthy(trim.assignment))
            {
                chameleon_list[format_readable_name(trim)] = trim_path;
                chameleon_typecache[trim_path] = TRUE;
            }
        }
    }

    // tg: code/modules/clothing/chameleon/chameleon_action_subtypes.dm:289
    protected internal override async DmTask update_item(Type picked_trim_path = null)
    {
        obj_item_card_id_advanced_chameleon agent_card = target as obj_item_card_id_advanced_chameleon;

        SSid_access.apply_trim_override(agent_card, picked_trim_path, TRUE);

        agent_card.update_label();
        agent_card.update_appearance(UPDATE_ICON);
    }
}
