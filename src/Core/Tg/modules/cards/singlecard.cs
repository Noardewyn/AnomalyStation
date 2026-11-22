// Translated from tgstation (snapshot 1d99bd7f): code/modules/cards/singlecard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the screentips (add_context: not now, by the owner, D264) (syndicate G3d, D316). The var block is generated (D111).

// tg: code/modules/cards/singlecard.dm:1
public partial class obj_item_toy_singlecard : obj_item_toy
{
    // tg: code/modules/cards/singlecard.dm:31
    [DmParams("mapload", "cardname", "parent_deck")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object new_cardname = Dm.arg(__extra, 0);
        object parent_deck_arg = Dm.arg(__extra, 1);
        cardname = Dm.truthy(new_cardname) ? Dm.str(new_cardname) : cardname;
        if (parent_deck_arg is obj_item_toy_cards_deck parent_deck)
        {
            deckstyle = parent_deck.deckstyle;
            has_unique_card_icons = parent_deck.has_unique_card_icons;
            icon_state = Dm.text("singlecard_down_[]", parent_deck.deckstyle);
            hitsound = parent_deck.hitsound;
            force = parent_deck.force;
            throwforce = parent_deck.throwforce;
            throw_speed = parent_deck.throw_speed;
            throw_range = parent_deck.throw_range;
            attack_verb_continuous = parent_deck.attack_verb_continuous;
            attack_verb_simple = parent_deck.attack_verb_simple;

            if (parent_deck.holodeck != null)
            {
                flags_1 = Dm.bitor(Dm.num(flags_1), HOLOGRAM_1);
                parent_deck.holodeck.spawned.Add(this);
            }
        }
        if (Dm.truthy(mapload))
            //maploaded card needs to be faceup anyways, and doing this will give it its name and appearance properly
            Flip(CARD_FACEUP);

        register_context();
        return dot;
    }

    // tg: code/modules/cards/singlecard.dm:56
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!ishuman(user))
            return dot;

        if (Dm.truthy(user.is_holding(this)))
        {
            user.visible_message(span_notice(I18n.T("[] checks [] card.", user, user.p_their())), span_notice(I18n.T("The card reads: [].", cardname)));
            if (Dm.truthy(blank))
                dot.Add(span_notice(I18n.T("The card is blank. Write on it with a pen.")));
        }
        else if (HAS_TRAIT(user, TRAIT_XRAY_VISION))
            dot.Add(span_notice(I18n.T("You scan the card with your x-ray vision and it reads: [].", cardname)));
        else
            dot.Add(span_warning(I18n.T("You need to have the card in your hand to check it!")));

        object marked = getMarkedColor(user as mob_living_carbon);
        if (Dm.truthy(marked))
            dot.Add(span_notice(I18n.T("The card has a [] mark on the corner!", marked)));
        return dot;
    }

    // add_context (:74): the screentips (not now, by the owner, D264)

    // tg: code/modules/cards/singlecard.dm:104
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is slitting [] wrists with \\the []! It looks like [] [] an unlucky card!", user, user.p_their(), this, user.p_they(), user.p_have())));
        playsound(this, "sound/items/weapons/bladeslice.ogg", 50, TRUE);
        return BRUTELOSS;
    }

    /**
     * Flips the card over
     *
     * * Arguments:
     * * is_face_up (optional) - Sets flipped state to CARD_FACEDOWN or CARD_FACEUP if given (otherwise just invert the flipped state)
     */
    // tg: code/modules/cards/singlecard.dm:115
    public virtual void Flip(object is_face_up = null)
    {
        if (is_face_up != null)
            flipped = Dm.num(is_face_up);
        else
            flipped = Dm.truthy(flipped) ? FALSE : TRUE;

        name = Dm.truthy(flipped) ? cardname : "card";
        update_appearance();
    }

    /**
     * Returns a color if a card is marked and the user can see it
     *
     * * Arguments:
     * * user - We need to check if the user see the marked card
     */
    // tg: code/modules/cards/singlecard.dm:130
    public virtual object getMarkedColor(mob_living_carbon user = null)
    {
        if (user == null)
            return null;
        bool is_marked_with_visible_color = Dm.truthy(marked_color) && !Dm.eq(marked_color, "invisible");
        if (is_marked_with_visible_color || (Dm.eq(marked_color, "invisible") && HAS_TRAIT(user, TRAIT_REAGENT_SCANNER)))
            return marked_color;
        return null;
    }

    // tg: code/modules/cards/singlecard.dm:137
    public override DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(flipped))
            icon_state = Dm.text("singlecard_down_[]", deckstyle);
        else if (Dm.truthy(has_unique_card_icons)) // each card in a deck has a different icon
            icon_state = Dm.text("sc_[]_[]", cardname, deckstyle);
        else // all cards are the same icon state (blank or scribble)
            icon_state = Dm.truthy(blank) ? Dm.text("sc_blank_[]", deckstyle) : Dm.text("sc_scribble_[]", deckstyle);
        return base.update_icon_state(__extra);
    }

    // tg: code/modules/cards/singlecard.dm:146
    public override object update_name(float? updates = null)
    {
        name = Dm.truthy(flipped) ? cardname : "card";
        return base.update_name(updates);
    }

    // tg: code/modules/cards/singlecard.dm:150
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item_toy_singlecard card = null;

        if (tool is obj_item_toy_cards_deck dealer_deck)
        {
            if (!HAS_TRAIT(dealer_deck, TRAIT_WIELDED)) // recycle card into deck (if unwielded)
            {
                if (dealer_deck.insert(this) != null)
                {
                    user.balloon_alert_to_viewers(I18n.T("puts card in deck"));
                    return ITEM_INTERACT_SUCCESS;
                }

                to_chat(user, span_warning(I18n.T("\\The [] is stacked too high!", dealer_deck)));
                return ITEM_INTERACT_BLOCKING;
            }

            card = await dealer_deck.draw(user);
        }

        if (tool is obj_item_toy_singlecard tool_card)
            card = tool_card;

        if (card != null) // card + card = combine into cardhand
        {
            if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
                card.Flip();

            if (tool is obj_item_toy_cards_deck)
                // only decks cause a balloon alert
                user.balloon_alert_to_viewers(I18n.T("deals a card"));

            obj_item_toy_cards_cardhand new_cardhand = Dm.@new<obj_item_toy_cards_cardhand>(drop_location());
            new_cardhand.pixel_x = pixel_x;
            new_cardhand.pixel_y = pixel_y;
            new_cardhand.insert(this);
            new_cardhand.insert(card);

            if (!isturf(loc)) // make a cardhand in our active hand
            {
                await user.temporarilyRemoveItemFromInventory(this, TRUE);
                await new_cardhand.pickup(user);
                await user.put_in_active_hand(new_cardhand);
            }
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_toy_cards_cardhand) // insert into cardhand
            return await tool.item_interaction(user, this, modifiers);

        object marked_cheating_color = null;

        if (tool is obj_item_pen pen)
            marked_cheating_color = Dm.eq(pen.colour, "white") ? "invisible" : pen.colour;

        if (tool is obj_item_toy_crayon crayon)
            marked_cheating_color = Dm.eq(crayon.crayon_color, "mime") ? "invisible" : crayon.crayon_color;

        if (Dm.truthy(marked_cheating_color) && !Dm.truthy(blank) && IS_WRITING_UTENSIL(tool)) // You cheated not only the game, but yourself
        {
            marked_color = marked_cheating_color;
            to_chat(user, span_notice(I18n.T("You put a [] mark in the corner of [] with the []. Cheat to win!", marked_color, this, tool)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(await user.can_write(tool)))
            return NONE;

        object cardtext = await stripped_input(user, "What do you wish to write on the card?", "Card Writing", "", 50);
        if (!Dm.truthy(cardtext) || !Dm.truthy(await user.can_perform_action(this)))
            return ITEM_INTERACT_BLOCKING;

        cardname = Dm.str(cardtext);
        blank = FALSE;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/cards/singlecard.dm:218
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        await attack_self(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/cards/singlecard.dm:222
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!ishuman(user) || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY | FORBID_TELEKINESIS_REACH)))
            return null;

        Flip();
        if (isturf(loc)) // only display this message when flipping in a visible spot like on a table
            user.balloon_alert_to_viewers(I18n.T("flips a card"));
        return null;
    }

    // tg: code/modules/cards/singlecard.dm:230
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        transform = Dm.turn(transform, 90);
        // use the simple_rotation component to make this turn with Alt+RMB & Alt+LMB at some point in the future - TimT
        return CLICK_ACTION_SUCCESS;
    }
}
