using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/BorgShaker.tsx (silicons SI2, D326: the service cyborg's
// shaker). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class BorgShaker
{
    // tg: BorgShaker.tsx:23  enum ContainerPreference
    private const string BeverageApparatus = "beverage_apparatus";
    private const string InternalBeaker = "internal_beaker";

    // tg: BorgShaker.tsx:28
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double minVolume = data.Num("minVolume");
        IReadOnlyList<UiValue> sodas = data.Arr("sodas");
        IReadOnlyList<UiValue> alcohols = data.Arr("alcohols");
        string selected = data.Str("selectedReagent");
        string container = data.Str("reagentSearchContainer");
        c.WindowTheme = data.Str("theme", null);

        double dynamicHeight = System.Math.Ceiling(sodas.Count / 4.0) * 23 + System.Math.Ceiling(alcohols.Count / 4.0) * 23 + 140;

        var buttons = new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.Button)
            {
                Key = "lookup",
                Icon = "book",
                Text = "Reaction search",
                Disabled = container != InternalBeaker && !data.Truthy("apparatusHasItem"),
                Tooltip = "Look up recipes and reagents! Choose a container source",
                OnClick = () => c.Act("reaction_lookup"),
            },
            new UiNode(UiKind.Button)
            {
                Key = "beaker",
                Icon = "flask",
                Width = "23px",
                Color = container == InternalBeaker ? "green" : "default",
                Tooltip = "Search source: Internal Beaker",
                OnClick = () => c.Act("set_preferred_container", ("value", InternalBeaker)),
            },
            new UiNode(UiKind.Button)
            {
                Key = "apparatus",
                Icon = "vial",
                Width = "24px",
                Tooltip = "Search source: Beverage Apparatus",
                Color = container == BeverageApparatus ? "green" : "default",
                OnClick = () => c.Act("set_preferred_container", ("value", BeverageApparatus)),
            });

        return c.Window(650, dynamicHeight, null,
            new UiNode(UiKind.Section) { Key = "sodas", Title = "Non-Alcoholic", Buttons = buttons }.With(ReagentDisplay(c, sodas, selected, minVolume)),
            new UiNode(UiKind.Section) { Key = "alcohols", Title = "Alcoholic" }.With(ReagentDisplay(c, alcohols, selected, minVolume)));
    }

    // tg: BorgShaker.tsx:111
    private static UiNode ReagentDisplay(TguiContext c, IReadOnlyList<UiValue> reagents, string selected, double minimum)
    {
        if (reagents.Count == 0)
            return new UiNode(UiKind.NoticeBox) { Key = "none", Text = "No reagents available!" };
        return new UiNode(UiKind.Fragment).With(reagents.Select(reagent =>
        {
            string name = reagent.Str("name");
            return new UiNode(UiKind.Button)
            {
                Key = name,
                Icon = "tint",
                Width = "150px",
                Text = name,
                Color = name == selected ? "green" : "default",
                Disabled = reagent.Num("volume") < minimum,
                OnClick = () => c.Act(name),
            };
        }).ToArray());
    }
}
