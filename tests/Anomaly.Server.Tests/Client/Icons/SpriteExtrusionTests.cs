using Anomaly.Client.Shared.Icons;

namespace Anomaly.Server.Tests.Client.Icons;

/// <summary>D285: a held thing's tg picture made solid the way Minecraft draws a held item (Hud/ViewModel).</summary>
public class SpriteExtrusionTests
{
    // A picture of the given size with the listed pixels drawn (x, y from the top), each a different colour.
    private static Raster Picture(int w, int h, params (int X, int Y)[] drawn)
    {
        var px = new byte[w * h * 4];
        int n = 0;
        foreach ((int x, int y) in drawn)
        {
            int i = (y * w + x) * 4;
            px[i] = (byte)(10 * ++n);
            px[i + 1] = 20;
            px[i + 2] = 30;
            px[i + 3] = 255;
        }
        return new Raster(w, h, px);
    }

    [Fact]
    public void AnEmptyPictureHasNoSolid()
    {
        Assert.Null(SpriteExtrusion.Build(Picture(4, 4), 0, 0, 1f));
    }

    [Fact]
    public void OnePixelIsABoxOfSixFaces()
    {
        ExtrudedSprite solid = SpriteExtrusion.Build(Picture(8, 8, (3, 5)), 0, 0, 1f);
        Assert.Equal(4, solid.SideFaces);
        Assert.Equal((2 + 4) * 2 * 3, solid.Indices.Length); // front, back and four sides, two triangles each
        Assert.Equal(1, solid.DrawnWidth);
        Assert.Equal(1, solid.DrawnHeight);
    }

    [Fact]
    public void OnlyTheOutlineGetsSides()
    {
        // an L of three pixels: (0,0) (0,1) (1,1); 8 edges face an empty pixel or the picture's border
        ExtrudedSprite solid = SpriteExtrusion.Build(Picture(2, 2, (0, 0), (0, 1), (1, 1)), 0, 0, 1f);
        Assert.Equal(8, solid.SideFaces);
        Assert.Equal(2, solid.DrawnWidth);
        Assert.Equal(2, solid.DrawnHeight);
    }

    [Fact]
    public void TheGripIsTheOriginAndAPixelIsItsSize()
    {
        // one pixel at (3, 5) from the top, the grip at its top left corner: the box spans x 0..s, y -s..0, z -s/2..s/2
        float s = 0.25f;
        ExtrudedSprite solid = SpriteExtrusion.Build(Picture(8, 8, (3, 5)), 3, 5, s);
        float[] p = solid.Positions;
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < p.Length; i += 3)
        {
            minX = Math.Min(minX, p[i]);
            maxX = Math.Max(maxX, p[i]);
            minY = Math.Min(minY, p[i + 1]);
            maxY = Math.Max(maxY, p[i + 1]);
            minZ = Math.Min(minZ, p[i + 2]);
            maxZ = Math.Max(maxZ, p[i + 2]);
        }
        Assert.Equal(0f, minX, 5);
        Assert.Equal(s, maxX, 5);
        Assert.Equal(-s, minY, 5);
        Assert.Equal(0f, maxY, 5);
        Assert.Equal(-s / 2, minZ, 5);
        Assert.Equal(s / 2, maxZ, 5);
    }

    [Fact]
    public void ASideTakesItsPixelsColour()
    {
        // two pixels side by side; every side face's texture coordinate is the middle of one of them in the 2 x 1 crop
        ExtrudedSprite solid = SpriteExtrusion.Build(Picture(4, 4, (1, 2), (2, 2)), 0, 0, 1f);
        Assert.Equal(2, solid.Texture.Width);
        Assert.Equal(1, solid.Texture.Height);
        for (int v = 8; v < solid.Uvs.Length / 2; v++) // the first 8 vertices are the front and the back
        {
            float u = solid.Uvs[v * 2], w = solid.Uvs[v * 2 + 1];
            Assert.True(Math.Abs(u - 0.25f) < 1e-5 || Math.Abs(u - 0.75f) < 1e-5, $"u {u}");
            Assert.Equal(0.5f, w, 5);
        }
        // the crop keeps the pixels' colours in order
        Assert.Equal(10, solid.Texture.Pixels[0]);
        Assert.Equal(20, solid.Texture.Pixels[4]);
    }

    // tg's fire axe in the right hand, EAST (axes_righthand.dmi fireaxe0): the head up front, the handle down to the back.
    private static readonly string[] FireAxe =
    {
        "...............####.............",
        "...............#####............",
        "...............#######..........",
        "...............#######..........",
        "................######..........",
        "................######..........",
        ".................####...........",
        "...........##....###............",
        ".........####...................",
        "........####....................",
        ".......###......................",
        "......###.......................",
        "......###.......................",
        ".......##.......................",
    };

    private static Raster Ascii(string[] rows, int top)
    {
        var drawn = new List<(int, int)>();
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                if (rows[y][x] == '#')
                    drawn.Add((x, y + top));
        return Picture(32, 32, drawn.ToArray());
    }

    [Fact]
    public void ALargeThingIsHeldNearItsHandlesEnd()
    {
        // tg's hand pixel (13.5, 19.3) is under the axe's head; the hand belongs at the handle's far end, around (7, 26)
        (double x, double y) = HeldItemGrip.Grip(Ascii(FireAxe, 14), 13.5, 19.3);
        Assert.InRange(x, 5.5, 9.5);
        Assert.InRange(y, 24.0, 27.5);
    }

    [Fact]
    public void AScrewdriversHeavyHandleOnTheLeftIsStillItsHandle()
    {
        string[] screwdriver =
        {
            "######..........",
            "################",
            "######..........",
        };
        (double x, _) = HeldItemGrip.Grip(Ascii(screwdriver, 15), 16, 16);
        Assert.InRange(x, 0.5, 3.5);
    }

    [Fact]
    public void ASmallThingIsHeldWhereTgDrawsTheHand()
    {
        (double x, double y) = HeldItemGrip.Grip(Picture(32, 32, (13, 19), (14, 19), (13, 20), (14, 21)), 13.5, 19.3);
        Assert.Equal(13.5, x, 5);
        Assert.Equal(19.3, y, 5);
    }

    [Fact]
    public void AFaintPixelIsInTheFaceButHasNoSides()
    {
        Raster picture = Picture(6, 6, (1, 1));
        picture.Pixels[(4 * 6 + 4) * 4 + 3] = 48; // tg's rad glow outline's alpha (#39ff1430)
        ExtrudedSprite solid = SpriteExtrusion.Build(picture, 0, 0, 1f);
        Assert.Equal((4, 4), (solid.DrawnWidth, solid.DrawnHeight));
        Assert.Equal(4, solid.SideFaces);
    }

    [Fact]
    public void LargeAndSmallThings()
    {
        Assert.True(HeldItemGrip.IsLarge(16, 14));
        Assert.False(HeldItemGrip.IsLarge(9, 6));
    }
}
