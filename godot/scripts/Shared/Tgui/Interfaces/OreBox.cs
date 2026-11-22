using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/OreBox.tsx (Lavaland L2a, D304).
// The window reads tg's ui_data of the ore box (satchel_ore_box.dm:84) and sends removeall.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class OreBox
{
    // tg: OreBox.tsx:17
    public static UiNode Render(TguiContext c)
    {
        IReadOnlyList<UiValue> materials = c.Data.Arr("materials");
        var table = new UiNode(UiKind.Table) { Key = "ores" }.With(
            new UiNode(UiKind.TableRow) { Key = "head" }.With(
                new UiNode(UiKind.TableCell) { Bold = true, Text = "Item" },
                new UiNode(UiKind.TableCell) { Bold = true, TextAlign = "right", Text = "Amount" }));
        for (int id = 0; id < materials.Count; id++)
        {
            UiValue material = materials[id];
            table.With(new UiNode(UiKind.TableRow) { Key = "ore:" + id }.With(
                new UiNode(UiKind.TableCell) { Text = TguiFormat.ToTitleCase(material.Str("name")) },
                new UiNode(UiKind.TableCell) { TextAlign = "right", Color = "label", Text = TguiFormat.JsNumber(material.Num("amount")) }));
        }
        // tg: OreBox.tsx:22  <Window width={335} height={415}> <Window.Content scrollable>: the content in the window's own area
        return c.Window(335, 415, null,
            new UiNode(UiKind.Section)
            {
                Key = "ores",
                Title = "Ores & Boulders",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "empty",
                    Text = "Empty",
                    Disabled = materials.Count == 0,
                    OnClick = () => c.Act("removeall"),
                },
            }.With(table),
            new UiNode(UiKind.Section) { Key = "help" }.With(
                new UiNode(UiKind.Box) { Text = "Ores can be loaded here via a mining satchel or by hand. Boulders can also be stored here\nGibtonite is not accepted." }));
    }
}
