// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tcg/tcg.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the trading cards (their faces from SStrading_card_game's card datums, tapping, flipping, the radial menu), the
// card piles (drawing, shuffling, flipping), the packs with their rarity odds, the flipper coin, the card binder and the card datum
// filled from tg's strings/tcg sets. The cards' pictures in the examine name chaser are chat icons (getFlatIcon, icon2html), a
// deferred leaf. The var blocks are generated (D111) but for the card datum's numbers, which tg's sets give as text or numbers.

// tg: code/game/objects/items/tcg/tcg.dm:4
public partial class obj_item_tcgcard : obj_item
{
    // tg: code/game/objects/items/tcg/tcg.dm:1-2
    private const float TAPPED_ANGLE = 90;
    private const float UNTAPPED_ANGLE = 0;

    // tg: code/game/objects/items/tcg/tcg.dm:21
    [DmParams("mapload", "datum_series", "datum_id")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object datum_series = Dm.arg(__extra, 0);
        object datum_id = Dm.arg(__extra, 1);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_item_scaling), 0.3f, 1));
        //If they are passed as null let's replace them with the vars on the card. this also means we can allow for map loaded ccards
        if (!Dm.truthy(datum_series))
            datum_series = series;
        if (!Dm.truthy(datum_id))
            datum_id = id;
        var temp_list = SStrading_card_game.cached_cards[datum_series] as DmList;
        if (temp_list == null)
            return dot;
        var temp = ((DmList)temp_list["ALL"])[datum_id] as datum_card;
        if (temp == null)
            return dot;
        name = temp.name;
        desc = Dm.text("<i>[]</i>", temp.desc);
        icon = icon(temp.icon);
        icon_state = temp.icon_state;
        id = temp.id;
        series = temp.series;
        return dot;
    }

    // This totally isn't overengineered to hell, shut up
    /**
     * Alright so some brief details here, we store all "immutable" (Think like power) card variables in a global list, indexed by id
     * This proc gets the card's associated card datum to play with
     */
    // tg: code/game/objects/items/tcg/tcg.dm:46
    public virtual object extract_datum()
    {
        var cached_cards = SStrading_card_game.cached_cards[series] as DmList;
        if (cached_cards == null)
            return null;
        if (!Dm.truthy(((DmList)cached_cards["ALL"])[id]))
            throw CRASH(Dm.text("A card without a datum has appeared, either the global list is empty, or you fucked up bad. Series{[]} ID{[]} Len{[]}", series, id, SStrading_card_game.cached_cards.len), "code/game/objects/items/tcg/tcg.dm", 51);
        return ((DmList)cached_cards["ALL"])[id];
    }

    // tg: code/game/objects/items/tcg/tcg.dm:54
    public override object get_name_chaser(mob user = null, DmList name_chaser = null)
    {
        name_chaser ??= Dm.list();
        if (Dm.truthy(flipped))
            return base.get_name_chaser(user, name_chaser);
        var data_holder = extract_datum() as datum_card;

        Dm.append(ref name_chaser, I18n.T("Faction: []", data_holder.faction));
        Dm.append(ref name_chaser, I18n.T("Cost: []", data_holder.summoncost));
        Dm.append(ref name_chaser, I18n.T("Type: [] - []", data_holder.cardtype, data_holder.cardsubtype));
        Dm.append(ref name_chaser, I18n.T("Power/Resolve: []/[]", data_holder.power, data_holder.resolve));
        if (Dm.truthy(data_holder.rules)) //This can sometimes be empty
            Dm.append(ref name_chaser, I18n.T("Ruleset: []", data_holder.rules));
        // name_chaser += list("[icon2html(get_cached_flat_icon(), user, "extra_classes" = "hugeicon")]"): the card's picture in the chat line
        Dm.Deferred("code/game/objects/items/tcg/tcg.dm:65", "get_name_chaser: icon2html of the card's flat icon, its picture in the chat line (the 3D client's chat is text: chat icons, M10+)");

        return name_chaser;
    }

    // tg: code/game/objects/items/tcg/tcg.dm:69
    public virtual object get_cached_flat_icon()
    {
        // if(!cached_flat_icon) cached_flat_icon = getFlatIcon(src): the flattened picture is only drawn in the chat (chat icons)
        Dm.Deferred("code/game/objects/items/tcg/tcg.dm:69", "get_cached_flat_icon: getFlatIcon, the card's picture for the chat (chat icons, M10+)");
        return cached_flat_icon;
    }

    // tg: code/game/objects/items/tcg/tcg.dm:76
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!isturf(loc))
            return await base.attack_hand(user, modifiers, __extra);
        DmList choices = GLOB.tcgcard_radial_choices;
        if (!Dm.truthy(length(choices)))
        {
            choices = GLOB.tcgcard_radial_choices = Dm.list(
                Dm.kv("Pickup", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_pickup")),
                Dm.kv("Tap", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_tap")),
                Dm.kv("Flip", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_flip")));
        }
        object choice = await show_radial_menu(user, this, choices, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(await check_menu(user as mob_living)))
            return null;
        switch (choice)
        {
            case "Tap":
                tap_card(user);
                break;
            case "Pickup":
                await user.put_in_hands(this);
                break;
            case "Flip":
                flip_card(user);
                break;
            case null:
                return null;
        }
        return null;
    }

    // tg: code/game/objects/items/tcg/tcg.dm:99
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        flip_card(user);
        return dot;
    }

    // tg: code/game/objects/items/tcg/tcg.dm:103
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        if (!Dm.truthy(flipped))
        {
            var template = extract_datum() as datum_card;
            name = template.name;
        }
        else
            name = I18n.T("Trading Card");
        return dot;
    }

    // tg: code/game/objects/items/tcg/tcg.dm:111
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        if (!Dm.truthy(flipped))
        {
            var template = extract_datum() as datum_card;
            desc = Dm.text("<i>[]</i>", template.desc);
        }
        else
            desc = I18n.T("It's the back of a trading card... no peeking!");
        return dot;
    }

    // tg: code/game/objects/items/tcg/tcg.dm:119
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(flipped))
        {
            icon_state = "cardback";
            return await base.update_icon_state(__extra);
        }

        var template = extract_datum() as datum_card;
        if (template == null)
            return null;
        icon_state = template.icon_state;
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/tcg/tcg.dm:130
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_tcgcard)))
        {
            var second_card = (obj_item_tcgcard)tool;
            var new_deck = Dm.@new<obj_item_tcgcard_deck>(drop_location());
            new_deck.flipped = flipped;
            await user.transferItemToLoc(second_card, new_deck); //Start a new pile with both cards, in the order of card placement.
            await user.transferItemToLoc(this, new_deck);
            await new_deck.update_icon_state();
            await user.put_in_hands(new_deck);
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_tcgcard_deck)))
        {
            var old_deck = (obj_item_tcgcard_deck)tool;
            if (length(old_deck.contents) >= 30)
            {
                to_chat(user, span_notice(I18n.T("This pile has too many cards for a regular deck!")));
                return ITEM_INTERACT_BLOCKING;
            }
            await user.transferItemToLoc(this, old_deck);
            flipped = old_deck.flipped;
            old_deck.update_appearance();
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/items/tcg/tcg.dm:154
    public virtual async DmTask<object> check_menu(mob_living user = null)
    {
        if (!istype(user, typeof(mob_living)))
            return FALSE;
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(await user.Adjacent(this)))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items/tcg/tcg.dm:161
    public virtual void tap_card(mob user = null)
    {
        matrix ntransform = Dm.matrix(transform);
        if (Dm.truthy(tapped))
            ntransform.TurnTo(TAPPED_ANGLE, UNTAPPED_ANGLE);
        else
            ntransform.TurnTo(UNTAPPED_ANGLE, TAPPED_ANGLE);
        tapped = Dm.num(!Dm.truthy(tapped));
        Dm.animate(this, Dm.named("transform", ntransform), Dm.named("time", 2), Dm.named("easing", SINE_EASING));
    }

    // tg: code/game/objects/items/tcg/tcg.dm:170
    public virtual void flip_card(mob user = null)
    {
        to_chat(user, span_notice(I18n.T("You turn the card over.")));
        if (!Dm.truthy(flipped))
        {
            name = I18n.T("Trading Card");
            desc = I18n.T("It's the back of a trading card... no peeking!");
            icon_state = "cardback";
        }
        else
        {
            var template = extract_datum() as datum_card;
            name = template.name;
            desc = Dm.text("<i>[]</i>", template.desc);
            icon_state = template.icon_state;
        }
        flipped = Dm.num(!Dm.truthy(flipped));
    }
}

// tg: code/game/objects/items/tcg/tcg.dm:74
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/game/objects/items/tcg/tcg.dm:74  GLOBAL_LIST_EMPTY(tcgcard_radial_choices)
    public DmList tcgcard_radial_choices;

    // tg: code/game/objects/items/tcg/tcg.dm:74
    public virtual void InitGlobaltcgcard_radial_choices()
    {
        tcgcard_radial_choices = Dm.list();
    }
}

/**
 * A stack item that's not actually a stack because ORDER MATTERS with a deck of cards!
 * The "top" card of the deck will always be the bottom card in the stack for our purposes.
 */
// tg: code/game/objects/items/tcg/tcg.dm:188
public partial class obj_item_tcgcard_deck : obj_item
{
    // tg: code/game/objects/items/tcg/tcg.dm:197-199
    public static mutable_appearance radial_draw => TgState.Current.obj_item_tcgcard_deck__radial_draw ??= image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_draw") as mutable_appearance;
    public static mutable_appearance radial_shuffle => TgState.Current.obj_item_tcgcard_deck__radial_shuffle ??= image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_shuffle") as mutable_appearance;
    public static mutable_appearance radial_pickup => TgState.Current.obj_item_tcgcard_deck__radial_pickup ??= image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_pickup") as mutable_appearance;

    // tg: code/game/objects/items/tcg/tcg.dm:201
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_tcg)));
        RegisterSignal(atom_storage, COMSIG_STORAGE_REMOVED_ITEM, nameof(on_item_removed));
        return dot;
    }

    // tg: code/game/objects/items/tcg/tcg.dm:206
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(flipped))
        {
            icon_state = Dm.text("[]_up", base_icon_state);
            return await base.update_icon_state(__extra);
        }

        float sw__1 = contents.len;
        if (sw__1 >= 1 && sw__1 <= 10)
            icon_state = Dm.text("[]_tcg_low", base_icon_state);
        else if (sw__1 >= 11 && sw__1 <= 20)
            icon_state = Dm.text("[]_tcg_half", base_icon_state);
        else if (sw__1 >= 21 && sw__1 <= INFINITY)
            icon_state = Dm.text("[]_tcg_full", base_icon_state);
        else
            icon_state = Dm.text("[]_tcg", base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/tcg/tcg.dm:222
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("\\The [] has [] cards inside.", this, contents.len)));
        return dot;
    }

    // tg: code/game/objects/items/tcg/tcg.dm:226
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        DmList choices = Dm.list(
            Dm.kv("Draw", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_draw")),
            Dm.kv("Shuffle", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_shuffle")),
            Dm.kv("Pickup", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_pickup")),
            Dm.kv("Flip", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_flip")));
        object choice = await show_radial_menu(user, this, choices, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), require_near: TRUE, tooltips: TRUE);
        if (!Dm.truthy(await check_menu(user as mob_living)))
            return null;
        switch (choice)
        {
            case "Draw":
                await draw_card(user);
                break;
            case "Shuffle":
                shuffle_deck(user);
                break;
            case "Pickup":
                await user.put_in_hands(this);
                break;
            case "Flip":
                flip_deck();
                break;
            case null:
                return null;
        }
        return null;
    }

    // tg: code/game/objects/items/tcg/tcg.dm:247
    public override object Destroy(float? force = null)
    {
        for (float card = 1; card <= contents.len; card++)
        {
            var stored_card = contents[card] as obj_item_tcgcard;
            Waiting.NoSleep(stored_card.forceMove(drop_location() as atom));
        }
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/tcg/tcg.dm:253
    public virtual async DmTask<object> check_menu(mob_living user = null)
    {
        if (!istype(user, typeof(mob_living)))
            return FALSE;
        if (Dm.truthy(user.incapacitated) || !Dm.truthy(await user.Adjacent(this)))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/items/tcg/tcg.dm:260
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_tcgcard)))
            return NONE;
        if (contents.len >= 30)
        {
            to_chat(user, span_notice(I18n.T("This pile has too many cards for a regular deck!")));
            return ITEM_INTERACT_BLOCKING;
        }
        var new_card = (obj_item_tcgcard)tool;
        new_card.flipped = flipped;
        await new_card.forceMove(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/tcg/tcg.dm:271
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        shuffle_deck(user);
        return await base.attack_self(user, modifiers, __extra);
    }

    /**
     * The user draws a single card. The deck is then handled based on how many cards are left.
     */
    // tg: code/game/objects/items/tcg/tcg.dm:278
    public virtual async DmTask draw_card(mob user = null)
    {
        if (!Dm.truthy(contents.len))
            throw CRASH("A TCG deck was created with no cards inside of it.", "code/game/objects/items/tcg/tcg.dm", 280);
        var drawn_card = contents[contents.len] as obj_item_tcgcard;
        await user.put_in_hands(drawn_card);
        drawn_card.flipped = flipped; //If it's a face down deck, it'll be drawn face down, if it's a face up pile you'll draw it face up.
        await drawn_card.update_icon_state();
        user.visible_message(span_notice(I18n.T("[] draws a card from \\the []!", user, this)),
            span_notice(I18n.T("You draw a card from \\the []!", this)));
        if (contents.len <= 1)
        {
            var final_card = contents[1] as obj_item_tcgcard;
            await user.transferItemToLoc(final_card, drop_location());
            qdel(this);
        }
    }

    /**
     * The user shuffles the order of the deck, then closes any visability into the deck's storage to prevent cheesing.
     * *User: The person doing the shuffling, used in visable message and closing UI.
     * *Visible: Will anyone need to hear the visable message about the shuffling?
     */
    // tg: code/game/objects/items/tcg/tcg.dm:300
    public virtual void shuffle_deck(mob user = null, float? visable = null)
    {
        visable ??= TRUE;
        if (contents == null)
            return;
        contents = shuffle(contents) as DmList;
        if (user.active_storage != null)
            user.active_storage.hide_contents(user);
        if (Dm.truthy(visable))
            user.visible_message(span_notice(I18n.T("[] shuffles \\the []!", user, this)),
                span_notice(I18n.T("You shuffle \\the []!", this)));
    }

    /**
     * The user flips the deck, turning it into a face up/down pile, and reverses the order of the cards from top to bottom.
     */
    // tg: code/game/objects/items/tcg/tcg.dm:314
    public virtual void flip_deck()
    {
        flipped = Dm.num(!Dm.truthy(flipped));
        DmList temp_deck = contents.Copy();
        contents = reverse_range(temp_deck) as DmList;
        //Now flip the cards to their opposite positions.
        foreach (obj_item_tcgcard nu_card in Dm.each<obj_item_tcgcard>(contents))
        {
            nu_card.flipped = flipped;
            Waiting.NoSleep(nu_card.update_icon_state());
        }
        Waiting.NoSleep(update_icon_state());
    }

    /**
     * Signal handler for COMSIG_STORAGE_REMOVED_ITEM. Qdels src if contents are empty, flips the removed card if needed.
     */
    // tg: code/game/objects/items/tcg/tcg.dm:328
    public virtual void on_item_removed(datum_storage storage_datum = null, obj_item thing = null, atom remove_to_loc = null, float? silent = null)
    {
        silent ??= FALSE;
        if (!istype(thing, typeof(obj_item_tcgcard)))
            return;
        var card = (obj_item_tcgcard)thing;
        card.flipped = flipped;
        card.update_appearance(UPDATE_ICON_STATE);

        if (length(contents) == 0)
            qdel(this);
    }
}

// tg: code/game/objects/items/tcg/tcg.dm:341
public partial class obj_item_cardpack : obj_item
{
    // tg: code/game/objects/items/tcg/tcg.dm:389
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_item_scaling), 0.4f, 1));
        rarity_table = SStrading_card_game.get_rarity_table(type, rarity_table) as DmList;
        guar_rarity = SStrading_card_game.get_guarenteed_rarity_table(type, guar_rarity) as DmList;
        return dot;
    }

    // tg: code/game/objects/items/tcg/tcg.dm:395
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        DmList cards;
        if (Dm.truthy(drop_all_cards))
            cards = ((DmList)SStrading_card_game.cached_cards[series])["ALL"] as DmList;
        else
            cards = buildCardListWithRarity(card_count, guaranteed_count) as DmList;

        foreach (object template in Dm.each(cards))
        {
            //Makes a new card based of the series of the pack.
            Dm.@new<obj_item_tcgcard>(get_turf(user), series, template);
        }
        to_chat(user, span_notice(I18n.T("Wow! Check out these cards!")));
        Dm.@new<obj_effect_decal_cleanable_wrapping>(get_turf(user));
        playsound(loc as atom, Dm.resource("sound/items/poster/poster_ripped.ogg"), 20, TRUE);
        if (prob(contains_coin))
        {
            to_chat(user, span_notice(I18n.T("...and it came with a flipper, too!")));
            Dm.@new<obj_item_coin_thunderdome>(get_turf(user));
        }
        qdel(this);
        return dot;
    }

    ///Returns a list of cards ids of card_cnt weighted by rarity from the pack's tables that have matching series, with gnt_cnt of the guaranteed table.
    // tg: code/game/objects/items/tcg/tcg.dm:440
    public virtual object buildCardListWithRarity(object card_cnt = null, object rarity_cnt = null)
    {
        DmList toReturn = Dm.list();
        //You can always get at least one of some rarity
        Dm.append(ref toReturn, returnCardsByRarity(rarity_cnt, guar_rarity));
        Dm.append(ref toReturn, returnCardsByRarity(card_cnt, rarity_table));
        return toReturn;
    }

    ///Returns a list of card datums of the length cardCount that match a random rarity weighted by rarity_table[]
    // tg: code/game/objects/items/tcg/tcg.dm:448
    public virtual object returnCardsByRarity(object cardCount = null, DmList rarity_table = null)
    {
        DmList toReturn = Dm.list();
        for (float card = 1; card <= Dm.num(cardCount); card++)
        {
            object rarity = 0;
            //Some number between 1 and the sum of all values in the list
            float weight = 0;
            foreach (object chance in Dm.each(rarity_table))
                weight += Dm.num(rarity_table[chance]);
            float random = rand(weight);
            foreach (object bracket in Dm.each(rarity_table))
            {
                //Steals blatantly from pick_weight(), sorry buddy I need the index
                random -= Dm.num(rarity_table[bracket]);
                if (random <= 0)
                {
                    rarity = bracket;
                    break;
                }
            }
            //What we're doing here is using the cached the results of the rarity we find.
            //This allows us to only have to run this once per rarity, ever.
            //Unless you reload the cards of course, in which case we have to do this again.
            var cards = ((DmList)SStrading_card_game.cached_cards[series])[rarity] as DmList;
            if (Dm.truthy(cards.len))
                Dm.append(ref toReturn, pick(cards));
            else
            {
                //If we still don't find anything yell into the void. Lazy coders.
                log_runtime(Dm.text("The index [] of rarity_table does not exist in the global cache", rarity));
            }
        }
        return toReturn;
    }
}

//All of these values should be overridden by either a template or a card itself
// tg: code/game/objects/items/tcg/tcg.dm:474
public partial class datum_card : datum
{
    // The card's numbers are object, not the float the default would give them: tg's set_one.json gives them as text ("5"), set_two
    // as numbers, and the summons read them back with text2num (tcg_machines.dm:233).
    ///What it costs to summon this card to the battlefield.
    // tg: code/game/objects/items/tcg/tcg.dm:483
    public object summoncost = -1;
    ///How hard this card hits (by default)
    // tg: code/game/objects/items/tcg/tcg.dm:485
    public object power = -1;
    ///How hard this card can get hit (by default)
    // tg: code/game/objects/items/tcg/tcg.dm:487
    public object resolve = -1;

    // tg: code/game/objects/items/tcg/tcg.dm:504
    [DmParams("data", "templates")]
    public override async DmTask New(params object[] __extra)
    {
        DmList data = Dm.arg<DmList>(__extra, 0) ?? Dm.list();
        DmList templates = Dm.arg<DmList>(__extra, 1) ?? Dm.list();
        applyTemplates(data, templates);
        apply(data);
        applyKeywords(Dm.bitor(data, templates) as DmList);
        await DmTask.CompletedTask;
    }

    ///For each var that the card datum and the json entry share, we set the datum var to the json entry
    // tg: code/game/objects/items/tcg/tcg.dm:510
    public virtual void apply(DmList data = null)
    {
        // data & vars: the keys of the entry that name a var of the datum, in the entry's order; vars[name] is the var by its name
        foreach (object name in Dm.each(data))
        {
            if (name is not string var_name || !Dm.has_var(this, var_name))
                continue;
            Dm.set_var(this, var_name, data[name]);
        }
    }

    ///Applies a json file to a card datum
    // tg: code/game/objects/items/tcg/tcg.dm:515
    public virtual void applyTemplates(DmList data = null, DmList templates = null)
    {
        templates ??= Dm.list();
        apply(templates["default"] as DmList);
        apply(templates[data["template"]] as DmList);
    }

    ///Searches for keywords in the card's variables, marked by wrapping them in {$}
    ///Adds on hovor logic to them, using the passed in list
    ///We use the changed_vars list just to make the var searching faster
    // tg: code/game/objects/items/tcg/tcg.dm:522
    public virtual void applyKeywords(DmList changed_vars = null)
    {
        foreach (object name in Dm.each(changed_vars))
        {
            if (name is not string var_name || !Dm.has_var(this, var_name))
                continue;
            object value = Dm.get_var(this, var_name);
            if (!istext(value))
                continue;
            Dm.set_var(this, var_name, SStrading_card_game.resolve_keywords(value));
        }
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/tcg/tcg.dm:197-199
    internal mutable_appearance obj_item_tcgcard_deck__radial_draw;
    internal mutable_appearance obj_item_tcgcard_deck__radial_shuffle;
    internal mutable_appearance obj_item_tcgcard_deck__radial_pickup;
}
