using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosEmojipedia.tsx (gaps S4c, D327): EmojiPedia, the emoji
// a PDA message can send, filtered by name; a click copies its :tag: to the clipboard. Each emoji is drawn from tg's icon (the
// emojipedia16x16 sprite sheet's states, icons/ui/chat/emoji.dmi). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosEmojipedia
{
    private const string EmojiSet = "icons/ui/chat/emoji.dmi";

    // tg: NtosEmojipedia.tsx:17
    public static UiNode Render(TguiContext c)
    {
        string filter = c.UseState("filter", "");
        var search = TguiFormat.CreateSearch<UiValue>(filter, emoji => emoji.Str("name"));
        List<UiValue> filteredEmojis = c.Data.Arr("emoji_list").Where(search).ToList();

        // required: follow semantic versioning every time you touch this file
        var section = new UiNode(UiKind.Section)
        {
            Title = "Emojipedia V3.7.10" + (filter != "" ? " - " + filter : ""),
            Buttons = new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Input) { Key = "filter", Tooltip = "Search by name", TextValue = filter, OnInput = v => c.SetState("filter", v) }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "help", Tooltip = "Click on an emoji to copy its tag!", Icon = "circle-question" })),
        };
        foreach (UiValue emoji in filteredEmojis)
        {
            string name = emoji.Str("name");
            section.With(new UiNode(UiKind.Button)
            {
                Key = "emoji:" + name,
                Tooltip = name,
                OnClick = () => TguiContext.CopyText?.Invoke(":" + name + ":"),
            }.With(new UiNode(UiKind.Box) { DmIcon = EmojiSet, DmIconState = name, Size = 2 }));
        }
        return NtosWindow.Render(c, 600, 640, null, new UiNode(UiKind.Box) { Scrollable = true, Fill = true }.With(section));
    }
}
