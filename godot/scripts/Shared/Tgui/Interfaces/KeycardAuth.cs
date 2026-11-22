using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/KeycardAuth.jsx (gaps J4, D298).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class KeycardAuth
{
    // tg: tgui/packages/tgui/interfaces/KeycardAuth.jsx:6
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        double waiting = data.Num("waiting");
        UiValue auth_required = data.Get("auth_required");
        bool awaitingConfirmation = data.Truthy("auth_required");
        bool nothingPending = auth_required is { Kind: UiValueKind.Number } && data.Num("auth_required") == 0;

        UiNode Choice(string key, string icon, string text) => new UiNode(UiKind.Button) { Key = key, Icon = icon, Fluid = true, Text = text, OnClick = () => c.Act(key) };

        // tg: KeycardAuth.jsx:9  <Window width={375} height={145}>
        return c.Window(375, 145, null,
            new UiNode(UiKind.Section).With(
                new UiNode(UiKind.Box).With(
                    waiting == 1 ? new UiNode(UiKind.Box) { Text = "Waiting for another device to confirm your request..." } : null),
                new UiNode(UiKind.Box).With(
                    waiting == 0 && awaitingConfirmation
                        ? new UiNode(UiKind.Button)
                        {
                            Key = "auth_swipe",
                            Icon = "check-square",
                            Color = "red",
                            TextAlign = "center",
                            Fluid = true,
                            Text = "Authorize",
                            OnClick = () => c.Act("auth_swipe"),
                        }
                        : null,
                    waiting == 0 && nothingPending ? Choice("red_alert", "exclamation-triangle", "Red Alert") : null,
                    waiting == 0 && nothingPending ? Choice("emergency_maint", "wrench", "Emergency Maintenance Access") : null,
                    waiting == 0 && nothingPending ? Choice("bsa_unlock", "meteor", "Bluespace Artillery Unlock") : null,
                    waiting == 0 && nothingPending ? Choice("give_janitor_access", "key", "Grant Janitor Access") : null)));
    }
}
