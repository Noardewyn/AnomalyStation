using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/RepairBot.tsx (gaps package C3): SimpleBot's settings and controls and the materials the
// bot carries (repairbot.dm:286). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class RepairBot
{
    // tg: RepairBot.tsx:42
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool access = !data.Truthy("locked") || data.Truthy("can_hack");

        // tg: RepairBot.tsx:48  <Window width={450} height={415}>
        return c.Window(450, 415, null,
            new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
                new UiNode(UiKind.StackItem).With(SimpleBot.BotSettings(c)),
                access ? new UiNode(UiKind.StackItem) { Grow = 1 }.With(SimpleBot.BotControl(c)) : null,
                access ? new UiNode(UiKind.StackItem) { Grow = 1 }.With(RepairBotMats(c)) : null));
    }

    // tg: RepairBot.tsx:71
    private static UiNode RepairBotMats(TguiContext c)
    {
        IReadOnlyList<UiValue> materials = c.Data.Field("repairbot_materials")?.Items ?? Array.Empty<UiValue>();
        var section = new UiNode(UiKind.Section) { Title = "Materials" };
        if (materials.Count == 0)
            section.With(new UiNode(UiKind.NoticeBox) { Text = "No Materials!" });
        var row = new UiNode(UiKind.Stack);
        foreach (UiValue mat in materials)
        {
            string reference = mat.Str("material_ref");
            row.With(new UiNode(UiKind.StackItem) { Key = reference, Grow = 1 }.With(new UiNode(UiKind.ImageButton)
            {
                Key = "material:" + reference,
                DmIcon = mat.Str("material_icon"),
                DmIconState = mat.Str("material_icon_state"),
                OnClick = () => c.Act("remove_item", ("item_reference", reference)),
            }));
        }
        return section.With(row);
    }
}
