using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/CircuitAccessChecker.tsx: the access checker
// component's window (which accesses its ID must hold, one or all of them). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class CircuitAccessChecker
{
    // tg: CircuitAccessChecker.tsx:14
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool oneAccess = data.Truthy("oneAccess");
        IReadOnlyList<UiValue> regions = data.Arr("regions");
        List<string> accesses = data.Arr("accesses").Select(a => a.AsStr()).ToList();

        // <Window width={420} height={360}>
        return c.Window(420, 360, null,
            new UiNode(UiKind.LabeledList).With(
                new UiNode(UiKind.LabeledItem) { Label = "Access Required" }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "one_access",
                        Icon = oneAccess ? "unlock" : "lock",
                        Text = oneAccess ? "One" : "All",
                        OnClick = () => c.Act("one_access"),
                    })),
            AccessConfig.Render(c,
                accesses: regions,
                selectedList: accesses,
                accessMod: reference => c.Act("set", ("access", reference)),
                grantAll: () => c.Act("grant_all"),
                denyAll: () => c.Act("clear_all"),
                grantDep: reference => c.Act("grant_region", ("region", reference)),
                denyDep: reference => c.Act("deny_region", ("region", reference))));
    }
}
