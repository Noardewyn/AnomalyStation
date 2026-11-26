using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.M2Appearance.T2.AppearanceRig;

namespace Anomaly.Core.Tests.Tg.M2Appearance.T2;

/// <summary>
/// The overlay stack of a real human (icon file, state, layer and colour of every entry) as tg's code builds it, for the
/// healthy body, for every brute and burn level of every zone, and for both postures; the expected lists are written from
/// carbon_update_icons.dm:284-303 (update_damage_overlays) and :372-418 (update_body_parts), _bodyparts.dm:1139-1192
/// (update_bodypart_damage_state, get_bodypart_damage_state) and :1330-1478 (get_limb_icon).
/// </summary>
public class HumanStackGoldenTests
{
    private const string Skin = "#ffe0d1"; // a /consistent human's skin tone (caucasian1, skintone2hex)

    [Fact]
    public void AHealthyStandingHumanIsTheEightLimbImagesAndTheEyesAtTheirLayersAndNothingElse()
    {
        var rig = new AppearanceRig();
        string[] stack = rig.Room.In(() => rig.Overlays().Select(Entry).ToArray());
        Assert.Equal(HealthyStack(Skin), stack);
        // the limb images are made without a dir (image_dir = null): they follow the mob's
        Assert.All(rig.Room.In(() => rig.Overlays()), a => Assert.True(((DmAppearance)a).InheritsDir));
        Assert.Null(rig.DamageOverlay());
        rig.Room.AssertNoRuntimes();
    }

    public static TheoryData<string, int, bool> LevelCases()
    {
        var data = new TheoryData<string, int, bool>();
        foreach (string zone in Zones)
            for (int level = 1; level <= 3; level++)
            {
                data.Add(zone, level, true);
                data.Add(zone, level, false);
            }
        return data;
    }

    // update_bodypart_damage_state: round((dmg / max_damage) * 3, 1), BYOND's round: halves go up. The damage is taken from the
    // middle of each band (a third of the way through: 0.35, 0.65 and 0.95 of the maximum give 1.05, 1.95 and 2.85).
    private static readonly float[] BandFraction = { 0f, 0.35f, 0.65f, 0.95f };

    [Theory]
    [MemberData(nameof(LevelCases))]
    public void EachBruteAndBurnLevelOfEachZoneIsTgsDamageOverlay(string zone, int level, bool brute)
    {
        var rig = new AppearanceRig(1802);
        float amount = rig.MaxDamage(zone) * BandFraction[level];
        rig.Damage(brute ? BRUTE : BURN, amount, zone);
        obj_item_bodypart part = rig.Part(zone);
        Assert.Equal(level, rig.Room.In(() => brute ? part.brutestate : part.burnstate));
        Assert.Equal(amount, rig.Room.In(() => brute ? part.brute_dam : part.burn_dam), 2);

        // the limb images are untouched, and one appearance with no icon at -DAMAGE_LAYER holds the part's overlays (:284-303)
        // (the eyes close in the eyelid colour when the damage puts the human in crit, _eyes.dm:476, without the open eyes' eyelid
        // overlays: the eyes' colour and those overlays are left out)
        static bool Eyes(string entry) => entry.StartsWith("icons/mob/human/human_eyes.dmi:", StringComparison.Ordinal);
        static string Uncoloured(string entry) => Eyes(entry) ? entry[..entry.LastIndexOf('#')] : entry;
        static bool Eyelid(string entry) => entry == $":@{-EYES_LAYER}#";
        Assert.Equal(HealthyStack(Skin).Where(e => !Eyelid(e)).Select(Uncoloured), rig.Room.In(() => rig.Overlays().Where(a => Icon(a) != null).Select(Entry).Where(e => !Eyelid(e)).Select(Uncoloured).ToArray()));
        var damage = rig.DamageOverlay();
        Assert.NotNull(damage);
        string[] expected = brute
            ? new[]
            {
                // :1155-1170 the blood overlay, tinted with the blood type's damage colour (BLOOD_COLOR_RED, #AA1010), and its
                // detail on -DAMAGE_OVERLAY_LAYER with RESET_COLOR (appearance flag 2)
                $"{DamMob}:human_{zone}_{level}0@{-DAMAGE_LAYER}#{BLOOD_COLOR_RED}",
                $"{DamMob}:human_{zone}_{level}0_overlay@{-DAMAGE_OVERLAY_LAYER}#",
            }
            : new[] { $"{DamMob}:human_{zone}_0{level}@{-DAMAGE_LAYER}#" }; // :1172-1177
        Assert.Equal(expected, damage.Value.Nested);
        if (brute)
            Assert.Equal(RESET_COLOR, rig.Room.In(() => ((IDmAppearance)Dm.each(damage.Value.Holder.overlays).Cast<object>().ElementAt(1)).appearance_flags));

        // healed: the next update cuts the damage appearance
        // (HEAL_ALL would reach cure_all_traumas, not translated: M8)
        rig.Room.Command(() => Waiting.NoSleep(rig.Human.fully_heal(HEAL_BRUTE | HEAL_BURN)));
        rig.Room.Step(8); // and the damaged part's red flash (DV400, 3 ds) has gone out
        Assert.Null(rig.DamageOverlay());
        Assert.Equal(HealthyStack(Skin), rig.Room.In(() => rig.Overlays().Select(Entry).ToArray()));
        rig.Room.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(0.00f, 0)]
    [InlineData(0.16f, 0)]   // 0.48
    [InlineData(0.20f, 1)]   // 0.6
    [InlineData(0.50f, 2)]   // exactly 1.5: BYOND's round goes up
    [InlineData(0.80f, 2)]   // 2.4
    [InlineData(0.84f, 3)]   // 2.52
    [InlineData(1.00f, 3)]
    public void TheDamageStateIsTheDamageRatioTimesThreeRoundedHalvesUp(float ratio, int expected)
    {
        var rig = new AppearanceRig(1803);
        rig.Damage(BRUTE, 50f * ratio, BODY_ZONE_L_LEG); // a leg holds 50
        Assert.Equal(expected, rig.Room.In(() => rig.Part(BODY_ZONE_L_LEG).brutestate));
        var damage = rig.DamageOverlay();
        if (expected == 0)
            Assert.Null(damage);
        else
            Assert.Equal($"{DamMob}:human_l_leg_{expected}0@{-DAMAGE_LAYER}#{BLOOD_COLOR_RED}", damage.Value.Nested[0]);
        rig.Room.AssertNoRuntimes();
    }

    [Fact]
    public void SeveralZonesShareOneDamageAppearanceInBodypartOrderBruteBeforeBurnInEachPart()
    {
        var rig = new AppearanceRig(1804);
        rig.Damage(BURN, 25f, BODY_ZONE_R_LEG);     // 0.5 of 50: level 2
        rig.Damage(BRUTE, 130f, BODY_ZONE_HEAD);    // 0.65 of 200: level 2
        rig.Damage(BRUTE, 130f, BODY_ZONE_CHEST);   // 0.65: level 2
        rig.Damage(BURN, 60f, BODY_ZONE_CHEST);     // 0.3 of the chest's 200: level 1 (a part takes at most max_damage in all)
        var damage = rig.DamageOverlay();
        Assert.NotNull(damage);
        // get_bodyparts order: chest, head, legs, arms; inside a part the brute pair, then the burn
        Assert.Equal(new[]
        {
            $"{DamMob}:human_chest_20@{-DAMAGE_LAYER}#{BLOOD_COLOR_RED}",
            $"{DamMob}:human_chest_20_overlay@{-DAMAGE_OVERLAY_LAYER}#",
            $"{DamMob}:human_chest_01@{-DAMAGE_LAYER}#",
            $"{DamMob}:human_head_20@{-DAMAGE_LAYER}#{BLOOD_COLOR_RED}",
            $"{DamMob}:human_head_20_overlay@{-DAMAGE_OVERLAY_LAYER}#",
            $"{DamMob}:human_r_leg_02@{-DAMAGE_LAYER}#",
        }, damage.Value.Nested);
        rig.Room.AssertNoRuntimes();
    }

    [Fact]
    public void LyingChangesTheTransformAndThePixelOffsetAndLeavesTheOverlayStackAsItWas()
    {
        // living.dm:743-750 on_lying_down (add_offsets y -6: PIXEL_Y_OFFSET_LYING), living_update_icons.dm:5-58 update_transform:
        // a Turn of lying_angle (clockwise: a = cos, b = sin, d = -sin, e = cos), no translation at size 1; standing turns it back
        var rig = new AppearanceRig(1805);
        string[] standing = rig.Room.In(() => rig.Overlays().Select(Entry).ToArray());
        var seen = new HashSet<float>();
        for (int round = 0; round < 6; round++)
        {
            rig.Room.Command(() => Waiting.NoSleep(rig.Human.set_resting(TRUE, TRUE)));
            rig.Room.Step(2);
            float angle = rig.Room.In(() => Dm.num(rig.Human.lying_angle));
            Assert.Contains(angle, new[] { 90f, 270f });
            seen.Add(angle);
            var m = rig.Room.In(() => (matrix)rig.Human.transform);
            float sin = angle == 90 ? 1 : -1;
            Assert.Equal((0f, sin, 0f, -sin, 0f, 0f), (MathF.Round(m.a, 4) + 0f, MathF.Round(m.b, 4), MathF.Round(m.c, 4), MathF.Round(m.d, 4), MathF.Round(m.e, 4) + 0f, MathF.Round(m.f, 4) + 0f));
            Assert.Equal(-6f, rig.Room.In(() => rig.Human.pixel_y)); // PIXEL_Y_OFFSET_LYING
            Assert.Equal(LYING_MOB_LAYER, rig.Room.In(() => rig.Human.layer));
            Assert.Equal(standing, rig.Room.In(() => rig.Overlays().Select(Entry).ToArray()));

            rig.Room.Command(() => Waiting.NoSleep(rig.Human.set_resting(FALSE, TRUE, TRUE)));
            rig.Room.Step(2);
            var back = rig.Room.In(() => (matrix)rig.Human.transform);
            Assert.Equal((1f, 0f, 0f, 0f, 1f, 0f), (MathF.Round(back.a, 4), MathF.Round(back.b, 4) + 0f, MathF.Round(back.c, 4) + 0f, MathF.Round(back.d, 4) + 0f, MathF.Round(back.e, 4), MathF.Round(back.f, 4) + 0f));
            Assert.Equal(0f, rig.Room.In(() => rig.Human.pixel_y));
            Assert.Equal(standing, rig.Room.In(() => rig.Overlays().Select(Entry).ToArray()));
        }
        Assert.Equal(2, seen.Count); // the angle is picked each time: both sides turned up in six falls (a fixed seed)
        rig.Room.AssertNoRuntimes();
    }

    [Fact]
    public void TheDamageAppearanceStaysOnALyingBody()
    {
        var rig = new AppearanceRig(1806);
        rig.Damage(BRUTE, 130f, BODY_ZONE_CHEST);
        rig.Room.Command(() => Waiting.NoSleep(rig.Human.set_resting(TRUE, TRUE)));
        rig.Room.Step(2);
        Assert.Equal($"{DamMob}:human_chest_20@{-DAMAGE_LAYER}#{BLOOD_COLOR_RED}", rig.DamageOverlay().Value.Nested[0]);
        rig.Room.AssertNoRuntimes();
    }
}
