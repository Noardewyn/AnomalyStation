using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/DestructiveAnalyzer.tsx (gaps RD3, D333: the destructive
// analyzer). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// The loaded item's picture is tg's sprite drawn from its icon and state (DV496: the server sends them instead of tg's base64 PNG).

public static class DestructiveAnalyzer
{
    // tg: DestructiveAnalyzer.tsx:23
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool serverConnected = data.Truthy("server_connected");
        bool indestructible = data.Truthy("indestructible");
        string loadedItem = data.Field("loaded_item") is { IsNull: false } item ? item.AsStr() : null;
        UiValue itemIcon = data.Field("item_icon");
        bool alreadyDeconstructed = data.Truthy("already_deconstructed");
        string recoverablePoints = data.Str("recoverable_points");
        IReadOnlyList<UiValue> nodeData = data.Arr("node_data");

        // tg: DestructiveAnalyzer.tsx:34
        if (!serverConnected)
        {
            return c.Window(400, 260, "Destructive Analyzer",
                new UiNode(UiKind.NoticeBox) { Key = "server", TextAlign = "center", Color = "danger", Text = "Not connected to a server. Please sync one using a multitool." });
        }
        // tg: DestructiveAnalyzer.tsx:45
        if (string.IsNullOrEmpty(loadedItem))
        {
            return c.Window(400, 260, "Destructive Analyzer",
                new UiNode(UiKind.NoticeBox) { Key = "empty", TextAlign = "center", Color = "danger", Text = "No item loaded!\nPut any item inside to see what it's capable of!" });
        }

        // tg: DestructiveAnalyzer.tsx:77
        var methods = new UiNode(UiKind.Section) { Key = "methods", Title = "Deconstruction Methods" };
        if (!indestructible)
            methods.With(new UiNode(UiKind.NoticeBox) { Key = "indestructible", TextAlign = "center", Color = "danger", Text = "This item can't be deconstructed!" });
        if (indestructible)
        {
            if (!string.IsNullOrEmpty(recoverablePoints))
            {
                methods.With(
                    new UiNode(UiKind.Box) { Key = "points-title", FontSize = "14px", Text = "Research points from deconstruction" },
                    new UiNode(UiKind.Box) { Key = "points", Text = recoverablePoints });
            }
            methods.With(c.ButtonConfirm("deconstruct", new UiNode(UiKind.Button)
            {
                Icon = "hammer",
                Tooltip = alreadyDeconstructed
                    ? "This item item has already been deconstructed, and will not give any additional information."
                    : "Destroys the object currently residing in the machine.",
                Text = "Deconstruct",
                OnClick = () => c.Act("deconstruct"),
            }));
        }
        // tg: DestructiveAnalyzer.tsx:106
        foreach (UiValue node in nodeData)
        {
            bool nodeHidden = node.Truthy("node_hidden");
            string nodePath = node.Str("node_path");
            methods.With(new UiNode(UiKind.Box) { Key = "node-row:" + nodePath }.With(c.ButtonConfirm("node:" + nodePath, new UiNode(UiKind.Button)
            {
                Icon = "cash-register",
                Disabled = !nodeHidden,
                Tooltip = nodeHidden ? "Deconstruct this to research the selected node." : "This node has already been researched.",
                Text = node.Str("node_name"),
                OnClick = () => c.Act("deconstruct", ("deconstruct_path", nodePath)),
            })));
        }

        // tg: DestructiveAnalyzer.tsx:58  <Window width={400} height={260} title="Destructive Analyzer"> <Window.Content scrollable>
        return c.Window(400, 260, "Destructive Analyzer",
            new UiNode(UiKind.Section)
            {
                Key = "item",
                Title = loadedItem,
                Buttons = new UiNode(UiKind.Button) { Key = "eject", Icon = "eject", Tooltip = "Ejects the item currently inside the machine.", OnClick = () => c.Act("eject_item") },
            }.With(
                // tg: DestructiveAnalyzer.tsx:70  <Image src={`data:image/jpeg;base64,${item_icon}`} height="64px" width="64px" />
                itemIcon is { IsNull: false }
                    ? new UiNode(UiKind.Box) { Key = "icon", DmIcon = itemIcon.Str("icon"), DmIconState = itemIcon.Str("icon_state"), Width = "64px", Height = "64px" }
                    : null),
            methods);
    }
}
