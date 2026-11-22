// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/hypospray.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file but the medipen's suicide (a live-partial row, M10): the hypospray's injection with its
// sound, drawing into it, the medipens (used up when empty, their label, self-injection in hand), the survival pens'
// pressure gauge. The var blocks are generated.

// tg: code/modules/reagents/reagent_containers/hypospray.dm:1
public partial class obj_item_reagent_containers_hypospray : obj_item_reagent_containers
{
    // tg: code/modules/reagents/reagent_containers/hypospray.dm:23
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/modules/reagents/reagent_containers/hypospray.dm:26
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return NONE;
        return Dm.truthy(await inject((mob_living)interacting_with, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/reagents/reagent_containers/hypospray.dm:31
    public override async DmTask<object> interact_with_atom_secondary(atom target = null, mob_living user = null, DmList modifiers = null)
    {
        if (target.reagents == null)
            return NONE;
        if (isliving(target))
        {
            to_chat(user, span_warning(I18n.T("[] can't be used to draw blood!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        datum_reagents holder = (datum_reagents)reagents;
        if (Dm.truthy(holder.holder_full()))
        {
            to_chat(user, span_notice(I18n.T("[] is full.", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        datum_reagents target_holder = (datum_reagents)target.reagents;
        if (!Dm.truthy(target_holder.total_volume))
        {
            to_chat(user, span_warning(I18n.T("[] is empty!", target)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(target.is_drawable(user)))
        {
            to_chat(user, span_warning(I18n.T("You cannot directly remove reagents from []!", target)));
            return ITEM_INTERACT_BLOCKING;
        }
        object trans = await target_holder.trans_to(this, amount_per_transfer_from_this, transferred_by: user);
        if (Dm.truthy(trans))
            to_chat(user, span_notice(I18n.T("You fill [] with [] units of the solution. It now contains [] units.", this, trans, holder.total_volume)));
        return ITEM_INTERACT_SUCCESS;
    }

    ///Handles all injection checks, injection and logging.
    // tg: code/modules/reagents/reagent_containers/hypospray.dm:54
    public virtual async DmTask<object> inject(mob_living affected_mob = null, mob user = null)
    {
        if (Dm.truthy(used_up))
        {
            to_chat(user, span_warning(I18n.T("[] tip is broken and is now unusable!", this)));
            return FALSE;
        }
        if (!iscarbon(affected_mob))
            return FALSE;

        datum_reagents holder = (datum_reagents)reagents;
        //Always log attemped injects for admins
        DmList injected = Dm.list();
        foreach (datum_reagent injected_reagent in Dm.each<datum_reagent>(holder.reagent_list))
            injected.Add(injected_reagent.name);
        object contained = english_list(injected);
        log_combat(user, affected_mob, "attempted to inject", this, Dm.text("([])", contained));

        user.changeNext_move(CLICK_CD_MELEE);
        if (!Dm.truthy(used_up) && (Dm.truthy(ignore_flags) || Dm.truthy(affected_mob.try_inject(user, injection_flags: INJECT_TRY_SHOW_ERROR_MESSAGE)))) // Ignore flag should be checked first or there will be an error message.
        {
            to_chat(affected_mob, span_warning(I18n.T("You feel a tiny prick!")));
            to_chat(user, span_notice(I18n.T("You inject [] with [].", affected_mob, this)));
            if (!Dm.truthy(stealthy))
                playsound(affected_mob, Dm.resource("sound/items/hypospray.ogg"), 50, TRUE);
            float fraction = min(amount_per_transfer_from_this / holder.total_volume, 1);

            if (affected_mob.reagents != null)
            {
                object trans = 0;
                if (!Dm.truthy(infinite))
                    trans = await holder.trans_to(affected_mob, amount_per_transfer_from_this, transferred_by: user, methods: INJECT);
                else
                {
                    await holder.expose(affected_mob, INJECT, fraction);
                    trans = await holder.trans_to(affected_mob, amount_per_transfer_from_this, methods: INJECT, copy_only: TRUE);
                }
                to_chat(user, span_notice(I18n.T("[] unit\\s injected. [] unit\\s remaining in [].", trans, holder.total_volume, this)));
                log_combat(user, affected_mob, "injected", this, Dm.text("([])", contained));
            }
            return TRUE;
        }
        return FALSE;
    }
}

// tg: code/modules/reagents/reagent_containers/hypospray.dm:115
public partial class obj_item_reagent_containers_hypospray_combat_nanites : obj_item_reagent_containers_hypospray_combat
{
    // tg: code/modules/reagents/reagent_containers/hypospray.dm:124
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, (((datum_reagents)reagents).total_volume > 0) ? null : 0);
        return await base.update_icon_state(__extra);
    }
}

//MediPens
// tg: code/modules/reagents/reagent_containers/hypospray.dm:140
public partial class obj_item_reagent_containers_hypospray_medipen : obj_item_reagent_containers_hypospray
{
    // tg: code/modules/reagents/reagent_containers/hypospray.dm:166
    public override async DmTask<object> inject(mob_living affected_mob = null, mob user = null)
    {
        object dot = await base.inject(affected_mob, user);
        datum_reagents holder = (datum_reagents)reagents;
        if (Dm.truthy(dot) && !Dm.truthy(holder.total_volume))
        {
            used_up = TRUE; //Makes them useless afterwards
            holder.flags = NONE;
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/hypospray.dm:173
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(await user.can_perform_action(this, Dm.bitor(FORBID_TELEKINESIS_REACH, ALLOW_RESTING))))
            await inject((mob_living)user, user);
        return null;
    }

    // tg: code/modules/reagents/reagent_containers/hypospray.dm:177
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, (((datum_reagents)reagents).total_volume > 0) ? null : 0);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/reagents/reagent_containers/hypospray.dm:181
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        label_text = span_notice(I18n.T("There is a sticker pasted onto the side which reads, 'WARNING: This medipen contains [], do not use if allergic to any listed chemicals.", pretty_string_from_reagent_list(((datum_reagents)reagents).reagent_list, names_only: TRUE, join_text: ", ", final_and: TRUE, capitalize_names: TRUE)));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/hypospray.dm:185
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(label_examine))
            Dm.append(ref dot, label_text);
        if (Dm.truthy(Dm.length((reagents as datum_reagents)?.reagent_list)))
            Dm.append(ref dot, span_notice(I18n.T("It is loaded.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("It is spent.")));
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/hypospray.dm:270
public partial class obj_item_reagent_containers_hypospray_medipen_tuberculosiscure : obj_item_reagent_containers_hypospray_medipen
{
    // tg: code/modules/reagents/reagent_containers/hypospray.dm:280
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        datum_reagents holder = (datum_reagents)reagents;
        if (holder.total_volume >= Dm.num(volume))
        {
            icon_state = (string)base_icon_state;
            return dot;
        }
        icon_state = Dm.text("[][]", base_icon_state, (holder.total_volume > 0) ? 1 : 0);
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/hypospray.dm:287
public partial class obj_item_reagent_containers_hypospray_medipen_survival : obj_item_reagent_containers_hypospray_medipen
{
    // tg: code/modules/reagents/reagent_containers/hypospray.dm:297
    public override async DmTask<object> inject(mob_living affected_mob = null, mob user = null)
    {
        if (Dm.truthy(lavaland_equipment_pressure_check(get_turf(user))))
        {
            amount_per_transfer_from_this = Dm.num(Dm.initial(this, (obj_item_reagent_containers_hypospray_medipen_survival p) => p.amount_per_transfer_from_this));
            return await base.inject(affected_mob, user);
        }

        if (Dm.truthy(DOING_INTERACTION(user, DOAFTER_SOURCE_SURVIVALPEN)))
        {
            to_chat(user, span_notice(I18n.T("You are too busy to use \\the []!", this)));
            return null;
        }

        to_chat(user, span_notice(I18n.T("You start manually releasing the low-pressure gauge...")));
        if (!Dm.truthy(await do_after(user, 10 * SECONDS, affected_mob, interaction_key: DOAFTER_SOURCE_SURVIVALPEN)))
            return null;

        amount_per_transfer_from_this = Dm.num(Dm.initial(this, (obj_item_reagent_containers_hypospray_medipen_survival p) => p.amount_per_transfer_from_this)) * 0.5f;
        return await base.inject(affected_mob, user);
    }
}

// tg: code/modules/reagents/reagent_containers/hypospray.dm:324
public partial class obj_item_reagent_containers_hypospray_medipen_survival_luxury_purple : obj_item_reagent_containers_hypospray_medipen_survival_luxury
{
    // tg: code/modules/reagents/reagent_containers/hypospray.dm:331
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (prob(0.1f)) // idea by melbert
        {
            name = I18n.T("luxury ourple medipen");
            desc = replacetext(desc, "purple", "ourple") as string; // i do not feel like copy-pasting the entire desc for a one-word change
        }
        return dot;
    }
}
