using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/OrionGame.jsx: The Orion Trail's screens: the title,
// the instructions, the journey (the location or event with its response buttons, and the adventure status: the settlers with their
// mood faces and kill buttons, the supplies), the game over and the space port's market. The mood faces are tg's moods sheet: the
// mood states of icons/hud/screen_gen.dmi tinted with the sheet's colours (asset_cache/assets/moods.dm). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class OrionGame
{
    // tg: OrionGame.jsx:22
    private static readonly Dictionary<string, string> ButtonIcons = new()
    {
        ["Restore Hull"] = "wrench", ["Fix Engine"] = "rocket", ["Repair Electronics"] = "server", ["Wait"] = "clock", ["Continue"] = "arrow-right",
        ["Explore Ship"] = "door-open", ["Leave the Derelict"] = "arrow-right", ["Welcome aboard."] = "user-plus", ["Where did you go?!"] = "user-minus",
        ["A good find."] = "box-open", ["Continue travels."] = "arrow-right", ["Keep Speed"] = "tachometer-alt", ["Slow Down"] = "arrow-left",
        ["Speed Past"] = "tachometer-alt", ["Go Around"] = "redo", ["Oh..."] = "circle", ["Dock"] = "dollar-sign",
    };

    // tg: OrionGame.jsx:50
    private static readonly (string Title, string Blurb)[] LocationInfo =
    {
        ("Pluto", "Pluto, long since occupied with long-range sensors and scanners, stands ready to, and indeed continues to probe the far reaches of the galaxy."),
        ("Asteroid Belt", "At the edge of the Sol system lies a treacherous asteroid belt. Many have been crushed by stray asteroids and misguided judgement."),
        ("Proxima Centauri", "The nearest star system to Sol, in ages past it stood as a reminder of the boundaries of sub-light travel, now a low-population sanctuary for adventurers and traders."),
        ("Dead Space", "This region of space is particularly devoid of matter. Such low-density pockets are known to exist, but the vastness of it is astounding."),
        ("Rigel Prime", "Rigel Prime, the center of the Rigel system, burns hot, basking its planetary bodies in warmth and radiation."),
        ("Tau Ceti Beta", "Tau Ceti Beta has recently become a waypoint for colonists headed towards Orion. There are many ships and makeshift stations in the vicinity."),
        ("Space Bugs", "You see some space bugs out your window. They contort in various reality bending ways, and it makes you sick. You know it's Galactic Policy to report all sightings of space bugs."),
        ("Space Outpost Beta-9", "You have come into range of the first man-made structure in this region of space. It has been constructed not by travellers from Sol, but by colonists from Orion. It stands as a monument to the colonists' success."),
        ("Orion Prime", "You have made it to Orion! Congratulations! Your crew is one of the few to start a new foothold for mankind!"),
    };

    // tg: code/modules/asset_cache/assets/moods.dm:5  the moods sheet's colours, mood1 to mood9
    private static readonly string[] MoodColors = { "#f15d36", "#f38943", "#f38943", "#dfa65b", "#4b96c4", "#86d656", "#2eeb9a", "#2eeb9a", "#2eeb9a" };

    // tg: OrionGame.jsx:625
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        int status = (int)data.Num("gamestatus");
        UiNode screen = data.Str("eventname") == "Space Port Raid" ? Normal(c, data) : status switch
        {
            0 => Start(c, data),
            1 => Instructions(c),
            2 => Normal(c, data),
            3 => GameOver(c, data),
            4 => Market(c, data),
            _ => Start(c, data),
        };
        return c.Window(420, 535, data.Str("gamename"), new UiNode(UiKind.Box) { Key = "content", Scrollable = true }.With(screen));
    }

    // a mood face of the moods sheet (class "moods32x32 mood<n>")
    private static UiNode Mood(string key, int mood)
    {
        int index = Math.Clamp(mood, 1, MoodColors.Length);
        return new UiNode(UiKind.Box) { Key = key, DmIcon = "icons/hud/screen_gen.dmi", DmIconState = "mood" + index, Color = MoodColors[index - 1] };
    }

    private static UiNode Supply(string key, string icon, string text, string color) =>
        new UiNode(UiKind.StackItem) { Key = key }.With(new UiNode(UiKind.Button) { Key = key + "-button", Fluid = true, Icon = icon, Text = text, Color = color });

    private static UiNode Supplies(string food, string fuel, string hull, string electronics, string engine) =>
        new UiNode(UiKind.Stack) { Key = "supplies", Vertical = true, Fill = true, Justify = "center" }.With(
            Supply("food", "hamburger", "Food Left: " + food, "green"),
            Supply("fuel", "gas-pump", "Fuel Left: " + fuel, "olive"),
            Supply("hull", "wrench", "Hull Parts: " + hull, "average"),
            Supply("electronics", "server", "Electronics: " + electronics, "blue"),
            Supply("engine", "rocket", "Engine Parts: " + engine, "violet"));

    // tg: OrionGame.jsx:101
    private static UiNode AdventureStatus(TguiContext c, UiValue data)
    {
        bool lingsSuspected = data.Truthy("lings_suspected");
        bool inEvent = !string.IsNullOrEmpty(data.Str("eventname"));
        UiValue moods = data.Field("settlermoods");
        var settlers = new UiNode(UiKind.StackItem) { Key = "settlers", Grow = 1 };
        foreach (UiValue settler in data.Arr("settlers"))
        {
            string who = settler.AsStr();
            settlers.Children.Add(new UiNode(UiKind.Stack) { Key = "settler:" + who, Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "name", Grow = 1, Text = who },
                new UiNode(UiKind.StackItem) { Key = "kill" }.With(new UiNode(UiKind.Button)
                {
                    Key = "kill:" + who, Fluid = true, Color = "red", TextAlign = "center", Icon = "skull", Text = "KILL", Disabled = lingsSuspected || inEvent,
                    OnClick = () => c.Act("target_kill", ("who", who)),
                }),
                new UiNode(UiKind.StackItem) { Key = "mood" }.With(Mood("mood:" + who, (int)moods.Num(who) + 1))));
        }
        return new UiNode(UiKind.Section)
        {
            Key = "adventure-status",
            Title = "Adventure Status",
            Fill = true,
            Buttons = lingsSuspected
                ? new UiNode(UiKind.Button)
                {
                    Key = "random-kill", Fluid = true, Color = "black", TextAlign = "center", Icon = "skull", Text = "RANDOM KILL", Disabled = inEvent,
                    OnClick = () => c.Act("random_kill"),
                }
                : null,
        }.With(new UiNode(UiKind.Stack) { Key = "status-stack", Fill = true }.With(
            settlers,
            new UiNode(UiKind.Divider) { Key = "divider", Vertical = true },
            new UiNode(UiKind.StackItem) { Key = "supplies-item" }.With(Supplies(
                TguiFormat.JsNumber(data.Num("food")), TguiFormat.JsNumber(data.Num("fuel")), TguiFormat.JsNumber(data.Num("hull")),
                TguiFormat.JsNumber(data.Num("electronics")), TguiFormat.JsNumber(data.Num("engine"))))));
    }

    // tg: OrionGame.jsx:188
    private static UiNode Start(TguiContext c, UiValue data) =>
        new UiNode(UiKind.Section) { Key = "start", Fill = true }.With(new UiNode(UiKind.Stack) { Key = "start-stack", Vertical = true, Fill = true, Justify = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "gap1", Grow = 1 },
            new UiNode(UiKind.StackItem) { Key = "gamename", FontSize = "32px", TextAlign = "center", Text = data.Str("gamename") },
            new UiNode(UiKind.StackItem) { Key = "tagline", Grow = 1, FontSize = "15px", Color = "label", TextAlign = "center", Text = "\"Experience the journey of your ancestors!\"" },
            new UiNode(UiKind.StackItem) { Key = "begin" }.With(new UiNode(UiKind.Button) { Key = "start_game", Fluid = true, FontSize = "15px", Icon = "play", Text = "Begin Game", OnClick = () => c.Act("start_game") }),
            new UiNode(UiKind.StackItem) { Key = "instructions" }.With(new UiNode(UiKind.Button) { Key = "instructions-button", Fluid = true, FontSize = "15px", Icon = "info", Text = "Instructions", OnClick = () => c.Act("instructions") }),
            new UiNode(UiKind.StackItem) { Key = "gap2", Grow = 3 }));

    // tg: OrionGame.jsx:224
    private static UiNode Instructions(TguiContext c)
    {
        string[] fakeSettlers = { "John", "William", "Alice", "Tom" };
        var example = new UiNode(UiKind.StackItem) { Key = "example-settlers", Grow = 1 };
        foreach (string settler in fakeSettlers)
        {
            example.Children.Add(new UiNode(UiKind.Stack) { Key = "settler:" + settler, Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "name", Grow = 1, Text = settler },
                new UiNode(UiKind.StackItem) { Key = "kill" }.With(new UiNode(UiKind.Button) { Key = "kill", Fluid = true, Color = "red", TextAlign = "center", Icon = "skull", Text = "KILL" }),
                new UiNode(UiKind.StackItem) { Key = "mood" }.With(Mood("mood", 5))));
        }
        return new UiNode(UiKind.Fragment) { Key = "instructions-screen" }.With(
            new UiNode(UiKind.Section)
            {
                Key = "objective", Color = "label", Title = "Objective",
                Buttons = new UiNode(UiKind.Button) { Key = "back", Text = "Back to Main Menu", OnClick = () => c.Act("back_to_menu") },
            }.With(new UiNode(UiKind.Box)
            {
                Key = "objective-text", FontSize = "11px",
                Text = "In the 2200's, the Orion trail was established as a dangerous yet opportunistic trail through space for those willing to risk it. Many pioneers seeking new lives on the galactic frontier would find exactly what they were seeking... or lose their lives on the way.",
            }),
            new UiNode(UiKind.Section) { Key = "status-example", Title = "Status Example" }.With(new UiNode(UiKind.Stack) { Key = "status-stack" }.With(
                example,
                new UiNode(UiKind.Divider) { Key = "divider", Vertical = true },
                new UiNode(UiKind.StackItem) { Key = "status-text" }.With(
                    new UiNode(UiKind.Box) { Key = "p1", Text = "This is the status panel for your pioneers. Each one requires 1 food every time you continue towards Orion. You can find more crew on your journey, and lose them as fast as you found 'em." },
                    new UiNode(UiKind.Box) { Key = "p2", Text = "If you run out of food or crew, it's GAME OVER for you!" }))),
            new UiNode(UiKind.Section) { Key = "resources", Title = "Resources" }.With(new UiNode(UiKind.Stack) { Key = "resources-stack" }.With(
                new UiNode(UiKind.StackItem) { Key = "resources-text", Grow = 1 }.With(
                    new UiNode(UiKind.Box) { Key = "intro", Text = "If you want to make it to Orion, you'll need to manage your resources:" },
                    new UiNode(UiKind.Box) { Key = "food", Text = "Food: Your crewmembers consume it. More crew means this goes down faster!", Color = "lightgreen" },
                    new UiNode(UiKind.Box) { Key = "fuel", Text = "Fuel: You use 5u of fuel with every movement. Don't let it run out.", Color = "olive" },
                    new UiNode(UiKind.Box) { Key = "parts", Text = "Parts: Used to repair breakdowns. Nobody likes wasting time on repairs!", Color = "yellow" }),
                new UiNode(UiKind.Divider) { Key = "divider", Vertical = true },
                new UiNode(UiKind.StackItem) { Key = "supplies-item" }.With(Supplies("80", "60", "1", "1", "1")))));
    }

    // tg: OrionGame.jsx:334
    private static UiNode Normal(TguiContext c, UiValue data)
    {
        string eventName = data.Str("eventname");
        string eventText = data.Str("eventtext");
        int turns = (int)data.Num("turns");
        (string Title, string Blurb) location = turns >= 1 && turns <= LocationInfo.Length ? LocationInfo[turns - 1] : ("", "");
        var buttons = new UiNode(UiKind.StackItem) { Key = "buttons" };
        IReadOnlyList<UiValue> eventButtons = data.Arr("buttons");
        if (data.Field("buttons") is { IsNull: false })
        {
            foreach (UiValue button in eventButtons)
            {
                string name = button.AsStr();
                buttons.Children.Add(new UiNode(UiKind.StackItem) { Key = "response:" + name }.With(new UiNode(UiKind.Button)
                {
                    Key = name, Width = "16em", Icon = ButtonIcons.GetValueOrDefault(name), Text = name, OnClick = () => c.Act(name),
                }));
            }
        }
        else
            buttons.Children.Add(new UiNode(UiKind.Button) { Key = "continue", Width = "16em", Icon = "arrow-right", Text = "Continue", OnClick = () => c.Act("continue") });
        return new UiNode(UiKind.Stack) { Key = "normal", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "location-item", Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "location", Title = !string.IsNullOrEmpty(eventName) ? "Event" : "Location", Fill = true }.With(
                new UiNode(UiKind.Stack) { Key = "location-stack", Fill = true, TextAlign = "center", Vertical = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "text", Grow = 1 }.With(
                        new UiNode(UiKind.Box) { Key = "title", Bold = true, FontSize = "15px", Text = !string.IsNullOrEmpty(eventName) ? eventName : location.Title },
                        new UiNode(UiKind.Box) { Key = "blurb", FontSize = "15px", Text = !string.IsNullOrEmpty(eventText) ? eventText : location.Blurb }),
                    buttons))),
            new UiNode(UiKind.StackItem) { Key = "status-item" }.With(AdventureStatus(c, data)));
    }

    // tg: OrionGame.jsx:405
    private static UiNode GameOver(TguiContext c, UiValue data) =>
        new UiNode(UiKind.Section) { Key = "gameover", Fill = true }.With(new UiNode(UiKind.Stack) { Key = "gameover-stack", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "gap1", Grow = 1 },
            new UiNode(UiKind.StackItem) { Key = "title", Color = "red", FontSize = "32px", TextAlign = "center", Text = "Game Over" },
            new UiNode(UiKind.StackItem) { Key = "reason", Grow = 1, FontSize = "15px", Color = "label", TextAlign = "center", Text = data.Str("reason") },
            new UiNode(UiKind.StackItem) { Key = "menu" }.With(new UiNode(UiKind.Button) { Key = "back_to_menu", Fluid = true, FontSize = "15px", Icon = "arrow-left", Text = "Main Menu", OnClick = () => c.Act("back_to_menu") }),
            new UiNode(UiKind.StackItem) { Key = "gap2", Grow = 3 }));

    private static UiNode MarketButton(TguiContext c, string key, string icon, string text, string color, string action, params (string Key, object Value)[] parameters) =>
        new UiNode(UiKind.StackItem) { Key = key }.With(new UiNode(UiKind.Button) { Key = key + "-button", Fluid = true, Icon = icon, Text = text, Color = color, OnClick = () => c.Act(action, parameters) });

    // tg: OrionGame.jsx:436
    private static UiNode Market(TguiContext c, UiValue data)
    {
        bool raided = data.Truthy("spaceport_raided");
        int turns = (int)data.Num("turns");
        var stack = new UiNode(UiKind.Stack) { Key = "market-stack", Fill = true, TextAlign = "center", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "welcome", Grow = 1 }.With(
                new UiNode(UiKind.Box) { Key = "port", Bold = true, FontSize = "15px", Text = turns == 4 ? "Tau Ceti Beta" : "Small Space Port" },
                raided
                    ? new UiNode(UiKind.Box) { Key = "raided", FontSize = "14px", Color = "red", Text = "You are lucky to have escaped with your life. Attempting to dock again would be certain death." }
                    : new UiNode(UiKind.Box) { Key = "hello", FontSize = "14px", Text = "Hello, Pioneer! We have supplies for you to help you reach Orion. They aren't free, though!" }));
        if (raided)
        {
            stack.Children.Add(new UiNode(UiKind.StackItem) { Key = "security", Text = "The Port is under high security. Any possibility of purchasing goods has long since sailed." });
            stack.Children.Add(new UiNode(UiKind.StackItem) { Key = "gap", Grow = 1 });
        }
        else
        {
            stack.Children.Add(new UiNode(UiKind.StackItem) { Key = "general", Text = "General Markets:" });
            stack.Children.Add(new UiNode(UiKind.StackItem) { Key = "markets" }.With(new UiNode(UiKind.Stack) { Key = "columns", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "left", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "left-stack", Vertical = true }.With(
                    MarketButton(c, "food-to-fuel", "gas-pump", "5 Food -> 5 Fuel", "green", "trade", ("what", 2)),
                    new UiNode(UiKind.Divider) { Key = "divider" },
                    new UiNode(UiKind.StackItem) { Key = "hangar", Text = "Port Hangar Bay:" },
                    MarketButton(c, "hull", "wrench", "5 Fuel for Hull Plates", "average", "buyparts", ("part", 2)),
                    MarketButton(c, "electronics", "server", "5 Fuel for Electronics", "blue", "buyparts", ("part", 3)),
                    MarketButton(c, "engine", "rocket", "5 Fuel for Engine Parts", "violet", "buyparts", ("part", 1)))),
                new UiNode(UiKind.StackItem) { Key = "right", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "right-stack", Vertical = true }.With(
                    MarketButton(c, "fuel-to-food", "hamburger", "5 Fuel -> 5 Food", "olive", "trade", ("what", 1)),
                    new UiNode(UiKind.Divider) { Key = "divider" },
                    new UiNode(UiKind.StackItem) { Key = "bar", Text = "Port Bar:" },
                    MarketButton(c, "buycrew", "user-plus", "10 Food, 10 Fuel for Crew", "white", "buycrew"),
                    MarketButton(c, "sellcrew", "user-minus", "Crew for 7 Food, 7 Fuel", "black", "sellcrew"),
                    MarketButton(c, "oddcrew", "meteor", "Odd Crew (Same Price)", "purple", "buycrew", ("odd", 1)))))));
        }
        return new UiNode(UiKind.Stack) { Key = "market", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "market-item", Grow = 1 }.With(new UiNode(UiKind.Section)
            {
                Key = "market-section", Title = "Market", Fill = true,
                Buttons = new UiNode(UiKind.Fragment) { Key = "market-buttons" }.With(
                    new UiNode(UiKind.Button) { Key = "raid", Text = "Raid", Icon = "skull", Color = "black", Disabled = raided, OnClick = () => c.Act("raid_spaceport") },
                    new UiNode(UiKind.Button) { Key = "leave", Text = "Leave", Icon = "arrow-right", OnClick = () => c.Act("leave_spaceport") }),
            }.With(stack)),
            new UiNode(UiKind.StackItem) { Key = "status-item" }.With(AdventureStatus(c, data)));
    }
}
