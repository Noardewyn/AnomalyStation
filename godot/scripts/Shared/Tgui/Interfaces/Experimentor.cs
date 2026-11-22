using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Experimentor.tsx (gaps RD4, D333: the experimentor).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// The loaded item, its eject button and the techweb nodes it can reveal; a row of experiment buttons (Poke, Irradiate, Gas, Heat,
// Freeze, Obliterate) and the relics' Discover! button. The item's picture is tg's sprite drawn from its icon and state (DV496: the
// server sends them instead of tg's base64 PNG).

public static class Experimentor
{
    // tg: Experimentor.tsx:45
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool hasItem = data.Truthy("hasItem");
        bool isOnCooldown = data.Truthy("isOnCooldown");
        bool isServerConnected = data.Truthy("isServerConnected");
        UiValue loadedItem = data.Field("loadedItem");
        IReadOnlyList<UiValue> availableExperiments = data.Arr("availableExperiments");

        UiNode content;
        if (!isServerConnected)
            content = new UiNode(UiKind.NoticeBox) { Key = "server", Color = "danger", TextAlign = "center", Text = "Not connected to a server. Please sync one using a multitool." };
        else if (!hasItem || loadedItem is not { IsNull: false })
            content = new UiNode(UiKind.NoticeBox) { Key = "empty", Color = "danger", TextAlign = "center", Text = "No item present in experimentation chamber. Please insert one." };
        else
            content = ExperimentScreen(c, loadedItem, availableExperiments, isOnCooldown);

        // tg: Experimentor.tsx:56  <Window width={450} height={350} title="E.X.P.E.R.I-MENTOR">
        return c.Window(450, 350, "E.X.P.E.R.I-MENTOR", content);
    }

    // tg: Experimentor.tsx:94
    private static UiNode ExperimentScreen(TguiContext c, UiValue item, IReadOnlyList<UiValue> experiments, bool isOnCooldown)
    {
        string name = item.Str("name");
        UiValue icon = item.Field("icon");
        bool isRelic = item.Truthy("isRelic");
        IReadOnlyList<UiValue> associatedNodes = item.Arr("associatedNodes");

        var regularExperiments = experiments.Where(exp => !exp.Truthy("isDiscover")).ToList();
        UiValue discoverExperiment = experiments.FirstOrDefault(exp => exp.Truthy("isDiscover"));

        return new UiNode(UiKind.Stack) { Key = "screen", Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "top", Grow = 1 }.With(
                new UiNode(UiKind.Stack) { Key = "top-row", Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "preview", Grow = 1 }.With(ItemPreview(c, name, icon)),
                    new UiNode(UiKind.StackItem) { Key = "nodes", Grow = 1 }.With(NodePreview(associatedNodes)))),
            new UiNode(UiKind.StackItem) { Key = "buttons" }.With(
                ExperimentButtons(c, isRelic, isOnCooldown, regularExperiments, discoverExperiment)));
    }

    // tg: Experimentor.tsx:130
    private static UiNode ItemPreview(TguiContext c, string name, UiValue icon)
    {
        return new UiNode(UiKind.Stack) { Key = "item", Fill = true, Vertical = true, Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "title-row", Align = "stretch" }.With(
                new UiNode(UiKind.Stack) { Key = "title", Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "eject-item" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "eject",
                        Fluid = true,
                        Color = "bad",
                        Icon = "eject",
                        Height = "100%",
                        FontSize = 1.5,
                        Tooltip = "Eject",
                        TextAlign = "center",
                        OnClick = () => c.Act("eject"),
                    }),
                    new UiNode(UiKind.StackItem) { Key = "name", Grow = 1 }.With(
                        new UiNode(UiKind.Section) { Key = "name-section", Fill = true, Bold = true, TextAlign = "center", Text = TguiFormat.ToTitleCase(name) }))),
            // tg: Experimentor.tsx:158  <Image width="128px" height="128px" src={`data:image/jpeg;base64,${icon}`} />
            new UiNode(UiKind.StackItem) { Key = "picture", Grow = 1 }.With(
                icon is { IsNull: false }
                    ? new UiNode(UiKind.Box) { Key = "icon", DmIcon = icon.Str("icon"), DmIconState = icon.Str("icon_state"), Width = "128px", Height = "128px" }
                    : null));
    }

    // tg: Experimentor.tsx:174
    private static UiNode NodePreview(IReadOnlyList<UiValue> nodes)
    {
        var section = new UiNode(UiKind.Section) { Key = "affected", Fill = true, Title = "Affected Nodes" };
        if (nodes.Count > 0)
        {
            int index = 0;
            section.With(new UiNode(UiKind.LabeledList) { Key = "node-list" }.With(nodes.Select(node =>
            {
                bool isUnlocked = node.Truthy("isUnlocked");
                return new UiNode(UiKind.LabeledItem)
                {
                    Key = "node:" + index++,
                    Label = node.Str("name"),
                    Color = isUnlocked ? "good" : "bad",
                    Text = isUnlocked ? "Unlocked" : "Locked",
                };
            }).ToArray()));
        }
        else
        {
            // tg: Experimentor.tsx:193  <Icon fontSize={4} name="circle-question" className="FabricatorRecipe__Title--disabled" />
            section.With(new UiNode(UiKind.Stack) { Key = "unknown", Fill = true, Vertical = true, Align = "center", Justify = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "unknown-item" }.With(
                    new UiNode(UiKind.Icon) { Key = "question", Icon = "circle-question", FontSize = 4, Color = "label" })));
        }
        return section;
    }

    // tg: Experimentor.tsx:213
    private static UiNode ExperimentButtons(TguiContext c, bool isRelic, bool disabled, IReadOnlyList<UiValue> experiments, UiValue discoverExperiment)
    {
        var row = new UiNode(UiKind.Stack) { Key = "experiment-row", Fill = true };
        foreach (UiValue exp in experiments)
        {
            string id = exp.Str("id");
            row.With(new UiNode(UiKind.StackItem) { Key = "exp-item:" + id }.With(new UiNode(UiKind.Button)
            {
                Key = "exp:" + id,
                Width = 3,
                Height = 3,
                FontSize = 1.6,
                TextAlign = "center",
                Disabled = disabled || !exp.Truthy("isAvailable"),
                Tooltip = exp.Str("name"),
                Icon = exp.Str("fa_icon"),
                OnClick = () => c.Act("experiment", ("id", id)),
            }));
        }
        var discoverItem = new UiNode(UiKind.StackItem) { Key = "discover", Grow = 1 };
        if (discoverExperiment is { IsNull: false })
        {
            string discoverId = discoverExperiment.Str("id");
            discoverItem.With(new UiNode(UiKind.Button)
            {
                Bold = true,
                Fluid = true,
                Height = 3,
                FontSize = 1.6,
                TextAlign = "center",
                Icon = discoverExperiment.Str("fa_icon"),
                Disabled = !isRelic || disabled || !discoverExperiment.Truthy("isAvailable"),
                OnClick = () => c.Act("experiment", ("id", discoverId)),
                Text = "Discover!",
            });
        }
        row.With(discoverItem);
        return new UiNode(UiKind.Section) { Key = "experiments", Fill = true }.With(row);
    }
}
