namespace Anomaly.Client.Shared.Icons;

/// <summary>
/// DV212: the HUD's hand boxes stand on the opposite sides from tg's, and tg's pictures are drawn for tg's sides: left as they are, the hands and
/// the handcuffs' chain would point inward. The sheets' hand pictures are mirrored as they load, each with its R or L put back readable, and the
/// hand turned fingers up, as a hand held up before the eyes (the owner, 2026-11-01): the left leans left with its thumb inward, the right leans right.
/// </summary>
public static class HandBoxes
{
    private const int Size = 32;

    /// <summary>Mirrors the hand pictures of <paramref name="resource"/> in <paramref name="pixels"/>, the raster of <paramref name="sheet"/>.</summary>
    public static void Mirror(string resource, IconSheet sheet, Raster pixels)
    {
        if (resource == "icons/hud/screen_gen.dmi")
        {
            MirrorState(sheet, pixels, "markus", null, null);
            MirrorState(sheet, pixels, "gabrielle", null, null);
            return;
        }
        if (!resource.StartsWith("icons/hud/screen_", StringComparison.Ordinal))
            return;
        // The letter's columns in the picture as tg draws it, in the box's corner on the outer side of tg's boxes; the operative style has no letters.
        (int Left, int Right)? rightLetter = resource.EndsWith("_operative.dmi", StringComparison.Ordinal) ? null
            : resource.EndsWith("_trasenknox.dmi", StringComparison.Ordinal) ? (4, 11) : (4, 8);
        (int Left, int Right)? leftLetter = rightLetter is var (l, r) ? (Size - 1 - r, Size - 1 - l) : null;
        MirrorState(sheet, pixels, "hand_l", leftLetter, (8, 24));
        MirrorState(sheet, pixels, "lhandactive", leftLetter, (8, 24));
        MirrorState(sheet, pixels, "hand_r", rightLetter, (7, 23));
        MirrorState(sheet, pixels, "rhandactive", rightLetter, (7, 23));
    }

    private static void MirrorState(IconSheet sheet, Raster pixels, string name, (int Left, int Right)? letter, (int Left, int Right)? hand)
    {
        IconState state = sheet.Find(name);
        if (state == null)
            return;
        foreach ((int x, int y, int w, int h) in state.Rects)
        {
            if (w != Size || h != Size)
                continue;
            Flip(pixels, x, x + Size - 1, y, y + Size - 1);
            if (letter is var (left, right))
                Flip(pixels, x + Size - 1 - right, x + Size - 1 - left, y, y + 12); // the mirrored letter's columns, flipped back where they now stand
            if (hand is var (handLeft, handRight))
                TurnHandUp(pixels, x, y, handLeft, handRight, letter);
        }
    }

    // Rows 9 to 23 hold the hand in every style; their sum is even, so a box's row stripes stay in step. The letter's corner stays put.
    private static void TurnHandUp(Raster pixels, int x0, int y0, int left, int right, (int Left, int Right)? letter)
    {
        for (int x = left; x <= right; x++)
        {
            for (int top = 9, bottom = 23; top < bottom; top++, bottom--)
            {
                int inPicture = Size - 1 - x;
                if (top <= 11 && letter is var (a, b) && inPicture >= a && inPicture <= b)
                    continue;
                uint swap = pixels.Get(x0 + x, y0 + top);
                pixels.Set(x0 + x, y0 + top, pixels.Get(x0 + x, y0 + bottom));
                pixels.Set(x0 + x, y0 + bottom, swap);
            }
        }
    }

    private static void Flip(Raster pixels, int left, int right, int top, int bottom)
    {
        for (int y = top; y <= bottom; y++)
        {
            for (int a = left, b = right; a < b; a++, b--)
            {
                uint swap = pixels.Get(a, y);
                pixels.Set(a, y, pixels.Get(b, y));
                pixels.Set(b, y, swap);
            }
        }
    }
}
