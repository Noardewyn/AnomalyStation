using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosCyborgRemoteMonitor.jsx (gaps S4b, D327): SiliConnect,
// the station's cyborgs with their status, condition, charge, model and upgrades, a message button, and the log downloaded by tapping a
// cyborg. useSharedState is the window's own state. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosCyborgRemoteMonitor
{
    // tg: NtosCyborgRemoteMonitor.jsx:15
    public static UiNode Render(TguiContext c) =>
        NtosWindow.Render(c, 600, 800, null, Content(c));

    // tg: NtosCyborgRemoteMonitor.jsx:25
    public static string ProgressSwitch(double param) => param switch
    {
        -1 => "_",
        0 => "Connecting",
        25 => "Starting Transfer",
        50 => "Downloading",
        75 => "Downloading",
        100 => "Formatting",
        _ => null,
    };

    // tg: NtosCyborgRemoteMonitor.jsx:42
    public static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        int tabMain = c.UseState("tab_main", 1);
        bool card = data.Truthy("card");
        IReadOnlyList<UiValue> cyborgs = data.Arr("cyborgs");
        double dlProgress = data.Num("DL_progress");
        IReadOnlyList<UiValue> storedlog = data.Arr("borglog");

        if (cyborgs.Count == 0)
            return new UiNode(UiKind.NoticeBox) { Text = "No cyborg units detected." };

        var stack = new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "tabs" }.With(new UiNode(UiKind.Tabs).With(
                new UiNode(UiKind.Tab) { Key = "tab:cyborgs", Icon = "robot", Text = "Cyborgs", Selected = tabMain == 1, OnClick = () => c.SetState("tab_main", 1) },
                new UiNode(UiKind.Tab) { Key = "tab:log", Icon = "clipboard", Text = "Stored Log File", Selected = tabMain == 2, OnClick = () => c.SetState("tab_main", 2) })));
        if (tabMain == 1)
        {
            if (!card)
                stack.With(new UiNode(UiKind.StackItem) { Key = "nocard" }.With(new UiNode(UiKind.NoticeBox) { Text = "Certain features require an ID card login." }));
            var list = new UiNode(UiKind.Section) { Fill = true, Scrollable = true };
            foreach (UiValue cyborg in cyborgs)
            {
                string reference = cyborg.Str("ref");
                double status = cyborg.Num("status");
                bool lockedDown = cyborg.Truthy("locked_down");
                double integ = cyborg.Num("integ");
                UiValue charge = cyborg.Get("charge");
                bool hasCharge = charge != null && charge.Kind == UiValueKind.Number;
                list.With(new UiNode(UiKind.Section)
                {
                    Key = "cyborg:" + reference,
                    Title = cyborg.Str("name"),
                    Buttons = new UiNode(UiKind.Button)
                    {
                        Key = "message",
                        Icon = "terminal",
                        Text = "Send Message",
                        Color = "blue",
                        Disabled = !card,
                        OnClick = () => c.Act("messagebot", ("ref", reference)),
                    },
                }.With(new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Key = "status", Label = "Status" }.With(new UiNode(UiKind.Box)
                    {
                        Color = status != 0 ? "bad" : lockedDown ? "average" : "good",
                        Text = status != 0 ? "Not Responding" : lockedDown ? "Locked Down" : cyborg.Truthy("shell_discon") ? "Nominal/Disconnected" : "Nominal",
                    }),
                    new UiNode(UiKind.LabeledItem) { Key = "condition", Label = "Condition" }.With(new UiNode(UiKind.Box)
                    {
                        Color = integ <= 25 ? "bad" : integ <= 75 ? "average" : "good",
                        Text = integ == 0 ? "Hard Fault" : integ <= 25 ? "Functionality Disrupted" : integ <= 75 ? "Functionality Impaired" : "Operational",
                    }),
                    new UiNode(UiKind.LabeledItem) { Key = "charge", Label = "Charge" }.With(new UiNode(UiKind.Box)
                    {
                        // a null charge compares as 0 in JavaScript (null <= 30): bad
                        Color = (hasCharge ? charge.Number : 0) <= 30 ? "bad" : charge.Number <= 70 ? "average" : "good",
                        Text = hasCharge ? TguiFormat.JsNumber(charge.Number) + "%" : "Not Found",
                    }),
                    new UiNode(UiKind.LabeledItem) { Key = "model", Label = "Model", Text = cyborg.Str("module") },
                    new UiNode(UiKind.LabeledItem) { Key = "upgrades", Label = "Upgrades", Text = string.Concat(cyborg.Arr("upgrades").Select(u => u.Text)) })));
            }
            stack.With(new UiNode(UiKind.StackItem) { Key = "list", Grow = 1 }.With(list));
        }
        if (tabMain == 2)
        {
            var log = new UiNode(UiKind.Section) { Fill = true, Scrollable = true, Background = "black" };
            int i = 0;
            foreach (UiValue entry in storedlog)
                log.With(new UiNode(UiKind.Box) { Key = "log:" + i++, Color = "green", Text = entry.Text });
            stack.With(
                new UiNode(UiKind.StackItem) { Key = "download" }.With(new UiNode(UiKind.Section).With(
                    new UiNode(UiKind.Box) { Text = "Scan a cyborg to download stored logs." },
                    new UiNode(UiKind.ProgressBar) { Key = "progress", Value = dlProgress / 100, Text = ProgressSwitch(dlProgress) })),
                new UiNode(UiKind.StackItem) { Key = "storedlog", Grow = 1 }.With(log));
        }
        return stack;
    }
}
