using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Lighting;

/// <summary>
/// M7 lumcount, the callers: a mob's has_light_nearby (mob.dm:1313) reads the turf's lumcount, which makes reading (mob.dm:1333),
/// the NEED_LIGHT interactions (living.dm:1426), the examine range (mob.dm:568) and the sleeping tick's darkness (debuffs.dm:271) live.
/// </summary>
public class DarknessTests
{
    /// <summary>A static-lit toy room (as LumcountTests.LitRoom) with a chat sink.</summary>
    private static (TgToyRoom Room, ChatCapture Chat) DarkRoom()
    {
        var room = TgToyRoom.Create(width: 16, height: 11);
        var chat = new ChatCapture();
        room.In(() =>
        {
            PresentationSinks.Of(room.World).Messages = chat;
            ((area)TgToyRoom.T(5, 5).loc).static_lighting = TRUE;
            Waiting.NoSleep(SSlighting.Initialize());
        });
        return (room, chat);
    }

    private static mob_living_carbon_human Human(int x, int y)
    {
        var human = Dm.@new<mob_living_carbon_human>(TgToyRoom.T(x, y));
        ADD_TRAIT(human, TRAIT_LITERATE, "test");
        return human;
    }

    private static void Fire() => Waiting.NoSleep(SSlighting.fire(FALSE, TRUE));

    private static string Text(ChatCapture chat) => string.Join("\n", chat.Html);

    [Fact]
    public void ReadingNeedsLightAndTheMarkedRoomIsDarkUntilALampIsLit()
    {
        var (room, chat) = DarkRoom();
        room.In(() =>
        {
            var human = Human(5, 5);
            var book = Dm.@new<obj>(TgToyRoom.T(5, 5));
            Assert.False(Dm.truthy(human.has_light_nearby()));
            Assert.False(Dm.truthy(human.can_read(book)));
            Assert.Contains("It's too dark in here to read!", Text(chat));
            Assert.True(Dm.truthy(human.can_read(book, READING_CHECK_LITERACY))); // no light check asked

            Dm.@new<test_lum_lamp>(TgToyRoom.T(5, 5));
            Fire();
            Assert.True(Dm.truthy(human.has_light_nearby()));
            Assert.True(Dm.truthy(human.can_read(book)));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void NightVisionAndAHeldFlashlightPassTheDarkTile()
    {
        var (room, chat) = DarkRoom();
        room.In(() =>
        {
            var seer = Human(5, 5);
            var book = Dm.@new<obj>(TgToyRoom.T(5, 5));
            Assert.False(Dm.truthy(seer.can_read(book)));
            seer.lighting_cutoff = LIGHTING_CUTOFF_HIGH; // has_nightvision (mob.dm:1262): the plane is bright enough
            Assert.True(Dm.truthy(seer.has_nightvision()));
            Assert.True(Dm.truthy(seer.can_read(book)));

            var holder = Human(9, 5);
            var flashlight = Dm.@new<obj_item_flashlight>(TgToyRoom.T(9, 5));
            Assert.True(Dm.truthy(Waiting.NoSleep(holder.put_in_hands(flashlight))));
            Assert.False(Dm.truthy(holder.has_light_nearby()));
            Waiting.NoSleep(flashlight.toggle_light(holder));
            Assert.True(Dm.truthy(holder.has_light_nearby())); // lum_power 0.5 > LIGHTING_TILE_IS_DARK
            Assert.True(Dm.truthy(holder.can_read(book)));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void TheAnalyzersAltClickIsRefusedInTheDarkAndWorksInLight()
    {
        var (room, chat) = DarkRoom();
        room.In(() =>
        {
            var human = Human(5, 5);
            var analyzer = Dm.@new<obj_item_analyzer>(TgToyRoom.T(5, 5));
            Assert.True(Dm.truthy(Waiting.NoSleep(human.put_in_hands(analyzer))));

            Waiting.NoSleep(human.base_click_alt(analyzer)); // interaction_flags_click NEED_LIGHT (gas_analyzer.dm:19)
            Assert.Contains("You need more light to do this!", Text(chat));
            Assert.DoesNotContain("barometer", Text(chat));

            chat.Html.Clear();
            Dm.@new<test_lum_lamp>(TgToyRoom.T(5, 5));
            Fire();
            Waiting.NoSleep(human.base_click_alt(analyzer));
            Assert.DoesNotContain("You need more light", Text(chat));
            Assert.Contains("barometer function won't work indoors!", Text(chat)); // the toy room is indoors

            // night vision passes the same click in the dark
            chat.Html.Clear();
            Waiting.NoSleep(analyzer.set_light_on(FALSE));
            var night = Human(12, 8);
            night.lighting_cutoff = LIGHTING_CUTOFF_HIGH;
            var second = Dm.@new<obj_item_analyzer>(TgToyRoom.T(12, 8));
            Assert.True(Dm.truthy(Waiting.NoSleep(night.put_in_hands(second))));
            Waiting.NoSleep(night.base_click_alt(second));
            Assert.Contains("barometer function won't work indoors!", Text(chat));
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void ExaminingFarIsRefusedOnADarkTurfWithoutLuminosityAndPassesWithLightOrNightVision()
    {
        var (room, chat) = DarkRoom();
        room.In(() =>
        {
            var human = Human(3, 5);
            turf far_turf = TgToyRoom.T(10, 5);
            var far = Dm.@new<obj>(far_turf);
            var near = Dm.@new<obj>(TgToyRoom.T(4, 5));
            // a static-lit turf keeps luminosity 1 ("default to fullbright", lighting_object.dm:49), so tg lets a mob examine it
            // from afar even when it is dark; a turf of no luminosity (an unlit area, space) is what mob.dm:568 refuses
            Assert.Equal(1f, far_turf.luminosity);
            Waiting.NoSleep(human.run_examinate(far));
            Assert.NotEmpty(chat.Html);

            chat.Html.Clear();
            far_turf.luminosity = 0;
            Waiting.NoSleep(human.run_examinate(far));
            Assert.Empty(chat.Html);
            Waiting.NoSleep(human.run_examinate(near)); // next to you (get_dist <= 1)
            Assert.NotEmpty(chat.Html);

            chat.Html.Clear();
            var holder = Human(9, 5); // a held flashlight lights its holder's tile and the far object's tile alike
            var flashlight = Dm.@new<obj_item_flashlight>(TgToyRoom.T(9, 5));
            Assert.True(Dm.truthy(Waiting.NoSleep(holder.put_in_hands(flashlight))));
            Waiting.NoSleep(flashlight.toggle_light(holder));
            Waiting.NoSleep(human.run_examinate(far)); // get_dynamic_lumcount of (10, 5): the holder is next to it
            Assert.NotEmpty(chat.Html);

            chat.Html.Clear();
            Waiting.NoSleep(flashlight.toggle_light(holder));
            var night = Human(3, 8);
            night.lighting_cutoff = LIGHTING_CUTOFF_HIGH;
            Waiting.NoSleep(night.run_examinate(far));
            Assert.NotEmpty(chat.Html);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void SleepingInTheDarkRestsBetterThanInLight()
    {
        var (room, chat) = DarkRoom();
        room.In(() =>
        {
            float Heal(int x, int y)
            {
                var sleeper = Human(x, y);
                // the same great sanity for both (the moods since gaps MS1): its level sets the base sleep quality, 0.2 for a great
                // one (debuffs.dm:255-267), and stamina heals at least 0.4 * HEALING_SLEEP_DEFAULT * seconds (debuffs.dm:320), so
                // a lower base would hide the dark's +0.1 under that floor
                Waiting.NoSleep(sleeper.mob_mood.set_sanity(140, maximum: SANITY_MAXIMUM));
                Assert.Equal((float)SANITY_LEVEL_GREAT, sleeper.mob_mood.sanity_level);
                Waiting.NoSleep(sleeper.adjust_stamina_loss(20, updating_stamina: FALSE));
                float before = Dm.num(sleeper.get_stamina_loss());
                var sleeping = (datum_status_effect_incapacitating_sleeping)Waiting.NoSleep(sleeper.apply_status_effect(typeof(datum_status_effect_incapacitating_sleeping)));
                Assert.NotNull(sleeping);
                Waiting.NoSleep(sleeping.tick(2));
                return before - Dm.num(sleeper.get_stamina_loss());
            }

            float dark = Heal(3, 3);
            Dm.@new<test_lum_lamp>(TgToyRoom.T(12, 8));
            Fire();
            float lit = Heal(12, 8);
            // debuffs.dm:271-274: +0.1 sleep quality in the dark on a great sanity's 0.2: 0.4 * quality * seconds
            Assert.Equal(0.4f * 0.2f * 2, lit, 1e-3f);
            Assert.Equal(0.4f * 0.3f * 2, dark, 1e-3f);
        });
        Assert.Empty(room.RuntimeText);
    }
}
