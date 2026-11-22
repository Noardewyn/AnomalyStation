using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces.Common;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/common/StatusDisplayControls.tsx (M10 slice G5: the
// communications console's status display page). Engine-free. useSharedState is the window's local state here.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class StatusDisplayControls
{
    // tg: common/StatusDisplayControls.tsx:11
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        int maxStatusLineLength = (int)data.Num("maxStatusLineLength");
        string upperText = c.UseState("statusUpperText", data.Str("upperText") ?? "");
        string lowerText = c.UseState("statusLowerText", data.Str("lowerText") ?? "");

        UiNode Picture(string key, string icon, string text, string picture, string color = null) => new UiNode(UiKind.Button)
        {
            Key = key,
            Icon = icon,
            Color = color,
            Text = text,
            OnClick = () => c.Act("setStatusPicture", ("picture", picture)),
        };

        return new UiNode(UiKind.Fragment).With(
            new UiNode(UiKind.Section) { Key = "states" }.With(
                Picture("off", "toggle-off", "Off", "blank", "bad"),
                Picture("shuttle", "space-shuttle", "Shuttle ETA / Off", "shuttle")),
            new UiNode(UiKind.Section) { Key = "graphics", Title = "Graphics" }.With(
                Picture("default", "flag", "Logo", "default"),
                Picture("currentalert", "exclamation", "Security Alert Level", "currentalert"),
                Picture("lockdown", "exclamation-triangle", "Lockdown", "lockdown"),
                Picture("biohazard", "biohazard", "Biohazard", "biohazard"),
                Picture("radiation", "radiation", "Radiation", "radiation")),
            new UiNode(UiKind.Section) { Key = "message", Title = "Message" }.With(
                new UiNode(UiKind.Stack) { Vertical = true, Align = "stretch" }.With(
                    new UiNode(UiKind.StackItem) { Key = "upper" }.With(new UiNode(UiKind.Input)
                    {
                        Key = "upperText",
                        Fluid = true,
                        MaxLength = maxStatusLineLength,
                        TextValue = upperText,
                        OnInput = v => c.SetState("statusUpperText", v),
                    }),
                    new UiNode(UiKind.StackItem) { Key = "lower" }.With(new UiNode(UiKind.Input)
                    {
                        Key = "lowerText",
                        Fluid = true,
                        MaxLength = maxStatusLineLength,
                        TextValue = lowerText,
                        OnInput = v => c.SetState("statusLowerText", v),
                    }),
                    new UiNode(UiKind.StackItem) { Key = "send" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "sendStatus",
                        Icon = "comment-o",
                        Text = "Send",
                        OnClick = () => c.Act("setStatusMessage",
                            ("upperText", c.UseState("statusUpperText", "")), ("lowerText", c.UseState("statusLowerText", ""))),
                    }))));
    }
}
