using System.Collections.Generic;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AnomalyRefinery.jsx: the anomaly refinery's
// implosion control (the inserted core with its required radius and the implode button, the valve's giver and target tanks with
// the swap button) and its bomb simulator (the merged gas mix with its valve and react button, and each tank's gas), the eject
// buttons and the tab switch between them. The tab is the window's local state (tgui's shared state of one window). Engine-free.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AnomalyRefinery
{
    private const string TabKey = "exploderTab";

    // tg: AnomalyRefinery.jsx:15
    public static UiNode Render(TguiContext c) => c.Window(550, 350, "Anomaly Refinery", AnomalyRefineryContent(c));

    // tg: AnomalyRefinery.jsx:25
    private static UiNode AnomalyRefineryContent(TguiContext c)
    {
        UiValue data = c.Data;
        int currentTab = c.UseState(TabKey, 1);
        bool core = data.Truthy("core");
        bool valvePresent = data.Truthy("valvePresent");
        bool active = data.Truthy("active");

        var stack = new UiNode(UiKind.Stack) { Vertical = true, Fill = true };
        if (currentTab == 1)
            stack.With(CoreCompressorContent(c));
        if (currentTab == 2)
            stack.With(BombProcessorContent(c));
        stack.With(new UiNode(UiKind.StackItem) { Key = "buttons" }.With(
            new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "eject_core", Fluid = true, TextAlign = "center", Icon = "eject", Text = "Eject Core", Disabled = !core || active,
                        OnClick = () => c.Act("eject_core"),
                    }),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "tab", Fluid = true, TextAlign = "center", Icon = currentTab == 1 ? "server" : "compress-arrows-alt",
                        Text = currentTab == 1 ? "Run Simulations" : "Implosion Control",
                        OnClick = () => c.SetState(TabKey, currentTab == 1 ? 2 : 1),
                    }),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(
                    new UiNode(UiKind.Button)
                    {
                        Key = "eject_bomb", Fluid = true, TextAlign = "center", Icon = "eject", Text = "Eject Bomb", Disabled = !valvePresent || active,
                        OnClick = () => c.Act("eject_bomb"),
                    }))));
        return stack;
    }

    private static UiValue Gas(IReadOnlyList<UiValue> gasList, int index) => index < gasList.Count ? gasList[index] : UiValue.Null;

    // tg: AnomalyRefinery.jsx:75
    private static UiNode[] CoreCompressorContent(TguiContext c)
    {
        UiValue data = c.Data;
        bool core = data.Truthy("core");
        double requiredRadius = data.Num("requiredRadius");
        IReadOnlyList<UiValue> gasList = data.Arr("gasList");
        bool valveReady = data.Truthy("valveReady");
        bool active = data.Truthy("active");
        bool valvePresent = data.Truthy("valvePresent");
        UiValue target = Gas(gasList, 0);
        UiValue giver = Gas(gasList, 1);

        var coreSection = new UiNode(UiKind.Section)
        {
            Key = "core", Fill = true, Title = "Inserted Core",
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "start_implosion", Icon = "compress-arrows-alt", Background = "red", Text = "Implode Core",
                Disabled = active || !valveReady || !core, OnClick = () => c.Act("start_implosion"),
            },
        };
        if (!core)
            coreSection.With(new UiNode(UiKind.Modal) { Key = "no-core", TextAlign = "center", Text = "No Core Inserted!" });
        coreSection.With(new UiNode(UiKind.LabeledList).With(
            new UiNode(UiKind.LabeledItem) { Key = "name", Label = "Name" }.With(new UiNode(UiKind.Box) { Text = core ? data.Str("core") : "-" }),
            new UiNode(UiKind.LabeledItem) { Key = "radius", Label = "Required Radius" }.With(
                new UiNode(UiKind.Box) { Text = requiredRadius != 0 ? TguiFormat.ToFixed(requiredRadius) + " tiles" : "Implosion not possible." })));

        var bombSection = new UiNode(UiKind.Section)
        {
            Key = "bomb", Fill = true, Title = "Inserted Bomb",
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "swap", Icon = "exchange-alt", Text = "Swap Merging Order", Disabled = !valveReady, OnClick = () => c.Act("swap"),
            },
        };
        if (!valvePresent)
            bombSection.With(new UiNode(UiKind.Modal) { Key = "no-bomb", TextAlign = "center", Text = "No Bomb Inserted!" });
        // tg's giver column shows the target tank's moles, and its target column checks the giver's moles for the pressure (kept)
        bombSection.With(new UiNode(UiKind.Stack) { Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "giver", Grow = 1, TextAlign = "center" }.With(
                new UiNode(UiKind.Box) { Key = "giver-name", Height = "2em", Width = "100%", Bold = true, Text = "Giver Tank (" + (giver.Truthy("name") ? giver.Str("name") : "Not Available") + ")" },
                new UiNode(UiKind.Box) { Key = "giver-moles", Height = "2em", Width = "100%", Text = (giver.Truthy("total_moles") ? TguiFormat.ToFixed(target.Num("total_moles"), 2) : "-") + " moles at " + (giver.Truthy("total_moles") ? TguiFormat.ToFixed(giver.Num("temperature"), 2) : "-") + " Kelvin" },
                new UiNode(UiKind.Box) { Key = "giver-pressure", Height = "2em", Width = "100%", Text = (giver.Truthy("total_moles") ? TguiFormat.ToFixed(giver.Num("pressure"), 2) : "-") + " kPa" }),
            new UiNode(UiKind.StackItem) { Key = "arrow" }.With(new UiNode(UiKind.Icon) { Key = "arrow-right", Icon = "arrow-right", Size = 2 }),
            new UiNode(UiKind.StackItem) { Key = "target", Grow = 1, TextAlign = "center" }.With(
                new UiNode(UiKind.Box) { Key = "target-name", Height = "2em", Width = "100%", Bold = true, Text = "Target Tank (" + (target.Truthy("name") ? target.Str("name") : "Not Available") + ")" },
                new UiNode(UiKind.Box) { Key = "target-moles", Height = "2em", Width = "100%", Text = (target.Truthy("total_moles") ? TguiFormat.ToFixed(target.Num("total_moles"), 2) : "-") + " moles at " + (target.Truthy("total_moles") ? TguiFormat.ToFixed(target.Num("temperature"), 2) : "-") + " Kelvin" },
                new UiNode(UiKind.Box) { Key = "target-pressure", Height = "2em", Width = "100%", Text = (giver.Truthy("total_moles") ? TguiFormat.ToFixed(target.Num("pressure"), 2) : "-") + " kPa" })));

        return new[]
        {
            new UiNode(UiKind.StackItem) { Key = "core-item", Grow = 1 }.With(coreSection),
            new UiNode(UiKind.StackItem) { Key = "bomb-item", Grow = 1 }.With(bombSection),
        };
    }

    // tg: AnomalyRefinery.jsx:182
    private static UiNode[] BombProcessorContent(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<UiValue> gasList = data.Arr("gasList");
        double reactionIncrement = data.Num("reactionIncrement");
        UiValue combined = Gas(gasList, 2);

        var mixSection = new UiNode(UiKind.Section)
        {
            Key = "combined", Fill = true, Scrollable = true, Title = combined.Str("name"),
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "react", Icon = "vial", TextAlign = "center",
                Tooltip = reactionIncrement == 0 ? "Valve status: Closed" : "Valve status: Open. Current reaction count:" + TguiFormat.ToFixed(reactionIncrement),
                Text = reactionIncrement == 0 ? "Open Valve" : "React",
                Disabled = !Gas(gasList, 0).Truthy("total_moles") || !Gas(gasList, 1).Truthy("total_moles"),
                OnClick = () => c.Act("react"),
            },
        };
        if (!combined.Truthy("total_moles"))
            mixSection.With(new UiNode(UiKind.Modal) { Key = "no-gas", TextAlign = "center", Text = "No Gas Present" });
        mixSection.With(GasmixParser.Render(combined, null, null));

        var tanks = new UiNode(UiKind.Stack) { Fill = true };
        for (int i = 0; i < 2; i++)
        {
            UiValue individualGasmix = Gas(gasList, i);
            var section = new UiNode(UiKind.Section)
            {
                Key = "tank" + i, Fill = true, Scrollable = true,
                Title = individualGasmix.Truthy("name") ? individualGasmix.Str("name") : "Not Available",
            };
            if (!individualGasmix.Truthy("total_moles"))
                section.With(new UiNode(UiKind.Modal) { Key = "no-gas", TextAlign = "center", Text = "No Gas Present" });
            section.With(GasmixParser.Render(individualGasmix, null, null));
            tanks.With(new UiNode(UiKind.StackItem) { Key = "tank-item" + i, Grow = 1 }.With(section));
        }

        return new[]
        {
            new UiNode(UiKind.StackItem) { Key = "combined-item", Grow = 1 }.With(mixSection),
            new UiNode(UiKind.StackItem) { Key = "tanks-item", Grow = 1 }.With(tanks),
        };
    }
}
