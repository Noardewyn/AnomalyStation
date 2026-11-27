using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Fishing through the real server: a joined player casts a rod at a toilet, strikes at the bite, plays tg's minigame on the HUD
/// with the mouse held and let go (the bait rises while the button is down) and lands the fish; a joined player opens the
/// fish-porter's portal with a hand and fishes in it.
/// </summary>
public class FishingGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const float BITING_PHASE = 2;
    private const float MINIGAME_PHASE = 3;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action f)
    {
        using (w.World.Enter())
            f();
    }

    private static ClickTarget Entity(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static readonly ShotAim NoAim = new(0, 0, 0, 0, 0);

    private static (RealHeadlessWorld.Player P, mob_living_carbon_human Me, obj_item_fishing_rod Rod) Fisher(RealHeadlessWorld w)
    {
        RealHeadlessWorld.Player p = w.Join("Angler");
        var me = (mob_living_carbon_human)w.MobOf(p);
        w.Run(10);
        var rod = In(w, () =>
        {
            var r = Dm.@new<obj_item_fishing_rod>(get_turf(me));
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(r))));
            return r;
        });
        w.Run(5);
        return (p, me, rod);
    }

    /// <summary>An open tile two steps from the mob in some direction, with nothing dense on it.</summary>
    private static turf SpotNear(mob me) => Dm.each(range(2, me)).OfType<turf>()
        .Where(t => t is turf_open && !Dm.truthy(t.density) && get_dist(t, me) == 2 && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density)))
        .First();

    private static datum_fishing_challenge WaitChallenge(RealHeadlessWorld w, mob me)
    {
        datum_fishing_challenge c = null;
        for (int i = 0; i < 4 * Second && c == null; i++)
        {
            w.Run(1);
            c = In(w, () => GLOB.fishing_challenges_by_user[me] as datum_fishing_challenge);
        }
        Assert.True(c != null, "the cast started no challenge");
        return c;
    }

    private static void WaitBite(RealHeadlessWorld w, datum_fishing_challenge c)
    {
        bool biting = false;
        for (int i = 0; i < 40 * Second && !biting; i++)
        {
            w.Run(1);
            biting = In(w, () => c.phase == BITING_PHASE);
        }
        Assert.True(biting, "nothing bit");
    }

    [Fact]
    public void A_player_fishes_a_toilet_and_wins_the_minigame()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14751, TimeoutSteps = 9000 });
        var (p, me, rod) = Fisher(w);
        var toilet = In(w, () =>
        {
            var t = Dm.@new<obj_structure_toilet>(SpotNear(me));
            Waiting.NoSleep(t.AddComponent(typeof(datum_component_fishing_spot), GLOB.preset_fish_sources[typeof(datum_fish_source_toilet)]));
            return t;
        });
        w.Run(Second);

        Assert.True(p.Session.Click(Entity(toilet), ClickButton.Left, ClickModifiers.None));
        datum_fishing_challenge challenge = WaitChallenge(w, me);
        Assert.True(In(w, () => rod.fishing_line != null), "no line was cast");
        WaitBite(w, challenge);
        Assert.True(p.Session.Click(Entity(toilet), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Equal(MINIGAME_PHASE, In(w, () => challenge.phase));
        In(w, () => challenge.reward_path = typeof(obj_item_fish_sludgefish));

        // the minigame's HUD reaches the player's screen
        long hudId = In(w, () => challenge.fishing_hud.dm_id);
        Assert.Contains(hudId, p.Session.Replica.Screen.Keys);
        // its picture holds the background, the rod's frame, the bait with its bars, the fish and the completion bar
        string fishState = In(w, () => challenge.fishing_hud.hud_fish.icon_state);
        var states = new List<string>();
        void Walk(TableRef r, int depth)
        {
            if (depth > 6 || !p.Session.Replica.TryGetAppearance(r, out AppearanceEntry e))
                return;
            states.Add(p.Session.Replica.ResolveString(e.IconState));
            foreach (TableRef o in e.Underlays)
                Walk(o, depth + 1);
            foreach (TableRef o in e.Overlays)
                Walk(o, depth + 1);
        }
        Walk(p.Session.Replica.Screen[hudId].Appearance, 0);
        output.WriteLine("the HUD's states: " + string.Join(", ", states.Distinct()));
        Assert.Contains("bait_bottom", states);
        Assert.Contains("bait_bar", states);
        Assert.Contains(fishState, states);
        Assert.Contains("completion_overlay", states);

        // play it with the mouse: hold the button while the bait is under the fish, let go while it is over it
        bool held = false;
        bool over = false;
        for (int i = 0; i < 90 * Second && !over; i++)
        {
            (float fish, float bait, float baitHeight, float fishHeight, bool done) = In(w, () => QDELETED(challenge)
                ? (0f, 0f, 0f, 0f, true)
                : (challenge.fish_position, challenge.bait_position, challenge.bait_height, challenge.fish_height, false));
            if (done)
                break;
            bool want = bait + baitHeight / 2 < fish + fishHeight / 2;
            if (want && !held)
                p.Session.MouseDown(Entity(toilet), ClickButton.Left, ClickModifiers.None, NoAim, w.Now);
            else if (!want && held)
                p.Session.MouseUp(Entity(toilet), ClickModifiers.None, NoAim);
            held = want;
            w.Run(1);
            over = In(w, () => QDELETED(challenge));
        }
        if (held)
            p.Session.MouseUp(Entity(toilet), ClickModifiers.None, NoAim);
        Assert.True(over, "the minigame never ended");
        w.Run(Second);
        bool caught = In(w, () => Dm.each_cast<atom>(me.contents).OfType<obj_item_fish_sludgefish>().Any()
            || Dm.each_cast<atom>(range(2, me)).OfType<obj_item_fish_sludgefish>().Any());
        Assert.True(caught, "the sludgefish was not landed");
        Assert.DoesNotContain(hudId, p.Session.Replica.Screen.Keys);
        output.WriteLine($"runtimes {w.World.RuntimeErrorCount}");
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void A_player_opens_the_fish_porter_and_fishes_in_its_portal()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14752, TimeoutSteps = 9000 });
        var (p, me, rod) = Fisher(w);
        var porter = In(w, () =>
        {
            turf spot = Dm.each(range(1, me)).OfType<turf>().First(t => t is turf_open && t != me.loc && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density)));
            return Dm.@new<obj_machinery_fishing_portal_generator>(spot);
        });
        w.Run(Second);
        // an empty hand opens the only dimension it knows, the aquarium's
        In(w, () => { me.swap_hand(); });
        w.Run(2);
        Assert.True(p.Session.Click(Entity(porter), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.True(In(w, () => porter.active?.fish_source is datum_fish_source_portal), "the portal did not open");
        Assert.True(In(w, () => HAS_TRAIT(porter, TRAIT_FISHING_SPOT)));
        In(w, () => { me.swap_hand(); });
        w.Run(2);
        Assert.True(p.Session.Click(Entity(porter), ClickButton.Left, ClickModifiers.None));
        datum_fishing_challenge challenge = WaitChallenge(w, me);
        Assert.Same(porter, In(w, () => challenge.location));
        output.WriteLine($"runtimes {w.World.RuntimeErrorCount}");
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
