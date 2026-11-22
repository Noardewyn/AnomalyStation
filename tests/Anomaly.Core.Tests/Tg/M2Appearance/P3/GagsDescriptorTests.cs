using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Appearance.P3;

/// <summary>
/// The jumpsuit's GAGS icon (scope 2j A.2, DV19 "GAGS icons are composed on the client"): tg's
/// SSgreyscale.GetColoredIconByType (controllers/subsystem/processing/greyscale.dm:60) with its checks, returning the
/// contracts' text <c>gags:&lt;config type path&gt;|&lt;colours&gt;</c> for the client to compose.
/// </summary>
public class GagsDescriptorTests
{
    [Fact]
    public void TheJumpsuitsWornIconIsTheConfigPathAndItsColours()
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 51);
        room.In(() =>
        {
            Assert.NotNull(SSgreyscale);
            Assert.True(Dm.truthy(SSgreyscale.initialized)); // an SS_NO_INIT subsystem counts as initialized (master.dm:479-481)
            // items.dm:397-398: worn_icon = SSgreyscale.GetColoredIconByType(greyscale_config_worn, greyscale_colors)
            Assert.Equal("gags:/datum/greyscale_config/jumpsuit/worn|#3f3f3f",
                SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_jumpsuit_worn), "#3f3f3f"));
            // the colour list format is joined (:66-67)
            Assert.Equal("gags:/datum/greyscale_config/jumpsuit|#ffffff#000000",
                SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_jumpsuit), Dm.list("#ffffff", "#000000")));
            Assert.Equal(new object[] { "#ffffff", "#000000" }, Dm.each(SSgreyscale.ParseColorString("#ffffff#000000")).Cast<object>().ToArray());
            // tg's CRASHes: not a configuration, or colours that are neither a list nor text
            Assert.Throws<DmCrashException>(() => SSgreyscale.GetColoredIconByType(typeof(obj), "#3f3f3f"));
            Assert.Throws<DmCrashException>(() => SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_jumpsuit_worn), 5));
        });
        Assert.Empty(room.RuntimeText);
    }
}
