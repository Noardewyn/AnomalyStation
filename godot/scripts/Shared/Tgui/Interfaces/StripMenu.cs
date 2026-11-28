using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/StripMenu.tsx (M8z slice B, D236). The window reads tg's
// ui_data of the strip menu (strippable.dm:349-398). Leaves: an item's picture is its own icon file and state (DV150: tg sends a
// base64 JPEG of icon(item.icon, item.icon_state)), the slot placeholders are tg's icons/ui/inventory PNGs (resolveAsset), CornerText's
// text shadow is not drawn, and Button's tooltip is the kit's.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class StripMenu
{
    // tg: StripMenu.tsx:10
    private const int Rows = 5;
    private const int Columns = 6;

    // tg: StripMenu.tsx:13
    private const string ButtonDimensions = "64px";

    // the window host's chrome beyond tgui's: a title bar a pixel taller than tgui's 31, a 16 px row for the resize grip, and a few pixels of
    // border on each side (the grid is exactly as wide as the content area of tgui's 426)
    private const double HostChrome = 1 + 16 + 8;
    private const double HostBorder = 4;

    // tg: StripMenu.tsx:15  getGridSpotKey
    private static string GridSpotKey(int row, int column) => row + "/" + column;

    // tg: StripMenu.tsx:55  ALTERNATE_ACTIONS
    private static readonly Dictionary<string, (string Icon, string Text)> AlternateActions = new(StringComparer.Ordinal)
    {
        ["knot"] = ("shoe-prints", "Knot"),
        ["untie"] = ("shoe-prints", "Untie"),
        ["unknot"] = ("shoe-prints", "Unknot"),
        ["remove_item_cuffs"] = ("handcuffs", "Remove Handcuffs"),
        ["enable_internals"] = ("tg-air-tank", "Enable internals"),
        ["disable_internals"] = ("tg-air-tank-slash", "Disable internals"),
        ["adjust_jumpsuit"] = ("tshirt", "Adjust jumpsuit"),
        ["adjust_sensor"] = ("microchip", "Adjust sensors"),
        ["strip_accessory"] = ("ribbon", "Strip accessory"),
        ["deploy"] = ("plus", "Deploy MOD"),
        ["undeploy"] = ("minus", "Undeploy MOD"),
        ["activate_mod"] = ("power-off", "Activate MOD"),
        ["deactivate_mod"] = ("power-off", "Deactivate MOD"),
        ["access_storage"] = ("box-open", "Open"),
    };

    // tg: StripMenu.tsx:111  SLOTS
    private sealed record Slot(string DisplayName, int Row, int Column, string Image, string Corner = null, string CornerAlign = null);

    private static readonly Dictionary<string, Slot> Slots = new(StringComparer.Ordinal)
    {
        ["eyes"] = new("eyewear", 0, 1, "inventory-glasses.png"),
        ["head"] = new("headwear", 0, 2, "inventory-head.png"),
        ["neck"] = new("neckwear", 1, 1, "inventory-neck.png"),
        ["mask"] = new("mask", 1, 2, "inventory-mask.png"),
        ["pet_collar"] = new("collar", 1, 2, "inventory-collar.png"),
        ["ears"] = new("earwear", 1, 3, "inventory-ears.png"),
        ["parrot_headset"] = new("headset", 1, 3, "inventory-ears.png"),
        ["handcuffs"] = new("handcuffs", 1, 4, null),
        ["legcuffs"] = new("legcuffs", 1, 5, null),
        ["jumpsuit"] = new("uniform", 2, 1, "inventory-uniform.png"),
        ["suit"] = new("suit", 2, 2, "inventory-suit.png"),
        ["gloves"] = new("gloves", 2, 3, "inventory-gloves.png"),
        ["right_hand"] = new("right hand", 2, 4, "inventory-hand_r.png", "R", "left"),
        ["left_hand"] = new("left hand", 2, 5, "inventory-hand_l.png", "L", "right"),
        ["shoes"] = new("shoes", 3, 2, "inventory-shoes.png"),
        ["suit_storage"] = new("suit storage item", 4, 0, "inventory-suit_storage.png"),
        ["id"] = new("ID", 4, 1, "inventory-id.png"),
        ["belt"] = new("belt", 4, 2, "inventory-belt.png"),
        ["back"] = new("backpack", 4, 3, "inventory-back.png"),
        ["left_pocket"] = new("left pocket", 4, 4, "inventory-pocket.png"),
        ["right_pocket"] = new("right pocket", 4, 5, "inventory-pocket.png"),
    };

    /// <summary>Slice 4D-1 (the Tab inventory's strip view): tg's label of an alternate action (<c>ALTERNATE_ACTIONS</c>), null for one tg's window does not draw.</summary>
    public static string AlternateLabel(string action) => action != null && AlternateActions.TryGetValue(action, out (string Icon, string Text) a) ? a.Text : null;

    /// <summary>Slice 4D-1: tg's display name of a strip slot (<c>SLOTS</c>: "left pocket"), null for a key tg's window does not place.</summary>
    public static string DisplayName(string key) => key != null && Slots.TryGetValue(key, out Slot slot) ? slot.DisplayName : null;

    // tg: StripMenu.tsx:222  enum ObscuringLevel
    private const int Completely = 1;
    private const int Hidden = 2;
    private const int Inaccessible = 3;

    // tg's resolveAsset("inventory-glasses.png") is the asset the inventory datum sends (code/modules/asset_cache/assets/inventory.dm:3)
    private static string AssetFile(string asset) => "icons/ui/inventory/" + asset["inventory-".Length..];

    // tg: StripMenu.tsx:17  CornerText: a box whose text sits two pixels in from its side
    private static UiNode CornerText(string align, string text) => new UiNode(UiKind.Box)
    {
        Key = "corner",
        Text = text,
        TextAlign = align,
        Anchor = align == "left" ? "top-left" : "top-right",
    };

    // tg: StripMenu.tsx:252
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;

        // tg: StripMenu.tsx:255  the grid spots, a later key of a spot replacing an earlier one's
        var gridSpots = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, _) in data.Obj("items"))
        {
            if (Slots.TryGetValue(key, out Slot spot))
                gridSpots[GridSpotKey(spot.Row, spot.Column)] = key;
        }

        // tg: StripMenu.tsx:262  <Window title={`Stripping ${data.name}`} width={426} height={387}>; (64 + 6) * 6 + 6 = 426. The host's window
        // chrome is a pixel taller in the title bar, has a 16 px row for the resize grip and a border, which tgui has not: the window
        // grows by them so that the grid fits without scrolling.
        return c.Window(426 + HostBorder, 387 + HostChrome, "Stripping " + data.Str("name"),
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                Enumerable.Range(0, Rows).Select(row => new UiNode(UiKind.StackItem) { Key = "row" + row }.With(
                    new UiNode(UiKind.Stack) { Fill = true }.With(
                        Enumerable.Range(0, Columns).Select(column => GridCell(c, data, gridSpots, row, column)))))));
    }

    // tg: StripMenu.tsx:268
    private static UiNode GridCell(TguiContext c, UiValue data, Dictionary<string, string> gridSpots, int row, int column)
    {
        string key = GridSpotKey(row, column);
        if (!gridSpots.TryGetValue(key, out string keyAtSpot))
            return new UiNode(UiKind.StackItem) { Key = key, Width = ButtonDimensions, Height = ButtonDimensions };

        UiValue item = data.Field("items").Field(keyAtSpot);
        Slot slot = Slots[keyAtSpot];
        bool isNull = item == null || item.Kind == UiValueKind.Null;
        bool hasName = !isNull && item.Get("name") != null; // 'name' in item
        int obscured = (int)(item?.Get("obscured") is UiValue o ? o.AsNum(0) : 0);

        UiNode content = null;
        var alternateActions = new List<UiNode>();
        string tooltip = null;

        if (isNull)
            tooltip = slot.DisplayName;
        else if (hasName)
        {
            // tg: StripMenu.tsx:288  <Image src={`data:image/jpeg;base64,${item.icon}`} width="64px" height="64px" />
            content = new UiNode(UiKind.Box) { Key = "item", DmIcon = item.Str("icon"), DmIconState = item.Str("icon_state"), Size = 2 };
            tooltip = item.Str("name");
            IReadOnlyList<UiValue> alternate = item.Arr("alternate");
            for (int idx = 0; idx < alternate.Count; idx++)
            {
                string alternateKey = alternate[idx].AsStr();
                if (!AlternateActions.TryGetValue(alternateKey, out (string Icon, string Text) alternateAction))
                    continue;
                bool inaccessible = obscured == Inaccessible;
                // tg: StripMenu.tsx:320  alternateActionStyle: left 0 for the first, right 0 for the second, the bottom for all; 20 x 20
                alternateActions.Add(new UiNode(UiKind.Button)
                {
                    Key = "alt:" + keyAtSpot + ":" + alternateKey,
                    Icon = alternateAction.Icon,
                    Tooltip = alternateAction.Text,
                    Disabled = inaccessible,
                    Opacity = inaccessible ? 0.7 : 1,
                    Background = "rgba(0, 0, 0, 0.6)",
                    Width = "20px",
                    Height = "20px",
                    Anchor = idx == 0 ? "bottom-left" : idx == 1 ? "bottom-right" : "top-left",
                    OnClick = () => c.Act("alt", ("key", keyAtSpot), ("alternate_action", alternateKey)),
                });
            }
        }
        else if (!isNull && item.Get("obscured") != null && (obscured == Hidden || obscured == Completely))
        {
            // tg: StripMenu.tsx:351  <Icon name={Completely ? 'ban' : 'eye-slash'} size={3} />
            content = new UiNode(UiKind.Icon) { Key = "obscured", Icon = obscured == Completely ? "ban" : "eye-slash", Size = 3 };
            tooltip = "obscured " + slot.DisplayName;
        }

        // tg: StripMenu.tsx:385  the slot's Button: its placeholder picture (when it holds no item), the content, the corner letter
        var button = new UiNode(UiKind.Button)
        {
            Key = "slot:" + keyAtSpot,
            Fluid = true,
            Tooltip = tooltip,
            Width = "100%",
            Height = "100%",
            Background = !isNull && item.Truthy("interacting") ? "#845d15" : null, // hsl(39, 73%, 30%)
            OnClick = () => c.Act("use", ("key", keyAtSpot)),
        };
        if (slot.Image != null && !(!isNull && hasName))
            button.With(new UiNode(UiKind.Box) { Key = "placeholder", DmIcon = AssetFile(slot.Image), Size = 2, Opacity = 0.7 });
        button.With(content);
        if (slot.Corner != null)
            button.With(CornerText(slot.CornerAlign, slot.Corner));

        // tg: StripMenu.tsx:373  <Stack.Item style={{ width: BUTTON_DIMENSIONS, height: BUTTON_DIMENSIONS }}><Box position relative>
        return new UiNode(UiKind.StackItem) { Key = key, Width = ButtonDimensions, Height = ButtonDimensions }.With(
            new UiNode(UiKind.Overlay) { Width = ButtonDimensions, Height = ButtonDimensions }.With(button).With(alternateActions));
    }
}
