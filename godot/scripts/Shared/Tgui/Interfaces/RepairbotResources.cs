using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/RepairbotResources.tsx (gaps package C3): a player repairbot's stacks (repairbot_abilities.dm:73).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// Not drawn: the rounded stack cards' border and the smiling icon in the title row keep tg's meaning as a plain row and a button-row icon (the kit has no style prop).

public static class RepairbotResources
{
    // tg: RepairbotResources.tsx:28
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> stacks = data.Field("stacks")?.Items ?? Array.Empty<UiValue>();
        c.WindowTheme = "ntos";
        var row = new UiNode(UiKind.Stack) { Wrap = true };
        foreach (UiValue stack in stacks)
        {
            string reference = stack.Str("stack_reference");
            row.With(new UiNode(UiKind.StackItem) { Key = reference }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.ImageButton)
                {
                    Key = "icon:" + reference,
                    DmIcon = stack.Str("stack_icon"),
                    DmIconState = stack.Str("stack_icon_state"),
                }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Stack) { Vertical = true }.With(
                    new UiNode(UiKind.StackItem) { Width = "100px" }.With(new UiNode(UiKind.ProgressBar)
                    {
                        Key = "amount:" + reference,
                        Value = stack.Num("stack_amount"),
                        MaxValue = stack.Num("stack_maximum_amount"),
                        Color = "green",
                    }),
                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                    {
                        Key = "eject:" + reference,
                        Width = "75px",
                        Text = "Eject",
                        OnClick = () => c.Act("eject", ("item_reference", reference)),
                    }))))));
        }

        // tg: RepairbotResources.tsx:32  <Window title="Resource Management" width={405} height={260} theme="ntos">
        return c.Window(405, 260, "Resource Management",
            new UiNode(UiKind.Section)
            {
                Fill = true,
                Title = "Resource Management",
                TextAlign = "center",
                Scrollable = true,
                Buttons = new UiNode(UiKind.ImageButton) { Key = "smile", DmIcon = data.Str("repairbot_icon"), DmIconState = data.Str("repairbot_icon_state") },
            }.With(row));
    }
}
