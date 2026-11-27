// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/roulette_machine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the roulette table (its owner's account, the bets and their payouts, the spin with its flicks, number overlays and
// light, the poker chips it drops, its wires and welded top) with its Roulette window, and the roulette wheel beacon. The var blocks
// are generated (D111).

///Machine that lets you play roulette. Odds are pre-defined to be the same as European Roulette without the "En Prison" rule
// tg: code/game/machinery/roulette_machine.dm:22
public partial class obj_machinery_roulette : obj_machinery
{
    // tg: code/game/machinery/roulette_machine.dm:1-19
    private const float ROULETTE_SINGLES_PAYOUT = 36;
    private const float ROULETTE_SIMPLE_PAYOUT = 2;
    private const float ROULETTE_DOZ_COL_PAYOUT = 3;
    private const string ROULETTE_BET_ODD = "odd";
    private const string ROULETTE_BET_EVEN = "even";
    private const string ROULETTE_BET_1TO18 = "s1-18"; //adds s to prevent text2num from working
    private const string ROULETTE_BET_19TO36 = "s19-36"; //adds s to prevent text2num from working
    private const string ROULETTE_BET_1TO12 = "s1-12";
    private const string ROULETTE_BET_13TO24 = "s13-24";
    private const string ROULETTE_BET_25TO36 = "s25-36";
    private const string ROULETTE_BET_2TO1_FIRST = "s1st col";
    private const string ROULETTE_BET_2TO1_SECOND = "s2nd col";
    private const string ROULETTE_BET_2TO1_THIRD = "s3rd col";
    private const string ROULETTE_BET_BLACK = "black";
    private const string ROULETTE_BET_RED = "red";
    private const float ROULETTE_JACKPOT_AMOUNT = 1000;

    // tg: code/game/machinery/roulette_machine.dm:31
    public static ref DmList numbers => ref TgState.Current.obj_machinery_roulette__numbers;
    //Make sure this is ordered from left to right.
    // tg: code/game/machinery/roulette_machine.dm:43
    public static ref DmList coin_values => ref TgState.Current.obj_machinery_roulette__coin_values;

    // tg: code/game/machinery/roulette_machine.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        jackpot_loop = Dm.@new<datum_looping_sound_jackpot>(this, FALSE);
        set_wires(Dm.@new<datum_wires_roulette>(this));
        return dot;
    }

    // tg: code/game/machinery/roulette_machine.dm:63
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref jackpot_loop);
        my_card = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/roulette_machine.dm:68
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        prize_theft(0.05f);
        return await base.atom_break(damage_flag);
    }

    // tg: code/game/machinery/roulette_machine.dm:72
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)))
            return null;
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "Roulette", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/roulette_machine.dm:80
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["IsAnchored"] = anchored;
        data["BetAmount"] = chosen_bet_amount;
        data["BetType"] = chosen_bet_type;
        object or__1 = my_card?.registered_account?.account_balance;
        data["HouseBalance"] = Dm.truthy(or__1) ? or__1 : 0;
        data["LastSpin"] = last_spin;
        data["Spinning"] = playing;

        if (ishuman(user))
        {
            var human_user = (mob_living_carbon_human)user;
            var id_card = human_user.get_idcard(TRUE) as obj_item_card_id;
            object or__2 = id_card?.registered_account?.account_balance;
            data["AccountBalance"] = Dm.truthy(or__2) ? or__2 : 0;
            data["CanUnbolt"] = Dm.num(id_card == my_card);
        }
        else
        {
            data["AccountBalance"] = 0;
            data["CanUnbolt"] = FALSE;
        }

        return data;
    }

    // tg: code/game/machinery/roulette_machine.dm:101
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "anchor":
                await set_anchored(Dm.num(!Dm.truthy(anchored)));
                dot = TRUE;
                break;
            case "ChangeBetAmount":
                chosen_bet_amount = clamp(Dm.num(text2num(@params["amount"])), 10, 500);
                dot = TRUE;
                break;
            case "ChangeBetType":
                chosen_bet_type = (string)@params["type"];
                dot = TRUE;
                break;
        }
        update_appearance(); // Not applicable to all objects.
        return dot;
    }

    ///Handles setting ownership and the betting itself.
    // tg: code/game/machinery/roulette_machine.dm:119
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)) && Dm.truthy(is_wire_tool(tool)))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy(playing))
            return NONE;
        var player_card = tool.GetID() as obj_item_card_id;
        if (player_card == null)
            return NONE;
        if (isidcard(tool))
            playsound(this, Dm.resource("sound/machines/card_slide.ogg"), 50, TRUE);
        else
            playsound(this, Dm.resource("sound/machines/terminal/terminal_success.ogg"), 50, TRUE);

        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)) || !Dm.truthy(on) || Dm.truthy(locked))
        {
            to_chat(user, span_notice(I18n.T("The machine appears to be disabled.")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (player_card.registered_account == null)
        {
            await say(I18n.T("You don't have a bank account!"));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 30, TRUE);
            return ITEM_INTERACT_BLOCKING;
        }

        if (my_card == null)
        {
            object msg = await tgui_input_text(user, I18n.T("Name of your roulette wheel"), I18n.T("Roulette Customization"), I18n.T("Roulette Machine"), max_length: MAX_NAME_LEN);
            if (!Dm.truthy(msg))
                return ITEM_INTERACT_BLOCKING;
            name = (string)msg;
            desc = I18n.T("Owned by [], draws directly from [] account.", player_card.registered_account.account_holder, user.p_their());
            my_card = player_card;
            RegisterSignal(my_card, COMSIG_QDELETING, nameof(on_my_card_deleted));
            to_chat(user, span_notice(I18n.T("You link the wheel to your account.")));
            power_change();
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.@in(player_card, SSeconomy.dep_cards)) // IS_DEPARTMENTAL_CARD(player_card), Are they using a department ID
        {
            await say(I18n.T("You cannot gamble with the department budget!"));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 30, TRUE);
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.num(player_card.registered_account.account_balance) < chosen_bet_amount) //Does the player have enough funds
        {
            await say(I18n.T("You do not have the funds to play! Lower your bet or get more money."));
            playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 30, TRUE);
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(chosen_bet_amount) || chosen_bet_type == null)
            return ITEM_INTERACT_BLOCKING;

        //double to nothing bets
        DmList doubles = Dm.list(
            ROULETTE_BET_2TO1_FIRST,
            ROULETTE_BET_2TO1_SECOND,
            ROULETTE_BET_2TO1_THIRD,
            ROULETTE_BET_1TO12,
            ROULETTE_BET_13TO24,
            ROULETTE_BET_25TO36);
        //result of text2num is null if text starts with a character, meaning it's not a singles bet
        bool single = text2num(chosen_bet_type) != null;
        float potential_payout_mult;
        if (single)
            potential_payout_mult = ROULETTE_SINGLES_PAYOUT;
        else if (Dm.@in(chosen_bet_type, doubles))
            potential_payout_mult = ROULETTE_DOZ_COL_PAYOUT;
        else
            potential_payout_mult = ROULETTE_SIMPLE_PAYOUT;
        float potential_payout = chosen_bet_amount * potential_payout_mult;

        if (!Dm.truthy(await check_owner_funds(potential_payout)))
            return ITEM_INTERACT_BLOCKING; //owner is too poor

        if (last_anti_spam > world.time) //do not cheat me
            return ITEM_INTERACT_BLOCKING;

        last_anti_spam = world.time + anti_spam_cooldown;

        icon_state = "rolling"; //Prepare the new icon state for rolling before hand.
        flick("flick_up", this);
        playsound(this, Dm.resource("sound/machines/piston/piston_raise.ogg"), 70);
        playsound(this, Dm.resource("sound/machines/chime.ogg"), 50);

        _addtimer(Dm.@new<datum_callback>(this, nameof(play), user, player_card, chosen_bet_type, chosen_bet_amount, potential_payout), 4, file: "code/game/machinery/roulette_machine.dm", line: 194); //Animation first
        return ITEM_INTERACT_SUCCESS;
    }

    ///deletes the my_card ref to prevent harddels
    // tg: code/game/machinery/roulette_machine.dm:199
    public virtual void on_my_card_deleted(datum source = null)
    {
        my_card = null;
    }

    ///Proc called when player is going to try and play
    // tg: code/game/machinery/roulette_machine.dm:204
    public virtual async DmTask play(mob user = null, obj_item_card_id player_id = null, object bet_type = null, object bet_amount = null, object potential_payout = null)
    {
        if (my_card?.registered_account == null) // Something happened to my_card during the 0.4 seconds delay of the timed callback.
        {
            icon_state = "idle";
            flick("flick_down", this);
            playsound(this, Dm.resource("sound/machines/piston/piston_lower.ogg"), 70);
            return;
        }

        object payout = potential_payout;

        my_card.registered_account.transfer_money(player_id.registered_account, bet_amount, "Roulette: Bet");

        playing = TRUE;
        cut_overlays();
        update_appearance();
        await set_light(0);
        if (isliving(user))
        {
            var living_user = (mob_living)user;
            await living_user.add_mood_event("roulette", typeof(datum_mood_event_slots));
        }

        float rolled_number = rand(0, 36);

        playsound(this, Dm.resource("sound/machines/roulette/roulettewheel.ogg"), 50);
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_play), player_id, bet_type, bet_amount, payout, rolled_number, user), 34, file: "code/game/machinery/roulette_machine.dm", line: 229); //4 deciseconds more so the animation can play
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_play_animation)), 3 * SECONDS, file: "code/game/machinery/roulette_machine.dm", line: 230);

        await use_energy(active_power_usage);
    }

    // tg: code/game/machinery/roulette_machine.dm:234
    public virtual void finish_play_animation()
    {
        icon_state = "idle";
        flick("flick_down", this);
        playsound(this, Dm.resource("sound/machines/piston/piston_lower.ogg"), 70);
    }

    ///Ran after a while to check if the player won or not.
    // tg: code/game/machinery/roulette_machine.dm:240
    public virtual async DmTask<object> finish_play(obj_item_card_id player_id = null, object bet_type = null, object bet_amount = null, object potential_payout = null, object rolled_number = null, mob user = null)
    {
        last_spin = Dm.num(rolled_number);

        object is_winner = check_win(bet_type, bet_amount, rolled_number); //Predetermine if we won
        object color = numbers[Dm.text("[]", rolled_number)]; //Weird syntax, but dict uses strings.
        string result = Dm.text("[] []", rolled_number, color); //e.g. 31 black

        await say(I18n.T("The result is: []", result));

        playing = FALSE;
        await update_icon(ALL, potential_payout, color, rolled_number, is_winner);
        await handle_color_light(color);

        if (!Dm.truthy(is_winner))
        {
            await say(I18n.T("You lost! Better luck next time."));
            playsound(this, Dm.resource("sound/machines/synth/synth_no.ogg"), 50);
            if (isliving(user) && Dm.@in(user, viewers(null, this)))
            {
                var living_user = (mob_living)user;
                await living_user.add_mood_event("roulette", typeof(datum_mood_event_slots_loss));
            }
            return FALSE;
        }

        // Prevents money generation exploits. Doesn't prevent the owner being a scrooge and running away with the money.
        object account_balance = my_card?.registered_account?.account_balance;
        potential_payout = (Dm.num(account_balance) >= Dm.num(potential_payout)) ? potential_payout : account_balance;

        await say(I18n.T("You have won [] []! Congratulations!", potential_payout, MONEY_NAME));
        playsound(this, Dm.resource("sound/machines/synth/synth_yes.ogg"), 50);
        if (isliving(user) && Dm.@in(user, viewers(null, this)))
        {
            var living_user = (mob_living)user;
            await living_user.add_mood_event("roulette", Dm.num(potential_payout) >= ROULETTE_JACKPOT_AMOUNT ? typeof(datum_mood_event_slots_win_jackpot) : typeof(datum_mood_event_slots_win_big));
        }
        dispense_prize(potential_payout);
        return null;
    }

    ///Fills a list of coins that should be dropped.
    // tg: code/game/machinery/roulette_machine.dm:276
    public virtual void dispense_prize(object payout = null)
    {
        if (!Dm.truthy(payout))
            return;

        if (Dm.num(payout) >= ROULETTE_JACKPOT_AMOUNT)
            jackpot_loop.start();

        float remaining_payout = Dm.num(payout);

        my_card.registered_account.adjust_money(-Dm.num(payout), "Roulette: Payout");

        foreach (object coin_type in Dm.each(coin_values)) //Loop through all coins from most valuable to least valuable. Try to give as much of that coin (the iterable) as possible until you can't anymore, then move to the next.
        {
            float value = Dm.num(coin_values[coin_type]); //Change this to use initial value once we change to mat datum coins.
            float coin_count = round(remaining_payout / value);

            if (!Dm.truthy(coin_count)) //Cant make coins of this type, as we can't reach its value.
                continue;

            remaining_payout -= value * coin_count;
            coins_to_dispense[coin_type] = Dm.num(coins_to_dispense[coin_type]) + coin_count;
        }

        drop_coin(); //Start recursively dropping coins
    }

    ///Recursive function that runs until it runs out of coins to drop.
    // tg: code/game/machinery/roulette_machine.dm:300
    public virtual object drop_coin()
    {
        object coin_to_drop = null;

        foreach (object i in Dm.each(coins_to_dispense)) //Find which coin to drop
        {
            if (Dm.num(coins_to_dispense[i]) <= 0) //Less than 1? go to next potential coin.
                continue;
            coin_to_drop = i;
            break;
        }

        if (!Dm.truthy(coin_to_drop)) //No more coins, stop recursion.
        {
            jackpot_loop.stop();
            return FALSE;
        }

        coins_to_dispense[coin_to_drop] = Dm.num(coins_to_dispense[coin_to_drop]) - 1;

        var drop_loc = get_step(loc as atom, drop_dir) as turf;
        var cash = (obj_item)Dm.@new((Type)coin_to_drop, drop_loc);
        //We animate the chip with a cool drop, bounce and slide effect with random offsets to fan out the chips
        cash.pixel_y = 16;
        cash.pixel_z = 8;
        float extra_x = rand(0, 8);
        float extra_y = round(extra_x * 0.7f);
        Dm.animate(cash, 250 * MILLISECONDS, Dm.named("pixel_z", 0), Dm.named("easing", Dm.bitor(BOUNCE_EASING, EASE_OUT))); //fall to the ground
        Dm.animate(300 * MILLISECONDS, Dm.named("pixel_y", rand(-6, 6) + extra_y), Dm.named("pixel_x", rand(0 + extra_x, 8 + extra_x) * Dm.num(pick(1, -1))), Dm.named("flags", ANIMATION_PARALLEL)); //midnight shitcode instead of proper maths
        playsound(cash, pick(Dm.list(Dm.resource("sound/machines/coindrop.ogg"), Dm.resource("sound/machines/coindrop2.ogg"))), 40, TRUE);

        _addtimer(Dm.@new<datum_callback>(this, nameof(drop_coin)), 3, file: "code/game/machinery/roulette_machine.dm", line: 327); //Recursion time
        return null;
    }

    ///Fills a list of coins that should be dropped.
    // tg: code/game/machinery/roulette_machine.dm:331
    public virtual void prize_theft(object percentage = null)
    {
        if (Dm.truthy(locked))
            return;
        locked = TRUE;
        float stolen_cash = Dm.num(my_card?.registered_account?.account_balance) * Dm.num(percentage);
        dispense_prize(stolen_cash);
    }

    ///Returns TRUE if the player bet correctly.
    // tg: code/game/machinery/roulette_machine.dm:340
    public virtual object check_win(object bet_type = null, object bet_amount = null, object rolled_number = null)
    {
        object actual_bet_number = text2num(bet_type); //Only returns the numeric bet types, AKA singles.
        if (actual_bet_number != null) //This means we're playing singles
            return Dm.num(Dm.eq(rolled_number, actual_bet_number));

        float rolled = Dm.num(rolled_number);
        switch (bet_type) //Otherwise, we are playing a "special" game, switch on all the cases so we can check.
        {
            case ROULETTE_BET_ODD:
                return Dm.num(rolled % 2 != 0); // ISODD(rolled_number) (__DEFINES/maths.dm:86, hand-expanded, rules 11.1)
            case ROULETTE_BET_EVEN:
                return Dm.num(rolled % 2 == 0 && rolled != 0); // ISEVEN(rolled_number) (__DEFINES/maths.dm:84, hand-expanded, rules 11.1)
            case ROULETTE_BET_1TO18:
                return Dm.num(rolled >= 1 && rolled <= 18); //between 1 to 18
            case ROULETTE_BET_19TO36:
                return Dm.num(rolled > 18); //between 19 to 36, no need to check bounds because we won't go higher anyways
            case ROULETTE_BET_BLACK:
                return Dm.num(Dm.eq("black", numbers[Dm.text("[]", rolled_number)])); //Check if our number is black in the numbers dict
            case ROULETTE_BET_RED:
                return Dm.num(Dm.eq("red", numbers[Dm.text("[]", rolled_number)])); //Check if our number is black in the numbers dict
            case ROULETTE_BET_1TO12:
                return Dm.num(rolled >= 1 && rolled <= 12);
            case ROULETTE_BET_13TO24:
                return Dm.num(rolled >= 13 && rolled <= 24);
            case ROULETTE_BET_25TO36:
                return Dm.num(rolled >= 25 && rolled <= 36);
            case ROULETTE_BET_2TO1_FIRST:
            {
                //You could do this mathematically but w/e this is easy to understand
                //numbers in the first column
                DmList winners = Dm.list(1, 4, 7, 10, 13, 16, 19, 22, 25, 28, 31, 34);
                return Dm.num(Dm.@in(rolled_number, winners));
            }
            case ROULETTE_BET_2TO1_SECOND:
            {
                //numbers in the second column
                DmList winners = Dm.list(2, 5, 8, 11, 14, 17, 20, 23, 26, 29, 32, 35);
                return Dm.num(Dm.@in(rolled_number, winners));
            }
            case ROULETTE_BET_2TO1_THIRD:
            {
                //numbers in the third column
                DmList winners = Dm.list(3, 6, 9, 12, 15, 18, 21, 24, 27, 30, 33, 36);
                return Dm.num(Dm.@in(rolled_number, winners));
            }
        }
        return null;
    }

    ///Returns TRUE if the owner has enough funds to payout
    // tg: code/game/machinery/roulette_machine.dm:380
    public virtual async DmTask<object> check_owner_funds(object payout = null)
    {
        if (Dm.num(my_card.registered_account.account_balance) >= Dm.num(payout))
            return TRUE; //We got the betting amount
        await say(I18n.T("The bank account of [] does not have enough funds to pay out the potential prize, contact them to fill up their account or lower your bet!", my_card.registered_account.account_holder));
        playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 30, TRUE);
        return FALSE;
    }

    // tg: code/game/machinery/roulette_machine.dm:387
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)))
            return dot;

        if (Dm.truthy(playing))
            Dm.append(ref dot, "random_numbers");
        return dot;
    }

    // tg: code/game/machinery/roulette_machine.dm:395
    [DmParams("updates", "payout", "color", "rolled_number", "is_winner")]
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        updates ??= ALL;
        object payout = Dm.arg(__extra, 0);
        object color = Dm.arg(__extra, 1);
        object rolled_number = Dm.arg(__extra, 2);
        object is_winner = Dm.arg(__extra, 3) ?? FALSE;
        object dot = await base.update_icon(updates, __extra);
        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)))
            return dot;

        if (!Dm.truthy(payout) || !Dm.truthy(color) || rolled_number == null) //Don't fall for tricks.
            return dot;

        //Overlay for ring
        if (Dm.truthy(is_winner) && Dm.num(payout) >= ROULETTE_JACKPOT_AMOUNT)
            add_overlay("jackpot");
        else
            add_overlay(color);

        float numberright = Dm.num(rolled_number) % 10; //Right hand number
        float numberleft = (Dm.num(rolled_number) - numberright) / 10; //Left hand number

        float shift_amount = 2; //How much the icon moves left/right

        if (numberleft != 0) //Don't make the number if we are 0.
        {
            var number1 = mutable_appearance(icon, Dm.text("[]", numberleft)) as mutable_appearance;
            number1.pixel_w = -shift_amount;
            add_overlay(number1);
        }
        else
            shift_amount = 0; //We can stay centered.

        var number2 = mutable_appearance(icon, Dm.text("[]", numberright)) as mutable_appearance;
        number2.pixel_w = shift_amount;
        add_overlay(number2);
        return dot;
    }

    // tg: code/game/machinery/roulette_machine.dm:425
    public virtual async DmTask handle_color_light(object color = null)
    {
        switch (color)
        {
            case "green":
                await set_light(2, 2, LIGHT_COLOR_GREEN);
                break;
            case "red":
                await set_light(2, 2, COLOR_SOFT_RED);
                break;
        }
    }

    // tg: code/game/machinery/roulette_machine.dm:432
    public override async DmTask<object> welder_act(mob_living user = null, obj_item I = null)
    {
        object dot = await base.welder_act(user, I);
        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)))
        {
            to_chat(user, span_notice(I18n.T("You start re-attaching the top section of []...", this)));
            if (Dm.truthy(await I.use_tool(this, user, 30, volume: 50)))
            {
                to_chat(user, span_notice(I18n.T("You re-attach the top section of [].", this)));
                await set_machine_stat(Dm.bitand(machine_stat, ~MAINT));
                icon_state = "idle";
            }
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You start welding the top section from []...", this)));
            if (Dm.truthy(await I.use_tool(this, user, 30, volume: 50)))
            {
                to_chat(user, span_notice(I18n.T("You removed the top section of [].", this)));
                await set_machine_stat(Dm.bitor(machine_stat, MAINT));
                icon_state = "open";
            }
        }
        return dot;
    }

    // tg: code/game/machinery/roulette_machine.dm:447
    public override async DmTask<object> shock__proc(mob_living shocking = null, float? chance = null, object shock_source = null, float? siemens_coeff = null)
    {
        if (!Dm.truthy(on)) // unpowered, no shock
            return FALSE;
        return await base.shock__proc(shocking, chance, shock_source, siemens_coeff);
    }
}

// tg: code/game/machinery/roulette_machine.dm:452
public partial class obj_item_roulette_wheel_beacon : obj_item
{
    // tg: code/game/machinery/roulette_machine.dm:459
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(used))
            return null;
        ((atom)loc).visible_message(span_warning(I18n.T("\\The [] begins to beep loudly!", this)));
        used = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(launch_payload)), 4 * SECONDS, file: "code/game/machinery/roulette_machine.dm", line: 464);
        return null;
    }

    // tg: code/game/machinery/roulette_machine.dm:466
    public virtual async DmTask launch_payload()
    {
        await podspawn(Dm.list(
            Dm.kv("target", drop_location()),
            Dm.kv("path", typeof(obj_structure_closet_supplypod_centcompod)),
            Dm.kv("spawn", typeof(obj_machinery_roulette))));

        qdel(this);
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/roulette_machine.dm:31
    internal DmList obj_machinery_roulette__numbers = Dm.list(Dm.kv("0", "green"), Dm.kv("1", "red"), Dm.kv("3", "red"), Dm.kv("5", "red"), Dm.kv("7", "red"), Dm.kv("9", "red"), Dm.kv("12", "red"), Dm.kv("14", "red"), Dm.kv("16", "red"),
        Dm.kv("18", "red"), Dm.kv("19", "red"), Dm.kv("21", "red"), Dm.kv("23", "red"), Dm.kv("25", "red"), Dm.kv("27", "red"), Dm.kv("30", "red"), Dm.kv("32", "red"), Dm.kv("34", "red"), Dm.kv("36", "red"),
        Dm.kv("2", "black"), Dm.kv("4", "black"), Dm.kv("6", "black"), Dm.kv("8", "black"), Dm.kv("10", "black"), Dm.kv("11", "black"), Dm.kv("13", "black"), Dm.kv("15", "black"), Dm.kv("17", "black"), Dm.kv("20", "black"),
        Dm.kv("22", "black"), Dm.kv("24", "black"), Dm.kv("26", "black"), Dm.kv("28", "black"), Dm.kv("29", "black"), Dm.kv("31", "black"), Dm.kv("33", "black"), Dm.kv("35", "black"));

    // tg: code/game/machinery/roulette_machine.dm:43
    internal DmList obj_machinery_roulette__coin_values = Dm.list(Dm.kv(typeof(obj_item_poker_chip_black), 1000), Dm.kv(typeof(obj_item_poker_chip_green), 250), Dm.kv(typeof(obj_item_poker_chip_blue), 100), Dm.kv(typeof(obj_item_poker_chip_red), 25), Dm.kv(typeof(obj_item_poker_chip_white_blue), 10), Dm.kv(typeof(obj_item_poker_chip_white_black), 1));
}
