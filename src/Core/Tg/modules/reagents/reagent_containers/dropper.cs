// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/dropper.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file (the dropper: drops in the eyes, transfer to and from containers, its fill overlay). The var
// block is generated.

// tg: code/modules/reagents/reagent_containers/dropper.dm:1
public partial class obj_item_reagent_containers_dropper : obj_item_reagent_containers
{
    // tg: code/modules/reagents/reagent_containers/dropper.dm:15
    public override async DmTask<object> interact_with_atom(atom target = null, mob_living user = null, DmList modifiers = null)
    {
        if (target.reagents == null)
            return NONE;
        datum_reagents holder = (datum_reagents)reagents;
        datum_reagents target_holder = (datum_reagents)target.reagents;

        if (holder.total_volume > 0)
        {
            if (Dm.truthy(target_holder.holder_full()))
            {
                to_chat(user, span_notice(I18n.T("[] is full.", target)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(target.is_injectable(user)))
            {
                to_chat(user, span_warning(I18n.T("You cannot transfer reagents to []!", target)));
                return ITEM_INTERACT_BLOCKING;
            }

            float trans = 0;
            float fraction = min(amount_per_transfer_from_this / holder.total_volume, 1);

            if (ismob(target))
            {
                user.changeNext_move(CLICK_CD_MELEE);
                if (ishuman(target))
                {
                    mob_living_carbon_human victim = (mob_living_carbon_human)target;

                    obj_item safe_thing = victim.is_eyes_covered() as obj_item;

                    if (safe_thing != null)
                    {
                        if (safe_thing.reagents == null)
                            safe_thing.create_reagents(100);

                        trans = round(Dm.num(await holder.trans_to(safe_thing, amount_per_transfer_from_this, transferred_by: user, methods: TOUCH)), CHEMICAL_VOLUME_ROUNDING);

                        target.visible_message(span_danger(I18n.T("[] tries to squirt something into []'s eyes, but fails!", user, target)),
                                                span_userdanger(I18n.T("[] tries to squirt something into your eyes, but fails!", user)));

                        if (Dm.truthy(trans))
                            to_chat(user, span_notice(I18n.T("You transfer [] unit\\s of the solution.", trans)));
                        update_appearance();
                        return ITEM_INTERACT_BLOCKING;
                    }
                }
                else if (isalien(target)) //hiss-hiss has no eyes!
                {
                    to_chat(target, span_danger(I18n.T("[] does not seem to have any eyes!", target)));
                    return ITEM_INTERACT_BLOCKING;
                }

                target.visible_message(
                    span_danger(I18n.T("[] squirts something into []'s eyes!", user, target)),
                    span_userdanger(I18n.T("[] squirts something into your eyes!", user))
                );

                if (Dm.truthy(target._listen_lookup?[COMSIG_MOB_REAGENTS_DROPPED_INTO_EYES]))
                    target._SendSignal(COMSIG_MOB_REAGENTS_DROPPED_INTO_EYES, Dm.list(target, user, this, holder, fraction));
                await holder.expose(target, TOUCH, fraction);
                mob M = (mob)target;
                log_combat(user, M, "squirted", holder.get_reagent_log_string());
            }

            trans = round(Dm.num(await holder.trans_to(target, amount_per_transfer_from_this, transferred_by: user)), CHEMICAL_VOLUME_ROUNDING);
            if (Dm.truthy(trans))
                to_chat(user, span_notice(I18n.T("You transfer [] unit\\s of the solution.", trans)));
            playsound(this, Dm.resource("sound/effects/droplet.ogg"), 70, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            update_appearance();
            target.update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(target.is_drawable(user, FALSE))) //No drawing from mobs here
        {
            to_chat(user, span_warning(I18n.T("You cannot directly remove reagents from []!", target)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(target_holder.total_volume))
        {
            to_chat(user, span_warning(I18n.T("[] is empty!", target)));
            return ITEM_INTERACT_BLOCKING;
        }

        float trans__1 = round(Dm.num(await target_holder.trans_to(this, amount_per_transfer_from_this, transferred_by: user)), CHEMICAL_VOLUME_ROUNDING);

        if (Dm.truthy(trans__1))
            to_chat(user, span_notice(I18n.T("You fill [] with [] unit\\s of the solution.", this, trans__1)));

        update_appearance();
        target.update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/dropper.dm:87
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(((datum_reagents)reagents).total_volume))
            return dot;
        mutable_appearance filling = (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/medical/reagent_fillings.dmi"), "dropper");
        filling.color = mix_color_from_reagents(((datum_reagents)reagents).reagent_list);
        Dm.append(ref dot, filling);
        return dot;
    }
}
