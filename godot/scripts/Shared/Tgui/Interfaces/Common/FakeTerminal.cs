namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/common/FakeTerminal.tsx (syndicate X1a, D316: the
// contractor uplink's terminals). Engine-free. tgui's interval and timeout are the window's render-again time (TguiContext.RenderAgainAt).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class FakeTerminal
{
    /// <summary>
    /// tg's FakeTerminal: <paramref name="allMessages"/> typed out a line every 1 / <paramref name="linesPerSecond"/> seconds (its own clock
    /// starts at the first render under <paramref name="key"/>); a tick after the last line it waits <paramref name="finishedTimeout"/>
    /// milliseconds and calls <paramref name="onFinished"/> once.
    /// </summary>
    // tg: FakeTerminal.tsx:9
    public static UiNode Render(TguiContext c, string key, IReadOnlyList<string> allMessages, double linesPerSecond = 2.5, double finishedTimeout = 0, Action onFinished = null)
    {
        double start = c.UseState(key + ":start", double.NaN);
        if (double.IsNaN(start))
        {
            start = c.Now;
            c.Local.SetQuiet(key + ":start", start);
        }
        double interval = 1 / linesPerSecond;
        // tick k (from 1) shows the message of index k - 1 while k - 1 <= the count (tg's last tick shows an undefined line); tick
        // count + 2 stops the interval and starts the finish timeout
        int ticks = (int)Math.Floor((c.Now - start) / interval);
        int shown = Math.Min(ticks, allMessages.Count);
        int finishTick = allMessages.Count + 2;
        double finishAt = start + finishTick * interval + finishedTimeout / 1000;
        if (ticks < finishTick)
            c.RenderAgainAt(start + (ticks + 1) * interval);
        else if (onFinished != null && !c.UseState(key + ":finished", false))
        {
            if (c.Now >= finishAt)
            {
                c.Local.SetQuiet(key + ":finished", true);
                onFinished();
            }
            else
            {
                c.RenderAgainAt(finishAt);
            }
        }

        // tg: FakeTerminal.tsx:47  <Box m={1}> the lines, each with a <br />
        var box = new UiNode(UiKind.Box) { Key = key };
        for (int i = 0; i < shown; i++)
            box.With(new UiNode(UiKind.Box) { Key = key + ":" + i, Text = allMessages[i] });
        return box;
    }
}
