using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Server.Tests.Client.Render;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M3 R6 (the owner's fourth walk): a directional window, a windoor and a border firelock are a band of a few pixels along one edge of tg's 32 x 32 square, and on a pane as high as
/// the wall only a low strip showed (the slime pens of xenobiology were half a metre high). They take the picture of their full-tile counterpart, found in tg's own types
/// (<see cref="BorderCounterparts"/>): the counterpart map for the kinds Meta uses, a full-height picture for every border piece of Meta, and what every window type of tg gets.
/// </summary>
public sealed class BorderCounterpartTests : WalkFixBase
{
    public BorderCounterpartTests(ITestOutputHelper output) : base(output)
    {
    }

    private static readonly ReflectionTypeSource Types = new();

    private static TypeDefaults Counterpart(RenderKind shape, string path)
    {
        Assert.True(BorderCounterparts.TryFind(shape, path, Types, out BorderCounterpart found), path);
        return found.Defaults;
    }

    [Theory]
    [InlineData("/obj/structure/window/spawner/directional/north", "/obj/structure/window/fulltile", "icons/obj/smooth_structures/window.dmi", "window")]
    [InlineData("/obj/structure/window/spawner/directional/south", "/obj/structure/window/fulltile", "icons/obj/smooth_structures/window.dmi", "window")]
    [InlineData("/obj/structure/window/spawner/directional/east", "/obj/structure/window/fulltile", "icons/obj/smooth_structures/window.dmi", "window")]
    [InlineData("/obj/structure/window/spawner/directional/west", "/obj/structure/window/fulltile", "icons/obj/smooth_structures/window.dmi", "window")]
    [InlineData("/obj/structure/window/reinforced/spawner/directional/north", "/obj/structure/window/reinforced/fulltile", "icons/obj/smooth_structures/reinforced_window.dmi", "reinforced_window")]
    [InlineData("/obj/structure/window/reinforced/spawner/directional/west", "/obj/structure/window/reinforced/fulltile", "icons/obj/smooth_structures/reinforced_window.dmi", "reinforced_window")]
    [InlineData("/obj/structure/window/reinforced/tinted/spawner/directional/west", "/obj/structure/window/reinforced/tinted/fulltile", "icons/obj/smooth_structures/tinted_window.dmi", "tinted_window")]
    [InlineData("/obj/structure/window/reinforced/tinted/frosted/spawner/directional/north", "/obj/structure/window/reinforced/tinted/fulltile", "icons/obj/smooth_structures/tinted_window.dmi", "tinted_window")]
    [InlineData("/obj/structure/window/plasma/spawner/directional/south", "/obj/structure/window/plasma/fulltile", "icons/obj/smooth_structures/plasma_window.dmi", "plasma_window")]
    [InlineData("/obj/structure/window/reinforced/plasma/spawner/directional/south", "/obj/structure/window/reinforced/plasma/fulltile", "icons/obj/smooth_structures/rplasma_window.dmi", "rplasma_window")]
    [InlineData("/obj/structure/window/reinforced/plasma/spawner/directional/north", "/obj/structure/window/reinforced/plasma/fulltile", "icons/obj/smooth_structures/rplasma_window.dmi", "rplasma_window")]
    [InlineData("/obj/structure/window/unanchored/spawner/directional/east", "/obj/structure/window/fulltile", "icons/obj/smooth_structures/window.dmi", "window")]
    [InlineData("/obj/structure/window/reinforced/unanchored/spawner/directional/east", "/obj/structure/window/reinforced/fulltile", "icons/obj/smooth_structures/reinforced_window.dmi", "reinforced_window")]
    [InlineData("/obj/structure/window/bronze/spawner/directional/east", "/obj/structure/window/bronze/fulltile", "icons/obj/smooth_structures/clockwork_window.dmi", "clockwork_window")]
    public void ADirectionalWindowTakesTheFullTileWindowOfItsKind(string path, string counterpart, string icon, string baseState)
    {
        Assert.True(BorderCounterparts.TryFind(RenderKind.DirectionalWindow, path, Types, out BorderCounterpart found), path);
        Assert.Equal(counterpart, found.TypePath);
        Assert.False(found.Own);
        Assert.Equal(icon, found.Defaults.Icon);
        Assert.Equal(baseState, found.Defaults.BaseIconState);
    }

    [Theory]
    [InlineData("/obj/machinery/door/window/left/directional/north", "window")]
    [InlineData("/obj/machinery/door/window/right/directional/east", "window")]
    [InlineData("/obj/machinery/door/window/brigdoor/left/directional/west", "reinforced_window")]
    [InlineData("/obj/machinery/door/window/brigdoor/right/directional/north", "reinforced_window")]
    [InlineData("/obj/machinery/door/window/brigdoor/security/cell/left/directional/south", "reinforced_window")]
    [InlineData("/obj/machinery/door/window/brigdoor/security/holding/left/directional/south", "reinforced_window")]
    public void AWindoorTakesThePlainOrTheReinforcedWindowByItsGlass(string path, string baseState)
    {
        // tg: the secure doors have reinf = 1 (windowdoor.dm:502), the others 0.
        Assert.Equal(baseState, Counterpart(RenderKind.Windoor, path).BaseIconState);
    }

    [Theory]
    [InlineData("/obj/machinery/door/firedoor/border_only")]
    [InlineData("/obj/machinery/door/firedoor/border_only/closed")]
    public void ABorderFirelockTakesTheFullFirelocksIcon(string path)
    {
        TypeDefaults full = Counterpart(RenderKind.FirelockBorder, path);
        Assert.Equal("icons/obj/doors/doorfireglass.dmi", full.Icon);
    }

    [Fact]
    public void WhatIsNotABorderPieceHasNoCounterpart()
    {
        Assert.False(BorderCounterparts.TryFind(RenderKind.Billboard, "/obj/structure/window/spawner/directional/north", Types, out _));
        Assert.False(BorderCounterparts.TryFind(RenderKind.DirectionalWindow, "/obj/structure/table", Types, out _));
        Assert.False(BorderCounterparts.TryFind(RenderKind.DirectionalWindow, null, Types, out _));
        // A window type the classes do not know still takes the plain window of its root.
        Assert.True(BorderCounterparts.TryFind(RenderKind.DirectionalWindow, "/obj/structure/window/no/such/type", Types, out BorderCounterpart plain));
        Assert.Equal("/obj/structure/window/fulltile", plain.TypePath);
    }

    // The picture is tg's own straight run: the counterpart's base state with the junction EAST | WEST.
    [RealIconsFact]
    public void TheFourWindowKindsOfMetaDrawTheirStraightRun()
    {
        IconLibrary library = RealIcons.NewLibrary();
        foreach ((string path, string icon, string state) in new[]
        {
            ("/obj/structure/window/spawner/directional/north", "icons/obj/smooth_structures/window.dmi", "window-12"),
            ("/obj/structure/window/reinforced/spawner/directional/east", "icons/obj/smooth_structures/reinforced_window.dmi", "reinforced_window-12"),
            ("/obj/structure/window/reinforced/tinted/spawner/directional/west", "icons/obj/smooth_structures/tinted_window.dmi", "tinted_window-12"),
            ("/obj/structure/window/reinforced/plasma/spawner/directional/south", "icons/obj/smooth_structures/rplasma_window.dmi", "rplasma_window-12"),
        })
        {
            Assert.True(BorderCounterparts.TryPicture(RenderKind.DirectionalWindow, path, "ignored", Types, library, out string gotIcon, out string gotState), path);
            Assert.Equal((icon, state), (gotIcon, gotState));
        }
    }

    // The border firelock keeps its state changes: the full firelock's icon has every state the border one's does.
    [RealIconsFact]
    public void ABorderFirelockKeepsItsStates()
    {
        IconLibrary library = RealIcons.NewLibrary();
        foreach (string state in new[] { "door_closed", "door_open", "door_opening", "door_closing", "door_deny" })
        {
            Assert.True(BorderCounterparts.TryPicture(RenderKind.FirelockBorder, "/obj/machinery/door/firedoor/border_only", state, Types, library, out string icon, out string got), state);
            Assert.Equal(("icons/obj/doors/doorfireglass.dmi", state), (icon, got));
        }
        // A state the full firelock has not: no counterpart picture, the piece's own band is stretched instead.
        Assert.False(BorderCounterparts.TryPicture(RenderKind.FirelockBorder, "/obj/machinery/door/firedoor/border_only", "no_such_state", Types, library, out _, out _));
    }

    // The windoor's states (left, leftopen, leftopening, leftdeny...) do not matter: the pane is the same glass shut and open, and slides.
    [RealIconsFact]
    public void AWindoorsPaneIsTheSameGlassInEveryState()
    {
        IconLibrary library = RealIcons.NewLibrary();
        foreach (string state in new[] { "left", "leftopen", "leftopening", "leftclosing", "leftdeny", "rightsecure", "rightsecureopen" })
        {
            Assert.True(BorderCounterparts.TryPicture(RenderKind.Windoor, "/obj/machinery/door/window/left/directional/north", state, Types, library, out string icon, out string got), state);
            Assert.Equal(("icons/obj/smooth_structures/window.dmi", "window-12"), (icon, got));
        }
    }

    // The owner's rule: every directional window of Meta gets a full-height picture (and so does every windoor and border firelock): its drawn rows reach from the top of the square to
    // the bottom, where tg's own band covers 7 of 32 rows (windoors 5, the border firelock 12).
    [RealIconsFact]
    public void EveryBorderPieceOfMetaGetsAFullHeightPicture()
    {
        MetaWorld meta = RequireMeta();
        if (meta == null)
            return;
        IconLibrary library = RealIcons.NewLibrary();
        var kinds = new SortedDictionary<string, (int Count, string Picture, int Top, int Bottom)>(StringComparer.Ordinal);
        foreach (FakeMapEntity e in meta.Map.Entities)
        {
            RenderKind shape = PropKinds.Classify(e.TypePath);
            if (!BorderCounterparts.Applies(shape))
                continue;
            bool found = BorderCounterparts.TryPicture(shape, e.TypePath, e.IconState, Types, library, out string icon, out string state);
            Assert.True(found, e.TypePath + " (" + e.IconState + ") has no full-tile picture");
            Assert.True(library.TryGetIcon(icon, out IIcon sheet), icon);
            Assert.True(sheet.TryGetState(state, out IIconState iconState), icon + " " + state);
            Raster picture = iconState.Get(0, 0);
            (int top, int bottom) = DrawnRows(picture);
            Assert.True(top <= 1 && bottom >= picture.Height - 2, e.TypePath + ": " + icon + " " + state + " draws rows " + top + ".." + bottom + " of " + picture.Height);
            string key = shape + " " + e.TypePath;
            kinds[key] = kinds.TryGetValue(key, out var seen) ? (seen.Count + 1, icon + " " + state, top, bottom) : (1, icon + " " + state, top, bottom);
        }
        foreach (var kind in kinds)
            _output.WriteLine(kind.Key + " x" + kind.Value.Count + " -> " + kind.Value.Picture + " rows " + kind.Value.Top + ".." + kind.Value.Bottom);
        Assert.Contains(kinds.Keys, k => k.StartsWith("DirectionalWindow /obj/structure/window/reinforced/spawner/directional/", StringComparison.Ordinal));
        Assert.Contains(kinds.Keys, k => k.StartsWith("DirectionalWindow /obj/structure/window/spawner/directional/", StringComparison.Ordinal));
    }

    // tg's own sprites for the same pieces are the band the owner saw (the proof that the fix is needed): 7 rows of 32 for a window.
    [RealIconsFact]
    public void TgsOwnDirectionalWindowSpriteIsALowBand()
    {
        IconLibrary library = RealIcons.NewLibrary();
        Assert.True(library.TryGetIcon("icons/obj/structures.dmi", out IIcon sheet));
        foreach (string state in new[] { "window", "rwindow", "twindow", "fwindow", "plasmawindow", "plasmarwindow" })
        {
            Assert.True(sheet.TryGetState(state, out IIconState iconState), state);
            (int top, int bottom) = DrawnRows(iconState.Get(0, 0));
            Assert.True(bottom - top + 1 <= 8, state + " draws rows " + top + ".." + bottom);
        }
    }

    // What every window type of tg gets, so that a kind with no counterpart is known (the stretched band of its own sprite): none of them lacks one.
    [Fact]
    public void EveryWindowTypeOfTgHasACounterpart()
    {
        var withoutOne = new List<string>();
        var approximate = new List<string>();
        int checkedTypes = 0;
        foreach (string path in AllClassPaths())
        {
            RenderKind shape = PropKinds.Classify(path);
            if (!BorderCounterparts.Applies(shape) || !Types.TryGet(path, out _))
                continue;
            checkedTypes++;
            if (!BorderCounterparts.TryFind(shape, path, Types, out BorderCounterpart found))
                withoutOne.Add(shape + " " + path);
            else if (found.Own)
                approximate.Add(path + " is full-tile itself: " + found.TypePath);
        }
        _output.WriteLine(checkedTypes + " border piece types of tg; without a counterpart: " + (withoutOne.Count == 0 ? "none" : string.Join(", ", withoutOne)));
        foreach (string line in approximate)
            _output.WriteLine(line);
        Assert.True(checkedTypes > 100, checkedTypes.ToString());
        Assert.Empty(withoutOne);
    }

    // The first and last rows of the picture with a drawn pixel.
    private static (int Top, int Bottom) DrawnRows(Raster picture)
    {
        int top = -1, bottom = -1;
        for (int y = 0; y < picture.Height; y++)
        {
            for (int x = 0; x < picture.Width; x++)
            {
                if (picture.Pixels[(y * picture.Width + x) * 4 + 3] == 0)
                    continue;
                top = top < 0 ? y : top;
                bottom = y;
                break;
            }
        }
        return (top, bottom);
    }
}
