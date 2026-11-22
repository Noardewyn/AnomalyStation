using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5;

/// <summary>
/// tg's unit test <c>armor_verification</c> (code/modules/unit_tests/armor_verification.dm), ported (rules 15) with the
/// armour arithmetic of datums/armor/_armor.dm and _atom_armor.dm (M5 slice 1), plus armor_to_protection_class and _name.
/// </summary>
public class ArmorVerificationTests
{
    // tg: code/modules/unit_tests/armor_verification.dm:30
    private static float ArmorTotals(datum_armor armor)
    {
        float total = 0;
        foreach (object key in Dm.each(Dm.list(ACID, BIO, BOMB, BULLET, CONSUME, ENERGY, FIRE, LASER, MELEE, WOUND)))
            total += Dm.num(Dm.get_var(armor, Dm.str(key)));
        return total;
    }

    // tg: code/modules/unit_tests/armor_verification.dm:4
    [Fact]
    public void ArmorVerification()
    {
        var r = new AudioRoom(6, 6, seed: 5111);
        r.In(() =>
        {
            obj dummy = Dm.@new<obj>(locate(3, 3, 1));

            dummy.set_armor(typeof(datum_armor_none));
            datum_armor armor = dummy.get_armor();
            Assert.NotNull(armor); // "armor didn't populate correctly when needed"
            Assert.Equal(0f, ArmorTotals(armor)); // "none armor type had armor values"

            armor = armor.generate_new_with_specific(Dm.list(Dm.kv(FIRE, 20))) as datum_armor;
            Assert.Equal(20f, ArmorTotals(armor)); // "modified armor type had incorrect values"

            armor = armor.generate_new_with_specific(Dm.list(Dm.kv(ACID, 20))) as datum_armor;
            Assert.Equal(40f, ArmorTotals(armor)); // "modified armor type had incorrect values"

            armor = get_armor_by_type(typeof(datum_armor_immune)) as datum_armor;
            float totals = ArmorTotals(armor);
            armor = armor.generate_new_with_multipliers(Dm.list(Dm.kv(ARMOR_ALL, 0))) as datum_armor;
            Assert.Equal(totals, ArmorTotals(armor)); // "modified an immune armor type"

            float wanted = 40;
            dummy.set_armor(typeof(datum_armor_none));
            dummy.set_armor_rating(ENERGY, wanted * 0.5f);
            dummy.set_armor_rating(FIRE, wanted * 0.5f);
            Assert.Equal(wanted, ArmorTotals(dummy.get_armor())); // "modified armor type had incorrect values"
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheArmourArithmeticAddsSubtractsAndNamesRatings()
    {
        var r = new AudioRoom(6, 6, seed: 5112);
        r.In(() =>
        {
            var window = get_armor_by_type(typeof(datum_armor_structure_window)) as datum_armor;
            var sum = window.add_other_armor(typeof(datum_armor_structure_window)) as datum_armor;
            Assert.Equal(100f, Dm.num(sum.get_rating(MELEE)));
            var none = window.subtract_other_armor(window) as datum_armor;
            Assert.Equal(FALSE, Dm.num(none.has_any_armor()));
            Assert.Equal(TRUE, Dm.num(window.has_any_armor()));
            var halved = window.generate_new_with_multipliers(Dm.list(Dm.kv(MELEE, 0.5f))) as datum_armor;
            Assert.Equal(25f, Dm.num(halved.get_rating(MELEE)));
            Assert.Equal(0f, Dm.num(halved.get_rating(FIRE))); // tg sets only the multiplied ratings on the new datum (_armor.dm:107-110)
            var immune = get_armor_by_type(typeof(datum_armor_immune)) as datum_armor;
            Assert.Equal(100f, Dm.num(immune.get_rating(MELEE)));
            Assert.Same(immune, immune.add_other_armor(window));
            Assert.Equal("V", armor_to_protection_class(50));
            Assert.Equal("-III", armor_to_protection_class(-25)); // DM round(25, 10) is 30
            Assert.Equal("EXPLOSIVE", armor_to_protection_name(BOMB));
        });
        r.AssertNoRuntimes();
    }
}
