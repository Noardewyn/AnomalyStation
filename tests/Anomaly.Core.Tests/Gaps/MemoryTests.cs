using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package G4: the memories (code/datums/memory, __HELPERS/memory_helpers.dm) and the memory panel (mob.dm:1562). A slip on a lubed
/// floor, a burning, a death seen and lived through and the captain's spare code each leave tg's memory with tg's name built from the
/// characters' jobs; a death reaches only the minds in range; a story from a fixed seed reads as tg builds it; the HUD's memories button
/// opens tg's "MemoryPanel" window with each memory's name and quality.
/// </summary>
public class MemoryTests(ITestOutputHelper output)
{
    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(24, 9, seed: seed);
        r.In(() => TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root)); // memories.json
        mob_living_carbon_human bob = Crewman(r, 4, 4, "Bob", typeof(datum_job_assistant));
        return (r, bob);
    }

    private static mob_living_carbon_human Crewman(AudioRoom r, int x, int y, string key, Type job)
    {
        mob_living_carbon_human who = r.Join(x, y, key);
        r.In(() => who.mind.set_assigned_role(SSjob.get_job_type(job) as datum_job));
        return who;
    }

    private static datum_memory Memory(AudioRoom r, mob who, Type type) => r.In(() => who.mind.memories[type] as datum_memory);

    [Fact]
    public void ASlipABurnAndTheCaptainsCodeLeaveTheirMemoriesWithTgsNames()
    {
        var (r, bob) = Room(9801);

        // a lubed floor slips the walker; the memory names him by his job and the floor as tg's was_slipped does
        r.In(() => Waiting.NoSleep(((turf_open)locate(5, 4, 1)).MakeSlippery(TURF_WET_LUBE, 100)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.Move(locate(5, 4, 1), EAST)));
        r.Step(2);
        datum_memory slip = Memory(r, bob, typeof(datum_memory_was_slipped));
        Assert.NotNull(slip);
        Assert.Equal("The slipping of the assistant.", Dm.str(slip.name));
        Assert.Matches("^the (perilous )?(wet|lubed|slippery|cold) floor$", Dm.str(slip.antagonist_name));
        Assert.Equal((float)STORY_VALUE_MEH, slip.story_value);
        Assert.False(Dm.truthy(Dm.bitand(slip.memory_flags, MEMORY_FLAG_NOMOOD)), "the slipped human has a mood (gaps MS1): the memory keeps a mood verb (_memory.dm:114)");
        Assert.Equal(r.In(() => Dm.str(((area)get_area(bob)).name)), Dm.str(slip.where));

        // a burning human remembers the fire
        r.In(() =>
        {
            Waiting.NoSleep(bob.adjust_fire_stacks(5));
            bob.ignite_mob();
        });
        r.Step(2 * Second);
        Assert.Equal("The burning of the assistant.", r.In(() => Dm.str(Memory(r, bob, typeof(datum_memory_was_burning))?.name)));
        r.In(() => bob.extinguish_mob());

        // a full captain gets the spare ID safe's code as a key memory (and a biscuit card with the code, gaps S3); an acting one does not
        mob_living_carbon_human cap = Crewman(r, 4, 6, "Cap", typeof(datum_job_captain));
        r.In(() =>
        {
            SSid_access.spare_id_safe_code = "40213";
            Waiting.NoSleep(SSjob.promote_to_captain(cap, FALSE));
            Waiting.NoSleep(SSjob.promote_to_captain(bob, TRUE));
        });
        datum_memory code = Memory(r, cap, typeof(datum_memory_key_captains_spare_code));
        Assert.Equal("The code to the golden safe on the bridge, 40213.", Dm.str(code.name));
        Assert.Equal((float)STORY_VALUE_KEY, code.story_value);
        Assert.Null(Memory(r, bob, typeof(datum_memory_key_captains_spare_code)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADeathIsRememberedByTheViewersInRangeAndDesensitizesTheDeadAndTheWitness()
    {
        var (r, bob) = Room(9802);
        mob_living_carbon_human alice = Crewman(r, 6, 4, "Alice", typeof(datum_job_chief_medical_officer));
        mob_living_carbon_human carol = Crewman(r, 20, 4, "Carol", typeof(datum_job_assistant)); // 16 tiles away: out of range
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.death()));
        r.Step(2);

        Assert.Equal("The death of the assistant.", Dm.str(Memory(r, alice, typeof(datum_memory_witnessed_death)).name));
        Assert.Equal("The death of the assistant.", Dm.str(Memory(r, bob, typeof(datum_memory_witnessed_death)).name)); // he saw it too, while alive
        Assert.Null(Memory(r, carol, typeof(datum_memory_witnessed_death)));
        r.In(() =>
        {
            // tg's desensitization: one human death seen (Alice), one lived through (Bob: experienced_death); Carol saw nothing
            Assert.Equal((1f, 1 - DESENSITIZED_REDUCTION_PER_DEATH), (alice.mind.deaths_witnessed, alice.mind.desensitized_level));
            Assert.Equal(1f, bob.mind.deaths_witnessed);
            Assert.Equal(0f, carol.mind.deaths_witnessed);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AStoryFromAFixedSeedReadsAsTgBuildsIt()
    {
        var (r, bob) = Room(9803);
        // the mood verb is picked when the memory is made (_memory.dm:134): pinned too, so the story does not lean on the stream the
        // room's boot left (gaps CI1's and BR1's subsystems moved it)
        r.In(() =>
        {
            r.World.Rng.Reseed(78, 1);
            bob.add_mob_memory(typeof(datum_memory_was_slipped), Dm.named("antagonist", "a banana peel"));
        });
        datum_memory slip = Memory(r, bob, typeof(datum_memory_was_slipped));
        string story = r.In(() =>
        {
            r.World.Rng.Reseed(78, 1);
            return Dm.str(slip.generate_story(STORY_ENGRAVING, NONE));
        });
        output.WriteLine(story);
        // tg's pieces: a foreword of memories.json, a start of the memory with its names, the place, and since the moods (gaps MS1) the
        // protagonist's mood phrase for his neutral mood (_memory.dm:298, the slip's neutral mood phrases) capitalized after the full stop;
        // tg's loop leaves out a piece equal to the last one; the end is trimmed
        Assert.Equal(story, r.In(() =>
        {
            r.World.Rng.Reseed(78, 1);
            return Dm.str(slip.generate_story(STORY_ENGRAVING, NONE));
        }));
        Assert.Equal("In the engraving you can see the tale of the assistant not being able to keep standing when faced with a banana peel while in "
            + "Test Room. The assistant is uninterested while on the ground.", story);
        // the quick copy keeps the name and value but cannot make a story
        r.In(() =>
        {
            datum_mind other = Dm.@new<datum_mind>("Other");
            bob.mind.quick_copy_all_memories(other);
            var copy = (datum_memory)other.memories[typeof(datum_memory_was_slipped)];
            Assert.IsType<datum_memory_copy>(copy);
            Assert.Equal((Dm.str(slip.name), slip.story_value), (Dm.str(copy.name), copy.story_value));
            Assert.True(Dm.truthy(Dm.bitand(copy.memory_flags, MEMORY_NO_STORY)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMemoriesButtonOpensThePanelWithEachMemorysNameAndQuality()
    {
        var (r, bob) = Room(9804);
        r.In(() =>
        {
            bob.add_mob_memory(typeof(datum_memory_was_slipped), Dm.named("antagonist", "a banana peel"));
            bob.add_mob_memory(typeof(datum_memory_key_message_server_key), Dm.named("decrypt_key", "frog42"));
        });
        Take(bob);
        atom button = r.In(() => bob.hud_used.screen_objects[HUD_MOB_MEMORIES] as atom);
        Assert.IsType<atom_movable_screen_memories>(button);
        Assert.Equal(ui_human_memories_menu, r.In(() => Dm.str(((atom_movable_screen)button).screen_loc)));

        Click(r, bob, button);
        JsonElement payload = LastUpdate(Take(bob), out _);
        Assert.Equal("MemoryPanel", InterfaceOf(payload));
        JsonElement[] memories = payload.GetProperty("data").GetProperty("memories").EnumerateArray().ToArray();
        Assert.Equal(new[] { ("The slipping of the assistant.", 2), ("The daily message server key is frog42. Keep it a secret from the clown.", -1) },
            memories.Select(m => (m.GetProperty("name").GetString(), m.GetProperty("quality").GetInt32())).ToArray());
        Assert.NotNull(r.In(() => bob.mind.memory_panel));
        r.AssertNoRuntimes();
    }
}
