using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NetpodOutfits.tsx (gaps BR1, D339): the net pod's
// outfit picker, the avatar's clothes by collection with a search.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NetpodOutfits
{
    private const string TypeKey = "selectedType", SearchKey = "search";

    // tg: NetpodOutfits.tsx:33
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string netsuit = data.Str("netsuit");
        IReadOnlyList<UiValue> collections = data.Arr("collections");
        string selectedName = c.UseState(TypeKey, collections.Count > 0 ? collections[0].Str("name") : "");
        string search = c.UseState(SearchKey, "");
        UiValue selectedType = collections.FirstOrDefault(collection => collection.Str("name") == selectedName);

        IReadOnlyList<UiValue> outfits = selectedType?.Arr("outfits") ?? Array.Empty<UiValue>();
        // tg: NetpodOutfits.tsx:41  .sort((a, b) => (a.name > b.name ? 1 : 0)): by name, a stable sort
        var filtered = outfits.Where(TguiFormat.CreateSearch<UiValue>(search, outfit => outfit.Str("name")))
            .OrderBy(outfit => outfit.Str("name"), StringComparer.Ordinal).ToList();

        string selected = outfits.FirstOrDefault(outfit => outfit.Str("path") == netsuit)?.Str("name") ?? "None";

        var tabs = new UiNode(UiKind.Tabs) { Key = "types", Vertical = true };
        for (int index = 0; index < collections.Count; index++)
        {
            string name = collections[index].Str("name");
            tabs.With(new UiNode(UiKind.Tab) { Key = "type:" + name, Text = name, Selected = name == selectedName, OnClick = () => c.SetState(TypeKey, name) });
            if (index > 0)
                tabs.With(new UiNode(UiKind.Divider) { Key = "divider:" + index });
        }

        var list = new UiNode(UiKind.Section) { Key = "outfits", Fill = true, Scrollable = true };
        for (int index = 0; index < filtered.Count; index++)
        {
            string path = filtered[index].Str("path"), name = filtered[index].Str("name");
            list.With(new UiNode(UiKind.StackItem) { Key = "outfit:" + index, Candystripe = true }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "select",
                    Selected = netsuit == path,
                    Color = "transparent",
                    Text = name,
                    OnClick = () => c.Act("select_outfit", ("outfit", path)),
                }));
        }

        return c.Window(400, 300, "Net Pod",
            new UiNode(UiKind.Stack) { Key = "view", Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "select", Grow = 1 }.With(
                    new UiNode(UiKind.Section)
                    {
                        Key = "select",
                        Fill = true,
                        Title = "Select an outfit",
                        Buttons = new UiNode(UiKind.Input)
                        {
                            Key = "search",
                            AutoFocus = true,
                            Tooltip = "Search",
                            TextValue = search,
                            OnInput = value => c.SetState(SearchKey, value ?? ""),
                        },
                    }.With(
                        new UiNode(UiKind.Stack) { Key = "columns", Fill = true }.With(
                            new UiNode(UiKind.StackItem) { Key = "types", Grow = 1 }.With(tabs),
                            new UiNode(UiKind.Divider) { Key = "divider" },
                            new UiNode(UiKind.StackItem) { Key = "outfits", Grow = 5 }.With(list)))),
                new UiNode(UiKind.StackItem) { Key = "selected" }.With(
                    new UiNode(UiKind.NoticeBox) { Key = "selected:box", Color = "info", TextAlign = "right", Text = selected })));
    }
}
