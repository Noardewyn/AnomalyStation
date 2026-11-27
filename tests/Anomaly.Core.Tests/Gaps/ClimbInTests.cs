using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The radial's Climb in (DV639, DV640) and the MOD drill's dig (DV354) in a booted toy room with Bob as a player. Each live receiver
/// of tg's drag of a player's own mob onto it is offered when that drag would take him (a DNA scanner, a disposal bin, a suit storage
/// unit, the hypnochair and the implant chair, the photocopier and a morgue tray, an open closet) and not when it would not (a closed
/// machine, a cluttered unit, a buckled or cuffed player, a full closet, a crate over a standing man, a machine whose receiver is not
/// ported); the choice puts him in through tg's own drag (or, for a closet, tg's step and close). The drill no longer digs the rock its
/// wearer walks into; it digs on its click while the button is held, and the sphere's excavation still digs on the bump.
/// </summary>
public class ClimbInTests
{
    private const string Drag = "left=1;button=left;drag=left";

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        return (r, feed, bob);
    }

    private static List<(RadialExtra Kind, string Label)> Offered(AudioRoom r, mob who, atom target) => r.In(() => RadialExtras.Of(who, target));

    private static bool OffersClimbIn(AudioRoom r, mob who, atom target)
        => Offered(r, who, target).Any(e => e.Kind is RadialExtra.Enter or RadialExtra.StepIn);

    // the radial's Climb in or Climb on as the client sends it: tg's drag of his own mob onto the thing (ContextHintAction.DropSelf)
    private static void DropSelf(AudioRoom r, mob_living_carbon_human who, atom target, int seconds)
    {
        r.Command(() => ClientSeam.Drop(who.client, who, target, Drag, Drag, 5));
        r.Step(seconds * Second);
    }

    private static T Open<T>(AudioRoom r, int x, int y) where T : obj_machinery
    {
        T machine = Place<T>(r, x, y);
        r.In(() => Waiting.NoSleep(machine.open_machine()));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(machine.state_open), typeof(T).Name + " did not open"));
        return machine;
    }

    [Fact]
    public void Bob_climbs_into_an_open_dna_scanner_and_a_closed_one_is_not_offered()
    {
        var (r, feed, bob) = Room(13701);
        var scanner = Open<obj_machinery_dna_scannernew>(r, 5, 4);
        Assert.Contains((RadialExtra.Enter, "Climb in"), Offered(r, bob, scanner));
        DropSelf(r, bob, scanner, 1);
        r.In(() =>
        {
            Assert.Same(scanner, bob.loc);
            Assert.Same(bob, scanner.occupant);
            Assert.False(Dm.truthy(scanner.state_open), "the scanner closed on him");
        });
        // Carl outside is offered nothing: the scanner is shut (dna_scanner.dm:98 close_machine needs it open)
        var carl = r.Join(5, 5, "Carl");
        Assert.False(OffersClimbIn(r, carl, scanner));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_buckled_or_cuffed_player_is_not_offered_the_scanner()
    {
        var (r, feed, bob) = Room(13702);
        var scanner = Open<obj_machinery_dna_scannernew>(r, 5, 4);
        var chair = Place<obj_structure_chair>(r, 4, 4);
        r.In(() => Waiting.NoSleep(chair.buckle_mob(bob)));
        r.Step(2);
        Assert.False(OffersClimbIn(r, bob, scanner), "buckled: close_machine leaves a buckled mob out");
        r.In(() => Waiting.NoSleep(chair.unbuckle_mob(bob)));
        r.Step(2);
        Assert.True(OffersClimbIn(r, bob, scanner));
        // handcuffed: tg's drag stops at can_perform_action (incapacitated), which the hover asks without its message
        var cuffs = r.In(() => Dm.@new<obj_item_restraints_handcuffs>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.equip_to_slot(cuffs, ITEM_SLOT_HANDCUFFED)));
        r.Step(2);
        r.In(() => Assert.Same(cuffs, bob.handcuffed));
        int said = feed.Chat.Count;
        Assert.False(OffersClimbIn(r, bob, scanner), "cuffed");
        Assert.Equal(said, feed.Chat.Count); // the hover is quiet
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Bob_climbs_into_a_disposal_bin()
    {
        var (r, feed, bob) = Room(13703);
        var bin = Place<obj_machinery_disposal_bin>(r, 5, 4);
        Assert.Contains((RadialExtra.Enter, "Climb in"), Offered(r, bob, bin));
        DropSelf(r, bob, bin, 3); // stuff_mob_in's 2 s
        r.In(() => Assert.Same(bin, bob.loc));
        Assert.True(feed.Any("You climb into"), "tg's message");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Bob_squeezes_into_an_empty_suit_storage_unit_and_a_cluttered_one_is_not_offered()
    {
        var (r, feed, bob) = Room(13704);
        var unit = Open<obj_machinery_suit_storage_unit>(r, 5, 4);
        var cluttered = Open<obj_machinery_suit_storage_unit>(r, 4, 5);
        r.In(() => cluttered.storage = Dm.@new<obj_item_tank_internals_oxygen>(cluttered));
        Assert.False(OffersClimbIn(r, bob, cluttered), "too cluttered inside");
        Assert.Contains((RadialExtra.Enter, "Climb in"), Offered(r, bob, unit));
        DropSelf(r, bob, unit, 4); // 3 s
        r.In(() =>
        {
            Assert.Same(unit, bob.loc);
            Assert.Same(bob, unit.occupant);
            Assert.False(Dm.truthy(unit.state_open));
        });
        Assert.True(feed.Any("cramped space"), "tg's message");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Bob_sits_in_the_hypnochair_and_the_implant_chair()
    {
        var (r, feed, bob) = Room(13705);
        var hypno = Open<obj_machinery_hypnochair>(r, 5, 4);
        var implanter = Open<obj_machinery_implantchair>(r, 3, 4);
        Assert.Contains((RadialExtra.Enter, "Climb in"), Offered(r, bob, hypno));
        Assert.Contains((RadialExtra.Enter, "Climb in"), Offered(r, bob, implanter));
        DropSelf(r, bob, hypno, 1);
        r.In(() => Assert.Same(hypno, bob.loc));
        var carl = r.Join(5, 5, "Carl");
        Assert.False(OffersClimbIn(r, carl, hypno), "taken");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Bob_climbs_onto_the_photocopier_and_a_morgue_tray()
    {
        var (r, feed, bob) = Room(13706);
        var copier = Place<obj_machinery_photocopier>(r, 5, 4);
        Assert.Contains((RadialExtra.Enter, "Climb on"), Offered(r, bob, copier));
        DropSelf(r, bob, copier, 3); // 2 s
        r.In(() =>
        {
            Assert.Same(copier.loc, bob.loc);
            Assert.Same(bob, copier.ass);
        });
        Assert.False(OffersClimbIn(r, bob, copier), "he is on it already");

        var (r2, feed2, carl) = Room(13707);
        var morgue = Place<obj_structure_bodycontainer_morgue>(r2, 5, 5);
        r2.In(() => Waiting.NoSleep(morgue.open()));
        r2.Step(2);
        var tray = r2.In(() => morgue.connected_value);
        Assert.NotNull(tray);
        // Carl beside the tray, on its far side from the morgue
        r2.In(() => Waiting.NoSleep(carl.forceMove(get_step(tray, get_dir(morgue, tray)))));
        r2.Step(2);
        Assert.Contains((RadialExtra.Enter, "Climb on"), Offered(r2, carl, tray));
        DropSelf(r2, carl, tray, 1);
        r2.In(() => Assert.Same(tray.loc, carl.loc));
        r.AssertNoRuntimes();
        r2.AssertNoRuntimes();
    }

    [Fact]
    public void Bob_steps_into_an_open_closet_and_shuts_it_behind_him()
    {
        var (r, feed, bob) = Room(13708);
        var closet = Place<obj_structure_closet>(r, 5, 4);
        Assert.False(OffersClimbIn(r, bob, closet), "a shut closet");
        Click(r, bob, closet); // the hand opens it
        r.Step(Second);
        r.In(() => Assert.True(Dm.truthy(closet.opened), "the click did not open the closet"));
        Assert.Contains((RadialExtra.StepIn, "Climb in"), Offered(r, bob, closet));
        r.Command(() => RadialExtras.Pick(bob.client, closet, "Climb in"));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Same(closet, bob.loc);
            Assert.False(Dm.truthy(closet.opened), "shut behind him");
        });
        // a full closet takes no one more (insertion_allowed's mob_storage_capacity)
        var full = Place<obj_structure_closet>(r, 3, 4);
        var carl = r.Join(3, 5, "Carl");
        r.In(() =>
        {
            Waiting.NoSleep(full.open(carl));
            full.mob_storage_capacity = 1;
            Waiting.NoSleep(Dm.@new<mob_living_carbon_human>(full).forceMove(full));
        });
        r.Step(2);
        Assert.False(OffersClimbIn(r, carl, full), "full");
        // a crate does not close over a standing man (horizontal: insertion_allowed and can_close)
        var crate = Place<obj_structure_closet_crate>(r, 2, 5);
        r.In(() => Waiting.NoSleep(crate.open(carl)));
        r.Step(2);
        Assert.False(OffersClimbIn(r, carl, crate), "a crate over a standing man");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_machine_whose_receiver_is_not_ported_and_a_cryo_cell_for_an_able_man_offer_nothing()
    {
        var (r, feed, bob) = Room(13709);
        // the sleeper is not ported (D216); its drag would be refused
        var sleeper = Place<obj_machinery_sleeper>(r, 5, 4);
        Assert.False(OffersClimbIn(r, bob, sleeper));
        // the cryo cell takes only an incapacitated carbon (cryo.dm:645), whom tg's drag lets act
        var cryo = Place<obj_machinery_cryo_cell>(r, 3, 4);
        r.In(() => Waiting.NoSleep(cryo.open_machine()));
        r.Step(2);
        Assert.False(OffersClimbIn(r, bob, cryo));
        r.AssertNoRuntimes();
    }

    // ---- the MOD drill (DV354) ----

    private static obj_item_mod_control_pre_equipped_mining MiningSuit(AudioRoom r, mob_living_carbon_human bob)
    {
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var mod = r.In(() => Dm.@new<obj_item_mod_control_pre_equipped_mining>(bob.loc));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(mod, mod.slot_flags)))));
        r.Step(2);
        r.Command(() => Waiting.Detach(mod.quick_deploy(bob)));
        r.Step(4);
        r.Command(() => Waiting.Detach(mod.toggle_activate(bob)));
        r.Step(12 * Second);
        r.In(() => Assert.True(Dm.truthy(mod.active), "the suit did not activate"));
        return mod;
    }

    private static T ModuleOf<T>(AudioRoom r, obj_item_mod_control mod) where T : obj_item_mod_module => r.In(() => Dm.locate<T>(mod.modules));

    private static turf Rock(AudioRoom r, int x, int y)
    {
        r.In(() => Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_closed_mineral))));
        r.Step(2);
        return r.In(() => locate(x, y, 1));
    }

    [Fact]
    public void The_drill_digs_the_rock_it_is_held_on_and_not_the_rock_its_wearer_walks_into()
    {
        var (r, feed, bob) = Room(13710);
        var mod = MiningSuit(r, bob);
        var drill = ModuleOf<obj_item_mod_module_drill>(r, mod);
        Rock(r, 5, 4);
        r.Command(() => Waiting.Detach(drill.on_select(bob)));
        r.Step(4);
        r.In(() => Assert.True(Dm.truthy(drill.active)));
        // walking into the rock digs nothing
        r.In(() => Waiting.NoSleep(bob.Move(locate(5, 4, 1), EAST)));
        r.Step(8 * Second);
        Assert.True(r.In(() => locate(5, 4, 1) is turf_closed_mineral), "the bump dug the rock");
        // the drill's click (tg's module use button, the middle one) held on the rock digs it
        turf rock = r.In(() => locate(5, 4, 1));
        r.Command(() =>
        {
            ClientSeam.MouseDown(bob.client, rock, "middle=1;button=middle;icon-x=16;icon-y=16");
            ClientSeam.Click(bob.client, rock, "middle=1;button=middle;icon-x=16;icon-y=16");
        });
        r.Step(4 * Second);
        r.Command(() => ClientSeam.MouseUp(bob.client, rock, "middle=1;button=middle;icon-x=16;icon-y=16"));
        r.Step(2);
        Assert.False(r.In(() => locate(5, 4, 1) is turf_closed_mineral), "the drill did not dig the rock it was held on");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_drills_click_without_the_button_held_digs_nothing()
    {
        var (r, feed, bob) = Room(13711);
        var mod = MiningSuit(r, bob);
        var drill = ModuleOf<obj_item_mod_module_drill>(r, mod);
        turf rock = Rock(r, 5, 4);
        r.Command(() => Waiting.Detach(drill.on_select(bob)));
        r.Step(4);
        r.Command(() => ClientSeam.Click(bob.client, rock, "middle=1;button=middle;icon-x=16;icon-y=16"));
        r.Step(4 * Second);
        Assert.True(r.In(() => locate(5, 4, 1) is turf_closed_mineral), "a click let go digs nothing (DV348)");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_sphere_excavates_the_rock_it_rolls_into()
    {
        var (r, feed, bob) = Room(13712);
        var mod = MiningSuit(r, bob);
        var drill = ModuleOf<obj_item_mod_module_drill>(r, mod);
        var sphere = ModuleOf<obj_item_mod_module_sphere_transform>(r, mod);
        Assert.NotNull(sphere);
        Rock(r, 5, 4);
        r.Command(() => Waiting.Detach(sphere.on_select(bob)));
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(sphere.active), "the sphere did not activate");
            Assert.True(Dm.truthy(drill.ballin));
            Assert.Equal(TOOL_MINING, drill.tool_behaviour);
        });
        r.In(() => Waiting.NoSleep(bob.Move(locate(5, 4, 1), EAST)));
        r.Step(4 * Second);
        Assert.False(r.In(() => locate(5, 4, 1) is turf_closed_mineral), "the sphere did not dig the rock it rolled into");
        // out of the sphere the bump digs nothing again
        r.Command(() => Waiting.Detach(sphere.on_select(bob)));
        r.Step(2 * Second);
        r.In(() => Assert.False(Dm.truthy(drill.ballin)));
        Rock(r, 3, 4);
        r.In(() => Waiting.NoSleep(bob.Move(locate(3, 4, 1), WEST)));
        r.Step(4 * Second);
        Assert.True(r.In(() => locate(3, 4, 1) is turf_closed_mineral), "the bump dug after the sphere");
        r.AssertNoRuntimes();
    }
}
