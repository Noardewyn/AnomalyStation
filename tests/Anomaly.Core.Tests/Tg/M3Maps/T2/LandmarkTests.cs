using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

/// <summary>
/// M3 T2: the landmarks of a map load against tg's DM (game/objects/effects/landmarks.dm; INITIALIZE_IMMEDIATE,
/// __DEFINES/subsystems.dm:96) and DV26's late-join fallback (docs/ai/DEVIATIONS.md, MapSeam.LatejoinSpawns). A landmark
/// initialises inside its own New, so the GLOB lists fill in creation order: the reader's order is the columns left to right
/// (x ascending), each column from its top row down (y descending), a tile's atoms in model order. Fixtures: landmarks (6 x 4),
/// nolatejoin (4 x 3).
/// </summary>
public class LandmarkTests
{
    private static (float X, float Y) At(atom a) => (((turf)a.loc).x, ((turf)a.loc).y);

    [Fact]
    public void The_start_landmarks_fill_the_global_list_in_creation_order()
    {
        World world = T2Maps.Boot("landmarks", out TgTestWorld.Log log);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            // landmarks.dm:44-50: GLOB.start_landmarks_list += src, in each landmark's Initialize
            var starts = GLOB.start_landmarks_list.Items().Cast<obj_effect_landmark_start>().ToList();
            Assert.Equal(new[] { (1f, 3f), (1f, 1f), (2f, 4f), (3f, 1f), (4f, 2f), (5f, 1f) }, starts.Select(At).ToArray());
            Assert.Equal(new[] { "Janitor", "Assistant", "Assistant", "Assistant", "Clown", "start" }, starts.Select(s => s.name).ToArray());
            Assert.IsType<obj_effect_landmark_start_janitor>(starts[0]);
            Assert.IsType<obj_effect_landmark_start_assistant_override>(starts[2]);
            // the base landmark list holds the start landmarks and the plain one, in the same order; the late-join landmarks
            // were in it while they initialised and left it when their QDEL hint ran Destroy (landmarks.dm:19-26, :345-348)
            var all = GLOB.landmarks_list.Items().Cast<obj_effect_landmark>().ToList();
            Assert.Equal(new[] { (1f, 3f), (1f, 1f), (2f, 4f), (3f, 4f), (3f, 1f), (4f, 2f), (5f, 1f) }, all.Select(At).ToArray());
            Assert.DoesNotContain(all, l => l is obj_effect_landmark_latejoin);
            Assert.IsType<obj_effect_landmark>(all[3]);
            Assert.Equal("marker", all[3].name); // the map's name edit reached the landmark before its Initialize
        }
    }

    [Fact]
    public void A_start_landmark_gets_its_tag_from_its_name_and_a_job_spawn_override_registers_itself()
    {
        World world = T2Maps.Boot("landmarks");
        using (world.Enter())
        {
            var starts = GLOB.start_landmarks_list.Items().Cast<obj_effect_landmark_start>().ToList();
            // landmarks.dm:49-50: `if(name != "start") tag = "start*[name]"`; the Clown's name came from the map
            Assert.Equal("start*Janitor", starts[0].tag);
            Assert.Equal("start*Clown", starts[4].tag);
            Assert.Null(starts[5].tag); // the plain "start"
            Assert.Same(starts[4], Dm.locate_ref("start*Clown"));
            // landmarks.dm:47-48: LAZYADDASSOCLIST(GLOB.jobspawn_overrides, name, src) for jobspawn_override only
            var overrides = Assert.IsType<DmList>(GLOB.jobspawn_overrides);
            Assert.Equal(1f, overrides.len);
            var assistants = Assert.IsType<DmList>(overrides[JOB_ASSISTANT]);
            Assert.Same(starts[2], Assert.Single(assistants.Items()));
            Assert.True(Dm.truthy(starts[2].jobspawn_override));
            Assert.False(Dm.truthy(starts[2].delete_after_roundstart)); // /assistant/override: delete_after_roundstart = FALSE
        }
    }

    [Fact]
    public void A_start_landmark_that_is_deleted_leaves_the_lists_and_the_override_registry()
    {
        World world = T2Maps.Boot("landmarks");
        using (world.Enter())
        {
            var starts = GLOB.start_landmarks_list.Items().Cast<obj_effect_landmark_start>().ToList();
            var janitor = starts[0];
            var assistant = starts[1];
            var over = starts[2];
            // after_round_start (landmarks.dm:37-42): qdel(src) when delete_after_roundstart (production build)
            assistant.after_round_start();
            Assert.True(QDELETED(assistant));
            Assert.DoesNotContain(assistant, GLOB.start_landmarks_list.Items());
            Assert.DoesNotContain(assistant, GLOB.landmarks_list.Items());
            over.after_round_start(); // the override keeps itself
            Assert.False(QDELETED(over));
            // landmarks.dm:52-60: Destroy removes an override from jobspawn_overrides and drops the empty registry
            qdel(over);
            Assert.DoesNotContain(over, GLOB.start_landmarks_list.Items());
            Assert.Null(GLOB.jobspawn_overrides);
            Assert.False(QDELETED(janitor));
        }
    }

    [Fact]
    public void Late_join_landmarks_register_the_turf_and_delete_themselves()
    {
        World world = T2Maps.Boot("landmarks");
        using (world.Enter())
        {
            // landmarks.dm:345-348: SSjob.latejoin_trackers += loc (the turf), INITIALIZE_HINT_QDEL
            Assert.Equal(new[] { locate(2, 2, 1), locate(3, 1, 1) }, SSjob.latejoin_trackers.Items().Cast<turf>().ToArray());
            Assert.Empty(locate(2, 2, 1).contents.Items());
            var left = locate(3, 1, 1).contents.Items().Cast<atom>().ToList();
            Assert.IsType<obj_effect_landmark_start_assistant>(Assert.Single(left));
        }
        // DV26 is for maps without any late-join landmark: this map has some, so the host spawns players on them
        Assert.Equal(new[] { new SpawnPoint(2, 2, 1), new SpawnPoint(3, 1, 1) }, MapSeam.LatejoinSpawns(world));
    }

    [Fact]
    public void Without_late_join_landmarks_the_spawns_are_the_assistant_starts_turfs_in_list_order()
    {
        World world = T2Maps.Boot("nolatejoin", out TgTestWorld.Log log);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            Assert.True(SSjob.latejoin_trackers == null || SSjob.latejoin_trackers.len == 0);
            // columns first: x=1: (1,2) Assistant, (1,1) Janitor; x=2: (2,3); x=3: (3,1)
            var starts = GLOB.start_landmarks_list.Items().Cast<obj_effect_landmark_start>().ToList();
            Assert.Equal(new[] { "Assistant", "Janitor", "Assistant", "Assistant" }, starts.Select(s => s.name).ToArray());
        }
        // DEVIATION(DV26): tg would use the arrivals shuttle, then the CentCom error room; neither exists (D108)
        Assert.Equal(new[] { new SpawnPoint(1, 2, 1), new SpawnPoint(2, 3, 1), new SpawnPoint(3, 1, 1) }, MapSeam.LatejoinSpawns(world));
    }

    [Fact]
    public void A_map_with_neither_late_join_nor_assistant_landmarks_has_no_spawns()
    {
        World world = T2Maps.Boot("areas");
        Assert.Empty(MapSeam.LatejoinSpawns(world));
        World none = TgTestWorld.Boot(seed: 1);
        Assert.Empty(MapSeam.LatejoinSpawns(none)); // a world without a selected map
    }
}
