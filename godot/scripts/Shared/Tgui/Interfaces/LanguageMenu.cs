using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/LanguageMenu.tsx (gaps L2, D327: the language menu): the
// languages the mob speaks or understands (every one for an admin or a ghost), spoken first then by name, each with its icon, name and
// description, whether it is spoken (and whether the tongue allows it), understood (or how much of it), its key, the default
// language's checkbox, and an admin's or a ghost's grant, removal and omnitongue. Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class LanguageMenu
{
    // tg: LanguageMenu.tsx:228
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool adminMode = data.Truthy("admin_mode");
        bool isLiving = data.Truthy("is_living");
        IReadOnlyList<UiValue> languages = data.Get("languages")?.Items ?? Array.Empty<UiValue>();

        // only show languages we can speak OR understand, UNLESS we're an admin
        // also, push all languages we can speak to the top, then all languagse we can only understand, then alphabetize
        var shown = languages.Where(language => adminMode || language.Truthy("can_speak") || language.Truthy("can_understand") || language.Num("partial_understanding") > 0).ToList();
        StableSort(shown, (a, b) => ((a.Truthy("can_speak") ? 1 : 0) - (b.Truthy("can_speak") ? 1 : 0)) * -2 + (string.CompareOrdinal(a.Str("name"), b.Str("name")) > 0 ? 1 : 0));

        var header = new UiNode(UiKind.TableRow) { Key = "header" }.With(
            new UiNode(UiKind.TableCell) { Key = "name", Bold = true, Text = "Name" },
            new UiNode(UiKind.TableCell) { Key = "speak", Bold = true, Text = "Speak" },
            new UiNode(UiKind.TableCell) { Key = "understand", Bold = true, Text = "Understand" },
            new UiNode(UiKind.TableCell) { Key = "key", Bold = true, Underline = true, Tooltip = "Use this key in your message to speak in this language.", Text = "Key" });
        if (isLiving)
            header.With(new UiNode(UiKind.TableCell) { Key = "default", Bold = true, Underline = true, Tooltip = "Determines which language you speak naturally, without using the 'key'.", Text = "Default" });
        if (adminMode)
            header.With(new UiNode(UiKind.TableCell) { Key = "admin" });

        var table = new UiNode(UiKind.Table) { Key = "languages" }.With(header);
        foreach (UiValue language in shown)
            table.With(Row(c, language, isLiving, adminMode));

        bool omnitongue = data.Truthy("omnitongue");
        var section = new UiNode(UiKind.Section)
        {
            Key = "section",
            Fill = true,
            Scrollable = true,
            Title = adminMode ? "- Admin Mode -" : null,
            Buttons = adminMode
                ? new UiNode(UiKind.Button)
                {
                    Key = "omnitongue",
                    Tooltip = "If enabled, the mob's tongue will no longer prevent them from speaking languages they are physically incapable of speaking.",
                    Selected = omnitongue,
                    Text = "Omnitongue " + (omnitongue ? "Enabled" : "Disabled"),
                    OnClick = () => c.Act("toggle_omnitongue"),
                }
                : null,
        }.With(table);
        return c.Window(adminMode ? 700 : 500, Math.Min(shown.Count * 25 + (adminMode ? 100 : 70), 500), "Language Menu", section);
    }

    // tg: LanguageMenu.tsx:100  LanguageRow
    private static UiNode Row(TguiContext c, UiValue language, bool isLiving, bool adminMode)
    {
        string name = language.Str("name");
        bool canSpeak = language.Truthy("can_speak");
        bool couldSpeak = language.Truthy("could_speak");
        bool canUnderstand = language.Truthy("can_understand");
        double partial = language.Num("partial_understanding");
        string key = language.Str("key");

        // tg: LangSpeakIcon (:42)
        var speakIcon = new UiNode(UiKind.Icon)
        {
            Key = "speakIcon",
            Icon = "comment",
            Color = couldSpeak ? (canSpeak ? "good" : "bad") : (canSpeak ? "average" : "grey"),
            Tooltip = couldSpeak
                ? null
                : canSpeak
                    ? "Despite knowing how to speak " + name + ", you are unable due to physical limitations (usually, your tongue)."
                    : "Even if you were to learn how to speak " + name + ", you would be unable due to physical limitations (usually, your tongue).",
        };

        // tg: LangUnderstandIcon (:61)
        UiNode understand = !canUnderstand && partial > 0
            ? new UiNode(UiKind.Box) { Key = "partial", Underline = true, Tooltip = "You can only partially understand " + name + ".", Text = TguiFormat.JsNumber(partial) + "%" }
            : new UiNode(UiKind.Icon) { Key = "brain", Icon = "brain", Color = canUnderstand ? "good" : "bad" };

        // tg: LanguageNameAndDesc (:83)
        string desc = language.Str("desc");
        var nameBox = new UiNode(UiKind.Box) { Key = "nameText", Underline = !string.IsNullOrEmpty(desc), Tooltip = string.IsNullOrEmpty(desc) ? null : desc, Text = name };
        string icon = language.Str("icon");
        string iconState = language.Str("icon_state");
        UiNode nameCell = !string.IsNullOrEmpty(icon) && !string.IsNullOrEmpty(iconState)
            ? new UiNode(UiKind.TableCell) { Key = "name" }.With(new UiNode(UiKind.Stack) { Key = "nameStack" }.With(
                new UiNode(UiKind.StackItem) { Key = "iconItem" }.With(new UiNode(UiKind.Box) { Key = "icon", DmIcon = icon, DmIconState = iconState, Width = "32px", Height = "32px" }),
                new UiNode(UiKind.StackItem) { Key = "nameItem" }.With(nameBox)))
            : new UiNode(UiKind.TableCell) { Key = "name" }.With(nameBox);

        var row = new UiNode(UiKind.TableRow) { Key = "language:" + name, Candystripe = true }.With(
            nameCell,
            new UiNode(UiKind.TableCell) { Key = "speak" }.With(speakIcon),
            new UiNode(UiKind.TableCell) { Key = "understand" }.With(understand),
            new UiNode(UiKind.TableCell) { Key = "key", Text = string.IsNullOrEmpty(key) ? "N/A" : "," + key });
        if (isLiving)
        {
            bool isDefault = language.Truthy("is_default");
            row.With(new UiNode(UiKind.TableCell) { Key = "defaultCell" }.With(new UiNode(UiKind.Button)
            {
                Key = "default",
                Checkbox = isDefault,
                Color = isDefault ? "good" : couldSpeak && canSpeak ? "grey" : "transparent",
                OnClick = () => c.Act("select_default", ("language_name", name)),
            }));
        }
        if (adminMode)
        {
            row.With(new UiNode(UiKind.TableCell) { Key = "adminCell" }.With(
                new UiNode(UiKind.Button) { Key = "grant", Disabled = canSpeak && canUnderstand, Text = "Grant", OnClick = () => c.Act("grant_language", ("language_name", name)) },
                new UiNode(UiKind.Button) { Key = "remove", Disabled = !canSpeak && !canUnderstand, Text = "Remove", OnClick = () => c.Act("remove_language", ("language_name", name)) }));
        }
        return row;
    }

    // JavaScript's Array.prototype.sort is stable (a TimSort): an insertion sort keeps equal elements in order with tg's comparator
    private static void StableSort(List<UiValue> list, Func<UiValue, UiValue, int> compare)
    {
        for (int i = 1; i < list.Count; i++)
        {
            UiValue item = list[i];
            int j = i - 1;
            while (j >= 0 && compare(list[j], item) > 0)
            {
                list[j + 1] = list[j];
                j--;
            }
            list[j + 1] = item;
        }
    }
}
