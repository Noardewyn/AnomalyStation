using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PaintingMachine.jsx (the PDA & ID painter). The two
// dropdowns' selections are the window's own state (tg's useSharedState). The eject buttons fill their row (the kit has no ellipsis), the warning's tooltip takes the kit's default place. Original copyright and license: tgstation, AGPL-3.0
// (code). See ../tgstation/LICENSE.

public static class PaintingMachine
{
    // tg: PaintingMachine.jsx:6
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        var pdaTypes = data.Obj("pdaTypes");
        var cardTrims = data.Obj("cardTrims");
        bool hasPDA = data.Truthy("hasPDA");
        bool hasID = data.Truthy("hasID");
        string pdaName = data.Str("pdaName");
        string idName = data.Str("idName");

        // tg: PaintingMachine.jsx:11  useSharedState('pdaSelection', pdaTypes[Object.keys(pdaTypes)[0]])
        string selectedPDA = c.UseState("pdaSelection", pdaTypes.Count > 0 ? pdaTypes[0].Value.AsStr() : null);
        // tg: PaintingMachine.jsx:16
        string selectedTrim = c.UseState("trimSelection", cardTrims.Count > 0 ? cardTrims[0].Value.AsStr() : null);

        // tg: PaintingMachine.jsx:21  <Window width={500} height={620}>
        return c.Window(500, 620, null,
            new UiNode(UiKind.Section) { Key = "content", Fill = true, Scrollable = true }.With(
                // tg: PaintingMachine.jsx:23
                new UiNode(UiKind.Section)
                {
                    Key = "pda",
                    Title = "PDA Painter",
                    Buttons = new UiNode(UiKind.Fragment).With(
                        c.ButtonConfirm("trim_pda", new UiNode(UiKind.Button)
                        {
                            Text = "Paint PDA",
                            Disabled = !hasPDA,
                            OnClick = () => c.Act("trim_pda", ("selection", selectedPDA)),
                        }),
                        c.ButtonConfirm("reset_pda", new UiNode(UiKind.Button)
                        {
                            Text = "Reset Imprint",
                            Disabled = !hasPDA,
                            OnClick = () => c.Act("reset_pda"),
                        })),
                }.With(
                    new UiNode(UiKind.Stack) { Vertical = true }.With(
                        new UiNode(UiKind.StackItem).With(EjectButton("eject_pda", pdaName, () => c.Act("eject_pda"))),
                        new UiNode(UiKind.StackItem).With(PainterDropdown(c, "pdaSelection", pdaTypes)))),
                // tg: PaintingMachine.jsx:59
                new UiNode(UiKind.Section)
                {
                    Key = "id",
                    Title = "ID Trim Imprinter",
                    Buttons = new UiNode(UiKind.Fragment).With(
                        c.ButtonConfirm("reset_card", new UiNode(UiKind.Button)
                        {
                            Text = "Reset ID Account",
                            Disabled = !hasID,
                            OnClick = () => c.Act("reset_card"),
                        }),
                        c.ButtonConfirm("trim_card", new UiNode(UiKind.Button)
                        {
                            Text = "Imprint ID Trim",
                            Disabled = !hasID,
                            OnClick = () => c.Act("trim_card", ("selection", selectedTrim)),
                        }),
                        new UiNode(UiKind.Button)
                        {
                            Key = "warning",
                            Icon = "question-circle",
                            Tooltip = "WARNING: This is destructive and will wipe ALL access on the card.",
                        }),
                }.With(
                    new UiNode(UiKind.Stack) { Vertical = true }.With(
                        new UiNode(UiKind.StackItem).With(EjectButton("eject_card", idName, () => c.Act("eject_card"))),
                        new UiNode(UiKind.StackItem).With(PainterDropdown(c, "trimSelection", cardTrims))))));
    }

    // tg: PaintingMachine.jsx:110
    private static UiNode EjectButton(string key, string name, System.Action onClickEject) => new UiNode(UiKind.Button)
    {
        Key = key,
        Fluid = true,
        Icon = "eject",
        Text = string.IsNullOrEmpty(name) ? "-----" : name,
        OnClick = onClickEject,
    };

    // tg: PaintingMachine.jsx:124
    private static UiNode PainterDropdown(TguiContext c, string stateKey, IReadOnlyList<KeyValuePair<string, UiValue>> options)
    {
        string selectedOption = c.UseState(stateKey, options.Count > 0 ? options[0].Value.AsStr() : null);
        return new UiNode(UiKind.Dropdown)
        {
            Key = stateKey,
            Width = "100%",
            TextValue = selectedOption,
            Options = options.Select(o => o.Value.AsStr()).ToList(),
            OnSelected = sel => c.SetState(stateKey, sel),
        };
    }
}
