namespace Anomaly.Client.Shared;

/// <summary>
/// D282 (the owner, 2026-10-27): the one owner of the mouse's state. The pointer is free in cursor mode or while an overlay (the Esc
/// menu, the settings, the dev console, the dev menu) covers the game, and captured for the view otherwise; nothing else frees or
/// captures it. Tab toggles cursor mode. A window opened while the view had the mouse (a machine's, the PDA, the chat being typed
/// in) turns cursor mode on, and closing the last such window turns it off again, unless the player pressed Tab meanwhile.
/// </summary>
public sealed class MouseOwner
{
    private bool _byWindow; // cursor mode was turned on by a window opening, not by the player
    private bool _windowsWere;

    /// <summary>The pointer is the player's: it hovers and clicks the HUD and the world under it, the view does not turn.</summary>
    public bool CursorMode { get; private set; }

    /// <summary>Tab, or a scripted step: cursor mode on or off by the player's choice.</summary>
    public void Set(bool on)
    {
        CursorMode = on;
        _byWindow = false;
    }

    /// <summary>Told every frame whether a window of the player's is open; true when that changed cursor mode.</summary>
    public bool Windows(bool open)
    {
        bool was = CursorMode;
        if (open && !_windowsWere && !CursorMode)
        {
            CursorMode = true;
            _byWindow = true;
        }
        else if (!open && _windowsWere && _byWindow)
        {
            CursorMode = false;
            _byWindow = false;
        }
        _windowsWere = open;
        return CursorMode != was;
    }

    /// <summary>Whether the pointer is free now.</summary>
    public bool Free(bool overlay) => CursorMode || overlay;
}
