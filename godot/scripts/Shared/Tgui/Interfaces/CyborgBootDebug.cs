using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/CyborgBootDebug.jsx (silicons SI2, D326: the robot
// suit's boot debug window, a multitool on an unfinished cyborg). Engine-free (compiled into the tests too). The Input's onBlur is the
// kit's commit (Enter or leaving the field).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class CyborgBootDebug
{
    // tg: CyborgBootDebug.jsx:6
    private const string TooltipName = "Enter a new name for this unit. Set to blank to reset to default, which means unit will be able to choose its own name.";
    private const string TooltipLocomotion = "If restricted, unit will be under lockdown until released.";
    private const string TooltipPanel = "If unlocked, unit's cover panel will be accessible even without proper authorization.";
    private const string TooltipAisync = "If closed, this unit will not be paired with any AI.";
    private const string TooltipAi = "Controls who will be the master AI of this unit.";
    private const string TooltipLawsync = "If closed, this unit will not synchronize its laws with its master AI.";

    private static UiNode Info(string key, string tooltip) => new UiNode(UiKind.Button) { Key = key, Icon = "info", Tooltip = tooltip };

    // tg: CyborgBootDebug.jsx:36
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        string designation = data.Str("designation");
        string master = data.Str("master");
        bool lawsync = data.Truthy("lawsync");
        bool aisync = data.Truthy("aisync");
        bool locomotion = data.Truthy("locomotion");
        bool panel = data.Truthy("panel");

        var basic = new UiNode(UiKind.Section) { Key = "basic", Title = "Basic Settings" }.With(new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Key = "designation", Label = "Designation", Buttons = Info("info-name", TooltipName) }.With(new UiNode(UiKind.Input)
            {
                Key = "name",
                Fluid = true,
                TextValue = string.IsNullOrEmpty(designation) ? "Default Cyborg" : designation,
                OnCommit = value => c.Act("rename", ("new_name", value)),
            }),
            new UiNode(UiKind.LabeledItem) { Key = "locomotion", Label = "Servo Motor Functions", Buttons = Info("info-locomotion", TooltipLocomotion) }.With(new UiNode(UiKind.Button)
            {
                Key = "locomotion",
                Icon = locomotion ? "unlock" : "lock",
                Text = locomotion ? "Free" : "Restricted",
                Color = locomotion ? "good" : "bad",
                OnClick = () => c.Act("locomotion"),
            }),
            new UiNode(UiKind.LabeledItem) { Key = "panel", Label = "Cover Panel", Buttons = Info("info-panel", TooltipPanel) }.With(new UiNode(UiKind.Button)
            {
                Key = "panel",
                Icon = panel ? "lock" : "unlock",
                Text = panel ? "Locked" : "Unlocked",
                OnClick = () => c.Act("panel"),
            })));

        bool hasMaster = !string.IsNullOrEmpty(master);
        var ai = new UiNode(UiKind.Section) { Key = "ai", Title = "AI Settings" }.With(new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Key = "aisync", Label = "AI Connection Port", Buttons = Info("info-aisync", TooltipAisync) }.With(new UiNode(UiKind.Button)
            {
                Key = "aisync",
                Icon = aisync ? "unlock" : "lock",
                Text = aisync ? "Open" : "Closed",
                OnClick = () => c.Act("aisync"),
            }),
            new UiNode(UiKind.LabeledItem) { Key = "master", Label = "Master AI", Buttons = Info("info-ai", TooltipAi) }.With(new UiNode(UiKind.Button)
            {
                Key = "set_ai",
                Icon = !aisync ? "times" : hasMaster ? "edit" : "sync",
                Text = !aisync ? "None" : hasMaster ? master : "Automatic",
                Color = hasMaster ? "default" : "good",
                Disabled = !aisync,
                OnClick = () => c.Act("set_ai"),
            }),
            new UiNode(UiKind.LabeledItem) { Key = "lawsync", Label = "LawSync Port", Buttons = Info("info-lawsync", TooltipLawsync) }.With(new UiNode(UiKind.Button)
            {
                Key = "lawsync",
                Icon = !aisync ? "lock" : lawsync ? "unlock" : "lock",
                Text = !aisync ? "Closed" : lawsync ? "Open" : "Closed",
                Disabled = !aisync,
                OnClick = () => c.Act("lawsync"),
            })));

        // <Window width={master?.length > 26 ? 537 : 440} height={289}>
        return c.Window(master.Length > 26 ? 537 : 440, 289, null, basic, ai);
    }
}
