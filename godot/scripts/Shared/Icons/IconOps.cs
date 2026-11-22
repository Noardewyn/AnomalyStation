using System.Globalization;
using System.Linq;

namespace Anomaly.Client.Shared.Icons;

// Owner: M6 slice 7 (D155, protocol.md section 34.3). The client's half of BYOND's /icon procs: the server keeps an icon object's
// Blend operations (Anomaly.Core.DmShim.icon) and sends the icon as the recipe resource
// iconops:FILE|STATE|FRAME|OP|... (each part URI-escaped; an operation FUNCTION,X,Y,c,COLOUR or FUNCTION,X,Y,i,FILE,STATE);
// this file makes the icon of such a resource: the one state of the file the icon was made from (limited to FRAME when it is not 0),
// each frame blended in order as BYOND's /icon/proc/Blend does (tg's damaged clothing, clothing.dm:519). The M7 zaps slice: a
// DrawBox, FUNCTION,X1,Y1,b,COLOUR,X2,Y2 (an empty colour erases), and an operand icon with operations of its own,
// FUNCTION,X,Y,i,FILE,STATE,K followed by its K parts (the beam's cut terminal segment and its alpha mask, beam.dm:299-313). The M7
// husks slice: a MapColors, FUNCTION,1,1,m,N1,...,Nk (k 12 or 20), and a recipe with an empty STATE, new /icon(file): every state of the
// file, each made when first shown (tg's ColorTone of a husk's limb file, __HELPERS/icons.dm:213).
// M10x slice D4: a Crop, 102,X1,Y1,k,X2,Y2, and a Scale, 103,1,1,s,WIDTH,HEIGHT (tg's preference pictures, which change the size).
// Syndicate A3: a Shift, 104,1,1,t,DIR,OFFSET,WRAP (the antagonists' preference pictures).
// Syndicate G4c: a SwapColor, 105,1,1,w,OLD,NEW (an empty NEW erases; the supply pod's door masks).
// Gaps SP1: a direction pick, 106,1,1,d,DIR (BYOND's icon(icon, state, dir): the lizard features' side views).

/// <summary>An icon of one state made by a recipe.</summary>
internal sealed class RecipeIcon : IIcon
{
    private readonly string _state;
    private readonly IIconState _made;

    public RecipeIcon(string state, IIconState made)
    {
        _state = state ?? string.Empty;
        _made = made;
    }

    // BYOND's icon(file, state) holds the state as its default state (OpenDream's InsertStates names it "" for the constructor): the
    // beam's cut tip is shown with icon_state "" (beam.dm:330); a picture that names the state keeps finding it.
    public bool TryGetState(string name, out IIconState state)
    {
        state = (name ?? string.Empty) == _state || string.IsNullOrEmpty(name) ? _made : null;
        return state != null;
    }
}

/// <summary>The M7 husks slice: an icon of every state of a file (<c>new /icon(file)</c>) made by a recipe, each state made when first shown.</summary>
internal sealed class FileRecipeIcon : IIcon
{
    private readonly IIcon _source;
    private readonly Func<string, IIconState, IIconState> _make;
    private readonly Dictionary<string, IIconState> _made = new(StringComparer.Ordinal);

    public FileRecipeIcon(IIcon source, Func<string, IIconState, IIconState> make)
    {
        _source = source;
        _make = make;
    }

    public bool TryGetState(string name, out IIconState state)
    {
        name ??= string.Empty;
        lock (_made)
        {
            if (!_made.TryGetValue(name, out state))
                _made[name] = state = _source.TryGetState(name, out IIconState from) ? _make(name, from) : null;
        }
        return state != null;
    }
}

internal static class IconOps
{
    public const string Prefix = "iconops:";

    /// <summary>The icon of an <c>iconops:</c> resource, or null when its parts cannot be read or its file is missing.</summary>
    public static IIcon Load(IconLibrary library, string resource)
    {
        string[] parts = resource.Substring(Prefix.Length).Split('|');
        if (parts.Length < 3)
            return null;
        string file = Uri.UnescapeDataString(parts[0]), stateName = Uri.UnescapeDataString(parts[1]);
        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int frame))
            return null;
        if (!library.TryGetIcon(file, out IIcon source))
            return null;
        if (stateName.Length == 0)
            return new FileRecipeIcon(source, (name, state) => Compose(library, state, name, frame, parts));
        if (!source.TryGetShown(stateName, out IIconState shown))
            return null;
        IIconState made = Compose(library, shown, stateName, frame, parts);
        return made == null ? null : new RecipeIcon(stateName, made);
    }

    // The recipe's operations done on one state of its file (stateName: the state's name, which an operand icon of every state of its
    // file blends with); null when an operation cannot be read.
    private static IIconState Compose(IconLibrary library, IIconState state, string stateName, int frame, string[] parts)
    {
        int frames = frame > 0 ? 1 : state.Frames, dirs = state.Dirs;
        var rasters = new Raster[dirs * frames];
        for (int d = 0; d < dirs; d++)
            for (int f = 0; f < frames; f++)
                rasters[d * frames + f] = state.Get(d, frame > 0 ? frame - 1 : f).Clone();
        int width = state.Width, height = state.Height;
        for (int i = 3; i < parts.Length; i++)
        {
            string[] op = Uri.UnescapeDataString(parts[i]).Split(',');
            if (op.Length < 5 || !int.TryParse(op[0], out int function) || !int.TryParse(op[1], out int x) || !int.TryParse(op[2], out int y))
                return null;
            var mode = (IconBlend)Math.Clamp(function, 0, 6);
            if (op[3] == "b" && op.Length >= 7)
            {
                // BYOND /icon/proc/DrawBox(rgb, x1, y1, x2, y2): the box's pixels (1, 1 the lower left) take the colour, or are cleared
                uint box = 0;
                if (op[4].Length > 0 && !TryColor(op[4], out box, out _))
                    return null;
                if (!int.TryParse(op[5], out int x2) || !int.TryParse(op[6], out int y2))
                    return null;
                foreach (Raster r in rasters)
                    FillBox(r, x, y, x2, y2, box);
            }
            else if (op[3] is "k" or "s" && op.Length >= 6)
            {
                // M10x slice D4: BYOND /icon/proc/Crop(x1, y1, x2, y2) (1, 1 the lower left) and /icon/proc/Scale(width, height)
                if (!int.TryParse(op[4], out int a) || !int.TryParse(op[5], out int b))
                    return null;
                for (int r = 0; r < rasters.Length; r++)
                    rasters[r] = op[3] == "k" ? Cropped(rasters[r], x, y, a, b) : Scaled(rasters[r], a, b);
                width = rasters.Length > 0 ? rasters[0].Width : width;
                height = rasters.Length > 0 ? rasters[0].Height : height;
            }
            else if (op[3] == "t" && op.Length >= 7)
            {
                // Syndicate A3: BYOND /icon/proc/Shift(dir, offset, wrap): the pixels move toward dir (the antagonists' preference pictures)
                if (!int.TryParse(op[4], out int dir) || !int.TryParse(op[5], out int offset) || !int.TryParse(op[6], out int wrap))
                    return null;
                for (int r = 0; r < rasters.Length; r++)
                    rasters[r] = Shifted(rasters[r], dir, offset, wrap != 0);
            }
            else if (op[3] == "d")
            {
                // Gaps SP1: BYOND's icon(icon, state, dir): the icon of that one direction (the .dmi order: S, N, E, W, SE, SW, NE, NW)
                if (!int.TryParse(op[4], out int pick))
                    return null;
                int index = pick switch { 2 => 0, 1 => 1, 4 => 2, 8 => 3, 6 => 4, 10 => 5, 5 => 6, 9 => 7, _ => 0 };
                if (index >= dirs)
                    index = 0; // a state with fewer directions shows its first
                var one = new Raster[frames];
                for (int f = 0; f < frames; f++)
                    one[f] = rasters[index * frames + f];
                rasters = one;
                dirs = 1;
            }
            else if (op[3] == "w" && op.Length >= 6)
            {
                // Syndicate G4c: BYOND /icon/proc/SwapColor(old, new): the pixels of the old colour take the new one (none: transparent)
                if (!TryColor(op[4], out uint from, out bool fromAlpha))
                    return null;
                uint to = 0;
                bool toAlpha = false;
                if (op[5].Length > 0 && !TryColor(op[5], out to, out toAlpha))
                    return null;
                if (op[5].Length > 0 && !toAlpha)
                    to |= 0xFFu;
                foreach (Raster r in rasters)
                    r.SwapColor(from, fromAlpha, to);
            }
            else if (op[3] == "m")
            {
                // BYOND /icon/proc/MapColors: every pixel through the colour matrix (12 numbers: the alpha passes through)
                var numbers = new float[op.Length - 4];
                for (int n = 0; n < numbers.Length; n++)
                {
                    if (!float.TryParse(op[4 + n], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[n]))
                        return null;
                }
                if (numbers.Length is not (12 or 20))
                    return null;
                ColorMatrix matrix = ColorMatrix.FromList(numbers);
                foreach (Raster r in rasters)
                    r.Map(matrix);
            }
            else if (op[3] == "c")
            {
                if (!TryColor(op[4], out uint rgba, out bool hasAlpha))
                    return null;
                // BYOND: a colour is a solid icon of it, opaque without an alpha (which leaves the alpha as it is)
                if (!hasAlpha)
                    rgba |= 0xFFu;
                foreach (Raster r in rasters)
                    r.Blend(Raster.Solid(r.Width, r.Height, rgba), mode);
            }
            else if (op[3] == "i" && op.Length >= 6)
            {
                string otherFile = Uri.UnescapeDataString(op[4]), otherState = Uri.UnescapeDataString(op[5]);
                // M7 zaps: an operand with operations of its own, the next K parts, is its own recipe (the beam's alpha mask)
                string otherResource = otherFile;
                if (op.Length >= 7)
                {
                    if (!int.TryParse(op[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out int k) || k < 0 || i + k >= parts.Length)
                        return null;
                    otherResource = Prefix + string.Join("|", new[] { Uri.EscapeDataString(otherFile), Uri.EscapeDataString(otherState), "0" }.Concat(parts.Skip(i + 1).Take(k)));
                    i += k;
                }
                // an operand of every state of its file blends its state of this state's name (tg's ColorTone blends a copy of the
                // husk's limb file into it, each limb state with its own: a husk shows its own limb)
                if (!library.TryGetIcon(otherResource, out IIcon otherIcon) || !otherIcon.TryGetShown(otherState.Length == 0 ? stateName : otherState, out IIconState other))
                    continue; // BYOND blends nothing from a missing icon
                // an operand with more dirs gives the icon those dirs (OpenDream's DreamIconOperationBlendImage.OnApply), each a copy of the
                // icon's first: tg's hair gradient, a one-dir fade blended with the four-dir hair (head_hair_and_lips.dm:243), turns with the head
                if (other.Dirs > dirs)
                {
                    var more = new Raster[other.Dirs * frames];
                    for (int d = 0; d < other.Dirs; d++)
                        for (int f = 0; f < frames; f++)
                            more[d * frames + f] = d < dirs ? rasters[d * frames + f] : rasters[f].Clone();
                    rasters = more;
                    dirs = other.Dirs;
                }
                for (int d = 0; d < dirs; d++)
                {
                    for (int f = 0; f < frames; f++)
                    {
                        Raster r = rasters[d * frames + f], with = other.Get(Math.Min(d, other.Dirs - 1), Math.Min(f, other.Frames - 1));
                        r.Blend(with, mode, x - 1, r.Height - with.Height - (y - 1));
                    }
                }
            }
            else
            {
                return null;
            }
        }
        double[] delays = frames == 1 ? null : state.Delays;
        return new GeneratedState(dirs, frames, width, height, rasters, delays, state.Loop, state.Rewind);
    }

    // Shift's move (BYOND's directions: NORTH 1, SOUTH 2, EAST 4, WEST 8, and their sums; y up, so NORTH moves the rows toward the top).
    private static Raster Shifted(Raster r, int dir, int offset, bool wrap)
    {
        int dx = ((dir & 4) != 0 ? offset : 0) - ((dir & 8) != 0 ? offset : 0);
        int dy = ((dir & 2) != 0 ? offset : 0) - ((dir & 1) != 0 ? offset : 0); // raster rows go down
        var made = new Raster(r.Width, r.Height);
        for (int row = 0; row < r.Height; row++)
        {
            for (int col = 0; col < r.Width; col++)
            {
                int toCol = col + dx, toRow = row + dy;
                if (wrap)
                {
                    toCol = ((toCol % r.Width) + r.Width) % r.Width;
                    toRow = ((toRow % r.Height) + r.Height) % r.Height;
                }
                else if (toCol < 0 || toCol >= r.Width || toRow < 0 || toRow >= r.Height)
                    continue;
                Array.Copy(r.Pixels, (row * r.Width + col) * 4, made.Pixels, (toRow * made.Width + toCol) * 4, 4);
            }
        }
        return made;
    }

    // Crop's box (BYOND's pixel coordinates, 1-based, y up; outside the icon is transparent).
    private static Raster Cropped(Raster r, int x1, int y1, int x2, int y2)
    {
        int left = Math.Min(x1, x2), bottom = Math.Min(y1, y2);
        var made = new Raster(Math.Abs(x2 - x1) + 1, Math.Abs(y2 - y1) + 1);
        for (int row = 0; row < made.Height; row++)
        {
            int fromRow = r.Height - (bottom + made.Height - 1 - row);
            for (int col = 0; col < made.Width; col++)
            {
                int fromCol = left - 1 + col;
                if (fromRow >= 0 && fromRow < r.Height && fromCol >= 0 && fromCol < r.Width)
                    Array.Copy(r.Pixels, (fromRow * r.Width + fromCol) * 4, made.Pixels, (row * made.Width + col) * 4, 4);
            }
        }
        return made;
    }

    // shortcut: nearest-neighbour Scale, not BYOND's own resampling; good enough for the preference grids' pixel art, upgrade if a
    // smooth scale is ever shown.
    private static Raster Scaled(Raster r, int width, int height)
    {
        var made = new Raster(Math.Max(0, width), Math.Max(0, height));
        if (r.Width == 0 || r.Height == 0)
            return made;
        for (int row = 0; row < made.Height; row++)
            for (int col = 0; col < made.Width; col++)
                Array.Copy(r.Pixels, (row * r.Height / made.Height * r.Width + col * r.Width / made.Width) * 4, made.Pixels, (row * made.Width + col) * 4, 4);
        return made;
    }

    // DrawBox's fill: the corners in BYOND's pixel coordinates (1-based, y up), clipped to the raster, in either order.
    private static void FillBox(Raster r, int x1, int y1, int x2, int y2, uint rgba)
    {
        int left = Math.Max(1, Math.Min(x1, x2)), right = Math.Min(r.Width, Math.Max(x1, x2));
        int bottom = Math.Max(1, Math.Min(y1, y2)), top = Math.Min(r.Height, Math.Max(y1, y2));
        byte cr = (byte)(rgba >> 24), cg = (byte)(rgba >> 16), cb = (byte)(rgba >> 8), ca = (byte)rgba;
        for (int y = bottom; y <= top; y++)
        {
            int row = r.Height - y;
            for (int x = left; x <= right; x++)
            {
                int i = (row * r.Width + (x - 1)) * 4;
                r.Pixels[i] = cr;
                r.Pixels[i + 1] = cg;
                r.Pixels[i + 2] = cb;
                r.Pixels[i + 3] = ca;
            }
        }
    }

    // BYOND colour text #rgb, #rgba, #rrggbb or #rrggbbaa as 0xRRGGBBAA.
    private static bool TryColor(string text, out uint rgba, out bool hasAlpha)
    {
        rgba = 0xFFFFFFFF;
        hasAlpha = false;
        if (string.IsNullOrEmpty(text) || text[0] != '#')
            return false;
        string hex = text.Substring(1);
        if (hex.Length is 3 or 4)
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        if (hex.Length is not (6 or 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            return false;
        hasAlpha = hex.Length == 8;
        rgba = hasAlpha ? value : (value << 8) | 0xFF;
        return true;
    }
}
