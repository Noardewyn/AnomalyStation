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
/// M6 slice 5, gibbing (mob/living/death.dm, carbon/death.dm, effects/spawners/gibspawner.dm, the gibs of
/// decals/cleanable/blood.dm) in a running world: a person gibbed with everything dropped leaves the gibs, the gibbing animation,
/// the head with its brain, the other limbs, the chest's organs and what they held, and becomes a ghost; gibbed with nothing
/// dropped they leave only the bodypartless gibs; a player's dropped brain keeps their mind in tg's brain mob; the gibs streak along tg's move_to loop and squelch underfoot; BYOND's
/// get_step_to goes around a wall.
/// </summary>
public class GibbingTests
{
    private const int Second = 20;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(16, 12, seed: seed);
        r.In(() =>
        {
            for (int x = 2; x <= 15; x++)
            {
                for (int y = 2; y <= 11; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
        });
        r.Step(Second);
        return r;
    }

    private static turf At(int x, int y) => locate(x, y, 1);

    private static List<object> Around(int x, int y, int dist) => Dm.each(range(dist, At(x, y))).ToList();

    [Fact]
    public void AGibWithEverythingDroppedLeavesGibsTheHeadWithItsBrainTheLimbsAndTheItemsAndAGhost()
    {
        var r = Room(6801);
        mob_living_carbon_human victim = r.Join(8, 6, "Victim");
        client player = victim.client;
        obj_item_wrench wrench = r.In(() =>
        {
            var made = Dm.@new<obj_item_wrench>(victim.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(victim.put_in_l_hand(made))));
            return made;
        });
        (obj_item_bodypart_head head, obj_item_organ_brain brain, obj_item_organ_heart heart) = r.In(() =>
            ((obj_item_bodypart_head)victim.get_bodypart(BODY_ZONE_HEAD), (obj_item_organ_brain)victim.get_organ_slot(ORGAN_SLOT_BRAIN),
             (obj_item_organ_heart)victim.get_organ_slot(ORGAN_SLOT_HEART)));

        string realName = r.In(() => Dm.str(victim.real_name));
        string line = r.In(() => DevCommands.Gib(victim, brain: true));
        Assert.Contains("gibbed", line, StringComparison.Ordinal);
        r.Step();
        r.In(() =>
        {
            Assert.True(QDELETED(victim));
            List<object> around = Around(8, 6, 1);
            Assert.Contains(around, a => a is obj_effect_temp_visual_gib_animation g && g.icon_state == "gibbed-h"); // the human's gib_anim
            Assert.Contains(Around(8, 6, 3), a => a is obj_effect_decal_cleanable_blood_gibs);
        });
        mob_dead_observer ghost = player.mob as mob_dead_observer;
        Assert.NotNull(ghost);
        r.In(() =>
        {
            // transfer_identity: the mind goes to tg's brain mob in the brain; the player stays the ghost (living Logout made
            // the mind inactive), who can return to the brain
            Assert.NotNull(brain.brainmob);
            Assert.Same(brain, brain.brainmob.loc);
            Assert.Same(ghost.mind, brain.brainmob.mind);
            Assert.Same(brain.brainmob, ghost.mind.current);
            Assert.Equal(realName, Dm.str(brain.brainmob.real_name));
        });
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.False(QDELETED(head));
            Assert.Same(head, brain.loc); // tg's gibbing_organ_transfer: the brain stays in the head
            Assert.IsAssignableFrom<turf>(head.loc);
            Assert.IsAssignableFrom<turf>(heart.loc); // a chest organ is spilled
            Assert.Null(heart.owner);
            Assert.IsAssignableFrom<turf>(wrench.loc); // DROP_ITEMS
            List<object> around = Around(8, 6, 6);
            Assert.Equal(5, around.Count(a => a is obj_item_bodypart)); // the head, the arms and the legs; the chest goes with the body
            Assert.DoesNotContain(around, a => a is obj_effect_temp_visual_gib_animation); // 1.5 s
            Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/blob/blobattack.ogg" && h.Volume > 0);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AGibWithNothingDroppedLeavesOnlyTheBodypartlessGibs()
    {
        var r = Room(6802);
        mob_living_carbon_human witness = r.Join(4, 6, "Witness");
        mob_living_carbon_human victim = r.Dummy(9, 6);
        obj_item_bodypart_head head = r.In(() => (obj_item_bodypart_head)victim.get_bodypart(BODY_ZONE_HEAD));
        r.In(() => Waiting.Detach(victim.gib()));
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(victim));
            Assert.True(QDELETED(head));
            List<object> around = Around(9, 6, 6);
            Assert.DoesNotContain(around, a => a is obj_item_bodypart || a is obj_item_organ);
            // /obj/effect/gibspawner/human/bodypartless: gibs, cores and a torso, no limbs
            Assert.Contains(around, a => a is obj_effect_decal_cleanable_blood_gibs_torso);
            Assert.DoesNotContain(around, a => a is obj_effect_decal_cleanable_blood_gibs_limb || a is obj_effect_decal_cleanable_blood_gibs_body);
        });
        Assert.Contains(r.Sounds.Of(witness, "sound/effects/blob/blobattack.ogg"), h => h.Volume > 0);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADecalOverSpaceDeletesItselfAndNewStillGivesIt()
    {
        // decal.dm:14-18: a decal over a groundless turf returns INITIALIZE_HINT_QDEL, and BYOND's new still gives the object (a
        // caller such as a human's spawn_dust sets its pixel offsets, human/death.dm:10-12); the human's remains are a decal too
        // (live since silicons SI1c) and go the same way
        var r = Room(6804);
        mob_living_carbon_human victim = r.Dummy(9, 6);
        r.In(() =>
        {
            Waiting.NoSleep(At(9, 6).ChangeTurf(typeof(turf_open_space)));
            var blood = Dm.@new<obj_effect_decal_cleanable_blood>(At(9, 6));
            Assert.NotNull(blood);
            Assert.True(QDELETED(blood));
            victim.spawn_dust();
            Assert.DoesNotContain(Around(9, 6, 0), a => a is obj_effect_decal_remains_human);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void GibsStreakAlongTheMoveToLoopLeavingBloodAndSquelchUnderfoot()
    {
        var r = Room(6803);
        mob_living_carbon_human walker = r.Join(4, 4, "Walker");
        // tg's streak rolls its range (0 steps of the loop in a sixth of the rolls): the next gibs until one rolls a loop, whatever the seed
        obj_effect_decal_cleanable_blood_gibs gib = null;
        for (int attempt = 0; attempt < 20 && !r.In(() => gib != null && gib.loc == At(10, 6)); attempt++)
        {
            if (gib != null)
                r.In(() => qdel(gib));
            gib = r.In(() =>
            {
                var made = Dm.@new<obj_effect_decal_cleanable_blood_gibs>(At(8, 6));
                // streak: one step at once, then tg's move_to loop toward the turf beyond, 2 ds a step
                Waiting.NoSleep(made.streak(Dm.list(EAST)));
                return made;
            });
            Assert.True(r.In(() => gib.loc == At(9, 6)), "the first step is at once");
            r.Step(Second);
        }
        r.In(() =>
        {
            Assert.Same(At(10, 6), gib.loc); // the loop's target: get_step(src, EAST) after the first step
            Assert.Contains(Dm.each(At(10, 6).contents), a => a is obj_effect_decal_cleanable_blood_splatter); // spread_movement_effects
        });

        // squish_sound: a person walking onto the gibs
        r.In(() => Waiting.NoSleep(walker.forceMove(At(11, 6))));
        r.Sounds.Heard.Clear();
        r.In(() => Waiting.NoSleep(walker.Move(At(10, 6), WEST)));
        Assert.Contains(r.Sounds.Of(walker, "sound/effects/footstep/gib_step.ogg"), h => h.Volume > 0);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void GetStepToGoesAroundAWall()
    {
        var r = Room(6804);
        r.In(() =>
        {
            Waiting.NoSleep(At(7, 6).ChangeTurf(typeof(turf_closed_wall), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            var thing = Dm.@new<obj_item_wrench>(At(6, 6));
            turf step = get_step_to(thing, At(8, 6));
            // BYOND's breadth-first path step (OpenDream's order: east, northeast, north, ...): the wall east is skipped
            Assert.Same(At(7, 7), step);
            Assert.Null(get_step_to(thing, At(6, 6))); // already there
            Assert.Same(At(7, 6 + 1), get_step_to(thing, At(9, 7), 1));
        });
        r.AssertNoRuntimes();
    }
}
