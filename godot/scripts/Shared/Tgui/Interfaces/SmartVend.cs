using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SmartVend.tsx and common/LayoutToggle.tsx (gaps package H2,
// D333). The window reads tg's ui_data of a smart fridge, a dehydrator or a drying rack (food_and_drinks/machinery/smartfridge.dm): the
// stored items grouped by type and name with their picture and amount, the window's name, whether it is a dryer and whether it dries; it
// sends tg's acts, Release (an item's path and an amount) and Dry (and LayoutToggle's change_ui_state).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// The items' pictures are their tg icon and state (tgui's DmIcon). tgui reads the layout from the player's preference
// (config.interface.layout, /datum/preference/choiced/tgui_layout/smartfridge, whose default is the grid), which the port sends as null:
// the window starts as the preference's default, the grid, and the toggle switches it to the list.

public static class SmartVend
{
    // tg: common/LayoutToggle.tsx:12
    private const string LayoutGrid = "grid", LayoutList = "list";

    // tg: SmartVend.tsx:33
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string searchText = c.UseState("searchText", "");
        // tg: :36  useState(getLayoutState()): the preference, null in the port, whose default is the grid (preferences/tgui.dm)
        string displayMode = c.UseState("displayMode", LayoutGrid);
        Func<UiValue, bool> search = TguiFormat.CreateSearch<UiValue>(searchText, item => item.Str("name"));
        List<UiValue> all = data.Obj("contents").Select(pair => pair.Value).ToList();
        List<UiValue> contents = searchText.Length > 0 ? all.Where(search).ToList() : all;

        var buttons = new UiNode(UiKind.Stack) { Key = "buttons" };
        if (data.Truthy("isdryer"))
        {
            bool drying = data.Truthy("drying");
            buttons.With(new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
            {
                Key = "dry",
                Icon = drying ? "stop" : "tint",
                Text = drying ? "Stop drying" : "Dry",
                OnClick = () => c.Act("Dry"),
            }));
        }
        else
        {
            buttons.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Input)
                {
                    Key = "search",
                    Tooltip = "Search...",
                    TextValue = searchText,
                    OnInput = value => c.SetState("searchText", value),
                }),
                LayoutToggle(c, displayMode));
        }
        buttons.With(new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
        {
            Key = "help",
            Icon = "question",
            Tooltip = "LMB - Vend selected amount\nRMB - Vend all",
        }));

        var section = new UiNode(UiKind.Section) { Key = "storage", Fill = true, Scrollable = true, Title = "Storage", Buttons = buttons };
        if (contents.Count == 0)
            section.With(new UiNode(UiKind.NoticeBox) { Key = "nothing", Text = "Nothing found." });
        else
        {
            // tgui-core's non-fluid ImageButtons are inline blocks that wrap; the fluid ones stand one per row
            UiNode items = displayMode == LayoutGrid ? new UiNode(UiKind.Stack) { Key = "grid", Wrap = true } : new UiNode(UiKind.Box) { Key = "list" };
            foreach (UiValue item in contents)
                items.With(displayMode == LayoutGrid ? ItemTile(c, item) : ItemList(c, item));
            section.With(items);
        }

        // tg: :43  <Window width={431} height={575}>
        return c.Window(431, 575, null, section);
    }

    // tg: SmartVend.tsx:115
    private static UiNode ItemTile(TguiContext c, UiValue item)
    {
        string path = item.Str("path");
        double amount = item.Num("amount");
        string icon = item.Str("icon");
        return new UiNode(UiKind.ImageButton)
        {
            Key = "tile:" + path,
            DmIcon = icon.Length > 0 ? icon : null,
            DmIconState = icon.Length > 0 ? item.Str("icon_state") : null,
            Disabled = amount < 1,
            Tooltip = item.Str("name"),
            Buttons = amount > 1
                ? new UiNode(UiKind.NumberInput)
                {
                    Key = "amount:" + path,
                    MinValue = 1,
                    MaxValue = amount,
                    Step = 1,
                    Value = 1,
                    OnChange = value => c.Act("Release", ("path", path), ("amount", value)),
                }
                : null,
            ButtonsAlt = new UiNode(UiKind.Stack) { Bold = true, Color = "rgb(185, 185, 185)", FontSize = 0.8 }.With(
                new UiNode(UiKind.StackItem) { Grow = 1 },
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = "x" + TguiFormat.JsNumber(amount) })),
            // tg: :152  onClick releases one, onRightClick all of them
            OnClickWith = click => c.Act("Release", ("path", path), ("amount", click.Right ? amount : 1)),
        }.With(new UiNode(UiKind.Box) { Text = item.Str("name") });
    }

    // tg: SmartVend.tsx:166
    private static UiNode ItemList(TguiContext c, UiValue item)
    {
        string path = item.Str("path");
        double amount = item.Num("amount");
        bool disabled = amount <= 1;
        string icon = item.Str("icon");
        return new UiNode(UiKind.ImageButton)
        {
            Key = "row:" + path,
            Fluid = true,
            Size = 1, // tg: imageSize={32}
            DmIcon = icon.Length > 0 ? icon : null,
            DmIconState = icon.Length > 0 ? item.Str("icon_state") : null,
            Disabled = amount < 1,
            Buttons = new UiNode(UiKind.Stack) { Opacity = disabled ? 0.5 : (double?)null, Background = "rgba(175, 175, 175, 0.1)" }.With(
                new UiNode(UiKind.NumberInput)
                {
                    Key = "amount:" + path,
                    Width = "40px",
                    MinValue = 1,
                    MaxValue = amount,
                    Step = 1,
                    Value = 1,
                    Disabled = disabled, // tg: pointerEvents none when disabled
                    OnChange = value => c.Act("Release", ("path", path), ("amount", value)),
                }),
            // tg: :203  onClick releases one, onRightClick all of them
            OnClickWith = click => c.Act("Release", ("path", path), ("amount", click.Right ? amount : 1)),
        }.With(new UiNode(UiKind.Stack) { TextAlign = "left" }.With(
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box) { Text = item.Str("name") }),
            new UiNode(UiKind.StackItem) { Opacity = 0.5, FontSize = 0.8 }.With(new UiNode(UiKind.Box) { Text = "x" + TguiFormat.JsNumber(amount) })));
    }

    /**
     * Allows the user to toggle between grid and list layouts, if preference on Default value.
     * Otherwise it'll be controlled by preferences.
     */
    // tg: common/LayoutToggle.tsx:27
    private static UiNode LayoutToggle(TguiContext c, string state)
    {
        return new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
        {
            Key = "layout",
            Icon = state == LayoutGrid ? "list" : "border-all",
            Tooltip = state == LayoutGrid ? "View as List" : "View as Grid",
            OnClick = () =>
            {
                string newState = state == LayoutGrid ? LayoutList : LayoutGrid;
                c.SetState("displayMode", newState);
                c.Act("change_ui_state", ("new_state", newState));
            },
        });
    }
}
