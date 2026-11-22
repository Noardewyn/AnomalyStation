using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AiAirlock.jsx (silicons SI2, D326: a silicon's airlock
// control window). Engine-free (compiled into the tests too). LabeledList.Divider is the kit's Divider.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AiAirlock
{
    // tg: AiAirlock.jsx:6  dangerMap
    private static (string Color, string Text) Danger(double level) => level switch
    {
        2 => ("good", "Offline"),
        1 => ("average", "Caution"),
        _ => ("bad", "Optimal"),
    };

    private static UiNode Toggle(TguiContext c, string key, bool on, bool disabled, string action) => new UiNode(UiKind.Button)
    {
        Key = key,
        Icon = on ? "power-off" : "times",
        Text = on ? "Enabled" : "Disabled",
        Selected = on,
        Disabled = disabled,
        OnClick = () => c.Act(action),
    };

    private static UiNode Item(string key, string label, string color, UiNode buttons, string text) =>
        new UiNode(UiKind.LabeledItem) { Key = key, Label = label, Color = color, Buttons = buttons, Text = text };

    // tg: AiAirlock.jsx:21
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        UiValue power = data.Get("power");
        UiValue wires = data.Get("wires");
        var statusMain = Danger(power.Num("main"));
        var statusBackup = Danger(power.Num("backup"));
        double shock = data.Num("shock");
        var statusElectrify = Danger(shock);
        const string cut = "[Wires have been cut!]";

        string Timed(bool online, bool wiresCut, double timeLeft) =>
            (online ? "Online" : "Offline") + " " + (wiresCut ? cut : timeLeft > 0 ? "[" + timeLeft + "s]" : "");

        string mainText = Timed(power.Truthy("main"), !wires.Truthy("main_1") || !wires.Truthy("main_2"), power.Num("main_timeleft"));
        string backupText = Timed(power.Truthy("backup"), !wires.Truthy("backup_1") || !wires.Truthy("backup_2"), power.Num("backup_timeleft"));
        double shockLeft = data.Num("shock_timeleft");
        string shockText = (shock == 2 ? "Safe" : "Electrified") + " "
            + (!wires.Truthy("shock") ? cut : shockLeft > 0 ? "[" + shockLeft + "s]" : shockLeft == -1 ? "[Permanent]" : "");

        var powerSection = new UiNode(UiKind.Section) { Key = "power", Title = "Power Status" }.With(new UiNode(UiKind.LabeledList).With(
            Item("main", "Main", statusMain.Color, new UiNode(UiKind.Button)
            {
                Key = "disrupt-main",
                Icon = "lightbulb-o",
                Disabled = !power.Truthy("main"),
                Text = "Disrupt",
                OnClick = () => c.Act("disrupt-main"),
            }, mainText),
            Item("backup", "Backup", statusBackup.Color, new UiNode(UiKind.Button)
            {
                Key = "disrupt-backup",
                Icon = "lightbulb-o",
                Disabled = !power.Truthy("backup"),
                Text = "Disrupt",
                OnClick = () => c.Act("disrupt-backup"),
            }, backupText),
            Item("electrify", "Electrify", statusElectrify.Color, new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Button) { Key = "restore", Icon = "wrench", Disabled = !(wires.Truthy("shock") && shock == 0), Text = "Restore", OnClick = () => c.Act("shock-restore") },
                new UiNode(UiKind.Button) { Key = "temp", Icon = "bolt", Disabled = !wires.Truthy("shock"), Text = "Temporary", OnClick = () => c.Act("shock-temp") },
                new UiNode(UiKind.Button) { Key = "perm", Icon = "bolt", Disabled = !wires.Truthy("shock"), Text = "Permanent", OnClick = () => c.Act("shock-perm") }),
                shockText)));

        bool locked = data.Truthy("locked");
        bool welded = data.Truthy("welded");
        bool opened = data.Truthy("opened");
        string doorText = locked || welded
            ? "[Door is " + (locked ? "bolted" : "") + (locked && welded ? " and " : "") + (welded ? "welded" : "") + "!]"
            : null;

        var accessSection = new UiNode(UiKind.Section) { Key = "access", Title = "Access and Door Control" }.With(new UiNode(UiKind.LabeledList).With(
            Item("idscan", "ID Scan", "bad", Toggle(c, "idscan", data.Truthy("id_scanner"), !wires.Truthy("id_scanner"), "idscan-toggle"),
                !wires.Truthy("id_scanner") ? cut : null),
            Item("emergency", "Emergency Access", null, Toggle(c, "emergency", data.Truthy("emergency"), false, "emergency-toggle"), null),
            new UiNode(UiKind.Divider) { Key = "div1" },
            Item("bolts", "Door Bolts", "bad", new UiNode(UiKind.Button)
            {
                Key = "bolts",
                Icon = locked ? "lock" : "unlock",
                Text = locked ? "Lowered" : "Raised",
                Selected = locked,
                Disabled = !wires.Truthy("bolts"),
                OnClick = () => c.Act("bolt-toggle"),
            }, !wires.Truthy("bolts") ? cut : null),
            Item("feedback", "Door Feedback", "bad", Toggle(c, "feedback", data.Truthy("feedback"), !wires.Truthy("feedback"), "light-toggle"),
                !wires.Truthy("feedback") ? cut : null),
            Item("safe", "Door Force Sensors", "bad", Toggle(c, "safe", data.Truthy("safe"), !wires.Truthy("safe"), "safe-toggle"),
                !wires.Truthy("safe") ? cut : null),
            Item("speed", "Door Timing Safety", "bad", Toggle(c, "speed", data.Truthy("speed"), !wires.Truthy("timing"), "speed-toggle"),
                !wires.Truthy("timing") ? cut : null),
            new UiNode(UiKind.Divider) { Key = "div2" },
            Item("door", "Door Control", "bad", new UiNode(UiKind.Button)
            {
                Key = "openclose",
                Icon = opened ? "sign-out-alt" : "sign-in-alt",
                Text = opened ? "Open" : "Closed",
                Selected = opened,
                Disabled = locked || welded,
                OnClick = () => c.Act("open-close"),
            }, doorText)));

        // <Window width={500} height={390}>
        return c.Window(500, 390, null, powerSection, accessSection);
    }
}
