using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package P4a (D327): the economy in a booted toy room. The departmental budgets start split (cargo with nothing) and a payday
/// pays the crew from their department; an ID card takes cash into its account and gives a holochip back with Alt-click; NT Pay sends
/// credits to the account of a pay token; the account lookup console advances a paycheck and changes a pay modifier; the vault's bank
/// machine takes cash into the cargo budget and siphons it out as space cash; a holographic pay stand takes a forced fee into its
/// owner's account; a coin is worth its material.
/// </summary>
public class EconomyTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE); // the toy room has no APC
        // the economy fires once soon after the round starts (its first payday and grants); the next fire is five minutes on
        for (int i = 0; i < 30 && r.In(() => SSeconomy.times_fired) < 1; i++)
            r.Step(Second);
        Assert.Equal(1f, r.In(() => SSeconomy.times_fired));
        return (r, feed, bob);
    }

    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static datum_bank_account Account(AudioRoom r, string holder, Type job) => r.In(() =>
        Dm.@new<datum_bank_account>(holder, SSjob.get_job_type(job), 1, TRUE));

    private static object ProgramAct(AudioRoom r, mob user, datum_computer_file_program program, string action, DmList @params) => r.In(() =>
    {
        World w = World.Current;
        object caller = w.usr;
        w.usr = user;
        try
        {
            return Waiting.NoSleep(program.ui_act(action, @params));
        }
        finally
        {
            w.usr = caller;
        }
    });

    [Fact]
    public void The_budgets_start_split_and_a_payday_pays_the_crew_from_their_department()
    {
        var (r, feed, bob) = Room(9501);
        r.In(() =>
        {
            // the economy fires at once after its Initialize (tg's MC), so each budget already has its grants
            float grants = MAX_GRANT_DPT * SSeconomy.times_fired;
            Assert.Equal(7, SSeconomy.departmental_accounts.len);
            Assert.Equal(grants, SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance); // cargo starts with nothing
            Assert.Equal(round(35000f / 7 - 1) + grants, SSeconomy.get_dep_account(ACCOUNT_CIV).account_balance);
            Assert.Equal("Civil Budget", SSeconomy.get_dep_account(ACCOUNT_CIV).account_holder);
        });

        // an assistant's account, as a job's spawn makes it: five starting paychecks from the void
        datum_bank_account account = Account(r, "Bob", typeof(datum_job_assistant));
        r.In(() => Waiting.NoSleep(account.payday(STARTING_PAYCHECKS, free: TRUE)));
        Assert.Equal(25f * STARTING_PAYCHECKS, r.In(() => account.account_balance)); // the assistant's PAYCHECK_LOWER
        Assert.Matches("^[A-Z]{2}-[A-Z]-[0-9]{4}$", r.In(() => Dm.str(account.pay_token)));
        Assert.Same(account, r.In(() => SSeconomy.bank_accounts_by_id[Dm.text("[]", account.account_id)]));

        // the economy's fire: every budget gets its grant, then the crew's paydays come out of their department
        float civ = r.In(() => SSeconomy.get_dep_account(ACCOUNT_CIV).account_balance);
        float car = r.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance);
        r.In(() =>
        {
            // as the MC runs it (its MC_TICK_CHECK pauses a subsystem that is not running)
            SSeconomy.state = SS_RUNNING;
            Waiting.NoSleep(SSeconomy.fire(0));
            SSeconomy.state = SS_IDLE;
        });
        Assert.Equal(25f * STARTING_PAYCHECKS + 25, r.In(() => account.account_balance));
        Assert.Equal(civ + MAX_GRANT_DPT - 25, r.In(() => SSeconomy.get_dep_account(ACCOUNT_CIV).account_balance));
        Assert.Equal(car + MAX_GRANT_DPT, r.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance));
        Assert.True(r.In(() => SSeconomy.mail_waiting) >= 1);

        // a debt takes three quarters of what comes in
        r.In(() =>
        {
            account.account_debt = 100;
            account.adjust_money(40, "test");
        });
        Assert.Equal(25f * STARTING_PAYCHECKS + 25 + 10, r.In(() => account.account_balance));
        Assert.Equal(70f, r.In(() => account.account_debt));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_id_card_takes_cash_into_its_account_and_gives_a_holochip_back()
    {
        var (r, feed, bob) = Room(9502);
        datum_bank_account account = VendingTests.Funds(r, bob, 100);
        var card = r.In(() => bob.wear_id as obj_item_card_id);

        // a holochip and a coin pressed on the card go into the account
        r.In(() =>
        {
            var chip = Dm.@new<obj_item_holochip>(bob.loc, 50);
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(card.item_interaction(bob, chip)));
            Assert.True(QDELETED(chip));
            var coin = Dm.@new<obj_item_coin_gold>(bob.loc);
            Assert.True(Dm.num(coin.value) > 0); // a coin is worth its material
            float value = Dm.num(coin.value);
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(card.item_interaction(bob, coin)));
            Assert.Equal(150 + value, account.account_balance);
            account.account_balance = 150;
        });
        Assert.True(feed.Any("You insert the 50 credit holochip into"), Diag(feed, r));

        // Alt-click with the card in hand withdraws into a holochip, the amount asked in a number modal
        r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(card));
            Waiting.NoSleep(bob.put_in_active_hand(card));
        });
        Take(bob);
        Click(r, bob, card, "alt=1");
        JsonElement modal = LastUpdate(Take(bob), out string window);
        Assert.Equal("NumberInputModal", InterfaceOf(modal));
        Assert.Equal(150, modal.GetProperty("static_data").GetProperty("max_value").GetInt32());
        Act(r, bob, window, "submit", ("entry", 30));
        r.Step(Second);
        Assert.Equal(120f, r.In(() => account.account_balance));
        var withdrawn = r.In(() => bob.get_inactive_held_item() as obj_item_holochip);
        Assert.NotNull(withdrawn);
        Assert.Equal(30f, r.In(() => Dm.num(withdrawn.credits)));
        Assert.Equal("holochip", r.In(() => withdrawn.icon_state));
        Assert.True(feed.Any("You withdraw 30 credits into a holochip."), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Nt_pay_sends_credits_to_the_account_of_a_pay_token()
    {
        var (r, feed, bob) = Room(9503);
        datum_bank_account mine = VendingTests.Funds(r, bob, 100);
        datum_bank_account theirs = Account(r, "Carol Doe", typeof(datum_job_cook));
        var pda = r.In(() =>
        {
            var made = Dm.@new<obj_item_modular_computer_pda>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(made)); // the card in the PDA in hand talks to its holder
            var card = bob.wear_id as obj_item_card_id;
            Waiting.NoSleep(made.insert_id(card, bob));
            return made;
        });
        var pay = r.In(() => pda.find_file_by_name("ntpay") as datum_computer_file_program_nt_pay);
        Assert.NotNull(pay);
        DmList data = r.In(() => Waiting.NoSleep(pay.ui_data(bob)) as DmList);
        Assert.Equal(r.In(() => mine.pay_token), r.In(() => data["owner_token"]));

        // the token is looked up by name, then the transfer goes to it
        ProgramAct(r, bob, pay, "GetPayToken", r.In(() => Dm.list(Dm.kv("wanted_name", "Carol Doe"))));
        Assert.Equal(r.In(() => "Token: " + Dm.str(theirs.pay_token)), r.In(() => Dm.str(pay.wanted_token)));
        ProgramAct(r, bob, pay, "Transaction", r.In(() => Dm.list(Dm.kv("token", theirs.pay_token), Dm.kv("amount", 40))));
        Assert.Equal(60f, r.In(() => mine.account_balance));
        Assert.Equal(40f, r.In(() => theirs.account_balance));
        Assert.Equal("Transfer: To Carol Doe", r.In(() => Dm.str(((DmList)mine.transaction_history[mine.transaction_history.len])["reason"])));
        Assert.True(feed.Any("You send 40 credits(s) to Carol Doe."), Diag(feed, r));

        // too much is refused
        ProgramAct(r, bob, pay, "Transaction", r.In(() => Dm.list(Dm.kv("token", theirs.pay_token), Dm.kv("amount", 1000))));
        Assert.Equal(60f, r.In(() => mine.account_balance));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_account_lookup_console_advances_a_paycheck_and_changes_a_pay_modifier()
    {
        var (r, feed, bob) = Room(9504);
        datum_bank_account account = Account(r, "Dan Roe", typeof(datum_job_station_engineer));
        var console = r.In(() => Dm.@new<obj_machinery_computer_accounting>(locate(5, 4, 1)));
        r.Step(Second);
        Take(bob);
        Click(r, bob, console);
        JsonElement payload = LastUpdate(Take(bob), out string window);
        Assert.Equal("AccountingConsole", InterfaceOf(payload));
        JsonElement row = payload.GetProperty("data").GetProperty("accounts").EnumerateArray().Single(a => a.GetProperty("name").GetString() == "Dan Roe");
        string id = row.GetProperty("id").GetString();
        Assert.Equal("Station Engineer", row.GetProperty("job").GetString());
        float eng = r.In(() => SSeconomy.get_dep_account(ACCOUNT_ENG).account_balance);

        Act(r, bob, window, "paycheck_advance", ("account_id", id));
        Assert.Equal(50f, r.In(() => account.account_balance)); // the engineer's PAYCHECK_CREW, from the engineering budget
        Assert.Equal(eng - 50, r.In(() => SSeconomy.get_dep_account(ACCOUNT_ENG).account_balance));
        Assert.Equal(1f, r.In(() => account.paydays_to_skip)); // the next payday is skipped
        r.In(() => Waiting.NoSleep(account.payday(1, skippable: TRUE)));
        Assert.Equal(50f, r.In(() => account.account_balance));

        Act(r, bob, window, "change_pay_mod", ("account_id", id), ("pay_mod", "1.23"));
        Assert.Equal(1.25f, r.In(() => Dm.num(account.payday_modifier)), 3);
        Assert.Contains(r.In(() => Dm.each_cast<DmList>(SSeconomy.audit_log).Select(e => Dm.str(e["vendor"])).ToList()), v => v == "Paycheck raised");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_bank_machine_takes_cash_into_the_cargo_budget_and_siphons_it_out_as_space_cash()
    {
        var (r, feed, bob) = Room(9505);
        var machine = r.In(() => Dm.@new<obj_machinery_computer_bank_machine>(locate(5, 4, 1)));
        r.In(() =>
        {
            var chip = Dm.@new<obj_item_holochip>(bob.loc, 1000);
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(machine.item_interaction(bob, chip)));
        });
        r.Step(2);
        float budget = r.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance); // the round start's grant and the 1000
        Assert.True(feed.Any(Dm.text("Credits deposited! The Cargo Budget is now [] cr.", budget)), Diag(feed, r));

        r.In(() => machine.start_siphon(bob));
        Assert.Equal(1f, r.In(() => machine.unauthorized)); // no vault access
        r.Step(10 * Second); // SSmachines' process, 100 credits a second
        float siphoned = r.In(() => machine.syphoning_credits);
        Assert.True(siphoned > 0, "siphoned " + siphoned);
        Assert.Equal(budget - siphoned, r.In(() => SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance));
        r.In(() => machine.end_siphon());
        float cash = r.In(() => Dm.each<obj_item_stack_spacecash>(machine.loc).Sum(s => Dm.num(s.get_item_credit_value())));
        Assert.Equal(siphoned, cash);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_holographic_pay_stand_takes_a_forced_fee_into_its_owners_account()
    {
        var (r, feed, bob) = Room(9506);
        datum_bank_account mine = VendingTests.Funds(r, bob, 0);
        var card = r.In(() => bob.wear_id as obj_item_card_id);
        r.In(() =>
        {
            // a pay stand needs a floor (can_proj_holopay); the toy room's indestructible turf is not one
            var tile = Waiting.NoSleep((locate(5, 4, 1) as turf).ChangeTurf(typeof(turf_open_floor_iron))) as turf;
            card.set_holopay_fee(20);
            Waiting.NoSleep(card.try_project_paystand(bob, tile));
        });
        r.Step(2);
        var stand = r.In(() => card.my_store);
        Assert.True(stand != null, Diag(feed, r));
        Assert.Equal(20f, r.In(() => stand.force_fee));
        Assert.Equal(r.In(() => "Pays directly into " + bob.real_name + "'s bank account."), r.In(() => Dm.str(stand.desc)));

        mob_living_carbon_human carol = r.Dummy(5, 5);
        r.In(() =>
        {
            var chip = Dm.@new<obj_item_holochip>(carol.loc, 50);
            Waiting.NoSleep(carol.put_in_active_hand(chip));
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(stand.item_interaction(carol, chip)));
            Assert.Equal(30f, Dm.num(chip.credits));
        });
        r.Step(2);
        Assert.Equal(20f, r.In(() => mine.account_balance));
        Assert.True(feed.Any("has deposited 20 cr at your holographic pay stand."), Diag(feed, r));

        // the stand vanishes when its card goes out of range
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(10, 8, 1))));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(stand)));
        r.AssertNoRuntimes();
    }
}
