using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Bots;
using Anomaly.Tools.Bots;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Protocol version 11 (M4v slice 4, D143): a screen object's animate() chain and flick reach its owner's private block. tg's
/// clear_fullscreen fades the fullscreen out over a second before it deletes it (fullscreen.dm:22-40); the resist button flicks its
/// "_on" state when clicked (screen_objects.dm:582).
/// </summary>
public class ScreenLookReplicationTests
{
    // The alpha the HUD draws screen entry id with at the tick, as HudLayer does: the chain's over the opaque picture while one plays, else
    // the appearance's own.
    private static double HudLookAlpha(Bot bot, long id, double tick)
    {
        ScreenEntry entry = bot.Replica.Screen[id];
        if (Anomaly.Client.Shared.HudLook.TryPlay(bot.Replica, entry, tick, out Anomaly.Client.Shared.Icons.AnimatedLook look))
            return look.Alpha;
        Assert.True(bot.Replica.TryGetAppearance(entry.Appearance, out AppearanceEntry own));
        return own.Alpha / 255.0;
    }

    [Fact]
    public void A_cleared_fullscreen_fades_out_on_its_owners_screen_and_then_leaves()
    {
        using var rig = new BotServerRig(strict: true);
        Bot bot = rig.AddBot("Viewer", BotBehaviour.Idle);
        rig.UntilJoined(bot);
        rig.Tick(4);
        var human = (mob_living_carbon_human)rig.MobOf("Viewer");
        long id;
        using (rig.World.Enter())
        {
            var screen = (atom_movable_screen_fullscreen)human.overlay_fullscreen("m4v_test", typeof(atom_movable_screen_fullscreen_flash));
            id = screen.dm_id;
        }
        rig.Tick(2);
        Assert.True(bot.Replica.Screen.TryGetValue(id, out ScreenEntry shown));
        Assert.Null(shown.Animation);

        using (rig.World.Enter())
            human.clear_fullscreen("m4v_test", 10);
        rig.Tick(2);
        Assert.True(bot.Replica.Screen.TryGetValue(id, out ScreenEntry fading));
        AnimationTrack fade = Assert.Single(fading.Animation.Tracks);
        AnimationStep step = Assert.Single(fade.Steps);
        Assert.Equal(AnimatedVar.Alpha, step.Vars.Mask);
        Assert.Equal(0f, step.Vars.Alpha);
        Assert.Equal(10f, step.Time);
        Assert.Equal(255f, fade.Start.Alpha);
        Assert.True(bot.Replica.TryGetAppearance(fading.Appearance, out AppearanceEntry look));
        Assert.Equal(0, look.Alpha); // the appearance holds the chain's end; the HUD draws the fade over it

        // The live check (2026-10-14): the fade must show. The entry stays in the private block with its chain for the whole fade, and the
        // HUD's look (HudLook.TryPlay, what HudLayer draws) goes down from opaque to transparent over the second.
        long start = fade.StartTick;
        double before = HudLookAlpha(bot, id, start - 1);
        Assert.Equal(1.0, before, 3); // not started: the start value (the live check: a one-frame blink here), not the end's 0
        double last = 1.0;
        for (int i = 0; i < 4; i++)
        {
            rig.Tick(4);
            Assert.True(bot.Replica.Screen.TryGetValue(id, out ScreenEntry now), "the fullscreen stays on the screen while it fades");
            Assert.Equal(fading.Animation, now.Animation);
            double alpha = HudLookAlpha(bot, id, start + 4 * (i + 1));
            Assert.InRange(alpha, 0.01, last - 0.05); // fading, never at once
            last = alpha;
        }

        rig.Tick(30); // the fade's end and the deletion after it (clear_fullscreen_after_animate, a second after the clear)
        Assert.False(bot.Replica.Screen.ContainsKey(id));
        Assert.Equal(0, rig.World.RuntimeErrorCount);
    }

    [Fact]
    public void A_clicked_resist_button_flicks_its_on_state_on_its_owners_screen()
    {
        using var rig = new BotServerRig(strict: true);
        Bot bot = rig.AddBot("Clicker", BotBehaviour.Idle);
        rig.UntilJoined(bot);
        rig.Tick(4);
        var human = (mob_living_carbon_human)rig.MobOf("Clicker");
        atom_movable_screen_resist button;
        using (rig.World.Enter())
        {
            button = Dm.@new<atom_movable_screen_resist>();
            button.screen_loc = "EAST-1,SOUTH";
            Dm.append(human.client.screen, button);
        }
        rig.Tick(2);
        Assert.True(bot.Replica.Screen.TryGetValue(button.dm_id, out ScreenEntry before));
        Assert.True(before.FlickAppearance.IsNone);

        using (rig.World.Enter())
            Waiting.NoSleep(button.Click()); // no living usr: the flick alone (resisting is M9)
        rig.Tick(2);
        Assert.True(bot.Replica.Screen.TryGetValue(button.dm_id, out ScreenEntry flicked));
        Assert.False(flicked.FlickAppearance.IsNone);
        Assert.True(bot.Replica.TryGetAppearance(flicked.FlickAppearance, out AppearanceEntry look));
        Assert.Equal("act_resist_on", bot.Replica.ResolveString(look.IconState));
        Assert.True(bot.Replica.TryGetAppearance(flicked.Appearance, out AppearanceEntry own));
        Assert.Equal("act_resist", bot.Replica.ResolveString(own.IconState)); // the flick is no change of the button's own picture
        Assert.InRange(flicked.FlickTick, 1, bot.Replica.Tick);
        Assert.Equal(0, rig.World.RuntimeErrorCount);
    }
}
