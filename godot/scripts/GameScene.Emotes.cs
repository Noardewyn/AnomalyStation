using Godot;
using Anomaly.Client.Hud;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client;

// M9 slice B (protocol version 39, D256, docs/ai/specs/chat.md 5): the emote wheel on J, drawn by the radial's view (Hud/Radial) from
// Shared/Play/EmoteWheel. A pick sends tg's emote keybinding (PlaySession.Emote); "More..." the hidden verb that opens tg's emote panel.
public partial class GameScene
{
    private readonly EmoteWheel _emoteWheel = new();
    private Radial _emoteRadial;

    private void BuildEmoteWheel(CanvasLayer ui)
    {
        _emoteRadial = new Radial();
        ui.AddChild(_emoteRadial);
        _emoteRadial.Bind(_emoteWheel.Model);
        _emoteRadial.Title = "Emote";
    }

    private void UpdateEmoteWheel(double now)
    {
        if (_emoteRadial == null)
            return;
        _emoteWheel.Update(now);
        if (_emoteWheel.IsOpen && _mouse.CursorMode)
        {
            // cursor mode: the pointer aims from the screen's centre, as for the server's radial
            Vector2 pointer = GetViewport().GetMousePosition() - GetViewport().GetVisibleRect().Size / 2f;
            _emoteWheel.Model.AimAt(pointer.X, pointer.Y);
        }
        _emoteRadial.UiScale = _options.UiScale;
        _emoteRadial.Opacity = _options.RadialOpacity;
        _emoteRadial.Refresh();
    }

    // The wheel's key went down: it opens (a second press closes the wheel a tap left open).
    private bool OnEmoteWheelKey(PlayAction? action)
    {
        if (action != PlayAction.EmoteWheel)
            return false;
        if (_emoteWheel.Sticky)
            _emoteWheel.Close();
        else
            _emoteWheel.Press(App.NowSeconds);
        GetViewport().SetInputAsHandled();
        return true;
    }

    // The wheel's key went up: the segment aimed at is sent; a tap leaves the wheel open.
    private void OnEmoteWheelKeyUp(InputEventKey key)
    {
        if (!_options.Bindings.Has(PlayAction.EmoteWheel, key.PhysicalKeycode.ToString()))
            return;
        if (_emoteWheel.Release(App.NowSeconds) is RadialEntry entry)
            SendEmote(entry);
    }

    // A mouse button while the wheel is open is the wheel's: a left click picks when it waits after a tap, another button closes it.
    private bool OnEmoteWheelMouse(InputEventMouseButton button)
    {
        if (!_emoteWheel.IsOpen)
            return false;
        if (button.Pressed && _emoteWheel.Sticky)
        {
            if (button.ButtonIndex != MouseButton.Left)
                _emoteWheel.Close();
            else if (_emoteWheel.Click() is RadialEntry entry)
                SendEmote(entry);
        }
        return true;
    }

    private void SendEmote(in RadialEntry entry)
    {
        if (entry.Kind == RadialKind.EmotePanel)
            _session.Keybinding(KeybindingCode.VerbEmotePanel);
        else if (EmoteWheel.KeyOf(entry) is string key)
            _session.Emote(key);
    }
}
