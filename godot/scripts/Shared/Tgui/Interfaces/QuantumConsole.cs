using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/QuantumConsole.tsx (gaps BR1, D339): the bitrunners'
// quantum console: the virtual domains by difficulty, deploying one, the connected avatars and stopping the domain.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// The domain's title icons (modular, secondary objective, ghost interaction) and its grade's side icon are drawn in the header row
// beside the Deploy button: the kit's Collapsible title is text.

public static class QuantumConsole
{
    private const string TabKey = "tab";

    private static readonly (string Color, double From, double To)[] HealthRanges =
    {
        ("good", 90, double.PositiveInfinity),
        ("average", 50, 89),
        ("bad", double.NegativeInfinity, 45),
    };

    // tg: QuantumConsole.tsx:77
    private static string GetColor(double difficulty) => difficulty switch
    {
        1 => "yellow",
        2 => "average",
        3 => "bad",
        _ => "green",
    };

    // tg: QuantumConsole.tsx:90
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool connected = data.Num("connected") == 1;
        return c.Window(500, 500, "Quantum Console",
            connected && !data.Truthy("ready") ? LoadingScreen() : null,
            AccessView(c));
    }

    // tg: common/LoadingScreen.tsx:25  (the toolbox does not spin here)
    private static UiNode LoadingScreen() => new UiNode(UiKind.Stack) { Key = "loading", Align = "center", Fill = true, Justify = "center", Vertical = true }.With(
        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon) { Color = "blue", Icon = "toolbox", Size = 4 }),
        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = "Please wait..." }));

    // tg: QuantumConsole.tsx:103
    private static UiNode AccessView(TguiContext c)
    {
        UiValue data = c.Data;
        int tab = c.UseState(TabKey, 0);

        if (data.Num("connected") != 1)
            return new UiNode(UiKind.NoticeBox) { Key = "noserver", Color = "danger", Text = "No server connected!" };

        bool broadcasting = data.Truthy("broadcasting"), broadcastingOnCd = data.Truthy("broadcasting_on_cd"), randomized = data.Truthy("randomized"), ready = data.Truthy("ready");
        string generatedDomain = data.Field("generated_domain") is { Kind: UiValueKind.String } g ? g.Text : null;
        double occupants = data.Num("occupants"), points = data.Num("points");

        // tg: QuantumConsole.tsx:121  available_domains.sort((a, b) => a.cost - b.cost): a stable sort
        var sorted = data.Arr("available_domains").OrderBy(d => d.Num("cost")).ToList();
        var filtered = sorted.Where(d => d.Num("difficulty") == tab).ToList();

        string selected;
        if (!string.IsNullOrEmpty(generatedDomain))
            selected = randomized ? "???" : sorted.FirstOrDefault(d => d.Str("id") == generatedDomain)?.Str("name") ?? "";
        else
            selected = "Nothing loaded";

        UiNode Tab(int index, string text, string icon) => new UiNode(UiKind.Tab)
        {
            Key = "tab" + index,
            Color = GetColor(index),
            Icon = icon,
            Text = text,
            Selected = tab == index,
            OnClick = () => c.SetState(TabKey, index),
        };

        var domains = new UiNode(UiKind.Section)
        {
            Key = "domains",
            Title = "Virtual Domains",
            Fill = true,
            Scrollable = true,
            Buttons = new UiNode(UiKind.Stack) { Key = "buttons", Fill = true }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "broadcast",
                    Checkbox = broadcasting,
                    Disabled = broadcastingOnCd,
                    Text = "Broadcast",
                    Tooltip = "Toggles whether you broadcast your bitrun to station Entertainment Monitors.",
                    OnClick = () => c.Act("broadcast"),
                },
                new UiNode(UiKind.Button)
                {
                    Key = "random",
                    Disabled = !ready || occupants > 0 || points < 1 || !string.IsNullOrEmpty(generatedDomain),
                    Icon = "random",
                    Text = "Randomize",
                    Tooltip = "Get a random domain for more rewards. Weighted towards your current points. Minimum: 1 point.",
                    OnClick = () => c.Act("random_domain"),
                },
                new UiNode(UiKind.Icon) { Key = "star", Color = "pink", Icon = "star", Tooltip = "Accrued points for purchasing domains." },
                new UiNode(UiKind.Box) { Key = "points", Text = TguiFormat.JsNumber(points), Tooltip = "Accrued points for purchasing domains." }),
        }.With(
            new UiNode(UiKind.Tabs) { Key = "tabs", Fluid = true }.With(
                Tab(0, "Peaceful", "chevron-down"),
                Tab(1, "Easy", "chevron-down"),
                Tab(2, "Medium", "chevron-down"),
                Tab(3, "Hard", "skull")))
            .With(filtered.Select(domain => DomainEntry(c, domain)));

        return new UiNode(UiKind.Stack) { Key = "view", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "domains", Grow = 1 }.With(domains),
            new UiNode(UiKind.StackItem) { Key = "avatars" }.With(AvatarDisplay(c)),
            new UiNode(UiKind.StackItem) { Key = "selected" }.With(
                new UiNode(UiKind.Section) { Key = "selected" }.With(
                    new UiNode(UiKind.Stack) { Key = "row", Fill = true }.With(
                        new UiNode(UiKind.StackItem) { Key = "notice", Grow = 1 }.With(
                            new UiNode(UiKind.NoticeBox) { Key = "notice:box", Color = !string.IsNullOrEmpty(generatedDomain) ? "info" : null, Text = selected }),
                        new UiNode(UiKind.StackItem) { Key = "stop" }.With(
                            c.ButtonConfirm("stop_domain", new UiNode(UiKind.Button)
                            {
                                Disabled = !ready || string.IsNullOrEmpty(generatedDomain),
                                Text = "Stop Domain",
                                Tooltip = "Begins shutdown. Will notify anyone connected.",
                                OnClick = () => c.Act("stop_domain"),
                            }))))));
    }

    // tg: QuantumConsole.tsx:254
    private static UiNode DomainEntry(TguiContext c, UiValue domain)
    {
        UiValue data = c.Data;
        bool announceGhosts = domain.Truthy("announce_ghosts"), isModular = domain.Truthy("is_modular"), hasSecondary = domain.Truthy("has_secondary_objectives");
        double cost = domain.Num("cost"), difficulty = domain.Num("difficulty");
        string desc = domain.Str("desc"), id = domain.Str("id"), name = domain.Str("name"), gradeSymbol = domain.Str("grade_symbol");
        UiValue reward = domain.Field("reward");

        string generatedDomain = data.Field("generated_domain") is { Kind: UiValueKind.String } g ? g.Text : null;
        bool ready = data.Truthy("ready"), randomized = data.Truthy("randomized");
        double occupants = data.Num("occupants"), points = data.Num("points");

        bool current = generatedDomain == id;
        bool occupied = occupants > 0;
        string buttonIcon, buttonName;
        if (randomized)
        {
            buttonIcon = null;
            buttonName = "???";
        }
        else if (current)
        {
            buttonIcon = "download";
            buttonName = "Deployed";
        }
        else
        {
            buttonIcon = "coins";
            buttonName = "Deploy";
        }

        bool canView = name != "???";

        var header = new UiNode(UiKind.Stack) { Key = "header", Align = "center" };
        if (isModular && canView)
            header.With(new UiNode(UiKind.Icon) { Key = "modular", Icon = "cubes", Tooltip = "Modular" });
        if (hasSecondary && canView)
            header.With(new UiNode(UiKind.Icon) { Key = "secondary", Icon = "gem", Tooltip = "Secondary Objective Available" });
        if (announceGhosts && canView)
            header.With(new UiNode(UiKind.Icon) { Key = "ghosts", Icon = "ghost", Tooltip = "Ghost Interaction" });
        if (!string.IsNullOrEmpty(gradeSymbol))
            header.With(new UiNode(UiKind.Icon) { Key = "grade", Icon = gradeSymbol });
        header.With(new UiNode(UiKind.Button)
        {
            Key = "deploy",
            Disabled = !string.IsNullOrEmpty(generatedDomain) || !ready || occupied || points < cost,
            Icon = buttonIcon,
            Text = buttonName,
            Tooltip = !string.IsNullOrEmpty(generatedDomain) ? "Stop current domain first." : null,
            OnClick = () => c.Act("set_domain", ("id", id)),
        });

        string text = desc + (isModular ? " (Modular)" : "") + (hasSecondary ? " (Secondary Objective Available)" : "") + (announceGhosts ? " (Ghost Interaction)" : "");

        return new UiNode(UiKind.Collapsible)
        {
            Key = "domain:" + id,
            Color = GetColor(difficulty),
            Title = name,
            Buttons = header,
        }.With(
            new UiNode(UiKind.Stack) { Key = "body", Height = 5 }.With(
                new UiNode(UiKind.StackItem) { Key = "desc", Color = "label", Grow = 4, Wrap = true, Text = text },
                new UiNode(UiKind.Divider) { Key = "divider" },
                new UiNode(UiKind.StackItem) { Key = "details", Grow = 1 }.With(
                    new UiNode(UiKind.Table) { Key = "details" }.With(
                        new UiNode(UiKind.TableRow) { Key = "cost", Tooltip = "Points cost for deploying domain." }.With(DisplayDetails("cost", UiValue.Of(cost), "pink", "star")),
                        new UiNode(UiKind.TableRow) { Key = "reward", Tooltip = "Reward for competing domain." }.With(DisplayDetails("reward", reward, "gold", "coins"))))));
    }

    // tg: QuantumConsole.tsx:350
    private static UiNode AvatarDisplay(TguiContext c)
    {
        UiValue data = c.Data;
        string generatedDomain = data.Field("generated_domain") is { Kind: UiValueKind.String } g ? g.Text : null;
        double retriesLeft = data.Num("retries_left");

        var buttons = new UiNode(UiKind.Stack) { Key = "buttons", Align = "center" };
        if (!string.IsNullOrEmpty(generatedDomain))
            buttons.With(new UiNode(UiKind.StackItem) { Key = "bandwidth", Tooltip = "Available bandwidth for new connections." }.With(
                new UiNode(UiKind.Table) { Key = "bandwidth" }.With(new UiNode(UiKind.TableRow) { Key = "row" }.With(
                    DisplayDetails("bandwidth", UiValue.Of(retriesLeft), "green", "broadcast-tower")))));
        buttons.With(new UiNode(UiKind.StackItem) { Key = "refresh" }.With(
            new UiNode(UiKind.Button) { Key = "refresh", Icon = "sync", Text = "Refresh", Tooltip = "Refresh avatar data.", OnClick = () => c.Act("refresh") }));

        var table = new UiNode(UiKind.Table) { Key = "avatars" };
        foreach (UiValue avatar in data.Arr("avatars"))
        {
            double health = avatar.Num("health"), brute = avatar.Num("brute"), burn = avatar.Num("burn"), tox = avatar.Num("tox"), oxy = avatar.Num("oxy");
            string name = avatar.Str("name"), pilot = avatar.Str("pilot");
            var icons = new UiNode(UiKind.Stack) { Key = "icons" };
            if (brute == 0 && burn == 0 && tox == 0 && oxy == 0)
                icons.With(new UiNode(UiKind.StackItem) { Key = "ok" }.With(new UiNode(UiKind.Icon) { Color = "green", Icon = "check" }));
            icons.With(
                new UiNode(UiKind.StackItem) { Key = "brute" }.With(new UiNode(UiKind.Icon) { Color = brute > 50 ? "bad" : "gray", Icon = "tint" }),
                new UiNode(UiKind.StackItem) { Key = "burn" }.With(new UiNode(UiKind.Icon) { Color = burn > 50 ? "average" : "gray", Icon = "fire" }),
                new UiNode(UiKind.StackItem) { Key = "tox" }.With(new UiNode(UiKind.Icon) { Color = tox > 50 ? "green" : "gray", Icon = "skull-crossbones" }),
                new UiNode(UiKind.StackItem) { Key = "oxy" }.With(new UiNode(UiKind.Icon) { Color = oxy > 50 ? "blue" : "gray", Icon = "lungs" }));
            table.With(new UiNode(UiKind.TableRow) { Key = name }.With(
                new UiNode(UiKind.TableCell) { Key = "pilot", Color = "label", Text = pilot + " as \"" + name + "\"" },
                new UiNode(UiKind.TableCell) { Key = "icons" }.With(icons),
                new UiNode(UiKind.TableCell) { Key = "health" }.With(
                    new UiNode(UiKind.ProgressBar) { MinValue = -100, MaxValue = 100, Ranges = HealthRanges, Value = health })));
        }

        return new UiNode(UiKind.Section) { Key = "avatars", Title = "Connected Clients", Buttons = buttons }.With(table);
    }

    // tg: QuantumConsole.tsx:423
    private static UiNode DisplayDetails(string key, UiValue amount, string color, string icon)
    {
        if (amount == null || amount.Kind == UiValueKind.Null || (amount.Kind == UiValueKind.Number && amount.Number == 0))
            return new UiNode(UiKind.TableCell) { Key = key, Color = "label", Text = "None" };

        if (amount.Kind == UiValueKind.String)
            return new UiNode(UiKind.TableCell) { Key = key, Color = "label", Text = amount.Text }; // don't ask

        double value = amount.AsNum();
        if (value > 4)
            return new UiNode(UiKind.TableCell) { Key = key }.With(
                new UiNode(UiKind.Stack) { Key = "stack" }.With(
                    new UiNode(UiKind.StackItem) { Key = "amount", Text = TguiFormat.JsNumber(value) },
                    new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Icon) { Color = color, Icon = icon })));

        var icons = new UiNode(UiKind.Stack) { Key = "stack" };
        for (int index = 0; index < (int)value; index++)
            icons.With(new UiNode(UiKind.StackItem) { Key = "i" + index }.With(new UiNode(UiKind.Icon) { Color = color, Icon = icon }));
        return new UiNode(UiKind.TableCell) { Key = key }.With(icons);
    }
}
