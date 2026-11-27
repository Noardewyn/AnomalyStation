using System.IO;
using System.Linq;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ExodroneConsole.tsx: the exploration drone control
// console: the drones to take control of, the drone's master controls, equipment and cargo, its tools, the travel destinations with
// their bands, the travel and busy screens, the site's exploration with its events, an event's screen and an adventure's screen with
// their pictures, the drone's log, and the lost signal. tgui's Dimmer is a box centred in its section; the lost signal's modal shows
// a warning icon where tgui-core's Nanotrasen logo stands. The pictures are tg's icons/ui/adventure/ PNGs (tg's asset/simple/adventure),
// which the icon converter copies, and an adventure node's own base64 PNG. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ExodroneConsole
{
    /// <summary>tg's resolveAsset of an adventure screen's name: the picture of tg's asset/simple/adventure (asset_cache/assets/adventure.dm).</summary>
    public static string AdventurePicture(string name) => "icons/ui/adventure/" + name + ".png";

    private const string ChoosingToolsKey = "choosingTools";

    private const string TravelDimmerKey = "TravelDimmerShown";

    // The decoded base64 pictures of the adventures' nodes, by their text (a few per adventure, each decoded once).
    private static readonly Dictionary<string, UiPixels> RawPictures = new(StringComparer.Ordinal);

    // tg: ExodroneConsole.tsx:173
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool choosingTools = c.UseState(ChoosingToolsKey, false);
        var content = new List<UiNode> { ExodroneConsoleContent(c) };
        if (data.Truthy("signal_lost"))
            content.Add(SignalLostModal(c));
        if (choosingTools)
            content.Add(ToolSelectionModal(c));
        return c.Window(750, 600, null, content.ToArray());
    }

    // tg: ExodroneConsole.tsx:191
    private static UiNode SignalLostModal(TguiContext c) => new UiNode(UiKind.Modal) { Key = "signal_lost", Background = "#ff0000", TextAlign = "center", Width = "30em" }.With(
        new UiNode(UiKind.Icon) { Key = "logo", Icon = "exclamation-triangle", Size = 4 },
        new UiNode(UiKind.Box) { Key = "lost", Background = "#000000", Color = "red", FontSize = 2, Text = "CONNECTION LOST" },
        new UiNode(UiKind.Box)
        {
            Key = "text",
            Wrap = true,
            Text = "Connection to exploration drone interrupted. Please contact nearest Nanotrasen Exploration Division representative for further instructions.",
        },
        new UiNode(UiKind.Icon) { Key = "warning", Icon = "exclamation-triangle", Color = "black", Size = 5 },
        new UiNode(UiKind.Box) { Key = "confirm" }.With(
            new UiNode(UiKind.Button) { Key = "confirm", Color = "orange", Text = "Confirm", OnClick = () => c.Act("confirm_signal_lost") }));

    // tg: ExodroneConsole.tsx:222
    private static UiNode DroneSelectionSection(TguiContext c, IReadOnlyList<UiValue> allDrones)
    {
        var stack = new UiNode(UiKind.Stack) { Key = "drones", Vertical = true };
        foreach (UiValue drone in allDrones)
        {
            string reference = drone.Str("ref");
            stack.With(
                new UiNode(UiKind.StackItem) { Key = "drone:" + reference }.With(new UiNode(UiKind.Stack) { Key = "row", Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "name", Width = "10em", FontSize = "18px", Text = drone.Str("name") },
                    new UiNode(UiKind.Divider) { Key = "divider1" },
                    new UiNode(UiKind.StackItem) { Key = "description", Text = drone.Str("description") },
                    new UiNode(UiKind.StackItem) { Key = "grow", Grow = 1 },
                    new UiNode(UiKind.Divider) { Key = "divider2" },
                    new UiNode(UiKind.StackItem) { Key = "control" }.With(drone.Truthy("controlled")
                        ? new UiNode(UiKind.Box) { Key = "controlled", Text = "Controlled by another console." }
                        : new UiNode(UiKind.Button) { Key = "assume", Icon = "plug", Text = "Assume Control", OnClick = () => c.Act("select_drone", ("drone_ref", reference)) }))),
                new UiNode(UiKind.Divider) { Key = "divider:" + reference });
        }
        return new UiNode(UiKind.Section) { Key = "listing", Fill = true, Scrollable = true, Title = "Exploration Drone Listing" }.With(stack);
    }

    // tg: ExodroneConsole.tsx:265
    private static UiNode ToolSelectionModal(TguiContext c)
    {
        IReadOnlyList<KeyValuePair<string, UiValue>> allTools = c.Data.Obj("all_tools");
        var tools = new UiNode(UiKind.Stack) { Key = "tools", TextAlign = "center" };
        if (allTools.Count > 0)
        {
            foreach (KeyValuePair<string, UiValue> tool in allTools)
            {
                string toolName = tool.Key;
                tools.With(new UiNode(UiKind.StackItem) { Key = toolName }.With(new UiNode(UiKind.Stack) { Key = toolName, Vertical = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "button" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "add",
                        Height = 6,
                        Tooltip = tool.Value.Str("description"),
                        Icon = tool.Value.Str("icon"),
                        Size = 3,
                        OnClick = () =>
                        {
                            c.SetState(ChoosingToolsKey, false);
                            c.Act("add_tool", ("tool_type", toolName));
                        },
                    }),
                    new UiNode(UiKind.StackItem) { Key = "name" }.With(new UiNode(UiKind.Box) { Key = "name", Color = "label", Text = TguiFormat.Capitalize(toolName) }))));
            }
        }
        else
            tools.With(new UiNode(UiKind.StackItem) { Key = "back" }.With(new UiNode(UiKind.Button) { Key = "back", Text = "Back", OnClick = () => c.SetState(ChoosingToolsKey, false) }));
        return new UiNode(UiKind.Modal) { Key = "tools" }.With(new UiNode(UiKind.Section)
        {
            Key = "tools",
            Title = "Tool Selection",
            Buttons = new UiNode(UiKind.Button) { Key = "close", Icon = "xmark", Color = "transparent", OnClick = () => c.SetState(ChoosingToolsKey, false) },
        }.With(tools));
    }

    // tg: ExodroneConsole.tsx:322
    private static UiNode EquipmentBox(TguiContext c, UiValue cargo, bool configurable, int index)
    {
        IReadOnlyList<KeyValuePair<string, UiValue>> allTools = c.Data.Obj("all_tools");
        string name = cargo.Str("name");
        switch (cargo.Str("type"))
        {
            case "tool": // Tool icon+Remove button if configurable
            {
                string icon = allTools.FirstOrDefault(t => t.Key == name).Value?.Str("icon");
                return new UiNode(UiKind.Box) { Key = "cargo:" + index, Width = "100%", Height = 3 }.With(new UiNode(UiKind.Stack) { Key = "tool", Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Icon) { Key = "icon", Icon = icon, Size = 1.5 }),
                    new UiNode(UiKind.StackItem) { Key = "name", Grow = 1 }.With(new UiNode(UiKind.Box) { Key = "name", FontSize = "16px", Text = TguiFormat.Capitalize(name) }),
                    configurable
                        ? new UiNode(UiKind.StackItem) { Key = "eject" }.With(new UiNode(UiKind.Button)
                        {
                            Key = "eject", Color = "transparent", Icon = "eject", Height = 2.3, Tooltip = "Eject",
                            OnClick = () => c.Act("remove_tool", ("tool_type", name)),
                        })
                        : null));
            }
            case "cargo": // Jettison button.
            {
                string reference = cargo.Str("ref");
                return new UiNode(UiKind.Box) { Key = "cargo:" + index, Width = "100%", Height = 3 }.With(new UiNode(UiKind.Stack) { Key = "cargo", Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Icon) { Key = "icon", Icon = "box", Size = 1.5 }),
                    new UiNode(UiKind.StackItem) { Key = "name", Grow = 1 }.With(new UiNode(UiKind.Box) { Key = "name", FontSize = "16px", Text = TguiFormat.Capitalize(name) }),
                    new UiNode(UiKind.StackItem) { Key = "jettison" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "jettison", Color = "transparent", Icon = "eject", Height = 2.3, Tooltip = "Jettison " + name,
                        OnClick = () => c.Act("jettison", ("target_ref", reference)),
                    })));
            }
            default:
                return new UiNode(UiKind.Box) { Key = "cargo:" + index, Width = "100%", Height = 3, TextAlign = "center", Color = "label", Opacity = 0.4, Text = "Empty slot" };
        }
    }

    // tg: ExodroneConsole.tsx:437
    private static UiNode EquipmentGrid(TguiContext c)
    {
        UiValue drone = c.Data;
        bool configurable = drone.Truthy("configurable");
        double integrity = drone.Num("drone_integrity"), maxIntegrity = drone.Num("drone_max_integrity");
        var cargo = new UiNode(UiKind.Stack) { Key = "cargo", Vertical = true };
        int index = 0;
        foreach (UiValue element in drone.Arr("cargo"))
            cargo.With(new UiNode(UiKind.StackItem) { Key = "cargo:" + index }.With(EquipmentBox(c, element, configurable, index++)));
        return new UiNode(UiKind.Stack) { Key = "equipment", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "master" }.With(new UiNode(UiKind.Section) { Key = "master", Title = "Master Controls", MinWidth = "200px" }.With(
                new UiNode(UiKind.Stack) { Key = "master", Vertical = true, TextAlign = "center" }.With(
                    new UiNode(UiKind.StackItem) { Key = "status" }.With(new UiNode(UiKind.LabeledList) { Key = "status" }.With(
                        new UiNode(UiKind.LabeledItem) { Key = "id", Label = "Drone ID", Text = drone.Str("drone_name") },
                        new UiNode(UiKind.LabeledItem) { Key = "activity", Label = "Activity", Text = TguiFormat.Capitalize(drone.Str("drone_status")) },
                        new UiNode(UiKind.LabeledItem) { Key = "integrity", Label = "Integrity" }.With(new UiNode(UiKind.ProgressBar)
                        {
                            Key = "integrity",
                            Tooltip = TguiFormat.JsNumber(integrity) + "/" + TguiFormat.JsNumber(maxIntegrity),
                            Value = integrity,
                            MinValue = 0,
                            MaxValue = maxIntegrity,
                            Ranges = new[]
                            {
                                ("good", 0.7 * maxIntegrity, maxIntegrity),
                                ("average", 0.4 * maxIntegrity, 0.7 * maxIntegrity),
                                ("bad", double.NegativeInfinity, 0.4 * maxIntegrity),
                            },
                        }))),
                    new UiNode(UiKind.StackItem) { Key = "disconnect" }.With(
                        new UiNode(UiKind.Button) { Key = "disconnect", Fluid = true, Icon = "plug", Text = "Disconnect", OnClick = () => c.Act("end_control") }),
                    new UiNode(UiKind.StackItem) { Key = "destruct" }.With(
                        c.ButtonConfirm("destruct", new UiNode(UiKind.Button) { Key = "destruct", Fluid = true, Icon = "bomb", Color = "bad", Text = "Self-Destruct", OnClick = () => c.Act("self_destruct") }))))),
            new UiNode(UiKind.StackItem) { Key = "equipment" }.With(new UiNode(UiKind.Section)
            {
                Key = "equipment",
                Title = "Equipment",
                Buttons = configurable
                    ? new UiNode(UiKind.Button) { Key = "tools", Fluid = true, Color = "average", Icon = "wrench", OnClick = () => c.SetState(ChoosingToolsKey, true) }
                    : null,
            }.With(cargo)));
    }

    // tg: ExodroneConsole.tsx:540
    private static UiNode NoSiteDimmer() => new UiNode(UiKind.Stack) { Key = "no_sites", Vertical = true, TextAlign = "center" }.With(
        new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Icon) { Key = "map", Icon = "map", Color = "red", Size = 10 }),
        new UiNode(UiKind.StackItem) { Key = "text", FontSize = "18px", Color = "red", Text = "No Destinations." },
        new UiNode(UiKind.StackItem) { Key = "hint", Color = "red", Text = "(Use the Scanner Array Console to find new locations.)" });

    // tg: ExodroneConsole.tsx:558
    private static UiNode TravelTargetSelectionScreen(TguiContext c, bool showCancelButton, Action onSelectionDone)
    {
        UiValue drone = c.Data;
        if (drone.Str("drone_status") == "travel")
            return TravelDimmer(drone);
        bool canTravel = drone.Truthy("can_travel");
        string travelError = drone.Str("travel_error");
        double coefficient = drone.Num("drone_travel_coefficent");
        UiValue site = drone.Field("site") is { Kind: UiValueKind.Object } s ? s : null;
        IReadOnlyList<UiValue> sites = drone.Field("sites") is { Kind: UiValueKind.Array } list ? list.Items : null;
        bool choosingTools = c.UseState(ChoosingToolsKey, false);
        IReadOnlyList<KeyValuePair<string, UiValue>> allBands = drone.Obj("all_bands");

        double TravelCost(UiValue target) => site != null
            ? Math.Max(Math.Abs(site.Num("distance") - target.Num("distance")), 1) * coefficient
            : target.Num("distance") * coefficient;

        void TravelTo(string reference)
        {
            onSelectionDone();
            c.Act("start_travel", ("target_site", reference));
        }

        UiNode LaunchButtons(double eta, string reference) => new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.Box) { Key = "eta", Text = "ETA: " + TguiFormat.FormatTime(eta, "short") },
            new UiNode(UiKind.Button)
            {
                Key = "launch",
                Disabled = !canTravel,
                Icon = canTravel ? "rocket" : "triangle-exclamation",
                Text = canTravel ? "Launch" : travelError,
                OnClick = () => TravelTo(reference),
            });

        var section = new UiNode(UiKind.Section)
        {
            Key = "destinations",
            Title = "Travel Destinations",
            Fill = true,
            Scrollable = true,
            Buttons = showCancelButton ? new UiNode(UiKind.Button) { Key = "cancel", Text = "Cancel", OnClick = onSelectionDone } : null,
        };
        if (sites != null && sites.Count == 0 && !choosingTools)
            section.With(NoSiteDimmer());
        if (site != null)
            section.With(new UiNode(UiKind.Section) { Key = "home", Title = "Home", Buttons = LaunchButtons(site.Num("distance") * coefficient, null) });
        foreach (UiValue destination in (sites ?? Array.Empty<UiValue>()).Where(d => site == null || d.Str("ref") != site.Str("ref")))
        {
            string reference = destination.Str("ref");
            IReadOnlyList<KeyValuePair<string, UiValue>> bandInfo = destination.Obj("band_info");
            var details = new UiNode(UiKind.LabeledList) { Key = "details" }.With(
                new UiNode(UiKind.LabeledItem) { Key = "location", Label = "Location", Text = destination.Str("coordinates") },
                new UiNode(UiKind.LabeledItem) { Key = "description", Label = "Description", Text = destination.Str("description") },
                new UiNode(UiKind.Divider) { Key = "divider" });
            foreach (KeyValuePair<string, UiValue> band in allBands)
            {
                UiValue value = bandInfo.FirstOrDefault(b => b.Key == band.Key).Value;
                if (value == null || value.AsNum() == 0)
                    continue;
                details.With(new UiNode(UiKind.LabeledItem) { Key = "band:" + band.Key, Label = band.Key, Text = value.AsStr() });
            }
            section.With(new UiNode(UiKind.Section) { Key = "site:" + reference, Title = destination.Str("name"), Buttons = LaunchButtons(TravelCost(destination), reference) }.With(details));
        }
        return section;
    }

    // tg: ExodroneConsole.tsx:684
    private static UiNode TravelDimmer(UiValue drone) => new UiNode(UiKind.Section) { Key = "travel", Fill = true }.With(
        new UiNode(UiKind.Stack) { Key = "travel", Vertical = true, TextAlign = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Icon) { Key = "route", Icon = "route", Color = "yellow", Size = 10 }),
            new UiNode(UiKind.StackItem) { Key = "time", FontSize = "18px", Color = "yellow", Text = "Travel Time: " + TguiFormat.FormatTime(drone.Num("travel_time_left")) }));

    // tg: ExodroneConsole.tsx:703
    private static UiNode TimeoutScreen(UiValue drone) => new UiNode(UiKind.Section) { Key = "busy", Fill = true }.With(
        new UiNode(UiKind.Stack) { Key = "busy", Vertical = true, TextAlign = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Icon) { Key = "cog", Icon = "cog", Color = "green", Size = 10 }),
            new UiNode(UiKind.StackItem)
            {
                Key = "message",
                FontSize = "18px",
                Color = "green",
                Text = drone.Str("wait_message") + " (" + TguiFormat.FormatTime(drone.Num("wait_time_left")) + ")",
            }));

    // tg: ExodroneConsole.tsx:724
    private static UiNode ExplorationScreen(TguiContext c)
    {
        UiValue drone = c.Data;
        UiValue site = drone.Field("site");
        bool travelDimmerShown = c.UseState(TravelDimmerKey, false);
        if (travelDimmerShown)
            return TravelTargetSelectionScreen(c, true, () => c.SetState(TravelDimmerKey, false));
        var stack = new UiNode(UiKind.Stack) { Key = "exploration", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "site", Grow = 1 }.With(new UiNode(UiKind.LabeledList) { Key = "site" }.With(
                new UiNode(UiKind.LabeledItem) { Key = "site", Label = "Site", Text = site.Str("name") },
                new UiNode(UiKind.LabeledItem) { Key = "location", Label = "Location", Text = site.Str("coordinates") },
                new UiNode(UiKind.LabeledItem) { Key = "description", Label = "Description", Text = site.Str("description") })),
            new UiNode(UiKind.StackItem) { Key = "explore", Align = "center" }.With(
                new UiNode(UiKind.Button) { Key = "explore", Width = "75px", TextAlign = "center", Text = "Explore", OnClick = () => c.Act("explore") }));
        foreach (UiValue e in site.Arr("events"))
        {
            string reference = e.Str("ref");
            stack.With(new UiNode(UiKind.StackItem) { Key = "event:" + reference, Align = "center", Grow = 1 }.With(
                new UiNode(UiKind.Button) { Key = "event", MinWidth = "75px", TextAlign = "center", Text = TguiFormat.Capitalize(e.Str("name")), OnClick = () => c.Act("explore_event", ("target_event", reference)) }));
        }
        stack.With(new UiNode(UiKind.StackItem) { Key = "travel", Align = "center", Grow = 1 }.With(
            new UiNode(UiKind.Button) { Key = "travel", Width = "75px", TextAlign = "center", Text = "Travel", OnClick = () => c.SetState(TravelDimmerKey, true) }));
        return new UiNode(UiKind.Section) { Key = "exploration", Fill = true, Title = "Exploration" }.With(stack);
    }

    // tg: ExodroneConsole.tsx:792
    private static UiNode EventScreen(TguiContext c, UiValue @event)
    {
        return new UiNode(UiKind.Section) { Key = "event", Fill = true, Title = "Exploration" }.With(
            new UiNode(UiKind.Stack) { Key = "event", Vertical = true, Fill = true, TextAlign = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "scene" }.With(new UiNode(UiKind.Stack) { Key = "scene", Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "image" }.With(new UiNode(UiKind.Box) { Key = "image", DmIcon = AdventurePicture(@event.Str("image")), Width = "250px", Height = "125px" }),
                    new UiNode(UiKind.StackItem) { Key = "description" }.With(new UiNode(UiKind.Box) { Key = "description", Wrap = true, TextAlign = "left", Text = @event.Str("description") }))),
                new UiNode(UiKind.Divider) { Key = "divider" },
                new UiNode(UiKind.StackItem) { Key = "actions", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "actions", Vertical = true, Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "grow1", Grow = 1 },
                    new UiNode(UiKind.StackItem) { Key = "act", Grow = 1 }.With(
                        new UiNode(UiKind.Button) { Key = "act", Disabled = !@event.Truthy("action_enabled"), Text = @event.Str("action_text"), OnClick = () => c.Act("start_event") }),
                    @event.Truthy("skippable")
                        ? new UiNode(UiKind.StackItem) { Key = "ignore" }.With(new UiNode(UiKind.Button) { Key = "ignore", Text = @event.Str("ignore_text"), OnClick = () => c.Act("skip_event") })
                        : null,
                    new UiNode(UiKind.StackItem) { Key = "grow2", Grow = 1 }))));
    }

    /// <summary>tg's AdventureScreen (ExodroneConsole.tsx:845), which the AdventureBrowser window shares: the node's text, its picture and its choices.</summary>
    public static UiNode AdventureScreen(TguiContext c, UiValue adventureData)
    {
        string rawData = adventureData.Str("raw_image");
        UiNode picture = new UiNode(UiKind.Box) { Key = "image", Width = "200px", Height = "100px" };
        if (!string.IsNullOrEmpty(rawData))
            picture.Pixels = RawPicture(rawData);
        else
            picture.DmIcon = AdventurePicture(adventureData.Str("image"));
        var choices = new UiNode(UiKind.Stack) { Key = "choices", Vertical = true }.With(
            new UiNode(UiKind.Divider) { Key = "divider" },
            new UiNode(UiKind.StackItem) { Key = "grow1", Grow = 1 });
        foreach (UiValue choice in adventureData.Arr("choices"))
        {
            string key = choice.Str("key");
            choices.With(new UiNode(UiKind.StackItem) { Key = "choice:" + key }.With(
                new UiNode(UiKind.Button) { Key = "choice", Fluid = true, TextAlign = "center", Text = choice.Str("text"), OnClick = () => c.Act("adventure_choice", ("choice", key)) }));
        }
        choices.With(new UiNode(UiKind.StackItem) { Key = "grow2", Grow = 1 });
        return new UiNode(UiKind.Section) { Key = "adventure", Fill = true, Title = "Exploration" }.With(
            new UiNode(UiKind.Stack) { Key = "adventure" }.With(
                new UiNode(UiKind.StackItem) { Key = "description", Grow = 1 }.With(new UiNode(UiKind.Box) { Key = "description", Wrap = true, Text = adventureData.Str("description") }),
                new UiNode(UiKind.Divider) { Key = "divider" },
                new UiNode(UiKind.StackItem) { Key = "side" }.With(picture, choices)));
    }

    /// <summary>An adventure node's own picture (tg's raw_image, a <c>data:image/png;base64,</c> URI) as pixels; null when it cannot be read.</summary>
    public static UiPixels RawPicture(string dataUri)
    {
        lock (RawPictures)
        {
            if (RawPictures.TryGetValue(dataUri, out UiPixels cached))
                return cached;
        }
        UiPixels pixels = null;
        int comma = dataUri.IndexOf(',', StringComparison.Ordinal);
        try
        {
            Raster raster = PngDecoder.Decode(Convert.FromBase64String(dataUri[(comma + 1)..]));
            var rgba = new uint[raster.Width * raster.Height];
            for (int i = 0; i < rgba.Length; i++)
                rgba[i] = (uint)raster.Pixels[i * 4] << 24 | (uint)raster.Pixels[i * 4 + 1] << 16 | (uint)raster.Pixels[i * 4 + 2] << 8 | raster.Pixels[i * 4 + 3];
            pixels = new UiPixels(raster.Width, raster.Height, rgba);
        }
        catch (Exception e) when (e is FormatException or InvalidDataException)
        {
            pixels = null;
        }
        lock (RawPictures)
        {
            if (RawPictures.Count > 64)
                RawPictures.Clear();
            RawPictures[dataUri] = pixels;
        }
        return pixels;
    }

    // tg: ExodroneConsole.tsx:893
    private static UiNode DroneScreen(TguiContext c)
    {
        UiValue drone = c.Data;
        switch (drone.Str("drone_status"))
        {
            case "busy":
                return TimeoutScreen(drone);
            case "idle":
            case "travel":
                return TravelTargetSelectionScreen(c, false, () => { });
            case "adventure":
                return AdventureScreen(c, drone.Field("adventure_data"));
            case "exploration":
                return drone.Field("event") is { Kind: UiValueKind.Object } @event ? EventScreen(c, @event) : ExplorationScreen(c);
            default:
                return null;
        }
    }

    // tg: ExodroneConsole.tsx:921
    private static UiNode ExodroneConsoleContent(TguiContext c)
    {
        UiValue data = c.Data;
        if (!data.Truthy("drone"))
            return DroneSelectionSection(c, data.Arr("all_drones"));
        var log = new UiNode(UiKind.LabeledList) { Key = "log" };
        int index = 0;
        foreach (UiValue line in data.Arr("drone_log"))
        {
            index++;
            log.With(new UiNode(UiKind.LabeledItem) { Key = "log-" + index, Label = "#" + index, Text = line.AsStr() });
        }
        return new UiNode(UiKind.Stack) { Key = "console", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "main", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "main", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "equipment" }.With(EquipmentGrid(c)),
                new UiNode(UiKind.StackItem) { Key = "screen", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "screen", Vertical = true, Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "screen", Grow = 1 }.With(DroneScreen(c)),
                    new UiNode(UiKind.StackItem) { Key = "log", Grow = 1 }.With(
                        new UiNode(UiKind.Section) { Key = "log", Title = "Drone Log", Fill = true, Scrollable = true }.With(log)))))));
    }
}
