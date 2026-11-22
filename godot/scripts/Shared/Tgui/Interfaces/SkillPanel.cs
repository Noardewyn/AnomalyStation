using System.Collections.Generic;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/SkillPanel.jsx (gaps SL1, D333): the admins' skill
// panel (the console's skill-panel): each skill's description, level, experience and progress, and the adjust, set experience and
// set level buttons (admin/skill_panel.dm:23 ui_data). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class SkillPanel
{
    // tg: SkillPanel.jsx:22
    public static UiNode Render(TguiContext c)
    {
        IReadOnlyList<UiValue> skills = c.Data.Arr("skills");
        var items = new List<UiNode>();
        foreach (UiValue skill in skills)
        {
            string path = skill.Str("path");
            double lvlnum = skill.Num("lvlnum");
            bool maxxed = skill.Num("exp_req") == 0;
            items.Add(new UiNode(UiKind.LabeledItem) { Key = skill.Str("name"), Label = skill.Str("name") }.With(
                new UiNode(UiKind.Box) { Key = "desc", Color = "#FFDB58", Bold = true, Text = skill.Str("desc") },
                // tg: SkillPanel.jsx:83  Level: the level name in hsl(lvlnum * 50, 50%, 50%)
                new UiNode(UiKind.Stack) { Key = "level" }.With(
                    new UiNode(UiKind.StackItem) { Key = "label", Text = "Level: [" },
                    new UiNode(UiKind.StackItem) { Key = "name", Bold = true, Color = LevelColor(lvlnum), Text = skill.Str("lvl") },
                    new UiNode(UiKind.StackItem) { Key = "close", Text = "]" }),
                new UiNode(UiKind.Box) { Key = "exp", Text = $"Total Experience: [{skill.Str("exp")} XP]" },
                maxxed
                    ? new UiNode(UiKind.Stack) { Key = "next" }.With(
                        new UiNode(UiKind.StackItem) { Key = "label", Text = "XP To Next Level:" },
                        new UiNode(UiKind.StackItem) { Key = "maxxed", Color = "lightgreen", Bold = true, Text = "[MAXXED]" })
                    : new UiNode(UiKind.Box) { Key = "next", Text = $"XP To Next Level:[{skill.Str("exp_prog")} / {skill.Str("exp_req")}]" },
                new UiNode(UiKind.Box) { Key = "overall", Text = $"Overall Skill Progress: [{skill.Str("exp")} / {skill.Str("max_exp")}]" },
                new UiNode(UiKind.ProgressBar) { Key = "progress", Value = skill.Num("exp_percent"), Color = "good" },
                new UiNode(UiKind.Stack) { Key = "buttons" }.With(
                    new UiNode(UiKind.StackItem) { Key = "adj" }.With(new UiNode(UiKind.Button) { Key = "adj_exp", Text = "Adjust Exp", OnClick = () => c.Act("adj_exp", ("skill", path)) }),
                    new UiNode(UiKind.StackItem) { Key = "set" }.With(new UiNode(UiKind.Button) { Key = "set_exp", Text = "Set Exp", OnClick = () => c.Act("set_exp", ("skill", path)) }),
                    new UiNode(UiKind.StackItem) { Key = "lvl" }.With(new UiNode(UiKind.Button) { Key = "set_lvl", Text = "Set Level", OnClick = () => c.Act("set_lvl", ("skill", path)) }))));
        }
        // tg: SkillPanel.jsx:26  <Window title="Manage Skills" width={600} height={500}>; the section's title is skills.playername of the
        // array (undefined in tg: no title)
        return c.Window(600, 500, "Manage Skills", new UiNode(UiKind.Section) { Key = "content", Fill = true, Scrollable = true }.With(
            new UiNode(UiKind.LabeledList) { Key = "skills" }.With(items.ToArray())));
    }

    // tg: SkillPanel.jsx:88  hsl(lvlnum * 50, 50%, 50%) as #rrggbb
    public static string LevelColor(double lvlnum)
    {
        double h = (lvlnum * 50 % 360 + 360) % 360 / 360.0;
        const double s = 0.5, l = 0.5;
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;
        static double Hue(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
        int r = (int)System.Math.Round(Hue(p, q, h + 1.0 / 3) * 255);
        int g = (int)System.Math.Round(Hue(p, q, h) * 255);
        int b = (int)System.Math.Round(Hue(p, q, h - 1.0 / 3) * 255);
        return $"#{r:x2}{g:x2}{b:x2}";
    }
}
