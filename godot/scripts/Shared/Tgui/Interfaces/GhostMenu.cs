using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/GhostMenu.tsx (silicons SI6, D326). DEVIATION(DV390):
// the Extra View Distance input is left out (a 2D client's wider view; the first-person client has none).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class GhostMenu
{
    // tg: GhostMenu.tsx:41
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool hasFun = data.Truthy("has_fun"), canBoo = data.Truthy("can_boo");
        // tg: GhostMenu.tsx:48  the window's title bar buttons (Boo! and Possess): a row over the content here
        UiNode fun = !hasFun ? null : new UiNode(UiKind.Stack) { Key = "fun" }.With(
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "boo", Disabled = !canBoo, Text = "Boo!", Tooltip = "Haunts things near you, with a cooldown.", OnClick = () => c.Act("boo") }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Button) { Key = "possess", Text = "Possess", Tooltip = "Allows you to possess any non-sentient mob.", OnClick = () => c.Act("possess") }));
        return c.Window(500, 630, "Ghost Menu", fun,
            new UiNode(UiKind.Stack) { Key = "main", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "left", Width = "40%" }.With(
                    new UiNode(UiKind.Section) { Key = "round", Title = "Player & Round Info" }.With(RoundSection(c)),
                    new UiNode(UiKind.Section) { Key = "huds", Title = "HUDs" }.With(HudSection(c)),
                    new UiNode(UiKind.Section) { Key = "settings", Title = "Ghost Settings" }.With(GhostSettingsSection(c))),
                new UiNode(UiKind.StackItem) { Key = "right", Width = "60%" }.With(NotificationPreferences(c))));
    }

    // tg: GhostMenu.tsx:90
    private static UiNode RoundSection(TguiContext c)
    {
        string bodyName = c.Data.Field("body_name") is { Kind: UiValueKind.String } b ? b.AsStr() : null;
        return new UiNode(UiKind.Fragment).With(
            string.IsNullOrEmpty(bodyName) ? null : new UiNode(UiKind.ImageButton)
            {
                Key = "return_to_body",
                Fluid = true,
                DmIcon = "icons/mob/simple/mob.dmi",
                DmIconState = "ghost",
                Tooltip = "Click to re-enter your corpse.",
                OnClick = () => c.Act("return_to_body"),
                Buttons = c.ButtonConfirm("dnr", new UiNode(UiKind.Button) { Icon = "ghost", Tooltip = "Become unable to be resusitated, permanently leaving your corpse behind.", OnClick = () => c.Act("DNR") }),
            }.With(new UiNode(UiKind.Box) { FontSize = "11px", Text = "Re-enter " + bodyName }),
            new UiNode(UiKind.ImageButton) { Key = "crew_manifest", Fluid = true, DmIcon = "icons/obj/machines/wallmounts.dmi", DmIconState = "newscaster_off", OnClick = () => c.Act("crew_manifest") }.With(new UiNode(UiKind.Box) { FontSize = "11px", Text = "View Crew Manifest" }),
            new UiNode(UiKind.ImageButton) { Key = "signup_pai", Fluid = true, DmIcon = "icons/obj/aicards.dmi", DmIconState = "pai", OnClick = () => c.Act("signup_pai") }.With(new UiNode(UiKind.Box) { FontSize = "11px", Text = "Sign up as pAI" }));
    }

    // tg: GhostMenu.tsx:136
    private static UiNode HudSection(TguiContext c)
    {
        var stack = new UiNode(UiKind.Stack) { Vertical = true };
        foreach (UiValue hud in c.Data.Arr("hud_info"))
        {
            bool enabled = hud.Truthy("enabled");
            double flag = hud.Num("flag");
            stack.With(new UiNode(UiKind.StackItem) { Key = hud.Str("name") }.With(new UiNode(UiKind.Button)
            {
                Fluid = true,
                Icon = enabled ? "check" : "times",
                Color = enabled ? "good" : "bad",
                Tooltip = hud.Str("tooltip"),
                Text = hud.Str("name"),
                OnClick = () => c.Act("toggle_visibility", ("toggling", flag)),
            }));
        }
        if (!c.Data.Truthy("lag_switch_on"))
            stack.With(new UiNode(UiKind.Button) { Key = "tray", Text = "T-ray Scan", Tooltip = "Performs a t-ray scan where you are.", OnClick = () => c.Act("tray_scan") });
        return stack;
    }

    // tg: GhostMenu.tsx:166
    private static UiNode GhostSettingsSection(TguiContext c)
    {
        string current = c.Data.Str("current_darkness");
        string[] levels = c.Data.Arr("darkness_levels").Select(v => v.AsStr()).ToArray();
        return new UiNode(UiKind.Stack) { Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "darkness" }.With(new UiNode(UiKind.Dropdown)
            {
                Key = "darkness",
                Options = levels,
                TextValue = current,
                OnSelected = value => c.Act("darkness", ("darkness_level", value)),
            }),
            new UiNode(UiKind.StackItem) { Key = "restore" }.With(new UiNode(UiKind.Button)
            {
                Fluid = true,
                Text = "Restore Ghost Character",
                Tooltip = "Restores your ghost character's appearance and username to that in your character preferences.",
                OnClick = () => c.Act("restore_appearance"),
            }));
    }

    // tg: GhostMenu.tsx:217
    private static UiNode NotificationPreferences(TguiContext c)
    {
        IReadOnlyList<UiValue> notifications = c.Data.Arr("notification_data");
        if (notifications.Count == 0)
            return new UiNode(UiKind.Box) { Text = "No notifications!" };
        var ignores = notifications.OrderBy(n => n.Str("desc").ToLowerInvariant(), StringComparer.Ordinal).ToList();
        return new UiNode(UiKind.Section)
        {
            Key = "notifications",
            Scrollable = true,
            Fill = true,
            Title = "Ghost Role Notifications",
            Buttons = new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Button) { Key = "turn_all_on", Icon = "check", Color = "good", Tooltip = "Enable all notifications.", OnClick = () => c.Act("turn_all_on") },
                new UiNode(UiKind.Button) { Key = "turn_all_off", Icon = "times", Color = "bad", Tooltip = "Disable all notifications.", OnClick = () => c.Act("turn_all_off") }),
        }.With(ignores.Select(ignore =>
        {
            bool enabled = ignore.Truthy("enabled");
            string key = ignore.Str("key");
            return new UiNode(UiKind.Button)
            {
                Key = "notify:" + key,
                Fluid = true,
                Icon = enabled ? "times" : "check",
                Color = enabled ? "bad" : "good",
                Text = ignore.Str("desc"),
                OnClick = () => c.Act("change_notification", ("key", key)),
            };
        }));
    }
}
