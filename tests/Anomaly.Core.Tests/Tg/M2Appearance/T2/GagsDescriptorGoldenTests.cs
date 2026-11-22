using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Appearance.T2;

/// <summary>
/// The jumpsuit's GAGS icon (DV19, "GAGS icons are composed on the client"): SSgreyscale.GetColoredIconByType
/// (greyscale.dm:60-86) keeps all its checks and returns, where tg returns the generated icon, the text
/// <c>gags:&lt;config type path&gt;|&lt;colours&gt;</c> that the client composes. items.dm:394-399 calls it with the item's
/// greyscale_config_worn and greyscale_colors (a jumpsuit: /datum/greyscale_config/jumpsuit/worn and "#3f3f3f", color.dm:10-14).
/// </summary>
public class GagsDescriptorGoldenTests
{
    [Fact]
    public void TheJumpsuitsWornIconIsTheWornConfigAndTheItemsColours()
    {
        var r = new T2Room(2001);
        r.In(() =>
        {
            Assert.Equal("gags:/datum/greyscale_config/jumpsuit/worn|#3f3f3f", SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_jumpsuit_worn), "#3f3f3f"));
            // (the inhand configurations of the same item, items.dm:397-398, come with the items in hands: M4a)
            Assert.Equal("gags:/datum/greyscale_config/jumpsuit|#3f3f3f", SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_jumpsuit), "#3f3f3f"));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AColourListIsJoinedWithNothingBetweenTheColoursAndANewColourIsANewDescriptor()
    {
        var r = new T2Room(2002);
        r.In(() =>
        {
            Assert.Equal("gags:/datum/greyscale_config/jumpsuit|#ffffff#000000", SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_jumpsuit), Dm.list("#ffffff", "#000000")));
            Assert.NotEqual(SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_jumpsuit_worn), "#3f3f3f"), SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_jumpsuit_worn), "#3f3f40"));
            // the same configuration and colours always name the same icon (the client's cache key)
            Assert.Equal(SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_jumpsuit_worn), "#123456"), SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_jumpsuit_worn), "#123456"));
            // ParseColorString (greyscale.dm:98-102) splits what the descriptor joined
            Assert.Equal(new object[] { "#ffffff", "#000000" }, Dm.each(SSgreyscale.ParseColorString("#ffffff#000000")).Cast<object>().ToArray());
            Assert.Empty(Dm.each(SSgreyscale.ParseColorString("")).Cast<object>());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TgsChecksStandAsTgsCrashes()
    {
        var r = new T2Room(2003);
        r.In(() =>
        {
            Assert.Throws<DmCrashException>(() => SSgreyscale.GetColoredIconByType(typeof(obj), "#3f3f3f"));                    // :62 not a configuration
            Assert.Throws<DmCrashException>(() => SSgreyscale.GetColoredIconByType(null, "#3f3f3f"));
            Assert.Throws<DmCrashException>(() => SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_jumpsuit_worn), 5)); // :69 colours neither a list nor text
            Assert.Throws<DmCrashException>(() => SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_jumpsuit_worn), null));
        });
    }

    [Fact]
    public void ADescriptorIsWhatAnAtomsIconVarCarriesAndTheOverlaySnapshotKeepsItAsText()
    {
        // build_worn_icon uses worn_icon as the icon of the worn mutable_appearance; the snapshot keeps an icon given as text as it is
        var r = new T2Room(2004);
        r.In(() =>
        {
            string gags = Dm.str(SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_jumpsuit_worn), "#3f3f3f"));
            object ma = mutable_appearance(gags, "jumpsuit", -UNIFORM_LAYER);
            var holder = Dm.@new<obj>(locate(5, 5, 1));
            holder.add_overlay(ma);
            var snapshot = (IDmAppearance)Dm.each(holder.overlays).Cast<object>().Single();
            Assert.Equal(gags, snapshot.icon as string);
            Assert.Equal("jumpsuit", snapshot.icon_state);
            Assert.Equal(-UNIFORM_LAYER, snapshot.layer);
        });
        r.AssertNoRuntimes();
    }
}
