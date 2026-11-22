using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Trophycase.jsx (gaps ST2): the trophy display case's
// showpiece (its name, picture and description) and the historian panel of the curator's key (structures/displaycase.dm:431 ui_data,
// :442 ui_static_data). The picture is the showpiece's icon and state, drawn from tg's sprite (DV500).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Trophycase
{
    // tg: Trophycase.jsx:15
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        // tg: Trophycase.jsx:18  <Window width={300} height={380}>
        return c.Window(300, 380, null,
            new UiNode(UiKind.Stack) { Key = "stack", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "name_item" }.With(ShowpieceName(data)),
                new UiNode(UiKind.StackItem) { Key = "image_item" }.With(ShowpieceImage(data)),
                new UiNode(UiKind.StackItem) { Key = "description_item", Grow = 1 }.With(ShowpieceDescription(data)),
                new UiNode(UiKind.Divider),
                new UiNode(UiKind.StackItem) { Key = "historian_item" }.With(HistorianPanel(c, data))));
    }

    // tg: Trophycase.jsx:40
    private static UiNode HistorianPanel(TguiContext c, UiValue data)
    {
        bool hasShowpiece = data.Truthy("has_showpiece");
        bool historianMode = data.Truthy("historian_mode");
        bool holographic = data.Truthy("holographic_showpiece");
        bool described = data.Truthy("showpiece_description");
        var section = new UiNode(UiKind.Section) { Key = "historian", TextAlign = "left" };
        if (!historianMode)
            section.With(new UiNode(UiKind.Button) { Key = "insert_key", Icon = "key", Text = "Insert key for historian mode", OnClick = () => c.Act("insert_key") });
        else
        {
            section.With(new UiNode(UiKind.Box) { Key = "historian_buttons" }.With(
                new UiNode(UiKind.Button) { Key = "lock", Icon = "times", Text = "Lock historian mode", OnClick = () => c.Act("lock") },
                new UiNode(UiKind.Button)
                {
                    Key = "change_message",
                    Icon = "pencil",
                    Text = "Edit description",
                    Disabled = !hasShowpiece || holographic,
                    OnClick = () => c.Act("change_message"),
                }));
            if (holographic)
                section.With(new UiNode(UiKind.Box) { Key = "holographic", Wrap = true, Text = "A holographic trophy is already present. Replace it with a new trophy to create a new recording." });
            if (!hasShowpiece)
                section.With(new UiNode(UiKind.Box) { Key = "no_trophies", Text = "No trophies located." });
            if (hasShowpiece && !holographic && described)
                section.With(new UiNode(UiKind.Box) { Key = "recording", Wrap = true, Text = "Recording has begun. Trophy data will be saved overnight, as long as the trophy stays within an intact case." });
            if (hasShowpiece && !holographic && !described)
                section.With(new UiNode(UiKind.Box) { Key = "new_trophy", Wrap = true, Text = "New trophy detected. Please record a description to begin archival." });
        }
        return section;
    }

    // tg: Trophycase.jsx:101
    private static UiNode ShowpieceDescription(UiValue data)
    {
        bool hasShowpiece = data.Truthy("has_showpiece");
        bool holographic = data.Truthy("holographic_showpiece");
        string description = data.Field("showpiece_description") is { IsNull: false } d ? d.AsStr() : null;
        var section = new UiNode(UiKind.Section) { Key = "description", Fill = true, TextAlign = "center" };
        if (!hasShowpiece)
            section.With(new UiNode(UiKind.Box) { Key = "empty", Bold = true, Wrap = true, Text = "This exhibit is empty. History awaits your contribution!" });
        if (holographic)
            section.With(new UiNode(UiKind.Box) { Key = "hologram_text", Bold = true, Wrap = true, Text = description });
        if (!holographic && hasShowpiece)
        {
            section.With(new UiNode(UiKind.Box)
            {
                Key = "text",
                Wrap = true,
                Text = !string.IsNullOrEmpty(description)
                    ? TguiFormat.DecodeHtmlEntities(description)
                    : "This exhibit is under construction. Get the curator's key to finalize your contribution!",
            });
        }
        return section;
    }

    // tg: Trophycase.jsx:129
    private static UiNode ShowpieceImage(UiValue data)
    {
        UiValue icon = data.Field("showpiece_icon");
        if (icon is { IsNull: false })
        {
            // tg: Trophycase.jsx:134  <Image m={1} src={`data:image/jpeg;base64,${showpiece_icon}`} height="96px" width="96px" />
            return new UiNode(UiKind.Section) { Key = "image", TextAlign = "center" }.With(
                new UiNode(UiKind.Box) { Key = "icon", DmIcon = icon.Str("icon"), DmIconState = icon.Str("icon_state"), Width = "96px", Height = "96px" });
        }
        // tg: Trophycase.jsx:143  <Box height="96px" width="96px"><Dimmer fontSize="32px"><Icon name="landmark" /></Dimmer></Box>
        return new UiNode(UiKind.Section) { Key = "image", TextAlign = "center" }.With(
            new UiNode(UiKind.Box) { Key = "no_icon", Width = "96px", Height = "96px" }.With(
                new UiNode(UiKind.Icon) { Key = "landmark", Icon = "landmark", FontSize = "32px" }));
    }

    // tg: Trophycase.jsx:152
    private static UiNode ShowpieceName(UiValue data)
    {
        string name = data.Field("showpiece_name") is { IsNull: false } n ? n.AsStr() : null;
        return new UiNode(UiKind.Section) { Key = "name", TextAlign = "center" }.With(
            new UiNode(UiKind.Box) { Key = "showpiece_name", Bold = true, Text = !string.IsNullOrEmpty(name) ? TguiFormat.DecodeHtmlEntities(name) : "Under construction." });
    }
}
