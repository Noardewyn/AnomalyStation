using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M10;

/// <summary>
/// M10x slice D1 (lobby.md 8), written from tg's sprite_accessories.dm, clothing.dm and mobs.dm:71-118: SSaccessories' PreInit
/// lists one of every accessory by name (an accessory with no icon state, such as Bald, only as a name), splits them by gender,
/// adds a blank "None" to an optional feature, and a clothing accessory makes its appearance on the body layer.
/// </summary>
public class SpriteAccessoryTests
{
    [Fact]
    public void TheSubsystemListsEveryAccessoryByNameAndGender()
    {
        World w = TgTestWorld.Start();
        using (w.Enter())
        {
            datum_controller_subsystem_accessories ss = SSaccessories;
            Assert.Equal(221, ss.hairstyles_list.len); // hair.dm's 221 hair types
            Assert.True(ss.hairstyles_list.Contains("Bald"));
            Assert.Null(ss.hairstyles_list["Bald"]); // no icon state: a name only (sprite_accessories.dm:133)
            Assert.IsType<datum_sprite_accessory_facial_hair_shaved>(ss.facial_hairstyles_list["Shaved"]);
            Assert.Equal(40, ss.socks_list.len);
            Assert.True(ss.underwear_m.Contains("Briefs") && !ss.underwear_f.Contains("Briefs"));
            Assert.True(ss.underwear_m.Contains("Nude") && ss.underwear_f.Contains("Nude")); // neuter: both
            Assert.IsType<datum_sprite_accessory_blank>(((DmList)ss.feature_list[FEATURE_TAIL_CAT])[SPRITE_ACCESSORY_NONE]);
            Assert.False(((DmList)ss.feature_list[FEATURE_SNOUT]).Contains(SPRITE_ACCESSORY_NONE)); // a required feature
            Assert.Equal("reflected_high", ((datum_sprite_accessory)ss.hair_gradients_list["Reflected"]).icon_state);
            Assert.Equal("reflected_high_beard", ((datum_sprite_accessory)ss.facial_hair_gradients_list["Reflected"]).icon_state);
            Assert.Equal(4, ss.hair_masks_list.len);

            for (int i = 0; i < 20; i++)
            {
                Assert.True(ss.hairstyles_male_list.Contains(random_hairstyle(Dm.MALE)));
                Assert.True(ss.underwear_f.Contains(random_underwear(Dm.FEMALE)));
                Assert.True(ss.socks_list.Contains(random_socks()));
            }
        }
    }

    [Fact]
    public void ClothingMakesItsAppearanceOnTheBodyLayerAndNudeMakesNone()
    {
        World w = TgTestWorld.Start();
        using (w.Enter())
        {
            var briefs = (datum_sprite_accessory_clothing)SSaccessories.underwear_list["Briefs"];
            var look = (mutable_appearance)briefs.make_appearance("#ff0000", Dm.MALE, BODYSHAPE_HUMANOID);
            Assert.Equal("male_briefs", look.icon_state);
            Assert.Equal(-BODY_LAYER, Dm.num(look.layer));
            Assert.Equal("#ff0000", look.color);
            Assert.Null(Dm.@new<datum_sprite_accessory_clothing_underwear_nude>().make_appearance());
        }
    }
}
