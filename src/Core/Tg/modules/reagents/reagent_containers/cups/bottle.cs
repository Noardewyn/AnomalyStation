// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/cups/bottle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file's procs (the bottle, the syndicate and buffer bottles, the syrup pump bottle). The var
// blocks are generated.

// tg: code/modules/reagents/reagent_containers/cups/bottle.dm:3
public partial class obj_item_reagent_containers_cup_bottle : obj_item_reagent_containers_cup
{
    // tg: code/modules/reagents/reagent_containers/cups/bottle.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(icon_state))
            icon_state = "bottle";
        update_appearance();
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/cups/bottle.dm:155
public partial class obj_item_reagent_containers_cup_bottle_traitor : obj_item_reagent_containers_cup_bottle
{
    // tg: code/modules/reagents/reagent_containers/cups/bottle.dm:161
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        extra_reagent = (Type)pick(typeof(datum_reagent_toxin_polonium), typeof(datum_reagent_toxin_histamine), typeof(datum_reagent_toxin_formaldehyde), typeof(datum_reagent_toxin_venom), typeof(datum_reagent_toxin_fentanyl), typeof(datum_reagent_toxin_cyanide));
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(extra_reagent, 3));
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/cups/bottle.dm:252
public partial class obj_item_reagent_containers_cup_bottle_random_buffer : obj_item_reagent_containers_cup_bottle
{
    // tg: code/modules/reagents/reagent_containers/cups/bottle.dm:256
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(50))
        {
            name = I18n.T("Acidic buffer bottle");
            desc = I18n.T("A small bottle of acidic buffer.");
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_reaction_agent_acidic_buffer), 30));
        }
        else
        {
            name = I18n.T("Basic buffer bottle");
            desc = I18n.T("A small bottle of basic buffer.");
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(typeof(datum_reagent_reaction_agent_basic_buffer), 30));
        }
        return dot;
    }
}

/*
 *	Syrup bottles, basically a unspillable cup that transfers reagents upon clicking on it with a cup
 */
// tg: code/modules/reagents/reagent_containers/cups/bottle.dm:512
public partial class obj_item_reagent_containers_cup_bottle_syrup_bottle : obj_item_reagent_containers_cup_bottle
{
    // tg: code/modules/reagents/reagent_containers/cups/bottle.dm:524
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // this is not done via initial_reagent_flags because it represents state
        update_container_flags(Dm.bitor(SEALED_CONTAINER, TRANSPARENT));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/bottle.dm:529
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Alt-click to toggle the pump cap.")));
        Dm.append(ref dot, span_notice(I18n.T("Use a pen on it to rename it.")));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/bottle.dm:534
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        base.add_context(source, context, held_item, user);

        context[SCREENTIP_CONTEXT_ALT_LMB] = (Dm.truthy(is_open_container()) ? "Add Pump Cap" : "Remove Pump Cap");
        if (IS_WRITING_UTENSIL(held_item))
            context[SCREENTIP_CONTEXT_LMB] = "Write Label";
        else if (Dm.truthy(is_open_container()) && Dm.truthy(held_item?.is_refillable()))
            context[SCREENTIP_CONTEXT_LMB] = "Use Pump";

        return CONTEXTUAL_SCREENTIP_SET;
    }

    //when you attack the syrup bottle with a container it refills it
    // tg: code/modules/reagents/reagent_containers/cups/bottle.dm:546
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(is_open_container()) && Dm.truthy(tool.is_refillable()))
            return await refillable_act(user, tool);
        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/modules/reagents/reagent_containers/cups/bottle.dm:551
    public override async DmTask<object> nameformat(object input = null, mob_living user = null)
    {
        playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
        return Dm.text("[]bottle", Dm.truthy(input) ? Dm.text("[] ", input) : null);
    }

    // tg: code/modules/reagents/reagent_containers/cups/bottle.dm:556
    public virtual async DmTask<object> refillable_act(mob user = null, obj_item tool = null)
    {
        datum_reagents holder = (datum_reagents)reagents;
        if (!Dm.truthy(holder.total_volume))
        {
            balloon_alert(user, I18n.T("bottle empty!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(((datum_reagents)tool.reagents).holder_full()))
        {
            balloon_alert(user, I18n.T("container full!"));
            return ITEM_INTERACT_BLOCKING;
        }

        float transfer_amount = round(Dm.num(await holder.trans_to(tool, amount_per_transfer_from_this, transferred_by: user)), CHEMICAL_VOLUME_ROUNDING);
        if (Dm.truthy(transfer_amount))
            balloon_alert(user, I18n.T("transferred [] unit\\s", transfer_amount));
        flick("syrup_anim", this);
        tool.update_appearance();
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/bottle.dm:572
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(is_open_container()))
            icon_state = "syrup_open";
        else
            icon_state = "syrup";
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/bottle.dm:579
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(is_open_container()))
        {
            balloon_alert(user, I18n.T("put pump cap on"));
            update_container_flags(Dm.bitor(SEALED_CONTAINER, TRANSPARENT));
        }
        else
        {
            balloon_alert(user, I18n.T("removed pump cap"));
            reset_container_flags();
        }

        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }
}
