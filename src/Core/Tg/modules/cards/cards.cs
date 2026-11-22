// Translated from tgstation (snapshot 1d99bd7f): code/modules/cards/cards.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3d, D316: the uplink's suspicious deck of cards). The var blocks are generated (D111).

/*
** The base card class that is used by decks and cardhands
*/
// tg: code/modules/cards/cards.dm:4
public partial class obj_item_toy_cards : obj_item_toy
{
    // tg: code/modules/cards/cards.dm:21
    public override object Destroy(float? force = null)
    {
        if (card_atoms != null)
            QDEL_LIST(card_atoms);
        return base.Destroy(force);
    }

    /// This is how we play 52 card pickup
    // tg: code/modules/cards/cards.dm:27
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (Dm.truthy(dot) || hit_atom is not mob_living target) // was it caught or is the target not a living mob
            return dot;

        if (throwingdatum?.get_thrower() is not mob) // if a mob didn't throw it (need two people to play 52 pickup)
            return dot;

        if (count_cards() == 0)
            return dot;

        if (!Dm.truthy(can_play_52_card_pickup))
            return dot;

        foreach (obj_item_toy_singlecard card in Dm.each<obj_item_toy_singlecard>(fetch_card_atoms()))
        {
            card_atoms.Remove(card);
            Waiting.NoSleep(card.forceMove(target.drop_location() as atom));
            if (prob(50))
                card.Flip();
            card.pixel_x = rand(-16, 16);
            card.pixel_y = rand(-16, 16);
            matrix Matrix = matrix();
            object angle = pick(0, 90, 180, 270);
            Matrix.Turn(Dm.num(angle));
            card.transform = Matrix;
            card.update_appearance();
        }

        playsound(this, "sound/items/cards/cardshuffle.ogg", 50, TRUE);

        if (this is obj_item_toy_cards_cardhand)
        {
            qdel(this);
            return dot;
        }

        update_appearance();
        return dot;
    }

    /**
     * This is used to insert a list of cards into a deck or cardhand
     *
     * All cards that are inserted have their angle and pixel offsets reset to zero however their
     * flip state does not change unless it's being inserted into a deck which is always facedown
     * (see the deck/insert proc)
     * Returns the list of inserted cards
     *
     * Arguments:
     * * card_item - Either a singlecard or cardhand that gets inserted into the src
     */
    // tg: code/modules/cards/cards.dm:74
    public virtual DmList insert(obj_item_toy card_item = null)
    {
        fetch_card_atoms();
        // Can't add any cards, don't do anything
        if (count_cards() >= card_limit)
            return null;

        DmList cards_to_add = Dm.list();

        if (card_item is obj_item_toy_singlecard)
            cards_to_add.Add(card_item);

        if (card_item is obj_item_toy_cards_cardhand recycled)
            cards_to_add.Add(recycled.fetch_card_atoms());

        if (cards_to_add.len + count_cards() > card_limit)
            // Remove all cards past however many we can fit
            cards_to_add.Cut(card_limit - count_cards() + 1, cards_to_add.len + 1);

        foreach (obj_item_toy_singlecard card in Dm.each<obj_item_toy_singlecard>(cards_to_add))
        {
            Waiting.NoSleep(card.forceMove(this));
            // reset the position and angle
            card.pixel_x = 0;
            card.pixel_y = 0;
            matrix M = matrix();
            M.Turn(0);
            card.transform = M;
            card.update_appearance();
            card_atoms.Add(card);
        }

        if (card_item is obj_item_toy_cards_cardhand recycled_cardhand)
        {
            recycled_cardhand.card_atoms.Remove(cards_to_add);
            if (recycled_cardhand.fetch_card_atoms().len == 0)
                qdel(card_item);
        }

        update_appearance();
        return cards_to_add;
    }

    /**
     * Draws a card from the deck or hand of cards.
     *
     * Draws the top card, removing it from the associated card atoms list,
     * unless a card arg is supplied; then it picks that specific card, removes it from the
     * associated card atoms list, and returns it (the card arg is used by the radial menu for cardhands to select
     * specific cards out of the cardhand).
     * Arguments:
     * * mob/living/user - The user drawing the card.
     * * obj/item/toy/singlecard/card (optional) - The card drawn from the hand
    **/
    // tg: code/modules/cards/cards.dm:124
    public virtual async DmTask<obj_item_toy_singlecard> draw(mob_living user = null, obj_item_toy_singlecard card = null)
    {
        if (!isliving(user) || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY | FORBID_TELEKINESIS_REACH)))
            return null;

        if (count_cards() == 0)
        {
            to_chat(user, span_warning(I18n.T("There are no more cards to draw!")));
            return null;
        }

        DmList cards = fetch_card_atoms();

        card ??= pick_card(user, cards);
        cards.Remove(card);

        update_appearance();
        playsound(this, "sound/items/cards/cardflip.ogg", 50, TRUE);
        return card;
    }

    /// Picks what card the user draws from the deck
    // tg: code/modules/cards/cards.dm:142
    public virtual obj_item_toy_singlecard pick_card(mob_living user = null, DmList cards = null)
    {
        // By default just pick the top card
        return cards[1] as obj_item_toy_singlecard;
    }

    /// Returns the cards in this deck.
    /// Lazily generates the cards if they haven't already been made.
    // tg: code/modules/cards/cards.dm:148
    public virtual DmList fetch_card_atoms()
    {
        if (card_atoms != null)
            return card_atoms;

        card_atoms = Dm.list();

        foreach (object initial_card in Dm.each(initial_cards))
        {
            if (istext(initial_card))
            {
                card_atoms.Add(Dm.@new<obj_item_toy_singlecard>(this, initial_card, this));
                continue;
            }

            datum_deck_card deck_card = (initial_card is Type card_type ? Dm.@new(card_type) : initial_card) as datum_deck_card;
            if (deck_card == null)
            {
                stack_trace(Dm.text("[] (created in []) must either be a /datum/deck_card, or a /datum/deck_card path", initial_card, type), "code/modules/cards/cards.dm", 163);
                continue;
            }

            card_atoms.Add(deck_card.create_card(this));
        }

        return card_atoms;
    }

    /// Returns the number of cards in the deck.
    /// Avoids creating any cards if it is unnecessary.
    // tg: code/modules/cards/cards.dm:172
    public virtual float count_cards()
    {
        return card_atoms == null ? initial_cards.len : LAZYLEN(card_atoms);
    }
}

/// A basic interface for creating a card for a deck that isn't just a name.
// tg: code/modules/cards/cards.dm:176
public partial class datum_deck_card : datum
{
    // tg: code/modules/cards/cards.dm:183
    [DmParams("name")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        object new_name = Dm.arg(__extra, 0);
        if (new_name != null)
            name = new_name;
    }

    /// Creates a card for the given deck
    // tg: code/modules/cards/cards.dm:188
    public virtual object create_card(obj_item_toy_cards deck = null)
    {
        object card = Dm.@new(path, deck, name, deck);
        update_card(card as obj_item_toy_singlecard);
        return card;
    }

    // tg: code/modules/cards/cards.dm:193
    public virtual void update_card(obj_item_toy_singlecard card = null)
    {
        throw Dm.CRASH(Dm.text("[] does not implement update_card. If you just want a name, use a string instead.", type), "code/modules/cards/cards.dm", 194);
    }
}

/// A /datum/deck_card that just creates a card of the given type
// tg: code/modules/cards/cards.dm:197
public partial class datum_deck_card_of_type : datum_deck_card
{
    // tg: code/modules/cards/cards.dm:199
    [DmParams("path")]
    public override DmTask New(params object[] __extra)
    {
        path = Dm.arg(__extra, 0) as Type;
        return DmTask.CompletedTask;
    }

    // tg: code/modules/cards/cards.dm:202
    public override object create_card(obj_item_toy_cards deck = null)
    {
        return Dm.@new(path, deck);
    }
}
