using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Bots;
using Anomaly.Tools.Bots;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps package MS1 (D333, D339) through the real server: a joined player's mood face (tg's /atom/movable/screen/mood,
/// screen_objects.dm:1045) reaches the client in ui_mood with tg's mood icon state and the sanity colour, and changes on the client as
/// the player's mood and sanity change on the server (mood.dm:308: a good mood's face, an insane sanity's red, a special moodlet's face).
/// </summary>
public class MoodHudGameplayTests
{
    private static (string State, uint Rgba, string Loc) FaceOf(Bot bot, long id)
    {
        Assert.True(bot.Replica.Screen.TryGetValue(id, out ScreenEntry entry), "the mood face reached the client");
        Assert.True(bot.Replica.TryGetAppearance(entry.Appearance, out AppearanceEntry look));
        return (bot.Replica.ResolveString(look.IconState), look.Rgba, entry.ScreenLoc);
    }

    [Fact]
    public void AJoinedPlayersMoodFaceFollowsHisMoodAndSanityOnTheClient()
    {
        using var rig = new BotServerRig(strict: true);
        Bot bot = rig.AddBot("Moody", BotBehaviour.Idle);
        rig.UntilJoined(bot);
        rig.Tick(10);

        var human = (mob_living_carbon_human)rig.MobOf("Moody");
        long face;
        using (rig.World.Enter())
        {
            Assert.NotNull(human.mob_mood);
            var screen = human.hud_used.screen_objects[Defines.HUD_MOB_MOOD] as atom_movable_screen_mood;
            Assert.NotNull(screen);
            face = screen.dm_id;
            Waiting.NoSleep(human.set_nutrition(300)); // no hunger moodlet
        }
        rig.Tick(4);
        var start = FaceOf(bot, face);
        Assert.Equal(Defines.ui_mood, start.Loc);
        Assert.Equal("mood5", start.State);

        // a good mood: the happy face (+8, MOOD_LEVEL_HAPPY2)
        using (rig.World.Enter())
            Waiting.NoSleep(human.add_mood_event("test", typeof(datum_mood_event_wellfed)));
        rig.Tick(4);
        Assert.Equal("mood7", FaceOf(bot, face).State);

        // an insane sanity: tg's red (#f15d36)
        using (rig.World.Enter())
            Waiting.NoSleep(human.mob_mood.set_sanity(5, @override: Dm.TRUE));
        rig.Tick(4);
        Assert.Equal(0xF15D36FFu, FaceOf(bot, face).Rgba);

        // a special moodlet replaces the face (the happiness drug's overdose, drug_events.dm)
        using (rig.World.Enter())
            Waiting.NoSleep(human.add_mood_event("test", typeof(datum_mood_event_happiness_drug_good_od)));
        rig.Tick(4);
        Assert.Equal("mood_happiness_good", FaceOf(bot, face).State);
        Assert.Equal(0, rig.World.RuntimeErrorCount);
    }
}
