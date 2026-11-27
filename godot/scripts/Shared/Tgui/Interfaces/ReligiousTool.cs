using System.Collections.Generic;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ReligiousTool.jsx: the altar of the gods' window
// (/datum/component/religious_tool's ui_data, religious_tool.dm:110): the sect choice, then the sect's page and its rites. The
// Dimmer over an empty rite list is a centred box (as AntagInfoChangeling's). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ReligiousTool
{
    private const string TabKey = "tab";

    // tg: ReligiousTool.jsx:16
    private static readonly Dictionary<string, string> ALIGNMENT2COLOR = new()
    {
        ["good"] = "yellow",
        ["neutral"] = "white",
        ["evil"] = "red",
    };

    private static string AlignmentColor(string alignment) => alignment != null && ALIGNMENT2COLOR.TryGetValue(alignment, out string color) ? color : null;

    // tg: ReligiousTool.jsx:22
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        int tab = c.UseState(TabKey, 1);
        bool hasSects = data.Field("sects") is { Kind: UiValueKind.Array };
        string alignment = data.Str("alignment");

        var tabs = new UiNode(UiKind.Tabs) { Key = "tabs", TextAlign = "center", Fluid = true }.With(
            new UiNode(UiKind.Tab) { Key = "tab:sect", Text = "Sect ", Icon = "place-of-worship", Color = AlignmentColor(alignment), Selected = tab == 1, OnClick = () => c.SetState(TabKey, 1) });
        // tg: ReligiousTool.jsx:40  {!sects && <Tabs.Tab ...>Rites <Icon name="pray" /></Tabs.Tab>}
        if (!hasSects)
            tabs.With(new UiNode(UiKind.Tab) { Key = "tab:rites", Text = "Rites ", Icon = "pray", Color = AlignmentColor(alignment), Selected = tab == 2, OnClick = () => c.SetState(TabKey, 2) });

        UiNode body = null;
        if (tab == 1)
            body = hasSects ? SectSelectTab(c) : SectTab(c);
        else if (tab == 2)
            body = RiteTab(c);

        // tg: ReligiousTool.jsx:28  <Window title={toolname} width={560} height={500}><Window.Content scrollable>
        return c.Window(560, 500, data.Str("toolname"),
            new UiNode(UiKind.Stack) { Key = "root", Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "tabs" }.With(tabs),
                new UiNode(UiKind.StackItem) { Key = "body", Grow = 1 }.With(body)));
    }

    // tg: ReligiousTool.jsx:58
    private static UiNode SectTab(TguiContext c)
    {
        UiValue data = c.Data;
        string alignment = data.Str("alignment");
        string icon = data.Str("icon");
        string deity = data.Str("deity");
        string wanted = data.Str("wanted");
        double favor = data.Num("favor");
        return new UiNode(UiKind.Section) { Key = "sect", Fill = true }.With(
            new UiNode(UiKind.Stack) { Key = "stack", Fill = true, Vertical = true, FontSize = "15px", TextAlign = "center" }.With(
                // tg: ReligiousTool.jsx:73  <Icon name={icon} /> {` ${name} `} <Icon name={icon} />
                new UiNode(UiKind.StackItem) { Key = "name", FontSize = "32px" }.With(
                    new UiNode(UiKind.Icon) { Key = "icon-left", Icon = icon, Color = AlignmentColor(alignment) },
                    new UiNode(UiKind.Box) { Key = "text", Text = " " + data.Str("name") + " " },
                    new UiNode(UiKind.Icon) { Key = "icon-right", Icon = icon, Color = AlignmentColor(alignment) }),
                new UiNode(UiKind.StackItem) { Key = "quote", Grow = 1, Color = "grey", Text = "\"" + data.Str("quote") + "\"" },
                new UiNode(UiKind.StackItem) { Key = "favordesc", Color = favor == 0 ? "white" : "green", Text = data.Str("favordesc") },
                new UiNode(UiKind.StackItem) { Key = "desc", TextAlign = "left" }.With(BlockQuote("blockquote", data.Str("desc"))),
                new UiNode(UiKind.StackItem) { Key = "wanted" }.With(
                    new UiNode(UiKind.Section) { Key = "sacrifices", Title = "Wanted Sacrifices" }.With(
                        new UiNode(UiKind.Box)
                        {
                            Key = "wanted-text",
                            Text = string.IsNullOrEmpty(wanted) ? deity + " doesn't want any sacrifices." : deity + " wishes for " + wanted + ".",
                        }))));
    }

    // tg: ReligiousTool.jsx:100
    private static UiNode SectSelectTab(TguiContext c)
    {
        var stack = new UiNode(UiKind.Stack) { Key = "sects", Vertical = true };
        foreach (UiValue sect in c.Data.Arr("sects"))
        {
            string name = sect.Str("name");
            string path = sect.Str("path");
            // tg: ReligiousTool.jsx:106  <Collapsible title={<Stack><Icon /><Stack.Item grow>{sect.name}</Stack.Item><Stack.Item italic>"quote"</Stack.Item></Stack>}>
            stack.With(
                new UiNode(UiKind.Collapsible)
                {
                    Key = "sect:" + path,
                    Title = name + "  \"" + sect.Str("quote") + "\"",
                    Icon = sect.Str("icon"),
                    Color = "transparent",
                }.With(
                    new UiNode(UiKind.StackItem) { Key = "desc" }.With(
                        new UiNode(UiKind.Box) { Key = "text", Text = sect.Str("desc") },
                        new UiNode(UiKind.Button)
                        {
                            Key = "select",
                            TextAlign = "center",
                            Icon = "plus",
                            Fluid = true,
                            Text = "Select " + name,
                            OnClick = () => c.Act("sect_select", ("path", path)),
                        })),
                new UiNode(UiKind.Divider) { Key = "divider:" + path });
        }
        return new UiNode(UiKind.Section) { Key = "select", Fill = true, Title = "Sect Select", Scrollable = true }.With(stack);
    }

    // tg: ReligiousTool.jsx:147
    private static UiNode RiteTab(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> rites = data.Arr("rites");
        string alignment = data.Str("alignment");
        string deity = data.Str("deity");
        double favor = data.Num("favor");
        var root = new UiNode(UiKind.Fragment) { Key = "rites" };
        // tg: ReligiousTool.jsx:152  {!rites.length && <Section fill><Dimmer>...{deity} does not have any invocations.</Dimmer></Section>}
        if (rites.Count == 0)
        {
            root.With(new UiNode(UiKind.Section) { Key = "empty", Fill = true }.With(
                new UiNode(UiKind.Stack) { Key = "dimmer", Vertical = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "icon", TextAlign = "center" }.With(
                        new UiNode(UiKind.Icon) { Key = "icon", Icon = data.Str("icon"), Color = AlignmentColor(alignment), Size = 10 }),
                    new UiNode(UiKind.StackItem) { Key = "text", FontSize = "18px", Color = AlignmentColor(alignment), Text = deity + " does not have any invocations." })));
        }
        var list = new UiNode(UiKind.Stack) { Key = "list", Vertical = true };
        foreach (UiValue rite in rites)
        {
            string path = rite.Str("path");
            double cost = rite.Num("favor");
            list.With(new UiNode(UiKind.StackItem) { Key = "rite:" + path }.With(
                new UiNode(UiKind.Section)
                {
                    Key = "section",
                    Title = rite.Str("name"),
                    Buttons = new UiNode(UiKind.Button)
                    {
                        Key = "invoke",
                        Color = "transparent",
                        Icon = "arrow-right",
                        Text = "Invoke",
                        Disabled = favor < cost,
                        OnClick = () => c.Act("perform_rite", ("path", path)),
                    },
                }.With(
                    new UiNode(UiKind.Box) { Key = "cost", Color = favor < cost ? "red" : "grey" }.With(
                        new UiNode(UiKind.Icon) { Key = "star", Icon = "star", Color = AlignmentColor(alignment) },
                        new UiNode(UiKind.Box) { Key = "text", Text = " Costs " + TguiFormat.JsNumber(cost) + " favor." }),
                    BlockQuote("desc", rite.Str("desc")))));
        }
        return root.With(list);
    }

    // tgui-core's BlockQuote: a grey, indented block of text.
    private static UiNode BlockQuote(string key, string text) => new UiNode(UiKind.Box) { Key = key, Color = "label", Text = text };
}
