using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ui-overhaul.md section 12, slice 4C-1 (DV696): nothing the other container adds to a live run (its listing, read outside the world; its moves,
/// which are logged commands) changes the world beyond what a replay of the log does. Alice has a box open for many ticks and moves a pen out of it;
/// with tg's elastic arms (reach 2, reach.dm:46-52) the box is two tiles away, so the move's reach test walks tg's probe object (RangedReachCheck);
/// without them it is beside her. The live run's state hashes, written every few ticks, are the replay's.
/// </summary>
public class StoragesOtherReplayTests
{
    private const int HashEvery = 5;

    private static ServerConfig Config() => new()
    {
        Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f, HashEvery = HashEvery,
    };

    // The world's own setup at tick `at`, live and in the replay alike (a world phase, after the command phase): the mutation, the box with a pen at
    // `distance` tiles from Alice on open floor (and open floor between), and tg's opening of it.
    private static void Setup(World world, long at, Func<long> alice, bool arms, int distance, Action<obj_item, obj_item> made)
    {
        obj_item opened = null;
        world.AddPhase("test setup", w =>
        {
            if (w.Tick != at || !w.Objects.TryGet(alice(), out object o) || o is not mob_living_carbon_human her)
                return;
            static bool Open(turf t) => t is turf_open && t is not turf_open_space && !Dm.truthy(t.density);
            turf spot = null;
            foreach (int dir in new[] { (int)EAST, (int)WEST, (int)NORTH, (int)SOUTH })
            {
                turf one = get_step(her, dir), two = one == null ? null : get_step(one, dir);
                if (one != null && Open(one) && (distance == 1 || (two != null && Open(two))))
                {
                    spot = distance == 1 ? one : two;
                    break;
                }
            }
            Assert.NotNull(spot);
            if (arms)
                Waiting.Detach(her.dna.add_mutation(typeof(datum_mutation_elastic_arms), MUTATION_SOURCE_ACTIVATED));
            var box = Dm.@new<obj_item_storage_box>(spot);
            var pen = Dm.@new<obj_item_pen>(box);
            opened = box;
            made(box, pen);
        });
        world.AddPhase("test open", w =>
        {
            if (w.Tick != at + 2 || !w.Objects.TryGet(alice(), out object o) || o is not mob_living_carbon_human her)
                return;
            if (opened != null)
                Waiting.Detach(opened.atom_storage.open_storage(her));
        });
    }

    private static List<string> Hashes(string output) => output.Split('\n').Where(l => l.StartsWith("hash tick=", StringComparison.Ordinal)).Select(l => l.Trim()).ToList();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_run_with_a_box_open_and_a_move_from_it_replays_to_the_same_state_hashes(bool arms)
    {
        int distance = arms ? 2 : 1;
        CommandLog log;
        ulong hash;
        long end, at;
        List<string> liveHashes;
        using (var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 11, TimeoutSteps = 4000 }, Config()))
        {
            RealHeadlessWorld.Player alice = w.Join("Alice");
            w.Run(10);
            w.Host.EnqueueConsoleLine("teleport Alice 6 6"); // a command, so the replay does it too
            w.Run(10);
            at = w.Host.Tick + 5;
            obj_item box = null, pen = null;
            Setup(w.World, at, () => w.Host.AvatarOf("Alice"), arms, distance, (b, p) => (box, pen) = (b, p));
            w.Run(20);
            Assert.NotNull(box);
            var her = (mob_living_carbon_human)w.MobOf(alice);
            using (w.World.Enter())
            {
                Assert.Equal(arms ? 2f : 1f, her.reach_length);
                Assert.Equal(distance, (int)Dm.num(get_dist(her, box)));
                Assert.Same(box.atom_storage, her.active_storage);
            }
            Assert.Equal(box.dm_id, alice.Session.Storages.Storages.LastOrDefault(s => s.Place == StoragePlace.Other)?.Entity); // listed by tg's reach
            w.Run(40); // forty ticks with it open: a reach read every tick
            int active;
            using (w.World.Enter())
                active = (int)Dm.num(her.active_hand_index);
            Assert.True(alice.Session.InventoryMove(pen.dm_id, InventoryTarget.Hand, 0, active));
            w.Run(10);
            using (w.World.Enter())
                Assert.True(her.get_item_for_held_index(active) == pen, "the pen moved out of the box into her hand");
            w.Host.RequestStop();
            for (int i = 0; i < 10 && !w.Host.Finished; i++)
                w.Run(1);
            Assert.True(w.Host.Finished);
            (hash, end) = (w.Host.FinalHash, w.Host.Tick);
            liveHashes = Hashes(w.Out.ToString());
            log = CommandLog.Parse(w.Host.Log.ToText(), out string error);
            Assert.Null(error);
        }
        Assert.True(liveHashes.Count > 10, "hash lines: " + liveHashes.Count);

        var output = new System.IO.StringWriter();
        using var replay = new ServerHost(log.ReplayConfig(Config()), replay: log, output: output, strictNotTranslated: true);
        SoundLengths.Install(replay.World, RigSoundLengths.Lengths); // the live world's table (RealHeadlessWorld)
        Setup(replay.World, at, () => replay.AvatarOf("Alice"), arms, distance, (_, _) => { });
        RunResult result = replay.Run(FastForwardPacer.Instance);
        Assert.Equal(end, result.Tick);
        List<string> replayHashes = Hashes(output.ToString());
        for (int i = 0; i < Math.Min(liveHashes.Count, replayHashes.Count); i++)
            Assert.True(liveHashes[i] == replayHashes[i], "the first difference: live " + liveHashes[i] + " | replay " + replayHashes[i]);
        Assert.Equal(liveHashes.Count, replayHashes.Count);
        Assert.Equal(hash, result.FinalHash);
    }
}
