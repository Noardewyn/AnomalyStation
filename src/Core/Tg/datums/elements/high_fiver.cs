// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/high_fiver.dm (M9 slice F: the slapper's high-five)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * # High Fiver Element
 *
 * Attach to an item to make it offer a "high five" when offered to people
 */
// tg: code/datums/elements/high_fiver.dm:6
public partial class datum_element_high_fiver : datum_element
{
    // tg: code/datums/elements/high_fiver.dm:8
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_ITEM_OFFERING, nameof(on_offer));
        RegisterSignal(target, COMSIG_ITEM_OFFER_TAKEN, nameof(on_offer_taken));
        return dot;
    }

    // tg: code/datums/elements/high_fiver.dm:16
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_ITEM_OFFERING, COMSIG_ITEM_OFFER_TAKEN));
        return dot;
    }

    /// Signal proc for [COMSIG_ITEM_OFFERING] to set up the high-five on offer
    // tg: code/datums/elements/high_fiver.dm:21
    [SignalHandler]
    public virtual object on_offer(obj_item source = null, mob_living offerer = null)
    {
        offerer.visible_message(
            span_notice(I18n.T("[] raises [] arm, looking for a high-five!", offerer, offerer.p_their())),
            span_notice(I18n.T("You post up, looking for a high-five!")),
            vision_distance: 2);
        Waiting.NoSleep(offerer.apply_status_effect(typeof(datum_status_effect_offering_no_item_received_high_five), source, typeof(atom_movable_screen_alert_give_highfive)));

        return COMPONENT_OFFER_INTERRUPT;
    }

    /// Signal proc for [COMSIG_ITEM_OFFER_TAKEN] to continue through with the high-five on take
    // tg: code/datums/elements/high_fiver.dm:34
    [SignalHandler]
    public virtual object on_offer_taken(obj_item source = null, mob_living offerer = null, mob_living taker = null)
    {
        float open_hands_taker = 0;
        float slappers_giver = 0;
        // see how many hands the taker has open for high'ing
        foreach (object hand in Dm.each(taker.held_items))
        {
            if (hand == null)
                open_hands_taker++;
        }

        // see how many hands the offerer is using for high'ing
        foreach (obj_item slap_check in Dm.each<obj_item>(offerer.held_items))
        {
            if (Dm.truthy(Dm.bitand(slap_check.item_flags, HAND_ITEM)))
                slappers_giver++;
        }

        bool high_ten = slappers_giver >= 2;
        string descriptor = Dm.text("high-[]", high_ten ? "ten" : "five");

        if (open_hands_taker <= 0)
        {
            to_chat(taker, span_warning(I18n.T("You can't [] [] with no open hands!", descriptor, offerer)));
            Waiting.NoSleep(taker.add_mood_event(descriptor, typeof(datum_mood_event_high_five_full_hand))); // not so successful now!
            return COMPONENT_OFFER_INTERRUPT;
        }

        playsound(offerer, Dm.resource("sound/items/weapons/slap.ogg"), min(50 * slappers_giver, 300), TRUE, 1);
        offerer.add_mob_memory(typeof(datum_memory_high_five), Dm.named("deuteragonist", taker), Dm.named("high_five_type", descriptor), Dm.named("high_ten", high_ten ? TRUE : FALSE));
        taker.add_mob_memory(typeof(datum_memory_high_five), Dm.named("deuteragonist", offerer), Dm.named("high_five_type", descriptor), Dm.named("high_ten", high_ten ? TRUE : FALSE));

        if (high_ten)
        {
            to_chat(taker, span_nicegreen(I18n.T("You give high-tenning [] your all!", offerer)));
            offerer.visible_message(
                span_notice(I18n.T("[] enthusiastically high-tens []!", taker, offerer)),
                span_nicegreen(I18n.T("Wow! You're high-tenned []!", taker)),
                span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
                ignored_mobs: taker);

            Waiting.NoSleep(offerer.add_mood_event(descriptor, typeof(datum_mood_event_high_ten)));
            Waiting.NoSleep(taker.add_mood_event(descriptor, typeof(datum_mood_event_high_ten)));
        }
        else
        {
            to_chat(taker, span_nicegreen(I18n.T("You high-five []!", offerer)));
            offerer.visible_message(
                span_notice(I18n.T("[] high-fives []!", taker, offerer)),
                span_nicegreen(I18n.T("All right! You're high-fived by []!", taker)),
                span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
                ignored_mobs: taker);

            Waiting.NoSleep(offerer.add_mood_event(descriptor, typeof(datum_mood_event_high_five)));
            Waiting.NoSleep(taker.add_mood_event(descriptor, typeof(datum_mood_event_high_five)));
        }

        offerer.remove_status_effect(typeof(datum_status_effect_offering_no_item_received_high_five));
        return COMPONENT_OFFER_INTERRUPT;
    }
}
