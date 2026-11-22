using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>
/// M6 slice 7 (D155, protocol version 25): the compositor draws BYOND's filters on an appearance with its children (hand-made 4 x 4
/// icons): an outline round the drawn pixels, an alpha mask from a render target that is not drawn itself (tg's gloves of a
/// one-handed human), a displacement map (tg's cranial fissure) and a colour matrix.
/// </summary>
public class CompositorFilterTests
{
    private const uint Red = 0xFF0000FF, Green = 0x00FF00FF, Clear = 0;

    private readonly TestTables _tables = new();
    private readonly IconLibrary _library = new(new MemoryIconFiles());
    private readonly FakeIcon _icon = new();

    public CompositorFilterTests()
    {
        _library.Register("a.dmi", _icon);
        _icon.State("block", 1, 1, Raster.Solid(4, 4, Red));
        var dot = new Raster(4, 4);
        dot.Set(1, 1, Green);
        dot.Set(2, 1, Green);
        dot.Set(1, 2, Green);
        dot.Set(2, 2, Green);
        _icon.State("dot", 1, 1, dot);
        var left = new Raster(4, 4);
        for (int y = 0; y < 4; y++)
        {
            left.Set(0, y, 0x000000FF);
            left.Set(1, y, 0x000000FF);
        }
        _icon.State("left", 1, 1, left);
        // a map that moves every pixel one to the right's content (red 255: x + size)
        _icon.State("shift", 1, 1, Raster.Solid(4, 4, 0xFF8000FF));
        // tg's height masks: green 0 squashes a body down (cut.dmi's Cut1, the monkey's torso)
        _icon.State("down", 1, 1, Raster.Solid(4, 4, 0x800000FF));
    }

    private ComposedRaster Compose(TableRef root)
    {
        var compositor = new AppearanceCompositor(_library);
        Assert.True(compositor.TryCompose(_tables, root, ByondDir.South, 0, out ComposedRaster composed));
        return composed;
    }

    // The pixel at column x, row y counted from the icon square's bottom.
    private static uint At(ComposedRaster p, int x, int y) => p.Image.Get(p.OriginX + x, p.Image.Height - 1 - (p.OriginY + y));

    [Fact]
    public void An_outline_draws_round_the_picture_and_grows_it()
    {
        TableRef root = _tables.Add("a.dmi", "dot", e => e.Filters = new List<FilterEntry> { new() { Kind = FilterKind.Outline, Size = 1, Rgba = Red } });
        ComposedRaster p = Compose(root);
        Assert.Equal(6, p.Image.Width);
        Assert.Equal(Green, At(p, 1, 1));
        Assert.Equal(Red, At(p, 0, 1)); // beside the dot
        Assert.Equal(Red, At(p, 1, 0));
        Assert.Equal(Clear, At(p, 0, 0) & 255); // a round outline of 1 leaves the diagonal corner
    }

    [Fact]
    public void An_alpha_mask_from_a_hidden_render_target_cuts_the_picture_and_is_not_drawn()
    {
        TableRef mask = _tables.Add("a.dmi", "left", e => e.RenderTarget = _tables.Str("*MissGlove 1 R"));
        TableRef root = _tables.Add("a.dmi", "block", e =>
        {
            e.Overlays.Add(mask);
            e.Filters = new List<FilterEntry> { new() { Kind = FilterKind.Alpha, RenderSource = _tables.Str("*MissGlove 1 R"), Flags = FilterFlags.MaskInverse } };
        });
        ComposedRaster p = Compose(root);
        for (int y = 0; y < 4; y++)
        {
            Assert.Equal(Clear, At(p, 0, y) & 255); // the mask's opaque half is cut away
            Assert.Equal(Clear, At(p, 1, y) & 255);
            Assert.Equal(Red, At(p, 2, y)); // the rest stays, and the mask's black is drawn nowhere
            Assert.Equal(Red, At(p, 3, y));
        }
    }

    [Fact]
    public void A_displacement_map_moves_the_pixels_and_a_colour_matrix_maps_them()
    {
        TableRef shifted = _tables.Add("a.dmi", "dot", e => e.Filters = new List<FilterEntry>
        {
            new() { Kind = FilterKind.Displace, Icon = _tables.Res("a.dmi"), IconState = _tables.Str("shift"), Size = 1 },
        });
        ComposedRaster p = Compose(shifted);
        Assert.Equal(Green, At(p, 0, 1)); // column 0 shows what was in column 1
        Assert.Equal(Green, At(p, 1, 1));
        Assert.Equal(Clear, At(p, 2, 1) & 255);

        TableRef squashed = _tables.Add("a.dmi", "dot", e => e.Filters = new List<FilterEntry>
        {
            new() { Kind = FilterKind.Displace, Icon = _tables.Res("a.dmi"), IconState = _tables.Str("down"), Size = 1 },
        });
        p = Compose(squashed);
        Assert.Equal(Green, At(p, 1, 0)); // the dot's two rows, 1 and 2, one lower
        Assert.Equal(Green, At(p, 1, 1));
        Assert.Equal(Clear, At(p, 1, 2) & 255);

        // red into green: the matrix's rows are what each input channel gives (rr rg rb ra, gr gg gb ga, ...)
        var swap = new float[20];
        swap[1] = 1; swap[4] = 1; swap[10] = 1; swap[15] = 1;
        TableRef mapped = _tables.Add("a.dmi", "block", e => e.Filters = new List<FilterEntry> { new() { Kind = FilterKind.Color, ColorMatrix = swap } });
        Assert.Equal(Green, At(Compose(mapped), 2, 2));
    }

    [Fact]
    public void An_icons_blend_recipe_is_drawn_as_tg_draws_damaged_clothing()
    {
        // the damage pattern: grey on the left half, transparent on the right
        var pattern = new Raster(4, 4);
        for (int y = 0; y < 4; y++)
        {
            pattern.Set(0, y, 0x808080FF);
            pattern.Set(1, y, 0x808080FF);
        }
        var damage = new FakeIcon();
        damage.State("itemdamaged", 1, 1, pattern);
        _library.Register("icons/effects/item_damage.dmi", damage);

        // clothing.dm:519 as the server keeps it (Anomaly.Core.DmShim.icon): white over the drawn pixels, then times the pattern
        var made = Anomaly.Core.DmShim.Dm.icon("a.dmi", "dot", null, 1);
        made.Blend("#fff", Anomaly.Core.DmShim.Dm.ICON_ADD);
        made.Blend(Anomaly.Core.DmShim.Dm.icon("icons/effects/item_damage.dmi", "itemdamaged"), Anomaly.Core.DmShim.Dm.ICON_MULTIPLY);
        TableRef overlay = _tables.Add(made.dm_resource(), "dot");
        ComposedRaster p = Compose(overlay);
        Assert.Equal(0x808080FFu, At(p, 1, 1)); // the dot made white, then greyed by the pattern
        Assert.Equal(Clear, At(p, 2, 1) & 255); // the pattern's transparent half
        Assert.Equal(Clear, At(p, 0, 0) & 255); // outside the dot: white added, alpha untouched
    }

    [Fact]
    public void A_beams_cut_tip_recipe_erases_above_the_cut_and_fades_by_its_mask()
    {
        // beam.dm:299-313 as the server keeps it (the M7 zaps slice): the tip erased from row 3 up, then times a mask that is opaque
        // white below, half-transparent white on row 2 and clear above; shown with icon_state "" (BYOND's icon(file, state) holds the
        // state as its default state)
        var made = Anomaly.Core.DmShim.Dm.icon("a.dmi", "block");
        made.DrawBox(null, 1, 3, 4, 4);
        var mask = Anomaly.Core.DmShim.Dm.icon("a.dmi", "block");
        mask.DrawBox(Anomaly.Core.DmShim.Dm.rgb(255, 255, 255, 255), 1, 1, 4, 4);
        mask.DrawBox(Anomaly.Core.DmShim.Dm.rgb(255, 255, 255, 128), 1, 2, 4, 2);
        mask.DrawBox(null, 1, 3, 4, 4);
        made.Blend(mask, Anomaly.Core.DmShim.Dm.ICON_MULTIPLY);
        TableRef tip = _tables.Add(made.dm_resource(), "");
        ComposedRaster p = Compose(tip);
        Assert.Equal(Red, At(p, 0, 0)); // below the fade: as it was
        Assert.Equal(0xFF000080u, At(p, 3, 1)); // the fading row: half its alpha
        Assert.Equal(Clear, At(p, 0, 2) & 255); // the cut
        Assert.Equal(Clear, At(p, 3, 3) & 255);
    }
    [Fact]
    public void An_icons_MapColors_recipe_maps_every_pixel_as_byond_does()
    {
        // BYOND's MapColors (the values checked against BYOND's own results in OpenDream's Tests/Icon/expected_results/MapColors.dmi):
        // the channels through the matrix, rounded to the nearest; the 12 number form keeps the alpha, the 20 number form maps it
        var grey = Anomaly.Core.DmShim.Dm.icon("a.dmi", "block");
        grey.MapColors(0.299f, 0.299f, 0.299f, 0.587f, 0.587f, 0.587f, 0.114f, 0.114f, 0.114f);
        Assert.Equal(0x4C4C4CFFu, At(Compose(_tables.Add(grey.dm_resource(), "block")), 1, 1)); // 255 * 0.299 = 76.2

        var faded = Anomaly.Core.DmShim.Dm.icon("a.dmi", "dot");
        faded.MapColors(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0.5f, 0, 0, 0, 0);
        ComposedRaster p = Compose(_tables.Add(faded.dm_resource(), "dot"));
        Assert.Equal(0x00FF0080u, At(p, 1, 1)); // alpha 127.5, rounded up
        Assert.Equal(Clear, At(p, 0, 0) & 255);
    }

    [Fact]
    public void A_husks_ColorTone_recipe_tones_each_state_of_the_file()
    {
        // tg's ColorTone(HUSK_COLOR_TONE) on new /icon(file) (_bodyparts.dm:1487, __HELPERS/icons.dm:213): every state of the file,
        // each shown by its own icon_state, grey toned black -> rgb(96, 88, 80) at grey 90 -> white; the copy blended back in is
        // the same state's (the orange's upper half). Expected pixels worked by hand through BYOND's MapColors and Blend.
        var husk = Anomaly.Core.DmShim.Dm.icon("a.dmi");
        Anomaly.Core.Tg.GlobalProcs.ColorTone(husk, Anomaly.Core.Tg.Defines.HUSK_COLOR_TONE);
        string recipe = husk.dm_resource();
        Assert.Equal(0x524B44FFu, At(Compose(_tables.Add(recipe, "block")), 1, 1)); // red: grey 77, below the tone's grey
        Assert.Equal(0x9C9792FFu, At(Compose(_tables.Add(recipe, "shift")), 1, 1)); // orange: grey 152, above it
        ComposedRaster dot = Compose(_tables.Add(recipe, "dot"));
        Assert.Equal(Clear, At(dot, 0, 0) & 255); // a transparent pixel stays transparent
    }
}
