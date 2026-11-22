using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Timer.tsx (gaps AS1): the timer assembly (timer.dm:99).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Timer
{
    // tg: Timer.tsx:14
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool timing = data.Truthy("timing");
        bool loop = data.Truthy("loop");

        // tg: Timer.tsx:19  <Window width={275} height={115}>
        return c.Window(275, 115, null,
            new UiNode(UiKind.Section)
            {
                Title = "Timing Unit",
                Buttons = new UiNode(UiKind.Fragment).With(
                    new UiNode(UiKind.Button) { Key = "repeat", Icon = "sync", Text = loop ? "Repeating" : "Repeat", Selected = loop, OnClick = () => c.Act("repeat") },
                    new UiNode(UiKind.Button) { Key = "time", Icon = "clock-o", Text = timing ? "Stop" : "Start", Selected = timing, OnClick = () => c.Act("time") }),
            }.With(Content(c)));
    }

    /// <summary>Displays a few more buttons to control the timer.</summary>
    // tg: Timer.tsx:49
    private static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        bool timing = data.Truthy("timing");
        string minutes = TguiFormat.JsNumber(data.Num("minutes")).PadLeft(2, '0');
        string seconds = TguiFormat.JsNumber(data.Num("seconds")).PadLeft(2, '0');
        return new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.Button) { Key = "adjust-30", Icon = "fast-backward", Disabled = timing, OnClick = () => c.Act("input", ("adjust", -30)) },
            new UiNode(UiKind.Button) { Key = "adjust-1", Icon = "backward", Disabled = timing, OnClick = () => c.Act("input", ("adjust", -1)) },
            new UiNode(UiKind.Box) { Key = "clock", Text = minutes + ":" + seconds + " " },
            new UiNode(UiKind.Button) { Key = "adjust+1", Icon = "forward", Disabled = timing, OnClick = () => c.Act("input", ("adjust", 1)) },
            new UiNode(UiKind.Button) { Key = "adjust+30", Icon = "fast-forward", Disabled = timing, OnClick = () => c.Act("input", ("adjust", 30)) });
    }
}
