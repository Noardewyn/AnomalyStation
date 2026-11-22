// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/projectiles.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// The whole file: /datum/unit_test/projectile_movetypes (the M7 projectiles slice) and gun_go_bang (:10, M7x slice C: it shoots the
// Makarov pistol).

///Tests all projectiles that none of them are phasing.
// tg: code/modules/unit_tests/projectiles.dm:2 (/datum/unit_test/projectile_movetypes; class named by rules 2.3)
public sealed class datum_unit_test_projectile_movetypes : TgUnitTest
{
    // tg: code/modules/unit_tests/projectiles.dm:4
    public override async DmTask Run()
    {
        foreach (Type projectile in Dm.each_cast<Type>(typesof(typeof(obj_projectile))))
        {
            if (Dm.truthy(Dm.bitand(Dm.initial(projectile, (obj_projectile p) => p.movement_type), PHASING)))
                TEST_FAIL(Dm.text("[] has default movement type PHASING. Piercing projectiles should be done using the projectile piercing system, not movement_types!", projectile));
        }
    }
}

///Shoots a victim with a gun to ensure the gun properly loads and the victim take the correct amount of damage.
// tg: code/modules/unit_tests/projectiles.dm:10 (/datum/unit_test/gun_go_bang; M7x slice C, the Makarov pistol)
public sealed class datum_unit_test_gun_go_bang : TgUnitTest
{
    private const string INNATE_TRAIT = "innate";

    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
    }

    // tg: code/modules/unit_tests/projectiles.dm:12
    public override async DmTask Run()
    {
        // test is for a ballistic gun that starts loaded + chambered
        obj_item_gun_ballistic test_gun = allocate<obj_item_gun_ballistic_automatic_pistol>();
        mob_living_carbon_human victim = allocate<mob_living_carbon_human_consistent>();
        mob_living_carbon_human gunner = allocate<mob_living_carbon_human_consistent>();
        ADD_TRAIT(victim, TRAIT_PIERCEIMMUNE, INNATE_TRAIT); // So the human isn't randomly affected by shrapnel
        test_gun.can_misfire = FALSE; //just in case

        obj_item_ammo_casing loaded_casing = test_gun.chambered;
        TEST_ASSERT(loaded_casing != null, "Gun started without round chambered, should be loaded");
        obj_projectile loaded_bullet = loaded_casing.loaded_projectile;
        TEST_ASSERT(loaded_bullet != null, "Ammo casing has no loaded bullet");

        await gunner.put_in_hands(test_gun, forced: TRUE);
        gunner.set_combat_mode(FALSE); // just to make sure we know we're not trying to pistol-whip them
        float expected_damage = Dm.num(loaded_bullet.damage);
        loaded_bullet.def_zone = BODY_ZONE_CHEST;
        object did_we_shoot = await test_gun.melee_attack_chain(gunner, victim);
        TEST_ASSERT(Dm.truthy(did_we_shoot), "Gun does not appeared to have successfully fired.");
        TEST_ASSERT_EQUAL(victim.get_brute_loss(), expected_damage, Dm.text("Victim took incorrect amount of damage, expected [], got [].", expected_damage, victim.get_brute_loss()));

        obj_item_bodypart expected_part = victim.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart;
        TEST_ASSERT_EQUAL(expected_part.brute_dam, expected_damage, Dm.text("Intended bodypart took incorrect amount of damage, either it hit another bodypart or armor was incorrectly applied. Expected [], got [].", expected_damage, expected_part.brute_dam));
    }
}
