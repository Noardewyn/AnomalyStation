using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosFaxBond.tsx (gaps S3, D298: FaxBond, the heads' PDA
// app of their faxes). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosFaxBond
{
    // tg: NtosFaxBond.tsx:16
    public static UiNode Render(TguiContext c)
    {
        IReadOnlyList<UiValue> faxes_info = c.Data.Arr("faxes_info");
        var content = new List<UiNode>
        {
            new UiNode(UiKind.NoticeBox) { Text = "Scan any fax to be notified when it receives a message." },
        };
        if (faxes_info.Count > 0)
        {
            var table = new UiNode(UiKind.Table).With(
                new UiNode(UiKind.TableRow).With(
                    new UiNode(UiKind.TableCell) { Bold = true, Text = "ID" },
                    new UiNode(UiKind.TableCell) { Bold = true, Text = "Name" },
                    new UiNode(UiKind.TableCell) { Bold = true, Text = "Location" },
                    new UiNode(UiKind.TableCell)));
            foreach (UiValue fax in faxes_info)
            {
                string id = fax.Str("id");
                bool muted = fax.Truthy("muted");
                table.With(new UiNode(UiKind.TableRow) { Key = id, Candystripe = true }.With(
                    new UiNode(UiKind.TableCell) { Text = id },
                    new UiNode(UiKind.TableCell) { Text = fax.Str("name") },
                    new UiNode(UiKind.TableCell) { Text = fax.Str("location") },
                    new UiNode(UiKind.TableCell).With(new UiNode(UiKind.Stack).With(
                        new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                        {
                            Key = "mute:" + id,
                            Fluid = true,
                            Icon = muted ? "bell-slash" : "bell",
                            Color = muted ? "red" : null,
                            Tooltip = muted ? "Unmute Notifications" : "Mute Notifications",
                            OnClick = () => c.Act("mute", ("id", id)),
                        }),
                        new UiNode(UiKind.StackItem).With(c.ButtonConfirm("unsubscribe:" + id, new UiNode(UiKind.Button)
                        {
                            Fluid = true,
                            Icon = "link-slash",
                            Tooltip = "Unsubscribe",
                            OnClick = () => c.Act("unsubscribe", ("id", id)),
                        }))))));
            }
            content.Add(new UiNode(UiKind.Section).With(table));
        }

        // tg: NtosFaxBond.tsx:17  <NtosWindow width={400} height={500}> <NtosWindow.Content scrollable>
        return NtosWindow.Render(c, 400, 500, null,
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Grow = 1, Scrollable = true }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(content))));
    }
}
