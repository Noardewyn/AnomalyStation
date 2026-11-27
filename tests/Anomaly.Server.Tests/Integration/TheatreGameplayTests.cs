using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The mime through the real server with a headless client: a joined mime reads the advanced guide to the invisible blockade and
/// clicks its action button: three tiles of invisible wall stand in front of her, dense and fully transparent in her replica, and
/// her emote reaches her chat.
/// </summary>
public class TheatreGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [Fact]
    public void A_joined_mime_raises_the_invisible_blockade()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14141, TimeoutSteps = 6000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        var heard = new List<string>();
        alice.Client.ServerTextReceived += t => heard.Add(t.Html ?? "");
        w.Run(Second);
        using (w.World.Enter())
        {
            Waiting.Detach(SSjob.equip_rank(me, SSjob.get_job_type(typeof(datum_job_mime)) as datum_job, me.client));
        }
        w.Run(Second / 2);
        datum_action_cooldown_spell_forcewall_mime blockade = In(w, () =>
        {
            var guide = Dm.@new<obj_item_book_granter_action_spell_mime_mimery_blockade>(me.loc);
            Waiting.Detach(guide.on_reading_finished(me));
            Waiting.NoSleep(me.setDir(NORTH));
            return Dm.locate<datum_action_cooldown_spell_forcewall_mime>(me.actions);
        });
        Assert.NotNull(blockade);
        w.Run(Second / 2);

        // her action button, as her HUD shows it
        long button = In(w, () => (blockade.viewers[me.hud_used] as atom_movable_screen_movable_action_button).dm_id);
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Screen, button, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        var walls = In(w, () => new[] { me.loc, get_step(me, EAST), get_step(me, WEST) }
            .Select(t => Dm.locate<obj_effect_forcefield_mime_advanced>(t as atom)).ToList());
        Assert.All(walls, Assert.NotNull);
        foreach (var wall in walls)
        {
            Assert.True(alice.Session.Replica.TryGet(In(w, () => wall.dm_id), out ReplicaEntity seen), "the wall is in her replica");
            Assert.Equal(0, In(w, () => wall.alpha));
        }
        Assert.Contains(heard, h => h.Contains("You form a blockade in front of yourself.", StringComparison.Ordinal));
        output.WriteLine("server errors:\n" + string.Join("\n", w.Err.ToString().Split('\n').TakeLast(20)));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
