using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.M4bAudio;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The MetaStation leftovers: a hulk's punch (tg's attack_hulk and hulk_recoil). A wall dents or breaks by its hardness, a reinforced
/// wall hurts a recoiling hulk's arm, a person and a basic mob take 15 brute, a pacifist hulk holds back, an object takes the hulk's
/// damage and a machine hurts the punching arm, items and effects are not punched, a rock is punched through, and a real click of a
/// hulk in combat mode lands the punch.
/// </summary>
public class HulkPunchTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Hulk) Room(ulong seed, Type kind = null)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human hulk = r.Join(5, 4, "hulk"); // a client, to hear the sounds
        r.Step(Second);
        r.In(() => Waiting.NoSleep(hulk.dna.add_mutation(kind ?? typeof(datum_mutation_hulk), MUTATION_SOURCE_ACTIVATED)));
        r.Step(2);
        return (r, feed, hulk);
    }

    private static object Punch(AudioRoom r, mob_living_carbon_human hulk, atom target) =>
        r.In(() => Waiting.NoSleep(target.attack_hulk(hulk)));

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => h.File != null && h.File.Contains(file, StringComparison.Ordinal));

    private static float ArmBrute(AudioRoom r, mob_living_carbon_human who) =>
        r.In(() => (who.hand_bodyparts[who.active_hand_index] as obj_item_bodypart).brute_dam);

    [Fact]
    public void A_hulk_dents_a_wall_and_by_its_hardness_breaks_it_with_no_recoil()
    {
        var (r, feed, hulk) = Room(15311);
        turf wall = r.In(() => Waiting.NoSleep(locate(6, 4, 1).ChangeTurf(typeof(turf_closed_wall))) as turf);
        int punches = 0;
        while (r.In(() => locate(6, 4, 1) is turf_closed_wall) && punches < 40)
        {
            Assert.Equal(TRUE, Punch(r, hulk, r.In(() => locate(6, 4, 1))));
            punches++;
        }
        Assert.False(r.In(() => locate(6, 4, 1) is turf_closed_wall), "the wall still stands after 40 punches");
        Assert.True(Heard(r, "meteorimpact.ogg"));
        if (punches > 1)
        {
            Assert.True(Heard(r, "bang.ogg"));
            Assert.True(feed.Any("smash"));
        }
        Assert.Equal(0f, ArmBrute(r, hulk)); // the plain hulk does not recoil
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_wizardly_hulk_breaking_a_reinforced_wall_takes_its_recoil_on_the_arm()
    {
        var (r, _, hulk) = Room(15312, typeof(datum_mutation_hulk_wizardly));
        r.In(() => Waiting.NoSleep(locate(6, 4, 1).ChangeTurf(typeof(turf_closed_wall_r_wall))));
        int punches = 0;
        while (r.In(() => locate(6, 4, 1) is turf_closed_wall) && punches < 200)
        {
            Punch(r, hulk, r.In(() => locate(6, 4, 1)));
            punches++;
        }
        Assert.False(r.In(() => locate(6, 4, 1) is turf_closed_wall), "the reinforced wall still stands after 200 punches");
        Assert.Equal(41f, ArmBrute(r, hulk));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_hulk_punches_a_person_and_a_basic_mob_for_fifteen_brute_and_a_pacifist_hulk_holds_back()
    {
        var (r, feed, hulk) = Room(15313);
        mob_living_carbon_human bob = r.Dummy(6, 4);
        var mob = r.In(() => Dm.@new<mob_living_basic_mouse>(locate(4, 4, 1)));
        r.Step(2);
        Punch(r, hulk, bob);
        Assert.Equal(15f, r.In(() => Dm.num(bob.get_brute_loss())));
        Assert.True(feed.Any("You smash") || feed.Any("You pummel"), Chat(feed));
        float before = r.In(() => mob.health);
        Assert.Equal(TRUE, Punch(r, hulk, mob));
        Assert.True(r.In(() => mob.health < before || mob.stat == DEAD || mob.gc_destroyed != null));
        Assert.True(Heard(r, "punch"));

        r.In(() => ADD_TRAIT(hulk, TRAIT_PACIFISM, "test"));
        float bobBefore = r.In(() => Dm.num(bob.get_brute_loss()));
        Assert.Equal(FALSE, Punch(r, hulk, bob));
        Assert.Equal(bobBefore, r.In(() => Dm.num(bob.get_brute_loss())));
        Assert.True(feed.Any("You don't want to hurt"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_hulk_smashes_an_object_hurts_its_arm_on_a_machine_and_leaves_items_alone()
    {
        var (r, feed, hulk) = Room(15314);
        var table = r.In(() => Dm.@new<obj_structure_table>(locate(6, 4, 1)));
        float integrity = r.In(() => Dm.num(table.get_integrity()));
        Assert.Equal(TRUE, Punch(r, hulk, table));
        Assert.True(r.In(() => table.gc_destroyed != null || Dm.num(table.get_integrity()) < integrity));
        Assert.True(feed.Any("smash"), Chat(feed));

        var machine = r.In(() => Dm.@new<obj_machinery_vending_cola>(locate(4, 4, 1)));
        float deflection = r.In(() => machine.damage_deflection);
        Punch(r, hulk, machine);
        Assert.Equal(deflection * 0.1f, ArmBrute(r, hulk), 3);

        var item = r.In(() => Dm.@new<obj_item_toy_crayon_red>(locate(5, 5, 1)));
        Assert.Equal(FALSE, Punch(r, hulk, item));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_hulk_punches_through_rock_after_a_while()
    {
        var (r, _, hulk) = Room(15315);
        r.In(() => Waiting.NoSleep(locate(6, 4, 1).ChangeTurf(typeof(turf_closed_mineral))));
        Assert.Null(Punch(r, hulk, r.In(() => locate(6, 4, 1))));
        Assert.True(r.In(() => locate(6, 4, 1) is turf_closed_mineral));
        float end = r.In(() => world.time) + 400;
        while (r.In(() => locate(6, 4, 1) is turf_closed_mineral) && r.In(() => world.time) < end)
            r.Step();
        Assert.False(r.In(() => locate(6, 4, 1) is turf_closed_mineral), "the rock still stands");
        Assert.True(Heard(r, "meteorimpact.ogg"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_hulk_in_combat_mode_clicking_a_wall_lands_the_punch()
    {
        var r = new AudioRoom(11, 9, seed: 15316);
        Listen(r);
        mob_living_carbon_human hulk = r.Join(5, 4, "hulk");
        r.Step(Second);
        r.In(() =>
        {
            Waiting.NoSleep(hulk.dna.add_mutation(typeof(datum_mutation_hulk), MUTATION_SOURCE_ACTIVATED));
            hulk.set_combat_mode(TRUE);
        });
        r.In(() => Waiting.NoSleep(locate(6, 4, 1).ChangeTurf(typeof(turf_closed_wall))));
        r.Step(2);
        int clicks = 0;
        while (r.In(() => locate(6, 4, 1) is turf_closed_wall) && clicks < 40)
        {
            Click(r, hulk, r.In(() => locate(6, 4, 1)));
            r.Step(CLICK_CD_MELEE_TICKS);
            clicks++;
        }
        Assert.False(r.In(() => locate(6, 4, 1) is turf_closed_wall), "the clicked wall still stands");
        r.AssertNoRuntimes();
    }

    private const int CLICK_CD_MELEE_TICKS = 8;

    private static string Chat(Feed feed) => string.Join("\n", feed.Chat.Select(c => c.Html));
}
