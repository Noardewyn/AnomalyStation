using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/Holodeck.jsx: the holodeck console's default programs
// (each name without its "Holodeck - " prefix) with the Safeties button, and the dangerous programs once it is emagged. Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class Holodeck
{
    // tg: Holodeck.jsx:6
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool canToggleSafety = data.Truthy("can_toggle_safety");
        bool emagged = data.Truthy("emagged");
        string program = data.Str("program");
        var defaults = new UiNode(UiKind.Section)
        {
            Key = "default",
            Title = "Default Programs",
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "safety", Icon = emagged ? "unlock" : "lock", Text = "Safeties", Color = "bad", Disabled = !canToggleSafety, Selected = !emagged,
                OnClick = () => c.Act("safety"),
            },
        };
        foreach (UiValue def in data.Arr("default_programs"))
            defaults.Children.Add(ProgramButton(c, def, program, null));
        var content = new UiNode(UiKind.Box) { Key = "content", Scrollable = true }.With(defaults);
        if (emagged)
        {
            var dangerous = new UiNode(UiKind.Section) { Key = "emag", Title = "Dangerous Programs" };
            foreach (UiValue emag in data.Arr("emag_programs"))
                dangerous.Children.Add(ProgramButton(c, emag, program, "bad"));
            content.Children.Add(dangerous);
        }
        return c.Window(400, 500, null, content);
    }

    // tg: Holodeck.jsx:27, :45
    private static UiNode ProgramButton(TguiContext c, UiValue entry, string program, string color)
    {
        string id = entry.Str("id");
        string name = entry.Str("name");
        return new UiNode(UiKind.Button)
        {
            Key = "program:" + id, Fluid = true, Text = name.Length > 11 ? name.Substring(11) : "", TextAlign = "center", Color = color,
            Selected = id == program, OnClick = () => c.Act("load_program", ("id", id)),
        };
    }
}
