// Translated from tgstation (snapshot 1d99bd7f): code/modules/cards/deck/deck.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the screentips (add_context: not now, by the owner, D264) (syndicate G3d, D316: the uplink's suspicious deck of
// cards). The var blocks are generated (D111).

// tg: code/modules/cards/deck/deck.dm:4
public partial class obj_item_toy_cards_deck : obj_item_toy_cards
{
    // tg: code/modules/cards/deck/deck.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_drag_pickup)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("attacksound", Dm.resource("sound/items/cards/cardflip.ogg"))));
        register_context();

        initialize_cards();

        if (initial_cards.len > 0)
            card_limit = initial_cards.len;
        return dot;
    }

    /// Generates the cards in this deck. If not overridden, generates a standard 52-card deck (and 2 jokers).
    // tg: code/modules/cards/deck/deck.dm:44
    public virtual void initialize_cards()
    {
        initial_cards.Add("Joker Clown");
        initial_cards.Add("Joker Mime");
        foreach (string suit in new[] { "Hearts", "Spades", "Clubs", "Diamonds" })
        {
            initial_cards.Add(Dm.text("Ace of []", suit));
            for (int i = 2; i <= 10; i++)
                initial_cards.Add(Dm.text("[] of []", i, suit));
            foreach (string person in new[] { "Jack", "Queen", "King" })
                initial_cards.Add(Dm.text("[] of []", person, suit));
        }
    }

    // tg: code/modules/cards/deck/deck.dm:55
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is slitting [] wrists with \\the []! It looks like their luck ran out!", user, user.p_their(), this)));
        playsound(this, "sound/items/cards/cardshuffle.ogg", 50, TRUE);
        return BRUTELOSS;
    }

    // tg: code/modules/cards/deck/deck.dm:60
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;

        if (HAS_TRAIT(user, TRAIT_XRAY_VISION) && count_cards() > 0)
            dot.Add(span_notice(I18n.T("You scan the deck with your x-ray vision and the top card reads: [].", (fetch_card_atoms()[1] as obj_item_toy_singlecard).cardname)));

        // This can only happen if card_atoms have been generated
        if (LAZYLEN(card_atoms) > 0)
        {
            obj_item_toy_singlecard card = fetch_card_atoms()[1] as obj_item_toy_singlecard;

            object marked_color = card.getMarkedColor(user as mob_living_carbon);
            if (Dm.truthy(marked_color))
                dot.Add(span_notice(I18n.T("The top card of the deck has a [] mark on the corner!", marked_color)));
        }

        dot.Add(span_notice(I18n.T("Click and drag the deck to yourself to pickup."))); // This should be a context screentip
        return dot;
    }

    // add_context (:76): the screentips (not now, by the owner, D264)

    /**
     * Shuffles the cards in the deck
     *
     * Arguments:
     * * user - The person shuffling the cards.
     */
    // tg: code/modules/cards/deck/deck.dm:105
    public virtual void shuffle_cards(mob_living user = null)
    {
        if (!(shuffle_cooldown <= Dm.num(world.time))) // COOLDOWN_FINISHED
            return;
        shuffle_cooldown = Dm.num(world.time) + shuffle_time; // COOLDOWN_START
        shuffle_inplace(fetch_card_atoms());
        playsound(this, "sound/items/cards/cardshuffle.ogg", 50, TRUE);
        user.balloon_alert_to_viewers(I18n.T("shuffles the deck"));
        _addtimer(Dm.@new<datum_callback>(this, nameof(CardgameEvent), user), 60 * SECONDS, TIMER_OVERRIDE | TIMER_UNIQUE, file: "code/modules/cards/deck/deck.dm", line: 114);
    }

    /**
     * Grabs the top card in the deck, but does not actually manipulate the deck.
     */
    // tg: code/modules/cards/deck/deck.dm:117
    public virtual obj_item_toy_singlecard get_top_card(mob_living user = null)
    {
        if (count_cards() == 0)
        {
            to_chat(user, span_warning(I18n.T("There are no more cards to draw!")));
            return null;
        }
        DmList cards = fetch_card_atoms();
        return cards[1] as obj_item_toy_singlecard;
    }

    /// This checks if nearby mobs are playing a cardgame and triggers a mood and memory
    // tg: code/modules/cards/deck/deck.dm:125
    public virtual async DmTask CardgameEvent(mob_living dealer = null)
    {
        DmList card_players = Dm.list();
        foreach (mob_living_carbon person in Dm.each<mob_living_carbon>(viewers(COMBAT_MESSAGE_RANGE, loc)))
        {
            object held_card_item = person.is_holding_item_of_type(typeof(obj_item_toy_singlecard));
            if (!Dm.truthy(held_card_item))
                held_card_item = person.is_holding_item_of_type(typeof(obj_item_toy_cards_deck));
            if (!Dm.truthy(held_card_item))
                held_card_item = person.is_holding_item_of_type(typeof(obj_item_toy_cards_cardhand));
            if (Dm.truthy(held_card_item))
                card_players[person] = held_card_item;
        }

        if (card_players.len >= 2) // need at least 2 people to play a cardgame, duh!
        {
            foreach (mob_living_carbon player in Dm.each<mob_living_carbon>(card_players))
            {
                DmList other_players = card_players.Copy();
                other_players.Remove(player);
                object held_card_item = card_players[player];

                await player.add_mood_event("playing_cards", typeof(datum_mood_event_playing_cards));
                player.add_mob_memory(
                    typeof(datum_memory_playing_cards),
                    Dm.named("deuteragonist", dealer),
                    Dm.named("game", cardgame_desc),
                    Dm.named("protagonist_held_card", held_card_item),
                    Dm.named("other_players", other_players));
            }
        }
    }

    // tg: code/modules/cards/deck/deck.dm:146
    [DmParams("user", "modifiers", "flip_card")]
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        bool flip_card = Dm.truthy(Dm.arg(__extra, 0));
        if (!ishuman(user) || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY | FORBID_TELEKINESIS_REACH)))
            return null;

        obj_item_toy_singlecard card = await draw(user as mob_living);
        if (card == null)
            return null;
        if (flip_card)
            card.Flip();
        await card.pickup(user);
        await user.put_in_hands(card);
        user.balloon_alert_to_viewers(I18n.T("draws a card"));
        return null;
    }

    // tg: code/modules/cards/deck/deck.dm:159
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        await attack_hand(user, modifiers, TRUE);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/cards/deck/deck.dm:163
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!HAS_TRAIT(this, TRAIT_WIELDED))
        {
            to_chat(user, span_notice(I18n.T("You must hold the [] with both hands to shuffle.", this)));
            return CLICK_ACTION_BLOCKING;
        }

        shuffle_cards(user as mob_living);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/cards/deck/deck.dm:171
    public override DmTask<object> update_icon_state(params object[] __extra)
    {
        float count = count_cards();
        if (count >= 27)
            icon_state = Dm.text("[][]_full", deck_base_icon, deckstyle);
        else if (count >= 11)
            icon_state = Dm.text("[][]_half", deck_base_icon, deckstyle);
        else if (count >= 1)
            icon_state = Dm.text("[][]_low", deck_base_icon, deckstyle);
        else
            icon_state = Dm.text("[][]_empty", deck_base_icon, deckstyle);
        return base.update_icon_state(__extra);
    }

    // tg: code/modules/cards/deck/deck.dm:183
    public override DmList insert(obj_item_toy card_item = null)
    {
        DmList cards = base.insert(card_item);
        if (LAZYLEN(cards) == 0)
            return null;
        // any card inserted into the deck is always facedown
        foreach (obj_item_toy_singlecard card in Dm.each<obj_item_toy_singlecard>(cards))
            card.Flip(CARD_FACEDOWN);
        return cards;
    }

    // tg: code/modules/cards/deck/deck.dm:192
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_toy_singlecard && tool is not obj_item_toy_cards_cardhand)
            return NONE;

        if (insert(tool as obj_item_toy) == null)
        {
            to_chat(user, span_warning(I18n.T("\\The [] is stacked too high!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        string card_grammar = tool is obj_item_toy_singlecard ? "card" : "cards";
        user.balloon_alert_to_viewers(I18n.T("puts [] in deck", card_grammar));
        return ITEM_INTERACT_SUCCESS;
    }

    /// This is how we play 52 card pickup
    // tg: code/modules/cards/deck/deck.dm:205
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (Dm.truthy(dot) || hit_atom is not mob_living target) // was it caught or is the target not a living mob
            return dot;

        if (throwingdatum?.get_thrower() is not mob_living thrower) // if a mob didn't throw it (need two people to play 52 pickup)
            return dot;

        target.visible_message(span_warning(I18n.T("[] is forced to play 52 card pickup!", target)), span_warning(I18n.T("You are forced to play 52 card pickup.")));
        Waiting.NoSleep(target.add_mood_event("lost_52_card_pickup", typeof(datum_mood_event_lost_52_card_pickup)));
        Waiting.NoSleep(thrower.add_mood_event("won_52_card_pickup", typeof(datum_mood_event_won_52_card_pickup)));
        add_memory_in_range(target, 7, typeof(datum_memory_playing_card_pickup), Dm.named("protagonist", thrower), Dm.named("deuteragonist", target), Dm.named("antagonist", this));
        return dot;
    }
}

/// A card shoe. For holding a lot of cards, but not for committing card-based psychological warfare (crashing people by throwing 100+ cards at them).
// tg: code/modules/cards/deck/deck.dm:237
public partial class obj_item_toy_cards_deck_cardshoe : obj_item_toy_cards_deck
{
    // tg: code/modules/cards/deck/deck.dm:247
    public override void initialize_cards()
    {
    }
}
