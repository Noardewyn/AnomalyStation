using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SimpleBot.tsx (gaps package C3). The window of every bot but the MULE: the settings over
// tg's ui_data (_bots.dm:600) and the controls of each bot's custom_controls. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// Not drawn: the floorbot's two controls (FloorbotTiles, FloorbotLine): no ui_data of this snapshot sends them.

public static class SimpleBot
{
    // tg: SimpleBot.tsx:36
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool access = !data.Truthy("locked") || data.Truthy("can_hack");

        // tg: SimpleBot.tsx:42  <Window width={450} height={300}>
        return c.Window(450, 300, null,
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem).With(BotSettings(c)),
                access ? new UiNode(UiKind.StackItem) { Grow = 1 }.With(BotControl(c)) : null));
    }

    // tg: SimpleBot.tsx:60
    public static UiNode BotSettings(TguiContext c)
    {
        UiValue data = c.Data;
        bool access = !data.Truthy("locked") || data.Truthy("can_hack");
        return new UiNode(UiKind.Section) { Title = "Settings", Buttons = TabDisplay(c) }.With(
            !access ? new UiNode(UiKind.NoticeBox) { Text = "Locked!" } : SettingsDisplay(c));
    }

    // tg: SimpleBot.tsx:71
    public static UiNode BotControl(TguiContext c)
    {
        UiValue controls = c.Data.Field("custom_controls");
        var section = new UiNode(UiKind.Section) { Fill = true, Scrollable = true, Title = "Controls" };
        var list = new UiNode(UiKind.LabeledControls) { Wrap = true };
        if (controls != null)
        {
            foreach (KeyValuePair<string, UiValue> control in controls.Fields)
            {
                // tg: capitalizeAll(control[0].replace('_', ' ')): the first underscore only
                string label = CapitalizeAll(ReplaceFirstUnderscore(control.Key));
                list.With(new UiNode(UiKind.LabeledControl) { Key = "control:" + control.Key, Label = label }.With(ControlHelper(c, control.Key, control.Value)));
            }
        }
        return section.With(list);
    }

    /** Creates a lock button at the top of the controls */
    // tg: SimpleBot.tsx:89
    private static UiNode TabDisplay(TguiContext c)
    {
        UiValue data = c.Data;
        bool can_hack = data.Truthy("can_hack");
        bool emagged = data.Truthy("emagged");
        bool has_access = data.Truthy("has_access");
        bool locked = data.Truthy("locked");
        bool allow_possession = data.Field("settings")?.Truthy("allow_possession") == true;
        return new UiNode(UiKind.Fragment).With(
            can_hack
                ? new UiNode(UiKind.Button)
                {
                    Key = "hack",
                    Color = "danger",
                    Icon = emagged ? "bug" : "lock",
                    Selected = !emagged,
                    Text = emagged ? "Malfunctional" : "Safety Lock",
                    Tooltip = !emagged ? "Unlocks the safety protocols." : "Resets the bot operating system.",
                    OnClick = () => c.Act("hack"),
                }
                : null,
            allow_possession ? PaiButton(c) : null,
            new UiNode(UiKind.Button)
            {
                Key = "rename",
                Color = "transparent",
                Icon = "fa-poll-h",
                Text = "Rename",
                Tooltip = "Update the bot's name registration.",
                OnClick = () => c.Act("rename"),
            },
            new UiNode(UiKind.Button)
            {
                Key = "lock",
                Color = "transparent",
                Disabled = !has_access && !can_hack,
                Icon = locked ? "lock" : "lock-open",
                Selected = locked,
                Text = "Controls Lock",
                Tooltip = (locked ? "Unlock" : "Lock") + " the control panel.",
                OnClick = () => c.Act("lock"),
            });
    }

    /** Creates a button indicating PAI status and offers the eject action */
    // tg: SimpleBot.tsx:149
    private static UiNode PaiButton(TguiContext c)
    {
        bool pai_inserted = c.Data.Field("settings")?.Truthy("pai_inserted") == true;
        if (!pai_inserted)
            return new UiNode(UiKind.Button)
            {
                Key = "pai",
                Color = "transparent",
                Icon = "robot",
                Text = "No PAI Inserted",
                Tooltip = "Insert an active PAI card to control this device.",
            };
        return new UiNode(UiKind.Button)
        {
            Key = "pai",
            Icon = "eject",
            Text = "Eject PAI",
            Tooltip = "Ejects the current PAI.",
            OnClick = () => c.Act("eject_pai"),
        };
    }

    /** Displays the bot's standard settings: Power, patrol, etc. */
    // tg: SimpleBot.tsx:180
    private static UiNode SettingsDisplay(TguiContext c)
    {
        UiValue settings = c.Data.Field("settings");
        bool Set(string key) => settings?.Truthy(key) == true;
        bool airplane_mode = Set("airplane_mode");
        bool patrol_station = Set("patrol_station");
        bool power = Set("power");
        bool maintenance_lock = Set("maintenance_lock");
        bool allow_possession = Set("allow_possession");
        bool possession_enabled = Set("possession_enabled");
        return new UiNode(UiKind.LabeledControls).With(
            Setting(c, "Power", "power", "power-off", power ? "good" : "gray", "Powers " + (power ? "off" : "on") + " the bot."),
            Setting(c, "Airplane Mode", "airplane", "plane", airplane_mode ? "yellow" : "gray", (!airplane_mode ? "Disables" : "Enables") + " remote access via console."),
            Setting(c, "Patrol Station", "patrol", "map-signs", patrol_station ? "good" : "gray", (patrol_station ? "Disables" : "Enables") + " automatic station patrol."),
            Setting(c, "Maintenance Lock", "maintenance", "toolbox", maintenance_lock ? "yellow" : "gray", maintenance_lock ? "Opens the maintenance hatch for repairs." : "Closes the maintenance hatch."),
            allow_possession
                ? Setting(c, "Personality", "toggle_personality", "robot", possession_enabled ? "good" : "gray", possession_enabled ? "Resets personality to factory default." : "Enables download of a unique personality.")
                : null);
    }

    private static UiNode Setting(TguiContext c, string label, string action, string icon, string color, string tooltip) =>
        new UiNode(UiKind.LabeledControl) { Key = "setting:" + action, Label = label }.With(
            new UiNode(UiKind.Icon) { Key = action, Size = 2, Icon = icon, Color = color, Tooltip = tooltip, OnClick = () => c.Act(action) });

    /** Helper function which identifies which button to create. */
    // tg: SimpleBot.tsx:286
    private static UiNode ControlHelper(TguiContext c, string control, UiValue value)
    {
        if (control == "heal_threshold")
            return new UiNode(UiKind.Slider)
            {
                Key = control,
                Tooltip = "Adjusts the sensitivity for damage treatment.",
                MinValue = 5,
                MaxValue = 75,
                Step = 5,
                Unit = "%",
                Value = value?.Kind == UiValueKind.Number ? value.Number : 0,
                Ranges = new[]
                {
                    ("good", double.NegativeInfinity, 15.0),
                    ("average", 15.0, 55.0),
                    ("bad", 55.0, double.PositiveInfinity),
                },
                OnChange = threshold => c.Act(control, ("threshold", threshold)),
            };
        bool on = value?.Truthy == true;
        return new UiNode(UiKind.Icon)
        {
            Key = control,
            Color = on ? "good" : "gray",
            Icon = on ? "toggle-on" : "toggle-off",
            Size = 2,
            OnClick = () => c.Act(control),
        };
    }

    private static string ReplaceFirstUnderscore(string text)
    {
        int index = text.IndexOf('_');
        return index < 0 ? text : text[..index] + " " + text[(index + 1)..];
    }

    // tgui-core's capitalizeAll: every word starts with a capital
    private static string CapitalizeAll(string text) =>
        string.Join(" ", text.Split(' ').Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..]));
}
