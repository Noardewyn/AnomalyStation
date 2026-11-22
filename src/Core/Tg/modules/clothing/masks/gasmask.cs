// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/masks/gasmask.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: /obj/item/clothing/mask/gas whole (M4a clothing, gaps J2 D298: the filters, the cigarette slot's clicks, examine), the
// clown's and the mime's masks; gaps CL1 (D333): the welding mask (its visor up and down with tg's visor sounds and its icon, the tint
// through update_tint) and the tiki mask's radial of faces. The other subtypes are var-only. The fishing difficulty and the visor's
// dirt are Deferred leaves (not-translated.md 106).

// tg: code/modules/clothing/masks/gasmask.dm:1
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/clothing/masks/gasmask.dm:1  GLOBAL_LIST_INIT(clown_mask_options, list(...))
    public DmList clown_mask_options;

    // tg: code/modules/clothing/masks/gasmask.dm:1  (GLOBAL_MANAGED(clown_mask_options, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalclown_mask_options()
    {
        clown_mask_options = Dm.list(
            Dm.kv("True Form", "clown"),
            Dm.kv("The Coquette", "sexyclown"),
            Dm.kv("The Madman", "joker"),
            Dm.kv("The Rainbow Color", "rainbow"),
            Dm.kv("The Dealer", "cards"));
        Dm.append(ref gvars_datum_init_order, "clown_mask_options");
    }
}

// tg: code/modules/clothing/masks/gasmask.dm:10
public partial class obj_item_clothing_mask_gas : obj_item_clothing_mask
{
    // tg: code/modules/clothing/masks/gasmask.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(Dm.bitand(flags_cover, PEPPERPROOF)) && Dm.truthy(pepper_tint))
            Dm.Deferred("code/modules/clothing/masks/gasmask.dm:48", "Initialize: AddComponent(/datum/component/clothing_dirt, dirt_state), the pepper spray's dirt on a visor");

        if (Dm.truthy(fishing_modifier))
            Dm.Deferred("code/modules/clothing/masks/gasmask.dm:51", "Initialize: AddElement(/datum/element/adjust_fishing_difficulty, fishing_modifier): fishing");

        if (!Dm.truthy(max_filters) || starting_filter_type == null)
            return dot;

        for (int i = 1; i <= max_filters; i++)
        {
            obj_item_gas_filter inserted_filter = Dm.@new(starting_filter_type, this) as obj_item_gas_filter;
            LAZYADD(ref gas_filters, inserted_filter);
        }
        has_filter = TRUE;
        return dot;
    }

    // tg: code/modules/clothing/masks/gasmask.dm:61
    public override async DmTask<object> separate_worn_overlays(mutable_appearance standing = null, mutable_appearance draw_target = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        bodyshape ??= NONE;
        DmList dot = null;
        dot = await base.separate_worn_overlays(standing, draw_target, isinhands, icon_file, bodyshape) as DmList;
        if (!Dm.truthy(isinhands) && cig != null)
            Dm.append(ref dot, await cig.build_worn_icon(default_layer: FACEMASK_LAYER, default_icon_file: Dm.resource("icons/mob/clothing/mask.dmi"), bodyshape: bodyshape));
        return dot;
    }

    // tg: code/modules/clothing/masks/gasmask.dm:66
    public override object Destroy(float? force = null)
    {
        QDEL_LAZYLIST(ref gas_filters);
        return base.Destroy(force);
    }

    // tg: code/modules/clothing/masks/gasmask.dm:70
    [DmParams("equipee", "slot")]
    public override async DmTask equipped__proc(mob equipee = null, object slot = null, float? initial = null)
    {
        if (cig != null)
            await cig.equipped__proc(equipee, slot);
        await base.equipped__proc(equipee, slot, initial);
    }

    // tg: code/modules/clothing/masks/gasmask.dm:74
    public override async DmTask<object> adjust_visor(mob_living user = null)
    {
        if (cig != null)
        {
            balloon_alert(user, I18n.T("cig in the way!"));
            return FALSE;
        }
        return await base.adjust_visor(user);
    }

    // tg: code/modules/clothing/masks/gasmask.dm:80
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (cig != null)
            Dm.append(ref dot, span_notice(I18n.T("There is a [] jammed into the filter slot.", cig.name)));
        if (max_filters > 0 && cig == null)
            Dm.append(ref dot, span_notice(I18n.T("[] has [] slot\\s for filters.", this, max_filters)));
        if (Dm.num(LAZYLEN(gas_filters)) > 0)
        {
            Dm.append(ref dot, span_notice(I18n.T("Currently there [] [] filter\\s with []% durability.", Dm.num(LAZYLEN(gas_filters)) == 1 ? "is" : "are", LAZYLEN(gas_filters), get_filter_durability())));
            Dm.append(ref dot, span_notice(I18n.T("The filters can be removed by right-clicking with an empty hand on [].", this)));
        }
        return dot;
    }

    // tg: code/modules/clothing/masks/gasmask.dm:90
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == cig)
        {
            cig = null;
            if (ismob(loc))
            {
                mob wearer = loc as mob;
                await wearer.update_worn_mask();
            }
        }
        return dot;
    }

    // tg: code/modules/clothing/masks/gasmask.dm:98
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        bool valid_wearer = ismob(loc);
        mob wearer = loc as mob;
        if (tool is obj_item_cigarette)
        {
            if (max_filters <= 0 || cig != null)
            {
                balloon_alert(user, I18n.T("can't hold that!"));
                return await base.item_interaction(user, tool, modifiers, __extra);
            }

            if (Dm.truthy(has_filter))
            {
                balloon_alert(user, I18n.T("filters in the mask!"));
                return await base.item_interaction(user, tool, modifiers, __extra);
            }

            cig = tool as obj_item_cigarette;
            if (valid_wearer)
                await cig.equipped__proc(wearer, wearer.get_slot_by_item(cig));

            await cig.forceMove(this);
            if (valid_wearer)
                await wearer.update_worn_mask();
            return ITEM_INTERACT_SUCCESS;
        }

        if (cig != null)
        {
            object cig_interaction = await cig.item_interaction(user, tool);
            if (valid_wearer)
                await wearer.update_worn_mask();
            return cig_interaction;
        }

        if (tool is not obj_item_gas_filter)
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (Dm.num(LAZYLEN(gas_filters)) >= max_filters)
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return await base.item_interaction(user, tool, modifiers, __extra);

        LAZYADD(ref gas_filters, tool);
        has_filter = TRUE;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/clothing/masks/gasmask.dm:138
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (cig != null)
        {
            await user.put_in_hands(cig);
            cig = null;
            if (ismob(loc))
            {
                mob wearer = loc as mob;
                await wearer.update_worn_mask();
            }
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (!Dm.truthy(has_filter) || !Dm.truthy(max_filters))
            return SECONDARY_ATTACK_CONTINUE_CHAIN;
        for (int i = 1; i <= max_filters; i++)
        {
            obj_item_gas_filter filter = Dm.locate<obj_item_gas_filter>(this) as obj_item_gas_filter;
            if (filter == null)
                continue;
            await user.put_in_hands(filter);
            LAZYREMOVE(ref gas_filters, filter);
        }
        if (Dm.num(LAZYLEN(gas_filters)) <= 0)
            has_filter = FALSE;
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    ///Check _masks.dm for this one
    // tg: code/modules/clothing/masks/gasmask.dm:159
    public override object consume_filter(datum_gas_mixture breath = null)
    {
        if (Dm.num(LAZYLEN(gas_filters)) <= 0 || max_filters == 0)
            return breath;
        obj_item_gas_filter gas_filter = pick(gas_filters) as obj_item_gas_filter;
        object filtered_breath = gas_filter.reduce_filter_status(breath);
        if (gas_filter.filter_status <= 0)
        {
            LAZYREMOVE(ref gas_filters, gas_filter);
            qdel(gas_filter);
        }
        if (Dm.num(LAZYLEN(gas_filters)) <= 0)
            has_filter = FALSE;
        return filtered_breath;
    }

    /**
     * Getter for overall filter durability, takes into consideration all filters filter_status
     */
    // tg: code/modules/clothing/masks/gasmask.dm:174
    public virtual object get_filter_durability()
    {
        float max_filters_durability = Dm.num(LAZYLEN(gas_filters)) * 100;
        float current_filters_durability = 0;
        foreach (obj_item_gas_filter gas_filter in Dm.each<obj_item_gas_filter>(gas_filters))
            current_filters_durability += gas_filter.filter_status;
        float durability = (current_filters_durability / max_filters_durability) * 100;
        return durability;
    }
}

// tg: code/modules/clothing/masks/gasmask.dm:218
public partial class obj_item_clothing_mask_gas_welding : obj_item_clothing_mask_gas
{
    // tg: code/modules/clothing/masks/gasmask.dm:245
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await adjust_visor(user as mob_living);
        return null;
    }

    // tg: code/modules/clothing/masks/gasmask.dm:248
    public override async DmTask<object> adjust_visor(mob_living user = null)
    {
        object dot = await base.adjust_visor(user);
        if (Dm.truthy(dot))
            playsound(this, Dm.truthy(up) ? SFX_VISOR_UP : SFX_VISOR_DOWN, 50, TRUE);
        if (!Dm.truthy(fishing_modifier))
            return dot;
        // if(up) RemoveElement(/datum/element/adjust_fishing_difficulty) else AddElement(/datum/element/adjust_fishing_difficulty, fishing_modifier)
        Dm.Deferred("code/modules/clothing/masks/gasmask.dm:253", "adjust_visor: the adjust_fishing_difficulty element of the visor: fishing (M10+)");
        return dot;
    }

    // tg: code/modules/clothing/masks/gasmask.dm:259
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", Dm.initial(this, (obj_item_clothing_mask_gas_welding p) => p.icon_state), Dm.truthy(up) ? "up" : "");
        return dot;
    }
}

// tg: code/modules/clothing/masks/gasmask.dm:263
public partial class obj_item_clothing_mask_gas_welding_up : obj_item_clothing_mask_gas_welding
{
    // tg: code/modules/clothing/masks/gasmask.dm:263
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        visor_toggling();
        return dot;
    }
}

// tg: code/modules/clothing/masks/gasmask.dm:303
public partial class obj_item_clothing_mask_gas_clown_hat : obj_item_clothing_mask_gas
{
    // tg: code/modules/clothing/masks/gasmask.dm:325
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        clownmask_designs = Dm.list(
            Dm.kv("True Form", image(icon: icon, icon_state: "clown")),
            Dm.kv("The Coquette", image(icon: icon, icon_state: "sexyclown")),
            Dm.kv("The Madman", image(icon: icon, icon_state: "joker")),
            Dm.kv("The Rainbow Color", image(icon: icon, icon_state: "rainbow")),
            Dm.kv("The Dealer", image(icon: icon, icon_state: "cards")));
        Dm.Deferred("code/modules/clothing/masks/gasmask.dm:333", "Initialize: AddElement(/datum/element/swabable, CELL_LINE_TABLE_CLOWN, ...): the cell lines (the sample swab, not ported)");
        return dot;
    }

    // tg: code/modules/clothing/masks/gasmask.dm:336
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (user == null || Dm.truthy(user.incapacitated))
            return null;

        object choice = await show_radial_menu(user, this, clownmask_designs, radius: 36, require_near: TRUE);
        if (!Dm.truthy(choice))
            return FALSE;

        if (Dm.truthy(choice) && !Dm.truthy(user.incapacitated) && Dm.truthy(in_range(user, this)))
        {
            DmList options = GLOB.clown_mask_options;
            icon_state = Dm.str(options[choice]);
            await user.update_worn_mask();
            await update_item_action_buttons();
            to_chat(user, span_notice(I18n.T("Your Clown Mask has now morphed into [], all praise the Honkmother!", choice)));
            return TRUE;
        }
        return null;
    }
}

// tg: code/modules/clothing/masks/gasmask.dm:380
public partial class obj_item_clothing_mask_gas_mime : obj_item_clothing_mask_gas
{
    // tg: code/modules/clothing/masks/gasmask.dm:397
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        mimemask_designs = Dm.list(
            Dm.kv("Blanc", image(icon: icon, icon_state: "mime")),
            Dm.kv("Excit\u00e9", image(icon: icon, icon_state: "sexymime")),
            Dm.kv("Triste", image(icon: icon, icon_state: "sadmime")),
            Dm.kv("Effray\u00e9", image(icon: icon, icon_state: "scaredmime")));
        return dot;
    }

    // tg: code/modules/clothing/masks/gasmask.dm:406
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (user == null || Dm.truthy(user.incapacitated))
            return null;

        DmList options = Dm.list();
        options["Blanc"] = "mime";
        options["Triste"] = "sadmime";
        options["Effray\u00e9"] = "scaredmime";
        options["Excit\u00e9"] = "sexymime";

        object choice = await show_radial_menu(user, this, mimemask_designs, radius: 36, require_near: TRUE);
        if (!Dm.truthy(choice))
            return FALSE;

        if (Dm.truthy(choice) && !Dm.truthy(user.incapacitated) && Dm.truthy(in_range(user, this)))
        {
            icon_state = Dm.str(options[choice]);
            await user.update_worn_mask();
            await update_item_action_buttons();
            to_chat(user, span_notice(I18n.T("Your Mime Mask has now morphed into []!", choice)));
            return TRUE;
        }
        return null;
    }
}

// tg: code/modules/clothing/masks/gasmask.dm:478
public partial class obj_item_clothing_mask_gas_tiki_mask : obj_item_clothing_mask_gas
{
    // tg: code/modules/clothing/masks/gasmask.dm:492
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        tikimask_designs = Dm.list(
            Dm.kv("Original Tiki", Dm.image(icon: this.icon, icon_state: "tiki_eyebrow")),
            Dm.kv("Happy Tiki", Dm.image(icon: this.icon, icon_state: "tiki_happy")),
            Dm.kv("Confused Tiki", Dm.image(icon: this.icon, icon_state: "tiki_confused")),
            Dm.kv("Angry Tiki", Dm.image(icon: this.icon, icon_state: "tiki_angry"))
        );
        return dot;
    }

    // tg: code/modules/clothing/masks/gasmask.dm:501
    [DmParams("user")]
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        mob M = usr;
        DmList options = Dm.list();
        options["Original Tiki"] = "tiki_eyebrow";
        options["Happy Tiki"] = "tiki_happy";
        options["Confused Tiki"] = "tiki_confused";
        options["Angry Tiki"] = "tiki_angry";

        object choice = await show_radial_menu(user, this, tikimask_designs, custom_check: null, radius: 36, require_near: TRUE);
        if (!Dm.truthy(choice))
            return FALSE;

        if (!QDELETED(this) && Dm.truthy(choice) && !IS_UNCONSCIOUS_OR_CRIT(M) && in_range(M, this))
        {
            icon_state = Dm.str(options[choice]);
            await user.update_worn_mask();
            await update_item_action_buttons();
            to_chat(M, span_notice(I18n.T("The Tiki Mask has now changed into the [] Mask!", choice)));
            return 1;
        }
        return null;
    }
}
