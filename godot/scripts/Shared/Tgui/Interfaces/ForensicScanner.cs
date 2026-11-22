using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ForensicScanner/index.tsx and ForensicLogs.tsx (gaps J4, D298).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ForensicScanner
{
    // tg: ForensicScanner/index.tsx:7
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> logs = data.Arr("logs");
        return c.Window(512, 512, null,
            logs.Count == 0
                ? new UiNode(UiKind.NoticeBox) { Text = "Log empty." }
                : new UiNode(UiKind.Section)
                {
                    Title = "Scan history",
                    Fill = true,
                    Scrollable = true,
                    Buttons = new UiNode(UiKind.Fragment).With(
                        c.ButtonConfirm("clear", new UiNode(UiKind.Button) { Icon = "trash", Color = "danger", Text = "Clear logs", OnClick = () => c.Act("clear") }),
                        new UiNode(UiKind.Button) { Key = "print", Icon = "print", Text = "Print report", OnClick = () => c.Act("print") }),
                }.With(logs.Select((log, index) => ForensicLogs(c, log, index)).Reverse()));
    }

    // tg: ForensicScanner/ForensicLogs.tsx:13
    private static UiNode ForensicLogs(TguiContext c, UiValue log, int index)
    {
        UiValue categories = c.Data.Get("categories");
        IReadOnlyList<UiValue> dataEntries = log.Arr("dataEntries");
        string target = log.Str("scanTarget");
        string capitalized = target.Length > 0 ? char.ToUpperInvariant(target[0]) + target[1..] : target;
        return new UiNode(UiKind.Section)
        {
            Key = "log-" + index,
            Title = $"{capitalized} scan at {log.Str("scanTime")} ",
            Buttons = new UiNode(UiKind.Button) { Key = "delete-" + index, Icon = "trash", Color = "transparent", OnClick = () => c.Act("delete", ("index", index)) },
        }.With(dataEntries.Count == 0
            ? new UiNode(UiKind.Box) { Opacity = 0.5, Text = "No forensic traces found." }
            : new UiNode(UiKind.LabeledList).With(dataEntries.Select(entry =>
            {
                UiValue category = categories?.Get(entry.Str("category"));
                return new UiNode(UiKind.LabeledItem) { Key = category.Str("name"), Label = category.Str("name") }.With(
                    ForensicLog(entry.Str("category"), entry.Get("data"), category.Str("uiIcon"), category.Str("uiIconColor")));
            })));
    }

    // tg: ForensicScanner/ForensicLogs.tsx:61  (the category ids are the server's: tg's TSX tests 'Fingerprints' and 'ID_Access', which no category's id is, so they fall to the default row, as in tg)
    private static UiNode ForensicLog(string logCategoryId, UiValue log, string iconName, string iconColor)
    {
        IEnumerable<KeyValuePair<string, string>> entries = Entries(log);
        if (logCategoryId == "Reagents")
        {
            return new UiNode(UiKind.LabeledList).With(entries.Select(e =>
                new UiNode(UiKind.LabeledItem)
                {
                    Key = e.Key,
                    LabelNode = new UiNode(UiKind.Fragment).With(new UiNode(UiKind.Icon) { Icon = iconName, Color = iconColor }, new UiNode(UiKind.Box) { Text = e.Key }),
                    Text = $"{e.Value} u.",
                }));
        }
        if (logCategoryId == "Blood")
            return new UiNode(UiKind.Fragment).With(entries.Select(e => Row(e.Key, $"{e.Key}, {e.Value}".ToUpperInvariant(), iconName, iconColor)));
        return new UiNode(UiKind.Fragment).With(entries.Select(e => Row(e.Key, e.Value, iconName, iconColor)));
    }

    private static UiNode Row(string key, string text, string iconName, string iconColor) =>
        new UiNode(UiKind.Stack) { Key = key }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Icon) { Icon = iconName, Color = iconColor }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = text }));

    // Object.entries(log): an assoc list arrives as an object; DM's plain list of strings as an array, whose keys are the indices
    private static IEnumerable<KeyValuePair<string, string>> Entries(UiValue log)
    {
        if (log == null)
            yield break;
        if (log.Kind == UiValueKind.Array)
        {
            for (int i = 0; i < log.Items.Count; i++)
                yield return new KeyValuePair<string, string>(i.ToString(), Plain(log.Items[i]));
            yield break;
        }
        foreach (KeyValuePair<string, UiValue> field in log.Fields)
            yield return new KeyValuePair<string, string>(field.Key, Plain(field.Value));
    }

    private static string Plain(UiValue v) => v == null ? "" : v.Kind == UiValueKind.String ? v.Text : v.ToJson();
}
