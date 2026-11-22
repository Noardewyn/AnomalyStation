using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PortableScrubber.tsx (package E3, docs/ai/specs/engineering.md:
// the portable air scrubber's window). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
//
// tgui's inline filter buttons flow in rows: the kit's wrapped Stack. React's key (filter.id) is undefined in tg's data (the rows carry
// gasId, gasName and enabled only), so the key here is the gas id.

public static class PortableScrubber
{
    // tg: tgui/packages/tgui/interfaces/PortableScrubber.tsx:21
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;

        // tg: PortableScrubber.tsx:26  <Window width={320} height={420}>
        return c.Window(320, 420, null,
            PortableAtmos.PortableBasicInfo(c),
            // tg: PortableScrubber.tsx:29
            new UiNode(UiKind.Section) { Title = "Filters" }.With(
                new UiNode(UiKind.Stack) { Wrap = true }.With(
                    data.Arr("filterTypes").Select(filter =>
                    {
                        bool enabled = filter.Truthy("enabled");
                        string gasId = filter.Str("gasId");
                        return new UiNode(UiKind.Button)
                        {
                            Key = "filter:" + gasId,
                            Icon = enabled ? "check-square-o" : "square-o",
                            Text = TguiFormat.GetGasLabel(gasId, filter.Str("gasName")),
                            Selected = enabled,
                            OnClick = () => c.Act("toggle_filter", ("val", gasId)),
                        };
                    }))));
    }
}
