using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps package C3 (the bots) on MetaStation through the real server and a client's own stack. Meta's 11 bots are live and run their trees: the patrolling ones walk with no
/// new runtimes and the client's replica follows them. A crewman clicks Scrubs, MD (tg's SimpleBot window), powers him on through the window and the client sees the picture
/// change; he hears the speech and sees the scrubbing picture and a medbot's healing one. The rough cost of the bots on an idle round.
/// </summary>
public class BotsGameplayTests(ITestOutputHelper output)
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

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static string Look(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == a.dm_id);
        Assert.True(replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
        return replica.ResolveString(look.IconState);
    }

    private static turf ClearNeighbour(atom a)
        => new[] { SOUTH, NORTH, EAST, WEST }.Select(d => get_step(a, d) as turf)
            .FirstOrDefault(t => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(c => Dm.truthy(c.density)) && Dm.truthy(Waiting.NoSleep(a.Adjacent(t))));

    [MetaServerFact]
    public void Metas_bots_are_live_and_move_and_a_crewman_works_a_bots_window_and_the_client_sees_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 97, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Crewman");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // tg's Meta: three cleanbots, an autopatrol medbot, three MULEs, a repairbot and three secbots
        List<mob_living_basic_bot> bots = In(w, () => Dm.each<mob_living_basic_bot>().Where(b => b.z == 1 || get_area(b) is area_station).ToList());
        output.WriteLine(string.Join("\n", In(w, () => bots.Select(b => $"{b.GetType().Name} '{Dm.str(b.name)}' at {b.x},{b.y},{b.z} {b.mode} status {((datum_ai_controller)b.ai_controller).ai_status}").ToList())));
        Assert.Equal(11, bots.Count);
        foreach ((Type type, int count) in new[] { (typeof(mob_living_basic_bot_cleanbot), 3), (typeof(mob_living_basic_bot_medbot), 1), (typeof(mob_living_basic_bot_mulebot), 3), (typeof(mob_living_basic_bot_repairbot), 1), (typeof(mob_living_basic_bot_secbot), 3) })
            Assert.Equal(count, bots.Count(b => type.IsInstanceOfType(b)));
        Assert.All(bots, b => Assert.False(InertSeam.IsInert(b), b.GetType().Name));
        Assert.Equal(3, In(w, () => bots.OfType<mob_living_basic_bot_secbot>().Count(s => HAS_TRAIT(s, TRAIT_COMMISSIONED) || s is mob_living_basic_bot_secbot_pingsky)));

        // the patrolling ones walk their beacons (their replica follows)
        var patrollers = bots.Where(b => b is mob_living_basic_bot_cleanbot_autopatrol or mob_living_basic_bot_medbot_autopatrol or mob_living_basic_bot_secbot_beepsky_officer).ToList();
        Assert.NotEmpty(patrollers);
        var starts = In(w, () => patrollers.ToDictionary(b => b, b => get_turf(b)));
        w.Run(90 * Second);
        foreach (mob_living_basic_bot bot in patrollers)
        {
            output.WriteLine(In(w, () => $"{bot.GetType().Name} went from {starts[bot].x},{starts[bot].y} to {bot.x},{bot.y}, mode {bot.mode}"));
            Assert.NotSame(starts[bot], In(w, () => get_turf(bot)));
        }
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);

        // Scrubs, MD starts powered off: tg's picture of an off cleanbot reaches the client
        mob_living_basic_bot_cleanbot_medbay scrubs = bots.OfType<mob_living_basic_bot_cleanbot_medbay>().Single();
        Do(w, () => Waiting.NoSleep(me.forceMove(ClearNeighbour(scrubs))));
        w.Run(Second);
        Assert.Equal("cleanbot0", Look(replica, scrubs));

        // an empty hand on him: tg's SimpleBot window, locked to a crewman without his access
        Assert.True(p.Session.Click(On(scrubs), ClickButton.Left, default));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("SimpleBot", window.Interface);
        UiNode content = Shown(windows, replica, window.Window);
        Assert.Contains(content.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == "Locked!");

        // unlocked: the settings and the cleaning controls; the power button turns him on and the client sees it
        Do(w, () => scrubs.bot_access_flags = Dm.bitand(scrubs.bot_access_flags, Dm.bitnot(BOT_COVER_LOCKED)));
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        Assert.Contains(content.Walk(), n => n.Key == "control:clean_blood");
        Assert.Contains(content.Walk(), n => n.Key == "power");
        content.Walk().First(n => n.Key == "power").OnClick();
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(Dm.bitand(scrubs.bot_mode_flags, BOT_MODE_ON))));
        Assert.NotEqual("cleanbot0", Look(replica, scrubs)); // on: cleanbot1, or cleanbot-c if Meta's random dirt lies beside him

        // D142: the scrubbing picture of a decal cleaned beside him reaches the client
        obj_effect_decal_cleanable_dirt dirt = null;
        Do(w, () => dirt = Dm.@new<obj_effect_decal_cleanable_dirt>(ClearNeighbour(scrubs)));
        bool scrubbing = false;
        for (int i = 0; i < 40 * Second && !In(w, () => QDELETED(dirt)); i++)
        {
            w.Run(1);
            scrubbing |= Look(replica, scrubs) == "cleanbot-c";
        }
        Assert.True(In(w, () => QDELETED(dirt)), "Scrubs cleaned the decal");
        Assert.True(scrubbing, "the client saw the scrubbing picture");

        // D142: a medbot's healing picture and its speech and sound reach the client
        mob_living_basic_bot_medbot medbot = bots.OfType<mob_living_basic_bot_medbot>().Single();
        Do(w, () =>
        {
            Waiting.NoSleep(medbot.forceMove(ClearNeighbour(me)));
            Waiting.NoSleep(me.adjust_brute_loss(40));
        });
        bool healing = false;
        float brute = In(w, () => Dm.num(me.get_brute_loss()));
        for (int i = 0; i < 40 * Second && !healing; i++)
        {
            w.Run(1);
            healing = Look(replica, medbot) == "medbot_generic_active";
        }
        Assert.True(healing, "the client saw the healing picture");
        w.Run(5 * Second);
        Assert.True(In(w, () => Dm.num(me.get_brute_loss())) < brute);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);

        // D142: a secbot's alert sound and its stun's flick reach the client when the crewman hits him
        var sounds = new List<SoundEvent>();
        replica.SoundsReceived += e => sounds.AddRange(e);
        mob_living_basic_bot_secbot_beepsky_officer beepsky = bots.OfType<mob_living_basic_bot_secbot_beepsky_officer>().Single();
        Do(w, () =>
        {
            Waiting.NoSleep(beepsky.forceMove(ClearNeighbour(me)));
            me.set_combat_mode(TRUE);
            Waiting.NoSleep(beepsky.attack_hand(me, Dm.list()));
        });
        bool flicked = false;
        for (int i = 0; i < 30 * Second && !flicked; i++)
        {
            w.Run(1);
            ReplicaEntity seen = replica.Entities.SingleOrDefault(e => e.Id == beepsky.dm_id);
            flicked = seen != null && CryoGameplayTests.States(replica, seen.State.FlickAppearance).Contains("secbot-c");
        }
        Assert.True(flicked, "the client saw the stun's flick");
        Assert.True(In(w, () => Dm.truthy(me.IsParalyzed())));
        Assert.Contains(sounds, e => e.File == "sound/items/weapons/egloves.ogg");
        w.Run(10 * Second); // the zipties
        Assert.NotNull(In(w, () => me.handcuffed));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);

        // the rough cost of Meta's bots: each one's plan (4 a second) and a run of the world with and without them
        Do(w, () =>
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 400; i++)
                foreach (mob_living_basic_bot bot in bots)
                    if (!QDELETED(bot))
                        ((datum_ai_controller)bot.ai_controller).SelectBehaviors(0.25f);
            output.WriteLine($"all of Meta's bots' SelectBehaviors: {clock.Elapsed.TotalMilliseconds * 1000 / 400:F0} us a pass (4 passes a second)");
        });
        var run = System.Diagnostics.Stopwatch.StartNew();
        w.Run(20 * Second);
        output.WriteLine($"with the bots: {run.Elapsed.TotalMilliseconds / (20 * Second):F2} ms a tick");
        Do(w, () =>
        {
            foreach (mob_living_basic_bot bot in bots)
                qdel(bot);
        });
        w.Run(Second);
        run.Restart();
        w.Run(20 * Second);
        output.WriteLine($"without them: {run.Elapsed.TotalMilliseconds / (20 * Second):F2} ms a tick");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void A_cargo_mule_on_meta_delivers_a_crate_between_the_stations_beacons()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 98, TimeoutSteps = 40000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        w.Join("Crewman");
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        // a MULE of the cargo bay, on its home beacon (tg's third mule, Leaping Rabbit, is mapped in maintenance with no home)
        mob_living_basic_bot_mulebot mule = In(w, () => Dm.each<mob_living_basic_bot_mulebot>().First(m => m.z == 1 && Dm.truthy(((datum_ai_controller)m.ai_controller).blackboard[BB_MULEBOT_HOME_BEACON])));
        List<string> tags = In(w, () => Dm.each_cast<object>(GLOB.deliverybeacontags).Select(t => t as string).ToList());
        output.WriteLine($"{tags.Count} delivery beacons, the mule's home is '{In(w, () => ((datum_ai_controller)mule.ai_controller).blackboard[BB_MULEBOT_HOME_BEACON])}'");
        Assert.NotEmpty(tags);
        // a crate straight on the mule's tile is the hand-loaded one, then the closest other delivery beacon
        obj_structure_closet_crate crate = null;
        string target = In(w, () =>
        {
            crate = Dm.@new<obj_structure_closet_crate>(get_turf(mule));
            Waiting.NoSleep(mule.load__proc(crate));
            obj_machinery_navbeacon closest = Dm.each_cast<obj_machinery_navbeacon>(GLOB.deliverybeacons).Where(b => b.z == mule.z && get_dist(b, mule) > 3).OrderBy(b => get_dist(b, mule)).First();
            mule.set_destination(closest.location);
            mule.start();
            return closest.location;
        });
        Assert.Same(crate, In(w, () => mule.load));
        output.WriteLine($"delivering to {target}, {In(w, () => mule.mode)}");
        for (int i = 0; i < 180 * Second && In(w, () => mule.load) != null; i++)
            w.Run(1);
        output.WriteLine(In(w, () =>
        {
            var brain = (datum_ai_controller)mule.ai_controller;
            DmList lines = Dm.list();
            ((datum_bt_node)brain.behavior_nodes[1]).append_full_tree_state(lines, "");
            return $"the mule at {mule.x},{mule.y} mode {mule.mode}, crate at {crate.x},{crate.y}, status {brain.ai_status}, target {(brain.blackboard[BB_MULEBOT_TRAVEL_TARGET] as atom)?.x},{(brain.blackboard[BB_MULEBOT_TRAVEL_TARGET] as atom)?.y}, access {length(brain.get_access())}, flags {mule.bot_mode_flags}\n" + lines.Join("\n");
        }));
        Assert.Null(In(w, () => mule.load));
        Assert.True(In(w, () => isturf(crate.loc)));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
