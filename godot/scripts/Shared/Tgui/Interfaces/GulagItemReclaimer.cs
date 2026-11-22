using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/GulagItemReclaimer.tsx (gaps LC1): the prisoners whose
// things the reclaimer keeps, each with its retrieve button (machinery/gulag_item_reclaimer.dm:52 ui_data).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class GulagItemReclaimer
{
    // tg: GulagItemReclaimer.tsx:12
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool canReclaim = data.Truthy("can_reclaim");
        IReadOnlyList<UiValue> mobs = data.Get("mobs")?.Items ?? Array.Empty<UiValue>();
        // tg: GulagItemReclaimer.tsx:17  <Window width={325} height={400}>
        return c.Window(325, 400, null,
            new UiNode(UiKind.Box) { Key = "content", Scrollable = true }.With(
                mobs.Count == 0 ? new UiNode(UiKind.NoticeBox) { Key = "empty", Text = "No stored items" } : null,
                mobs.Count > 0
                    ? new UiNode(UiKind.Section) { Key = "stored", Title = "Stored Items" }.With(
                        new UiNode(UiKind.Table).With(mobs.Select(m => new UiNode(UiKind.TableRow) { Key = "mob:" + m.Str("mob") }.With(
                            new UiNode(UiKind.TableCell) { Key = "name", Text = m.Str("name") },
                            new UiNode(UiKind.TableCell) { Key = "retrieve", TextAlign = "right" }.With(new UiNode(UiKind.Button)
                            {
                                Key = "release:" + m.Str("mob"),
                                Text = "Retrieve Items",
                                Disabled = !canReclaim,
                                OnClick = () => c.Act("release_items", ("mobref", m.Str("mob"))),
                            })))))
                    : null));
    }
}
