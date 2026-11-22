using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M6;

/// <summary>
/// The blunt head trauma (item_attack.dm:442, the human's attack_effects on the head) in a running world: a blunt brute hit knocks
/// the victim senseless (brain damage, tg's confusion and eye blur), a sharp one does not; the eye blur puts tg's gaussian blur on
/// the hud's game plane master controller, weaker as it runs out, gone at its end; a confusion scrambles client/Move's direction
/// through COMSIG_MOB_CLIENT_PRE_MOVE, which the capsule's gate hands to the locomotion bridge; the concussion (mild.dm:93) is
/// gained once, its symptoms come from the carbon's Life, and it leaves its owner with the brain and comes back with it.
/// </summary>
public class HeadTraumaTests
{
    private const int Second = 20;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        r.In(() =>
        {
            for (int x = 2; x <= 11; x++)
            {
                for (int y = 2; y <= 8; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
        });
        r.Step(Second);
        return r;
    }

    private static obj_item_organ_brain Brain(mob_living_carbon who) => who.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;

    private static atom_movable_plane_master_controller GamePlane(mob who) => who.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;

    // The size of the game plane's "eye_blur" filter, or null without one.
    private static float? BlurSize(mob who)
    {
        DmList data = GamePlane(who).filter_data;
        if (data == null)
            return null;
        for (int i = 1; i <= data.Count; i++)
        {
            if (data[(float)i] is DmList f && Dm.eq(f["name"], "eye_blur"))
                return Dm.num(f["size"]);
        }
        return null;
    }

    [Fact]
    public void ABluntHeadHitKnocksTheVictimSenselessWithBrainDamageConfusionAndBlur()
    {
        var r = Room(6801);
        mob_living_carbon_human victim = r.Join(6, 5, "Victim");
        mob_living_carbon_human attacker = r.Dummy(5, 5);
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        r.Step(Second);
        r.In(() =>
        {
            var toolbox = Dm.@new<obj_item_storage_toolbox_mechanical>(attacker.loc);
            float before = Dm.num(Brain(victim).damage);
            // a hit of 100: tg's prob(damage_done) always knocks senseless
            Waiting.NoSleep(victim.attack_effects(100, BODY_ZONE_HEAD, 0, toolbox, attacker));
            Assert.Equal(before + 20f, Dm.num(Brain(victim).damage));
            Assert.Equal(20f * SECONDS, Dm.num(((datum_status_effect_confusion)victim.has_status_effect(typeof(datum_status_effect_confusion))).duration));
            Assert.Equal(20f * SECONDS, Dm.num(((datum_status_effect_eye_blur)victim.has_status_effect(typeof(datum_status_effect_eye_blur))).duration));
            // clamp(20 seconds left * 0.05, 0.6, 3): a gaussian blur of size 1 on the game plane
            Assert.Equal(1f, BlurSize(victim));
            Assert.IsType<dm_filter>(GamePlane(victim).get_filter("eye_blur"));
        });
        r.Step(2);
        Assert.Contains(chat.Html, h => h.Contains("You're knocked senseless!", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASharpOrLightHeadHitDoesNotConfuse()
    {
        var r = Room(6802);
        mob_living_carbon_human victim = r.Join(6, 5, "Victim");
        mob_living_carbon_human attacker = r.Dummy(5, 5);
        r.Step(Second);
        r.In(() =>
        {
            var knife = Dm.@new<obj_item_knife_kitchen>(attacker.loc);
            float before = Dm.num(Brain(victim).damage);
            Waiting.NoSleep(victim.attack_effects(100, BODY_ZONE_HEAD, 0, knife, attacker));
            Assert.Equal(before, Dm.num(Brain(victim).damage));
            Assert.Null(victim.has_status_effect(typeof(datum_status_effect_confusion)));
            // prob(0): the light hit's tenth goes to the brain, no status effects
            var toolbox = Dm.@new<obj_item_storage_toolbox_mechanical>(attacker.loc);
            Waiting.NoSleep(victim.attack_effects(0.0001f, BODY_ZONE_HEAD, 0, toolbox, attacker));
            Assert.Null(victim.has_status_effect(typeof(datum_status_effect_confusion)));
            Assert.Null(victim.has_status_effect(typeof(datum_status_effect_eye_blur)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEyeBlurFadesAsItRunsOutAndLeavesTheGamePlane()
    {
        var r = Room(6803);
        mob_living_carbon_human victim = r.Join(6, 5, "Victim");
        r.Step(Second);
        r.In(() => Waiting.NoSleep(victim.adjust_eye_blur(60 * SECONDS)));
        r.Step(1);
        Assert.Equal(3f, r.In(() => BlurSize(victim))); // 60 s * 0.05, the cap
        r.Step(10 * Second);
        float later = r.In(() => BlurSize(victim)) ?? 0f;
        Assert.InRange(later, 2.4f, 2.6f); // about 50 s left
        r.In(() => ADD_TRAIT(victim, TRAIT_SIGHT_BYPASS, "test"));
        Assert.Null(r.In(() => BlurSize(victim)));
        r.In(() => REMOVE_TRAIT(victim, TRAIT_SIGHT_BYPASS, "test"));
        Assert.NotNull(r.In(() => BlurSize(victim)));
        r.In(() => victim.remove_status_effect(typeof(datum_status_effect_eye_blur)));
        Assert.Null(r.In(() => BlurSize(victim)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AConfusionScramblesTheGatesDirectionAndAClearHeadDoesNot()
    {
        var r = Room(6804);
        mob_living_carbon_human player = r.Join(6, 5, "Player");
        r.Step(Second);
        var directions = new List<float>();
        r.In(() =>
        {
            for (int i = 0; i < 20; i++)
            {
                Assert.True(Dm.truthy(Waiting.NoSleep(player.client.capsule_move_gate(get_step(player, NORTH), (float)NORTH))));
                Assert.Equal((float)NORTH, Dm.num(player.client.dm_capsule_direct));
            }
            // past CONFUSION_FULL_THRESHOLD (40 s) every move picks one of the eight directions
            Waiting.NoSleep(player.adjust_confusion(60 * SECONDS));
            for (int i = 0; i < 40; i++)
            {
                Assert.True(Dm.truthy(Waiting.NoSleep(player.client.capsule_move_gate(get_step(player, NORTH), (float)NORTH))));
                directions.Add(Dm.num(player.client.dm_capsule_direct));
            }
        });
        Assert.All(directions, d => Assert.Contains(d, new float[] { NORTH, SOUTH, EAST, WEST, NORTHEAST, NORTHWEST, SOUTHEAST, SOUTHWEST }));
        Assert.True(directions.Distinct().Count() >= 4, "the scramble picks among the eight directions: " + string.Join(",", directions));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AConfusedPlayerWalkingEastStraysOffTheLineAndAClearHeadedOneDoesNot()
    {
        var room = new Anomaly.Core.Tests.Tg.M2Locomotion.P4.CapsuleRoom(seed: 6807);
        mob_living_carbon_human clear = room.Join(2, 4, "Clear");
        mob_living_carbon_human confused = room.Join(2, 10, "Confused");
        room.In(() => Waiting.NoSleep(confused.adjust_confusion(30 * SECONDS)));
        var clearRows = new HashSet<int>();
        var confusedRows = new HashSet<int>();
        for (int t = 0; t < 60; t++)
        {
            room.Frame(clear, 127, 0, 16384);
            room.Frame(confused, 127, 0, 16384);
            room.Step(1);
            clearRows.Add(room.Tile(clear).Y);
            confusedRows.Add(room.Tile(confused).Y);
        }
        Assert.Equal(new HashSet<int> { 4 }, clearRows);
        Assert.True(confusedRows.Count > 1, "the confusion's scramble moved the player off its row");
        Assert.True(room.Tile(clear).X > 4, "the clear-headed player walked east");
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheBridgeTurnsTheWishToTheScrambledDirection()
    {
        // north to east: a quarter turn clockwise; north to north-west: an eighth back
        MotionInputTurn(0, 127, NORTH, EAST, 127, 0);
        MotionInputTurn(0, 127, NORTH, NORTHWEST, -90, 90);
        MotionInputTurn(127, 0, EAST, SOUTH, 0, -127);
    }

    private static void MotionInputTurn(sbyte x, sbyte y, float from, float to, int wantX, int wantY)
    {
        var frame = new Anomaly.Core.Motion.MotionInput(1, x, y, 0, Anomaly.Core.Motion.MotionInputFlags.None);
        var turned = LocomotionSeam.TurnWish(frame, (byte)from, (byte)to);
        Assert.Equal((wantX, wantY), ((int)turned.MoveX, (int)turned.MoveY));
    }

    [Fact]
    public void TheConcussionIsGainedOnceLeavesWithTheBrainAndComesBack()
    {
        var r = Room(6805);
        mob_living_carbon_human victim = r.Join(6, 5, "Victim");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        r.Step(Second);
        datum_brain_trauma_mild_concussion concussion = r.In(() =>
            Waiting.NoSleep(victim.gain_trauma(typeof(datum_brain_trauma_mild_concussion))) as datum_brain_trauma_mild_concussion);
        Assert.NotNull(concussion);
        r.In(() =>
        {
            Assert.Same(victim, concussion.owner);
            Assert.Same(Brain(victim), concussion.brain);
            Assert.Same(concussion, victim.has_trauma_type(typeof(datum_brain_trauma_mild)));
            Assert.Null(Waiting.NoSleep(victim.gain_trauma(typeof(datum_brain_trauma_mild_concussion)))); // one of a kind
        });
        r.Step(2);
        Assert.Contains(chat.Html, h => h.Contains("Your head hurts!", StringComparison.Ordinal));

        r.In(() =>
        {
            Assert.Equal(1f, victim.cure_all_traumas(TRAUMA_RESILIENCE_BASIC));
            Assert.Null(victim.has_trauma_type(typeof(datum_brain_trauma)));
            Assert.Null(concussion.owner);
            Assert.Null(concussion.brain);
        });

        // A body without a player (a player's brain leaving takes its mind into the brain mob, M8): the trauma leaves with the
        // brain and comes back with it.
        mob_living_carbon_human dummy = r.Dummy(8, 5);
        r.Step(Second);
        datum_brain_trauma_mild_concussion carried = r.In(() =>
            Waiting.NoSleep(dummy.gain_trauma(typeof(datum_brain_trauma_mild_concussion))) as datum_brain_trauma_mild_concussion);
        obj_item_organ_brain brain = r.In(() => Brain(dummy));
        r.In(() => Waiting.NoSleep(brain.Remove(dummy, special: TRUE)));
        r.In(() =>
        {
            Assert.Null(carried.owner);
            Assert.Same(brain, carried.brain);
            Assert.Contains(carried, Dm.each_cast<datum_brain_trauma>(brain.traumas));
        });
        r.In(() => Waiting.NoSleep(brain.Insert(dummy, special: TRUE)));
        r.In(() => Assert.Same(dummy, carried.owner));
        r.Step(2);
        Assert.Contains(chat.Html, h => h.Contains("The pressure inside your head starts fading.", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheConcussionsSymptomsComeFromTheCarbonsLife()
    {
        var r = Room(6806);
        mob_living_carbon_human victim = r.Join(6, 5, "Victim");
        // the slurring a concussion can give reads tg's strings/slurring_drunk_text.json, which the test world's files do not hold
        string tg = Anomaly.Core.Tests.Tg.M3Maps.P2.MapFixtures.TgClone();
        Assert.NotNull(tg);
        r.In(() =>
        {
            GLOB.string_cache ??= Dm.list();
            GLOB.string_cache["slurring_drunk_text.json"] = json_decode(System.IO.File.ReadAllText(System.IO.Path.Combine(tg, "strings", "slurring_drunk_text.json")));
        });
        r.Step(Second);
        r.In(() => Waiting.NoSleep(victim.gain_trauma(typeof(datum_brain_trauma_mild_concussion))));
        // SPT_PROB(2.5) a second, 10 of rand(1,11)'s cases seen here (not the vomit): about 4.5 % per 2 s Life, so ten minutes miss once
        // in a million (1 - 0.955^300). A fixed seed keeps its luck: this one's first symptom comes at 264 s, others' at 0 to 62 s.
        bool symptom = false;
        for (int s = 0; s < 600 && !symptom; s++)
        {
            r.Step(Second);
            symptom = r.In(() => victim.has_status_effect(typeof(datum_status_effect_confusion)) != null
                || victim.has_status_effect(typeof(datum_status_effect_dizziness)) != null
                || victim.has_status_effect(typeof(datum_status_effect_speech_slurring_generic)) != null
                || Dm.truthy(victim.IsStun()) || Dm.truthy(victim.IsUnconscious()));
        }
        Assert.True(symptom, "the concussion's on_life gave no symptom in ten minutes");
        r.AssertNoRuntimes();
    }
}
