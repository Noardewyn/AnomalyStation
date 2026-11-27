// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/medigel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/reagents/reagent_containers/medigel.dm:30
public partial class obj_item_reagent_containers_medigel : obj_item_reagent_containers
{
    // tg: code/modules/reagents/reagent_containers/medigel.dm:58
    public override async DmTask setup_reskins()
    {
        if (icon_state == "medigel") // oh yeah baby raw icon state check to make sure we can't reskin preset gels
            await AddComponent(typeof(datum_component_reskinable_item), typeof(datum_atom_skin_med_gel));
    }

    // tg: code/modules/reagents/reagent_containers/medigel.dm:62
    public override void mode_change_message(mob user = null)
    {
        bool squirt_mode = Dm.eq(amount_per_transfer_from_this, Dm.initial(this, nameof(amount_per_transfer_from_this)));
        to_chat(user, span_notice(I18n.T("You will now apply the medigel's contents in []. You'll now use [] units per use.", squirt_mode ? "extended sprays" : "short bursts", amount_per_transfer_from_this)));
    }

    // tg: code/modules/reagents/reagent_containers/medigel.dm:66
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return NONE;
        datum_reagents holder = reagents as datum_reagents;
        if (holder == null || !Dm.truthy(holder.total_volume))
        {
            to_chat(user, span_warning(I18n.T("[] is empty!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        user.changeNext_move(CLICK_CD_MELEE);
        if (interacting_with == user)
        {
            interacting_with.visible_message(span_notice(I18n.T("[] attempts to [] [] on []self.", user, apply_method, this, user.p_them())));
            if (Dm.truthy(self_delay))
            {
                if (!Dm.truthy(await do_after(user, self_delay, interacting_with)))
                    return ITEM_INTERACT_BLOCKING;
                holder = reagents as datum_reagents;
                if (holder == null || !Dm.truthy(holder.total_volume))
                    return ITEM_INTERACT_BLOCKING;
            }
            to_chat(interacting_with, span_notice(I18n.T("You [] yourself with [].", apply_method, this)));
        }
        else
        {
            log_combat(user, interacting_with, "attempted to apply", this, holder.get_reagent_log_string());
            interacting_with.visible_message(
                span_danger(I18n.T("[] attempts to [] [] on [].", user, apply_method, this, interacting_with)),
                span_userdanger(I18n.T("[] attempts to [] [] on you.", user, apply_method, this)));
            if (!Dm.truthy(await do_after(user, CHEM_INTERACT_DELAY(3 * SECONDS, user), interacting_with)))
                return ITEM_INTERACT_BLOCKING;
            holder = reagents as datum_reagents;
            if (holder == null || !Dm.truthy(holder.total_volume))
                return ITEM_INTERACT_BLOCKING;
            interacting_with.visible_message(
                span_danger(I18n.T("[] []s [] down with [].", user, apply_method, interacting_with, this)),
                span_userdanger(I18n.T("[] []s you down with [].", user, apply_method, this)));
        }

        log_combat(user, interacting_with, "applied", this, holder.get_reagent_log_string());
        playsound(this, Dm.resource("sound/effects/spray.ogg"), 30, TRUE, -6);
        await holder.trans_to(interacting_with, amount_per_transfer_from_this, transferred_by: user, methods: apply_type);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/reagents/reagent_containers/medigel.dm:121
public partial class obj_item_reagent_containers_medigel_synthflesh : obj_item_reagent_containers_medigel
{
    // tg: code/modules/reagents/reagent_containers/medigel.dm:121
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.num((reagents as datum_reagents)?.total_volume) >= 60)
            dot.Add(span_info(I18n.T("One full bottle can restore a corpse husked by burns.")));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/medigel.dm:126
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (iscarbon(interacting_with) && Dm.truthy((reagents as datum_reagents)?.total_volume))
        {
            mob_living_carbon carbies = interacting_with as mob_living_carbon;
            if (HAS_TRAIT_FROM(carbies, TRAIT_HUSK, BURN) && Dm.num(carbies.get_fire_loss()) > UNHUSK_DAMAGE_THRESHOLD * 2.5f)
            {
                // give them a warning if the mob is a husk but synthflesh won't unhusk yet
                carbies.visible_message(span_boldwarning(I18n.T("[]'s burns need to be repaired first before synthflesh will unhusk it!", carbies)));
            }
        }
        return await base.interact_with_atom(interacting_with, user, modifiers);
    }
}
