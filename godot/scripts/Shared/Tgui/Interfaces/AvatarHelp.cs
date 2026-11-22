using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AvatarHelp.tsx (gaps BR1, D339): the avatar's domain
// information, the domain's help text and the six rules of bitrunning. A box's icon is drawn at the right of its title row (the kit's
// Section title is text).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AvatarHelp
{
    private const string DefaultHelp = "No information available! Ask for assistance if needed.";

    private static readonly (string Color, string Text, string Icon, string Title)[] BoxHelpEntries =
    {
        ("purple", "Study the area and do what needs to be done to recover the crate. Pay close attention to domain information and context clues.", "search-location", "Search"),
        ("green", "Bring the crate to the designated sending location in the safehouse. The area may seem out of place. Examine the safehouse to find it.", "boxes", "Recover"),
        ("blue", "The ladder represents the safest way to disconnect before the cache is recovered. Should your connection sever, the netpod offers limited resuscitation potential.", "plug", "Disconnect"),
        ("yellow", "While connected, you are somewhat safe from environmental hazards and intrusions, but not completely. Pay close attention to alerts.", "id-badge", "Security"),
        ("gold", "Generating avatars costs tremendous bandwidth. Do not waste them.", "coins", "Limited Attempts"),
        ("red", "Remember that you are physically linked to this presence. You are a foreign body in a hostile environment. It will attempt to forcefully eject you.", "skull-crossbones", "Realized Danger"),
    };

    // tg: AvatarHelp.tsx:52
    public static UiNode Render(TguiContext c)
    {
        UiValue field = c.Data.Field("help_text");
        string helpText = field == null ? DefaultHelp : field.AsStr();

        UiNode Row(int first) => new UiNode(UiKind.StackItem) { Key = "row" + first, Grow = 1 }.With(
            new UiNode(UiKind.Stack) { Key = "row", Fill = true }.With(BoxHelp(first), BoxHelp(first + 1)));

        return c.Window(600, 600, "Domain Information",
            new UiNode(UiKind.Stack) { Key = "view", Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "help", Grow = 1 }.With(
                    new UiNode(UiKind.Section) { Key = "help", Color = "good", Fill = true, Scrollable = true, Title = "Welcome to the Virtual Domain." }.With(
                        new UiNode(UiKind.Box) { Key = "text", Wrap = true, Text = helpText })),
                new UiNode(UiKind.StackItem) { Key = "boxes", Grow = 4 }.With(
                    new UiNode(UiKind.Stack) { Key = "boxes", Fill = true, Vertical = true }.With(Row(0), Row(2), Row(4)))));
    }

    // tg: AvatarHelp.tsx:101
    private static UiNode BoxHelp(int index)
    {
        (string color, string text, string icon, string title) = BoxHelpEntries[index];
        return new UiNode(UiKind.StackItem) { Key = "box" + index, Grow = 1 }.With(
            new UiNode(UiKind.Section)
            {
                Key = "box",
                Color = "label",
                Fill = true,
                Title = title,
                Buttons = new UiNode(UiKind.Icon) { Key = "icon", Color = color, Icon = icon },
            }.With(new UiNode(UiKind.Box) { Key = "text", Wrap = true, Text = text }));
    }
}
