using System.Linq;
using System.Text.RegularExpressions;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Orbit/ (index.tsx, OrbitSearchBar.tsx, OrbitContent.tsx,
// OrbitCollapsible.tsx, OrbitItem.tsx, OrbitTooltip.tsx, OrbitBlade.tsx, JobIcon.tsx, helpers.ts, constants.ts), M10x slice C: the ghosts'
// orbit menu (GLOB.orbit_menu's window). Engine-free (compiled into the tests too). The item's tooltip (OrbitTooltip's labelled list) is
// its rows as text; the selected item's underline (OrbitItem__selected) is the button's selected look; a Stack.Item's onClick is its
// button's.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Orbit
{
    // tg: constants.ts:1
    private static readonly Dictionary<string, string> Antag2Color = new(StringComparer.Ordinal)
    {
        ["Abductors"] = "pink",
        ["Ash Walkers"] = "olive",
        ["Biohazards"] = "brown",
        ["Bounty Hunters"] = "yellow",
        ["CentCom"] = "teal",
        ["Digital Anomalies"] = "teal",
        ["Emergency Response Team"] = "teal",
        ["Escaped Fugitives"] = "orange",
        ["Xenomorph Infestation"] = "violet",
        ["Spacetime Aberrations"] = "white",
        ["Deviant Crew"] = "white",
        ["Invasive Overgrowth"] = "green",
    };

    // tg: constants.ts:21 DEPARTMENT2COLOR (in its key order: for...in walks it so)
    private static readonly (string Department, string Color, string[] Trims)[] Department2Color =
    {
        ("cargo", "brown", new[] { "Bitrunner", "Cargo Technician", "Shaft Miner", "Quartermaster" }),
        ("command", "blue", new[] { "Captain", "Head of Personnel" }),
        ("engineering", "orange", new[] { "Atmospheric Technician", "Chief Engineer", "Station Engineer" }),
        ("medical", "teal", new[] { "Chemist", "Chief Medical Officer", "Coroner", "Medical Doctor", "Paramedic" }),
        ("science", "pink", new[] { "Geneticist", "Research Director", "Roboticist", "Scientist" }),
        ("security", "red", new[] { "Detective", "Head of Security", "Security Officer", "Warden" }),
        ("service", "green", new[] { "Bartender", "Botanist", "Chaplain", "Chef", "Clown", "Cook", "Curator", "Janitor", "Lawyer", "Mime", "Psychologist" }),
    };

    // tg: constants.ts:72 THREAT, :78 HEALTH
    private const double ThreatLow = 1, ThreatMedium = 5, ThreatHigh = 8;
    private const double HealthGood = 69, HealthAverage = 19, HealthBad = 0, HealthCrit = -30, HealthDead = -100, HealthRuined = -200;

    // tg: constants.ts:87 VIEWMODE (Health: 'heart', Orbiters: 'ghost', Department: 'id-badge')
    public static readonly (string Title, string Icon)[] ViewModes = { ("Health", "heart"), ("Orbiters", "ghost"), ("Department", "id-badge") };

    public const string ViewHealth = "heart", ViewOrbiters = "ghost", ViewDepartment = "id-badge";

    // tg: index.tsx:16
    public static UiNode Render(TguiContext c)
    {
        bool bladeOpen = c.UseState("bladeOpen", false);
        double dynamicWidth = bladeOpen ? 650 : 400;

        var main = new UiNode(UiKind.Stack) { Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "main", Grow = 1 }.With(
                new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "search" }.With(SearchBar(c)),
                    new UiNode(UiKind.StackItem) { Key = "content", Grow = 1 }.With(Content(c)))));
        if (bladeOpen)
            main.With(new UiNode(UiKind.StackItem) { Key = "blade" }.With(Blade(c)));
        return c.Window(dynamicWidth, 550, "Orbit", main);
    }

    /// <summary>Every observable of the payload (OrbitSearchBar's list, in its order).</summary>
    public static IEnumerable<UiValue> AllObservables(UiValue data)
        => new[] { "alive", "antagonists", "critical", "deadchat_controlled", "dead", "ghosts", "misc", "npcs" }.SelectMany(data.Arr);

    // tg: OrbitSearchBar.tsx:10
    private static UiNode SearchBar(TguiContext c)
    {
        bool autoObserve = c.UseState("autoObserve", false);
        bool bladeOpen = c.UseState("bladeOpen", false);
        bool realNameDisplay = c.UseState("realNameDisplay", false);
        string searchQuery = c.UseState("searchQuery", "");
        string viewMode = c.UseState("viewMode", ViewHealth);

        /** Gets a list of Observables, then filters the most relevant to orbit */
        void OrbitMostRelevant()
        {
            UiValue mostRelevant = MostRelevant(c.Data, searchQuery);
            if (mostRelevant != null)
                c.Act("orbit", ("ref", mostRelevant.Str("ref")), ("auto_observe", autoObserve));
        }

        string viewModeTitle = ViewModes.FirstOrDefault(v => v.Icon == viewMode).Title;

        var row = new UiNode(UiKind.Stack).With(
            new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Icon) { Icon = "search" }),
            new UiNode(UiKind.StackItem) { Key = "input", Grow = 1 }.With(new UiNode(UiKind.Input)
            {
                Key = "query",
                AutoFocus = true,
                Fluid = true,
                OnEnter = OrbitMostRelevant,
                OnInput = v => c.SetState("searchQuery", v),
                Tooltip = "Search...",
                TextValue = searchQuery,
            }),
            new UiNode(UiKind.Divider) { Key = "divider" },
            new UiNode(UiKind.StackItem) { Key = "viewmode" }.With(new UiNode(UiKind.Button)
            {
                Color = "transparent",
                Icon = viewMode,
                OnClick = () => c.SetState("viewMode", NextViewMode(viewMode)),
                Tooltip = "Color scheme: " + viewModeTitle,
            }));
        if (c.Data.Truthy("can_observe"))
        {
            row.With(new UiNode(UiKind.StackItem) { Key = "observe" }.With(new UiNode(UiKind.Button)
            {
                Color = autoObserve ? "good" : "transparent",
                Icon = autoObserve ? "toggle-on" : "toggle-off",
                OnClick = () => c.SetState("autoObserve", !autoObserve),
                Tooltip = "Toggle Auto-Observe. When active, you'll see the UI / full inventory of whoever you're orbiting. Neat!",
            }));
        }
        row.With(
            new UiNode(UiKind.StackItem) { Key = "refresh" }.With(new UiNode(UiKind.Button)
            {
                Color = "transparent",
                Icon = "sync-alt",
                OnClick = () => c.Act("refresh"),
                Tooltip = "Refresh",
            }),
            new UiNode(UiKind.StackItem) { Key = "realname" }.With(new UiNode(UiKind.Button)
            {
                Color = "transparent",
                Icon = "passport",
                OnClick = () => c.SetState("realNameDisplay", !realNameDisplay),
                Selected = realNameDisplay,
                Tooltip = "Toggle real name display. When active, you'll see real names instead of disguises in orbit menu.",
            }),
            new UiNode(UiKind.StackItem) { Key = "blade" }.With(new UiNode(UiKind.Button)
            {
                Color = "transparent",
                Icon = "sliders-h",
                OnClick = () => c.SetState("bladeOpen", !bladeOpen),
                Selected = bladeOpen,
                Tooltip = "Toggle settings blade",
            }));
        return new UiNode(UiKind.Section) { Key = "searchbar" }.With(row);
    }

    /// <summary>OrbitSearchBar's orbitMostRelevant: of the observables matching the query, the first one with the fewest orbiters (a stable sort).</summary>
    public static UiValue MostRelevant(UiValue data, string searchQuery)
        => AllObservables(data).Where(o => IsJobCkeyOrNameMatch(o, searchQuery)).OrderBy(o => o.Num("orbiters")).FirstOrDefault();

    /// <summary>Iterates through the view modes and switches to the next one</summary>
    public static string NextViewMode(string viewMode)
    {
        int thisIndex = Array.FindIndex(ViewModes, v => v.Icon == viewMode);
        return ViewModes[(thisIndex + 1) % ViewModes.Length].Icon;
    }

    // tg: OrbitContent.tsx:26
    private static UiNode Content(TguiContext c)
    {
        var stack = new UiNode(UiKind.Stack) { Vertical = true };
        foreach (UiValue crit in c.Data.Arr("critical"))
        {
            string @ref = crit.Str("ref");
            stack.With(new UiNode(UiKind.NoticeBox)
            {
                Key = "crit:" + @ref,
                Color = "purple",
                Tooltip = "Click to orbit",
                OnClick = () => c.Act("orbit", ("ref", @ref)),
            }.With(new UiNode(UiKind.Table).With(new UiNode(UiKind.TableRow).With(
                new UiNode(UiKind.TableCell) { Key = "name", Text = TguiFormat.ToTitleCase(crit.Str("full_name")) },
                new UiNode(UiKind.TableCell) { Key = "extra", Text = crit.Str("extra") }))));
        }

        foreach ((string title, List<UiValue> members) in GetAntagCategories(c.Data.Arr("antagonists")))
            stack.With(Collapsible(c, Antag2Color.TryGetValue(title, out string color) ? color : "bad", members, title));

        (string Color, string Key, string Title)[] sections =
        {
            ("purple", "deadchat_controlled", "Deadchat Controlled"),
            ("blue", "alive", "Alive"),
            (null, "dead", "Dead"),
            (null, "ghosts", "Ghosts"),
            (null, "misc", "Misc"),
            (null, "npcs", "NPCs"),
        };
        foreach ((string color, string key, string title) in sections)
            stack.With(Collapsible(c, color, c.Data.Arr(key), title));

        return new UiNode(UiKind.Section) { Key = "content", Fill = true, Scrollable = true }.With(stack);
    }

    /// <summary>Return a map of strings with each antag in its antag_category (sorted by the lower-cased category).</summary>
    public static List<(string Title, List<UiValue> Members)> GetAntagCategories(IReadOnlyList<UiValue> antagonists)
    {
        var categories = new List<(string Title, List<UiValue> Members)>();
        foreach (UiValue player in antagonists)
        {
            string group = player.Str("antag_group");
            int at = categories.FindIndex(x => x.Title == group);
            if (at < 0)
                categories.Add((group, new List<UiValue> { player }));
            else
                categories[at].Members.Add(player);
        }
        return categories.OrderBy(x => x.Title.ToLowerInvariant(), StringComparer.Ordinal).ToList();
    }

    // tg: OrbitCollapsible.tsx:24
    private static UiNode Collapsible(TguiContext c, string color, IReadOnlyList<UiValue> section, string title)
    {
        bool autoObserve = c.UseState("autoObserve", false);
        bool realNameDisplay = c.UseState("realNameDisplay", false);
        string searchQuery = c.UseState("searchQuery", "");
        string viewMode = c.UseState("viewMode", ViewHealth);

        List<UiValue> filteredSection = FilterAndSort(section, searchQuery, viewMode);
        if (filteredSection.Count == 0)
            return null;

        UiValue orbiting = c.Data.Get("orbiting");
        var items = new UiNode(UiKind.Stack) { Wrap = true }.With(filteredSection.Select(item =>
        {
            string @ref = item.Str("ref");
            return new UiNode(UiKind.StackItem) { Key = "poi:" + @ref }.With(Item(item, realNameDisplay, viewMode, color, orbiting,
                () => c.Act("orbit", ("auto_observe", autoObserve), ("ref", @ref))));
        }));
        return new UiNode(UiKind.Collapsible)
        {
            Key = "section:" + title,
            Bold = true,
            Color = color ?? "grey",
            Open = color != null,
            Title = title + " - (" + filteredSection.Count + ")",
        }.With(items);
    }

    /// <summary>OrbitCollapsible's filter by the search and its sort by the view mode (stable, as the browser's).</summary>
    public static List<UiValue> FilterAndSort(IEnumerable<UiValue> section, string searchQuery, string viewMode)
    {
        IEnumerable<UiValue> filtered = section.Where(o => IsJobCkeyOrNameMatch(o, searchQuery));
        return (viewMode == ViewDepartment
            ? filtered.OrderBy(DepartmentKey, StringComparer.Ordinal)
            : filtered.OrderBy(o => GetDisplayName(o.Str("full_name", null), o.Str("name", null)).TrimStart('"').ToLowerInvariant(), StringComparer.Ordinal)).ToList();
    }

    private static string DepartmentKey(UiValue poi)
        => poi.Str("job", null) is { Length: > 0 } job && GetDepartmentByJob(job) is string department ? department : "unknown";

    // tg: OrbitItem.tsx:18
    private static UiNode Item(UiValue item, bool realNameDisplay, string viewMode, string color, UiValue orbiting, Action orbit)
    {
        string fullName = item.Str("full_name", null);
        string iconState = item.Str("icon_state", null);
        string name = item.Str("name", null);
        double orbiters = item.Num("orbiters");
        bool selected = orbiting != null && !orbiting.IsNull && orbiting.Str("ref", null) == item.Str("ref", null);
        bool validIcon = !string.IsNullOrEmpty(iconState) && iconState != "hudunknown";
        bool hasTooltip = (item.Get("health") is { } h && h.Truthy) || !string.IsNullOrEmpty(item.Str("extra", null));

        // the button's content (the name, then the orbiters' ghost and count) as the kit draws it: the name is the button's text and the
        // orbiters stand beside it (a kit button sizes itself by its text, not by children laid over it)
        var row = new UiNode(UiKind.Stack);
        if (validIcon)
            row.With(new UiNode(UiKind.StackItem) { Key = "jobicon" }.With(JobIcon(item, realNameDisplay)));
        row.With(new UiNode(UiKind.StackItem) { Key = "button" }.With(new UiNode(UiKind.Button)
        {
            Color = GetDisplayColor(item, viewMode, color),
            Selected = selected,
            Tooltip = hasTooltip ? Tooltip(item, realNameDisplay) : null,
            OnClick = orbit,
            Text = realNameDisplay ? CapitalizeFirst(string.IsNullOrEmpty(name) ? fullName : name) : CapitalizeFirst(GetDisplayName(fullName, name)),
        }));
        if (orbiters != 0)
        {
            row.With(
                new UiNode(UiKind.StackItem) { Key = "ghost" }.With(new UiNode(UiKind.Icon) { Icon = "ghost" }),
                new UiNode(UiKind.StackItem) { Key = "count" }.With(new UiNode(UiKind.Box) { Text = TguiFormat.JsNumber(orbiters) }));
        }
        return row;
    }

    // tg: OrbitTooltip.tsx:12, its labelled list as text rows
    public static string Tooltip(UiValue item, bool realNameDisplay)
    {
        string extra = item.Str("extra", null);
        string fullName = item.Str("full_name", null);
        UiValue health = item.Get("health");
        string antag = item.Str("antag", null);
        string displayHealth = health is { Kind: UiValueKind.Number } && health.Number != 0 && health.Number >= 0 ? TguiFormat.JsNumber(health.Number) + "%" : "Critical";
        bool showAFK = item.Get("client") != null && !item.Truthy("client");
        string displayJob = realNameDisplay ? item.Str("mind_job", null) : item.Str("job", null);

        var rows = new List<string> { "Last Known Data" };
        if (!string.IsNullOrEmpty(extra))
        {
            string[] extraInfo = extra.Split(':');
            rows.Add(extraInfo[0] + ": " + (extraInfo.Length > 1 ? extraInfo[1] : ""));
        }
        else
        {
            if (!string.IsNullOrEmpty(fullName))
                rows.Add("Real ID: " + fullName);
            if (!string.IsNullOrEmpty(displayJob))
                rows.Add("Job: " + displayJob);
            if (!string.IsNullOrEmpty(antag))
                rows.Add("Threat: " + antag);
            if (health != null && health.Truthy)
                rows.Add("Health: " + displayHealth);
        }
        if (showAFK)
            rows.Add("Status: Away");
        return string.Join("\n", rows);
    }

    // tg: JobIcon.tsx:23 (the sechud icon at about twice its size; a cyborg's is the font icon of its job)
    private static UiNode JobIcon(UiValue item, bool realNameDisplay)
    {
        string icon = item.Str("icon", ""), iconState = item.Str("icon_state", "");
        string mindIcon = item.Str("mind_icon", ""), mindIconState = item.Str("mind_icon_state", "");
        string usedIcon = realNameDisplay ? (mindIcon.Length > 0 ? mindIcon : icon) : icon;
        string usedIconState = realNameDisplay ? (mindIconState.Length > 0 ? mindIconState : iconState) : iconState;
        if (iconState == "borg")
            return new UiNode(UiKind.Icon) { Color = "lightblue", Icon = item.Str("mind_job_icon", "") };
        return new UiNode(UiKind.Box) { DmIcon = usedIcon, DmIconState = usedIconState, Width = "20px", Height = "20px", CropToPixels = true };
    }

    // tg: OrbitBlade.tsx:20
    private static UiNode Blade(TguiContext c)
    {
        bool realNameDisplay = c.UseState("realNameDisplay", false);
        string viewMode = c.UseState("viewMode", ViewHealth);
        UiValue orbiting = c.Data.Get("orbiting");

        var stack = new UiNode(UiKind.Stack) { Vertical = true, Width = "244px" }.With(
            new UiNode(UiKind.StackItem) { Key = "settings" }.With(new UiNode(UiKind.Section)
            {
                Title = "Orbit Settings",
                Color = "label",
                Buttons = new UiNode(UiKind.Button) { Color = "bad", Icon = "times", OnClick = () => c.SetState("bladeOpen", false) },
            }.With(new UiNode(UiKind.Box)
            {
                Wrap = true,
                Text = "Keep in mind: Orbit does not update automatically. You will need to click the \"Refresh\" button to see the latest data.",
            })),
            new UiNode(UiKind.StackItem) { Key = "viewmode" }.With(new UiNode(UiKind.Section) { Title = "View Mode" }.With(
                new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                    new[] { new UiNode(UiKind.StackItem) { Key = "hint", Color = "label" }.With(new UiNode(UiKind.Box) { Wrap = true, Color = "label", Text = "Change the color and sorting scheme of observable items." }) }
                    .Concat(ViewModes.Select(v => new UiNode(UiKind.Button)
                    {
                        Key = "mode:" + v.Title,
                        TextAlign = "center",
                        Color = "transparent",
                        Fluid = true,
                        Icon = v.Icon,
                        Text = v.Title,
                        OnClick = () => c.SetState("viewMode", v.Icon),
                        Selected = v.Icon == viewMode,
                    }))))),
            new UiNode(UiKind.StackItem) { Key = "realname" }.With(new UiNode(UiKind.Section)
            {
                Title = "Real Name Display",
                Color = "label",
                Buttons = new UiNode(UiKind.Button) { Color = "transparent", Icon = "passport", Selected = realNameDisplay, OnClick = () => c.SetState("realNameDisplay", !realNameDisplay) },
            }.With(new UiNode(UiKind.Box)
            {
                Wrap = true,
                Text = "Real Name mode will display actual character names and their roundstart jobs insteas of being based on their worn ID. If the person lacks a roundstart job, it will still display their ID job icon.",
            })));
        if (orbiting != null && !orbiting.IsNull)
            stack.With(new UiNode(UiKind.StackItem) { Key = "info" }.With(OrbitInfo(orbiting)));
        return stack;
    }

    // tg: OrbitBlade.tsx:104
    private static UiNode OrbitInfo(UiValue orbiting)
    {
        string name = orbiting.Str("name", null), fullName = orbiting.Str("full_name", null), job = orbiting.Str("job", null);
        UiValue health = orbiting.Get("health");
        string department = !string.IsNullOrEmpty(job) ? GetDepartmentByJob(job) : null;
        bool showAFK = orbiting.Get("client") != null && !orbiting.Truthy("client");

        var title = new UiNode(UiKind.Stack).With(new UiNode(UiKind.StackItem) { Key = "name" }.With(new UiNode(UiKind.Box) { Text = TguiFormat.ToTitleCase(GetDisplayName(fullName, name)) }));
        if (showAFK)
            title.With(new UiNode(UiKind.StackItem) { Key = "afk" }.With(new UiNode(UiKind.Icon) { Color = "grey", Icon = "bed", Tooltip = "Away from keyboard" }));
        var stack = new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(new UiNode(UiKind.StackItem) { Key = "title" }.With(title));
        if (!string.IsNullOrEmpty(job))
        {
            var jobRow = new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Key = "icon" }.With(JobIcon(orbiting, false)),
                new UiNode(UiKind.StackItem) { Key = "job", Grow = 1 }.With(new UiNode(UiKind.Box) { Color = "label", Text = job }));
            if (department != null)
                jobRow.With(new UiNode(UiKind.StackItem) { Key = "dept" }.With(new UiNode(UiKind.Box) { Color = "grey", Text = CapitalizeFirst(department) }));
            stack.With(new UiNode(UiKind.StackItem) { Key = "jobrow" }.With(jobRow));
        }
        if (health != null)
            stack.With(new UiNode(UiKind.StackItem) { Key = "health" }.With(HealthDisplay(health.AsNum())));
        return new UiNode(UiKind.Section) { Title = "Orbiting" }.With(stack);
    }

    // tg: OrbitBlade.tsx:162
    private static UiNode HealthDisplay(double health)
    {
        string icon = "heart";
        string howDead = null;
        if (health <= HealthRuined)
        {
            howDead = "Very Dead: " + TguiFormat.JsNumber(health);
            icon = "skull";
        }
        else if (health <= HealthDead)
        {
            howDead = "Dead: " + TguiFormat.JsNumber(health);
            icon = "heart-broken";
        }
        else if (health <= HealthCrit)
        {
            howDead = "Health critical: " + TguiFormat.JsNumber(health);
            icon = "tired";
        }
        else if (health <= HealthBad)
        {
            howDead = "Bad: " + TguiFormat.JsNumber(health);
            icon = "heartbeat";
        }

        UiNode value = howDead != null
            ? new UiNode(UiKind.Box) { Color = "bad", Text = howDead }
            : new UiNode(UiKind.ProgressBar)
            {
                MaxValue = 100,
                MinValue = 0,
                Ranges = new[] { ("good", 70.0, double.PositiveInfinity), ("average", 20.0, HealthGood), ("bad", 0.0, HealthAverage) },
                Value = health,
            };
        return new UiNode(UiKind.Stack) { Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Icon) { Color = "grey", Icon = icon }),
            new UiNode(UiKind.StackItem) { Key = "value", Grow = 1 }.With(value));
    }

    // ---- helpers.ts ----

    /// <summary>Returns a disguised name in case the person is wearing someone else's ID</summary>
    public static string GetDisplayName(string fullName, string nickname = null)
    {
        if (string.IsNullOrEmpty(nickname))
            return fullName;

        if (fullName == null || !fullName.Contains('[') || Regex.IsMatch(fullName, @"\(as ") || Regex.IsMatch(fullName, "^Unknown"))
            return nickname;

        // return only the name before the first ' [' or ' ('
        return "\"" + Regex.Split(fullName, @" \[| \(")[0] + "\"";
    }

    /// <summary>Returns the department the player is in</summary>
    public static string GetDepartmentByJob(string job)
    {
        string withoutParenthesis = Regex.Replace(job, @" \(.*\)", "", RegexOptions.None, TimeSpan.FromSeconds(1)); // JS's replace: the first match
        foreach ((string department, _, string[] trims) in Department2Color)
        {
            if (trims.Contains(withoutParenthesis))
                return department;
        }
        return null;
    }

    private static string GetDepartmentColor(string job)
    {
        if (string.IsNullOrEmpty(job))
            return "grey";
        string department = GetDepartmentByJob(job);
        if (department == null)
            return "grey";
        return Department2Color.First(d => d.Department == department).Color;
    }

    private static string GetHealthColor(double health) => health > HealthGood ? "good" : health > HealthAverage ? "average" : "bad";

    private static string GetThreatColor(double orbiters) => orbiters > ThreatHigh ? "violet" : orbiters > ThreatMedium ? "blue" : orbiters > ThreatLow ? "teal" : "good";

    /// <summary>Displays color for buttons based on the health or orbiter count.</summary>
    public static string GetDisplayColor(UiValue item, string mode, string @override = null)
    {
        UiValue health = item.Get("health");

        // Things like blob camera, etc
        if (health is not { Kind: UiValueKind.Number })
            return !string.IsNullOrEmpty(@override) ? "good" : "grey";

        // Players that are AFK
        if (item.Get("client") != null && !item.Truthy("client"))
            return "grey";

        return mode switch
        {
            ViewOrbiters => GetThreatColor(item.Num("orbiters")),
            ViewDepartment => GetDepartmentColor(item.Str("job", null)),
            _ => GetHealthColor(health.Number),
        };
    }

    /// <summary>Checks if a full name or job title matches the search.</summary>
    public static bool IsJobCkeyOrNameMatch(UiValue observable, string searchQuery)
    {
        if (string.IsNullOrEmpty(searchQuery))
            return true;
        string query = searchQuery.ToLowerInvariant();
        return new[] { "full_name", "name", "job", "ckey" }.Any(k => observable.Str(k, null)?.ToLowerInvariant().Contains(query, StringComparison.Ordinal) == true);
    }

    // tgui-core's capitalizeFirst
    private static string CapitalizeFirst(string text) => string.IsNullOrEmpty(text) ? text ?? "" : char.ToUpperInvariant(text[0]) + text[1..];
}
