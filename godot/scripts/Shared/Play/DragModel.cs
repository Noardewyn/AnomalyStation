using System;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Protocol version 17 (drag and drop; docs/ai/specs/interaction.md section 8). Engine-free: no Godot API here (it is compiled into
// tests/Anomaly.Server.Tests too). BYOND's client turns a press into a drag once the mouse moves with the button held (OpenDream's
// MouseInputSystem: 3 screen pixels) and calls MouseDrop on the release over the thing under the mouse; a press and a release without
// that move is a click. The game scene feeds the cursor mode's (Tab) left button and pointer moves into this model; the captured view
// has no drags.

/// <summary>What a release does (<see cref="DragModel.Release"/>).</summary>
public enum DragOutcome : byte
{
    /// <summary>Nothing (no press was tracked, or a drag was let go over nothing: BYOND's MouseDrop with no over_object, which tg ignores).</summary>
    None = 0,

    /// <summary>The press never moved past the threshold: the click on the pressed thing, as before drags existed.</summary>
    Click = 1,

    /// <summary>A drag let go over a thing: the Drop action.</summary>
    Drop = 2,
}

/// <summary>A finished press: the click, or the drop of <see cref="Source"/> over <see cref="Over"/>.</summary>
public readonly record struct DragRelease(ClickTarget Source, ClickTarget Over, ClickButton Button, ClickModifiers Modifiers, byte DragTime);

/// <summary>
/// The press, drag and release of one mouse button in cursor mode: <see cref="Press"/> on a thing (a HUD entry, an item in a slot or a
/// world thing under the pointer), <see cref="Move"/> with the pointer, <see cref="Release"/> over the thing under the pointer then.
/// </summary>
public sealed class DragModel
{
    /// <summary>How far, in screen pixels, the pointer moves from the press before the press is a drag (OpenDream's 3, one more for a trackpad's jitter).</summary>
    public const float ThresholdPixels = 4f;

    private float _pressX, _pressY;
    private double _pressAt;
    private bool _draggable;

    /// <summary>True between a <see cref="Press"/> and its <see cref="Release"/> (or <see cref="Cancel"/>).</summary>
    public bool Pressed { get; private set; }

    /// <summary>True once the pressed pointer moved past <see cref="ThresholdPixels"/>: the release is a drop.</summary>
    public bool Dragging { get; private set; }

    /// <summary>The thing the press was on.</summary>
    public ClickTarget Source { get; private set; }

    /// <summary>The pressed button.</summary>
    public ClickButton Button { get; private set; }

    /// <summary>The modifiers held at the press (BYOND's drag params are the press's).</summary>
    public ClickModifiers Modifiers { get; private set; }

    /// <summary>The pointer's last position (where the dragged icon is drawn).</summary>
    public float PointerX { get; private set; }

    /// <summary>The pointer's last position.</summary>
    public float PointerY { get; private set; }

    /// <summary>
    /// A press of <paramref name="button"/> on <paramref name="source"/> at the screen point; false (nothing tracked) when there is no thing under it.
    /// The owner (2026-11-02): a thing that is not <paramref name="draggable"/> (a wall mount) never becomes a drag: however the pointer moves, the release is its click.
    /// </summary>
    public bool Press(in ClickTarget source, ClickButton button, ClickModifiers modifiers, float x, float y, double now, bool draggable = true)
    {
        Cancel();
        if (!source.IsSet || button == ClickButton.None)
            return false;
        _draggable = draggable;
        Pressed = true;
        Source = source;
        Button = button;
        Modifiers = modifiers;
        _pressX = PointerX = x;
        _pressY = PointerY = y;
        _pressAt = now;
        return true;
    }

    /// <summary>The pointer moved to the screen point; true when this move starts the drag.</summary>
    public bool Move(float x, float y)
    {
        PointerX = x;
        PointerY = y;
        if (!Pressed || Dragging || !_draggable)
            return false;
        float dx = x - _pressX, dy = y - _pressY;
        if (dx * dx + dy * dy <= ThresholdPixels * ThresholdPixels)
            return false;
        Dragging = true;
        return true;
    }

    /// <summary>
    /// The button went up over <paramref name="over"/> (<see cref="ChooseOver"/>'s answer, or none): a <see cref="DragOutcome.Click"/> on the
    /// pressed thing when the press never became a drag, a <see cref="DragOutcome.Drop"/> over a thing, or nothing.
    /// </summary>
    public DragOutcome Release(in ClickTarget over, double now, out DragRelease release)
    {
        release = default;
        if (!Pressed)
            return DragOutcome.None;
        bool dragged = Dragging;
        release = new DragRelease(Source, dragged ? over : Source, Button, Modifiers, DragDeciseconds(now - _pressAt));
        Cancel();
        if (!dragged)
            return DragOutcome.Click;
        return over.IsSet ? DragOutcome.Drop : DragOutcome.None;
    }

    /// <summary>Forgets the press (the mode changed, a window opened, the session ended).</summary>
    public void Cancel()
    {
        Pressed = false;
        Dragging = false;
        Source = default;
        Button = ClickButton.None;
        Modifiers = ClickModifiers.None;
    }

    /// <summary>
    /// What the drag is let go over: the HUD entry under the pointer (BYOND's screen is above the map), else the world thing or cell under it
    /// (<paramref name="world"/>, the pick along the pointer's ray), else nothing.
    /// </summary>
    public static ClickTarget ChooseOver(in ClickTarget hud, in ClickTarget world) => hud.IsSet ? hud : world;

    /// <summary>
    /// The owner's choice (2026-10-15, for now): in first person the own body is never under the pointer, so a drop on the HUD's
    /// health doll is a drop on the own mob (tg's drag onto oneself: a storage opens, and so on). Any other target is kept.
    /// </summary>
    public static ClickTarget SelfOnDoll(in ClickTarget over, Func<long, bool> isDoll, long ownEntity)
        => over.Kind == TargetKind.Screen && ownEntity != 0 && isDoll(over.Id) ? new ClickTarget(TargetKind.Entity, ownEntity, 0, 0, 0, 16, 16) : over;

    /// <summary>
    /// DEVIATION(DV417, extends DV72): the own body is never under the pointer (it is left out of the picks in both views), so a drag that
    /// starts on the HUD's health doll and drops on a world thing drops the own mob there, as tg's drag of one's own sprite (gaps L1: leaning
    /// on a wall behind you, climbing a table, buckling yourself). A drop back on the HUD keeps the doll as its source (tg's doll does
    /// nothing).
    /// </summary>
    public static DragRelease SelfFromDoll(in DragRelease release, Func<long, bool> isDoll, long ownEntity)
        => release.Source.Kind == TargetKind.Screen && ownEntity != 0 && isDoll(release.Source.Id) && release.Over.Kind is TargetKind.Entity or TargetKind.Turf
            ? release with { Source = new ClickTarget(TargetKind.Entity, ownEntity, 0, 0, 0, 16, 16) }
            : release;

    /// <summary>
    /// DEVIATION(DV72): in first person the own body is never under the pointer, so a ctrl-click on the HUD's health doll (cursor mode)
    /// is a ctrl-click on the own mob, as <see cref="SelfOnDoll"/> makes a drop on it one: tg's ctrl-click on oneself grasps the
    /// bleeding limb of the zone selected (<c>grabbedby</c>, carbon_defense.dm:518). Slice T5c: it aims at the doll's part clicked
    /// (<paramref name="zone"/>, <see cref="BodyZoneAim.DollZoneAt"/>; the server selects it as tg's <c>zone_selected</c>, DV30), so the limb
    /// clicked is grasped; off the doll's parts (None) the zone selector's stays, as in tg. tg's own ctrl-click on the doll does nothing.
    /// Any other click is kept.
    /// </summary>
    public static ClickTarget SelfCtrlClickOnDoll(in ClickTarget target, ClickModifiers modifiers, Func<long, bool> isDoll, long ownEntity, AimZone zone)
        => (modifiers & ClickModifiers.Ctrl) != 0 && target.Kind == TargetKind.Screen && ownEntity != 0 && isDoll(target.Id)
            ? new ClickTarget(TargetKind.Entity, ownEntity, 0, 0, 0, 0, 0, zone)
            : target;

    /// <summary>
    /// D183 (extends DV72): with an item in the active hand, a plain click (no modifier key) on a limb of the HUD's health doll in cursor
    /// mode is a click on the own mob aimed at that limb (<paramref name="zone"/>, <see cref="BodyZoneAim.DollZoneAt"/>; the server selects
    /// it as tg's <c>zone_selected</c>, DV30): tg clicks the own sprite, which first person never shows. With an empty hand, off a limb or
    /// with a modifier the click is kept (the empty hand's click on the doll is tg's self-check).
    /// </summary>
    public static ClickTarget SelfItemClickOnDoll(in ClickTarget target, ClickModifiers modifiers, bool holdsItem, Func<long, bool> isDoll, long ownEntity, AimZone zone)
        => holdsItem && modifiers == ClickModifiers.None && zone != AimZone.None && target.Kind == TargetKind.Screen && ownEntity != 0 && isDoll(target.Id)
            ? new ClickTarget(TargetKind.Entity, ownEntity, 0, 0, 0, 0, 0, zone)
            : target;

    /// <summary>
    /// Whether a cursor-mode press released as a click (not a drag) is sent as a click: on a HUD entry always (as before drag and drop); on a
    /// world thing or cell under the pointer only with Ctrl held (pulling: tg's ctrl-click, <c>CtrlClickOn</c>, pulls the thing under the
    /// pointer as it does under the crosshair). A plain click on the world in cursor mode stays nothing (the mouse is over the HUD there),
    /// except through a camera eye (<paramref name="worldClicks"/>: the AI's, which has no crosshair to click with; the owner, 2026-11-14).
    /// </summary>
    public static bool ClickIsSent(in DragRelease release, bool worldClicks = false)
        => release.Source.Kind == TargetKind.Screen
            || (release.Source.IsSet && (worldClicks || (release.Modifiers & ClickModifiers.Ctrl) != 0));

    /// <summary>A drag's length in deciseconds (BYOND's <c>world.time</c> unit), 0 to 255 (a longer drag is 255).</summary>
    public static byte DragDeciseconds(double seconds)
    {
        if (!(seconds > 0))
            return 0;
        double ds = Math.Floor(seconds * 10.0);
        return ds >= 255 ? (byte)255 : (byte)ds;
    }

    /// <summary>
    /// The appearance drawn under the pointer while dragging <paramref name="source"/> (BYOND shows the dragged object's icon): a screen
    /// entry's own, an item shown in a slot (a screen entry's vis child, protocol version 12), or a world entity's; none for a turf or a
    /// thing the replica does not know.
    /// </summary>
    public static TableRef IconOf(ReplicaWorld replica, in ClickTarget source)
    {
        if (replica == null)
            return default;
        switch (source.Kind)
        {
            case TargetKind.Screen:
                if (replica.Screen.TryGetValue(source.Id, out ScreenEntry entry))
                    return entry.Appearance;
                foreach (ScreenEntry holder in replica.Screen.Values)
                {
                    if (holder.VisChildren == null)
                        continue;
                    foreach (ScreenChild child in holder.VisChildren.Items)
                        if (child.Id == source.Id)
                            return child.Appearance;
                }
                return default;
            case TargetKind.Entity:
                return replica.TryGet(source.Id, out ReplicaEntity entity) ? entity.State.Appearance : default;
            default:
                return default;
        }
    }
}
