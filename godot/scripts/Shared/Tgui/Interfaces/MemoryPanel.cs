using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/MemoryPanel.jsx (gaps package G4, D291). The window reads tg's
// ui_data of the memory panel (modules/mob/mob.dm:1602): each memory's name and quality (its story value); it sends no act. Original
// copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// tg's own quirks kept: data.memories || [] is always an array, and an empty array is truthy in JavaScript, so the "You have no
// memories!" dimmer never shows (an empty panel is the title alone); the "meh" star's background ends in a semicolon, which the browser
// rejects as CSS, so it is a plain button. The tooltips' line breaks and indentation collapse to single spaces, as a browser shows them.
// The title section's maxHeight of 32px holds only its title row here.

public static class MemoryPanel
{
    // tg: MemoryPanel.jsx:6
    private const int STORY_VALUE_KEY = -1;
    private const int STORY_VALUE_SHIT = 0;
    private const int STORY_VALUE_NONE = 1;
    private const int STORY_VALUE_MEH = 2;
    private const int STORY_VALUE_OKAY = 3;
    private const int STORY_VALUE_AMAZING = 4;
    private const int STORY_VALUE_LEGENDARY = 5;

    // tg: MemoryPanel.jsx:14  the quality's button: its icon, look and tooltip
    private static UiNode MemoryQuality(double? quality, string key)
    {
        UiNode Quality(string icon, string color, string background, string tooltip) =>
            new UiNode(UiKind.Button) { Key = key, Icon = icon, Color = color, Background = background, Tooltip = tooltip };

        return quality switch
        {
            STORY_VALUE_KEY => Quality("key", "transparent", null,
                "This is a key memory. It contains important information you may want to double-check in the future."),
            STORY_VALUE_SHIT => Quality("poop", "transparent", null,
                "This memory is not interesting at all! It does not make for good art and is unlikely to pass to future generations."),
            STORY_VALUE_NONE => Quality("star", "transparent", null,
                "This memory pretty bland. It would make for some pretty mediocre art and is not likely to pass to future generations."),
            STORY_VALUE_MEH => Quality("star", null, "linear-gradient(to right, #964B30, #D68B60, #B66B30, #D68B60, #964B30);",
                "This memory is not super interesting. It could turn into an okay story but don't bet on it."),
            STORY_VALUE_OKAY => Quality("star", null, "linear-gradient(to right, #636363, #a3a3a3, #6e6e6e, #a3a3a3, #636363)",
                "This memory is pretty okay! Some good stories could be told from this and it might even come back in future generations."),
            STORY_VALUE_AMAZING => Quality("star", null, "linear-gradient(to right, #AA771C, #BCB68A, #B38728, #BCB68A, #AA771C)",
                "This memory is great! You could tell a great story from it, and it would have a good chanced pass to future generations!"),
            STORY_VALUE_LEGENDARY => Quality("crown", null, "linear-gradient(to right, #56A5B3, #75D4E2, #56A5B3, #75D4E2, #56A5B3)",
                "This memory is the stuff of legends! It would make for legendary art and is likely to pass to future generations."),
            // Default return / error
            _ => Quality("question", null, null,
                "This memory has no valid quality assigned. We have no idea how good or bad it may be. This is a bug, and should be reported!"),
        };
    }

    // tg: MemoryPanel.jsx:132
    public static UiNode Render(TguiContext c)
    {
        IReadOnlyList<UiValue> memories = c.Data.Arr("memories");

        // tg: MemoryPanel.jsx:136  <Window title="Memory Panel" width={400} height={500}>
        var list = new UiNode(UiKind.Stack) { Key = "memories", Vertical = true };
        for (int i = 0; i < memories.Count; i++)
        {
            UiValue memory = memories[i];
            string name = memory.Str("name");
            UiValue quality = memory.Get("quality");
            // tg: MemoryPanel.jsx:157  <Stack.Item key={memory.name}><Section><MemoryQuality quality={memory.quality} /> {memory.name}</Section>
            list.With(new UiNode(UiKind.StackItem) { Key = "memory:" + i }.With(
                new UiNode(UiKind.Section).With(
                    new UiNode(UiKind.Stack) { Align = "center" }.With(
                        MemoryQuality(quality?.Kind == UiValueKind.Number ? quality.Number : null, "quality:" + i),
                        new UiNode(UiKind.Box) { Key = "name:" + i, Text = name }))));
        }

        return c.Window(400, 500, "Memory Panel",
            // tg: MemoryPanel.jsx:138  <Section maxHeight="32px" title="Memories" buttons={<Button icon="info" ... />} />
            new UiNode(UiKind.Section)
            {
                Key = "header",
                Title = "Memories",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "info",
                    Color = "transparent",
                    Icon = "info",
                    Tooltip = "These are your memories. You gain them from doing notable things and you can use them in art!",
                },
            },
            list);
    }
}
