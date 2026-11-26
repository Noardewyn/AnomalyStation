using Anomaly.Client.Shared.Icons;

namespace Anomaly.Client.Shared.World;

// Owner: M3 P6. Engine-free. The shared picture atlas of the static-prop batch (scope 2i): billboards, wall decals and the shaped
// pieces sample one texture, and a metrics table (per entry: its rectangle and its quad's size and offset) tells the shader what to
// draw, so a chunk's props are one MultiMesh and one material however many different pictures they show.

/// <summary>One picture in the atlas.</summary>
/// <param name="Id">The entry's number (what an instance carries; frames of one picture are consecutive).</param>
/// <param name="X">Left edge of the stored rectangle in atlas pixels.</param>
/// <param name="Y">Top edge in atlas pixels.</param>
/// <param name="Width">Stored width in pixels (0: the picture is empty and draws nothing).</param>
/// <param name="Height">Stored height in pixels.</param>
/// <param name="QuadWidth">The quad's width in metres.</param>
/// <param name="QuadHeight">The quad's height in metres.</param>
/// <param name="CenterX">Metres from the feet's centre to the quad's horizontal centre.</param>
/// <param name="BottomY">Metres from the floor to the quad's bottom edge.</param>
public readonly record struct AtlasEntry(int Id, int X, int Y, int Width, int Height, float QuadWidth, float QuadHeight, float CenterX, float BottomY);

/// <summary>
/// Packs pictures into one growing RGBA atlas with shelves (rows of equal-ish height) and keeps the metrics table. The atlas is
/// <see cref="AtlasWidth"/> wide and made of pages <see cref="PageHeight"/> tall, added as needed, so entries never move when it grows.
/// A shelf never crosses a page, so every picture lies in one page: the renderer keeps the pages as the layers of a texture array and
/// uploads only the pages that changed (a whole 2048 x 8192 atlas is 64 MB; a page is 16 MB, and a new picture mid-game, a blast's
/// debris, touches one). Pictures are stored with a one pixel border of their own edge colour so that nearest filtering at a distance
/// does not pull in a neighbour.
/// </summary>
public sealed class AtlasBuilder
{
    /// <summary>The atlas width in pixels.</summary>
    public const int AtlasWidth = 2048;

    /// <summary>The height of one page in pixels (the tallest picture with its border).</summary>
    public const int PageHeight = 2048;

    /// <summary>The most pages the atlas grows to (16 MB each; Meta's map needs 3 or 4, every new picture of a round adds to them).</summary>
    public const int MaxPages = 16;

    /// <summary>Alpha at or above which a pixel counts as drawn when trimming.</summary>
    public const int TrimAlpha = 8;

    private readonly List<AtlasEntry> _entries = new();
    private readonly List<Shelf> _shelves = new();
    private int _nextShelfY;
    private readonly List<Raster> _pages = new() { new Raster(AtlasWidth, PageHeight) };
    private readonly HashSet<int> _dirtyPages = new() { 0 };
    // the rectangles drawn since the last TakeDirtyRects, one per shelf (a shelf's pictures side by side): the renderer uploads just them
    private readonly Dictionary<(int Page, int Y), (int X0, int X1, int Y1)> _dirtyRects = new();

    private struct Shelf
    {
        public int Y;
        public int Height;
        public int NextX;
    }

    /// <summary>Entries so far.</summary>
    public IReadOnlyList<AtlasEntry> Entries => _entries;

    /// <summary>The pages, top to bottom: atlas row y is row <c>y % PageHeight</c> of page <c>y / PageHeight</c>.</summary>
    public IReadOnlyList<Raster> Pages => _pages;

    /// <summary>The atlas height in pixels (all its pages).</summary>
    public int Height => _pages.Count * PageHeight;

    /// <summary>The atlas pixel at (<paramref name="x"/>, <paramref name="y"/>) (<c>0xRRGGBBAA</c>).</summary>
    public uint Get(int x, int y) => _pages[y / PageHeight].Get(x, y % PageHeight);

    /// <summary>The pages changed since the last <see cref="TakeDirtyPages"/>, and clears the set (the renderer uploads them).</summary>
    public List<int> TakeDirtyPages()
    {
        var pages = new List<int>(_dirtyPages);
        pages.Sort();
        _dirtyPages.Clear();
        _dirtyRects.Clear();
        return pages;
    }

    /// <summary>
    /// The rectangles drawn since the last call (page, x, y within the page, width, height), one per shelf that took pictures, and clears them
    /// with the dirty pages (the renderer that uploads rectangles does not need the pages).
    /// </summary>
    public List<(int Page, int X, int Y, int Width, int Height)> TakeDirtyRects()
    {
        var rects = new List<(int Page, int X, int Y, int Width, int Height)>(_dirtyRects.Count);
        foreach (var ((page, y), (x0, x1, y1)) in _dirtyRects)
            rects.Add((page, x0, y, x1 - x0, y1 - y));
        rects.Sort();
        _dirtyRects.Clear();
        _dirtyPages.Clear();
        return rects;
    }

    /// <summary>Incremented whenever the atlas picture or the table changed (the renderer re-uploads when it moves).</summary>
    public int Version { get; private set; }

    /// <summary>
    /// Adds the picture of a standing or wall-mounted thing: <paramref name="image"/> with its icon square's lower-left corner at
    /// (<paramref name="originX"/>, <paramref name="originY"/> from the bottom) as the compositor gives it. With <paramref name="trim"/> only the
    /// drawn pixels are stored and the quad covers just them (billboards); without it the whole picture is stored (pieces map it on faces).
    /// Returns the entry's id.
    /// </summary>
    public int Add(Raster image, int originX, int originY, bool trim = true)
    {
        int tx = 0, ty = 0, tw = image.Width, th = image.Height;
        if (trim)
            TrimBounds(image, out tx, out ty, out tw, out th);
        int id = _entries.Count;
        if (tw <= 0 || th <= 0)
        {
            _entries.Add(new AtlasEntry(id, 0, 0, 0, 0, 0f, 0f, 0f, 0f));
            Version++;
            return id;
        }
        PlaceRect(tw + 2, th + 2, out int px, out int py);
        int pageIndex = py / PageHeight;
        byte[] page = _pages[pageIndex].Pixels;
        int pageY = py - pageIndex * PageHeight;
        _dirtyPages.Add(pageIndex);
        var shelf = (pageIndex, pageY);
        _dirtyRects[shelf] = _dirtyRects.TryGetValue(shelf, out var had)
            ? (Math.Min(had.X0, px), Math.Max(had.X1, px + tw + 2), Math.Max(had.Y1, pageY + th + 2))
            : (px, px + tw + 2, pageY + th + 2);
        // Copy with a one pixel border of the nearest edge pixel, a row at a time (a model's 1-2k texture pixel by pixel stalled the frame).
        // The border keeps the colour but not the alpha: a picture's edge must not grow a halo.
        for (int y = -1; y <= th; y++)
        {
            int sy = Math.Clamp(ty + y, ty, ty + th - 1);
            int s = (sy * image.Width + tx) * 4;
            int d = ((pageY + 1 + y) * AtlasWidth + px + 1) * 4;
            Span<byte> row = page.AsSpan(d, tw * 4);
            image.Pixels.AsSpan(s, tw * 4).CopyTo(row);
            if (y < 0 || y >= th)
                for (int a = 3; a < row.Length; a += 4)
                    row[a] = 0;
            Edge(d - 4, s);
            Edge(d + tw * 4, s + (tw - 1) * 4);
        }

        void Edge(int at, int from)
        {
            image.Pixels.AsSpan(from, 3).CopyTo(page.AsSpan(at, 3));
            page[at + 3] = 0;
        }
        SpriteRect full = SpriteRect.Of(image.Width, image.Height, originX, originY);
        double ps = SpriteRect.MetresPerPixel;
        double left = full.Left + tx * ps, top = full.Top - ty * ps;
        var entry = new AtlasEntry(id, px + 1, py + 1, tw, th, (float)(tw * ps), (float)(th * ps), (float)(left + tw * ps / 2), (float)(top - th * ps));
        _entries.Add(entry);
        Version++;
        return id;
    }

    /// <summary>Adds a one colour swatch (a light fixture's emissive colour, a bar's metal): <paramref name="rgba"/> is <c>0xRRGGBBAA</c>.</summary>
    public int AddSwatch(uint rgba) => Add(Raster.Solid(2, 2, rgba), 0, 0, trim: false);

    /// <summary>The metrics table for the shader: two texels (RGBA, 8 floats) per entry, entry after entry: the stored rectangle (x, y, w, h in pixels), then the quad (width, height, centre x, bottom y in metres).</summary>
    public float[] Metrics()
    {
        var table = new float[_entries.Count * 8];
        for (int i = 0; i < _entries.Count; i++)
        {
            AtlasEntry e = _entries[i];
            int o = i * 8;
            table[o] = e.X;
            table[o + 1] = e.Y;
            table[o + 2] = e.Width;
            table[o + 3] = e.Height;
            table[o + 4] = e.QuadWidth;
            table[o + 5] = e.QuadHeight;
            table[o + 6] = e.CenterX;
            table[o + 7] = e.BottomY;
        }
        return table;
    }

    /// <summary>
    /// The rectangle a billboard draws a composed picture in, standing or lying, at <paramref name="scale"/> of the tile scale. At the tile scale
    /// standing it is <see cref="SpriteRect.Of"/>; else (a lying mob, an item, a mob at the mob scale) its drawn pixels rest on the floor (<see cref="SpriteRect.AsItem"/>),
    /// <paramref name="trimBottomRows"/> clear rows under them dropped. <paramref name="hostTrimBottomRows"/> drops the host's instead: an image that
    /// hangs on a mob (a data HUD's bar, tg's icon rows over the head) keeps its rows in the mob's icon square, as tg draws it in the mob's cell.
    /// </summary>
    public static SpriteRect PictureRect(ComposedRaster raster, bool lying, double scale, int? hostTrimBottomRows, out int trimBottomRows)
    {
        int width = raster.Image.Width, height = raster.Image.Height;
        SpriteRect rect = SpriteRect.Of(width, height, raster.OriginX, raster.OriginY);
        trimBottomRows = 0;
        if (!lying && scale == 1)
            return rect;
        if (hostTrimBottomRows is int host)
            trimBottomRows = host;
        else
        {
            TrimBounds(raster.Image, out _, out int trimY, out _, out int trimHeight);
            trimBottomRows = trimHeight > 0 ? height - (trimY + trimHeight) : 0;
        }
        return rect.AsItem(scale, trimBottomRows);
    }

    /// <summary>The smallest rectangle (left, top, width, height; rows from the top) that holds every pixel of alpha at least <see cref="TrimAlpha"/>; empty (0 wide) when there is none.</summary>
    public static void TrimBounds(Raster image, out int x, out int y, out int width, out int height)
    {
        int minX = image.Width, minY = image.Height, maxX = -1, maxY = -1;
        for (int row = 0; row < image.Height; row++)
        {
            for (int col = 0; col < image.Width; col++)
            {
                if (image.Pixels[(row * image.Width + col) * 4 + 3] >= TrimAlpha)
                {
                    if (col < minX) minX = col;
                    if (col > maxX) maxX = col;
                    if (row < minY) minY = row;
                    if (row > maxY) maxY = row;
                }
            }
        }
        if (maxX < 0)
        {
            x = y = width = height = 0;
            return;
        }
        x = minX;
        y = minY;
        width = maxX - minX + 1;
        height = maxY - minY + 1;
    }

    private void PlaceRect(int w, int h, out int x, out int y)
    {
        if (w > AtlasWidth || h > PageHeight)
            throw new ArgumentOutOfRangeException(nameof(w), "a picture of " + w + " x " + h + " pixels does not fit the atlas");
        for (int i = 0; i < _shelves.Count; i++)
        {
            Shelf shelf = _shelves[i];
            // A shelf takes pictures up to its height and at most 25% shorter than it, so thin pictures do not waste a tall shelf.
            if (h <= shelf.Height && h * 4 >= shelf.Height * 3 && shelf.NextX + w <= AtlasWidth)
            {
                x = shelf.NextX;
                y = shelf.Y;
                shelf.NextX += w;
                _shelves[i] = shelf;
                return;
            }
        }
        // A shelf that would cross into the next page starts on it instead.
        int pageTop = _nextShelfY / PageHeight * PageHeight;
        if (_nextShelfY + h > pageTop + PageHeight)
            _nextShelfY = pageTop + PageHeight;
        if (_nextShelfY + h > Height)
            Grow();
        _shelves.Add(new Shelf { Y = _nextShelfY, Height = h, NextX = w });
        x = 0;
        y = _nextShelfY;
        _nextShelfY += h;
    }

    private void Grow()
    {
        if (_pages.Count >= MaxPages)
            throw new InvalidOperationException("the prop atlas is full (" + MaxPages + " pages)");
        _dirtyPages.Add(_pages.Count);
        _pages.Add(new Raster(AtlasWidth, PageHeight));
    }
}
