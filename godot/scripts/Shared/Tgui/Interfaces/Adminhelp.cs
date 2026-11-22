using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

/// <summary>M10x slice G6: tg's Adminhelp window (tgui/packages/tgui/interfaces/Adminhelp.tsx), the player's admin help box.</summary>
public static class Adminhelp
{
    // tg: tgui/packages/tgui/interfaces/Adminhelp.tsx:22
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double adminCount = data.Num("adminCount");
        bool urgentAhelpEnabled = data.Truthy("urgentAhelpEnabled");
        bool bannedFromUrgentAhelp = data.Truthy("bannedFromUrgentAhelp");
        string urgentAhelpPromptMessage = data.Str("urgentAhelpPromptMessage");
        bool requestForAdmin = c.UseState("requestForAdmin", false);
        bool currentlyInputting = c.UseState("currentlyInputting", false);
        string ahelpMessage = c.UseState("ahelpMessage", "");

        const string confirmationText = "alert admins";
        UiNode urgent = null;
        if (urgentAhelpEnabled && adminCount <= 0)
        {
            UiNode prompt = currentlyInputting
                ? new UiNode(UiKind.Box) { Key = "confirm" }.With(
                    new UiNode(UiKind.Box) { Text = "Input '" + confirmationText + "' to proceed." },
                    new UiNode(UiKind.Input)
                    {
                        Key = "confirmation",
                        Tooltip = "Confirmation Prompt",
                        AutoFocus = true,
                        Fluid = true,
                        OnInput = value =>
                        {
                            if (value == confirmationText)
                            {
                                c.SetState("requestForAdmin", true);
                                c.SetState("currentlyInputting", false);
                            }
                        },
                    })
                : new UiNode(UiKind.Button)
                {
                    Key = "alert",
                    Color = requestForAdmin ? "orange" : "blue",
                    Icon = requestForAdmin ? "check-square-o" : "square-o",
                    Disabled = bannedFromUrgentAhelp,
                    Tooltip = bannedFromUrgentAhelp ? "You are banned from using urgent ahelps." : null,
                    Fluid = true,
                    TextAlign = "center",
                    Text = "Alert admins?",
                    OnClick = () =>
                    {
                        if (requestForAdmin)
                            c.SetState("requestForAdmin", false);
                        else
                            c.SetState("currentlyInputting", true);
                    },
                };
            urgent = new UiNode(UiKind.StackItem) { Key = "urgent" }.With(
                new UiNode(UiKind.NoticeBox) { Color = "info" }.With(new UiNode(UiKind.Box) { Text = urgentAhelpPromptMessage }, prompt));
        }

        // tg's theme="admin": the kit's default theme
        return c.Window(500, 300, "Create Adminhelp",
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "message", Grow = 1 }.With(
                    new UiNode(UiKind.TextArea)
                    {
                        Key = "ahelp",
                        AutoFocus = true,
                        Height = "100%",
                        Fluid = true,
                        Wrap = true,
                        Tooltip = "Admin help",
                        TextValue = ahelpMessage,
                        OnInput = value => c.SetState("ahelpMessage", value),
                    }),
                urgent,
                new UiNode(UiKind.StackItem) { Key = "submit" }.With(
                    new UiNode(UiKind.Button)
                    {
                        Color = "good",
                        Fluid = true,
                        TextAlign = "center",
                        Text = "Submit",
                        OnClick = () => c.Act("ahelp", ("urgent", c.UseState("requestForAdmin", false)), ("message", c.UseState("ahelpMessage", ""))),
                    })));
    }
}
