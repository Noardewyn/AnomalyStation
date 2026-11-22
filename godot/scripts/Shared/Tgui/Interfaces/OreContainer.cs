using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/OreContainer.tsx (gaps ST5): an ore container's stock
// (structures/ore_containers.dm:25 ui_data), filtered by a search on the ore's name; each ore is drawn from its own icon at 64 pixels,
// named by the first word of its title-cased name, with its amount and a Withdraw button that drops the stack at the user's feet.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// Not drawn: the spinner tg shows while an icon loads (the client's icons are local).

public static class OreContainer
{
    // tg: OreContainer.tsx:28
    public static UiNode Render(TguiContext c)
    {
        IReadOnlyList<UiValue> ores = c.Data.Arr("ores");
        string searchItem = c.UseState("searchItem", "");
        var search = TguiFormat.CreateSearch<UiValue>(searchItem, ore => ore.Str("name"));
        List<UiValue> oresFiltered = searchItem.Length > 0 ? ores.Where(search).ToList() : ores.ToList();

        var stock = new UiNode(UiKind.Stack) { Wrap = true };
        foreach (UiValue ore in oresFiltered)
        {
            string id = ore.Str("id");
            stock.With(new UiNode(UiKind.StackItem) { Key = "ore:" + id }.With(
                new UiNode(UiKind.Stack) { Vertical = true, TextAlign = "center" }.With(
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box)
                    {
                        Key = "icon:" + id,
                        DmIcon = ore.Str("icon"),
                        DmIconState = ore.Str("icon_state"),
                        Width = "64px",
                        Height = "64px",
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "name:" + id, Text = OreName(TguiFormat.ToTitleCase(ore.Str("name"))) }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "amount:" + id, Text = "Amount: " + TguiFormat.JsNumber(ore.Num("amount")) }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "withdraw:" + id,
                        Text = "Withdraw",
                        Color = "transparent",
                        OnClick = () => c.Act("withdraw", ("reference", id)),
                    }))));
        }

        // tg: OreContainer.tsx:36  <Window title="Ore Container" width={550} height={400}>
        return c.Window(550, 400, "Ore Container",
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Section).With(
                    new UiNode(UiKind.Input)
                    {
                        Key = "search",
                        AutoFocus = true,
                        Fluid = true,
                        Tooltip = "Search Ore...",
                        TextValue = searchItem,
                        OnInput = v => c.SetState("searchItem", v),
                    })),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Section)
                {
                    Title = "Stock",
                    Fill = true,
                    Scrollable = true,
                }.With(stock))));
    }

    // tg: OreContainer.tsx:96  Orename: the first word of the ore's name
    private static string OreName(string oreName)
    {
        string[] returnName = oreName.Split(' ');
        return returnName.Length == 0 ? null : returnName[0];
    }
}
