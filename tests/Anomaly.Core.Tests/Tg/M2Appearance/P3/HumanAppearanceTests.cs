using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.M2Appearance.P3.AppearanceFixtures;

namespace Anomaly.Core.Tests.Tg.M2Appearance.P3;

/// <summary>
/// A human's overlay stack as tg's appearance code builds it (scope 2j A.2, D104): the expected entries are read from the DM
/// (carbon_update_icons.dm:372-418 update_body_parts, _bodyparts.dm:1330-1478 get_limb_icon and :1151-1192
/// get_bodypart_damage_state, head_hair_and_lips.dm:47-62 and :188-235 for the empty head, living_update_icons.dm:5-59
/// update_transform), entry by entry: icon file, icon state, layer, colour.
/// </summary>
public class HumanAppearanceTests
{
    private const string Greyscale = "icons/mob/human/bodyparts_greyscale.dmi";
    private const string DamMob = "icons/mob/effects/dam_mob.dmi";

    [Fact]
    public void AStandingHealthyHumansStackIsTheLimbImagesTheEyelessAndTheDebrainedOverlays()
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 41);
        room.In(() =>
        {
            DmWorldState.Current.NotTranslatedThrows = true;
            var human = Human();
            Waiting.NoSleep(human.update_body(FALSE));

            // get_bodyparts_by_zones walks the human species' zone order (l_arm, r_arm, head, l_leg, r_leg, chest); each
            // limb gives "[limb_id]_[zone][_gender]" from the greyscale file at -BODYPARTS_LAYER, coloured with the draw
            // colour, and an arm its hand on -aux_layer; a leg is cut by leg_masks.dmi into its image on -BODYPARTS_LAYER and its
            // lower one on -BODYPARTS_LOW_LAYER (handle_masking, M6 slice 7b); the head (no eyes, no brain) adds eyes_missing and
            // debrained on -HAIR_LAYER (get_eyeless_overlay, get_debrain_overlay).
            string[] expected =
            {
                $"{Greyscale}:human_l_arm@{-BODYPARTS_LAYER}#{Skin}",
                $"{Greyscale}:human_l_hand@{-BODYPARTS_HIGH_LAYER}#{Skin}",
                $"{Greyscale}:human_r_arm@{-BODYPARTS_LAYER}#{Skin}",
                $"{Greyscale}:human_r_hand@{-BODYPARTS_HIGH_LAYER}#{Skin}",
                $"{Greyscale}:human_head_m@{-BODYPARTS_LAYER}#{Skin}",
                $"{Greyscale}*left_leg:human_l_leg@{-BODYPARTS_LAYER}#{Skin}",
                $"{Greyscale}*left_leg_lower:human_l_leg@{-BODYPARTS_LOW_LAYER}#{Skin}",
                $"{Greyscale}*right_leg:human_r_leg@{-BODYPARTS_LAYER}#{Skin}",
                $"{Greyscale}*right_leg_lower:human_r_leg@{-BODYPARTS_LOW_LAYER}#{Skin}",
                $"{Greyscale}:human_chest_m@{-BODYPARTS_LAYER}#{Skin}",
                $"icons/mob/human/human_eyes.dmi:eyes_missing@{-HAIR_LAYER}#",
                $"icons/mob/human/human_face.dmi:debrained@{-HAIR_LAYER}#",
            };
            Assert.Equal(expected, Overlays(human).Select(Describe).ToArray());
            // the limb images inherit the human's direction (made without a dir)
            Assert.All(Overlays(human), a => Assert.True(((DmAppearance)a).InheritsDir));
            // the limb icon cache keyed by tg's render key ("m-human-chest-#ffcc99-<height>" for the chest)
            Assert.Equal(Dm.text("m-human-chest-[]-[]", Skin, human.mob_height), human.icon_render_keys[BODY_ZONE_CHEST]);

            // a second update with nothing changed rebuilds nothing (limb_count_update 0) and keeps the same stack
            Assert.True(Dm.eq(0, Waiting.NoSleep(human.update_body_parts())));
            Assert.Equal(expected, Overlays(human).Select(Describe).ToArray());
            DmWorldState.Current.NotTranslatedThrows = false;
        });
        Assert.Empty(room.RuntimeText);
    }

    public static TheoryData<string, int, bool> DamageLevels()
    {
        var data = new TheoryData<string, int, bool>();
        foreach (string zone in new[] { BODY_ZONE_CHEST, BODY_ZONE_HEAD, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG })
        {
            for (int level = 1; level <= 3; level++)
            {
                data.Add(zone, level, true);
                data.Add(zone, level, false);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(DamageLevels))]
    public void EachBruteAndBurnLevelOfEachZoneIsTgsDamageOverlay(string zone, int level, bool brute)
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 42);
        room.In(() =>
        {
            DmWorldState.Current.NotTranslatedThrows = true;
            var human = Human();
            Waiting.NoSleep(human.update_body(FALSE));
            obj_item_bodypart part = Part(human, zone);
            if (brute) part.brutestate = level;
            else part.burnstate = level;
            Waiting.NoSleep(human.update_damage_overlays());

            // update_damage_overlays: one mutable appearance on -DAMAGE_LAYER (no icon) holding the part's overlays
            List<IDmAppearance> damage = Overlays(human).Where(a => a.layer == -DAMAGE_LAYER && a.icon == null).ToList();
            Assert.Single(damage);
            string[] nested = damage[0].overlays.CopyItems().Select(o => Describe((IDmAppearance)o)).ToArray();
            if (brute)
            {
                // "[dmg_overlay_type]_[body_zone]_[brutestate]0" tinted with the blood colour, then its "_overlay" detail
                // on -DAMAGE_OVERLAY_LAYER with RESET_COLOR
                Assert.Equal(new[]
                {
                    $"{DamMob}:human_{zone}_{level}0@{-DAMAGE_LAYER}#{BLOOD_COLOR_RED}",
                    $"{DamMob}:human_{zone}_{level}0_overlay@{-DAMAGE_OVERLAY_LAYER}#",
                }, nested);
                Assert.Equal(RESET_COLOR, ((IDmAppearance)damage[0].overlays[2f]).appearance_flags);
            }
            else
            {
                Assert.Equal(new[] { $"{DamMob}:human_{zone}_0{level}@{-DAMAGE_LAYER}#" }, nested);
            }

            // healed: the next update cuts the damage overlay (remove_overlay finds the snapshot of the cached MA)
            part.brutestate = 0;
            part.burnstate = 0;
            Waiting.NoSleep(human.update_damage_overlays());
            Assert.DoesNotContain(Overlays(human), a => a.layer == -DAMAGE_LAYER);
            Assert.Null(human.overlays_standing[DAMAGE_LAYER]);
            DmWorldState.Current.NotTranslatedThrows = false;
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void BruteAndBurnOnTwoZonesShareOneDamageOverlayInBodypartOrder()
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 43);
        room.In(() =>
        {
            DmWorldState.Current.NotTranslatedThrows = true;
            var human = Human();
            Part(human, BODY_ZONE_HEAD).burnstate = 1;
            Part(human, BODY_ZONE_CHEST).brutestate = 3;
            Part(human, BODY_ZONE_CHEST).burnstate = 2;
            Waiting.NoSleep(human.update_damage_overlays());
            IDmAppearance damage = Overlays(human).Single(a => a.layer == -DAMAGE_LAYER);
            // get_bodyparts order (the test body's: chest, head, ...): the chest's brute, its detail and burn, then the head's burn
            Assert.Equal(new[]
            {
                $"{DamMob}:human_chest_30@{-DAMAGE_LAYER}#{BLOOD_COLOR_RED}",
                $"{DamMob}:human_chest_30_overlay@{-DAMAGE_OVERLAY_LAYER}#",
                $"{DamMob}:human_chest_02@{-DAMAGE_LAYER}#",
                $"{DamMob}:human_head_01@{-DAMAGE_LAYER}#",
            }, damage.overlays.CopyItems().Select(o => Describe((IDmAppearance)o)).ToArray());
            DmWorldState.Current.NotTranslatedThrows = false;
        });
        Assert.Empty(room.RuntimeText);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(270)]
    public void LyingTurnsTheTransformByTheLyingAngleAndStandingTurnsItBack(int angle)
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 44);
        room.In(() =>
        {
            DmWorldState.Current.NotTranslatedThrows = true;
            var human = Human();
            human.dir = SOUTH;
            Assert.True(Dm.truthy(human.rotate_on_lying)); // carbon_defines.dm: rotate_on_lying = TRUE
            Waiting.NoSleep(human.update_body(FALSE));
            string[] standing = Overlays(human).Select(Describe).ToArray();

            // set_lying_angle (living.dm:2317, P1's) sets lying_angle, calls update_transform, then lying_prev
            human.lying_angle = angle;
            Assert.True(Dm.eq(TRUE, Waiting.NoSleep(human.update_transform())));
            human.lying_prev = angle;
            var turned = (matrix)human.transform;
            float sin = angle == 90 ? 1 : -1; // Turn(angle) is clockwise: a = cos, b = sin, d = -sin, e = cos
            Assert.Equal(0f, turned.a, 5);
            Assert.Equal(sin, turned.b, 5);
            Assert.Equal(-sin, turned.d, 5);
            Assert.Equal(0f, turned.e, 5);
            Assert.Equal((0f, 0f), (turned.c, turned.f)); // current_size 1: no translation
            Assert.Equal((float)SOUTH, human.dir);         // facing south: no fall to a side
            Assert.Same(((IDmAppearance)human).transform, ((IDmAppearance)human).transform);
            // lying draws the same stack turned by the transform: update_transform changes no overlay
            Assert.Equal(standing, Overlays(human).Select(Describe).ToArray());

            // on_lying_down (living.dm:750, P1's): add_offsets(LYING_DOWN_TRAIT, y_add = PIXEL_Y_OFFSET_LYING)
            human.add_offsets(LYING_DOWN_TRAIT, y_add: PIXEL_Y_OFFSET_LYING);
            Assert.Equal(-6f, human.pixel_y);
            Assert.True(Dm.eq(-6, human.has_offset(LYING_DOWN_TRAIT, PIXEL_Y_OFFSET)));

            // standing up: TurnTo(lying_prev, 0) back to the identity, and on_standing_up's remove_offsets
            human.lying_angle = 0;
            Waiting.NoSleep(human.update_transform());
            human.lying_prev = 0;
            var back = (matrix)human.transform;
            Assert.Equal((1f, 0f, 0f, 1f), (MathF.Round(back.a, 5), MathF.Round(back.b, 5) + 0f, MathF.Round(back.d, 5) + 0f, MathF.Round(back.e, 5)));
            human.remove_offsets(LYING_DOWN_TRAIT);
            Assert.Equal(0f, human.pixel_y);
            Assert.Null(human.offsets);
            DmWorldState.Current.NotTranslatedThrows = false;
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void FallingWhileFacingEastLandsFacingNorthOrSouth()
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 45);
        room.In(() =>
        {
            var human = Human();
            human.dir = EAST;
            human.lying_angle = 90;
            Waiting.NoSleep(human.update_transform());
            Assert.Contains(human.dir, new[] { (float)NORTH, (float)SOUTH }); // pick(NORTH, SOUTH), living_update_icons.dm:19
        });
        Assert.Empty(room.RuntimeText);
    }
}
