using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client;

/// <summary>M1 P6: the <c>--keys</c> input script (an input test without a keyboard).</summary>
public class InputScriptTests
{
    [Fact]
    public void AScriptParsesIntoSortedSteps()
    {
        Assert.True(InputScript.TryParse("3:yaw=90, 1:W+ ,5.5:w-,2:pitch=-20,1:Up+", out List<ScriptedInput> steps, out string error));
        Assert.Null(error);
        Assert.Equal(5, steps.Count);
        Assert.Equal(new[] { 1.0, 1.0, 2.0, 3.0, 5.5 }, steps.Select(s => s.AtSeconds));
        Assert.Contains(new ScriptedInput(1, ScriptAction.Press, "W", 0), steps);
        Assert.Contains(new ScriptedInput(1, ScriptAction.Press, "UP", 0), steps);
        Assert.Contains(new ScriptedInput(5.5, ScriptAction.Release, "W", 0), steps);
        Assert.Contains(new ScriptedInput(2, ScriptAction.Pitch, null, -20), steps);
        Assert.Contains(new ScriptedInput(3, ScriptAction.Yaw, null, 90), steps);
    }

    [Fact]
    public void TheDebugHudKeyCanBeScriptedToHideItForAScreenshot()
    {
        // ui-overhaul.md section 3: the debug HUD is on F3 (SS14's ShowDebugMonitors); F4, the names overlay's now, still parses
        Assert.True(InputScript.TryParse("1:F3+,1.1:F3-", out List<ScriptedInput> steps, out _));
        Assert.Equal(new[] { new ScriptedInput(1, ScriptAction.Press, "F3", 0), new ScriptedInput(1.1, ScriptAction.Release, "F3", 0) }, steps);
        Assert.True(InputScript.TryParse("1:F4+,1.1:F4-", out _, out _));
    }

    [Fact]
    public void TheRadioOocMeAndFullscreenKeysCanBeScripted()
    {
        // ui-overhaul.md section 5: ;, [, ' (Shift+' is me) and F11 for a playtest without the operating system's key events
        Assert.True(InputScript.TryParse("1:SEMICOLON+,1.1:SEMICOLON-,2:BRACKETLEFT+,2.1:BRACKETLEFT-,3:SHIFT+,3.1:APOSTROPHE+,3.2:APOSTROPHE-,3.3:SHIFT-,4:F11+,4.1:F11-",
            out List<ScriptedInput> steps, out string error), error);
        Assert.Equal(new[] { "SEMICOLON", "BRACKETLEFT", "SHIFT", "APOSTROPHE", "F11" }, steps.Where(s => s.Action == ScriptAction.Press).Select(s => s.Key));
        // the engine's names of the keys (GameScene.ScriptKey parses them so), which the default bindings name
        foreach (string key in new[] { "SEMICOLON", "BRACKETLEFT", "APOSTROPHE", "F11" })
            Assert.Contains(KeyBindings.Defaults().ActionOfPress(key == "F11" ? "F11" : char.ToUpperInvariant(key[0]) + key.Substring(1).ToLowerInvariant(), shift: key == "APOSTROPHE"),
                new PlayAction?[] { PlayAction.Radio, PlayAction.Ooc, PlayAction.Me, PlayAction.Fullscreen });
        Assert.False(InputScript.TryParse("1:COLON+", out _, out _));
    }

    [Fact]
    public void TheDevFlashlightKeyCanBeScripted()
    {
        Assert.True(InputScript.TryParse("2:L+,2.1:L-", out List<ScriptedInput> steps, out _));
        Assert.Equal(new[] { new ScriptedInput(2, ScriptAction.Press, "L", 0), new ScriptedInput(2.1, ScriptAction.Release, "L", 0) }, steps);
    }

    [Fact]
    public void TheDevMenuKeyCanBeScripted()
    {
        Assert.True(InputScript.TryParse("6:F8+,6.1:F8-", out List<ScriptedInput> steps, out _));
        Assert.Equal(new[] { new ScriptedInput(6, ScriptAction.Press, "F8", 0), new ScriptedInput(6.1, ScriptAction.Release, "F8", 0) }, steps);
    }

    [Fact]
    public void TheDevMenuCanBeDrivenWithEscClicksAtAPointAndTypedText()
    {
        // the owner's repro (2026-10-14): search, spawn with a map click, close with Esc, reopen, click the search again
        Assert.True(InputScript.TryParse("2:BACKSLASH+,16:click=left@700x419,16.5:type=crowbar,19.5:ESCAPE+,19.6:ESCAPE-,20:click=right", out List<ScriptedInput> steps, out _));
        Assert.Equal(6, steps.Count);
        Assert.Equal((ScriptAction.Click, "LEFT", (700f, 419f)), (steps[1].Action, steps[1].Key, steps[1].At.Value));
        Assert.Equal((ScriptAction.Type, "crowbar"), (steps[2].Action, steps[2].Text));
        Assert.Equal((ScriptAction.Press, "ESCAPE"), (steps[3].Action, steps[3].Key));
        Assert.Null(steps[5].At); // at the crosshair
    }

    [Theory]
    [InlineData("1:click=left@700")]
    [InlineData("1:click=left@ax5")]
    [InlineData("1:click=up@5x5")]
    [InlineData("1:type=")]
    public void BadDevMenuStepsAreRefused(string text)
    {
        Assert.False(InputScript.TryParse(text, out List<ScriptedInput> steps, out string error));
        Assert.False(string.IsNullOrEmpty(error));
        Assert.Empty(steps);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("W+")]
    [InlineData("x:W+")]
    [InlineData("1:K+")]
    [InlineData("1:W")]
    [InlineData("1:yaw=north")]
    [InlineData("-1:W+")]
    [InlineData("1:W+,,2:W-")]
    [InlineData("1:+")]
    public void BadScriptsAreRefusedWithAReason(string text)
    {
        Assert.False(InputScript.TryParse(text, out List<ScriptedInput> steps, out string error));
        Assert.False(string.IsNullOrEmpty(error));
        Assert.Empty(steps);
    }

    [Fact]
    public void TheArgumentCarriesTheScript()
    {
        var args = ClientArgs.Parse(new[] { "--keys=1:W+,3:yaw=90" });
        Assert.Empty(args.Errors);
        Assert.Equal(2, args.Keys.Count);
        Assert.True(args.Automated); // a scripted run leaves the mouse alone
        args = ClientArgs.Parse(new[] { "--keys=1:Z+,1.1:Z-,2:TAB+,2.1:TAB-" }); // the action keys (use in hand, cursor mode)
        Assert.Empty(args.Errors);
        Assert.Equal(4, args.Keys.Count);
        args = ClientArgs.Parse(new[] { "--keys=1:K+" });
        Assert.Single(args.Errors);
        Assert.Empty(args.Keys);
    }

    // A trackpad's gestures (the map editor's camera): two fingers moved, and a pinch, at a point of the screen.
    [Fact]
    public void GesturesAreParsed()
    {
        Assert.True(InputScript.TryParse("1:pan=0x-3.5@1200x700,2:pinch=1.25@640x360", out List<ScriptedInput> steps, out string error), error);
        Assert.Equal((ScriptAction.Gesture, (1200f, 700f), (0f, -3.5f), 0.0), (steps[0].Action, steps[0].At.Value, steps[0].To.Value, steps[0].Value));
        Assert.Equal((ScriptAction.Gesture, (640f, 360f), 1.25), (steps[1].Action, steps[1].At.Value, steps[1].Value));
        Assert.False(InputScript.TryParse("1:pan=3@10x10", out _, out _));
        Assert.False(InputScript.TryParse("1:pinch=0@10x10", out _, out _));
    }
}
