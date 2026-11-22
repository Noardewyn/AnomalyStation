// Translated from tgstation (snapshot 1d99bd7f): code/modules/cards/cardhand.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the screentips (add_context: not now, by the owner, D264) (syndicate G3d, D316).

// tg: code/modules/cards/cardhand.dm:1
public partial class obj_item_toy_cards_cardhand : obj_item_toy_cards
{
    private const int CARDS_MAX_DISPLAY_LIMIT = 5; // the amount of cards that are displayed in a hand
    private const int CARDS_PIXEL_X_OFFSET = -5; // start out displaying the 1st card -5 pixels left
    private const int CARDS_ANGLE_OFFSET = -45; // start out displaying the 1st card -45 degrees counter clockwise

    // tg: code/modules/cards/cardhand.dm:9
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        register_context();
        update_appearance();
        return dot;
    }

    // tg: code/modules/cards/cardhand.dm:15
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is slitting [] wrists with \\the []! It looks like [] [] a crummy hand!", user, user.p_their(), this, user.p_they(), user.p_have())));
        playsound(this, "sound/items/cards/cardshuffle.ogg", 50, TRUE);
        return BRUTELOSS;
    }

    // tg: code/modules/cards/cardhand.dm:20
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(span_notice(I18n.T("There are [] cards.", count_cards())));
        bool broadcast_check = false;
        foreach (obj_item_toy_singlecard card in Dm.each<obj_item_toy_singlecard>(fetch_card_atoms()))
        {
            if (Dm.truthy(user.is_holding(this)) || Dm.truthy(card.flipped))
            {
                dot.Add(span_notice(I18n.T("The hand contains a: []", card.cardname)));
                if (!Dm.truthy(card.flipped))
                    broadcast_check = true;
            }
            else if (HAS_TRAIT(user, TRAIT_XRAY_VISION))
                dot.Add(span_notice(I18n.T("You scan the cardhand with your x-ray vision and there is a: []", card.cardname)));
            object marked_color = card.getMarkedColor(user as mob_living_carbon);
            if (Dm.truthy(marked_color))
                dot.Add(span_notice(I18n.T("There is a [] mark on the corner of a card in the cardhand!", marked_color)));
        }
        if (broadcast_check)
            user.visible_message(span_notice(I18n.T("[] checks [] cards.", user, user.p_their())));
        return dot;
    }

    // add_context (:38): the screentips (not now, by the owner, D264)

    // tg: code/modules/cards/cardhand.dm:55
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (user is not mob_living living_user || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY | FORBID_TELEKINESIS_REACH)))
            return null;

        DmList handradial = Dm.list();
        foreach (obj_item_toy_singlecard card in Dm.each<obj_item_toy_singlecard>(fetch_card_atoms()))
            handradial[card] = Dm.image(icon: icon, icon_state: card.icon_state);

        obj_item_toy_singlecard choice = await show_radial_menu(usr, this, handradial, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), radius: 36, require_near: TRUE) as obj_item_toy_singlecard;
        if (choice == null)
            return FALSE;

        obj_item_toy_singlecard selected_card = await draw(living_user, choice);
        await selected_card.pickup(user);
        await user.put_in_hands(selected_card);

        if (count_cards() == 1)
        {
            await user.temporarilyRemoveItemFromInventory(this, TRUE);
            obj_item_toy_singlecard last_card = await draw(living_user);
            await last_card.pickup(user);
            await user.put_in_hands(last_card);
            qdel(this); // cardhand is empty now so delete it
        }
        return null;
    }

    // tg: code/modules/cards/cardhand.dm:78
    public virtual object check_menu(mob_living user = null)
    {
        return isliving(user) && !Dm.truthy(user.incapacitated) ? TRUE : FALSE;
    }

    // tg: code/modules/cards/cardhand.dm:81
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item_toy_singlecard card = null;
        bool interacting_with_deck = false;
        if (tool is obj_item_toy_singlecard tool_card)
            card = tool_card;

        if (tool is obj_item_toy_cards_deck dealer_deck)
        {
            if (!HAS_TRAIT(dealer_deck, TRAIT_WIELDED)) // recycle cardhand into deck (if unwielded)
            {
                if (dealer_deck.insert(this) != null)
                {
                    user.balloon_alert_to_viewers(I18n.T("puts card in deck"));
                    return ITEM_INTERACT_SUCCESS;
                }

                to_chat(user, span_warning(I18n.T("\\The [] is stacked too high!", dealer_deck)));
                return ITEM_INTERACT_BLOCKING;
            }

            card = dealer_deck.get_top_card(user);
            interacting_with_deck = true;
        }

        if (card == null)
            return NONE;

        if (insert(card) != null)
        {
            if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
                card.Flip();
            if (interacting_with_deck)
                await ((obj_item_toy_cards_deck)tool).draw(user);
            return ITEM_INTERACT_SUCCESS;
        }

        to_chat(user, span_warning(I18n.T("You can't hold any more cards in your hand!")));
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/cards/cardhand.dm:118
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        // This isn't strictly necessary, but this isn't expensive enough in reality
        DmList cards = fetch_card_atoms();

        cut_overlays();
        if (cards.len <= 1)
        {
            icon_state = null; // we want an error icon to appear if this doesn't get qdel
            return dot;
        }

        int starting_card_pos = (int)Math.Max(0, cards.len - CARDS_MAX_DISPLAY_LIMIT) + 1; // only display the top cards in the cardhand, +1 because list indexes start at 1
        int cards_to_display = (int)Math.Min(CARDS_MAX_DISPLAY_LIMIT, cards.len);
        // 90 degrees from the 1st card to the last, so split the divider by total cards displayed
        float angle_divider = round(90f / (cards_to_display - 1));
        // 10 pixels from the 1st card to the last, so split the divider by total cards displayed
        float pixel_divider = round(10f / (cards_to_display - 1));

        // starting from the 1st card to last, we want to slowly increase the angle and pixel_x offset
        // to spread the cards out using our dividers
        for (int i = 0; i <= cards_to_display - 1; i++)
        {
            obj_item_toy_singlecard card = cards[starting_card_pos + i] as obj_item_toy_singlecard;
            image card_overlay = Dm.image(icon: icon, icon_state: card.icon_state, pixel_x: CARDS_PIXEL_X_OFFSET + (i * pixel_divider));
            float rotation_angle = CARDS_ANGLE_OFFSET + (i * angle_divider);
            matrix M = matrix();
            M.Turn(rotation_angle);
            card_overlay.transform = M;
            add_overlay(card_overlay);
        }
        return dot;
    }
}
