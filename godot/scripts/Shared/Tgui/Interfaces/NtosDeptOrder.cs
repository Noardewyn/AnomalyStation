using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosDeptOrder.tsx (gaps P4d, D298: the Departmental
// Orders app): the department's free packs by category with their cooldown estimate, the cooldown with the heads' override, and the
// unlinked console's link button. tg's Dimmer is the kit's Modal (as in DnaConsole) and its Blink a plain icon. Engine-free (compiled
// into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosDeptOrder
{
    // tg: NtosDeptOrder.tsx:20-24
    private const double COST_MODERATE_BOUND = 700;
    private const double COST_LONG_BOUND = 2700;
    private const double COST_VERY_LONG_BOUND = 8000;

    // tg: NtosDeptOrder.tsx:103
    public static UiNode Render(TguiContext c) => NtosWindow.Render(c, 620, 580, "Department Orders", Content(c));

    // tg: NtosDeptOrder.tsx:69  DepartmentOrderContent
    public static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        if (data.Truthy("no_link"))
            return NoLinkDimmer(c);
        if (data.Truthy("time_left"))
            return CooldownDimmer(c);

        return new UiNode(UiKind.Stack) { Key = "content", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "notice" }.With(new UiNode(UiKind.NoticeBox)
            {
                Key = "info",
                Color = "info",
                Text = "As employees of Nanotrasen, the selection of orders here are completely free of charge, only incurring a cooldown on the service. Cheaper items will make you wait for less time before Nanotrasen allows another purchase, to encourage tasteful spending.",
            }),
            new UiNode(UiKind.StackItem) { Key = "catalog", Grow = 1 }.With(DepartmentCatalog(c)));
    }

    // tg: NtosDeptOrder.tsx:48  CooldownEstimate
    private static UiNode CooldownEstimate(double cost)
    {
        string color = cost >= COST_VERY_LONG_BOUND ? "red" : cost >= COST_LONG_BOUND ? "orange" : cost >= COST_MODERATE_BOUND ? "yellow" : "green";
        string text = cost >= COST_VERY_LONG_BOUND ? "very long" : cost >= COST_LONG_BOUND ? "long" : cost >= COST_MODERATE_BOUND ? "moderate" : "short";
        return new UiNode(UiKind.Box) { Key = "estimate", Color = color, Text = text + " cooldown." };
    }

    // tg: NtosDeptOrder.tsx:113
    private static UiNode CooldownDimmer(TguiContext c)
    {
        UiValue data = c.Data;
        bool canOverride = data.Truthy("can_override");
        return new UiNode(UiKind.Modal) { Key = "cooldown" }.With(new UiNode(UiKind.Stack) { Key = "stack", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "icon", TextAlign = "center" }.With(new UiNode(UiKind.Icon) { Key = "route", Color = "bug", Icon = "route", Size = 20 }),
            new UiNode(UiKind.StackItem) { Key = "timeLeft", FontSize = "18px", Color = "orange", Text = "Ready for another order in " + data.Str("time_left") + "..." },
            new UiNode(UiKind.StackItem) { Key = "overrideItem", TextAlign = "center", Color = "orange" }.With(new UiNode(UiKind.Button)
            {
                Key = "override",
                Width = "300px",
                Tooltip = canOverride ? "This action requires Head of Staff access!" : "Crate already shipped! No cancelling now!",
                Color = "red",
                Disabled = !canOverride,
                Text = "Override",
                FontSize = "22px",
                OnClick = () => c.Act("override_order"),
            })));
    }

    // tg: NtosDeptOrder.tsx:148
    private static UiNode NoLinkDimmer(TguiContext c)
    {
        UiValue data = c.Data;
        return new UiNode(UiKind.Modal) { Key = "nolink" }.With(new UiNode(UiKind.Stack) { Key = "stack", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "icon", TextAlign = "center" }.With(new UiNode(UiKind.Icon) { Key = "exclamation", Color = "red", Icon = "exclamation", Size = 16, Opacity = 0.8 }),
            new UiNode(UiKind.StackItem) { Key = "unlinked", TextAlign = "center", FontSize = "22px", Color = "red", Text = "Unlinked!" },
            new UiNode(UiKind.StackItem) { Key = "linkItem", TextAlign = "center", FontSize = "14px", Color = "red" }.With(new UiNode(UiKind.Button)
            {
                Key = "link",
                Disabled = !data.Truthy("id_inside"),
                Text = "Please insert a silver Head of Staff ID and press to continue.",
                OnClick = () => c.Act("link"),
            })));
    }

    // tg: NtosDeptOrder.tsx:175
    private static UiNode DepartmentCatalog(TguiContext c)
    {
        var supplies = c.Data.Get("supplies").Items;
        string first = supplies.Count > 0 ? supplies[0].Str("name") : null;
        string tabCategory = c.UseState("deptTab", first);
        UiValue category = supplies.FirstOrDefault(cat => cat.Str("name") == tabCategory) ?? supplies.FirstOrDefault();

        var tabs = new UiNode(UiKind.Tabs) { Key = "tabs", TextAlign = "center", Fluid = true };
        foreach (UiValue cat in supplies)
        {
            string name = cat.Str("name");
            tabs.With(new UiNode(UiKind.Tab) { Key = "tab:" + name, Selected = category == cat, Text = name, OnClick = () => c.SetState("deptTab", name) });
        }
        var packs = new UiNode(UiKind.Stack) { Key = "packs", Vertical = true };
        if (category != null)
        {
            foreach (UiValue pack in category.Get("packs").Items)
            {
                string id = pack.Str("id");
                packs.With(new UiNode(UiKind.StackItem) { Key = "pack:" + pack.Str("name"), Candystripe = true }.With(
                    new UiNode(UiKind.Stack) { Key = "row", Fill = true }.With(
                        new UiNode(UiKind.StackItem) { Key = "name", Grow = 1 }.With(new UiNode(UiKind.Box) { Key = "label", Underline = true, Tooltip = pack.Str("desc"), Text = pack.Str("name") }),
                        new UiNode(UiKind.StackItem) { Key = "order" }.With(
                            CooldownEstimate(pack.Num("cost")),
                            new UiNode(UiKind.Button) { Key = "orderButton", Text = "Order", OnClick = () => c.Act("order", ("id", id)) }))));
            }
        }
        return new UiNode(UiKind.Stack) { Key = "catalog", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "tabsItem" }.With(tabs),
            new UiNode(UiKind.StackItem) { Key = "packsItem", Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "packsSection", Fill = true, Scrollable = true }.With(packs)));
    }
}
