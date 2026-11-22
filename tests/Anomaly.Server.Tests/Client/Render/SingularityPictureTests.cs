using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>
/// The M7 visual check took the delamination's orange swirl for a miscoloured <c>singularity_s7</c> (tg's dark disc with a blue swirl
/// and a red centre). It is tg's own stage six: the delamination's singularity eats the crystal (<c>supermatter_upgrade</c>, then
/// <c>consumed_supermatter</c> lets it grow to <c>STAGE_SIX</c>), whose sprite is <c>icons/effects/352x352.dmi</c> <c>singularity_s11</c>,
/// orange with a violet core. Both stages are composed pixel for pixel as the dmi has them (the expected values were read from tg's
/// dmi files by a separate PNG decoder, frame 0 at the sheet's top left).
/// </summary>
public class SingularityPictureTests
{
    private static Raster Compose(string icon, string state)
    {
        var tables = new TestTables();
        TableRef singularity = tables.Add(icon, state);
        IconLibrary library = RealIcons.NewLibrary();
        var compositor = new AppearanceCompositor(library);
        Assert.True(compositor.TryCompose(tables, singularity, ByondDir.South, 0, out ComposedRaster composed), string.Join("; ", library.Problems));
        Assert.Equal((0, 0), (composed.OriginX, composed.OriginY));
        return composed.Image;
    }

    [RealIconsFact]
    public void StageFourIsTheDarkDiscWithTheBlueSwirlAndTheRedCentre()
    {
        Raster s7 = Compose("icons/effects/224x224.dmi", "singularity_s7");
        Assert.Equal((224, 224), (s7.Width, s7.Height));
        Assert.Equal(0xB5006CFFu, s7.Get(112, 112)); // the red centre
        Assert.Equal(0x10009CFFu, s7.Get(40, 112));  // the blue swirl
        Assert.Equal(0x000060FFu, s7.Get(112, 40));
        Assert.Equal(0x000012FFu, s7.Get(60, 112));  // the dark disc
        Assert.Equal(0x000000FFu, s7.Get(112, 200));
        Assert.Equal(0u, s7.Get(5, 112) & 0xFF);     // outside the disc
    }

    [RealIconsFact]
    public void StageSixTheDelaminationsSingularityIsTheOrangeSwirl()
    {
        Raster s11 = Compose("icons/effects/352x352.dmi", "singularity_s11");
        Assert.Equal((352, 352), (s11.Width, s11.Height));
        Assert.Equal(0x000000FFu, s11.Get(176, 176)); // the black eye
        Assert.Equal(0xAF182BFFu, s11.Get(176, 120));
        Assert.Equal(0xEA7E29FFu, s11.Get(176, 60));  // the orange swirl
        Assert.Equal(0xD3401AFFu, s11.Get(100, 176));
        Assert.Equal(0xCAA551CBu, s11.Get(176, 20));  // the rim, half transparent
    }
}
