namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// ui-overhaul.md section 5, "a press does one thing": an action bound to Ctrl or Shift alone that acts on the press only (down a level on Ctrl,
/// walk and run on Shift) is sent when that key is let go, and only if the key was alone: no other modifier key was down as it went down, and no
/// key or mouse button went down while it was held (a second key of its name included; one already held before does not count). So Ctrl+Q throws
/// and Ctrl+left click pulls without going down a level first, Shift+left click examines without switching walk and run, and Alt+Shift switches
/// nothing. Nothing is sent while a text field has the keyboard or in the free camera (Shift flies faster there). Actions with a release of their own (held throw, the pet commands, ...) start on the press. The options panel uses the same rule
/// (<see cref="Arm"/>, <see cref="ReleaseAlone"/>) when it captures a modifier.
/// </summary>
public sealed class ModifierTap
{
    private string _key; // the modifier waiting for its release, or null
    private PlayAction _action;
    private bool _spoiled;
    private long _mouse; // the mouse buttons down when it went down, less those let go since

    /// <summary>The actions the client also handles on the key's release: held, they start on the press.</summary>
    public static bool IsHeld(PlayAction action) => action is PlayAction.HoldThrow or PlayAction.MoveUpLevel or PlayAction.Resist
        or PlayAction.ViewPetCommands or PlayAction.Scoreboard or PlayAction.EmoteWheel or PlayAction.PushToTalk or PlayAction.PushToTalkTyping;

    /// <summary>Whether a modifier key other than <paramref name="key"/> itself is down (an event's own Shift, Ctrl, Alt and Meta flags).</summary>
    public static bool OtherModifiers(string key, bool shift, bool ctrl, bool alt, bool meta)
        => (shift && key != "Shift") || (ctrl && key != "Ctrl") || (alt && key != "Alt") || meta;

    /// <summary>Any key or mouse button went down (seen before the press's own action is resolved): it cancels the waiting modifier, even a second key of its name.</summary>
    public void AnyPress(string key)
    {
        if (_key != null)
            _spoiled = true;
    }

    /// <summary>The modifier waiting for its release (its key name), or null.</summary>
    public string Waiting => _key;

    /// <summary>
    /// Each frame, the mouse buttons down (<paramref name="mouseButtons"/>, a bit each): one that went down during the hold cancels the wait, whoever
    /// took its press (the dev menu's window marks it handled before the game sees it); one held from before does not, until it is let go and
    /// pressed again. A wait whose key is no longer down (<paramref name="keyDown"/>: its release went to another part of the interface, or the
    /// window lost the focus) is dropped, so the next tap of that key is not taken for a second key of its name.
    /// </summary>
    public void Frame(long mouseButtons, bool keyDown)
    {
        if (_key == null)
            return;
        if (!keyDown)
        {
            _key = null;
            return;
        }
        if ((mouseButtons & ~_mouse) != 0)
            _spoiled = true;
        _mouse &= mouseButtons;
    }

    /// <summary>
    /// <paramref name="key"/> (Ctrl or Shift) went down, with <paramref name="otherModifiers"/> down or not and <paramref name="mouseButtons"/>
    /// held: it waits alone for its release unless another modifier is down; a second key of the name already waiting leaves the wait cancelled.
    /// </summary>
    public void Arm(string key, bool otherModifiers, long mouseButtons)
    {
        if (key == _key)
        {
            _spoiled = true;
            return;
        }
        if (otherModifiers)
        {
            _key = null;
            return;
        }
        (_key, _spoiled, _mouse) = (key, false, mouseButtons);
    }

    /// <summary><paramref name="key"/> was let go: true when it was waiting and nothing cancelled it (the panel binds the modifier itself then).</summary>
    public bool ReleaseAlone(string key)
    {
        if (_key == null || key != _key)
            return false;
        _key = null;
        return !_spoiled;
    }

    /// <summary>
    /// The press of <paramref name="key"/> resolved to <paramref name="action"/>: true when that action is held back, waiting for the key's release,
    /// or dropped (<paramref name="blocked"/>: a text field has the keyboard or the free camera flies, and a lone Shift or Ctrl does nothing in the
    /// game then; or another modifier
    /// is down, <paramref name="otherModifiers"/>: the key is not alone).
    /// </summary>
    public bool Wait(string key, PlayAction? action, bool blocked, bool otherModifiers, long mouseButtons)
    {
        if (key is not ("Ctrl" or "Shift") || action is not PlayAction a || IsHeld(a))
            return false;
        if (blocked)
        {
            _key = null;
            return true;
        }
        Arm(key, otherModifiers, mouseButtons);
        _action = a;
        return true;
    }

    /// <summary>The window lost the focus: the waiting action is dropped (its key's release may never come, and keys pressed elsewhere are not seen).</summary>
    public void Drop() => _key = null;

    /// <summary>
    /// <paramref name="key"/> was let go: the waiting action to send now, or null (none, the key was not alone, or it is <paramref name="blocked"/>
    /// now: a text field has the keyboard or the free camera flies).
    /// </summary>
    public PlayAction? Release(string key, bool blocked) => ReleaseAlone(key) && !blocked ? _action : null;
}
