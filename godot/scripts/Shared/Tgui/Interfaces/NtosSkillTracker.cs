using System;
using System.Collections.Generic;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosSkillTracker.jsx (gaps SL1, D333): ExperTrak, the
// PDA's skill tracker: each skill's level, level and overall progress, and the legendary level's reward button
// (programs/skill_tracker.dm:12 ui_data). tgui-core's BlockQuote is a box. Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosSkillTracker
{
    private static readonly (string Color, double From, double To)[] GoodRange = { ("good", 0.75, 1.0) };

    // tg: NtosSkillTracker.jsx:13
    public static UiNode Render(TguiContext c)
    {
        IReadOnlyList<UiValue> skills = c.Data.Arr("skills");
        var sections = new List<UiNode>();
        for (int idx = 0; idx < skills.Count; idx++)
        {
            UiValue skill = skills[idx];
            string name = skill.Str("name");
            var rows = new List<UiNode>
            {
                new UiNode(UiKind.TableRow) { Key = "head", Bold = true }.With(
                    new UiNode(UiKind.TableCell) { Key = "head_level", TextAlign = "center", Text = "Level" },
                    new UiNode(UiKind.TableCell) { Key = "head_progress", TextAlign = "center", Text = "Level Progress" },
                    new UiNode(UiKind.TableCell) { Key = "head_overall", TextAlign = "center", Text = "Overall Progress" }),
                new UiNode(UiKind.TableRow) { Key = "values", Candystripe = true }.With(
                    new UiNode(UiKind.TableCell) { Key = "level", TextAlign = "center", Text = skill.Str("lvl_name") },
                    Percent("progress", skill.Num("progress_percent")),
                    Percent("overall", skill.Num("overall_percent"))),
            };
            if (skill.Truthy("reward"))
                rows.Add(new UiNode(UiKind.TableRow) { Key = "reward", Candystripe = true }.With(
                    new UiNode(UiKind.TableCell) { Key = "reward_cell", TextAlign = "center" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "reward_button",
                        Icon = "trophy",
                        Text = $"Contact the Professional {skill.Str("title")} Association",
                        OnClick = () => c.Act("PRG_reward", ("skill", name)),
                    })));
            sections.Add(new UiNode(UiKind.Section) { Key = "skill_" + idx, Title = name }.With(
                new UiNode(UiKind.Box) { Key = "desc", Text = skill.Str("desc") },
                new UiNode(UiKind.Section) { Key = "table_section" }.With(new UiNode(UiKind.Table) { Key = "table" }.With(rows.ToArray()))));
        }
        // tg: NtosSkillTracker.jsx:17  <NtosWindow width={500} height={600}>, <NtosWindow.Content scrollable>
        return NtosWindow.Render(c, 500, 600, null, new UiNode(UiKind.Section) { Key = "tracker", Title = "Skill Tracker" }.With(sections.ToArray()));
    }

    // tg: NtosSkillTracker.jsx:37  a percentage bar, or a dash when the value is 0 or absent
    private static UiNode Percent(string key, double value)
    {
        if (value == 0)
            return new UiNode(UiKind.TableCell) { Key = key, TextAlign = "center", Text = "—" };
        return new UiNode(UiKind.TableCell) { Key = key, TextAlign = "center" }.With(new UiNode(UiKind.ProgressBar)
        {
            Key = key + "_bar",
            Value = value,
            Ranges = GoodRange,
            Text = $"{Math.Round(value * 100, MidpointRounding.AwayFromZero)}%",
        });
    }
}
