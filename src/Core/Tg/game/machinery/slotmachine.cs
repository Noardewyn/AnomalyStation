// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/slotmachine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the slot machine and its themed kinds: the coins and holochips it takes, the spin with its reels and lines, the
// payouts in holochips or coins, the jackpot's announcement, rays and memory, the traps, the owner's bank account, the emag and the
// EMP, with its SlotMachine window. The var blocks are generated (D111).

/*******************************\
|   SLOT MACHINES               |
|   Original code by Glloyd     |
|   Tgstation port by Miauw     |
|   Tgui spin by stylemistake   |
\*******************************/

// tg: code/game/machinery/slotmachine.dm:22
public partial class obj_machinery_computer_slot_machine : obj_machinery_computer
{
    // tg: code/game/machinery/slotmachine.dm:8-20
    protected const float SPIN_PRICE = 5;
    protected const float WINNING_NOTHING = 0;
    protected const float WINNING_FREESPIN = 1;
    protected const float WINNING_SMALL = 2;
    protected const float WINNING_BIG = 3;
    protected const float WINNING_JACKPOT = 4;
    protected const float PRIZE_SMALL = 400;
    protected const float PRIZE_BIG = 1000;
    protected const float PRIZE_JACKPOT = 10000;
    protected const float SPIN_TIME = 4 * SECONDS;
    protected const float REEL_DEACTIVATE_DELAY = 0.4f * SECONDS;
    protected const float HOLOCHIP = 1;
    protected const float COIN = 2;

    // tg: code/game/machinery/slotmachine.dm:70
    public static ref DmList coinvalues => ref TgState.Current.obj_machinery_computer_slot_machine__coinvalues;
    // tg: code/game/machinery/slotmachine.dm:78
    public static ref DmList ray_filter => ref TgState.Current.obj_machinery_computer_slot_machine__ray_filter;

    // tg: code/game/machinery/slotmachine.dm:80
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        jackpots = rand(1, 4); //false hope
        plays = rand(75, 200);

        name = Dm.str(make_machine_name());

        if (jackpot_path != null && !Dm.@in(jackpot_path, symbol_paths))
            stack_trace(Dm.text("[] has jackpot_path [] not present in symbol_paths!", type, jackpot_path), "code/game/machinery/slotmachine.dm", 88);
        if (trap_path != null && !Dm.@in(trap_path, symbol_paths))
            stack_trace(Dm.text("[] has trap_path [] not present in symbol_paths!", type, trap_path), "code/game/machinery/slotmachine.dm", 90);

        build_symbol_data();
        // Populate the reels
        randomize_reels();

        if (coinvalues == null)
        {
            coinvalues = Dm.list();

            foreach (Type coin_path in Dm.each<Type>(typesof(typeof(obj_item_coin))))
            {
                cointype = coin_path;
                var C = (obj_item_coin)Dm.@new(cointype);
                coinvalues[Dm.text("[]", cointype)] = C.get_item_credit_value();
                qdel(C); //Sigh
            }
        }
        return dot;
    }

    // tg: code/game/machinery/slotmachine.dm:104
    public override object Destroy(float? force = null)
    {
        house_bank_account = null;
        return base.Destroy(force);
    }

    /// Generates a randomised slot name by pulling an adjective and a noun
    /// Produces things like "Lucky Sevens", "Robust Payday", "Honking Bonanza", etc.
    // tg: code/game/machinery/slotmachine.dm:110
    public virtual object make_machine_name()
    {
        object adjective = pick(slot_adjectives);
        object noun = pick(slot_nouns);
        return I18n.T("[] []", adjective, noun);
    }

    /// Builds symbol_data from symbol_paths to be used for DmIcon in TGUI
    // tg: code/game/machinery/slotmachine.dm:116
    public virtual void build_symbol_data()
    {
        symbol_data = Dm.list();
        foreach (Type symbol in Dm.each<Type>(symbol_paths))
        {
            // DEVIATION(DV664): each symbol also carries its icon file and state: the port sends no sprite sheets (DV44), so the SlotMachine
            // window draws the symbol from its own copy of tg's .dmi files instead of the slot_machines sheet's class (icon_id)
            Dm.append(ref symbol_data, Dm.list(Dm.list(
                Dm.kv("id", Dm.text("[]", symbol)),
                Dm.kv("icon_id", sanitize_css_class_name(Dm.text("[][]", Dm.initial(symbol, (atom p) => p.icon), Dm.initial(symbol, (atom p) => p.icon_state)))),
                Dm.kv("icon", Dm.text("[]", Dm.initial(symbol, (atom p) => p.icon))),
                Dm.kv("icon_state", Dm.initial(symbol, (atom p) => p.icon_state)))));
        }
    }

    // tg: code/game/machinery/slotmachine.dm:124
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (Dm.truthy(balance))
            await give_payout(balance);
        return null;
    }

    // tg: code/game/machinery/slotmachine.dm:128
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        object dot = base.process(seconds_per_tick, __extra); //Sanity checks.
        if (!Dm.truthy(dot))
            return dot;

        money += round(Dm.num(seconds_per_tick) / 2); //SPESSH MAJICKS
        return dot;
    }

    // tg: code/game/machinery/slotmachine.dm:135
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            icon_state = "slots_broken";
        else
            icon_state = "slots";
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/slotmachine.dm:142
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        if (Dm.truthy(working))
            icon_screen = "slots_screen_working";
        else
            icon_screen = "slots_screen";
        return await base.update_overlays(__extra);
    }

    // tg: code/game/machinery/slotmachine.dm:150
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item inserted = tool;
        if (istype(inserted, typeof(obj_item_coin)))
        {
            var inserted_coin = (obj_item_coin)inserted;
            if (paymode == COIN)
            {
                if (prob(2))
                {
                    if (!Dm.truthy(await user.transfer_item_to_turf(inserted_coin, drop_location() as turf, silent: FALSE)))
                        return ITEM_INTERACT_BLOCKING;
                    await inserted_coin.throw_at(user, 3, 10);
                    if (prob(10))
                        balance = max(balance - SPIN_PRICE, 0);
                    to_chat(user, span_warning(I18n.T("[] spits your coin back out!", this)));
                    return ITEM_INTERACT_BLOCKING;
                }
                else
                {
                    if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(inserted_coin)))
                        return ITEM_INTERACT_BLOCKING;
                    balloon_alert(user, I18n.T("coin inserted"));
                    balance += Dm.num(inserted_coin.value);
                    qdel(inserted_coin);
                    return ITEM_INTERACT_SUCCESS;
                }
            }
            else
                balloon_alert(user, I18n.T("holochips only!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (istype(inserted, typeof(obj_item_holochip)))
        {
            if (paymode == HOLOCHIP)
            {
                var inserted_chip = (obj_item_holochip)inserted;
                if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(inserted_chip)))
                    return ITEM_INTERACT_BLOCKING;
                // MONEY_NAME_AUTOPURAL(inserted_chip.credits) (__DEFINES/economy.dm:99), inline (rules 11.1)
                balloon_alert(user, I18n.T("[] [] inserted", inserted_chip.credits, Dm.text("credit[]", Dm.num(inserted_chip.credits) == 1 ? "" : "s")));
                balance += Dm.num(inserted_chip.credits);
                qdel(inserted_chip);
                return ITEM_INTERACT_SUCCESS;
            }
            else
                balloon_alert(user, I18n.T("coins only!"));
            return ITEM_INTERACT_BLOCKING;
        }

        var id_card = inserted.GetID() as obj_item_card_id;
        if (isidcard(id_card))
        {
            if (house_bank_account != null)
            {
                await say(I18n.T("Already linked to []!", house_bank_account.account_holder));
                playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 30, TRUE);
                return ITEM_INTERACT_BLOCKING;
            }

            datum_bank_account id_bank_account = id_card.registered_account;

            if (id_bank_account == null)
            {
                await say(I18n.T("No bank account detected on id card!"));
                playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 30, TRUE);
                return ITEM_INTERACT_BLOCKING;
            }

            if (!id_bank_account.has_money(PRIZE_SMALL))
            {
                await say(I18n.T("Insufficent funds for potential payout. Minimum of [] credits needed!", PRIZE_SMALL));
                playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 30, TRUE);
                return ITEM_INTERACT_BLOCKING;
            }

            object msg = await tgui_input_text(user, I18n.T("Name of your slot machine (optional)"), I18n.T("Slot Customization"), I18n.T("Slot Machine"), max_length: MAX_NAME_LEN);
            if (Dm.truthy(msg))
                name = (string)msg;

            playsound(this, Dm.resource("sound/machines/terminal/terminal_success.ogg"), 50, TRUE);
            desc = I18n.T("Owned by [], draws directly from [] account.", id_bank_account.account_holder, user.p_their());
            house_bank_account = id_bank_account;
            to_chat(user, span_notice(I18n.T("You link the slot machine to []'s account.", id_bank_account.account_holder)));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/machinery/slotmachine.dm:216
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (balance > 0)
        {
            await say(I18n.T("ERROR! Please empty the machine balance before altering paymode.")); //Prevents converting coins into holocredits and vice versa
            return ITEM_INTERACT_BLOCKING;
        }

        if (paymode == HOLOCHIP)
        {
            paymode = COIN;
            balloon_alert(user, I18n.T("now using coins"));
        }
        else
        {
            paymode = HOLOCHIP;
            balloon_alert(user, I18n.T("now using holochips"));
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/slotmachine.dm:229
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        var spark_system = Dm.@new<datum_effect_system_basic_spark_spread>(loc, 4, 0);
        await spark_system.start();
        playsound(this, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        balloon_alert(user, I18n.T("machine rigged"));
        return TRUE;
    }

    // tg: code/game/machinery/slotmachine.dm:239
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/slot_machines)): the asset transport is the client's (DV44); the
        // SlotMachine window draws each symbol from its own copy of tg's icons by the symbol's icon and state
        return Dm.list();
    }

    // tg: code/game/machinery/slotmachine.dm:244
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "SlotMachine", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/slotmachine.dm:251
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["symbols"] = symbol_data;
        data["cost"] = SPIN_PRICE;
        data["jackpot"] = PRIZE_JACKPOT;
        data["jackpot_id"] = Dm.text("[]", jackpot_path);
        if (trap_path != null)
            data["trap_id"] = Dm.text("[]", trap_path);
        return data;
    }

    // tg: code/game/machinery/slotmachine.dm:261
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList _reels = Dm.list();
        foreach (object reel in Dm.each(reels))
        {
            Dm.append(ref _reels, Dm.list(Dm.list(
                Dm.kv("symbols", reel))));
        }
        data["reels"] = _reels;
        data["balance"] = balance;
        data["working"] = working;
        data["winning"] = winning;
        data["money"] = money;
        data["plays"] = plays;
        data["jackpots"] = jackpots;
        data["paymode"] = paymode;
        return data;
    }

    // tg: code/game/machinery/slotmachine.dm:279
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "spin":
                await spin(ui.user);
                return TRUE;
            case "payout":
                if (balance > 0)
                {
                    await give_payout(balance);
                    balance = 0;
                    return TRUE;
                }
                break;
        }
        return dot;
    }

    // tg: code/game/machinery/slotmachine.dm:293
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (prob(15 * Dm.num(severity)))
            return dot;
        if (prob(1)) // :^)
            obj_flags = Dm.bitor(obj_flags, EMAGGED);
        float severity_ascending = 4 - Dm.num(severity);
        money = max(rand(money - (200 * severity_ascending), money + (200 * severity_ascending)), 0);
        balance = max(rand(balance - (50 * severity_ascending), balance + (50 * severity_ascending)), 0);
        // tg passes money as a second argument to give_payout, which takes one
        money -= max(0, Dm.num(await give_payout(min(rand(-50, 100 * severity_ascending))))); //This starts at -50 because it shouldn't always dispense coins yo
        await spin();
        return dot;
    }

    /// Returns TRUE if the owner has enough funds to payout
    // tg: code/game/machinery/slotmachine.dm:308
    public virtual async DmTask<object> has_funds_to_pay(object payout = null)
    {
        if (house_bank_account == null) // no owner so NT is paying
            return TRUE;

        if (house_bank_account.has_money(payout))
            return TRUE;

        await say(I18n.T("The bank account of [] does not have enough funds to pay out the potential prize, contact them to fill up their account or lower your bet!", house_bank_account.account_holder));
        playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 30, TRUE);
        return FALSE;
    }

    // tg: code/game/machinery/slotmachine.dm:319
    public virtual async DmTask spin(mob user = null)
    {
        if (!Dm.truthy(can_spin(user)))
            return;

        if (!Dm.truthy(await has_funds_to_pay(PRIZE_SMALL)))
            return;

        if (!Dm.truthy(await use_energy(active_power_usage, force: FALSE)))
        {
            await say(I18n.T("Not enough energy!"));
            return;
        }

        object the_name;
        if (user != null)
        {
            the_name = user.real_name;
            visible_message(span_notice(I18n.T("[] pulls the lever and the slot machine starts spinning!", user)));
            if (isliving(user))
            {
                var living_user = (mob_living)user;
                await living_user.add_mood_event("slots_spin", typeof(datum_mood_event_slots));
            }
        }
        else
            the_name = "Exaybachay";

        balance -= SPIN_PRICE;
        money += SPIN_PRICE;

        if (house_bank_account != null)
        {
            // Since big jackpots & free games aren't deducted directly from the account holder NT takes a 40% cut of profit
            // Also discourages infinite money exploits since people playing slot machines they own will cause them to slowly lose money
            house_bank_account.adjust_money(SPIN_PRICE * 0.60f, "Slot Machine: Spin");
        }

        plays += 1;
        working = TRUE;

        update_appearance();

        // Play the lever pull sound and a reel spin sound after a short delay
        playsound(this, Dm.resource("sound/machines/lever/lever_start.ogg"), 50);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.playsound), this, Dm.resource("sound/machines/roulette/roulettewheel.ogg"), 20), 0.3f * SECONDS, file: "code/game/machinery/slotmachine.dm", line: 357);

        // Randomize reels to get the new final state.
        randomize_reels();

        // Now wait for a pre-determined delay to set machine to a non-spinning state.
        // On the UI side, result is pre-determined: after the delay below,
        // animation stops at the newly calculated state of the reels.
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_spinning), user, the_name), SPIN_TIME, file: "code/game/machinery/slotmachine.dm", line: 365);
    }

    // tg: code/game/machinery/slotmachine.dm:367
    public virtual async DmTask finish_spinning(mob user = null, object the_name = null)
    {
        working = FALSE;
        await give_prizes(the_name, user as mob_living);
        update_appearance();
    }

    /// Check if the machine can be spun
    // tg: code/game/machinery/slotmachine.dm:373
    public virtual object can_spin(mob user = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            balloon_alert(user, I18n.T("no power!"));
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            balloon_alert(user, I18n.T("machine broken!"));
            return FALSE;
        }
        if (Dm.truthy(working))
        {
            balloon_alert(user, I18n.T("already spinning!"));
            return FALSE;
        }
        if (balance < SPIN_PRICE)
        {
            balloon_alert(user, I18n.T("insufficient balance!"));
            return FALSE;
        }
        return TRUE;
    }

    /// Randomize the states of all reels
    // tg: code/game/machinery/slotmachine.dm:389
    public virtual void randomize_reels()
    {
        foreach (DmList reel in Dm.each<DmList>(reels))
        {
            reel[1] = Dm.text("[]", pick(symbol_paths));
            reel[2] = Dm.text("[]", pick(symbol_paths));
            reel[3] = Dm.text("[]", pick(symbol_paths));
        }
    }

    /// Triggers a negative effect for a slot machine if all trap icons are lined up in the middle
    // tg: code/game/machinery/slotmachine.dm:396
    public virtual async DmTask activate_trap(mob_living user = null)
    {
        await say(I18n.T("Big Loser! Prepare for your special prize!"));

        if (trap_path == typeof(obj_item_restraints_handcuffs))
        {
            if (iscarbon(user))
            {
                var carbon_user = (mob_living_carbon)user;
                playsound(loc as atom, Dm.resource("sound/items/weapons/handcuffs.ogg"), 30, TRUE, -2);
                await carbon_user.set_handcuffed(Dm.@new<obj_item_restraints_handcuffs>(user));
            }
        }
        else if (trap_path == typeof(obj_item_suspiciousphone))
        {
            playsound(loc as atom, Dm.resource("sound/items/dump_it.ogg"), 30, TRUE, -2);
            balance = 0;
        }
        else if (trap_path == typeof(obj_singularity))
            await user.electrocute_act(80, this, flags: Dm.bitor(SHOCK_ILLUSION, SHOCK_NOGLOVES));
        else // gibonite, syndicate bombs, flashbangs, etc.
        {
            var bang = Dm.@new<obj_item_grenade_flashbang>(get_turf(this));
            await bang.arm_grenade(null, 1 * SECONDS);
        }
    }

    /// Checks if any prizes have been won, and pays them out
    // tg: code/game/machinery/slotmachine.dm:415
    public virtual async DmTask give_prizes(object usrname = null, mob_living user = null)
    {
        float linelength = Dm.num(get_lines());
        bool did_player_win = true;

        if (trap_path != null && Dm.truthy(check_middle_row_all(trap_path)))
            await activate_trap(user);

        else if (Dm.truthy(check_middle_row_all(jackpot_path)))
        {
            winning = WINNING_JACKPOT;
            float prize = money + PRIZE_JACKPOT;
            await say(I18n.T("JACKPOT! You win [] []!", prize, MONEY_NAME));
            priority_announce(I18n.T("Congratulations to [] for winning the jackpot at the slot machine in []!", user != null ? user.real_name : usrname, get_area(this)));
            await user.add_mood_event(SLOTS_MOOD_CATEGORY, typeof(datum_mood_event_slots_win_jackpot));
            add_memory_in_range(user, 7, typeof(datum_memory_won_jackpot), Dm.named("protagonist", user), Dm.named("deuteragonist", this));
            jackpots += 1;
            money = 0;
            if (paymode == HOLOCHIP)
                Dm.@new<obj_item_holochip>(loc, prize);
            else
            {
                for (float i = 1; i <= 5; i++)
                {
                    cointype = pick(subtypesof(typeof(obj_item_coin))) as Type;
                    var payout_coin = (obj_item_coin)Dm.@new(cointype, loc);
                    await random_step(payout_coin, 2, 50);
                    playsound(this, pick(Dm.list(Dm.resource("sound/machines/coindrop.ogg"), Dm.resource("sound/machines/coindrop2.ogg"))), 50, TRUE);
                    await sleep(REEL_DEACTIVATE_DELAY);
                }
            }
        }

        else if (linelength == 5)
        {
            winning = WINNING_BIG;
            await say(I18n.T("Big Winner! You win a thousand []!", MONEY_NAME));
            await give_money(PRIZE_BIG);
            await user.add_mood_event(SLOTS_MOOD_CATEGORY, typeof(datum_mood_event_slots_win_big));
        }

        else if (linelength == 4)
        {
            winning = WINNING_SMALL;
            await say(I18n.T("Winner! You win four hundred []!", MONEY_NAME));
            await give_money(PRIZE_SMALL);
            await user.add_mood_event(SLOTS_MOOD_CATEGORY, typeof(datum_mood_event_slots_win));
        }

        else if (linelength == 3)
        {
            winning = WINNING_FREESPIN;
            balloon_alert(user, I18n.T("won 3 free games!"));
            balance += SPIN_PRICE * 4;
            money = max(money - SPIN_PRICE * 4, money);
        }

        else
        {
            winning = WINNING_NOTHING;
            balloon_alert(user, I18n.T("no luck!"));
            did_player_win = false;
            await user.add_mood_event(SLOTS_MOOD_CATEGORY, typeof(datum_mood_event_slots_loss));
        }

        playsound(this, Dm.resource("sound/machines/lever/lever_stop.ogg"), 50);

        SStgui.update_uis(this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_winning)), 3 * SECONDS, file: "code/game/machinery/slotmachine.dm", line: 471);

        if (did_player_win)
        {
            add_filter("jackpot_rays", 3, ray_filter);
            Dm.animate(get_filter("jackpot_rays"), Dm.named("offset", 10), Dm.named("time", 3 * SECONDS), Dm.named("loop", -1));
            _addtimer(Dm.@new<datum_callback>(this, nameof(remove_filter), "jackpot_rays"), 3 * SECONDS, file: "code/game/machinery/slotmachine.dm", line: 476);
            playsound(this, Dm.resource("sound/machines/roulette/roulettejackpot.ogg"), 50, TRUE);
        }
    }

    // tg: code/game/machinery/slotmachine.dm:479
    public virtual void clear_winning()
    {
        winning = WINNING_NOTHING;
    }

    /// Checks for a jackpot (5 matching symbols in the middle row) for the given symbol typepath
    // tg: code/game/machinery/slotmachine.dm:483
    public virtual object check_middle_row_all(object symbol_path = null)
    {
        string symbol_id = Dm.text("[]", symbol_path);
        foreach (DmList reel in Dm.each<DmList>(reels))
        {
            if (!Dm.eq(reel[2], symbol_id))
                return FALSE;
        }
        return TRUE;
    }

    /// Finds the largest number of consecutive matching icons in a row
    // tg: code/game/machinery/slotmachine.dm:491
    public virtual object get_lines()
    {
        float amountthesame = 0;

        for (float row = 1; row <= 3; row++)
        {
            object current_symbol = null;
            float current_run = 0;

            foreach (DmList reel in Dm.each<DmList>(reels))
            {
                object symbol = reel[row];
                if (Dm.eq(symbol, current_symbol))
                    current_run++;
                else
                {
                    current_symbol = symbol;
                    current_run = 1;
                }
                if (current_run >= 3)
                    amountthesame = max(amountthesame, current_run);
            }
        }

        return amountthesame;
    }

    /// Give the specified amount of money. If the amount is greater than the amount of prize money available, add the difference as balance
    // tg: code/game/machinery/slotmachine.dm:511
    public virtual async DmTask give_money(object amount = null)
    {
        float amount_to_give = min(Dm.num(amount), money);
        float surplus = Dm.num(amount) - Dm.num(await give_payout(amount_to_give));
        money -= amount_to_give;
        balance += surplus;

        if (house_bank_account != null)
            house_bank_account.adjust_money(-amount_to_give, "Slot Machine: Payout");
    }

    /// Pay out the specified amount in either coins or holochips
    // tg: code/game/machinery/slotmachine.dm:521
    public virtual async DmTask<object> give_payout(object amount = null)
    {
        if (paymode == HOLOCHIP)
            cointype = typeof(obj_item_holochip);
        else
            cointype = Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? typeof(obj_item_coin_iron) : typeof(obj_item_coin_silver);

        if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            amount = await dispense(Dm.num(amount), cointype, null, null);

        else
        {
            mob_living target = Dm.locate<mob_living>(range(2, this));

            // DEVIATION(DV663): tg passes the target as the throw flag and 1 as the target (dispense(amount, cointype, target, 1)), so its
            // emagged machine never throws; here the rigged payout is thrown at the player beside it, as the machine's comment means
            amount = await dispense(Dm.num(amount), cointype, TRUE, target);
        }

        return amount;
    }

    /// Dispense the given amount. If machine is set to use coins, will use the specified coin type.
    /// If throwit and target are set, will launch the payment at the target
    // tg: code/game/machinery/slotmachine.dm:539
    public virtual async DmTask<object> dispense(float? amount = null, Type cointype = null, float? throwit = null, mob_living target = null)
    {
        amount ??= 0;
        cointype ??= typeof(obj_item_coin_silver);
        throwit ??= FALSE;
        if (paymode == HOLOCHIP)
        {
            var chip = Dm.@new<obj_item_holochip>(loc, amount);

            if (Dm.truthy(throwit) && target != null)
                await chip.throw_at(target, 3, 10);
        }
        else
        {
            float value = Dm.num(coinvalues[Dm.text("[]", cointype)]);
            if (value <= 0)
                throw CRASH("Coin value of zero, refusing to payout in dispenser", "code/game/machinery/slotmachine.dm", 551);
            while (amount >= value)
            {
                var thrown_coin = (obj_item_coin)Dm.@new(cointype, loc); //DOUBLE THE PAIN
                amount -= value;
                if (Dm.truthy(throwit) && target != null)
                    await thrown_coin.throw_at(target, 3, 10);
                else
                    await random_step(thrown_coin, 2, 40);
            }
        }

        playsound(this, pick(Dm.list(Dm.resource("sound/machines/coindrop.ogg"), Dm.resource("sound/machines/coindrop2.ogg"))), 50, TRUE);
        return amount;
    }
}

// tg: code/game/machinery/slotmachine.dm:566
public partial class obj_machinery_computer_slot_machine_command : obj_machinery_computer_slot_machine
{
    // tg: code/game/machinery/slotmachine.dm:581
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.append(ref slot_adjectives, Dm.list("Royal", "Regal", "Golden", "Captain's", "Glorious"));
        Dm.append(ref slot_nouns, Dm.list("Medal", "Ransom", "Authority", "Command"));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/machinery/slotmachine.dm:586
public partial class obj_machinery_computer_slot_machine_security : obj_machinery_computer_slot_machine
{
    // tg: code/game/machinery/slotmachine.dm:601
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.append(ref slot_adjectives, Dm.list("Stunned", "Flashed", "Confiscated", "Loyal", "Arrested"));
        Dm.append(ref slot_nouns, Dm.list("Baton", "Donut", "Contraband", "Brig", "Security"));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/machinery/slotmachine.dm:606
public partial class obj_machinery_computer_slot_machine_medical : obj_machinery_computer_slot_machine
{
    // tg: code/game/machinery/slotmachine.dm:621
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.append(ref slot_adjectives, Dm.list("Mutated", "Overdosed", "Infectious", "Healing", "Husked"));
        Dm.append(ref slot_nouns, Dm.list("Medkit", "Defib", "Patient", "Doctor", "Cure"));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/machinery/slotmachine.dm:626
public partial class obj_machinery_computer_slot_machine_engineering : obj_machinery_computer_slot_machine
{
    // tg: code/game/machinery/slotmachine.dm:641
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.append(ref slot_adjectives, Dm.list("Supercharged", "Pressurized", "Radioactive", "Overloaded", "Delaminating", "Insulated"));
        Dm.append(ref slot_nouns, Dm.list("Toolbox", "Emitter", "Supermatter"));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/machinery/slotmachine.dm:646
public partial class obj_machinery_computer_slot_machine_cargo : obj_machinery_computer_slot_machine
{
    // tg: code/game/machinery/slotmachine.dm:661
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.append(ref slot_adjectives, Dm.list("Express", "Smuggled", "Stolen", "Overdue", "Subsidized", "Manifested"));
        Dm.append(ref slot_nouns, Dm.list("Bounty", "Crate", "Manifest", "MULE", "Profit"));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/machinery/slotmachine.dm:666
public partial class obj_machinery_computer_slot_machine_service : obj_machinery_computer_slot_machine
{
    // tg: code/game/machinery/slotmachine.dm:681
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.append(ref slot_adjectives, Dm.list("Fermented", "Seasoned", "Tipsy", "Cleaned", "Organic", "Culinary", "Refreshing", "Divine", "Holy"));
        Dm.append(ref slot_nouns, Dm.list("Recipe", "Cocktail", "Harvest", "Scrubber"));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/machinery/slotmachine.dm:686
public partial class obj_machinery_computer_slot_machine_science : obj_machinery_computer_slot_machine
{
    // tg: code/game/machinery/slotmachine.dm:701
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.append(ref slot_adjectives, Dm.list("Atomic", "Bluespace", "Cosmic", "Golden", "Diamond", "Silver", "Uranium", "Quantum", "Anomalous", "Plasma", "Experimental", "Robotic"));
        Dm.append(ref slot_nouns, Dm.list("Anomaly", "Artifact", "Slime", "Extract", "Circuit", "Discovery", "Explosion"));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/machinery/slotmachine.dm:706
public partial class obj_machinery_computer_slot_machine_clown : obj_machinery_computer_slot_machine
{
    // tg: code/game/machinery/slotmachine.dm:720
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.append(ref slot_adjectives, Dm.list("Honking", "Slippery", "Pranked", "Squeaky", "Hilarious", "Giggling"));
        Dm.append(ref slot_nouns, Dm.list("Banana", "Peel", "Prank", "Joke", "Punchline", "Candy", "Honk"));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/machinery/slotmachine.dm:725
public partial class obj_machinery_computer_slot_machine_mime : obj_machinery_computer_slot_machine
{
    // tg: code/game/machinery/slotmachine.dm:739
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.append(ref slot_adjectives, Dm.list("Silent", "Transparent", "Unspeakable", "Voiceless", "Quiet", "Hushed", "Invisible", "Imaginary", "Empty"));
        Dm.append(ref slot_nouns, Dm.list("Silence", "Mute", "Introvert", "Nothing", "Baguette"));
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/machinery/slotmachine.dm:744
public partial class obj_machinery_computer_slot_machine_syndicate : obj_machinery_computer_slot_machine
{
    // tg: code/game/machinery/slotmachine.dm:759
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.append(ref slot_adjectives, Dm.list("Covert", "Nuclear", "Suspicious", "Bloody", "Syndie", "Sabotaged", "Clandestine", "Illicit", "Traitorous"));
        Dm.append(ref slot_nouns, Dm.list("Telecrystal", "Uplink", "Bomb", "Operative", "Disk", "Nuke", "Syndicate", "Traitor"));
        return base.Initialize(mapload, __extra);
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/slotmachine.dm:70
    internal DmList obj_machinery_computer_slot_machine__coinvalues;

    // tg: code/game/machinery/slotmachine.dm:78
    internal DmList obj_machinery_computer_slot_machine__ray_filter = Dm.list(Dm.kv("type", "rays"), Dm.kv("y", 14), Dm.kv("size", 40), Dm.kv("density", 4), Dm.kv("color", COLOR_RED_LIGHT), Dm.kv("factor", 15), Dm.kv("flags", FILTER_OVERLAY));
}
