using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// D285: what the own hands hold, read from tg's HUD as tg draws it: a hand slot (hand_l, hand_r) carries the held item as an overlay and a
// "...handactive" overlay on the active one; a wielded thing puts tg's /obj/item/offhand (icon state "offhand", twohanded.dm:455) in the
// other hand; tg's throw button reads act_throw_on while throwing (living_item_handling.dm:29).

/// <summary>The own hands: each hand's item appearance (none when empty), which is active, which holds tg's offhand, and the throw mode.</summary>
public readonly record struct HeldHandsView(TableRef Left, TableRef Right, bool LeftActive, bool LeftOffhand, bool RightOffhand, bool Throwing, bool Mech = false, bool Combat = false)
{
    /// <summary>The thing held in both hands (the hand that is not the offhand), or none.</summary>
    public TableRef Wielded => LeftOffhand && !RightOffhand ? Right : RightOffhand && !LeftOffhand ? Left : TableRef.None;
}

/// <summary>Reads <see cref="HeldHandsView"/> off the replica's screen.</summary>
public static class HeldHands
{
    public static HeldHandsView Find(ReplicaWorld replica)
    {
        TableRef left = TableRef.None, right = TableRef.None;
        bool leftActive = false, leftOff = false, rightOff = false, throwing = false;
        bool mech = false, combat = false;
        TableRef mechLeft = TableRef.None, mechRight = TableRef.None;
        foreach (ScreenEntry entry in replica.Screen.Values)
        {
            if (!replica.TryGetAppearance(entry.Appearance, out AppearanceEntry app))
                continue;
            string state = replica.ResolveString(app.IconState);
            // DV361: a mech's arm equipment (obj_vehicle_sealed_mecha.refresh_arm_views): the left arm is the left button's, drawn as the left hand's
            if (entry.Group == HudGroup.None && state is "mecha_arm_l" or "mecha_arm_r")
            {
                mech = true;
                TableRef arm = entry.VisChildren?.Items.Length > 0 ? entry.VisChildren.Items[0].Appearance : TableRef.None;
                if (state == "mecha_arm_l")
                    mechLeft = arm;
                else
                    mechRight = arm;
                continue;
            }
            if (state == "combat") // tg's combat toggle (screen_objects.dm:389): the own swing whooshes only in combat mode (SwingSound)
                combat = true;
            if (entry.Group != HudGroup.Static)
                continue;
            if (state == "act_throw_on")
                throwing = true;
            if (state != "hand_l" && state != "hand_r" || app.OverlayCount == 0)
                continue;
            bool active = false;
            TableRef item = TableRef.None;
            foreach (TableRef overlay in app.Overlays)
            {
                if (replica.TryGetAppearance(overlay, out AppearanceEntry o) && (replica.ResolveString(o.IconState) ?? string.Empty).EndsWith("handactive", StringComparison.Ordinal))
                    active = true;
                else
                    item = overlay;
            }
            // tg keeps the held item in the slot's vis_contents (or as a screen object of its own at the slot's screen_loc): a server that sends it as its own entry
            // at the hand's screen_loc is read as well.
            if (item.IsNone)
                item = ItemShownAt(replica, entry);
            bool offhand = !item.IsNone && replica.TryGetAppearance(item, out AppearanceEntry it) && replica.ResolveString(it.IconState) == "offhand";
            if (state == "hand_l")
            {
                left = item;
                leftOff = offhand;
                leftActive |= active;
            }
            else
            {
                right = item;
                rightOff = offhand;
            }
        }
        if (mech) // the pilot has no hands out: the mech's two arms are drawn in their place, both ready
            return new HeldHandsView(mechLeft, mechRight, true, false, false, false, true, combat);
        return new HeldHandsView(left, right, leftActive, leftOff, rightOff, throwing, Combat: combat);
    }

    private static TableRef ItemShownAt(ReplicaWorld replica, ScreenEntry hand)
    {
        foreach (ScreenEntry other in replica.Screen.Values)
        {
            if (other.Id != hand.Id && other.ScreenLoc == hand.ScreenLoc && other.Group != HudGroup.Storage)
                return other.Appearance;
        }
        return TableRef.None;
    }

    /// <summary>
    /// Whether two looks of a hand are the same thing (no raise when a gun's ammo overlay or a welder's flame changes its appearance): the same icon
    /// file and one icon state the other's start (tg's "pistol" and "pistol-e", "welder" and "welder1").
    /// </summary>
    public static bool SameThing(string iconA, string stateA, string iconB, string stateB) =>
        iconA == iconB && stateA != null && stateB != null && (stateA.StartsWith(stateB, StringComparison.Ordinal) || stateB.StartsWith(stateA, StringComparison.Ordinal));

    /// <summary>Whether a held look is a lit flashlight's: tg's flashlight.dm draws one that is on as <c>"[icon_state]-on"</c>.</summary>
    public static bool IsLit(ReplicaWorld replica, TableRef item) =>
        !item.IsNone && replica.TryGetAppearance(item, out AppearanceEntry app) && (replica.ResolveString(app.IconState) ?? string.Empty).EndsWith("-on", StringComparison.Ordinal);

    /// <summary>Whether an icon file is tg's guns' (<c>icons/obj/weapons/guns/</c>): the thing is held as a gun, its barrel at the crosshair.</summary>
    public static bool IsGunIcon(string icon) => icon != null && icon.Contains("weapons/guns/", StringComparison.Ordinal);

    /// <summary>
    /// How hurt each arm is, 0 to 1, from tg's health doll (HudReader): its damage level over 5, a disabled or lost arm 1, and a wound (tg's pulsing
    /// wound_outline filter on the limb) at least 0.6.
    /// </summary>
    public static (float Left, float Right) ArmHurt(ReplicaWorld replica)
    {
        float left = 0f, right = 0f;
        var found = new List<FoundZone>();
        foreach (ScreenEntry entry in replica.Screen.Values)
        {
            found.Clear();
            HudReader.Collect(replica, entry.Appearance, found);
            foreach (FoundZone zone in found)
            {
                if (zone.Zone.Zone is not ("l_arm" or "r_arm"))
                    continue;
                float hurt = zone.Zone.Kind switch
                {
                    ZoneKind.Level => zone.Zone.Level / (float)HealthZones.MaxLevel,
                    ZoneKind.Dead => 0f,
                    _ => 1f,
                };
                if (replica.TryGetAppearance(zone.Node, out AppearanceEntry node) && node.Filters?.Count > 0)
                    hurt = Math.Max(hurt, 0.6f);
                if (zone.Zone.Zone == "l_arm")
                    left = Math.Max(left, hurt);
                else
                    right = Math.Max(right, hurt);
            }
        }
        return (left, right);
    }
}
