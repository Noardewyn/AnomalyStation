using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/BattleArcade.tsx: the battle arcade's player stats and
// gear above one of its panels: the inn's shop and rest, the world map, the battle with the enemy's picture and the attacks, the
// campfire between fights and the game over. The pictures are tg's (icons/ui/arcade/). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class BattleArcade
{
    // tg: BattleArcade.tsx:38
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> gear = data.Arr("equipped_gear");
        var stats = new UiNode(UiKind.Section) { Key = "stats", Align = "center", TextAlign = "center" }.With(
            new UiNode(UiKind.Box) { Key = "title", Text = "Player stats" },
            new UiNode(UiKind.Box) { Key = "numbers" }.With(
                new UiNode(UiKind.Box) { Key = "hp", Text = "HP: " + TguiFormat.JsNumber(data.Num("player_current_hp")) + "/" + TguiFormat.JsNumber(data.Num("max_hp")), Color = "#c91212" },
                new UiNode(UiKind.Box) { Key = "mp", Text = "MP: " + TguiFormat.JsNumber(data.Num("player_current_mp")) + "/" + TguiFormat.JsNumber(data.Num("max_mp")), Color = "#0783b5" },
                new UiNode(UiKind.Box) { Key = "gold", Text = TguiFormat.JsNumber(data.Num("player_gold")) + "G", Color = "#b8c10b" }));
        if (gear.Count == 0)
            stats.Children.Add(new UiNode(UiKind.Box) { Key = "no-gear", Text = "No gear equipped!" });
        for (int i = 0; i < gear.Count; i++)
            stats.Children.Add(new UiNode(UiKind.Box) { Key = "gear:" + i, Text = gear[i].Str("slot") + ": " + gear[i].Str("name") });

        UiNode panel = data.Str("ui_panel") switch
        {
            "Shop" => ShopPanel(c, data),
            "World Map" => WorldMapPanel(c, data),
            "Battle" => BattlePanel(c, data),
            "Between Battle" => BetweenBattlePanel(c),
            _ => GameOverPanel(c),
        };
        return c.Window(420, 415, null, stats, panel);
    }

    private static UiNode Wide(string key, string icon, string text, Action click, string tooltip = null) =>
        new UiNode(UiKind.Button) { Key = key, Icon = icon, Width = "100%", Text = text, Tooltip = tooltip, OnClick = click };

    // tg: BattleArcade.tsx:84
    private static UiNode ShopPanel(TguiContext c, UiValue data)
    {
        double cost = data.Num("cost_of_items");
        double modifier = data.Num("unlocked_world_modifier");
        var section = new UiNode(UiKind.Section) { Key = "shop", Align = "center", TextAlign = "center" }.With(
            new UiNode(UiKind.Box) { Key = "welcome", Text = "Welcome to the Inn!" },
            new UiNode(UiKind.Box) { Key = "shopkeeper", DmIcon = "icons/ui/arcade/shopkeeper.png", Width = "8em", Height = "8em" },
            new UiNode(UiKind.Box)
            {
                Key = "blurb",
                Text = "Feel free to browse our wares, or take a nap. I'll be here to ensure you'll get a good night's rest without worry of ambushing or robbery.",
            });
        IReadOnlyList<UiValue> items = data.Arr("shop_items");
        for (int i = 0; i < items.Count; i++)
        {
            string item = items[i].AsStr();
            section.Children.Add(Wide("item:" + i, "shield", item + ": " + TguiFormat.JsNumber(cost * modifier) + "G", () => c.Act("buy_item", ("purchasing_item", item))));
        }
        section.Children.Add(Wide("sleep", "bed", "Rest " + TguiFormat.JsNumber(cost / 2) + "G", () => c.Act("sleep")));
        section.Children.Add(Wide("leave", "arrow-left", "Leave Inn", () => c.Act("leave")));
        return section;
    }

    // tg: BattleArcade.tsx:118
    private static UiNode WorldMapPanel(TguiContext c, UiValue data)
    {
        double unlocked = data.Num("latest_unlocked_world_position");
        var section = new UiNode(UiKind.Section) { Key = "world-map", Align = "center", TextAlign = "center" }.With(
            new UiNode(UiKind.Box) { Key = "title", Text = "WORLD MAP", Icon = "map" },
            new UiNode(UiKind.Box) { Key = "blurb", Text = "The further down you go, the harder the enemies will be, but the more loot you will get." },
            Wide("enter-inn", "house", "Enter Inn", () => c.Act("enter_inn")));
        IReadOnlyList<UiValue> worlds = data.Arr("all_worlds");
        for (int i = 0; i < worlds.Count; i++)
        {
            string world = worlds[i].AsStr();
            UiNode button = Wide("world:" + i, "fist-raised", world, () => c.Act("start_fight", ("selected_arena", world)));
            button.Disabled = i >= unlocked;
            section.Children.Add(button);
        }
        return section;
    }

    // tg: BattleArcade.tsx:150
    private static UiNode BattlePanel(TguiContext c, UiValue data)
    {
        string enemy = data.Str("enemy_name");
        var section = new UiNode(UiKind.Section) { Key = "battle", Align = "center", TextAlign = "center" };
        string feedback = data.Str("feedback_message");
        if (!string.IsNullOrEmpty(feedback))
            section.Children.Add(new UiNode(UiKind.Box) { Key = "feedback", Text = feedback });
        section.Children.Add(new UiNode(UiKind.Box) { Key = "enemy-hp", Text = enemy + "'s HP: " + TguiFormat.JsNumber(data.Num("enemy_hp")) + "/" + TguiFormat.JsNumber(data.Num("enemy_max_hp")) });
        section.Children.Add(new UiNode(UiKind.Box) { Key = "enemy-mp", Text = enemy + "'s MP: " + TguiFormat.JsNumber(data.Num("enemy_mp")) });
        section.Children.Add(new UiNode(UiKind.Box) { Key = "enemy-image", DmIcon = "icons/ui/arcade/" + data.Str("enemy_icon_id"), Width = "10em", Height = "10em" });
        IReadOnlyList<UiValue> attacks = data.Arr("attack_types");
        for (int i = 0; i < attacks.Count; i++)
        {
            string name = attacks[i].Str("name");
            section.Children.Add(Wide("attack:" + i, "fist-raised", name, () => c.Act(name), attacks[i].Str("tooltip")));
        }
        section.Children.Add(c.ButtonConfirm("flee", Wide("flee", "shoe-prints", "Flee (Lose half your Gold)", () => c.Act("flee"))));
        return section;
    }

    // tg: BattleArcade.tsx:192
    private static UiNode BetweenBattlePanel(TguiContext c) =>
        new UiNode(UiKind.Section) { Key = "between", Align = "center", TextAlign = "center" }.With(
            new UiNode(UiKind.Box) { Key = "fireplace", DmIcon = "icons/ui/arcade/fireplace.png", Width = "10em", Height = "10em" },
            new UiNode(UiKind.Box)
            {
                Key = "blurb",
                Text = "As night sets, you can choose to rest. This will restore your health and mana to full before the next battle, but you may also be ambushed, forcing you into the next fight without any additional healing.",
            },
            Wide("rest", "bed", "Attempt to sleep", () => c.Act("continue_with_rest")),
            Wide("no-rest", "fire", "Leave without sleeping", () => c.Act("continue_without_rest")),
            Wide("abandon", "shoe-prints", "Abandon Quest", () => c.Act("abandon_quest")));

    // tg: BattleArcade.tsx:224
    private static UiNode GameOverPanel(TguiContext c) =>
        new UiNode(UiKind.Section) { Key = "game-over", Align = "center", TextAlign = "center" }.With(
            new UiNode(UiKind.Box) { Key = "title", Color = "red", FontSize = "32px", Text = "Game Over" },
            new UiNode(UiKind.Button) { Key = "restart", Fluid = true, Icon = "arrow-left", Text = "Main Menu", OnClick = () => c.Act("restart") });
}
