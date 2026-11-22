using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/LawRack.tsx (silicons SI1, D326: the module rack's
// window, with styles/interfaces/LawRack.scss's colours). Engine-free (compiled into the tests too). The kit's Floating opens on
// hover, as the port's other Floating popups (ChemDispenser, PersonalCrafting). The AI face's frame and face are tg's two
// status_display.dmi pictures in an Overlay.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class LawRack
{
    // tg: tgui/packages/tgui/interfaces/LawRack.tsx:16
    private const double NONE = 0, SCREWED = 1, WELDED = 2;

    // tg: tgui/packages/tgui/interfaces/LawRack.tsx:64
    private static string GetSlotColor(UiValue slot, int index, bool hasCoreSlot)
    {
        bool isCoreSlot = index == 0 && hasCoreSlot;
        if (slot.Truthy("empty")) return isCoreSlot ? "#5AA6FF" : "#83BFB1";
        if (slot.Truthy("ioned")) return "#DD7973";
        return isCoreSlot ? "#4183CD" : "#14BB69";
    }

    // tg: tgui/packages/tgui/interfaces/LawRack.tsx:76
    private static UiNode LawSlotScrew(string key, bool noscrew, bool disabled, string tooltip, Action handleScrew) =>
        new UiNode(UiKind.Button)
        {
            Key = key,
            Tooltip = tooltip,
            Width = "20px",
            Height = "20px",
            Circular = true,
            Background = noscrew ? "#000000" : "#D3D3D3", // .LawRack__Screw (lightgrey) / .Empty (black)
            Disabled = disabled,
            OnClick = handleScrew,
        };

    // tg: tgui/packages/tgui/interfaces/LawRack.tsx:92
    private static UiNode LawSlot(TguiContext c, UiValue data, UiValue slot, int index)
    {
        bool holdingModule = data.Truthy("holding_module");
        bool holdingScrewdriver = data.Truthy("holding_screwdriver");
        bool holdingWelder = data.Truthy("holding_welder");
        bool holdingMultitool = data.Truthy("holding_multitool");
        bool hasCoreSlot = data.Truthy("has_core_slot");
        double security = slot.Num("security");
        bool empty = slot.Truthy("empty");
        string name = slot.Str("name");
        int slotNumber = index + 1;

        string screwTooltip = security == SCREWED && !holdingScrewdriver
            ? "You need a screwdriver to unscrew this module."
            : holdingScrewdriver && security == WELDED
                ? "You can't remove the screws before unwelding it."
                : null;
        bool screwDisabled = !holdingScrewdriver || empty || security == WELDED;
        bool noscrew = empty || security == NONE;

        var middle = new UiNode(UiKind.StackItem) { Width = "85%" };
        if (security == WELDED || holdingWelder)
        {
            middle.With(new UiNode(UiKind.Button)
            {
                Key = "weld" + index,
                TextAlign = "center",
                Fluid = true,
                Tooltip = security == WELDED && !holdingWelder
                    ? "You can't remove this module before unwelding it."
                    : holdingWelder && security == NONE && !empty
                        ? "Screw this module in place before welding."
                        : null,
                Background = security == WELDED ? "#646464" : null, // .LawRack__Welded
                Disabled = !holdingWelder || empty || security == NONE,
                OnClick = () => c.Act("weld_module", ("slot", slotNumber)),
                Text = security == WELDED ? "Welded" : empty ? (string.IsNullOrEmpty(name) ? "Empty" : name) : "Weld Module",
            });
        }
        if (security != WELDED && holdingMultitool)
        {
            middle.With(new UiNode(UiKind.Button)
            {
                Key = "multitool" + index,
                TextAlign = "center",
                Fluid = true,
                Disabled = !slot.Truthy("ioned"),
                OnClick = () => c.Act("multitool_module", ("slot", slotNumber)),
                Text = "Repair Ion Damage",
            });
        }
        if (security != WELDED && !holdingMultitool && !holdingWelder)
        {
            middle.With(new UiNode(UiKind.Button)
            {
                Key = "module" + index,
                TextAlign = "center",
                Fluid = true,
                Tooltip = security == SCREWED ? "You can't remove this module before unscrewing it." : null,
                Disabled = (!holdingModule && empty) || security != NONE,
                OnClick = () => c.Act(holdingModule && empty ? "insert_module" : "remove_module", ("slot", slotNumber)),
                Text = TguiFormat.CapitalizeAll(string.IsNullOrEmpty(name) ? "Empty" : name), // textTransform: capitalize
            });
        }

        return new UiNode(UiKind.Box) { Key = "slot" + index, Height = "33px", Background = GetSlotColor(slot, index, hasCoreSlot) }.With(
            new UiNode(UiKind.Stack) { Align = "center" }.With(
                new UiNode(UiKind.StackItem).With(LawSlotScrew("screwL" + index, noscrew, screwDisabled, screwTooltip, () => c.Act("screw_module", ("slot", slotNumber)))),
                middle,
                new UiNode(UiKind.StackItem).With(LawSlotScrew("screwR" + index, noscrew, screwDisabled, screwTooltip, () => c.Act("screw_module", ("slot", slotNumber))))));
    }

    // tg: tgui/packages/tgui/interfaces/LawRack.tsx:222
    private static UiNode LinkableFloating(TguiContext c, bool show, IReadOnlyList<UiValue> linkable, Action<string, string> linkAct, Action hide, UiNode button, string key)
    {
        if (!show)
            return button;
        return new UiNode(UiKind.Floating)
        {
            Key = key,
            Content = () => new UiNode(UiKind.Stack) { Vertical = true, Background = "#000000" }.With(
                linkable.Select(item => new UiNode(UiKind.StackItem) { Key = item.Str("ref") }.With(new UiNode(UiKind.Button)
                {
                    Icon = "link",
                    Bold = true,
                    Text = TguiFormat.CapitalizeAll(item.Str("name")), // textTransform: capitalize
                    OnClick = () =>
                    {
                        linkAct(item.Str("ref"), item.Str("name"));
                        if (linkable.Count == 1) hide();
                    },
                }))),
        }.With(button);
    }

    // tg: tgui/packages/tgui/interfaces/LawRack.tsx:270
    private static UiNode UnlinkableFloating(TguiContext c, bool show, IReadOnlyList<string> unlinkable, Action<int, string> unlinkAct, Action hide, UiNode button, bool canUnlink, string unlinkTooltip, string key)
    {
        if (!show)
            return button;
        return new UiNode(UiKind.Floating)
        {
            Key = key,
            Content = () => new UiNode(UiKind.Stack) { Vertical = true, Background = "#000000" }.With(
                unlinkable.Select((name, listIndex) => new UiNode(UiKind.StackItem) { Key = name + listIndex }.With(new UiNode(UiKind.Button)
                {
                    Icon = "link-slash",
                    Bold = true,
                    Disabled = !canUnlink,
                    Tooltip = unlinkTooltip,
                    Text = TguiFormat.CapitalizeAll(name), // textTransform: capitalize
                    OnClick = () =>
                    {
                        unlinkAct(listIndex, name);
                        if (unlinkable.Count == 1) hide();
                    },
                }))),
        }.With(button);
    }

    // tg: tgui/packages/tgui/interfaces/LawRack.tsx:327
    private static UiNode AiFlavorDisplay(string showFace)
    {
        var overlay = new UiNode(UiKind.Overlay) { Width = "60px", Height = "60px" }.With(
            new UiNode(UiKind.Box) { Key = "frame", DmIcon = "icons/obj/machines/status_display.dmi", DmIconState = "entertainment_frame", Width = "60px", Height = "60px" });
        if (!string.IsNullOrEmpty(showFace))
            overlay.With(new UiNode(UiKind.Box) { Key = "face", DmIcon = "icons/obj/machines/status_display.dmi", DmIconState = showFace, Width = "60px", Height = "60px" });
        return new UiNode(UiKind.Box) { Height = "60px" }.With(overlay);
    }

    private static UiNode Console(params UiNode[] lines) =>
        new UiNode(UiKind.Stack) { Vertical = true, Background = "#000000" }.With(lines); // .LawRack__Console

    // tg: tgui/packages/tgui/interfaces/LawRack.tsx:353
    private static UiNode BroadcasterDisplay(TguiContext c, UiValue data)
    {
        bool depowered = data.Truthy("depowered");
        UiValue parentRack = data.Get("parent_rack");
        bool hasParent = parentRack != null && parentRack.Kind != UiValueKind.Null;
        IReadOnlyList<UiValue> linkableRacks = data.Arr("linkable_racks");
        bool rackLink = c.UseState("rackLink", true);

        return new UiNode(UiKind.Stack).With(
            new UiNode(UiKind.StackItem) { Width = "50%" }.With(Console(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Bold = true, Text = "> Linked Rack:" }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Text = hasParent ? TguiFormat.Capitalize(parentRack.Str("name")) : "N/A" }))),
            new UiNode(UiKind.StackItem) { Width = "50%", Align = "center" }.With(new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button)
                {
                    Key = "unlink_rack",
                    Fluid = true,
                    Disabled = depowered || !hasParent,
                    Tooltip = depowered ? "No power!" : hasParent ? null : "No linked rack!",
                    Color = depowered || !hasParent ? "grey" : "black",
                    OnClick = () => c.Act("unlink_rack"),
                    Text = "> Unlink Rack",
                }),
                new UiNode(UiKind.StackItem).With(LinkableFloating(c, rackLink, linkableRacks,
                    (rackRef, name) => c.Act("link_rack", ("rack_ref", rackRef), ("rack_name", name)),
                    () => c.SetState("rackLink", false),
                    new UiNode(UiKind.Button)
                    {
                        Key = "new_link",
                        Fluid = true,
                        Disabled = linkableRacks.Count == 0 || depowered || hasParent,
                        Tooltip = depowered ? "No power!" : linkableRacks.Count > 0 ? (hasParent ? "Already linked!" : null) : "No linkable racks!",
                        Color = depowered || hasParent ? "grey" : "black",
                        OnClick = () => c.SetState("rackLink", !rackLink),
                        Text = "> New Link",
                    }, "float_racks")))));
    }

    // tg: tgui/packages/tgui/interfaces/LawRack.tsx:443
    private static UiNode BaseDisplay(TguiContext c, UiValue data)
    {
        bool depowered = data.Truthy("depowered");
        IReadOnlyList<UiValue> linkableSilicons = data.Arr("linkable_silicons");
        IReadOnlyList<string> linkedMobs = data.Arr("linked_mobs").Select(v => v.AsStr()).ToList();
        UiValue parentRack = data.Get("parent_rack");
        bool hasParent = parentRack != null && parentRack.Kind != UiValueKind.Null;
        bool siliconLink = c.UseState("siliconLink", true);
        bool siliconUnlink = c.UseState("siliconUnlink", true);

        return new UiNode(UiKind.Stack).With(
            new UiNode(UiKind.StackItem).With(Console(
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Bold = true, Text = "> Linked Silicons:" }),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Wrap = true, Text = linkedMobs.Count > 0 ? string.Join(", ", linkedMobs) : "N/A" }))),
            new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(
                UnlinkableFloating(c, siliconUnlink, linkedMobs,
                    (index, name) => c.Act("unlink_silicon", ("silicon_index", index), ("silicon_name", name)),
                    () => c.SetState("siliconUnlink", false),
                    new UiNode(UiKind.Button)
                    {
                        Key = "manage",
                        Fluid = true,
                        Disabled = linkedMobs.Count == 0 || depowered,
                        Tooltip = depowered ? "No power!" : linkedMobs.Count > 0 ? null : "No linked silicons!",
                        Color = depowered || linkedMobs.Count == 0 ? "grey" : "black",
                        OnClick = () => c.SetState("siliconUnlink", !siliconUnlink),
                        Text = "> Manage Connections",
                    }, !depowered, depowered ? "No power!" : null, "float_unlink"),
                new UiNode(UiKind.StackItem).With(LinkableFloating(c, siliconLink, linkableSilicons,
                    (siliconRef, name) => c.Act("link_silicon", ("silicon_ref", siliconRef), ("silicon_name", name)),
                    () => c.SetState("siliconLink", false),
                    new UiNode(UiKind.Button)
                    {
                        Key = "new_connection",
                        Fluid = true,
                        Disabled = linkableSilicons.Count == 0 || depowered || hasParent,
                        Tooltip = depowered ? "No power!" : linkableSilicons.Count > 0 ? (hasParent ? "Linked to another rack!" : null) : "No linkable silicons!",
                        Color = depowered || linkableSilicons.Count == 0 ? "gray" : "black",
                        OnClick = () => c.SetState("siliconLink", !siliconLink),
                        Text = "> New Connection",
                    }, "float_link")))));
    }

    // tg: tgui/packages/tgui/interfaces/LawRack.tsx:523
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool depowered = data.Truthy("depowered");
        UiValue linkableRacks = data.Get("linkable_racks");
        UiValue linkedMobsValue = data.Get("linked_mobs");
        UiValue parentRack = data.Get("parent_rack");
        bool hasParent = parentRack != null && parentRack.Kind != UiValueKind.Null;
        IReadOnlyList<UiValue> slots = data.Arr("slots");
        double refreshCooldown = data.Num("refresh_cooldown");
        bool hasCoreSlot = data.Truthy("has_core_slot");
        UiValue linkedRackModuleCount = data.Get("linked_rack_module_count");

        string ScreenDisplay()
        {
            if (depowered)
                return "off";
            if (hasParent || data.Arr("linked_mobs").Count > 0)
                return System.Math.Round(System.Random.Shared.NextDouble() * 100) == 1 ? "ai_hal" : "ai_sal";
            return "ai_bsod";
        }

        var slotList = new UiNode(UiKind.Stack) { Vertical = true }.With(
            slots.Select((slot, index) => new UiNode(UiKind.StackItem) { Key = "row" + index }.With(LawSlot(c, data, slot, index))));
        if (linkedRackModuleCount != null && linkedRackModuleCount.Kind != UiValueKind.Null)
            slotList.With(new UiNode(UiKind.StackItem) { Key = "remote", Background = "#000000", TextAlign = "center" }.With(
                new UiNode(UiKind.Box) { Text = $"{(long)linkedRackModuleCount.AsNum()} other module(s) are being loaded remotely." }));

        var controls = new UiNode(UiKind.Stack);
        if (hasCoreSlot)
            controls.With(new UiNode(UiKind.StackItem) { Key = "face", Width = "60px", TextAlign = "center" }.With(AiFlavorDisplay(ScreenDisplay())));
        if (linkableRacks != null && linkableRacks.Kind != UiValueKind.Null)
            controls.With(new UiNode(UiKind.StackItem) { Key = "broadcaster", Grow = 1 }.With(BroadcasterDisplay(c, data)));
        if (linkedMobsValue != null && linkedMobsValue.Kind != UiValueKind.Null)
            controls.With(new UiNode(UiKind.StackItem) { Key = "base", Grow = 1 }.With(BaseDisplay(c, data)));

        return c.Window(400, slots.Count * 40 + 200, "Module Rack",
            new UiNode(UiKind.Section).With(slotList),
            new UiNode(UiKind.Section)
            {
                Title = "Controls",
                Buttons = new UiNode(UiKind.Button)
                {
                    Key = "refresh",
                    Icon = "refresh",
                    Disabled = depowered || refreshCooldown > 0,
                    Tooltip = depowered ? "No power!" : refreshCooldown > 0 ? $"Cooldown: {System.Math.Round(refreshCooldown / 10)}s" : "Refreshes module and link data.",
                    OnClick = () => c.Act("refresh"),
                },
            }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(new UiNode(UiKind.StackItem).With(controls))));
    }
}
