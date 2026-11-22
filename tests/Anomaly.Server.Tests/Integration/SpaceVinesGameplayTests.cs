using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
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
/// Gaps package E4b (D327): the space vines through the real server and a client's own stack, in the server's room. A botanist with
/// a pack of kudzu seeds throws them on the floor (use in hand: tg's five seconds of throwing): the kudzu takes root under him and
/// his client sees the vine (tg's "Light" picture of icons/mob/spacevines.dmi). The vines spread over the room's floor and grow:
/// the client sees the thicker stages ("Med", "Hvy"). A kitchen knife cuts a vine beside him in a stroke (four times its force, and
/// the vines invert its demolition modifier), and his client sees it go.
/// </summary>
public class SpaceVinesGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static List<string> Seen(ReplicaWorld replica, atom a)
    {
        Assert.True(replica.TryGet(a.dm_id, out ReplicaEntity seen), "the client has no entity for the vine");
        return CryoGameplayTests.States(replica, seen.State.Appearance);
    }

    // the client shows the vine's current stage within a second (the server's icon state of the moment, as it may grow meanwhile)
    private static bool ShowsItsStage(RealHeadlessWorld w, ReplicaWorld replica, obj_structure_spacevine vine)
    {
        for (int tick = 0; tick < Second; tick++)
        {
            w.Run(1);
            if (replica.TryGet(vine.dm_id, out _) && Seen(replica, vine).Contains(In(w, () => vine.icon_state)))
                return true;
        }
        return false;
    }

    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    [Fact]
    public void A_botanist_plants_kudzu_sees_the_vines_spread_and_grow_and_cuts_one_with_a_knife()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 44, TimeoutSteps = 20000 });
        RealHeadlessWorld.Player p = w.Join("Botanist");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var chat = new List<string>();
        p.Client.ServerTextReceived += t => chat.Add(t.ToString());
        w.Run(Second);

        // 1. the seed pack in hand, used in hand: five seconds of throwing seeds, then the kudzu is planted under him
        var seeds = In(w, () =>
        {
            var s = Dm.@new<obj_item_seeds_kudzu>(me.loc);
            Waiting.NoSleep(me.put_in_active_hand(s));
            return s;
        });
        w.Run(Second);
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(7 * Second);
        Assert.Contains(chat, c => c.Contains("You plant the kudzu. You monster.", StringComparison.Ordinal));
        Assert.True(In(w, () => QDELETED(seeds)));
        var first = In(w, () => Dm.locate<obj_structure_spacevine>(me.loc));
        Assert.NotNull(first);
        var master = In(w, () => first.master);
        // tg's first stage ("Light1"), or the next if it has grown since
        Assert.True(ShowsItsStage(w, replica, first), "the client shows the vine's stage");
        Assert.Matches("^(Light1|Med[123])$", In(w, () => first.icon_state));

        // the client composes the vine's picture from tg's icon (when the converted icons are here)
        string iconRoot = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (iconRoot != null)
        {
            var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(iconRoot)));
            Assert.True(replica.TryGet(first.dm_id, out ReplicaEntity seen));
            Assert.True(compositor.TryCompose(new VisInsets(new Tables(replica)), seen.State.Appearance, ByondDir.South, 0, out ComposedRaster picture),
                string.Join("; ", compositor.Library.Problems));
            Assert.True(picture.Image.Width > 0);
        }

        // 2. the vines spread over the floor and grow: the client sees a thicker stage
        obj_structure_spacevine grown = null;
        for (int sec = 0; sec < 90 && grown == null; sec++)
        {
            w.Run(Second);
            grown = In(w, () => Dm.each<obj_structure_spacevine>(master.vines).FirstOrDefault(v => v.growth_stage >= 1 && v != first));
        }
        int count = In(w, () => (int)master.vines.len);
        output.WriteLine($"{count} vines");
        Assert.True(count > 1, "the vines spread");
        Assert.NotNull(grown);
        Assert.True(ShowsItsStage(w, replica, grown), "the client shows the grown vine's stage");
        Assert.Contains(Seen(replica, grown), s => s != null && (s.StartsWith("Med", StringComparison.Ordinal) || s.StartsWith("Hvy", StringComparison.Ordinal)));

        // 3. a kitchen knife: a vine beside him falls to a stroke (a sharp item cuts at four times its force), and the client sees it go
        var knife = In(w, () =>
        {
            Waiting.NoSleep(me.drop_all_held_items());
            if (me.buckled != null)
                Waiting.NoSleep(((atom_movable)me.buckled).unbuckle_mob(me, force: TRUE));
            var k = Dm.@new<obj_item_knife_kitchen>(me.loc);
            Waiting.NoSleep(me.put_in_active_hand(k));
            return k;
        });
        var target = In(w, () => Dm.each<obj_structure_spacevine>(master.vines)
            .FirstOrDefault(v => v.loc != me.loc && get_dist(v, me) == 1 && Dm.truthy(Waiting.NoSleep(me.Adjacent(v)))));
        Assert.NotNull(target);
        w.Run(Second);
        for (int stroke = 0; stroke < 4 && !In(w, () => QDELETED(target)); stroke++)
        {
            Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
        }
        Assert.True(In(w, () => QDELETED(target)), "the vine is cut");
        w.Run(Second);
        Assert.False(replica.TryGet(target.dm_id, out _), "the client no longer draws the cut vine");
    }
}
