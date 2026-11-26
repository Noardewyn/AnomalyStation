using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7x;

/// <summary>
/// DV591: a melee hit, armed or unarmed, lands on the attacker's selected zone every time; tg's 80% zone roll is not made.
/// </summary>
public class AimedMeleeTests
{
    private const int Hits = 30;

    [Fact]
    public void AWeaponAndAPunchAlwaysHitTheSelectedLimb()
    {
        var r = new AudioRoom(9, 9, seed: 5910);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        mob_living_carbon_human target = r.Dummy(5, 4);
        var toolbox = r.In(() => Dm.@new<obj_item_storage_toolbox>(bob.loc));
        r.Step(2);
        r.In(() => bob.zone_selected = BODY_ZONE_L_LEG);

        for (int i = 0; i < Hits; i++)
        {
            r.In(() => Waiting.NoSleep(target.attacked_by(toolbox, bob)));
            AssertOnlyLeftLegHurt(r, target, mustHurt: true);
            r.In(() => Waiting.NoSleep(target.fully_heal(HEAL_ALL)));
            r.In(() => Waiting.NoSleep(((datum_species)target.dna.species).harm(bob, target)));
            AssertOnlyLeftLegHurt(r, target);
            r.In(() => Waiting.NoSleep(target.fully_heal(HEAL_ALL)));
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMechsMeleeHitsTheAimedLimb()
    {
        var r = new AudioRoom(9, 9, seed: 5911);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        mob_living_carbon_human target = r.Dummy(5, 4);
        var mech = r.In(() => Dm.@new<obj_vehicle_sealed_mecha_ripley>(locate(3, 4, 1)));
        r.Step(2);
        r.In(() => { bob.zone_selected = BODY_ZONE_L_LEG; bob.combat_mode = TRUE; });

        for (int i = 0; i < Hits; i++)
        {
            r.In(() => Waiting.NoSleep(target.mech_melee_attack(mech, bob)));
            AssertOnlyLeftLegHurt(r, target, mustHurt: true);
            r.In(() => Waiting.NoSleep(target.fully_heal(HEAL_ALL)));
        }
        r.AssertNoRuntimes();
    }

    // DV592: a blow aimed at a lost limb does not start
    [Fact]
    public void ABlowAimedAtALostArmDoesNotLand()
    {
        var r = new AudioRoom(9, 9, seed: 5912);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        mob_living_carbon_human target = r.Dummy(5, 4);
        var toolbox = r.In(() => Dm.@new<obj_item_storage_toolbox>(bob.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(((obj_item_bodypart)target.get_bodypart(BODY_ZONE_L_ARM)).drop_limb()));
        r.In(() => { Waiting.NoSleep(target.fully_heal(HEAL_DAMAGE)); bob.zone_selected = BODY_ZONE_L_ARM; });

        for (int i = 0; i < 10; i++)
        {
            r.In(() => Waiting.NoSleep(toolbox.attack__proc(target, bob)));
            r.In(() => Waiting.NoSleep(((datum_species)target.dna.species).harm(bob, target)));
        }
        r.In(() => Assert.Equal(0f, Dm.num(target.get_brute_loss())));
        r.In(() => Assert.Null(target.get_bodypart(BODY_ZONE_L_ARM)));
        r.AssertNoRuntimes();
    }

    private static void AssertOnlyLeftLegHurt(AudioRoom r, mob_living_carbon_human target, bool mustHurt = false) => r.In(() =>
    {
        var hurt = Dm.each_cast<obj_item_bodypart>(target.get_bodyparts())
            .Where(bp => bp.brute_dam > 0)
            .Select(bp => bp.body_zone as string)
            .ToList();
        Assert.True(hurt.All(z => z == BODY_ZONE_L_LEG), string.Join(",", hurt));
        if (mustHurt)
            Assert.Contains(BODY_ZONE_L_LEG, hurt);
    });
}
