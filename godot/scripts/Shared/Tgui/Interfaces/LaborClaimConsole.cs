using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/LaborClaimConsole.jsx (Lavaland L2b, D304).
// The window reads tg's ui_data and ui_static_data of the labour camp's point claim console (laborstacker.dm:49, :79) and
// sends claim_points and move_shuttle. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class LaborClaimConsole
{
    // tg: LaborClaimConsole.jsx:6
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool can_go_home = data.Truthy("can_go_home");
        double unclaimed_points = data.Num("unclaimed_points");
        var list = new UiNode(UiKind.LabeledList) { Key = "status" }.With(
            new UiNode(UiKind.LabeledItem) { Key = "info", Label = "Status", Text = data.Str("status_info") });
        if (data.Truthy("shuttle_exists"))
            list.With(new UiNode(UiKind.LabeledItem) { Key = "shuttle", Label = "Shuttle controls" }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "move",
                    Text = "Move shuttle",
                    Disabled = !can_go_home,
                    OnClick = () => c.Act("move_shuttle"),
                }));
        list.With(
            new UiNode(UiKind.LabeledItem) { Key = "points", Label = "Points", Text = data.Str("id_points") },
            new UiNode(UiKind.LabeledItem)
            {
                Key = "unclaimed",
                Label = "Unclaimed points",
                Text = data.Str("unclaimed_points"),
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "claim",
                    Text = "Claim points",
                    Disabled = unclaimed_points == 0,
                    OnClick = () => c.Act("claim_points"),
                },
            });
        // tg: LaborClaimConsole.jsx:17  <Window width={315} height={440}>
        return c.Window(315, 440, null,
            new UiNode(UiKind.Section) { Key = "main" }.With(list),
            new UiNode(UiKind.Section) { Key = "directions", Title = "Directions" }.With(new UiNode(UiKind.Box)
            {
                Wrap = true,
                Text = "Collect boulders and ores from the mining area and bring them to the unloading machine. Then, pull the lever to smelt "
                    + "them into sheets. Use the production console to dispense the sheets, and then bring bring them to the claim console to "
                    + "earn points.\n\nBoulders cannot be smelted directly, and will require additional manual processing - strike them with "
                    + "a pickaxe to break them down further.\n\nSheets smelted from the labor camp's furnace will be stamped with an official "
                    + "seal of quality. Only stamped sheets can be claimed for points - so don't bother trying to strip down tables and chairs "
                    + "for bonus metal.",
            }));
    }
}
