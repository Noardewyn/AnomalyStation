using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ForensicsSpoofer.tsx (gaps J4, D298).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ForensicsSpoofer
{
    // tg: ForensicsSpoofer.tsx:25
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool silent = data.Truthy("silent");
        bool scanmode = data.Truthy("scanmode");
        IReadOnlyList<string> fibers = Keys(data.Get("fibers"));
        IReadOnlyList<string> fingerprints = Keys(data.Get("fingerprints"));
        string chosen_fiber = data.Str("chosen_fiber");
        string chosen_fingerprint = data.Str("chosen_fingerprint");
        double max_storage = data.Num("max_storage");
        int currentTab = c.UseState("tab", 0);

        c.WindowTheme = "syndicate";
        // tg: ForensicsSpoofer.tsx:38  <Window title="Forensics Spoofing Device" width={460} height={340} theme="syndicate">
        return c.Window(460, 340, "Forensics Spoofing Device",
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem).With(
                    new UiNode(UiKind.Section).With(
                        new UiNode(UiKind.Stack).With(
                            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button)
                            {
                                Key = "stealth",
                                Fluid = true,
                                Icon = silent ? "eye-slash" : "eye",
                                Text = silent ? "Silent Mode: On" : "Silent Mode: Off",
                                OnClick = () => c.Act("stealth"),
                            }),
                            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Button)
                            {
                                Key = "scanmode",
                                Fluid = true,
                                Icon = scanmode ? "magnifying-glass" : "share-from-square",
                                Text = scanmode ? "Mode: Scan" : "Mode: Apply",
                                Color = scanmode ? "blue" : "red",
                                OnClick = () => c.Act("scanmode"),
                            })))),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Section) { Fill = true }.With(
                        new UiNode(UiKind.Tabs) { Fluid = true }.With(
                            new UiNode(UiKind.Tab) { Key = "tab-prints", Icon = "fingerprint", Selected = currentTab == 0, Text = $"Fingerprints {fingerprints.Count}/{max_storage}", OnClick = () => c.SetState("tab", 0) },
                            new UiNode(UiKind.Tab) { Key = "tab-fibers", Icon = "shirt", Selected = currentTab == 1, Text = $"Fibers {fibers.Count}/{max_storage}", OnClick = () => c.SetState("tab", 1) }),
                        new UiNode(UiKind.Divider) { Rule = true },
                        new UiNode(UiKind.Stack) { Vertical = true }.With((currentTab == 0 ? fingerprints : fibers).Select(forensic_data =>
                        {
                            bool isPrint = currentTab == 0;
                            return new UiNode(UiKind.StackItem) { Key = forensic_data }.With(
                                new UiNode(UiKind.Stack).With(
                                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                                    {
                                        Key = "choose-" + forensic_data,
                                        Checkbox = (isPrint ? chosen_fingerprint : chosen_fiber) == forensic_data,
                                        OnClick = () => c.Act("choose", ("chosen", forensic_data)),
                                    }),
                                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                                    {
                                        Key = "delete-" + forensic_data,
                                        Color = "red",
                                        Icon = "trash",
                                        OnClick = () => c.Act("delete", ("chosen", forensic_data)),
                                    }),
                                    new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = isPrint && forensic_data.Length > 25 ? forensic_data[..25] : forensic_data }),
                                    isPrint
                                        ? new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Box)
                                        {
                                            Bold = true,
                                            TextAlign = "right",
                                            Text = $"({data.Get("fingerprints")?.Get(forensic_data)?.Text})",
                                        })
                                        : null));
                        }))))));
    }

    // Object.keys(): an assoc list arrives as an object, an empty DM list as an array
    private static IReadOnlyList<string> Keys(UiValue list) => list is { Kind: UiValueKind.Object } ? list.Fields.Select(f => f.Key).ToList() : System.Array.Empty<string>();
}
