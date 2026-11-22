using Anomaly.Client.Shared;
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
/// Gaps package C5 on MetaStation through the real server and a client's own stack: Meta's 8 monkeys and Pun Pun are alive with their trees, wander, and reach the client with
/// tg's monkey body (limbs and tail), Pun Pun in his waiter's suit; the 4 cube boxes hold cubes, water makes one a monkey, the recycler works; no runtime. Prints the rough cost of
/// Meta's monkeys on an idle round, since they are full human bodies.
/// </summary>
public class MonkeysGameplayTests(ITestOutputHelper output)
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

    private const byte ByondDirSouth = 2;

    private sealed class ReplicaLooks(ReplicaWorld replica) : Anomaly.Client.Shared.Icons.IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    private static List<string> StatesOf(ReplicaWorld replica, atom a)
    {
        var icons = new List<string>();
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == a.dm_id);
        Assert.True(replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
        foreach (TableRef o in look.Overlays)
        {
            if (replica.TryGetAppearance(o, out AppearanceEntry over))
                icons.Add(replica.ResolveString(over.IconState));
        }
        return icons;
    }

    [MetaServerFact]
    public void Metas_monkeys_and_Pun_Pun_live_wander_and_the_client_sees_their_bodies()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 98, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Crewman");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        w.Run(Second);
        // Meta's boot reaches code of later milestones (strict: false)
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e =>
        {
            sink?.Invoke(e);
            // the rig's table of sound lengths (RigSoundLengths) lacks some of the rooms' ambience: not a monkey's
            if (!e.Message.Contains("rustg_sound_length", StringComparison.Ordinal) && !e.Message.Contains("play_ambience", StringComparison.Ordinal))
                errors.Add(e);
        };

        List<mob_living_carbon_human_species_monkey> monkeys = In(w, () => Dm.each<mob_living_carbon_human_species_monkey>(world).ToList());
        var pun = (mob_living_carbon_human_species_monkey_punpun)monkeys.Single(m => m is mob_living_carbon_human_species_monkey_punpun);
        Assert.Equal(9, monkeys.Count); // Meta's 8 and Pun Pun
        foreach (mob_living_carbon_human_species_monkey monkey in monkeys)
        {
            Assert.False(In(w, () => InertSeam.IsInert(monkey)));
            var brain = In(w, () => monkey.ai_controller as datum_ai_controller);
            Assert.NotNull(brain);
            Assert.Contains("monkey.bt.json", In(w, () => brain.behavior_tree_json as string));
            Assert.Equal(AI_STATUS_ON, In(w, () => brain.ai_status as string));
        }
        Assert.Same(pun, In(w, () => GLOB.the_one_and_only_punpun));
        Assert.IsType<obj_item_clothing_under_suit_waiter>(In(w, () => pun.w_uniform));
        Assert.Equal(4, In(w, () => Dm.each<obj_item_storage_box_monkeycubes>(world).Count()));
        Assert.Single(In(w, () => Dm.each<obj_machinery_monkey_recycler>(world).ToList()));
        Assert.True(In(w, () => Dm.each<obj_structure_desk_bell>(world).Count(b => !InertSeam.IsInert(b))) >= 6, "the bells live");

        // the client sees Pun Pun's monkey body: the limbs, the tail and the suit
        Do(w, () => Waiting.NoSleep(me.forceMove(get_step(pun, NORTH))));
        w.Run(Second * 2);
        List<string> icons = StatesOf(replica, pun);
        output.WriteLine("Pun Pun's overlays: " + string.Join(", ", icons));
        Assert.Contains("monkey_chest", icons);
        Assert.Contains("monkey_head", icons);
        Assert.Contains("m_tail_monkey_default_BEHIND", icons);
        Assert.Contains("waiter", icons);

        // the owner's report: he stayed the same picture from every side; an AI's mob has no capsule, it turns by its dir
        p.Session.Poses.BeginFrame(replica.Tick, 0.05);
        ReplicaEntity punSeen = replica.Entities.Single(e => e.Id == pun.dm_id);
        Assert.True(p.Session.Poses.TryGetPose(punSeen, out RenderPose punPose));
        Assert.Equal(Anomaly.Client.Shared.Icons.BillboardMath.YawOfDir(punPose.Dir), punPose.Yaw);
        Assert.Equal(Anomaly.Client.Shared.Icons.ByondDir.South, Anomaly.Client.Shared.Icons.BillboardMath.ChooseDir(Anomaly.Client.Shared.Icons.BillboardMath.YawOfDir(Anomaly.Client.Shared.Icons.ByondDir.South), 10, 10, 10, 5, 4));
        Assert.Equal(Anomaly.Client.Shared.Icons.ByondDir.North, Anomaly.Client.Shared.Icons.BillboardMath.ChooseDir(Anomaly.Client.Shared.Icons.BillboardMath.YawOfDir(Anomaly.Client.Shared.Icons.ByondDir.South), 10, 10, 10, 15, 4));

        // the owner's report: his head stood above the picture's top (tg squashes a monkey by 7 pixels), and he is drawn from all four sides
        string iconRoot = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (iconRoot != null)
        {
            var compositor = new Anomaly.Client.Shared.Icons.AppearanceCompositor(new Anomaly.Client.Shared.Icons.IconLibrary(new Anomaly.Client.Shared.Icons.DirectoryIconFiles(iconRoot)));
            var tables = new ReplicaLooks(replica);
            ReplicaEntity seen = replica.Entities.Single(e => e.Id == pun.dm_id);
            Assert.Equal(4, compositor.Directions(tables, seen.State.Appearance));
            Assert.True(compositor.TryCompose(tables, seen.State.Appearance, ByondDirSouth, 0, out Anomaly.Client.Shared.Icons.ComposedRaster front));
            for (int x = 0; x < front.Image.Width; x++)
                Assert.Equal(0u, front.Image.Get(x, 0) & 255);
        }

        // they wander on the whole map (an empty round: tg's tree runs on the station)
        var starts = monkeys.ToDictionary(m => m, m => In(w, () => (m.x, m.y)));
        w.Run(Second * 60);
        List<string> movers = monkeys.Where(m => In(w, () => (m.x, m.y)) != starts[m]).Select(m => In(w, () => m.name as string)).ToList();
        output.WriteLine("moved in a minute: " + string.Join(", ", movers));
        Assert.True(movers.Count >= 5, string.Join(", ", movers));

        // water makes a monkey of a cube, and the replica sees it
        obj_item_food_monkeycube cube = null;
        Do(w, () =>
        {
            cube = Dm.@new<obj_item_food_monkeycube>(get_turf(me));
            Waiting.NoSleep(Dm.@new<datum_reagent_water>().expose_obj(cube, 5f));
        });
        w.Run(Second * 2);
        Assert.True(In(w, () => QDELETED(cube)));
        var born = In(w, () => Dm.each<mob_living_carbon_human_species_monkey>(world).Single(m => !monkeys.Contains(m)));
        Assert.Contains(replica.Entities, e => e.Id == born.dm_id);

        // the rough cost of all of Meta's monkeys on an idle round: their plans (4 a second) and Life (1 every 2 seconds)
        monkeys.Add(born);
        Do(w, () =>
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 400; i++)
                foreach (mob_living monkey in monkeys)
                    (monkey.ai_controller as datum_ai_controller).SelectBehaviors(0.25f);
            double plans = clock.Elapsed.TotalMilliseconds * 1000 / 100; // 400 rounds of plans are 100 seconds
            clock.Restart();
            for (int i = 0; i < 50; i++)
                foreach (mob_living monkey in monkeys)
                    monkey.Life(SSMOBS_DT());
            double life = clock.Elapsed.TotalMilliseconds * 1000 / 100; // 50 rounds of Life are 100 seconds
            output.WriteLine($"Meta's {monkeys.Count} monkeys: SelectBehaviors {plans:F1} us, Life {life:F1} us per second of round ({(plans + life) / monkeys.Count:F1} us each)");
        });
        Assert.True(errors.Count == 0, string.Join("\n----\n", errors.Select(e => e.ToString())));
        w.World.RuntimeErrorSink = sink;
    }
}
