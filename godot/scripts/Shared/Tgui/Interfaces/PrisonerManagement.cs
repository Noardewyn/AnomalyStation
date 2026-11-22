using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PrisonerManagement.tsx (gaps LC1): the login screen, the
// prisoner ID's points and goal, and the tracked implants by category with their data and buttons
// (machinery/computer/prisoner/management.dm:21 ui_data). tg's shared tab state is the window's local state.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class PrisonerManagement
{
    // tg: PrisonerManagement.tsx:256
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        // tg: PrisonerManagement.tsx:260  <Window width={465} height={565} title="Prisoner Management">
        return c.Window(465, 565, "Prisoner Management", data.Truthy("authorized") ? ManagementConsole(c, data) : LogIn(c));
    }

    // tg: PrisonerManagement.tsx:47
    private static UiNode ImplantDisplay(TguiContext c, UiValue implant)
    {
        string implantRef = implant.Str("ref");
        IReadOnlyList<KeyValuePair<string, UiValue>> info = implant.Get("info")?.Fields ?? Array.Empty<KeyValuePair<string, UiValue>>();
        IReadOnlyList<UiValue> buttons = implant.Get("buttons")?.Items ?? Array.Empty<UiValue>();
        return new UiNode(UiKind.Stack) { Key = "implant:" + implantRef, Vertical = true }.With(new UiNode(UiKind.StackItem).With(
            new UiNode(UiKind.LabeledList).With(
                info.Select(kv => new UiNode(UiKind.LabeledItem) { Key = "info:" + kv.Key, Label = kv.Key, Text = kv.Value.AsStr() }).Concat(new[]
                {
                    buttons.Count != 0
                        ? new UiNode(UiKind.LabeledItem) { Key = "options", Label = "Options" }.With(buttons.Select(button => new UiNode(UiKind.Button)
                        {
                            Key = "button:" + button.Str("action_key"),
                            Icon = button.Str("icon", null),
                            Color = button.Str("color", null),
                            Tooltip = button.Str("tooltip", null),
                            Text = button.Str("name", null),
                            OnClick = () =>
                            {
                                var args = new List<(string, object)> { ("implant_ref", implantRef), ("implant_action", button.Str("action_key")) };
                                foreach (var param in button.Get("action_params")?.Fields ?? Array.Empty<KeyValuePair<string, UiValue>>())
                                    args.Add((param.Key, param.Value.Kind == UiValueKind.Number ? param.Value.Number : param.Value.AsStr()));
                                c.Act("handle_implant", args.ToArray());
                            },
                        }))
                        : null,
                    new UiNode(UiKind.Divider) { Key = "divider" },
                }))));
    }

    // Converts a category ("tracking implant") to a more readable format ("Tracking")
    // tg: PrisonerManagement.tsx:102
    private static string FormatCategory(string category)
    {
        string firstWord = category.Split(' ')[0];
        return firstWord.Length == 0 ? firstWord : char.ToUpperInvariant(firstWord[0]) + firstWord.Substring(1);
    }

    // tg: PrisonerManagement.tsx:107
    private static UiNode AllImplantDisplay(TguiContext c, IReadOnlyList<UiValue> implants)
    {
        // tg: sortImplants (:89): the categories in the order they first appear
        var byCategory = new List<(string Category, List<UiValue> Implants)>();
        foreach (UiValue implant in implants)
        {
            string category = implant.Str("category");
            int index = byCategory.FindIndex(e => e.Category == category);
            if (index < 0)
                byCategory.Add((category, new List<UiValue> { implant }));
            else
                byCategory[index].Implants.Add(implant);
        }
        string implantTab = c.UseState("implantTab", byCategory.Count > 0 ? byCategory[0].Category : null);
        List<UiValue> shown = byCategory.FirstOrDefault(e => e.Category == implantTab).Implants;
        return new UiNode(UiKind.Stack) { Key = "implants", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Tabs).With(byCategory.Select(e => new UiNode(UiKind.Tab)
            {
                Key = "tab:" + e.Category,
                Selected = implantTab == e.Category,
                Text = FormatCategory(e.Category),
                OnClick = () => c.SetState("implantTab", e.Category),
            }))),
            new UiNode(UiKind.StackItem).With(implantTab != null && shown != null
                ? shown.Select(implant => ImplantDisplay(c, implant)).ToArray()
                : new[] { new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No implants detected." } }));
    }

    // tg: PrisonerManagement.tsx:146
    private static UiNode IdShowcase(TguiContext c, UiValue id)
    {
        bool hasId = id != null && id.Kind != UiValueKind.Null;
        return new UiNode(UiKind.Stack) { Key = "id_section", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.LabeledList).With(hasId
                ? new[]
                {
                    new UiNode(UiKind.LabeledItem) { Key = "id", Label = "ID" }.With(
                        new UiNode(UiKind.Button) { Key = "eject_id", Icon = "eject", OnClick = () => c.Act("eject_id") },
                        new UiNode(UiKind.Box) { Text = id.Str("name") }),
                    new UiNode(UiKind.LabeledItem) { Key = "points", Label = "Points" }.With(
                        new UiNode(UiKind.Button) { Key = "reset_id", Icon = "times", Color = "bad", OnClick = () => c.Act("reset_id") },
                        new UiNode(UiKind.Box) { Text = TguiFormat.JsNumber(id.Num("points")) }),
                    new UiNode(UiKind.LabeledItem) { Key = "goal", Label = "Goal" }.With(
                        new UiNode(UiKind.Button) { Key = "set_id_goal", Icon = "check", OnClick = () => c.Act("set_id_goal") },
                        new UiNode(UiKind.Box) { Text = TguiFormat.JsNumber(id.Num("goal")) }),
                }
                : new[]
                {
                    new UiNode(UiKind.LabeledItem) { Key = "id", Label = "ID" }.With(
                        new UiNode(UiKind.Button) { Key = "insert_id", Text = "No ID Inserted", OnClick = () => c.Act("insert_id") }),
                })),
            hasId
                ? new UiNode(UiKind.StackItem).With(new UiNode(UiKind.NoticeBox)
                {
                    Key = "quota",
                    Text = "Space Law recommends quotas of 100 points per minute they would normally serve in the brig.",
                })
                : null);
    }

    // tg: PrisonerManagement.tsx:196
    private static UiNode ManagementConsole(TguiContext c, UiValue data)
    {
        IReadOnlyList<UiValue> implants = data.Get("implants")?.Items ?? Array.Empty<UiValue>();
        return new UiNode(UiKind.Stack) { Key = "console", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Section) { Key = "id_management", Title = "ID Management" }.With(IdShowcase(c, data.Get("inserted_id")))),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "security_implants", Title = "Security Implants", Scrollable = true, Fill = true }.With(AllImplantDisplay(c, implants))),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.NoticeBox) { Key = "secure", Color = "info", TextAlign = "right" }.With(
                new UiNode(UiKind.Box) { Text = "Secure Your Workspace." },
                new UiNode(UiKind.Button) { Key = "logout", Icon = "lock", Color = "good", Text = "Log Out", OnClick = () => c.Act("logout") })));
    }

    // tg: PrisonerManagement.tsx:226
    private static UiNode LogIn(TguiContext c) =>
        new UiNode(UiKind.Section) { Key = "login_screen", Fill = true }.With(new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Grow = 1 },
            new UiNode(UiKind.StackItem) { Align = "center", Grow = 2 }.With(new UiNode(UiKind.Icon) { Key = "warning", Color = "average", Icon = "exclamation-triangle", FontSize = "15em" }),
            new UiNode(UiKind.StackItem) { Align = "center", Grow = 1 }.With(new UiNode(UiKind.Box) { Key = "hub", Color = "red", FontSize = "18px", Bold = true, Text = "Nanotrasen SecurityHUB" }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.NoticeBox) { Key = "not_logged_in", TextAlign = "right" }.With(
                new UiNode(UiKind.Box) { Text = "You are not logged in." },
                new UiNode(UiKind.Button) { Key = "login", Icon = "lock-open", Text = "Login", OnClick = () => c.Act("login") }))));
}
