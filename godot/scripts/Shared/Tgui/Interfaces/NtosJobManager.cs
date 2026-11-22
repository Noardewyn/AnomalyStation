using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosJobManager.jsx (gaps S4a, D327): Plexagon HR
// Core, the job slots with their open and close buttons and the prioritized jobs. The Dimmer over the cooling-down table is a
// Modal (the kit has no dimmer, as ApcControl). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosJobManager
{
    // tg: NtosJobManager.jsx:13
    public static UiNode Render(TguiContext c) =>
        NtosWindow.Render(c, 400, 620, null, new UiNode(UiKind.Box) { Scrollable = true, Fill = true }.With(NtosJobManagerContent(c)));

    // tg: NtosJobManager.jsx:23
    public static UiNode NtosJobManagerContent(TguiContext c)
    {
        UiValue data = c.Data;
        if (!data.Truthy("authed"))
            return new UiNode(UiKind.NoticeBox) { Text = "Current ID does not have access permissions to change job slots." };
        double cooldown = data.Num("cooldown");
        List<string> prioritized = data.Arr("prioritized").Select(p => p.Text ?? "").ToList();

        var table = new UiNode(UiKind.Table).With(new UiNode(UiKind.TableRow) { Key = "header", Bold = true }.With(
            new UiNode(UiKind.TableCell) { Text = "Prioritized" },
            new UiNode(UiKind.TableCell) { Text = "Slots" }));
        foreach (UiValue slot in data.Arr("slots"))
        {
            string title = slot.Str("title");
            double total = slot.Num("total");
            table.With(new UiNode(UiKind.TableRow) { Key = "slot:" + title, Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Bold = true }.With(new UiNode(UiKind.Button)
                {
                    Key = "priority",
                    Fluid = true,
                    Text = title,
                    Disabled = total <= 0,
                    Checkbox = total > 0 && prioritized.Contains(title),
                    OnClick = () => c.Act("PRG_priority", ("target", title)),
                }),
                new UiNode(UiKind.TableCell) { Text = TguiFormat.JsNumber(slot.Num("current")) + " / " + TguiFormat.JsNumber(total) },
                new UiNode(UiKind.TableCell).With(
                    new UiNode(UiKind.Button) { Key = "open", Text = "Open", Disabled = !slot.Truthy("status_open"), OnClick = () => c.Act("PRG_open_job", ("target", title)) },
                    new UiNode(UiKind.Button) { Key = "close", Text = "Close", Disabled = !slot.Truthy("status_close"), OnClick = () => c.Act("PRG_close_job", ("target", title)) })));
        }

        return new UiNode(UiKind.Section).With(
            cooldown > 0
                ? new UiNode(UiKind.Modal) { Key = "cooldown" }.With(new UiNode(UiKind.Box) { Bold = true, TextAlign = "center", FontSize = "20px", Text = "On Cooldown: " + TguiFormat.JsNumber(cooldown) + "s" })
                : null,
            table);
    }
}
