using Godot;
using System.Collections.Generic;

namespace Anomaly.Client;

// The owner (2026-11-11): one switch hides the whole interface (the HUD's screen objects, the crosshair, the chat, the minimap, the
// tooltips, the stats and the names over heads), for tg's cinematics now, an immersive mode and cutscenes later. The owner (2026-11-19):
// the windows a thing in the world opens (tgui's consoles and computers, an open container) still open while it is hidden. The console and the Esc menu
// (the Menu layer) stay, so the interface can be brought back; the world's own overlays (tg's fullscreens, the screen effects) and the
// build tag stay too. The player's switch is the console's "ui hide|show|toggle" and its key (Shift+F4); a cinematic on tg's splash plane
// (HudLayer.SplashShown) hides the interface while it shows.
public partial class GameScene
{
    private readonly List<CanvasLayer> _interfaceLayers = new();
    private bool _interfaceHiddenByPlayer;
    private bool _interfaceHiddenNow;

    /// <summary>The console's <c>ui hide|show|toggle</c> (and the key's toggle): the player's switch; the answer for the console's log.</summary>
    private string InterfaceCommand(string mode)
    {
        switch (mode)
        {
            case "hide": _interfaceHiddenByPlayer = true; break;
            case "show": _interfaceHiddenByPlayer = false; break;
            case "toggle": _interfaceHiddenByPlayer = !_interfaceHiddenByPlayer; break;
            default: return "usage: ui hide|show|toggle";
        }
        UpdateInterfaceVisibility();
        return _interfaceHiddenByPlayer ? "the interface is hidden (ui show or the key brings it back)" : "the interface is shown";
    }

    // every frame: the player's switch, or a cinematic on the screen
    private void UpdateInterfaceVisibility()
    {
        bool splash = _hudLayer?.SplashShown ?? false;
        _hudLayer?.SetSplashBackdrop(splash);
        bool hidden = _interfaceHiddenByPlayer || splash;
        if (hidden == _interfaceHiddenNow)
            return;
        _interfaceHiddenNow = hidden;
        foreach (CanvasLayer layer in _interfaceLayers)
            layer.Visible = !hidden;
        _hudLayer?.SetScreenHidden(hidden);
    }
}
