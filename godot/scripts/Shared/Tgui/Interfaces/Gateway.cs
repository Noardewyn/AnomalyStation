using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Gateway.jsx (the gateway control console). The ByondUi
// map is the client's second 3D view (UiKind.ByondUi); the server names no atom to draw it from, so it shows the static.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Gateway
{
    // tg: Gateway.jsx:13
    public static UiNode Render(TguiContext c) =>
        c.Window(350, 440, null,
            new UiNode(UiKind.Section) { Key = "content", Fill = true, Scrollable = true }.With(GatewayContent(c)));

    // tg: Gateway.jsx:23
    private static UiNode GatewayContent(TguiContext c)
    {
        UiValue data = c.Data;
        bool gatewayPresent = data.Truthy("gateway_present");
        bool gatewayStatus = data.Truthy("gateway_status");
        UiValue currentTarget = data.Field("current_target");
        IReadOnlyList<UiValue> destinations = data.Arr("destinations");
        string mapKey = data.Str("gateway_mapkey");

        if (!gatewayPresent)
        {
            // tg: Gateway.jsx:33
            return new UiNode(UiKind.Section) { Key = "no-gateway" }.With(
                new UiNode(UiKind.NoticeBox) { Key = "notice", Text = "No linked gateway" },
                new UiNode(UiKind.Button) { Key = "linkup", Fluid = true, Text = "Linkup", OnClick = () => c.Act("linkup") });
        }
        if (currentTarget != null && currentTarget.Truthy)
        {
            // tg: Gateway.jsx:43
            return new UiNode(UiKind.Section) { Key = "target", Title = currentTarget.Str("name") }.With(
                new UiNode(UiKind.ByondUi) { Key = "map:" + mapKey, MapRef = mapKey, Height = "320px", Width = "100%" },
                new UiNode(UiKind.Button) { Key = "deactivate", Fluid = true, TextAlign = "center", Text = "Deactivate", OnClick = () => c.Act("deactivate") });
        }
        if (destinations.Count == 0)
            return new UiNode(UiKind.Section) { Key = "none", Text = "No gateway nodes detected." };

        // tg: Gateway.jsx:66
        return new UiNode(UiKind.Fragment).With(
            (gatewayStatus ? null : new UiNode(UiKind.NoticeBox) { Key = "unpowered", Text = "Gateway Unpowered" }),
            new UiNode(UiKind.Fragment) { Key = "destinations" }.With(destinations.Select(dest =>
            {
                string reference = dest.Str("ref");
                var section = new UiNode(UiKind.Section) { Key = "dest:" + reference, Title = dest.Str("name") };
                if (dest.Truthy("available"))
                    return section.With(new UiNode(UiKind.Button)
                    {
                        Key = "activate",
                        Fluid = true,
                        Text = "Activate",
                        OnClick = () => c.Act("activate", ("destination", reference)),
                    });
                double timeout = dest.Num("timeout");
                return section.With(
                    new UiNode(UiKind.Box) { Key = "reason", Color = "bad", Text = dest.Str("reason") },
                    timeout != 0 ? new UiNode(UiKind.ProgressBar) { Key = "timeout", Value = timeout, Text = "Calibrating..." } : null);
            })));
    }
}
