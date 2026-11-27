using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The casino in a toy room (machinery/slotmachine.dm, roulette_machine.dm): a holochip put into the slot machine becomes its balance,
/// its SlotMachine window spins the reels for five credits and a line of five pays a thousand credits in a holochip; the window pays the
/// balance back out; three in a row give free spins; the multitool switches it to coins. The roulette wheel linked to its owner's card
/// takes a player's bet from its Roulette window and the swipe of the player's card, spins, and pays a winner from the owner's account
/// in poker chips, or keeps a loser's bet.
/// </summary>
public class CasinoTests
{
    private sealed class Casino
    {
        public AudioRoom R;
        public Feed Feed;
        public obj_machinery_computer_slot_machine Slots;
        public obj_machinery_roulette Wheel;
        public mob_living_carbon_human Bob;
    }

    private static Casino Room(ulong seed)
    {
        var c = new Casino();
        c.R = new AudioRoom(13, 11, seed: seed, mapExtras: w =>
        {
            c.Slots = Dm.@new<obj_machinery_computer_slot_machine>(locate(3, 5, 1));
            c.Wheel = Dm.@new<obj_machinery_roulette>(locate(5, 6, 1));
        });
        c.R.In(() => (get_area(locate(4, 5, 1)) as area).requires_power = FALSE);
        c.Feed = Listen(c.R);
        c.Bob = c.R.Join(4, 5, "Bob");
        c.R.In(() => Waiting.NoSleep(c.Bob.fully_replace_character_name(c.Bob.real_name, "Bob")));
        c.R.Step(Second);
        return c;
    }

    /// The slot machine's first Initialize makes one of every coin to learn their values (slotmachine.dm:99-102), the mythril coin
    /// included, whose material reaches its listed leaf, the fantasy affixes (VarBlockLeaves): that runtime is expected, no other is.
    private static void AssertNoOtherRuntimes(Casino c) =>
        Assert.Empty(Anomaly.Core.Tests.Tools.Types.VarBlockLeaves.Unexpected(typeof(obj_item_coin_mythril), c.R.Room.RuntimeText.ToList()));

    private static void Hold(Casino c, obj_item item) => c.R.In(() =>
    {
        foreach (obj_item held in Dm.each<obj_item>(c.Bob.held_items).ToList())
            Waiting.NoSleep(c.Bob.dropItemToGround(held));
        Assert.True(Dm.truthy(Waiting.NoSleep(c.Bob.put_in_active_hand(item))));
    });

    private static string OpenSlots(Casino c, out JsonElement payload)
    {
        c.R.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(c.Bob.held_items).ToList())
                Waiting.NoSleep(c.Bob.dropItemToGround(held));
        });
        Take(c.Bob);
        Click(c.R, c.Bob, c.Slots);
        c.R.Step(4);
        payload = LastFullUpdate(Take(c.Bob), out string window);
        return window;
    }

    private static float Holochips(Casino c) => c.R.In(() =>
        Dm.each<obj_item_holochip>(get_turf(c.Slots)).Sum(h => Dm.num(h.credits)));

    /// Sets the middle row of every reel to one symbol (the reels are a list of five reels of three symbols).
    private static void MiddleRow(Casino c, int count, Type symbol, Type other) => c.R.In(() =>
    {
        int i = 0;
        foreach (DmList reel in Dm.each<DmList>(c.Slots.reels))
        {
            reel[2] = Dm.text("[]", i < count ? symbol : other);
            reel[1] = Dm.text("[]", i % 2 == 0 ? other : symbol);
            reel[3] = Dm.text("[]", i % 2 == 0 ? other : symbol);
            i++;
        }
    });

    /// Two of the machine's symbols that are neither its jackpot nor its trap.
    private static (Type A, Type B) PlainSymbols(Casino c) => c.R.In(() =>
    {
        var plain = Dm.each<Type>(c.Slots.symbol_paths).Where(s => s != c.Slots.jackpot_path && s != c.Slots.trap_path).ToList();
        return (plain[0], plain[1]);
    });

    [Fact]
    public void A_holochip_buys_spins_and_a_line_of_five_pays_a_thousand_credits()
    {
        var c = Room(14320);
        string window = OpenSlots(c, out JsonElement payload);
        Assert.Equal("SlotMachine", InterfaceOf(payload));
        JsonElement statics = payload.GetProperty("static_data");
        Assert.Equal(5, statics.GetProperty("cost").GetInt32());
        Assert.Equal(10000, statics.GetProperty("jackpot").GetInt32());
        Assert.True(statics.GetProperty("symbols").GetArrayLength() > 0, "the symbols with their pictures");
        JsonElement firstSymbol = statics.GetProperty("symbols")[0];
        Assert.False(string.IsNullOrEmpty(firstSymbol.GetProperty("icon").GetString()));
        Assert.Equal(5, payload.GetProperty("data").GetProperty("reels").GetArrayLength());

        // no balance: the lever does nothing
        Act(c.R, c.Bob, window, "spin");
        Assert.True(c.Feed.Any("balloon: insufficient balance!"));

        Hold(c, c.R.In(() => Dm.@new<obj_item_holochip>(c.Bob.loc, 100)));
        Click(c.R, c.Bob, c.Slots);
        c.R.Step(2);
        Assert.Equal(100f, c.R.In(() => c.Slots.balance));
        Assert.True(c.Feed.Any("balloon: 100 credits inserted"));

        c.R.In(() => Waiting.NoSleep(c.Bob.dropItemToGround(c.Bob.get_active_held_item() as obj_item)));
        float money = c.R.In(() => c.Slots.money); // 3000 and a credit every two seconds of its process
        Act(c.R, c.Bob, window, "spin");
        Assert.Equal(95f, c.R.In(() => c.Slots.balance));
        Assert.Equal(money + 5, c.R.In(() => c.Slots.money));
        Assert.Equal(1f, c.R.In(() => c.Slots.working));
        Assert.True(c.Feed.Any("Bob pulls the lever and the slot machine starts spinning!"));
        Assert.Contains(c.R.Sounds.Heard, h => h.File == "sound/machines/lever/lever_start.ogg");

        // the reels stop on five of a kind in the middle row
        (Type a, Type b) = PlainSymbols(c);
        MiddleRow(c, 5, a, b);
        c.R.Step(5 * Second);
        Assert.Equal(0f, c.R.In(() => c.Slots.working));
        Assert.True(c.Feed.Any("Big Winner! You win a thousand credits!"));
        Assert.Equal(1000f, Holochips(c));
        Assert.InRange(c.R.In(() => c.Slots.money), money + 5 - 1000, money + 5 - 1000 + 3);
        Assert.Contains(c.R.Sounds.Heard, h => h.File == "sound/machines/roulette/roulettejackpot.ogg");

        // the payout button gives the rest of the balance back
        Act(c.R, c.Bob, window, "payout");
        Assert.Equal(0f, c.R.In(() => c.Slots.balance));
        Assert.Equal(1095f, Holochips(c));
        AssertNoOtherRuntimes(c);
    }

    [Fact]
    public void Three_in_a_row_give_free_spins_and_nothing_loses_the_bet()
    {
        var c = Room(14321);
        string window = OpenSlots(c, out _);
        c.R.In(() => c.Slots.balance = 10);
        (Type a, Type b) = PlainSymbols(c);

        Act(c.R, c.Bob, window, "spin");
        MiddleRow(c, 3, a, b);
        c.R.Step(5 * Second);
        Assert.True(c.Feed.Any("balloon: won 3 free games!"));
        Assert.Equal(25f, c.R.In(() => c.Slots.balance)); // 10 - 5 + 4 spins

        Act(c.R, c.Bob, window, "spin");
        c.R.In(() =>
        {
            // no run of three anywhere: the symbols alternate on every row
            int i = 0;
            foreach (DmList reel in Dm.each<DmList>(c.Slots.reels))
            {
                for (int row = 1; row <= 3; row++)
                    reel[row] = Dm.text("[]", (i + row) % 2 == 0 ? a : b);
                i++;
            }
        });
        c.R.Step(5 * Second);
        Assert.True(c.Feed.Any("balloon: no luck!"));
        Assert.Equal(20f, c.R.In(() => c.Slots.balance));
        Assert.Equal(0f, Holochips(c));
        Assert.NotNull(c.R.In(() => c.Bob.mob_mood?.mood_events["slots"]));
        AssertNoOtherRuntimes(c);
    }

    [Fact]
    public void The_multitool_switches_the_slot_machine_to_coins()
    {
        var c = Room(14322);
        Hold(c, c.R.In(() => Dm.@new<obj_item_multitool>(c.Bob.loc)));
        Click(c.R, c.Bob, c.Slots);
        c.R.Step(2);
        Assert.Equal(2f, c.R.In(() => c.Slots.paymode));
        Assert.True(c.Feed.Any("balloon: now using coins"));
        // a coin in: its value is the balance (the machine may spit one coin in fifty back out)
        c.R.In(() => c.R.World.Rng.Reseed(14322, 1));
        Hold(c, c.R.In(() => Dm.@new<obj_item_coin_gold>(c.Bob.loc)));
        Click(c.R, c.Bob, c.Slots);
        c.R.Step(2);
        float value = c.R.In(() => Dm.num(obj_machinery_computer_slot_machine.coinvalues["/obj/item/coin/gold"])); // its credit value
        Assert.Equal(value, c.R.In(() => c.Slots.balance));
        // a payout in coins: silver coins worth the balance
        c.R.In(() => Waiting.NoSleep(c.Slots.give_payout(c.Slots.balance)));
        Assert.NotEmpty(c.R.In(() => Dm.each<obj_item_coin_silver>(get_turf(c.Slots)).ToList()));
        AssertNoOtherRuntimes(c);
    }

    private static (datum_bank_account Owner, datum_bank_account Player, obj_item_card_id Card) LinkWheel(Casino c, float house, float wallet) => c.R.In(() =>
    {
        var ownerCard = Dm.@new<obj_item_card_id_advanced>(c.Wheel.loc);
        var owner = Dm.@new<datum_bank_account>("Ann", SSjob.get_job_type(typeof(datum_job_assistant)), 1, TRUE);
        owner.adjust_money(house);
        ownerCard.set_account(owner);
        c.Wheel.my_card = ownerCard;
        c.Wheel.power_change();
        var card = Dm.@new<obj_item_card_id_advanced>(c.Bob.loc);
        var player = Dm.@new<datum_bank_account>("Bob", SSjob.get_job_type(typeof(datum_job_assistant)), 1, TRUE);
        player.adjust_money(wallet);
        card.set_account(player);
        return (owner, player, card);
    });

    [Fact]
    public void The_roulette_takes_a_bet_spins_and_settles_it()
    {
        var c = Room(14323);
        (datum_bank_account owner, datum_bank_account player, obj_item_card_id card) = LinkWheel(c, 5000, 1000);
        float house = c.R.In(() => Dm.num(owner.account_balance));
        float wallet = c.R.In(() => Dm.num(player.account_balance));

        Take(c.Bob);
        Click(c.R, c.Bob, c.Wheel);
        c.R.Step(4);
        JsonElement payload = LastFullUpdate(Take(c.Bob), out string window);
        Assert.Equal("Roulette", InterfaceOf(payload));
        Assert.Equal(13, payload.GetProperty("data").GetProperty("LastSpin").GetInt32());
        Assert.Equal(house, payload.GetProperty("data").GetProperty("HouseBalance").GetSingle());

        Act(c.R, c.Bob, window, "ChangeBetAmount", ("amount", 100));
        Act(c.R, c.Bob, window, "ChangeBetType", ("type", "red"));
        Assert.Equal(100f, c.R.In(() => c.Wheel.chosen_bet_amount));
        Assert.Equal("red", c.R.In(() => c.Wheel.chosen_bet_type));

        // the swipe of the player's card: the bet goes to the owner and the wheel spins
        Hold(c, card);
        Click(c.R, c.Bob, c.Wheel);
        c.R.Step(Second / 2); // the wheel rises for four deciseconds first
        Assert.Equal(1f, c.R.In(() => c.Wheel.playing));
        Assert.Equal(wallet - 100, c.R.In(() => Dm.num(player.account_balance)));
        Assert.Equal(house + 100, c.R.In(() => Dm.num(owner.account_balance)));
        Assert.Contains(c.R.Sounds.Heard, h => h.File == "sound/machines/roulette/roulettewheel.ogg");

        // the result, red or not, settles the bet
        c.R.Step(8 * Second);
        Assert.Equal(0f, c.R.In(() => c.Wheel.playing));
        float rolled = c.R.In(() => c.Wheel.last_spin);
        bool red = rolled != 0 && IsRed(rolled);
        Assert.True(c.Feed.Any("The result is: " + rolled + " " + (rolled == 0 ? "green" : red ? "red" : "black")));
        if (red)
        {
            Assert.True(c.Feed.Any("You have won 200 credits! Congratulations!"));
            Assert.Equal(house - 100, c.R.In(() => Dm.num(owner.account_balance)));
        }
        else
        {
            Assert.True(c.Feed.Any("You lost! Better luck next time."));
            Assert.Equal(house + 100, c.R.In(() => Dm.num(owner.account_balance)));
        }
        AssertNoOtherRuntimes(c);
    }

    // tg: Roulette/helpers.tsx getNumberColor, as the wheel's numbers list colours them
    private static bool IsRed(float n) => (n >= 1 && n <= 10) || (n >= 19 && n <= 28) ? n % 2 == 1 : n % 2 == 0;

    [Fact]
    public void A_winning_number_pays_out_in_coins_and_a_losing_one_pays_nothing()
    {
        var c = Room(14324);
        (datum_bank_account owner, _, obj_item_card_id card) = LinkWheel(c, 5000, 1000);

        // 3 is red: a red bet of 100 pays 200 from the owner's account, dropped south of the wheel in poker chips
        object won = c.R.In(() => Waiting.NoSleep(c.Wheel.finish_play(card, "red", 100, 200, 3, c.Bob)));
        Assert.True(c.Feed.Any("The result is: 3 red"));
        Assert.True(c.Feed.Any("You have won 200 credits! Congratulations!"));
        Assert.Equal(4800f, c.R.In(() => Dm.num(owner.account_balance)));
        c.R.Step(5 * Second); // one coin every 3 deciseconds
        float coins = c.R.In(() => Dm.each<obj_item_poker_chip>(get_step(c.Wheel.loc as atom, SOUTH)).Sum(chip => Dm.num(obj_machinery_roulette.coin_values[chip.GetType()])));
        Assert.Equal(200f, coins);
        Assert.Equal(3f, c.R.In(() => c.Wheel.last_spin));

        // 20 is outside the first dozen: the house keeps the bet
        object lost = c.R.In(() => Waiting.NoSleep(c.Wheel.finish_play(card, "s1-12", 50, 150, 20, c.Bob)));
        Assert.Equal(0f, Dm.num(lost));
        Assert.True(c.Feed.Any("You lost! Better luck next time."));
        Assert.Equal(4800f, c.R.In(() => Dm.num(owner.account_balance)));

        // the zero wins only a bet on zero: even and odd both lose on it
        Assert.Equal(0f, c.R.In(() => Dm.num(c.Wheel.check_win("even", 10, 0))));
        Assert.Equal(1f, c.R.In(() => Dm.num(c.Wheel.check_win("0", 10, 0))));
        Assert.Equal(1f, c.R.In(() => Dm.num(c.Wheel.check_win("s3rd col", 10, 36))));

        // the department's card is refused
        c.R.Step(2 * Second);
        var budget = c.R.In(() => Dm.@new<obj_item_card_id_departmental_budget_car>(c.Bob.loc));
        Hold(c, budget);
        Click(c.R, c.Bob, c.Wheel);
        c.R.Step(4);
        Assert.True(c.Feed.Any("You cannot gamble with the department budget!"), string.Join(" | ", c.Feed.Chat.TakeLast(6).Select(m => m.Html)));
        AssertNoOtherRuntimes(c);
    }
}
