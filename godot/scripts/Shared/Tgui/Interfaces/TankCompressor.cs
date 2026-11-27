using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/TankCompressor.tsx: the tank section (its notice by
// pressure and leak, the pressure gauge, the flow rate knob with its max and reset buttons, the compressor switch, the eject
// button), and the disk section with the gas records as vertical tabs: the selected record's title, time, source, gases and its
// save and delete buttons. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class TankCompressor
{
    // tg: TankCompressor.tsx:52
    public static UiNode Render(TguiContext c) => c.Window(440, 440, "Tank Compressor", TankCompressorContent(c));

    // tg: TankCompressor.tsx:62
    private static UiNode TankCompressorContent(TguiContext c)
    {
        UiValue data = c.Data;
        string disk = data.Str("disk", null);
        string storage = data.Str("storage");
        return new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            TankCompressorControls(c),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Section)
            {
                Key = "disk",
                Scrollable = true,
                Fill = true,
                Title = !string.IsNullOrEmpty(disk) ? disk + " (" + storage + ")" : "No Disk Inserted",
                Buttons = new UiNode(UiKind.Button) { Key = "eject_disk", Icon = "eject", Disabled = string.IsNullOrEmpty(disk), Text = "Eject Disk", OnClick = () => c.Act("eject_disk") },
            }.With(TankCompressorRecords(c))));
    }

    // tg: TankCompressor.tsx:93
    private static UiNode TankCompressorControls(TguiContext c)
    {
        UiValue data = c.Data;
        bool tankPresent = data.Truthy("tankPresent");
        bool leaking = data.Truthy("leaking");
        double lastPressure = data.Num("lastPressure");
        double leakPressure = data.Num("leakPressure");
        double fragmentPressure = data.Num("fragmentPressure");
        double tankPressure = data.Num("tankPressure");
        double maxTransfer = data.Num("maxTransfer");
        bool active = data.Truthy("active");
        double transferRate = data.Num("transferRate");
        double ejectPressure = data.Num("ejectPressure");
        double pressure = tankPresent ? tankPressure : lastPressure;
        bool usingLastData = lastPressure != 0 && !tankPresent;
        string noticeColor = usingLastData || leaking || pressure > fragmentPressure ? "bad"
            : !tankPresent ? "blue"
            : pressure > leakPressure ? "average"
            : "good";
        string noticeText = usingLastData ? "Tank destroyed. Displaying last recorded data."
            : !tankPresent ? "No Tank Detected"
            : leaking ? "Tank Leaking"
            : pressure == 0 ? "No Pressure Detected"
            : pressure < leakPressure ? "Tank Pressure Nominal"
            : pressure < fragmentPressure ? "Leak Hazard"
            : "Explosive Hazard";

        return new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Section)
        {
            Key = "tank",
            Title = "Tank",
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "eject_tank", Icon = "eject", Disabled = !tankPresent || tankPressure > ejectPressure, Text = "Eject Tank", OnClick = () => c.Act("eject_tank"),
            },
        }.With(
            new UiNode(UiKind.NoticeBox) { Key = "notice", Color = noticeColor, Text = noticeText },
            new UiNode(UiKind.LabeledControls).With(
                new UiNode(UiKind.LabeledControl) { Key = "pressure", Label = "Pressure" }.With(new UiNode(UiKind.RoundGauge)
                {
                    Key = "gauge",
                    Size = 2.5,
                    Value = pressure,
                    MinValue = 0,
                    MaxValue = fragmentPressure * 1.15,
                    AlertAfter = leakPressure,
                    Ranges = new[]
                    {
                        ("good", 0.0, leakPressure),
                        ("average", leakPressure, fragmentPressure),
                        ("bad", fragmentPressure, fragmentPressure * 1.15),
                    },
                    Format = Canister.FormatPressure,
                }),
                new UiNode(UiKind.LabeledControl) { Key = "flow", Label = "Flow rate" }.With(
                    new UiNode(UiKind.Knob)
                    {
                        Key = "rate", Size = 2, Value = transferRate, Unit = "Liters/sec.", MinValue = 0, MaxValue = maxTransfer, Step = 1, StepPixelSize = 8,
                        OnChange = value => c.Act("change_rate", ("target", value)),
                    },
                    new UiNode(UiKind.Button) { Key = "rate-max", Color = "transparent", Icon = "fast-forward", OnClick = () => c.Act("change_rate", ("target", maxTransfer)) },
                    new UiNode(UiKind.Button) { Key = "rate-reset", Color = "transparent", Icon = "undo", OnClick = () => c.Act("change_rate", ("target", 0)) }),
                new UiNode(UiKind.LabeledControl) { Key = "compressor", Label = "Compressor" }.With(new UiNode(UiKind.Button)
                {
                    Key = "toggle_injection", FontSize = "18px", Icon = "power-off", Disabled = !tankPresent || (leaking && pressure < leakPressure), Selected = active,
                    Text = active ? "On" : "Off", OnClick = () => c.Act("toggle_injection"),
                }))));
    }

    // tg: TankCompressor.tsx:219
    private static UiNode TankCompressorRecords(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> records = data.Arr("records");
        string disk = data.Str("disk", null);
        string activeRecordRef = c.UseState("recordRef", records.Count > 0 ? records[0].Str("ref") : null);
        UiValue activeRecord = string.IsNullOrEmpty(activeRecordRef) ? null : records.FirstOrDefault(record => record.Str("ref") == activeRecordRef);
        if (records.Count == 0)
            return new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.NoticeBox) { Key = "no-records", Text = "No Records" });

        UiNode tabs = new UiNode(UiKind.Tabs) { Key = "tabs", Vertical = true }.With(records.Select(record =>
        {
            string @ref = record.Str("ref");
            return new UiNode(UiKind.Tab)
            {
                Key = "tab-" + record.Str("name"), Icon = "file", Selected = @ref == activeRecordRef, Text = record.Str("name"),
                OnClick = () => c.SetState("recordRef", @ref),
            };
        }));
        UiNode detail;
        if (activeRecord != null)
        {
            string @ref = activeRecord.Str("ref");
            UiNode gases = new UiNode(UiKind.LabeledList) { Key = "gases" }.With(activeRecord.Obj("gases").Select(gas =>
            {
                double amount = gas.Value.AsNum();
                return new UiNode(UiKind.LabeledItem) { Key = "gas-" + gas.Key, Label = gas.Key, Text = (amount != 0 ? TguiFormat.ToFixed(amount, 2) : "-") + " moles" };
            }));
            detail = new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.LabeledList) { Key = "record" }.With(
                new UiNode(UiKind.LabeledItem) { Key = "title", Label = "Title", Text = activeRecord.Str("name") },
                new UiNode(UiKind.LabeledItem) { Key = "time", Label = "Time", Text = activeRecord.Str("timestamp") },
                new UiNode(UiKind.LabeledItem) { Key = "source", Label = "Source", Text = activeRecord.Str("source") },
                new UiNode(UiKind.LabeledItem) { Key = "gases-row", Label = "Gases" }.With(gases),
                new UiNode(UiKind.LabeledItem) { Key = "actions", Label = "Actions" }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "save_record", Icon = "floppy-disk", Text = "Save to Disk", Disabled = string.IsNullOrEmpty(disk),
                        Tooltip = "Save the record selected to an inserted data disk.", OnClick = () => c.Act("save_record", ("ref", @ref)),
                    },
                    c.ButtonConfirm("delete_record", new UiNode(UiKind.Button)
                    {
                        Icon = "trash", Color = "bad", OnClick = () => c.Act("delete_record", ("ref", @ref)),
                    }))));
        }
        else
            detail = new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.NoticeBox) { Key = "no-selection", Text = "No Record Selected" });

        return new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Stack) { Fill = true }.With(new UiNode(UiKind.StackItem).With(tabs), detail));
    }
}
