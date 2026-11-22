using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M4a P7 (client interaction). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// docs/ai/specs/interaction.md 4.4: the HUD pick in cursor mode.

/// <summary>One screen entry as drawn: its id, the rectangle of its composed picture in screen pixels, and the picture (its alpha decides a hit).</summary>
/// <param name="EntryId">The screen entry's id.</param>
/// <param name="Rect">Where the picture is drawn (screen pixels, y down).</param>
/// <param name="Image">The picture (null: the whole rectangle counts, and the rectangle is the entry's 32 x 32 icon square).</param>
/// <param name="OriginX">Where the entry's own 32 x 32 icon square starts in the picture, pixels from the left.</param>
/// <param name="OriginY">Pixels from the bottom.</param>
/// <param name="Children">Protocol version 12: the entry's clickable vis_contents children (<see cref="ScreenEntry.VisChildren"/>), bottom to top; null for none.</param>
public readonly record struct HudDrawn(long EntryId, HudRect Rect, Raster Image, int OriginX, int OriginY, IReadOnlyList<HudChildDrawn> Children = null);

/// <summary>
/// Protocol version 12: one clickable vis_contents child of a drawn entry (<see cref="ScreenChild"/>), its picture composed alone.
/// </summary>
/// <param name="Id">The child's world id: the target of a click on its pixels.</param>
/// <param name="Image">The child's own composed picture (null: it is not drawn, never hit).</param>
/// <param name="OriginX">Where the child's 32 x 32 icon square starts in its picture, pixels from the left.</param>
/// <param name="OriginY">Pixels from the bottom.</param>
/// <param name="ShiftX">Where that square is in the holder's icon square, pixels to the right (the holder's own pixel offset, which BYOND adds to its children's).</param>
/// <param name="ShiftY">Pixels up.</param>
/// <param name="WholeBox">BYOND's <c>mouse_opacity</c> 2: the child's whole picture box takes the click, not only its opaque pixels.</param>
/// <param name="Fit">A picture too big for the holder's 32 x 32 square (a 64 x 64 polearm in a hand) is drawn this many times as big, centred in the square
/// (<see cref="FitOf"/>); 0: drawn as it is, its own square on the holder's.</param>
public readonly record struct HudChildDrawn(long Id, Raster Image, int OriginX, int OriginY, int ShiftX = 0, int ShiftY = 0, bool WholeBox = false, float Fit = 0f)
{
    /// <summary>
    /// DEVIATION(DV575): the scale that fits a picture of this size into a slot's 32 x 32 square (BYOND draws it from the square's lower left, spilling
    /// up and right over the next slots); 0 when it fits as it is.
    /// </summary>
    public static float FitOf(int width, int height) => width > 32 || height > 32 ? Math.Min(32f / width, 32f / height) : 0f;
}

/// <summary>What the HUD pick needs from the drawing code: the entries and the window cells, bottom to top.</summary>
public interface IHudSurface
{
    /// <summary>Adds what is drawn now, the topmost last (the native windows over the HUD).</summary>
    void Collect(List<HudDrawn> bottomToTop);
}

/// <summary>
/// The HUD pick (<see cref="IHudPicker"/>, interaction.md 4.4): the topmost drawn entry with an opaque pixel under the point; tg's zone selector
/// reads the pixel within the entry's 32 x 32 icon. Protocol version 12 (BYOND's vis_contents rule, interaction.md section 2): within an
/// entry, its clickable children are tried first, topmost first, each against its own picture (a pixel with any alpha, BYOND's opacity test;
/// <see cref="HudChildDrawn.WholeBox"/> takes its whole box); a hit is a screen click on the child (the item in a hand or a slot), else on the entry.
/// </summary>
public sealed class HudPicker : IHudPicker
{
    private readonly IHudSurface _surface;
    private readonly List<HudDrawn> _drawn = new();

    public HudPicker(IHudSurface surface) => _surface = surface ?? throw new ArgumentNullException(nameof(surface));

    /// <inheritdoc />
    public PickHit PickScreen(float x, float y)
    {
        _drawn.Clear();
        _surface.Collect(_drawn);
        for (int i = _drawn.Count - 1; i >= 0; i--)
        {
            HudDrawn d = _drawn[i];
            if (x < d.Rect.Left || x >= d.Rect.Right || y < d.Rect.Top || y >= d.Rect.Bottom || d.Rect.Width <= 0 || d.Rect.Height <= 0)
                continue;
            double fx = (x - d.Rect.Left) / d.Rect.Width, fy = (y - d.Rect.Top) / d.Rect.Height;
            int ix, iy;
            bool opaque = true;
            if (d.Image != null)
            {
                int col = Math.Clamp((int)(fx * d.Image.Width), 0, d.Image.Width - 1);
                int row = Math.Clamp((int)(fy * d.Image.Height), 0, d.Image.Height - 1);
                opaque = d.Image.Pixels[(row * d.Image.Width + col) * 4 + 3] >= BillboardMath.PickAlpha;
                ix = col - d.OriginX + 1;
                iy = d.Image.Height - 1 - row - d.OriginY + 1;
            }
            else
            {
                ix = (int)(fx * 32) + 1;
                iy = (int)((1 - fy) * 32) + 1;
            }
            // Version 12: the topmost child with a pixel at this point of the holder's icon square takes the click.
            if (d.Children != null)
            {
                for (int k = d.Children.Count - 1; k >= 0; k--)
                {
                    HudChildDrawn c = d.Children[k];
                    if (HitsChild(c, ix - 1, iy - 1))
                        return new PickHit(PickSurface.Screen, TargetKind.Screen, c.Id, 0, 0, 0, 0, x, y, 0, (byte)Math.Clamp(ix - c.ShiftX, 1, 32), (byte)Math.Clamp(iy - c.ShiftY, 1, 32));
                }
            }
            if (!opaque)
                continue;
            return new PickHit(PickSurface.Screen, TargetKind.Screen, d.EntryId, 0, 0, 0, 0, x, y, 0, (byte)Math.Clamp(ix, 1, 32), (byte)Math.Clamp(iy, 1, 32));
        }
        return PickHit.None;
    }

    // The holder's icon-square pixel (qx right, qy up from its lower left corner) on the child's own picture: a pixel with any alpha, or the
    // whole picture box for mouse_opacity 2.
    private static bool HitsChild(in HudChildDrawn c, int qx, int qy)
    {
        if (c.Image == null || c.Image.Width <= 0 || c.Image.Height <= 0)
            return false;
        int col = qx - c.ShiftX + c.OriginX, fromBottom = qy - c.ShiftY + c.OriginY;
        if (c.Fit > 0f)
        {
            // fitted: the whole picture, scaled, centred in the square
            col = (int)Math.Floor((qx + 0.5 - c.ShiftX - (32 - c.Image.Width * c.Fit) / 2) / c.Fit);
            fromBottom = (int)Math.Floor((qy + 0.5 - c.ShiftY - (32 - c.Image.Height * c.Fit) / 2) / c.Fit);
        }
        if (col < 0 || col >= c.Image.Width || fromBottom < 0 || fromBottom >= c.Image.Height)
            return false;
        if (c.WholeBox)
            return true;
        int row = c.Image.Height - 1 - fromBottom;
        return c.Image.Pixels[(row * c.Image.Width + col) * 4 + 3] > 0;
    }
}
