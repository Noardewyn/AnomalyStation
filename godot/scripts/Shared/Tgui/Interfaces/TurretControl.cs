using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/TurretControl.tsx (syndicate G4d, D316): the turret
// control panel (turret_control.dm:136).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class TurretControl
{
    // tg: TurretControl.tsx:16
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool enabled = data.Truthy("enabled");
        bool lethal = data.Truthy("lethal");
        bool siliconUser = data.Truthy("siliconUser");
        bool shootCyborgs = data.Truthy("shootCyborgs");
        bool isLocked = data.Truthy("locked") && !siliconUser;

        // tg: TurretControl.tsx:22  <Window width={305} height={siliconUser ? 168 : 164}>
        return c.Window(305, siliconUser ? 168 : 164, null,
            AirAlarm.InterfaceLockNoticeBox(c),
            new UiNode(UiKind.Section) { Key = "settings" }.With(
                new UiNode(UiKind.LabeledList).With(
                    new UiNode(UiKind.LabeledItem) { Label = "Turret Status" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "power",
                        Icon = enabled ? "power-off" : "times",
                        Text = enabled ? "Enabled" : "Disabled",
                        Selected = enabled,
                        Disabled = isLocked,
                        OnClick = () => c.Act("power"),
                    }),
                    new UiNode(UiKind.LabeledItem) { Label = "Turret Mode" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "mode",
                        Icon = lethal ? "exclamation-triangle" : "minus-circle",
                        Text = lethal ? "Lethal" : "Stun",
                        Color = lethal ? "bad" : "average",
                        Disabled = isLocked,
                        OnClick = () => c.Act("mode"),
                    }),
                    new UiNode(UiKind.LabeledItem) { Label = "Target Cyborgs" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "shoot_silicons",
                        Icon = shootCyborgs ? "check" : "times",
                        Text = shootCyborgs ? "Yes" : "No",
                        Selected = shootCyborgs,
                        Disabled = isLocked,
                        OnClick = () => c.Act("shoot_silicons"),
                    }))));
    }
}
