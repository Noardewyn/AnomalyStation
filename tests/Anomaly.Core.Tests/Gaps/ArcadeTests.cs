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
/// The arcade cabinets in a toy room (machinery/computer/arcade/): The Orion Trail's journey through its window (a new game, the
/// turns, an event and its response, the market, the win at Orion with its tickets and the game over when the food runs out), its
/// Realism Mode emag; the battle arcade's fight against an enemy with its turns and its win's tickets and gold; the arcade tickets
/// traded for a prize; Mediborg's Amputation Adventure taking a hand for its tickets.
/// </summary>
public class ArcadeTests
{
    private sealed class Arcade
    {
        public AudioRoom R;
        public Feed Feed;
        public obj_machinery_computer_arcade_orion_trail Orion;
        public obj_machinery_computer_arcade_battle Battle;
        public obj_machinery_computer_arcade_amputation Amputation;
        public mob_living_carbon_human Bob;
    }

    private static Arcade Room(ulong seed)
    {
        var a = new Arcade();
        a.R = new AudioRoom(13, 11, seed: seed, mapExtras: w =>
        {
            a.Orion = Dm.@new<obj_machinery_computer_arcade_orion_trail>(locate(3, 5, 1));
            a.Battle = Dm.@new<obj_machinery_computer_arcade_battle>(locate(5, 6, 1));
            a.Amputation = Dm.@new<obj_machinery_computer_arcade_amputation>(locate(5, 4, 1));
        });
        a.Feed = Listen(a.R);
        a.Bob = a.R.Join(4, 5, "Bob");
        a.R.In(() => Waiting.NoSleep(a.Bob.fully_replace_character_name(a.Bob.real_name, "Bob")));
        a.R.Step(Second);
        return a;
    }

    private static string Open(Arcade a, atom machine, out JsonElement payload)
    {
        a.R.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(a.Bob.held_items).ToList())
                Waiting.NoSleep(a.Bob.dropItemToGround(held));
        });
        Take(a.Bob);
        Click(a.R, a.Bob, machine);
        a.R.Step(4);
        payload = LastFullUpdate(Take(a.Bob), out string window);
        return window;
    }

    private static float Tickets(Arcade a, atom near) => a.R.In(() =>
        Dm.each<obj_item_stack_arcadeticket>(get_turf(near)).Sum(t => Dm.num(t.amount)));

    [Fact]
    public void Orion_Trail_starts_a_game_travels_and_reaches_Orion_for_two_tickets()
    {
        var a = Room(14310);
        string window = Open(a, a.Orion, out JsonElement payload);
        Assert.Equal("OrionGame", InterfaceOf(payload));
        Assert.Equal("The Orion Trail", payload.GetProperty("static_data").GetProperty("gamename").GetString());
        Assert.Equal(0, payload.GetProperty("data").GetProperty("gamestatus").GetInt32());

        Act(a.R, a.Bob, window, "start_game");
        JsonElement data = Data(a.R, a.Bob, out _);
        Assert.Equal(2, data.GetProperty("gamestatus").GetInt32());
        Assert.Equal(4, data.GetProperty("settlers").GetArrayLength());
        Assert.Contains(data.GetProperty("settlers").EnumerateArray(), s => s.GetString() == "Bob");
        Assert.Equal(80, data.GetProperty("food").GetDouble());

        // a turn: food for four, five fuel
        a.R.In(() => a.R.World.Rng.Reseed(14310, 1));
        a.R.In(() =>
        {
            a.Orion.@event = null;
            a.Orion.turns = 2;
        });
        Act(a.R, a.Bob, window, "continue");
        a.R.Step(4);
        Assert.Equal(3f, a.R.In(() => a.Orion.turns));
        Assert.Equal(55f, a.R.In(() => Dm.num(a.Orion.fuel)));

        // an engine breakdown fixed with a spare part
        Take(a.Bob);
        a.R.In(() =>
        {
            Waiting.NoSleep(a.Orion.encounter_event(typeof(datum_orion_event_engine_part), a.Bob, 0, 0, 0));
            SStgui.update_uis(a.Orion);
        });
        data = Data(a.R, a.Bob, out _);
        Assert.Equal("Engine Breakdown", data.GetProperty("eventname").GetString());
        Assert.Contains(data.GetProperty("buttons").EnumerateArray(), b => b.GetString() == "Fix Engine");
        Act(a.R, a.Bob, window, "Fix Engine");
        a.R.Step(4);
        Assert.Null(a.R.In(() => a.Orion.@event));
        Assert.Equal(0f, a.R.In(() => Dm.num(a.Orion.engine)));

        // the last turn reaches Orion: two tickets
        a.R.In(() => a.Orion.turns = 9);
        Act(a.R, a.Bob, window, "continue");
        a.R.Step(4);
        Assert.Equal(0f, a.R.In(() => a.Orion.gameStatus));
        Assert.True(a.Feed.Any("Congratulations, you made it to Orion!"));
        Assert.Equal(2f, Tickets(a, a.Orion));
        Assert.Contains(a.R.Sounds.Heard, h => h.File == "sound/machines/arcade/win.ogg");
        a.R.AssertNoRuntimes();
    }

    [Fact]
    public void Orion_Trail_ends_when_the_food_runs_out_and_the_market_trades()
    {
        var a = Room(14311);
        string window = Open(a, a.Orion, out _);
        Act(a.R, a.Bob, window, "start_game");
        a.R.Step(2);

        // the market at a space port
        a.R.In(() =>
        {
            Waiting.NoSleep(a.Orion.encounter_event(typeof(datum_orion_event_space_port), a.Bob, 0, 0, 0));
        });
        Act(a.R, a.Bob, window, "Dock");
        JsonElement data = Data(a.R, a.Bob, out _);
        Assert.Equal(4, data.GetProperty("gamestatus").GetInt32());
        float fuel = a.R.In(() => Dm.num(a.Orion.fuel));
        Act(a.R, a.Bob, window, "buyparts", ("part", 1));
        a.R.Step(2);
        Assert.Equal(fuel - 5, a.R.In(() => Dm.num(a.Orion.fuel)));
        Assert.Equal(2f, a.R.In(() => Dm.num(a.Orion.engine)));
        Act(a.R, a.Bob, window, "leave_spaceport");
        a.R.Step(2);
        Assert.Equal(2f, a.R.In(() => a.Orion.gameStatus));

        // no food left: game over with tg's reason
        a.R.In(() =>
        {
            a.Orion.food = 1;
            a.Orion.turns = 5;
        });
        a.R.In(() => a.R.World.Rng.Reseed(14311, 1));
        Act(a.R, a.Bob, window, "continue");
        data = Data(a.R, a.Bob, out _);
        Assert.Equal(3, data.GetProperty("gamestatus").GetInt32());
        Assert.Equal("You ran out of food and starved.", data.GetProperty("reason").GetString());
        // tg's turn goes on after its game over (orion.dm:240, no return): it may roll an event onto the game-over screen, which then
        // takes the next act as the event's response; the menu button is reached once no event is pending
        a.R.In(() => a.Orion.@event = null);
        Act(a.R, a.Bob, window, "back_to_menu");
        a.R.Step(2);
        Assert.Equal(0f, a.R.In(() => a.Orion.gameStatus));
        a.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_emag_turns_on_Orion_Trails_realism_mode()
    {
        var a = Room(14312);
        bool emagged = a.R.In(() => Dm.truthy(Waiting.NoSleep(a.Orion.emag_act(a.Bob))));
        Assert.True(emagged);
        Assert.Equal("The Orion Trail: Realism Edition", a.R.In(() => a.Orion.name));
        Assert.True(a.Feed.Any("Realism Mode."));
        Assert.Equal(2f, a.R.In(() => a.Orion.gameStatus)); // the emag starts a new game
        // the screwdriver will not take the cabinet apart while it is emagged
        var screwdriver = a.R.In(() => Dm.@new<obj_item_screwdriver>(a.Bob.loc));
        Assert.Equal(ITEM_INTERACT_BLOCKING, Dm.num(a.R.In(() => Waiting.NoSleep(a.Orion.screwdriver_act(a.Bob, screwdriver)))));
        // a win in realism mode gives the explosive model ship and turns the cabinet back to normal
        a.R.In(() => Waiting.NoSleep(a.Orion.win(a.Bob)));
        Assert.Single(a.R.In(() => Dm.each<obj_item_orion_ship>(a.Orion.loc).ToList()));
        Assert.Equal("The Orion Trail", a.R.In(() => a.Orion.name));
        Assert.False(Dm.truthy(Dm.bitand(a.R.In(() => a.Orion.obj_flags), EMAGGED)));
        a.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_battle_arcade_fights_an_enemy_and_pays_its_win()
    {
        var a = Room(14313);
        string window = Open(a, a.Battle, out JsonElement payload);
        Assert.Equal("BattleArcade", InterfaceOf(payload));
        Assert.Equal("World Map", payload.GetProperty("data").GetProperty("ui_panel").GetString());
        Assert.Equal(9, payload.GetProperty("static_data").GetProperty("all_worlds").GetArrayLength());

        Act(a.R, a.Bob, window, "start_fight", ("selected_arena", "The Plains"));
        JsonElement data = Data(a.R, a.Bob, out _);
        Assert.Equal("Battle", data.GetProperty("ui_panel").GetString());
        float enemy_hp = a.R.In(() => Dm.num(a.Battle.enemy_hp));
        Assert.InRange(enemy_hp, 90, 125);

        // an attack, then the enemy's turn a second later
        a.R.In(() => a.R.World.Rng.Reseed(14313, 1));
        Act(a.R, a.Bob, window, "Attack");
        a.R.Step(2);
        Assert.True(a.R.In(() => Dm.num(a.Battle.enemy_hp)) < enemy_hp, "the enemy took damage");
        Assert.Equal(0f, a.R.In(() => a.Battle.player_turn));
        a.R.Step(2 * Second);
        Assert.Equal(1f, a.R.In(() => a.Battle.player_turn));

        // the killing blow: two tickets, the enemy's gold and the rest between fights
        float gold = a.R.In(() => a.Battle.player_gold);
        a.R.In(() => a.Battle.enemy_hp = 1);
        Act(a.R, a.Bob, window, "Attack");
        a.R.Step(2 * Second);
        Assert.Equal("Between Battle", a.R.In(() => a.Battle.ui_panel));
        Assert.True(a.R.In(() => a.Battle.player_gold) > gold, "the enemy's gold");
        Assert.Equal(2f, Tickets(a, a.Battle));
        a.R.AssertNoRuntimes();
    }

    [Fact]
    public void Two_arcade_tickets_buy_a_prize()
    {
        var a = Room(14314);
        var tickets = a.R.In(() =>
        {
            var stack = Dm.@new<obj_item_stack_arcadeticket>(a.Bob.loc, 3);
            Assert.True(Dm.truthy(Waiting.NoSleep(a.Bob.put_in_active_hand(stack))));
            return stack;
        });
        a.R.Step(2);
        int before = a.R.In(() => Dm.each<atom_movable>(get_turf(a.Orion)).Count());
        Click(a.R, a.Bob, a.Orion);
        a.R.Step(4);
        Assert.Equal(1f, a.R.In(() => Dm.num(tickets.amount)));
        Assert.Equal(before + 1, a.R.In(() => Dm.each<atom_movable>(get_turf(a.Orion)).Count()));
        Assert.True(a.Feed.Any("balloon: prize claimed"));
        Assert.Contains(a.R.Sounds.Heard, h => h.File == "sound/machines/machine_vend.ogg");
        Assert.NotNull(a.R.In(() => a.Bob.mob_mood?.mood_events["arcade"]));

        // one ticket is not enough
        Click(a.R, a.Bob, a.Orion);
        a.R.Step(4);
        Assert.True(a.Feed.Any("balloon: need 2 tickets!"));
        a.R.AssertNoRuntimes();
    }

    [Fact]
    public void The_amputation_arcade_takes_a_hand_for_its_tickets()
    {
        var a = Room(14315);
        obj_item_bodypart hand = a.R.In(() => a.Bob.get_active_hand() as obj_item_bodypart);
        Assert.NotNull(hand);
        a.R.In(() => a.R.World.Rng.Reseed(14315, 1));
        Click(a.R, a.Bob, a.Amputation);
        a.R.Step(6 * Second);
        Assert.True(a.Feed.Any("The guillotine drops on your arm, and the machine sucks it in!"));
        Assert.True(QDELETED(hand), "the hand went into the machine");
        Assert.InRange(Tickets(a, a.Amputation), 6, 10);
        a.R.AssertNoRuntimes();
    }
}
