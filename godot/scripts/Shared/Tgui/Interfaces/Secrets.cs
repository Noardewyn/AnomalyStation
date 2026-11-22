using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Secrets.jsx: the Secrets Panel, from /datum/secrets_menu's
// ui_data (secrets.dm:36). DV351: only the tabs and buttons whose ui_act is translated are drawn (the lights of Helpful, the power of
// Fun); Debugging and Only Fun For You come with theirs. Original copyright and license: tgstation, AGPL-3.0 (code). See
// ../tgstation/LICENSE.

public static class Secrets
{
    private const int Helpful = 2, Fun = 3;

    // tg: Secrets.jsx:662
    public static UiNode Render(TguiContext c)
    {
        bool isFunmin = c.Data.Truthy("is_funmin");
        int tabIndex = c.UseState("tabIndex", Helpful);
        // tg: Secrets.jsx:16 TAB2NAME
        (string title, string blurb, double gauge) = tabIndex == Fun
            ? ("Fun", "How I ran an \"\"\"event\"\"\"", 75.0)
            : ("Helpful", "Where fuckwits put logging", 25.0);

        UiNode TabButton(int index, string text, string icon, bool disabled) => new UiNode(UiKind.Button)
        {
            Key = text,
            Text = text,
            Icon = icon,
            Disabled = disabled,
            Selected = tabIndex == index,
            OnClick = () => c.SetState("tabIndex", index),
        };

        UiNode Act(string action, string text, string icon) => new UiNode(UiKind.StackItem) { Key = action }.With(
            new UiNode(UiKind.Button) { Key = action, Text = text, Icon = icon, Fluid = true, OnClick = () => c.Act(action) });

        // tg's theme="admin": the kit's default theme
        return c.Window(500, 520, "Secrets Panel",
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "head" }.With(
                    new UiNode(UiKind.Section) { Title = "Secrets" }.With(
                        new UiNode(UiKind.NoticeBox) { Color = "black", Text = "\"The first rule of adminbuse is: you don't talk about the adminbuse.\"" },
                        new UiNode(UiKind.Stack) { Key = "tabs" }.With(
                            new UiNode(UiKind.StackItem) { Key = "helpful" }.With(TabButton(Helpful, "Helpful", "question", false)),
                            new UiNode(UiKind.StackItem) { Key = "fun" }.With(TabButton(Fun, "Fun", "smile", !isFunmin)),
                            new UiNode(UiKind.StackItem) { Key = "gauge" }.With(
                                new UiNode(UiKind.LabeledControls).With(
                                    new UiNode(UiKind.LabeledControl) { Label = "Chances of admin complaint", MinWidth = "66px" }.With(
                                        new UiNode(UiKind.RoundGauge)
                                        {
                                            Key = "complaint",
                                            Size = 2,
                                            Value = gauge,
                                            MinValue = 0,
                                            MaxValue = 100,
                                            AlertAfter = 70,
                                            Ranges = new[] { ("good", -2.0, 25.0), ("average", 25.0, 75.0), ("bad", 75.0, 100.0) },
                                            Format = value => value.ToString("0") + "%",
                                        })))))),
                new UiNode(UiKind.StackItem) { Key = "tab", Grow = 1 }.With(
                    new UiNode(UiKind.Section) { Title = title + " Or: " + blurb }.With(
                        new UiNode(UiKind.Stack) { Vertical = true }.With(tabIndex == Fun
                            ? new[]
                            {
                                // tg: Secrets.jsx:318-340
                                Act("power", "All areas powered", "bolt"),
                                Act("unpower", "All areas unpowered", "moon"),
                                Act("quickpower", "recharge SMESs", "plug"),
                            }
                            : new[]
                            {
                                // tg: Secrets.jsx:131-146
                                Act("blackout", "Break all lights", "grin-beam-sweat"),
                                Act("whiteout", "Fix all lights", "magic"),
                            })))));
    }
}
