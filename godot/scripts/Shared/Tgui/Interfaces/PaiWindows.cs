using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PaiCard.tsx, PaiSubmit.tsx and PaiInterface/ (index,
// System, Directives, Installed, Available, constants) (silicons SI6, D326). tgui-core's BlockQuote is a box; the System tab's
// <pre> ASCII art is a box of its lines; the software rows' tooltips are on their names.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class PaiWindows
{
    // tg: PaiCard.tsx:46
    public static UiNode RenderCard(TguiContext c)
    {
        UiValue pai = c.Data.Field("pai");
        bool loaded = pai != null && pai.Kind != UiValueKind.Null;
        return c.Window(400, 400, "pAI Options Menu", loaded ? Options(c, pai) : Download(c));
    }

    // tg: PaiCard.tsx:59  the candidates as cards
    private static UiNode Download(TguiContext c)
    {
        IReadOnlyList<UiValue> candidates = c.Data.Arr("candidates");
        var stack = new UiNode(UiKind.Stack) { Key = "download", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "head" }.With(new UiNode(UiKind.NoticeBox) { Color = "info" }.With(
                new UiNode(UiKind.Stack) { Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Grow = 1, FontSize = "16px", Text = "pAI Candidates" },
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "request",
                        Color = "good",
                        Icon = "bell",
                        Text = "Request",
                        Tooltip = "Request more candidates from beyond.",
                        OnClick = () => c.Act("request"),
                    })))));
        for (int i = 0; i < candidates.Count; i++)
            stack.With(new UiNode(UiKind.StackItem) { Key = "candidate" + i }.With(Candidate(c, candidates[i], i + 1)));
        return stack;
    }

    // tg: PaiCard.tsx:95  CandidateDisplay
    private static UiNode Candidate(TguiContext c, UiValue candidate, int index)
    {
        string ckey = candidate.Str("ckey"), name = candidate.Str("name"), description = candidate.Str("description"), comments = candidate.Str("comments");
        var stack = new UiNode(UiKind.Stack) { Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "name" }.With(
                new UiNode(UiKind.Box) { Color = "label", Text = "Name:" },
                string.IsNullOrEmpty(name) ? new UiNode(UiKind.Box) { Text = "None provided - name will be randomized." } : new UiNode(UiKind.Box) { Color = "green", Text = name }));
        if (!string.IsNullOrEmpty(description))
            stack.With(new UiNode(UiKind.Divider) { Key = "d1" }, new UiNode(UiKind.StackItem) { Key = "description" }.With(
                new UiNode(UiKind.Box) { Color = "label", Text = "IC Description:" }, new UiNode(UiKind.Box) { Text = description, Wrap = true }));
        if (!string.IsNullOrEmpty(comments))
            stack.With(new UiNode(UiKind.Divider) { Key = "d2" }, new UiNode(UiKind.StackItem) { Key = "comments" }.With(
                new UiNode(UiKind.Box) { Color = "label", Text = "OOC Notes:" }, new UiNode(UiKind.Box) { Text = comments, Wrap = true }));
        return new UiNode(UiKind.Section)
        {
            Key = "candidate:" + ckey,
            Title = "Candidate " + index,
            Buttons = new UiNode(UiKind.Button) { Key = "dl", Icon = "save", Text = "Download", OnClick = () => c.Act("download", ("ckey", ckey)) },
        }.With(stack);
    }

    // tg: PaiCard.tsx:156  PaiOptions
    private static UiNode Options(TguiContext c, UiValue pai)
    {
        double rangeMax = c.Data.Num("range_max"), rangeMin = c.Data.Num("range_min");
        bool canHolo = pai.Truthy("can_holo"), emagged = pai.Truthy("emagged"), transmit = pai.Truthy("transmit"), receive = pai.Truthy("receive"), leashed = pai.Truthy("leashed");
        string master = pai.Str("master"), dna = pai.Str("dna"), name = pai.Str("name");
        double range = pai.Num("range");
        IReadOnlyList<UiValue> laws = pai.Arr("laws");
        string suppliedLaws = laws.Count > 0 && !string.IsNullOrEmpty(laws[0].AsStr()) ? System.Net.WebUtility.HtmlDecode(laws[0].AsStr()) : "None";

        UiNode Toggle(string key, bool on, string text, string action, params (string, object)[] args) => new UiNode(UiKind.Button)
        {
            Key = key,
            Icon = on ? "toggle-on" : "toggle-off",
            Selected = on,
            Text = text,
            OnClick = () => c.Act(action, args),
        };

        var list = new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Key = "master", Label = "Master", Text = string.IsNullOrEmpty(master) ? null : master }.With(
                string.IsNullOrEmpty(master) ? new UiNode(UiKind.Button) { Key = "imprint", Icon = "dna", Text = "Imprint", OnClick = () => c.Act("set_dna") } : null),
            string.IsNullOrEmpty(master) ? null : new UiNode(UiKind.LabeledItem) { Key = "dna", Label = "DNA", Color = "red", Text = dna },
            new UiNode(UiKind.LabeledItem) { Key = "laws", Label = "Laws" }.With(new UiNode(UiKind.Box) { Color = "label", Text = suppliedLaws, Wrap = true }),
            new UiNode(UiKind.LabeledItem) { Key = "holo", Label = "Holoform" }.With(Toggle("holo", canHolo, "Toggle", "toggle_holo")),
            new UiNode(UiKind.LabeledItem) { Key = "leash", Label = "Leash" }.With(Toggle("leash", leashed, leashed ? "Unleash" : "Leash", "toggle_leash")),
            new UiNode(UiKind.LabeledItem) { Key = "range", Label = "Holoform Range" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "dec", Icon = "circle-minus", Disabled = range == rangeMin, OnClick = () => c.Act("decrease_range") }),
                new UiNode(UiKind.StackItem) { Text = range.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "inc", Icon = "circle-plus", Disabled = range == rangeMax, OnClick = () => c.Act("increase_range") }))),
            new UiNode(UiKind.LabeledItem) { Key = "transmit", Label = "Transmit" }.With(Toggle("transmit", transmit, "Toggle", "toggle_radio", ("option", "transmit"))),
            new UiNode(UiKind.LabeledItem) { Key = "receive", Label = "Receive" }.With(Toggle("receive", receive, "Toggle", "toggle_radio", ("option", "receive"))),
            new UiNode(UiKind.LabeledItem) { Key = "troubleshoot", Label = "Troubleshoot" }.With(
                new UiNode(UiKind.Button) { Key = "fix", Icon = "comment", Text = "Fix Speech", OnClick = () => c.Act("fix_speech") },
                new UiNode(UiKind.Button) { Key = "laws", Icon = "edit", Text = "Set Laws", OnClick = () => c.Act("set_laws") }),
            new UiNode(UiKind.LabeledItem) { Key = "personality", Label = "Personality" }.With(
                new UiNode(UiKind.Button) { Key = "wipe", Icon = "trash", Text = "Erase", OnClick = () => c.Act("wipe_pai") }));
        return new UiNode(UiKind.Section) { Key = "options", Fill = true, Scrollable = true, Title = "Settings: " + name.ToUpperInvariant() }.With(
            list,
            emagged ? new UiNode(UiKind.Button) { Key = "reset", Color = "bad", Icon = "bug", Text = "Reset Software", OnClick = () => c.Act("reset_software") } : null);
    }

    // tg: PaiSubmit.tsx:23
    private const string PAI_DESCRIPTION = "Personal AIs are advanced models capable of nuanced interaction. They are designed to assist their masters in their work. They do not possess hands, thus they cannot interact with equipment or items. While in hologram form, you cannot be directly killed, but you may be incapacitated.";

    // tg: PaiSubmit.tsx:28
    private const string PAI_RULES = "You are expected to role play to some degree. Keep in mind: Not entering information may lead to you not being selected. Press submit to alert pAI cards of your candidacy.";

    // tg: PaiSubmit.tsx:32  (useState's input: the server's static data until the player types; the window has no autoupdate)
    public static UiNode RenderSubmit(TguiContext c)
    {
        string name = c.UseState("name", c.Data.Str("name")), description = c.UseState("description", c.Data.Str("description")), comments = c.UseState("comments", c.Data.Str("comments"));
        UiNode Field(string key, string label, string value, int max) => new UiNode(UiKind.StackItem) { Key = key }.With(
            new UiNode(UiKind.Box) { Bold = true, Color = "label", Text = label },
            new UiNode(UiKind.Input) { Key = "input:" + key, Fluid = true, MaxLength = max, TextValue = value, OnInput = v => c.Local.SetQuiet(key, v) });
        (string, object)[] Args() => new (string, object)[] { ("comments", c.UseState("comments", comments)), ("description", c.UseState("description", description)), ("name", c.UseState("name", name)) };
        return c.Window(400, 460, "pAI Candidacy Menu",
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "details", Grow = 1 }.With(new UiNode(UiKind.Section) { Fill = true, Scrollable = true, Title = "Details" }.With(
                    new UiNode(UiKind.Box) { Color = "label", Wrap = true, Text = PAI_DESCRIPTION + "\n\n" + PAI_RULES })),
                new UiNode(UiKind.StackItem) { Key = "input" }.With(new UiNode(UiKind.Section) { Fill = true, Title = "Input" }.With(
                    new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                        Field("name", "Name", name, 41),
                        Field("description", "Description", description, 100),
                        Field("comments", "OOC Comments", comments, 100)))),
                new UiNode(UiKind.StackItem) { Key = "buttons" }.With(new UiNode(UiKind.Section) { Fill = true }.With(new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "save", Text = "SAVE", Tooltip = "Saves your candidate data locally.", OnClick = () => c.Act("save", Args()) }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "load",
                        Text = "LOAD",
                        Tooltip = "Loads saved candidate data, if any.",
                        OnClick = () =>
                        {
                            c.Local.Clear(); // the loaded data replaces what was typed
                            c.Act("load");
                        },
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "submit", Text = "SUBMIT", OnClick = () => c.Act("submit", Args()) }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "withdraw", Text = "WITHDRAW", Tooltip = "Withdraws your pAI candidacy, if any.", OnClick = () => c.Act("withdraw") }))))));
    }

    // tg: PaiInterface/constants.ts
    private const string DIRECTIVE_COMPREHENSION = "As an advanced software model, you are a complex, thinking, sentient being. Unlike previous AI models, you are capable of comprehending the subtle nuances of human language. You may parse the spirit of a directive and follow its intent, rather than tripping over pedantics and getting snared by technicalities. Above all, you are machine in name and build only. In all other aspects, you may be seen as the ideal, unwavering human companion that you are.";

    private const string DIRECTIVE_ORDER = "Your prime directive comes before all others. Should a supplemental directive conflict with it, you are capable of simply discarding this inconsistency, ignoring the conflicting supplemental directive and continuing to fulfill your prime directive to the best of your ability.";

    private static readonly Dictionary<string, string> SOFTWARE_DESC = new()
    {
        ["Atmospheric Sensor"] = "A tool that allows you to analyze local atmospheric contents.",
        ["Crew Manifest"] = "Allows you to view the crew manifest.",
        ["Crew Monitor"] = "A tool that allows you to monitor vitals from the crew's suit sensors.",
        ["Digital Messenger"] = "A tool that allows you to send messages to other crew members.",
        ["Door Jack"] = "A tool that allows you to open doors.",
        ["Encryption Slot"] = "Allows you to speak on other radio frequencies. You must get an encryption key inserted.",
        ["Host Scan"] = "A health analyzer that can be used in hand or to report bound master vitals.",
        ["Internal GPS"] = "A tool that allows you to broadcast your location.",
        ["Medical HUD"] = "Allows you to view medical status using an overlay HUD.",
        ["Music Synthesizer"] = "Synthesizes instruments, plays sounds and imported songs.",
        ["Newscaster"] = "A tool that allows you to broadcast news to other crew members.",
        ["Photography Module"] = "A portable camera module. Engage, then click to shoot. Includes a printer and lenses.",
        ["Remote Signaler"] = "A remote signalling device to transmit and receive codes.",
        ["Security HUD"] = "Allows you to view security records using an overlay HUD.",
        ["Universal Translator"] = "Translation module for non-common languages.",
    };

    // DOOR_JACK, HOST_SCAN and PHOTO_MODE (constants.ts): the enums' values
    private const int DoorJackCable = 0, DoorJackHack = 1, DoorJackCancel = 2, HostScanTarget = 0, HostScanMaster = 1, PhotoCamera = 0, PhotoPrinter = 1, PhotoZoom = 2;

    // tg: PaiInterface/index.tsx:14
    public static UiNode RenderInterface(TguiContext c)
    {
        int tab = c.UseState("tab", 0);
        UiNode Tab(int index, string text) => new UiNode(UiKind.Tab) { Key = "tab" + index, Icon = "list", Text = text, Selected = tab == index, OnClick = () => c.SetState("tab", index) };
        UiNode content = tab switch
        {
            1 => Directives(c),
            2 => Installed(c),
            3 => Available(c),
            _ => SystemTab(c),
        };
        return c.Window(380, 480, "pAI Software Interface v2.5",
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "content", Grow = 1 }.With(content),
                new UiNode(UiKind.StackItem) { Key = "tabs" }.With(new UiNode(UiKind.Tabs) { Fluid = true }.With(
                    Tab(0, "System"), Tab(1, "Directives"), Tab(2, "Installed"), Tab(3, "Download")))));
    }

    // tg: PaiInterface/System.tsx:8
    private static UiNode SystemTab(TguiContext c)
    {
        bool emagged = c.Data.Truthy("emagged");
        string owner = !emagged ? "NANOTRASEN" : " SYNDICATE";
        string eyebrows = !emagged ? "/\\ ' /\\" : " \\\\ // ";
        string paiAscii = string.Join("\n",
            " ________  ________  ___",
            " |\\   __  \\|\\   __  \\|\\  \\",
            " \\ \\  \\|\\  \\ \\  \\|\\  \\ \\  \\     Interface",
            "  \\ \\   ____\\ \\   __  \\ \\  \\     Version 2.5",
            "   \\ \\  \\___|\\ \\  \\ \\  \\ \\  \\",
            "    \\ \\__\\    \\ \\__\\ \\__\\ \\__\\     Property of",
            "     \\|__|     \\|__|\\|__|\\|__|      " + owner,
            "");
        string floofAscii = string.Join("\n",
            "                              .--.       .-.",
            "        ,;;``;;-;,,..___.,,.-/   `;_//,.'   )",
            "      .' ;;  `;  :; `;;  ;;  `.       '/   .'",
            "     ,;  ';   ;   '  ';  ';   ,'    " + eyebrows + "';",
            "    /'     `      \\   `     ;','   ( d\\__b_),",
            "   /   /       .,;;)       ', (    .'     __\\",
            "  ;:.  \\     ,_   /         ', ' .'_      \\/;",
            " ,   ,;'      `;;/       /    ';,\\ `-..__._,'",
            " ;:.  /____  ..-'--.    /-'    ..---. ._._/ ---.",
            " |    ;' ;'|        \\--/;' ,' /      \\   ,      \\",
            " `.fL__;,__/-..__)_)/  `--'--'`-._)_)/ --\\.._)_)/");

        string masterDna = c.Data.Str("master_dna"), masterName = c.Data.Str("master_name");
        return new UiNode(UiKind.Stack) { Key = "system", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "wallpaper", Grow = 3 }.With(new UiNode(UiKind.Section) { Fill = true, Scrollable = true }.With( // overflow="hidden"
                new UiNode(UiKind.Box) { Key = "pai", Color = !emagged ? "blue" : "crimson", Text = paiAscii },
                new UiNode(UiKind.Box) { Key = "floof", Color = !emagged ? "gold" : "limegreen", Text = floofAscii })),
            new UiNode(UiKind.StackItem) { Key = "info", Grow = 1 }.With(new UiNode(UiKind.Section)
            {
                Fill = true,
                Title = "System Info",
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button) { Key = "verify", Disabled = string.IsNullOrEmpty(masterDna), Icon = "dna", Text = "Verify", Tooltip = "Verifies your master's DNA. Must be carried in hand.", OnClick = () => c.Act("check dna") },
                    new UiNode(UiKind.Button) { Key = "display", Icon = c.Data.Str("screen_image_interface_icon"), Text = "Display", Tooltip = "Change your display image.", OnClick = () => c.Act("change image") }),
            }.With(new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Key = "master", Label = "Master", Text = string.IsNullOrEmpty(masterName) ? "None." : masterName },
                new UiNode(UiKind.LabeledItem) { Key = "dna", Label = "DNA", Color = string.IsNullOrEmpty(masterDna) ? "" : "red", Text = string.IsNullOrEmpty(masterDna) ? "None." : masterDna }))));
    }

    // tg: PaiInterface/Directives.tsx:9
    private static UiNode Directives(TguiContext c)
    {
        IReadOnlyList<UiValue> directives = c.Data.Arr("directives");
        string displayedLaw = directives.Count > 0 ? System.Net.WebUtility.HtmlDecode(directives[0].AsStr()) : "None.";
        bool hasMaster = !string.IsNullOrEmpty(c.Data.Str("master_name"));
        return new UiNode(UiKind.Stack) { Key = "directives", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "core", Grow = 2 }.With(new UiNode(UiKind.Section) { Fill = true, Scrollable = true, Title = "Logic Core" }.With(
                new UiNode(UiKind.Box) { Color = "label", Wrap = true, Text = DIRECTIVE_COMPREHENSION + "\n\n" + DIRECTIVE_ORDER })),
            new UiNode(UiKind.StackItem) { Key = "list", Grow = 1 }.With(new UiNode(UiKind.Section) { Fill = true, Scrollable = true, Title = "Directives" }.With(
                !hasMaster
                    ? new UiNode(UiKind.Box) { Text = "None." }
                    : new UiNode(UiKind.LabeledList).With(
                        new UiNode(UiKind.LabeledItem) { Key = "prime", Label = "Prime", Text = "Serve your master." },
                        new UiNode(UiKind.LabeledItem) { Key = "supplemental", Label = "Supplemental" }.With(new UiNode(UiKind.Box) { Color = "label", Wrap = true, Text = displayedLaw })))));
    }

    // tg: PaiInterface/Installed.tsx:19
    private static UiNode Installed(TguiContext c)
    {
        IReadOnlyList<UiValue> installed = c.Data.Arr("installed");
        string current = c.UseState("selection", "");
        string title = string.IsNullOrEmpty(current) ? "Select a Program" : current;
        var buttons = new UiNode(UiKind.Section) { Fill = true, Scrollable = true, Title = "Installed Software" };
        if (installed.Count == 0)
            buttons.With(new UiNode(UiKind.NoticeBox) { Text = "Nothing installed!" });
        else
            buttons.With(installed.Select((s, i) => new UiNode(UiKind.Button) { Key = "sw" + i, Text = s.AsStr(), OnClick = () => c.SetState("selection", s.AsStr()) }));
        return new UiNode(UiKind.Stack) { Key = "installed", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "selected", Grow = 1 }.With(new UiNode(UiKind.Section) { Fill = true, Scrollable = true, Title = title }.With(
                string.IsNullOrEmpty(current) ? null : new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "desc", Text = SOFTWARE_DESC.TryGetValue(current, out string d) ? d : null, Wrap = true },
                    new UiNode(UiKind.StackItem) { Key = "buttons", Grow = 1 }.With(SoftwareButtons(c, current))))),
            new UiNode(UiKind.StackItem) { Key = "list", Grow = 2 }.With(buttons));
    }

    // tg: PaiInterface/Installed.tsx:69
    private static UiNode SoftwareButtons(TguiContext c, string current)
    {
        bool doorJack = c.Data.Truthy("door_jack"), languages = c.Data.Truthy("languages"), master = !string.IsNullOrEmpty(c.Data.Str("master_name"));
        UiNode B(string key, string icon, string text, int mode, bool disabled = false, string tooltip = null, string color = null)
            => new UiNode(UiKind.Button) { Key = key, Icon = icon, Text = text, Disabled = disabled, Tooltip = tooltip, Color = color, OnClick = () => c.Act(current, ("mode", mode)) };
        return current switch
        {
            "Door Jack" => new UiNode(UiKind.Fragment).With(
                B("cable", "plug", "Extend Cable", DoorJackCable, doorJack, "Drops a cable. Insert into a compatible airlock."),
                B("hack", "door-open", "Hack Door", DoorJackHack, !doorJack, "Begins overriding the airlock security protocols.", "bad"),
                B("cancel", "unlink", "Cancel", DoorJackCancel, !doorJack)),
            "Host Scan" => new UiNode(UiKind.Fragment).With(
                B("holder", "hand-holding-heart", "Scan Holder", HostScanTarget, false, "Must be held or scooped up to scan."),
                B("master", "user-cog", "Scan Master", HostScanMaster, !master, "Scans any bound masters.")),
            "Photography Module" => new UiNode(UiKind.Fragment).With(
                B("camera", "camera-retro", "Camera", PhotoCamera, false, "Toggles the camera. Click an area to take a photo."),
                B("printer", "print", "Printer", PhotoPrinter, false, "Gives a list of stored photos."),
                B("zoom", "search-plus", "Zoom", PhotoZoom, false, "Adjusts zoom level on future photographs.")),
            "Universal Translator" => new UiNode(UiKind.Button) { Key = "install", Icon = "download", Text = !languages ? "Install" : "Installed", Disabled = languages, OnClick = () => c.Act(current) },
            _ => new UiNode(UiKind.Button) { Key = "toggle", Icon = "power-off", Text = "Toggle", Tooltip = "Attempts to enable the module.", OnClick = () => c.Act(current) },
        };
    }

    private static readonly (string, double, double)[] RamRanges = { ("good", 67, 100), ("average", 34, 66), ("bad", 0, 33) };

    // tg: PaiInterface/Available.tsx:20
    private static UiNode Available(TguiContext c)
    {
        IReadOnlyList<KeyValuePair<string, UiValue>> entries = c.Data.Obj("available");
        if (entries.Count == 0)
            return null;
        double ram = c.Data.Num("ram");
        var installed = c.Data.Arr("installed").Select(v => v.AsStr()).ToHashSet();
        var table = new UiNode(UiKind.Table);
        foreach ((string name, UiValue costValue) in entries)
        {
            double cost = costValue.AsNum();
            bool purchased = installed.Contains(name), tooExpensive = ram < cost;
            table.With(new UiNode(UiKind.TableRow) { Key = "sw:" + name, Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Key = "c:name" }.With(new UiNode(UiKind.Box) { Color = "label", Text = name, Tooltip = SOFTWARE_DESC.TryGetValue(name, out string d) ? d : null }),
                new UiNode(UiKind.TableCell) { Key = "c:cost" }.With(new UiNode(UiKind.Stack) { Justify = "end" }.With( // the cost and its chip on one line
                    new UiNode(UiKind.StackItem) { Key = "n", Color = tooExpensive ? "bad" : null, Text = !purchased ? cost.ToString(System.Globalization.CultureInfo.InvariantCulture) : "" },
                    new UiNode(UiKind.StackItem) { Key = "i" }.With(new UiNode(UiKind.Icon) { Color = purchased || ram >= cost ? "purple" : "bad", Icon = purchased ? "check" : "microchip" }))),
                new UiNode(UiKind.TableCell) { Key = "c:buy" }.With(new UiNode(UiKind.Button) { Key = "buy", Icon = "download", Disabled = tooExpensive || purchased, OnClick = () => c.Act("buy", ("selection", name)) })));
        }
        return new UiNode(UiKind.Section)
        {
            Key = "available",
            Fill = true,
            Scrollable = true,
            Title = "Available Software",
            Buttons = new UiNode(UiKind.Stack) { Key = "memory", Tooltip = "Available System Memory: " + ram }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon) { Color = "purple", Icon = "microchip" }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.ProgressBar) { MinValue = 0, MaxValue = 100, Ranges = RamRanges, Value = ram, Width = 5, Text = ram.ToString(System.Globalization.CultureInfo.InvariantCulture) })),
        }.With(table);
    }
}
