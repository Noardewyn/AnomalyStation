using Anomaly.Core.DmShim;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

/// <summary>What the release of a first-person drag of oneself sends (<see cref="SelfDrag.Release"/>).</summary>
public enum SelfDragOutcome : byte
{
    /// <summary>Nothing: no drag was held, or it was let go on nothing a drop can go to (a floor, a wall, nothing, a thing out of reach).</summary>
    None = 0,

    /// <summary>Let go on the own body: the click on oneself it always was, with the press's zone and modifiers.</summary>
    Click = 1,

    /// <summary>Let go on a thing: tg's drop of the own mob onto it (the drag of one's own sprite).</summary>
    Drop = 2,
}

/// <summary>
/// DEVIATION(DV691): the first-person drag of oneself (ui-overhaul.md section 17). Engine-free (compiled into tests/Anomaly.Server.Tests too).
/// In the captured view a plain left press with the crosshair on the own body (looking down, <see cref="LookDown.PickExclude"/>) sends nothing
/// and is held; the player turns to an object and lets go there: the drop of the own mob onto it, as cursor mode's drag sends; let go on the own
/// body it is the click on oneself, sent then with the press's zone; let go on anything else nothing. Only the own body starts it; a press with a
/// modifier (examine, the grasp) stays the click on the press, as tg's drop takes no modifiers. Tab, the view lost or the right button drop it.
/// </summary>
public sealed class SelfDrag
{
    private double _pressAt;

    /// <summary>True between a <see cref="Press"/> that started the drag and its <see cref="Release"/> or <see cref="Cancel"/>.</summary>
    public bool Holding { get; private set; }

    /// <summary>The own body as pressed: the crosshair's target then, its picture pixel giving the click's zone.</summary>
    public ClickTarget Pressed { get; private set; }

    /// <summary>The modifiers held at the press (none: a modified press never starts the drag).</summary>
    public ClickModifiers Modifiers { get; private set; }

    /// <summary>Whether a first-person left press on <paramref name="pressed"/> starts the drag: the own body, no modifier, not in cursor mode.</summary>
    public static bool Starts(in ClickTarget pressed, long ownEntity, bool cursorMode, ClickModifiers modifiers)
        => !cursorMode && ownEntity != 0 && modifiers == ClickModifiers.None && pressed.Kind == TargetKind.Entity && pressed.Id == ownEntity;

    /// <summary>A left press in the view; true when it starts the drag (then nothing is sent for it), false when it is the click on the press.</summary>
    public bool Press(in ClickTarget pressed, long ownEntity, bool cursorMode, ClickModifiers modifiers, double now)
    {
        Cancel();
        if (!Starts(pressed, ownEntity, cursorMode, modifiers))
            return false;
        (Holding, Pressed, Modifiers, _pressAt) = (true, pressed, modifiers, now);
        return true;
    }

    /// <summary>Drops the drag with nothing sent (Tab, the view lost, the right button).</summary>
    public void Cancel()
    {
        Holding = false;
        Pressed = default;
        Modifiers = ClickModifiers.None;
    }

    /// <summary>
    /// Whether a held drag is dropped now, with nothing sent: the pressed body is no longer the own mob (<paramref name="ownEntity"/>: a body
    /// swap), the view is no longer the captured first-person one (<paramref name="firstPerson"/> false, <paramref name="cursorMode"/> on: Tab
    /// or a window), a panel took the input, something else takes the mouse (<paramref name="overlay"/>: the emote wheel, a radial, the dev
    /// menu, a text field), the left button is no longer down without the release having ended the drag (<paramref name="buttonDown"/> false:
    /// the release went elsewhere, or the window lost the focus), or the own body can no longer be shown (<paramref name="bodyShowable"/>:
    /// <see cref="LookDown.CanShowOwnBody"/>; death or a stun lays it down). Looking away from the body is the gesture itself and keeps it.
    /// </summary>
    public bool Lost(long ownEntity, bool firstPerson, bool cursorMode, bool panels, bool bodyShowable, bool overlay, bool buttonDown)
        => Holding && (Pressed.Id != ownEntity || ownEntity == 0 || !firstPerson || cursorMode || panels || !bodyShowable || overlay || !buttonDown);

    /// <summary>
    /// The button came up with the crosshair on <paramref name="over"/>: on the own body the click on oneself (<paramref name="release"/>'s
    /// source and over are the press's target), on another thing not known to be out of reach (<paramref name="outOfReach"/>: the server's
    /// answer to the hover, DV687) the drop of the own mob onto it (tg's own reach rule decides it then), on anything else nothing.
    /// </summary>
    public SelfDragOutcome Release(in ClickTarget over, long ownEntity, bool outOfReach, double now, out DragRelease release)
    {
        release = default;
        if (!Holding)
            return SelfDragOutcome.None;
        ClickTarget pressed = Pressed;
        ClickModifiers modifiers = Modifiers;
        byte time = DragModel.DragDeciseconds(now - _pressAt);
        Cancel();
        if (pressed.Id != ownEntity || ownEntity == 0)
            return SelfDragOutcome.None; // the own mob changed while held (a body swap): the pressed body is no longer oneself
        if (over.Kind != TargetKind.Entity)
            return SelfDragOutcome.None;
        if (over.Id == ownEntity)
        {
            release = new DragRelease(pressed, pressed, ClickButton.Left, modifiers, time);
            return SelfDragOutcome.Click;
        }
        if (outOfReach)
            return SelfDragOutcome.None;
        release = new DragRelease(pressed, over, ClickButton.Left, modifiers, time);
        return SelfDragOutcome.Drop;
    }

    /// <summary>
    /// The picture in the hint strip's square while the drag is held: the pressed body's appearance, the picture the drag mark at the crosshair
    /// shows (<see cref="DragModel.IconOf"/>); none when nothing is held.
    /// </summary>
    public TableRef HintPicture(ReplicaWorld replica) => Holding ? DragModel.IconOf(replica, Pressed) : default;

    /// <summary>The hint strip's name while the drag is held.</summary>
    public static string HintName => I18n.T("Yourself");

    /// <summary>The hint strip's one line while the drag is held: the primary click's mouse button and "Release on an object" (none when no mouse button is bound to it).</summary>
    public static List<PromptLine> HintLines(KeyBindings bindings)
        => KeyChips.MouseButtonOf(bindings, PlayAction.PrimaryClick) is string key ? new List<PromptLine> { new(key, I18n.T("Release on an object")) } : new List<PromptLine>();

    /// <summary>
    /// The prompt's name while the drag is held: a thing under the crosshair named as the prompt names it (<paramref name="promptName"/>,
    /// <see cref="KeyChips.PromptName"/>: none out of reach, DV687), no action lines; none for a cell or nothing.
    /// </summary>
    public static string PromptName(in ClickTarget over, string promptName)
        => over.Kind == TargetKind.Entity && !string.IsNullOrEmpty(promptName) ? promptName : null;
}
