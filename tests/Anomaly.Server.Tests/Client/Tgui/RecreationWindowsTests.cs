using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// The recreation windows. The Holodeck (tg's Holodeck.jsx): the default programs, the loaded one lit, and the dangerous programs once
/// emagged; the OrionGame (OrionGame.jsx): the start screen, the journey with its location, settlers, mood faces and supplies, an
/// event's response buttons, and the game over; the BattleArcade (BattleArcade.tsx): the world map's unlocked worlds and the battle's
/// attacks; the SlotMachine (SlotMachine.tsx): the reels drawn from their symbols' icons, the balance and the spin; the Roulette
/// (Roulette/): the board's bets and the bet table. Each is registered under its name.
/// </summary>
public class RecreationWindowsTests
{
    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static (UiNode Tree, TguiContext Context, List<(string Action, UiValue Args)> Acts) Render(string name, string json, Func<TguiContext, UiNode> render, TguiLocalState local = null)
    {
        Assert.NotNull(TguiInterfaces.Find(name));
        var acts = new List<(string, UiValue)>();
        var context = new TguiContext(1, name, "w", 2, UiValue.FromJson(json), (action, args) => acts.Add((action, args)), local ?? new TguiLocalState());
        return (render(context), context, acts);
    }

    [Fact]
    public void TheHolodeckListsItsProgramsAndLoadsOne()
    {
        const string json = "{\"default_programs\":[{\"id\":\"holodeck_offline\",\"name\":\"Holodeck - Offline\"},{\"id\":\"holodeck_dodgeball\",\"name\":\"Holodeck - Dodgeball Court\"}]," +
            "\"program\":\"holodeck_offline\",\"can_toggle_safety\":0}";
        var (tree, context, acts) = Render("Holodeck", json, Holodeck.Render);
        Assert.Equal((400.0, 500.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Offline", ByKey(tree, "program:holodeck_offline").Text);
        Assert.True(ByKey(tree, "program:holodeck_offline").Selected);
        Assert.False(ByKey(tree, "program:holodeck_dodgeball").Selected);
        Assert.True(ByKey(tree, "safety").Disabled);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "emag");
        ByKey(tree, "program:holodeck_dodgeball").OnClick();
        (string action, UiValue args) = Assert.Single(acts);
        Assert.Equal("load_program", action);
        Assert.Equal("holodeck_dodgeball", args.Str("id"));

        const string emagged = "{\"default_programs\":[],\"emag_programs\":[{\"id\":\"holodeck_wildlifesim\",\"name\":\"Holodeck - Wildlife Simulation\"}]," +
            "\"program\":\"holodeck_wildlifesim\",\"can_toggle_safety\":1,\"emagged\":1}";
        (tree, _, _) = Render("Holodeck", emagged, Holodeck.Render);
        UiNode dangerous = ByKey(tree, "program:holodeck_wildlifesim");
        Assert.Equal("bad", dangerous.Color);
        Assert.Equal("Wildlife Simulation", dangerous.Text);
        Assert.False(ByKey(tree, "safety").Disabled);
        Assert.Equal("unlock", ByKey(tree, "safety").Icon);
    }

    [Fact]
    public void TheOrionGameStartsTravelsAndEnds()
    {
        var (tree, context, acts) = Render("OrionGame", "{\"gamename\":\"The Orion Trail\",\"gamestatus\":0}", OrionGame.Render);
        Assert.Equal("The Orion Trail", context.WindowTitle);
        Assert.Contains("Experience the journey of your ancestors!", tree.AllText());
        ByKey(tree, "start_game").OnClick();
        Assert.Equal("start_game", Assert.Single(acts).Action);

        const string journey = "{\"gamename\":\"The Orion Trail\",\"gamestatus\":2,\"turns\":2,\"settlers\":[\"Alice\",\"Bob\"],\"settlermoods\":{\"Alice\":7,\"Bob\":2}," +
            "\"food\":80,\"fuel\":60,\"hull\":1,\"electronics\":1,\"engine\":1,\"eventname\":null,\"eventtext\":null,\"buttons\":null,\"lings_suspected\":0}";
        (tree, _, acts) = Render("OrionGame", journey, OrionGame.Render);
        Assert.Contains("Asteroid Belt", tree.AllText());
        Assert.Contains(tree.Walk(), n => n.Key == "settler:Alice");
        UiNode face = ByKey(ByKey(tree, "settler:Alice"), "mood:Alice");
        Assert.Equal("icons/hud/screen_gen.dmi", face.DmIcon);
        Assert.Equal("mood8", face.DmIconState);
        ByKey(tree, "continue").OnClick();
        Assert.Equal("continue", Assert.Single(acts).Action);

        const string breakdown = "{\"gamename\":\"The Orion Trail\",\"gamestatus\":2,\"turns\":3,\"settlers\":[\"Alice\"],\"settlermoods\":{\"Alice\":5}," +
            "\"food\":70,\"fuel\":55,\"hull\":1,\"electronics\":1,\"engine\":1,\"eventname\":\"Engine Breakdown\",\"eventtext\":\"Oh no!\",\"buttons\":[\"Fix Engine\",\"Wait\"]}";
        (tree, _, acts) = Render("OrionGame", breakdown, OrionGame.Render);
        Assert.Contains("Engine Breakdown", tree.AllText());
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "continue");
        Assert.True(ByKey(tree, "kill:Alice").Disabled, "no killing in an event");
        ByKey(tree, "Fix Engine").OnClick();
        Assert.Equal("Fix Engine", Assert.Single(acts).Action);

        (tree, _, acts) = Render("OrionGame", "{\"gamename\":\"The Orion Trail\",\"gamestatus\":3,\"reason\":\"You ran out of food and starved.\"}", OrionGame.Render);
        Assert.Contains("Game Over", tree.AllText());
        Assert.Contains("You ran out of food and starved.", tree.AllText());
        ByKey(tree, "back_to_menu").OnClick();
        Assert.Equal("back_to_menu", Assert.Single(acts).Action);
    }

    [Fact]
    public void TheBattleArcadeOpensItsUnlockedWorldsAndAttacks()
    {
        const string map = "{\"ui_panel\":\"World Map\",\"player_current_hp\":100,\"max_hp\":100,\"player_current_mp\":10,\"max_mp\":10,\"player_gold\":0," +
            "\"equipped_gear\":[],\"latest_unlocked_world_position\":1,\"all_worlds\":[\"The Plains\",\"The Forest\"]}";
        var (tree, context, acts) = Render("BattleArcade", map, BattleArcade.Render);
        Assert.Equal((420.0, 415.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains("No gear equipped!", tree.AllText());
        Assert.False(ByKey(tree, "world:0").Disabled);
        Assert.True(ByKey(tree, "world:1").Disabled, "the forest is not unlocked yet");
        ByKey(tree, "world:0").OnClick();
        (string action, UiValue args) = Assert.Single(acts);
        Assert.Equal("start_fight", action);
        Assert.Equal("The Plains", args.Str("selected_arena"));

        const string battle = "{\"ui_panel\":\"Battle\",\"player_current_hp\":90,\"max_hp\":100,\"player_current_mp\":10,\"max_mp\":10,\"player_gold\":5," +
            "\"equipped_gear\":[{\"slot\":\"Weapon\",\"name\":\"Wooden Sword\"}],\"enemy_name\":\"Slime\",\"enemy_hp\":40,\"enemy_max_hp\":50,\"enemy_mp\":5," +
            "\"enemy_icon_id\":\"boss1.gif\",\"feedback_message\":\"You hit!\",\"attack_types\":[{\"name\":\"Attack\",\"tooltip\":\"Hit it.\"},{\"name\":\"Defend\",\"tooltip\":\"Block.\"}]}";
        (tree, _, acts) = Render("BattleArcade", battle, BattleArcade.Render);
        Assert.Contains("Slime's HP: 40/50", tree.AllText());
        Assert.Contains("Weapon: Wooden Sword", tree.AllText());
        Assert.Equal("icons/ui/arcade/boss1.gif", ByKey(tree, "enemy-image").DmIcon);
        ByKey(tree, "attack:0").OnClick();
        Assert.Equal("Attack", Assert.Single(acts).Action);
    }

    [Fact]
    public void TheSlotMachineDrawsItsReelsAndSpins()
    {
        const string json = "{\"symbols\":[{\"id\":\"/obj/item/a\",\"icon\":\"icons/obj/a.dmi\",\"icon_state\":\"a\"},{\"id\":\"/obj/item/b\",\"icon\":\"icons/obj/b.dmi\",\"icon_state\":\"b\"}]," +
            "\"cost\":5,\"jackpot\":10000,\"jackpot_id\":\"/obj/item/a\",\"reels\":[{\"symbols\":[\"/obj/item/a\",\"/obj/item/b\",\"/obj/item/a\"]}," +
            "{\"symbols\":[\"/obj/item/b\",\"/obj/item/b\",\"/obj/item/a\"]}],\"balance\":50,\"working\":0,\"winning\":0,\"money\":3000,\"plays\":1,\"jackpots\":0,\"paymode\":1}";
        var (tree, _, acts) = Render("SlotMachine", json, SlotMachine.Render);
        Assert.Equal("50 cr", ByKey(tree, "balance-value").Text);
        UiNode reel = ByKey(tree, "reel:1");
        UiNode middle = ByKey(ByKey(reel, "row:1"), "symbol");
        Assert.Equal("icons/obj/b.dmi", middle.DmIcon);
        Assert.Equal("b", middle.DmIconState);
        Assert.False(ByKey(tree, "refund").Disabled);
        ByKey(tree, "spin").OnClick();
        Assert.Equal("spin", Assert.Single(acts).Action);
        ByKey(tree, "refund").OnClick();
        Assert.Equal("payout", acts[1].Action);
    }

    [Fact]
    public void TheRouletteBoardPicksABetAndItsAmount()
    {
        const string json = "{\"IsAnchored\":1,\"BetAmount\":10,\"BetType\":\"s1-12\",\"HouseBalance\":5000,\"LastSpin\":3,\"Spinning\":0}";
        var (tree, _, acts) = Render("Roulette", json, Roulette.Render);
        Assert.Equal("red", ByKey(tree, "bet:3").Color);
        Assert.Equal("black", ByKey(tree, "bet:2").Color);
        Assert.Equal("red", ByKey(tree, "last-spin").Color);
        Assert.Equal("10 cr on 1-12", ByKey(tree, "current").Text);
        Assert.Equal("House Balance: 5000 cr", ByKey(tree, "house-balance").Text);
        Assert.Equal("Bolted", ByKey(tree, "anchor").Text);
        ByKey(tree, "bet:red").OnClick();
        Assert.Equal(("ChangeBetType", "red"), (acts[0].Action, acts[0].Args.Str("type")));
        ByKey(tree, "amount:100").OnClick();
        Assert.Equal(("ChangeBetAmount", 100.0), (acts[1].Action, acts[1].Args.Num("amount")));
    }
}
