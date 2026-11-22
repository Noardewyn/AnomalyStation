namespace Anomaly.Core.DmShim;

// Owner: the M2 contracts step (a type only; docs/ai/specs/m2-ownership.md). M4c slice 4: the builtin icon(file, state) that
// tg's radial menus use for their pictures (suit_storage_unit.dm:357-370, radial.dmi's buttons), as the file and the state
// it names. M6 slice 7 (D155): Blend, kept as a list of operations that the client applies (the recipe resource, iconops:), and
// fcopy_rsc of such an icon; the M7 zaps slice: DrawBox (the beam's cut terminal segment, beam.dm:299) and a blended icon's own
// operations in the recipe; the M7 husks slice: MapColors (its number forms) and SetIntensity, for tg's ColorTone (__HELPERS/icons.dm:213);
// M10x slice D4: Crop and Scale (tg's preference pictures); syndicate G4c: SwapColor (the supply pod's door masks); the other icon
// procs wait for their callers.

/// <summary>
/// The BYOND <c>/icon</c> (BYOND ref "icon"): an icon object that procs can edit (<c>Blend</c>, <c>Scale</c>, ...). The server
/// never renders icons: the client composes appearances from tg's icon files (D104, M2 P7). So an icon object is what it was
/// made from, an icon file and the one state <c>icon(file, state)</c> extracted from it (BYOND: "the resulting icon contains
/// only that state"), which is all an appearance that shows it needs: the client draws that state of that file. A builtin
/// value type: no world id, never registered; hashed by its fields. Made by <see cref="Dm.icon"/>.
/// </summary>
[DmPath("/icon")]
public class icon
{
    /// <summary>The icon file it was made from (a resource), null for an empty icon.</summary>
    public object dm_file;

    /// <summary>The state it holds (<c>icon(file, state)</c>'s state), null for every state of the file.</summary>
    public string dm_state;

    /// <summary>The direction it was limited to (0 for all).</summary>
    public float dm_dir;

    /// <summary>The animation frame it was limited to (0 for all).</summary>
    public float dm_frame;

    /// <summary>Silicons SI4d (D326): the resource path prefix of an image the world made at run time (<see cref="DmWorldState.RuntimeResources"/>).</summary>
    public const string RuntimePrefix = "rsc/";

    private (int Width, int Height, uint[] Rgba)? _pixels;

    // Silicons SI4d: the pixels of a runtime image (a PNG the world wrote); the server reads no tg icon (D104)
    private (int Width, int Height, uint[] Rgba) Pixels()
    {
        if (_pixels is { } decoded)
            return decoded;
        string path = (dm_file as DmResource)?.Path;
        if (path == null || !path.StartsWith(RuntimePrefix, StringComparison.Ordinal)
            || !DmWorldState.Current.RuntimeResources.TryGetValue(path, out byte[] png))
            throw new DmRuntimeException("the server reads the pixels of no icon but the PNGs the world wrote (D104): " + (path ?? "an empty icon"));
        _pixels = DmPng.Decode(png);
        return _pixels.Value;
    }

    /// <summary>DM <c>icon.Width()</c> of an image the world made at run time.</summary>
    public float Width() => Pixels().Width;

    /// <summary>DM <c>icon.Height()</c> of an image the world made at run time.</summary>
    public float Height() => Pixels().Height;

    /// <summary>
    /// DM <c>icon.GetPixel(x, y)</c> (BYOND ref <c>/icon/proc/GetPixel</c>) of an image the world made at run time: <c>"#rrggbb"</c>, with
    /// the alpha (<c>"#rrggbbaa"</c>) when it is not opaque, null for a transparent pixel or one outside the icon; 1, 1 is the lower left.
    /// </summary>
    public object GetPixel(object x, object y)
    {
        (int width, int height, uint[] rgba) = Pixels();
        int px = (int)Dm.num(x), py = (int)Dm.num(y);
        if (px < 1 || py < 1 || px > width || py > height)
            return null;
        uint p = rgba[(height - py) * width + px - 1];
        uint alpha = p & 0xFF;
        if (alpha == 0)
            return null;
        string rgb = "#" + (p >> 8).ToString("x6", System.Globalization.CultureInfo.InvariantCulture);
        return alpha == 255 ? rgb : rgb + alpha.ToString("x2", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>M6 slice 7: the <c>Blend</c> operations done on it, in order: the function (BYOND's <c>ICON_ADD</c> 0 ... <c>ICON_UNDERLAY</c> 6), the operand (a colour text or another icon) and the position (1, 1 is the lower left).</summary>
    public readonly List<(int Function, object With, int X, int Y)> dm_ops = new();

    /// <summary>
    /// DM <c>icon.Blend(icon, function = ICON_ADD, x = 1, y = 1)</c> (BYOND ref <c>/icon/proc/Blend</c>): combines a colour or another
    /// icon into this one. The server draws nothing (D104): the operation is kept, and the client applies it to the picture of the
    /// icon's file and state when the icon is shown (<see cref="dm_resource"/>).
    /// </summary>
    public void Blend(object icon, object function = null, object x = null, object y = null)
    {
        object operand = icon switch
        {
            string color => color,
            DmShim.icon other => Dm.icon(other),
            _ => throw new DmRuntimeException("Blend(): cannot blend " + Dm.KindOf(icon)),
        };
        dm_ops.Add(((int)(function == null ? 0f : Dm.num(function)), operand, (int)(x == null ? 1f : Dm.num(x)), (int)(y == null ? 1f : Dm.num(y))));
    }

    /// <summary>The M7 zaps slice: the <see cref="dm_ops"/> function that marks a <c>DrawBox</c> (not a BYOND <c>ICON_*</c> value); its
    /// operand is the box's colour text (null to erase) with its upper right corner, its position the lower left corner.</summary>
    public const int DrawBoxFunction = 100;

    /// <summary>
    /// DM <c>icon.DrawBox(rgb, x1, y1, x2 = x1, y2 = y1)</c> (BYOND ref <c>/icon/proc/DrawBox</c>): fills the box from (x1, y1) to
    /// (x2, y2), 1, 1 being the lower left pixel, with the colour, or erases it to transparent when <paramref name="rgb"/> is null.
    /// The server draws nothing (D104): the operation is kept, and the client applies it in order with the <c>Blend</c>s.
    /// </summary>
    public void DrawBox(object rgb, object x1, object y1, object x2 = null, object y2 = null)
    {
        if (rgb != null && rgb is not string)
            throw new DmRuntimeException("DrawBox(): the colour is not text: " + Dm.KindOf(rgb));
        int left = (int)Dm.num(x1), bottom = (int)Dm.num(y1);
        int right = x2 == null ? left : (int)Dm.num(x2), top = y2 == null ? bottom : (int)Dm.num(y2);
        dm_ops.Add((DrawBoxFunction, ((string)rgb, right, top), left, bottom));
    }

    /// <summary>The M7 husks slice: the <see cref="dm_ops"/> function that marks a <c>MapColors</c>; its operand is the matrix, 12 or 20 numbers.</summary>
    public const int MapColorsFunction = 101;

    /// <summary>
    /// DM <c>icon.MapColors(rr, rg, rb, gr, gg, gb, br, bg, bb, r0 = 0, g0 = 0, b0 = 0)</c> or its 16 and 20 number form with alpha
    /// (BYOND ref <c>/icon/proc/MapColors</c>): every pixel's colour becomes the matrix applied to it (the alpha passes through in the
    /// 12 number form). The server draws nothing (D104): the matrix is kept, padded to 12 or 20 numbers, and the client applies it in
    /// order with the other operations. The colour text forms have no caller yet: a runtime error.
    /// </summary>
    public void MapColors(params object[] args)
    {
        int count = args.Length;
        while (count > 0 && args[count - 1] == null)
            count--; // trailing nulls are not given (OpenDream's TrimEnd)
        if (count > 0 && args[0] is string)
            throw new DmRuntimeException("MapColors(): the colour text forms are not supported");
        int size = count is >= 9 and <= 12 ? 12 : count is >= 16 and <= 20 ? 20 : throw new DmRuntimeException("MapColors(): " + count + " arguments");
        var matrix = new float[size];
        for (int i = 0; i < count; i++)
            matrix[i] = args[i] == null ? 0f : Dm.num(args[i]);
        dm_ops.Add((MapColorsFunction, matrix, 1, 1));
    }

    /// <summary>
    /// DM <c>icon.SetIntensity(r, g = r, b = r)</c> (BYOND ref <c>/icon/proc/SetIntensity</c>): multiplies the colour channels, as the
    /// MapColors of that diagonal (the same pixels: BYOND rounds both to the nearest); a negative factor does nothing (OpenDream).
    /// </summary>
    public void SetIntensity(object r = null, object g = null, object b = null)
    {
        float red = r == null ? 0f : Dm.num(r), green = g == null ? red : Dm.num(g), blue = b == null ? red : Dm.num(b);
        if (red < 0 || green < 0 || blue < 0)
            return;
        MapColors(red, 0f, 0f, 0f, green, 0f, 0f, 0f, blue, 0f, 0f, 0f);
    }

    /// <summary>M10x slice D4: the <see cref="dm_ops"/> function that marks a <c>Crop</c>; its operand is the upper right corner, its position the lower left.</summary>
    public const int CropFunction = 102;

    /// <summary>M10x slice D4: the <see cref="dm_ops"/> function that marks a <c>Scale</c>; its operand is the new width and height.</summary>
    public const int ScaleFunction = 103;

    /// <summary>
    /// DM <c>icon.Crop(x1, y1, x2, y2)</c> (BYOND ref <c>/icon/proc/Crop</c>): the icon becomes the box from (x1, y1) to (x2, y2), 1, 1 being
    /// the lower left pixel (tg's preference pictures, universal_icon.dm:59). Kept for the client, as the other operations.
    /// </summary>
    public void Crop(object x1, object y1, object x2, object y2)
        => dm_ops.Add((CropFunction, ((int)Dm.num(x2), (int)Dm.num(y2)), (int)Dm.num(x1), (int)Dm.num(y1)));

    /// <summary>DM <c>icon.Scale(width, height)</c> (BYOND ref <c>/icon/proc/Scale</c>): the icon resized to that size. Kept for the client.</summary>
    public void Scale(object width, object height)
        => dm_ops.Add((ScaleFunction, ((int)Dm.num(width), (int)Dm.num(height)), 1, 1));

    /// <summary>Syndicate A3: the <see cref="dm_ops"/> function that marks a <c>Shift</c>; its operand is the direction, the offset and the wrap.</summary>
    public const int ShiftFunction = 104;

    /// <summary>
    /// DM <c>icon.Shift(dir, offset, wrap = 0)</c> (BYOND ref <c>/icon/proc/Shift</c>): the pixels move <paramref name="offset"/> pixels toward
    /// <paramref name="dir"/>; those that leave the icon come back at the other edge with <paramref name="wrap"/>, or are lost (the antagonists'
    /// preference pictures, antag_datum.dm:500). Kept for the client, as the other operations.
    /// </summary>
    public void Shift(object dir, object offset, object wrap = null)
        => dm_ops.Add((ShiftFunction, ((int)Dm.num(dir), (int)Dm.num(offset), wrap == null ? 0 : (int)Dm.num(wrap)), 1, 1));

    /// <summary>Syndicate G4c: the <see cref="dm_ops"/> function that marks a <c>SwapColor</c>; its operand is the old and the new colour text (null: transparent).</summary>
    public const int SwapColorFunction = 105;

    /// <summary>
    /// DM <c>icon.SwapColor(OldColor, NewColor)</c> (BYOND ref <c>/icon/proc/SwapColor</c>): the pixels of the old colour take the new one, a
    /// null new colour erasing them (tg's supply pod masks, supplypod.dm:230). An old colour with alpha matches the alpha too; one
    /// without matches the colour of any visible pixel. Kept for the client, as the other operations.
    /// </summary>
    public void SwapColor(object old_color, object new_color)
    {
        if (old_color is not string oldText || (new_color != null && new_color is not string))
            throw new DmRuntimeException("SwapColor(): the colours are not text");
        dm_ops.Add((SwapColorFunction, (oldText, (string)new_color), 1, 1));
    }

    /// <summary>Gaps SP1: the <see cref="dm_ops"/> function that marks a direction pick, <c>icon(icon, state, dir)</c>'s one direction; its
    /// operand is the direction.</summary>
    public const int DirFunction = 106;

    /// <summary>
    /// The resource the client draws this icon from: its file, or with <c>Blend</c> operations the recipe
    /// <c>iconops:FILE|STATE|FRAME|OP|...</c> (each part URI-escaped, since the M7 husks slice only its % and |; an operation is <c>FUNCTION,X,Y,c,COLOUR</c> or
    /// <c>FUNCTION,X,Y,i,FILE,STATE</c>), which the client's icon library composes (docs/ai/specs/protocol.md section 34.3). The M7 zaps
    /// slice: a <c>DrawBox</c> is <c>FUNCTION,X1,Y1,b,COLOUR,X2,Y2</c> (an empty colour erases), and an operand icon with operations of its
    /// own is <c>FUNCTION,X,Y,i,FILE,STATE,K</c> followed by the K parts of those operations (the beam's alpha mask, beam.dm:309). The M7
    /// husks slice: a <c>MapColors</c> is <c>FUNCTION,1,1,m,N1,...,Nk</c> (k 12 or 20); an empty STATE is every state of the file
    /// (<c>new /icon(file)</c>), the operations applied to each. M10x slice D4: a <c>Crop</c> is <c>102,X1,Y1,k,X2,Y2</c> and a <c>Scale</c>
    /// <c>103,1,1,s,WIDTH,HEIGHT</c>. Syndicate A3: a <c>Shift</c> is <c>104,1,1,t,DIR,OFFSET,WRAP</c>.
    /// Syndicate G4c: a <c>SwapColor</c> is <c>105,1,1,w,OLD,NEW</c> (an empty NEW erases). Gaps SP1: a direction pick (<c>icon(icon, state,
    /// dir)</c>, BYOND's icon of that one direction) is <c>106,1,1,d,DIR</c>, in its place among the operations.
    /// </summary>
    public string dm_resource()
    {
        string file = FileText(dm_file);
        if (dm_ops.Count == 0 || file == null) return file;
        var parts = new List<string> { Part(file), Part(dm_state ?? ""), ((int)dm_frame).ToString(System.Globalization.CultureInfo.InvariantCulture) };
        AppendOps(parts);
        return "iconops:" + string.Join("|", parts);
    }

    // The recipe parts of this icon's operations; an operand icon's own operations follow its part, counted in it.
    private void AppendOps(List<string> parts)
    {
        foreach ((int function, object with, int x, int y) in dm_ops)
        {
            string head = function + "," + x + "," + y;
            if (with is float[] matrix)
                parts.Add(Part(head + ",m," + string.Join(",", System.Linq.Enumerable.Select(matrix, n => n.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))));
            else if (with is ValueTuple<int, int> size)
                parts.Add(Part(head + (function == CropFunction ? ",k," : ",s,") + size.Item1 + "," + size.Item2));
            else if (with is ValueTuple<int, int, int> shift)
                parts.Add(Part(head + ",t," + shift.Item1 + "," + shift.Item2 + "," + shift.Item3));
            else if (function == DirFunction && with is int pickedDir)
                parts.Add(Part(head + ",d," + pickedDir));
            else if (with is ValueTuple<string, string> swap)
                parts.Add(Part(head + ",w," + swap.Item1 + "," + (swap.Item2 ?? "")));
            else if (with is ValueTuple<string, int, int> box)
                parts.Add(Part(head + ",b," + (box.Item1 ?? "") + "," + box.Item2 + "," + box.Item3));
            else if (with is DmShim.icon other)
            {
                string operand = head + ",i," + Field(FileText(other.dm_file) ?? "") + "," + Field(other.dm_state ?? "");
                if (other.dm_ops.Count == 0)
                {
                    parts.Add(Part(operand));
                    continue;
                }
                int at = parts.Count;
                parts.Add(null);
                other.AppendOps(parts);
                parts[at] = Part(operand + "," + (parts.Count - at - 1));
            }
            else
                parts.Add(Part(head + ",c," + (string)with));
        }
    }

    // The M7 husks slice: a part URI-escapes only what the recipe's syntax needs (% and |; a field inside an operation also ,), which
    // the client's URI unescaping reads as before; a husk's limb recipe must fit Limits.MaxResourceBytes (512).
    private static string Part(string text) => text.Replace("%", "%25").Replace("|", "%7C");

    private static string Field(string text) => Part(text).Replace(",", "%2C");

    private static string FileText(object file) => file switch
    {
        DmResource resource => resource.Path,
        string path when path.Length > 0 => path,
        _ => null,
    };
}

public static partial class Dm
{
    /// <summary>
    /// DM <c>icon(icon, state, dir, frame, moving)</c> (BYOND ref <c>/proc/icon</c>, supports named arguments): a new
    /// <c>/icon</c> made from an icon file (a resource) or a copy of another icon object, limited to <paramref name="state"/>,
    /// <paramref name="dir"/> and <paramref name="frame"/> when given. <paramref name="moving"/> (the movement states) is not
    /// kept: no M4c caller passes it. Anything else as the icon is a runtime error.
    /// </summary>
    public static icon icon(object icon = null, object state = null, object dir = null, object frame = null, object moving = null)
    {
        var made = new icon();
        switch (icon)
        {
            case null:
                break;
            case DmResource resource:
                made.dm_file = resource;
                break;
            case string path when path.Length > 0:
                made.dm_file = RuntimeFile(path) ?? (object)path; // a resource literal is text in the port until resources are placed (RULE-GAP, see image())
                break;
            case DmFileRef fileRef:
                made.dm_file = RuntimeFile(fileRef.Path) ?? (object)fileRef.Path;
                break;
            case DmShim.icon other:
                made.dm_file = other.dm_file;
                made.dm_state = other.dm_state;
                made.dm_dir = other.dm_dir;
                made.dm_frame = other.dm_frame;
                made.dm_ops.AddRange(other.dm_ops);
                break;
            default:
                throw new DmRuntimeException("icon(): cannot make an icon from " + KindOf(icon));
        }
        if (state != null)
        {
            if (state is not string text)
                throw new DmRuntimeException("icon(): the icon state is not text: " + KindOf(state));
            made.dm_state = text;
        }
        if (dir != null)
        {
            made.dm_dir = num(dir);
            // gaps SP1: BYOND's icon of that one direction (the lizard features' side views, the flat icons' turned layers), kept for the client
            made.dm_ops.Add((DmShim.icon.DirFunction, (int)num(dir), 1, 1));
        }
        if (frame != null) made.dm_frame = num(frame);
        return made;
    }

    // Silicons SI4d: an icon of a PNG the world wrote (its file seam has it) takes the file's bytes into the world's resource cache as BYOND
    // does, under its content's hash (a later write to the same path makes another resource); a path the seam does not have is tg's.
    private static DmResource RuntimeFile(string path)
    {
        if (path.StartsWith(DmShim.icon.RuntimePrefix, StringComparison.Ordinal))
            return resource(path);
        if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || Anomaly.Core.Kernel.World.CurrentOrNull == null)
            return null;
        DmWorldState state = DmWorldState.Current;
        byte[] bytes = state.Files.ReadBytes(path);
        if (bytes == null)
            return null;
        string key = DmShim.icon.RuntimePrefix + Convert.ToHexString(System.Security.Cryptography.MD5.HashData(bytes)).ToLowerInvariant() + ".png";
        state.AddRuntimeResource(key, bytes);
        return resource(key);
    }

    /// <summary>
    /// DM <c>fcopy_rsc(file)</c> (BYOND ref <c>/proc/fcopy_rsc</c>): the file as a cache resource. An icon object is its own
    /// resource in the port (the replicator sends <see cref="DmShim.icon.dm_resource"/>); a resource or a path is itself.
    /// </summary>
    public static object fcopy_rsc(object file) => file;
}
