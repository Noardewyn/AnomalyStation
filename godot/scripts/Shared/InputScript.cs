using System.Globalization;

namespace Anomaly.Client.Shared;

/// <summary>What a <see cref="ScriptedInput"/> does.</summary>
public enum ScriptAction : byte
{
    /// <summary>Press a key (<see cref="ScriptedInput.Key"/>: W, A, S, D, Up, Down, Left, Right).</summary>
    Press,

    /// <summary>Release a key.</summary>
    Release,

    /// <summary>Set the view yaw in degrees (what moving the mouse does), Godot's convention: 90 faces west.</summary>
    Yaw,

    /// <summary>Set the view pitch in degrees.</summary>
    Pitch,

    /// <summary>Click a mouse button at the crosshair (<see cref="ScriptedInput.Key"/>: LEFT, RIGHT or MIDDLE), as the player's click would: left is the help click, right the shove (M2).</summary>
    Click,

    /// <summary>Type <see cref="ScriptedInput.Text"/>: a key press and release with its character per letter (a text field's input).</summary>
    Type,

    /// <summary>Protocol version 17: press the left button at <see cref="ScriptedInput.At"/>, move the pointer to <see cref="ScriptedInput.To"/> and let go there (a drag and drop in cursor mode).</summary>
    Drag,

    /// <summary>A trackpad's gesture at <see cref="ScriptedInput.At"/>: two fingers moved by <see cref="ScriptedInput.To"/> (<c>pan=DXxDY@XxY</c>), or, with <see cref="ScriptedInput.Value"/> above 0, a pinch of that factor (<c>pinch=FACTOR@XxY</c>).</summary>
    Gesture,

    /// <summary>
    /// Package T, slice T4c (test tooling): the context radial's entry <see cref="ScriptedInput.Text"/> picked (<c>radial=LABEL</c>): the right
    /// button held at the crosshair as a player's (the radial opens), the entry of that label aimed (<c>RadialModel.AimAtLabel</c>) in place
    /// of the mouse's movement, and the button let go, so the release's own choice and dispatch run.
    /// </summary>
    Radial,
}

/// <summary>One step of an input script: at <see cref="AtSeconds"/> after the game started, do <see cref="Action"/>.</summary>
public readonly record struct ScriptedInput(double AtSeconds, ScriptAction Action, string Key, double Value)
{
    /// <summary>A click's point on the screen in pixels (<c>click=left@X x Y</c>), or null: the crosshair.</summary>
    public (float X, float Y)? At { get; init; }

    /// <summary>The text of a <see cref="ScriptAction.Type"/> step.</summary>
    public string Text { get; init; }

    /// <summary>Where a <see cref="ScriptAction.Drag"/> lets go, in screen pixels.</summary>
    public (float X, float Y)? To { get; init; }
}

/// <summary>
/// The <c>--keys</c> script of the client (an input test without a keyboard or a mouse): a comma-separated list of
/// <c>SECONDS:STEP</c> with <c>STEP</c> one of <c>W+</c> (press), <c>W-</c> (release), <c>yaw=DEG</c>, <c>pitch=DEG</c>; keys are
/// W, A, S, D, UP, DOWN, LEFT, RIGHT, and, since M2, U, F (combat mode) and C (rest since ui-overhaul.md section 3, walk/run before; SHIFT tapped alone switches walk and run now), and QUOTELEFT, F9, F10, F3 and F4 (the console, the analyzer and the options panels, the debug HUD on F3: <c>--keys=1:F3+,1.1:F3-</c> hides it for a screenshot; F4 is the names overlay), and, since M3 I1c, L (the dev flashlight until ui-overhaul.md section 6, slice E, which moved it to U: <c>--keys=1:U+,1.1:U-</c>; L is the language menu now), and, since M4b, F8 (the dev menu before D137), and, since D137, BACKSLASH (the dev menu, the | key) and F12 (fullscreen until ui-overhaul.md section 3, F11 now), and ESCAPE, and ENTER and BACKSPACE (a field's submit and erase), and F5 (the view: a ghost's free look and first person, D160), and T, Y, M, O and P (the say window, M9; Y is the emote wheel since ui-overhaul.md), and J (the emote wheel, M9 slice B; the pet commands now), and META, S, DELETE, V and ALT (the map editor's menu keys: <c>--keys=1:META+,1.1:S+,1.2:S-,1.3:META-</c> is Command+S), and SEMICOLON, BRACKETLEFT, APOSTROPHE and F11 (ui-overhaul.md: the radio, OOC and me keys, Shift+' with SHIFT held, and fullscreen: <c>--keys=1:SEMICOLON+,1.1:SEMICOLON-</c>); <c>click=left@XxY</c> clicks a point of the screen (pixels; <c>click=wheelup@XxY</c> and <c>click=wheeldown@XxY</c> turn the wheel a notch there), <c>drag=left@XxY&gt;XxY</c> drags from one point to another in cursor mode (protocol version 17; <c>right</c> and <c>middle</c> drag with those buttons) and <c>type=TEXT</c> types letters into a text field (2026-10-14, the dev menu's repro); <c>click=left</c> and
/// <c>click=right</c> click the mob under the crosshair; <c>radial=LABEL</c> (slice T4c) picks the context radial's entry of that label on what is under
/// the crosshair (<c>radial=Turn_over</c>: case ignored, _ a space); <c>MOUSELEFT+</c> and <c>MOUSELEFT-</c> (and <c>MOUSERIGHT</c>, <c>MOUSEMIDDLE</c>) press a
/// mouse button at the crosshair and let it go at a later step, so view steps can turn the view while it is held. Example: <c>--keys=1:W+,3:yaw=90,5:W-</c> walks forward from 1 s, turns the view west
/// at 3 s (the same key now walks west) and lets go at 5 s. The game scene feeds the key steps to the engine's input
/// (<c>Input.ParseInputEvent</c>) so that the whole path from the key state to the <c>InputFrame</c> runs.
/// </summary>
public static class InputScript
{
    private static readonly HashSet<string> Keys = new(StringComparer.Ordinal) { "W", "A", "S", "D", "UP", "DOWN", "LEFT", "RIGHT", "U", "F", "C", "QUOTELEFT", "F9", "F10", "F3", "F4", "L", "F8", "BACKSLASH", "F12", "ESCAPE", "Z", "Q", "E", "X", "R", "TAB", "ENTER", "BACKSPACE", "F5", "F6", "SHIFT", "B", "T", "Y", "M", "O", "P", "J", "H", "SPACE", "CTRL", "META", "S", "DELETE", "V", "ALT",
        "SEMICOLON", "BRACKETLEFT", "APOSTROPHE", "F11", // ui-overhaul.md section 5: the radio, OOC and me keys and fullscreen
        // the map editor's own keys (docs/ai/specs/map-editor.md 5.16), by the engine's names of them
        "KEY1", "KEY2", "KEY3", "KEY4", "EQUAL", "MINUS", "PERIOD", "COMMA", "PAGEUP", "PAGEDOWN", "HOME", "KPADD", "KPSUBTRACT", "G",
        "KEY5", "KEY6", "KEY7", "KEY8", "KEY9", "KEY0", // ui-overhaul.md section 10: the hotbar's other number keys
        "I", // DV694: the inventory key
        "MOUSELEFT", "MOUSERIGHT", "MOUSEMIDDLE" }; // a mouse button pressed (+) and let go (-) at the crosshair, with view steps between (ui-overhaul.md section 17)

    /// <summary>Parses a script; on an error returns false with the problem.</summary>
    public static bool TryParse(string text, out List<ScriptedInput> steps, out string error)
    {
        steps = new List<ScriptedInput>();
        if (ParseInto(text, steps, out error))
            return true;
        steps.Clear();
        return false;
    }

    private static bool ParseInto(string text, List<ScriptedInput> steps, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "the script is empty";
            return false;
        }
        foreach (string part in text.Split(','))
        {
            string item = part.Trim();
            int colon = item.IndexOf(':');
            if (colon <= 0 || !TryDouble(item.Substring(0, colon), out double at) || at < 0)
            {
                error = $"'{item}': expected SECONDS:STEP";
                return false;
            }
            string step = item.Substring(colon + 1).Trim();
            if (step.StartsWith("yaw=", StringComparison.OrdinalIgnoreCase) || step.StartsWith("pitch=", StringComparison.OrdinalIgnoreCase))
            {
                int equals = step.IndexOf('=');
                if (!TryDouble(step.Substring(equals + 1), out double degrees))
                {
                    error = $"'{item}': expected degrees";
                    return false;
                }
                steps.Add(new ScriptedInput(at, step[0] is 'y' or 'Y' ? ScriptAction.Yaw : ScriptAction.Pitch, null, degrees));
                continue;
            }
            if (step.StartsWith("click=", StringComparison.OrdinalIgnoreCase))
            {
                // click=left, or click=left@XxY: at a point of the screen in pixels (a window of the dev menu)
                string button = step.Substring(6).Trim().ToUpperInvariant();
                (float X, float Y)? point = null;
                int atSign = button.IndexOf('@');
                if (atSign >= 0)
                {
                    string[] xy = button.Substring(atSign + 1).Split('X');
                    if (xy.Length != 2 || !TryDouble(xy[0], out double px) || !TryDouble(xy[1], out double py) || px < 0 || py < 0)
                    {
                        error = $"'{item}': expected click=BUTTON@XxY (pixels)";
                        return false;
                    }
                    point = ((float)px, (float)py);
                    button = button.Substring(0, atSign);
                }
                if (button is not ("LEFT" or "RIGHT" or "MIDDLE" or "WHEELUP" or "WHEELDOWN"))
                {
                    error = $"'{item}': expected click=left, click=right, click=middle, click=wheelup or click=wheeldown";
                    return false;
                }
                steps.Add(new ScriptedInput(at, ScriptAction.Click, button, 0) { At = point });
                continue;
            }
            if (step.StartsWith("drag=", StringComparison.OrdinalIgnoreCase))
            {
                // drag=left@XxY>XxY: a cursor-mode drag from one point of the screen to another (pixels), protocol version 17; the right and the middle
                // button drag too (the map editor's camera)
                string spec = step.Substring(5).Trim().ToUpperInvariant();
                int atSign = spec.IndexOf('@');
                int arrow = spec.IndexOf('>');
                if (atSign < 0 || arrow < atSign || spec.Substring(0, atSign) is not ("LEFT" or "RIGHT" or "MIDDLE")
                    || !TryPoint(spec.Substring(atSign + 1, arrow - atSign - 1), out (float X, float Y) from) || !TryPoint(spec.Substring(arrow + 1), out (float X, float Y) to))
                {
                    error = $"'{item}': expected drag=left@XxY>XxY (pixels; or right, middle)";
                    return false;
                }
                steps.Add(new ScriptedInput(at, ScriptAction.Drag, spec.Substring(0, atSign), 0) { At = from, To = to });
                continue;
            }
            if (step.StartsWith("pan=", StringComparison.OrdinalIgnoreCase) || step.StartsWith("pinch=", StringComparison.OrdinalIgnoreCase))
            {
                // pan=DXxDY@XxY (two fingers moved by DX, DY; either may be negative) or pinch=FACTOR@XxY, at a point of the screen in pixels: a trackpad's gestures
                bool pinch = step[1] is 'i' or 'I';
                string spec = step.Substring(step.IndexOf('=') + 1).Trim().ToUpperInvariant();
                int atSign = spec.IndexOf('@');
                string[] delta = atSign < 0 ? Array.Empty<string>() : spec.Substring(0, atSign).Split('X');
                double dx = 0, dy = 0, factor = 0;
                if (atSign < 0 || !TryPoint(spec.Substring(atSign + 1), out (float X, float Y) where)
                    || (pinch ? !(TryDouble(spec.Substring(0, atSign), out factor) && factor > 0) : delta.Length != 2 || !TryDouble(delta[0], out dx) || !TryDouble(delta[1], out dy)))
                {
                    error = $"'{item}': expected pan=DXxDY@XxY or pinch=FACTOR@XxY (pixels)";
                    return false;
                }
                steps.Add(new ScriptedInput(at, ScriptAction.Gesture, null, factor) { At = where, To = ((float)dx, (float)dy) });
                continue;
            }
            if (step.StartsWith("radial=", StringComparison.OrdinalIgnoreCase))
            {
                // radial=LABEL: the context radial's entry by its label as shown, case ignored; an underscore stands for a space (radial=Turn_over),
                // so the argument needs no quotes in a shell (a quoted radial=Turn over works too)
                string label = step.Substring(7).Replace('_', ' ').Trim();
                if (label.Length == 0 || label.Length > 64)
                {
                    error = $"'{item}': expected radial=LABEL (1 to 64 characters, no comma; _ for a space)";
                    return false;
                }
                steps.Add(new ScriptedInput(at, ScriptAction.Radial, null, 0) { Text = label });
                continue;
            }
            if (step.StartsWith("type=", StringComparison.OrdinalIgnoreCase))
            {
                string typed = step.Substring(5);
                if (typed.Length == 0 || typed.Length > 64)
                {
                    error = $"'{item}': expected type=TEXT (1 to 64 characters, no comma)";
                    return false;
                }
                steps.Add(new ScriptedInput(at, ScriptAction.Type, null, 0) { Text = typed });
                continue;
            }
            if (step.Length >= 2 && (step[^1] == '+' || step[^1] == '-'))
            {
                string key = step.Substring(0, step.Length - 1).ToUpperInvariant();
                if (!Keys.Contains(key))
                {
                    error = $"'{item}': unknown key '{key}'";
                    return false;
                }
                steps.Add(new ScriptedInput(at, step[^1] == '+' ? ScriptAction.Press : ScriptAction.Release, key, 0));
                continue;
            }
            error = $"'{item}': expected KEY+, KEY-, yaw=DEG, pitch=DEG, click=BUTTON[@XxY], drag=left@XxY>XxY, pan=DXxDY@XxY, pinch=FACTOR@XxY, radial=LABEL or type=TEXT";
            return false;
        }
        steps.Sort((a, b) => a.AtSeconds.CompareTo(b.AtSeconds)); // List.Sort is not stable, but equal times do not matter here
        return true;
    }

    private static bool TryPoint(string text, out (float X, float Y) point)
    {
        point = default;
        string[] xy = text.Split('X');
        if (xy.Length != 2 || !TryDouble(xy[0], out double px) || !TryDouble(xy[1], out double py) || px < 0 || py < 0)
            return false;
        point = ((float)px, (float)py);
        return true;
    }

    private static bool TryDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
