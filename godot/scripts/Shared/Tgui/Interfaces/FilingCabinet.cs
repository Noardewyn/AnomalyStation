using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/FilingCabinet.tsx (gaps S3, D298). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class FilingCabinet
{
    // tg: FilingCabinet.tsx:12
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string cabinet_name = data.Str("cabinet_name");
        IReadOnlyList<UiValue> contents = data.Arr("contents");
        IReadOnlyList<UiValue> contents_ref = data.Arr("contents_ref");

        var rows = new List<UiNode>();
        for (int index = 0; index < contents.Count; index++)
        {
            string itemRef = index < contents_ref.Count ? contents_ref[index].AsStr() : "";
            // tg: FilingCabinet.tsx:19  <Stack color="black" backgroundColor="white" mb={0.5}>
            rows.Add(new UiNode(UiKind.Stack) { Key = itemRef }.With(
                new UiNode(UiKind.StackItem) { Grow = 1, Background = "#ffffff", Align = "center" }.With(new UiNode(UiKind.Box) { Color = "black", TextAlign = "center", Text = contents[index].AsStr() }),
                new UiNode(UiKind.StackItem) { Background = "#ffffff" }.With(
                    new UiNode(UiKind.Button) { Key = "remove:" + itemRef, Icon = "eject", OnClick = () => c.Act("remove_object", ("ref", itemRef)) })));
        }
        if (contents.Count == 0)
        {
            // tg: FilingCabinet.tsx:40
            rows.Add(new UiNode(UiKind.Section).With(new UiNode(UiKind.Box) { Color = "white", TextAlign = "center", Text = $"The {cabinet_name} is empty!" }));
        }

        // tg: FilingCabinet.tsx:17  <Window title={cabinet_name || 'Filing Cabinet'} width={350} height={300}>
        //      <Window.Content backgroundColor="#B88F3D" scrollable>
        return c.Window(350, 300, string.IsNullOrEmpty(cabinet_name) ? "Filing Cabinet" : cabinet_name, PaperworkWindows.Content("#B88F3D", rows.ToArray()));
    }
}
