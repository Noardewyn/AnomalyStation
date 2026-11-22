using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tests.Tg.M2Appearance.P3.AppearanceFixtures;

namespace Anomaly.Core.Tests.M7;

/// <summary>
/// A body burnt to a husk (become_husk(BURN), status_procs.dm:575; husk_trait_toggle redraws the body) draws its limbs as a
/// burn husk (_bodyparts.dm:1214 update_limb: HUSKED_BURN unless the husk comes from the zombie status effect only): each limb's
/// icon is its file toned by tg's ColorTone(HUSK_COLOR_TONE) (_bodyparts.dm:1487, __HELPERS/icons.dm:213), kept as the iconops
/// recipe the client draws (D104), and a husk blood overlay "[husk_type]_husk_[body_zone]" (BLEND_INSET_OVERLAY, RESET_COLOR,
/// blood red without blood DNA) joins it.
/// </summary>
public class HuskTests
{
    private const string Greyscale = "icons/mob/human/bodyparts_greyscale.dmi";

    // ColorTone(rgb(96, 88, 80)) as __HELPERS/icons.dm:213-228 does it: gray = round(96*0.3 + 88*0.59 + 80*0.11, 1) = 90; GrayScale,
    // then the lower half times 255/90 and the tone, plus a copy (taken after GrayScale) less gray 90 scaled into 255 - tone.
    // (Parts escape only % and |, so the recipe stays text that fits a resource, Limits.MaxResourceBytes.)
    private static string HuskRecipe(string state) => "iconops:" + Greyscale + "|" + state + "|0|"
        + string.Join("|", new[]
        {
            "101,1,1,m,0.3,0.3,0.3,0.59,0.59,0.59,0.11,0.11,0.11,0,0,0",
            "101,1,1,m," + F(255f / 90) + ",0,0,0," + F(255f / 90) + ",0,0,0," + F(255f / 90) + ",0,0,0",
            "2,1,1,c,#605850",
            "0,1,1,i," + Greyscale + ",,3",
            "101,1,1,m,0.3,0.3,0.3,0.59,0.59,0.59,0.11,0.11,0.11,0,0,0",
            "1,1,1,c,#5a5a5a",
            "101,1,1,m," + F(159f / 165) + ",0,0,0,0," + F(167f / 165) + ",0,0,0,0," + F(175f / 165) + ",0,0,0,0,0,0,0,0,1",
        });

    private static string F(float n) => n.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void ABurntHuskDrawsItsLimbsAsABurnHusk()
    {
        var r = new AudioRoom(9, 9, seed: 7964);
        r.Step(20);
        mob_living_carbon_human ann = r.Dummy(4, 4);
        r.Step(2);
        r.In(() => ann.become_husk(BURN));
        r.Step(2);
        var chest = r.In(() => (obj_item_bodypart)ann.get_bodypart(BODY_ZONE_CHEST));
        Assert.Equal((float)HUSKED_BURN, r.In(() => chest.is_husked));
        Assert.Equal("", r.In(() => Dm.str(chest.dmg_overlay_type)));

        List<IDmAppearance> overlays = r.In(() => Overlays(ann));
        // the chest: its greyscale file toned as a husk, drawn in the husk colour
        // the dummy's physique is a random pick (the seed's stream moved with gaps MS1): the dimorphic chest's state follows it
        string chestState = r.In(() => "human_chest_" + chest.limb_gender);
        IDmAppearance limb = Assert.Single(overlays, a => a.icon_state == chestState && a.layer == -BODYPARTS_LAYER);
        icon toned = Assert.IsType<icon>(limb.icon);
        Assert.Null(toned.dm_state); // new /icon(file): every state of the file
        Assert.Equal(HuskRecipe(""), toned.dm_resource());
        Assert.Equal(chest.husk_color, limb.color);
        // its husk blood: tg's state, masked by the body below it, not tinted by the limb's colour
        IDmAppearance blood = Assert.Single(overlays, a => a.icon_state == "humanoid_husk_chest");
        Assert.Equal("icons/mob/human/bodyparts.dmi", IconPath(blood));
        Assert.Equal((float)BLEND_INSET_OVERLAY, blood.blend_mode);
        Assert.Equal((float)RESET_COLOR, blood.appearance_flags);
        Assert.Equal((float)-BODYPARTS_LAYER, blood.layer);
        Assert.Equal(BLOOD_COLOR_RED, blood.color);
        // every limb has its blood (a leg's two halves masked, handle_masking), and an arm's hand is toned as well
        foreach (string zone in new[] { BODY_ZONE_HEAD, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG })
            Assert.Contains(overlays, a => a.icon_state == "humanoid_husk_" + zone);
        IDmAppearance hand = Assert.Single(overlays, a => a.icon_state == "human_l_hand");
        Assert.Equal(HuskRecipe(""), Assert.IsType<icon>(hand.icon).dm_resource());
        r.AssertNoRuntimes();
    }
}
