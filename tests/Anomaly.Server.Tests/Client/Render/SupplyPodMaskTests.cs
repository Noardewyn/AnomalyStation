using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>
/// Syndicate G4c (D316): BYOND's icon.SwapColor in the icon recipe, and tg's supply pod door masked by its fin shape
/// (supplypod.dm:243): the mask's white body is swapped out, so the door keeps only the pixels outside the fins.
/// </summary>
public class SupplyPodMaskTests
{
    [Fact]
    public void SwapColorReplacesItsColourAndClearsWithNone()
    {
        Raster r = Raster.Solid(2, 1, 0xFFFFFFFF);
        r.Blend(Raster.Solid(1, 1, 0x000000FF), IconBlend.Overlay);
        r.SwapColor(0xFFFFFFFF, matchAlpha: true, 0x00000000);
        Assert.Equal(0x000000FFu, r.Get(0, 0)); // the black pixel stays
        Assert.Equal(0x00000000u, r.Get(1, 0)); // the white one is gone
        Raster half = Raster.Solid(1, 1, 0xFF000080);
        half.SwapColor(0xFF0000FF, matchAlpha: true, 0x00FF00FF);
        Assert.Equal(0xFF000080u, half.Get(0, 0)); // an old colour with alpha matches the alpha too
        half.SwapColor(0xFF0000FF, matchAlpha: false, 0x00FF00FF);
        Assert.Equal(0x00FF00FFu, half.Get(0, 0)); // one without matches any visible pixel of that colour
    }

    [RealIconsFact]
    public void ThePodDoorLosesItsFinsInTheRecipe()
    {
        object file = Dm.resource("icons/obj/supplypods.dmi");
        icon door = Dm.icon(file, "pod_door");
        icon mask = Dm.icon(file, "mask_topfin");
        mask.MapColors(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1);
        mask.SwapColor("#ffffffff", null);
        mask.Blend(COLOR_BLACK, ICON_SUBTRACT);
        door.Blend(mask, ICON_ADD);
        string recipe = door.dm_resource();
        Assert.Contains(",w,", Uri.UnescapeDataString(recipe));

        IconLibrary library = RealIcons.NewLibrary();
        Assert.True(library.TryGetIcon("icons/obj/supplypods.dmi", out IIcon plain));
        Assert.True(plain.TryGetState("pod_door", out IIconState plainDoor));
        Assert.True(plain.TryGetState("mask_topfin", out IIconState fins));
        Assert.True(library.TryGetIcon(recipe, out IIcon masked));
        Assert.True(masked.TryGetState("pod_door", out IIconState maskedDoor));
        Raster before = plainDoor.Get(0, 0), after = maskedDoor.Get(0, 0), fin = fins.Get(0, 0);
        int kept = 0, cut = 0;
        for (int y = 0; y < before.Height; y++)
        {
            for (int x = 0; x < before.Width; x++)
            {
                bool door0 = (before.Get(x, y) & 0xFF) != 0, finPixel = (fin.Get(x, y) & 0xFF) != 0, shown = (after.Get(x, y) & 0xFF) != 0;
                if (door0 && finPixel)
                    Assert.False(shown, $"the fin's pixel {x},{y} shows on the masked door");
                if (door0 && !finPixel && shown)
                {
                    kept++;
                    Assert.True((before.Get(x, y) | 0xFF) == (after.Get(x, y) | 0xFF), $"the door's pixel {x},{y} changed colour: {before.Get(x, y):X8} -> {after.Get(x, y):X8}");
                }
                if (door0 && finPixel)
                    cut++;
            }
        }
        Assert.True(kept > 0, "the masked door shows nothing");
        Assert.True(cut > 0, "the fins cover none of the door");
    }
}
