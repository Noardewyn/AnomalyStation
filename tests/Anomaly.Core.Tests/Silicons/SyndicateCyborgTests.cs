using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Silicons;

/// <summary>
/// Silicons SI9 (D326): the syndicate cyborgs. An assault cyborg has the syndicate override laws, no AI, the LMG and the operative
/// pinpointer, and is out of the silicon faction; the medical and saboteur models carry tg's tools; the saboteur's chameleon projector
/// disguises it as a Nanotrasen engineering cyborg and a hit breaks the disguise; the cyborg pinpointer finds the nearest operative.
/// </summary>
public class SyndicateCyborgTests
{
    private const int Second = 20;

    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        r.In(() => (get_area(locate(5, 5, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        return (r, bob);
    }

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(mob_living_silicon_robot_model_syndicate), typeof(mob_living_silicon_robot_model_syndicate_medical), typeof(mob_living_silicon_robot_model_syndicate_saboteur),
            typeof(obj_item_borg_chameleon), typeof(obj_item_pinpointer_syndicate_cyborg), typeof(obj_item_gun_energy_printer), typeof(obj_item_antag_spawner_nuke_ops_borg_tele),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void AnAssaultCyborgServesTheSyndicate()
    {
        var (r, _) = Room(9951);
        var borg = r.In(() => Dm.@new<mob_living_silicon_robot_model_syndicate>(locate(5, 5, 1)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.IsType<datum_ai_laws_syndicate_override>(borg.laws);
            Assert.Null(borg.connected_ai);
            Assert.IsType<obj_item_robot_model_syndicate>(borg.model);
            var modules = Dm.each(borg.model.modules).ToList();
            Assert.Contains(modules, m => m is obj_item_gun_energy_printer);
            Assert.Contains(modules, m => m is obj_item_pinpointer_syndicate_cyborg);
            Assert.DoesNotContain(FACTION_SILICON, Dm.each(borg.faction).Select(Dm.str));
            Assert.IsType<obj_item_radio_borg_syndicate>(borg.radio);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSaboteursChameleonDisguisesItUntilAHit()
    {
        var (r, bob) = Room(9952);
        var borg = r.In(() => Dm.@new<mob_living_silicon_robot_model_syndicate_saboteur>(locate(4, 5, 1)));
        r.Step(Second);
        var chameleon = r.In(() => Dm.each(borg.model.modules).OfType<obj_item_borg_chameleon>().Single());
        string realName = r.In(() => borg.name);
        r.In(() =>
        {
            chameleon.activate(borg);
            Assert.Equal(chameleon.friendlyName, borg.name);
            Assert.Equal("engineer", borg.model.cyborg_base_icon);
            Assert.Equal("robot", borg.bubble_icon);
        });
        // a hit (attack_hand's signal) breaks the camouflage
        r.In(() =>
        {
            World.Current.usr = bob;
            Waiting.NoSleep(borg.attack_hand(bob, Dm.list()));
        });
        Assert.Equal(realName, r.In(() => borg.name));
        Assert.Equal(FALSE, r.In(() => chameleon.active));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCyborgPinpointerFindsTheNearestOperative()
    {
        var (r, bob) = Room(9953);
        r.In(() =>
        {
            // the toy room has no nukie base: it stands in loaded, with no reservation (as SyndicateN2cTests)
            SSmapping.loaded_lazy_templates ??= Dm.list();
            SSmapping.loaded_lazy_templates[LAZY_TEMPLATE_KEY_NUKIEBASE] = TRUE;
            var none = Dm.list();
            none.Add(new object[] { null });
            ((datum_lazy_template)GLOB.lazy_templates[LAZY_TEMPLATE_KEY_NUKIEBASE]).reservations = none;
            var op = Dm.@new<datum_antagonist_nukeop>();
            op.send_to_spawnpoint = FALSE;
            Waiting.NoSleep(bob.mind.add_antag_datum(op));
        });
        var borg = r.In(() => Dm.@new<mob_living_silicon_robot_model_syndicate>(locate(8, 5, 1)));
        r.Step(Second);
        var pinpointer = r.In(() => Dm.each(borg.model.modules).OfType<obj_item_pinpointer_syndicate_cyborg>().Single());
        r.In(() => pinpointer.scan_for_target());
        Assert.Same(bob, r.In(() => pinpointer.target));
        r.AssertNoRuntimes();
    }
}
