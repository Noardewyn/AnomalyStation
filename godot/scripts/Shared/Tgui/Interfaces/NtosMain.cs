using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosMain.tsx (M9 slice E: NtOS's main menu). Engine-free
// (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosMain
{
    // tg: NtosMain.tsx:9  enum alert_relevancies
    private const int AlertRelevancySafe = 0;
    private const int AlertRelevancyPertinent = 2;

    // tg: NtosMain.tsx:15
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        int alertStyle = (int)data.Num("alert_style");
        string alertColor = data.Str("alert_color");
        string alertName = data.Str("alert_name");
        string theme = data.Str("PC_device_theme");
        bool showImprint = data.Truthy("show_imprint");
        IReadOnlyList<UiValue> programs = data.Get("programs")?.Items ?? Array.Empty<UiValue>();
        bool hasLight = data.Truthy("has_light");
        bool lightOn = data.Truthy("light_on");
        string lightColor = data.Str("comp_light_color");
        IReadOnlyList<UiValue> removableMedia = data.Get("removable_media")?.Items ?? Array.Empty<UiValue>();
        UiValue login = data.Get("login");
        UiValue proposed = data.Get("proposed_login");
        bool pai = data.Get("pai") is UiValue p && p.Truthy;
        var headerPrograms = programs.Where(app => app.Truthy("header_program")).ToList();

        var content = new List<UiNode>();
        if (removableMedia.Count > 0 || headerPrograms.Count > 0)
        {
            var apps = headerPrograms.Select(app => new UiNode(UiKind.StackItem) { Key = "header:" + app.Str("name") }.With(new UiNode(UiKind.Button)
            {
                Key = "run",
                Text = app.Str("desc"),
                Icon = app.Str("icon"),
                OnClick = () => c.Act("PC_runprogram", ("name", app.Str("name"))),
            })).ToList();
            apps.Add(new UiNode(UiKind.StackItem) { Key = "alert" }.With(new UiNode(UiKind.Button)
            {
                Key = "alertlevel",
                Color = alertStyle == AlertRelevancySafe ? "transparent" : alertColor,
                // textColor (alert_color on a safe level, else black): the kit's button has no text colour; the level's name shows
                Tooltip = "The current alert level. Indicator becomes more intense when there is a threat, moreso if your department is responsible for handling it.",
                Text = alertName,
                Selected = alertStyle == AlertRelevancyPertinent, // tgui's alertBlink
            }));
            content.Add(new UiNode(UiKind.Section) { Key = "top" }.With(
                new UiNode(UiKind.Stack).With(apps),
                new UiNode(UiKind.Stack).With(removableMedia.Select(device => new UiNode(UiKind.StackItem) { Key = "media:" + device.Text }.With(new UiNode(UiKind.Button)
                {
                    Key = "eject",
                    Fluid = true,
                    Icon = "eject",
                    Text = device.Text,
                    Disabled = string.IsNullOrEmpty(device.Text),
                    OnClick = () => c.Act("PC_Eject_Disk", ("name", device.Text)),
                })))));
        }

        var buttons = new List<UiNode>();
        if (hasLight)
        {
            buttons.Add(new UiNode(UiKind.Button) { Key = "lightcolor", OnClick = () => c.Act("PC_light_color") }.With(new UiNode(UiKind.ColorBox) { Key = "colorbox", Color = lightColor }));
            buttons.Add(new UiNode(UiKind.Button) { Key = "light", Icon = "lightbulb", Color = lightOn ? "good" : "bad", Selected = lightOn, OnClick = () => c.Act("PC_toggle_light") });
        }
        buttons.Add(new UiNode(UiKind.Button) { Key = "ejectid", Icon = "eject", Text = "Eject ID", Disabled = !proposed.Truthy("IDInserted"), OnClick = () => c.Act("PC_Eject_Disk", ("name", "ID")) });
        if (showImprint)
        {
            string proposedName = proposed.Str("IDName");
            bool same = proposedName == login.Str("IDName") && proposed.Str("IDJob") == login.Str("IDJob");
            buttons.Add(new UiNode(UiKind.Button) { Key = "imprint", Icon = "dna", Text = "Imprint ID", Disabled = string.IsNullOrEmpty(proposedName) || same, OnClick = () => c.Act("PC_Imprint_ID", ("name", "ID")) });
        }

        string Line(string field)
        {
            string proposedValue = proposed.Str(field);
            return showImprint
                ? login.Str(field) + " " + (string.IsNullOrEmpty(proposedValue) ? "" : "(" + proposedValue + ")")
                : proposedValue ?? "";
        }

        content.Add(new UiNode(UiKind.Section) { Key = "details", Title = "Details", Buttons = new UiNode(UiKind.Box).With(buttons) }.With(
            new UiNode(UiKind.Table).With(
                new UiNode(UiKind.TableRow) { Key = "idname" }.With(new UiNode(UiKind.TableCell) { Key = "idname", Text = "ID Name: " + Line("IDName") }),
                new UiNode(UiKind.TableRow) { Key = "assignment" }.With(new UiNode(UiKind.TableCell) { Key = "assignment", Text = "Assignment: " + Line("IDJob") }))));

        if (pai)
        {
            content.Add(new UiNode(UiKind.Section) { Key = "pai", Title = "pAI" }.With(new UiNode(UiKind.Table).With(
                new UiNode(UiKind.TableRow) { Key = "ejectpai" }.With(new UiNode(UiKind.TableCell).With(new UiNode(UiKind.Button) { Key = "ejectpai", Fluid = true, Icon = "eject", Color = "transparent", Text = "Eject pAI", OnClick = () => c.Act("PC_Pai_Interact", ("option", "eject")) })),
                new UiNode(UiKind.TableRow) { Key = "configpai" }.With(new UiNode(UiKind.TableCell).With(new UiNode(UiKind.Button) { Key = "configpai", Fluid = true, Icon = "cat", Color = "transparent", Text = "Configure pAI", OnClick = () => c.Act("PC_Pai_Interact", ("option", "interact")) })))));
        }

        // tg: NtosMain.tsx:205  ProgramsTable
        content.Add(new UiNode(UiKind.Section) { Key = "programs", Title = "Programs" }.With(new UiNode(UiKind.Table).With(
            programs.Where(program => !program.Truthy("header_program")).Select(program => new UiNode(UiKind.TableRow) { Key = "program:" + program.Str("name") }.With(
                new UiNode(UiKind.TableCell).With(new UiNode(UiKind.Button)
                {
                    Key = "run",
                    Fluid = true,
                    Color = program.Truthy("alert") ? "yellow" : "transparent",
                    Icon = program.Str("icon"),
                    Text = program.Str("desc"),
                    OnClick = () => c.Act("PC_runprogram", ("name", program.Str("name"))),
                }),
                new UiNode(UiKind.TableCell) { Width = "18px" } /* collapsing */.With(program.Truthy("running")
                    ? new UiNode(UiKind.Button) { Key = "kill", Color = "transparent", Icon = "times", Tooltip = "Close program", OnClick = () => c.Act("PC_killprogram", ("name", program.Str("name"))) }
                    : null))))));

        // <NtosWindow.Content scrollable>: one growing, scrolled item
        return NtosWindow.Render(c, 400, 500, theme == "syndicate" ? "Syndix Main Menu" : "NtOS Main Menu",
            new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Grow = 1, Scrollable = true }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(content))));
    }
}
