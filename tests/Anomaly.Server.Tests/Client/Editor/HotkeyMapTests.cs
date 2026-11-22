using Anomaly.Client.Shared.Editor;

namespace Anomaly.Server.Tests.Client.Editor;

/// <summary>The map editor's keys (docs/ai/specs/map-editor.md 5.16): which key starts which function, as the mapper set them over the defaults.</summary>
public sealed class HotkeyMapTests
{
    private const int S = 83, Z = 90, R = 82, W = 87, Equal = 61, Shift = 4194325;

    private static HotkeyMap Map() => new(new[]
    {
        new HotkeyAction("file.save", "File", "Save", new[] { new KeyCombo(S, Meta: true) }),
        new HotkeyAction("edit.redo", "Edit", "Redo", new[] { new KeyCombo(Z, Shift: true, Meta: true), new KeyCombo(89, Meta: true) }),
        new HotkeyAction("edit.turn", "Edit", "Turn", new[] { new KeyCombo(R) }),
        new HotkeyAction("edit.turnback", "Edit", "Turn back", new[] { new KeyCombo(R, Shift: true) }),
        new HotkeyAction("level.up", "Levels", "Level up", new[] { new KeyCombo(Equal), new KeyCombo(Equal, Shift: true) }),
        new HotkeyAction("view.arrows", "View", "Arrows", Array.Empty<KeyCombo>()),
        new HotkeyAction("cam.back", "Camera", "Fly back", new[] { new KeyCombo(S) }, Held: true),
        new HotkeyAction("cam.forward", "Camera", "Fly forward", new[] { new KeyCombo(W) }, Held: true),
        new HotkeyAction("cam.fast", "Camera", "Fly faster", new[] { new KeyCombo(Shift) }, Held: true),
    });

    [Fact]
    public void APressedKeyStartsItsFunction()
    {
        HotkeyMap map = Map();
        Assert.Empty(map.Clashes()); // S flies back and the command key with S saves: two keys
        Assert.Equal("file.save", map.Find(new KeyCombo(S, Meta: true)));
        Assert.Equal("edit.turn", map.Find(new KeyCombo(R)));
        Assert.Equal("edit.turnback", map.Find(new KeyCombo(R, Shift: true)));
        Assert.Equal("edit.redo", map.Find(new KeyCombo(89, Meta: true)));
        Assert.Equal("level.up", map.Find(new KeyCombo(Equal, Shift: true)));
        Assert.Null(map.Find(new KeyCombo(R, Ctrl: true)));
        // a held function is started by no press: its keys are asked for
        Assert.Null(map.Find(new KeyCombo(S)));
        Assert.Equal(new[] { new KeyCombo(S) }, map.Of("cam.back"));
        Assert.Empty(map.Of("view.arrows"));
        Assert.Empty(map.Of("no.such"));
    }

    [Fact]
    public void AKeyGivenToAFunctionIsTakenFromTheOneThatHadIt()
    {
        HotkeyMap map = Map();
        Assert.Equal("edit.turn", map.Set("view.arrows", new KeyCombo(R)));
        Assert.Equal("view.arrows", map.Find(new KeyCombo(R)));
        Assert.Empty(map.Of("edit.turn"));
        Assert.Equal("edit.turnback", map.Find(new KeyCombo(R, Shift: true))); // another key: it stays

        // a function set to a key has that key alone; one of several keys taken leaves the others
        Assert.Null(map.Set("edit.turn", new KeyCombo(84)));
        Assert.Equal("level.up", map.Set("edit.turn", new KeyCombo(Equal)));
        Assert.Equal(new[] { new KeyCombo(Equal, Shift: true) }, map.Of("level.up"));
        Assert.Null(map.Find(new KeyCombo(84)));

        // no key at all
        Assert.Null(map.Set("edit.turn", null));
        Assert.Empty(map.Of("edit.turn"));
        Assert.Throws<ArgumentException>(() => map.Set("no.such", new KeyCombo(R)));
    }

    [Fact]
    public void AHeldKeyIsTheKeyAloneOrWithShift()
    {
        HotkeyMap map = Map();
        // the flight's key given with modifiers is the key alone
        Assert.Null(map.Set("cam.forward", new KeyCombo(69, Ctrl: true, Shift: true)));
        Assert.Equal(new[] { new KeyCombo(69) }, map.Of("cam.forward"));
        // R to fly forward: the turn and the turn back (Shift with R) both lose it, for the view has R while Shift is down too
        map.Set("cam.forward", new KeyCombo(R));
        Assert.Empty(map.Of("edit.turn"));
        Assert.Empty(map.Of("edit.turnback"));
        // the command key with S is not S: the save keeps it when the flight is given S again
        Assert.Null(map.Set("cam.back", new KeyCombo(S)));
        Assert.Equal("file.save", map.Find(new KeyCombo(S, Meta: true)));
        // a plain S given to a function is taken from the flight
        Assert.Equal("cam.back", map.Set("view.arrows", new KeyCombo(S)));
        Assert.Empty(map.Of("cam.back"));
        // Shift is a key of its own for a held function
        Assert.Equal(new[] { new KeyCombo(Shift) }, map.Of("cam.fast"));
        Assert.Empty(map.Clashes());
    }

    [Fact]
    public void WhatWasChangedIsKeptAndReadAgain()
    {
        HotkeyMap map = Map();
        Assert.Empty(map.Changed());
        map.Set("view.arrows", new KeyCombo(R));
        map.Set("file.save", null);
        Dictionary<string, string> changed = map.Changed();
        Assert.Equal(new[] { "edit.turn", "file.save", "view.arrows" }, changed.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal((string.Empty, string.Empty, "82"), (changed["edit.turn"], changed["file.save"], changed["view.arrows"]));
        Assert.False(map.IsDefault("view.arrows"));
        Assert.True(map.IsDefault("level.up"));

        HotkeyMap read = Map();
        read.Load(changed);
        Assert.Equal("view.arrows", read.Find(new KeyCombo(R)));
        Assert.Null(read.Find(new KeyCombo(S, Meta: true)));
        Assert.Equal(changed, read.Changed());

        // a file that names one function alone: the key it gives is taken from the function that has it by default
        read.Load(new Dictionary<string, string> { ["view.arrows"] = "M+83 S+82", ["no.such"] = "65", ["edit.redo"] = "what" });
        Assert.Equal("view.arrows", read.Find(new KeyCombo(S, Meta: true)));
        Assert.Equal("view.arrows", read.Find(new KeyCombo(R, Shift: true)));
        Assert.Empty(read.Of("file.save"));
        Assert.Empty(read.Of("edit.turnback"));
        Assert.Empty(read.Of("edit.redo")); // (a text that is no key: the function has none)
        Assert.Equal("edit.turn", read.Find(new KeyCombo(R)));

        // its own keys again: the function that took one loses it
        Assert.Equal(new[] { "view.arrows" }, read.Reset("file.save"));
        Assert.Equal("file.save", read.Find(new KeyCombo(S, Meta: true)));
        read.ResetAll();
        Assert.Empty(read.Changed());
    }

    [Theory]
    [InlineData("82", 82, false, false, false, false)]
    [InlineData("C+S+A+M+90", 90, true, true, true, true)]
    [InlineData("M+S+90", 90, false, true, false, true)]
    public void AKeyIsWrittenAndReadAsTheSettingsFileHasIt(string text, int key, bool ctrl, bool shift, bool alt, bool meta)
    {
        var combo = new KeyCombo(key, ctrl, shift, alt, meta);
        Assert.Equal(combo, KeyCombo.Parse(text));
        Assert.Equal(combo, KeyCombo.Parse(combo.ToText()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("R")]
    [InlineData("X+82")]
    [InlineData("S+")]
    [InlineData("S+0")]
    [InlineData("-5")]
    public void ATextThatIsNoKeyIsNone(string text) => Assert.Null(KeyCombo.Parse(text));
}
