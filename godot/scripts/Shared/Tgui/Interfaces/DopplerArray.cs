using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/DopplerArray.jsx: the doppler array's records as
// vertical tabs, the selected record's timestamp, coordinates, displacement, radii (with the theoretical ones of a capped blast)
// and possible causes, its delete and save buttons, and the disk footer. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class DopplerArray
{
    // tg: DopplerArray.jsx:15
    public static UiNode Render(TguiContext c) => c.Window(650, 320, c.ConfigTitle, DopplerArrayContent(c));

    // tg: DopplerArray.jsx:25
    private static UiNode DopplerArrayContent(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> records = data.Arr("records");
        string disk = data.Str("disk", null);
        string storage = data.Str("storage");
        string activeRecordName = c.UseState("activeRecordrecord", records.Count > 0 ? records[0].Str("name") : null);
        UiValue activeRecord = records.FirstOrDefault(record => record.Str("name") == activeRecordName);

        UiNode footer = new UiNode(UiKind.Section) { Key = "footer", Title = !string.IsNullOrEmpty(disk) ? disk + " (" + storage + ")" : "No Disk Inserted" }.With(
            new UiNode(UiKind.Button)
            {
                Key = "eject_disk", TextAlign = "center", Fluid = true, Icon = "eject", Text = "Eject Disk", Disabled = string.IsNullOrEmpty(disk),
                OnClick = () => c.Act("eject_disk"),
            });

        UiNode body;
        if (records.Count == 0)
            body = new UiNode(UiKind.NoticeBox) { Key = "no-records", Text = "No Records" };
        else
        {
            UiNode tabs = new UiNode(UiKind.Tabs) { Key = "tabs", Vertical = true }.With(records.Select(record =>
            {
                string name = record.Str("name");
                return new UiNode(UiKind.Tab)
                {
                    Key = "tab-" + name, Icon = "file", Selected = name == activeRecordName, Text = name,
                    OnClick = () => c.SetState("activeRecordrecord", name),
                };
            }));
            UiNode detail = activeRecord != null
                ? new UiNode(UiKind.StackItem).With(RecordSection(c, activeRecord, disk))
                : new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.NoticeBox) { Key = "no-selection", Text = "No Record Selected" });
            body = new UiNode(UiKind.Section) { Key = "records" }.With(
                new UiNode(UiKind.Stack).With(new UiNode(UiKind.StackItem).With(tabs), detail));
        }

        return new UiNode(UiKind.Stack) { Vertical = true, Height = "100%" }.With(
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(body),
            new UiNode(UiKind.StackItem).With(footer));
    }

    // tg: DopplerArray.jsx:63
    private static UiNode RecordSection(TguiContext c, UiValue record, string disk)
    {
        string @ref = record.Str("ref");
        IReadOnlyList<UiValue> reactions = record.Arr("reaction_results");
        UiNode causes = reactions.Count > 0
            ? new UiNode(UiKind.Fragment).With(reactions.Select(r => new UiNode(UiKind.Box) { Key = "cause-" + r.AsStr(), Text = r.AsStr() }))
            : new UiNode(UiKind.Box) { Key = "no-cause", Text = "No information available" };
        return new UiNode(UiKind.Section)
        {
            Key = "record",
            Title = record.Str("name"),
            Buttons = new UiNode(UiKind.Fragment).With(
                c.ButtonConfirm("delete_record", new UiNode(UiKind.Button)
                {
                    Icon = "trash", Text = "Delete", Color = "bad", OnClick = () => c.Act("delete_record", ("ref", @ref)),
                }),
                new UiNode(UiKind.Button)
                {
                    Key = "save_record", Icon = "floppy-disk", Text = "Save", Disabled = string.IsNullOrEmpty(disk),
                    Tooltip = "Save the record selected to an inserted data disk.", OnClick = () => c.Act("save_record", ("ref", @ref)),
                }),
        }.With(new UiNode(UiKind.LabeledList).With(
            Item("timestamp", "Timestamp", record.Str("timestamp")),
            Item("coordinates", "Coordinates", record.Str("coordinates")),
            Item("displacement", "Displacement", record.Str("displacement") + " seconds"),
            Item("epicenter", "Epicenter Radius", Radius(record, "epicenter")),
            Item("outer", "Outer Radius", Radius(record, "outer")),
            Item("shockwave", "Shockwave Radius", Radius(record, "shockwave")),
            new UiNode(UiKind.LabeledItem) { Key = "causes", Label = "Possible Cause(s)" }.With(causes)));
    }

    // The factual radius, with " (Theoretical: n)" when the record has a theoretical one (DopplerArray.jsx:102-122).
    private static string Radius(UiValue record, string which)
    {
        string text = record.Str("factual_" + which + "_radius");
        if (record.Truthy("theory_" + which + "_radius"))
            text += " (Theoretical: " + record.Str("theory_" + which + "_radius") + ")";
        return text;
    }

    private static UiNode Item(string key, string label, string text) =>
        new UiNode(UiKind.LabeledItem) { Key = key, Label = label, Text = text };
}
