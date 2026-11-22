using System;
using System.Collections.Generic;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SkillStation.jsx (gaps SL1, D333): the Skillsoft
// station's inserted chip (implant, eject) and the occupant's implanted chips (extract or destroy, activate or deactivate), with the
// operation's time left (library/skill_learning/skill_station.dm:255 ui_data). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class SkillStation
{
    // tg: SkillStation.jsx:259
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool working = data.Truthy("working");
        string error = data.Str("error");
        var content = new List<UiNode>();
        if (!string.IsNullOrEmpty(error))
            content.Add(new UiNode(UiKind.NoticeBox) { Key = "error", Text = error });
        if (working)
            content.Add(new UiNode(UiKind.NoticeBox) { Key = "working", Color = "danger" }.With(
                new UiNode(UiKind.Stack) { Key = "working_column", Vertical = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "warning", Text = "Operation in progress. Please do not leave the chamber." },
                    new UiNode(UiKind.StackItem) { Key = "timeleft", Text = "Time Left: " + TimeFormat(data.Num("timeleft")) })));
        UiNode inserted = InsertedSkillchip(c);
        if (inserted != null)
            content.Add(inserted);
        content.Add(ImplantedSkillchips(c));
        // tg: SkillStation.jsx:263  <Window title="Skillsoft Station" width={500} height={500}>
        return c.Window(500, 500, "Skillsoft Station", new UiNode(UiKind.Section) { Key = "content", Fill = true, Scrollable = true }.With(content.ToArray()));
    }

    // tg: SkillStation.jsx:17
    private static UiNode InsertedSkillchip(TguiContext c)
    {
        UiValue data = c.Data;
        bool working = data.Truthy("working");
        if (!data.Truthy("skillchip_ready"))
            return working ? null : new UiNode(UiKind.NoticeBox) { Key = "insert", Color = "info", Text = "Please insert a skillchip." };

        bool implantable = data.Truthy("implantable");
        string reason = data.Str("implantable_reason");
        double slotUse = data.Num("slot_use");
        double slotsUsed = data.Num("slots_used");
        double slotsMax = data.Num("slots_max");
        var rows = new List<UiNode>
        {
            new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Skillchip" }.With(new UiNode(UiKind.Box) { Bold = true, Text = data.Str("skill_name") }),
            new UiNode(UiKind.LabeledItem) { Key = "desc", Label = "Description" }.With(new UiNode(UiKind.Box) { Text = data.Str("skill_desc") }),
            new UiNode(UiKind.LabeledItem) { Key = "complexity", Label = "Complexity" }.With(
                new UiNode(UiKind.Icon) { Icon = "brain" }, new UiNode(UiKind.Box) { Text = " " + data.Str("complexity") }),
            new UiNode(UiKind.LabeledItem) { Key = "slot", Label = "Slot Size" }.With(
                new UiNode(UiKind.Box) { Color = slotsUsed + slotUse > slotsMax ? "red" : null }.With(
                    new UiNode(UiKind.Icon) { Icon = "save" }, new UiNode(UiKind.Box) { Text = " " + data.Str("slot_use") })),
        };
        if (!string.IsNullOrEmpty(reason))
            rows.Add(new UiNode(UiKind.LabeledItem) { Key = "reason", Label = "Error", Color = implantable ? "good" : "bad", Text = reason });

        return new UiNode(UiKind.Section)
        {
            Key = "inserted",
            Title = "Inserted Skillchip",
            Buttons = new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Button)
                {
                    Key = "implant",
                    Icon = "syringe",
                    Disabled = !implantable || working,
                    Color = implantable ? "good" : "default",
                    Text = "Implant",
                    Tooltip = string.IsNullOrEmpty(reason) ? null : reason,
                    OnClick = () => c.Act("implant"),
                },
                new UiNode(UiKind.Button) { Key = "eject", Icon = "eject", Disabled = working, Text = "Eject", OnClick = () => c.Act("eject") }),
        }.With(
            new UiNode(UiKind.Stack) { Key = "body", Fill = true, Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "icon" }.With(new UiNode(UiKind.Icon) { Icon = data.Str("skill_icon"), Size = 3 }),
                new UiNode(UiKind.StackItem) { Key = "list", Grow = 1 }.With(new UiNode(UiKind.LabeledList).With(rows.ToArray()))));
    }

    // tg: SkillStation.jsx:98
    private static UiNode ImplantedSkillchips(TguiContext c)
    {
        UiValue data = c.Data;
        bool working = data.Truthy("working");
        double complexityUsed = data.Num("complexity_used");
        double complexityMax = data.Num("complexity_max");
        IReadOnlyList<UiValue> current = data.Arr("current");
        var section = new UiNode(UiKind.Section) { Key = "implanted", Title = "Implanted Skillchips" };
        if (current.Count == 0)
            return section.With(new UiNode(UiKind.Box) { Key = "none", Text = "No skillchips detected." });

        var rows = new List<UiNode>
        {
            new UiNode(UiKind.TableRow) { Key = "head", Bold = true }.With(
                new UiNode(UiKind.TableCell) { Key = "chip", Text = "Chip" },
                HeadButton("complexity", "brain", "Complexity", $"{data.Str("complexity_used")}/{data.Str("complexity_max")}"),
                HeadButton("slots", "save", "Slot Size", $"{data.Str("slots_used")}/{data.Str("slots_max")}"),
                HeadButton("active", "check", "Is Active", null),
                HeadButton("cooldown", "hourglass-half", "Cooldown", null),
                HeadButton("actions", "tasks", "Actions", null)),
        };
        foreach (UiValue skill in current)
        {
            string chipRef = skill.Str("ref");
            bool active = skill.Truthy("active");
            bool removable = skill.Truthy("removable");
            double complexity = skill.Num("complexity");
            double cooldown = skill.Num("cooldown");
            bool onCooldown = cooldown != 0;
            string activeError = skill.Str("active_error");
            string complexityColor = active ? "good" : complexity + complexityUsed > complexityMax ? "bad" : "grey";
            rows.Add(new UiNode(UiKind.TableRow) { Key = "chip_" + chipRef }.With(
                new UiNode(UiKind.TableCell) { Key = "name" }.With(
                    new UiNode(UiKind.Icon) { Icon = skill.Str("icon") }, new UiNode(UiKind.Box) { Text = " " + skill.Str("name") }),
                new UiNode(UiKind.TableCell) { Key = "complexity", Bold = true, Color = complexityColor, TextAlign = "center", Text = skill.Str("complexity") },
                new UiNode(UiKind.TableCell) { Key = "slot", Bold = true, Color = "good", TextAlign = "center", Text = skill.Str("slot_use") },
                new UiNode(UiKind.TableCell) { Key = "active", TextAlign = "center" }.With(
                    new UiNode(UiKind.Icon) { Icon = active ? "check" : "times", Color = active ? "good" : "bad" }),
                new UiNode(UiKind.TableCell) { Key = "cooldown", TextAlign = "center", Text = cooldown > 0 ? $"{Math.Ceiling(cooldown / 10)}s" : "0s" },
                new UiNode(UiKind.TableCell) { Key = "actions", TextAlign = "center" }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "remove",
                        Icon = removable ? "eject" : "trash",
                        Color = removable ? "good" : "bad",
                        Tooltip = removable ? "Extract" : "Destroy",
                        Disabled = onCooldown || working,
                        OnClick = () => c.Act("remove", ("ref", chipRef)),
                    },
                    new UiNode(UiKind.Button)
                    {
                        Key = "toggle",
                        Icon = active ? "check-square-o" : "square-o",
                        Color = active ? "good" : "default",
                        Tooltip = !string.IsNullOrEmpty(activeError) && !active ? activeError : active ? "Deactivate" : "Activate",
                        Disabled = onCooldown || working || (!active && complexity + complexityUsed > complexityMax),
                        OnClick = () => c.Act("toggle_activate", ("ref", chipRef)),
                    })));
        }
        return section.With(new UiNode(UiKind.Table) { Key = "table" }.With(rows.ToArray()));
    }

    // tg: SkillStation.jsx:110  the header's transparent icon buttons
    private static UiNode HeadButton(string key, string icon, string tooltip, string text) =>
        new UiNode(UiKind.TableCell) { Key = key, TextAlign = "center" }.With(
            new UiNode(UiKind.Button) { Key = key + "_button", Color = "transparent", Icon = icon, Tooltip = tooltip, Text = text });

    // tg: SkillStation.jsx:241  TimeFormat: hh:mm:ss of a deciseconds value
    public static string TimeFormat(double value)
    {
        string seconds = Math.Floor(value / 10 % 60).ToString("00");
        string minutes = Math.Floor(value / (10 * 60) % 60).ToString("00");
        string hours = Math.Floor(value / (10 * 60 * 60) % 24).ToString("00");
        return $"{hours}:{minutes}:{seconds}";
    }
}
