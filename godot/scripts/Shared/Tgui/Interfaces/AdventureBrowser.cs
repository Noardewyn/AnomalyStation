using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AdventureBrowser.tsx: the admins' adventure manager:
// the loaded adventures with their files and authors, and a playtest of one on the ExodroneConsole's adventure screen (or its delay).
// Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AdventureBrowser
{
    // tg: AdventureBrowser.tsx:78
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string feedback = data.Str("feedback_message");
        UiNode shown = data.Truthy("play_mode") ? DebugPlayer(c) : AdventureList(c);
        return string.IsNullOrEmpty(feedback)
            ? c.Window(600, 400, "Adventure Overview", shown)
            : c.Window(600, 400, "Adventure Overview", new UiNode(UiKind.NoticeBox) { Key = "feedback", Text = feedback }, shown);
    }

    // tg: AdventureBrowser.tsx:28
    private static UiNode AdventureList(TguiContext c)
    {
        var table = new UiNode(UiKind.Table) { Key = "adventures" }.With(new UiNode(UiKind.TableRow) { Key = "header" }.With(
            new UiNode(UiKind.TableCell) { Key = "filename", Color = "label", Text = "Filename" },
            new UiNode(UiKind.TableCell) { Key = "title", Color = "label", Text = "Title" },
            new UiNode(UiKind.TableCell) { Key = "author", Color = "label", Text = "Author" },
            new UiNode(UiKind.TableCell) { Key = "playtest", Color = "label", Text = "Playtest" }));
        foreach (UiValue adventure in c.Data.Arr("adventures"))
        {
            string reference = adventure.Str("ref");
            table.With(new UiNode(UiKind.TableRow) { Key = reference, Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Key = "filename", Text = adventure.Str("filename") },
                new UiNode(UiKind.TableCell) { Key = "title", Text = adventure.Str("name") },
                new UiNode(UiKind.TableCell) { Key = "author", Text = adventure.Str("uploader") },
                new UiNode(UiKind.TableCell) { Key = "playtest" }.With(
                    new UiNode(UiKind.Button) { Key = "play", Color = "good", Text = "Play", OnClick = () => c.Act("play", ("ref", reference)) })));
        }
        return table;
    }

    // tg: AdventureBrowser.tsx:57
    private static UiNode DebugPlayer(TguiContext c)
    {
        UiValue data = c.Data;
        double delayTime = data.Num("delay_time");
        return new UiNode(UiKind.Section)
        {
            Key = "playtest",
            Title = "Playtest",
            Buttons = new UiNode(UiKind.Button) { Key = "end", Text = "End Playtest", OnClick = () => c.Act("end_play") },
        }.With(delayTime > 0
            ? new UiNode(UiKind.Box) { Key = "delay", Text = "DELAY " + TguiFormat.FormatTime(delayTime) + " / " + data.Str("delay_message") }
            : ExodroneConsole.AdventureScreen(c, data.Field("adventure_data")));
    }
}
