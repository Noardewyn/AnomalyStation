using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PortableTurret.jsx (syndicate G4d, D316): the portable
// turret's settings (portable_turret.dm:245).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class PortableTurret
{
    // tg: PortableTurret.jsx:6
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool siliconUser = data.Truthy("silicon_user");
        bool locked = data.Truthy("locked");
        bool on = data.Truthy("on");
        bool manualControl = data.Truthy("manual_control");
        bool allowManualControl = data.Truthy("allow_manual_control");
        bool lasertagTurret = data.Truthy("lasertag_turret");

        // tg: PortableTurret.jsx:24  <Window width={310} height={lasertag_turret ? 110 : 292}>
        return c.Window(310, lasertagTurret ? 110 : 292, null,
            new UiNode(UiKind.NoticeBox) { Key = "notice", Text = "Swipe an ID card to " + (locked ? "unlock" : "lock") + " this interface." },
            new UiNode(UiKind.Section) { Key = "status" }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem)
                    {
                        Label = "Status",
                        Buttons = !lasertagTurret && (allowManualControl || (manualControl && siliconUser))
                            ? new UiNode(UiKind.Button)
                            {
                                Key = "manual",
                                Icon = manualControl ? "wifi" : "terminal",
                                Text = manualControl ? "Remotely Controlled" : "Manual Control",
                                Disabled = manualControl,
                                Color = "bad",
                                OnClick = () => c.Act("manual"),
                            }
                            : null,
                    }.With(new UiNode(UiKind.Button)
                    {
                        Key = "power",
                        Icon = on ? "power-off" : "times",
                        Text = on ? "On" : "Off",
                        Selected = on,
                        Disabled = locked,
                        OnClick = () => c.Act("power"),
                    }))),
            lasertagTurret ? null : TargetSettings(c, locked));
    }

    // tg: PortableTurret.jsx:61
    private static UiNode TargetSettings(TguiContext c, bool locked)
    {
        UiValue data = c.Data;
        return new UiNode(UiKind.Section)
        {
            Key = "targets",
            Title = "Target Settings",
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "shootheads",
                Checkbox = !data.Truthy("neutralize_heads"),
                Text = "Ignore Command",
                Disabled = locked,
                OnClick = () => c.Act("shootheads"),
            },
        }.With(
            Flag(c, locked, "neutralize_all", "Non-Security and Non-Command", "shootall"),
            Flag(c, locked, "check_weapons", "Unauthorized Weapons", "authweapon"),
            Flag(c, locked, "neutralize_unidentified", "Unidentified Life Signs", "checkxenos"),
            Flag(c, locked, "neutralize_nonmindshielded", "Non-Mindshielded", "checkloyal"),
            Flag(c, locked, "neutralize_criminals", "Wanted Criminals", "shootcriminals"),
            Flag(c, locked, "neutralize_cyborgs", "Cyborgs", "shootborgs"));
    }

    // tg: PortableTurret.jsx:72  <Button.Checkbox fluid checked={...} content="..." disabled={locked} onClick={() => act('...')} />
    private static UiNode Flag(TguiContext c, bool locked, string field, string text, string action) => new UiNode(UiKind.Button)
    {
        Key = action,
        Fluid = true,
        Checkbox = c.Data.Truthy(field),
        Text = text,
        Disabled = locked,
        OnClick = () => c.Act(action),
    };
}
