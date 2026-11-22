using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M10;

/// <summary>
/// M10x slice D2 (lobby.md 8), written from tg's head_hair_and_lips.dm, hair.dm (getCachedIcon), _eyes.dm (generate_body_overlay)
/// and human_update_icons.dm (get_underwear_overlays): a human's hair, its rear braid blended in, its outer braid above the hat,
/// its colour, its gradient, the briefs and the eyes are on the body; a hat's hair mask is blended into the hair.
/// </summary>
public class BodyLooksTests
{
    private const int Second = 20;

    // Every appearance on the body, the overlays of overlays included.
    private static List<image> Looks(mob_living_carbon_human h)
    {
        var found = new List<image>();
        void Walk(object o)
        {
            if (o is image a)
            {
                found.Add(a);
                foreach (object inner in Dm.each(a.overlays))
                    Walk(inner);
            }
            else if (o is DmList list)
            {
                foreach (object item in Dm.each(list))
                    Walk(item);
            }
        }
        Walk(h.overlays_standing);
        return found;
    }

    private static string Recipe(image a) => a.icon is icon i ? i.dm_resource() + "|" + i.dm_state : a.icon?.ToString();

    [Fact]
    public void HairUnderwearAndEyesDrawOnTheBodyAndAHatMasksTheHair()
    {
        var r = new AudioRoom(8, 8, seed: 285);
        mob_living_carbon_human h = r.Dummy(4, 4);
        r.Step(Second);
        r.In(() =>
        {
            h.set_haircolor("#aa5500", update: TRUE);
            h.set_facial_haircolor("#aa5500", update: TRUE);
            h.set_hairstyle("Braid (Floorlength)", update: TRUE);
            h.set_facial_hairstyle("Beard (Full)", update: TRUE);
            h.underwear = "Briefs";
            h.underwear_color = "#3355ff";
            Waiting.NoSleep(h.set_eye_color("#00ff00"));
            Waiting.NoSleep(h.update_eyes()); // update_body_parts skips the eyes when no limb key changed (carbon_update_icons.dm:405)
            Waiting.NoSleep(h.update_body(TRUE));
        });
        r.Step(1);
        r.In(() =>
        {
            List<image> looks = Looks(h);
            image hair = Assert.Single(looks, a => Recipe(a)?.Contains("hair_braid|", StringComparison.Ordinal) == true);
            Assert.Contains("hair_braid_a1", Recipe(hair)); // the inner appendage blended in (getCachedIcon)
            Assert.Equal("#aa5500", hair.color);
            Assert.Equal(-HAIR_LAYER, hair.layer);
            Assert.Contains(looks, a => Dm.eq(a.icon_state, "hair_braid_a1o") && a.layer == -OUTER_HAIR_LAYER);
            Assert.Contains(looks, a => Dm.eq(a.icon_state, "facial_fullbeard") && Dm.eq(a.color, "#aa5500"));
            image briefs = Assert.Single(looks, a => Dm.eq(a.icon_state, "male_briefs"));
            Assert.Equal("#3355ff", briefs.color);
            Assert.Contains(looks, a => Dm.eq(a.icon_state, "eyes_l") && Dm.eq(a.color, "#00ff00"));
            Assert.Contains(looks, a => Dm.eq(a.icon_state, "eyes_r") && Dm.eq(a.color, "#00ff00"));

            var hat = Dm.@new<obj_item_clothing_head_beret>(h.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(h.equip_to_slot_if_possible(hat, ITEM_SLOT_HEAD))));
        });
        r.Step(1);
        r.In(() =>
        {
            image hair = Assert.Single(Looks(h), a => Recipe(a)?.Contains("hair_braid|", StringComparison.Ordinal) == true);
            Assert.Contains("hair_masks.dmi", Recipe(hair));
            Assert.Contains("hide_above_45deg", Recipe(hair));
        });
    }

    [Fact]
    public void AHairGradientIsTheGradientBlendedWithTheHair()
    {
        var r = new AudioRoom(8, 8, seed: 286);
        mob_living_carbon_human h = r.Dummy(4, 4);
        r.Step(Second);
        r.In(() =>
        {
            h.set_hairstyle("Afro", update: TRUE);
            h.set_hair_gradient_style("Fade Up", update: TRUE);
            h.set_hair_gradient_color("#ff00ff", update: TRUE);
            Waiting.NoSleep(h.update_body(TRUE));
        });
        r.Step(1);
        r.In(() =>
        {
            image gradient = Assert.Single(Looks(h), a => Recipe(a)?.Contains("hair_gradients.dmi", StringComparison.Ordinal) == true);
            Assert.Contains("fadeup", Recipe(gradient));
            Assert.Contains("hair_afro", Recipe(gradient));
            Assert.Equal("#ff00ff", gradient.color);
        });
    }
}
